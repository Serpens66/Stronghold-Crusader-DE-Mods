using BepInEx.Logging;
using HarmonyLib;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace BugfixesAndQoL.VanillaPerformance
{
    // V13: Vanilla redraws the whole minimap (native pass 0xF5A40, ~40 % of a simulation tick on
    // large maps) on every tick. It is drawn at most every 100 ms instead, immediately on camera,
    // radar size or zoom changes and after a tick gap (loading). Audit and measurements:
    // baseline knowledge/RADAR_PASS_F5A40.md, VanillaOptimization.md.
    // The pass writes only the managed radar buffer and the radar click table (no simulation
    // state), so a mismatch between peers cannot desync; the setting is still host-controlled
    // because a lockstep match runs at the speed of its slowest peer. A skipped tick leaves an old image in
    // its pooled buffer (6-buffer ring); GameMap.processTestMap therefore keeps the previous
    // lastRadarMap for such buffers instead of uploading the old image.
    // Installed once from LibraryLoaded; detour and Harmony patch stay for the process lifetime,
    // the setting only switches the logical activation.
    internal static unsafe class MinimapRefreshThrottle
    {
        internal const int RadarPassRva = 0xF5A40;
        internal const int DisplacedLength = 6;
        internal const int IntervalMilliseconds = 100;
        // mov [rsp+0x10],rbx ; push rbp
        internal static readonly byte[] Prologue = { 0x48, 0x89, 0x5C, 0x24, 0x10, 0x55 };

        // Call site 0x86B9B passes rcx, rdx, r8, r9d and seven stack dwords; the result is unused.
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void RadarPassDelegate(ulong radar, ulong retData, ulong radarMap,
            int screenCentreX, int screenCentreY, int screenTilesWide, int screenTilesHigh,
            int radarMapWidth, int radarMapHeight, int radarZoom, int reserved);

        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.VanillaPerformance.MinimapThrottle");
        private static readonly DetourHandle<RadarPassDelegate> hook = new DetourHandle<RadarPassDelegate>();
        private static readonly RadarBufferFreshness freshness = new RadarBufferFreshness();
        private static RadarThrottlePolicy policy;
        private static HookTransaction transaction;
        private static ManualLogSource log;
        private static int installed, enabled, failed, skipLogged, restoreLogged;

        internal static void Install(ManualLogSource logger, CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref installed, 1) != 0) return;
            log = logger;
            try
            {
                if (context == null || !Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                    throw new InvalidOperationException("the minimap throttle is bound to the audited native DLL " +
                        Shared.DebugLogHelper.CurrentNativeSha256 + ".");
                if (context.Memory.Length < RadarPassRva + Prologue.Length ||
                    !context.Memory.Slice(RadarPassRva, Prologue.Length).SequenceEqual(Prologue))
                    throw new InvalidOperationException("the 0xF5A40 prologue differs from the audited six bytes.");
                policy = new RadarThrottlePolicy(IntervalMilliseconds * Stopwatch.Frequency / 1000,
                    Stopwatch.Frequency, redrawOnCameraChange: true);
                // The restore patch goes in first: without it a skipped tick would upload an old image.
                InstallRestorePatch();
                ulong target = unchecked((ulong)context.ModuleHandle.ToInt64()) + RadarPassRva;
                HookTransaction pending = null;
                try
                {
                    pending = new HookTransaction(context.Region,
                        SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                        new HookTransactionOptions { Backend = HookInstallPhase.Backend(), FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true });
                    pending.AddDetour(hook, HookTarget.FromAddress(target), RadarPass);
                    CommitResult result = pending.Commit();
                    if (!result.IsCompleteSuccess || !hook.Success)
                        throw new InvalidOperationException("the 0xF5A40 detour did not install completely: " + result);
                    ValidateInstalledHook(target);
                    transaction = pending;
                    pending = null;
                }
                finally { pending?.Dispose(); } // Only a failed, unpublished candidate is rolled back.
                Shared.DebugLogHelper.LogInfo(log, "VANILLA_PERF_MINIMAP_THROTTLE_INSTALLED: detour 0xF5A40 (Indirect, 6 bytes, method=reference-rva), " +
                    "restore GameMap.processTestMap; intervalMs=" + IntervalMilliseconds + ".");
            }
            catch (Exception exception) { FailOpen(exception); }
        }

        internal static void SetEnabled(bool value) =>
            Volatile.Write(ref enabled, value && Volatile.Read(ref failed) == 0 && transaction != null ? 1 : 0);

        private static void InstallRestorePatch()
        {
            MethodInfo target = AccessTools.Method(typeof(GameMap), nameof(GameMap.processTestMap),
                new[] { typeof(short[]), typeof(int), typeof(EngineInterface.PlayState), typeof(byte[]) });
            FieldInfo field = AccessTools.Field(typeof(GameMap), "lastRadarMap");
            if (target == null || !target.IsPublic || target.ReturnType != typeof(void) ||
                field == null || field.FieldType != typeof(byte[]) || field.IsStatic)
                throw new MissingMemberException("GameMap.processTestMap/lastRadarMap differ from the audit.");
            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(MinimapRefreshThrottle), nameof(BeforeProcessTestMap)),
                postfix: new HarmonyMethod(typeof(MinimapRefreshThrottle), nameof(AfterProcessTestMap)));
        }

        private static void ValidateInstalledHook(ulong expectedTarget)
        {
            var detour = hook.Hook as NativeDetour<RadarPassDelegate>;
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != expectedTarget ||
                detour.Scheme.ToString() != "Indirect" || detour.DisplacedByteCount != DisplacedLength ||
                detour.ChainDepth != 1 || detour.TrampolineAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.PointerSlot == IntPtr.Zero)
                throw new InvalidOperationException("the installed 0xF5A40 detour contract differs.");
            IntPtr entry = new IntPtr(unchecked((long)expectedTarget));
            if (Marshal.ReadByte(entry, 0) != 0xFF || Marshal.ReadByte(entry, 1) != 0x25 ||
                IntPtr.Add(entry, 6 + Marshal.ReadInt32(entry, 2)) != detour.PointerSlot ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("the installed 0xF5A40 pointer-slot patch differs.");
        }

        // Simulation thread, once per DLL_RunTick (inside EngineInterface.threadLock).
        private static void RadarPass(ulong radar, ulong retData, ulong radarMap, int screenCentreX, int screenCentreY,
            int screenTilesWide, int screenTilesHigh, int radarMapWidth, int radarMapHeight, int radarZoom, int reserved)
        {
            bool draw = true;
            try
            {
                if (Volatile.Read(ref enabled) != 0)
                    draw = policy.ShouldDraw(Stopwatch.GetTimestamp(), screenCentreX, screenCentreY,
                        screenTilesWide, screenTilesHigh, radarMapWidth, radarMapHeight, radarZoom);
                // Also while disabled: a buffer skipped before a setting change becomes fresh again.
                freshness.MarkBuffer(unchecked((long)radarMap), draw);
            }
            catch (Exception exception)
            {
                draw = true;
                FailOpen(exception);
            }
            if (draw)
            {
                hook.Original(radar, retData, radarMap, screenCentreX, screenCentreY, screenTilesWide,
                    screenTilesHigh, radarMapWidth, radarMapHeight, radarZoom, reserved);
                return;
            }
            if (skipLogged == 0)
            {
                skipLogged = 1;
                Shared.DebugLogHelper.LogInfo(log, "VANILLA_PERF_MINIMAP_THROTTLE_CONFIRMED: first minimap pass skipped after startup cleanup.");
            }
        }

        // Main thread (Director.Update). __state keeps the image chosen before this buffer.
        private static void BeforeProcessTestMap(byte[] ___lastRadarMap, out byte[] __state) => __state = ___lastRadarMap;

        private static void AfterProcessTestMap(byte[] radarMap, ref byte[] ___lastRadarMap, byte[] __state)
        {
            if (Volatile.Read(ref failed) != 0 || radarMap == null || radarMap.Length == 0) return;
            try
            {
                long address;
                fixed (byte* pointer = radarMap) address = (long)pointer;
                if (!freshness.IsStale(address)) return;
                ___lastRadarMap = __state;
                if (Interlocked.Exchange(ref restoreLogged, 1) == 0)
                    Shared.DebugLogHelper.LogInfo(log, "VANILLA_PERF_MINIMAP_RESTORE_CONFIRMED: an old minimap image was kept out of the upload.");
            }
            catch (Exception exception) { FailOpen(exception); }
        }

        // Logical deactivation only: the detour stays and draws on every tick from now on.
        private static void FailOpen(Exception exception)
        {
            Volatile.Write(ref enabled, 0);
            if (Interlocked.Exchange(ref failed, 1) == 0)
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/VanillaPerformance/MinimapRefreshThrottle.cs:159",
                    "VANILLA_PERF_MINIMAP_THROTTLE_DISABLED: Vanilla minimap retained; " + exception);
        }
    }
}
