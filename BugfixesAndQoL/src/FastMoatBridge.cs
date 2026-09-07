using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        private const int FastSearchNodeBudget = 16384;
        private readonly Dictionary<int, FastMoatGraph> fastMoatGraphs =
            new Dictionary<int, FastMoatGraph>();
        private readonly Dictionary<FastMoatBridgeKey, bool> fastMoatBridgeDecisions =
            new Dictionary<FastMoatBridgeKey, bool>();
        private readonly Dictionary<FastCursorRouteKey, RouteProbeSummary> fastCursorRoutes =
            new Dictionary<FastCursorRouteKey, RouteProbeSummary>();
        private readonly object fastCursorSearchOwner = new object();
        private long fastVanillaBypasses, fastFallbackChecks, fastBridgeCacheHits;
        private long fastBridgeBuilds, fastSearches, fastBudgetAborts, fastExpandedNodes;
        private int fastMaximumExpandedNodes;
        private long fastMaximumSearchTicks;
        private long fastBridgeInvalidEndpoints, fastBridgeSameRegionRejects;
        private long fastBridgeNoMoatRejects, fastBridgeNoEndpointRejects;
        private long fastBridgeDisconnectedRejects, fastHighRegionEndpoints;
        private long fastDeferredHumanScopes, fastDeferredHumanUses, fastDeferredHumanExpirations;
        private DeferredFastMoveScope deferredFastMoveScope;

        private void InvalidateFastMoatData()
        {
            fastMoatGraphs.Clear();
            fastMoatBridgeDecisions.Clear();
            fastCursorRoutes.Clear();
        }

        private void LogAndResetFastMoatMetrics()
        {
            if (fastVanillaBypasses > 0 || fastFallbackChecks > 0 || fastSearches > 0 ||
                fastDeferredHumanScopes > 0 || fastDeferredHumanUses > 0)
            {
                Shared.DebugLogHelper.LogInfo(log,
                    "Bugfixes and QoL stage=friendly-moat-fast-performance " +
                    $"vanillaBypasses={fastVanillaBypasses} fallbackChecks={fastFallbackChecks} " +
                    $"bridgeBuilds={fastBridgeBuilds} bridgeCacheHits={fastBridgeCacheHits} " +
                    $"searches={fastSearches} expanded={fastExpandedNodes} " +
                    $"maxExpanded={fastMaximumExpandedNodes} budgetAborts={fastBudgetAborts} " +
                    $"maxSearchMs={fastMaximumSearchTicks * 1000.0 / Stopwatch.Frequency:F3} " +
                    $"bridgeRejects=invalid:{fastBridgeInvalidEndpoints},sameRegion:{fastBridgeSameRegionRejects}," +
                    $"noMoat:{fastBridgeNoMoatRejects},noEndpoint:{fastBridgeNoEndpointRejects}," +
                    $"disconnected:{fastBridgeDisconnectedRejects} highRegionEndpoints={fastHighRegionEndpoints} " +
                    $"deferredHumanScopes={fastDeferredHumanScopes} deferredHumanUses={fastDeferredHumanUses} " +
                    $"deferredHumanExpirations={fastDeferredHumanExpirations}.");
            }
            fastVanillaBypasses = fastFallbackChecks = fastBridgeCacheHits = 0;
            fastBridgeBuilds = fastSearches = fastBudgetAborts = fastExpandedNodes = 0;
            fastMaximumExpandedNodes = 0;
            fastMaximumSearchTicks = 0;
            fastBridgeInvalidEndpoints = fastBridgeSameRegionRejects = 0;
            fastBridgeNoMoatRejects = fastBridgeNoEndpointRejects = 0;
            fastBridgeDisconnectedRejects = fastHighRegionEndpoints = 0;
            fastDeferredHumanScopes = fastDeferredHumanUses = fastDeferredHumanExpirations = 0;
        }

        private bool HasFastFriendlyMoatBridge(int playerId, int startTileId, int targetTileId)
        {
            fastFallbackChecks++;
            if (!IsValidTileId(startTileId) || !IsValidTileId(targetTileId) ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
            {
                fastBridgeInvalidEndpoints++;
                return false;
            }

            int startRegion = pathRegionGrid[startTileId];
            int targetRegion = pathRegionGrid[targetTileId];
            if (startRegion > short.MaxValue || targetRegion > short.MaxValue)
                fastHighRegionEndpoints++;
            if (startRegion > 0 && startRegion == targetRegion &&
                !IsCompletedMoatTile(startTileId) && !IsCompletedMoatTile(targetTileId))
            {
                fastBridgeSameRegionRejects++;
                return false;
            }

            if (!fastMoatGraphs.TryGetValue(playerId, out FastMoatGraph graph) ||
                graph.MapEpoch != mapEpoch || graph.Revision != placementRevision)
            {
                graph = BuildFastMoatGraph(playerId);
                fastMoatGraphs[playerId] = graph;
                fastBridgeBuilds++;
            }
            if (graph.FriendlyMoatTiles.Count == 0)
            {
                fastBridgeNoMoatRejects++;
                return false;
            }

            int startNode = FastBridgeEndpointNode(startTileId);
            int targetNode = FastBridgeEndpointNode(targetTileId);
            var key = new FastMoatBridgeKey(
                mapEpoch, placementRevision, playerId, startNode, targetNode);
            if (fastMoatBridgeDecisions.TryGetValue(key, out bool cached))
            {
                fastBridgeCacheHits++;
                return cached;
            }

            List<int> starts = GetFastBridgeNodes(graph, startTileId);
            HashSet<int> targets = new HashSet<int>(GetFastBridgeNodes(graph, targetTileId));
            if (starts.Count == 0 || targets.Count == 0)
            {
                fastBridgeNoEndpointRejects++;
                fastMoatBridgeDecisions[key] = false;
                return false;
            }

            var visited = new HashSet<int>();
            var queue = new Queue<int>();
            foreach (int node in starts)
                if (visited.Add(node)) queue.Enqueue(node);
            while (queue.Count != 0)
            {
                int node = queue.Dequeue();
                if (targets.Contains(node))
                {
                    fastMoatBridgeDecisions[key] = true;
                    return true;
                }
                if (!graph.Edges.TryGetValue(node, out List<int> neighbours))
                    continue;
                foreach (int neighbour in neighbours)
                    if (visited.Add(neighbour)) queue.Enqueue(neighbour);
            }
            fastBridgeDisconnectedRejects++;
            fastMoatBridgeDecisions[key] = false;
            return false;
        }

        private void ClearDeferredFastMoveScope()
        {
            deferredFastMoveScope = null;
        }

        private void CaptureDeferredFastMoveScope(MoveCommandScope command)
        {
            if (!RequiredOnlyMode || command == null)
                return;
            if (!GameTribeManagerAPI.Instance.TryGetTribeById(
                    command.TribeId, out GameTribe* tribe) || tribe == null)
                return;

            int ownerId = tribe->r_PlayerIdOwner;
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            if (!players.IsPlayerIdValid(ownerId) || players.IsAIPlayer(ownerId) ||
                ownerId != players.GetLocalPlayerId())
                return;

            // A new local command supersedes older delayed work. AI commands must not
            // clear this scope while the native large-group dispatcher is still draining.
            deferredFastMoveScope = null;
            if (!command.MoatRelevant)
                return;
            EnsureMoveCommandGroupSummary(command);
            if (command.ActiveUnitIdsAtDispatch.Length == 0)
                return;

            // Large groups can defer individual path builders until after the
            // synchronous tribe command returns. Retain only this local human
            // command; AI/FastRecruit traffic can never acquire this authority.
            deferredFastMoveScope = new DeferredFastMoveScope(
                mapEpoch, command.TribeId,
                Stopwatch.GetTimestamp() + Stopwatch.Frequency * 30L,
                command.ActiveUnitIdsAtDispatch);
            fastDeferredHumanScopes++;
        }

        private bool IsDeferredFastMoveAuthorized(PlanScope plan, GameUnit* unit)
        {
            DeferredFastMoveScope scope = deferredFastMoveScope;
            if (scope == null || activeMoveCommand != null || plan == null || unit == null)
                return false;
            if (scope.MapEpoch != mapEpoch || Stopwatch.GetTimestamp() > scope.ExpiresAt)
            {
                deferredFastMoveScope = null;
                fastDeferredHumanExpirations++;
                return false;
            }
            if (unit->r_TribeId != scope.TribeId || !scope.UnitIds.Contains(plan.UnitId))
                return false;
            fastDeferredHumanUses++;
            return true;
        }

        private int FastBridgeEndpointNode(int tileId)
        {
            int region = pathRegionGrid[tileId];
            return region > 0 && !IsCompletedMoatTile(tileId)
                ? region
                : FastMoatNode(tileId);
        }

        private FastMoatGraph BuildFastMoatGraph(int playerId)
        {
            var graph = new FastMoatGraph(mapEpoch, placementRevision);
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero)
                return graph;
            int count = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (count <= 1 || count > MaximumMoatRecordId + 1)
                return graph;

            for (int moatId = 1; moatId < count; moatId++)
            {
                if (!TryReadMoatRecord(tileManager, moatId, out byte* record,
                        out int tileId, out int x, out int y) ||
                    !IsCompletedMoatTile(tileId))
                    continue;
                int ownerId = record[MoatOwnerOffset];
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(ownerId) ||
                    (ownerId != playerId &&
                     !GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(playerId, ownerId)))
                    continue;
                graph.FriendlyMoatTiles.Add(tileId);
                graph.Coordinates[tileId] = new FastTilePosition(x, y);
            }

            foreach (int tileId in graph.FriendlyMoatTiles)
            {
                FastTilePosition position = graph.Coordinates[tileId];
                int moatNode = FastMoatNode(tileId);
                graph.EnsureNode(moatNode);
                for (int direction = 0; direction < 8; direction++)
                {
                    int x = position.X + WeightedMoatRoutePlanner.DirectionX[direction];
                    int y = position.Y + WeightedMoatRoutePlanner.DirectionY[direction];
                    if ((uint)x >= MapWidth || (uint)y >= MapWidth)
                        continue;
                    int neighbourTile = GameTileManagerAPI.Instance.GetTileId(x, y);
                    if (!IsValidTileId(neighbourTile))
                        continue;
                    if (graph.FriendlyMoatTiles.Contains(neighbourTile))
                    {
                        graph.AddUndirected(moatNode, FastMoatNode(neighbourTile));
                        continue;
                    }
                    int region = pathRegionGrid[neighbourTile];
                    if (region > 0 && !IsCompletedMoatTile(neighbourTile))
                        graph.AddUndirected(moatNode, region);
                }
            }
            return graph;
        }

        private bool HasFastFriendlyMoatBridgeForCells(
            int playerId, IList<int> starts, IList<int> targets)
        {
            if (starts == null || targets == null) return false;
            var uniqueStarts = new Dictionary<int, int>();
            var uniqueTargets = new Dictionary<int, int>();
            foreach (int start in starts)
            {
                if ((uint)start >= MapCellCount) continue;
                int startTile = GameTileManagerAPI.Instance.GetTileId(
                    start % MapWidth, start / MapWidth);
                if (IsValidTileId(startTile))
                    uniqueStarts[FastBridgeEndpointNode(startTile)] = startTile;
            }
            foreach (int target in targets)
            {
                if ((uint)target >= MapCellCount) continue;
                int targetTile = GameTileManagerAPI.Instance.GetTileId(
                    target % MapWidth, target / MapWidth);
                if (IsValidTileId(targetTile))
                    uniqueTargets[FastBridgeEndpointNode(targetTile)] = targetTile;
            }
            foreach (int startTile in uniqueStarts.Values)
            foreach (int targetTile in uniqueTargets.Values)
            {
                if (HasFastFriendlyMoatBridge(playerId, startTile, targetTile))
                    return true;
            }
            return false;
        }

        private List<int> GetFastBridgeNodes(FastMoatGraph graph, int tileId)
        {
            var result = new List<int>(8);
            if (graph.FriendlyMoatTiles.Contains(tileId))
            {
                result.Add(FastMoatNode(tileId));
                return result;
            }
            int region = pathRegionGrid[tileId];
            if (region > 0 && !IsCompletedMoatTile(tileId))
            {
                result.Add(region);
                return result;
            }

            var position = GameTileManagerAPI.Instance.GetTileVectorFromId(tileId);
            for (int direction = 0; direction < 8; direction++)
            {
                int x = position.X + WeightedMoatRoutePlanner.DirectionX[direction];
                int y = position.Y + WeightedMoatRoutePlanner.DirectionY[direction];
                if ((uint)x >= MapWidth || (uint)y >= MapWidth)
                    continue;
                int neighbour = GameTileManagerAPI.Instance.GetTileId(x, y);
                int node = graph.FriendlyMoatTiles.Contains(neighbour)
                    ? FastMoatNode(neighbour)
                    : IsValidTileId(neighbour) && pathRegionGrid[neighbour] > 0 &&
                      !IsCompletedMoatTile(neighbour) ? pathRegionGrid[neighbour] : 0;
                if (node != 0 && !result.Contains(node)) result.Add(node);
            }
            return result;
        }

        private bool TryProbeFastCursorRoute(
            int playerId, int startTileId, int targetTileId, out RouteProbeSummary summary)
        {
            var key = new FastCursorRouteKey(
                mapEpoch, placementRevision, playerId, startTileId, targetTileId);
            if (fastCursorRoutes.TryGetValue(key, out summary))
                return summary.RouteFound;
            summary = new RouteProbeSummary(playerId)
            {
                StartRegion = pathRegionGrid[startTileId],
                TargetRegion = pathRegionGrid[targetTileId],
                AttackProbeEvaluated = true
            };
            if (!HasFastFriendlyMoatBridge(playerId, startTileId, targetTileId))
            {
                fastCursorRoutes[key] = summary;
                return false;
            }
            var start = GameTileManagerAPI.Instance.GetTileVectorFromId(startTileId);
            var target = GameTileManagerAPI.Instance.GetTileVectorFromId(targetTileId);
            weightedMoatRoutePlanner.SetSearchSession(
                fastCursorSearchOwner, playerId, mapEpoch, placementRevision);
            long beforeNodes = weightedMoatRoutePlanner.SearchNodes;
            long started = Stopwatch.GetTimestamp();
            fastSearches++;
            bool found = weightedMoatRoutePlanner.TryProbeReachability(
                playerId, start.X, start.Y, target.X, target.Y, false,
                MoatTraversalPolicy.FriendlyOnly, out WeightedMoatRouteSummary route,
                FastSearchNodeBudget);
            RecordFastSearch(route, started, beforeNodes);
            summary.ReachedWithMoat = found && route.MoatEdges > 0;
            summary.FriendlyMoatTiles = route.MoatEdges;
            summary.RouteDistance = found ? route.RouteLength : int.MaxValue;
            summary.TargetedExpandedNodes = route.ExpandedNodes;
            summary.TargetedSearchMilliseconds = route.SearchMilliseconds;
            summary.RouteFound = summary.ReachedWithMoat;
            fastCursorRoutes[key] = summary;
            return summary.RouteFound;
        }

        private void RecordFastSearch(
            WeightedMoatRouteSummary summary, long started, long nodesBefore)
        {
            int expanded = (int)Math.Min(int.MaxValue, Math.Max(
                summary.ExpandedNodes, weightedMoatRoutePlanner.SearchNodes - nodesBefore));
            fastExpandedNodes += expanded;
            fastMaximumExpandedNodes = Math.Max(fastMaximumExpandedNodes, expanded);
            fastMaximumSearchTicks = Math.Max(
                fastMaximumSearchTicks, Stopwatch.GetTimestamp() - started);
            if (string.Equals(summary.Reason, "search-budget-exceeded", StringComparison.Ordinal))
                fastBudgetAborts++;
        }

        private void RecordFastFieldSearch(MoatCandidateField field, long started)
        {
            if (field == null) return;
            fastSearches++;
            fastExpandedNodes += field.Expanded;
            fastMaximumExpandedNodes = Math.Max(fastMaximumExpandedNodes, field.Expanded);
            fastMaximumSearchTicks = Math.Max(
                fastMaximumSearchTicks, Stopwatch.GetTimestamp() - started);
            if (field.BudgetExceeded) fastBudgetAborts++;
        }

        private static int FastMoatNode(int tileId) => MaximumRegionId + 1 + tileId;

        private sealed class FastMoatGraph
        {
            public FastMoatGraph(int mapEpoch, long revision)
            {
                MapEpoch = mapEpoch;
                Revision = revision;
            }
            public int MapEpoch { get; }
            public long Revision { get; }
            public HashSet<int> FriendlyMoatTiles { get; } = new HashSet<int>();
            public Dictionary<int, FastTilePosition> Coordinates { get; } =
                new Dictionary<int, FastTilePosition>();
            public Dictionary<int, List<int>> Edges { get; } =
                new Dictionary<int, List<int>>();
            public void EnsureNode(int node)
            {
                if (!Edges.ContainsKey(node)) Edges[node] = new List<int>();
            }
            public void AddUndirected(int first, int second)
            {
                EnsureNode(first); EnsureNode(second);
                if (!Edges[first].Contains(second)) Edges[first].Add(second);
                if (!Edges[second].Contains(first)) Edges[second].Add(first);
            }
        }

        private sealed class DeferredFastMoveScope
        {
            public DeferredFastMoveScope(
                int mapEpoch, int tribeId, long expiresAt, IEnumerable<int> unitIds)
            {
                MapEpoch = mapEpoch;
                TribeId = tribeId;
                ExpiresAt = expiresAt;
                UnitIds = new HashSet<int>(unitIds);
            }
            public int MapEpoch { get; }
            public int TribeId { get; }
            public long ExpiresAt { get; }
            public HashSet<int> UnitIds { get; }
        }

        private readonly struct FastTilePosition
        {
            public FastTilePosition(int x, int y) { X = x; Y = y; }
            public int X { get; }
            public int Y { get; }
        }

        private readonly struct FastCursorRouteKey : IEquatable<FastCursorRouteKey>
        {
            public FastCursorRouteKey(int epoch, long revision, int player, int start, int target)
            { Epoch = epoch; Revision = revision; Player = player; Start = start; Target = target; }
            private int Epoch { get; }
            private long Revision { get; }
            private int Player { get; }
            private int Start { get; }
            private int Target { get; }
            public bool Equals(FastCursorRouteKey other) => Epoch == other.Epoch &&
                Revision == other.Revision && Player == other.Player &&
                Start == other.Start && Target == other.Target;
            public override bool Equals(object obj) => obj is FastCursorRouteKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Epoch;
                    hash = hash * 397 ^ Revision.GetHashCode();
                    hash = hash * 397 ^ Player;
                    hash = hash * 397 ^ Start;
                    return hash * 397 ^ Target;
                }
            }
        }

        private readonly struct FastMoatBridgeKey : IEquatable<FastMoatBridgeKey>
        {
            public FastMoatBridgeKey(int epoch, long revision, int player, int start, int target)
            { Epoch = epoch; Revision = revision; Player = player; Start = start; Target = target; }
            private int Epoch { get; }
            private long Revision { get; }
            private int Player { get; }
            private int Start { get; }
            private int Target { get; }
            public bool Equals(FastMoatBridgeKey other) => Epoch == other.Epoch &&
                Revision == other.Revision && Player == other.Player &&
                Start == other.Start && Target == other.Target;
            public override bool Equals(object obj) => obj is FastMoatBridgeKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Epoch;
                    hash = hash * 397 ^ Revision.GetHashCode();
                    hash = hash * 397 ^ Player;
                    hash = hash * 397 ^ Start;
                    return hash * 397 ^ Target;
                }
            }
        }
    }
}
