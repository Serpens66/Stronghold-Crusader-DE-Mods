using BepInEx.Logging;
using System;
using System.Collections.Generic;

namespace SerpNativeAPI
{
    internal readonly struct NativeByteInvariant
    {
        public NativeByteInvariant(long address, byte value)
        {
            Address = address;
            Value = value;
        }

        public long Address { get; }
        public byte Value { get; }
    }

    internal sealed class GatehouseTimingTarget
    {
        public const int VanillaAiDistance = 200;
        public const int VanillaAiDelay = 1200;
        public const int VanillaHumanDistance = 140;
        public const int VanillaHumanDelay = 100;

        public GatehouseTimingTarget(
            long aiDistance,
            long aiDelay,
            long humanDistance,
            long humanDelay,
            IReadOnlyList<NativeByteInvariant> instructionInvariants = null)
        {
            AiDistance = aiDistance;
            AiDelay = aiDelay;
            HumanDistance = humanDistance;
            HumanDelay = humanDelay;
            InstructionInvariants = instructionInvariants ?? Array.Empty<NativeByteInvariant>();
            Intervals = new[]
            {
                new NativeInterval(aiDistance, aiDistance + 4),
                new NativeInterval(aiDelay, aiDelay + 4),
                new NativeInterval(humanDistance, humanDistance + 4),
                new NativeInterval(humanDelay, humanDelay + 4)
            };
        }

        public long AiDistance { get; }
        public long AiDelay { get; }
        public long HumanDistance { get; }
        public long HumanDelay { get; }
        public IReadOnlyList<NativeInterval> Intervals { get; }
        public IReadOnlyList<NativeByteInvariant> InstructionInvariants { get; }
    }

    internal sealed class GatehouseTimingService
    {
        private const int TicksPerSecond = 40;
        private const int UnitsPerTile = 8;
        private readonly object sync = new object();
        private readonly string binaryHash;
        private readonly GatehouseTimingTarget target;
        private readonly INativeMemory memory;
        private readonly NativeOwnershipRegistry ownership;
        private readonly ManualLogSource log;
        private int expectedAiDistance = GatehouseTimingTarget.VanillaAiDistance;
        private int expectedAiDelay = GatehouseTimingTarget.VanillaAiDelay;
        private int expectedHumanDistance = GatehouseTimingTarget.VanillaHumanDistance;
        private int expectedHumanDelay = GatehouseTimingTarget.VanillaHumanDelay;

        public GatehouseTimingService(
            string binaryHash,
            GatehouseTimingTarget target,
            INativeMemory memory,
            NativeOwnershipRegistry ownership,
            ManualLogSource log)
        {
            this.binaryHash = binaryHash;
            this.target = target;
            this.memory = memory;
            this.ownership = ownership;
            this.log = log;
            VerifyExpected();
        }

        public IGatehouseTimingCapability Bind(string ownerGuid) => new OwnerCapability(this, ownerGuid);

