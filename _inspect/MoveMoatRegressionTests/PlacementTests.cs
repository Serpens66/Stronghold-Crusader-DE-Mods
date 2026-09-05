using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private void PlacementTests()
        {
            var api = GameTileManagerAPI.Instance;
            var units = GameUnitManagerAPI.Instance.Units;
            var manager = (byte*)nativePathManager;
            activePlan = pendingPlan = null; ClearUnitMoveFrames(); activeMoveCommand = null;
            activeAttackCommand = null; activeMoatWorkSelection = null; activeAttackApproachDiagnostic = null;
            activeDirectCursorMove = new DirectCursorMoveScope(mapEpoch, 1, 1, 150, 10, 1150, 2, new[] { 1 }, default);
            Check(TryAllowDirectCursorMoveRegionPair(nativePathManager,1,2,1,0),"bound click feedback can extend a rejected region answer");
            activeMoveCommand = new MoveCommandScope();
            Check(!TryAllowDirectCursorMoveRegionPair(nativePathManager,1,2,1,0),"immediate chore dispatch cannot inherit the feedback-only region answer");
            activeMoveCommand = null;
            activePlan = new PlanScope(1,150,10);
            Check(!TryAllowDirectCursorMoveRegionPair(nativePathManager,1,2,1,0),"nested native probe keeps its own region contract");
            activePlan = null; activeDirectCursorMove = null;
            enemyTiles.Clear(); enemyPlayerId = 0; api.Occupants.Clear(); api.ForceOccupied = false;
            for (int x = 10; x <= 180; x++)
            {
                tileFlags[1000 + x] = x >= 13 && x <= 135 ? CompletedMoatTileFlag : 0x8000;
                nativeMovementMasks[1000 + x] = 0x44;
                nativeHeightLayer[1000 + x] = 0;
                nativeBuildingLayer[1000 + x] = 0;
                pathRegionGrid[1000 + x] = (short)(x < 13 ? 1 : x <= 135 ? 0 : 2);
                movementTargetAvailability[10 * 800 + x] = 1;
            }
            nativeMovementMasks[1010] = 0x04; nativeMovementMasks[1180] = 0x40;
            cursorTopologies.Clear(); placementRevision++; tick++;
            nativeTribeManager = (IntPtr)91;
            EnsureCursorTopology(1);
            originalPathBuilder = (m, c, p) => 0;
            originalUnitStandingOnCompletedMoat = (m, id) => IsCompletedMoatTile((int)units[id].r_CurrentPositionTileId) ? 1 : 0;
            originalRegionPairReachability = null;

            void ResetUnits(int count)
            {
                for (int id = 1; id <= count; id++)
                    units[id] = new GameUnit { Digger = true, r_GlobalId = (uint)(100 + id),
                        r_TribeId = 1, r_ControllableForPlayerId = 1, r_AliveState = AliveState.IsAlive,
                        r_CurrentTilePositionX = id % 2 == 0 ? 13 : 10, r_CurrentTilePositionY = 10,
                        r_NextTilePositionX2 = id % 2 == 0 ? 13 : 10, r_NextTilePositionY2 = 10,
                        r_CurrentPositionTileId = (uint)(id % 2 == 0 ? 1013 : 1010), r_MovingRelevant = 8 };
                activeMoveCommand = new MoveCommandScope { TribeId = 1, TargetX = 13, TargetY = 10, UnitsOnMoatAtDispatch = count / 2 };
                activePlan = pendingPlan = null; ClearUnitMoveFrames();
            }
            UnitMoveHereEventArgs Pre(int id)
            {
                units[id].r_AttackMoveToTargetTileX = 13; units[id].r_AttackMoveToTargetTileY = 10;
                var args = new UnitMoveHereEventArgs(EventHookPhase.Pre, id, 13, 10, 0);
                ObserveUnitMoveOrder(args); return args;
            }
            void Post(int id, long result)
            { ObserveUnitMoveOrder(new UnitMoveHereEventArgs(EventHookPhase.Post, id, 13, 10, 0) { ReturnValue = result }); }

            byte* tribeRecords = (byte*)NativeMemory.AllocZeroed(2 * TribeRecordSize);
            try
            {
                nativeTribeManager = (IntPtr)tribeRecords;
                *(short*)(tribeRecords + TribeRecordSize + TribeUnitCountOffset) = 2;
                foreach (int leader in new[] { 1, 2 })
                foreach (bool reverse in new[] { false, true })
                {
                    ResetUnits(2); activeMoveCommand.TargetX = 150;
                    *(short*)(tribeRecords + TribeRecordSize + TribeLeadUnitIdOffset) = (short)leader;
                    getGroupUnitId = (m,t,index) => reverse ? 2-index : index+1;
                    originalFirstGroupUnitOnCompletedMoat = (m,t) => 2;
                    int selected = SelectOwnerSafeGroupMoatMode(nativeTribeManager,1);
                    Check(selected == (leader == 1 ? 1 : 0), "island/moat group chooses working branch for both leaders and iteration orders");
                    Check(activeMoveCommand.MoatRelevant && activeMoveCommand.LastGroupMoatModeDiagnostic != null,
                        "early group decision cannot disappear from diagnostics");
                    // Baseline 11B520: leader==returned moat unit enters E7C40;
                    // otherwise a zero leader PCL enters 118E00, before any Unit builder.
                    bool common = selected != leader && pathRegionGrid[(int)units[leader].r_CurrentPositionTileId] == 0;
                    Check(common || selected == leader,"neither mixed order falls into the inaccessible ordinary-PCL branch");
                }
            }
            finally { NativeMemory.Free(tribeRecords); nativeTribeManager = (IntPtr)91; }

            foreach (int count in new[] { 1, 5, 20, 27, 29, 120 })
            {
                ResetUnits(count); var goals = new HashSet<int>();
                long allocated = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
                originalCommonGroupMove = (m, t, x, y, patrol, fresh) => {
                    for (int id = 1; id <= count; id++)
                    {
                        var args = Pre(id);
                        Check(unitMoveFrame.Placement != null, "common native branch owns a placement");
                        Check(goals.Add(args.TileY * 800 + args.TileX), "each group member gets a distinct free place");
                        *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager, id);
                        PlanScope plan = GetUnitMovePlan(unitMoveFrame, id);
                        Check(plan != null && plan.UnitId == id && plan.TargetX == args.TileX, "mode uses allocated native Unit target");
                        Check(units[id].r_AttackMoveToTargetTileX == args.TileX, "native destination fields agree with event");
                        GetNativeMovementStart(units + id, out int sx, out int sy);
                        *(int*)(manager + 8) = sx; *(int*)(manager + 12) = sy;
                        *(int*)(manager + 16) = args.TileX; *(int*)(manager + 20) = args.TileY;
                        *(byte**)(manager + PathManagerOutputBufferOffset) = nativeUnitManager + NativeUnitPathBufferOffset + id * NativeUnitPathBufferStride;
                        *(int*)(manager + PathManagerOutputLengthOffset) = 0;
                        if (plan.FriendlyRouteQualified)
                            Check(BuildPathWithCompletedMoatRouteVariant(nativePathManager, 1, 1) > 0,
                                "actual publisher accepts individual placement path and owned buffer");
                        Post(id, 1);
                    }
                    var state = placementBatch.Searches[1];
                    Check(state.ReverseExpanded + state.FromAnchor.ExpandedNodes < 500,
                        "different placement targets share connectivity work instead of flooding per unit");
                    Console.WriteLine($"PLACEMENT WORK units={count} candidates={state.Search.ExpandedNodes} connectivity={state.ReverseExpanded + state.FromAnchor.ExpandedNodes}");
                    return 1;
                };
                Check(ObserveCommonGroupMove(nativeTribeManager, 1, 13, 10, 0, 1) == 1 && placementBatch == null,
                    "native common group return restores context");
                watch.Stop(); allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Console.WriteLine($"PLACEMENT PRODUCTION units={count} unique={goals.Count} ms={watch.Elapsed.TotalMilliseconds:F3} allocatedBytes={allocated}");
            }

            ResetUnits(4);
            originalCommonGroupMove = (m,t,x,y,p,n) => {
                var skipped = Pre(1); var first = unitMoveFrame.Placement;
                skipped.SkipOriginalFunction = true;
                Check(GetCurrentUnitMoveFrame() == null && first.Finished, "skipped original releases slot without Post");
                Check(units[1].r_AttackMoveToTargetTileX == 13, "skip restores native common destination");
                var failed = Pre(2); int released = failed.TileX; Post(2,0);
                var changed = Pre(3); var third = unitMoveFrame.Placement;
                changed.TileX = 150; SynchronizePlacement(unitMoveFrame);
                Check(third.Released && changed.TileX == 150 && units[3].r_AttackMoveToTargetTileX == 150,
                    "later subscriber target matches native fields after original copied arguments"); Post(3,0);
                Check(units[3].r_AttackMoveToTargetTileX == 13,"rejected changed target restores saved native destination");
                var reused = Pre(4); var fourth = unitMoveFrame.Placement; units[4].r_GlobalId++;
                Post(4,0);
                Check(fourth.Finished && units[4].r_AttackMoveToTargetTileX == fourth.X,
                    "ID reuse never writes saved state into the replacement unit");
                return 1;
            };
            ObserveCommonGroupMove(nativeTribeManager,1,13,10,0,1);

            ResetUnits(3);
            originalCommonGroupMove = (m,t,x,y,p,n) => {
                var first=Pre(1);int used=first.TileY*800+first.TileX;Post(1,1);
                placementRevision++; // A synchronous terrain callback invalidates searches, not committed slots.
                var second=Pre(2);
                Check(second.TileY*800+second.TileX!=used,"committed reservation survives a search revision");Post(2,1);
                units[3].r_PathPlanStateBitFlags=1;units[3].r_MovingRelevant=0;
                units[3].r_NextTilePositionX2=14;
                var step=Pre(3);*moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,3);
                GetNativeMovementStart(units+3,out int sx,out int sy);
                Check(sx==14 && unitMoveFrame.Plan.TargetX==step.TileX,"moving group unit qualifies from Vanilla's next tile");Post(3,1);
                return 1;
            };
            ObserveCommonGroupMove(nativeTribeManager,1,13,10,0,1);

            ResetUnits(2);
            originalCommonGroupMove = (m,t,x,y,p,n) => {
                Pre(1); var parent = unitMoveFrame;
                var nested = new UnitMoveHereEventArgs(EventHookPhase.Pre,2,13,10,0);
                ObserveUnitMoveOrder(nested);
                Check(unitMoveFrame.Placement == null, "nested Unit probe cannot allocate parent group slots");
                Post(2,0); Check(ReferenceEquals(unitMoveFrame,parent), "nested Unit return restores placement parent");
                // Simulated missing Post is rolled back at native group return.
                return 1;
            };
            ObserveCommonGroupMove(nativeTribeManager,1,13,10,0,1);
            Check(units[1].r_AttackMoveToTargetTileX == 13, "common branch finally rolls back missing Post");
            ClearUnitMoveFrames(); activeMoveCommand = null;

            int executingId = 0; nativeExecutingUnitId = &executingId;
            api.Occupants[1013] = 1;
            for (int id = 1; id <= 5; id++)
            { units[id].r_CurrentPositionTileId = 1013; units[id].r_CurrentTilePositionX = units[id].r_NextTilePositionX2 = 13; }
            int ordinaryCalls = 0; originalFreePlace = (m, id, x, y) => ordinaryCalls++;
            var idleGoals = new HashSet<int>();
            originalUnstack = (m,id) => {
                FindUnstackPlace(nativePathManager,-1,13,10);
                int tile = *(int*)(manager + 0x4C);
                if (tile == 0) return 0;
                Check(idleGoals.Add(tile) && tile != 1013, "native idle trigger assigns distinct free places");
                var args = new UnitMoveHereEventArgs(EventHookPhase.Pre,id,*(int*)(manager+0x44),*(int*)(manager+0x48),0);
                ObserveUnitMoveOrder(args);
                *moatPathMode = EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,id);
                Check(GetUnitMovePlan(unitMoveFrame,id).TargetX == args.TileX, "idle movement enters normal Unit plan pipeline");
                Post(id,1); return 1;
            };
            tick++;
            for (int id=2;id<=5;id++) { executingId=id; Check(ObserveNativeUnstack((IntPtr)nativeUnitManager,id)==1,"overlapping friendly moat unit separates"); }
            executingId=2;
            Check(ordinaryCalls==0 && unstackUnit==null,"only scoped native free search replaced, caller restored");
            api.Occupants.Clear();
            originalUnstack=(m,id)=> { FindUnstackPlace(nativePathManager,-1,13,10);return 0; };
            ObserveNativeUnstack((IntPtr)nativeUnitManager,2);
            Check(ordinaryCalls==1,"nonoverlapping native behavior preserved");
            api.Occupants[1013]=1; enemyTiles.Add(1013); placementRevision++;
            ObserveNativeUnstack((IntPtr)nativeUnitManager,2);
            Check(ordinaryCalls==2,"enemy moat never receives idle extension");
            enemyTiles.Clear(); api.Occupants.Clear();
            nativeExecutingUnitId = null;
            PlacementKernelReferenceTests();
        }

        private static void PlacementKernelReferenceTests()
        {
            var random = new Random(82157);
            const int width=5, count=25;
            for (int sample=0;sample<100;sample++)
            {
                var edge=new bool[count,count]; var available=new bool[count];
                var graph=new CursorRegionGraph(count);
                for(int a=0;a<count;a++)
                {
                    available[a]=random.Next(3)!=0;
                    for(int b=0;b<count;b++)
                        if(a!=b && Math.Abs(a%width-b%width)<=1 && Math.Abs(a/width-b/width)<=1 && random.Next(3)!=0)
                        { edge[a,b]=true;graph.ChangeEdge(a,b,1); }
                }
                int anchor=random.Next(count);
                var distance=new int[count]; Array.Fill(distance,int.MaxValue);distance[anchor]=0;
                var queue=new Queue<int>();queue.Enqueue(anchor);
                while(queue.Count!=0)
                {
                    int a=queue.Dequeue();
                    for(int b=0;b<count;b++)if((edge[a,b]||edge[b,a]) && distance[b]==int.MaxValue)
                    { distance[b]=distance[a]+1;queue.Enqueue(b); }
                }
                var search=new MoatPlacementSearch(width,width,anchor,(a,b)=>edge[a,b]);
                var state=new PlacementSearchState { Graph=graph,Revision=graph.Revision,Anchor=anchor,
                    FromAnchor=graph.StartForwardSearch(anchor),Search=search };
                var reserved=new HashSet<int>();
                for(int unit=1;unit<=30;unit++)
                {
                    int source=random.Next(count);var reachable=new bool[count];reachable[source]=true;queue.Enqueue(source);
                    while(queue.Count!=0)
                    {
                        int a=queue.Dequeue();
                        for(int b=0;b<count;b++)if(edge[a,b]&&!reachable[b]){reachable[b]=true;queue.Enqueue(b);}
                    }
                    var forward=graph.StartForwardSearch(source);
                    for(int b=count-1;b>=0;b--)
                    {
                        Check(forward.CanReach(b)==reachable[b],"incremental forward graph agrees with independent directed reference");
                        Check(state.CanReach(source,b)==reachable[b],"shared anchor proof preserves directed alternatives");
                    }
                    int best=int.MaxValue;
                    for(int b=0;b<count;b++)if(available[b]&&reachable[b]&&!reserved.Contains(b))best=Math.Min(best,distance[b]);
                    bool found=search.TryReserve(unit,source,b=>available[b],b=>graph.CanReach(source,b),out int cell);
                    Check(found==(best!=int.MaxValue),"placement existence matches independent directed reference");
                    if(found)
                    {
                        Check(reachable[cell]&&available[cell]&&distance[cell]==best&&reserved.Add(cell),
                            "closest admissible unreserved place matches reference");
                        if(unit%3==0){search.Release(unit,cell);reserved.Remove(cell);}
                    }
                }
                long expanded=search.ExpandedNodes;
                Array.Fill(available,false);
                for(int repeat=0;repeat<10;repeat++)Check(!search.TryReserve(77,0,b=>available[b],b=>true,out _),"full component reports no free place");
                long exhausted=search.ExpandedNodes;
                Check(exhausted<=count,"placement component expands each node once");
                Check(!search.TryReserve(77,0,b=>false,b=>true,out _)&&search.ExpandedNodes==exhausted,
                    "repeated failed placement does not expand graph again");
                graph.ChangeEdge(0,1,1);
                Check(!state.Matches(graph) && !state.FromAnchor.CanReach(anchor),"topology revision invalidates partial and completed placement proofs");
            }
            Console.WriteLine("PASS: placement kernel vs independent directed reachability and nearest-place reference (100 maps).");
        }
    }
}
