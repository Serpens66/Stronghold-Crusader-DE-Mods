using SHCDESE.Interop;
using System;

namespace StockpileAccessFixTest
{
    internal enum StockpileEpisodeAction
    {
        None,
        CandidateStarted,
        ConfirmAndRepair,
        Progress,
        Verified,
        Unverified
    }

    internal readonly struct StockpileWorkerContract
    {
        internal StockpileWorkerContract(eChimps unitType, ushort fetchState, int handlerRva)
        {
            UnitType = unitType;
            FetchState = fetchState;
            HandlerRva = handlerRva;
        }

        internal eChimps UnitType { get; }
        internal ushort FetchState { get; }
        internal int HandlerRva { get; }
    }

    internal static class StockpileWorkerContracts
    {
        internal static readonly StockpileWorkerContract[] All =
        {
            new StockpileWorkerContract(eChimps.CHIMP_TYPE_FLETCHER, 1, StockpileAccessFixNativeDefinition.FletcherHandlerRva),
            new StockpileWorkerContract(eChimps.CHIMP_TYPE_MILLER, 3, StockpileAccessFixNativeDefinition.MillerHandlerRva),
            new StockpileWorkerContract(eChimps.CHIMP_TYPE_BAKER, 7, StockpileAccessFixNativeDefinition.BakerHandlerRva),
            new StockpileWorkerContract(eChimps.CHIMP_TYPE_BREWER, 2, StockpileAccessFixNativeDefinition.BrewerHandlerRva),
            new StockpileWorkerContract(eChimps.CHIMP_TYPE_POLETURNER, 2, StockpileAccessFixNativeDefinition.PoleturnerHandlerRva),
            new StockpileWorkerContract(eChimps.CHIMP_TYPE_BLACKSMITH, 2, StockpileAccessFixNativeDefinition.BlacksmithHandlerRva),
            new StockpileWorkerContract(eChimps.CHIMP_TYPE_ARMOURER, 2, StockpileAccessFixNativeDefinition.ArmourerHandlerRva),
            new StockpileWorkerContract(eChimps.CHIMP_TYPE_INNKEEPER, 2, StockpileAccessFixNativeDefinition.InnkeeperHandlerRva)
        };

        internal static bool TryGet(eChimps unitType, out StockpileWorkerContract contract)
        {
            foreach (StockpileWorkerContract candidate in All)
            {
                if (candidate.UnitType != unitType)
                    continue;

                contract = candidate;
                return true;
            }

            contract = default;
            return false;
        }
    }

    internal readonly struct StockpileObservation
    {
        internal StockpileObservation(
            int unitId,
            uint unitGlobalId,
            eChimps unitType,
            ushort state,
            bool alive,
            bool supportedFetchState,
            bool ownedStockpile,
            bool storageGenerationMatches,
            ushort pathFlags,
            ushort alternatePathConnectionId,
            ushort currentX,
            ushort currentY,
            ushort targetX,
            ushort targetY,
            ushort entryX,
            ushort entryY,
            ushort storageBuildingId,
            ushort productionBuildingId)
        {
            UnitId = unitId;
            UnitGlobalId = unitGlobalId;
            UnitType = unitType;
            State = state;
            Alive = alive;
            SupportedFetchState = supportedFetchState;
            OwnedStockpile = ownedStockpile;
            StorageGenerationMatches = storageGenerationMatches;
            PathFlags = pathFlags;
            AlternatePathConnectionId = alternatePathConnectionId;
            CurrentX = currentX;
            CurrentY = currentY;
            TargetX = targetX;
            TargetY = targetY;
            EntryX = entryX;
            EntryY = entryY;
            StorageBuildingId = storageBuildingId;
            ProductionBuildingId = productionBuildingId;
        }

        internal int UnitId { get; }
        internal uint UnitGlobalId { get; }
        internal eChimps UnitType { get; }
        internal ushort State { get; }
        internal bool Alive { get; }
        internal bool SupportedFetchState { get; }
        internal bool OwnedStockpile { get; }
        internal bool StorageGenerationMatches { get; }
        internal ushort PathFlags { get; }
        internal ushort AlternatePathConnectionId { get; }
        internal ushort CurrentX { get; }
        internal ushort CurrentY { get; }
        internal ushort TargetX { get; }
        internal ushort TargetY { get; }
        internal ushort EntryX { get; }
        internal ushort EntryY { get; }
        internal ushort StorageBuildingId { get; }
        internal ushort ProductionBuildingId { get; }

        internal bool IsValidFetchRoute =>
            Alive && SupportedFetchState && OwnedStockpile && StorageGenerationMatches &&
            StorageBuildingId != 0 && EntryX != 0 && EntryY != 0 &&
            TargetX == EntryX && TargetY == EntryY;

        internal bool HasIdleBugSignature =>
            IsValidFetchRoute && PathFlags == 0 &&
            (CurrentX != TargetX || CurrentY != TargetY);

        internal bool IsSameStuckSnapshotAs(in StockpileObservation other) =>
            UnitId == other.UnitId && UnitGlobalId == other.UnitGlobalId &&
            UnitType == other.UnitType && State == other.State &&
            StorageBuildingId == other.StorageBuildingId &&
            ProductionBuildingId == other.ProductionBuildingId &&
            CurrentX == other.CurrentX && CurrentY == other.CurrentY &&
            TargetX == other.TargetX && TargetY == other.TargetY &&
            EntryX == other.EntryX && EntryY == other.EntryY &&
            PathFlags == other.PathFlags &&
            AlternatePathConnectionId == other.AlternatePathConnectionId;

        internal bool IsSameUnitSlotAs(in StockpileObservation other) =>
            UnitId == other.UnitId && UnitGlobalId == other.UnitGlobalId &&
            UnitType == other.UnitType;
    }

