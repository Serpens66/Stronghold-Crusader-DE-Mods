using System;
using System.Collections.Generic;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Economy;
using APIShared.Simulation;
using APIShared.Units;
using APIShared.WorldMutations;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        // R1 only. R4/8/9 subsequently run naturally; they are not additional
        // rescue cases. The public tree constructor is setup, never a worker,
        // cargo, serial, target, timer, resource or production-state write.
        private sealed class WoodcutterProof
        {
            internal int Hut,Tree,TreeX,TreeY,TreeTile,InitialResource,LastResource,Candidate;
            internal uint CandidateGlobal;
            internal uint HutGlobal,TreeGlobal;
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal long Session,World,Captures,Attempts,Rescues;
            internal bool Active=true,CapacityPending=true,Initialized,Contact,FirstLog,Complete,Retired,DeletionObserved;
            internal bool OriginalJob,Normalized,YieldPending,Noop;
            internal int CapacityWait,OwnCalls,OwnNative1,OwnNative0,ResourceDebits,LogsIn,LogsUsed,PlanksIn,PlanksOut;
            internal int YieldAmount,Deposits,Remaining,LogLines,HiddenProductionTicks;
            internal string Fault;
            internal AllyTransferCapacity Capacity;
            internal readonly Stack<WoodcutterGoodsFrame> Frames=new Stack<WoodcutterGoodsFrame>();
        }
        private sealed class WoodcutterGoodsFrame
        {
            internal int Building,Before,Delta,Cargo;
            internal uint Global;
            internal eGoods Good;
        }
        private WoodcutterProof woodcutterProof;
        private static volatile Fixture woodcutterFixture;
        private static bool woodcutterRegistered;
        private IEnumerable<AutomationStep> AutomaticWoodcutterSteps()
        {yield return new AutomationStep("natural-woodcutter-r1-original-tree-to-physical-planks",TimeSpan.FromSeconds(1200),AutomaticWoodcutter,true);}

        private void ArmWoodcutter()
        {
            if(!woodcutterRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-natural-woodcutter",ObserveWoodcutter,
                    out string reason,order:100),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-woodcutter-original-r1",new WoodcutterMoveObserver(),out string moves,
                    enabled:()=>woodcutterFixture!=null && !woodcutterFixture.stopped && woodcutterFixture.woodcutterProof!=null &&
                        woodcutterFixture.woodcutterProof.Active && !woodcutterFixture.woodcutterProof.Retired),moves);
                woodcutterRegistered=true;
            }
            woodcutterFixture=this;
        }
        private unsafe void SetupWoodcutterWorld()
        {
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Woodcutter setup requires the actual human owner");
            FindSite();var p=woodcutterProof;
            Require(WorldMutationEvents.TryGetCurrentWorld(out p.Session,out p.World) && p.Session==session,
                "Wood fixture setup has no exact own native world");
            // Keep this normal factory's free-placement convention explicit;
            // the tested moat writer below gets no override or suppression.
            CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            p.Hut=CreateBakerPrefab(cx,cy,eMappers.MAPPER_WOODSMAN,eStructs.STRUCT_WOODCUTTERS_HUT);
            Require(BuildingAccess.TryGetById(p.Hut,out GameBuilding* hut,out _) && hut!=null,"New woodsman's hut cannot be resolved");
            p.HutGlobal=hut->r_GlobalId;
            // FlatSquare has prequalified the entire original 25x25 region.
            // A lone real Oak is placed ten tiles from the hut before employee
            // acquisition. We still require native selection of THIS lifetime.
            p.TreeX=cx+10;p.TreeY=cy+3;p.TreeTile=runtime.Native.Tile(p.TreeX,p.TreeY);
            Require(p.TreeTile>=0 && MoatWorkerWithinOwnKeepRange(p.TreeTile),"Dedicated wood tree lies outside the controlled own-Keep world");
            for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
            {
                int tile=runtime.Native.Tile(p.TreeX+dx,p.TreeY+dy);
                Require(tile>=0 && runtime.Native.OrdinaryGround(tile) && runtime.Native.Head(tile)==0 &&
                    GameTileManagerAPI.Instance.GetTileBuildingId(tile)==0 && GameTileManagerAPI.Instance.GetTileVegetationId(tile)==0,
                    "Dedicated Oak envelope is not genuinely free ordinary ground");
            }
            //107E00 excludes growth>=4 even when health/resources are positive.
            //107390 seeds Oak3 progress72; its synchronous105A10 handler only
            // updates display and does not normalize this stage like Apple does.
            long created=GameVegetationManagerAPI.Instance.CreateEx(checked((ushort)p.TreeX),checked((ushort)p.TreeY),VegetationType.OakTree,growthStage:(int)TreeGrowthStage.Stage3);
            Require(created>0 && created<int.MaxValue,"Public Oak constructor did not return a positive native index");
            p.Tree=checked((int)created);
            Require(NativeVegetationAccess.TryGet(p.Tree,out GameVegetation* tree) && tree!=null,
                "Public Oak constructor cannot be resolved in the actual native index domain");
            log("automatic-wood-constructor-readback tree="+p.Tree+"/"+tree->r_GlobalId+" life="+tree->r_AliveState+
                " actualStage="+tree->r_GrowthStage+" actualProgress="+tree->r_GrowthProgress+" health="+tree->r_Health+
                " felled="+tree->r_IsFelled+" resources="+tree->r_ResourceState+" requestedStage=3 expectedProgress=72");
            Require(tree->r_GlobalId!=0 && tree->r_GrowthStage==(uint)TreeGrowthStage.Stage3 && tree->r_GrowthProgress==72 &&
                tree->r_VegetationType==VegetationType.OakTree && tree->r_TilePositionX==p.TreeX && tree->r_TilePositionY==p.TreeY &&
                tree->r_TileId==(uint)p.TreeTile && (tree->r_AliveState==AliveState.NeedsInit || tree->r_AliveState==AliveState.IsAlive) &&
                tree->r_IsFelled==0 && tree->r_Health>0 && tree->r_ResourceState>=3 &&
                GameTileManagerAPI.Instance.GetTileVegetationId(p.TreeTile)==p.Tree,"Public Oak initial lifetime/resource/tile readback differs");
            p.TreeGlobal=tree->r_GlobalId;p.InitialResource=p.LastResource=tree->r_ResourceState;
            Require(millerWorld==null || !millerWorld.Active,"Wood fixture cannot overwrite an active Miller fixture");
            millerWorld=new MillerWorld{Active=false};ExtendMillerStorage();
            ArmWoodcutter();
            log("automatic-wood-world hut="+p.Hut+"/"+p.HutGlobal+" tree="+p.Tree+"/"+p.TreeGlobal+
                " xy="+p.TreeX+","+p.TreeY+" resource="+p.InitialResource+
                " constructorStage=3 actualStage="+tree->r_GrowthStage+" actualProgress="+tree->r_GrowthProgress+
                " selectorGrowthEligible=true naturalEmployee=true manualYield=false rawWorkerWrites=0 humanAdjacencyClaim=false");
        }
        private unsafe GameBuilding* WoodcutterHut()
        {
            var p=woodcutterProof;
            Require(BuildingAccess.TryGetById(p.Hut,out GameBuilding* hut,out _) && hut!=null && hut->r_GlobalId==p.HutGlobal &&
                hut->r_PlayerIdOwner==owner && hut->r_BuildingType==eStructs.STRUCT_WOODCUTTERS_HUT &&
                hut->r_AliveState==AliveState.IsAlive,"Original woodsman's hut lifetime is unavailable");
            return hut;
        }
        private unsafe bool WoodcutterOriginalAlive()
        {
            var p=woodcutterProof;if(p.Actor==null || p.Retired)return false;
            Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.Session && world==p.World,
                "Woodcutter observer crossed its exact world");
            if(!UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) || u==null || u->r_GlobalId!=p.Actor.Global)
            {p.Retired=true;return false;}
            if(u->r_AliveState==AliveState.MarkedForDeletion){p.DeletionObserved=true;p.Retired=true;return false;}
            Require(UnitAccess.IsReallyAlive(u) && u->r_UnitChimp==p.Actor.Type && u->r_ControllableForPlayerId==owner &&
                u->r_LinkedProductionBuildingId==p.Hut && u->r_LinkedProductionBuildingGlobalId==p.HutGlobal,
                "Original woodcutter type/owner/workplace changed");
            Require(runtime.Native.TryTribeGlobal(p.Actor.Tribe,out uint tribeGlobal) && tribeGlobal==p.Before.TribeGlobalId &&
                (p.Actor.Tribe==0 || runtime.Native.JobDword(u,0x93C)==tribeGlobal),"Original woodcutter tribe lifetime changed");
            if(runtime.Native.Word(u,0x9B0)!=0)
            {
                //12BC00 calls196F80 once the third real log is home, then
                // converts it in hidden AI5/6.196FD0 relinks before AI7/8.
                Require(runtime.Native.Word(u,0x9B0)==1 && (u->r_AIState==5 || u->r_AIState==6) && p.LogsIn==3 &&
                    u->r_CurrentHealth==p.Actor.Health && u->r_TribeId==p.Actor.Tribe &&
                    u->r_CurrentTilePositionX==runtime.Native.Word(u,0x992) && u->r_CurrentTilePositionY==runtime.Native.Word(u,0x994) &&
                    u->r_CurrentPositionTileId==(uint)runtime.Native.Tile(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY) &&
                    u->r_PathPlanStateBitFlags==0 && u->r_MovementSubstep==8 &&
                    CountInTileChain((int)u->r_CurrentPositionTileId,p.Actor.Id)==0,
                    "Natural wood hidden production lost its exact home/phase/health/tribe/unlinked pose");
            }
            else CheckActor(p.Actor,true);
            return true;
        }
        private unsafe GameVegetation* WoodcutterOriginalTree()
        {
            var p=woodcutterProof;
            Require(NativeVegetationAccess.TryGet(p.Tree,out GameVegetation* tree) && tree!=null && tree->r_GlobalId==p.TreeGlobal &&
                tree->r_AliveState==AliveState.IsAlive && tree->r_VegetationType==VegetationType.OakTree &&
                tree->r_TilePositionX==p.TreeX && tree->r_TilePositionY==p.TreeY && tree->r_TileId==(uint)p.TreeTile &&
                GameTileManagerAPI.Instance.GetTileVegetationId(p.TreeTile)==p.Tree,"Selected original wood tree lifetime changed");
            return tree;
        }
        private unsafe bool WoodcutterAccessesOpen(int mutationTile)
        {
            var p=woodcutterProof;var hut=WoodcutterHut();
            if(!AppleTreeAccessOpen(hut->r_TileAccessPositionX,hut->r_TileAccessPositionY))return false;
            if(mutationTile==runtime.Native.Tile(hut->r_TileAccessPositionX,hut->r_TileAccessPositionY) || mutationTile==p.TreeTile)return false;
            // Audited E0C20/E0EB0 use the eight radius-one offsets at2D2E50.
            for(int d=0;d<8;d++)
            {
                int x=p.TreeX+Dx[d],y=p.TreeY+Dy[d];
                if(runtime.Native.Tile(x,y)==mutationTile || !AppleTreeAccessOpen(x,y))return false;
            }
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int i=0;i<buildings.Length;i++)
            {
                var b=buildings.GetValuePointer(i);
                if(b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_GOODS_YARD)
                {
                    var point=GameTileManagerAPI.Instance.GetTileVectorFromId(mutationTile);
                    if(Math.Abs((int)point.X-b->r_TileAccessPositionX)<=1 && Math.Abs((int)point.Y-b->r_TileAccessPositionY)<=1)return false;
                }
            }
            return true;
        }
        private unsafe bool FindNaturalWoodcutter()
        {
            var p=woodcutterProof;var hut=WoodcutterHut();int id=DiscoverNaturalWoodcutter();
            if(id==0 || !UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || !UnitAccess.IsReallyAlive(u) ||
                u->r_UnitChimp!=eChimps.CHIMP_TYPE_WOODCUTTER || u->r_ControllableForPlayerId!=owner ||
                u->r_LinkedProductionBuildingId!=p.Hut || u->r_LinkedProductionBuildingGlobalId!=p.HutGlobal || u->r_AIState!=1 ||
                u->r_CarryGoodAmount!=0 || u->r_TargetVegetationId!=p.Tree || runtime.Native.JobDword(u,0x700)!=p.TreeGlobal)return false;
            var before=runtime.SnapshotUnit(id,true,false);
            if(before==null || before.Unsupported!=null || before.Job==null || (before.Flags!=2 && before.Flags!=4) || !before.OrdinaryStep || before.Substep>=8 ||
                !before.HasCapturedVegetation || before.CapturedVegetationGlobal!=p.TreeGlobal ||
                !runtime.Native.OrdinaryGround(before.TileId) || !MoatWorkerSourceEligible(before.NextTileId) ||
                runtime.Native.Head(before.NextTileId)!=0 || before.NextTileId==runtime.Native.Tile(before.RequestedX,before.RequestedY) ||
                !WoodcutterAccessesOpen(before.NextTileId))return false;
            var tree=WoodcutterOriginalTree();
            if(tree->r_IsFelled!=0 || tree->r_ResourceState!=p.InitialResource || tree->r_Health==0 ||
                Math.Abs(before.RequestedX-p.TreeX)>1 || Math.Abs(before.RequestedY-p.TreeY)>1 ||
                before.RequestedX==p.TreeX && before.RequestedY==p.TreeY || !AppleTreeAccessOpen(before.RequestedX,before.RequestedY))return false;
            if(((int*)&hut->r_NullAmount)[(int)eGoods.STORED_WOOD_LOGS]!=0 || ((int*)&hut->r_NullAmount)[(int)eGoods.STORED_WOOD_PLANKS]!=0)return false;
            var world=new NativeWorkerResumeWorld(runtime.Native);
            if(!world.TryCapture(before,out var receipt,out _) || receipt.Bindings.VegetationId!=p.Tree || receipt.Bindings.VegetationGlobal!=p.TreeGlobal)return false;
            p.Before=before;p.Actor=new Actor{Id=id,Global=u->r_GlobalId,Type=u->r_UnitChimp,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,
                X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y};
            Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long generation) && sid==p.Session && sid==session && generation==p.World,
                "Wood R1 source no longer belongs to the exact setup world");
            return true;
        }
        private unsafe int DiscoverNaturalWoodcutter()
        {
            var p=woodcutterProof;
            // Reuse the natural production fixture's actual allocation contract:
            // exact linked workplace + global and type. Do not assume a generic
            // building employee field is this handler's registration slot.
            if(p.Candidate!=0)
            {
                Require(UnitAccess.TryGetById(p.Candidate,out GameUnit* known,out _) && known!=null && known->r_GlobalId==p.CandidateGlobal &&
                    UnitAccess.IsReallyAlive(known) && known->r_UnitChimp==eChimps.CHIMP_TYPE_WOODCUTTER &&
                    known->r_ControllableForPlayerId==owner && known->r_LinkedProductionBuildingId==p.Hut &&
                    known->r_LinkedProductionBuildingGlobalId==p.HutGlobal,"First naturally assigned woodcutter lifetime changed before source admission");
                return p.Candidate;
            }
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
                if(UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null && u->r_UnitChimp==eChimps.CHIMP_TYPE_WOODCUTTER &&
                    u->r_ControllableForPlayerId==owner && u->r_LinkedProductionBuildingId==p.Hut && u->r_LinkedProductionBuildingGlobalId==p.HutGlobal)
                {p.Candidate=id;p.CandidateGlobal=u->r_GlobalId;return id;}
            return 0;
        }
        private unsafe void ApplyWoodcutterNextOnly()
        {
            var p=woodcutterProof;var before=p.Before;var u=Resolve(p.Actor);IntPtr pointer=(IntPtr)u;
            Require(WoodcutterOriginalAlive() && WoodcutterAccessesOpen(before.NextTileId) &&
                u->r_CurrentPositionTileId==(uint)before.TileId && u->r_NextPositionTileId2==(uint)before.NextTileId &&
                u->r_MovementSubstep==before.Substep && before.Job.Matches(u->r_UnitChimp,u->r_AIState,
                    o=>runtime.Native.JobWord((GameUnit*)pointer,o),o=>runtime.Native.JobDword((GameUnit*)pointer,o)),
                "Natural R1 source changed before construction");
            actors.Add(p.Actor);Plugin.SetGeneralFixtureSession(expectedEnabled);var observer=Plugin.GeneralObserver;
            Require(observer!=null && observer.IsActive==expectedEnabled,"Wood source feature setting differs");
            p.Captures=observer.CaptureCount;p.Attempts=observer.AttemptCount;p.Rescues=observer.RescueCount;
            var tiles=GameTileManagerAPI.Instance;
            EditorBrush.Call(tiles.GetTileManager(),owner,before.NextTileId,before.NextY,1,unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require(runtime.Native.OrdinaryGround(before.TileId) && (runtime.Native.Flags(before.NextTileId)&(uint)TilePropertyFlag.IsMoat)!=0 &&
                WoodcutterAccessesOpen(before.NextTileId),"Actual width1 moat did not preserve Current and all original job approaches");
            Require(u->r_CurrentPositionTileId==(uint)before.TileId && u->r_NextPositionTileId2==(uint)before.NextTileId &&
                u->r_MovementSubstep==before.Substep && u->r_TargetTilePositionX2==before.RequestedX && u->r_TargetTilePositionY2==before.RequestedY &&
                before.Job.Matches(u->r_UnitChimp,u->r_AIState,o=>runtime.Native.JobWord((GameUnit*)pointer,o),o=>runtime.Native.JobDword((GameUnit*)pointer,o)),
                "Original construction changed the worker's immutable tree job/pose/goal");
            WoodcutterOriginalTree();
            Require(observer.AttemptCount==p.Attempts && observer.RescueCount==p.Rescues,"Wood rescue ran synchronously inside construction");
            Require(expectedEnabled?observer.CaptureCount>p.Captures && observer.PendingCount>0:observer.CaptureCount==p.Captures && observer.PendingCount==0,
                "Actual source capture does not match the configured setting");
            log("automatic-wood-r1-source unit="+p.Actor.Id+"/"+p.Actor.Global+" tree="+p.Tree+"/"+p.TreeGlobal+
                " serial700="+runtime.Native.JobDword(u,0x700)+" current="+before.X+","+before.Y+" next="+before.NextX+","+before.NextY+
                " ss="+before.Substep+" goal="+before.RequestedX+","+before.RequestedY+" nextOnly=true actualWriter=true directRescue=false");
        }
        private unsafe void ApplyWoodcutterCompletedNoop()
        {
            var p=woodcutterProof;var before=p.Before;var observer=Plugin.GeneralObserver;
            // A new writer can conservatively invalidate older pending geometry.
            // Repeat only AFTER positive completion / bounded OFF observation.
            long captures=observer.CaptureCount,attempts=observer.AttemptCount,rescues=observer.RescueCount;int pending=observer.PendingCount;
            Require((runtime.Native.Flags(before.NextTileId)&(uint)TilePropertyFlag.IsMoat)!=0,"Original moat source changed before final no-op");
            var tiles=GameTileManagerAPI.Instance;
            EditorBrush.Call(tiles.GetTileManager(),owner,before.NextTileId,before.NextY,1,unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require(observer.CaptureCount==captures && observer.PendingCount==pending && observer.AttemptCount==attempts && observer.RescueCount==rescues,
                "Repeated unchanged moat writer created an additional causal capture/recovery");
            p.Noop=true;
            log("automatic-wood-noop completedObservation=true originalMutationUnchanged=true captureDelta=0 attemptDelta=0 rescueDelta=0");
        }
        private sealed class WoodcutterMoveObserver:IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var f=woodcutterFixture;var p=f?.woodcutterProof;
                if(f==null || f.stopped || p==null || !p.Active || p.Retired || p.Actor==null || context==null ||
                    context.UnitId!=p.Actor.Id || context.SessionId!=p.Session || context.WorldGeneration!=p.World)return null;
                try
                {
                    if(!f.WoodcutterOriginalAlive())return null;
                    bool own=context.OriginOwnerGuid==Plugin.Guid;
                    if(own)
                    {
                        Require(f.expectedEnabled && ++p.OwnCalls==1,"Unexpected duplicate own woodcutter continuation");
                        var u=f.Resolve(p.Actor);var b=p.Before;IntPtr pointer=(IntPtr)u;
                        p.Normalized=u->r_PathPlanStateBitFlags==5 && NativeRelocation.IsNormalizedPose(u,(int)u->r_CurrentPositionTileId,f.runtime.Native.JobDword(u,0x740)) &&
                            u->r_CurrentTilePositionX==u->r_NextTilePositionX2 && u->r_CurrentTilePositionY==u->r_NextTilePositionY2 &&
                            (u->r_CurrentPositionTileId==(uint)b.TileId || u->r_CurrentPositionTileId==(uint)b.NextTileId);
                        p.OriginalJob=context.X==b.RequestedX && context.Y==b.RequestedY && context.Extra==0 &&
                            u->r_TargetTilePositionX2==context.X && u->r_TargetTilePositionY2==context.Y &&
                            u->r_TargetVegetationId==p.Tree && f.runtime.Native.JobDword(u,0x700)==p.TreeGlobal &&
                            b.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,o=>f.runtime.Native.JobWord((GameUnit*)pointer,o),
                                o=>f.runtime.Native.JobDword((GameUnit*)pointer,o));
                        f.WoodcutterOriginalTree();Require(p.Normalized && p.OriginalJob,"Own R1 publication lost normalized original tree/serial/job/goal");
                    }
                    return new WoodcutterMoveCompletion(f,p,context,own);
                }
                catch(Exception error){p.Fault=error.Message;return null;}
            }
        }
        private sealed class WoodcutterMoveCompletion:IUnitMoveInvocation
        {
            private readonly Fixture f;private readonly WoodcutterProof p;private readonly UnitMoveContext c;private readonly bool own;private int completed;
            internal WoodcutterMoveCompletion(Fixture f,WoodcutterProof p,UnitMoveContext c,bool own){this.f=f;this.p=p;this.c=c;this.own=own;}
            public void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || result==null || !ReferenceEquals(result.Context,c))return;
                try
                {
                    if(!f.WoodcutterOriginalAlive())return;
                    if(!own)return;
                    Require(result.IsCurrentWorld && result.NativeInvoked && result.NativeReturned && result.Failure==null,
                        "Own R1 publication did not return an actual current-world original");
                    if(result.NativeResult==1)p.OwnNative1++;else if(result.NativeResult==0)p.OwnNative0++;else Require(false,"Unknown own R1 result");
                }
                catch(Exception error){p.Fault=error.Message;}
            }
        }
        private static unsafe void ObserveWoodcutter(int tick)
        {
            var f=woodcutterFixture;var p=f?.woodcutterProof;
            if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null)return;
            try
            {
                if(p.CapacityPending)
                {
                    if(!f.MillerAddedYardsReady()){Require(++p.CapacityWait<=64,"Wood output yards failed native initialization");return;}
                    if(!BuildingAccess.TryGetById(p.Hut,out GameBuilding* hut,out _) || hut==null || hut->r_GlobalId!=p.HutGlobal)
                        Require(false,"Wood hut disappeared before initialization");
                    if(hut->r_AliveState==AliveState.NeedsInit){Require(++p.CapacityWait<=64,"Wood hut failed initialization");return;}
                    Require(NativeVegetationAccess.TryGet(p.Tree,out GameVegetation* waitingTree) && waitingTree!=null && waitingTree->r_GlobalId==p.TreeGlobal,
                        "Dedicated Oak lifetime changed before native initialization");
                    if(waitingTree->r_AliveState==AliveState.NeedsInit){Require(++p.CapacityWait<=64,"Dedicated Oak failed native initialization");return;}
                    f.WoodcutterHut();f.WoodcutterOriginalTree();
                    Require(AllyTransfers.TryGetCapacity(f.owner,eGoods.STORED_WOOD_PLANKS,out p.Capacity,out string reason),reason);
                    Require(p.Capacity.HasStorage && p.Capacity.FreeAmount>=48,"Natural wood output has insufficient real stockpile capacity");
                    p.CapacityPending=false;p.Initialized=true;
                }
                if(p.Actor==null || !f.WoodcutterOriginalAlive())return;
                var u=f.Resolve(p.Actor);
                if(f.runtime.Native.Word(u,0x9B0)==1)p.HiddenProductionTicks++;
                if(!p.FirstLog)
                {
                    var tree=f.WoodcutterOriginalTree();int resource=tree->r_ResourceState;
                    if(resource!=p.LastResource)
                    {
                        Require(resource==p.LastResource-1 && p.ResourceDebits==0 && u->r_TargetVegetationId==p.Tree &&
                            f.runtime.Native.JobDword(u,0x700)==p.TreeGlobal &&
                            Math.Abs(u->r_CurrentTilePositionX-p.TreeX)<3 && Math.Abs(u->r_CurrentTilePositionY-p.TreeY)<3 &&
                            (u->r_AIState==3 || u->r_AIState==4),"First original tree resource decrement lacks actual serial-bound physical processing");
                        p.ResourceDebits++;p.Contact=true;
                        f.log("automatic-wood-original-tree-consumed tick="+tick+" tree="+p.Tree+"/"+p.TreeGlobal+" resource="+p.LastResource+"->"+resource+
                            " worker="+p.Actor.Id+"/"+p.Actor.Global+" ai="+u->r_AIState+" physicalTreeContact=true");
                    }
                    p.LastResource=resource;
                }
                if(p.YieldAmount>0 && p.Deposits==p.YieldAmount && u->r_CarryGoodAmount==0)
                {
                    Require(p.FirstLog && p.Contact && p.LogsIn==3 && p.LogsUsed==3 && p.PlanksIn==3 && p.PlanksOut==3 &&
                        p.Frames.Count==0 && !p.YieldPending,"Full wood delivery lacks exact natural local input/conversion/withdrawal chain");
                    Require(p.HiddenProductionTicks>0,"Wood conversion never observed its actual native hidden workplace phase");p.Complete=true;
                }
            }
            catch(Exception error){p.Fault=error.Message;}
        }
        private unsafe void ObserveWoodcutterGoods(AddGoodToGoodsyardEventArgs args)
        {
            var p=woodcutterProof;
            if(stopped || p==null || !p.Active || p.Actor==null || p.Retired || p.Complete || p.Fault!=null || args==null ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id ||
                args.Good!=eGoods.STORED_WOOD_LOGS && args.Good!=eGoods.STORED_WOOD_PLANKS)return;
            try
            {
                if(!WoodcutterOriginalAlive())return;
                var u=Resolve(p.Actor);bool local=args.BuildingId==p.Hut;
                Require(BuildingAccess.TryGetById(args.BuildingId,out GameBuilding* store,out _) && store!=null &&
                    store->r_GlobalId==unchecked((uint)args.BuildingGlobalId) && store->r_PlayerIdOwner==owner &&
                    store->r_AliveState==AliveState.IsAlive && store->r_BuildingType==(local?eStructs.STRUCT_WOODCUTTERS_HUT:eStructs.STRUCT_GOODS_YARD),
                    "Wood operation lost its exact local/store lifetime");
                int amount=((int*)&store->r_NullAmount)[(int)args.Good];
                if(args.Phase==EventHookPhase.Pre)
                {
                    //12BC00 AI6 ends with B6100(Planks,-3,capacity0,add1):
                    // native capacity0 is an explicit local-buffer clear.
                    bool finalClear=local && args.Good==eGoods.STORED_WOOD_PLANKS && args.AddAmount==-3 && u->r_AIState==6 &&
                        p.PlanksIn==3 && p.LogsUsed==3 && p.PlanksOut==0;
                    Require(args.Add && args.AddAmount!=0 && args.Capacity==(finalClear?0:local?3:48),"Wood native amount/capacity/mutation contract changed");
                    if(local)
                    {
                        Require(store->r_GlobalId==p.HutGlobal && u->r_CurrentTilePositionX==runtime.Native.Word(u,0x992) &&
                            u->r_CurrentTilePositionY==runtime.Native.Word(u,0x994),"Wood local input/production was remote from original home");
                        if(args.Good==eGoods.STORED_WOOD_LOGS && args.AddAmount==1)
                        {
                            Require(u->r_AIState==0 && p.LogsIn<3,"Unexpected local raw-log credit");
                            if(p.LogsIn==0)Require(p.Contact && p.ResourceDebits==1,"First raw log lacks original tree processing");
                        }
                        else if(args.Good==eGoods.STORED_WOOD_LOGS)
                            Require(args.AddAmount==-1 && u->r_AIState==6 && p.LogsUsed<p.LogsIn,"Raw-log consumption is not a real local conversion");
                        else if(args.AddAmount==1)Require(u->r_AIState==5 && p.PlanksIn<3,"Unexpected local plank production");
                        else Require(finalClear,
                            "Wood output withdrawal is not the exact natural three-plank batch");
                    }
                    else Require(args.Good==eGoods.STORED_WOOD_PLANKS && args.AddAmount==1 && u->r_AIState==8 && p.YieldAmount>0 &&
                        u->r_CarryGoodAmount==p.Remaining && p.Remaining>0 && runtime.Native.Word(u,0x98E)==args.BuildingId &&
                        runtime.Native.JobDword(u,0x6F8)==store->r_GlobalId && amount>=0 && amount<48 &&
                        Math.Abs(u->r_CurrentTilePositionX-store->r_TileAccessPositionX)<3 && Math.Abs(u->r_CurrentTilePositionY-store->r_TileAccessPositionY)<3,
                        "Final plank credit lacks its original native cargo/active store/physical contact");
                    p.Frames.Push(new WoodcutterGoodsFrame{Building=args.BuildingId,Global=store->r_GlobalId,Good=args.Good,
                        Before=amount,Delta=args.AddAmount,Cargo=u->r_CarryGoodAmount});
                }
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.Frames.Count>0,"Wood native credit/debit has no matching Pre");var old=p.Frames.Pop();
                    Require(old.Building==args.BuildingId && old.Global==store->r_GlobalId && old.Good==args.Good && old.Delta==args.AddAmount &&
                        amount-old.Before==old.Delta && u->r_CarryGoodAmount==old.Cargo,"Wood actual local/store delta differs from matched original operation");
                    if(local && args.Good==eGoods.STORED_WOOD_LOGS)
                    {if(args.AddAmount==1){p.LogsIn++;p.FirstLog=true;}else p.LogsUsed++;}
                    else if(local){if(args.AddAmount==1)p.PlanksIn++;else p.PlanksOut=3;}
                    else {p.Deposits++;p.Remaining--;}
                    if(p.LogLines++<64)log("automatic-wood-physical-goods good="+args.Good+" delta="+args.AddAmount+" local="+local+
                        " building="+args.BuildingId+"/"+store->r_GlobalId+" actual="+old.Before+"->"+amount+" ai="+u->r_AIState);
                }
            }
            catch(Exception error){p.Fault=error.Message;}
        }
        private unsafe void ObserveWoodcutterYield(UnitCalculateBonusYieldEventArgs args)
        {
            var p=woodcutterProof;
            if(stopped || p==null || !p.Active || p.Actor==null || p.Retired || p.Complete || p.Fault!=null || args==null || args.UnitId!=p.Actor.Id ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            try
            {
                if(!WoodcutterOriginalAlive())return;var u=Resolve(p.Actor);
                Require(u->r_AIState==8 && p.FirstLog && p.LogsIn==3 && p.LogsUsed==3 && p.PlanksIn==3 && p.PlanksOut==3 &&
                    args.GoodAmount==12 && p.YieldAmount==0,"Natural wood yield lacks complete original local production");
                if(args.Phase==EventHookPhase.Pre){Require(!p.YieldPending,"Nested natural wood yield");p.YieldPending=true;}
                else if(args.Phase==EventHookPhase.Post)
                {Require(p.YieldPending && args.ReturnValue>0 && args.ReturnValue<=48,"Natural wood yield did not return a bounded positive batch");
                    p.YieldPending=false;p.YieldAmount=p.Remaining=checked((int)args.ReturnValue);}
            }
            catch(Exception error){p.Fault=error.Message;}
        }
        private unsafe string WoodcutterAcquisitionFacts()
        {
            var p=woodcutterProof;WoodcutterHut();int id=p.Candidate;
            string actor="none";
            if(id>0 && UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null)
            {
                var b=runtime.SnapshotUnit(id,true,false);
                actor=id+"/"+u->r_GlobalId+" ai="+u->r_AIState+" current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+
                    " next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+" ss="+u->r_MovementSubstep+" flags="+u->r_PathPlanStateBitFlags+
                    " selectedTree="+u->r_TargetVegetationId+" serial700="+runtime.Native.JobDword(u,0x700)+
                    " step="+b?.OrdinaryStep+" unsupported="+(b?.Unsupported??"none")+" nextMoat="+(b!=null && MoatWorkerSourceEligible(b.NextTileId))+
                    " goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+
                    " capturedTree="+b?.HasCapturedVegetation+" capturedTreeGlobal="+b?.CapturedVegetationGlobal;
            }
            string tree="missing";
            if(NativeVegetationAccess.TryGet(p.Tree,out GameVegetation* v) && v!=null)
                tree="actualTreeGlobal="+v->r_GlobalId+" life="+v->r_AliveState+" health="+v->r_Health+" felled="+v->r_IsFelled+" resources="+v->r_ResourceState;
            return actor+" expectedTree="+p.Tree+"/"+p.TreeGlobal+" "+tree+" capacity="+p.Capacity?.FreeAmount;
        }
        private IEnumerable<AutomationWait> AutomaticWoodcutter(AutomationRun run)
        {
            woodcutterProof=new WoodcutterProof();var p=woodcutterProof;
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObserveWoodcutterGoods))
            using(var yieldObserver=UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(ObserveWoodcutterYield))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupWoodcutterWorld();});
                Director.instance.SetEngineFrameRate(50);
                for(int wait=0;p.CapacityPending && wait<64;wait++)
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    Require(p.Fault==null,p.Fault);
                }
                Require(!p.CapacityPending && p.Initialized,"Natural wood world failed bounded initialization/capacity admission");
                for(int sample=0;p.Actor==null && sample<4000;sample++)
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,1+(sample&1));
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    run.InSimulation(run.CurrentMission,()=>{Require(p.Fault==null,p.Fault);FindNaturalWoodcutter();
                        if(sample%128==0 || sample==3999)log("automatic-wood-acquisition sample="+sample+" "+WoodcutterAcquisitionFacts());return true;});
                }
                Require(p.Actor!=null,"Natural Woodcutter R1 did not select the exact dedicated Oak on an admitted clear acceptedNext step");
                run.InSimulation(run.CurrentMission,()=>{ApplyWoodcutterNextOnly();return true;});
                Director.instance.SetEngineFrameRate(250);
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                for(int sample=0;sample<(expectedEnabled?6000:32);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{Require(p.Fault==null,p.Fault);
                        if(sample%128==0)log("automatic-wood-progress sample="+sample+" contact="+p.Contact+" logs="+p.LogsIn+
                            " consumed="+p.LogsUsed+" produced="+p.PlanksIn+" withdrawn="+p.PlanksOut+" yield="+p.YieldAmount+" deposits="+p.Deposits);
                        return p.Retired || p.Complete;});
                    if(stop)break;
                }
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                run.InSimulation(run.CurrentMission,()=>{
                    Require(p.Fault==null && p.Frames.Count==0 && !p.YieldPending,"Wood observation incomplete: "+p.Fault);
                    if(expectedEnabled)Require(p.Complete && !p.Retired && p.Contact && p.OriginalJob && p.Normalized && p.OwnCalls==1 && p.OwnNative1==1 &&
                        p.Deposits==p.YieldAmount && p.YieldAmount>0 && WoodcutterOriginalAlive(),"R1 rescue did not complete its original serial-bound tree/log/conversion/physical output chain");
                    else Require(p.OwnCalls==0 && p.OwnNative0==0 && p.OwnNative1==0 &&
                        (WoodcutterOriginalAlive() || p.DeletionObserved),"OFF has own calls or an unobserved original lifetime loss");
                    ApplyWoodcutterCompletedNoop();
                    log("automatic-wood-result enabled="+expectedEnabled+" originalTreeContact="+p.Contact+" originalTree="+p.Tree+"/"+p.TreeGlobal+
                        " originalJob="+p.OriginalJob+" normalized="+p.Normalized+" ownNative1="+p.OwnNative1+" ownNative0="+p.OwnNative0+
                        " logsIn="+p.LogsIn+" logsUsed="+p.LogsUsed+" localPlanks="+p.PlanksIn+" withdrawn="+p.PlanksOut+
                        " naturalYield="+p.YieldAmount+" physicalCredits="+p.Deposits+" complete="+p.Complete+" nativeDeletionObserved="+p.DeletionObserved+
                        " hiddenProductionTicks="+p.HiddenProductionTicks+" noOp=true rescuePhase=1 otherTravelPhasesRescueCovered=false");
                    Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.Session && world==p.World,
                        "Completed wood case no longer owns its original native world");
                    Plugin.SetGeneralFixtureSession(false);Director.instance.SetEngineFrameRate(250);
                    return true;
                });
            }
            finally
            {
                p.Active=false;if(ReferenceEquals(woodcutterFixture,this))woodcutterFixture=null;
                // Finish/DisposeStep rejects InSimulation even in the same world.
                // Abort/end global deactivation belongs to Plugin's existing
                // completed/OnEnd owners. No game/global write from this finally.
            }
        }
    }
}
