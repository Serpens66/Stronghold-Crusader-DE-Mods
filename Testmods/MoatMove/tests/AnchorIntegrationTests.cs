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

                InitializeAnchor();
                Check(anchorAvailable, "anchor core does not load from the plugin directory");
                ResetAnchorMap();
                int total = MapWidth * MapWidth, expected = (total + AnchorMaskNodesPerTick - 1) / AnchorMaskNodesPerTick, ticks = 0;
                while (!AnchorReady(1) && ticks <= expected) AnchorTick(++ticks);
                Check(ticks == expected, $"mask preparation took {ticks} ticks instead of {expected}");
                for (int p = 2; p <= 8; p++) Check(anchorMaskNext[p] == -1, "mask started for a player that is not alive");

                int start = 10 * MapWidth + 10;
                Check(AnchorNative.mma_task_create(anchorContext, 1, 10 * MapWidth + 150, 1, 7, new[] { start }, 1, out int task) == 0,
                    "task creation rejected");
                anchorLiveTasks.Add(task);
                anchorTaskBudget = AnchorTasksPerTick; StepAnchorTasks();
                Check(AnchorNative.mma_task_status(anchorContext, task) == AnchorNative.Ready && anchorTaskBudget == AnchorTasksPerTick - 1,
                    "a new task is not computed within the same tick budget");
                anchorDispatchTask = task;
                var plan = new PlanScope(1, 150, 10) { PlayerId = 1 };
                long routes = anchorMoatRoutes;
                Check(TryAnchorRequiredRoute(plan, false, 10, 10, out _, out bool moat) && moat &&
                    plan.QualifiedRoute != null && anchorMoatRoutes == routes + 1, "moat route not published through the live audit");

                // Blocking a ground tile of the route is mirrored into the mask; the task is stale.
                int blocked = api.GetTileId(140, 10);
                nativeMovementMasks[blocked] = 0; MarkFastTopologyTile(blocked); AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, task) == AnchorNative.Stale, "terrain change leaves the task fresh");
                Check(!TryAnchorRequiredRoute(new PlanScope(1, 150, 10) { PlayerId = 1 }, false, 10, 10, out _, out _),
                    "stale task still answers");
                var search = new AnchorSearch { Owner = this, Task = task };
                for (int i = 0; i < AnchorMaxRestarts; i++)
                {
                    Check(TryRestartAnchorTask(search), "stale task not restarted");
                    anchorTaskBudget = AnchorTasksPerTick; StepAnchorTasks();
                    Check(AnchorNative.mma_task_status(anchorContext, task) == AnchorNative.Ready, "restarted task not recomputed");
                    nativeMovementMasks[blocked] = (byte)(i % 2 == 0 ? 0x44 : 0); MarkFastTopologyTile(blocked); AnchorTick(++ticks);
                }
                Check(!TryRestartAnchorTask(search), "stale task restarts without bound");
                nativeMovementMasks[blocked] = 0x44; MarkFastTopologyTile(blocked); AnchorTick(++ticks);

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
