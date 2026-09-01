using BepInEx.Logging;
using System;
using System.Collections.Generic;

namespace SerpNativeAPI
{
    /// <summary>Selects the native coordinate used as the origin of gatehouse enemy distance checks.</summary>
    public enum GatehouseDistanceOrigin
    {
        /// <summary>Use Vanilla's begin coordinate at one corner of the gatehouse bounds.</summary>
        VanillaBuildingBegin,
        /// <summary>Use the exact center of the complete gatehouse bounding box.</summary>
        BuildingBoundsCenter
    }

    /// <summary>
    /// Applies a validated, transactional gatehouse distance origin. The intended future consumer
    /// is BugfixesAndQoL; this is documentation, not a runtime dependency.
    /// </summary>
    public interface IGatehouseDistanceOriginCapability
    {
        /// <summary>Attempts to apply and verify the requested process-wide gatehouse distance origin.</summary>
        bool TryApply(GatehouseDistanceOrigin origin, out NativeCapabilityDiagnostic diagnostic);
    }

    internal sealed class GatehouseDistanceOriginTarget
    {
        public GatehouseDistanceOriginTarget(long block, byte[] vanillaBytes, byte[] centeredBytes)
        {
            if (vanillaBytes == null || centeredBytes == null || vanillaBytes.Length == 0 ||
                vanillaBytes.Length != centeredBytes.Length)
                throw new ArgumentException("Gatehouse distance blocks must be non-empty and have equal lengths.");

            Block = block;
            VanillaBytes = (byte[])vanillaBytes.Clone();
            CenteredBytes = (byte[])centeredBytes.Clone();
            Intervals = new[] { new NativeInterval(block, checked(block + vanillaBytes.Length)) };
        }

        public long Block { get; }
        public byte[] VanillaBytes { get; }
        public byte[] CenteredBytes { get; }
        public IReadOnlyList<NativeInterval> Intervals { get; }
    }

    internal sealed class GatehouseDistanceOriginService
    {
        private readonly string binaryHash;
        private readonly GatehouseDistanceOriginTarget target;
        private readonly INativeMemory memory;
        private readonly NativeOwnershipRegistry ownership;
        private readonly object mutationSync;
        private readonly ManualLogSource log;
        private GatehouseDistanceOrigin expectedOrigin = GatehouseDistanceOrigin.VanillaBuildingBegin;

        public GatehouseDistanceOriginService(
            string binaryHash,
            GatehouseDistanceOriginTarget target,
            INativeMemory memory,
            NativeOwnershipRegistry ownership,
            object mutationSync,
            ManualLogSource log)
        {
            this.binaryHash = binaryHash;
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
            this.ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
            this.mutationSync = mutationSync ?? throw new ArgumentNullException(nameof(mutationSync));
            this.log = log;
            VerifyExpected();
        }

        public IGatehouseDistanceOriginCapability Bind(string ownerGuid) => new OwnerCapability(this, ownerGuid);