    internal sealed class StockpileAccessEpisodePolicy
    {
        internal const int RequiredConsecutiveTicks = 50;
        internal const int RetryCooldownTicks = 200;
        internal const int VerificationTimeoutTicks = 1200;

        private StockpileObservation candidate;
        private StockpileObservation repairStart;
        private int lastTick;
        private int consecutiveTicks;
        private int repairTick;
        private int cooldownUntilTick;
        private bool progressReported;
        private Phase phase;

        internal bool CanDiscard => phase == Phase.None && cooldownUntilTick == 0;

        internal static bool CanStartCandidate(
            in StockpileObservation observation,
            bool hasMatchingRecentlyActiveRoute) =>
            hasMatchingRecentlyActiveRoute && observation.HasIdleBugSignature;

        internal StockpileEpisodeAction Observe(in StockpileObservation observation, int tick)
        {
            if (phase == Phase.AwaitingRepairOutcome)
                return StockpileEpisodeAction.None;

            if (phase == Phase.Verifying)
                return ObserveVerification(observation, tick);

            if (cooldownUntilTick != 0)
            {
                if (tick < cooldownUntilTick)
                    return StockpileEpisodeAction.None;
                cooldownUntilTick = 0;
            }

            if (!observation.HasIdleBugSignature)
            {
                ResetTracking();
                return StockpileEpisodeAction.None;
            }

            if (phase != Phase.Tracking || tick != lastTick + 1 ||
                !candidate.IsSameStuckSnapshotAs(observation))
            {
                candidate = observation;
                lastTick = tick;
                consecutiveTicks = 1;
                phase = Phase.Tracking;
                return StockpileEpisodeAction.CandidateStarted;
            }

            candidate = observation;
            lastTick = tick;
            consecutiveTicks++;
            if (consecutiveTicks < RequiredConsecutiveTicks)
                return StockpileEpisodeAction.None;

            phase = Phase.AwaitingRepairOutcome;
            return StockpileEpisodeAction.ConfirmAndRepair;
        }

        internal void RecordRepairOutcome(in StockpileObservation observation, int tick, bool routeAccepted)
        {
            if (phase != Phase.AwaitingRepairOutcome)
                throw new InvalidOperationException("No confirmed stockpile-access episode awaits a repair outcome.");

            if (!routeAccepted)
            {
                EnterCooldown(tick);
                return;
            }

            repairStart = observation;
            repairTick = tick;
            progressReported = false;
            phase = Phase.Verifying;
        }

        internal void Cancel()
        {
            candidate = default;
            repairStart = default;
            lastTick = 0;
            consecutiveTicks = 0;
            repairTick = 0;
            cooldownUntilTick = 0;
            progressReported = false;
            phase = Phase.None;
        }

        private StockpileEpisodeAction ObserveVerification(in StockpileObservation observation, int tick)
        {
            if (!repairStart.IsSameUnitSlotAs(observation) || !observation.Alive)
            {
                EnterCooldown(tick);
                return StockpileEpisodeAction.Unverified;
            }

            // The worker may clear its stockpile link as part of the normal state transition.
            // Therefore fetch-state exit is verified before comparing storage episode details.
            if (!observation.SupportedFetchState || observation.State != repairStart.State)
            {
                Cancel();
                return StockpileEpisodeAction.Verified;
            }

            if (observation.StorageBuildingId != repairStart.StorageBuildingId ||
                !observation.OwnedStockpile || !observation.StorageGenerationMatches)
            {
                EnterCooldown(tick);
                return StockpileEpisodeAction.Unverified;
            }

            if (!progressReported &&
                (observation.CurrentX != repairStart.CurrentX ||
                 observation.CurrentY != repairStart.CurrentY ||
                 (observation.CurrentX == observation.TargetX &&
                  observation.CurrentY == observation.TargetY)))
            {
                progressReported = true;
                return StockpileEpisodeAction.Progress;
            }

            if (tick - repairTick < VerificationTimeoutTicks)
                return StockpileEpisodeAction.None;

            EnterCooldown(tick);
            return StockpileEpisodeAction.Unverified;
        }

        private void EnterCooldown(int tick)
        {
            ResetTracking();
            repairStart = default;
            repairTick = 0;
            progressReported = false;
            cooldownUntilTick = checked(tick + RetryCooldownTicks);
        }

        private void ResetTracking()
        {
            candidate = default;
            lastTick = 0;
            consecutiveTicks = 0;
            phase = Phase.None;
        }

        private enum Phase
        {
            None,
            Tracking,
            AwaitingRepairOutcome,
            Verifying
        }
    }
}
