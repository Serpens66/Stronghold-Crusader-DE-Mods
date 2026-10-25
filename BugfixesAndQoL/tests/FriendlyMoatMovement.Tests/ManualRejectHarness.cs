using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BugfixesAndQoL;
using BugfixesAndQoL.UnitCommands;
using APIShared;

namespace CommandFixture
{
    internal enum CursorPairFallbackKind { UnitApproach, BuildingApproach, DirectTile }
    internal sealed class AttackCursorPairScope
    {
        internal int UnitId, PlayerId=1, TargetX=20, TargetY=20, BuildingId;
        internal CursorPairFallbackKind FallbackKind=CursorPairFallbackKind.DirectTile;
        internal bool GroupCursorAuthorized;
        internal string GroupSelectionSignature;
    }
    internal struct SelectedCursorUnitSnapshot
    {
        internal SelectedCursorUnitSnapshot(int id,int x,int y,int tile,bool dig) { UnitId=id;StartX=x;StartY=y;StartTileId=tile;CanDig=dig; }
        internal int UnitId,StartX,StartY,StartTileId;internal bool CanDig;
    }
    internal struct CursorGroupRouteSummary
    {
        internal string SelectionSignature;
        internal int SelectedUnits,DiggerUnits,LegallyReachableUnits,RepresentativeUnitId,RepresentativeStartX,RepresentativeStartY,RepresentativeStartTileId;
        internal bool RepresentativeCanDig,AllowFallback;
    }
    internal static class EnemyBridgeDiagnosticBridge { internal static void ObserveRegion(int p,int s,int t,int m,int v,int r){} }
    internal unsafe struct ManagerView { internal void* Pointer; }
    internal sealed class GameBuildingManagerAPI { internal static GameBuildingManagerAPI Instance=new();internal unsafe ManagerView GetBuildingManager()=>default; }
    public unsafe partial class UnitCommandPathRuntime
    {
        private object log,activeDirectCursorMove,activeLadderAttackProbe,activeBuildingCursorConnectivity;
        private object regionPairReachabilityDetour=new();
        private Func<IntPtr,int,int,int,int,int> originalRegionPairReachability;
        private Func<IntPtr,int,int,int> originalBuildingCursorReachability=(m,b,id)=>0;
        private int? effectiveRegionOverride;
        private bool badContract;
        private void TryLogDiagnosticFailure(string stage,Exception error){}
        private static AttackCursorPairScope CreateCursorScopeForSnapshot(AttackCursorPairScope t,SelectedCursorUnitSnapshot u)=>new() {
            UnitId=u.UnitId,PlayerId=t.PlayerId,TargetX=t.TargetX,TargetY=t.TargetY,FallbackKind=t.FallbackKind,BuildingId=t.BuildingId };
        private int CallBuildingCursorWithRegions(IntPtr m,int b,int id)=>ProbeNativeCursorPath(id,20,20)?1:0;
        private void ValidateExactBytes(ReadOnlySpan<byte> memory,int rva,byte[] expected,string name) { if(badContract)throw new Exception("contract"); }
        private void ValidateGameUnitFieldOffset(string name,int offset){}
        private int ObserveScopedRegionPairReachabilityCore(IntPtr m,int p,int s,int t,int mode,out int vanilla)
        { vanilla=originalRegionPairReachability(m,p,s,t,mode);return effectiveRegionOverride??vanilla; }
        private void RejectTests()
        {
            int beforeAssertions=assertions;
            GameUnit* units=(GameUnit*)NativeMemory.AllocZeroed(551*(nuint)sizeof(GameUnit));
            byte* allocation=(byte*)NativeMemory.AllocZeroed(NativeProbeManagerBytes+0x18);
            nativeProbeGrid=(byte*)NativeMemory.AllocZeroed(NativeProbeGridBytes);
            nativeProbeRectangle=(byte*)NativeMemory.AllocZeroed(0x20);
            try
            {
                nativePathManager=(IntPtr)(allocation+0x18);nativeUnitManager=(byte*)units;
                APIShared.UnitAccess.Units=units;APIShared.UnitAccess.UnitCapacity=551;
                for(int i=1;i<=550;i++) { units[i].r_GlobalId=(uint)i;units[i].r_ControllableForPlayerId=1;units[i].r_CurrentTilePositionX=10;units[i].r_CurrentTilePositionY=10;units[i].r_CurrentPositionTileId=8010;units[i].r_NextTilePositionX2=21;units[i].r_NextTilePositionY2=10; }
                pathRegionGrid[8010]=1;pathRegionGrid[8021]=3;pathRegionGrid[16020]=2;
                badContract=true;InitializeCursorRejectContract(ReadOnlySpan<byte>.Empty);
                Check(!cursorRejectContractReady,"optional native contract failure retains ordinary path");
                badContract=false;InitializeCursorRejectContract(ReadOnlySpan<byte>.Empty);
                int calls=0,queries=0,reportedModeDelta=0;bool extraQuery=false,throwPlan=false,pathFail=false,noQuery=false;
                originalRegionPairReachability=(m,p,s,t,k)=>{queries++;return s==3?2:0;};
                originalCentralMovementPlan=(m,id,x,y)=> {
                    calls++;GameUnit* u=units+id;
                    ((byte*)u)[sizeof(GameUnit)-1]=71;((byte*)nativePathManager)[0xC4]=72;
                    nativeProbeGrid[NativeProbeGridBytes-1]=73;nativeProbeRectangle[31]=74;((byte*)nativePathManager)[-8]=75;
                    if(u->r_CurrentTilePositionX==x&&u->r_CurrentTilePositionY==y)return 1;
                    int s=unchecked((short)pathRegionGrid[u->r_CurrentTilePositionY*800+u->r_CurrentTilePositionX]),t=unchecked((short)pathRegionGrid[y*800+x]);
                    if(s==t||IsCompletedMoatTile((int)u->r_CurrentPositionTileId))return 1;
                    int r=noQuery?0:ObserveScopedRegionPairReachability(nativePathManager,u->r_ControllableForPlayerId,s,t,unchecked((short)u->r_PathConnectionMode)+reportedModeDelta);
                    if(extraQuery)ObserveScopedRegionPairReachability(nativePathManager,u->r_ControllableForPlayerId,s,t,unchecked((short)u->r_PathConnectionMode));
                    if(throwPlan)throw new InvalidOperationException("planner-original");
                    return !pathFail&&r>0?1:-1;
                };
                var ids=new int[550];for(int i=0;i<ids.Length;i++)ids[i]=i+1;
                void Restored() { Check(((byte*)nativePathManager)[0xC4]==0&&nativeProbeGrid[NativeProbeGridBytes-1]==0&&nativeProbeRectangle[31]==0&&((byte*)nativePathManager)[-8]==0&&((byte*)(units+1))[sizeof(GameUnit)-1]==0,"full real buffers and unit restored"); }
                foreach(int size in new[]{100,200,550})
                {
                    fixtureTick++;calls=queries=0;
#if SERP_DEVDIAG
                    ManualProbeCapture.ExchangeSummary();
#endif
                    Check(TryQualifyNativeSelection(new(),ids[..size],"fixture",out var bound,out var group)&&bound==null&&!group.AllowFallback&&group.SelectedUnits==size,
                        "all unreachable candidates remain rejected");
                    Check(calls==1&&queries==1,"one full native probe for homogeneous rejected selection of "+size);Restored();
#if SERP_DEVDIAG
                    var evidence=ManualProbeCapture.ExchangeSummary();
                    Check(evidence.Probes==1&&evidence.NativeAvoided==size-1&&evidence.RejectLearned==1&&evidence.RejectCandidates==size&&evidence.RestoreManager==NativeProbeManagerBytes,
                        "diagnosis confirms reduced native calls and complete per-executed-probe restoration");
#endif
                }
                // A reachable member in a different component must still be visited.
                units[550].r_CurrentTilePositionX=21;fixtureTick++;calls=queries=0;
                Check(TryQualifyNativeSelection(new(),ids,"mixed",out var witness,out var mixed)&&mixed.AllowFallback&&witness.UnitId==550&&calls==2,
                    "late reachable group member is not hidden by another component's rejection");units[550].r_CurrentTilePositionX=10;Restored();
                foreach(int scenario in new[]{0,1,2,3,4,5,6,7,8,9,10,11})
                {
                    fixtureTick++;calls=queries=0;extraQuery=scenario==0;pathFail=scenario==1;effectiveRegionOverride=scenario==1?1:null;
                    noQuery=scenario==2;activeDirectCursorMove=scenario==3?new object():null;activeLadderAttackProbe=scenario==4?new object():null;
                    if(scenario==5)completedMoatTiles.Add(8010);
                    if(scenario==6)pathRegionGrid[8010]=2;
                    if(scenario==7){units[1].r_CurrentTilePositionX=20;units[1].r_CurrentTilePositionY=20;}
                    if(scenario==8)regionPairReachabilityDetour=null;
                    if(scenario==9)cursorRejectContractReady=false;
                    reportedModeDelta=scenario==10?1:0;
                    activeBuildingCursorConnectivity=scenario==11?new object():null;
                    using(var scope=BeginCursorRejectBatch(true))
                    {
                        BeginNativeProbeBatch();try { ProbeNativeCursorPath(1,20,20);ProbeNativeCursorPath(2,20,20); }
                        finally {EndNativeProbeBatch();}
                        Check(calls==2,"ambiguous/positive/no-query/special/moat/equal/arrived/unavailable case keeps individual probes "+scenario);
                    }
                    completedMoatTiles.Clear();pathRegionGrid[8010]=1;units[1].r_CurrentTilePositionX=units[1].r_CurrentTilePositionY=10;
                    regionPairReachabilityDetour=new object();cursorRejectContractReady=true;Restored();
                }
                extraQuery=noQuery=pathFail=false;reportedModeDelta=0;effectiveRegionOverride=null;activeDirectCursorMove=activeLadderAttackProbe=activeBuildingCursorConnectivity=null;
                foreach(int invalidation in new[]{0,1,2,3})
                {
                    fixtureTick++;calls=0;using(var scope=BeginCursorRejectBatch(true))
                    {
                        BeginNativeProbeBatch();try {
                            ProbeNativeCursorPath(1,20,20);
                            if(invalidation==0)placementRevision++;else if(invalidation==1)mapEpoch++;else if(invalidation==2)cursorDiplomacy++;else fixtureTick++;
                            ProbeNativeCursorPath(2,20,20);
                            Check(calls==2&&scope.Value.Disabled,"revision/epoch/diplomacy/tick change disables reuse "+invalidation);
                        }finally{EndNativeProbeBatch();}
                    }
                }
                fixtureTick++;calls=0;using(var scope=BeginCursorRejectBatch(true))
                {
                    BeginNativeProbeBatch();try {
                        throwPlan=true;bool threw=false;try{ProbeNativeCursorPath(1,20,20);}catch(InvalidOperationException e){threw=e.Message=="planner-original";}
                        Check(threw&&scope.Value.Rejections.Count==0&&!nativeManualProbe,"original exception preserved and cannot publish proof");Restored();
                        throwPlan=false;ProbeNativeCursorPath(2,20,20);Check(calls==2,"probe after exception still runs independently");
                    }finally{EndNativeProbeBatch();}
                }
                fixtureTick++;calls=0;using(var scope=BeginCursorRejectBatch(true))
                {
                    BeginNativeProbeBatch();try {
                        ProbeNativeCursorPath(1,20,20);units[2].r_PathConnectionMode=2;ProbeNativeCursorPath(2,20,20);
                        Check(calls==2,"connection mode partitions proofs");units[2].r_PathConnectionMode=0;
                        units[3].r_ControllableForPlayerId=2;ProbeNativeCursorPath(3,20,20);Check(calls==3,"player partitions proofs");units[3].r_ControllableForPlayerId=1;
                        using(var nested=BeginCursorRejectBatch(true)){Check(!ReferenceEquals(nested.Value,scope.Value)&&nested.Value.Rejections.Count==0,"nested batch starts empty");ProbeNativeCursorPath(4,20,20);Check(calls==4,"nested batch cannot inherit a proof");}
                        ProbeNativeCursorPath(5,20,20);Check(calls==4,"outer proof remains scoped after nesting");
                        scope.Value.Rejections.Clear();
                        for(int i=0;i<128;i++)scope.Value.Rejections[new CursorRegionRejectKey(1,100+i,2,0)]=0;
                        Check(scope.Value.Rejections.Count==128,"bounded proof fixture full");ProbeNativeCursorPath(6,20,20);ProbeNativeCursorPath(7,20,20);
                        Check(calls==6&&scope.Value.Rejections.Count==128,"full proof buffer never grows or fabricates rejection");
                    }finally{EndNativeProbeBatch();}
                }
                var nestedScopes=new CursorRejectScope[9];for(int i=0;i<nestedScopes.Length;i++)nestedScopes[i]=BeginCursorRejectBatch(true);
                Check(nestedScopes[7].Value!=null&&nestedScopes[8].Value==null,"eight eager contexts; exhaustion leaves acceleration unavailable");
                for(int i=nestedScopes.Length-1;i>=0;i--)nestedScopes[i].Dispose();
                Check(cursorRejectBatch==null&&cursorRejectObservation.Batch==null&&nativeProbeBatchDepth==0,"scope and observation cleanup complete");
                fixtureTick++;calls=0;using(var scope=BeginCursorRejectBatch(true))
                {
                    BeginNativeProbeBatch();try {
                        units[1].r_PathConnectionMode=units[2].r_PathConnectionMode=ushort.MaxValue;
                        Check(TryGetCursorRejectKey(units+1,20,20,fixtureTick,out var signed)&&signed.Mode==-1,"mode matches native sign extension");
                        ProbeNativeCursorPath(1,20,20);ProbeNativeCursorPath(2,20,20);Check(calls==1,"signed native mode preserves proof partition");
                        units[1].r_PathConnectionMode=units[2].r_PathConnectionMode=0;
                        pathRegionGrid[16021]=4;ProbeNativeCursorPath(3,21,20);Check(calls==2,"destination region partitions proofs");
                    }finally{EndNativeProbeBatch();}
                }
                fixtureTick++;calls=0;TryQualifyNativeSelection(new(){FallbackKind=CursorPairFallbackKind.BuildingApproach,BuildingId=1},ids[..2],"building",out _,out _);
                Check(calls==2,"building cursor retains full independent native work");
                fixtureTick++;calls=0;ProbeNativeCursorPath(1,20,20);ProbeNativeCursorPath(2,20,20);Check(calls==2,"unscoped cursor retains full probes");
                foreach(var pair in new[]{(0,2),(1,0),(65535,2)})
                {
                    fixtureTick++;calls=queries=0;pathRegionGrid[8010]=(ushort)pair.Item1;pathRegionGrid[16020]=(ushort)pair.Item2;
                    TryQualifyNativeSelection(new(),ids[..100],"signed/zero",out _,out _);
                    Check(calls==1&&queries==1,"observed native rejection also reusable for zero/signed region values "+pair);
                }
                Console.WriteLine("PASS cursor region rejection: "+(assertions-beforeAssertions)+" production-path assertions; 100/200/550 native probes reduced to one per rejected key.");
            }
            finally { NativeMemory.Free(units);NativeMemory.Free(allocation);NativeMemory.Free(nativeProbeGrid);NativeMemory.Free(nativeProbeRectangle);APIShared.UnitAccess.Units=null;APIShared.UnitAccess.UnitCapacity=3; }
        }
    }
}
