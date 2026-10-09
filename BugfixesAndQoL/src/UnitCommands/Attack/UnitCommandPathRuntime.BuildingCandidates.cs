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
            IEnemyGatePathPolicy gate = BeginEnemyGateSearch(
                ResolveEnemyGateBuildingPlayer(movementClass, tribeId),
                EnemyGateSearchKind.BuildingApproach, out object gateScope);
            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("BuildingApproach", movementClass);
            object temporaryScope = BeginTemporaryAssassinScope("building-query", tribeId, movementClass, buildingId, sourceRegion);
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
                EndTemporaryAssassinScope(temporaryScope, bridgeCompleted, 0);
                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, bridgeCompleted, 0);
                EndEnemyGateSearch(gate, gateScope,
                    EnemyGateSearchKind.BuildingApproach, true, false);
            }
        }

        internal void ObserveBuildingApproachBuilderWithMoat(
            IntPtr pathManager, int tribeId, int buildingId, int requestedResults,
            int sourceRegion, int movementClass)
        {
            AttackApproachDiagnosticScope previous = activeAttackApproachDiagnostic;
            BuildingApproachPerformanceScope previousPerformance =
                activeBuildingApproachPerformance;
            AttackApproachDiagnosticScope scope = null;
            BuildingApproachPerformanceScope performance = null;
            long started = 0;
            try
            {
                AttackCommandScope command = activeAttackCommand;
                if (!disposed && pathManager != IntPtr.Zero && command != null &&
                    command.MapEpoch == mapEpoch && command.TribeId == tribeId &&
                    IsBuildingAttackCommand(command.Command) &&
                    !command.Options.RequiredOnly)
                {
                    EnsureAttackCommandCandidates(command);
                    ResolveAttackApproachRepresentative(
                        command, out int unitId, out int playerId, out eChimps unitType);
                    scope = new AttackApproachDiagnosticScope(
                        command,
                        AttackApproachKind.BuildingApproach,
                        command.Sequence,
                        command.Command,
                        tribeId,
                        buildingId,
                        -1,
                        -1,
                        requestedResults,
                        sourceRegion,
                        movementClass,
                        unitId,
                        playerId,
                        unitType,
                        CaptureAttackApproachState(
                            pathManager,
                            requirePairedResult: true,
                            requireReachableScore: false));
                    performance = new BuildingApproachPerformanceScope(
                        command.Sequence, buildingId);
                    activeAttackApproachDiagnostic = scope;
                    activeBuildingApproachPerformance = performance;
                }
            }
            catch (Exception ex)
            {
                activeAttackApproachDiagnostic = previous;
                activeBuildingApproachPerformance = previousPerformance;
                TryLogDiagnosticFailure("attack-approach-building-pre", ex);
                scope = null;
            }

            try
            {
                started = Stopwatch.GetTimestamp();
                RunBuildingApproachBuilderWithVanillaLadderFix(
                    pathManager, tribeId, buildingId, requestedResults, sourceRegion, movementClass);
            }
            finally
            {
                if (performance != null && started != 0)
                    performance.TotalElapsedTicks = Stopwatch.GetTimestamp() - started;
                if (scope != null)
                {
                    try
                    {
                        scope.After = CaptureAttackApproachState(
                            pathManager,
                            requirePairedResult: true,
                            requireReachableScore: false);
                        LogAttackApproachDiagnostic(scope);
                        LogBuildingApproachPerformance(performance);
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure("attack-approach-building-post", ex);
                    }
                }
                activeBuildingApproachPerformance = previousPerformance;
                activeAttackApproachDiagnostic = previous;
            }
        }

        internal void ObserveBuildingCandidateConsumer(
            IntPtr tribeManager, int tribeId, int builderVariant)
        {
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
                    EnemyGateSearchKind.BuildingConsumer, true, false);
            }
        }

        internal void ObserveBuildingCandidateConsumerWithMoat(
            IntPtr tribeManager, int tribeId, int builderVariant)
        {
            AttackApproachDiagnosticScope previous = activeAttackApproachDiagnostic;
            BuildingConsumerPerformanceScope previousPerformance =
                activeBuildingConsumerPerformance;
            AttackApproachDiagnosticScope scope = null;
            BuildingConsumerPerformanceScope performance = null;
            BuildingApproachCandidate[] vanillaCandidates = Array.Empty<BuildingApproachCandidate>();
            AttackCommandScope fastCommand = null;
            AttackApproachState fastBefore = default;
            bool vanillaCompleted = false;
            long vanillaStarted = 0;
            long vanillaElapsedTicks = 0;
            try
            {
                AttackCommandScope command = activeAttackCommand;
                if (!disposed && tribeManager != IntPtr.Zero && command != null &&
                    command.MapEpoch == mapEpoch && command.TribeId == tribeId &&
                    IsBuildingAttackCommand(command.Command))
                {
                    vanillaCandidates = CaptureBuildingApproachCandidates(nativePathManager);
                    if (command.Options.RequiredOnly)
                    {
                        fastCommand = command;
                        fastBefore = CaptureAttackApproachState(
                            nativePathManager,
                            requirePairedResult: true,
                            requireReachableScore: false);
                    }
                    else
                    {
                        EnsureAttackCommandCandidates(command);
                        ResolveAttackApproachRepresentative(
                            command, out int unitId, out int playerId, out eChimps unitType);
                        scope = new AttackApproachDiagnosticScope(
                            command,
                            AttackApproachKind.BuildingCandidateConsumer,
                            command.Sequence,
                            command.Command,
                            tribeId,
                            command.TargetValue1,
                            -1,
                            -1,
                            -1,
                            -1,
                            -1,
                            unitId,
                            playerId,
                            unitType,
                            CaptureAttackApproachState(
                                nativePathManager,
                                requirePairedResult: true,
                                requireReachableScore: false))
                        {
                            ConsumerVariant = builderVariant
                        };
                        performance = new BuildingConsumerPerformanceScope(
                            command.Sequence, command.TargetValue1, vanillaCandidates.Length);
                        activeAttackApproachDiagnostic = scope;
                    }
                }
            }
            catch (Exception ex)
            {
                activeAttackApproachDiagnostic = previous;
                TryLogDiagnosticFailure("attack-approach-building-consumer-pre", ex);
                scope = null;
            }

            try
            {
                vanillaStarted = Stopwatch.GetTimestamp();
                originalBuildingCandidateConsumer(tribeManager, tribeId, builderVariant);
                vanillaCompleted = true;
            }
            finally
            {
                if (vanillaStarted != 0)
                {
                    vanillaElapsedTicks = Stopwatch.GetTimestamp() - vanillaStarted;
                    if (performance != null)
                        performance.VanillaElapsedTicks = vanillaElapsedTicks;
                }
                if (scope == null && vanillaCompleted && fastCommand != null &&
                    CaptureAttackApproachState(nativePathManager, requirePairedResult: true)
                        .UsableResultCount == 0)
                {
                    EnsureAttackCommandCandidates(fastCommand);
                    ResolveAttackApproachRepresentative(
                        fastCommand, out int unitId, out int playerId, out eChimps unitType);
                    scope = new AttackApproachDiagnosticScope(
                        fastCommand, AttackApproachKind.BuildingCandidateConsumer,
                        fastCommand.Sequence, fastCommand.Command, tribeId,
                        fastCommand.TargetValue1, -1, -1, -1, -1, -1,
                        unitId, playerId, unitType, fastBefore)
                    {
                        ConsumerVariant = builderVariant
                    };
                    performance = new BuildingConsumerPerformanceScope(
                        fastCommand.Sequence, fastCommand.TargetValue1,
                        vanillaCandidates.Length)
                    {
                        VanillaElapsedTicks = vanillaElapsedTicks
                    };
                    activeAttackApproachDiagnostic = scope;
                }
                if (scope != null)
                {
                    try
                    {
                        AttackApproachState vanillaAfter =
                            CaptureAttackApproachState(nativePathManager, requirePairedResult: true);
                        BuildingConsumerFallbackResult fallback;
                        LadderBuildingCandidateRestoreResult ladderRestore;
                        long fallbackStarted = Stopwatch.GetTimestamp();
                        activeBuildingConsumerPerformance = performance;
                        try
                        {
                            fallback = vanillaCompleted
                                ? TryApplyBuildingConsumerFallback(
                                    scope, tribeManager, vanillaCandidates, vanillaAfter)
                                : BuildingConsumerFallbackResult.NotAttempted("vanilla-threw");
                        }
                        finally
                        {
                            if (performance != null)
                            {
                                performance.FallbackElapsedTicks =
                                    Stopwatch.GetTimestamp() - fallbackStarted;
                            }
                            activeBuildingConsumerPerformance = previousPerformance;
                        }
                        ladderRestore = vanillaCompleted
                            ? RestoreVanillaLadderBuildingCandidates(
                                scope.OwnerCommand, tribeManager, vanillaCandidates)
                            : new LadderBuildingCandidateRestoreResult(
                                "vanilla-threw", 0, 0, 0, 0);
                        PublishBuildingApproachPairs(scope.OwnerCommand, nativePathManager);
                        scope.After = CaptureAttackApproachState(
                            nativePathManager,
                            requirePairedResult: true,
                            requireReachableScore: false);
                        LogBuildingConsumerCandidates(
                            scope, vanillaCandidates, vanillaAfter, fallback, ladderRestore);
                        LogBuildingConsumerPerformance(performance, fallback);
                        LogAttackApproachDiagnostic(scope);
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure("attack-approach-building-consumer-post", ex);
                    }
                }
                else if (vanillaCompleted && fastCommand != null)
                {
                    try
                    {
                        LadderBuildingCandidateRestoreResult ladderRestore =
                            RestoreVanillaLadderBuildingCandidates(
                                fastCommand, tribeManager, vanillaCandidates);
                        if (ladderRestore.RestoredPairs > 0)
                        {
                            PublishBuildingApproachPairs(fastCommand, nativePathManager);
                            LogCommandDiagnostic(
                                $"stage=building-consumer-ladder commandSeq={fastCommand.Sequence} " +
                                $"building={fastCommand.TargetValue1}/{fastCommand.TargetValue2} " +
                                $"proofs={ladderRestore.ProvenTransitions} " +
                                $"afterMoatPairs={ladderRestore.AfterMoatPairs} " +
                                $"eligible={ladderRestore.EligiblePairs} " +
                                $"ladderRestored={ladderRestore.RestoredPairs} " +
                                $"reason={ladderRestore.Reason}.");
                        }
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure(
                            "attack-approach-building-consumer-ladder-fast", ex);
                    }
                }
                activeBuildingConsumerPerformance = previousPerformance;
                activeAttackApproachDiagnostic = previous;
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

        internal bool BuildingCandidateEdge(int player,int from,int to,int d,bool endpoint,bool reserved,
            out bool moat,out bool structure)
        {
            bool valid=weightedMoatRoutePlanner.TryGetTraversalEdge(player,from%MapWidth,from/MapWidth,
                GameTileManagerAPI.Instance.GetTileId(from%MapWidth,from/MapWidth),to%MapWidth,to/MapWidth,
                GameTileManagerAPI.Instance.GetTileId(to%MapWidth,to/MapWidth),d,endpoint,reserved,
                MoatTraversalPolicy.FriendlyOnly,out MoatTraversalEdgeKind kind,out structure);
            moat=kind!=MoatTraversalEdgeKind.Ground; return valid;
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
