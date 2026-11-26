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
        private sealed class FletcherWorld
        {
            internal int Workshop,Armoury,InitialCargo,Remaining,Batches,Deposits,Inputs,Yields,SecondCargo,CapacityWait,NoopX,NoopY;
            internal long NaturalYield,Session,World;
            internal uint WorkshopGlobal,ArmouryGlobal;
            internal eGoods Product=eGoods.STORED_BOWS;
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal bool Active=true,CapacityPending,YieldPending,OwnResume,Complete;
            internal int YieldArgument,DeliveryStore,DeliveryX,DeliveryY,Handoffs,HomeInputs,WoodUsed,LastWood,CapturedKind;
            internal uint DeliveryGlobal;
            internal string Fault;
            internal AllyTransferCapacity Wood,Weapon;
            internal readonly Stack<FletcherGoodsFrame> Goods=new Stack<FletcherGoodsFrame>();
        }
        private sealed class FletcherGoodsFrame
        {internal int Store,Amount,Before,Cargo;internal uint Global;internal eGoods Good;}
        private FletcherWorld fletcherWorld;
        private static bool fletcherCapacityRegistered,fletcherMovesRegistered;
        private static volatile Fixture fletcherCapacityFixture,fletcherMoveFixture;
        private IEnumerable<AutomationStep> AutomaticFletcherSteps()
        {yield return new AutomationStep("automatic-natural-fletcher-moat-and-two-batches",TimeSpan.FromSeconds(900),AutomaticFletcher,true);
            yield return new AutomationStep("automatic-natural-fletcher-real-moat-noop",TimeSpan.FromSeconds(90),FletcherNoop,true);}

        private void RequestFletcherCapacity()
        {
            if(!fletcherCapacityRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-fletcher-capacity",ReadFletcherCapacity,out string reason),reason);
                fletcherCapacityRegistered=true;
            }
            fletcherCapacityFixture=this;fletcherWorld.CapacityPending=true;
        }
        private static unsafe void ReadFletcherCapacity(int tick)
        {
            var f=fletcherCapacityFixture;var p=f?.fletcherWorld;
            if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null)return;
            try
            {
                if(p.Batches==2 && !p.Complete)
                {
                    f.ValidateFletcher(p.Actor);
                    Require(f.Resolve(p.Actor)->r_CarryGoodAmount==0 && p.Inputs==p.CapturedKind && p.HomeInputs==p.CapturedKind && p.WoodUsed==1 && p.Yields==1,
                        "Fletcher final deposit did not complete its original cargo/production cycle");
                    p.Complete=true;
                    f.log("automatic-fletcher-complete-readback tick="+tick+" unit="+p.Actor.Id+"/"+p.Actor.Global+" cargo=0 batches=2");
                }
                if(p.HomeInputs==p.CapturedKind && p.CapturedKind>0 && p.WoodUsed==0)
                {
                    var shop=f.ValidateFletcher(p.Actor);var u=f.Resolve(p.Actor);
                    int wood=((int*)&shop->r_NullAmount)[(int)eGoods.STORED_WOOD_PLANKS];
                    if(wood!=p.LastWood)
                    {
                        //12D230 AI5 consumes its complete2/3-Wood local buffer directly.
                        // There is no B6100 debit event for this operation.
                        Require(p.LastWood==p.CapturedKind && wood==0 && u->r_AIState==4 && u->r_CarryGoodAmount==0 &&
                            u->r_CurrentTilePositionX==f.runtime.Native.Word(u,0x992) &&
                            u->r_CurrentTilePositionY==f.runtime.Native.Word(u,0x994),
                            "Fletcher local Wood consumption lost its original input/phase/position");
                        p.WoodUsed++;
                        f.log("automatic-fletcher-local-consumption tick="+tick+" unit="+p.Actor.Id+"/"+p.Actor.Global+" actualWood="+p.LastWood+"->0 ai=4");
                    }
                    p.LastWood=wood;
                }
                if(!p.CapacityPending)return;
                if(!f.MillerAddedYardsReady())
                {Require(++p.CapacityWait<=64,"Fletcher ingredient yards did not initialize");return;}
                Require(BuildingAccess.TryGetById(p.Armoury,out GameBuilding* armoury,out _) && armoury!=null &&
                    armoury->r_GlobalId==p.ArmouryGlobal && armoury->r_PlayerIdOwner==f.owner &&
                    armoury->r_BuildingType==eStructs.STRUCT_ARMOURY,"Fletcher setup Armoury lifetime changed");
                Require(BuildingAccess.TryGetById(p.Workshop,out GameBuilding* workshop,out _) && workshop!=null &&
                    workshop->r_GlobalId==p.WorkshopGlobal,"Fletcher setup workshop lifetime changed");
                if(armoury->r_AliveState==AliveState.NeedsInit || workshop->r_AliveState==AliveState.NeedsInit)
                {Require(++p.CapacityWait<=64,"Fletcher buildings did not initialize");return;}
                Require(armoury->r_AliveState==AliveState.IsAlive && workshop->r_AliveState==AliveState.IsAlive &&
                    (workshop->r_ProducedGoodId==eGoods.STORED_BOWS || workshop->r_ProducedGoodId==eGoods.STORED_CROSSBOWS),
                    "Natural Fletcher selected neither Bows nor Crossbows; no product switch is permitted");
                p.Product=workshop->r_ProducedGoodId;
                Require(AllyTransfers.TryGetCapacity(f.owner,eGoods.STORED_WOOD_PLANKS,out p.Wood,out string woodReason),woodReason);
                Require(AllyTransfers.TryGetCapacity(f.owner,p.Product,out p.Weapon,out string weaponReason),weaponReason);
                p.CapacityPending=false;
                f.log("automatic-fletcher-capacity tick="+tick+" woodFree="+p.Wood.FreeAmount+" sharedArmouryFree="+p.Weapon.FreeAmount);
            }
            catch(Exception error){f.FletcherFault(error.Message);p.CapacityPending=false;}
        }
        private unsafe void SetupFletcherWorld()
        {
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Fletcher fixture requires the actual human owner");
            FindSite();
            Require(WorldMutationEvents.TryGetCurrentWorld(out fletcherWorld.Session,out fletcherWorld.World) &&
                fletcherWorld.Session==session,"Fletcher setup has no exact own native world");
            CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            fletcherWorld.Workshop=CreateBakerPrefab(cx,cy,eMappers.MAPPER_FLETCHER,eStructs.STRUCT_FLETCHERS_WORKSHOP);
            Require(BuildingAccess.TryGetById(fletcherWorld.Workshop,out GameBuilding* workshop,out _) && workshop!=null,
                "Created Workshop lifetime cannot be read back");fletcherWorld.WorkshopGlobal=workshop->r_GlobalId;
            fletcherWorld.Armoury=CreateBakerPrefab(cx-10,cy,eMappers.MAPPER_ARMOURY,eStructs.STRUCT_ARMOURY);
            Require(BuildingAccess.TryGetById(fletcherWorld.Armoury,out GameBuilding* armoury,out _) && armoury!=null,
                "Created Fletcher armoury lifetime cannot be read back");fletcherWorld.ArmouryGlobal=armoury->r_GlobalId;
            Require((byte*)&workshop->r_NextProducedGoodId-(byte*)workshop==0x2EA-0x5C &&
                (byte*)&workshop->r_ProducedGoodId-(byte*)workshop==0x2EC-0x5C,
                "Fletcher captured/selected production words differ from the audited layout");
            // Existing Miller helper holds only the dedicated yard identities here:
            // inactive, no mill or Miller observer/capacity request is created.
            Require(millerWorld==null || !millerWorld.Active,"Fletcher cannot overwrite an active Miller fixture");
            millerWorld=new MillerWorld{Active=false};ExtendMillerStorage();
            RequestFletcherCapacity();
            log("automatic-fletcher-world workshop="+fletcherWorld.Workshop+"/"+fletcherWorld.WorkshopGlobal+
                " armoury="+fletcherWorld.Armoury+"/"+fletcherWorld.ArmouryGlobal+" naturalEmployees=true rawWorkerWrites=false");
        }
        private unsafe GameBuilding* ValidateFletcher(Actor actor)
        {
            var unit=Resolve(actor);var proof=fletcherWorld;GameBuilding* workshop=null;
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_UnitChimp==eChimps.CHIMP_TYPE_FLETCHER &&
                unit->r_ControllableForPlayerId==owner && unit->r_LinkedProductionBuildingId==proof.Workshop &&
                unit->r_LinkedProductionBuildingGlobalId==proof.WorkshopGlobal &&
                BuildingAccess.TryGetById(proof.Workshop,out workshop,out _) && workshop!=null &&
                workshop->r_GlobalId==proof.WorkshopGlobal && workshop->r_AliveState==AliveState.IsAlive &&
                workshop->r_PlayerIdOwner==owner && workshop->r_BuildingType==eStructs.STRUCT_FLETCHERS_WORKSHOP,
                "Original Fletcher/workplace lifetime changed");
            Require((proof.CapturedKind==2 || proof.CapturedKind==3) && (runtime.Native.Word(unit,0xA46)>>8)==proof.CapturedKind && workshop->r_NextProducedGoodId==proof.Product &&
                workshop->r_ProducedGoodId==proof.Product && proof.CapturedKind==(proof.Product==eGoods.STORED_BOWS?2:3),
                "Fletcher captured A47 kind or latched/selected product changed");
            return workshop;
        }
        private unsafe bool FletcherFutureAccessSafe(UnitSnapshot before,GameBuilding* workshop,int x,int y)
        {
            if(AppleStepContains(workshop->r_TileAccessPositionX,workshop->r_TileAccessPositionY,x,y,1) ||
                AppleStepContains(runtime.Native.Word(Resolve(fletcherWorld.Actor),0x992),runtime.Native.Word(Resolve(fletcherWorld.Actor),0x994),x,y,1) ||
                AppleStepContains(before.RequestedX,before.RequestedY,x,y,1))return false;
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int index=0;index<buildings.Length;index++)
            {
                var b=buildings.GetValuePointer(index);
                if(b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner &&
                    (b->r_BuildingType==eStructs.STRUCT_ARMOURY || b->r_BuildingType==eStructs.STRUCT_GOODS_YARD) &&
                    AppleStepContains(b->r_TileAccessPositionX,b->r_TileAccessPositionY,x,y,1))return false;
            }
            return true;
        }
        private unsafe Actor FindNaturalFletcher()
        {
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=eChimps.CHIMP_TYPE_FLETCHER ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=fletcherWorld.Workshop ||
                    u->r_LinkedProductionBuildingGlobalId!=fletcherWorld.WorkshopGlobal || u->r_AIState!=7 || u->r_CarryGoodAmount==0)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                if(before==null || before.Unsupported!=null || before.Job==null || !before.OrdinaryStep || before.Substep>=8 ||
                    !TryStartedMoatAnchor(before,out int x,out int y))continue;
                if(!before.Job.TryGetValue(0xA47,out uint capturedKind) || capturedKind!=2 && capturedKind!=3)continue;
                fletcherWorld.CapturedKind=(int)capturedKind;
                var actor=new Actor{Id=id,Global=u->r_GlobalId,Type=u->r_UnitChimp,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,
                    X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y};
                fletcherWorld.Actor=actor;
                if(!FletcherFutureAccessSafe(before,ValidateFletcher(actor),x,y)){fletcherWorld.Actor=null;continue;}
                int store=runtime.Native.Word(u,0x98E);
                if(!BuildingAccess.TryGetById(store,out GameBuilding* armoury,out _) || armoury==null ||
                    armoury->r_GlobalId!=runtime.Native.JobDword(u,0x6F8) || armoury->r_AliveState!=AliveState.IsAlive ||
                    armoury->r_PlayerIdOwner!=owner || armoury->r_BuildingType!=eStructs.STRUCT_ARMOURY)
                {fletcherWorld.Actor=null;continue;}
                var shop=ValidateFletcher(actor);
                if(((int*)&shop->r_NullAmount)[(int)eGoods.STORED_WOOD_PLANKS]!=0)
                {fletcherWorld.Actor=null;continue;}
                fletcherWorld.Before=before;return actor;
            }
            return null;
        }
        private void ArmFletcherMoves()
        {
            if(!fletcherMovesRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-fletcher-output",new FletcherMoveObserver(),out string reason,
                    order:200,enabled:()=>fletcherMoveFixture!=null && !fletcherMoveFixture.stopped && fletcherMoveFixture.expectedEnabled &&
                        fletcherMoveFixture.fletcherWorld!=null && fletcherMoveFixture.fletcherWorld.Active && fletcherMoveFixture.fletcherWorld.Actor!=null &&
                        fletcherMoveFixture.fletcherWorld.Before!=null && fletcherMoveFixture.fletcherWorld.Batches==0 && fletcherMoveFixture.fletcherWorld.Fault==null),reason);
                fletcherMovesRegistered=true;
            }
            fletcherMoveFixture=this;
        }
        private sealed class FletcherMoveObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var f=fletcherMoveFixture;var p=f?.fletcherWorld;
                if(f==null || p?.Actor==null || context==null || context.SessionId!=f.session || context.UnitId!=p.Actor.Id)return null;
                try
                {
                    f.ValidateFletcher(p.Actor);var u=f.Resolve(p.Actor);bool own=context.OriginOwnerGuid==Plugin.Guid;
                    Require(u->r_AIState==7 && u->r_CarryGoodAmount==p.Remaining && context.Extra==0,
                        "Fletcher output move changed phase/cargo");
                    int store=f.runtime.Native.Word(u,0x98E);uint global=f.runtime.Native.JobDword(u,0x6F8);
                    Require(BuildingAccess.TryGetById(store,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==global &&
                        b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==f.owner && b->r_BuildingType==eStructs.STRUCT_ARMOURY,
                        "Fletcher output move lost its bound armoury");
                    if(own)
                    {
                        IntPtr pointer=(IntPtr)u;
                        Require(store==p.DeliveryStore && global==p.DeliveryGlobal && context.X==p.Before.RequestedX && context.Y==p.Before.RequestedY &&
                            u->r_TargetTilePositionX2==context.X && u->r_TargetTilePositionY2==context.Y &&
                            p.Before.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,
                                o=>f.runtime.Native.JobWord((GameUnit*)pointer,o),o=>f.runtime.Native.JobDword((GameUnit*)pointer,o)),
                            "Owned Fletcher continuation changed original job/binding/goal");
                    }
                    else
                    {
                        Require(p.OwnResume && p.Handoffs<16 && string.IsNullOrEmpty(context.OriginOwnerGuid) && context.ParentInvocationId==0 &&
                            u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY &&
                            context.X==b->r_TileAccessPositionX && context.Y==b->r_TileAccessPositionY,
                            "Fletcher armoury reselection lacks the original owned resume and exact new access");
                        Require(BuildingAccess.TryGetById(p.DeliveryStore,out GameBuilding* old,out _) && old!=null &&
                            old->r_GlobalId==p.DeliveryGlobal && old->r_AliveState==AliveState.IsAlive && old->r_PlayerIdOwner==f.owner &&
                            old->r_BuildingType==eStructs.STRUCT_ARMOURY &&
                            (Math.Abs(u->r_CurrentTilePositionX-old->r_TileAccessPositionX)>=3 ||
                             Math.Abs(u->r_CurrentTilePositionY-old->r_TileAccessPositionY)>=3 || !f.PoleturnerArmouryCanReceive(u,old)),
                            "Fletcher changed armoury without native C9080/shared-C6EA0 rejection");
                    }
                    return new FletcherMoveCompletion(f,p,context,own,store,global);
                }
                catch(Exception error){f.FletcherFault(error.Message);return null;}
            }
        }
        private sealed class FletcherMoveCompletion : IUnitMoveInvocation
        {
            private readonly Fixture f;private readonly FletcherWorld p;private readonly UnitMoveContext c;
            private readonly bool own;private readonly int store;private readonly uint global;private int completed;
            internal FletcherMoveCompletion(Fixture f,FletcherWorld p,UnitMoveContext c,bool own,int store,uint global)
            {this.f=f;this.p=p;this.c=c;this.own=own;this.store=store;this.global=global;}
            public unsafe void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || !p.Active || f.stopped || p.Fault!=null)return;
                try
                {
                    Require(result!=null && ReferenceEquals(result.Context,c) && result.IsCurrentWorld && result.NativeInvoked &&
                        result.NativeReturned && result.Failure==null,"Fletcher output move has no current native result");
                    f.ValidateFletcher(p.Actor);var u=f.Resolve(p.Actor);
                    Require(u->r_AIState==7 && u->r_CarryGoodAmount==p.Remaining && f.runtime.Native.Word(u,0x98E)==store &&
                        f.runtime.Native.JobDword(u,0x6F8)==global,"Fletcher native output move changed original cargo/binding");
                    f.log("automatic-fletcher-output-move own="+own+" actualResult="+result.NativeResult+" store="+store+"/"+global+" goal="+c.X+","+c.Y);
                    if(own && result.NativeResult==0)return;
                    Require(result.NativeResult==1 && u->r_TargetTilePositionX2==c.X && u->r_TargetTilePositionY2==c.Y,
                        "Fletcher output handoff lacks an actual successful native route");
                    if(own)p.OwnResume=true;else p.Handoffs++;
                    p.DeliveryStore=store;p.DeliveryGlobal=global;p.DeliveryX=c.X;p.DeliveryY=c.Y;
                }
                catch(Exception error){f.FletcherFault(error.Message);}
            }
        }
        private void FletcherFault(string reason)
        {if(fletcherWorld!=null && fletcherWorld.Fault==null){fletcherWorld.Fault=reason;log("automatic-fletcher-first-failure "+reason);}}
        private unsafe void ObserveFletcherGoods(AddGoodToGoodsyardEventArgs args)
        {
            var p=fletcherWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id ||
                args.Good!=eGoods.STORED_WOOD_PLANKS && args.Good!=eGoods.STORED_BOWS && args.Good!=eGoods.STORED_CROSSBOWS)return;
            string frame="phase="+args.Phase+" unit="+p.Actor.Id+"/"+p.Actor.Global+" building="+args.BuildingId+"/"+args.BuildingGlobalId+
                " good="+args.Good+" delta="+args.AddAmount+" capacity="+args.Capacity+" add="+args.Add;
            try
            {
                log("automatic-fletcher-goods-frame "+frame);ValidateFletcher(p.Actor);var u=Resolve(p.Actor);
                bool local=args.BuildingId==p.Workshop,output=args.Good!=eGoods.STORED_WOOD_PLANKS;
                Require(BuildingAccess.TryGetById(args.BuildingId,out GameBuilding* store,out _) && store!=null &&
                    store->r_GlobalId==unchecked((uint)args.BuildingGlobalId) && store->r_AliveState==AliveState.IsAlive &&
                    store->r_PlayerIdOwner==owner && store->r_BuildingType==(local?eStructs.STRUCT_FLETCHERS_WORKSHOP:
                        output?eStructs.STRUCT_ARMOURY:eStructs.STRUCT_GOODS_YARD),
                    "Fletcher operation lost its exact original workplace/store lifetime");
                if(local)
                    Require(!output && store->r_GlobalId==p.WorkshopGlobal && u->r_CurrentTilePositionX==runtime.Native.Word(u,0x992) &&
                        u->r_CurrentTilePositionY==runtime.Native.Word(u,0x994),"Fletcher local material credit is not physically at its original home");
                else
                    Require(runtime.Native.Word(u,0x98E)==args.BuildingId && runtime.Native.JobDword(u,0x6F8)==store->r_GlobalId &&
                        Math.Abs(u->r_CurrentTilePositionX-store->r_TileAccessPositionX)<3 && Math.Abs(u->r_CurrentTilePositionY-store->r_TileAccessPositionY)<3,
                        "Fletcher store operation lacks exact active binding and native physical contact");
                int amount=((int*)&store->r_NullAmount)[(int)args.Good];
                if(args.Phase==EventHookPhase.Pre)
                {
                    Require(args.Add && args.Capacity==(output?50:48),"Fletcher native write/capacity contract changed");
                    if(output)
                    {
                        Require(!local && args.Good==p.Product && args.AddAmount==1 && u->r_AIState==7 && u->r_CarryGoodAmount>0 &&
                            PoleturnerArmouryCanReceive(u,store),"Fletcher output is not the captured product into a valid shared Armoury");
                        if(p.Remaining==0)
                        {
                            Require(p.Batches==1 && p.Inputs==p.CapturedKind && p.HomeInputs==p.CapturedKind && p.WoodUsed==1 && p.Yields==1 && !p.YieldPending,
                                "Fletcher second batch lacks exact fresh material consumption/production");
                            Require(u->r_CarryGoodAmount==p.NaturalYield,"Fletcher second cargo differs from its actual natural yield result");
                            p.SecondCargo=p.Remaining=u->r_CarryGoodAmount;
                        }
                        Require(u->r_CarryGoodAmount==p.Remaining,"Fletcher cargo differs from its full original batch");
                        if(expectedEnabled && p.Batches==0)Require(p.OwnResume && args.BuildingId==p.DeliveryStore &&
                            store->r_GlobalId==p.DeliveryGlobal && u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY,
                            "Fletcher first batch lacks its original owned resume and native Armoury handoff");
                    }
                    else
                    {
                        Require(p.Batches==1 && p.Remaining==0 && u->r_AIState==(local?2:1) && u->r_CarryGoodAmount==(local?1:0),
                            "Fletcher Wood operation is not the actual R1-store/R2-home input");
                        if(local)Require(args.AddAmount==1 && p.Inputs==p.HomeInputs+1 && p.HomeInputs<p.CapturedKind && amount==p.HomeInputs,
                            "Fletcher local Wood credit lacks its exact single fresh pickup/buffer");
                        else Require(args.AddAmount==-1 && p.Inputs==p.HomeInputs && p.Inputs<p.CapturedKind,
                            "Fletcher duplicated or exceeded its captured2/3 fresh Wood pickups");
                    }
                    p.Goods.Push(new FletcherGoodsFrame{Store=args.BuildingId,Global=store->r_GlobalId,Good=args.Good,
                        Amount=args.AddAmount,Before=amount,Cargo=u->r_CarryGoodAmount});
                }
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.Goods.Count>0,"Fletcher goods Post lacks matching Pre");var old=p.Goods.Pop();
                    Require(old.Store==args.BuildingId && old.Global==store->r_GlobalId && old.Good==args.Good && old.Amount==args.AddAmount &&
                        amount-old.Before==old.Amount && u->r_CarryGoodAmount==old.Cargo,
                        "Fletcher physical goods delta/lifetime/cargo changed across native write");
                    if(output){p.Deposits++;if(--p.Remaining==0)p.Batches++;}
                    else if(local){p.HomeInputs++;p.LastWood=amount;}else p.Inputs++;
                    log("automatic-fletcher-native-goods "+frame+" product="+p.Product+" batches="+p.Batches+
                        " remaining="+p.Remaining+" freshWood="+p.Inputs+" homeInputs="+p.HomeInputs);
                }
            }
            catch(Exception error){FletcherFault(error.Message+" ["+frame+"]");}
        }
        private unsafe void ObserveFletcherYield(UnitCalculateBonusYieldEventArgs args)
        {
            var p=fletcherWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null || args.UnitId!=p.Actor.Id ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            try
            {
                var workshop=ValidateFletcher(p.Actor);var u=Resolve(p.Actor);
                //12D230 AI4 waits for actual workshop animation, then commits
                // AI6 BEFORE18D940(unit,1,1); cargo is assigned only after return.
                if(u->r_AIState!=6)return;
                Require(u->r_CarryGoodAmount==0 && u->r_CurrentTilePositionX==runtime.Native.Word(u,0x992) &&
                    u->r_CurrentTilePositionY==runtime.Native.Word(u,0x994) &&
                    ((int*)&workshop->r_NullAmount)[(int)eGoods.STORED_WOOD_PLANKS]==0,
                    "Fletcher actual yield is not at the original home after its complete Wood consumption");
                Require(p.Batches==1 && p.Remaining==0 && p.Inputs==p.CapturedKind && p.HomeInputs==p.CapturedKind && p.WoodUsed==1 && p.Yields==0 && args.GoodAmount>0,
                    "Fletcher production did not follow its captured2/3 fresh Wood inputs and actual local consumption");
                if(args.Phase==EventHookPhase.Pre)
                {Require(!p.YieldPending,"Nested Fletcher production yield");p.YieldPending=true;p.YieldArgument=args.GoodAmount;}
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.YieldPending && args.GoodAmount==p.YieldArgument && args.ReturnValue>0,
                        "Fletcher natural production yield has no matching positive completion");
                    p.YieldPending=false;p.Yields++;p.NaturalYield=args.ReturnValue;
                    log("automatic-fletcher-natural-production unit="+p.Actor.Id+"/"+p.Actor.Global+
                        " requiredWood="+p.CapturedKind+" inputOperations="+p.Inputs+" argument="+args.GoodAmount+" observedYield="+args.ReturnValue+" actualAI=6");
                }
            }
            catch(Exception error){FletcherFault(error.Message);}
        }
        private unsafe void LogFletcherAcquisition(int sample)
        {
            var facts=new List<string>();
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=eChimps.CHIMP_TYPE_FLETCHER ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=fletcherWorld.Workshop)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                facts.Add(id+"/"+u->r_GlobalId+":ai="+u->r_AIState+",saved="+runtime.Native.Word(u,0x91A)+
                    ",flags="+u->r_PathPlanStateBitFlags+",ss="+u->r_MovementSubstep+",cargo="+u->r_CarryGoodAmount+
                    ",at="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+",next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+
                    ",goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+",unsupported="+(before?.Unsupported??"none"));
                if(facts.Count==3)break;
            }
            log("automatic-fletcher-acquire sample="+sample+" workshop="+fletcherWorld.Workshop+"/"+fletcherWorld.WorkshopGlobal+" facts="+string.Join(" | ",facts));
        }
        private unsafe AutomaticWorkerMutation PrepareFletcherMutation(Actor actor)
        {
            var p=fletcherWorld;var u=Resolve(actor);var acquired=p.Before;
            var before=runtime.SnapshotUnit(actor.Id,true,false);int x=0,y=0;
            Require(acquired!=null && before!=null && before.Unsupported==null && before.Job!=null && before.OrdinaryStep && before.AIState==7 &&
                before.GlobalId==actor.Global && before.X==acquired.X && before.Y==acquired.Y && before.NextX==acquired.NextX &&
                before.NextY==acquired.NextY && before.Substep==acquired.Substep && before.Flags==acquired.Flags &&
                before.RequestedX==acquired.RequestedX && before.RequestedY==acquired.RequestedY &&
                TryStartedMoatAnchor(before,out x,out y) && FletcherFutureAccessSafe(before,ValidateFletcher(actor),x,y),
                "Natural loaded Fletcher's accepted source/goal/access changed before moat construction");
            p.Before=before;p.InitialCargo=p.Remaining=u->r_CarryGoodAmount;Require(p.InitialCargo>0,"Natural Fletcher lost its weapon cargo");
            p.DeliveryStore=runtime.Native.Word(u,0x98E);p.DeliveryGlobal=runtime.Native.JobDword(u,0x6F8);
            p.DeliveryX=before.RequestedX;p.DeliveryY=before.RequestedY;actors.Add(actor);ArmFletcherMoves();
            p.NoopX=x;p.NoopY=y;
            return ApplyAutomaticStartedWorkerMoat(actor,before,x,y);
        }
        private unsafe bool FletcherCycleComplete(Actor actor)
        {ValidateFletcher(actor);return fletcherWorld.Complete;}
        private uint[] FletcherNoopTiles()
        {
            var p=fletcherWorld;var result=new uint[25*6];var tiles=GameTileManagerAPI.Instance;int index=0;
            for(int y=p.NoopY-2;y<=p.NoopY+2;y++)for(int x=p.NoopX-2;x<=p.NoopX+2;x++)
            {
                int tile=runtime.Native.Tile(x,y);Require(tile>=0,"Fletcher no-op footprint escaped the active map");
                result[index++]=runtime.Native.Flags(tile);result[index++]=tiles.GetTileHeight(tile);result[index++]=(uint)tiles.GetTileType(tile);
                result[index++]=runtime.Native.Connections(tile);result[index++]=unchecked((uint)runtime.Native.Building(tile));
                result[index++]=unchecked((uint)tiles.GetTileVegetationId(tile));
            }
            return result;
        }
        private static bool FletcherNoopEqual(uint[] a,uint[] b)
        {if(a==null || b==null || a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        private unsafe void ApplyFletcherNoop(Actor actor)
        {
            var p=fletcherWorld;var tiles=GameTileManagerAPI.Instance;
            Require(p.Before!=null && p.Actor==actor && !p.Active,"Fletcher no-op must follow its completed natural source case");
            for(int y=p.NoopY-1;y<=p.NoopY;y++)for(int x=p.NoopX-1;x<=p.NoopX;x++)
            {
                int tile=runtime.Native.Tile(x,y);
                Require(tile>=0 && (runtime.Native.Flags(tile)&(uint)TilePropertyFlag.IsMoat)!=0 && runtime.Native.Head(tile)==0,
                    "Fletcher no-op requires the same completed empty2x2 moat, not a new occupied mutation");
            }
            UnitSnapshot before=null;int cargoBefore=AutomaticActorAlive(actor)?WorkerCarry(actor):0;
            if(AutomaticActorAlive(actor)){CheckActor(actor,true);before=runtime.SnapshotUnit(actor.Id,true,false);}
            Plugin.SetGeneralFixtureSession(expectedEnabled);var observer=Plugin.GeneralObserver;
            Require(observer.PendingCount==0,"Fletcher no-op must not retire an unsettled original recovery proof");
            long attempts=observer.AttemptCount,rescues=observer.RescueCount;var geometry=FletcherNoopTiles();
            BeginAutomaticWorkerMoves(actor,before?.RequestedX??p.Before.RequestedX,before?.RequestedY??p.Before.RequestedY);
            // A real repeated writer AFTER the source case has settled. Repeating
            // it before recovery would legitimately retire historical geometry.
            EditorBrush.Call(tiles.GetTileManager(),owner,tiles.GetTileId(p.NoopX,p.NoopY),p.NoopY,2,
                unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require(FletcherNoopEqual(geometry,FletcherNoopTiles()) && observer.AttemptCount==attempts && observer.RescueCount==rescues &&
                automaticWorkerMoves.OwnBegins==0,"Repeated native moat was not a deferred logical no-op");
            if(before!=null)
            {
                CheckActor(actor,true);var after=runtime.SnapshotUnit(actor.Id,true,false);
                Require(after!=null && after.GlobalId==before.GlobalId && after.X==before.X && after.Y==before.Y &&
                    after.NextX==before.NextX && after.NextY==before.NextY && after.Flags==before.Flags && after.Substep==before.Substep &&
                    after.RequestedX==before.RequestedX && after.RequestedY==before.RequestedY && after.AIState==before.AIState &&
                    WorkerCarry(actor)==cargoBefore &&
                    (before.Job==null && after.Job==null || before.Job!=null && before.Job.MatchesContinuation(Resolve(actor)->r_UnitChimp,after.AIState,
                        o=>runtime.Native.JobWord(Resolve(actor),o),o=>runtime.Native.JobDword(Resolve(actor),o))),
                    "Fletcher no-op synchronously changed the original live actor's pose/intent/job");
            }
            log("automatic-fletcher-noop-source enabled="+expectedEnabled+" unit="+actor.Id+"/"+actor.Global+
                " actualRepeatedWriter=true geometryUnchanged=true settledOriginalCase=true directRescue=false");
        }
        private void RequireFletcherOwnedWorld()
        {
            var p=fletcherWorld;
            Require(p!=null && WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) &&
                sid==session && sid==p.Session && world==p.World,"Fletcher case crossed its exact own native world");
        }
        private IEnumerable<AutomationWait> FletcherNoop(AutomationRun run)
        {
            var p=fletcherWorld;Require(p!=null && p.Actor!=null,"Fletcher no-op lacks its preceding exact natural source case");
            long attempts=0,rescues=0;uint[] geometry=null;
            try
            {
                run.InSimulation(run.CurrentMission,()=>{RequireFletcherOwnedWorld();ApplyFletcherNoop(p.Actor);geometry=FletcherNoopTiles();
                    attempts=Plugin.GeneralObserver.AttemptCount;rescues=Plugin.GeneralObserver.RescueCount;return true;});
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,8);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    RequireFletcherOwnedWorld();
                    Require(FletcherNoopEqual(geometry,FletcherNoopTiles()) && Plugin.GeneralObserver.AttemptCount==attempts &&
                        Plugin.GeneralObserver.RescueCount==rescues && Plugin.GeneralObserver.PendingCount==0 &&
                        automaticWorkerMoves.OwnBegins==0 && automaticWorkerMoves.Fault==null,
                        "Fletcher repeated empty moat scheduled recovery or changed geometry");
                    if(AutomaticActorAlive(p.Actor))CheckActor(p.Actor,true);
                    log("automatic-fletcher-noop-result enabled="+expectedEnabled+" advancedTicks=8 geometryUnchanged=true"+
                        " additionalGlobalAttempts=0 additionalGlobalRescues=0 exactOriginalOwnCalls=0");
                    Plugin.SetGeneralFixtureSession(false);return true;
                });
            }
            finally
            {
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;
                // Automation Finish retires the iterator before Dispose: no
                // simulation/global writes here, including on replacement.
                // Plugin Finished/OnEnd owns abort deactivation.
            }
        }
        private IEnumerable<AutomationWait> AutomaticFletcher(AutomationRun run)
        {
            fletcherWorld=new FletcherWorld();var p=fletcherWorld;
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObserveFletcherGoods))
            using(var production=UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(ObserveFletcherYield))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupFletcherWorld();});
                while(p.CapacityPending)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Require(p.Fault==null,p.Fault);
                }
                Execute(run,()=>{
                    Require(p.Wood!=null && p.Wood.HasStorage && p.Wood.FreeAmount>=12 && p.Weapon!=null &&
                        p.Weapon.HasStorage && p.Weapon.FreeAmount>=8,"Natural Fletcher lacks current ingredient/output capacity");
                    int prior=GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_WOOD_PLANKS);
                    Require(GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_WOOD_PLANKS,12) &&
                        GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_WOOD_PLANKS)==prior+12,
                        "Natural Fletcher real Wood input was not physically admitted");
                });
                Actor actor=null;
                for(int sample=0;sample<2000 && actor==null;sample++)
                {
                    if(sample==128)Director.instance.SetEngineFrameRate(50);
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,sample<128?16:1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{Require(p.Fault==null,p.Fault);actor=FindNaturalFletcher();if(sample%128==0)LogFletcherAcquisition(sample);return true;});
                }
                Require(actor!=null,"No natural loaded Fletcher R7 qualified on a clear accepted step outside future access");
                Director.instance.SetEngineFrameRate(250);
                AutomaticWorkerMutation mutation=null;
                // Execute suppresses fixture construction: the tested mutation MUST
                // use only the ordinary simulation lock, without that suppression.
                run.InSimulation(run.CurrentMission,()=>{mutation=PrepareFletcherMutation(actor);return true;});
                bool complete=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?6000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        Require(p.Fault==null,p.Fault);if(!AutomaticActorAlive(actor))return true;
                        complete=FletcherCycleComplete(actor);
                        return expectedEnabled && complete;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    RequireFletcherOwnedWorld();
                    Require(p.Fault==null && p.Goods.Count==0 && !p.YieldPending,"Fletcher native observation incomplete: "+p.Fault);
                    if(expectedEnabled)Require(complete,"Original Fletcher did not complete its full weapon batch, captured2/3 fresh Wood inputs, natural production and full second delivery");
                    log("automatic-fletcher-cycle enabled="+expectedEnabled+" initialCargo="+p.InitialCargo+" secondCargo="+p.SecondCargo+
                        " capturedA47="+p.CapturedKind+" product="+p.Product+" weaponCredits="+p.Deposits+" batches="+p.Batches+" freshWood="+p.Inputs+" naturalProduction="+p.Yields+" homeInputs="+p.HomeInputs+" consumedWood="+p.WoodUsed+
                        " requiredWood="+p.CapturedKind+" naturalYield="+p.NaturalYield+" ownOriginalResume="+p.OwnResume+" nativeArmouryHandoffs="+p.Handoffs+" complete="+complete);
                    EndAutomaticWorkerCase(mutation,complete);
                });
            }
            finally
            {
                p.Active=false;if(ReferenceEquals(fletcherCapacityFixture,this))fletcherCapacityFixture=null;
                if(ReferenceEquals(fletcherMoveFixture,this))fletcherMoveFixture=null;
            }
        }
    }
}