        private bool TryApply(
            string ownerGuid,
            GatehouseDistanceOrigin origin,
            out NativeCapabilityDiagnostic diagnostic)
        {
            if (origin != GatehouseDistanceOrigin.VanillaBuildingBegin &&
                origin != GatehouseDistanceOrigin.BuildingBoundsCenter)
            {
                diagnostic = Diagnostic(
                    NativeCapabilityState.ValidationFailed,
                    $"Unknown gatehouse distance origin value: {(int)origin}.");
                return false;
            }

            lock (mutationSync)
            {
                if (!ownership.TryReserve(
                        ownerGuid,
                        NativeCapabilityIds.GatehouseDistanceOrigin,
                        NativeReservationMode.Exclusive,
                        target.Intervals,
                        out string conflictOwner))
                {
                    diagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.GatehouseDistanceOrigin,
                        NativeCapabilityState.Conflict,
                        binaryHash,
                        "The gatehouse distance-origin memory is already reserved by another owner.",
                        conflictOwner);
                    return false;
                }

                try
                {
                    VerifyExpected();
                    if (origin == expectedOrigin)
                    {
                        diagnostic = Diagnostic(
                            NativeCapabilityState.Available,
                            $"The requested gatehouse distance origin {origin} is already active and was verified.");
                        return true;
                    }

                    byte[] oldBytes = BytesFor(expectedOrigin);
                    byte[] desiredBytes = BytesFor(origin);
                    GatehouseNativeMutation.Execute(
                        memory,
                        target.Intervals,
                        VerifyExpected,
                        () =>
                        {
                            WriteBytes(desiredBytes);
                            VerifyBytes(desiredBytes, "requested distance-origin block");
                        },
                        () =>
                        {
                            WriteBytes(oldBytes);
                            VerifyBytes(oldBytes, "rolled-back distance-origin block");
                        });

                    expectedOrigin = origin;
                    VerifyExpected();
                    diagnostic = Diagnostic(
                        NativeCapabilityState.Available,
                        $"Gatehouse distance origin {origin} was applied and verified.");
                    NativeApiLog.Info(
                        log,
                        $"capability={NativeCapabilityIds.GatehouseDistanceOrigin}, build={binaryHash}, owner={ownerGuid}, origin={origin}, status=applied");
                    return true;
                }
                catch (Exception ex)
                {
                    diagnostic = Diagnostic(NativeCapabilityState.ValidationFailed, ex.Message);
                    NativeApiLog.Error(
                        log,
                        $"capability={NativeCapabilityIds.GatehouseDistanceOrigin}, build={binaryHash}, owner={ownerGuid}, origin={origin}, status=failed, error={ex}");
                    return false;
                }
            }
        }

        private byte[] BytesFor(GatehouseDistanceOrigin origin) =>
            origin == GatehouseDistanceOrigin.BuildingBoundsCenter ? target.CenteredBytes : target.VanillaBytes;

        private void VerifyExpected() =>
            VerifyBytes(BytesFor(expectedOrigin), expectedOrigin == GatehouseDistanceOrigin.BuildingBoundsCenter
                ? "centered distance-origin block"
                : "Vanilla distance-origin block");

        private void WriteBytes(byte[] values)
        {
            for (int index = 0; index < values.Length; index++)
                memory.WriteByte(target.Block + index, values[index]);
        }

