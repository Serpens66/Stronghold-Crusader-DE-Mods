extern alias BugfixesRuntime;
using BepInEx.Logging;
using APIShared;
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
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.ReachabilityMap.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
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
            reachabilityOwnerKind = scopeOwner?.GetType().Name ?? (includeEnemyRoutes ? "enemyDiagnostic" : "unscoped");
            int currentTick = CaptureCurrentGameTick();
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                startX < 0 || startX >= MapWidth ||
                startY < 0 || startY >= MapWidth)
            {
                runtime.cacheMapEpoch = -1;
                runtime.cachedRouteSummary = new RouteProbeSummary(playerId);
                return;
            }

            if (scopeOwner != null && ReferenceEquals(scopeOwner, reachabilityOwner) && reachabilityTick == currentTick &&
                visitedWithoutMoat != null && runtime.cacheMapEpoch == runtime.mapEpoch &&
                runtime.cachePlayerId == playerId && runtime.cacheStartX == startX &&
                runtime.cacheStartY == startY && cacheIncludesEnemyRoutes == includeEnemyRoutes)
            {
                if (!deferTraversal) AdvanceReachabilityMap();
                runtime.cachedReachabilityMapHits++;
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
            runtime.cacheMapEpoch = -1;
            reachabilityOwner = scopeOwner;
            reachabilityTick = currentTick;
            cacheIncludesEnemyRoutes = includeEnemyRoutes;
            // Terrain reported since the last search is re-read before the cached words answer.
            if (!includeEnemyRoutes) RefreshReachabilityWords(playerId);
            cachedReachabilityExpandedNodes = 0;
            runtime.cachePlayerId = playerId;
            runtime.cacheStartX = startX;
            runtime.cacheStartY = startY;
            runtime.cachedReachabilityMapHits = 0;
            runtime.cachedTraversedRegionCount = 0;
            runtime.cachedRouteSummary = new RouteProbeSummary(playerId);

            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            if (!IsValidTileId(startTileId))
                return;

            int startRegion = runtime.pathRegionGrid[startTileId];
            runtime.cachedRouteSummary.StartRegion = startRegion;
            int startCell = (startY * MapWidth) + startX;
            bool startIsMoat = (runtime.tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
            CompletedMoatRelationship startRelationship = startIsMoat
                ? runtime.ResolveCompletedMoatRelationship(playerId, startTileId)
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
                runtime.cachedRouteSummary.FriendlyMoatTiles = 1;
            }
            else if (startRelationship == CompletedMoatRelationship.Enemy && includeEnemyRoutes)
            {
                startState = EnemyMoatRouteState;
                visitedWithEnemyMoat[startCell] = gridGeneration;
                distanceWithEnemyMoat[startCell] = 0;
                runtime.cachedRouteSummary.EnemyMoatTiles = 1;
            }
            else
            {
                runtime.cachedRouteSummary.InvalidMoatTiles = 1;
                return;
            }

            ObserveTraversedRegion(startRegion, startState);
            reachabilityQueueHead = 0;
            reachabilityQueueTail = 1;
            queue[0] = startCell | (startState << RouteStateShift);
            runtime.cacheMapEpoch = runtime.mapEpoch;
            if (!deferTraversal) AdvanceReachabilityMap();
        }
        internal void AdvanceReachabilityMap(int targetCell = -1, int targetRegion = 0, bool requireMoat = false)
        {
            if (runtime.cacheMapEpoch != runtime.mapEpoch || reachabilityQueueHead >= reachabilityQueueTail) return;
            long advanceStarted = System.Diagnostics.Stopwatch.GetTimestamp(); int headBefore = reachabilityQueueHead;
            runtime.weightedMoatRoutePlanner.BeginReachabilityProbe();
            try
            {
                // FriendlyOnly searches in anchor mode read one cached word per cell and take tile
                // ids from the byte watch's node table; the same edges in the same direction order
                // as VisitNeighbour (110-150 ms gaps were per-neighbour lookups, pf-iter-11).
                FastTraversalCache words = cacheIncludesEnemyRoutes || DisableWordReachability ? null : ReachabilityWords(runtime.cachePlayerId);
                int[] tileOf = words != null ? AnchorNodeTiles() : null;
                // A target in a small closed backward component cannot be reached in any state: answer
                // without exhausting the forward frontier (AI diggers asked for the same unreachable
                // moat target every few ticks, each search 16-57 ms over 236k-473k cells, pf-iter-19).
                if (words != null && targetCell >= 0 && ProvesReachabilityTargetUnreachable(words, targetCell)) return;
                // A discovered ground route is already a conclusive negative answer to
                // "requires moat". Otherwise exhaust the frontier before returning no route.
                while (reachabilityQueueHead < reachabilityQueueTail &&
                    (targetRegion <= 0 || reachedFriendlyMoatRegions[targetRegion] != gridGeneration) &&
                    (targetCell < 0 || (requireMoat ? visitedWithMoat : visitedWithoutMoat)[targetCell] != gridGeneration))
                {
                    int encoded = queue[reachabilityQueueHead++];
                    int state = encoded >> RouteStateShift, cell = encoded & RouteCellMask;
                    int y = cell / MapWidth, x = cell % MapWidth;
                    int distance = GetRouteDistance(state, cell);
                    if (tileOf != null)
                    {
                        uint word = words.EdgeWord(cell);
                        for (int d = 0; d < 8; d++)
                        {
                            if ((word & (1U << d)) == 0) continue;
                            int nextCell = cell + WeightedMoatRoutePlanner.DirectionY[d] * MapWidth + WeightedMoatRoutePlanner.DirectionX[d];
                            int nextTile = tileOf[nextCell];
                            if (nextTile < 0) continue;
                            EnqueueNeighbour(nextCell, nextTile, state, (word & (1U << (16 + d))) != 0 ? MoatTraversalEdgeKind.FriendlyMoat : MoatTraversalEdgeKind.Ground,
                                (word & (1U << (24 + d))) != 0, distance, ref reachabilityQueueTail);
                        }
                        continue;
                    }
                    for (int d = 0; d < WeightedMoatRoutePlanner.DirectionX.Length; d++)
                        VisitNeighbour(runtime.cachePlayerId, x, y, x + WeightedMoatRoutePlanner.DirectionX[d],
                            y + WeightedMoatRoutePlanner.DirectionY[d], d, state, distance, ref reachabilityQueueTail);
                }
                cachedReachabilityExpandedNodes = reachabilityQueueHead;
                runtime.cachedRouteSummary.TraversedRegionCount = runtime.cachedTraversedRegionCount;
            }
            catch { runtime.cacheMapEpoch = -1; throw; }
            finally
            {
                runtime.weightedMoatRoutePlanner.EndReachabilityProbe();
                RecordReachabilityAdvance(System.Diagnostics.Stopwatch.GetTimestamp() - advanceStarted, reachabilityQueueHead - headBefore,
                    targetCell, targetRegion, requireMoat, reachabilityQueueHead >= reachabilityQueueTail);
            }
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
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            int nextTileId = tiles.GetTileId(nextX, nextY);
            if (!IsValidTileId(nextTileId))
                return;

            MoatTraversalEdgeKind edgeKind; bool structuralEdge;
            FastTraversalCache words = cacheIncludesEnemyRoutes ? null : ReachabilityWords(playerId);
            if (words != null)
            {
                // FriendlyOnly answers come from the cached words (same predicate, see ReachabilityWords).
                // The current cell's tile is valid: it was checked when the cell was queued.
                if (!words.Edge(currentY * MapWidth + currentX, nextCell, direction, false, out bool wet, out structuralEdge)) return;
                edgeKind = wet ? MoatTraversalEdgeKind.FriendlyMoat : MoatTraversalEdgeKind.Ground;
            }
            else if (!runtime.weightedMoatRoutePlanner.TryGetTraversalEdge(
                    playerId,
                    currentX,
                    currentY,
                    tiles.GetTileId(currentX, currentY),
                    nextX,
                    nextY,
                    nextTileId,
                    direction,
                    false,
                    false,
                    MoatTraversalPolicy.AllowEnemyForDiagnostic,
                    out edgeKind,
                    out structuralEdge))
            {
                return;
            }
            EnqueueNeighbour(nextCell, nextTileId, currentState, edgeKind, structuralEdge, currentDistance, ref queueTail);
        }

        // Backward closure of a target over allowed (FriendlyOnly word) edges: every cell with a path
        // to the target, if that set is closed within the bound. It depends only on the words, not on
        // the start, so it is cached per player and target until a word change touches it or the
        // words are dropped. A start outside a closed closure cannot reach the target in any state,
        // so the forward search need not run out (AI diggers asked for the same unreachable moat
        // target every few ticks, each search 16-57 ms over 236k-473k cells, pf-iter-19).
        private const int ReachabilityClosureLimit = 16384, ReachabilityClosureEntries = 64;
        private sealed class ReachabilityClosure { internal int Player; internal long Forgets; internal bool Closed; internal HashSet<int> Cells; }
        private readonly Dictionary<long, ReachabilityClosure> reachabilityClosures = new Dictionary<long, ReachabilityClosure>();
        private int[] reachabilityProofQueue;
        internal long ReachabilityProofs, ReachabilityClosuresBuilt;
        private bool ProvesReachabilityTargetUnreachable(FastTraversalCache words, int target)
        {
            int player = runtime.cachePlayerId, start = runtime.cacheStartY * MapWidth + runtime.cacheStartX;
            if ((uint)target >= (uint)MapCellCount || (uint)start >= (uint)MapCellCount) return false;
            long key = ((long)player << 32) | (uint)target;
            if (!reachabilityClosures.TryGetValue(key, out ReachabilityClosure closure) || closure.Forgets != words.Forgets)
            {
                if (reachabilityClosures.Count >= ReachabilityClosureEntries) reachabilityClosures.Clear();
                reachabilityClosures[key] = closure = BuildReachabilityClosure(words, player, target);
            }
            if (!closure.Closed || closure.Cells.Contains(start)) return false;
            ReachabilityProofs++;
            return true;
        }
        private ReachabilityClosure BuildReachabilityClosure(FastTraversalCache words, int player, int target)
        {
            ReachabilityClosuresBuilt++;
            var cells = new HashSet<int> { target };
            int[] queue = reachabilityProofQueue ?? (reachabilityProofQueue = new int[ReachabilityClosureLimit]);
            int head = 0, tail = 0; queue[tail++] = target;
            bool closed = true;
            while (head < tail && closed)
            {
                int node = queue[head++], x = node % MapWidth, y = node / MapWidth;
                for (int d = 0; d < 8; d++)
                {
                    // Incoming edge u -> node in direction d: u lies at node - (dx, dy).
                    int ux = x - WeightedMoatRoutePlanner.DirectionX[d], uy = y - WeightedMoatRoutePlanner.DirectionY[d];
                    if ((uint)ux >= MapWidth || (uint)uy >= MapWidth) continue;
                    int u = uy * MapWidth + ux;
                    if (cells.Contains(u) || (words.EdgeWord(u) & (1U << d)) == 0) continue;
                    if (tail >= ReachabilityClosureLimit) { closed = false; break; }
                    cells.Add(u); queue[tail++] = u;
                }
            }
            return new ReachabilityClosure { Player = player, Forgets = words.Forgets, Closed = closed, Cells = cells };
        }
        // Changed words (a refresh of the player's reachability words): drop every closure of that
        // player that contains a changed cell or one of its neighbours (a new or removed incoming edge).
        private void InvalidateReachabilityClosures(int player, IReadOnlyCollection<int> changed)
        {
            if (changed.Count == 0 || reachabilityClosures.Count == 0) return;
            List<long> stale = null;
            foreach (var entry in reachabilityClosures)
            {
                if (entry.Value.Player != player) continue;
                foreach (int node in changed)
                {
                    int x = node % MapWidth, y = node / MapWidth; bool touched = false;
                    for (int ny = Math.Max(0, y - 1); ny <= Math.Min(MapWidth - 1, y + 1) && !touched; ny++)
                        for (int nx = Math.Max(0, x - 1); nx <= Math.Min(MapWidth - 1, x + 1) && !touched; nx++)
                            touched = entry.Value.Cells.Contains(ny * MapWidth + nx);
                    if (touched) { (stale ?? (stale = new List<long>())).Add(entry.Key); break; }
                }
            }
            if (stale != null) foreach (long key in stale) reachabilityClosures.Remove(key);
        }

        // Test hook: the per-neighbour path also for FriendlyOnly searches (equality test).
        internal bool DisableWordReachability;

        // An allowed edge into nextCell: state transition, visit, summary and queue.
        private void EnqueueNeighbour(int nextCell, int nextTileId, int currentState, MoatTraversalEdgeKind edgeKind,
            bool structuralEdge, int currentDistance, ref int queueTail)
        {
            if (structuralEdge)
                runtime.cachedRouteSummary.StructuralEdgesObserved++;

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
            if ((runtime.tileFlags[nextTileId] & CompletedMoatTileFlag) != 0)
            {
                if (nextState == FriendlyMoatRouteState)
                    runtime.cachedRouteSummary.FriendlyMoatTiles++;
                else if (nextState == EnemyMoatRouteState)
                    runtime.cachedRouteSummary.EnemyMoatTiles++;
            }
            ObserveTraversedRegion(runtime.pathRegionGrid[nextTileId], nextState);
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
                runtime.cachedTraversedRegionCount++;
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
            RouteProbeSummary summary = runtime.cachedRouteSummary;
            if (targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth)
                return summary;

            int targetCell = targetY * MapWidth + targetX;
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            summary.TargetRegion = IsValidTileId(targetTileId)
                ? runtime.pathRegionGrid[targetTileId]
                : 0;
            summary.ReachedWithoutMoat =
                visitedWithoutMoat[targetCell] == gridGeneration;
            summary.ReachedWithMoat = visitedWithMoat[targetCell] == gridGeneration;
            summary.EnemyOnlyReachable =
                visitedWithEnemyMoat[targetCell] == gridGeneration &&
                !summary.ReachedWithoutMoat && !summary.ReachedWithMoat;
            summary.TraversedRegionCount = runtime.cachedTraversedRegionCount;
            summary.ReachabilityCacheHits = runtime.cachedReachabilityMapHits;
            return summary;
        }

        internal RouteProbeSummary GetCachedRouteSummaryForRegion(int targetRegion)
        {
            AdvanceReachabilityMap();
            RouteProbeSummary summary = runtime.cachedRouteSummary;
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
            summary.TraversedRegionCount = runtime.cachedTraversedRegionCount;
            summary.ReachabilityCacheHits = runtime.cachedReachabilityMapHits;
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
                int moatId = runtime.getMoatIdAtTile(tileManager, tileId);
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

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogUnscopedAttackMode(int unitId, GameUnit* unit, int vanillaResult)
        {
#if SERP_DEVDIAG
            TribeAICommand command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
            if (command != TribeAICommand.AttackUnit &&
                command != TribeAICommand.AttackBuilding &&
                command != TribeAICommand.ForceAttackBuilding)
            {
                return;
            }

            string signature = $"{runtime.mapEpoch}:{(uint)command}:{unit->r_AIState}:" +
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
            runtime.LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-attack-mode-unscoped unit={unitId} " +
                $"player={unit->r_ControllableForPlayerId} command={command}({(uint)command}) " +
                $"aiState={unit->r_AIState} current=({unit->r_CurrentTilePositionX}," +
                $"{unit->r_CurrentTilePositionY}) attackMove=({unit->r_AttackMoveToTargetTileX}," +
                $"{unit->r_AttackMoveToTargetTileY}) contextUnit={unit->r_AI_ContextTargetUnitId}/" +
                $"{unit->r_AI_ContextTargetUnitGlobalId} " +
                $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                $"contextTile=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                $"vanillaStandingOnMoat={vanillaResult}.");
#endif
        }
    }
}
