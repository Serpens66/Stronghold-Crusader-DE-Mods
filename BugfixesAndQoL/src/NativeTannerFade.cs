using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed unsafe class NativeTannerFade
    {
        private const string AuditedSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int TannerUpdaterRva = 0xB10D0;
        private const int CurrentBuildingIdRva = 0x8F9BE4;
        private const int BuildingArrayRva = 0x64CCBB0;
        private const int BuildingStride = 0x32C;
        private const int ImageOffset = 0x78;
        private const int AlphaOffset = 0x11A;
        private const int AliveStateOffset = 0x12C;
        private const int BuildingTypeOffset = 0x12E;
        private const int OwnerOffset = 0x132;
        private const int GlobalIdOffset = 0x134;
        private const int OriginTileOffset = 0xE8;
        private const int PhaseOffset = 0x174;
        private const short TannerBuildingType = (short)eStructs.STRUCT_TANNERS_WORKSHOP;
        private static readonly byte[] EntryPrefix =
        {
            0x40, 0x53, 0x55, 0x41, 0x54, 0x41, 0x57
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void TannerUpdateDelegate();

        private readonly DetourHandle<TannerUpdateDelegate> handle =
            new DetourHandle<TannerUpdateDelegate>();
        private readonly NativeTannerFadeTracker tracker = new NativeTannerFadeTracker();
        private readonly object sync = new object();
        private readonly ManualLogSource log;
        private readonly ulong moduleBase;
        private HookTransaction transaction;
        private IDisposable mapUnloadSubscription;
        private IDisposable buildingDeleteSubscription;
        private int firstCallbackLogged;
        private int callbackErrorLogged;
        private int enabled;

        internal NativeTannerFade(ManualLogSource log, CrusaderLibraryLoadContext context,
            bool initiallyEnabled)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            enabled = initiallyEnabled ? 1 : 0;
            if (context == null || context.ModuleHandle == IntPtr.Zero)
                throw new ArgumentException("CrusaderDE native module is unavailable.", nameof(context));
            moduleBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            ValidateInstalledDll(context.ModuleHandle);
            ValidateEntry();

            HookTransaction candidate = null;
            IDisposable unload = null;
            IDisposable deleted = null;
            try
            {
                candidate = new HookTransaction(context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                candidate.AddDetour(handle,
                    HookTarget.FromAddress(moduleBase + TannerUpdaterRva),
                    (TannerUpdateDelegate)UpdateTanner);
                CommitResult result = candidate.Commit();
                NativeDetour<TannerUpdateDelegate> detour =
                    handle.Hook as NativeDetour<TannerUpdateDelegate>;
                ValidateDetour(result, detour);
                unload = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(_ => Clear());
                deleted = BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(args =>
                {
                    if (args.Phase == EventHookPhase.Post)
                        RemoveBuilding(args.BuildingId);
                });
                Write($"NATIVE_FIX_READY rva=0x{TannerUpdaterRva:X} scheme={detour.Scheme} span={detour.DisplacedByteCount} fade_updates={NativeTannerFadeState.FadeUpdates}");
                transaction = candidate;
                mapUnloadSubscription = unload;
                buildingDeleteSubscription = deleted;
                candidate = null;
                unload = null;
                deleted = null;
            }
            catch
            {
                // Only this unpublished initialization candidate may be rolled back.
                deleted?.Dispose();
                unload?.Dispose();
                candidate?.Dispose();
                throw;
            }
        }

        internal void SetEnabled(bool value) =>
            Volatile.Write(ref enabled, value ? 1 : 0);

        private void UpdateTanner()
        {
            int buildingId = 0;
            byte* record = null;
            NativeBuildingIdentity identity = default;
            bool eligible = false;
            bool restoredVanilla = false;
            int phaseBefore = -1;
            try
            {
                buildingId = *(int*)(moduleBase + CurrentBuildingIdRva);
                if (buildingId > 0 && buildingId < 4000)
                {
                    record = (byte*)(moduleBase + BuildingArrayRva +
                        (ulong)buildingId * BuildingStride);
                    eligible = *(short*)(record + BuildingTypeOffset) == TannerBuildingType &&
                        *(short*)(record + AliveStateOffset) != 0;
                    if (eligible)
                    {
                        identity = ReadIdentity(record);
                        phaseBefore = *(short*)(record + PhaseOffset);
                        uint currentAlpha = (ushort)*(short*)(record + AlphaOffset);
                        uint vanillaSeed;
                        lock (sync)
                            if (tracker.TryGetVanillaTransparency(buildingId, identity,
                                phaseBefore, currentAlpha, out vanillaSeed))
                            {
                                *(short*)(record + AlphaOffset) = (short)vanillaSeed;
                                restoredVanilla = true;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                eligible = false;
                LogCallbackError(ex);
            }

            // Vanilla always runs exactly once, with its own previous alpha restored.
            handle.Original();

            try
            {
                if (Volatile.Read(ref firstCallbackLogged) == 0 &&
                    Interlocked.Exchange(ref firstCallbackLogged, 1) == 0)
                    Write(record == null
                        ? $"NATIVE_UPDATER_CALLBACK_FIRST building={buildingId} record=unavailable eligible={eligible}"
                        : $"NATIVE_UPDATER_CALLBACK_FIRST building={buildingId} type={*(short*)(record + BuildingTypeOffset)} alive={*(short*)(record + AliveStateOffset)} phase={*(short*)(record + PhaseOffset)} image={*(int*)(record + ImageOffset)} vanilla_raw={(ushort)*(short*)(record + AlphaOffset)} eligible={eligible}");
                if (!eligible || record == null ||
                    *(short*)(record + BuildingTypeOffset) != TannerBuildingType ||
                    *(short*)(record + AliveStateOffset) == 0 ||
                    !ReadIdentity(record).Equals(identity))
                    return;

                // A disabled feature still restores Vanilla's previous alpha before
                // the call above, then drops its state without altering Vanilla output.
                if (Volatile.Read(ref enabled) == 0)
                {
                    RemoveBuilding(buildingId);
                    return;
                }

                int phase = *(short*)(record + PhaseOffset);
                int image = *(int*)(record + ImageOffset);
                uint vanillaRaw = (ushort)*(short*)(record + AlphaOffset);
                NativeFadeSample sample;
                lock (sync)
                    sample = tracker.Observe(buildingId, identity, phase, image, vanillaRaw);
                if (sample.Active && sample.EffectiveTransparency != vanillaRaw)
                    *(short*)(record + AlphaOffset) = (short)sample.EffectiveTransparency;
                else if (NativeTannerFadeTracker.ShouldBridgeExitFrame(restoredVanilla,
                    phaseBefore, phase, image, vanillaRaw, sample))
                    *(short*)(record + AlphaOffset) = 0;
            }
            catch (Exception ex)
            {
                LogCallbackError(ex);
            }
        }

        private static NativeBuildingIdentity ReadIdentity(byte* record) =>
            new NativeBuildingIdentity(*(uint*)(record + GlobalIdOffset),
                *(uint*)(record + OriginTileOffset), *(ushort*)(record + OwnerOffset));

        private void RemoveBuilding(int buildingId)
        {
            lock (sync)
                tracker.Remove(buildingId);
        }

        private void Clear()
        {
            lock (sync)
                tracker.Clear();
        }

        private void LogCallbackError(Exception ex)
        {
            if (Interlocked.Exchange(ref callbackErrorLogged, 1) == 0)
                log.LogError($"TANNER_NATIVE_FIX_CALLBACK_FAILED {ex}");
        }

        private void ValidateEntry()
        {
            var actual = new byte[EntryPrefix.Length];
            Marshal.Copy(unchecked((IntPtr)(long)(moduleBase + TannerUpdaterRva)),
                actual, 0, actual.Length);
            for (int i = 0; i < actual.Length; i++)
                if (actual[i] != EntryPrefix[i])
                    throw new InvalidOperationException(
                        "The installed tannery updater differs from the audited Vanilla bytes.");
        }

        private void ValidateDetour(CommitResult result, NativeDetour<TannerUpdateDelegate> detour)
        {
            if (!result.IsCompleteSuccess || !handle.Success || detour == null ||
                !detour.IsInstalled || detour.TargetAddress != moduleBase + TannerUpdaterRva ||
                detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero ||
                detour.TrampolineAddress == IntPtr.Zero || detour.ChainDepth != 1)
                throw new InvalidOperationException(
                    "The tannery updater NativeDetour was not installed with the audited target and chain.");
            string scheme = detour.Scheme.ToString();
            if (scheme != "Indirect" || detour.DisplacedByteCount != 7 ||
                detour.PointerSlot == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Unexpected tannery updater NativeDetour contract: scheme={scheme} span={detour.DisplacedByteCount}.");
            var patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)(moduleBase + TannerUpdaterRva)),
                patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25)
                throw new InvalidOperationException(
                    "The tannery updater detour entry is not an indirect jump.");
            int relativeSlot = BitConverter.ToInt32(patch, 2);
            long actualSlot = unchecked((long)(moduleBase + TannerUpdaterRva + 6)) + relativeSlot;
            if (actualSlot != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException(
                    "The tannery updater pointer slot differs from the installed patch.");
        }

        private static void ValidateInstalledDll(IntPtr handle)
        {
            string path = null;
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
                if (module.BaseAddress == handle)
                {
                    path = module.FileName;
                    break;
                }
            if (path == null)
                throw new InvalidOperationException("The loaded CrusaderDE.dll path could not be resolved.");
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!string.Equals(actual, AuditedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"The installed native DLL is not the audited build: {actual}.");
            }
        }

        private void Write(string message) =>
            log.LogInfo($"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] TANNER_FADE_FIX {message}");
    }
}
