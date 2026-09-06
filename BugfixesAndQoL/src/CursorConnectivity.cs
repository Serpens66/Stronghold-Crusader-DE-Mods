using System;
using System.Collections.Generic;
using System.Diagnostics;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        private sealed class CursorTopology
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
        private readonly Dictionary<int, CursorTopology> cursorTopologies = new Dictionary<int, CursorTopology>();
        private readonly Dictionary<int, SelectedCursorUnitSnapshot> cursorSources = new Dictionary<int, SelectedCursorUnitSnapshot>();
        private readonly Dictionary<int, int> cursorSourceCounts = new Dictionary<int, int>();
        private BuildingCursorConnectivityScope activeBuildingCursorConnectivity;
        private sealed class BuildingCursorConnectivityScope
        {
            public int UnitId, PlayerId, StartTile, BuildingId;
            public uint UnitGlobalId, BuildingGlobalId;
        }

        private int CallBuildingCursorWithRegions(IntPtr manager, int buildingId, int unitId)
        {
            if (disposed || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null ||
                !CanDigMoat(unit) || !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) || building == null)
                return originalBuildingCursorReachability(manager, buildingId, unitId);
            var previous = activeBuildingCursorConnectivity;
            activeBuildingCursorConnectivity = new BuildingCursorConnectivityScope {
                UnitId = unitId, UnitGlobalId = unit->r_GlobalId, PlayerId = unit->r_ControllableForPlayerId,
                StartTile = unchecked((int)unit->r_CurrentPositionTileId), BuildingId = buildingId, BuildingGlobalId = building->r_GlobalId };
            try { return originalBuildingCursorReachability(manager, buildingId, unitId); }
            finally { activeBuildingCursorConnectivity = previous; }
        }

        private bool TryAnswerBuildingCursorPair(int nativeStart, int candidate, byte useCache, out int result)
        {
            result = 0;
            var scope = activeBuildingCursorConnectivity;
            // B70C0 passes E2CA0 arguments in the opposite order to the direct cursor.
            // Its footprint enumeration and strict native height test run unchanged.
            if (scope == null || useCache != 0 || nativeStart != scope.StartTile) return false;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(scope.UnitId, out GameUnit* unit) || unit == null ||
                unit->r_GlobalId != scope.UnitGlobalId || unit->r_ControllableForPlayerId != scope.PlayerId ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(scope.BuildingId, out GameBuilding* building) || building == null ||
                building->r_GlobalId != scope.BuildingGlobalId) return true;
            if (ProbeCursorConnectivity(scope.PlayerId, nativeStart, candidate, out RouteProbeSummary summary))
                result = summary.RouteFound ? 1 : 0;
            return true;
        }
        private int[] selectedCursorIds = Array.Empty<int>();
        private CursorSelectionIdentity[] selectedCursorIdentity = Array.Empty<CursorSelectionIdentity>();
        private int cursorSelectionRevision;
        private string cursorSelectionToken = string.Empty;
        private ulong cursorDiplomacy;
        private byte* nativePortalGateStates;
        private long cursorTopologyBuilds, cursorTopologyUpdates, cursorQueries, cursorQueryTicks, cursorTopologyTicks;
        private long cursorLastLogTime, cursorLastLogQueries;
        private struct CursorSelectionIdentity
        {
            public int Id, X, Y, Player, Type, Alive;
            public uint Global;
            public bool Same(CursorSelectionIdentity other) => Id == other.Id && X == other.X && Y == other.Y &&
                Player == other.Player && Type == other.Type && Alive == other.Alive && Global == other.Global;
        }

        private int CursorNode(int player, int tile)
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

        private void DirtyCursorTile(int tile)
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

        private void UpdateCursorBoundary(CursorTopology topology, int player, int tile)
        {
            UpdateGroundBoundary(topology, player, tile);
            if (topology.Boundaries.TryGetValue(tile, out long[] old))
            {
                foreach (long edge in old)
                {
                    int source = (int)((edge >> 32) & 0x7FFFFFFF), destination = (int)edge;
                    topology.Graph.ChangeEdge(source, destination, -1);
                    if (edge < 0) topology.Ground.ChangeEdge(source, destination, -1);
                }
                topology.Boundaries.Remove(tile);
            }
            int from = CursorNode(player, tile);
            if (from < 0) return;
            var pos = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            // Small stack buffer: ordinary region interiors allocate nothing.
            long* edges = stackalloc long[8]; int count = 0;
            for (int d = 0; d < 8; d++)
            {
                int x = pos.X + WeightedMoatRoutePlanner.DirectionX[d], y = pos.Y + WeightedMoatRoutePlanner.DirectionY[d];
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                int next = GameTileManagerAPI.Instance.GetTileId(x, y), to = CursorNode(player, next);
                if (to < 0 || from == to) continue;
                // Different ordinary PCLs connect through native portal records, not
                // by assuming that a physically adjacent pair bypasses its gate filter.
                if (from <= MaximumRegionId && to <= MaximumRegionId) continue;
                if (!weightedMoatRoutePlanner.TryGetTraversalEdge(player, pos.X, pos.Y, tile,
                    x, y, next, d, false, false, MoatTraversalPolicy.FriendlyOnly, out _, out _)) continue;
                long packed = ((long)from << 32) | (uint)to;
                if (!IsCompletedMoatTile(tile) && !IsCompletedMoatTile(next))
                { packed |= long.MinValue; topology.Ground.ChangeEdge(from, to, 1); }
                edges[count++] = packed;
                topology.Graph.ChangeEdge(from, to, 1);
            }
            if (count == 0) return;
            var saved = new long[count];
            for (int i = 0; i < count; i++) saved[i] = edges[i];
            topology.Boundaries.Add(tile, saved);
        }

        private enum GroundConnectionDecision { Unknown, Reachable, Excluded }

        private bool IsSamePositiveGroundRegion(int start, int target)
        {
            if (!IsValidTileId(start) || !IsValidTileId(target) ||
                IsCompletedMoatTile(start) || IsCompletedMoatTile(target) ||
                ((tileFlags[start] | tileFlags[target]) & CursorSpecialStructureTileFlagMask) != 0)
            {
                return false;
            }

            int startRegion = pathRegionGrid[start];
            return startRegion > 0 && startRegion == pathRegionGrid[target];
        }

        private GroundConnectionDecision ProbeGroundConnection(int player, int start, int target)
        {
            if (!IsValidTileId(start) || !IsValidTileId(target)) return GroundConnectionDecision.Unknown;
            if (IsCompletedMoatTile(start) || IsCompletedMoatTile(target)) return GroundConnectionDecision.Excluded;
            if (start == target) return GroundConnectionDecision.Reachable;
            if (nativePathManager == IntPtr.Zero || !cursorTopologies.TryGetValue(player, out CursorTopology topology) ||
                !topology.Ready || topology.Epoch != mapEpoch || topology.Dirty.Count != 0 ||
                topology.RegionGeneration != *(int*)((byte*)nativePathManager + 0x74)) return GroundConnectionDecision.Unknown;
            return topology.GroundUpper.CanReach(GroundUpperNode(start), GroundUpperNode(target))
                ? GroundConnectionDecision.Unknown : GroundConnectionDecision.Excluded;
        }

        private int GroundUpperNode(int tile)
        {
            if (!IsValidTileId(tile) || IsCompletedMoatTile(tile)) return -1;
            int region = pathRegionGrid[tile];
            return region > 0 && region <= MaximumRegionId &&
                (tileFlags[tile] & CursorSpecialStructureTileFlagMask) == 0
                ? region : MaximumRegionId + 1 + tile;
        }

        private void UpdateGroundBoundary(CursorTopology topology, int player, int tile)
        {
            if (topology.GroundBoundaries.TryGetValue(tile, out long[] old))
            {
                foreach (long pair in old) topology.GroundUpper.ChangeEdge((int)(pair >> 32), (int)pair, -1);
                topology.GroundBoundaries.Remove(tile);
            }
            int from = GroundUpperNode(tile);
            if (from < 0) return;
            var pos = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            long* pairs = stackalloc long[8]; int count=0;
            for (int d=0;d<8;d++)
            {
                int x=pos.X+WeightedMoatRoutePlanner.DirectionX[d],y=pos.Y+WeightedMoatRoutePlanner.DirectionY[d];
                if ((uint)x>=MapWidth || (uint)y>=MapWidth) continue;
                int next=GameTileManagerAPI.Instance.GetTileId(x,y),to=GroundUpperNode(next);
                if (to<0 || from==to) continue;
                if (!weightedMoatRoutePlanner.TryGetTraversalEdge(player,pos.X,pos.Y,tile,x,y,next,d,false,false,
                    MoatTraversalPolicy.GroundOnly,out _,out _)) continue;
                pairs[count++]=((long)from<<32)|(uint)to;
                topology.GroundUpper.ChangeEdge(from,to,1);
            }
            if (count==0) return;
            var saved=new long[count]; for(int i=0;i<count;i++)saved[i]=pairs[i];
            topology.GroundBoundaries.Add(tile,saved);
        }

        private CursorTopology EnsureCursorTopology(int player, bool buildConnections = true)
        {
            ulong diplomacy = 0;
            for (int a = 1; a <= 8; a++)
                for (int b = 1; b <= 8; b++)
                    if (a == b || GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(a, b))
                        diplomacy |= 1UL << ((a - 1) * 8 + b - 1);
            if (cursorDiplomacy != diplomacy) { cursorTopologies.Clear(); cursorDiplomacy = diplomacy; }
            int generation = *(int*)((byte*)nativePathManager + 0x74);
            if (!cursorTopologies.TryGetValue(player, out CursorTopology topology) ||
                topology.Epoch != mapEpoch || topology.RegionGeneration != generation)
            {
                topology = new CursorTopology { Epoch = mapEpoch, RegionGeneration = generation };
                cursorTopologies[player] = topology;
            }
            RefreshCursorPortals(topology, player);
            if (!buildConnections) return topology;
            if (topology.Ready && topology.Dirty.Count == 0) return topology;
            long started = Stopwatch.GetTimestamp();
            weightedMoatRoutePlanner.BeginReachabilityProbe();
            try
            {
                if (!topology.Ready)
                {
                    cursorTopologyBuilds++;
                    for (int tile = 0; tile < NativeTileCount; tile++) UpdateCursorBoundary(topology, player, tile);
                    topology.Ready = true;
                }
                else
                    foreach (int tile in topology.Dirty) { UpdateCursorBoundary(topology, player, tile); cursorTopologyUpdates++; }
                topology.Dirty.Clear();
            }
            finally { weightedMoatRoutePlanner.EndReachabilityProbe(); cursorTopologyTicks += Stopwatch.GetTimestamp() - started; }
            return topology;
        }

        private bool ProbeCursorConnectivity(int player, int start, int target, out RouteProbeSummary summary)
        {
            summary = new RouteProbeSummary(player);
            if (!ExtensionsEnabled || !IsValidTileId(start) || !IsValidTileId(target)) return false;
            long started = Stopwatch.GetTimestamp(); cursorQueries++;
            try
            {
                var topology = EnsureCursorTopology(player, false);
                int from = CursorNode(player, start), to = CursorNode(player, target);
                // An unchanged native ground region already proves connectivity. In
                // this common hover case even the first moat topology build is unnecessary.
                bool normal = from > 0 && from <= MaximumRegionId && from == to;
                if (!normal && from >= 0 && to >= 0 && (!topology.Ready || topology.Dirty.Count != 0))
                    topology = EnsureCursorTopology(player);
                normal |= topology.Ready && topology.Ground.CanReach(from, to) && !IsCompletedMoatTile(start) && !IsCompletedMoatTile(target);
                bool reachable = normal || topology.Ready && topology.Graph.CanReach(from, to);
                summary.StartRegion = pathRegionGrid[start]; summary.TargetRegion = pathRegionGrid[target];
                summary.AttackProbeEvaluated = true; summary.ReachedWithoutMoat = normal;
                summary.ReachedWithMoat = reachable && !normal;
                summary.FriendlyMoatTiles = summary.ReachedWithMoat ? 1 : 0;
                summary.RouteFound = reachable;
                return true;
            }
            finally { cursorQueryTicks += Stopwatch.GetTimestamp() - started; }
        }

        private void ObserveCursorPerformance()
        {
            long now = Stopwatch.GetTimestamp();
            if (cursorQueries == cursorLastLogQueries || now - cursorLastLogTime < Stopwatch.Frequency * 5L) return;
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
        }

        private void RefreshCursorPortals(CursorTopology topology, int player)
        {
            // Read-only first-phase E2610 portal contract (FBCB9319). Its second
            // phase permits blocked transitions and is NOT walkable connectivity.
            int* context = (int*)nativePathManager;
            int count = Math.Min(Math.Max(context[0], 1), 200);
            long* candidate = stackalloc long[6];
            topology.NextBlockedBuildings.Clear();
            for (int id = 1; id < 200; id++)
            {
                int n = 0;
                int offset = id * 0x81;
                if (id < count && context[offset + 0x809] == 1)
                {
                    int building = context[offset + 0x80C];
                    int owner = context[offset + 0x882];
                    bool friendly = owner == player || owner > 0 && owner <= 8 && GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(player, owner);
                    bool open = nativePortalGateStates != null && building > 0 && building <= 10000 &&
                        *(short*)(nativePortalGateStates + building * 0x32C) != 0;
                    bool allowed = context[offset + 0x80A] != 1 && context[offset + 0x80F] != 0 && (friendly || open);
                    if (!allowed && building > 0) topology.NextBlockedBuildings.Add(building);
                    if (allowed)
                    {
                        int a = context[offset + 0x816], b = context[offset + 0x817], c = context[offset + 0x883];
                        AddPortalCandidate(candidate, ref n, a, b); AddPortalCandidate(candidate, ref n, b, a);
                        AddPortalCandidate(candidate, ref n, a, c); AddPortalCandidate(candidate, ref n, c, a);
                        AddPortalCandidate(candidate, ref n, b, c); AddPortalCandidate(candidate, ref n, c, b);
                    }
                }
                long[] old = topology.Portals[id];
                bool same = (old?.Length ?? 0) == n;
                if (same) for (int i = 0; i < n; i++) if (old[i] != candidate[i]) { same = false; break; }
                if (same) continue;
                if (old != null) foreach (long edge in old)
                {
                    topology.Graph.ChangeEdge((int)(edge >> 32), (int)edge, -1);
                    topology.Ground.ChangeEdge((int)(edge >> 32), (int)edge, -1);
                }
                long[] next = n == 0 ? null : new long[n];
                for (int i = 0; i < n; i++)
                {
                    next[i] = candidate[i];
                    topology.Graph.ChangeEdge((int)(candidate[i] >> 32), (int)candidate[i], 1);
                    topology.Ground.ChangeEdge((int)(candidate[i] >> 32), (int)candidate[i], 1);
                }
                topology.Portals[id] = next;
            }
            foreach (int building in topology.BlockedBuildings)
                if (!topology.NextBlockedBuildings.Contains(building)) DirtyCursorBuilding(building);
            foreach (int building in topology.NextBlockedBuildings)
                if (!topology.BlockedBuildings.Contains(building)) DirtyCursorBuilding(building);
            topology.BlockedBuildings.Clear();
            foreach (int building in topology.NextBlockedBuildings) topology.BlockedBuildings.Add(building);
        }

        private static void AddPortalCandidate(long* edges, ref int count, int from, int to)
        {
            if (from > 0 && from <= MaximumRegionId && to > 0 && to <= MaximumRegionId && from != to)
                edges[count++] = ((long)from << 32) | (uint)to;
        }

        private void DirtyCursorBuilding(int id)
        {
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* building) || building == null) return;
            int minX = Math.Max(0, Math.Min(building->r_TilePositionXBegin, building->r_TilePositionXEnd) - 2);
            int maxX = Math.Min(MapWidth - 1, Math.Max(building->r_TilePositionXBegin, building->r_TilePositionXEnd) + 2);
            int minY = Math.Max(0, Math.Min(building->r_TilePositionYBegin, building->r_TilePositionYEnd) - 2);
            int maxY = Math.Min(MapWidth - 1, Math.Max(building->r_TilePositionYBegin, building->r_TilePositionYEnd) + 2);
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (IsValidTileId(tile)) foreach (var entry in cursorTopologies) entry.Value.Dirty.Add(tile);
            }
        }

        private bool cursorSelectionAvailable;
        private readonly Dictionary<string, long> cursorDecisionCounts = new Dictionary<string, long>();
        private readonly HashSet<string> cursorDecisionDetails = new HashSet<string>();

        private void RecordCursorDecision(string reason, AttackCursorPairScope scope)
        {
            cursorDecisionCounts.TryGetValue(reason, out long count);
            cursorDecisionCounts[reason] = count + 1;
            if (!cursorDecisionDetails.Add(reason)) return;
            LogDetailedInfo($"Bugfixes and QoL stage=friendly-moat-movement-cursor-decision reason={reason} " +
                $"selectionSourceAvailable={cursorSelectionAvailable} selected={selectedCursorIds.Length} " +
                $"unit={scope?.UnitId ?? 0} target=({scope?.TargetX},{scope?.TargetY}) kind={scope?.FallbackKind}.");
        }

        private bool CaptureCursorSelection(int player, out int[] ids, out string token)
        {
            SelectedUnitInfo[] selected =
                GamePlayerManagerAPI.Instance.GetSelectedChimps() ?? Array.Empty<SelectedUnitInfo>();
            int count = selected.Length;
            ids = selectedCursorIds; token = cursorSelectionToken;
            cursorSelectionAvailable = true;
            bool changed = selectedCursorIds.Length != count;
            if (changed) { selectedCursorIds = new int[count]; selectedCursorIdentity = new CursorSelectionIdentity[count]; }
            int valid = 0;
            for (int i = 0; i < count; i++)
            {
                int id = selected[i].UnitId;
                var identity = new CursorSelectionIdentity { Id = id };
                if (id > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* unit) && unit != null)
                {
                    identity.Global = unit->r_GlobalId; identity.X = unit->r_CurrentTilePositionX; identity.Y = unit->r_CurrentTilePositionY;
                    identity.Player = unit->r_ControllableForPlayerId; identity.Type = (int)unit->r_UnitChimp; identity.Alive = (int)unit->r_AliveState;
                    if (unit->r_AliveState == AliveState.IsAlive && (player < 0 || identity.Player == player)) valid++;
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
