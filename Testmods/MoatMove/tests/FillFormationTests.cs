using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
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
                    r_CurrentPositionTileId=1010,r_MovementSubstep=8,r_AI_LastIssuedTribeCommand=7 };
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
                // The weighted fill and group qualification chain of MoatMove's former exact mode was removed with it
                // (2026-10-30): with MoatMove, BugfixesAndQoL skips weighted publication in the required-only flow.
                captureWeighted=false; activePlan=pendingPlan=null; ClearUnitMoveFrames(); api.Occupants.Clear();
                tileFlags[1016]=0x8000; enemyTiles.Clear(); nativeTribeManager=(IntPtr)tribes;
                *(int*)(tribes+TribeRecordSize+0x2C)=1;
                for(int x=20;x<=44;x++){tileFlags[1000+x]=CompletedMoatTileFlag;enemyTiles.Add(1000+x);}
                int calls=0, available=220, lastSpacing=0;
                originalFormationSlot=(m,spacing,x,y)=> {
                    calls++; lastSpacing=spacing; int at=*(int*)(tribes+0x14);
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
                    Check(lastSpacing==1,
                        "Vanilla safety spacing one remains unchanged during a direct Move");
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
                            r_CurrentPositionTileId=1050,r_MovementSubstep=8,r_AttackMoveToTargetTileX=(ushort)target,r_AttackMoveToTargetTileY=10 };
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
                formationOwner=null;formationExhausted=false;*(int*)(tribes+0x14)=1;
                ChooseOwnerSafeFormationSlot(nativePathManager,3,60,10);
                Check(lastSpacing==3,"command spacing preserves native unit-type spacing three");
                ChooseOwnerSafeFormationSlot(nativePathManager,1,60,10);
                Check(lastSpacing==1,"Move spacing setting never replaces Vanilla safety spacing one");
                activeMoveCommand.IsPatrolPath=true;*(int*)(tribes+0x14)=1;
                ChooseOwnerSafeFormationSlot(nativePathManager,2,60,10);
                Check(lastSpacing==2,"patrol movement retains its Vanilla spacing");
                activeMoveCommand.IsPatrolPath=false;TestSettings.Settings.EnableMod=false;
                *(int*)(tribes+0x14)=1;
                ChooseOwnerSafeFormationSlot(nativePathManager,3,60,10);
                Check(lastSpacing==3,"disabled Move formation feature retains all Vanilla spacing");
                TestSettings.Settings.EnableMod=true;
                int assassinSpacing=0;
                originalAssassinGroundFormationSlot=(m,spacing,x,y)=>{assassinSpacing=spacing;return spacing;};
                activeMoveCommand.IsPatrolPath=false;
                foreach(int configured in new[]{1,2,3,4})
                {
                    Check(ChooseAssassinGroundFormationSlot(nativePathManager,configured,60,10)==configured &&
                        assassinSpacing==configured,
                        "Assassin unit-type spacing remains authoritative for native spacing "+configured);
                }
                Check(ChooseAssassinGroundFormationSlot(nativePathManager,1,60,10)==1,
                    "Assassin safety spacing one remains unchanged");
                TestSettings.Settings.EnableMod=false;
                Check(ChooseAssassinGroundFormationSlot(nativePathManager,3,60,10)==3,
                    "disabled feature restores Vanilla Assassin ground spacing three");
                TestSettings.Settings.EnableMod=true;
                // Exercise the optional current formation-provider boundary independently
                // of the productive formation engine covered by Formations.Tests.
                var savedOriginal = originalFormationSlot;
                var savedAssassinOriginal = originalAssassinGroundFormationSlot;
                bool savedDisposed = disposed;
                int providerFallbacks = 0;
                int* outputTriple = (int*)(tribes + 0x0C);
                outputTriple[0] = 61; outputTriple[1] = 11; outputTriple[2] = 2;
                var formationProvider = new FormationRuntime { Standard = (m,s,x,y) => true };
                formationRuntime = formationProvider;
                originalFormationSlot = (m,s,x,y) => providerFallbacks++;
                ChooseOwnerSafeFormationSlot(nativePathManager,3,60,10);
                Check(providerFallbacks==0,"successful formation provider is authoritative");
                formationProvider.Standard = (m,s,x,y) => {
                    outputTriple[0]=99; outputTriple[1]=98; outputTriple[2]=97;
                    throw new InvalidOperationException("test formation provider");
                };
                disposed = true; // force the traversal fallback to the unchanged selector
                originalFormationSlot = (m,s,x,y) => {
                    Check(outputTriple[0]==61 && outputTriple[1]==11 && outputTriple[2]==2,
                        "failed formation provider restores all native output fields before fallback");
                    providerFallbacks++;
                };
                ChooseOwnerSafeFormationSlot(nativePathManager,3,60,10);
                Check(formationProvider.Disabled && providerFallbacks==1,
                    "failed formation provider is logically disabled and original called once");
                formationProvider = new FormationRuntime { Assassin = (m,s,x,y) => 123 };
                formationRuntime = formationProvider;
                originalAssassinGroundFormationSlot = (m,s,x,y) => { providerFallbacks++; return 80; };
                Check(ChooseAssassinGroundFormationSlot(nativePathManager,3,60,10)==123 && providerFallbacks==1,
                    "successful Assassin formation provider is authoritative");
                formationProvider.Assassin = (m,s,x,y) => {
                    outputTriple[0]=99; outputTriple[1]=98; outputTriple[2]=97;
                    throw new InvalidOperationException("test Assassin provider");
                };
                originalAssassinGroundFormationSlot = (m,s,x,y) => {
                    Check(outputTriple[0]==61 && outputTriple[1]==11 && outputTriple[2]==2,
                        "failed Assassin provider restores all native output fields before fallback");
                    providerFallbacks++; return 80;
                };
                Check(ChooseAssassinGroundFormationSlot(nativePathManager,3,60,10)==80 &&
                    formationProvider.Disabled && providerFallbacks==2,"failed Assassin provider falls back once");
                formationRuntime=null;disposed=savedDisposed;
                originalFormationSlot=savedOriginal;originalAssassinGroundFormationSlot=savedAssassinOriginal;
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
                var successfulFormationSlot=originalFormationSlot;int throwingOriginalCalls=0;bool originalThrew=false;
                originalFormationSlot=(m,spacing,x,y)=>{throwingOriginalCalls++;throw new InvalidOperationException("native original failure");};
                try{ChooseOwnerSafeFormationSlot(nativePathManager,2,60,10);}catch{originalThrew=true;}finally{originalFormationSlot=successfulFormationSlot;}
                Check(originalThrew && throwingOriginalCalls==1,"throwing native formation original is never invoked twice");
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

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        private sealed class AttackCommandScope
        {
            internal MovementOptionsSnapshot Options = MovementOptionsSnapshot.Capture();
            internal RequiredRouteMetrics Required = new RequiredRouteMetrics();
            internal RequiredRouteCache RequiredCache = new RequiredRouteCache();
            internal System.Collections.Generic.HashSet<int> CandidateUnitIds = new System.Collections.Generic.HashSet<int>();
            public System.Collections.Generic.HashSet<LadderRegionTransition> PositiveLadderRegionTransitions = new System.Collections.Generic.HashSet<LadderRegionTransition>();
            public bool CandidatesCaptured;
            public int MapEpoch, TribeId, TargetValue1, TargetValue2, Sequence;
            public TribeAICommand Command;
            public long NativeBuilderTicks, AuditTicks, WeightedPhaseTicks, WeightedAuditTicks;
            public int NativeBuilderCalls, AuditCalls;
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
                    nativeBuildingLayer[1000+x]=0;pathRegionGrid[1000+x]=(ushort)(x<55?1:2);
                    movementTargetAvailability[8000+x]=1;
                    tileFlags[2000+x]=0;nativeMovementMasks[2000+x]=0;nativeBuildingLayer[2000+x]=1;
                }
                var pool=new System.Collections.Generic.List<BuildingApproachCandidate>();
                for(int i=0;i<351;i++)pool.Add(new BuildingApproachCandidate(1060+i,i<12?2060+i:0,10000000));
                var command=new AttackCommandScope { MapEpoch=mapEpoch,TribeId=1,TargetValue1=1,TargetValue2=900,Sequence=1,Command=TribeAICommand.AttackBuilding };
                var scope=new AttackApproachDiagnosticScope{OwnerCommand=command,TribeId=1};
                getGroupUnitId=(m,t,n)=>n+1;
                *(short*)(tribes+TribeRecordSize+TribeLeadUnitIdOffset)=1;
                var ladderProducer=new[]
                {
                    new BuildingApproachCandidate(1060,2060,VanillaUnreachableCandidateScore),
                    new BuildingApproachCandidate(1061,2061,VanillaUnreachableCandidateScore),
                    new BuildingApproachCandidate(1062,2062,VanillaUnreachableCandidateScore)
                };
                pathRegionGrid[1062]=3;
                command.PositiveLadderRegionTransitions.Add(new LadderRegionTransition(1,1,2));
                activeAttackCommand=command;
                WriteBuildingApproachCandidates(nativePathManager,new System.Collections.Generic.List<BuildingApproachCandidate>
                {
                    new BuildingApproachCandidate(1060,2060,7)
                });
                var ladderRestore=RestoreVanillaLadderBuildingCandidates(command,nativeTribeManager,ladderProducer);
                var ladderAfter=CaptureBuildingApproachCandidates(nativePathManager);
                Check(ladderRestore.ProvenTransitions==1 && ladderRestore.AfterMoatPairs==1 &&
                    ladderRestore.EligiblePairs==1 && ladderRestore.RestoredPairs==1,
                    "only a removed pair in an exactly proven ladder region is restored");
                Check(ladderAfter.Length==2 && ladderAfter[0].ApproachTileId==1060 &&
                    ladderAfter[0].Score==7 && ladderAfter[1].ApproachTileId==1061 &&
                    ladderAfter[1].Score==VanillaUnreachableCandidateScore,
                    "Vanilla order and score survive before the restored Vanilla-sentinel pair");
                command.PositiveLadderRegionTransitions.Clear();
                WriteBuildingApproachCandidates(nativePathManager,new System.Collections.Generic.List<BuildingApproachCandidate>());
                ladderRestore=RestoreVanillaLadderBuildingCandidates(command,nativeTribeManager,ladderProducer);
                Check(ladderRestore.RestoredPairs==0 &&
                    CaptureBuildingApproachCandidates(nativePathManager).Length==0,
                    "a building pair is never restored without a positive region proof");
                var fullBuffer=new System.Collections.Generic.List<BuildingApproachCandidate>();
                for(int index=0;index<VanillaAttackFloodResultCapacity-1;index++)
                    fullBuffer.Add(new BuildingApproachCandidate(300000+index,0,index+1));
                WriteBuildingApproachCandidates(nativePathManager,fullBuffer);
                command.PositiveLadderRegionTransitions.Add(new LadderRegionTransition(1,1,2));
                ladderRestore=RestoreVanillaLadderBuildingCandidates(command,nativeTribeManager,ladderProducer);
                Check(ladderRestore.RestoredPairs==0 && ladderRestore.EligiblePairs==2 &&
                    CaptureBuildingApproachCandidates(nativePathManager).Length==VanillaAttackFloodResultCapacity-1,
                    "a full native candidate buffer remains bounded and unchanged");
                command.PositiveLadderRegionTransitions.Clear();
                pathRegionGrid[1062]=2;
                foreach(int count in new[]{1,120,680})
                {
                    *(short*)(tribes+TribeRecordSize+TribeUnitCountOffset)=(short)count;
                    for(int id=1;id<=count;id++)units[id]=new GameUnit{Digger=true,r_GlobalId=(uint)(9000+id),r_TribeId=1,r_ControllableForPlayerId=1,
                        r_AliveState=AliveState.IsAlive,r_CurrentTilePositionX=50,r_CurrentTilePositionY=10,r_NextTilePositionX2=50,r_NextTilePositionY2=10,r_MovementSubstep=8};
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
                        if(moat)
                        {
                            command.PositiveLadderRegionTransitions.Add(new LadderRegionTransition(1,1,2));
                            var merged=RestoreVanillaLadderBuildingCandidates(command,nativeTribeManager,before);
                            Check(merged.RestoredPairs==0 && CaptureBuildingApproachCandidates(nativePathManager).Length==351,
                                "moat-published candidates are retained first without ladder duplicates");
                            command.PositiveLadderRegionTransitions.Clear();
                        }
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
                                // Fast flow: the place qualifies at the validated pre-builder failure, not at the mode event.
                                Check(*moatPathMode==0 && TryRecoverBeforeBuilder((IntPtr)nativeUnitManager,id,50,10,x,10)==1,
                                    "staging place qualifies at the validated pre-builder failure");
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
