using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace TannerAnimationDiagnostic
{
    internal sealed unsafe class NativeTannerFade
    {
        private const string AuditedSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int PacketWriterRva = 0x19D110;
        private const int PacketManagerRva = 0xA98820;
        private const int BuildingArrayRva = 0x64CBB0;
        private const int BuildingStride = 0x32C;
        private const int GlobalIdOffset = 0x134;
        private const int TannerAnimationFile = 69;
        private const int TannerAnimationLayer = 19;
        private static readonly byte[] EntryPrefix =
        {
            0x4C, 0x8B, 0xDC, 0x53, 0x55, 0x57, 0x41, 0x56,
            0x41, 0x57, 0x48, 0x83, 0xEC, 0x60
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AnimationPacketDelegate(IntPtr packetManager, int buildingId,
            int animationLayer, int tileX, int tileY, int kind, int file, int image,
            int colour, int offsetX, int offsetY, uint transparency, int flags,
            int shift, int special);

        private readonly DetourHandle<AnimationPacketDelegate> handle =
            new DetourHandle<AnimationPacketDelegate>();
        private readonly NativeTannerFadeTracker tracker = new NativeTannerFadeTracker();
        private readonly object sync = new object();
        private readonly ManualLogSource log;
        private readonly ulong moduleBase;
        private HookTransaction transaction;
        private IDisposable mapUnloadSubscription;
        private long unpausedTicks;
        private int firstPacketLogged;
        private int firstTannerPacketLogged;
        private int callbackErrorLogged;

        internal NativeTannerFade(ManualLogSource log, CrusaderLibraryLoadContext context)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (context == null || context.ModuleHandle == IntPtr.Zero)
                throw new ArgumentException("CrusaderDE native module is unavailable.", nameof(context));
            moduleBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            ValidateInstalledDll(context.ModuleHandle);
            ValidateEntry();

            HookTransaction candidate = null;
            IDisposable unload = null;
            bool tickSubscribed = false;
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
                    HookTarget.FromAddress(moduleBase + PacketWriterRva),
                    (AnimationPacketDelegate)WriteAnimationPacket);
                CommitResult result = candidate.Commit();
                NativeDetour<AnimationPacketDelegate> detour =
                    handle.Hook as NativeDetour<AnimationPacketDelegate>;
                ValidateDetour(result, detour);
                unload = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(_ => Clear());
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
                tickSubscribed = true;
                Write($"NATIVE_FIX_READY rva=0x{PacketWriterRva:X} scheme={detour.Scheme} span={detour.DisplacedByteCount}");
                transaction = candidate;
                mapUnloadSubscription = unload;
                candidate = null;
                unload = null;
            }
            catch
            {
                // Only this unpublished initialization candidate may be rolled back.
                if (tickSubscribed)
                    GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
                unload?.Dispose();
                candidate?.Dispose();
                throw;
            }
        }

        private void OnGameTick(int ignoredTick) => Interlocked.Increment(ref unpausedTicks);

        private void WriteAnimationPacket(IntPtr packetManager, int buildingId,
            int animationLayer, int tileX, int tileY, int kind, int file, int image,
            int colour, int offsetX, int offsetY, uint transparency, int flags,
            int shift, int special)
        {
            uint delivered = transparency;
            try
            {
                if (Volatile.Read(ref firstPacketLogged) == 0 &&
                    Interlocked.Exchange(ref firstPacketLogged, 1) == 0)
                    Write($"NATIVE_PACKET_CALLBACK_FIRST kind={kind} layer={animationLayer} file={file} image={image} vanilla_raw={transparency}");

                if (packetManager == unchecked((IntPtr)(long)(moduleBase + PacketManagerRva)) &&
                    kind == 4 && animationLayer == TannerAnimationLayer &&
                    file == TannerAnimationFile && buildingId > 0 && buildingId < 4000)
                {
                    long tick = Interlocked.Read(ref unpausedTicks);
                    uint generation = *(uint*)(moduleBase + BuildingArrayRva +
                        (ulong)buildingId * BuildingStride + GlobalIdOffset);
                    if (Volatile.Read(ref firstTannerPacketLogged) == 0 &&
                        Interlocked.Exchange(ref firstTannerPacketLogged, 1) == 0)
                        Write($"NATIVE_TANNER_PACKET_FIRST building={buildingId} generation={generation} image={image} vanilla_raw={transparency} tick={tick}");

                    NativeFadeSample sample;
                    lock (sync)
                        sample = tracker.Observe(buildingId, generation, image,
                            transparency, tick);
                    delivered = sample.EffectiveTransparency;
                    if (sample.Ended)
                        Write($"NATIVE_FADE_END building={buildingId} generation={generation} image={image} vanilla_raw={transparency} elapsed_ticks={sample.ElapsedTicks}");
                    if (sample.Started || (sample.Changed && delivered != transparency))
                    {
                        string marker = sample.Started ? "NATIVE_FADE_START" : "NATIVE_FADE_STEP";
                        string alpha = ((32.0 - delivered) / 32.0).ToString("F6", CultureInfo.InvariantCulture);
                        Write($"{marker} building={buildingId} generation={generation} image={image} vanilla_raw={transparency} effective_raw={delivered} effective_alpha={alpha} elapsed_ticks={sample.ElapsedTicks}");
                    }
                }
            }
            catch (Exception ex)
            {
                delivered = transparency;
                if (Interlocked.Exchange(ref callbackErrorLogged, 1) == 0)
                    log.LogError($"TANNER_NATIVE_FIX_CALLBACK_FAILED {ex}");
            }
            handle.Original(packetManager, buildingId, animationLayer, tileX, tileY, kind,
                file, image, colour, offsetX, offsetY, delivered, flags, shift, special);
        }

        private void Clear()
        {
            lock (sync)
                tracker.Clear();
            Write("NATIVE_FADE_RESET reason=map_unload");
        }

        private void ValidateEntry()
        {
            var actual = new byte[EntryPrefix.Length];
            Marshal.Copy(unchecked((IntPtr)(long)(moduleBase + PacketWriterRva)),
                actual, 0, actual.Length);
            for (int i = 0; i < actual.Length; i++)
                if (actual[i] != EntryPrefix[i])
                    throw new InvalidOperationException(
                        "The installed animation packet writer differs from the audited Vanilla bytes.");
        }

        private void ValidateDetour(CommitResult result,
            NativeDetour<AnimationPacketDelegate> detour)
        {
            if (!result.IsCompleteSuccess || !handle.Success || detour == null ||
                !detour.IsInstalled || detour.TargetAddress != moduleBase + PacketWriterRva ||
                detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero ||
                detour.TrampolineAddress == IntPtr.Zero || detour.ChainDepth != 1)
                throw new InvalidOperationException(
                    "The animation packet NativeDetour was not installed with the audited target and chain.");
            string scheme = detour.Scheme.ToString();
            if (scheme != "Indirect" || detour.DisplacedByteCount != 6 ||
                detour.PointerSlot == IntPtr.Zero)
                throw new InvalidOperationException(
                    $"Unexpected animation packet NativeDetour contract: scheme={scheme} span={detour.DisplacedByteCount}.");
            var patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)(moduleBase + PacketWriterRva)),
                patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25)
                throw new InvalidOperationException(
                    "The animation packet detour entry is not an indirect jump.");
            int relativeSlot = BitConverter.ToInt32(patch, 2);
            long actualSlot = unchecked((long)(moduleBase + PacketWriterRva + 6)) + relativeSlot;
            if (actualSlot != detour.PointerSlot.ToInt64())
                throw new InvalidOperationException(
                    "The animation packet detour pointer slot differs from the installed patch.");
            if (Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException(
                    "The animation packet detour pointer slot does not target its hook entry.");
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
            log.LogInfo($"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss.fffzzz}] TANNER_DIAG {message}");
    }
}
