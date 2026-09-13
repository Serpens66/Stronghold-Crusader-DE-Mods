using System;
using System.Collections.Generic;
using SHCDESE.API;
using SHCDESE.Interop;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        private sealed class FastRoutingState
        {
            internal FastRoutePool Pool;
            internal readonly SortedDictionary<int, FastTraversalCache> Maps = new SortedDictionary<int, FastTraversalCache>();
            internal readonly Dictionary<FastFieldKey, FastNativeMask> NativeMasks = new Dictionary<FastFieldKey, FastNativeMask>();
            internal ulong Access;
            internal bool AccessKnown;
        }
        private FastRoutingState fastSimulationRouting, fastCursorRouting;
        private long fastNewQueries, fastNewExpanded, fastNewTooLong, fastNewInvalidations;

        private FastRoutingState GetFastRouting(bool cursor)
        {
            FastRoutingState state = cursor ? fastCursorRouting : fastSimulationRouting;
            if (state != null) return state;
            state = new FastRoutingState();
            FastRoutingState captured = state;
            long oneField = settings.NativeFast ? new FastNativeKernel.Layout(MapWidth, MapWidth).Bytes : (long)MapWidth * MapWidth * 9;
            // One forward candidate field is accounted for in the simulation budget.
            state.Pool = new FastRoutePool(MapWidth, MapWidth,
                cursor ? oneField * 2 : 64L * 1024 * 1024 - oneField,
                key => MakeFastEdge(captured, key),
                settings.NativeFast ? (Func<Func<FastFieldKey>, MoatSearchEdge, IFastRouteField>)((key, edge) =>
                    new FastNativeRouteField(MapWidth, MapWidth, edge, () => GetFastNativeMask(captured, key()))) : null, oneField);
            if (cursor) fastCursorRouting = state; else fastSimulationRouting = state;
            return state;
        }

        private FastNativeMask GetFastNativeMask(FastRoutingState state, FastFieldKey key)
        {
            var maskKey = new FastFieldKey(key.Player, 0, key.Ground);
            if (!state.NativeMasks.TryGetValue(maskKey, out FastNativeMask mask))
            {
                mask = new FastNativeMask(MapWidth, MapWidth, MakeFastEdge(state, key));
                state.NativeMasks.Add(maskKey, mask);
            }
            return mask;
        }

        private MoatSearchEdge MakeFastEdge(FastRoutingState state, FastFieldKey key)
        {
            if (!state.Maps.TryGetValue(key.Player, out FastTraversalCache map))
            {
                int player = key.Player;
                EnsureCursorTopology(player, false);
                map = new FastTraversalCache(MapWidth, MapWidth,
                    (int a, int b, int direction, bool ground, out bool wet, out bool structure) =>
                        FastLiveEdge(player, a, b, direction, ground, out wet, out structure));
                state.Maps.Add(player, map);
            }
            return (int a, int b, int direction, out bool wet, out bool structure) =>
                map.Edge(a, b, direction, key.Ground, out wet, out structure);
        }

        private bool FastLiveEdge(int player, int from, int to, int direction, bool ground,
            out bool wet, out bool structure)
        {
            wet = structure = false;
            if ((uint)from >= MapWidth * MapWidth || (uint)to >= MapWidth * MapWidth) return false;
            int x = from % MapWidth, y = from / MapWidth, nx = to % MapWidth, ny = to / MapWidth;
            int a = GameTileManagerAPI.Instance.GetTileId(x, y), b = GameTileManagerAPI.Instance.GetTileId(nx, ny);
            if (!IsValidTileId(a) || !IsValidTileId(b)) return false;
            if (cursorTopologies.TryGetValue(player, out CursorTopology topology) &&
                (topology.BlockedBuildings.Contains(nativeBuildingLayer[a]) ||
                 topology.BlockedBuildings.Contains(nativeBuildingLayer[b]))) return false;
            bool allowed = weightedMoatRoutePlanner.TryGetTraversalEdge(player, x, y, a, nx, ny, b,
                direction, false, false, ground ? MoatTraversalPolicy.GroundOnly : MoatTraversalPolicy.FriendlyOnly,
                out MoatTraversalEdgeKind kind, out structure);
            wet = kind != MoatTraversalEdgeKind.Ground;
            return allowed;
        }

        private ulong ReadFastAccessFingerprint()
        {
            ulong hash = 14695981039346656037UL;
            for (int a = 1; a <= 8; a++) for (int b = 1; b <= 8; b++)
                hash = unchecked((hash ^ (GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(a, b) ? 1UL : 0)) * 1099511628211UL);
            if (nativePathManager == IntPtr.Zero) return hash;
            int* context = (int*)nativePathManager;
            int count = Math.Min(Math.Max(context[0], 1), 200);
            hash = unchecked((hash ^ (uint)count) * 1099511628211UL);
            for (int id = 1; id < count; id++)
            {
                int offset = id * 0x81;
                // Only stable access fields, never E2610's scratch/cache words.
                hash = unchecked((hash ^ (uint)context[offset + 0x809]) * 1099511628211UL);
                hash = unchecked((hash ^ (uint)context[offset + 0x80A]) * 1099511628211UL);
                hash = unchecked((hash ^ (uint)context[offset + 0x80F]) * 1099511628211UL);
                hash = unchecked((hash ^ (uint)context[offset + 0x882]) * 1099511628211UL);
                int building = context[offset + 0x80C];
                hash = unchecked((hash ^ (uint)building) * 1099511628211UL);
                if (nativePortalGateStates != null && building > 0 && building <= 10000)
                    hash = unchecked((hash ^ (ushort)*(short*)(nativePortalGateStates + building * 0x32C)) * 1099511628211UL);
            }
            return hash;
        }

        private void RefreshFastRouting(FastRoutingState state)
        {
            ulong access = ReadFastAccessFingerprint();
            bool changed = state.AccessKnown && access != state.Access;
            state.Access = access; state.AccessKnown = true;
            foreach (var pair in state.Maps)
            {
                EnsureCursorTopology(pair.Key, false);
                if (changed) pair.Value.MarkAll();
                IReadOnlyCollection<int> nodes = pair.Value.Refresh();
                if (nodes.Count != 0)
                {
                    state.NativeMasks.Remove(new FastFieldKey(pair.Key, 0, true));
                    state.NativeMasks.Remove(new FastFieldKey(pair.Key, 0, false));
                    state.Pool.Invalidate(pair.Key, nodes, settings.NativeFast); fastNewInvalidations++;
                }
            }
        }

        private void MarkFastTopologyTile(int tile)
        {
            if (!RequiredOnlyMode || !IsValidTileId(tile)) return;
            var position = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            for (int y = Math.Max(0, position.Y - 2); y <= Math.Min(MapWidth - 1, position.Y + 2); y++)
                for (int x = Math.Max(0, position.X - 2); x <= Math.Min(MapWidth - 1, position.X + 2); x++)
                {
                    int node = y * MapWidth + x;
                    if (fastSimulationRouting != null) foreach (var map in fastSimulationRouting.Maps.Values) map.Mark(node);
                    if (fastCursorRouting != null) foreach (var map in fastCursorRouting.Maps.Values) map.Mark(node);
                }
        }

        private void MarkFastTopologyAll()
        {
            if (fastSimulationRouting != null) foreach (var map in fastSimulationRouting.Maps.Values) map.MarkAll();
            if (fastCursorRouting != null) foreach (var map in fastCursorRouting.Maps.Values) map.MarkAll();
        }

        private FastRouteStatus AdvanceFastField(FastFieldLease lease, int start, int budget, int maximumEdges = 2000)
        {
            if (lease == null) return FastRouteStatus.Pending;
            int before = lease.Field.Expanded;
            FastRouteStatus status = lease.Field.Advance(start, budget, maximumEdges);
            fastNewExpanded += lease.Field.Expanded - before;
            return status;
        }

        private bool TryFastEncoded(int player, int sx, int sy, int tx, int ty, bool reserved,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route, int unitId = 0, bool groundOnly = false)
        {
            route = default; summary = default;
            FastRoutingState state = GetFastRouting(false);
            RefreshFastRouting(state);
            int requestedTarget = ty * MapWidth + tx;
            TryFastGroupSuffix(player, requestedTarget, out int anchor, out byte[] suffix, unitId);
            using (FastFieldLease lease = state.Pool.Acquire(new FastFieldKey(player, anchor, groundOnly, reserved)))
            {
                if (lease == null) { summary = WeightedMoatRouteSummary.Failed("fast-pool-busy", 0); return false; }
                int start = sy * MapWidth + sx;
                FastRouteStatus status = AdvanceFastField(lease, start, int.MaxValue);
                if (status != FastRouteStatus.Found)
                {
                    if (status == FastRouteStatus.TooLong) fastNewTooLong++;
                    summary = WeightedMoatRouteSummary.Failed("fast-" + status, 0, lease.Field.Expanded);
                    return false;
                }
                int prefixCount = lease.Field.Distance(start);
                int count = prefixCount + (suffix?.Length ?? 0);
                if (count > 2000) { fastNewTooLong++; summary = WeightedMoatRouteSummary.Failed("fast-TooLong", 0); return false; }
                var bytes = new byte[(count + 1) / 2];
                if (lease.Field.WritePacked(start, bytes, out int written) != FastRouteStatus.Found) return false;
                if (suffix != null) foreach (byte direction in suffix)
                { bytes[written >> 1] |= (byte)(direction << ((written & 1) * 4)); written++; }
                int node = start, moat = 0, structures = 0, diagonal = 0, changes = 0, previous = -1;
                ulong fingerprint = 14695981039346656037UL;
                for (int i = 0; i < written; i++)
                {
                    int direction = (bytes[i >> 1] >> ((i & 1) * 4)) & 15;
                    int next = node + WeightedMoatRoutePlanner.DirectionY[direction] * MapWidth + WeightedMoatRoutePlanner.DirectionX[direction];
                    if (!FastLiveEdge(player, node, next, direction, groundOnly, out bool wet, out bool structure))
                    { MarkFastTopologyAll(); summary = WeightedMoatRouteSummary.Failed("fast-live-edge-changed", 0); return false; }
                    if (wet) moat++; if (structure) structures++; if ((direction & 1) != 0) diagonal++;
                    if (previous >= 0 && previous != direction) changes++;
                    previous = direction; fingerprint = unchecked((fingerprint ^ (byte)direction) * 1099511628211UL);
                    node = next;
                }
                if (node != requestedTarget) { summary = WeightedMoatRouteSummary.Failed("fast-endpoint-mismatch", 0); return false; }
                summary = WeightedMoatRouteSummary.Succeeded(written, written - moat, moat, structures,
                    diagonal, changes, fingerprint, 0, 0, lease.Field.Expanded);
                route = new WeightedMoatEncodedRoute(bytes, written);
                return true;
            }
        }

        private bool TryBuildMovementReachabilityEncoded(int player, int sx, int sy, int tx, int ty, bool reserved,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route)
        {
            return RequiredOnlyMode
                ? TryFastEncoded(player, sx, sy, tx, ty, reserved, out summary, out route)
                : weightedMoatRoutePlanner.TryBuildReachabilityEncoded(player, sx, sy, tx, ty, reserved, out summary, out route);
        }

        private bool FastGroundReachable(FastRoutingState state, int player, int start, int target, int unitId)
        {
            if (TryFastGroupSuffix(player, target, out int anchor, out _, unitId)) target = anchor;
            int a = GameTileManagerAPI.Instance.GetTileId(start % MapWidth, start / MapWidth);
            int b = GameTileManagerAPI.Instance.GetTileId(target % MapWidth, target / MapWidth);
            if (!IsValidTileId(a) || !IsValidTileId(b) || IsCompletedMoatTile(a) || IsCompletedMoatTile(b)) return false;
            using (FastFieldLease lease = state.Pool.Acquire(new FastFieldKey(player, target, true)))
            {
                if (lease == null) throw new InvalidOperationException("Fast synchronous ground reserve unavailable.");
                // A route longer than the packed buffer still proves a ground alternative.
                return AdvanceFastField(lease, start, int.MaxValue, int.MaxValue) == FastRouteStatus.Found;
            }
        }

        private bool TryFindFastRequiredRoute(PlanScope plan, bool reserved, bool evaluateMissing, out RouteProbeSummary summary)
        {
            long started = StartFastMeasurement();
            try { return TryFindFastRequiredRouteCore(plan, reserved, evaluateMissing, out summary); }
            finally { FinishFastMeasurement(started); }
        }

        private bool TryFindFastRequiredRouteCore(PlanScope plan, bool reserved, bool evaluateMissing, out RouteProbeSummary summary)
        {
            summary = default;
            if (plan == null || !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) ||
                unit == null || !CanDigMoat(unit) || (uint)plan.TargetX >= MapWidth || (uint)plan.TargetY >= MapWidth ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != unit->r_ControllableForPlayerId))) return false;
            plan.PlayerId = unit->r_ControllableForPlayerId; plan.UnitGlobalId = unit->r_GlobalId; plan.IdentityBound = true;
            QualifiedMovementRoute cached = GetReusableQualifiedRoute(plan, unit);
            if (cached != null)
            {
                summary = FastSummary(plan.PlayerId, cached.StartX, cached.StartY, plan.TargetX, plan.TargetY, false, cached.Summary);
                return cached.Summary.MoatEdges > 0;
            }
            if (!evaluateMissing) return false;
            GetNativeMovementStart(unit, out int sx, out int sy);
            if (plan.RouteStartX >= 0) { sx = plan.RouteStartX; sy = plan.RouteStartY; }
            if ((uint)sx >= MapWidth || (uint)sy >= MapWidth) return false;
            int start = sy * MapWidth + sx, target = plan.TargetY * MapWidth + plan.TargetX;
            FastRoutingState state = GetFastRouting(false); RefreshFastRouting(state); fastNewQueries++;
            bool ground = FastGroundReachable(state, plan.PlayerId, start, target, plan.UnitId);
            plan.QualifiedRoute = null;
            plan.QualifiedTerminalRoute = default;
            plan.QualifiedTerminalSummary = default;
            WeightedMoatRouteSummary found = default;
            if (!ground && TryFastEncoded(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, reserved, out found, out WeightedMoatEncodedRoute route, plan.UnitId))
                plan.QualifiedRoute = new QualifiedMovementRoute(sx, sy, plan.TargetX, plan.TargetY, plan.PlayerId,
                    mapEpoch, CaptureCurrentGameTick(), placementRevision, route, found, default, false);
            if (!ground && plan.QualifiedRoute == null && plan.MoatWorkMovement &&
                TryBuildTerminalFillRoute(plan, unit, sx, sy, out found, out WeightedMoatEncodedRoute terminal))
            { plan.QualifiedTerminalRoute = terminal; plan.QualifiedTerminalSummary = found; }
            summary = FastSummary(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, ground, found);
            return !ground && (plan.QualifiedRoute != null || plan.QualifiedTerminalRoute.IsValid) && found.MoatEdges > 0;
        }

        private RouteProbeSummary FastSummary(int player, int sx, int sy, int tx, int ty, bool ground, WeightedMoatRouteSummary found)
        {
            int a = GameTileManagerAPI.Instance.GetTileId(sx, sy), b = GameTileManagerAPI.Instance.GetTileId(tx, ty);
            return new RouteProbeSummary(player) { StartRegion = IsValidTileId(a) ? pathRegionGrid[a] : 0,
                TargetRegion = IsValidTileId(b) ? pathRegionGrid[b] : 0, RouteFound = !ground && found.Found && found.MoatEdges > 0,
                AttackProbeEvaluated = true, ReachedWithoutMoat = ground, ReachedWithMoat = !ground && found.Found,
                FriendlyMoatTiles = found.MoatEdges, StructuralEdgesObserved = found.StructuralEdges,
                RouteDistance = found.RouteLength };
        }
    }
}
