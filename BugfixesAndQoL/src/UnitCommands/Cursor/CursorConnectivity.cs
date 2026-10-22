using System;
using System.Collections.Generic;
using System.Diagnostics;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        internal sealed class CursorTopology
        {
            public readonly CursorRegionGraph Ground = new CursorRegionGraph(NativeTileCount + MaximumRegionId + 1);
            // Overapproximation of the actual GroundOnly kernel, independent of
            // cursor portal/owner exclusions. Only negative answers are proofs.
            public readonly CursorRegionGraph GroundUpper = new CursorRegionGraph(NativeTileCount + MaximumRegionId + 1);
            public readonly Dictionary<int, long[]> GroundBoundaries = new Dictionary<int, long[]>();
            public readonly long[][] Portals = new long[200][];
            public readonly HashSet<int> BlockedBuildings = new HashSet<int>();
            public readonly HashSet<int> NextBlockedBuildings = new HashSet<int>();
            public readonly CursorRegionGraph Graph = new CursorRegionGraph(NativeTileCount + MaximumRegionId + 1);
            public readonly Dictionary<int, long[]> Boundaries = new Dictionary<int, long[]>();
            public readonly HashSet<int> Dirty = new HashSet<int>();
            public int Epoch, RegionGeneration;
            public bool Ready;
        }
        internal readonly Dictionary<int, CursorTopology> cursorTopologies = new Dictionary<int, CursorTopology>();
        internal BuildingCursorConnectivityScope activeBuildingCursorConnectivity;
        internal sealed class BuildingCursorConnectivityScope
        {
            public int UnitId, PlayerId, StartTile, BuildingId;
            public uint UnitGlobalId, BuildingGlobalId;
        }

        internal int CallBuildingCursorWithRegions(IntPtr manager, int buildingId, int unitId)
        {
            if (disposed || unitId <= 0 || buildingId <= 0 ||
                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null ||
                (!ManualCommandsEnabled && !CanDigMoat(unit)) || !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) || building == null)
                return originalBuildingCursorReachability(manager, buildingId, unitId);
            var previous = activeBuildingCursorConnectivity;
            activeBuildingCursorConnectivity = new BuildingCursorConnectivityScope {
                UnitId = unitId, UnitGlobalId = unit->r_GlobalId, PlayerId = unit->r_ControllableForPlayerId,
                StartTile = unchecked((int)unit->r_CurrentPositionTileId), BuildingId = buildingId, BuildingGlobalId = building->r_GlobalId };
            try { return originalBuildingCursorReachability(manager, buildingId, unitId); }
            finally { activeBuildingCursorConnectivity = previous; }
        }

        internal bool TryAnswerBuildingCursorPair(int nativeStart, int candidate, byte useCache, out int result)
        {
            result = 0;
            var scope = activeBuildingCursorConnectivity;
            // B70C0 passes E2CA0 arguments in the opposite order to the direct cursor.
            // Its footprint enumeration and strict native height test run unchanged.
            if ((!ManualCommandsEnabled && !TraversalEnabled) || scope == null || useCache != 0 || nativeStart != scope.StartTile) return false;
            if (!APIShared.UnitAccess.TryGetById(scope.UnitId, out GameUnit* unit, out _) || unit == null ||
                unit->r_GlobalId != scope.UnitGlobalId || unit->r_ControllableForPlayerId != scope.PlayerId ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(scope.BuildingId, out GameBuilding* building) || building == null ||
                building->r_GlobalId != scope.BuildingGlobalId) return true;
            if (ProbeCursorConnectivity(scope.PlayerId, nativeStart, candidate, out RouteProbeSummary summary))
                result = summary.RouteFound ? 1 : 0;
            return true;
        }
        internal int[] selectedCursorIds = Array.Empty<int>();
        internal CursorSelectionIdentity[] selectedCursorIdentity = Array.Empty<CursorSelectionIdentity>();
        internal int cursorSelectionRevision;
        internal string cursorSelectionToken = string.Empty;
        internal ulong cursorDiplomacy;
        internal byte* nativePortalGateStates;
        internal long cursorTopologyBuilds,cursorTopologyUpdates;
        internal long cursorLastLogTime, cursorLastLogQueries;
        internal struct CursorSelectionIdentity
        {
            public int Id, X, Y, Player, Type, Alive;
            public uint Global;
            public bool Same(CursorSelectionIdentity other) => Id == other.Id && X == other.X && Y == other.Y &&
                Player == other.Player && Type == other.Type && Alive == other.Alive && Global == other.Global;
        }

        internal int CursorNode(int player, int tile)
        {
            if (!IsValidTileId(tile)) return -1;
            int buildingId = nativeBuildingLayer[tile];
            if (buildingId != 0 && cursorTopologies.TryGetValue(player, out CursorTopology topology) &&
                topology.BlockedBuildings.Contains(buildingId)) return -1;
            if (IsCompletedMoatTile(tile))
                return IsFriendlyCompletedMoatForWeightedShadow(player, tile) ? MaximumRegionId + 1 + tile : -1;
            int region = pathRegionGrid[tile];
            // Preserve directed structure edges rather than merging wall/ramp tiles by PCL.
            if ((tileFlags[tile] & CursorSpecialStructureTileFlagMask) != 0)
                return MaximumRegionId + 1 + tile; // Incoming edges may reach a directed endpoint with no exits.
            return region > 0 && region <= MaximumRegionId ? region : -1;
        }

        internal void DirtyCursorTile(int tile)
        {
            if (!IsValidTileId(tile) || cursorTopologies.Count == 0) return;
            var pos = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            // A native 3x3 update also changes edges originating just outside that area.
            for (int y = Math.Max(0, pos.Y - 2); y <= Math.Min(MapWidth - 1, pos.Y + 2); y++)
                for (int x = Math.Max(0, pos.X - 2); x <= Math.Min(MapWidth - 1, pos.X + 2); x++)
                {
                    int affected = GameTileManagerAPI.Instance.GetTileId(x, y);
                    if (IsValidTileId(affected)) foreach (var entry in cursorTopologies) entry.Value.Dirty.Add(affected);
                }
        }

        internal bool ProbeCursorConnectivity(int player, int start, int target, out RouteProbeSummary summary)
        {
            summary = new RouteProbeSummary(player);
            if (!ExtensionsEnabled || !IsValidTileId(start) || !IsValidTileId(target)) return false;
            if (!TraversalEnabled) return ProbeNativeCursorConnectivity(player, start, target, out summary);
            return Traversal.ProbeCursorConnectivityTraversal(player, start, target, out summary);
        }

        internal void ObserveCursorPerformance()
        {
#if SERP_DEVDIAG
            if (disposed)
                return;
            // The friendly-moat cursor counters live in the MoatMove provider; without it they stay zero.
            UnitCommandTraversalProvider provider = Traversal;
            long cursorQueries = provider?.CursorQueryCount ?? 0;
            long cursorQueryTicks = provider?.CursorQueryTicks ?? 0;
            long cursorTopologyTicks = provider?.CursorTopologyTicks ?? 0;
            if (cursorQueries == cursorLastLogQueries)
                return;

            long now = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
            if (now - cursorLastLogTime < Stopwatch.Frequency * 5L) return;
            cursorLastLogTime = now; cursorLastLogQueries = cursorQueries;
            long nodes = 0, hits = 0;
            foreach (var entry in cursorTopologies)
            {
                nodes += entry.Value.Graph.ExpandedNodes + entry.Value.Ground.ExpandedNodes;
                hits += entry.Value.Graph.CacheHits + entry.Value.Ground.CacheHits;
            }
            var decisions = new System.Text.StringBuilder();
            foreach (var entry in cursorDecisionCounts)
            {
                if (decisions.Length != 0) decisions.Append(',');
                decisions.Append(entry.Key).Append(':').Append(entry.Value);
            }
            LogDetailedInfo($"Bugfixes and QoL stage=friendly-moat-movement-cursor-performance selected={selectedCursorIds.Length} queries={cursorQueries} " +
                $"regionCacheHits={hits} regionNodes={nodes} topologyBuilds={cursorTopologyBuilds} tileUpdates={cursorTopologyUpdates} " +
                $"queryMs={cursorQueryTicks * 1000.0 / Stopwatch.Frequency:F3} topologyMs={cursorTopologyTicks * 1000.0 / Stopwatch.Frequency:F3} " +
                $"selectionSourceAvailable={cursorSelectionAvailable} decisions=[{decisions}] pathSearches=0 " +
                $"placementBatches={placementCalls} placementSlots={placementSlots} placementRollbacks={placementRollbacks} " +
                $"unstackCalls={unstackCalls} unstackMoves={unstackMoves} " +
                $"formationRejected={formationRejected} formationReplaced={formationReplaced} formationFallbacks={formationFallbacks} " +
                $"fillRoutes=[{string.Join(";", fillRouteDecisions)}].");
#endif
        }

        internal bool cursorSelectionAvailable;
