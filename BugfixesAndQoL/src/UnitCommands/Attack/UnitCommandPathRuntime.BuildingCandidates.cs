using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        internal void ObserveBuildingApproachBuilder(
            IntPtr pathManager,
            int tribeId,
            int buildingId,
            int requestedResults,
            int sourceRegion,
            int movementClass)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            IEnemyGatePathPolicy gate = BeginEnemyGateSearch(
                ResolveEnemyGateBuildingPlayer(movementClass, tribeId),
                EnemyGateSearchKind.BuildingApproach, out object gateScope);
            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("BuildingApproach", movementClass);
#if SERP_DEVDIAG
            object temporaryScope = BeginTemporaryAssassinScope("building-query", tribeId, movementClass, buildingId, sourceRegion);
#endif

            bool bridgeCompleted = false;
            try
            {
                ObserveEnemyGateAssassinBuilding(gate, tribeId, buildingId, sourceRegion, movementClass);
                ObserveBuildingApproachBuilderWithMoat(
                    pathManager, tribeId, buildingId, requestedResults,
                    sourceRegion, movementClass);
                bridgeCompleted = true;
            }
            finally
            {
#if SERP_DEVDIAG
                EndTemporaryAssassinScope(temporaryScope, bridgeCompleted, 0);
#endif

                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, bridgeCompleted, 0);
                EndEnemyGateSearch(gate, gateScope,
                    EnemyGateSearchKind.BuildingApproach, bridgeCompleted, false);
                Shared.DevDiag.StopSpike(ref measured, "uc.attack.buildingApproach", 20000);
            }
        }

        internal void ObserveBuildingApproachBuilderWithMoat(
            IntPtr pathManager, int tribeId, int buildingId, int requestedResults,
            int sourceRegion, int movementClass)
        {
            // The approach scope only serves friendly-moat routes (MoatMove).
            if (TraversalEnabled)
                Traversal.BuildingApproachBuilderTraversal(
                    pathManager, tribeId, buildingId, requestedResults, sourceRegion, movementClass);
            else
                RunBuildingApproachBuilderWithVanillaLadderFix(
                    pathManager, tribeId, buildingId, requestedResults, sourceRegion, movementClass);
        }

        internal void ObserveBuildingCandidateConsumer(
            IntPtr tribeManager, int tribeId, int builderVariant)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            IEnemyGatePathPolicy gate = BeginEnemyGateSearch(
                ResolveEnemyGateTribePlayer(tribeId),
                EnemyGateSearchKind.BuildingConsumer, out object gateScope);
            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("BuildingConsumer", -1);
            bool bridgeCompleted = false;
            try
            {
                ObserveBuildingCandidateConsumerWithMoat(
                    tribeManager, tribeId, builderVariant);
                bridgeCompleted = true;
            }
            finally
            {
                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, bridgeCompleted, 0);
                EndEnemyGateSearch(gate, gateScope,
                    EnemyGateSearchKind.BuildingConsumer, bridgeCompleted, false);
                Shared.DevDiag.StopSpike(ref measured, "uc.attack.buildingConsumer", 20000);
            }
        }

        internal void ObserveBuildingCandidateConsumerWithMoat(
            IntPtr tribeManager, int tribeId, int builderVariant)
        {
            // The consumer scope and its candidate fallback only serve friendly-moat routes
            // (MoatMove). Without the traversal provider: Vanilla plus the ladder fix.
            if (TraversalEnabled)
                Traversal.BuildingCandidateConsumerTraversal(tribeManager, tribeId, builderVariant);
            else
                RunBuildingCandidateConsumerWithVanillaLadderFix(tribeManager, tribeId, builderVariant);
        }

        // Restores the ladder candidates Vanilla's consumer dropped (same steps as the
        // fast branch above, without any friendly-moat scope).
        internal void RunBuildingCandidateConsumerWithVanillaLadderFix(
            IntPtr tribeManager, int tribeId, int builderVariant)
        {
            AttackCommandScope command = activeAttackCommand;
            bool ladder = !disposed && tribeManager != IntPtr.Zero && command != null &&
                command.MapEpoch == mapEpoch && command.TribeId == tribeId &&
                IsBuildingAttackCommand(command.Command) &&
                settings.EnableMod && settings.EnableLadderAttackPathfindingFix;
            BuildingApproachCandidate[] vanillaCandidates = Array.Empty<BuildingApproachCandidate>();
            if (ladder)
            {
                try { vanillaCandidates = CaptureBuildingApproachCandidates(nativePathManager); }
                catch (Exception ex)
                {
                    ladder = false;
                    TryLogDiagnosticFailure("building-consumer-ladder-capture", ex);
                }
            }
            originalBuildingCandidateConsumer(tribeManager, tribeId, builderVariant);
            if (!ladder)
                return;
            try
            {
                LadderBuildingCandidateRestoreResult ladderRestore =
                    RestoreVanillaLadderBuildingCandidates(command, tribeManager, vanillaCandidates);
                if (ladderRestore.RestoredPairs > 0)
                    LogCommandDiagnostic(
                        $"stage=building-consumer-ladder commandSeq={command.Sequence} " +
                        $"building={command.TargetValue1}/{command.TargetValue2} " +
                        $"proofs={ladderRestore.ProvenTransitions} " +
                        $"eligible={ladderRestore.EligiblePairs} " +
                        $"ladderRestored={ladderRestore.RestoredPairs} " +
                        $"reason={ladderRestore.Reason}.");
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("building-consumer-ladder", ex);
            }
        }

        internal bool IsLegalBuildingCandidate(AttackCommandScope command, GameBuilding* building,
            int player, BuildingApproachCandidate candidate)
        {
            int tile=candidate.ApproachTileId;
            if (tile <= 0 || !IsValidTileId(tile) || candidate.FootprintTileId<0 ||
                (IsCompletedMoatTile(tile) && ResolveCompletedMoatRelationship(player,tile)!=CompletedMoatRelationship.Friendly)) return false;
            if (candidate.FootprintTileId!=0)
                return IsValidBuildingApproachPair(command.TargetValue1,building,tile,candidate.FootprintTileId);
            var pos=GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            return pos.X<MapWidth && pos.Y<MapWidth && movementTargetAvailability[pos.Y*MapWidth+pos.X]!=0;
        }

        internal struct BuildingApproachCandidate
        {
            public BuildingApproachCandidate(int approachTileId, int footprintTileId, int score)
            {
                ApproachTileId = approachTileId;
                FootprintTileId = footprintTileId;
                Score = score;
                ApproachX = -1;
                ApproachY = -1;
                TargetRegion = 0;
                OriginalOrder = -1;
            }

            public int ApproachTileId;
            public int FootprintTileId;
            public int Score;
            public int ApproachX;
            public int ApproachY;
            public int TargetRegion;
            public int OriginalOrder;
        }

    }
}
