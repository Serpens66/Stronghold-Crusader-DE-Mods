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
        internal void EnsureReachabilityMap(
            int playerId,
            int startX,
            int startY,
            bool includeEnemyRoutes = false, bool deferTraversal = false, object owner = null)
        {
            EnsureReachabilityStorage();
            object scopeOwner = owner ?? (object)activeMoatWorkSelection ?? activeMoveCommand ??
                (object)activeBuildingConsumerPerformance ?? activeBuildingApproachPerformance;
            int currentTick = CaptureCurrentGameTick();
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                startX < 0 || startX >= MapWidth ||
                startY < 0 || startY >= MapWidth)
            {
                cacheMapEpoch = -1;
                cachedRouteSummary = new RouteProbeSummary(playerId);
                return;
            }

            if (scopeOwner != null && ReferenceEquals(scopeOwner, reachabilityOwner) && reachabilityTick == currentTick &&
                visitedWithoutMoat != null && cacheMapEpoch == mapEpoch &&
                cachePlayerId == playerId && cacheStartX == startX &&
                cacheStartY == startY && cacheIncludesEnemyRoutes == includeEnemyRoutes)
            {
                if (!deferTraversal) AdvanceReachabilityMap();
                cachedReachabilityMapHits++;
                if (activeBuildingConsumerPerformance != null)
                    activeBuildingConsumerPerformance.ReachabilityCacheHits++;
                if (activeBuildingApproachPerformance != null)
                    activeBuildingApproachPerformance.ReachabilityCacheHits++;
                return;
            }

            if (activeBuildingConsumerPerformance != null)
                activeBuildingConsumerPerformance.ReachabilityMapsBuilt++;
            if (activeBuildingApproachPerformance != null)
                activeBuildingApproachPerformance.ReachabilityMapsBuilt++;

            if (gridGeneration == int.MaxValue)
            {
                Array.Clear(visitedWithoutMoat, 0, visitedWithoutMoat.Length);
                Array.Clear(visitedWithMoat, 0, visitedWithMoat.Length);
                Array.Clear(visitedWithEnemyMoat, 0, visitedWithEnemyMoat.Length);
                Array.Clear(observedRouteRegions, 0, observedRouteRegions.Length);
                Array.Clear(reachedGroundRegions, 0, reachedGroundRegions.Length);
                Array.Clear(reachedFriendlyMoatRegions, 0, reachedFriendlyMoatRegions.Length);
                Array.Clear(reachedEnemyMoatRegions, 0, reachedEnemyMoatRegions.Length);
                gridGeneration = 1;
            }
            else
            {
                gridGeneration++;
            }

            // Publish the cache only after a complete traversal. An exception or an
            // invalid source must not make a partially built graph reusable.
            cacheMapEpoch = -1;
            reachabilityOwner = scopeOwner;
            reachabilityTick = currentTick;
            cacheIncludesEnemyRoutes = includeEnemyRoutes;
            cachedReachabilityExpandedNodes = 0;
            cachePlayerId = playerId;
            cacheStartX = startX;
            cacheStartY = startY;
            cachedReachabilityMapHits = 0;
            cachedTraversedRegionCount = 0;
            cachedRouteSummary = new RouteProbeSummary(playerId);

            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            if (!IsValidTileId(startTileId))
                return;

            int startRegion = pathRegionGrid[startTileId];
            cachedRouteSummary.StartRegion = startRegion;
            int startCell = (startY * MapWidth) + startX;
            bool startIsMoat = (tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
            CompletedMoatRelationship startRelationship = startIsMoat
                ? ResolveCompletedMoatRelationship(playerId, startTileId)
                : CompletedMoatRelationship.Friendly;
            int startState;
            if (!startIsMoat)
            {
                startState = GroundRouteState;
                visitedWithoutMoat[startCell] = gridGeneration;
                distanceWithoutMoat[startCell] = 0;
            }
            else if (startRelationship == CompletedMoatRelationship.Friendly)
            {
                startState = FriendlyMoatRouteState;
                visitedWithMoat[startCell] = gridGeneration;
                distanceWithMoat[startCell] = 0;
                cachedRouteSummary.FriendlyMoatTiles = 1;
            }
            else if (startRelationship == CompletedMoatRelationship.Enemy && includeEnemyRoutes)
            {
                startState = EnemyMoatRouteState;
                visitedWithEnemyMoat[startCell] = gridGeneration;
                distanceWithEnemyMoat[startCell] = 0;
                cachedRouteSummary.EnemyMoatTiles = 1;
            }
            else
            {
                cachedRouteSummary.InvalidMoatTiles = 1;
                return;
            }

            ObserveTraversedRegion(startRegion, startState);
            reachabilityQueueHead = 0;
            reachabilityQueueTail = 1;
            queue[0] = startCell | (startState << RouteStateShift);
            cacheMapEpoch = mapEpoch;
            if (!deferTraversal) AdvanceReachabilityMap();
        }
        internal void AdvanceReachabilityMap(int targetCell = -1)
        {
            if (cacheMapEpoch != mapEpoch || reachabilityQueueHead >= reachabilityQueueTail) return;
            weightedMoatRoutePlanner.BeginReachabilityProbe();
            try
            {
                // A discovered ground route is already a conclusive negative answer to
                // "requires moat". Otherwise exhaust the frontier before returning no route.
                while (reachabilityQueueHead < reachabilityQueueTail &&
                    (targetCell < 0 || visitedWithoutMoat[targetCell] != gridGeneration))
                {
                    int encoded = queue[reachabilityQueueHead++];
                    int state = encoded >> RouteStateShift, cell = encoded & RouteCellMask;
                    int y = cell / MapWidth, x = cell % MapWidth;
                    int distance = GetRouteDistance(state, cell);
                    for (int d = 0; d < WeightedMoatRoutePlanner.DirectionX.Length; d++)
                        VisitNeighbour(cachePlayerId, x, y, x + WeightedMoatRoutePlanner.DirectionX[d],
                            y + WeightedMoatRoutePlanner.DirectionY[d], d, state, distance, ref reachabilityQueueTail);
                }
                cachedReachabilityExpandedNodes = reachabilityQueueHead;
                cachedRouteSummary.TraversedRegionCount = cachedTraversedRegionCount;
            }
            catch { cacheMapEpoch = -1; throw; }
            finally { weightedMoatRoutePlanner.EndReachabilityProbe(); }
        }
        internal void EnsureReachabilityStorage()
        {
            if (visitedWithoutMoat != null)
                return;

            visitedWithoutMoat = new int[MapCellCount];
            visitedWithMoat = new int[MapCellCount];
            visitedWithEnemyMoat = new int[MapCellCount];
            distanceWithoutMoat = new int[MapCellCount];
            distanceWithMoat = new int[MapCellCount];
            distanceWithEnemyMoat = new int[MapCellCount];
            queue = new int[MapCellCount * 3];
            observedRouteRegions = new int[MaximumRegionId + 1];
            reachedGroundRegions = new int[MaximumRegionId + 1];
            reachedFriendlyMoatRegions = new int[MaximumRegionId + 1];
            reachedEnemyMoatRegions = new int[MaximumRegionId + 1];
        }

        internal void VisitNeighbour(
            int playerId,
            int currentX,
            int currentY,
            int nextX,
            int nextY,
            int direction,
            int currentState,
            int currentDistance,
            ref int queueTail)
        {
            if (nextX < 0 || nextX >= MapWidth || nextY < 0 || nextY >= MapWidth)
                return;

            int nextCell = (nextY * MapWidth) + nextX;
            int currentTileId = GameTileManagerAPI.Instance.GetTileId(currentX, currentY);
            int nextTileId = GameTileManagerAPI.Instance.GetTileId(nextX, nextY);
            if (!IsValidTileId(currentTileId) || !IsValidTileId(nextTileId))
                return;

            if (!weightedMoatRoutePlanner.TryGetTraversalEdge(
                    playerId,
                    currentX,
                    currentY,
                    currentTileId,
                    nextX,
                    nextY,
                    nextTileId,
                    direction,
                    false,
                    false,
                    cacheIncludesEnemyRoutes
                        ? MoatTraversalPolicy.AllowEnemyForDiagnostic
                        : MoatTraversalPolicy.FriendlyOnly,
                    out MoatTraversalEdgeKind edgeKind,
                    out bool structuralEdge))
            {
                return;
            }

            if (structuralEdge)
                cachedRouteSummary.StructuralEdgesObserved++;

            int nextState = currentState == EnemyMoatRouteState ||
                edgeKind == MoatTraversalEdgeKind.EnemyMoat
                    ? EnemyMoatRouteState
                    : currentState == FriendlyMoatRouteState ||
                      edgeKind == MoatTraversalEdgeKind.FriendlyMoat
                        ? FriendlyMoatRouteState
                        : GroundRouteState;
            int[] visited = GetRouteVisitedMap(nextState);
            if (visited[nextCell] == gridGeneration || queueTail >= queue.Length)
                return;

            visited[nextCell] = gridGeneration;
            GetRouteDistanceMap(nextState)[nextCell] = currentDistance + 1;
            if ((tileFlags[nextTileId] & CompletedMoatTileFlag) != 0)
            {
                if (nextState == FriendlyMoatRouteState)
                    cachedRouteSummary.FriendlyMoatTiles++;
                else if (nextState == EnemyMoatRouteState)
                    cachedRouteSummary.EnemyMoatTiles++;
            }
            ObserveTraversedRegion(pathRegionGrid[nextTileId], nextState);
            queue[queueTail++] = nextCell | (nextState << RouteStateShift);
        }

        internal int[] GetRouteVisitedMap(int state) =>
            state == GroundRouteState
                ? visitedWithoutMoat
                : state == FriendlyMoatRouteState
                    ? visitedWithMoat
                    : visitedWithEnemyMoat;

        internal int[] GetRouteDistanceMap(int state) =>
            state == GroundRouteState
                ? distanceWithoutMoat
                : state == FriendlyMoatRouteState
                    ? distanceWithMoat
                    : distanceWithEnemyMoat;

        internal int GetRouteDistance(int state, int cell) =>
            GetRouteDistanceMap(state)[cell];

        internal void ObserveTraversedRegion(int region, int state)
        {
            if (region <= 0 || region > MaximumRegionId)
                return;

            if (observedRouteRegions[region] != gridGeneration)
            {
                observedRouteRegions[region] = gridGeneration;
                cachedTraversedRegionCount++;
            }
            int[] reachedRegions = state == GroundRouteState
                ? reachedGroundRegions
                : state == FriendlyMoatRouteState
                    ? reachedFriendlyMoatRegions
                    : reachedEnemyMoatRegions;
            reachedRegions[region] = gridGeneration;
        }

        internal RouteProbeSummary GetCachedRouteSummaryForTarget(int targetX, int targetY)
        {
            if ((uint)targetX < MapWidth && (uint)targetY < MapWidth) AdvanceReachabilityMap(targetY * MapWidth + targetX);
            RouteProbeSummary summary = cachedRouteSummary;
            if (targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth)
                return summary;

            int targetCell = targetY * MapWidth + targetX;
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            summary.TargetRegion = IsValidTileId(targetTileId)
                ? pathRegionGrid[targetTileId]
                : 0;
            summary.ReachedWithoutMoat =
                visitedWithoutMoat[targetCell] == gridGeneration;
            summary.ReachedWithMoat = visitedWithMoat[targetCell] == gridGeneration;
            summary.EnemyOnlyReachable =
                visitedWithEnemyMoat[targetCell] == gridGeneration &&
                !summary.ReachedWithoutMoat && !summary.ReachedWithMoat;
            summary.TraversedRegionCount = cachedTraversedRegionCount;
            summary.ReachabilityCacheHits = cachedReachabilityMapHits;
            return summary;
        }

        internal RouteProbeSummary GetCachedRouteSummaryForRegion(int targetRegion)
        {
            AdvanceReachabilityMap();
            RouteProbeSummary summary = cachedRouteSummary;
            summary.TargetRegion = targetRegion;
            if (targetRegion <= 0 || targetRegion > MaximumRegionId)
                return summary;

            summary.ReachedWithoutMoat =
                reachedGroundRegions[targetRegion] == gridGeneration;
            summary.ReachedWithMoat =
                reachedFriendlyMoatRegions[targetRegion] == gridGeneration;
            summary.EnemyOnlyReachable =
                reachedEnemyMoatRegions[targetRegion] == gridGeneration &&
                !summary.ReachedWithoutMoat && !summary.ReachedWithMoat;
            summary.TraversedRegionCount = cachedTraversedRegionCount;
            summary.ReachabilityCacheHits = cachedReachabilityMapHits;
            return summary;
        }

        internal bool TryClassifyFriendlyMoat(
            IntPtr tileManager,
            GamePlayerManagerAPI playerApi,
            int tileId,
            int playerId,
            ref RouteProbeSummary summary)
        {
            int moatOwnerId;
            BuildingConsumerPerformanceScope consumerPerformance =
                activeBuildingConsumerPerformance;
            BuildingApproachPerformanceScope approachPerformance =
                activeBuildingApproachPerformance;
            Dictionary<int, int> ownerCache = consumerPerformance != null
                ? consumerPerformance.MoatOwnerByTile
                : approachPerformance?.MoatOwnerByTile;
            if (ownerCache != null && ownerCache.TryGetValue(tileId, out int cachedOwnerId))
            {
                if (consumerPerformance != null)
                    consumerPerformance.MoatOwnerCacheHits++;
                else
                    approachPerformance.MoatOwnerCacheHits++;
                moatOwnerId = cachedOwnerId;
            }
            else
            {
                int moatId = getMoatIdAtTile(tileManager, tileId);
                int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
                if (!IsValidMoatRecordId(moatId, moatCount))
                {
                    moatOwnerId = -1;
                }
                else
                {
                    byte* moatRecord = (byte*)tileManager.ToPointer() +
                        MoatRecordArrayOffset + moatId * MoatRecordSize;
                    moatOwnerId = moatRecord[MoatOwnerOffset];
                }
                if (ownerCache != null)
                {
                    if (consumerPerformance != null)
                        consumerPerformance.MoatOwnerCacheMisses++;
                    else
                        approachPerformance.MoatOwnerCacheMisses++;
                    ownerCache[tileId] = moatOwnerId;
                }
            }
            summary.ObserveOwner(moatOwnerId);
            if (!playerApi.IsPlayerIdValid(moatOwnerId))
            {
                summary.InvalidMoatTiles++;
                return false;
            }

            bool friendly = moatOwnerId == playerId ||
                playerApi.IsPlayerAlliedTo(playerId, moatOwnerId);
            if (friendly)
                summary.FriendlyMoatTiles++;
            else
                summary.EnemyMoatTiles++;
            return friendly;
        }

        internal void LogUnscopedAttackMode(int unitId, GameUnit* unit, int vanillaResult)
        {
            TribeAICommand command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
            if (command != TribeAICommand.AttackUnit &&
                command != TribeAICommand.AttackBuilding &&
                command != TribeAICommand.ForceAttackBuilding)
            {
                return;
            }

            string signature = $"{mapEpoch}:{(uint)command}:{unit->r_AIState}:" +
                $"{unit->r_CurrentTilePositionX}:{unit->r_CurrentTilePositionY}:" +
                $"{unit->r_AttackMoveToTargetTileX}:{unit->r_AttackMoveToTargetTileY}:" +
                $"{unit->r_AI_ContextTargetUnitId}:{unit->r_AI_ContextTargetUnitGlobalId}:" +
                $"{unit->r_AI_ContextTargetBuildingTileId}:" +
                $"{unit->r_ContextTargetTileX}:{unit->r_ContextTargetTileY}:{vanillaResult}";
            if (lastUnscopedAttackModes.TryGetValue(unitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }

            lastUnscopedAttackModes[unitId] = signature;
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-attack-mode-unscoped unit={unitId} " +
                $"player={unit->r_ControllableForPlayerId} command={command}({(uint)command}) " +
                $"aiState={unit->r_AIState} current=({unit->r_CurrentTilePositionX}," +
                $"{unit->r_CurrentTilePositionY}) attackMove=({unit->r_AttackMoveToTargetTileX}," +
                $"{unit->r_AttackMoveToTargetTileY}) contextUnit={unit->r_AI_ContextTargetUnitId}/" +
                $"{unit->r_AI_ContextTargetUnitGlobalId} " +
                $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                $"contextTile=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                $"vanillaStandingOnMoat={vanillaResult}.");
        }

        internal void ResetMapState()
        {
            // TEMP_GATE_ROUTE_ACCEPTANCE: maps never retain an unfinished order association.
            if (temporaryAssassinScope != null) TemporaryGateRouteAcceptanceBridge.ReportFailure("assassin-map-unpaired-order");
            temporaryAssassinScope = null;
            manualCommandContexts?.Clear(); targetCommandParents?.Clear();
            moveEventObservers?.Clear(); moveEventDepths?.Clear(); targetEventObservers?.Clear();
            nativeCursorAnswers.Clear(); nativeCursorAnswerTick = int.MinValue;
            LogAndResetFastMoatMetrics();
            ClearUnitMoveFrames();
            mapEpoch++;
            InvalidateFastMoatData();
            cursorTopologies.Clear(); noBuilderDetails = 0; preBuilderRejections.Clear();
            cursorDecisionCounts.Clear(); cursorDecisionDetails.Clear();
            fillRouteDecisions.Clear(); fillRouteLogTick = -1; fillRouteLogCount = 0; formationOwner = null;
            formationRuntime?.ResetTransientState();
            ResetMoatWorkTargetSelection();
            ResetDirectMoatCommandScopes();
            cacheMapEpoch = -1;
            cacheStartX = -1;
            cacheStartY = -1;
            cachePlayerId = -1;
            cachedTraversedRegionCount = 0;
            cachedReachabilityMapHits = 0;
            cachedRouteSummary = default;
            loggedBuildingCursorReachabilityDecisions.Clear();
            lastUnscopedAttackModes.Clear();
            lastAttackCommandCandidates.Clear();
            trackedAttackUnits.Clear();
            activeMoveCommand = null;
            Traversal?.ResetMapState();
            ClearDeferredFastMoveScope();
            activePlan = null;
            pendingPlan = null;
            pendingAttackCursorPair = null;
            activeAttackCommand = null;
            activeAttackApproachDiagnostic = null;
            activeBuildingApproachPerformance = null;
            activeBuildingConsumerPerformance = null;
            trackedMoatMoves.Clear();
            requiredBackgroundTrackedUnitIds.Clear();
            requiredBackgroundDiagnostics.Clear();
            requiredBackgroundSuppressedDiagnostics.Clear();
            requiredBackgroundDiagnosticTick = int.MinValue;
            trackedNativeWaypointQueues.Clear();
            loggedDiggerDecisions.Clear();
            lastWeightedPublicationDecisionByUnit.Clear();
        }

    }
}