#if SERP_DEVDIAG
        internal readonly Dictionary<string, long> cursorDecisionCounts = new Dictionary<string, long>();

        internal readonly HashSet<string> cursorDecisionDetails = new HashSet<string>();
#endif

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void RecordCursorDecision(string reason, AttackCursorPairScope scope)
        {
#if SERP_DEVDIAG
            cursorDecisionCounts.TryGetValue(reason, out long count);
            cursorDecisionCounts[reason] = count + 1;
            if (!cursorDecisionDetails.Add(reason)) return;
            LogDetailedInfo($"Bugfixes and QoL stage=friendly-moat-movement-cursor-decision reason={reason} " +
                $"selectionSourceAvailable={cursorSelectionAvailable} selected={selectedCursorIds.Length} " +
                $"unit={scope?.UnitId ?? 0} target=({scope?.TargetX},{scope?.TargetY}) kind={scope?.FallbackKind}.");
#endif
        }

        internal bool CaptureCursorSelection(int player, out int[] ids, out string token)
        {
            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            if (localPlayerId < 1 || localPlayerId > 8)
            {
                ids = Array.Empty<int>();
                token = string.Empty;
                cursorSelectionAvailable = false;
                return false;
            }
            if (!APIShared.LocalSelectionAPI.TryCapture(localPlayerId, out APIShared.LocalSelectionSnapshot selected))
            {
                ids = Array.Empty<int>();
                token = string.Empty;
                cursorSelectionAvailable = false;
                return false;
            }
            int count = selected.Count;
            ids = selectedCursorIds; token = cursorSelectionToken;
            cursorSelectionAvailable = true;
            bool changed = selectedCursorIds.Length != count;
            if (changed) { selectedCursorIds = new int[count]; selectedCursorIdentity = new CursorSelectionIdentity[count]; }
            int valid = 0;
            for (int i = 0; i < count; i++)
            {
                int id = selected[i].UnitId;
                var identity = new CursorSelectionIdentity { Id = id };
                if (id > 0 && APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null)
                {
                    identity.Global = unit->r_GlobalId; identity.X = unit->r_CurrentTilePositionX; identity.Y = unit->r_CurrentTilePositionY;
                    identity.Player = unit->r_ControllableForPlayerId; identity.Type = (int)unit->r_UnitChimp; identity.Alive = (int)unit->r_AliveState;
                    if (APIShared.UnitAccess.IsReallyAlive(unit) && (player < 0 || identity.Player == player)) valid++;
                }
                changed |= !identity.Same(selectedCursorIdentity[i]);
                selectedCursorIdentity[i] = identity; selectedCursorIds[i] = id;
            }
            if (changed) cursorSelectionToken = (++cursorSelectionRevision).ToString(System.Globalization.CultureInfo.InvariantCulture);
            ids = selectedCursorIds; token = cursorSelectionToken;
            return valid > 0;
        }
    }
}
