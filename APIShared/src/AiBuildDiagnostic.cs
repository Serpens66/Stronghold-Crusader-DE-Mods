using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace APIShared
{
    // AIBuildDiagnoseTest BEGIN -- remove this file and its two project/bootstrap references together.
    /// <summary>Read-only evidence from the native AI construction path.</summary>
    public sealed class AiBuildDiagnosticRecord
    {
        /// <summary>Creates a read-only diagnostic record.</summary>
        public AiBuildDiagnosticRecord(string stage, int playerId, long attemptId, long a, long b, long c, long d)
        {
            Stage = stage;
            PlayerId = playerId;
            AttemptId = attemptId;
            A = a; B = b; C = c; D = d;
        }
        /// <summary>Native observation stage.</summary>
        public string Stage { get; }
        /// <summary>One-based AI player ID.</summary>
        public int PlayerId { get; }
        /// <summary>Process-unique wood construction attempt, or zero outside one.</summary>
        public long AttemptId { get; }
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
        private const int RouteRva = 0xC3BF0;
        private const int RouteSize = 133;
        private const int RouteDisplacedBytes = 7;
        private const string RouteHash = "1AC2D046356187774EE0EF0C7F782C59B3E416BAEDDBD463305304CF709EB0B0";
        private const int PlayerStride = 0x583C;
        private const int VillageSlotRva = 0x379D0CC;
        private const int SchedulerDelayRva = 0x379D8B0;
        private const int EconomyPhaseRva = 0x379E630;
        private static readonly object Sync = new object();
        private static Action<AiBuildDiagnosticRecord> observer;
        private static SchedulerService scheduler;
        private static RouteService route;
        private static long nextAttemptId;
        [ThreadStatic] private static Stack<WoodAttempt> woodAttempts;
        private static long moduleBase;
        private static string nativeHash;
        private static ScanRegion region;
        private static ManualLogSource log;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SchedulerDelegate(ulong state, int playerId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RouteDelegate(ulong manager, int playerId, int mapperIndex, int tileX, int tileY);

        private struct WoodAttempt
        {
            internal long Id;
            internal int PlayerId;
        }

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

        /// <summary>Registers a single observer and installs the audited observation hooks once.</summary>
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
                    ValidateEntry(bytes, SchedulerRva, ExpectedDisplacedBytes);
                    ProbeBackend(unchecked((ulong)moduleBase) + SchedulerRva, callback);
                    var candidate = new SchedulerService();
                    scheduler = candidate;
                    candidate.Install(moduleBase, region);
                    byte[] routeBytes = new byte[RouteSize];
                    Marshal.Copy(new IntPtr(checked(moduleBase + RouteRva)), routeBytes, 0, routeBytes.Length);
                    if (!string.Equals(ApiSharedRuntime.ComputeSha256(routeBytes), RouteHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Route function hash differs from the audited build.");
                    ValidateEntry(routeBytes, RouteRva, RouteDisplacedBytes);
                    ProbeRouteBackend(unchecked((ulong)moduleBase) + RouteRva);
                    var routeCandidate = new RouteService();
                    route = routeCandidate;
                    routeCandidate.Install(moduleBase, region);
                    Volatile.Write(ref observer, callback);
                    return true;
                }
                catch (Exception ex)
                {
                    // Any successfully installed hook stays process-rooted. The existing
                    // BugfixesAndQoL observations remain useful if another hook fails.
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
            long attemptId = TryGetCurrentWoodAttempt(out long currentId, out int currentPlayer) &&
                currentPlayer == playerId ? currentId : 0;
            try { target(new AiBuildDiagnosticRecord(stage, playerId, attemptId, a, b, c, d)); }
            catch (Exception ex) { NativeApiLog.Error(log, "AI diagnostic observer failed: " + ex); }
        }

        /// <summary>Enters the synchronous Vanilla wood call; zero means diagnostics are absent.</summary>
        public static long BeginWoodAttempt(int playerId)
        {
            if (!HasObserver || playerId < 1 || playerId > 8) return 0;
            long id = Interlocked.Increment(ref nextAttemptId);
            if (woodAttempts == null) woodAttempts = new Stack<WoodAttempt>();
            woodAttempts.Push(new WoodAttempt { Id = id, PlayerId = playerId });
            return id;
        }

        /// <summary>Restores the previous synchronous wood context after the Vanilla call.</summary>
        public static void EndWoodAttempt(long id)
        {
            if (id == 0 || woodAttempts == null || woodAttempts.Count == 0) return;
            if (woodAttempts.Peek().Id == id) woodAttempts.Pop();
            else woodAttempts.Clear();
        }

        /// <summary>Gets the active attempt on the current simulation thread.</summary>
        public static bool TryGetCurrentWoodAttempt(out long id, out int playerId)
        {
            if (woodAttempts != null && woodAttempts.Count != 0)
            {
                WoodAttempt current = woodAttempts.Peek();
                id = current.Id;
                playerId = current.PlayerId;
                return true;
            }
            id = 0;
            playerId = 0;
            return false;
        }

        /// <summary>Reads the audited native placement flags without changing them.</summary>
        public static bool TryReadPlacementStatus(out int preparation, out int rejected, out int placementMode)
        {
            preparation = rejected = placementMode = 0;
            if (!HasObserver || moduleBase == 0) return false;
            try
            {
                long tileManager = checked(moduleBase + 0x405EDB0);
                preparation = Marshal.ReadInt32(new IntPtr(tileManager + 0x204E6F8));
                rejected = Marshal.ReadInt32(new IntPtr(tileManager + 0x204E6FC));
                placementMode = Marshal.ReadInt32(new IntPtr(tileManager + 0x204E7FC));
                return true;
            }
            catch { return false; }
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
                    transaction = pending;
                    ValidateDetour(hook.Hook as NativeDetour<SchedulerDelegate>,
                        unchecked((ulong)baseAddress) + SchedulerRva);
                }
                catch
                {
                    if (!hook.Success) pending.Dispose(); // Unpublished candidate only.
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

        private sealed class RouteService
        {
            private readonly DetourHandle<RouteDelegate> hook = new DetourHandle<RouteDelegate>();
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
                    ulong target = unchecked((ulong)baseAddress) + RouteRva;
                    pending.AddDetour(hook, HookTarget.FromAddress(target), Invoke);
                    CommitResult result = pending.Commit();
                    if (!result.IsCompleteSuccess || !hook.Success)
                        throw new InvalidOperationException("Route diagnostic hook did not commit completely.");
                    transaction = pending;
                    ValidateRouteDetour(hook.Hook as NativeDetour<RouteDelegate>, target);
                }
                catch
                {
                    if (!hook.Success) pending.Dispose(); // Unpublished candidate only.
                    throw;
                }
            }

            private int Invoke(ulong manager, int playerId, int mapperIndex, int tileX, int tileY)
            {
                bool observe = mapperIndex == 3 &&
                    TryGetCurrentWoodAttempt(out long id, out int owner) && owner == playerId && HasObserver;
                if (observe)
                {
                    try { PublishRouteComponents(playerId, mapperIndex, tileX, tileY); }
                    catch (Exception ex) { NativeApiLog.Error(log, "AI route component observation failed: " + ex); }
                }
                int result = hook.Original(manager, playerId, mapperIndex, tileX, tileY);
                if (observe)
                {
                    try { Publish("route-result", playerId, result, mapperIndex, tileX, tileY); }
                    catch (Exception ex) { NativeApiLog.Error(log, "AI route result observation failed: " + ex); }
                }
                return result;
            }

            private static void PublishRouteComponents(int playerId, int mapperIndex, int tileX, int tileY)
            {
                // Audit: 0xC3BF0 reads the player's keep tile and the target tile through
                // the 320800-entry PCL grid; these are the exact inputs to its route test.
                if (playerId < 1 || playerId > 8 || tileX < 0 || tileX >= 800 ||
                    tileY < 0 || tileY >= 400) return;
                int sourceTile = Marshal.ReadInt32(new IntPtr(checked(moduleBase + 0x379AFB0 +
                    (long)playerId * PlayerStride)));
                int targetTile = Marshal.ReadInt32(new IntPtr(checked(moduleBase + 0x402FF2C +
                    (long)tileY * 12))) + tileX;
                if (sourceTile < 0 || sourceTile >= 320800 || targetTile < 0 || targetTile >= 320800)
                {
                    Publish("route-component-unavailable", playerId, sourceTile, targetTile);
                    return;
                }
                long grid = checked(moduleBase + 0x50EC690);
                int source = (ushort)Marshal.ReadInt16(new IntPtr(grid + sourceTile * 2L));
                int target = (ushort)Marshal.ReadInt16(new IntPtr(grid + targetTile * 2L));
                Publish("route-components", playerId, source, target, sourceTile, targetTile);
            }
        }

        private static void ValidateEntry(byte[] bytes, int rva, int displaced)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = 0x180000000UL + (uint)rva;
            int length = 0;
            while (length < 6)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid ||
                    instruction.FlowControl != FlowControl.Next)
                    throw new InvalidOperationException("Diagnostic detour entry is not a straight-line prologue.");
                length += instruction.Length;
            }
            if (length != displaced)
                throw new InvalidOperationException("Diagnostic indirect displacement differs from audit.");
        }

        private static void ProbeRouteBackend(ulong entry)
        {
            IntPtr copy = Marshal.AllocHGlobal(64);
            NativeDetour<RouteDelegate> probe = null;
            try
            {
                byte[] bytes = new byte[64];
                Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
                Marshal.Copy(bytes, 0, copy, bytes.Length);
                RouteDelegate noop = (manager, playerId, mapperIndex, tileX, tileY) => 1;
                var request = new DetourRequest<RouteDelegate>
                {
                    Name = "AIBuildDiagnoseTest copied route entry",
                    TargetAddress = unchecked((ulong)copy.ToInt64()),
                    Callback = noop
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<RouteDelegate>;
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != RouteDisplacedBytes || probe.IsInstalled ||
                    probe.TargetAddress != unchecked((ulong)copy.ToInt64()) ||
                    probe.PointerSlot == IntPtr.Zero || probe.HookEntryPointAddress == IntPtr.Zero)
                    throw new InvalidOperationException("Installed RedBird backend rejects the route entry contract.");
                probe.Enable();
                ValidateRouteDetour(probe, unchecked((ulong)copy.ToInt64()));
                GC.KeepAlive(noop);
            }
            finally
            {
                probe?.Dispose();
                Marshal.FreeHGlobal(copy);
            }
        }

        private static void ValidateRouteDetour(NativeDetour<RouteDelegate> detour, ulong target)
        {
            if (detour == null || !detour.IsInstalled || detour.TargetAddress != target ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != RouteDisplacedBytes ||
                detour.PointerSlot == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("Committed route detour differs from the indirect backend contract.");
            byte[] patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)target), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)target + 6 + BitConverter.ToInt32(patch, 2)) != detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Committed route pointer slot or hook entry differs.");
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
