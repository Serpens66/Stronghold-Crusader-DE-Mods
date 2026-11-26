using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.WorldMutations;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class Miner1Proof
        {
            internal int Mine,Candidate,AnchorX,AnchorY,HomeX,HomeY,Writes,Produced,BeforeAmount,Delta;
            internal uint MineGlobal,CandidateGlobal;
            internal long Session,World;
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal bool Active,Arrived,Registered,Pending;
            internal string Fault;
        }
        private Miner1Proof miner1Proof;
        private static volatile Fixture miner1Fixture;
        private static bool miner1Registered;
        private IEnumerable<AutomationStep> AutomaticMiner1Steps()
        {yield return new AutomationStep("natural-miner1-r2-original-workplace-and-production",TimeSpan.FromSeconds(1200),AutomaticMiner1,true);}

        private bool IsMiner1World()
        {
            var p=miner1Proof;
            return p!=null && WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) &&
                sid==session && sid==p.Session && world==p.World;
        }
        private void RequireMiner1World()=>Require(IsMiner1World(),"Miner1 fixture crossed its exact original session/world");
        private unsafe GameBuilding* Miner1Mine(bool initializing=false)
        {
            RequireMiner1World();var p=miner1Proof;
            Require(BuildingAccess.TryGetById(p.Mine,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==p.MineGlobal &&
                b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_IRON_MINE &&
                (b->r_AliveState==AliveState.IsAlive || initializing && b->r_AliveState==AliveState.NeedsInit),
                "Original Mine changed lifetime, owner, type or life");
            return b;
        }
        private unsafe void SetupMiner1()
        {
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Miner1 requires the actual human owner");
            FindSite();var p=miner1Proof;
            Require(WorldMutationEvents.TryGetCurrentWorld(out p.Session,out p.World) && p.Session==session,
                "Miner1 setup has no exact current world");
            CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            p.Mine=CreateBakerPrefab(cx,cy,eMappers.MAPPER_IRON_MINE,eStructs.STRUCT_IRON_MINE);
            Require(BuildingAccess.TryGetById(p.Mine,out GameBuilding* b,out _) && b!=null,"Mine constructor returned no building");
            p.MineGlobal=b->r_GlobalId;Miner1Mine(true);
            // The ordinary constructor/employee path is tested. Free placement
            // is explicit setup, not a claim about human mineral placement.
            // Two local Iron writes fit the real Mine buffer (capacity8).
            // External storage/delivery is a separate Miner2 acceptance case.
            log("automatic-miner1-world mine="+p.Mine+"/"+p.MineGlobal+
                " naturalEmployment=true manualWorkerWrites=0 goodsWrites=0 humanMineralPlacementClaim=false");
        }
        private unsafe Miner1Proof FindNaturalMiner1()
        {
            var p=miner1Proof;var mine=Miner1Mine(true);
            if(mine->r_AliveState==AliveState.NeedsInit)return null;
            int id=mine->r_WorkerId_1;
            if(id==0)return null;
            if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || !UnitAccess.IsReallyAlive(u) ||
                u->r_UnitChimp!=eChimps.CHIMP_TYPE_MINER1)return null;
            if(p.Candidate==0){p.Candidate=id;p.CandidateGlobal=u->r_GlobalId;}
            Require(id==p.Candidate && u->r_GlobalId==p.CandidateGlobal && mine->N0000176A==p.CandidateGlobal,
                "First naturally assigned Miner1 changed identity before source qualification");
            Require(u->r_ControllableForPlayerId==owner && u->r_LinkedProductionBuildingId==p.Mine &&
                u->r_LinkedProductionBuildingGlobalId==p.MineGlobal && runtime.Native.Word(u,0x9EE)==0 &&
                runtime.Native.Word(u,0x9F0)==p.Mine,"Natural Miner1 lost its actual employment slot");
            if(u->r_AIState!=2)return null;
            var b=runtime.SnapshotUnit(id,true,false);
            if(b==null || b.Unsupported!=null || b.Job==null || !b.OrdinaryStep || b.Substep>=8 ||
                !TryStartedMoatAnchor(b,out int x,out int y))return null;
            int hx=mine->r_TileAccessPositionX,hy=mine->r_TileAccessPositionY;
            if(AppleStepContains(hx,hy,x,y,1) || AppleStepContains(b.RequestedX,b.RequestedY,x,y,1) ||
                *(ushort*)&mine->N0000176C!=0)return null;
            // Protect all already initialized future storage accesses as well.
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int i=0;i<buildings.Length;i++)
            {
                var other=buildings.GetValuePointer(i);
                if(other->r_AliveState==AliveState.IsAlive && other->r_PlayerIdOwner==owner &&
                    other->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                    AppleStepContains(other->r_TileAccessPositionX,other->r_TileAccessPositionY,x,y,1))return null;
            }
            var world=new NativeWorkerResumeWorld(runtime.Native);
            if(!world.TryCapture(b,out var receipt,out _) || receipt.Bindings.Required!=WorkerExternalBindings.Workplace ||
                receipt.Bindings.WorkplaceId!=p.Mine || receipt.Bindings.WorkplaceGlobal!=p.MineGlobal)return null;
            p.Before=b;p.AnchorX=x;p.AnchorY=y;p.HomeX=hx;p.HomeY=hy;
            p.Actor=new Actor{Id=id,Global=u->r_GlobalId,Type=u->r_UnitChimp,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,
                X=b.X,Y=b.Y,SourceX=b.X,SourceY=b.Y};
            return p;
        }
        private unsafe bool Miner1AtOriginalWorkplace()
        {
            var p=miner1Proof;var mine=Miner1Mine();var u=Resolve(p.Actor);
            Require(UnitAccess.IsReallyAlive(u) && u->r_CurrentHealth==p.Actor.Health && u->r_TribeId==p.Actor.Tribe &&
                runtime.Native.TryTribeGlobal(p.Actor.Tribe,out uint tribeGlobal) && tribeGlobal==p.Before.TribeGlobalId &&
                (p.Actor.Tribe==0 || runtime.Native.JobDword(u,0x93C)==tribeGlobal) &&
                u->r_LinkedProductionBuildingId==p.Mine && u->r_LinkedProductionBuildingGlobalId==p.MineGlobal &&
                runtime.Native.Word(u,0x9EE)==0 && runtime.Native.Word(u,0x9F0)==p.Mine &&
                mine->r_WorkerId_1==p.Actor.Id && mine->N0000176A==p.Actor.Global &&
                mine->r_TileAccessPositionX==p.HomeX && mine->r_TileAccessPositionY==p.HomeY,
                "Original Miner1 life, health, tribe or employment/access changed");
            int hidden=runtime.Native.Word(u,0x9B0);
            if(hidden==0)CheckActor(p.Actor,true);
            if(u->r_AIState!=1 || hidden!=1)return false;
            int tile=runtime.Native.Tile(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY);
            Require(Math.Abs(u->r_CurrentTilePositionX-p.HomeX)<3 && Math.Abs(u->r_CurrentTilePositionY-p.HomeY)<3 &&
                Math.Abs(u->r_CurrentTilePositionX-p.Before.RequestedX)<=1 && Math.Abs(u->r_CurrentTilePositionY-p.Before.RequestedY)<=1 &&
                tile>=0 && u->r_CurrentPositionTileId==(uint)tile && runtime.Native.HasOccupancyCount(tile,p.Actor.Id,0),
                "Hidden Miner1 is not at its original physical workplace with zero visible occupancy");
            p.Arrived=true;
            if(*(ushort*)&mine->N0000176C!=p.Actor.Id)return false;
            p.Registered=true;return true;
        }
        private static void ObserveMiner1Tick(int tick)
        {
            var f=miner1Fixture;var p=f?.miner1Proof;
            if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null)return;
            if(!f.IsMiner1World()){p.Active=false;if(ReferenceEquals(miner1Fixture,f))miner1Fixture=null;return;}
            try
            {
                // AI110 can mark the original actor deleted many updates after
                // its last Move completion. Observe the real marker, never infer it.
                var moves=f.automaticWorkerMoves;
                if(moves!=null && ReferenceEquals(moves.Actor,p.Actor) && moves.Session==p.Session)
                    ObserveAutomaticDeletion(moves);
                if(!f.AutomaticActorAlive(p.Actor))return;
                bool first=!p.Registered;
                if(f.Miner1AtOriginalWorkplace() && first)f.log("automatic-miner1-registered tick="+tick+
                    " unit="+p.Actor.Id+"/"+p.Actor.Global+" mine="+p.Mine+"/"+p.MineGlobal+" slot=0 hiddenByNative=true visibleOccupancy=0");
            }
            catch(Exception e){p.Fault=e.Message;}
        }
        private unsafe void ObserveMiner1Production(AddGoodToGoodsyardEventArgs a)
        {
            var p=miner1Proof;
            if(p==null || !p.Active || p.Fault!=null || !IsMiner1World() || a.BuildingId!=p.Mine ||
                a.Good!=eGoods.STORED_IRON_INGOTS || !a.Add || a.AddAmount<=0 ||
                GameBuildingManagerAPI.Instance.GetCurrentContextBuildingId()!=p.Mine)return;
            try
            {
                if(!AutomaticActorAlive(p.Actor) || !Miner1AtOriginalWorkplace())return;
                var mine=Miner1Mine();
                Require(unchecked((uint)a.BuildingGlobalId)==p.MineGlobal && a.AddAmount==1 && a.Capacity==8,
                    "Mine's actual production changed its lifetime or native Iron+1/capacity8 contract");
                if(a.Phase==EventHookPhase.Pre)
                {Require(!p.Pending,"Nested Mine production lost its matched physical write");p.Pending=true;p.BeforeAmount=(int)mine->r_IronIngotsAmount;p.Delta=a.AddAmount;}
                else
                {
                    Require(p.Pending && a.AddAmount==p.Delta && mine->r_IronIngotsAmount==p.BeforeAmount+p.Delta,
                        "Original Mine production has no matching actual Iron inventory delta");
                    p.Pending=false;p.Writes++;p.Produced+=p.Delta;
                    log("automatic-miner1-production unit="+p.Actor.Id+"/"+p.Actor.Global+" mine="+p.Mine+"/"+p.MineGlobal+
                        " slot=0 iron="+p.BeforeAmount+"->"+mine->r_IronIngotsAmount+" actualWrites="+p.Writes+" workerDeliveryClaim=false");
                }
            }
            catch(Exception e){p.Fault=e.Message;}
        }
        private void ArmMiner1()
        {
            if(!miner1Registered)
            {Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-miner1-workplace",ObserveMiner1Tick,out string reason,order:100),reason);miner1Registered=true;}
            miner1Proof.Active=true;miner1Fixture=this;
        }
        private unsafe void LogMiner1Acquisition(int sample)
        {
            var p=miner1Proof;var b=Miner1Mine(true);int id=b->r_WorkerId_1;string fact="none";
            if(id!=0 && UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null)
                fact=id+"/"+u->r_GlobalId+" type="+u->r_UnitChimp+" life="+u->r_AliveState+" ai="+u->r_AIState+
                    " flags="+u->r_PathPlanStateBitFlags+" ss="+u->r_MovementSubstep+" at="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+
                    " next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+" goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2;
            log("automatic-miner1-acquisition sample="+sample+" mine="+p.Mine+"/"+p.MineGlobal+" facts="+fact);
        }
        private IEnumerable<AutomationWait> AutomaticMiner1(AutomationRun run)
        {
            miner1Proof=new Miner1Proof();var p=miner1Proof;AutomaticWorkerMutation mutation=null;
            using(var production=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObserveMiner1Production))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupMiner1();});
                Director.instance.SetEngineFrameRate(50);
                bool acquired=false;
                for(int sample=0;sample<5000 && !acquired;sample++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{acquired=FindNaturalMiner1()!=null;if(sample%128==0)LogMiner1Acquisition(sample);return true;});
                }
                Require(acquired,"No original naturally employed Miner1 R2 qualified for a legal source outside future access");
                run.InSimulation(run.CurrentMission,()=>{
                    RequireMiner1World();actors.Add(p.Actor);ArmMiner1();
                    mutation=ApplyAutomaticStartedWorkerMoat(p.Actor,p.Before,p.AnchorX,p.AnchorY);return true;
                });
                Director.instance.SetEngineFrameRate(250);
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?8000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,expectedEnabled?4:2);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        RequireMiner1World();if(expectedEnabled)Require(p.Fault==null,p.Fault);
                        if(!AutomaticActorAlive(p.Actor))return true;
                        if(sample%128==0)log("automatic-miner1-progress sample="+sample+" registered="+p.Registered+" writes="+p.Writes+" produced="+p.Produced);
                        return expectedEnabled && p.Registered && p.Writes>=2 && !p.Pending;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    RequireMiner1World();var observer=Plugin.GeneralObserver;bool alive=AutomaticActorAlive(p.Actor);
                    if(expectedEnabled)
                        Require(alive && p.Fault==null && Miner1AtOriginalWorkplace() && p.Arrived && p.Registered &&
                            p.Writes>=2 && p.Produced==p.Writes && !p.Pending && automaticWorkerMoves.Fault==null &&
                            automaticWorkerMoves.OwnSuccesses==1 && observer.RescueCount>mutation.Rescues,
                            "Original rescued Miner1 did not physically arrive, retain employment and participate in two real production writes");
                    else Require(observer.CaptureCount==mutation.Captures && observer.AttemptCount==mutation.Attempts &&
                        observer.RescueCount==mutation.Rescues && automaticWorkerMoves.OwnBegins==0 &&
                        (alive || automaticWorkerMoves.DeletionObserved),"Disabled Miner1 lacks native lifetime evidence or executed own rescue");
                    log("automatic-miner1-result enabled="+expectedEnabled+" unit="+p.Actor.Id+"/"+p.Actor.Global+" originalAlive="+alive+
                        " arrived="+p.Arrived+" originalSlotRegistered="+p.Registered+" productionWrites="+p.Writes+" physicalIron="+p.Produced+
                        " ownNative1="+automaticWorkerMoves.OwnSuccesses+" deletionObserved="+automaticWorkerMoves.DeletionObserved+
                        " hiddenWorkplaceOccupancyExpected=0 workerDeliveryClaim=false fault="+(p.Fault??"none"));
                    p.Active=false;automaticWorkerMoves.Active=false;Plugin.SetGeneralFixtureSession(false);Director.instance.SetEngineFrameRate(250);return true;
                });
            }
            finally
            {
                p.Active=false;if(ReferenceEquals(miner1Fixture,this))miner1Fixture=null;
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;
                // Automation Finish retires its iterator before Dispose. No
                // simulation, speed or global feature writes are legal here.
            }
        }
    }
}
