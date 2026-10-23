using System;
using System.Runtime.InteropServices;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        // Native selection is an external boundary; dispatch, provider, scope and
        // incremental tile search below are the actual extracted production methods.
        private void DigWorkFallbackTests()
        {
            var api = GameTileManagerAPI.Instance;
            IntPtr oldTiles = api.TileManager;
            var oldSelector = originalFindMoatWorkTarget;
            var oldResolver = originalResolveMoatWorkTile;
            GameUnit* units = GameUnitManagerAPI.Instance.Units;
            GameUnit oldUnit = units[1];
            int oldMode = settings.RouteMode;
            ushort oldMoatRegion = pathRegionGrid[1013];
            byte* records = (byte*)NativeMemory.AllocZeroed((nuint)(MoatRecordCountOffset + 16));
            try
            {
                api.TileManager = (IntPtr)records;
                *(int*)(records + MoatRecordCountOffset) = 2;
                byte* record = records + MoatRecordArrayOffset + MoatRecordSize;
                *(int*)(record + MoatRecordTileIdOffset) = 1017;
                *(short*)(record + MoatRecordXOffset) = 17;
                *(short*)(record + MoatRecordYOffset) = 10;
                units[1].r_CurrentTilePositionX = units[1].r_NextTilePositionX2 = 10;
                units[1].r_CurrentTilePositionY = units[1].r_NextTilePositionY2 = 10;
                units[1].r_ControllableForPlayerId = 1;
                units[1].r_AliveState = AliveState.IsAlive; units[1].Digger = true;
                pathRegionGrid[1013] = 0;
                activeMoveCommand = null; activePlan = pendingPlan = null;
                ClearUnitMoveFrames(); activeMoatWorkSelection = null;

                foreach (int mode in new[] { 0, 1 })
                {
                    settings.RouteMode = mode;
                    foreach (bool addon in new[] { false, true })
                    foreach (bool startsInMoat in new[] { false, true })
                    foreach (int answer in new[] { -1, 0, 1 })
                    {
                        // The successful native answer includes an unsuccessful
                        // intermediate probe, as E7C40 can try multiple exits.
                        if (addon && answer < 0) continue;
                        nativeTraversalDisabled = !addon;
                        units[1].r_CurrentTilePositionX = startsInMoat ? 13 : 10;
                        int calls = 0, generation = gridGeneration;
                        var parent = new MoatWorkSelectionScope(mapEpoch, (IntPtr)records, 1, 1, 1,
                            units[1].r_CurrentTilePositionX, 10, startsInMoat ? 1013 : 1010, startsInMoat ? 0 : 1)
                            { TraversalWorkState = new DigWorkState() };
                        activeMoatWorkSelection = parent;
                        originalFindMoatWorkTarget = (m,p,u,r) =>
                        {
                            calls++;
                            Check(activeMoatWorkSelection == null, "native work attempt does not inherit a parent fallback");
                            Check(!TryAllowDigWorkRegionPair(nativePathManager, 1, 1, 2, 0, 0),
                                "failed intermediate native work probe starts no addon search");
                            Check(!TryAllowDigWorkRegionSearch(nativePathManager, 1, 2,
                                units[1].r_CurrentTilePositionX, 10, 0, out _), "native moat exit search is unmodified");
                            return answer;
                        };
                        Check(FindMoatWorkTargetWithOwnerRoute((IntPtr)records,1,1,1) == answer && calls == 1,
                            "vanilla-only and successful native selections execute once");
                        Check(gridGeneration == generation && pendingDigMoatTarget == null,
                            "native work builds zero extra search maps and handoffs");
                        Check(ReferenceEquals(parent, activeMoatWorkSelection), "nested native work restores outer scope");
                        activeMoatWorkSelection = null;
                    }

                    nativeTraversalDisabled = false;
                    units[1].r_CurrentTilePositionX = 10;
                    int attempts = 0, reservations = 0;
                    originalFindMoatWorkTarget = (m,p,u,r) =>
                    {
                        attempts++;
                        if (!TryAllowDigWorkRegionPair(nativePathManager,p,1,2,0,0)) return -1;
                        var scope = activeMoatWorkSelection;
                        int expanded = cachedReachabilityExpandedNodes;
                        Check(reachabilityQueueHead < reachabilityQueueTail,
                            "positive work-region witness stops before exhausting the frontier");
                        Check(TryAllowDigWorkRegionPair(nativePathManager,p,1,2,0,0) &&
                            cachedReachabilityExpandedNodes == expanded && scope.SearchBuilds == 1,
                            "same work region reuses the incremental search");
                        reservations++; return 1;
                    };
                    Check(FindMoatWorkTargetWithOwnerRoute((IntPtr)records,1,1,1) == 1 && attempts == 2 && reservations == 1,
                        "only failed complete native selection is retried; one reservation");
                    var pending = pendingDigMoatTarget;
                    Check(pending != null && pending.TileId == 1017 && pending.SearchScope.SearchBuilds == 1,
                        "friendly-moat work keeps the exact endpoint handoff");
                    Check(!TryGetDigWorkRoute(pending.SearchScope,10,9,out _),
                        "square coordinates outside the native map cannot index native tile arrays");
                    int endpointExpanded = cachedReachabilityExpandedNodes;
                    Check(ValidatePendingDigTarget(pending) && cachedReachabilityExpandedNodes == endpointExpanded,
                        "handoff validation reuses the exact endpoint route");
                    originalResolveMoatWorkTile = (m,id,kind,x,y) =>
                    {
                        *(int*)((byte*)m + SelectedMoatApproachXOffset) = 17;
                        *(int*)((byte*)m + SelectedMoatApproachYOffset) = 10;
                        return 1017;
                    };
                    Check(ResolveMoatWorkTileWithOwnerRoute((IntPtr)records,1,1,10,10) == 1017 &&
                        pendingPlan != null && pendingPlan.MoatWorkMovement && pendingPlan.UnitId == 1 &&
                        pendingPlan.TargetX == 17 && pendingDigMoatTarget == null,
                        "actual native resolver hands the qualified dig target to the unit planner once");
                    Check(cachedReachabilityExpandedNodes == endpointExpanded, "resolver does not repeat the endpoint search");
                    units[1].r_CurrentTilePositionX = 11;
                    Check(!ValidatePendingDigTarget(pending), "moved worker invalidates the work handoff");
                    units[1].r_CurrentTilePositionX = 10;
                    mapEpoch++;
                    Check(!ValidatePendingDigTarget(pending), "map replacement invalidates the work handoff");
                    mapEpoch--;
                    tick++;
                    Check(!ValidatePendingDigTarget(pending), "expired work handoff is rejected");

                    foreach (bool enemy in new[] { false, true })
                    {
                        if (enemy) enemyTiles.Add(1013); else tileFlags[1013] = 0x100;
                        int negativeExpanded = -1;
                        originalFindMoatWorkTarget = (m,p,u,r) =>
                        {
                            if (activeMoatWorkSelection == null) return -1;
                            Check(!TryAllowDigWorkRegionPair(nativePathManager,p,1,2,0,0),
                                "enemy or blocked moat cannot authorize work");
                            negativeExpanded = cachedReachabilityExpandedNodes;
                            Check(!TryAllowDigWorkRegionPair(nativePathManager,p,1,3,0,0) &&
                                cachedReachabilityExpandedNodes == negativeExpanded,
                                "exhausted negative frontier is reused for other regions");
                            return -1;
                        };
                        Check(FindMoatWorkTargetWithOwnerRoute((IntPtr)records,1,1,1) == -1 &&
                            negativeExpanded >= 0 && pendingDigMoatTarget == null, "unreachable work has no reservation or handoff");
                        enemyTiles.Remove(1013); tileFlags[1013] = CompletedMoatTileFlag;
                    }
                }
                Console.WriteLine("PASS: dig work uses zero extra searches for vanilla success/main alone; addon-only failure retry, early exit, negative reuse, reservation and exact resolver handoff.");
            }
            finally
            {
                api.TileManager = oldTiles; originalFindMoatWorkTarget = oldSelector;
                originalResolveMoatWorkTile = oldResolver;
                units[1] = oldUnit; settings.RouteMode = oldMode;
                pathRegionGrid[1013] = oldMoatRegion; nativeTraversalDisabled = false;
                activeMoatWorkSelection = null; pendingDigMoatTarget = null; pendingPlan = null;
                cacheMapEpoch = -1; NativeMemory.Free(records);
            }
        }
    }
}
