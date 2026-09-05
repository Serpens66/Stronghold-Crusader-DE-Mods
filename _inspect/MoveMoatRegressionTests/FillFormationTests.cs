using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private void FillFormationTests()
        {
            var api = GameTileManagerAPI.Instance;
            var units = GameUnitManagerAPI.Instance.Units;
            var manager = (byte*)nativePathManager;
            byte* records = (byte*)NativeMemory.AllocZeroed((nuint)(MoatRecordCountOffset + 16));
            IntPtr oldTiles = api.TileManager, oldTribes = nativeTribeManager;
            byte* tribes = (byte*)NativeMemory.AllocZeroed(2 * TribeRecordSize);
            try
            {
                captureWeighted = false; activeMoveCommand = null; activePlan = pendingPlan = null;
                ClearUnitMoveFrames(); activeAttackCommand = null; activeMoatWorkSelection = null;
                activeAttackApproachDiagnostic = null; placementBatch = null; api.Occupants.Clear();
                enemyTiles.Clear(); enemyPlayerId = 0; api.Rows[11 * 3] = NativeTileCount;
                for (int x = 10; x <= 220; x++)
                {
                    tileFlags[1000+x] = 0x8000; nativeMovementMasks[1000+x] = 0x44;
                    nativeHeightLayer[1000+x] = 0; nativeBuildingLayer[1000+x] = 0;
                    pathRegionGrid[1000+x] = 1; movementTargetAvailability[10*800+x] = 1;
                }
                tileFlags[1013] = CompletedMoatTileFlag;
                tileFlags[1016] = CompletedMoatTileFlag; enemyTiles.Add(1016);
                units[1] = new GameUnit { Digger=true,r_GlobalId=551,r_ControllableForPlayerId=1,
                    r_CurrentTilePositionX=10,r_CurrentTilePositionY=10,r_NextTilePositionX2=10,r_NextTilePositionY2=10,
                    r_CurrentPositionTileId=1010,r_MovingRelevant=8,r_AI_LastIssuedTribeCommand=7 };
                Check(WeightedMovementCostProfile.TryCreate(1,1,0,0,0,0,false,out var comparisonProfile,out _), "comparison profile");
                byte* oneEdge = stackalloc byte[1]; oneEdge[0] = 2;
                nativeMovementMasks[1010] = 0;
                Check(!weightedMoatRoutePlanner.TryDescribeEncodedPath(1,10,10,11,10,comparisonProfile,oneEdge,1,false,out _),
                    "strict publication rejects missing ground direction");
                Check(weightedMoatRoutePlanner.TryDescribeEncodedPath(1,10,10,11,10,comparisonProfile,oneEdge,1,false,out var priced,-1,true) && priced.GroundEdges==1,
                    "native comparison can price calibrated ground without granting traversal");
                Check(!weightedMoatRoutePlanner.TryDescribeEncodedPath(1,10,10,12,10,comparisonProfile,oneEdge,1,false,out _,-1,true),
                    "comparison requires exact endpoint");
                oneEdge[0]=15;
                Check(!weightedMoatRoutePlanner.TryDescribeEncodedPath(1,10,10,11,10,comparisonProfile,oneEdge,1,false,out _,-1,true), "comparison rejects invalid nibble");
                oneEdge[0]=2; tileFlags[1011]=CompletedMoatTileFlag; enemyTiles.Add(1011);
                Check(!weightedMoatRoutePlanner.TryDescribeEncodedPath(1,10,10,11,10,comparisonProfile,oneEdge,1,false,out _,-1,true), "comparison rejects unbound enemy endpoint");
                enemyTiles.Remove(1011); tileFlags[1011]=0x100;
                Check(!weightedMoatRoutePlanner.TryDescribeEncodedPath(1,10,10,11,10,comparisonProfile,oneEdge,1,false,out _,-1,true), "comparison rejects uncalibrated structure");
                tileFlags[1011]=0x8000; nativeMovementMasks[1010]=0x44;
                int* attackRecords=stackalloc int[1503];
                foreach(int size in new[]{1,20,120,500})
                {
                    for(int i=0;i<size;i++){attackRecords[i*3]=i+1;attackRecords[i*3+1]=i%2;attackRecords[i*3+2]=1000+i;}
                    attackRecords[size*3]=123456;
                    int removed=WeightedMoatRoutePlanner.FilterNativeAttackCandidates(attackRecords,size,t=>t%3==0);
                    Check(removed==size/3,"attack pool rejection count");
                    int w=0;
                    for(int i=0;i<size;i++)if((i+1)%3!=0)
                    {Check(attackRecords[w*3]==i+1 && attackRecords[w*3+1]==i%2 && attackRecords[w*3+2]==1000+i,"native order flag and score retained");w++;}
                    if(removed>0)Check(attackRecords[w*3]==0 && attackRecords[w*3+1]==0,"attack sentinel");
                    Check(attackRecords[size*3]==123456,"attack pool bounds");
                }
                attackRecords[0]=1;attackRecords[1]=1;attackRecords[2]=17;
                attackRecords[3]=2;attackRecords[4]=1;attackRecords[5]=18;
                bool filterThrew=false;
                try{WeightedMoatRoutePlanner.FilterNativeAttackCandidates(attackRecords,2,t=>t==1?true:throw new InvalidOperationException());}
                catch(InvalidOperationException){filterThrew=true;}
                Check(filterThrew && attackRecords[0]==1 && attackRecords[3]==2,"filter exception does not partially mutate output");
                api.TileManager = (IntPtr)records;
                *(int*)(records + MoatRecordCountOffset) = 951;
                byte* record = records + MoatRecordArrayOffset + 950 * MoatRecordSize;
                *(int*)record=1016; *(short*)(record+4)=16; *(short*)(record+6)=10;
                getMoatIdAtTile=(m,t)=>t==1016?950:0;
                originalHasFillMoatApproach=(m,s,t,y)=>1;
                api.Occupants[1015]=2; // force the same native far-side approach
                int selections=0, reservations=0, mode1=0, mode2=0;
                originalFindMoatWorkTarget=(m,p,u,r)=> {
                    selections++;
                    if(AllowFillMoatApproachThroughFriendlyMoat(m,1,1016,10)==0)return -1;
                    reservations++; return 950;
                };
                originalResolveMoatWorkTile=(m,id,mode,sx,sy)=> {
                    if(mode==1)mode1++;else mode2++;
                    *(int*)((byte*)m+SelectedMoatTileIdOffset)=1016;
                    *(int*)((byte*)m+SelectedMoatApproachXOffset)=mode==1?16:17;
                    *(int*)((byte*)m+SelectedMoatApproachYOffset)=10;
                    return mode==1?1016:1017;
                };
                int NativeDetourPath(IntPtr m)
                {
                    byte* path=*(byte**)((byte*)m+PathManagerOutputBufferOffset);
                    for(int i=0;i<9;i++)path[i]=0;
                    // Five east, five west, then seven east: exact work contact at node 16.
                    for(int i=0;i<17;i++)path[i>>1]|=(byte)((i>=5 && i<10?6:2)<<((i&1)*4));
                    *(int*)((byte*)m+PathManagerOutputLengthOffset)=17; return 17;
                }
                originalPathBuilder=(m,c,p)=>NativeDetourPath(m);
                originalPathReconstruction=NativeDetourPath;
                originalUnitStandingOnCompletedMoat=(m,id)=>0;
                originalRegionPairReachability=null;
                captureWeighted=true;
                foreach(bool reconstruction in new[]{false,true})
                {
                    tick++; cacheMapEpoch=-1; placementRevision++;
                    Check(FindMoatWorkTargetWithOwnerRoute((IntPtr)records,1,1,2)==950,"real fill selector chooses one work target");
                    Check(ResolveMoatWorkTileWithOwnerRoute((IntPtr)records,950,1,10,10)==1016 && pendingFillMoatApproach!=null,
                        "mode one preserves actual selection handoff");
                    Check(ResolveMoatWorkTileWithOwnerRoute((IntPtr)records,950,2,10,10)==1017 && pendingPlan.MoatWorkTargetTileId==1016,
                        "mode two binds actual work contact and approach");
                    var pre=new UnitMoveHereEventArgs(EventHookPhase.Pre,1,17,10,0);ObserveUnitMoveOrder(pre);
                    *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,1);
                    *(int*)(manager+8)=10;*(int*)(manager+12)=10;*(int*)(manager+16)=17;*(int*)(manager+20)=10;
                    *(int*)(manager+PathManagerRouteVariantOffset)=0;
                    byte* buffer=nativeUnitManager+NativeUnitPathBufferOffset+NativeUnitPathBufferStride;
                    *(byte**)(manager+PathManagerOutputBufferOffset)=buffer;
                    *(int*)(manager+PathManagerOutputLengthOffset)=0;
                    int result=reconstruction?BuildReconstructedUnitPath(nativePathManager):BuildPathWithCompletedMoatRouteVariant(nativePathManager,1,1);
                    Check(result==7 && *(int*)(manager+PathManagerOutputLengthOffset)==7,
                        "full native fill chain optimizes positive terminal path through both builders");
                    Check(TryAuditFallbackPath(nativePathManager,buffer,result,unitMoveFrame.Plan,units+1,out _),"published fill retains exact owner audit");
                    var shadow=TryCaptureBuilderWeightedScope(nativePathManager);
                    Check(shadow!=null && shadow.FillPlan!=null,"weighted capture binds the actual work plan");
                    Check(DescribeWeightedRoute(shadow,buffer,result,out var costs) && costs.RouteLength==7 && costs.MoatEdges==4,
                        "cost description counts the entire prefix and both contact edges");
                    byte savedByte=buffer[0];
                    Check(!TryPublishSafelyFasterWeightedRoute(nativePathManager,buffer,7,shadow,costs,out _,out _,out _,out _) &&
                        buffer[0]==savedByte && *(int*)(manager+PathManagerOutputLengthOffset)==7,
                        "already shortest contact route keeps bytes and length when forty ticks cannot be saved");
                    Check(!weightedMoatRoutePlanner.TryDescribeEncodedPath(1,10,10,17,10,shadow.CostProfile,buffer,7,false,out _),
                        "ordinary decoder still rejects enemy contact");
                    uint global=units[1].r_GlobalId; units[1].r_GlobalId++;
                    Check(!DescribeWeightedRoute(shadow,buffer,7,out _),"work contact fails on ID reuse"); units[1].r_GlobalId=global;
                    enemyTiles.Add(1013);
                    Check(!DescribeWeightedRoute(shadow,buffer,7,out _),"second enemy contact on the prefix remains forbidden");enemyTiles.Remove(1013);
                    shadow.TargetX=18;
                    Check(!DescribeWeightedRoute(shadow,buffer,7,out _),"changed builder endpoint cannot borrow work contact");shadow.TargetX=17;
                    // The unchanged two-edge suffix is still valid with an empty prefix.
                    units[1].r_CurrentTilePositionX=units[1].r_NextTilePositionX2=15;shadow.StartX=15;
                    byte* detour=stackalloc byte[6];for(int i=0;i<6;i++)detour[i]=0;
                    for(int i=0;i<12;i++)detour[i>>1]|=(byte)((i<5?6:2)<<((i&1)*4));
                    var generous=new[]{new MoatSearchLimit(shadow.CostProfile.GetEdgeFixedCost(false),shadow.CostProfile.GetEdgeFixedCost(true),long.MaxValue/4)};
                    Check(TryImproveFillPrefix(shadow,detour,12,generous,out var emptyCosts,out var emptyRoute) && emptyRoute.DirectionCount==2 && emptyCosts.MoatEdges==2,
                        "empty friendly prefix retains both counted terminal edges");
                    units[1].r_CurrentTilePositionX=units[1].r_NextTilePositionX2=10;shadow.StartX=10;
                    if(!reconstruction)
                    {
                        // Add a fully friendly, cheaper route around the contact field.
                        api.Rows[11*3]=2000;nativeMovementMasks[1015]|=0x10;
                        for(int x=15;x<=17;x++){tileFlags[2000+x]=0x8000;nativeMovementMasks[2000+x]=0x45;nativeHeightLayer[2000+x]=0;}
                        InvalidateMovementSearchData();
                        NativeDetourPath(nativePathManager);
                        Check(DescribeWeightedRoute(shadow,buffer,17,out var longCosts),"native incumbent valid with friendly alternative");
                        bool directPublished=TryPublishSafelyFasterWeightedRoute(nativePathManager,buffer,17,shadow,longCosts,out var directCosts,out _,out _,out string whyDirect);
                        Check(directPublished &&
                            directCosts.MoatEdges==2 && directCosts.RouteLength==9,
                            $"cheaper fully friendly alternative beats preserved terminal suffix under all profiles: {whyDirect} length={directCosts.RouteLength} moat={directCosts.MoatEdges}");
                        api.Rows[11*3]=NativeTileCount;nativeMovementMasks[1015]=0x44;
                        for(int x=15;x<=17;x++)tileFlags[2000+x]=0x100;
                        InvalidateMovementSearchData();
                    }
                    shadow.FillPlan.MoatWorkTargetTileId=1013;
                    Check(!DescribeWeightedRoute(shadow,buffer,7,out _),"unrelated work tile never authorizes enemy contact");
                    ObserveUnitMoveOrder(new UnitMoveHereEventArgs(EventHookPhase.Post,1,17,10,0){ReturnValue=result});
                }
                Check(selections==2 && reservations==2 && mode1==2 && mode2==2,"one selection and reservation per complete later work cycle");
                // Real UnitPre -> mode -> both builders -> publication -> UnitPost.
                pendingPlan=activePlan=null;ClearUnitMoveFrames();enemyTiles.Clear();api.Occupants.Clear();
                tileFlags[1016]=0x8000;tileFlags[1055]=CompletedMoatTileFlag;
                for(int x=50;x<=130;x++)pathRegionGrid[1000+x]=(short)(x<55?1:2);
                originalPathBuilder=(m,c,p)=>0;originalPathReconstruction=m=>0;
                captureWeighted=true;
                foreach(int count in new[]{1,120,680})
                {
                    tick++;InvalidateMovementSearchData();
                    activeMoveCommand=new MoveCommandScope{TribeId=1,TargetX=90,TargetY=10};
                    long beginNodes=weightedMoatRoutePlanner.SearchNodes,allocated=GC.GetAllocatedBytesForCurrentThread();
                    var timer=Stopwatch.StartNew();
                    for(int id=1;id<=count;id++)
                    {
                        int target=60+id%60;
                        units[id]=new GameUnit{Digger=true,r_GlobalId=(uint)(8000+id),r_ControllableForPlayerId=1,r_TribeId=1,
                            r_CurrentTilePositionX=50,r_CurrentTilePositionY=10,r_NextTilePositionX2=50,r_NextTilePositionY2=10,r_MovingRelevant=8};
                        ObserveUnitMoveOrder(new UnitMoveHereEventArgs(EventHookPhase.Pre,id,target,10,0));
                        *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,id);
                        var bound=GetCurrentUnitMoveFrame().Plan;
                        Check(GetReusableQualifiedRoute(bound,units+id)!=null,"mode retains exact encoded qualification");
                        long searches=weightedMoatRoutePlanner.SearchRuns;
                        *(int*)(manager+8)=50;*(int*)(manager+12)=10;*(int*)(manager+16)=target;*(int*)(manager+20)=10;
                        *(byte**)(manager+PathManagerOutputBufferOffset)=nativeUnitManager+NativeUnitPathBufferOffset+id*NativeUnitPathBufferStride;
                        *(int*)(manager+PathManagerOutputLengthOffset)=0;*(int*)(manager+PathManagerRouteVariantOffset)=0;
                        int result=id%2==0?BuildReconstructedUnitPath(nativePathManager):BuildPathWithCompletedMoatRouteVariant(nativePathManager,1,0);
                        Check(result==target-50,"qualified path published to individual unit");
                        Check(weightedMoatRoutePlanner.SearchRuns==searches,"builder and optimality check do not repeat qualification search");
                        long savedRevision=placementRevision;placementRevision++;
                        Check(GetReusableQualifiedRoute(bound,units+id)==null,"revision invalidates retained path");placementRevision=savedRevision;
                        units[id].r_GlobalId++;
                        Check(GetReusableQualifiedRoute(bound,units+id)==null,"reused game ID does not reuse a bound plan");units[id].r_GlobalId--;
                        units[id].r_CurrentTilePositionX++;
                        Check(GetReusableQualifiedRoute(bound,units+id)==null,"changed actual start rejects retained path");units[id].r_CurrentTilePositionX--;
                        tick++;
                        Check(GetReusableQualifiedRoute(bound,units+id)==null,"later tick rejects retained path");tick--;
                        ObserveUnitMoveOrder(new UnitMoveHereEventArgs(EventHookPhase.Post,id,target,10,0){ReturnValue=result});
                    }
                    Console.WriteLine($"GROUP PRODUCTION units={count} ms={timer.Elapsed.TotalMilliseconds:F3} nodes={weightedMoatRoutePlanner.SearchNodes-beginNodes} bytes={GC.GetAllocatedBytesForCurrentThread()-allocated}");
                }
                tileFlags[1055]=0x8000;
                captureWeighted=false; activePlan=pendingPlan=null; ClearUnitMoveFrames(); api.Occupants.Clear();
                tileFlags[1016]=0x8000; enemyTiles.Clear(); nativeTribeManager=(IntPtr)tribes;
                *(int*)(tribes+TribeRecordSize+0x2C)=1;
                for(int x=20;x<=44;x++){tileFlags[1000+x]=CompletedMoatTileFlag;enemyTiles.Add(1000+x);}
                int calls=0, available=220;
                originalFormationSlot=(m,spacing,x,y)=> {
                    calls++; int at=*(int*)(tribes+0x14);
                    if(at<0 || at>=available)at=0;
                    *(int*)(tribes+0x14)=at;
                    *(int*)(tribes+0x0C)=at==0?60:at<=25?19+at:35+at;
                    *(int*)(tribes+0x10)=10;
                };
                foreach(int count in new[]{1,20,120,128,156})
                {
                    tick++; calls=0; activeMoveCommand=new MoveCommandScope{TribeId=1,TargetX=60,TargetY=10};
                    *(int*)(tribes+0x14)=0;
                    long before=weightedMoatRoutePlanner.SearchRuns;
                    long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
                    for(int id=1;id<=count;id++)
                    {
                        ChooseOwnerSafeFormationSlot(nativePathManager,1,60,10);
                        int target=*(int*)(tribes+0x0C);
                        Check(target>=60,"native allocator skips every enemy formation slot");
                        units[id].r_AttackMoveToTargetTileX=(ushort)target;units[id].r_AttackMoveToTargetTileY=10;
                        *(int*)(tribes+0x14)+=1;
                    }
                    Console.WriteLine($"FORMATION units={count} nativeCalls={calls} ms={watch.Elapsed.TotalMilliseconds:F3} bytes={GC.GetAllocatedBytesForCurrentThread()-allocated}");
                    Check(calls==count+(count>1?25:0),"enemy candidates scanned only once per native list");
                    Check(weightedMoatRoutePlanner.SearchRuns==before,"native slot filtering never searches a path");
                    originalPathBuilder=(m,c,p)=> {
                        byte* state=(byte*)m;int length=*(int*)(state+16)-*(int*)(state+8);
                        byte* buffer=*(byte**)(state+PathManagerOutputBufferOffset);
                        for(int i=0;i<(length+1)/2;i++)buffer[i]=0x22;
                        *(int*)(state+PathManagerOutputLengthOffset)=length;return length;
                    };
                    for(int id=1;id<=count;id++)
                    {
                        int target=units[id].r_AttackMoveToTargetTileX;
                        units[id]=new GameUnit { Digger=true,r_GlobalId=(uint)(700+id),r_TribeId=1,r_ControllableForPlayerId=1,
                            r_CurrentTilePositionX=50,r_CurrentTilePositionY=10,r_NextTilePositionX2=50,r_NextTilePositionY2=10,
                            r_CurrentPositionTileId=1050,r_MovingRelevant=8,r_AttackMoveToTargetTileX=(ushort)target,r_AttackMoveToTargetTileY=10 };
                        ObserveUnitMoveOrder(new UnitMoveHereEventArgs(EventHookPhase.Pre,id,target,10,0));
                        *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,id);
                        TryAllowUnitMoveRegion(nativePathManager,1,1,50,10,1,out _);
                        *(int*)(manager+8)=50;*(int*)(manager+12)=10;*(int*)(manager+16)=target;*(int*)(manager+20)=10;
                        *(byte**)(manager+PathManagerOutputBufferOffset)=nativeUnitManager+NativeUnitPathBufferOffset+id*NativeUnitPathBufferStride;
                        *(int*)(manager+PathManagerOutputLengthOffset)=0;
                        int length=BuildPathWithCompletedMoatRouteVariant(nativePathManager,1,1);
                        Check(length==target-50,"native replacement target reaches individual own builder");
                        ObserveUnitMoveOrder(new UnitMoveHereEventArgs(EventHookPhase.Post,id,target,10,0){ReturnValue=length});
                    }
                    Check(activeMoveCommand.UnitMovePositive==count && activeMoveCommand.UnitMoveWithoutBuilder==0,
                        "every native formation member completes its actual Unit/region/builder chain");
                }
                available=26;tick++;activeMoveCommand=new MoveCommandScope{TribeId=1,TargetX=60,TargetY=10};
                *(int*)(tribes+0x14)=1;calls=0;
                ChooseOwnerSafeFormationSlot(nativePathManager,1,60,10);
                Check(*(int*)(tribes+0x0C)==60,"native list reset falls back to valid common click");
                int exhaustedCalls=calls;
                for(int i=0;i<120;i++){*(int*)(tribes+0x14)=1;ChooseOwnerSafeFormationSlot(nativePathManager,1,60,10);}
                Check(calls<=exhaustedCalls+1,"exhausted unchanged candidate list is not scanned per unit");
                tileFlags[1060]|=CursorSpecialStructureTileFlagMask;
                ChooseOwnerSafeFormationSlot(nativePathManager,1,60,10);
                Check(*(int*)(tribes+0x0C)==60,"native common fallback retains structure target for individual portal validation");
                tileFlags[1060]=0x8000;
                int oldCalls=calls;*(int*)(tribes+0x14)=4000;
                ChooseOwnerSafeFormationSlot(nativePathManager,1,60,10);
                Check(calls==oldCalls && *(int*)(tribes+0x14)==0,"native caller limit cannot abort remaining group members");
                activeMoatWorkSelection=new MoatWorkSelectionScope(mapEpoch,api.TileManager,1,1,2,10,10,1010,1);
                *(int*)(tribes+0x14)=1;
                ChooseOwnerSafeFormationSlot(nativePathManager,1,60,10);
                Check(*(int*)(tribes+0x0C)==20,"nested work keeps native formation contract");activeMoatWorkSelection=null;
                tick++; *(int*)(tribes+0x14)=1; injectOwnerFailure=true;
                ChooseOwnerSafeFormationSlot(nativePathManager,1,60,10);injectOwnerFailure=false;
                Check(*(int*)(tribes+0x14)==1 && *(int*)(tribes+0x0C)==20,"lookup failure restores selector index before native fallback");
                placementRevision++; *(int*)(tribes+0x14)=1; enemyTiles.Remove(1020);
                ChooseOwnerSafeFormationSlot(nativePathManager,1,60,10);
                Check(*(int*)(tribes+0x0C)==20,"owner revision invalidates previous exhausted candidate result");
            }
            finally
            {
                captureWeighted=false;api.TileManager=oldTiles;nativeTribeManager=oldTribes;
                activeMoveCommand=null;activePlan=pendingPlan=null;ClearUnitMoveFrames();api.Occupants.Clear();
                enemyTiles.Clear(); NativeMemory.Free(records);NativeMemory.Free(tribes);
            }
        }
    }
}

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private sealed class AttackCommandScope
        {
            internal GroupRouteSession Routes = new GroupRouteSession(MoveMoatTestPlugin.Settings.EnableMod, MoveMoatTestPlugin.Settings.RouteMode == 1);
            internal System.Collections.Generic.HashSet<int> CandidateUnitIds = new System.Collections.Generic.HashSet<int>();
            public int MapEpoch, TribeId, TargetValue1, TargetValue2, Sequence;
            public TribeAICommand Command;
            public long NativeBuilderTicks, AuditTicks, WeightedPhaseTicks, WeightedAuditTicks, FastShadowTicks;
            public int NativeBuilderCalls, AuditCalls, FastShadowEligible, FastShadowValidated, FastShadowNativeEqual;
            public System.Collections.Generic.Dictionary<string,int> FastShadowRejections = new System.Collections.Generic.Dictionary<string,int>();
            public System.Collections.Generic.Dictionary<string,int> FastShadowStates = new System.Collections.Generic.Dictionary<string,int>();
            public System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<int>> PublishedBuildingApproaches = new System.Collections.Generic.Dictionary<int,System.Collections.Generic.HashSet<int>>();
        }
        private sealed class AttackApproachDiagnosticScope
        { public AttackCommandScope OwnerCommand; public int TribeId; }
        private static bool IsBuildingAttackCommand(TribeAICommand command) => command == TribeAICommand.AttackBuilding;
        private static bool IsWallStairOrRampStructure(int type) => type==99;
        private bool MatchesAttackTargetContext(GameUnit* unit,TribeAICommand command,int id,int global) => false;

        private void BuildingCandidateTests()
        {
            Check(IsValidAttackSourceRegionContext(0,true,0,true),"friendly completed moat permits AttackUnit source region zero");
            Check(!IsValidAttackSourceRegionContext(0,true,0,false),"ordinary or hostile source region zero fails closed");
            Check(!IsValidAttackSourceRegionContext(0,false,0,true),"invalid source tile fails closed");
            Check(IsValidAttackSourceRegionContext(7,true,7,false),"ordinary positive source region remains valid");
            Check(!IsValidAttackSourceRegionContext(7,true,8,false),"positive source region must match concrete tile");
            var api=GameTileManagerAPI.Instance; var units=GameUnitManagerAPI.Instance.Units;
            var oldTribes=nativeTribeManager; var oldBuilding=GameBuildingManagerAPI.Instance.Building;
            byte* tribes=(byte*)NativeMemory.AllocZeroed(TribeRecordSize*2);
            GameBuilding building=new GameBuilding { r_GlobalId=900,r_AliveState=AliveState.IsAlive,r_PlayerIdOwner=2 };
            try
            {
                nativeTribeManager=(IntPtr)tribes; GameBuildingManagerAPI.Instance.Building=&building; api.Buildings=nativeBuildingLayer;
                ClearUnitMoveFrames(); activePlan=pendingPlan=null;activeMoveCommand=null;activeMoatWorkSelection=null;
                enemyTiles.Clear(); api.Occupants.Clear(); api.Rows[11*3]=2000;
                for(int x=50;x<430;x++)
                {
                    tileFlags[1000+x]=0x8000;nativeMovementMasks[1000+x]=0x44;nativeHeightLayer[1000+x]=0;
                    nativeBuildingLayer[1000+x]=0;pathRegionGrid[1000+x]=(short)(x<55?1:2);
                    movementTargetAvailability[8000+x]=1;
                    tileFlags[2000+x]=0;nativeMovementMasks[2000+x]=0;nativeBuildingLayer[2000+x]=1;
                }
                var pool=new System.Collections.Generic.List<BuildingApproachCandidate>();
                for(int i=0;i<351;i++)pool.Add(new BuildingApproachCandidate(1060+i,i<12?2060+i:0,10000000));
                var command=new AttackCommandScope { MapEpoch=mapEpoch,TribeId=1,TargetValue1=1,TargetValue2=900,Sequence=1,Command=TribeAICommand.AttackBuilding };
                var scope=new AttackApproachDiagnosticScope{OwnerCommand=command,TribeId=1};
                getGroupUnitId=(m,t,n)=>n+1;
                *(short*)(tribes+TribeRecordSize+TribeLeadUnitIdOffset)=1;
                foreach(int count in new[]{1,120,680})
                {
                    *(short*)(tribes+TribeRecordSize+TribeUnitCountOffset)=(short)count;
                    for(int id=1;id<=count;id++)units[id]=new GameUnit{Digger=true,r_GlobalId=(uint)(9000+id),r_TribeId=1,r_ControllableForPlayerId=1,
                        r_AliveState=AliveState.IsAlive,r_CurrentTilePositionX=50,r_CurrentTilePositionY=10,r_NextTilePositionX2=50,r_NextTilePositionY2=10,r_MovingRelevant=8};
                    foreach(bool moat in new[]{false,true})
                    {
                        tileFlags[1055]=moat?CompletedMoatTileFlag:0x8000; tick++;InvalidateMovementSearchData();
                        WriteBuildingApproachCandidates(nativePathManager,pool);
                        var before=CaptureBuildingApproachCandidates(nativePathManager);
                        if(moat)WriteBuildingApproachCandidates(nativePathManager,new System.Collections.Generic.List<BuildingApproachCandidate>());
                        else { var native=new System.Collections.Generic.List<BuildingApproachCandidate>();foreach(var c0 in pool){var c=c0;c.Score=c.ApproachTileId-1050+1;native.Add(c);}WriteBuildingApproachCandidates(nativePathManager,native); }
                        activeAttackCommand=command;activeBuildingConsumerPerformance=new BuildingConsumerPerformanceScope(1,1,351);
                        long runs=weightedMoatRoutePlanner.SearchRuns,bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
                        var result=TryApplyBuildingConsumerFallback(scope,nativeTribeManager,before,default);
                        timer.Stop();
                        Check(weightedMoatRoutePlanner.SearchRuns==runs,"candidate evaluation never performs individual encoded/weighted searches");
                        var after=CaptureBuildingApproachCandidates(nativePathManager);
                        Check(after.Length==351,"351 native building places survive including staging");
                        for(int i=0;i<351;i++)Check(after[i].ApproachTileId==1060+i && after[i].FootprintTileId==(i<12?2060+i:0),"native paired prefix and staging order retained");
                        Check(activeBuildingConsumerPerformance.ReachabilityMapsBuilt==(moat?1:0),"one shared field or unchanged native list");
                        PublishBuildingApproachPairs(command,nativePathManager);
                        Console.WriteLine($"BUILDING PRODUCTION units={count} moat={moat} candidates={after.Length} ms={timer.Elapsed.TotalMilliseconds:F3} nodes={activeBuildingConsumerPerformance.SearchNodes} bytes={GC.GetAllocatedBytesForCurrentThread()-bytes}");
                        if(moat)
                        {
                            captureWeighted=true; originalPathBuilder=(m,c,p)=>0;originalPathReconstruction=m=>0;
                            int calls=Math.Min(count,after.Length);
                            for(int id=1;id<=calls;id++)
                            {
                                int x=after[id-1].ApproachTileId-1000;
                                units[id].r_AttackMoveToTargetTileX=(ushort)x;units[id].r_AttackMoveToTargetTileY=10;
                                Check(MatchesSynchronousAttackMovementContext(units+id,command,out _),"published staging target is a bound movement context");
                                ObserveUnitMoveOrder(new UnitMoveHereEventArgs(EventHookPhase.Pre,id,x,10,0));
                                *moatPathMode=EnableCompletedMoatModeForScopedMovement((IntPtr)nativeUnitManager,id);
                                Check(ReferenceEquals(nativeGroundOwner,command),"attack search owner survives successive unit frames");
                                byte* m=(byte*)nativePathManager;*(int*)(m+8)=50;*(int*)(m+12)=10;*(int*)(m+16)=x;*(int*)(m+20)=10;
                                *(byte**)(m+PathManagerOutputBufferOffset)=nativeUnitManager+NativeUnitPathBufferOffset+id*NativeUnitPathBufferStride;
                                *(int*)(m+PathManagerOutputLengthOffset)=0;
                                int path=id%2==0?BuildReconstructedUnitPath(nativePathManager):BuildPathWithCompletedMoatRouteVariant(nativePathManager,1,0);
                                Check(path==x-50,"native assigned building/staging place reaches own audited builder");
                                ObserveUnitMoveOrder(new UnitMoveHereEventArgs(EventHookPhase.Post,id,x,10,0){ReturnValue=path});
                            }
                        }
                    }
                }
                // The leader cannot cross this enemy barrier. Other suitable starts can
                // still supply staging positions; their proof never becomes a unit path.
                tileFlags[1055]=CompletedMoatTileFlag;enemyTiles.Add(1055);
                units[2].r_CurrentTilePositionX=80;units[2].r_NextTilePositionX2=80;
                *(short*)(tribes+TribeRecordSize+TribeUnitCountOffset)=2;
                WriteBuildingApproachCandidates(nativePathManager,new System.Collections.Generic.List<BuildingApproachCandidate>());
                activeBuildingConsumerPerformance=new BuildingConsumerPerformanceScope(1,1,351);
                TryApplyBuildingConsumerFallback(scope,nativeTribeManager,pool.ToArray(),default);
                Check(CaptureBuildingApproachCandidates(nativePathManager).Length==351 && activeBuildingConsumerPerformance.ReachabilityMapsBuilt==2,
                    "supplemental multi-source field supplies disconnected group component");
                units[2].Digger=false;
                WriteBuildingApproachCandidates(nativePathManager,new System.Collections.Generic.List<BuildingApproachCandidate>());
                TryApplyBuildingConsumerFallback(scope,nativeTribeManager,pool.ToArray(),default);
                Check(CaptureBuildingApproachCandidates(nativePathManager).Length==0,"unsuitable member cannot provide friendly moat permission");
                enemyTiles.Remove(1055);units[2].Digger=true;
                WriteBuildingApproachCandidates(nativePathManager,pool);
                var snapshot=CaptureBuildingApproachBuffer(nativePathManager);
                injectOwnerFailure=true;bool threw=false;
                try{TryApplyBuildingConsumerFallback(scope,nativeTribeManager,pool.ToArray(),default);}catch{threw=true;}finally{injectOwnerFailure=false;}
                Check(threw,"injected ownership error reaches atomic publication boundary");
                var unchanged=CaptureBuildingApproachBuffer(nativePathManager);
                for(int i=0;i<snapshot.Length;i++)Check(unchanged[i].ApproachTileId==snapshot[i].ApproachTileId && unchanged[i].FootprintTileId==snapshot[i].FootprintTileId && unchanged[i].Score==snapshot[i].Score,"failed selection leaves entire native candidate buffer unchanged");
                PublishBuildingApproachPairs(command,nativePathManager);
                units[1].r_AttackMoveToTargetTileX=100;units[1].r_AttackMoveToTargetTileY=10;
                building.r_GlobalId++;
                Check(!MatchesSynchronousAttackMovementContext(units+1,command,out _),"building ID reuse invalidates staging context");building.r_GlobalId--;
                tileFlags[1100]=CompletedMoatTileFlag;enemyTiles.Add(1100);
                Check(!MatchesSynchronousAttackMovementContext(units+1,command,out _),"enemy staging target invalidates context");
                // Native compaction writes only the first int of the terminator; stale companion is not a record.
                byte* end=(byte*)nativePathManager+PathManagerFloodResultTileOffset;
                *(int*)end=0;*(int*)(end+4)=999;
                Check(CaptureBuildingApproachCandidates(nativePathManager).Length==0,"native first-word terminator ignores stale companion");
            }
            finally
            {
                nativeTribeManager=oldTribes;GameBuildingManagerAPI.Instance.Building=oldBuilding;NativeMemory.Free(tribes);
                activeAttackCommand=null;activeBuildingConsumerPerformance=null;captureWeighted=false;
                activePlan=pendingPlan=null;ClearUnitMoveFrames();enemyTiles.Clear();
            }
        }
    }
}