        private void VerifyBytes(byte[] expected, string name)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                byte actual = memory.ReadByte(target.Block + index);
                if (actual != expected[index])
                    throw new InvalidOperationException(
                        $"Gatehouse {name} changed unexpectedly at +0x{index:X}: expected=0x{expected[index]:X2}, actual=0x{actual:X2}.");
            }
        }

        private NativeCapabilityDiagnostic Diagnostic(NativeCapabilityState state, string reason) =>
            new NativeCapabilityDiagnostic(NativeCapabilityIds.GatehouseDistanceOrigin, state, binaryHash, reason);

        internal static int ComputeCenteredDistanceNative(
            int beginX,
            int beginY,
            int endX,
            int endY,
            int unitX,
            int unitY)
        {
            int dx = Math.Abs(checked((beginX + endX) * 4 - unitX));
            int dy = Math.Abs(checked((beginY + endY) * 4 - unitY));
            return Math.Max(dx, dy);
        }

        private sealed class OwnerCapability : IGatehouseDistanceOriginCapability
        {
            private readonly GatehouseDistanceOriginService service;
            private readonly string ownerGuid;

            public OwnerCapability(GatehouseDistanceOriginService service, string ownerGuid)
            {
                this.service = service;
                this.ownerGuid = ownerGuid;
            }

            public bool TryApply(GatehouseDistanceOrigin origin, out NativeCapabilityDiagnostic diagnostic) =>
                service.TryApply(ownerGuid, origin, out diagnostic);
        }
    }

    internal static class GatehouseNativeMutation
    {
        public static void Execute(
            INativeMemory memory,
            IReadOnlyList<NativeInterval> intervals,
            Action verifyExpected,
            Action writeAndVerify,
            Action rollbackAndVerify)
        {
            if (memory == null)
                throw new ArgumentNullException(nameof(memory));
            if (intervals == null || intervals.Count == 0)
                throw new ArgumentException("At least one native interval is required.", nameof(intervals));
            if (verifyExpected == null)
                throw new ArgumentNullException(nameof(verifyExpected));
            if (writeAndVerify == null)
                throw new ArgumentNullException(nameof(writeAndVerify));
            if (rollbackAndVerify == null)
                throw new ArgumentNullException(nameof(rollbackAndVerify));

            List<PageProtection> protections = AcquireWritablePages(memory, intervals);
            Exception primary = null;
            Exception cleanup = null;
            bool writesStarted = false;
            try
            {
                try
                {
                    // Close the race between the public preflight and acquiring page write access.
                    verifyExpected();
                    writesStarted = true;
                    writeAndVerify();
                }
                catch (Exception ex)
                {
                    primary = ex;
                    if (writesStarted)
                    {
                        try { rollbackAndVerify(); }
                        catch (Exception rollback)
                        {
                            primary = new AggregateException("The native write and rollback both failed.", primary, rollback);
                        }
                    }
                }
            }
            finally
            {
                for (int index = protections.Count - 1; index >= 0; index--)
                {
                    PageProtection protection = protections[index];
                    try { memory.RestoreProtection(protection.Address, memory.PageSize, protection.Protection); }
                    catch (Exception ex) { cleanup = Combine(cleanup, ex); }
                }
                foreach (NativeInterval interval in intervals)
                {
                    try { memory.Flush(interval.Start, checked((int)(interval.End - interval.Start))); }
                    catch (Exception ex) { cleanup = Combine(cleanup, ex); }
                }
            }

            if (primary != null && cleanup != null)
                throw new AggregateException("The gatehouse transaction and cleanup both failed.", primary, cleanup);
            if (primary != null)
                throw primary;
            if (cleanup != null)
                throw cleanup;
        }

        private static List<PageProtection> AcquireWritablePages(
            INativeMemory memory,
            IReadOnlyList<NativeInterval> intervals)
        {
            if (memory.PageSize <= 0)
                throw new InvalidOperationException("The native memory adapter returned an invalid page size.");

            var pages = new SortedSet<long>();
            foreach (NativeInterval interval in intervals)
            {
                long firstPage = PageStart(interval.Start, memory.PageSize);
                long lastPage = PageStart(interval.End - 1, memory.PageSize);
                for (long page = firstPage; page <= lastPage; page = checked(page + memory.PageSize))
                    pages.Add(page);
            }

            var protections = new List<PageProtection>();
            try
            {
                foreach (long page in pages)
                    protections.Add(new PageProtection(page, memory.MakeWritable(page, memory.PageSize)));
                return protections;
            }
            catch (Exception primary)
            {
                Exception cleanup = null;
                for (int index = protections.Count - 1; index >= 0; index--)
                {
                    PageProtection protection = protections[index];
                    try { memory.RestoreProtection(protection.Address, memory.PageSize, protection.Protection); }
                    catch (Exception ex) { cleanup = Combine(cleanup, ex); }
                }
                if (cleanup != null)
                    throw new AggregateException("Acquiring writable native pages and cleanup both failed.", primary, cleanup);
                throw;
            }
        }

        private static long PageStart(long address, int pageSize) => address - address % pageSize;

        private static Exception Combine(Exception current, Exception next) =>
            current == null ? next : new AggregateException(current, next);

        private readonly struct PageProtection
        {
            public PageProtection(long address, uint protection)
            {
                Address = address;
                Protection = protection;
            }

            public long Address { get; }
            public uint Protection { get; }
        }
    }
}
