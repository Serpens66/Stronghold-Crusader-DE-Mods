using System;
using System.IO;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        // Anchor mode against the real native core (build.bat builds it into .local/anchor
        // before the tests): masks start for alive human players only and complete in a fixed
        // number of ticks, a moat route passes the live audit, a terrain change on the route
        // makes the task stale, stale restarts are bounded, and a steady tick allocates nothing.
        // Replan watch (RouteReplanPolicy with AnchorSingleMoves) on the anchor corridor (ground east of
        // x 136, player 1's mask ready): a walking unit 3 steps before a new rule tile re-plans to its
        // unchanged target and keeps an allowed detour; a detour beyond the policy stops it on its next
        // tile; once the core finds a clear route the unit resumes its target. Unit 7 walks east from
        // (140,10) to (160,10); MoveHandler plays 196280's result.
        private void ReplanWatchTests(ref int ticks)
        {
            bool singleBefore = settings.AnchorSingleMoves; settings.AnchorSingleMoves = true;
            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            GameUnit* walker = units.Units + 7;
            GameUnit saved = *walker;
            Check(APIShared.Pathfinding.RouteTileRules.TryRegister("tests.moatmove", "replan", out APIShared.Pathfinding.RouteTileRuleSet fire, out _),
                "replan rule registration failed");
            fire.ReplanPolicy = new APIShared.Pathfinding.RouteReplanPolicy(3, 50, 10, 400);
            int[] Steps(int first, int east, int last)
            {
                var steps = new System.Collections.Generic.List<int>();
                if (first >= 0) steps.Add(first);
                for (int i = 0; i < east; i++) steps.Add(2);
                if (last >= 0) steps.Add(last);
                return steps.ToArray();
            }
            int[] straight = Steps(-1, 20, -1), detour = Steps(1, 18, 3), tooLong = Steps(1, 38, 3);
            int[] answer = detour;
            units.MoveHandler = (id, x, y) =>
            {
                if (x == 160 && y == 10) Route(id, 140, 10, answer, x, y);
                else Route(id, x, y, new int[0], x, y);
            };
            try
            {
                walker->r_GlobalId = 9007; walker->r_ControllableForPlayerId = 1; walker->r_AliveState = AliveState.IsAlive; walker->r_TribeId = 0;
                walker->r_IsKilledByProjectile = 0; walker->r_MovementSubstep = 0;
                Route(7, 140, 10, straight, 160, 10);
                units.Moves.Clear();
                long detours = replanDetours, waits = replanWaitsStarted, resumed = replanResumed;
                Check(fire.Set(143, 10, APIShared.Pathfinding.RouteTileEffect.Penalty, 254), "fire rule rejected");
                AnchorTick(++ticks);
                Check(replanTileCount == 1, "the replan tile set misses the new rule tile");
                // New rule tiles check every 8th unit for 8 ticks: unit 7 is decided within them.
                for (int i = 0; i < 8; i++) ReplanTick(++ticks);
                Check(replanDetours == detours + 1 && units.Moves.Count == 1 && units.Moves[0][0] == 7 && units.Moves[0][1] == 160 && units.Moves[0][2] == 10,
                    $"an allowed detour was not taken (moves={units.Moves.Count} detours={replanDetours - detours})");

                // The same situation with a 40-step answer (allowed: 20 + 10): the unit stops on its next tile.
                Route(7, 140, 10, straight, 160, 10); answer = tooLong; units.Moves.Clear();
                for (int i = 0; i < 40 && replanWaitsStarted == waits; i++) ReplanTick(++ticks);
                Check(replanWaitsStarted == waits + 1 && replanWaits.ContainsKey(7) && units.Moves.Count == 2 &&
                    units.Moves[1][1] == 140 && units.Moves[1][2] == 10 && walker->r_PathPlanStateBitFlags == 0,
                    $"a detour beyond the policy did not stop the unit (moves={units.Moves.Count})");

                // The fire goes out: the next probe finds the clear core route and the unit resumes.
                fire.ClearAll(); AnchorTick(++ticks); answer = straight; units.Moves.Clear();
                for (int i = 0; i < 64 && replanResumed == resumed; i++) ReplanTick(++ticks);
                Check(replanResumed == resumed + 1 && !replanWaits.ContainsKey(7) && units.Moves.Count == 1 && units.Moves[0][1] == 160,
                    $"a waiting unit did not resume once its route was clear (moves={units.Moves.Count})");
                // Without rule tiles or waits the watch returns at once.
                long checks = replanChecks;
                for (int i = 0; i < 40; i++) ReplanTick(++ticks);
                Check(replanChecks == checks, "the replan watch checks units without rule tiles");
            }
            finally
            {
                units.MoveHandler = null; units.Moves.Clear(); fire.ClearAll(); fire.ReplanPolicy = null; AnchorTick(++ticks);
                *walker = saved; ResetReplanWatch(); settings.AnchorSingleMoves = singleBefore;
            }
        }

        // Writes a stored route starting at (x,y) and the move target into unit id's record and buffer.
        private void Route(int id, int x, int y, int[] steps, int targetX, int targetY)
        {
            GameUnit* unit = GameUnitManagerAPI.Instance.Units + id;
            byte* path = nativeUnitManager + NativeUnitPathBufferOffset + id * NativeUnitPathBufferStride;
            for (int i = 0; i < NativeUnitPathBufferStride; i++) path[i] = 0;
            for (int i = 0; i < steps.Length; i++) path[i >> 1] |= (byte)(steps[i] << ((i & 1) * 4));
            unit->r_PreviousTilePositionX = unit->r_NextTilePositionX2 = unit->r_CurrentTilePositionX = x;
            unit->r_PreviousTilePositionY = unit->r_NextTilePositionY2 = unit->r_CurrentTilePositionY = y;
            unit->r_CurrentPathPlanIndex = 0; unit->r_PathPlanLength = steps.Length; unit->r_PathPlanStateBitFlags = steps.Length > 0 ? 2 : 0;
            unit->r_TargetTilePositionX2 = targetX; unit->r_TargetTilePositionY2 = targetY;
        }

        private void AnchorIntegrationTests()
        {
            string directory = Environment.GetEnvironmentVariable("MOAT_TEST_ANCHOR_DIR");
            if (string.IsNullOrEmpty(directory) || !File.Exists(Path.Combine(directory, AnchorNative.Library)))
                throw new FileNotFoundException("Anchor core required: run native/Build-MoatMoveAnchor.ps1 first.", directory);
            var api = GameTileManagerAPI.Instance;
            bool anchorBefore = settings.Anchor; string directoryBefore = settings.PluginDirectory; int modeBefore = settings.RouteMode;
            // Anchor mode runs on Fast's RequiredOnly flow only.
            settings.Anchor = true; settings.PluginDirectory = directory; settings.RouteMode = 1;
            try
            {
                // Same corridor as PlacementTests: ground 10..12, completed moat 13..135, ground 136..180.
                for (int x = 10; x <= 180; x++)
                {
                    int tile = api.GetTileId(x, 10);
                    tileFlags[tile] = x >= 13 && x <= 135 ? CompletedMoatTileFlag : 0x8000;
                    nativeMovementMasks[tile] = 0x44; nativeHeightLayer[tile] = 0; nativeBuildingLayer[tile] = 0;
                    pathRegionGrid[tile] = (ushort)(x < 13 ? 1 : x <= 135 ? 0 : 2);
                }
                nativeMovementMasks[api.GetTileId(10, 10)] = 0x04; nativeMovementMasks[api.GetTileId(180, 10)] = 0x40;
                cursorTopologies.Clear(); placementRevision++; MarkFastTopologyAll(); tick++;
                EnsureCursorTopology(1);

                // Ground shortcut: away from moats one live evaluation decides both policies. Words
                // equal a cache that evaluates both policies for every edge (moat corridor, its rims
                // and diagonal corners).
                var bothPolicies = new FastTraversalCache(MapWidth, MapWidth,
                    (int a, int b, int d, bool g, out bool w, out bool s) => AnchorLiveEdge(1, a, b, d, g, out w, out s));
                var shortcut = new FastTraversalCache(MapWidth, MapWidth,
                    (int a, int b, int d, bool g, out bool w, out bool s) => AnchorLiveEdge(1, a, b, d, g, out w, out s), GroundEqualsFriendly);
                for (int y = 8; y <= 12; y++)
                    for (int x = 8; x <= 182; x++)
                        Check(bothPolicies.Word(y * MapWidth + x) == shortcut.Word(y * MapWidth + x), $"ground shortcut changes the word of {x},{y}");
                Check(shortcut.EdgeEvaluations < bothPolicies.EdgeEvaluations, "ground shortcut saves no live evaluation");

                // Shared words: player-neutral nodes are read once for all players; near a gate closed to
                // one player and along the moat every player keeps its own word. Words equal unshared
                // per-player caches.
                int gateTile = api.GetTileId(60, 10), savedLayer = nativeBuildingLayer[gateTile];
                nativeBuildingLayer[gateTile] = 4242;
                anchorBlocked[2] = new System.Collections.Generic.HashSet<int> { 4242 }; anchorBlockedUnion.Clear(); anchorBlockedUnion.Add(4242);
                var sharedWords = new FastSharedWords(MapWidth * MapWidth);
                FastTraversalCache PlayerCache(int player, bool share) => new FastTraversalCache(MapWidth, MapWidth,
                    (int a, int b, int d, bool g, out bool w, out bool s) => AnchorLiveEdge(player, a, b, d, g, out w, out s), GroundEqualsFriendly,
                    share ? sharedWords : null, share ? AnchorPlayerNeutral : (FastPlayerNeutral)null);
                FastTraversalCache shared1 = PlayerCache(1, true), shared2 = PlayerCache(2, true), own1 = PlayerCache(1, false), own2 = PlayerCache(2, false);
                Check(own1.Word(10 * MapWidth + 59) != own2.Word(10 * MapWidth + 59), "the closed test gate does not differ between players");
                for (int y = 8; y <= 12; y++)
                    for (int x = 8; x <= 182; x++)
                    {
                        int node = y * MapWidth + x;
                        Check(shared1.Word(node) == own1.Word(node) && shared2.Word(node) == own2.Word(node), $"shared words differ from a player's own word at {x},{y}");
                    }
                Check(sharedWords.Reuses > 0, "no player-neutral word was shared");
                nativeBuildingLayer[gateTile] = (ushort)savedLayer; anchorBlocked[2] = null; anchorBlockedUnion.Clear();

                InitializeAnchor();
                Check(anchorAvailable, "anchor core does not load from the plugin directory");

                // Reachability words (anchor mode): every corridor edge answers exactly like the live
                // FriendlyOnly predicate VisitNeighbour used before (allowed, moat kind, structure).
                FastTraversalCache reach = ReachabilityWords(1);
                Check(reach != null, "anchor mode provides no reachability words");
                for (int y = 9; y <= 11; y++)
                    for (int x = 9; x <= 181; x++)
                        for (int d = 0; d < 8; d++)
                        {
                            int nx = x + WeightedMoatRoutePlanner.DirectionX[d], ny = y + WeightedMoatRoutePlanner.DirectionY[d];
                            bool live = weightedMoatRoutePlanner.TryGetTraversalEdge(1, x, y, api.GetTileId(x, y), nx, ny, api.GetTileId(nx, ny), d,
                                false, false, MoatTraversalPolicy.FriendlyOnly, out MoatTraversalEdgeKind kind, out bool liveStructure);
                            bool cached = reach.Edge(y * MapWidth + x, ny * MapWidth + nx, d, false, out bool wet, out bool structure);
                            Check(cached == live && (!live || wet == (kind != MoatTraversalEdgeKind.Ground) && structure == liveStructure),
                                $"reachability word differs from the live predicate at {x},{y} direction {d}");
                        }
                // Seeded from the anchor mask: the same answers, also next to a gate the mask closes.
                nativeBuildingLayer[gateTile] = 4242;
                anchorBlocked[2] = new System.Collections.Generic.HashSet<int> { 4242 };
                anchorMaps[2] = PlayerCache(2, false);
                for (int y = 8; y <= 12; y++) for (int x = 8; x <= 182; x++) anchorMaps[2].Word(y * MapWidth + x);
                // A pending resweep round has not re-read these words yet: they are not seeded.
                ResweepAnchorMasks(); reachWords[2] = null;
                FastTraversalCache pendingReach = ReachabilityWords(2);
                pendingReach.Edge(10 * MapWidth + 20, 10 * MapWidth + 21, 2, false, out _, out _);
                Check(pendingReach.Seeded == 0, "a word not yet re-read by a pending resweep round was seeded");
                // The round finished: every clean anchor word is current.
                anchorSweepCursor = -1; anchorSweepLeft = 0; reachWords[2] = null;
                FastTraversalCache seededReach = ReachabilityWords(2);
                for (int y = 9; y <= 11; y++)
                    for (int x = 9; x <= 181; x++)
                        for (int d = 0; d < 8; d++)
                        {
                            int nx = x + WeightedMoatRoutePlanner.DirectionX[d], ny = y + WeightedMoatRoutePlanner.DirectionY[d];
                            bool live = weightedMoatRoutePlanner.TryGetTraversalEdge(2, x, y, api.GetTileId(x, y), nx, ny, api.GetTileId(nx, ny), d,
                                false, false, MoatTraversalPolicy.FriendlyOnly, out _, out _);
                            Check(seededReach.Edge(y * MapWidth + x, ny * MapWidth + nx, d, false, out _, out _) == live,
                                $"seeded reachability word differs from the live predicate at {x},{y} direction {d}");
                        }
                Check(seededReach.Seeded > 0, "no reachability word was taken from the anchor mask");
                nativeBuildingLayer[gateTile] = (ushort)savedLayer; anchorBlocked[2] = null; anchorMaps[2] = null; reachWords[2] = null;
                ResetAnchorMap();
                // The scheduler scans unit owners on the first tick and every 256 ticks.
                Check(AnchorPlayerScanDue(1), "first anchor tick does not scan unit owners");
                RequestAnchorMasksForPlayers(1 << 1);
                Check(!AnchorPlayerScanDue(2) && AnchorPlayerScanDue(256), "unit owners are not rescanned every 256 ticks");
                int total = MapWidth * MapWidth, expected = (total + AnchorMaskNodesPerTick - 1) / AnchorMaskNodesPerTick, ticks = 0;
                while (!AnchorReady(1) && ticks <= expected) AnchorTick(++ticks);
                Check(ticks == expected, $"mask preparation took {ticks} ticks instead of {expected}");
                for (int p = 2; p <= 8; p++) Check(anchorMaskNext[p] == -1, "mask started for a player without units");

                int start = 10 * MapWidth + 10, anchor = 10 * MapWidth + 150;
                long tasksBefore = anchorTasks;
                // Profile 8/56 reduces to 1/7; the group's task is ready when the call returns.
                int task = TryComputeAnchorTask(1, anchor, 8, 56, new[] { start }, 1);
                Check(task != 0 && AnchorNative.mma_task_status(anchorContext, task) == AnchorNative.Ready && anchorTasks == tasksBefore + 1,
                    "a group move's task is not computed synchronously");
                Check(TryComputeAnchorTask(1, anchor, 8, -1, new[] { start }, 1) == 0 && TryComputeAnchorTask(1, anchor, 1, 7, new int[0], 0) == 0,
                    "an invalid profile or empty group creates a task");
                anchorDispatchTask = task;
                var plan = new PlanScope(1, 150, 10) { PlayerId = 1 };
                long routes = anchorMoatRoutes;
                Check(TryAnchorRequiredRoute(plan, false, 10, 10, out _, out bool moat, task) && moat &&
                    plan.QualifiedRoute != null && anchorMoatRoutes == routes + 1, "moat route not published through the live audit");
                // Publication skips its second per-edge check only for routes audited in this tick.
                Check(IsAnchorAuditedRoute(plan.QualifiedRoute) && !IsAnchorAuditedRoute(new QualifiedMovementRoute(10, 10, 150, 10, 1,
                    mapEpoch, CaptureCurrentGameTick(), placementRevision, plan.QualifiedRoute.Route, plan.QualifiedRoute.Summary, default, false)),
                    "only the audited route object is marked for publication");
                QualifiedMovementRoute audited = plan.QualifiedRoute;

                // Phase 4 A: the mode probe qualifies the core's moat route. B: a ground decision
                // carries the core's ground route to the builder instead of Vanilla's search.
                var digger = new GameUnit { Digger = true, r_GlobalId = 901, r_ControllableForPlayerId = 1, r_AliveState = AliveState.IsAlive,
                    r_CurrentTilePositionX = 10, r_CurrentTilePositionY = 10, r_NextTilePositionX2 = 10, r_NextTilePositionY2 = 10, r_MovementSubstep = 8 };
                var walker = digger; walker.r_GlobalId = 902;
                walker.r_CurrentTilePositionX = walker.r_NextTilePositionX2 = 137;
                var probe = new PlanScope(1, 150, 10);
                Check(TryAnchorQualifiesMoat(probe, &digger) && probe.QualifiedRoute != null && probe.QualifiedRoute.Summary.MoatEdges > 0,
                    "mode probe ignores the core's moat route");
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10), &digger), "builder publishes a moat answer as ground route");
                int groundTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { 10 * MapWidth + 137 }, 1);
                Check(groundTask != 0, "ground task not computed");
                anchorDispatchTask = groundTask;
                var groundPlan = new PlanScope(1, 150, 10);
                Check(!TryAnchorQualifiesMoat(new PlanScope(1, 150, 10), &walker), "ground decision qualifies as moat");
                long groundRoutes = anchorGroundRoutes;
                Check(TryPrepareAnchorGround(groundPlan, &walker) && groundPlan.QualifiedRoute != null &&
                    groundPlan.QualifiedRoute.Summary.MoatEdges == 0 && anchorGroundRoutes == groundRoutes + 1, "ground route not prepared for the builder");
                // The builder reuses the mode probe's ground decision instead of a second query.
                // Reuse binds the plan to the live unit slot, so the walker takes slot 2 here.
                GameUnit* pool = GameUnitManagerAPI.Instance.Units;
                pool[2] = walker;
                var reusePlan = new PlanScope(2, 150, 10);
                long reuses = anchorRouteReuses, queries = anchorQueries;
                Check(!TryAnchorQualifiesMoat(reusePlan, pool + 2) && reusePlan.QualifiedRoute != null && TryPrepareAnchorGround(reusePlan, pool + 2) &&
                    anchorRouteReuses == reuses + 1 && anchorQueries == queries + 1, "builder repeats the mode probe's ground query");
                pool[2] = default;
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10) { ExactRouteEndpoints = true }, &walker),
                    "intermediate builder target uses the anchor route");
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10) { ModeObserved = true, VanillaModeDetected = true }, &walker),
                    "Vanilla's own moat mode replaced by a ground route");
                anchorDispatchTask = 0;
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10), &walker) && !TryAnchorQualifiesMoat(new PlanScope(1, 150, 10), &digger),
                    "anchor answers outside an anchor dispatch");
                ReleaseAnchorTask(groundTask);

                // Units that cannot enter moats use the group's ground-only task (moat cost 0):
                // the corridor through the moat is closed for them, its ground part stays open.
                var rider = walker; rider.Digger = false; rider.r_GlobalId = 903;
                var blockedRider = digger; blockedRider.Digger = false; blockedRider.r_GlobalId = 904;
                int onlyGround = TryComputeAnchorTask(1, anchor, 1, 0, new[] { 10 * MapWidth + 137, start }, 2);
                Check(onlyGround != 0, "ground-only task not computed");
                anchorDispatchTask = 0; anchorGroundTask = onlyGround;
                var riderPlan = new PlanScope(1, 150, 10);
                Check(TryPrepareAnchorGround(riderPlan, &rider) && riderPlan.QualifiedRoute != null &&
                    riderPlan.QualifiedRoute.Summary.MoatEdges == 0, "non-digger receives no ground route");
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10), &blockedRider), "ground-only task routes a non-digger through the moat");
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10), &walker), "a digger uses the ground-only task");
                anchorGroundTask = 0; ReleaseAnchorTask(onlyGround);

                // Outside group moves the anchor mode answers nothing and runs no synchronous Fast
                // field (combat resume, AI orders before their mask is ready keep Vanilla).
                var units = GameUnitManagerAPI.Instance.Units;
                units[1] = new GameUnit { Digger = true, r_GlobalId = 905, r_TribeId = 1, r_ControllableForPlayerId = 1, r_AliveState = AliveState.IsAlive,
                    r_CurrentTilePositionX = 10, r_CurrentTilePositionY = 10, r_NextTilePositionX2 = 10, r_NextTilePositionY2 = 10,
                    r_CurrentPositionTileId = (uint)api.GetTileId(10, 10), r_MovementSubstep = 8 };
                anchorDispatchTask = 0;
                long skips = fastAnchorSkips, builds = GetFastRouting(false).Pool.Builds;
                Check(!TryFindFastRequiredRoute(new PlanScope(1, 150, 10), false, true, out _) && fastAnchorSkips == skips + 1 &&
                    GetFastRouting(false).Pool.Builds == builds, "anchor mode runs a synchronous Fast field outside group moves");
                anchorDispatchTask = task;

                // Blocking a ground tile of the route is mirrored into the mask; the task is stale.
                int blocked = api.GetTileId(140, 10);
                nativeMovementMasks[blocked] = 0; MarkFastTopologyTile(blocked); AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, task) == AnchorNative.Stale, "terrain change leaves the task fresh");
                Check(!IsAnchorAuditedRoute(audited), "an audit mark survives its tick");
                Check(!TryAnchorRequiredRoute(new PlanScope(1, 150, 10) { PlayerId = 1 }, false, 10, 10, out _, out _, task),
                    "stale task still answers");
                nativeMovementMasks[blocked] = 0x44; MarkFastTopologyTile(blocked); AnchorTick(++ticks);
                ReleaseAnchorTask(task); anchorDispatchTask = 0;

                // Group move frames: a nested move uses only its own tasks (none hides the outer
                // ones), its end restores the outer tasks, and a move without Post (skipped
                // original) ends with the enclosing move or at the next tick.
                int outer = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                int outerGround = TryComputeAnchorTask(1, anchor, 1, 0, new[] { 10 * MapWidth + 137 }, 1);
                int inner = TryComputeAnchorTask(1, anchor, 1, 7, new[] { 10 * MapWidth + 137 }, 1);
                BeginAnchorFrame(7, outer, outerGround); BeginAnchorFrame(8, inner);
                Check(anchorDispatchTask == inner && anchorGroundTask == 0, "nested move does not use only its own tasks");
                BeginAnchorFrame(9, 0);
                Check(anchorDispatchTask == 0, "a nested move without task uses an enclosing move's task");
                EndAnchorFrame(8);
                Check(anchorDispatchTask == outer && anchorGroundTask == outerGround && !anchorLiveTasks.Contains(inner) && anchorFrames.Count == 1,
                    "nested move end does not restore the outer tasks");
                BeginAnchorFrame(10, 0);
                AnchorTick(++ticks);
                Check(anchorFrames.Count == 0 && anchorDispatchTask == 0 && anchorGroundTask == 0 &&
                    !anchorLiveTasks.Contains(outer) && !anchorLiveTasks.Contains(outerGround), "open move frames survive the tick");

                // Members a group move never re-planned are counted with a sample of their ids.
                int counted = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long unreachedBefore = anchorUnreachedUnits;
                BeginAnchorFrame(11, counted, 0, new[] { 5, 6, 7 });
                MarkAnchorUnit(5); MarkAnchorUnit(7);
                EndAnchorFrame(11);
                Check(anchorEndedUnreached == 1 && anchorEndedMembers == 3 && anchorUnreachedSample[0] == 6 &&
                    anchorUnreachedUnits == unreachedBefore + 1, "a member without re-plan is not counted");
                anchorEndedUnreached = 0;

                // Pending tasks: a group move captures only its starts; the field is computed at
                // the first unit query, and never for a move whose members Vanilla does not re-plan.
                long tasksBeforeLazy = anchorTasks, unused = anchorUnusedMoves;
                var spec = new AnchorTaskSpec { Player = 1, Anchor = anchor, Ground = 1, Moat = 7, Starts = new[] { start } };
                BeginAnchorFrame(13, 0, 0, null, spec);
                Check(anchorDispatchTask == AnchorPendingTask && anchorTasks == tasksBeforeLazy, "a group move computes its field before any unit asks");
                EndAnchorFrame(13);
                Check(anchorTasks == tasksBeforeLazy && anchorUnusedMoves == unused + 1 && anchorDispatchTask == 0, "an unused group move computes a field");
                BeginAnchorFrame(14, 0, 0, null, spec);
                Check(TryAnchorQualifiesMoat(new PlanScope(1, 150, 10), &digger) && anchorTasks == tasksBeforeLazy + 1 && anchorDispatchTask > 0,
                    "the first unit query does not compute the pending field");
                int lazyTask = anchorDispatchTask;
                Check(TryAnchorQualifiesMoat(new PlanScope(1, 150, 10), &digger) && anchorTasks == tasksBeforeLazy + 1, "a later query recomputes the field");
                EndAnchorFrame(14);
                Check(!anchorLiveTasks.Contains(lazyTask) && anchorDispatchTask == 0, "a computed pending task survives its move");

                // Formation plans: BugfixesAndQoL's targets reach the move frame, the first query answers
                // all planned members in one batch, and a member's answer equals its single query.
                long batchHits = anchorBatchHits, batchUnits = anchorBatchUnits;
                GameUnit* members = GameUnitManagerAPI.Instance.Units;
                members[3] = digger; members[3].r_GlobalId = 906;
                BeginAnchorFrame(15, 0, 0, new[] { 3 }, spec);
                ObserveAnchorFormation(15, new[] { 3 }, new uint[] { 906 }, new[] { 150 }, new[] { 10 });
                var batchPlan = new PlanScope(3, 150, 10);
                Check(TryAnchorQualifiesMoat(batchPlan, members + 3) && anchorBatchUnits == batchUnits + 1 && anchorBatchHits == batchHits + 1,
                    "a planned member is not answered from the move's batch");
                anchorBatchIndex.Clear();
                var singlePlan = new PlanScope(3, 150, 10);
                Check(TryAnchorQualifiesMoat(singlePlan, members + 3) && anchorBatchHits == batchHits + 1, "a member without batch entry is not asked singly");
                byte[] batchBytes = batchPlan.QualifiedRoute.Route.Bytes, singleBytes = singlePlan.QualifiedRoute.Route.Bytes;
                bool sameBytes = batchBytes.Length == singleBytes.Length;
                for (int i = 0; sameBytes && i < batchBytes.Length; i++) sameBytes = batchBytes[i] == singleBytes[i];
                Check(sameBytes, "batch and single answers differ");
                EndAnchorFrame(15); members[3] = default;
                // A plan for another tribe never attaches to this move.
                BeginAnchorFrame(16, 0, 0, new[] { 3 }, spec);
                ObserveAnchorFormation(17, new[] { 3 }, new uint[] { 906 }, new[] { 150 }, new[] { 10 });
                Check(anchorFrames[anchorFrames.Count - 1].Targets == null, "a formation plan attaches to another tribe's move");
                EndAnchorFrame(16); AnchorTick(++ticks);

                // A failed live audit re-reads only the stale area (no full resweep); the next tick
                // mirrors the change and the task reports stale.
                int auditTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long resweeps = anchorResweeps, rejects = anchorEncodeRejects;
                nativeMovementMasks[blocked] = 0;
                Check(!TryAnchorRequiredRoute(new PlanScope(1, 150, 10) { PlayerId = 1 }, false, 10, 10, out _, out _, auditTask) &&
                    anchorEncodeRejects == rejects + 1, "a route over an unreported terrain change passes the live audit");
                AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, auditTask) == AnchorNative.Stale && anchorResweeps == resweeps && anchorSweepCursor == -1,
                    "a failed audit does not refresh just its stale area");
                nativeMovementMasks[blocked] = 0x44; MarkAnchorArea(10 * MapWidth + 140); AnchorTick(++ticks);
                ReleaseAnchorTask(auditTask);

                // BFQoL's tile hooks report the changed tile: only its area is re-read (no
                // resweep). A nested unit move (terrain: false) leaves the masks untouched.
                int tileTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long tileResweeps = anchorResweeps;
                nativeMovementMasks[blocked] = 0; InvalidateMovementSearchData(blocked); AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, tileTask) == AnchorNative.Stale && anchorResweeps == tileResweeps && anchorSweepCursor == -1,
                    "a reported tile change does not refresh just its area");
                nativeMovementMasks[blocked] = 0x44; InvalidateMovementSearchData(terrain: false); AnchorTick(++ticks);
                Check(anchorResweeps == tileResweeps, "a nested unit move requests a terrain resweep");
                InvalidateMovementSearchData(blocked); AnchorTick(++ticks);
                ReleaseAnchorTask(tileTask);

                // A direction byte changed without any report (gates, neighbouring buildings): the
                // native byte watch marks it within one watch round; no resweep.
                int watchTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long watchResweeps = anchorResweeps;
                nativeMovementMasks[blocked] = 0;
                int watchTicks = (MapWidth * MapWidth + AnchorWatchNodesPerTick - 1) / AnchorWatchNodesPerTick;
                for (int i = 0; i < watchTicks + 1; i++) AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, watchTask) == AnchorNative.Stale &&
                    anchorResweeps == watchResweeps, "an unreported direction byte change is not mirrored into the mask");
                nativeMovementMasks[blocked] = 0x44;
                for (int i = 0; i < watchTicks + 1; i++) AnchorTick(++ticks);
                ReleaseAnchorTask(watchTask);

                // APIShared route tile rules: a Forbidden tile loses its incoming edges for the selected
                // players only (the anchor mask follows within one tick); clearing the rule restores them.
                Check(APIShared.Pathfinding.RouteTileRules.TryRegister("tests.moatmove", "forbidden", out APIShared.Pathfinding.RouteTileRuleSet tileRules, out _),
                    "tile rule registration failed");
                int ruleFrom = 10 * MapWidth + 149, ruleTo = 10 * MapWidth + 150;
                Check(LiveEdge(1, ruleFrom, ruleTo, 2, false, null, out _, out _), "the corridor edge is closed before the rule");
                Check(tileRules.Set(150, 10, APIShared.Pathfinding.RouteTileEffect.Forbidden, 0, 1U << 1), "rule rejected");
                AnchorTick(++ticks);
                Check(!LiveEdge(1, ruleFrom, ruleTo, 2, false, null, out _, out _) && LiveEdge(2, ruleFrom, ruleTo, 2, false, null, out _, out _),
                    "a Forbidden tile rule does not close the edge for exactly its players");
                Check((anchorMaps[1].Word(ruleFrom) & (1U << 2)) == 0, "the anchor mask keeps an edge into a Forbidden tile");
                Check(LiveEdge(1, ruleTo, ruleFrom, 6, false, null, out _, out _), "a unit on a Forbidden tile cannot leave it");
                tileRules.ClearAll(); AnchorTick(++ticks);
                Check((anchorMaps[1].Word(ruleFrom) & (1U << 2)) != 0, "clearing the tile rule does not restore the edge");
                // A Penalty rule reaches the core: the player's tasks become stale, also when it is removed.
                int penaltyTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                Check(tileRules.Set(150, 10, APIShared.Pathfinding.RouteTileEffect.Penalty, 30, 1U << 1), "penalty rule rejected");
                AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, penaltyTask) == AnchorNative.Stale, "a Penalty rule does not reach the core");
                ReleaseAnchorTask(penaltyTask);
                penaltyTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                tileRules.ClearAll(); AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, penaltyTask) == AnchorNative.Stale, "a removed Penalty rule stays in the core");
                ReleaseAnchorTask(penaltyTask);
                ReplanWatchTests(ref ticks);

                // The word-based reachability search (cached words, node tile table) visits the same
                // cells with the same distances and summary as the per-neighbour search.
                Check(AnchorNodeTiles() != null, "the byte watch provides no node tile table after a full round");
                string ReachabilitySnapshot(bool disable)
                {
                    DisableWordReachability = disable;
                    try
                    {
                        EnsureReachabilityMap(1, 10, 10);
                        AdvanceReachabilityMap();
                        var text = new System.Text.StringBuilder();
                        for (int cell = 0; cell < MapWidth * MapWidth; cell++)
                        {
                            if (visitedWithoutMoat[cell] == gridGeneration) text.Append('g').Append(cell).Append(':').Append(distanceWithoutMoat[cell]).Append(' ');
                            if (visitedWithMoat[cell] == gridGeneration) text.Append('m').Append(cell).Append(':').Append(distanceWithMoat[cell]).Append(' ');
                        }
                        RouteProbeSummary summary = cachedRouteSummary;
                        return text.Append("expanded=").Append(cachedReachabilityExpandedNodes).Append(" moatTiles=").Append(summary.FriendlyMoatTiles)
                            .Append(" structural=").Append(summary.StructuralEdgesObserved).Append(" regions=").Append(cachedTraversedRegionCount).ToString();
                    }
                    finally { DisableWordReachability = false; }
                }
                string slowSearch = ReachabilitySnapshot(true), fastSearch = ReachabilitySnapshot(false);
                Check(slowSearch.Split(' ').Length > 100 && fastSearch == slowSearch, "the word-based reachability search differs from the per-neighbour search");
                // An exhausted search answers later starts it visited: a target it never reached is
                // unreachable without a second exhaustive search; a reachable target is still searched;
                // a word change at a visited cell discards it.
                EnsureReachabilityMap(1, 10, 10); AdvanceReachabilityMap();
                long hits = ExhaustedReachabilityHits;
                EnsureReachabilityMap(1, 11, 10, deferTraversal: true);
                AdvanceReachabilityMap(targetCell: 10 * MapWidth + 190);
                Check(ExhaustedReachabilityHits == hits + 1 && reachabilityQueueHead < reachabilityQueueTail && visitedWithoutMoat[10 * MapWidth + 190] != gridGeneration,
                    "an unreachable target is searched again from a start the exhausted search visited");
                AdvanceReachabilityMap(targetCell: 10 * MapWidth + 150);
                Check(ExhaustedReachabilityHits == hits + 1 && (visitedWithMoat[10 * MapWidth + 150] == gridGeneration || visitedWithoutMoat[10 * MapWidth + 150] == gridGeneration),
                    "a reachable target was answered unreachable from the exhausted search");
                int visitedTile = api.GetTileId(170, 10);
                nativeMovementMasks[visitedTile] = 0x40; MarkFastTopologyTile(visitedTile); AdvanceFastMarkEpoch(); RefreshReachabilityWords(1);
                Check(exhaustedReachability[1] == null, "a word change at a visited cell keeps the exhausted search");
                nativeMovementMasks[visitedTile] = 0x44; MarkFastTopologyTile(visitedTile); AdvanceFastMarkEpoch(); RefreshReachabilityWords(1);

                // A resweep request during a running round extends it from the cursor instead of
                // restarting at node 0, so frequent requests still reach every node.
                ResweepAnchorMasks(); AnchorTick(++ticks); AnchorTick(++ticks);
                int cursor = anchorSweepCursor;
                Check(cursor == 2 * AnchorResweepNodesPerTick, $"resweep cursor {cursor} after two ticks");
                ResweepAnchorMasks();
                Check(anchorSweepCursor == cursor && anchorSweepLeft == total, "a resweep request restarts the running round");
                for (int i = 0; i < (total + AnchorResweepNodesPerTick - 1) / AnchorResweepNodesPerTick; i++) AnchorTick(++ticks);
                Check(anchorSweepCursor == -1 && anchorSweepLeft == 0, "an extended resweep round does not finish after one full map");

                // Gate changes: an unchanged access snapshot requests nothing; a changed portal entry
                // without a building marks nothing and needs no resweep; one whose building footprint
                // is unknown falls back to a resweep, as does a changed portal table size.
                int* portal = (int*)nativePathManager;
                int savedCount = portal[0], gate = 0x81, savedState = portal[gate + 0x809], savedBuilding = portal[gate + 0x80C], savedOpen = portal[gate + 0x80F];
                AnchorTick(++ticks); long gateSweeps = anchorResweeps;
                AnchorTick(++ticks);
                Check(anchorResweeps == gateSweeps, "an unchanged access snapshot requests a resweep");
                portal[0] = 2; AnchorTick(++ticks);
                Check(anchorResweeps == gateSweeps + (savedCount == 2 ? 0 : 1), "a changed portal table size does not resweep");
                gateSweeps = anchorResweeps;
                portal[gate + 0x80F] = savedOpen == 0 ? 1 : 0; portal[gate + 0x80C] = 0; AnchorTick(++ticks);
                Check(anchorResweeps == gateSweeps, "a changed portal without building forces a resweep");
                portal[gate + 0x809] = 1; portal[gate + 0x80C] = 9999; AnchorTick(++ticks);
                Check(anchorResweeps == gateSweeps + 1, "a gate with unknown footprint does not fall back to a resweep");
                portal[0] = savedCount; portal[gate + 0x809] = savedState; portal[gate + 0x80C] = savedBuilding; portal[gate + 0x80F] = savedOpen;
                AnchorTick(++ticks);
                for (int i = 0; i < (total + AnchorResweepNodesPerTick - 1) / AnchorResweepNodesPerTick * 2; i++) AnchorTick(++ticks);

                // Steady state: no terrain change, no task work, not a player-request tick.
                AnchorTick(++ticks);
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 8; i++) AnchorTick(++ticks);
                Check((ticks & ~255) == ((ticks - 8) & ~255), "allocation window crossed a player-request tick");
                Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "steady anchor tick allocates");
                RequestAnchorMask(3);
                Check(anchorMaskNext[3] == 0, "a player's move order does not start that player's mask");
            }
            finally
            {
                ResetAnchorMap(); anchorAvailable = false;
                settings.Anchor = anchorBefore; settings.PluginDirectory = directoryBefore; settings.RouteMode = modeBefore;
            }
        }
    }
}
