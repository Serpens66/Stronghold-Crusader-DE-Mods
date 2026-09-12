using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        private readonly struct LadderRegionTransition : IEquatable<LadderRegionTransition>
        {
            public LadderRegionTransition(int playerId, int sourceRegion, int targetRegion)
            {
                PlayerId = playerId;
                SourceRegion = sourceRegion;
                TargetRegion = targetRegion;
            }

            public int PlayerId { get; }
            public int SourceRegion { get; }
            public int TargetRegion { get; }

            public bool Equals(LadderRegionTransition other) =>
                PlayerId == other.PlayerId && SourceRegion == other.SourceRegion &&
                TargetRegion == other.TargetRegion;

            public override bool Equals(object obj) =>
                obj is LadderRegionTransition other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = (hash * 397) ^ SourceRegion;
                    return (hash * 397) ^ TargetRegion;
                }
            }
        }

        private readonly struct LadderBuildingCandidateRestoreResult
        {
            public LadderBuildingCandidateRestoreResult(
                string reason,
                int provenTransitions,
                int afterMoatPairs,
                int eligiblePairs,
                int restoredPairs)
            {
                Reason = reason;
                ProvenTransitions = provenTransitions;
                AfterMoatPairs = afterMoatPairs;
                EligiblePairs = eligiblePairs;
                RestoredPairs = restoredPairs;
            }

            public string Reason { get; }
            public int ProvenTransitions { get; }
            public int AfterMoatPairs { get; }
            public int EligiblePairs { get; }
            public int RestoredPairs { get; }
        }

        private sealed class LadderAttackProbeScope
        {
            public LadderAttackProbeScope(
                AttackApproachKind kind,
                int commandSequence,
                int tribeId,
                int leadUnitId,
                int buildingId,
                int targetX,
                int targetY,
                int sourceRegion,
                int playerId,
                int groupMovementMode)
            {
                Kind = kind;
                CommandSequence = commandSequence;
                TribeId = tribeId;
                LeadUnitId = leadUnitId;
                BuildingId = buildingId;
                TargetX = targetX;
                TargetY = targetY;
                SourceRegion = sourceRegion;
                PlayerId = playerId;
                GroupMovementMode = groupMovementMode;
            }

            public AttackApproachKind Kind { get; }
            public int CommandSequence { get; }
            public int TribeId { get; }
            public int LeadUnitId { get; }
            public int BuildingId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int SourceRegion { get; }
            public int PlayerId { get; }
            public int GroupMovementMode { get; }
            public int ModeRetryCount { get; set; }
            public int PositiveModeRetryCount { get; set; }
            public int LastModeRetryResult { get; set; }
        }

        private void RunAttackApproachFloodWithVanillaLadderFix(
            IntPtr pathManager,
            int tribeId,
            int targetContext,
            uint targetX,
            uint targetY,
            int requestedResults,
            int sourceRegion,
            int playerId)
        {
            LadderAttackProbeScope probe = TryCreateLadderAttackProbe(
                AttackApproachKind.UnitFlood,
                pathManager,
                tribeId,
                targetContext,
                unchecked((int)targetX),
                unchecked((int)targetY),
                sourceRegion,
                playerId,
                out string rejectionReason);
            if (probe == null)
            {
                LogVanillaLadderProbeNotCreated(
                    AttackApproachKind.UnitFlood, tribeId, rejectionReason);
                originalAttackApproachFloodBuilder(
                    pathManager, tribeId, targetContext, targetX, targetY,
                    requestedResults, sourceRegion, playerId);
                return;
            }

            LadderAttackProbeScope previous = activeLadderAttackProbe;
            activeLadderAttackProbe = probe;
            try
            {
                // The attack builder hard-codes route mode 0. Its scoped E2610 calls are
                // retried in place with the exact mode selected by ordinary MoveHere.
                originalAttackApproachFloodBuilder(
                    pathManager, tribeId, targetContext, targetX, targetY,
                    requestedResults, sourceRegion, playerId);
                if (DetailedDiagnosticsEnabled)
                {
                    LogVanillaGroupModeResult(
                        probe, CaptureAttackApproachState(pathManager));
                }
            }
            finally
            {
                activeLadderAttackProbe = previous;
            }
        }

        private void RunBuildingApproachBuilderWithVanillaLadderFix(
            IntPtr pathManager,
            int tribeId,
            int buildingId,
            int requestedResults,
            int sourceRegion,
            int playerId)
        {
            LadderAttackProbeScope probe = TryCreateLadderAttackProbe(
                AttackApproachKind.BuildingApproach,
                pathManager,
                tribeId,
                buildingId,
                -1,
                -1,
                sourceRegion,
                playerId,
                out string rejectionReason);
            if (probe == null)
            {
                LogVanillaLadderProbeNotCreated(
                    AttackApproachKind.BuildingApproach, tribeId, rejectionReason);
                originalBuildingApproachBuilder(
                    pathManager, tribeId, buildingId, requestedResults, sourceRegion, playerId);
                return;
            }

            LadderAttackProbeScope previous = activeLadderAttackProbe;
            activeLadderAttackProbe = probe;
            try
            {
                originalBuildingApproachBuilder(
                    pathManager, tribeId, buildingId, requestedResults, sourceRegion, playerId);
                if (DetailedDiagnosticsEnabled)
                {
                    LogVanillaGroupModeResult(
                        probe,
                        CaptureAttackApproachState(
                            pathManager,
                            requirePairedResult: true,
                            requireReachableScore: false));
                }
            }
            finally
            {
                activeLadderAttackProbe = previous;
            }
        }

        private LadderAttackProbeScope TryCreateLadderAttackProbe(
            AttackApproachKind kind,
            IntPtr pathManager,
            int tribeId,
            int targetContext,
            int targetX,
            int targetY,
            int sourceRegion,
            int playerId,
            out string rejectionReason)
        {
            rejectionReason = "none";
            if (disposed)
            {
                rejectionReason = "disposed";
                return null;
            }
            if (!settings.EnableMod || !settings.EnableLadderAttackPathfindingFix)
            {
                rejectionReason = "setting-disabled";
                return null;
            }
            if (pathManager == IntPtr.Zero || pathManager != nativePathManager)
            {
                rejectionReason = "path-manager-mismatch";
                return null;
            }
            if (tribeId < 0 || tribeId >= MaximumTribeCount ||
                sourceRegion <= 0 || sourceRegion > MaximumRegionId ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                getTribeMovementMode == null || originalRegionPairReachability == null)
            {
                rejectionReason = "invalid-native-context";
                return null;
            }

            AttackCommandScope command = activeAttackCommand;
            bool commandMatches = kind == AttackApproachKind.UnitFlood
                ? command != null && command.Command == TribeAICommand.AttackUnit &&
                    command.TargetValue1 == targetContext
                : command != null && IsBuildingAttackCommand(command.Command) &&
                    command.TargetValue1 == targetContext;
            if (!commandMatches || command.MapEpoch != mapEpoch || command.TribeId != tribeId)
            {
                rejectionReason = "attack-command-mismatch";
                return null;
            }

            byte* tribe = (byte*)nativeTribeManager.ToPointer() + tribeId * TribeRecordSize;
            int leadUnitId = *(ushort*)(tribe + TribeLeadUnitIdOffset);
            if (leadUnitId <= 0 || leadUnitId > MaximumUnitCount ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(leadUnitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_TribeId != tribeId || unit->r_ControllableForPlayerId != playerId)
            {
                rejectionReason = "invalid-leader-unit";
                return null;
            }
            GetNativeMovementStart(unit, out int startX, out int startY);
            if ((uint)startX >= MapWidth || (uint)startY >= MapWidth)
            {
                rejectionReason = "invalid-leader-position";
                return null;
            }
            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            if (!IsValidTileId(startTileId) || pathRegionGrid[startTileId] != sourceRegion)
            {
                rejectionReason = "leader-source-region-mismatch";
                return null;
            }

            int groupMovementMode = getTribeMovementMode(nativeTribeManager, tribeId);
            if (groupMovementMode <= 0)
            {
                rejectionReason = "vanilla-group-mode-zero";
                return null;
            }

            rejectionReason = "ready";
            return new LadderAttackProbeScope(
                kind,
                command.Sequence,
                tribeId,
                leadUnitId,
                kind == AttackApproachKind.BuildingApproach ? targetContext : 0,
                targetX,
                targetY,
                sourceRegion,
                playerId,
                groupMovementMode);
        }

        private bool TryHandleVanillaLadderRegionPair(
            IntPtr pathManager,
            int playerId,
            int sourceRegion,
            int targetRegion,
            int routeKind,
            int vanillaResult,
            out int effectiveResult)
        {
            effectiveResult = vanillaResult;
            LadderAttackProbeScope probe = activeLadderAttackProbe;
            if (probe == null || pathManager != nativePathManager || vanillaResult != 0 ||
                playerId != probe.PlayerId || sourceRegion != probe.SourceRegion ||
                sourceRegion <= 0 || sourceRegion > MaximumRegionId ||
                targetRegion <= 0 || targetRegion > MaximumRegionId || routeKind != 0 ||
                probe.GroupMovementMode <= 0 || originalRegionPairReachability == null)
            {
                return false;
            }

            // Call the E2610 trampoline directly: this preserves Vanilla's exact return
            // value and graph-state writes without recursing through the shared detour.
            probe.ModeRetryCount++;
            int groupModeResult = originalRegionPairReachability(
                pathManager,
                playerId,
                sourceRegion,
                targetRegion,
                probe.GroupMovementMode);
            probe.LastModeRetryResult = groupModeResult;
            if (groupModeResult == 0)
                return false;

            probe.PositiveModeRetryCount++;
            if (probe.Kind == AttackApproachKind.BuildingApproach)
            {
                AttackCommandScope command = activeAttackCommand;
                if (command != null && command.Sequence == probe.CommandSequence &&
                    command.MapEpoch == mapEpoch && command.TribeId == probe.TribeId &&
                    IsBuildingAttackCommand(command.Command) &&
                    command.TargetValue1 == probe.BuildingId)
                {
                    command.PositiveLadderRegionTransitions.Add(
                        new LadderRegionTransition(playerId, sourceRegion, targetRegion));
                }
            }
            effectiveResult = groupModeResult;
            return true;
        }

        private LadderBuildingCandidateRestoreResult RestoreVanillaLadderBuildingCandidates(
            AttackCommandScope command,
            IntPtr tribeManager,
            BuildingApproachCandidate[] producerCandidates)
        {
            if (disposed || !settings.EnableMod ||
                !settings.EnableLadderAttackPathfindingFix || command == null ||
                command != activeAttackCommand || command.MapEpoch != mapEpoch ||
                !IsBuildingAttackCommand(command.Command) ||
                tribeManager == IntPtr.Zero || tribeManager != nativeTribeManager ||
                nativePathManager == IntPtr.Zero)
            {
                return new LadderBuildingCandidateRestoreResult(
                    "invalid-command-scope", 0, 0, 0, 0);
            }

            int proofCount = command.PositiveLadderRegionTransitions.Count;
            BuildingApproachCandidate[] retained =
                CaptureBuildingApproachCandidates(nativePathManager);
            int retainedPairCount = 0;
            var retainedPairs = new HashSet<ulong>();
            var combined = new List<BuildingApproachCandidate>(retained);
            foreach (BuildingApproachCandidate candidate in retained)
            {
                if (candidate.ApproachTileId > 0 && candidate.FootprintTileId > 0)
                {
                    retainedPairCount++;
                    retainedPairs.Add(GetBuildingApproachPairKey(candidate));
                }
            }

            if (proofCount == 0 || producerCandidates == null || producerCandidates.Length == 0)
            {
                return new LadderBuildingCandidateRestoreResult(
                    proofCount == 0 ? "no-positive-region-proof" : "no-producer-candidates",
                    proofCount, retainedPairCount, 0, 0);
            }

            int eligible = 0;
            int restored = 0;
            int maximumCount = VanillaAttackFloodResultCapacity - 1;
            foreach (BuildingApproachCandidate producer in producerCandidates)
            {
                if (producer.ApproachTileId <= 0 || producer.FootprintTileId <= 0 ||
                    !IsValidTileId(producer.ApproachTileId))
                {
                    continue;
                }

                ulong pairKey = GetBuildingApproachPairKey(producer);
                if (retainedPairs.Contains(pairKey))
                    continue;

                int targetRegion = pathRegionGrid[producer.ApproachTileId];
                bool provenAndLegal = false;
                foreach (LadderRegionTransition transition in
                    command.PositiveLadderRegionTransitions)
                {
                    if (transition.SourceRegion <= 0 ||
                        transition.TargetRegion != targetRegion ||
                        !TryValidateHostileBuildingTarget(
                            command.TargetValue1,
                            unchecked((uint)command.TargetValue2),
                            transition.PlayerId,
                            out GameBuilding* building) ||
                        !IsLegalBuildingCandidate(command, building, transition.PlayerId, producer))
                    {
                        continue;
                    }

                    provenAndLegal = true;
                    break;
                }

                if (!provenAndLegal)
                    continue;

                eligible++;
                if (combined.Count >= maximumCount)
                    continue;

                BuildingApproachCandidate restoredCandidate = producer;
                // 123090 assigned this exact Vanilla sentinel before compacting the pair.
                // The following attack dispatcher consumes only approach and footprint.
                restoredCandidate.Score = VanillaUnreachableCandidateScore;
                combined.Add(restoredCandidate);
                retainedPairs.Add(pairKey);
                restored++;
            }

            if (restored == 0)
            {
                return new LadderBuildingCandidateRestoreResult(
                    eligible == 0 ? "no-proven-candidate-pair" : "candidate-buffer-full",
                    proofCount, retainedPairCount, eligible, 0);
            }

            BuildingApproachCandidate[] snapshot =
                CaptureBuildingApproachBuffer(nativePathManager);
            try
            {
                WriteBuildingApproachCandidates(nativePathManager, combined);
            }
            catch
            {
                RestoreBuildingApproachBuffer(nativePathManager, snapshot);
                throw;
            }

            return new LadderBuildingCandidateRestoreResult(
                "restored", proofCount, retainedPairCount, eligible, restored);
        }

        private static ulong GetBuildingApproachPairKey(BuildingApproachCandidate candidate) =>
            ((ulong)(uint)candidate.ApproachTileId << 32) |
            (uint)candidate.FootprintTileId;

        private void LogVanillaGroupModeResult(
            LadderAttackProbeScope probe,
            AttackApproachState result)
        {
            if (probe.ModeRetryCount == 0)
                return;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"MoatMove Vanilla ladder attack group-mode result: " +
                $"commandSeq={probe.CommandSequence}, kind={probe.Kind}, " +
                $"tribe={probe.TribeId}, leadUnitId={probe.LeadUnitId}, " +
                $"building={probe.BuildingId}, groupMode={probe.GroupMovementMode}, " +
                $"modeRetries={probe.ModeRetryCount}, " +
                $"positiveModeRetries={probe.PositiveModeRetryCount}, " +
                $"lastModeResult={probe.LastModeRetryResult}, " +
                $"results={result.UsableResultCount}.");
        }

        private void LogVanillaLadderProbeNotCreated(
            AttackApproachKind kind,
            int tribeId,
            string reason)
        {
            if (!DetailedDiagnosticsEnabled || activeAttackCommand == null)
                return;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"MoatMove Vanilla ladder attack scope not created: " +
                $"commandSeq={activeAttackCommand.Sequence}, kind={kind}, tribe={tribeId}, " +
                $"reason={reason}, setting={settings.EnableLadderAttackPathfindingFix}.");
        }
    }
}
