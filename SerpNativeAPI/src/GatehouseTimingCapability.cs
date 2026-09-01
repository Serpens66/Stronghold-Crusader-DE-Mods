using BepInEx.Logging;
using System;
using System.Collections.Generic;

namespace SerpNativeAPI
{
    internal sealed class GatehouseTimingTarget
    {
        public const int VanillaAiDistance = 200;
        public const int VanillaAiDelay = 1200;
        public const int VanillaHumanDistance = 140;
        public const int VanillaHumanDelay = 100;

        public GatehouseTimingTarget(long aiDistance, long aiDelay, long humanDistance, long humanDelay)
        {
            AiDistance = aiDistance;
            AiDelay = aiDelay;
            HumanDistance = humanDistance;
            HumanDelay = humanDelay;
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
                    ValidateRange(settings.HumanDelaySeconds, 0.0, 30.0, nameof(settings.HumanDelaySeconds));
                    ValidateRange(settings.AiDelaySeconds, 0.0, 120.0, nameof(settings.AiDelaySeconds));
                    ValidateRange(settings.HumanDistanceTiles, 5.0, 50.0, nameof(settings.HumanDistanceTiles));
                    ValidateRange(settings.AiDistanceTiles, 5.0, 50.0, nameof(settings.AiDistanceTiles));
                    humanDelay = Convert(settings.HumanDelaySeconds, TicksPerSecond);
                    aiDelay = Convert(settings.AiDelaySeconds, TicksPerSecond);
                    humanDistance = Convert(settings.HumanDistanceTiles, UnitsPerTile);
                    aiDistance = Convert(settings.AiDistanceTiles, UnitsPerTile);
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

                if (aiDistance == expectedAiDistance && aiDelay == expectedAiDelay &&
                    humanDistance == expectedHumanDistance && humanDelay == expectedHumanDelay)
                {
                    diagnostic = Diagnostic(NativeCapabilityState.Available, "The requested gatehouse timing values are already active.");
                    return true;
                }

                try
                {
                    VerifyExpected();
                    WriteTransaction(aiDistance, aiDelay, humanDistance, humanDelay);
                    expectedAiDistance = aiDistance;
                    expectedAiDelay = aiDelay;
                    expectedHumanDistance = humanDistance;
                    expectedHumanDelay = humanDelay;
                    VerifyExpected();
                    diagnostic = Diagnostic(NativeCapabilityState.Available, "Gatehouse timing values were applied and verified.");
                    NativeApiLog.Info(log, $"capability={NativeCapabilityIds.GatehouseTiming}, build={binaryHash}, owner={ownerGuid}, enabled={settings.Enabled}, status=applied.");
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
            int oldAiDistance = memory.ReadInt32(target.AiDistance);
            int oldAiDelay = memory.ReadInt32(target.AiDelay);
            int oldHumanDistance = memory.ReadInt32(target.HumanDistance);
            int oldHumanDelay = memory.ReadInt32(target.HumanDelay);
            long first = Math.Min(Math.Min(target.AiDistance, target.AiDelay), Math.Min(target.HumanDistance, target.HumanDelay));
            long last = Math.Max(Math.Max(target.AiDistance, target.AiDelay), Math.Max(target.HumanDistance, target.HumanDelay));
            int length = checked((int)(last - first + 4));
            uint oldProtection = memory.MakeWritable(first, length);
            Exception primary = null;
            Exception cleanup = null;
            try
            {
                try
                {
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
            finally
            {
                try { memory.RestoreProtection(first, length, oldProtection); }
                catch (Exception ex) { cleanup = ex; }
                try { memory.Flush(first, length); }
                catch (Exception ex) { cleanup = cleanup == null ? ex : new AggregateException(cleanup, ex); }
            }

            if (primary != null && cleanup != null)
                throw new AggregateException("The gatehouse transaction and cleanup both failed.", primary, cleanup);
            if (primary != null)
                throw primary;
            if (cleanup != null)
                throw cleanup;
        }

        private void VerifyExpected()
        {
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

        private static int Convert(double value, int multiplier) =>
            checked((int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero));

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
