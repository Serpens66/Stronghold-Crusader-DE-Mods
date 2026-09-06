using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using Iced.Intel;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        public static void RunMachineContract(string root)
        {
            const ulong library=0x180000000, stub=0x181000000;
            byte[] bytes={0x33,0xC0,0x8B,0xD6,0x48,0x89,0x05,0x8E,0x70,0xF1,0x05,0x49,0x8B,0xCF};
            var decoder=Decoder.Create(64,new ByteArrayCodeReader(bytes)); decoder.IP=library+0x19664B;
            var original=new List<Instruction>();
            while(decoder.IP<library+0x196659) { decoder.Decode(out Instruction instruction); original.Add(instruction); }
            var assembler=new Assembler(64);
            EmitRecoveryAdapter(assembler,original.ToArray(),0x123456789ABCDEF0,library);
            using var stream=new MemoryStream();
            assembler.Assemble(new StreamCodeWriter(stream),stub);
            byte[] encoded=stream.ToArray();
            File.WriteAllBytes(Path.Combine(root,"BugfixesAndQoL","tests","FriendlyMoatMovement.Tests","latest-recovery-stub.bin"),encoded);
            var checkedDecoder=Decoder.Create(64,new ByteArrayCodeReader(encoded)); checkedDecoder.IP=stub;
            bool callback=false, continuation=false, nativeStore=false;
            while(checkedDecoder.IP<stub+(ulong)encoded.Length)
            {
                checkedDecoder.Decode(out Instruction instruction);
                Check(instruction.Code!=Code.INVALID,"actual emitted recovery instruction is valid");
                if(instruction.Mnemonic==Mnemonic.Call) callback=instruction.Op0Register==Register.RAX;
                if(instruction.IsJmpShortOrNear && instruction.NearBranchTarget==library+0x196585) continuation=true;
                if(instruction.IsIPRelativeMemoryOperand && instruction.IPRelativeMemoryAddress==original[2].IPRelativeMemoryAddress) nativeStore=true;
            }
            Check(callback&&continuation&&nativeStore,"actual emitter preserves callback, native direct branch and relocated failure store");
            Console.WriteLine($"PASS: actual recovery emitter assembled and decoded ({encoded.Length} bytes, no native code executed).");
        }

        private void FillSelectionTests()
        {
            byte* records=(byte*)NativeMemory.AllocZeroed((nuint)(MoatRecordCountOffset+16));
            var api=GameTileManagerAPI.Instance;
            IntPtr previous=api.TileManager; api.TileManager=(IntPtr)records;
            try
            {
                *(int*)(records+MoatRecordCountOffset)=64000;
                int currentId=950;
                getMoatIdAtTile=(manager,tile)=>tile==1017?currentId:0;
                foreach(int id in new[]{799,800,32768,63999,950})
                {
                    byte* record=records+MoatRecordArrayOffset+id*16;
                    *(int*)record=1017; *(short*)(record+4)=17; *(short*)(record+6)=10;
                    currentId=id;
                    Check(TryGetMoatRecord((IntPtr)records,1017,10,out int readId,out _,out _) && readId==id,
                        "actual record reader accepts full native capacity");
                    Check(TryReadMoatRecord((IntPtr)records,id,out _,out _,out _,out _),"resolver record reader shares capacity contract");
                }
                for(int x=10;x<=18;x++) { nativeHeightLayer[1000+x]=0; movementTargetAvailability[10*800+x]=1; }
                tileFlags[1017]=CompletedMoatTileFlag; enemyTiles.Add(1017);
                originalHasFillMoatApproach=(manager,source,tile,y)=>0;
                int calls=0;
                originalFindMoatWorkTarget=(manager,player,unit,mode)=>
                {
                    calls++;
                    if(AllowFillMoatApproachThroughFriendlyMoat(manager,1,1017,10)==0) return -1;
                    records[MoatRecordArrayOffset+950*16+15]+=20;
                    return 950;
                };
                MoatWorkSelectionScope NewWork()=>new MoatWorkSelectionScope(mapEpoch,(IntPtr)records,1,1,2,10,10,1010,1);
                var work=NewWork();
                Check(SelectMoatWorkTarget(work,(IntPtr)records,1,1,2)==950 && calls==1 && work.FillApproaches.ContainsKey(950),
                    "enemy work behind friendly moat selected by a single native pass");
                Check(records[MoatRecordArrayOffset+950*16+15]==20,"winner reserved exactly once");
                api.Occupants[1016]=2; api.Occupants[1018]=2;
                Check(SelectMoatWorkTarget(NewWork(),(IntPtr)records,1,1,2)==-1 && calls==2,"occupied approaches rejected before reservation");
                Check(records[MoatRecordArrayOffset+950*16+15]==20,"rejected selection never modifies reservation");
                api.Occupants.Clear();
                enemyTiles.Add(1013);
                Check(SelectMoatWorkTarget(NewWork(),(IntPtr)records,1,1,2)==-1,"next work cycle observes changed moat owner");
                enemyTiles.Remove(1013);
                Check(SelectMoatWorkTarget(NewWork(),(IntPtr)records,1,1,2)==950,"next work cycle discards previous negative result");
                // Height is local to the contact, independently of the worker's elevation.
                originalHasFillMoatApproach=(manager,source,tile,y)=>1;
                pathRegionGrid[1016]=1; nativeHeightLayer[1016]=24; nativeHeightLayer[1017]=24;
                api.Occupants[1018]=2;
                Check(SelectMoatWorkTarget(NewWork(),(IntPtr)records,1,1,2)==950,"contact-height rule does not compare against distant lower worker");
                nativeHeightLayer[1017]=0;
                Check(SelectMoatWorkTarget(NewWork(),(IntPtr)records,1,1,2)==-1,"excessive height rejects even a native-positive candidate");
            }
            finally
            {
                api.TileManager=previous; api.Occupants.Clear();
                enemyTiles.Clear(); tileFlags[1017]=0x8000;
                nativeHeightLayer[1016]=nativeHeightLayer[1017]=0; pathRegionGrid[1016]=2;
                NativeMemory.Free(records);
            }
        }

        private void CursorInvocationTests()
        {
            int* coordinates=(int*)NativeMemory.AllocZeroed(8);
            GameCursorManager* cursor=(GameCursorManager*)NativeMemory.AllocZeroed((nuint)sizeof(GameCursorManager));
            var player=GamePlayerManagerAPI.Instance;
            var units=GameUnitManagerAPI.Instance.Units;
            var savedPair=originalCursorTilePairReachability;
            cursorTargetX=coordinates; cursorTargetY=coordinates+1; *cursorTargetX=17; *cursorTargetY=10;
            player.Cursor=cursor; *(int*)nativeUnitManager=1025;
            EngineInterface.Selection=new[]{1,0}; units[1].r_UnitSelected=1;
            originalCursorTilePairFallbackSelection=_=>0;
            getRepresentativeSelectedUnit=(_,kind)=>EngineInterface.Selection.Length==0?0:EngineInterface.Selection[0];
            selectionCanDigMoat=_=>{
                for(int i=0;i<EngineInterface.Selection.Length;i+=2)
                    if(CanDigMoat(units+EngineInterface.Selection[i])) return 1;
                return 0;
            };
            int nativeCalls=0;
            originalCursorTilePairReachability=(_,target,start,cache)=>{ nativeCalls++; return 7; };
            int Hover(int pairTarget=1017)
            {
                pendingAttackCursorPair=null;
                int gate=ObserveCursorTilePairFallbackSelection((IntPtr)nativeUnitManager);
                return gate==0?0:AllowAttackCursorTilePairThroughCompletedMoat(nativePathManager,pairTarget,1010,1);
            }
            try
            {
                Check(Hover()==1 && nativeCalls==0,"complete selection -> scope -> pair -> native positive cursor branch without a ground detour");
                Check(GamePlayerManagerAPI.Instance.GetSelectedChimps()[0].UnitId==1,
                    "2.0.2 selection projection preserves the 1-based unit ID");
                foreach(int count in new[]{1,120,1000})
                {
                    int[] selected=new int[count*2]; for(int i=0;i<count;i++) selected[i*2]=i+1;
                    EngineInterface.Selection=selected;
                    for(int warm=0;warm<5;warm++) Hover();
                    long allocated=GC.GetAllocatedBytesForCurrentThread(), time=Stopwatch.GetTimestamp();
                    long runs=weightedMoatRoutePlanner.SearchRuns;
                    bool valid=true; for(int i=0;i<100;i++) valid &= Hover()==1;
                    double elapsed=(Stopwatch.GetTimestamp()-time)*1000.0/Stopwatch.Frequency;
                    allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
                    Check(valid && runs==weightedMoatRoutePlanner.SearchRuns,"complete cursor chain never searches a route");
                    Check(allocated < 5000000,
                        "2.0.2 SelectedUnitInfo projection remains within the bounded cursor allocation budget");
                    Console.WriteLine($"CURSOR FULL CHAIN units={count} queries=100 ms={elapsed:F3} allocatedBytes={allocated} pathSearches=0");
                }
                EngineInterface.Selection=Array.Empty<int>();
                Check(!CaptureCursorSelection(1,out _,out _) && cursorSelectionAvailable,"empty selection differs from unavailable source");
                EngineInterface.Selection=new[]{1,0};
                tileFlags[1015]=CompletedMoatTileFlag;
                for(int x=16;x<=18;x++) pathRegionGrid[1000+x]=3;
                (*(int*)((byte*)nativePathManager+0x74))++; DirtyCursorTile(1015);
                Check(Hover()==1,"two friendly moat crossings remain reachable");
                enemyTiles.Add(1015); DirtyCursorTile(1015);
                Check(Hover()==0,"enemy moat blocks full cursor chain");
                enemyTiles.Remove(1015); tileFlags[1015]=0x8000;
                for(int x=16;x<=18;x++) pathRegionGrid[1000+x]=2;
                (*(int*)((byte*)nativePathManager+0x74))++; DirtyCursorTile(1015);
                foreach(uint flags in new uint[]{0x100,0x200,0x10000000})
                {
                    tileFlags[1017]=flags; nativeMovementMasks[1017]=0; DirtyCursorTile(1017);
                    Check(Hover()==1,"structure bits do not veto reachable directed endpoint");
                    units[1001]=new GameUnit { r_GlobalId=9001,r_AliveState=AliveState.IsAlive,r_ControllableForPlayerId=1,r_CurrentTilePositionX=17,r_CurrentTilePositionY=10 };
                    GameTileManagerAPI.Instance.Occupants[1017]=1001;
                    Check(Hover()==1,"friendly occupied structure retains movement cursor across moat");
                    cursor->r_HoverOverUnitId=1001;
                    Check(Hover()==1,"friendly structure sprite hover does not become an attack or veto movement reachability");
                    cursor->r_HoverOverUnitId=0; GameTileManagerAPI.Instance.Occupants.Clear();
                    Check(ProbeCursorConnectivity(1,1017,1010,out var reverse) && !reverse.RouteFound,"incoming structure endpoint invents no reverse edge");
                    nativeHeightLayer[1017]=100; DirtyCursorTile(1017);
                    Check(Hover()==0,"inaccessible structure height remains blocked");
                    nativeHeightLayer[1017]=0; DirtyCursorTile(1017);
                }
                GameBuilding* gate=(GameBuilding*)NativeMemory.AllocZeroed((nuint)sizeof(GameBuilding));
                GameBuildingManagerAPI.Instance.Building=gate;
                gate->r_TilePositionXBegin=gate->r_TilePositionXEnd=17;
                gate->r_TilePositionYBegin=gate->r_TilePositionYEnd=10;
                int* portals=(int*)nativePathManager; int portal=0x81;
                nativeBuildingLayer[1017]=1; portals[0]=2;
                portals[portal+0x809]=1; portals[portal+0x80C]=1; portals[portal+0x80F]=1;
                portals[portal+0x882]=1; portals[portal+0x80A]=1;
                DirtyCursorTile(1017);
                Check(Hover()==0,"blocked portal prevents structure cursor connection");
                portals[portal+0x80A]=2; portals[portal+0x882]=2;
                Check(Hover()==0,"foreign portal prevents structure cursor connection");
                portals[portal+0x882]=1;
                Check(Hover()==1,"owned allowed portal restores structure cursor connection");
                nativeBuildingLayer[1017]=0; portals[0]=0; portals[portal+0x80C]=0;
                DirtyCursorTile(1017); GameBuildingManagerAPI.Instance.Building=null; NativeMemory.Free(gate);
                tileFlags[1017]=0x8000; nativeMovementMasks[1017]=0x44; DirtyCursorTile(1017);
                units[1001]=new GameUnit { r_GlobalId=9001,r_AliveState=AliveState.IsAlive,r_ControllableForPlayerId=2,r_CurrentTilePositionX=17,r_CurrentTilePositionY=10 };
                cursor->r_HoverOverUnitId=1001; GameTileManagerAPI.Instance.Occupants[1017]=1001;
                // The sprite lies over 1018; Vanilla passes the physical target tile 1017.
                *cursorTargetX=18;
                movementTargetAvailability[10*800+17]=0;
                Check(Hover()==1,"unit attack uses physical target region despite sprite offset and occupied target");
                ObserveCursorTilePairFallbackSelection((IntPtr)nativeUnitManager);
                var bound=pendingAttackCursorPair;
                units[1001].r_GlobalId++;
                Check(!TryProbeUnitApproachCursorRoute(bound,out _,out _,out _),"reused attack target ID rejected");
                units[1001].r_GlobalId--;
                units[1001].r_CurrentTilePositionX=18;
                Check(!TryProbeUnitApproachCursorRoute(bound,out _,out _,out _),"moved attack target invalidates bound endpoint");
                units[1001].r_CurrentTilePositionX=17; units[1001].r_ControllableForPlayerId=1;
                Check(!TryProbeUnitApproachCursorRoute(bound,out _,out _,out _),"friendly target not an attack");
                units[1001].r_ControllableForPlayerId=2; units[1001].r_AliveState=AliveState.Dead;
                Check(!TryProbeUnitApproachCursorRoute(bound,out _,out _,out _),"dead target not an attack");
                cursor->r_HoverOverUnitId=0; GameTileManagerAPI.Instance.Occupants.Clear();
                movementTargetAvailability[10*800+17]=1; *cursorTargetX=17;
                units[2].Digger=false; EngineInterface.Selection=new[]{2,0,1,0};
                Check(Hover()==1,"mixed selection resolves an eligible digger without granting a moat capability to others");
                EngineInterface.Selection=new[]{2,0}; originalCursorTilePairFallbackSelection=_=>1;
                Check(Hover()==7 && nativeCalls==1,"native special selection without diggers retains original pair behavior");
            }
            finally
            {
                units[2].Digger=true; units[1].r_UnitSelected=0;
                EngineInterface.Selection=new[]{1,0}; player.Cursor=null;
                cursorTargetX=cursorTargetY=null; pendingAttackCursorPair=null;
                originalCursorTilePairReachability=savedPair;
                NativeMemory.Free(coordinates); NativeMemory.Free(cursor);
            }
        }

        private void CursorAdapterTests()
        {
            cursorTopologies.Clear(); enemyTiles.Clear();
            for (int x=10;x<=18;x++) { tileFlags[1000+x]=0x8000; pathRegionGrid[1000+x]=(short)(x<13?1:2); }
            tileFlags[1013]=CompletedMoatTileFlag;
            nativeHeightLayer[1013]=0;
            for(int x=10;x<=18;x++) nativeMovementMasks[1000+x]=0x44;
            long lazyBuilds=cursorTopologyBuilds;
            Check(ProbeCursorConnectivity(1,1010,1011,out _) && cursorTopologyBuilds==lazyBuilds,
                "ordinary same-region hover needs no topology build");
            Check(ProbeCursorConnectivity(1,1010,1017,out var route) && route.RouteFound && route.ReachedWithMoat,
                "production cursor topology joins regions through friendly moat");
            long builds=cursorTopologyBuilds, pathRuns=weightedMoatRoutePlanner.SearchRuns;
            Check(ProbeGroundConnection(1,1010,1017)==GroundConnectionDecision.Excluded,
                "complete ground upper graph excludes a moat-only connection");
            Check(ProbeGroundConnection(1,1010,1011)==GroundConnectionDecision.Unknown,
                "merged region is not a concrete positive path proof");
            cursorTopologies[1].Dirty.Add(1013);
            Check(ProbeGroundConnection(1,1010,1017)==GroundConnectionDecision.Unknown,
                "dirty graph never supplies a negative proof");
            cursorTopologies[1].Dirty.Remove(1013);
            for(int sx=10;sx<=18;sx++)for(int tx=10;tx<=18;tx++)
            {
                bool actual=weightedMoatRoutePlanner.TryProbeReachability(1,sx,10,tx,10,false,
                    MoatTraversalPolicy.GroundOnly,out _);
                Check(ProbeGroundConnection(1,1000+sx,1000+tx)!=GroundConnectionDecision.Excluded || !actual,
                    "negative upper-graph proof agrees with actual directed ground search");
            }
            pathRuns=weightedMoatRoutePlanner.SearchRuns;
            Check(ProbeCursorConnectivity(1,1013,1017,out route) && route.RouteFound,"cursor start on moat has its own node");
            enemyTiles.Add(1013); DirtyCursorTile(1013);
            Check(ProbeCursorConnectivity(1,1010,1017,out route) && !route.RouteFound,"owner change removes cached positive connection");
            enemyTiles.Clear(); DirtyCursorTile(1013);
            Check(ProbeCursorConnectivity(1,1010,1017,out route) && route.RouteFound,"owner change removes cached negative connection");
            Check(cursorTopologyBuilds==builds,"local changes do not rebuild the full map");
            Check(weightedMoatRoutePlanner.SearchRuns==pathRuns,"cursor creates no route searches");

            int nativePairs = 0;
            originalCursorTilePairReachability = (manager,target,start,cache) => { nativePairs++; return 7; };
            EngineInterface.Selection = new[]{1,0};
            AttackCursorPairScope DirectScope() => new AttackCursorPairScope(mapEpoch,1,1,10,10,1010,17,10,1017,CursorPairFallbackKind.DirectTile);
            pendingAttackCursorPair = DirectScope();
            Check(AllowAttackCursorTilePairThroughCompletedMoat(nativePathManager,1017,1010,1)==1 && nativePairs==0,
                "actual cursor adapter answers before native area search");
            enemyTiles.Add(1013); DirtyCursorTile(1013); pendingAttackCursorPair=DirectScope();
            Check(AllowAttackCursorTilePairThroughCompletedMoat(nativePathManager,1017,1010,1)==0 && nativePairs==0,
                "negative cursor decision also avoids native area search");
            enemyTiles.Clear(); DirtyCursorTile(1013);
            Check(AllowAttackCursorTilePairThroughCompletedMoat(nativePathManager,1017,1010,1)==7 && nativePairs==1,
                "unbound native consumer retains original result");
            activeAttackApproachDiagnostic=new object(); pendingAttackCursorPair=DirectScope();
            Check(AllowAttackCursorTilePairThroughCompletedMoat(nativePathManager,1017,1010,1)==7 && nativePairs==2,
                "nested real attack consumer retains native contract");
            activeAttackApproachDiagnostic=null;
            GameBuilding building = new GameBuilding { r_GlobalId=42 };
            GameBuildingManagerAPI.Instance.Building=&building;
            GameUnit* first=GameUnitManagerAPI.Instance.Units+1;
            first->r_CurrentPositionTileId=1010;
            originalBuildingCursorReachability=(manager,id,unit)=>AllowAttackCursorTilePairThroughCompletedMoat(nativePathManager,1010,1017,0);
            Check(CallBuildingCursorWithRegions(IntPtr.Zero,1,1)==1 && nativePairs==2 && activeBuildingCursorConnectivity==null,
                "native building candidate uses reversed pair and restores scope");
            GameBuildingManagerAPI.Instance.Building=null;

            // Native first-phase portal filter: owned open connection, then blocked kind.
            int* context=(int*)nativePathManager;
            context[0]=2; int index=0x81;
            context[index+0x809]=1; context[index+0x80A]=2;
            context[index+0x80F]=1; context[index+0x882]=1;
            context[index+0x816]=1; context[index+0x817]=2;
            Check(ProbeCursorConnectivity(1,1010,1017,out route) && route.ReachedWithoutMoat,"native portal connects ground regions without a path probe");
            context[index+0x80A]=1;
            Check(ProbeCursorConnectivity(1,1010,1017,out route) && !route.ReachedWithoutMoat && route.ReachedWithMoat,
                "blocked native portal does not erase the additional friendly connection");
            context[0]=0;

            CursorInvocationTests();
            foreach(int count in new[]{1,120,1000})
            {
                var ids=new int[count*2];
                for(int i=0;i<count;i++) ids[i*2]=i+1;
                EngineInterface.Selection=ids;
                Check(CaptureCursorSelection(1,out _,out var token),"production selection capture");
                // Warm the public 2.0.2 selection projection and graph paths.
                for(int i=0;i<5;i++) { CaptureCursorSelection(1,out _,out _); ProbeCursorConnectivity(1,1010,1017,out _); }
                long allocated=GC.GetAllocatedBytesForCurrentThread();
                long nodes=cursorTopologies[1].Graph.ExpandedNodes;
                long before=Stopwatch.GetTimestamp();
                bool valid=true;
                for(int i=0;i<1000;i++)
                {
                    valid &= CaptureCursorSelection(1,out _,out var current) && current==token;
                    valid &= ProbeCursorConnectivity(1,1010,1017,out route) && route.RouteFound;
                }
                double ms=(Stopwatch.GetTimestamp()-before)*1000.0/Stopwatch.Frequency;
                allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
                Check(valid && allocated < 30000000,
                    "unchanged cursor/selection stays within the bounded 2.0.2 projection budget");
                Check(cursorTopologies[1].Graph.ExpandedNodes==nodes,"unchanged cursor reuses its regional closure");
                Console.WriteLine($"CURSOR PRODUCTION ADAPTER units={count} queries=1000 ms={ms:F3} allocatedBytes={allocated} newNodes=0 pathSearches=0");
                var direct=DirectScope();
                for(int warm=0;warm<4;warm++) TryQualifySelectedGroupCursorRoute(direct,out _,out _);
                long groupAllocated=GC.GetAllocatedBytesForCurrentThread();
                for(int hover=0;hover<100;hover++) TryQualifySelectedGroupCursorRoute(direct,out _,out _);
                groupAllocated=GC.GetAllocatedBytesForCurrentThread()-groupAllocated;
                Check(groupAllocated < 5000000,
                    "group cursor stays within the bounded 2.0.2 projection budget");
                Console.WriteLine($"CURSOR GROUP ADAPTER units={count} queries=100 allocatedBytes={groupAllocated}");
                GameUnitManagerAPI.Instance.Units[1].r_GlobalId++;
                Check(CaptureCursorSelection(1,out _,out var replaced) && replaced!=token,"slot reuse invalidates cursor selection identity");
            }
        }
    }

    public static class CursorGraphTests
    {
        public static void Run()
        {
            int checks=0; var random=new Random(871);
            // Independent transitive closure on directed graphs, including parallel edges.
            for(int test=0;test<80;test++)
            {
                const int n=18; var graph=new CursorRegionGraph(n); var counts=new int[n,n];
                for(int change=0;change<60;change++)
                {
                    int a=random.Next(n), b=random.Next(n); if(a==b) continue;
                    int delta=counts[a,b]>0 && random.Next(2)==0 ? -1 : 1;
                    counts[a,b]+=delta; graph.ChangeEdge(a,b,delta);
                    var expected=new bool[n,n];
                    for(int i=0;i<n;i++) for(int j=0;j<n;j++) expected[i,j]=i==j||counts[i,j]>0;
                    for(int k=0;k<n;k++) for(int i=0;i<n;i++) for(int j=0;j<n;j++) expected[i,j]|=expected[i,k]&&expected[k,j];
                    for(int j=0;j<n;j++) for(int i=0;i<n;i++)
                    {
                        if(graph.CanReach(i,j)!=expected[i,j]) throw new Exception("Cursor directed/reference-count regression");
                        checks++;
                    }
                }
            }
            Console.WriteLine($"PASS: {checks} independent cursor connectivity comparisons after edge additions/removals.");
        }
    }
}