        private bool TryApply(string ownerGuid, GatehouseTimingSettings settings, out NativeCapabilityDiagnostic diagnostic)
        {
            int aiDistance;
            int aiDelay;
            int humanDistance;
            int humanDelay;
            try
            {
                if (settings.Enabled)
                {
                    ValidateRange(settings.HumanReopenDelaySeconds, 0.0, 30.0, nameof(settings.HumanReopenDelaySeconds));
                    ValidateRange(settings.AiReopenDelaySeconds, 0.0, 120.0, nameof(settings.AiReopenDelaySeconds));
                    ValidateRange(settings.HumanCloseDistanceTiles, 5.0, 50.0, nameof(settings.HumanCloseDistanceTiles));
                    ValidateRange(settings.AiCloseDistanceTiles, 5.0, 50.0, nameof(settings.AiCloseDistanceTiles));
                    humanDelay = ConvertNativeUInt16(settings.HumanReopenDelaySeconds, TicksPerSecond, nameof(settings.HumanReopenDelaySeconds));
                    aiDelay = ConvertNativeUInt16(settings.AiReopenDelaySeconds, TicksPerSecond, nameof(settings.AiReopenDelaySeconds));
                    humanDistance = ConvertNativeUInt16(settings.HumanCloseDistanceTiles, UnitsPerTile, nameof(settings.HumanCloseDistanceTiles));
                    aiDistance = ConvertNativeUInt16(settings.AiCloseDistanceTiles, UnitsPerTile, nameof(settings.AiCloseDistanceTiles));
                }
                else
                {
                    aiDistance = GatehouseTimingTarget.VanillaAiDistance;
                    aiDelay = GatehouseTimingTarget.VanillaAiDelay;
                    humanDistance = GatehouseTimingTarget.VanillaHumanDistance;
                    humanDelay = GatehouseTimingTarget.VanillaHumanDelay;
                }
            }
            catch (Exception ex)
            {
                diagnostic = Diagnostic(NativeCapabilityState.ValidationFailed, ex.Message);
                return false;
            }

            lock (sync)
            {
                if (!ownership.TryReserve(
                        ownerGuid,
                        NativeCapabilityIds.GatehouseTiming,
                        NativeReservationMode.Exclusive,
                        target.Intervals,
                        out string conflictOwner))
                {
                    diagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.GatehouseTiming,
                        NativeCapabilityState.Conflict,
                        binaryHash,
                        "The gatehouse timing memory is already reserved by another owner.",
                        conflictOwner);
                    return false;
                }

                try
                {
                    VerifyExpected();
                    if (aiDistance == expectedAiDistance && aiDelay == expectedAiDelay &&
                        humanDistance == expectedHumanDistance && humanDelay == expectedHumanDelay)
                    {
                        diagnostic = Diagnostic(NativeCapabilityState.Available, "The requested gatehouse values are already active and were verified.");
                        return true;
                    }
                    WriteTransaction(aiDistance, aiDelay, humanDistance, humanDelay);
                    expectedAiDistance = aiDistance;
                    expectedAiDelay = aiDelay;
                    expectedHumanDistance = humanDistance;
                    expectedHumanDelay = humanDelay;
                    VerifyExpected();
                    string values = FormatValues(aiDistance, aiDelay, humanDistance, humanDelay);
                    diagnostic = Diagnostic(NativeCapabilityState.Available, "Gatehouse values were applied and verified: " + values);
                    NativeApiLog.Info(log, $"capability={NativeCapabilityIds.GatehouseTiming}, build={binaryHash}, owner={ownerGuid}, enabled={settings.Enabled}, status=applied, {values}");
                    return true;
                }
                catch (Exception ex)
                {
                    diagnostic = Diagnostic(NativeCapabilityState.ValidationFailed, ex.Message);
                    NativeApiLog.Error(log, $"capability={NativeCapabilityIds.GatehouseTiming}, build={binaryHash}, owner={ownerGuid}, status=failed, error={ex}");
                    return false;
                }
            }
        }

