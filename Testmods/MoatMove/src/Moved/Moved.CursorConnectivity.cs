extern alias BugfixesRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL CursorConnectivity.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal readonly Dictionary<int, SelectedCursorUnitSnapshot> cursorSources = new Dictionary<int, SelectedCursorUnitSnapshot>();
        internal readonly Dictionary<int, int> cursorSourceCounts = new Dictionary<int, int>();
        internal long cursorQueries,cursorQueryTicks,cursorTopologyTicks;
        // Read by BugfixesAndQoL's DEVDIAG cursor performance summary.
        internal override long CursorQueryCount => cursorQueries;
        internal override long CursorQueryTicks => cursorQueryTicks;
        internal override long CursorTopologyTicks => cursorTopologyTicks;

        internal void UpdateCursorBoundary(CursorTopology topology, int player, int tile)
        {
            if (topology.Boundaries.TryGetValue(tile, out long[] old))
            {
                foreach (long edge in old) topology.Graph.ChangeEdge((int)(edge >> 32), (int)edge, -1);
                topology.Boundaries.Remove(tile);
            }
            int from = runtime.CursorNode(player, tile);
            if (from < 0) return;
            var pos = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            // Small stack buffer: ordinary region interiors allocate nothing.
            long* edges = stackalloc long[8]; int count = 0;
            for (int d = 0; d < 8; d++)
            {
                int x = pos.X + WeightedMoatRoutePlanner.DirectionX[d], y = pos.Y + WeightedMoatRoutePlanner.DirectionY[d];
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                int next = GameTileManagerAPI.Instance.GetTileId(x, y), to = runtime.CursorNode(player, next);
                if (to < 0 || from == to) continue;
                // Different ordinary PCLs connect through native portal records, not
                // by assuming that a physically adjacent pair bypasses its gate filter.
                if (from <= MaximumRegionId && to <= MaximumRegionId) continue;
                if (!runtime.weightedMoatRoutePlanner.TryGetTraversalEdge(player, pos.X, pos.Y, tile,
                    x, y, next, d, false, false, MoatTraversalPolicy.FriendlyOnly, out _, out _)) continue;
                edges[count++] = ((long)from << 32) | (uint)to;
                topology.Graph.ChangeEdge(from, to, 1);
            }
            if (count == 0) return;
            var saved = new long[count];
            for (int i = 0; i < count; i++) saved[i] = edges[i];
            topology.Boundaries.Add(tile, saved);
        }

        // Full build of a topology without boundary records: the same edges in the same tile order as
        // UpdateCursorBoundary over every tile, with the node
        // of each tile computed once and neighbours read from a table instead of per-pair Extender
        // calls. The per-tile path took 121-139 ms per build in 4 of 79 stress runs (pf-iter-55).
        // Inputs (regions, flags, buildings, moats, blocked buildings) do not change during the build.
        private int[] cursorNeighbors, cursorBuildNodes, cursorCoordinateTiles;
        private ushort[] cursorTileX, cursorTileY;
        private int cursorNeighborEpoch = int.MinValue;
        // Neighbour tile per tile and direction (-1: off map or not a valid tile) and tile positions,
        // with the results of the GetTileVectorFromId/GetTileId calls UpdateCursorBoundary makes. Built
        // per map on first use: the Extender's tile layout is only valid for a loaded map (a table built
        // at ResetMapState held 0 of ~2,500 cursor boundaries per player, pf-topo-3). GetTileId is read
        // once per coordinate (640,000 calls) instead of once per tile and direction.
        private void EnsureCursorNeighborTable()
        {
            if (cursorNeighbors != null && cursorNeighborEpoch == runtime.mapEpoch) return;
            int[] neighbors = cursorNeighbors ?? new int[NativeTileCount * 8], coordinates = cursorCoordinateTiles ?? new int[MapWidth * MapWidth];
            ushort[] xs = cursorTileX ?? new ushort[NativeTileCount], ys = cursorTileY ?? new ushort[NativeTileCount];
            for (int y = 0; y < MapWidth; y++)
                for (int x = 0; x < MapWidth; x++)
                {
                    int next = GameTileManagerAPI.Instance.GetTileId(x, y);
                    coordinates[y * MapWidth + x] = IsValidTileId(next) ? next : -1;
                }
            for (int tile = 0; tile < NativeTileCount; tile++)
            {
                int row = tile * 8;
                if (!IsValidTileId(tile)) { for (int d = 0; d < 8; d++) neighbors[row + d] = -1; xs[tile] = ys[tile] = 0; continue; }
                var pos = GameTileManagerAPI.Instance.GetTileVectorFromId(tile); xs[tile] = pos.X; ys[tile] = pos.Y;
                for (int d = 0; d < 8; d++)
                {
                    int x = pos.X + WeightedMoatRoutePlanner.DirectionX[d], y = pos.Y + WeightedMoatRoutePlanner.DirectionY[d];
                    neighbors[row + d] = (uint)x < MapWidth && (uint)y < MapWidth ? coordinates[y * MapWidth + x] : -1;
                }
            }
            cursorTileX = xs; cursorTileY = ys; cursorNeighbors = neighbors; cursorCoordinateTiles = coordinates;
            cursorNeighborEpoch = runtime.mapEpoch;
        }

        private void BuildCursorTopology(CursorTopology topology, int player)
        {
            EnsureCursorNeighborTable();
            int[] nodes = cursorBuildNodes ?? (cursorBuildNodes = new int[NativeTileCount]);
            for (int tile = 0; tile < NativeTileCount; tile++) nodes[tile] = runtime.CursorNode(player, tile);
            int[] neighbors = cursorNeighbors;
            long* edges = stackalloc long[8];
            for (int tile = 0; tile < NativeTileCount; tile++)
            {
                int row = tile * 8, count = 0, from = nodes[tile];
                if (from < 0) continue;
                int x = cursorTileX[tile], y = cursorTileY[tile];
                for (int d = 0; d < 8; d++)
                {
                    int next = neighbors[row + d]; if (next < 0) continue;
                    int to = nodes[next]; if (to < 0 || from == to) continue;
                    if (from <= MaximumRegionId && to <= MaximumRegionId) continue;
                    if (!runtime.weightedMoatRoutePlanner.TryGetTraversalEdge(player, x, y, tile,
                        x + WeightedMoatRoutePlanner.DirectionX[d], y + WeightedMoatRoutePlanner.DirectionY[d], next, d, false, false, MoatTraversalPolicy.FriendlyOnly, out _, out _)) continue;
                    edges[count++] = ((long)from << 32) | (uint)to;
                    topology.Graph.ChangeEdge(from, to, 1);
                }
                if (count == 0) continue;
                var cursorSaved = new long[count]; for (int i = 0; i < count; i++) cursorSaved[i] = edges[i];
                topology.Boundaries.Add(tile, cursorSaved);
            }
        }

        internal bool IsSamePositiveGroundRegion(int start, int target)
        {
            if (!IsValidTileId(start) || !IsValidTileId(target) ||
                runtime.IsCompletedMoatTile(start) || runtime.IsCompletedMoatTile(target) ||
                ((runtime.tileFlags[start] | runtime.tileFlags[target]) & CursorSpecialStructureTileFlagMask) != 0)
            {
                return false;
            }

            int startRegion = runtime.pathRegionGrid[start];
            return startRegion > 0 && startRegion == runtime.pathRegionGrid[target];
        }


        internal CursorTopology EnsureCursorTopology(int player, bool buildConnections = true)
        {
            ulong diplomacy = 0;
            for (int a = 1; a <= 8; a++)
                for (int b = 1; b <= 8; b++)
                    if (a == b || GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(a, b))
                        diplomacy |= 1UL << ((a - 1) * 8 + b - 1);
            string rebuildReason = null;
            if (runtime.cursorDiplomacy != diplomacy) { runtime.cursorTopologies.Clear(); runtime.cursorDiplomacy = diplomacy; rebuildReason = "diplomacy"; }
            int generation = *(int*)((byte*)runtime.nativePathManager + 0x74);
            if (!runtime.cursorTopologies.TryGetValue(player, out CursorTopology topology) ||
                topology.Epoch != runtime.mapEpoch || topology.RegionGeneration != generation)
            {
                rebuildReason = rebuildReason ?? (topology == null ? "new" : topology.Epoch != runtime.mapEpoch ? "epoch" :
                    "regionGeneration " + topology.RegionGeneration + "->" + generation);
                // The replaced topology is dropped from the map right here; nothing else holds it.
                topology = new CursorTopology(topology) { Epoch = runtime.mapEpoch, RegionGeneration = generation };
                runtime.cursorTopologies[player] = topology;
            }
            RefreshCursorPortals(topology, player);
            if (!buildConnections) return topology;
            if (topology.Ready && topology.Dirty.Count == 0) return topology;
            long started = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
            runtime.weightedMoatRoutePlanner.BeginReachabilityProbe();
            try
            {
                if (!topology.Ready)
                {
                    runtime.cursorTopologyBuilds++;
                    long measured = 0;
                    Shared.DevDiag.Start(ref measured);
                    if (topology.Boundaries.Count == 0) BuildCursorTopology(topology, player);
                    else for (int tile = 0; tile < NativeTileCount; tile++) UpdateCursorBoundary(topology, player, tile);
                    topology.Ready = true;
                    Shared.DevDiag.StopSpike(ref measured, "moat.cursorTopology.build", 0, () =>
                        $"player={player} reason={rebuildReason ?? "notReady"} boundaries={topology.Boundaries.Count}");
                }
                else
                    foreach (int tile in topology.Dirty) { UpdateCursorBoundary(topology, player, tile); runtime.cursorTopologyUpdates++; }
                topology.Dirty.Clear();
            }
            finally { runtime.weightedMoatRoutePlanner.EndReachabilityProbe(); cursorTopologyTicks += (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - started; }
            return topology;
        }

        internal override bool ProbeCursorConnectivityTraversal(int player, int start, int target, out RouteProbeSummary summary)
        {
            summary = new RouteProbeSummary(player);
            long started = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L); cursorQueries++;
            try
            {
                if (IsSamePositiveGroundRegion(start, target))
                {
                    fastVanillaBypasses++;
                    summary.StartRegion = runtime.pathRegionGrid[start];
                    summary.TargetRegion = runtime.pathRegionGrid[target];
                    summary.AttackProbeEvaluated = true;
                    summary.ReachedWithoutMoat = true;
                    summary.RouteFound = true;
                    return true;
                }
                return TryProbeFastCursorRoute(player, start, target, out summary);
            }
            finally { cursorQueryTicks += (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - started; }
        }

        internal void RefreshCursorPortals(CursorTopology topology, int player)
        {
            // Read-only first-phase E2610 portal contract (FBCB9319). Its second
            // phase permits blocked transitions and is NOT walkable connectivity.
            int* context = (int*)runtime.nativePathManager;
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
                    bool open = runtime.nativePortalGateStates != null && building > 0 && building <= 10000 &&
                        *(short*)(runtime.nativePortalGateStates + building * 0x32C) != 0;
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
                if (old != null) foreach (long edge in old) topology.Graph.ChangeEdge((int)(edge >> 32), (int)edge, -1);
                long[] next = n == 0 ? null : new long[n];
                for (int i = 0; i < n; i++)
                {
                    next[i] = candidate[i];
                    topology.Graph.ChangeEdge((int)(candidate[i] >> 32), (int)candidate[i], 1);
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

        internal static void AddPortalCandidate(long* edges, ref int count, int from, int to)
        {
            if (from > 0 && from <= MaximumRegionId && to > 0 && to <= MaximumRegionId && from != to)
                edges[count++] = ((long)from << 32) | (uint)to;
        }

        internal void DirtyCursorBuilding(int id)
        {
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* building) || building == null) return;
            if (!Shared.GameBuildingFootprint.TryGetBounds(building, out Shared.GameBuildingFootprintBounds bounds)) return;
            int minX = Math.Max(0, bounds.MinX - 2);
            int maxX = Math.Min(MapWidth - 1, bounds.MaxX + 2);
            int minY = Math.Max(0, bounds.MinY - 2);
            int maxY = Math.Min(MapWidth - 1, bounds.MaxY + 2);
            for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
            {
                int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (IsValidTileId(tile)) foreach (var entry in runtime.cursorTopologies) entry.Value.Dirty.Add(tile);
            }
        }
    }
}
