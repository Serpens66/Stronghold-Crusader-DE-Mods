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

        internal void UpdateCursorBoundary(CursorTopology topology, int player, int tile)
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
                long packed = ((long)from << 32) | (uint)to;
                if (!runtime.IsCompletedMoatTile(tile) && !runtime.IsCompletedMoatTile(next))
                { packed |= long.MinValue; topology.Ground.ChangeEdge(from, to, 1); }
                edges[count++] = packed;
                topology.Graph.ChangeEdge(from, to, 1);
            }
            if (count == 0) return;
            var saved = new long[count];
            for (int i = 0; i < count; i++) saved[i] = edges[i];
            topology.Boundaries.Add(tile, saved);
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

        internal GroundConnectionDecision ProbeGroundConnection(int player, int start, int target)
        {
            if (!IsValidTileId(start) || !IsValidTileId(target)) return GroundConnectionDecision.Unknown;
            if (runtime.IsCompletedMoatTile(start) || runtime.IsCompletedMoatTile(target)) return GroundConnectionDecision.Excluded;
            if (start == target) return GroundConnectionDecision.Reachable;
            if (runtime.nativePathManager == IntPtr.Zero || !runtime.cursorTopologies.TryGetValue(player, out CursorTopology topology) ||
                !topology.Ready || topology.Epoch != runtime.mapEpoch || topology.Dirty.Count != 0 ||
                topology.RegionGeneration != *(int*)((byte*)runtime.nativePathManager + 0x74)) return GroundConnectionDecision.Unknown;
            return topology.GroundUpper.CanReach(GroundUpperNode(start), GroundUpperNode(target))
                ? GroundConnectionDecision.Unknown : GroundConnectionDecision.Excluded;
        }

        internal int GroundUpperNode(int tile)
        {
            if (!IsValidTileId(tile) || runtime.IsCompletedMoatTile(tile)) return -1;
            int region = runtime.pathRegionGrid[tile];
            return region > 0 && region <= MaximumRegionId &&
                (runtime.tileFlags[tile] & CursorSpecialStructureTileFlagMask) == 0
                ? region : MaximumRegionId + 1 + tile;
        }

        internal void UpdateGroundBoundary(CursorTopology topology, int player, int tile)
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
                if (!runtime.weightedMoatRoutePlanner.TryGetTraversalEdge(player,pos.X,pos.Y,tile,x,y,next,d,false,false,
                    MoatTraversalPolicy.GroundOnly,out _,out _)) continue;
                pairs[count++]=((long)from<<32)|(uint)to;
                topology.GroundUpper.ChangeEdge(from,to,1);
            }
            if (count==0) return;
            var saved=new long[count]; for(int i=0;i<count;i++)saved[i]=pairs[i];
            topology.GroundBoundaries.Add(tile,saved);
        }

        internal CursorTopology EnsureCursorTopology(int player, bool buildConnections = true)
        {
            ulong diplomacy = 0;
            for (int a = 1; a <= 8; a++)
                for (int b = 1; b <= 8; b++)
                    if (a == b || GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(a, b))
                        diplomacy |= 1UL << ((a - 1) * 8 + b - 1);
            if (runtime.cursorDiplomacy != diplomacy) { runtime.cursorTopologies.Clear(); runtime.cursorDiplomacy = diplomacy; }
            int generation = *(int*)((byte*)runtime.nativePathManager + 0x74);
            if (!runtime.cursorTopologies.TryGetValue(player, out CursorTopology topology) ||
                topology.Epoch != runtime.mapEpoch || topology.RegionGeneration != generation)
            {
                topology = new CursorTopology { Epoch = runtime.mapEpoch, RegionGeneration = generation };
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
                    for (int tile = 0; tile < NativeTileCount; tile++) UpdateCursorBoundary(topology, player, tile);
                    topology.Ready = true;
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
                if (runtime.RequiredOnlyMode)
                {
                    bool fastNormal = IsSamePositiveGroundRegion(start, target);
                    if (fastNormal)
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
                var topology = EnsureCursorTopology(player, false);
                int from = runtime.CursorNode(player, start), to = runtime.CursorNode(player, target);
                // An unchanged native ground region already proves connectivity. In
                // this common hover case even the first moat topology build is unnecessary.
                bool normal = from > 0 && from <= MaximumRegionId && from == to;
                if (!normal && from >= 0 && to >= 0 && (!topology.Ready || topology.Dirty.Count != 0))
                    topology = EnsureCursorTopology(player);
                normal |= topology.Ready && topology.Ground.CanReach(from, to) && !runtime.IsCompletedMoatTile(start) && !runtime.IsCompletedMoatTile(target);
                bool reachable = normal || topology.Ready && topology.Graph.CanReach(from, to);
                summary.StartRegion = runtime.pathRegionGrid[start]; summary.TargetRegion = runtime.pathRegionGrid[target];
                summary.AttackProbeEvaluated = true; summary.ReachedWithoutMoat = normal;
                summary.ReachedWithMoat = reachable && !normal;
                summary.FriendlyMoatTiles = summary.ReachedWithMoat ? 1 : 0;
                summary.RouteFound = reachable;
                return true;
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
        internal enum GroundConnectionDecision { Unknown, Reachable, Excluded }
    }
}