        private void WriteTransaction(int aiDistance, int aiDelay, int humanDistance, int humanDelay)
        {
            int oldAiDistance = expectedAiDistance;
            int oldAiDelay = expectedAiDelay;
            int oldHumanDistance = expectedHumanDistance;
            int oldHumanDelay = expectedHumanDelay;
            List<PageProtection> protections = AcquireWritablePages();
            Exception primary = null;
            Exception cleanup = null;
            bool writesStarted = false;
            try
            {
                try
                {
                    // Recheck after acquiring write access so a change between the public
                    // preflight and the transaction cannot be silently adopted as rollback state.
                    VerifyExpected();
                    writesStarted = true;
                    memory.WriteInt32(target.AiDistance, aiDistance);
                    memory.WriteInt32(target.AiDelay, aiDelay);
                    memory.WriteInt32(target.HumanDistance, humanDistance);
                    memory.WriteInt32(target.HumanDelay, humanDelay);
                    Verify(target.AiDistance, aiDistance, "AI distance");
                    Verify(target.AiDelay, aiDelay, "AI delay");
                    Verify(target.HumanDistance, humanDistance, "human distance");
                    Verify(target.HumanDelay, humanDelay, "human delay");
                }
                catch (Exception ex)
                {
                    primary = ex;
                    if (writesStarted)
                    {
                        try
                        {
                            memory.WriteInt32(target.AiDistance, oldAiDistance);
                            memory.WriteInt32(target.AiDelay, oldAiDelay);
                            memory.WriteInt32(target.HumanDistance, oldHumanDistance);
                            memory.WriteInt32(target.HumanDelay, oldHumanDelay);
                            Verify(target.AiDistance, oldAiDistance, "rolled-back AI distance");
                            Verify(target.AiDelay, oldAiDelay, "rolled-back AI delay");
                            Verify(target.HumanDistance, oldHumanDistance, "rolled-back human distance");
                            Verify(target.HumanDelay, oldHumanDelay, "rolled-back human delay");
                        }
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
                foreach (NativeInterval interval in target.Intervals)
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

        private List<PageProtection> AcquireWritablePages()
        {
            if (memory.PageSize <= 0)
                throw new InvalidOperationException("The native memory adapter returned an invalid page size.");

            var pages = new SortedSet<long>();
            foreach (NativeInterval interval in target.Intervals)
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

        private void VerifyExpected()
        {
            foreach (NativeByteInvariant invariant in target.InstructionInvariants)
            {
                byte actual = memory.ReadByte(invariant.Address);
                if (actual != invariant.Value)
                    throw new InvalidOperationException($"Gatehouse instruction byte changed unexpectedly at target offset: expected=0x{invariant.Value:X2}, actual=0x{actual:X2}.");
            }
            Verify(target.AiDistance, expectedAiDistance, "AI distance");
            Verify(target.AiDelay, expectedAiDelay, "AI delay");
            Verify(target.HumanDistance, expectedHumanDistance, "human distance");
            Verify(target.HumanDelay, expectedHumanDelay, "human delay");
        }

        private void Verify(long address, int expected, string name)
        {
            int actual = memory.ReadInt32(address);
            if (actual != expected)
                throw new InvalidOperationException($"Gatehouse {name} changed unexpectedly: expected={expected}, actual={actual}.");
        }

        private NativeCapabilityDiagnostic Diagnostic(NativeCapabilityState state, string reason) =>
            new NativeCapabilityDiagnostic(NativeCapabilityIds.GatehouseTiming, state, binaryHash, reason);

        private static void ValidateRange(double value, double minimum, double maximum, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(name, $"{name} must be finite and between {minimum} and {maximum}.");
        }

        internal static int ConvertNativeUInt16(double value, int multiplier, string name)
        {
            int converted = checked((int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero));
            if (converted < ushort.MinValue || converted > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(name, $"{name} converts to {converted}, outside the native UInt16 range.");
            return converted;
        }

        private static long PageStart(long address, int pageSize) => address - address % pageSize;

        private static Exception Combine(Exception current, Exception next) =>
            current == null ? next : new AggregateException(current, next);

        private static string FormatValues(int aiDistance, int aiDelay, int humanDistance, int humanDelay) =>
            $"humanClose={humanDistance / (double)UnitsPerTile:0.###}tiles/{humanDistance}units, " +
            $"humanReopen={humanDelay / (double)TicksPerSecond:0.###}s/{humanDelay}ticks, " +
            $"aiClose={aiDistance / (double)UnitsPerTile:0.###}tiles/{aiDistance}units, " +
            $"aiReopen={aiDelay / (double)TicksPerSecond:0.###}s/{aiDelay}ticks";

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

        private sealed class OwnerCapability : IGatehouseTimingCapability
        {
            private readonly GatehouseTimingService service;
            private readonly string ownerGuid;
            public OwnerCapability(GatehouseTimingService service, string ownerGuid) { this.service = service; this.ownerGuid = ownerGuid; }
            public bool TryApply(GatehouseTimingSettings settings, out NativeCapabilityDiagnostic diagnostic) =>
                service.TryApply(ownerGuid, settings, out diagnostic);
        }
    }
}
