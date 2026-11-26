using System;
using System.Collections.Generic;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Economy;
using APIShared.Simulation;
using APIShared.Units;
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
        private sealed class BrewerWorld
        {
            internal int Brewery,InitialCargo,Remaining,Batches,Deposits,Inputs,Yields,SecondCargo,CapacityWait;
            internal uint BreweryGlobal;
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal bool Active=true,CapacityPending,YieldPending,OwnResume,Complete;
            internal int YieldArgument,DeliveryStore,DeliveryX,DeliveryY,Handoffs,HomeInputs,HopsUsed,AleProduced,AleConsumed;
            internal uint DeliveryGlobal;
            internal string Fault;
            internal AllyTransferCapacity Hops,Ale;
            internal readonly Stack<BrewerGoodsFrame> Goods=new Stack<BrewerGoodsFrame>();
        }
        private sealed class BrewerGoodsFrame
        {internal int Store,Amount,Before,Cargo;internal uint Global;internal eGoods Good;}
        private BrewerWorld brewerWorld;
        private static bool brewerCapacityRegistered,brewerMovesRegistered;
        private static volatile Fixture brewerCapacityFixture,brewerMoveFixture;
        private IEnumerable<AutomationStep> AutomaticBrewerSteps()
        {yield return new AutomationStep("automatic-natural-brewer-moat-and-two-batches",TimeSpan.FromSeconds(900),AutomaticBrewer,true);}

        private void RequestBrewerCapacity()
        {
            if(!brewerCapacityRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-brewer-capacity",ReadBrewerCapacity,out string reason),reason);
                brewerCapacityRegistered=true;
            }
            brewerCapacityFixture=this;brewerWorld.CapacityPending=true;
        }
        private static unsafe void ReadBrewerCapacity(int tick)
        {
            var fixture=brewerCapacityFixture;var proof=fixture?.brewerWorld;
            if(fixture==null || fixture.stopped || proof==null || !proof.Active || proof.Fault!=null)return;
            try
            {
                if(proof.Batches==2 && !proof.Complete)
                {
                    // R8 B6100 Post precedes139950's final cargo decrement.
                    // Latch the actual next-tick readback before a third natural
                    // input can make a GUI-paced poll miss the completed cycle.
                    fixture.ValidateBrewer(proof.Actor);
                    Require(fixture.Resolve(proof.Actor)->r_CarryGoodAmount==0 && proof.Inputs==1 && proof.Yields==1 &&
                        proof.HomeInputs==1 && proof.HopsUsed==1 && proof.AleProduced==1 && proof.AleConsumed==1,
                        "Brewer final Ale write did not leave its exact completed cargo/cycle");
                    proof.Complete=true;
                    fixture.log("automatic-brewer-complete-readback tick="+tick+" unit="+proof.Actor.Id+"/"+proof.Actor.Global+" cargo=0 batches=2");
                }
                if(!proof.CapacityPending)return;
                // Reuse the proven dedicated-yard initialization/lifetime reader.
                if(!fixture.MillerAddedYardsReady())
                {Require(++proof.CapacityWait<=64,"Brewer ingredient yards did not initialize");return;}
                Require(AllyTransfers.TryGetCapacity(fixture.owner,eGoods.STORED_RAW_HOPS,out proof.Hops,out string hopsReason),hopsReason);
                Require(AllyTransfers.TryGetCapacity(fixture.owner,eGoods.STORED_FOOD_ALE,out proof.Ale,out string aleReason),aleReason);
                proof.CapacityPending=false;
                fixture.log("automatic-brewer-capacity tick="+tick+" hopsFree="+proof.Hops.FreeAmount+" aleFree="+proof.Ale.FreeAmount);
            }
            catch(Exception error){proof.Fault=error.Message;proof.CapacityPending=false;}
        }
        private unsafe void SetupBrewerWorld()
        {
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Brewer fixture requires the actual human owner");
            FindSite();
            CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            brewerWorld.Brewery=CreateBakerPrefab(cx,cy,eMappers.MAPPER_BREWER,eStructs.STRUCT_BREWERS_WORKSHOP);
            Require(BuildingAccess.TryGetById(brewerWorld.Brewery,out GameBuilding* brewery,out _) && brewery!=null,
                "Created Brewery lifetime cannot be read back");brewerWorld.BreweryGlobal=brewery->r_GlobalId;
            // Existing Miller helper holds only the dedicated yard identities here:
            // inactive, no mill or Miller observer/capacity request is created.
            Require(millerWorld==null || !millerWorld.Active,"Brewer cannot overwrite an active Miller fixture");
            millerWorld=new MillerWorld{Active=false};ExtendMillerStorage();
            RequestBrewerCapacity();
            log("automatic-brewer-world brewery="+brewerWorld.Brewery+"/"+brewerWorld.BreweryGlobal+
                " naturalEmployees=true rawWorkerWrites=false");
        }
        private unsafe GameBuilding* ValidateBrewer(Actor actor)
        {
            var unit=Resolve(actor);var proof=brewerWorld;GameBuilding* brewery=null;
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_UnitChimp==eChimps.CHIMP_TYPE_BREWER &&
                unit->r_ControllableForPlayerId==owner && unit->r_LinkedProductionBuildingId==proof.Brewery &&
                unit->r_LinkedProductionBuildingGlobalId==proof.BreweryGlobal &&
                BuildingAccess.TryGetById(proof.Brewery,out brewery,out _) && brewery!=null &&
                brewery->r_GlobalId==proof.BreweryGlobal && brewery->r_AliveState==AliveState.IsAlive &&
                brewery->r_PlayerIdOwner==owner && brewery->r_BuildingType==eStructs.STRUCT_BREWERS_WORKSHOP,
                "Original Brewer/workplace lifetime changed");
            return brewery;
        }
        private unsafe bool BrewerFutureAccessSafe(UnitSnapshot before,GameBuilding* brewery,int x,int y)
        {
            if(AppleStepContains(brewery->r_TileAccessPositionX,brewery->r_TileAccessPositionY,x,y,1) ||
                AppleStepContains(runtime.Native.Word(Resolve(brewerWorld.Actor),0x992),runtime.Native.Word(Resolve(brewerWorld.Actor),0x994),x,y,1) ||
                AppleStepContains(before.RequestedX,before.RequestedY,x,y,1))return false;
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int index=0;index<buildings.Length;index++)
            {
                var b=buildings.GetValuePointer(index);
                if(b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner &&
                    b->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                    AppleStepContains(b->r_TileAccessPositionX,b->r_TileAccessPositionY,x,y,1))return false;
            }
            return true;
        }
        private unsafe Actor FindNaturalBrewer()=>FindNaturalBrewer(out _,out _);
        private unsafe Actor FindNaturalBrewer(out string rejection,out UnitSnapshot rejectedBefore)
        {
            rejection="no-live-owned-linked-loaded-brewer-r8";rejectedBefore=null;
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=eChimps.CHIMP_TYPE_BREWER ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=brewerWorld.Brewery ||
                    u->r_LinkedProductionBuildingGlobalId!=brewerWorld.BreweryGlobal || u->r_AIState!=8 || u->r_CarryGoodAmount==0)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                rejectedBefore=before;
                if(before==null){rejection="snapshot-missing";continue;}
                if(before.Unsupported!=null){rejection="snapshot-unsupported:"+before.Unsupported;continue;}
                if(before.Job==null){rejection="snapshot-missing-typed-job";continue;}
                if(!before.OrdinaryStep || before.Substep>=8){rejection="not-started-ordinary-step";continue;}
                if(!TryStartedMoatAnchor(before,out int x,out int y)){rejection="started-moat-anchor-rejected";continue;}
                var actor=new Actor{Id=id,Global=u->r_GlobalId,Type=u->r_UnitChimp,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,
                    X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y};
                brewerWorld.Actor=actor;
                if(!BrewerFutureAccessSafe(before,ValidateBrewer(actor),x,y)){rejection="protected-future-access";brewerWorld.Actor=null;continue;}
                int store=runtime.Native.Word(u,0x98E);
                if(!BuildingAccess.TryGetById(store,out GameBuilding* goodsyard,out _) || goodsyard==null ||
                    goodsyard->r_GlobalId!=runtime.Native.JobDword(u,0x6F8) || goodsyard->r_AliveState!=AliveState.IsAlive ||
                    goodsyard->r_PlayerIdOwner!=owner || goodsyard->r_BuildingType!=eStructs.STRUCT_GOODS_YARD)
                {rejection="selected-store-lifetime-owner-type";brewerWorld.Actor=null;continue;}
                var brewery=ValidateBrewer(actor);int* local=(int*)&brewery->r_NullAmount;
                if(local[(int)eGoods.STORED_RAW_HOPS]!=0 || local[(int)eGoods.STORED_FOOD_ALE]!=0)
                {rejection="brewery-local-input-or-output-not-zero";brewerWorld.Actor=null;continue;}
                brewerWorld.Before=before;return actor;
            }
            return null;
        }
        private void ArmBrewerMoves()
        {
            if(!brewerMovesRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-brewer-output",new BrewerMoveObserver(),out string reason,
                    order:200,enabled:()=>brewerMoveFixture!=null && !brewerMoveFixture.stopped && brewerMoveFixture.expectedEnabled &&
                        brewerMoveFixture.brewerWorld!=null && brewerMoveFixture.brewerWorld.Active && brewerMoveFixture.brewerWorld.Actor!=null &&
                        brewerMoveFixture.brewerWorld.Before!=null && brewerMoveFixture.brewerWorld.Batches==0 && brewerMoveFixture.brewerWorld.Fault==null),reason);
                brewerMovesRegistered=true;
            }
            brewerMoveFixture=this;
        }
        private sealed class BrewerMoveObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var f=brewerMoveFixture;var p=f?.brewerWorld;
                if(f==null || p?.Actor==null || context==null || context.SessionId!=f.session || context.UnitId!=p.Actor.Id)return null;
                try
                {
                    f.ValidateBrewer(p.Actor);var u=f.Resolve(p.Actor);bool own=context.OriginOwnerGuid==Plugin.Guid;
                    Require(u->r_AIState==8 && u->r_CarryGoodAmount==p.Remaining && context.Extra==0,
                        "Brewer output move changed phase/cargo");
                    int store=f.runtime.Native.Word(u,0x98E);uint global=f.runtime.Native.JobDword(u,0x6F8);
                    Require(BuildingAccess.TryGetById(store,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==global &&
                        b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==f.owner && b->r_BuildingType==eStructs.STRUCT_GOODS_YARD,
                        "Brewer output move lost its bound goodsyard");
                    if(own)
                    {
                        IntPtr pointer=(IntPtr)u;
                        Require(store==p.DeliveryStore && global==p.DeliveryGlobal && context.X==p.Before.RequestedX && context.Y==p.Before.RequestedY &&
                            u->r_TargetTilePositionX2==context.X && u->r_TargetTilePositionY2==context.Y &&
                            p.Before.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,
                                o=>f.runtime.Native.JobWord((GameUnit*)pointer,o),o=>f.runtime.Native.JobDword((GameUnit*)pointer,o)),
                            "Owned Brewer continuation changed original job/binding/goal");
                    }
                    else
                    {
                        Require(p.OwnResume && p.Handoffs<16 && string.IsNullOrEmpty(context.OriginOwnerGuid) && context.ParentInvocationId==0 &&
                            u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY &&
                            context.X==b->r_TileAccessPositionX && context.Y==b->r_TileAccessPositionY,
                            "Brewer goodsyard reselection lacks the original owned resume and exact new access");
                        Require(BuildingAccess.TryGetById(p.DeliveryStore,out GameBuilding* old,out _) && old!=null &&
                            old->r_GlobalId==p.DeliveryGlobal && old->r_AliveState==AliveState.IsAlive && old->r_PlayerIdOwner==f.owner &&
                            old->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                            (Math.Abs(u->r_CurrentTilePositionX-old->r_TileAccessPositionX)>=3 ||
                             Math.Abs(u->r_CurrentTilePositionY-old->r_TileAccessPositionY)>=3 || !f.BrewerStoreCanReceiveAle(u,old)),
                            "Brewer changed goodsyard without native C9080/BD470 rejection");
                    }
                    return new BrewerMoveCompletion(f,p,context,own,store,global);
                }
                catch(Exception error){f.BrewerFault(error.Message);return null;}
            }
        }
        private sealed class BrewerMoveCompletion : IUnitMoveInvocation
        {
            private readonly Fixture f;private readonly BrewerWorld p;private readonly UnitMoveContext c;
            private readonly bool own;private readonly int store;private readonly uint global;private int completed;
            internal BrewerMoveCompletion(Fixture f,BrewerWorld p,UnitMoveContext c,bool own,int store,uint global)
            {this.f=f;this.p=p;this.c=c;this.own=own;this.store=store;this.global=global;}
            public unsafe void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || !p.Active || f.stopped || p.Fault!=null)return;
                try
                {
                    Require(result!=null && ReferenceEquals(result.Context,c) && result.IsCurrentWorld && result.NativeInvoked &&
                        result.NativeReturned && result.Failure==null,"Brewer output move has no current native result");
                    f.ValidateBrewer(p.Actor);var u=f.Resolve(p.Actor);
                    Require(u->r_AIState==8 && u->r_CarryGoodAmount==p.Remaining && f.runtime.Native.Word(u,0x98E)==store &&
                        f.runtime.Native.JobDword(u,0x6F8)==global,"Brewer native output move changed original cargo/binding");
                    f.log("automatic-brewer-output-move own="+own+" actualResult="+result.NativeResult+" store="+store+"/"+global+" goal="+c.X+","+c.Y);
                    if(own && result.NativeResult==0)return;
                    Require(result.NativeResult==1 && u->r_TargetTilePositionX2==c.X && u->r_TargetTilePositionY2==c.Y,
                        "Brewer output handoff lacks an actual successful native route");
                    if(own)p.OwnResume=true;else p.Handoffs++;
                    p.DeliveryStore=store;p.DeliveryGlobal=global;p.DeliveryX=c.X;p.DeliveryY=c.Y;
                }
                catch(Exception error){f.BrewerFault(error.Message);}
            }
        }
        private void BrewerFault(string reason)
        {if(brewerWorld!=null && brewerWorld.Fault==null){brewerWorld.Fault=reason;log("automatic-brewer-first-failure "+reason);}}
        private unsafe bool BrewerStoreCanReceiveAle(GameUnit* unit,GameBuilding* store)
        {
            if(Math.Abs(unit->r_CurrentTilePositionX-store->r_TileAccessPositionX)>=3 ||
                Math.Abs(unit->r_CurrentTilePositionY-store->r_TileAccessPositionY)>=3)return false;
            int* amounts=(int*)&store->r_NullAmount;
            for(int good=(int)eGoods.STORED_WOOD_LOGS;good<(int)eGoods.Count;good++)
                if(good!=(int)eGoods.STORED_FOOD_ALE && amounts[good]!=0)return false;
            return amounts[(int)eGoods.STORED_FOOD_ALE]<16;
        }
        private unsafe void ObserveBrewerGoods(AddGoodToGoodsyardEventArgs args)
        {
            var p=brewerWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id ||
                args.Good!=eGoods.STORED_RAW_HOPS && args.Good!=eGoods.STORED_FOOD_ALE)return;
            string frame="phase="+args.Phase+" unit="+p.Actor.Id+"/"+p.Actor.Global+" building="+args.BuildingId+"/"+args.BuildingGlobalId+
                " good="+args.Good+" delta="+args.AddAmount+" capacity="+args.Capacity+" add="+args.Add;
            try
            {
                log("automatic-brewer-goods-frame "+frame);var brewery=ValidateBrewer(p.Actor);var u=Resolve(p.Actor);
                bool local=args.BuildingId==p.Brewery,ale=args.Good==eGoods.STORED_FOOD_ALE;
                Require(BuildingAccess.TryGetById(args.BuildingId,out GameBuilding* store,out _) && store!=null &&
                    store->r_GlobalId==unchecked((uint)args.BuildingGlobalId) && store->r_AliveState==AliveState.IsAlive &&
                    store->r_PlayerIdOwner==owner && store->r_BuildingType==(local?eStructs.STRUCT_BREWERS_WORKSHOP:eStructs.STRUCT_GOODS_YARD),
                    "Brewer goods operation lacks its exact original building lifetime");
                if(local)
                    Require(store->r_GlobalId==p.BreweryGlobal && u->r_CurrentTilePositionX==runtime.Native.Word(u,0x992) &&
                        u->r_CurrentTilePositionY==runtime.Native.Word(u,0x994),"Brewer local processing is not physically at its native home");
                else
                    Require(runtime.Native.Word(u,0x98E)==args.BuildingId && runtime.Native.JobDword(u,0x6F8)==store->r_GlobalId &&
                        Math.Abs(u->r_CurrentTilePositionX-store->r_TileAccessPositionX)<3 && Math.Abs(u->r_CurrentTilePositionY-store->r_TileAccessPositionY)<3,
                        "Brewer store operation lacks exact active binding and C9080 physical contact");
                int amount=((int*)&store->r_NullAmount)[(int)args.Good];
                if(args.Phase==EventHookPhase.Pre)
                {
                    Require(args.Add && args.Capacity==16,"Brewer operation is not the native write/capacity contract");
                    if(!local && ale)
                    {
                        Require(args.AddAmount==1 && u->r_AIState==8 && u->r_CarryGoodAmount>0,"Brewer output is not the natural R8 Ale credit");
                        if(p.Remaining==0)
                        {
                            Require(p.Batches==1 && p.Inputs==1 && p.HomeInputs==1 && p.HopsUsed==1 && p.AleProduced==1 && p.AleConsumed==1 &&
                                p.Yields==1 && !p.YieldPending,"Brewer second output lacks its exact fresh input/local production chain");
                            p.SecondCargo=p.Remaining=u->r_CarryGoodAmount;
                        }
                        Require(u->r_CarryGoodAmount==p.Remaining,"Brewer output cargo differs from the original complete batch");
                        if(expectedEnabled && p.Batches==0)Require(p.OwnResume && args.BuildingId==p.DeliveryStore &&
                            store->r_GlobalId==p.DeliveryGlobal && u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY,
                            "First Brewer batch lacks its original owned resume and native output handoff");
                    }
                    else
                    {
                        Require(p.Batches==1 && p.Remaining==0,"Brewer input/processing occurred before its complete first output");
                        if(!local)
                            Require(!ale && args.AddAmount==-1 && u->r_AIState==3 && u->r_CarryGoodAmount==1 && p.Inputs==0,
                                "Brewer fresh Hops pickup is not its committed R3 single debit");
                        else if(!ale && args.AddAmount==1)
                            Require(u->r_AIState==3 && u->r_CarryGoodAmount==1 && p.Inputs==1 && p.HomeInputs==0,
                                "Brewer home Hops credit does not follow its fresh original pickup");
                        else if(!ale && args.AddAmount==-1)
                            Require(u->r_AIState==6 && u->r_CarryGoodAmount==0 && p.HomeInputs==1 && p.HopsUsed==0 && p.AleProduced==0,
                                "Brewer local Hops debit is not its first natural processing input");
                        else if(ale && args.AddAmount==1)
                            Require(u->r_AIState==5 && u->r_CarryGoodAmount==0 && p.HopsUsed==1 && p.AleProduced==0,
                                "Brewer local Ale credit lacks the consumed original Hops");
                        else
                            Require(ale && args.AddAmount==-1 && u->r_AIState==6 && u->r_CarryGoodAmount==0 && p.AleProduced==1 && p.AleConsumed==0,
                                "Brewer output withdrawal lacks its actual freshly produced Ale");
                    }
                    p.Goods.Push(new BrewerGoodsFrame{Store=args.BuildingId,Global=store->r_GlobalId,Good=args.Good,
                        Amount=args.AddAmount,Before=amount,Cargo=u->r_CarryGoodAmount});
                }
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.Goods.Count>0,"Brewer goods Post lacks matching Pre");var old=p.Goods.Pop();
                    Require(old.Store==args.BuildingId && old.Global==store->r_GlobalId && old.Good==args.Good &&
                        old.Amount==args.AddAmount && amount-old.Before==old.Amount && u->r_CarryGoodAmount==old.Cargo,
                        "Brewer physical goods delta/identity/cargo changed across native operation");
                    if(!local && ale){p.Deposits++;if(--p.Remaining==0)p.Batches++;}
                    else if(!local)p.Inputs++;
                    else if(!ale && args.AddAmount==1)p.HomeInputs++;
                    else if(!ale)p.HopsUsed++;
                    else if(args.AddAmount==1)p.AleProduced++;else p.AleConsumed++;
                    log("automatic-brewer-native-goods "+frame+" batches="+p.Batches+" remaining="+p.Remaining+" freshHops="+p.Inputs+
                        " homeInputs="+p.HomeInputs+" hopsUsed="+p.HopsUsed+" localAle="+p.AleProduced+" outputWithdrawn="+p.AleConsumed);
                }
            }
            catch(Exception error){BrewerFault(error.Message+" ["+frame+"]");}
        }
        private unsafe void ObserveBrewerYield(UnitCalculateBonusYieldEventArgs args)
        {
            var p=brewerWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null || args.UnitId!=p.Actor.Id ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            try
            {
                ValidateBrewer(p.Actor);var u=Resolve(p.Actor);
                //139950 only publishes output yield after the actual workshop Ale
                // withdrawal, in109/saved7; this is not Baker's109/saved5 contract.
                Require(u->r_AIState==109 && runtime.Native.Word(u,0x91A)==7 && p.Batches==1 && p.Remaining==0 &&
                    p.Inputs==1 && p.HomeInputs==1 && p.HopsUsed==1 && p.AleProduced==1 && p.AleConsumed==1 && p.Yields==0 && args.GoodAmount>0,
                    "Brewer output yield lacks its exact original fresh production chain");
                if(args.Phase==EventHookPhase.Pre)
                {Require(!p.YieldPending,"Nested Brewer production yield");p.YieldPending=true;p.YieldArgument=args.GoodAmount;}
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.YieldPending && args.GoodAmount==p.YieldArgument && args.ReturnValue>0,
                        "Brewer production yield lacks matching positive completion");
                    p.YieldPending=false;p.Yields++;
                    log("automatic-brewer-natural-production unit="+p.Actor.Id+"/"+p.Actor.Global+
                        " freshHops="+p.Inputs+" argument="+args.GoodAmount+" observedYield="+args.ReturnValue+" savedAI=7");
                }
            }
            catch(Exception error){BrewerFault(error.Message);}
        }
        private unsafe void LogBrewerAcquisition(int sample)
        {
            var facts=new List<string>();
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=eChimps.CHIMP_TYPE_BREWER ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=brewerWorld.Brewery)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                facts.Add(id+"/"+u->r_GlobalId+":ai="+u->r_AIState+",saved="+runtime.Native.Word(u,0x91A)+
                    ",flags="+u->r_PathPlanStateBitFlags+",ss="+u->r_MovementSubstep+",cargo="+u->r_CarryGoodAmount+
                    ",at="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+",next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+
                    ",goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+",unsupported="+(before?.Unsupported??"none"));
                if(facts.Count==3)break;
            }
            log("automatic-brewer-acquire sample="+sample+" brewery="+brewerWorld.Brewery+"/"+brewerWorld.BreweryGlobal+" facts="+string.Join(" | ",facts));
        }
        private unsafe AutomaticWorkerMutation PrepareBrewerMutation(Actor actor)
        {
            var p=brewerWorld;var u=Resolve(actor);var acquired=p.Before;
            var before=runtime.SnapshotUnit(actor.Id,true,false);int x=0,y=0;
            Require(acquired!=null && before!=null && before.Unsupported==null && before.Job!=null && before.OrdinaryStep && before.AIState==8 &&
                before.GlobalId==actor.Global && before.X==acquired.X && before.Y==acquired.Y && before.NextX==acquired.NextX &&
                before.NextY==acquired.NextY && before.Substep==acquired.Substep && before.Flags==acquired.Flags &&
                before.RequestedX==acquired.RequestedX && before.RequestedY==acquired.RequestedY &&
                TryStartedMoatAnchor(before,out x,out y) && BrewerFutureAccessSafe(before,ValidateBrewer(actor),x,y),
                "Natural loaded Brewer's accepted source/goal/access changed before moat construction");
            p.Before=before;p.InitialCargo=p.Remaining=u->r_CarryGoodAmount;Require(p.InitialCargo>0,"Natural Brewer lost its ale cargo");
            p.DeliveryStore=runtime.Native.Word(u,0x98E);p.DeliveryGlobal=runtime.Native.JobDword(u,0x6F8);
            p.DeliveryX=before.RequestedX;p.DeliveryY=before.RequestedY;actors.Add(actor);ArmBrewerMoves();
            return ApplyAutomaticStartedWorkerMoat(actor,before,x,y);
        }
        private unsafe bool BrewerCycleComplete(Actor actor)
        {ValidateBrewer(actor);return brewerWorld.Complete;}
        private IEnumerable<AutomationWait> AutomaticBrewer(AutomationRun run)
        {
            brewerWorld=new BrewerWorld();var p=brewerWorld;
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObserveBrewerGoods))
            using(var production=UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(ObserveBrewerYield))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupBrewerWorld();});
                while(p.CapacityPending)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Require(p.Fault==null,p.Fault);
                }
                Execute(run,()=>{
                    Require(p.Hops!=null && p.Hops.HasStorage && p.Hops.FreeAmount>=16 && p.Ale!=null &&
                        p.Ale.HasStorage && p.Ale.FreeAmount>=16,"Natural Brewer lacks current ingredient/output capacity");
                    int prior=GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_RAW_HOPS);
                    Require(GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_RAW_HOPS,12) &&
                        GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_RAW_HOPS)==prior+12,
                        "Natural Brewer real Hops input was not physically admitted");
                });
                Actor actor=null;
                for(int sample=0;sample<2000 && actor==null;sample++)
                {
                    if(sample==128)Director.instance.SetEngineFrameRate(50);
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,sample<128?16:1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{Require(p.Fault==null,p.Fault);actor=FindNaturalBrewer();if(sample%128==0)LogBrewerAcquisition(sample);return true;});
                }
                Require(actor!=null,"No natural loaded Brewer R8 qualified on a clear accepted step outside future access");
                Director.instance.SetEngineFrameRate(250);
                AutomaticWorkerMutation mutation=null;
                // Execute suppresses fixture construction: the tested mutation MUST
                // use only the ordinary simulation lock, without that suppression.
                run.InSimulation(run.CurrentMission,()=>{mutation=PrepareBrewerMutation(actor);return true;});
                bool complete=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?6000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        Require(p.Fault==null,p.Fault);if(!AutomaticActorAlive(actor))return true;
                        complete=BrewerCycleComplete(actor);
                        return expectedEnabled && complete;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    Require(p.Fault==null && p.Goods.Count==0 && !p.YieldPending,"Brewer native observation incomplete: "+p.Fault);
                    if(expectedEnabled)Require(complete,"Original Brewer did not complete its full ale batch, fresh Hops input, natural production and full second delivery");
                    log("automatic-brewer-cycle enabled="+expectedEnabled+" initialCargo="+p.InitialCargo+" secondCargo="+p.SecondCargo+
                        " aleCredits="+p.Deposits+" batches="+p.Batches+" freshHops="+p.Inputs+" naturalProduction="+p.Yields+" homeInputs="+p.HomeInputs+" hopsUsed="+p.HopsUsed+
                        " localAle="+p.AleProduced+" outputWithdrawn="+p.AleConsumed+
                        " ownOriginalResume="+p.OwnResume+" nativeStoreHandoffs="+p.Handoffs+" complete="+complete);
                    EndAutomaticWorkerCase(mutation,complete);
                });
            }
            finally
            {
                p.Active=false;if(ReferenceEquals(brewerCapacityFixture,this))brewerCapacityFixture=null;
                if(ReferenceEquals(brewerMoveFixture,this))brewerMoveFixture=null;
            }
        }
    }
}
