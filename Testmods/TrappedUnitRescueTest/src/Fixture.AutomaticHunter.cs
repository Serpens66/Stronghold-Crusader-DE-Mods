using System;
using System.Collections.Generic;
using System.Threading;
using APIShared;
using APIShared.Automation;
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
        private sealed class HunterProof
        {
            internal int Hut,Granary,InitWait,Remaining,Batches,Deposits,Pickups,Yields,HiddenTicks,NoopX,NoopY;
            internal uint HutGlobal,GranaryGlobal,TribeGlobal;
            internal long Session,World;
            internal Actor Actor;internal UnitSnapshot Before;internal AutomaticWorkerMutation Mutation;
            internal bool Active=true,Ready,Mutated,Retired,Complete,YieldPending,OwnResume;
            internal int YieldEpoch,DeliveryStore,DeliveryX,DeliveryY,Handoffs;
            internal uint DeliveryGlobal;internal string Fault,SourceReject;
            internal readonly int[] Deer=new int[3],Credits=new int[2],Prey=new int[2],HuntPrey=new int[2];
            internal readonly uint[] DeerGlobals=new uint[3],PreyGlobals=new uint[2],HuntGlobals=new uint[2];
            internal readonly long[] YieldAmounts=new long[2];
            internal readonly HunterHiddenPose[] PreHide=new HunterHiddenPose[2];
            internal readonly Stack<HunterGoodsFrame> Frames=new Stack<HunterGoodsFrame>();
        }
        private sealed class HunterHiddenPose
        {internal int X,Y,Tile,HomeX,HomeY,GoalX,GoalY;}
        private sealed class HunterGoodsFrame
        {internal int Building,Before,Delta,Cargo;internal uint Global;internal bool Local;}
        private HunterProof hunterProof;
        private static volatile Fixture hunterFixture;private static bool hunterRegistered;
        private IEnumerable<AutomationStep> AutomaticHunterSteps()
        {
            yield return new AutomationStep("natural-hunter-r5-two-physical-meat-batches",TimeSpan.FromSeconds(1200),AutomaticHunter,true);
            yield return new AutomationStep("natural-hunter-settled-real-moat-noop",TimeSpan.FromSeconds(90),HunterNoop,true);
        }
        private void HunterOwnedWorld()
        {
            var p=hunterProof;
            Require(p!=null && WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) &&
                sid==session && sid==p.Session && world==p.World,"Hunter fixture crossed its exact native world");
        }
        private void HunterFault(string reason)
        {var p=hunterProof;if(p!=null && p.Fault==null){p.Fault=reason;log("automatic-hunter-first-failure "+reason);}}
        private void ArmHunter()
        {
            if(!hunterRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-natural-hunter-r5",ObserveHunter,out string ticks,order:100),ticks);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-hunter-original-r5",new HunterMoveObserver(),out string moves,
                    enabled:()=>hunterFixture!=null && !hunterFixture.stopped && hunterFixture.hunterProof!=null &&
                        hunterFixture.hunterProof.Active && hunterFixture.hunterProof.Mutated && !hunterFixture.hunterProof.Retired),moves);
                hunterRegistered=true;
            }
            hunterFixture=this;
        }
        private unsafe GameBuilding* HunterHut()
        {
            var p=hunterProof;
            Require(BuildingAccess.TryGetById(p.Hut,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==p.HutGlobal &&
                b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_HUNTERS_HUT,
                "Original Hunter hut lifetime changed");return b;
        }
        private unsafe void SetupHunter()
        {
            var p=hunterProof;HunterOwnedWorld();
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Hunter fixture requires actual human owner");FindSite();
            CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            p.Granary=CreateBakerPrefab(cx-10,cy,eMappers.MAPPER_GRANARY,eStructs.STRUCT_GRANARY);
            p.Hut=CreateBakerPrefab(cx,cy,eMappers.MAPPER_HUNTER,eStructs.STRUCT_HUNTERS_HUT);
            GameBuilding* hut=null;GameBuilding* granary=null;
            Require(BuildingAccess.TryGetById(p.Hut,out hut,out _) && hut!=null &&
                BuildingAccess.TryGetById(p.Granary,out granary,out _) && granary!=null,"Hunter setup buildings missing");
            p.HutGlobal=hut->r_GlobalId;p.GranaryGlobal=granary->r_GlobalId;var tiles=GameTileManagerAPI.Instance;
            for(int i=0;i<p.Deer.Length;i++)
            {
                int x=cx+10,y=cy-8+i*3,tile=runtime.Native.Tile(x,y);
                Require(tile>=0 && runtime.Native.OrdinaryGround(tile) && tiles.GetTileBuildingId(tile)==0 && runtime.Native.Head(tile)==0,
                    "Neutral Deer spawn tile is not ordinary/free");
                GameUnit* deer=null;int id=checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(0,0,x,y,tiles.GetTileHeight(tile),eChimps.CHIMP_TYPE_DEER));
                Require(id>0 && UnitAccess.TryGetById(id,out deer,out _) && deer!=null && deer->r_GlobalId!=0 &&
                    deer->r_UnitChimp==eChimps.CHIMP_TYPE_DEER && deer->r_ControllableForPlayerId==0 &&
                    (deer->r_AliveState==AliveState.NeedsInit || deer->r_AliveState==AliveState.IsAlive) &&
                    deer->r_CurrentTilePositionX==x && deer->r_CurrentTilePositionY==y,"Public neutral Deer spawn readback failed");
                p.Deer[i]=id;p.DeerGlobals[i]=deer->r_GlobalId;
                log("automatic-hunter-prey-spawn unit="+id+"/"+deer->r_GlobalId+" owner=0 type="+deer->r_UnitChimp+
                    " life="+deer->r_AliveState+" tile="+x+","+y+" naturalHp="+deer->r_CurrentHealth);
            }
            log("automatic-hunter-world hut="+p.Hut+"/"+p.HutGlobal+" granary="+p.Granary+"/"+p.GranaryGlobal+" neutralPrey=3 rawWorkerWrites=false");
        }
        private unsafe bool HunterAlive()
        {
            HunterOwnedWorld();var p=hunterProof;if(p.Actor==null || p.Retired)return false;
            if(!UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) || u==null || u->r_GlobalId!=p.Actor.Global){p.Retired=true;return false;}
            if(u->r_AliveState==AliveState.MarkedForDeletion)
            {if(automaticWorkerMoves!=null)automaticWorkerMoves.DeletionObserved=true;p.Retired=true;return false;}
            var hut=HunterHut();
            Require(UnitAccess.IsReallyAlive(u) && u->r_UnitChimp==eChimps.CHIMP_TYPE_HUNTER && u->r_ControllableForPlayerId==owner &&
                hut->r_WorkerId_1==p.Actor.Id && hut->N0000176A==p.Actor.Global &&
                u->r_LinkedProductionBuildingId==p.Hut && u->r_LinkedProductionBuildingGlobalId==p.HutGlobal &&
                u->r_CurrentHealth==p.Actor.Health && u->r_TribeId==p.Actor.Tribe &&
                runtime.Native.TryTribeGlobal(p.Actor.Tribe,out uint tribe) && tribe==p.TribeGlobal,"Original Hunter identity/HP/tribe/workplace changed");
            int epoch=p.Pickups;
            if(runtime.Native.Word(u,0x9B0)!=0)
            {
                var pose=epoch<2?p.PreHide[epoch]:null;
                Require(pose!=null && runtime.Native.Word(u,0x9B0)==1 && (u->r_AIState==3 || u->r_AIState==109) &&
                    HunterSameSettledPose(u,pose) && CountInTileChain(pose.Tile,p.Actor.Id)==0,
                    "Hunter hidden production lost its observed pre-hide109/saved3 pose/home/goal/unlink proof");
            }
            else
            {
                CheckActor(p.Actor,true);
                // R2 commits109/saved3 and exposes16 real updates before Hide.
                // AI3 exit instead uses109/saved0 and immediately UNHIDES; it
                // must never be mistaken for the incoming production witness.
                if(u->r_AIState==109 && runtime.Native.Word(u,0x91A)==3)
                {
                    Require(epoch<2 && p.Prey[epoch]!=0 && u->r_PathPlanStateBitFlags==0 && u->r_MovementSubstep==8 &&
                        runtime.Native.JobDword(u,0x740)==0,"Hunter pre-hide phase lacks settled original carcass return");
                    if(p.PreHide[epoch]==null)
                    {p.PreHide[epoch]=new HunterHiddenPose{X=u->r_CurrentTilePositionX,Y=u->r_CurrentTilePositionY,
                        Tile=(int)u->r_CurrentPositionTileId,HomeX=runtime.Native.Word(u,0x992),HomeY=runtime.Native.Word(u,0x994),
                        GoalX=u->r_TargetTilePositionX2,GoalY=u->r_TargetTilePositionY2};
                        log("automatic-hunter-prehide epoch="+epoch+" unit="+p.Actor.Id+"/"+p.Actor.Global+
                            " current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+" home="+runtime.Native.Word(u,0x992)+","+runtime.Native.Word(u,0x994)+
                            " ai=109 saved=3 visibleOccupancy=1");}
                    Require(HunterSameSettledPose(u,p.PreHide[epoch]),"Hunter settled pre-hide production pose changed");
                }
            }
            return true;
        }
        private unsafe bool HunterSameSettledPose(GameUnit* u,HunterHiddenPose p)=>
            u->r_PathPlanStateBitFlags==0 && u->r_MovementSubstep==8 && runtime.Native.JobDword(u,0x740)==0 &&
            u->r_CurrentTilePositionX==p.X && u->r_CurrentTilePositionY==p.Y && u->r_CurrentPositionTileId==(uint)p.Tile &&
            p.Tile==runtime.Native.Tile(p.X,p.Y) && runtime.Native.Word(u,0x992)==p.HomeX && runtime.Native.Word(u,0x994)==p.HomeY &&
            u->r_TargetTilePositionX2==p.GoalX && u->r_TargetTilePositionY2==p.GoalY;
        private static unsafe void ObserveHunter(int tick)
        {
            var f=hunterFixture;var p=f?.hunterProof;if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null)return;
            try
            {
                f.HunterOwnedWorld();
                if(!p.Ready)
                {
                    Require(++p.InitWait<=64,"Hunter setup initialization exceeded64 ticks");
                    GameBuilding* h=null;GameBuilding* g=null;
                    Require(BuildingAccess.TryGetById(p.Hut,out h,out _) && h!=null && h->r_GlobalId==p.HutGlobal &&
                        BuildingAccess.TryGetById(p.Granary,out g,out _) && g!=null && g->r_GlobalId==p.GranaryGlobal,
                        "Hunter setup building identity changed");
                    bool ready=h->r_AliveState==AliveState.IsAlive && g->r_AliveState==AliveState.IsAlive;
                    for(int i=0;i<p.Deer.Length;i++)
                    {Require(UnitAccess.TryGetById(p.Deer[i],out GameUnit* d,out _) && d!=null && d->r_GlobalId==p.DeerGlobals[i] &&
                        d->r_UnitChimp==eChimps.CHIMP_TYPE_DEER && d->r_ControllableForPlayerId==0,"Neutral prey lifetime changed during init");
                        ready&=d->r_AliveState==AliveState.IsAlive && d->r_CurrentHealth>0;}
                    if(!ready)return;
                    Require(g->r_CurrentGoodStackAmount<=186,"Hunter setup needs64 actual Granary capacity");p.Ready=true;
                }
                if(p.Actor==null)
                {
                    var h=f.HunterHut();int id=h->r_WorkerId_1;if(id==0)return;
                    Require(UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==h->N0000176A,
                        "Hut employee identity is unresolved");
                    // A natural employee may still await native type conversion.
                    // Bind only its positively completed Hunter/workplace identity.
                    if(!UnitAccess.IsReallyAlive(u) || u->r_UnitChimp!=eChimps.CHIMP_TYPE_HUNTER || u->r_ControllableForPlayerId!=f.owner ||
                        u->r_LinkedProductionBuildingId!=p.Hut || u->r_LinkedProductionBuildingGlobalId!=p.HutGlobal)return;
                    p.Actor=new Actor{Id=id,Global=u->r_GlobalId,Type=u->r_UnitChimp,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,
                        X=u->r_CurrentTilePositionX,Y=u->r_CurrentTilePositionY,SourceX=u->r_CurrentTilePositionX,SourceY=u->r_CurrentTilePositionY};
                    Require(f.runtime.Native.TryTribeGlobal(p.Actor.Tribe,out p.TribeGlobal),"Hunter tribe unavailable");
                    f.log("automatic-hunter-natural-employee unit="+id+"/"+p.Actor.Global+" hut="+p.Hut+"/"+p.HutGlobal);
                }
                if(!f.HunterAlive())return;var actor=f.Resolve(p.Actor);
                if(f.runtime.Native.Word(actor,0x9B0)!=0)p.HiddenTicks++;
                int epoch=p.Pickups;
                if(epoch<2 && (actor->r_AIState==1 || actor->r_AIState==9 || actor->r_AIState==10))
                {
                    int target=f.runtime.Native.Word(actor,0x9F6);uint targetGlobal=f.runtime.Native.JobDword(actor,0x9F8);
                    int at=Array.IndexOf(p.Deer,target);
                    if(at>=0 && p.DeerGlobals[at]==targetGlobal && UnitAccess.TryGetById(target,out GameUnit* live,out _) && live!=null &&
                        live->r_GlobalId==targetGlobal && UnitAccess.IsReallyAlive(live) && live->r_CurrentHealth>0 && f.runtime.Native.Word(live,0x8F8)==0)
                    {p.HuntPrey[epoch]=target;p.HuntGlobals[epoch]=targetGlobal;}
                }
                if(epoch<2 && actor->r_AIState==2 && p.Prey[epoch]==0)
                {
                    int id=f.runtime.Native.Word(actor,0x9F6);uint global=f.runtime.Native.JobDword(actor,0x9F8);int index=Array.IndexOf(p.Deer,id);
                    Require(index>=0 && global==p.DeerGlobals[index] && id==p.HuntPrey[epoch] && global==p.HuntGlobals[epoch] &&
                        UnitAccess.TryGetById(id,out GameUnit* prey,out _) && prey!=null &&
                        prey->r_GlobalId==global && prey->r_UnitChimp==eChimps.CHIMP_TYPE_DEER && prey->r_ControllableForPlayerId==0 &&
                        f.runtime.Native.Word(prey,0x8F8)!=0 && prey->r_CurrentHealth==0,"Natural carcass return is not one of the actual neutral Deer");
                    Require(epoch==0 || global!=p.PreyGlobals[0],"Second hunt reused first corpse");p.Prey[epoch]=id;p.PreyGlobals[epoch]=global;
                    f.log("automatic-hunter-natural-carcass epoch="+epoch+" prey="+id+"/"+global+" ai=2 tick="+tick);
                }
                if(p.Batches==2 && !p.Complete)
                {Require(actor->r_CarryGoodAmount==0 && p.Pickups==2 && p.Yields==2 && p.Credits[0]==6 && p.Credits[1]==6 &&
                    p.PreyGlobals[0]!=0 && p.PreyGlobals[1]!=0 && p.HiddenTicks>0 && !p.YieldPending && p.Frames.Count==0,
                    "Hunter final deposit did not complete two natural chains");p.Complete=true;}
            }
            catch(Exception e){f.HunterFault(e.Message);}
        }
        private unsafe void ObserveHunterGoods(AddGoodToGoodsyardEventArgs a)
        {
            var p=hunterProof;if(stopped || p==null || !p.Active || p.Actor==null || p.Fault!=null || p.Batches>=2 || a==null || a.Good!=eGoods.STORED_FOOD_MEAT)return;
            bool local=a.BuildingId==p.Hut;
            // AE570 is a building update, so local production has no unit context.
            if(!local && GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            try
            {
                Require(HunterAlive(),"Hunter lifetime ended during meat operation");var u=Resolve(p.Actor);
                Require(BuildingAccess.TryGetById(a.BuildingId,out GameBuilding* b,out _) && b!=null &&
                    b->r_GlobalId==unchecked((uint)a.BuildingGlobalId) && b->r_AliveState==AliveState.IsAlive &&
                    b->r_PlayerIdOwner==owner && b->r_BuildingType==(local?eStructs.STRUCT_HUNTERS_HUT:eStructs.STRUCT_GRANARY),
                    "Meat operation lost exact building identity");int amount=checked((int)b->r_MeatAmount);
                if(a.Phase==EventHookPhase.Pre)
                {
                    Require(a.Add,"Meat event changed native add flag");
                    if(local && a.AddAmount==1)
                    {int epoch=p.Pickups;Require(epoch<2 && p.Prey[epoch]!=0 && p.Credits[epoch]<6 && a.Capacity==6 && amount==p.Credits[epoch] &&
                        u->r_AIState==3 && runtime.Native.Word(u,0x9B0)==1 && u->r_CarryGoodAmount==0,
                        "Hut Meat+1 lacks original corpse/hidden processing");}
                    else if(local)
                        Require(a.AddAmount==-6 && a.Capacity==0 && u->r_AIState==4 && u->r_CarryGoodAmount==0 &&
                            p.Pickups<2 && p.Credits[p.Pickups]==6 && amount==6 && p.Batches==p.Pickups,
                            "Hut Meat-6 is not natural AI4 full-buffer pickup");
                    else
                    {
                        Require(a.AddAmount==1 && a.Capacity==250 && u->r_AIState==5 && p.Remaining>0 && u->r_CarryGoodAmount==p.Remaining &&
                            runtime.Native.Word(u,0x98E)==a.BuildingId && runtime.Native.JobDword(u,0x6F8)==b->r_GlobalId &&
                            Math.Abs(u->r_CurrentTilePositionX-b->r_TileAccessPositionX)<3 && Math.Abs(u->r_CurrentTilePositionY-b->r_TileAccessPositionY)<3 &&
                            b->r_CurrentGoodStackAmount<250,"Granary credit lacks R5 cargo/store/global/physical contact/capacity");
                        if(expectedEnabled && p.Mutated && p.Batches==0)Require(p.OwnResume && a.BuildingId==p.DeliveryStore && b->r_GlobalId==p.DeliveryGlobal &&
                            u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY,
                            "First meat batch lacks original owned continuation/native store handoff");
                    }
                    p.Frames.Push(new HunterGoodsFrame{Building=a.BuildingId,Global=b->r_GlobalId,Before=amount,Delta=a.AddAmount,Cargo=u->r_CarryGoodAmount,Local=local});
                }
                else if(a.Phase==EventHookPhase.Post)
                {
                    Require(p.Frames.Count>0,"Hunter goods Post lacks Pre");var old=p.Frames.Pop();
                    Require(old.Building==a.BuildingId && old.Global==b->r_GlobalId && old.Delta==a.AddAmount && old.Local==local &&
                        amount==(a.Capacity==0?0:old.Before+old.Delta) && u->r_CarryGoodAmount==old.Cargo,"Hunter native goods delta/lifetime/cargo changed");
                    if(local){if(a.AddAmount==1)p.Credits[p.Pickups]++;else p.Pickups++;}
                    else {p.Deposits++;if(--p.Remaining==0)p.Batches++;}
                    log("automatic-hunter-native-goods local="+local+" building="+a.BuildingId+"/"+b->r_GlobalId+" delta="+a.AddAmount+
                        " actual="+old.Before+"->"+amount+" credits="+p.Credits[0]+","+p.Credits[1]+" pickups="+p.Pickups+" batches="+p.Batches+" remaining="+p.Remaining);
                }
            }
            catch(Exception e){HunterFault(e.Message);}
        }
        private unsafe void ObserveHunterYield(UnitCalculateBonusYieldEventArgs a)
        {
            var p=hunterProof;if(stopped || p==null || !p.Active || p.Actor==null || p.Fault!=null || a==null || a.UnitId!=p.Actor.Id)return;
            try
            {
                Require(HunterAlive(),"Hunter yield lost original lifetime");var u=Resolve(p.Actor);
                Require(u->r_AIState==5 && u->r_CarryGoodAmount==0 && a.GoodAmount==6 && a.b50PercentBonus==1 && p.Yields<2 &&
                    p.Pickups==p.Yields+1 && p.Credits[p.Yields]==6 && p.Prey[p.Yields]!=0,"Hunter yield is not actual AI5 natural Meat6 branch");
                if(a.Phase==EventHookPhase.Pre){Require(!p.YieldPending,"Nested Hunter yield");p.YieldPending=true;p.YieldEpoch=p.Yields;}
                else if(a.Phase==EventHookPhase.Post)
                {Require(p.YieldPending && p.YieldEpoch==p.Yields && a.ReturnValue>0 && a.ReturnValue<=250,"Hunter natural yield lacks bounded positive result");
                    p.YieldAmounts[p.Yields]=a.ReturnValue;p.Remaining=checked((int)a.ReturnValue);p.Yields++;p.YieldPending=false;
                    log("automatic-hunter-natural-yield epoch="+(p.Yields-1)+" amount="+a.ReturnValue+" prey="+p.Prey[p.Yields-1]+"/"+p.PreyGlobals[p.Yields-1]);}
            }
            catch(Exception e){HunterFault(e.Message);}
        }
        private unsafe bool HunterFutureSafe(UnitSnapshot before,int x,int y)
        {
            var h=HunterHut();var u=Resolve(hunterProof.Actor);
            if(AppleStepContains(before.RequestedX,before.RequestedY,x,y,1) || AppleStepContains(h->r_TileAccessPositionX,h->r_TileAccessPositionY,x,y,1) ||
                AppleStepContains(runtime.Native.Word(u,0x992),runtime.Native.Word(u,0x994),x,y,1))return false;
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int i=0;i<buildings.Length;i++){var b=buildings.GetValuePointer(i);
                if(b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_GRANARY &&
                    AppleStepContains(b->r_TileAccessPositionX,b->r_TileAccessPositionY,x,y,1))return false;}
            return true;
        }
        private bool RejectHunterSource(string reason){hunterProof.SourceReject=reason;return false;}
        private unsafe bool FindHunterSource()
        {
            var p=hunterProof;if(!p.Ready || p.Actor==null)return RejectHunterSource("world-or-natural-employee-pending");
            if(!HunterAlive())return RejectHunterSource("original-life-retired");var u=Resolve(p.Actor);
            if(u->r_AIState!=5 || u->r_CarryGoodAmount==0 || p.Yields!=1 || p.Pickups!=1 || p.Batches!=0 || p.Deposits!=0 || u->r_CarryGoodAmount!=p.Remaining)return RejectHunterSource("not-first-natural-loaded-R5-before-deposit");
            var before=runtime.SnapshotUnit(p.Actor.Id,true,false);
            if(before==null || before.Unsupported!=null || before.Job==null)return RejectHunterSource("snapshot:"+(before?.Unsupported??"missing-job"));
            if(!TryStartedMoatAnchor(before,out int x,out int y))return RejectHunterSource("no-ordinary-started-free-2x2-envelope");
            if(!HunterFutureSafe(before,x,y))return RejectHunterSource("2x2-halo-overlaps-future-home-or-granary-access");
            int store=runtime.Native.Word(u,0x98E);uint global=runtime.Native.JobDword(u,0x6F8);
            if(!BuildingAccess.TryGetById(store,out GameBuilding* b,out _) || b==null || b->r_GlobalId!=global ||
                b->r_AliveState!=AliveState.IsAlive || b->r_PlayerIdOwner!=owner || b->r_BuildingType!=eStructs.STRUCT_GRANARY || b->r_CurrentGoodStackAmount>218)return RejectHunterSource("source-granary-lifetime-or-free32-capacity");
            if(!new NativeWorkerResumeWorld(runtime.Native).TryCapture(before,out var ticket,out string rejected))return RejectHunterSource("native-ticket:"+rejected);
            if(ticket.Bindings.WorkplaceId!=p.Hut || ticket.Bindings.WorkplaceGlobal!=p.HutGlobal || ticket.Bindings.StoreId!=store ||
                ticket.Bindings.StoreGlobal!=global)return RejectHunterSource("ticket-home-store-binding-mismatch");
            p.Before=before;p.NoopX=x;p.NoopY=y;p.DeliveryStore=store;p.DeliveryGlobal=global;p.DeliveryX=before.RequestedX;p.DeliveryY=before.RequestedY;p.SourceReject=null;return true;
        }
        private sealed class HunterMoveObserver:IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext c)
            {
                var f=hunterFixture;var p=f?.hunterProof;
                if(f==null || p==null || c==null || p.Actor==null || c.UnitId!=p.Actor.Id || c.SessionId!=p.Session || c.WorldGeneration!=p.World)return null;
                try
                {
                    Require(f.HunterAlive(),"Hunter move lost original lifetime");var u=f.Resolve(p.Actor);if(u->r_AIState!=5 || p.Batches!=0)return null;
                    bool own=c.OriginOwnerGuid==Plugin.Guid;
                    // OFF classifies native movement/refusal/deletion, without
                    // imposing ON's owned-continuation handoff prerequisite.
                    if(!own && !f.expectedEnabled)return null;
                    Require(c.Extra==0 && u->r_CarryGoodAmount==p.Remaining,"R5 move changed cargo/mode");
                    int store=f.runtime.Native.Word(u,0x98E);uint global=f.runtime.Native.JobDword(u,0x6F8);
                    Require(BuildingAccess.TryGetById(store,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==global &&
                        b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==f.owner && b->r_BuildingType==eStructs.STRUCT_GRANARY,"R5 move lost live Granary");
                    if(own){IntPtr ptr=(IntPtr)u;Require(store==p.DeliveryStore && global==p.DeliveryGlobal && c.X==p.Before.RequestedX && c.Y==p.Before.RequestedY &&
                        p.Before.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,o=>f.runtime.Native.JobWord((GameUnit*)ptr,o),o=>f.runtime.Native.JobDword((GameUnit*)ptr,o)),
                        "Own Hunter move changed original job/store/goal");}
                    else
                    {
                        Require(p.OwnResume && string.IsNullOrEmpty(c.OriginOwnerGuid) && c.ParentInvocationId==0 && p.Handoffs<16 &&
                            c.X==b->r_TileAccessPositionX && c.Y==b->r_TileAccessPositionY && u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY,
                            "Granary handoff lacks original continuation/new access");
                        Require(BuildingAccess.TryGetById(p.DeliveryStore,out GameBuilding* old,out _) && old!=null && old->r_GlobalId==p.DeliveryGlobal &&
                            old->r_AliveState==AliveState.IsAlive && old->r_PlayerIdOwner==f.owner && old->r_BuildingType==eStructs.STRUCT_GRANARY &&
                            (Math.Abs(u->r_CurrentTilePositionX-old->r_TileAccessPositionX)>=3 || Math.Abs(u->r_CurrentTilePositionY-old->r_TileAccessPositionY)>=3 ||
                             old->r_CurrentGoodStackAmount>=250),"Granary handoff lacks native contact/capacity rejection");
                    }
                    return new HunterMoveResult(f,p,c,own,store,global);
                }
                catch(Exception e){f.HunterFault(e.Message);return null;}
            }
        }
        private sealed class HunterMoveResult:IUnitMoveInvocation
        {
            private readonly Fixture f;private readonly HunterProof p;private readonly UnitMoveContext c;private readonly bool own;
            private readonly int store;private readonly uint global;private int done;
            internal HunterMoveResult(Fixture f,HunterProof p,UnitMoveContext c,bool own,int store,uint global)
            {this.f=f;this.p=p;this.c=c;this.own=own;this.store=store;this.global=global;}
            public unsafe void Complete(UnitMoveCompletion r)
            {
                if(Interlocked.Exchange(ref done,1)!=0 || !p.Active || f.stopped || p.Fault!=null)return;
                try
                {
                    Require(r!=null && ReferenceEquals(r.Context,c) && r.IsCurrentWorld && r.NativeInvoked && r.NativeReturned && r.Failure==null && f.HunterAlive(),
                        "Hunter move lacks coherent native completion");var u=f.Resolve(p.Actor);
                    Require(u->r_AIState==5 && u->r_CarryGoodAmount==p.Remaining && f.runtime.Native.Word(u,0x98E)==store && f.runtime.Native.JobDword(u,0x6F8)==global,
                        "Hunter move changed original cargo/store");if(own && r.NativeResult==0)return;
                    Require(r.NativeResult==1 && u->r_TargetTilePositionX2==c.X && u->r_TargetTilePositionY2==c.Y,"Hunter continuation/handoff lacks Native1");
                    if(own)p.OwnResume=true;else p.Handoffs++;p.DeliveryStore=store;p.DeliveryGlobal=global;p.DeliveryX=c.X;p.DeliveryY=c.Y;
                }
                catch(Exception e){f.HunterFault(e.Message);}
            }
        }
        private unsafe string HunterFacts()
        {
            var p=hunterProof;string unit="unassigned";
            if(p.Actor!=null && UnitAccess.TryGetById(p.Actor.Id,out GameUnit* u,out _) && u!=null)
                unit=p.Actor.Id+"/"+u->r_GlobalId+" life="+u->r_AliveState+" ai="+u->r_AIState+" flags="+u->r_PathPlanStateBitFlags+
                    " ss="+u->r_MovementSubstep+" current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+
                    " goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+" cargo="+u->r_CarryGoodAmount;
            return unit+" credits="+p.Credits[0]+","+p.Credits[1]+" pickups="+p.Pickups+" yields="+p.Yields+" batches="+p.Batches+" sourceReject="+p.SourceReject+" fault="+p.Fault;
        }
        private IEnumerable<AutomationWait> AutomaticHunter(AutomationRun run)
        {
            hunterProof=new HunterProof();var p=hunterProof;
            Require(WorldMutationEvents.TryGetCurrentWorld(out p.Session,out p.World) && p.Session==session,"Hunter has no initial native world");
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObserveHunterGoods))
            using(var yields=UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(ObserveHunterYield))
            try
            {
                ArmHunter();Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupHunter();});Director.instance.SetEngineFrameRate(50);
                for(int n=0;n<8000 && p.Before==null;n++)
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    run.InSimulation(run.CurrentMission,()=>{HunterOwnedWorld();Require(p.Fault==null,p.Fault);FindHunterSource();
                        if(n%128==0)log("automatic-hunter-acquire sample="+n+" "+HunterFacts());return true;});
                }
                Require(p.Before!=null,"No natural first R5 ordinary source before any original meat deposit: "+HunterFacts());
                run.InSimulation(run.CurrentMission,()=>{
                    HunterOwnedWorld();Require(HunterAlive() && p.Fault==null && HunterFutureSafe(p.Before,p.NoopX,p.NoopY),p.Fault??"Hunter source changed");
                    actors.Add(p.Actor);p.Mutated=true;p.Mutation=ApplyAutomaticStartedWorkerMoat(p.Actor,p.Before,p.NoopX,p.NoopY);return true;});
                Director.instance.SetEngineFrameRate(250);
                for(int n=0;n<(expectedEnabled?8000:32);n++)
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    bool stop=run.InSimulation(run.CurrentMission,()=>{HunterOwnedWorld();Require(p.Fault==null,p.Fault);
                        if(n%128==0)log("automatic-hunter-progress sample="+n+" "+HunterFacts());return p.Retired || p.Complete;});if(stop)break;
                }
                run.InSimulation(run.CurrentMission,()=>{
                    HunterOwnedWorld();Require(p.Fault==null && p.Frames.Count==0 && !p.YieldPending,p.Fault??"Hunter native event pair incomplete");
                    if(expectedEnabled)Require(p.Complete && p.OwnResume && p.Batches==2 && p.Deposits==p.YieldAmounts[0]+p.YieldAmounts[1],
                        "Hunter did not complete full original and naturally hunted/produced second batch");
                    EndAutomaticWorkerCase(p.Mutation,p.Complete);
                    log("automatic-hunter-result enabled="+expectedEnabled+" rescuePhase=5 fullOriginalBatch="+(p.Batches>=1)+" fullSecondBatch="+p.Complete+
                        " yield0="+p.YieldAmounts[0]+" yield1="+p.YieldAmounts[1]+" physicalCredits="+p.Deposits+" hiddenTicks="+p.HiddenTicks+
                        " prey0="+p.Prey[0]+"/"+p.PreyGlobals[0]+" prey1="+p.Prey[1]+"/"+p.PreyGlobals[1]+" handoffs="+p.Handoffs);return true;});
            }
            finally
            {p.Active=false;if(ReferenceEquals(hunterFixture,this))hunterFixture=null;
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;}
        }
        private uint[] HunterNoopGeometry()
        {
            var p=hunterProof;var t=GameTileManagerAPI.Instance;var result=new uint[150];int i=0;
            for(int y=p.NoopY-2;y<=p.NoopY+2;y++)for(int x=p.NoopX-2;x<=p.NoopX+2;x++)
            {int tile=runtime.Native.Tile(x,y);Require(tile>=0,"Hunter no-op halo outside active map");
                result[i++]=runtime.Native.Flags(tile);result[i++]=t.GetTileHeight(tile);result[i++]=(uint)t.GetTileType(tile);
                result[i++]=runtime.Native.Connections(tile);result[i++]=unchecked((uint)runtime.Native.Building(tile));result[i++]=unchecked((uint)t.GetTileVegetationId(tile));}
            return result;
        }
        private static bool HunterSameGeometry(uint[] a,uint[] b)
        {if(a==null || b==null || a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        private IEnumerable<AutomationWait> HunterNoop(AutomationRun run)
        {
            var p=hunterProof;Require(p!=null && p.Mutated,"Hunter no-op lacks settled original source");uint[] geometry=null;long attempts=0,rescues=0;
            try
            {
                run.InSimulation(run.CurrentMission,()=>{ApplyHunterNoop(out geometry,out attempts,out rescues);return true;});
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                yield return run.WaitForTicks(run.CurrentMission,8);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                run.InSimulation(run.CurrentMission,()=>{
                    HunterOwnedWorld();Require(HunterSameGeometry(geometry,HunterNoopGeometry()) && Plugin.GeneralObserver.AttemptCount==attempts &&
                        Plugin.GeneralObserver.RescueCount==rescues && Plugin.GeneralObserver.PendingCount==0 && automaticWorkerMoves.OwnBegins==0 &&
                        automaticWorkerMoves.Fault==null,"Hunter no-op produced additional recovery");
                    if(AutomaticActorAlive(p.Actor))CheckActor(p.Actor,true);
                    log("automatic-hunter-noop enabled="+expectedEnabled+" actualRepeatedWriter=true geometryUnchanged=true advancedTicks=8 additionalOwnCalls=0");
                    Plugin.SetGeneralFixtureSession(false);return true;});
            }
            finally{if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;}
        }
        private unsafe void ApplyHunterNoop(out uint[] geometry,out long attempts,out long rescues)
        {
            var p=hunterProof;HunterOwnedWorld();Require(p.Fault==null && Plugin.GeneralObserver.PendingCount==0,"Hunter no-op has unsettled original recovery");
            bool alive=AutomaticActorAlive(p.Actor);UnitSnapshot before=null;ushort cargo=0;int ai=0;
            if(alive){CheckActor(p.Actor,true);before=runtime.SnapshotUnit(p.Actor.Id,true,false);var u=Resolve(p.Actor);cargo=u->r_CarryGoodAmount;ai=u->r_AIState;
                BeginAutomaticWorkerMoves(p.Actor,p.Before.RequestedX,p.Before.RequestedY);}
            else {Require(automaticWorkerMoves!=null && automaticWorkerMoves.DeletionObserved,"OFF lifetime disappeared without native deletion");
                BeginAutomaticWorkerMoves(p.Actor,p.Before.RequestedX,p.Before.RequestedY);automaticWorkerMoves.DeletionObserved=true;}
            Plugin.SetGeneralFixtureSession(expectedEnabled);var o=Plugin.GeneralObserver;
            attempts=o.AttemptCount;rescues=o.RescueCount;geometry=HunterNoopGeometry();var tiles=GameTileManagerAPI.Instance;
            for(int y=p.NoopY-1;y<=p.NoopY;y++)for(int x=p.NoopX-1;x<=p.NoopX;x++)
            {int tile=runtime.Native.Tile(x,y);Require((tiles.GetTilePropertyFlag(tile)&TilePropertyFlag.IsMoat)!=0 && runtime.Native.Head(tile)==0,
                "Hunter repeated footprint is not complete empty Moat");}
            EditorBrush.Call(tiles.GetTileManager(),owner,tiles.GetTileId(p.NoopX,p.NoopY),p.NoopY,2,unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require(HunterSameGeometry(geometry,HunterNoopGeometry()) && o.AttemptCount==attempts && o.RescueCount==rescues && o.PendingCount==0,
                "Repeated native Hunter moat changed geometry/evidence");
            if(alive)
            {var after=runtime.SnapshotUnit(p.Actor.Id,true,false);var u=Resolve(p.Actor);IntPtr ptr=(IntPtr)u;
                Require(before!=null && after!=null && before.GlobalId==after.GlobalId && before.X==after.X && before.Y==after.Y &&
                    before.NextX==after.NextX && before.NextY==after.NextY && before.RequestedX==after.RequestedX && before.RequestedY==after.RequestedY &&
                    before.Flags==after.Flags && before.Substep==after.Substep && ai==u->r_AIState && cargo==u->r_CarryGoodAmount &&
                    (before.Job==null || before.Job.Matches(u->r_UnitChimp,u->r_AIState,o2=>runtime.Native.JobWord((GameUnit*)ptr,o2),o2=>runtime.Native.JobDword((GameUnit*)ptr,o2))),
                    "Repeated Hunter writer changed live pose/intent/job/cargo");CheckActor(p.Actor,true);}
        }
    }
}
