using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using Iced.Intel;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace APIShared
{
    // AIBuildDiagnoseTest BEGIN -- remove this file and its two project/bootstrap references together.
    /// <summary>Read-only evidence from the native AI construction path.</summary>
    public sealed class AiBuildDiagnosticRecord
    {
        /// <summary>Creates a read-only diagnostic record.</summary>
        public AiBuildDiagnosticRecord(string stage, int playerId, long a, long b, long c, long d)
        {
            Stage = stage;
            PlayerId = playerId;
            A = a; B = b; C = c; D = d;
        }
        /// <summary>Native observation stage.</summary>
        public string Stage { get; }
        /// <summary>One-based AI player ID.</summary>
        public int PlayerId { get; }
        /// <summary>Stage-specific first value.</summary>
        public long A { get; }
        /// <summary>Stage-specific second value.</summary>
        public long B { get; }
        /// <summary>Stage-specific third value.</summary>
        public long C { get; }
        /// <summary>Stage-specific fourth value.</summary>
        public long D { get; }
    }

    /// <summary>Optional process-lifetime AI construction observer. Inert without registration.</summary>
    public static class AiBuildDiagnostic
    {
        private const int SchedulerRva = 0x539B0;
        private const int SchedulerSize = 743;
        private const int ExpectedDisplacedBytes = 10;
        private const string SchedulerHash = "477FC129E9B1A2BBFD9BE2093F30C2E21BC0964337A9843CC7F7BF34531FED8E";
        private const int PlayerStride = 0x583C;
        private const int VillageSlotRva = 0x379D0CC;
        private const int SchedulerDelayRva = 0x379D8B0;
        private const int EconomyPhaseRva = 0x379E630;
        private static readonly object Sync = new object();
        private static Action<AiBuildDiagnosticRecord> observer;
        private static SchedulerService scheduler;
        private static long moduleBase;
        private static string nativeHash;
        private static ScanRegion region;
        private static ManualLogSource log;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SchedulerDelegate(ulong state, int playerId);

        internal static void Initialize(long baseAddress, string hash, ScanRegion scanRegion, ManualLogSource logger)
        {
            lock (Sync)
            {
                moduleBase = baseAddress;
                nativeHash = hash;
                region = scanRegion;
                log = logger;
            }
        }

        /// <summary>True only after the test observer has registered.</summary>
        public static bool HasObserver => Volatile.Read(ref observer) != null;

        /// <summary>Registers a single observer and then installs the scheduler hook once.</summary>
        public static bool TryRegister(string ownerGuid, Action<AiBuildDiagnosticRecord> callback, out string error)
        {
            error = null;
            if (ownerGuid != "AIBuildDiagnoseTest_Serp" || callback == null)
            {
                error = "The diagnostic owner or callback is invalid.";
                return false;
            }
            lock (Sync)
            {
                if (observer != null)
                {
                    if (ReferenceEquals(observer, callback)) return true;
                    error = "A different diagnostic observer is already registered.";
                    return false;
                }
                if (moduleBase == 0 || region == null ||
                    !string.Equals(nativeHash, ApiSharedRuntime.SupportedHash, StringComparison.OrdinalIgnoreCase))
                {
                    error = "The validated native module is unavailable.";
                    return false;
                }
                try
                {
                    byte[] bytes = new byte[SchedulerSize];
                    Marshal.Copy(new IntPtr(checked(moduleBase + SchedulerRva)), bytes, 0, bytes.Length);
                    if (!string.Equals(ApiSharedRuntime.ComputeSha256(bytes), SchedulerHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Scheduler function hash differs from the audited build.");
                    ValidateEntry(bytes);
                    ProbeBackend(unchecked((ulong)moduleBase) + SchedulerRva, callback);
                    var candidate = new SchedulerService();
                    candidate.Install(moduleBase, region);
                    scheduler = candidate;
                    Volatile.Write(ref observer, callback);
                    return true;
                }
                catch (Exception ex)
                {
                    // Existing BugfixesAndQoL observations remain useful if only the
                    // separate scheduler entry cannot be hooked.
                    Volatile.Write(ref observer, callback);
                    error = ex.Message;
                    return false;
                }
            }
        }

        /// <summary>Publishes a record only while the test observer is present.</summary>
        public static void Publish(string stage, int playerId, long a = 0, long b = 0, long c = 0, long d = 0)
        {
            Action<AiBuildDiagnosticRecord> target = Volatile.Read(ref observer);
            if (target == null) return;
            try { target(new AiBuildDiagnosticRecord(stage, playerId, a, b, c, d)); }
            catch (Exception ex) { NativeApiLog.Error(log, "AI diagnostic observer failed: " + ex); }
        }

        private sealed class SchedulerService
        {
            private readonly DetourHandle<SchedulerDelegate> hook = new DetourHandle<SchedulerDelegate>();
            private HookTransaction transaction;

            internal void Install(long baseAddress, ScanRegion scanRegion)
            {
                var pending = new HookTransaction(scanRegion,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = false
                    });
                try
                {
                    pending.AddDetour(hook, HookTarget.FromAddress(unchecked((ulong)baseAddress) + SchedulerRva), Invoke);
                    CommitResult result = pending.Commit();
                    if (!result.IsCompleteSuccess || !hook.Success)
                        throw new InvalidOperationException("Scheduler diagnostic hook did not commit completely.");
                    ValidateDetour(hook.Hook as NativeDetour<SchedulerDelegate>,
                        unchecked((ulong)baseAddress) + SchedulerRva);
                    transaction = pending;
                }
                catch
                {
                    pending.Dispose(); // Unpublished candidate only.
                    throw;
                }
            }

            private void Invoke(ulong state, int playerId)
            {
                if (playerId >= 1 && playerId <= 8)
                {
                    try { PublishScheduler("scheduler-before", playerId); }
                    catch (Exception ex) { NativeApiLog.Error(log, "AI scheduler pre-observation failed: " + ex); }
                }
                try { hook.Original(state, playerId); }
                finally
                {
                    if (playerId >= 1 && playerId <= 8)
                    {
                        try { PublishScheduler("scheduler-after", playerId); }
                        catch (Exception ex) { NativeApiLog.Error(log, "AI scheduler post-observation failed: " + ex); }
                    }
                }
            }

            private static void PublishScheduler(string stage, int playerId)
            {
                long offset = checked(moduleBase + (long)playerId * PlayerStride);
                int village = Marshal.ReadInt32(new IntPtr(offset + VillageSlotRva));
                int delay = Marshal.ReadInt32(new IntPtr(offset + SchedulerDelayRva));
                int phase = Marshal.ReadInt32(new IntPtr(offset + EconomyPhaseRva));
                Publish(stage, playerId, village, delay, phase);
            }
        }

        private static void ValidateEntry(byte[] bytes)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = 0x1800539B0;
            int length = 0;
            while (length < 6)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid ||
                    instruction.FlowControl != FlowControl.Next)
                    throw new InvalidOperationException("Scheduler detour entry is not a straight-line prologue.");
                length += instruction.Length;
            }
            if (length != ExpectedDisplacedBytes)
                throw new InvalidOperationException("Scheduler indirect displacement is not 10 bytes.");
        }

        private static void ProbeBackend(ulong entry, Action<AiBuildDiagnosticRecord> callback)
        {
            IntPtr copy = Marshal.AllocHGlobal(64);
            NativeDetour<SchedulerDelegate> probe = null;
            try
            {
                byte[] bytes = new byte[64];
                Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
                Marshal.Copy(bytes, 0, copy, bytes.Length);
                SchedulerDelegate noop = (state, playerId) => { };
                var request = new DetourRequest<SchedulerDelegate>
                {
                    Name = "AIBuildDiagnoseTest copied scheduler entry",
                    TargetAddress = unchecked((ulong)copy.ToInt64()),
                    Callback = noop
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<SchedulerDelegate>;
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != ExpectedDisplacedBytes || probe.IsInstalled ||
                    probe.TargetAddress != unchecked((ulong)copy.ToInt64()) ||
                    probe.PointerSlot == IntPtr.Zero || probe.HookEntryPointAddress == IntPtr.Zero)
                    throw new InvalidOperationException("Installed RedBird backend rejects the scheduler entry contract.");
                probe.Enable();
                ValidateDetour(probe, unchecked((ulong)copy.ToInt64()));
                GC.KeepAlive(callback);
                GC.KeepAlive(noop);
            }
            finally
            {
                probe?.Dispose();
                Marshal.FreeHGlobal(copy);
            }
        }

        private static void ValidateDetour(NativeDetour<SchedulerDelegate> detour, ulong target)
        {
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != target ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != ExpectedDisplacedBytes ||
                detour.PointerSlot == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Committed scheduler detour differs from the indirect backend contract.");
            byte[] patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)target), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)target + 6 + BitConverter.ToInt32(patch, 2)) != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Committed scheduler pointer slot or hook entry differs.");
        }
    }
    // AIBuildDiagnoseTest END
}
