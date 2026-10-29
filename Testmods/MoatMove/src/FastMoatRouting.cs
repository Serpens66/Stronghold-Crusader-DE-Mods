extern alias BugfixesRuntime;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;
using System;
using System.Collections.Generic;
using SHCDESE.API;
using SHCDESE.Interop;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
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
                        FastLiveEdge(player, a, b, direction, ground, out wet, out structure), GroundEqualsFriendly);
                state.Maps.Add(player, map);
            }
            return (int a, int b, int direction, out bool wet, out bool structure) =>
                map.Edge(a, b, direction, key.Ground, out wet, out structure);
        }

        private bool FastLiveEdge(int player, int from, int to, int direction, bool ground,
            out bool wet, out bool structure) =>
            LiveEdge(player, from, to, direction, ground,
                runtime.cursorTopologies.TryGetValue(player, out CursorTopology topology) ? topology.BlockedBuildings : null, out wet, out structure);

        // WeightedMoatRoutePlanner.TryGetTraversalEdge treats GroundOnly and FriendlyOnly alike unless
        // an endpoint is a completed moat (moat entry/owner/ground-only rules) or, for a diagonal, a
        // corner tile is one (FriendlyOnly corner-owner rule). Same planner moat predicate and tile ids.
        private bool GroundEqualsFriendly(int from, int to, int direction)
        {
            if ((uint)from >= MapWidth * MapWidth || (uint)to >= MapWidth * MapWidth) return false;
            int x = from % MapWidth, y = from / MapWidth, nx = to % MapWidth, ny = to / MapWidth;
            WeightedMoatRoutePlanner planner = runtime.weightedMoatRoutePlanner;
            // Endpoints as FastLiveEdge passes them; corners as the planner derives them.
            int a = GameTileManagerAPI.Instance.GetTileId(x, y), b = GameTileManagerAPI.Instance.GetTileId(nx, ny);
            if (!IsValidTileId(a) || !IsValidTileId(b) || planner.IsCompletedMoat(a) || planner.IsCompletedMoat(b)) return false;
            if ((direction & 1) == 0) return true;
            int first = planner.GetTileId(nx, y), second = planner.GetTileId(x, ny);
            return IsValidTileId(first) && IsValidTileId(second) && !planner.IsCompletedMoat(first) && !planner.IsCompletedMoat(second);
        }

        // Shared live predicate; blocked: gate buildings closed to the player (null: none known).
        private bool LiveEdge(int player, int from, int to, int direction, bool ground, HashSet<int> blocked,
            out bool wet, out bool structure)
        {
            wet = structure = false;
            if ((uint)from >= MapWidth * MapWidth || (uint)to >= MapWidth * MapWidth) return false;
            int x = from % MapWidth, y = from / MapWidth, nx = to % MapWidth, ny = to / MapWidth;
            int a = GameTileManagerAPI.Instance.GetTileId(x, y), b = GameTileManagerAPI.Instance.GetTileId(nx, ny);
            if (!IsValidTileId(a) || !IsValidTileId(b)) return false;
            if (blocked != null && blocked.Count != 0 &&
                (blocked.Contains(runtime.nativeBuildingLayer[a]) || blocked.Contains(runtime.nativeBuildingLayer[b]))) return false;
            WeightedMoatRoutePlanner planner = runtime.weightedMoatRoutePlanner;
            bool allowed = planner.TryGetTraversalEdge(player, x, y, a, nx, ny, b,
                direction, false, false, ground ? MoatTraversalPolicy.GroundOnly : MoatTraversalPolicy.FriendlyOnly,
                out MoatTraversalEdgeKind kind, out structure);
            wet = kind != MoatTraversalEdgeKind.Ground;
            // Ground edges follow Vanilla's ground search DA590: the native direction byte alone.
            // The height correction is DAFD0's (moat mode); GroundOnly reaches it only for an edge
            // that passed every other rule. Without this, keeps, towers and walls had no ground
            // edges and their units fell back to Vanilla (pf-iter-7).
            if (!allowed && ground && planner.edgeRejectionRule == "height") { allowed = true; wet = false; }
            // The publication audit rejects a diagonal past an enemy moat corner; GroundOnly does not
            // check corners (FriendlyOnly does), so ground edges apply the audit's rule themselves.
            // APIShared route tile rules: a Forbidden tile has no incoming edges (leaving stays possible).
            if (allowed && APIShared.Pathfinding.RouteTileRules.HasRules &&
                APIShared.Pathfinding.RouteTileRules.GetEffect(nx, ny, player, out _) == APIShared.Pathfinding.RouteTileEffect.Forbidden)
                return false;
            if (allowed && ground && (direction & 1) != 0 &&
                (runtime.IsCompletedEnemyMoatForPlayer(player, GameTileManagerAPI.Instance.GetTileId(nx, y)) ||
                 runtime.IsCompletedEnemyMoatForPlayer(player, GameTileManagerAPI.Instance.GetTileId(x, ny))))
                allowed = false;
            return allowed;
        }

        private ulong ReadFastAccessFingerprint()
        {
            ulong hash = 14695981039346656037UL;
            for (int a = 1; a <= 8; a++) for (int b = 1; b <= 8; b++)
                hash = unchecked((hash ^ (GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(a, b) ? 1UL : 0)) * 1099511628211UL);
            if (runtime.nativePathManager == IntPtr.Zero) return hash;
            int* context = (int*)runtime.nativePathManager;
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
                if (runtime.nativePortalGateStates != null && building > 0 && building <= 10000)
                    hash = unchecked((hash ^ (ushort)*(short*)(runtime.nativePortalGateStates + building * 0x32C)) * 1099511628211UL);
            }
            return hash;
        }

        private void RefreshFastRouting(FastRoutingState state)
        {
            ulong access = ReadFastAccessFingerprint();
            bool changed = state.AccessKnown && access != state.Access;
            state.Access = access; state.AccessKnown = true;
            AdvanceFastMarkEpoch();
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

        // Tile reports repeat (one per native 3x3 update, bursts while AI builds, digs or buildings
        // burn). Marks are idempotent until a cache refresh consumes them, so a tile already marked
        // in the current refresh epoch is skipped; every refresh starts a new epoch.
        private int fastMarkEpoch = 1;
        private int[] fastTileMarkEpochs;
        private void AdvanceFastMarkEpoch() { if (++fastMarkEpoch == int.MaxValue) { fastMarkEpoch = 1; if (fastTileMarkEpochs != null) Array.Clear(fastTileMarkEpochs, 0, fastTileMarkEpochs.Length); } }

        // radius 2: a reported tile update (D90D0 rewrites its 3x3 area, whose words also read
        // their neighbours). radius 1: one changed direction byte (MoatMove's byte watch); the
        // byte feeds its own node's edges and the diagonal/moat checks of direct neighbours.
        private void MarkFastTopologyTile(int tile, int radius = 2)
        {
            if (!RequiredOnlyMode || !IsValidTileId(tile)) return;
            var position = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            if ((uint)position.X < MapWidth && (uint)position.Y < MapWidth)
            {
                if (fastTileMarkEpochs == null || fastTileMarkEpochs.Length != MapWidth * MapWidth) fastTileMarkEpochs = new int[MapWidth * MapWidth];
                int center = position.Y * MapWidth + position.X;
                if (fastTileMarkEpochs[center] == fastMarkEpoch) return;
                fastTileMarkEpochs[center] = fastMarkEpoch;
            }
            for (int y = Math.Max(0, position.Y - radius); y <= Math.Min(MapWidth - 1, position.Y + radius); y++)
                for (int x = Math.Max(0, position.X - radius); x <= Math.Min(MapWidth - 1, position.X + radius); x++)
                {
                    int node = y * MapWidth + x;
                    if (fastSimulationRouting != null) foreach (var map in fastSimulationRouting.Maps.Values) map.Mark(node);
                    if (fastCursorRouting != null) foreach (var map in fastCursorRouting.Maps.Values) map.Mark(node);
                    foreach (var map in anchorMaps) map?.Mark(node);
                    foreach (var map in reachWords) map?.Mark(node);
                }
        }

        // Cached FriendlyOnly words for the managed reachability search (dig and moat work scopes):
        // exactly VisitNeighbour's live predicate (no gate-building exclusion, unlike anchor masks),
        // marked like the other caches and refreshed before each new search. AI diggers otherwise
        // evaluated every edge of whole components live on every selection (236 ms gaps, pf-iter-2).
        private readonly FastTraversalCache[] reachWords = new FastTraversalCache[9];
        // Anchor mode only: AnchorTick keeps access changes (gate footprints, alliances) marked; other
        // modes keep the live predicate.
        private FastTraversalCache ReachabilityWords(int player)
        {
            if (player < 1 || player > 8 || !anchorAvailable || !settings.Anchor) return null;
            return reachWords[player] ?? CreateReachabilityWords(player);
        }
        // Own method: a lambda capturing player inside ReachabilityWords allocates its closure on
        // every call, i.e. once per BFS neighbour (24 % GC time in the 329 ms gap of pf-iter-3).
        private FastTraversalCache CreateReachabilityWords(int player) =>
            reachWords[player] = new FastTraversalCache(MapWidth, MapWidth,
                (int a, int b, int direction, bool ground, out bool wet, out bool structure) =>
                    LiveEdge(player, a, b, direction, ground, null, out wet, out structure), GroundEqualsFriendly)
            { Seed = (int node, out uint word) => SeedReachabilityWord(player, node, out word) };

        // The anchor mask evaluates the same LiveEdge and additionally closes the player's blocked
        // gate buildings; away from those its clean word is the reachability word. A first search
        // over a whole component otherwise read every node live (216 ms gap, pf-iter-8).
        private bool SeedReachabilityWord(int player, int node, out uint word)
        {
            word = 0;
            FastTraversalCache anchor = anchorMaps[player];
            if (anchor == null || !anchor.TryGetClean(node, out word) || !AnchorSweptSinceRequest(node)) return false;
            HashSet<int> blocked = anchorBlocked[player];
            if (blocked == null || blocked.Count == 0) return true;
            int x = node % MapWidth, y = node / MapWidth;
            int[] tileOf = AnchorNodeTiles();
            GameTileManagerAPI tiles = tileOf == null ? GameTileManagerAPI.Instance : null;
            for (int ny = Math.Max(0, y - 1); ny <= Math.Min(MapWidth - 1, y + 1); ny++)
                for (int nx = Math.Max(0, x - 1); nx <= Math.Min(MapWidth - 1, x + 1); nx++)
                {
                    int tile = tileOf != null ? tileOf[ny * MapWidth + nx] : tiles.GetTileId(nx, ny);
                    if (IsValidTileId(tile) && blocked.Contains(runtime.nativeBuildingLayer[tile])) return false;
                }
            return true;
        }
        private void RefreshReachabilityWords(int player)
        {
            FastTraversalCache map = ReachabilityWords(player);
            if (map == null) return;
            InvalidateExhaustedReachability(player, map.Refresh()); AdvanceFastMarkEpoch();
        }

        // anchorMasks == false (a failed live audit): the caller marks the stale area of the anchor
        // masks and reachability words itself; only Fast's own caches are marked completely.
        private void MarkFastTopologyAll(bool anchorMasks = true)
        {
            if (fastSimulationRouting != null) foreach (var map in fastSimulationRouting.Maps.Values) map.MarkAll();
            if (fastCursorRouting != null) foreach (var map in fastCursorRouting.Maps.Values) map.MarkAll();
            if (anchorMasks) { anchorResweepGlobal++; ResweepAnchorMasks(); }
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
                return TryFinishFastEncoded(player, start, requestedTarget, bytes, written, groundOnly, lease.Field.Expanded, out summary, out route);
            }
        }

        // Live audit of a packed route (low nibble first): every transition, endpoint and summary.
        // anchor: the route comes from an anchor mask, audited with the anchor's gate rule; a
        // failed edge re-reads only the area of the stale node instead of every anchor mask.
        private bool TryFinishFastEncoded(int player, int start, int requestedTarget, byte[] bytes, int written, bool groundOnly,
            int expanded, out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route, bool anchor = false)
        {
            route = default;
            int node = start, moat = 0, structures = 0, diagonal = 0, changes = 0, previous = -1;
            ulong fingerprint = 14695981039346656037UL;
            for (int i = 0; i < written; i++)
            {
                int direction = (bytes[i >> 1] >> ((i & 1) * 4)) & 15;
                int next = node + WeightedMoatRoutePlanner.DirectionY[direction] * MapWidth + WeightedMoatRoutePlanner.DirectionX[direction];
                bool live = anchor ? AnchorLiveEdge(player, node, next, direction, groundOnly, out bool wet, out bool structure)
                    : FastLiveEdge(player, node, next, direction, groundOnly, out wet, out structure);
                if (!live)
                {
                    MarkFastTopologyAll(anchorMasks: false); MarkAnchorArea(node);
                    summary = WeightedMoatRouteSummary.Failed("fast-live-edge-changed", 0); return false;
                }
                if (wet) moat++; if (structure) structures++; if ((direction & 1) != 0) diagonal++;
                if (previous >= 0 && previous != direction) changes++;
                previous = direction; fingerprint = unchecked((fingerprint ^ (byte)direction) * 1099511628211UL);
                node = next;
            }
            if (node != requestedTarget) { summary = WeightedMoatRouteSummary.Failed("fast-endpoint-mismatch", 0); return false; }
            summary = WeightedMoatRouteSummary.Succeeded(written, written - moat, moat, structures,
                diagonal, changes, fingerprint, 0, 0, expanded);
            route = new WeightedMoatEncodedRoute(bytes, written);
            return true;
        }

        internal override bool TryBuildMovementReachabilityEncoded(int player, int sx, int sy, int tx, int ty, bool reserved,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route)
        {
            return RequiredOnlyMode
                ? TryFastEncoded(player, sx, sy, tx, ty, reserved, out summary, out route)
                : runtime.weightedMoatRoutePlanner.TryBuildReachabilityEncoded(player, sx, sy, tx, ty, reserved, out summary, out route);
        }

        internal override bool TryPrepareGroundReconstruction(PlanScope plan, GameUnit* unit)
        {
            GetNativeMovementStart(unit, out int sx, out int sy);
            FastRoutingState state = GetFastRouting(false); RefreshFastRouting(state);
            bool ground = FastGroundReachable(state, plan.PlayerId,
                sy * MapWidth + sx, plan.TargetY * MapWidth + plan.TargetX, plan.UnitId);
            if (ground)
            {
                plan.QualifiedRoute = null; plan.QualifiedTerminalRoute = default;
                if (TryFastEncoded(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, false,
                    out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route,
                    plan.UnitId, groundOnly: true))
                    plan.QualifiedRoute = new QualifiedMovementRoute(sx, sy, plan.TargetX, plan.TargetY,
                        plan.PlayerId, runtime.mapEpoch, CaptureCurrentGameTick(), runtime.placementRevision,
                        route, summary, default, false);
            }
            return ground;
        }

        private bool FastGroundReachable(FastRoutingState state, int player, int start, int target, int unitId)
        {
            if (TryFastGroupSuffix(player, target, out int anchor, out _, unitId)) target = anchor;
            int a = GameTileManagerAPI.Instance.GetTileId(start % MapWidth, start / MapWidth);
            int b = GameTileManagerAPI.Instance.GetTileId(target % MapWidth, target / MapWidth);
            if (!IsValidTileId(a) || !IsValidTileId(b) || runtime.IsCompletedMoatTile(a) || runtime.IsCompletedMoatTile(b)) return false;
            using (FastFieldLease lease = state.Pool.Acquire(new FastFieldKey(player, target, true)))
            {
                if (lease == null) throw new InvalidOperationException("Fast synchronous ground reserve unavailable.");
                // A route longer than the packed buffer still proves a ground alternative.
                return AdvanceFastField(lease, start, int.MaxValue, int.MaxValue) == FastRouteStatus.Found;
            }
        }

        internal override bool TryFindFastRequiredRoute(PlanScope plan, bool reserved, bool evaluateMissing, out RouteProbeSummary summary)
        {
            long started = StartFastMeasurement();
            try { return TryFindFastRequiredRouteCore(plan, reserved, evaluateMissing, out summary); }
            finally { FinishFastMeasurement(started); }
        }

        private bool TryFindFastRequiredRouteCore(PlanScope plan, bool reserved, bool evaluateMissing, out RouteProbeSummary summary)
        {
            summary = default;
            if (plan == null || !APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) ||
                unit == null || !runtime.CanDigMoat(unit) || (uint)plan.TargetX >= MapWidth || (uint)plan.TargetY >= MapWidth ||
                (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != unit->r_ControllableForPlayerId))) return false;
            plan.PlayerId = unit->r_ControllableForPlayerId; plan.UnitGlobalId = unit->r_GlobalId; plan.IdentityBound = true;
            QualifiedMovementRoute cached = runtime.GetReusableQualifiedRoute(plan, unit);
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
            fastNewQueries++;
            // Anchor mode: the cheaper weighted route decides; otherwise Fast's ground-first rule.
            if (TryAnchorRequiredRoute(plan, reserved, sx, sy, out summary, out bool anchorMoat, ResolveAnchorTask(false))) return anchorMoat;
            // Anchor mode without a core answer (outside group moves: combat resume, AI orders
            // before their mask is ready) keeps Vanilla's result instead of a synchronous Fast
            // field (up to 930 ms per query with AI armies, 29.10.2026). Moat work keeps Fast.
            if (anchorAvailable && !plan.MoatWorkMovement)
            {
                fastAnchorSkips++;
                summary = FastSummary(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, false, default);
                return false;
            }
            FastRoutingState state = GetFastRouting(false); RefreshFastRouting(state);
            bool ground = FastGroundReachable(state, plan.PlayerId, start, target, plan.UnitId);
            plan.QualifiedRoute = null;
            plan.QualifiedTerminalRoute = default;
            plan.QualifiedTerminalSummary = default;
            WeightedMoatRouteSummary found = default;
            if (!ground && TryFastEncoded(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, reserved, out found, out WeightedMoatEncodedRoute route, plan.UnitId))
                plan.QualifiedRoute = new QualifiedMovementRoute(sx, sy, plan.TargetX, plan.TargetY, plan.PlayerId,
                    runtime.mapEpoch, CaptureCurrentGameTick(), runtime.placementRevision, route, found, default, false);
            if (!ground && plan.QualifiedRoute == null && plan.MoatWorkMovement &&
                TryBuildTerminalFillRoute(plan, unit, sx, sy, out found, out WeightedMoatEncodedRoute terminal))
            { plan.QualifiedTerminalRoute = terminal; plan.QualifiedTerminalSummary = found; }
            summary = FastSummary(plan.PlayerId, sx, sy, plan.TargetX, plan.TargetY, ground, found);
            return !ground && (plan.QualifiedRoute != null || plan.QualifiedTerminalRoute.IsValid) && found.MoatEdges > 0;
        }

        private RouteProbeSummary FastSummary(int player, int sx, int sy, int tx, int ty, bool ground, WeightedMoatRouteSummary found)
        {
            int a = GameTileManagerAPI.Instance.GetTileId(sx, sy), b = GameTileManagerAPI.Instance.GetTileId(tx, ty);
            return new RouteProbeSummary(player) { StartRegion = IsValidTileId(a) ? runtime.pathRegionGrid[a] : 0,
                TargetRegion = IsValidTileId(b) ? runtime.pathRegionGrid[b] : 0, RouteFound = !ground && found.Found && found.MoatEdges > 0,
                AttackProbeEvaluated = true, ReachedWithoutMoat = ground, ReachedWithMoat = !ground && found.Found,
                FriendlyMoatTiles = found.MoatEdges, StructuralEdgesObserved = found.StructuralEdges,
                RouteDistance = found.RouteLength };
        }
    }
}
