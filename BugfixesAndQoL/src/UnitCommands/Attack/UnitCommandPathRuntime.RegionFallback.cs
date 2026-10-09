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
        internal int ObserveScopedRegionPairReachability(
            IntPtr pathManager,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind)
        {
            int result = ObserveScopedRegionPairReachabilityCore(
                pathManager, movementClass, sourceRegion, targetRegion, routeKind,
                out int vanillaResult);
            EnemyBridgeDiagnosticBridge.ObserveRegion(movementClass, sourceRegion, targetRegion, routeKind, vanillaResult, result);
            // The already-owned E2610 detour can report its result to the optional
            // test policy. Without that policy, the existing result is unchanged.
            try
            {
                IEnemyGatePathPolicy policy = EnemyGatePathPolicyBridge.Current;
                if (policy != null && policy.HasPublishedMask &&
                    policy is IEnemyGateRegionPairObserver observer &&
                    movementClass > 0 && movementClass <= 8 &&
                    pathManager == nativePathManager)
                {
                    string source = activeDirectFillCommand != null ? "direct-fill" :
                        activeAttackApproachDiagnostic != null ? "attack-approach" :
                        activeMoveCommand != null ? "tribe-move" : "other";
                    observer.ObserveRegionPair(movementClass, sourceRegion,
                        targetRegion, routeKind, vanillaResult, result, source);
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("enemy-gate-region-observer", ex);
            }
            return result;
        }

        internal int ObserveScopedRegionPairReachabilityCore(
            IntPtr pathManager,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind,
            out int vanillaResult)
        {
            vanillaResult = originalRegionPairReachability(
                pathManager, movementClass, sourceRegion, targetRegion, routeKind);
            if (TryHandleVanillaLadderRegionPair(
                    pathManager,
                    movementClass,
                    sourceRegion,
                    targetRegion,
                    routeKind,
                    vanillaResult,
                    out int ladderResult))
            {
                return ladderResult;
            }
            // E2610 uses argument 2 as player ID. Keep the established delegate ABI/name, but
            // pass the value to the work selector according to the confirmed native semantics.
            if (TryAllowDigWorkRegionPair(
                    pathManager, movementClass, sourceRegion, targetRegion, routeKind,
                    vanillaResult))
            {
                return 1;
            }
            if (TryAllowDirectCursorMoveRegionPair(
                    pathManager, movementClass, sourceRegion, targetRegion, vanillaResult) ||
                TryAllowDirectFillRegionPair(
                    pathManager, movementClass, sourceRegion, targetRegion, vanillaResult))
            {
                return 1;
            }
            MoveCommandScope moveCommand = activeMoveCommand;
            if (moveCommand != null && pathManager == nativePathManager)
            {
                try
                {
                    moveCommand.RegionCalls++;
                    string signature = $"E2610-observed:{movementClass}:{sourceRegion}:" +
                        $"{targetRegion}:{routeKind}:{vanillaResult}";
                    if (moveCommand.EarlyRegionLogSignatures.Add(signature))
                    {
                        LogCommandDiagnostic(
                            $"stage=movehere-region-pair-observed " +
                            $"commandSeq={moveCommand.Sequence} tribe={moveCommand.TribeId} " +
                            $"player={movementClass} regions={sourceRegion}->{targetRegion} " +
                            $"routeKind={routeKind} vanilla={vanillaResult} " +
                            $"effective={vanillaResult} " +
                            "decision=vanilla-graph-state-preserved");
                    }
                }
                catch
                {
                    // Observation must never alter the native reachability result.
                }
            }
            AttackApproachDiagnosticScope scope = activeAttackApproachDiagnostic;
            if (scope == null || disposed)
                return vanillaResult;

            try
            {
                scope.ObserveRegionPair(
                    movementClass, sourceRegion, targetRegion, routeKind, vanillaResult);

                if (vanillaResult == 0 && scope.Kind == AttackApproachKind.UnitFlood &&
                    scope.Command == TribeAICommand.AttackUnit)
                {
                    string decisionKey =
                        $"{movementClass}:{sourceRegion}:{targetRegion}:{routeKind}";
                    if (!scope.RegionFallbackDecisions.TryGetValue(
                            decisionKey, out AttackRegionFallbackDecision decision))
                    {
                        decision = EvaluateAttackUnitRegionFallback(
                            scope, movementClass, sourceRegion, targetRegion, routeKind);
                        scope.RegionFallbackDecisions[decisionKey] = decision;

                        string logSignature =
                            $"attack-unit-region:{scope.CommandSequence}:{decisionKey}:" +
                            $"{decision.Allowed}:{decision.Reason}:{decision.ApproachX}:" +
                            $"{decision.ApproachY}:{decision.Summary.ObservedOwnerMask}:" +
                            $"{decision.Summary.FriendlyMoatTiles}:" +
                            $"{decision.Summary.EnemyMoatTiles}";
                        if (scope.OwnerCommand.AttackApproachDiagnosticSignatures.Add(logSignature))
                        {
                            LogCommandDiagnostic(
                                $"stage=attack-unit-region-fallback " +
                                $"commandSeq={scope.CommandSequence} unit={scope.UnitId} " +
                                $"player={scope.PlayerId} targetContext={scope.TargetContext} " +
                                $"target=({scope.TargetX},{scope.TargetY}) " +
                                $"class={movementClass} regions={sourceRegion}->{targetRegion} " +
                                $"routeKind={routeKind} vanilla=0 " +
                                $"effective={(decision.Allowed ? 1 : 0)} " +
                                $"approach=({decision.ApproachX},{decision.ApproachY}) " +
                                $"reason={decision.Reason} {decision.Summary.ToLogFields()}");
                        }
                    }

                    if (decision.Allowed)
                        return 1;
                }
                else if (vanillaResult == 0 &&
                    scope.Kind == AttackApproachKind.BuildingApproach &&
                    IsBuildingAttackCommand(scope.Command))
                {
                    string decisionKey =
                        $"building:{movementClass}:{sourceRegion}:{targetRegion}:{routeKind}";
                    if (!scope.RegionFallbackDecisions.TryGetValue(
                            decisionKey, out AttackRegionFallbackDecision decision))
                    {
                        BuildingApproachPerformanceScope performance =
                            activeBuildingApproachPerformance;
                        long fallbackStarted = Stopwatch.GetTimestamp();
                        try
                        {
                            decision = EvaluateAttackBuildingRegionFallback(
                                scope, movementClass, sourceRegion, targetRegion, routeKind);
                        }
                        finally
                        {
                            if (performance != null)
                            {
                                performance.RegionFallbackEvaluations++;
                                performance.RegionFallbackElapsedTicks +=
                                    Stopwatch.GetTimestamp() - fallbackStarted;
                            }
                        }
                        scope.RegionFallbackDecisions[decisionKey] = decision;

                        string logSignature =
                            $"attack-building-region:{scope.CommandSequence}:{decisionKey}:" +
                            $"{decision.Allowed}:{decision.Reason}:{decision.ApproachX}:" +
                            $"{decision.ApproachY}:{decision.Summary.ObservedOwnerMask}:" +
                            $"{decision.Summary.FriendlyMoatTiles}:" +
                            $"{decision.Summary.EnemyMoatTiles}";
                        if (scope.OwnerCommand.AttackApproachDiagnosticSignatures.Add(logSignature))
                        {
                            LogCommandDiagnostic(
                                $"stage=building-approach-region-fallback " +
                                $"commandSeq={scope.CommandSequence} building=" +
                                $"{scope.OwnerCommand.TargetValue1}/{scope.OwnerCommand.TargetValue2} " +
                                $"unit={scope.UnitId} player={scope.PlayerId} " +
                                $"class={movementClass} regions={sourceRegion}->{targetRegion} " +
                                $"routeKind={routeKind} vanilla=0 " +
                                $"effective={(decision.Allowed ? 1 : 0)} " +
                                $"approach=({decision.ApproachX},{decision.ApproachY}) " +
                                $"reason={decision.Reason} {decision.Summary.ToLogFields()}");
                        }
                    }

                    if (decision.Allowed)
                        return 1;
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("scoped-region-pair", ex);
            }
            return vanillaResult;
        }

        internal AttackRegionFallbackDecision EvaluateAttackUnitRegionFallback(
            AttackApproachDiagnosticScope scope,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind)
        {
            if (scope == null || scope.OwnerCommand == null ||
                scope.OwnerCommand.MapEpoch != mapEpoch)
                return AttackRegionFallbackDecision.Reject("invalid-scope");
            if (scope.UnitId <= 0 || scope.PlayerId < 0 ||
                movementClass != scope.MovementClass || sourceRegion != scope.SourceRegion ||
                sourceRegion < 0 || sourceRegion > MaximumRegionId ||
                targetRegion < 0 || targetRegion > MaximumRegionId || routeKind != 0)
            {
                return AttackRegionFallbackDecision.Reject("movement-or-region-context-mismatch");
            }
            if (!APIShared.UnitAccess.TryGetById(scope.UnitId, out GameUnit* unit, out _) ||
                unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                unit->r_TribeId != scope.TribeId ||
                unit->r_ControllableForPlayerId != scope.PlayerId || !CanDigMoat(unit))
            {
                return AttackRegionFallbackDecision.Reject("representative-unit-not-vanilla-digger");
            }
            if (scope.TargetContext != scope.OwnerCommand.TargetValue1 ||
                !TryGetHostileLivingUnitAtTile(
                    scope.PlayerId,
                    scope.TargetX,
                    scope.TargetY,
                    scope.OwnerCommand.TargetValue1,
                    scope.OwnerCommand.TargetValue2,
                    out _,
                    out _))
            {
                return AttackRegionFallbackDecision.Reject("hostile-target-context-mismatch");
            }

            int startTileId = GameTileManagerAPI.Instance.GetTileId(
                unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
            bool friendlyCompletedMoatStart = IsValidTileId(startTileId) &&
                IsCompletedMoatTile(startTileId) &&
                ResolveCompletedMoatRelationship(scope.PlayerId, startTileId) ==
                    CompletedMoatRelationship.Friendly;
            int actualSourceRegion = IsValidTileId(startTileId) ? pathRegionGrid[startTileId] : -1;
            if (!IsValidAttackSourceRegionContext(
                    sourceRegion,
                    IsValidTileId(startTileId),
                    actualSourceRegion,
                    friendlyCompletedMoatStart))
                return AttackRegionFallbackDecision.Reject("source-region-mismatch");

            AttackCursorPairScope routeScope = new AttackCursorPairScope(
                mapEpoch,
                scope.UnitId,
                scope.PlayerId,
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY,
                startTileId,
                scope.TargetX,
                scope.TargetY,
                GameTileManagerAPI.Instance.GetTileId(scope.TargetX, scope.TargetY),
                CursorPairFallbackKind.UnitApproach);
            bool routeFound = TryFindFriendlyCompletedMoatRouteToAttackApproach(
                routeScope, out int approachX, out int approachY, out RouteProbeSummary summary);
            if (!routeFound)
                return AttackRegionFallbackDecision.Reject("no-required-friendly-moat-route", summary);
            // Native UnitFlood legitimately uses targetRegion=0 as an approach-search
            // sentinel. Bind a positive target region when supplied, but never reject the
            // owner-qualified concrete approach tile merely because Vanilla passed zero.
            if (summary.StartRegion != sourceRegion ||
                (targetRegion > 0 && summary.TargetRegion != targetRegion))
            {
                return AttackRegionFallbackDecision.Reject(
                    "resolved-region-pair-mismatch", summary, approachX, approachY);
            }

            return AttackRegionFallbackDecision.Allow(summary, approachX, approachY);
        }

        internal static bool IsValidAttackSourceRegionContext(
            int sourceRegion,
            bool startTileValid,
            int actualSourceRegion,
            bool friendlyCompletedMoatStart)
        {
            if (sourceRegion < 0 || sourceRegion > MaximumRegionId ||
                !startTileValid || actualSourceRegion != sourceRegion)
            {
                return false;
            }

            // Region zero is only a valid source sentinel on the concrete completed
            // moat tile whose owner has already been resolved as friendly.
            return sourceRegion != 0 || friendlyCompletedMoatStart;
        }

        internal AttackRegionFallbackDecision EvaluateAttackBuildingRegionFallback(
            AttackApproachDiagnosticScope scope,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind)
        {
            if (scope == null || scope.OwnerCommand == null ||
                scope.OwnerCommand.MapEpoch != mapEpoch ||
                scope.OwnerCommand.TribeId != scope.TribeId ||
                !IsBuildingAttackCommand(scope.OwnerCommand.Command))
            {
                return AttackRegionFallbackDecision.Reject("invalid-building-scope");
            }
            if (movementClass != scope.MovementClass || sourceRegion != scope.SourceRegion ||
                sourceRegion < 0 || sourceRegion > MaximumRegionId ||
                targetRegion <= 0 || targetRegion > MaximumRegionId || routeKind != 0)
            {
                return AttackRegionFallbackDecision.Reject(
                    "building-movement-or-region-context-mismatch");
            }

            AttackCommandScope command = scope.OwnerCommand;
            int playerId = -1;
            List<int> diggerUnitIds = new List<int>();
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero)
                return AttackRegionFallbackDecision.Reject("missing-tile-manager");
            if (!TryCaptureOrderedActiveGroupUnits(
                    nativeTribeManager, scope.TribeId, out int[] groupUnitIds))
            {
                return AttackRegionFallbackDecision.Reject("invalid-command-group");
            }
            foreach (int unitId in groupUnitIds)
            {
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_TribeId != scope.TribeId || !CanDigMoat(unit))
                {
                    continue;
                }

                int candidatePlayerId = unit->r_ControllableForPlayerId;
                int startX = unit->r_CurrentTilePositionX;
                int startY = unit->r_CurrentTilePositionY;
                if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                    continue;
                int startTileId = GameTileManagerAPI.Instance.GetTileId(
                    startX, startY);
                if (!playerApi.IsPlayerIdValid(candidatePlayerId) ||
                    !IsValidTileId(startTileId) || pathRegionGrid[startTileId] != sourceRegion)
                {
                    continue;
                }
                if (sourceRegion == 0)
                {
                    RouteProbeSummary sourceSummary =
                        new RouteProbeSummary(candidatePlayerId);
                    if (!IsCompletedMoatTile(startTileId) ||
                        !TryClassifyFriendlyMoat(
                            tileManager,
                            playerApi,
                            startTileId,
                            candidatePlayerId,
                            ref sourceSummary))
                    {
                        continue;
                    }
                }
                if (playerId < 0)
                    playerId = candidatePlayerId;
                if (candidatePlayerId == playerId)
                    diggerUnitIds.Add(unitId);
            }
            if (diggerUnitIds.Count == 0 || playerId < 0)
            {
                return AttackRegionFallbackDecision.Reject(
                    sourceRegion == 0
                        ? "no-friendly-moat-source-digger"
                        : "no-source-region-digger");
            }

            if (!TryValidateHostileBuildingTarget(
                    command.TargetValue1,
                    unchecked((uint)command.TargetValue2),
                    playerId,
                    out GameBuilding* building) ||
                building == null || IsWallStairOrRampStructure(building->r_BuildingType))
            {
                return AttackRegionFallbackDecision.Reject("invalid-hostile-building");
            }

            RouteProbeSummary observed = new RouteProbeSummary(playerId);
            IReadOnlyList<int> approachTiles = GetBuildingApproachTilesForRegion(
                command.TargetValue1, building, targetRegion);
            var starts = new List<int>(); var targets = new List<int>();
            foreach(int id in diggerUnitIds)
                if(APIShared.UnitAccess.TryGetById(id,out GameUnit* unit, out _) && unit!=null)
                    starts.Add(unit->r_CurrentTilePositionY*MapWidth+unit->r_CurrentTilePositionX);
            foreach(int tile in approachTiles)
            { var p=GameTileManagerAPI.Instance.GetTileVectorFromId(tile); targets.Add(p.Y*MapWidth+p.X); }
            if(targets.Count!=0)
            {
                if (RequiredOnlyMode &&
                    !HasFastFriendlyMoatBridgeForCells(playerId, starts, targets))
                {
                    return AttackRegionFallbackDecision.Reject(
                        "no-friendly-moat-bridge", observed);
                }
                var field=buildingCandidateFields.Count!=0?buildingCandidateFields.Pop():new MoatCandidateField(MapWidth,MapWidth);
                weightedMoatRoutePlanner.BeginReachabilityProbe();
                try
                {
                    long fieldStarted = Stopwatch.GetTimestamp();
                    int[] distances=ResolveMovementCandidates(field,starts,targets,
                        (int f,int t,int d,out bool m,out bool st)=>BuildingCandidateEdge(playerId,f,t,d,false,false,out m,out st),
                        (int f,int t,int d,out bool m,out bool st)=>BuildingCandidateEdge(playerId,f,t,d,true,true,out m,out st),
                        out int expanded);
                    if (RequiredOnlyMode) RecordFastFieldSearch(field, fieldStarted);
                    if(activeBuildingApproachPerformance!=null)
                    { activeBuildingApproachPerformance.ReachabilityMapsBuilt++; activeBuildingApproachPerformance.SharedNodes+=expanded; }
                    for(int i=0;i<distances.Length;i++) if(distances[i]>=0)
                    {
                        observed.RouteFound=true; observed.RouteDistance=distances[i];
                        return AttackRegionFallbackDecision.Allow(observed,targets[i]%MapWidth,targets[i]/MapWidth,"shared-friendly-building-reachability");
                    }
                }
                finally { weightedMoatRoutePlanner.EndReachabilityProbe(); buildingCandidateFields.Push(field); }
            }

            return AttackRegionFallbackDecision.Reject(
                "no-owner-qualified-building-approach-in-region", observed);
        }

        internal IReadOnlyList<int> GetBuildingApproachTilesForRegion(
            int buildingId, GameBuilding* building, int targetRegion)
        {
            BuildingApproachPerformanceScope performance =
                activeBuildingApproachPerformance;
            if (performance != null && performance.BuildingId == buildingId &&
                performance.ApproachTilesByRegion != null)
            {
                return performance.ApproachTilesByRegion.TryGetValue(
                    targetRegion, out List<int> cached)
                    ? cached
                    : Array.Empty<int>();
            }

            long started = Stopwatch.GetTimestamp();
            Dictionary<int, List<int>> byRegion = new Dictionary<int, List<int>>();
            Dictionary<int, HashSet<int>> seenByRegion = new Dictionary<int, HashSet<int>>();
            int footprintTiles = 0;
            int approachTiles = 0;

            // DA020 pairs an approach tile with one of four cardinal StructureGrid tiles.
            // Index the target building once instead of rescanning every native tile for
            // every E2610 region pair. Do not use the smaller building record bounds: some
            // valid, walkable reservations lie outside them.
            for (int footprintTileId = 0; footprintTileId < NativeTileCount; footprintTileId++)
            {
                if (!IsExactBuildingContextTile(buildingId, building, footprintTileId))
                    continue;
                footprintTiles++;

                UnmanagedVector2<ushort> footprint =
                    GameTileManagerAPI.Instance.GetTileVectorFromId(footprintTileId);
                int footprintX = footprint.X;
                int footprintY = footprint.Y;
                for (int index = 0; index < 4; index++)
                {
                    int approachX = footprintX + EndpointNeighbourX[index];
                    int approachY = footprintY + EndpointNeighbourY[index];
                    if (approachX < 0 || approachX >= MapWidth ||
                        approachY < 0 || approachY >= MapWidth)
                    {
                        continue;
                    }

                    int approachTileId = GameTileManagerAPI.Instance.GetTileId(
                        approachX, approachY);
                    if (!IsWalkableBuildingApproachEndpoint(approachTileId))
                        continue;
                    int region = pathRegionGrid[approachTileId];
                    if (region <= 0 || region > MaximumRegionId)
                        continue;

                    if (!seenByRegion.TryGetValue(region, out HashSet<int> seen))
                    {
                        seen = new HashSet<int>();
                        seenByRegion.Add(region, seen);
                        byRegion.Add(region, new List<int>());
                    }
                    if (!seen.Add(approachTileId))
                        continue;
                    byRegion[region].Add(approachTileId);
                    approachTiles++;
                }
            }

            if (performance != null && performance.BuildingId == buildingId)
            {
                performance.IndexElapsedTicks += Stopwatch.GetTimestamp() - started;
                performance.IndexScans++;
                performance.IndexedNativeTiles += NativeTileCount;
                performance.IndexedFootprintTiles = footprintTiles;
                performance.IndexedApproachTiles = approachTiles;
                performance.ApproachTilesByRegion = byRegion;
            }
            return byRegion.TryGetValue(targetRegion, out List<int> result)
                ? result
                : Array.Empty<int>();
        }

    }
}
