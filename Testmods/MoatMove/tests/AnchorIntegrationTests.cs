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

                InitializeAnchor();
                Check(anchorAvailable, "anchor core does not load from the plugin directory");
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

                // A failed live audit re-reads only the stale area (no full resweep); the next tick
                // mirrors the change and the task reports stale.
                int auditTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long resweeps = anchorResweeps, rejects = anchorEncodeRejects;
                nativeMovementMasks[blocked] = 0;
                Check(!TryAnchorRequiredRoute(new PlanScope(1, 150, 10) { PlayerId = 1 }, false, 10, 10, out _, out _, auditTask) &&
                    anchorEncodeRejects == rejects + 1, "a route over an unreported terrain change passes the live audit");
                AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, auditTask) == AnchorNative.Stale && anchorResweeps == resweeps && anchorSweep[1] == -1,
                    "a failed audit does not refresh just its stale area");
                nativeMovementMasks[blocked] = 0x44; MarkAnchorArea(10 * MapWidth + 140); AnchorTick(++ticks);
                ReleaseAnchorTask(auditTask);

                // BFQoL's tile hooks report the changed tile: only its area is re-read (no
                // resweep). A nested unit move (terrain: false) leaves the masks untouched.
                int tileTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long tileResweeps = anchorResweeps;
                nativeMovementMasks[blocked] = 0; InvalidateMovementSearchData(blocked); AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, tileTask) == AnchorNative.Stale && anchorResweeps == tileResweeps && anchorSweep[1] == -1,
                    "a reported tile change does not refresh just its area");
                nativeMovementMasks[blocked] = 0x44; InvalidateMovementSearchData(terrain: false); AnchorTick(++ticks);
                Check(anchorResweeps == tileResweeps, "a nested unit move requests a terrain resweep");
                InvalidateMovementSearchData(blocked); AnchorTick(++ticks);
                ReleaseAnchorTask(tileTask);

                // A resweep request during a running round extends it from the cursor instead of
                // restarting at node 0, so frequent requests still reach every node.
                ResweepAnchorMasks(); AnchorTick(++ticks); AnchorTick(++ticks);
                int cursor = anchorSweep[1];
                Check(cursor == 2 * AnchorResweepNodesPerTick, $"resweep cursor {cursor} after two ticks");
                ResweepAnchorMasks();
                Check(anchorSweep[1] == cursor && anchorSweepLeft[1] == total, "a resweep request restarts the running round");
                for (int i = 0; i < (total + AnchorResweepNodesPerTick - 1) / AnchorResweepNodesPerTick; i++) AnchorTick(++ticks);
                Check(anchorSweep[1] == -1 && anchorSweepLeft[1] == 0, "an extended resweep round does not finish after one full map");

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
