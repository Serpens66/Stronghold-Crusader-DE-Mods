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
        private sealed class BakerWorld
        {
            internal int Bakery,Granary,InitialCargo,Remaining,Batches,Deposits,Inputs,Yields,SecondCargo,CapacityWait;
            internal uint BakeryGlobal,GranaryGlobal;
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal bool Active=true,CapacityPending,YieldPending,OwnResume,Complete;
            internal int YieldArgument,DeliveryStore,DeliveryX,DeliveryY,Handoffs;
            internal uint DeliveryGlobal;
            internal string Fault;
            internal AllyTransferCapacity Flour,Bread;
            internal readonly Stack<BakerGoodsFrame> Goods=new Stack<BakerGoodsFrame>();
        }
        private sealed class BakerGoodsFrame
        {internal int Store,Amount,Before,Cargo;internal uint Global;internal eGoods Good;}
        private BakerWorld bakerWorld;
        private static bool bakerCapacityRegistered,bakerMovesRegistered;
        private static volatile Fixture bakerCapacityFixture,bakerMoveFixture;
        private IEnumerable<AutomationStep> AutomaticBakerSteps()
        {yield return new AutomationStep("automatic-natural-baker-moat-and-two-batches",TimeSpan.FromSeconds(900),AutomaticBaker,true);}

        private void RequestBakerCapacity()
        {
            if(!bakerCapacityRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-baker-capacity",ReadBakerCapacity,out string reason),reason);
                bakerCapacityRegistered=true;
            }
            bakerCapacityFixture=this;bakerWorld.CapacityPending=true;
        }
        private static unsafe void ReadBakerCapacity(int tick)
        {
            var fixture=bakerCapacityFixture;var proof=fixture?.bakerWorld;
            if(fixture==null || fixture.stopped || proof==null || !proof.Active || proof.Fault!=null)return;
            try
            {
                if(proof.Batches==2 && !proof.Complete)
                {
                    // B6100 Post precedes the caller's final cargo decrement.
                    // Latch the actual next-tick readback before a third natural
                    // input can make a GUI-paced poll miss the completed cycle.
                    fixture.ValidateBaker(proof.Actor);
                    Require(fixture.Resolve(proof.Actor)->r_CarryGoodAmount==0 && proof.Inputs==1 && proof.Yields==1,
                        "Baker final Bread write did not leave its exact completed cargo/cycle");
                    proof.Complete=true;
                    fixture.log("automatic-baker-complete-readback tick="+tick+" unit="+proof.Actor.Id+"/"+proof.Actor.Global+" cargo=0 batches=2");
                }
                if(!proof.CapacityPending)return;
                // Reuse the proven dedicated-yard initialization/lifetime reader.
                if(!fixture.MillerAddedYardsReady())
                {Require(++proof.CapacityWait<=64,"Baker ingredient yards did not initialize");return;}
                Require(BuildingAccess.TryGetById(proof.Granary,out GameBuilding* granary,out _) && granary!=null &&
                    granary->r_GlobalId==proof.GranaryGlobal && granary->r_PlayerIdOwner==fixture.owner &&
                    granary->r_BuildingType==eStructs.STRUCT_GRANARY,"Baker fixture granary lifetime changed");
                if(granary->r_AliveState==AliveState.NeedsInit)
                {Require(++proof.CapacityWait<=64,"Baker granary did not initialize");return;}
                Require(granary->r_AliveState==AliveState.IsAlive,"Baker granary disappeared");
                Require(AllyTransfers.TryGetCapacity(fixture.owner,eGoods.STORED_FLOUR,out proof.Flour,out string flourReason),flourReason);
                Require(AllyTransfers.TryGetCapacity(fixture.owner,eGoods.STORED_FOOD_BREAD,out proof.Bread,out string breadReason),breadReason);
                proof.CapacityPending=false;
                fixture.log("automatic-baker-capacity tick="+tick+" flourFree="+proof.Flour.FreeAmount+" breadFree="+proof.Bread.FreeAmount);
            }
            catch(Exception error){proof.Fault=error.Message;proof.CapacityPending=false;}
        }
        private unsafe int CreateBakerPrefab(int x,int y,eMappers mapper,eStructs type)
        {
            int id=0;
            var placement=GameTileManagerAPI.Instance.TileManager;
            Require(!placement.UsePlacementBlockedOverride,"Baker construction cannot nest a foreign placement override");
            bool oldValue=placement.PlacementBlockedOverrideValue;
            using(var observed=BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args=>{
                if(args.Phase==EventHookPhase.Post && args.PlayerId==owner && args.Building==type && args.ReturnValue>0)
                    id=checked((int)args.ReturnValue);
            }))
            try
            {GameBuildingManagerAPI.Instance.CreatePrefab(owner,x,y,mapper,BuildingScales.GetScale(mapper),0,true,true);}
            finally{placement.UsePlacementBlockedOverride=false;placement.PlacementBlockedOverrideValue=oldValue;}
            Require(id>0 && BuildingAccess.TryGetById(id,out GameBuilding* building,out _) && building!=null &&
                building->r_PlayerIdOwner==owner && building->r_BuildingType==type && building->r_GlobalId!=0,
                "Baker prefab did not create its exact native building");
            return id;
        }
        private unsafe void SetupBakerWorld()
        {
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Baker fixture requires the actual human owner");
            FindSite();
            CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            bakerWorld.Bakery=CreateBakerPrefab(cx,cy,eMappers.MAPPER_BAKER,eStructs.STRUCT_BAKERS_WORKSHOP);
            Require(BuildingAccess.TryGetById(bakerWorld.Bakery,out GameBuilding* bakery,out _) && bakery!=null,
                "Created Bakery lifetime cannot be read back");bakerWorld.BakeryGlobal=bakery->r_GlobalId;
            bakerWorld.Granary=CreateBakerPrefab(cx-10,cy,eMappers.MAPPER_GRANARY,eStructs.STRUCT_GRANARY);
            Require(BuildingAccess.TryGetById(bakerWorld.Granary,out GameBuilding* granary,out _) && granary!=null,
                "Created Baker granary lifetime cannot be read back");bakerWorld.GranaryGlobal=granary->r_GlobalId;
            // Existing Miller helper holds only the dedicated yard identities here:
            // inactive, no mill or Miller observer/capacity request is created.
            Require(millerWorld==null || !millerWorld.Active,"Baker cannot overwrite an active Miller fixture");
            millerWorld=new MillerWorld{Active=false};ExtendMillerStorage();
            RequestBakerCapacity();
            log("automatic-baker-world bakery="+bakerWorld.Bakery+"/"+bakerWorld.BakeryGlobal+
                " granary="+bakerWorld.Granary+"/"+bakerWorld.GranaryGlobal+" naturalEmployees=true rawWorkerWrites=false");
        }
        private unsafe GameBuilding* ValidateBaker(Actor actor)
        {
            var unit=Resolve(actor);var proof=bakerWorld;GameBuilding* bakery=null;
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_UnitChimp==eChimps.CHIMP_TYPE_BAKER &&
                unit->r_ControllableForPlayerId==owner && unit->r_LinkedProductionBuildingId==proof.Bakery &&
                unit->r_LinkedProductionBuildingGlobalId==proof.BakeryGlobal &&
                BuildingAccess.TryGetById(proof.Bakery,out bakery,out _) && bakery!=null &&
                bakery->r_GlobalId==proof.BakeryGlobal && bakery->r_AliveState==AliveState.IsAlive &&
                bakery->r_PlayerIdOwner==owner && bakery->r_BuildingType==eStructs.STRUCT_BAKERS_WORKSHOP,
                "Original Baker/workplace lifetime changed");
            return bakery;
        }
        private unsafe bool BakerFutureAccessSafe(UnitSnapshot before,GameBuilding* bakery,int x,int y)
        {
            if(AppleStepContains(bakery->r_TileAccessPositionX,bakery->r_TileAccessPositionY,x,y,1) ||
                AppleStepContains(runtime.Native.Word(Resolve(bakerWorld.Actor),0x992),runtime.Native.Word(Resolve(bakerWorld.Actor),0x994),x,y,1) ||
                AppleStepContains(before.RequestedX,before.RequestedY,x,y,1))return false;
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int index=0;index<buildings.Length;index++)
            {
                var b=buildings.GetValuePointer(index);
                if(b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner &&
                    (b->r_BuildingType==eStructs.STRUCT_GRANARY || b->r_BuildingType==eStructs.STRUCT_GOODS_YARD) &&
                    AppleStepContains(b->r_TileAccessPositionX,b->r_TileAccessPositionY,x,y,1))return false;
            }
            return true;
        }
        private unsafe Actor FindNaturalBaker()
        {
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=eChimps.CHIMP_TYPE_BAKER ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=bakerWorld.Bakery ||
                    u->r_LinkedProductionBuildingGlobalId!=bakerWorld.BakeryGlobal || u->r_AIState!=6 || u->r_CarryGoodAmount==0)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                if(before==null || before.Unsupported!=null || before.Job==null || !before.OrdinaryStep || before.Substep>=8 ||
                    !TryStartedMoatAnchor(before,out int x,out int y))continue;
                var actor=new Actor{Id=id,Global=u->r_GlobalId,Type=u->r_UnitChimp,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,
                    X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y};
                bakerWorld.Actor=actor;
                if(!BakerFutureAccessSafe(before,ValidateBaker(actor),x,y)){bakerWorld.Actor=null;continue;}
                int store=runtime.Native.Word(u,0x98E);
                if(!BuildingAccess.TryGetById(store,out GameBuilding* granary,out _) || granary==null ||
                    granary->r_GlobalId!=runtime.Native.JobDword(u,0x6F8) || granary->r_AliveState!=AliveState.IsAlive ||
                    granary->r_PlayerIdOwner!=owner || granary->r_BuildingType!=eStructs.STRUCT_GRANARY)
                {bakerWorld.Actor=null;continue;}
                bakerWorld.Before=before;return actor;
            }
            return null;
        }
        private void ArmBakerMoves()
        {
            if(!bakerMovesRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-baker-output",new BakerMoveObserver(),out string reason,
                    order:200,enabled:()=>bakerMoveFixture!=null && !bakerMoveFixture.stopped && bakerMoveFixture.expectedEnabled &&
                        bakerMoveFixture.bakerWorld!=null && bakerMoveFixture.bakerWorld.Active && bakerMoveFixture.bakerWorld.Actor!=null &&
                        bakerMoveFixture.bakerWorld.Before!=null && bakerMoveFixture.bakerWorld.Batches==0 && bakerMoveFixture.bakerWorld.Fault==null),reason);
                bakerMovesRegistered=true;
            }
            bakerMoveFixture=this;
        }
        private sealed class BakerMoveObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var f=bakerMoveFixture;var p=f?.bakerWorld;
                if(f==null || p?.Actor==null || context==null || context.SessionId!=f.session || context.UnitId!=p.Actor.Id)return null;
                try
                {
                    f.ValidateBaker(p.Actor);var u=f.Resolve(p.Actor);bool own=context.OriginOwnerGuid==Plugin.Guid;
                    Require(u->r_AIState==6 && u->r_CarryGoodAmount==p.Remaining && context.Extra==0,
                        "Baker output move changed phase/cargo");
                    int store=f.runtime.Native.Word(u,0x98E);uint global=f.runtime.Native.JobDword(u,0x6F8);
                    Require(BuildingAccess.TryGetById(store,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==global &&
                        b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==f.owner && b->r_BuildingType==eStructs.STRUCT_GRANARY,
                        "Baker output move lost its bound granary");
                    if(own)
                    {
                        IntPtr pointer=(IntPtr)u;
                        Require(store==p.DeliveryStore && global==p.DeliveryGlobal && context.X==p.Before.RequestedX && context.Y==p.Before.RequestedY &&
                            u->r_TargetTilePositionX2==context.X && u->r_TargetTilePositionY2==context.Y &&
                            p.Before.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,
                                o=>f.runtime.Native.JobWord((GameUnit*)pointer,o),o=>f.runtime.Native.JobDword((GameUnit*)pointer,o)),
                            "Owned Baker continuation changed original job/binding/goal");
                    }
                    else
                    {
                        Require(p.OwnResume && p.Handoffs<16 && string.IsNullOrEmpty(context.OriginOwnerGuid) && context.ParentInvocationId==0 &&
                            u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY &&
                            context.X==b->r_TileAccessPositionX && context.Y==b->r_TileAccessPositionY,
                            "Baker granary reselection lacks the original owned resume and exact new access");
                        Require(BuildingAccess.TryGetById(p.DeliveryStore,out GameBuilding* old,out _) && old!=null &&
                            old->r_GlobalId==p.DeliveryGlobal && old->r_AliveState==AliveState.IsAlive && old->r_PlayerIdOwner==f.owner &&
                            old->r_BuildingType==eStructs.STRUCT_GRANARY &&
                            (Math.Abs(u->r_CurrentTilePositionX-old->r_TileAccessPositionX)>=3 ||
                             Math.Abs(u->r_CurrentTilePositionY-old->r_TileAccessPositionY)>=3 || old->r_CurrentGoodStackAmount>=250),
                            "Baker changed granary without native C9080/BD470 rejection");
                    }
                    return new BakerMoveCompletion(f,p,context,own,store,global);
                }
                catch(Exception error){f.BakerFault(error.Message);return null;}
            }
        }
        private sealed class BakerMoveCompletion : IUnitMoveInvocation
        {
            private readonly Fixture f;private readonly BakerWorld p;private readonly UnitMoveContext c;
            private readonly bool own;private readonly int store;private readonly uint global;private int completed;
            internal BakerMoveCompletion(Fixture f,BakerWorld p,UnitMoveContext c,bool own,int store,uint global)
            {this.f=f;this.p=p;this.c=c;this.own=own;this.store=store;this.global=global;}
            public unsafe void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || !p.Active || f.stopped || p.Fault!=null)return;
                try
                {
                    Require(result!=null && ReferenceEquals(result.Context,c) && result.IsCurrentWorld && result.NativeInvoked &&
                        result.NativeReturned && result.Failure==null,"Baker output move has no current native result");
                    f.ValidateBaker(p.Actor);var u=f.Resolve(p.Actor);
                    Require(u->r_AIState==6 && u->r_CarryGoodAmount==p.Remaining && f.runtime.Native.Word(u,0x98E)==store &&
                        f.runtime.Native.JobDword(u,0x6F8)==global,"Baker native output move changed original cargo/binding");
                    f.log("automatic-baker-output-move own="+own+" actualResult="+result.NativeResult+" store="+store+"/"+global+" goal="+c.X+","+c.Y);
                    if(own && result.NativeResult==0)return;
                    Require(result.NativeResult==1 && u->r_TargetTilePositionX2==c.X && u->r_TargetTilePositionY2==c.Y,
                        "Baker output handoff lacks an actual successful native route");
                    if(own)p.OwnResume=true;else p.Handoffs++;
                    p.DeliveryStore=store;p.DeliveryGlobal=global;p.DeliveryX=c.X;p.DeliveryY=c.Y;
                }
                catch(Exception error){f.BakerFault(error.Message);}
            }
        }
        private void BakerFault(string reason)
        {if(bakerWorld!=null && bakerWorld.Fault==null){bakerWorld.Fault=reason;log("automatic-baker-first-failure "+reason);}}
        private unsafe void ObserveBakerGoods(AddGoodToGoodsyardEventArgs args)
        {
            var p=bakerWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id ||
                args.Good!=eGoods.STORED_FLOUR && args.Good!=eGoods.STORED_FOOD_BREAD)return;
            string frame="phase="+args.Phase+" unit="+p.Actor.Id+"/"+p.Actor.Global+" store="+args.BuildingId+"/"+args.BuildingGlobalId+
                " good="+args.Good+" delta="+args.AddAmount+" capacity="+args.Capacity+" add="+args.Add;
            try
            {
                log("automatic-baker-goods-frame "+frame);ValidateBaker(p.Actor);var u=Resolve(p.Actor);
                bool bread=args.Good==eGoods.STORED_FOOD_BREAD;
                Require(BuildingAccess.TryGetById(args.BuildingId,out GameBuilding* store,out _) && store!=null &&
                    store->r_GlobalId==unchecked((uint)args.BuildingGlobalId) && store->r_AliveState==AliveState.IsAlive &&
                    store->r_PlayerIdOwner==owner && store->r_BuildingType==(bread?eStructs.STRUCT_GRANARY:eStructs.STRUCT_GOODS_YARD) &&
                    runtime.Native.Word(u,0x98E)==args.BuildingId && runtime.Native.JobDword(u,0x6F8)==store->r_GlobalId &&
                    Math.Abs(u->r_CurrentTilePositionX-store->r_TileAccessPositionX)<3 && Math.Abs(u->r_CurrentTilePositionY-store->r_TileAccessPositionY)<3,
                    "Baker goods operation lacks exact live storage binding and physical contact");
                int amount=((int*)&store->r_NullAmount)[(int)args.Good];
                if(args.Phase==EventHookPhase.Pre)
                {
                    Require(args.Add && args.AddAmount==(bread?1:-1) && args.Capacity==(bread?250:32) &&
                        u->r_AIState==(bread?6:8) && u->r_CarryGoodAmount>0,
                        "Baker goods event is not its native Bread credit or post-R7 Flour debit");
                    if(bread)
                    {
                        if(p.Remaining==0)
                        {
                            Require(p.Batches==1 && p.Inputs==1 && p.Yields==1 && !p.YieldPending,
                                "Baker second bread batch lacks one fresh input and natural production");
                            p.SecondCargo=p.Remaining=u->r_CarryGoodAmount;
                        }
                        Require(u->r_CarryGoodAmount==p.Remaining,"Baker output cargo no longer matches its original complete batch");
                        if(expectedEnabled && p.Batches==0)Require(p.OwnResume && args.BuildingId==p.DeliveryStore &&
                            store->r_GlobalId==p.DeliveryGlobal && u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY,
                            "First Baker batch lacks its own original-goal resume and native output handoff");
                    }
                    else Require(p.Batches==1 && p.Remaining==0 && p.Inputs==0,"Baker repeated or premature fresh Flour input");
                    p.Goods.Push(new BakerGoodsFrame{Store=args.BuildingId,Global=store->r_GlobalId,Good=args.Good,
                        Amount=args.AddAmount,Before=amount,Cargo=u->r_CarryGoodAmount});
                }
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.Goods.Count>0,"Baker goods Post lacks matching Pre");var old=p.Goods.Pop();
                    Require(old.Store==args.BuildingId && old.Global==store->r_GlobalId && old.Good==args.Good &&
                        old.Amount==args.AddAmount && amount-old.Before==old.Amount && u->r_CarryGoodAmount==old.Cargo,
                        "Baker native physical goods delta/identity/cargo changed across its operation");
                    if(bread){p.Deposits++;if(--p.Remaining==0)p.Batches++;}else p.Inputs++;
                    log("automatic-baker-native-goods "+frame+" batches="+p.Batches+" remaining="+p.Remaining+" freshInputs="+p.Inputs);
                }
            }
            catch(Exception error){BakerFault(error.Message+" ["+frame+"]");}
        }
        private unsafe void ObserveBakerYield(UnitCalculateBonusYieldEventArgs args)
        {
            var p=bakerWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null || args.UnitId!=p.Actor.Id ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            try
            {
                ValidateBaker(p.Actor);var u=Resolve(p.Actor);
                // R7 pickup also calculates yield, BEFORE committing AI8. Only
                // production3 has already committed109/saved5 at the output call.
                if(u->r_AIState!=109 || runtime.Native.Word(u,0x91A)!=5)return;
                Require(p.Batches==1 && p.Remaining==0 && p.Inputs==1 && p.Yields==0 && args.GoodAmount>0,
                    "Baker production did not follow exactly one fresh Flour input");
                if(args.Phase==EventHookPhase.Pre)
                {Require(!p.YieldPending,"Nested Baker production yield");p.YieldPending=true;p.YieldArgument=args.GoodAmount;}
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.YieldPending && args.GoodAmount==p.YieldArgument && args.ReturnValue>0,
                        "Baker natural production yield has no matching positive completion");
                    p.YieldPending=false;p.Yields++;
                    log("automatic-baker-natural-production unit="+p.Actor.Id+"/"+p.Actor.Global+
                        " inputOperations="+p.Inputs+" argument="+args.GoodAmount+" observedYield="+args.ReturnValue+" savedAI=5");
                }
            }
            catch(Exception error){BakerFault(error.Message);}
        }
        private unsafe void LogBakerAcquisition(int sample)
        {
            var facts=new List<string>();
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=eChimps.CHIMP_TYPE_BAKER ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=bakerWorld.Bakery)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                facts.Add(id+"/"+u->r_GlobalId+":ai="+u->r_AIState+",saved="+runtime.Native.Word(u,0x91A)+
                    ",flags="+u->r_PathPlanStateBitFlags+",ss="+u->r_MovementSubstep+",cargo="+u->r_CarryGoodAmount+
                    ",at="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+",next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+
                    ",goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+",unsupported="+(before?.Unsupported??"none"));
                if(facts.Count==3)break;
            }
            log("automatic-baker-acquire sample="+sample+" bakery="+bakerWorld.Bakery+"/"+bakerWorld.BakeryGlobal+" facts="+string.Join(" | ",facts));
        }
        private unsafe AutomaticWorkerMutation PrepareBakerMutation(Actor actor)
        {
            var p=bakerWorld;var u=Resolve(actor);var acquired=p.Before;
            var before=runtime.SnapshotUnit(actor.Id,true,false);int x=0,y=0;
            Require(acquired!=null && before!=null && before.Unsupported==null && before.Job!=null && before.OrdinaryStep && before.AIState==6 &&
                before.GlobalId==actor.Global && before.X==acquired.X && before.Y==acquired.Y && before.NextX==acquired.NextX &&
                before.NextY==acquired.NextY && before.Substep==acquired.Substep && before.Flags==acquired.Flags &&
                before.RequestedX==acquired.RequestedX && before.RequestedY==acquired.RequestedY &&
                TryStartedMoatAnchor(before,out x,out y) && BakerFutureAccessSafe(before,ValidateBaker(actor),x,y),
                "Natural loaded Baker's accepted source/goal/access changed before moat construction");
            p.Before=before;p.InitialCargo=p.Remaining=u->r_CarryGoodAmount;Require(p.InitialCargo>0,"Natural Baker lost its bread cargo");
            p.DeliveryStore=runtime.Native.Word(u,0x98E);p.DeliveryGlobal=runtime.Native.JobDword(u,0x6F8);
            p.DeliveryX=before.RequestedX;p.DeliveryY=before.RequestedY;actors.Add(actor);ArmBakerMoves();
            return ApplyAutomaticStartedWorkerMoat(actor,before,x,y);
        }
        private unsafe bool BakerCycleComplete(Actor actor)
        {ValidateBaker(actor);return bakerWorld.Complete;}
        private IEnumerable<AutomationWait> AutomaticBaker(AutomationRun run)
        {
            bakerWorld=new BakerWorld();var p=bakerWorld;
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObserveBakerGoods))
            using(var production=UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(ObserveBakerYield))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupBakerWorld();});
                while(p.CapacityPending)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Require(p.Fault==null,p.Fault);
                }
                Execute(run,()=>{
                    Require(p.Flour!=null && p.Flour.HasStorage && p.Flour.FreeAmount>=32 && p.Bread!=null &&
                        p.Bread.HasStorage && p.Bread.FreeAmount>=32,"Natural Baker lacks current ingredient/output capacity");
                    int prior=GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_FLOUR);
                    Require(GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_FLOUR,30) &&
                        GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_FLOUR)==prior+30,
                        "Natural Baker real Flour input was not physically admitted");
                });
                Actor actor=null;
                for(int sample=0;sample<2000 && actor==null;sample++)
                {
                    if(sample==128)Director.instance.SetEngineFrameRate(50);
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,sample<128?16:1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{Require(p.Fault==null,p.Fault);actor=FindNaturalBaker();if(sample%128==0)LogBakerAcquisition(sample);return true;});
                }
                Require(actor!=null,"No natural loaded Baker R6 qualified on a clear accepted step outside future access");
                Director.instance.SetEngineFrameRate(250);
                AutomaticWorkerMutation mutation=null;
                // Execute suppresses fixture construction: the tested mutation MUST
                // use only the ordinary simulation lock, without that suppression.
                run.InSimulation(run.CurrentMission,()=>{mutation=PrepareBakerMutation(actor);return true;});
                bool complete=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?6000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        Require(p.Fault==null,p.Fault);if(!AutomaticActorAlive(actor))return true;
                        complete=BakerCycleComplete(actor);
                        return expectedEnabled && complete;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    Require(p.Fault==null && p.Goods.Count==0 && !p.YieldPending,"Baker native observation incomplete: "+p.Fault);
                    if(expectedEnabled)Require(complete,"Original Baker did not complete its full bread batch, fresh Flour input, natural production and full second delivery");
                    log("automatic-baker-cycle enabled="+expectedEnabled+" initialCargo="+p.InitialCargo+" secondCargo="+p.SecondCargo+
                        " breadCredits="+p.Deposits+" batches="+p.Batches+" freshFlour="+p.Inputs+" naturalProduction="+p.Yields+
                        " ownOriginalResume="+p.OwnResume+" nativeGranaryHandoffs="+p.Handoffs+" complete="+complete);
                    EndAutomaticWorkerCase(mutation,complete);
                });
            }
            finally
            {
                p.Active=false;if(ReferenceEquals(bakerCapacityFixture,this))bakerCapacityFixture=null;
                if(ReferenceEquals(bakerMoveFixture,this))bakerMoveFixture=null;
            }
        }
    }
}
