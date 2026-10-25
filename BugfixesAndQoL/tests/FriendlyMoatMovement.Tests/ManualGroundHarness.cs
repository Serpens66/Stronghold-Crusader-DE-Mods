using System;
using System.Runtime.InteropServices;
using APIShared;
using BugfixesAndQoL.UnitCommands;
using SHCDESE.Interop.Enums;

// The external planner is a double. Selection, proof capture/validation, policy
// matching, the existing cache, and all native snapshot copies are production code.
namespace CommandFixture
{
    public unsafe partial class UnitCommandPathRuntime
    {
        private const int PathBuilderRva = 0xF4930;
        private byte* nativeMovementMasks, nativeDirectionMasks, movementTargetAvailability;
        private uint* tileFlags;
        private IEnemyGatePathPolicy BeginEnemyGateSearch(int player, EnemyGateSearchKind kind, out object scope)
        { var gate=EnemyGatePathPolicyBridge.Current;scope=gate?.EnterNativeSearch(player,kind);return gate; }
        private void EndEnemyGateSearch(IEnemyGatePathPolicy gate,object scope,EnemyGateSearchKind kind,bool done,bool success)=>gate?.ExitNativeSearch(scope,kind,done,success);

        private void GroundTests()
        {
            int before=assertions;
            GameUnit* units=(GameUnit*)NativeMemory.AllocZeroed(551*(nuint)sizeof(GameUnit));
            byte* allocation=(byte*)NativeMemory.AllocZeroed(NativeProbeManagerBytes+0x18);
            nativeProbeGrid=(byte*)NativeMemory.AllocZeroed(NativeProbeGridBytes);
            nativeProbeRectangle=(byte*)NativeMemory.AllocZeroed(0x20);
            nativeMovementMasks=(byte*)NativeMemory.AllocZeroed(CursorGroundRejectProof.TileCount);
            nativeDirectionMasks=(byte*)NativeMemory.AllocZeroed(8);
            cursorGroundNeighborOffsets=(int*)NativeMemory.AllocZeroed(800*8*sizeof(int));
            cursorGroundRows=(ushort*)NativeMemory.AllocZeroed(CursorGroundRejectProof.TileCount*sizeof(ushort));
            movementTargetAvailability=(byte*)NativeMemory.Alloc(800*800);
            tileFlags=(uint*)NativeMemory.AllocZeroed(CursorGroundRejectProof.TileCount*sizeof(uint));
            try
            {
                nativePathManager=(IntPtr)(allocation+0x18);nativeUnitManager=(byte*)units;
                APIShared.UnitAccess.Units=units;APIShared.UnitAccess.UnitCapacity=551;
                GameTileManagerAPI.Instance.MapColumnLookupTable=cursorGroundRows;
                new Span<byte>(movementTargetAvailability,800*800).Fill(1);
                for(int t=0;t<CursorGroundRejectProof.TileCount;t++)cursorGroundRows[t]=(ushort)(t/800);
                int[] offsets={-800,-799,1,801,800,799,-1,-801};
                for(int y=0;y<800;y++)for(int d=0;d<8;d++)cursorGroundNeighborOffsets[y*8+d]=offsets[d];
                for(int d=0;d<8;d++)nativeDirectionMasks[d]=(byte)(1<<d);
                for(int t=8010;t<=8020;t++)nativeMovementMasks[t]=(byte)((t<8020?4:0)|(t>8010?64:0));
                for(int i=1;i<=550;i++)
                { units[i].r_GlobalId=(uint)i;units[i].r_ControllableForPlayerId=1;units[i].r_CurrentTilePositionX=10;units[i].r_CurrentTilePositionY=10;units[i].r_CurrentPositionTileId=8010;units[i].r_UnitChimp=26; }
                pathRegionGrid[8010]=pathRegionGrid[16020]=1;
                originalRegionPairReachability=(m,p,s,t,k)=>s==t?s:0;
                cursorRejectContractReady=cursorGroundContractReady=true;
                APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance.Profiles[26]=PathfindingProfile.Default;
                int calls=0,generation=1,stampEnd=8020;bool throwAfter=false,extraBuilder=false,targetVisited=false;
                int builderResult=0;
                originalCentralMovementPlan=(m,id,x,y)=>
                {
                    using var gateScope=(EnemyGatePathPolicyBridge.Current as GroundPolicy)?.Enter();
                    calls++;var u=units+id;
                    if(u->r_CurrentTilePositionX==x&&u->r_CurrentTilePositionY==y)return 1;
                    if(u->r_CurrentTilePositionX==30)return 1; // a later independent reachable member
                    byte* state=(byte*)nativePathManager;
                    *(int*)(state+4)=generation;*(int*)(state+8)=u->r_CurrentTilePositionX;*(int*)(state+12)=u->r_CurrentTilePositionY;
                    *(int*)(state+16)=x;*(int*)(state+20)=y;
                    short* stamps=(short*)(nativeProbeGrid+NativeProbeGridBytes/2);
                    for(int t=8010;t<=stampEnd;t++)stamps[t]=(short)generation;
                    if(targetVisited)stamps[y*800+x]=(short)generation;
                    ((byte*)u)[sizeof(GameUnit)-1]=31;nativeProbeRectangle[31]=32;
                    ObserveCursorGroundBuilder(nativePathManager,u->r_ControllableForPlayerId,
                        (int)APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance.Profiles[u->r_UnitChimp],builderResult);
                    if(extraBuilder)ObserveCursorGroundBuilder(nativePathManager,1,1,0);
                    if(throwAfter)throw new InvalidOperationException("after-ground-capture");
                    return builderResult>0?builderResult:-1;
                };
                int[] ids=new int[550];for(int i=0;i<ids.Length;i++)ids[i]=i+1;
                void Restored()=>Check(*(int*)((byte*)nativePathManager+4)==0&&nativeProbeGrid[NativeProbeGridBytes/2+8010*2]==0&&nativeProbeRectangle[31]==0&&((byte*)(units+1))[sizeof(GameUnit)-1]==0&&!nativeManualProbe,"ground proof retains complete snapshot/context restoration");
                foreach(var profile in new[]{PathfindingProfile.Default,PathfindingProfile.Large})
                foreach(int size in new[]{100,200,550})
                {
                    APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance.Profiles[26]=profile;
                    fixtureTick++;calls=0;
#if SERP_DEVDIAG
                    BugfixesAndQoL.ManualProbeCapture.ExchangeSummary();
#endif
                    var clock=System.Diagnostics.Stopwatch.StartNew();
                    Check(TryQualifyNativeSelection(new(),ids[..size],"closed",out var bound,out var group)&&bound==null&&!group.AllowFallback,"same-region island stays rejected");
                    Check(calls==1,"one native probe, remaining closed-set probes avoided: "+size+"/"+profile);Restored();
                    Console.WriteLine($"CLOSED SET units={size} profile={profile} fullProbes={calls} ms={clock.Elapsed.TotalMilliseconds:F3}");
#if SERP_DEVDIAG
                    var evidence=BugfixesAndQoL.ManualProbeCapture.ExchangeSummary();
                    Check(evidence.GroundReasons[1]==1&&evidence.GroundReasons[2]==size-1&&evidence.NativeAvoided==size-1&&evidence.RestoreManager==NativeProbeManagerBytes,"ground diagnostics correlate actual probes, hits and full restore bytes");
#endif
                }
                for(int i=1;i<=550;i++)
                {
                    units[i].r_CurrentTilePositionX=10+i%11;units[i].r_CurrentPositionTileId=(uint)(8010+i%11);
                    units[i].r_NextTilePositionX2=30;units[i].r_NextTilePositionY2=20;
                }
                foreach(int size in new[]{100,200,550})
                {
                    fixtureTick++;calls=0;TryQualifyNativeSelection(new(),ids[..size],"spread-moving",out var bound,out _);
                    Check(calls==1&&bound==null,"moving units at different tiles in the same proved set share one negative witness "+size);Restored();
                }
                for(int i=1;i<=550;i++){units[i].r_CurrentTilePositionX=10;units[i].r_CurrentPositionTileId=8010;}
                units[550].r_CurrentTilePositionX=30;fixtureTick++;calls=0;
                Check(TryQualifyNativeSelection(new(),ids,"mixed",out var witness,out var mixed)&&mixed.AllowFallback&&witness.UnitId==550&&calls==2,"reachable later member outside proof still grants cursor");
                units[550].r_CurrentTilePositionX=10;
                foreach(int scenario in new[]{0,1,2,3,4,5,6})
                {
                    fixtureTick++;calls=0;stampEnd=scenario==0?8015:8020;generation=scenario==1?0:scenario==2?32001:1;
                    targetVisited=scenario==3;extraBuilder=scenario==4;builderResult=scenario==5?2:0;
                    if(scenario==6)completedMoatTiles.Add(8010);
                    using(var scope=BeginCursorRejectBatch(true,550))
                    {
                        BeginNativeProbeBatch();try{ProbeNativeCursorPath(1,20,20);ProbeNativeCursorPath(2,20,20);}
                        finally{EndNativeProbeBatch();}
                        Check(calls==2&&scope.Value.GroundCount==0,"partial/invalid generation/target visited/extra builder/positive/moat cannot publish "+scenario);
                    }
                    Restored();completedMoatTiles.Clear();
                }
                stampEnd=8020;generation=32000;targetVisited=extraBuilder=false;builderResult=0;
                fixtureTick++;calls=0;TryQualifyNativeSelection(new(),ids,"generation",out _,out _);Check(calls==1,"last valid generation works before wrap");generation=1;
                fixtureTick++;calls=0;TryQualifyNativeSelection(new(),ids[..15],"small",out _,out _);Check(calls==15,"small selections avoid capture overhead");
                foreach(int change in new[]{0,1,2})
                {
                    fixtureTick++;calls=0;using(var scope=BeginCursorRejectBatch(true,550))
                    {
                        BeginNativeProbeBatch();try
                        {
                            ProbeNativeCursorPath(1,20,20);
                            if(change==0)fixtureTick++;else if(change==1)mapEpoch++;else cursorDiplomacy++;
                            ProbeNativeCursorPath(2,20,20);Check(calls==2&&scope.Value.Disabled&&scope.Value.GroundCount==0,"tick/map/diplomacy invalidates all ground records "+change);
                        }finally{EndNativeProbeBatch();}
                    }
                }
                fixtureTick++;calls=0;using(var scope=BeginCursorRejectBatch(true,550))
                {
                    BeginNativeProbeBatch();try
                    {
                        for(int i=1;i<=6;i++){units[i].r_PathConnectionMode=(ushort)i;ProbeNativeCursorPath(i,20,20);}
                        Check(calls==6&&scope.Value.GroundCount==4&&scope.Value.GroundAttempts==4,"ground proof pool stops at four records and full probes continue");
                        for(int i=1;i<=6;i++)units[i].r_PathConnectionMode=0;
                    }finally{EndNativeProbeBatch();}
                }
                fixtureTick++;calls=0;
                using(var scope=BeginCursorRejectBatch(true,550))
                {
                    BeginNativeProbeBatch();try
                    {
                        throwAfter=true;bool threw=false;try{ProbeNativeCursorPath(1,20,20);}catch(InvalidOperationException e){threw=e.Message=="after-ground-capture";}
                        Check(threw&&scope.Value.GroundCount==0,"exception after capture never publishes pending proof");Restored();throwAfter=false;
                        ProbeNativeCursorPath(2,20,20);Check(calls==2&&scope.Value.GroundCount==1,"probe after exception executes independently");
                        using(var nested=BeginCursorRejectBatch(true,550)){ProbeNativeCursorPath(3,20,20);Check(calls==3&&nested.Value.GroundCount==1,"nested ground batch cannot inherit proof");}
                        ProbeNativeCursorPath(4,20,20);Check(calls==3,"outer proof survives isolated child");
                        units[5].r_PathConnectionMode=2;ProbeNativeCursorPath(5,20,20);Check(calls==4,"connection modes partition ground proofs");units[5].r_PathConnectionMode=0;
                        units[6].r_ControllableForPlayerId=2;ProbeNativeCursorPath(6,20,20);Check(calls==5,"players partition ground proofs");units[6].r_ControllableForPlayerId=1;
                        placementRevision++;ProbeNativeCursorPath(7,20,20);Check(calls==6&&scope.Value.Disabled,"topology change invalidates ground proof");
                    }finally{EndNativeProbeBatch();}
                }
                // A cut imposed by an immutable player mask is part of the graph.
                var gate=new GroundPolicy();EnemyGatePathPolicyBridge.Current=gate;
                nativeMovementMasks[8020]|=4; // boundary to 8021, rejected only by this policy
                fixtureTick++;calls=0;
                using(var scope=BeginCursorRejectBatch(true,550))
                {
                    BeginNativeProbeBatch();try
                    {
                        ProbeNativeCursorPath(1,20,20);ProbeNativeCursorPath(2,20,20);Check(calls==1&&scope.Value.GroundCount==1,"exact active gate snapshot permits closed-set reuse");
                        gate.ScopeAvailable=false;ProbeNativeCursorPath(3,20,20);Check(calls==2,"missing gate scope cannot borrow filtered proof");gate.ScopeAvailable=true;
                        gate.Current=false;ProbeNativeCursorPath(4,20,20);Check(calls==3,"stale publication cannot borrow filtered proof");gate.Current=true;
                        EnemyGatePathPolicyBridge.Current=new GroundPolicy();ProbeNativeCursorPath(5,20,20);Check(calls==4,"new provider identity cannot borrow prior proof");
                    }finally{EndNativeProbeBatch();}
                }
                EnemyGatePathPolicyBridge.Current=null;
                fixtureTick++;calls=0;
                using(var scope=BeginCursorRejectBatch(true,550))
                {
                    BeginNativeProbeBatch();try{foreach(int id in ids)ProbeNativeCursorPath(id,20,20);}
                    finally{EndNativeProbeBatch();}
                    Check(calls==550&&scope.Value.GroundAttempts==4&&scope.Value.GroundCount==0,"unfiltered boundary rejects proof, capture work capped at four attempts");
                }
                nativeMovementMasks[8020]&=unchecked((byte)~4);
                // Immediate target rejection follows native ordering, especially arrival.
                fixtureTick++;calls=0;movementTargetAvailability[16020]=0;ProbeNativeCursorPath(1,20,20);Check(calls==0,"native unavailable target needs no snapshot");movementTargetAvailability[16020]=1;
                fixtureTick++;tileFlags[16020]=0x30;ProbeNativeCursorPath(1,20,20);Check(calls==0,"native universal target flags need no snapshot");
                units[1].r_CurrentTilePositionX=units[1].r_CurrentTilePositionY=20;fixtureTick++;Check(ProbeNativeCursorPath(1,20,20)&&calls==1,"arrival is tested before target flags");units[1].r_CurrentTilePositionX=units[1].r_CurrentTilePositionY=10;tileFlags[16020]=0;
                tileFlags[16020]=0x10000100;APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance.Profiles[26]=PathfindingProfile.Large;fixtureTick++;calls=0;
                ProbeNativeCursorPath(1,20,20);Check(calls==0,"large profile follows native additional target refusal");
                APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance.Profiles[26]=PathfindingProfile.Default;fixtureTick++;
                ProbeNativeCursorPath(1,20,20);Check(calls==1,"default profile is not rejected by large-profile-only flags");tileFlags[16020]=0;
                // Every direction, one-way closures and range errors use the actual validator.
                var proof=new CursorGroundRejectProof();short* grid=(short*)(nativeProbeGrid+NativeProbeGridBytes/2);
                for(int d=0;d<8;d++)
                {
                    new Span<short>(grid,CursorGroundRejectProof.TileCount).Clear();grid[8010]=1;nativeMovementMasks[8010]=(byte)(1<<d);
                    Check(!proof.Capture(grid,1,8010,16020,nativeMovementMasks,nativeDirectionMasks,cursorGroundNeighborOffsets,cursorGroundRows,null),"outgoing edge blocks closure direction "+d);
                    grid[8010+offsets[d]]=1;byte saved=nativeMovementMasks[8010+offsets[d]];nativeMovementMasks[8010+offsets[d]]=0;
                    Check(proof.Capture(grid,1,8010,16020,nativeMovementMasks,nativeDirectionMasks,cursorGroundNeighborOffsets,cursorGroundRows,null),"one-way closed pair valid direction "+d);nativeMovementMasks[8010+offsets[d]]=saved;
                }
                // A separate graph traversal is the oracle: any accepted closed set
                // must make the target unreachable from EVERY member, not just its seed.
                var random=new Random(365);const int vertices=24;int start=8000,target=8000+vertices-1;
                for(int scenario=0;scenario<96;scenario++)
                {
                    new Span<short>(grid,CursorGroundRejectProof.TileCount).Clear();
                    new Span<byte>(nativeMovementMasks,CursorGroundRejectProof.TileCount).Clear();
                    for(int i=0;i<vertices;i++)
                    {
                        grid[start+i]=(short)(i==0||random.Next(2)==0?1:0);
                        if(i+1<vertices&&random.Next(2)==0)nativeMovementMasks[start+i]|=4;
                        if(i>0&&random.Next(2)==0)nativeMovementMasks[start+i]|=64;
                    }
                    grid[target]=0;
                    if(scenario%2==0)
                    {
                        new Span<short>(grid,CursorGroundRejectProof.TileCount).Clear();
                        var proposed=new System.Collections.Generic.Queue<int>();proposed.Enqueue(0);grid[start]=1;
                        while(proposed.Count>0)
                        {
                            int current=proposed.Dequeue();
                            foreach(int next in new[]{current-1,current+1})
                                if(next>=0&&next<vertices&&grid[start+next]==0&&(nativeMovementMasks[start+current]&(next<current?64:4))!=0){grid[start+next]=1;proposed.Enqueue(next);}
                        }
                    }
                    bool accepted=proof.Capture(grid,1,start,target,nativeMovementMasks,nativeDirectionMasks,cursorGroundNeighborOffsets,cursorGroundRows,null);
                    if(!accepted)continue;
                    for(int member=0;member<vertices;member++)
                    {
                        if(!proof.Contains(start+member))continue;
                        var seen=new bool[vertices];var queue=new System.Collections.Generic.Queue<int>();queue.Enqueue(member);seen[member]=true;
                        while(queue.Count>0)
                        {
                            int current=queue.Dequeue();
                            foreach(int next in new[]{current-1,current+1})
                                if(next>=0&&next<vertices&&!seen[next]&&(nativeMovementMasks[start+current]&(next<current?64:4))!=0){seen[next]=true;queue.Enqueue(next);}
                        }
                        Check(!seen[vertices-1],"independent directed graph oracle agrees with accepted negative proof");
                    }
                }
                Console.WriteLine("PASS cursor ground proofs: "+(assertions-before)+" production-path assertions.");
            }
            finally
            {
                EnemyGatePathPolicyBridge.Current=null;APIShared.UnitAccess.Units=null;APIShared.UnitAccess.UnitCapacity=3;GameTileManagerAPI.Instance.MapColumnLookupTable=null;
                NativeMemory.Free(units);NativeMemory.Free(allocation);NativeMemory.Free(nativeProbeGrid);NativeMemory.Free(nativeProbeRectangle);
                NativeMemory.Free(nativeMovementMasks);NativeMemory.Free(nativeDirectionMasks);NativeMemory.Free(cursorGroundNeighborOffsets);NativeMemory.Free(cursorGroundRows);NativeMemory.Free(movementTargetAvailability);NativeMemory.Free(tileFlags);
            }
        }
        private sealed class GroundPolicy : IEnemyGatePathPolicy,IEnemyGateRoutePolicyProvider,IEnemyGateRoutePolicySnapshot
        {
            internal bool ScopeAvailable=true,Current=true;private int depth;
            public bool HasPublishedMask=>true;public int PlayerId=>1;public bool IsCurrent=>Current;
            public bool IsDirectionAllowed(int tile,int direction)=>tile!=8020||direction!=2;
            public object EnterNativeSearch(int p,EnemyGateSearchKind kind){if(!ScopeAvailable)return null;depth++;return this;}
            public void ExitNativeSearch(object s,EnemyGateSearchKind k,bool done,bool success){if(s!=null)depth--;}
            public bool TryCaptureRoutePolicy(int player,out IEnemyGateRoutePolicySnapshot snapshot){snapshot=depth>0&&Current?this:null;return snapshot!=null;}
            // Simulates the already-entered native F4930 scope in the external planner double.
            internal IDisposable Enter()=>ScopeAvailable?new Entry(this):null;
            private sealed class Entry:IDisposable {private readonly GroundPolicy p;internal Entry(GroundPolicy p){this.p=p;p.depth++;}public void Dispose(){p.depth--;}}
        }
    }
}
namespace APIShared
{
    internal enum EnemyGateSearchKind { Builder }
    internal interface IEnemyGatePathPolicy
    {
        bool HasPublishedMask {get;}
        object EnterNativeSearch(int p,EnemyGateSearchKind k);
        void ExitNativeSearch(object scope,EnemyGateSearchKind k,bool done,bool success);
    }
    internal interface IEnemyGateRoutePolicyProvider { bool TryCaptureRoutePolicy(int player,out IEnemyGateRoutePolicySnapshot snapshot); }
    internal interface IEnemyGateRoutePolicySnapshot {int PlayerId {get;}bool IsCurrent {get;}bool IsDirectionAllowed(int tile,int direction);}
    internal interface IEnemyGateRegionPairObserver {void ObserveRegionPair(int p,int s,int t,int m,int v,int r,string source);}
    internal static class EnemyGatePathPolicyBridge {internal static IEnemyGatePathPolicy Current;}
}
namespace SHCDESE.Interop.Enums {internal enum PathfindingProfile {Large=0,Default=1}}
namespace APIShared.ScriptExtenderReplacements
{
    internal sealed class GamePathingManagerAPI
    {
        internal static readonly GamePathingManagerAPI Instance=new();
        internal readonly SHCDESE.Interop.Enums.PathfindingProfile[] Profiles=new SHCDESE.Interop.Enums.PathfindingProfile[90];
        internal bool IsAvailable=>true;
        internal Span<SHCDESE.Interop.Enums.PathfindingProfile> GetUnitTypePathfindingProfiles()=>Profiles;
    }
}
