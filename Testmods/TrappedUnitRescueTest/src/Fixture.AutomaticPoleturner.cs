using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
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
        private sealed class PoleturnerWorld
        {
            internal int Workshop,Armoury,InitialCargo,Remaining,Batches,Deposits,Inputs,Yields,SecondCargo,CapacityWait;
            internal uint WorkshopGlobal,ArmouryGlobal;
            internal eGoods Product=eGoods.STORED_SPEARS;
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal bool Active=true,CapacityPending,YieldPending,OwnResume,Complete;
            internal int YieldArgument,DeliveryStore,DeliveryX,DeliveryY,Handoffs,HomeInputs,WoodUsed,LastWood,CapturedKind;
            internal uint DeliveryGlobal;
            internal string Fault;
            internal AllyTransferCapacity Wood,Weapon;
            internal readonly Stack<PoleturnerGoodsFrame> Goods=new Stack<PoleturnerGoodsFrame>();
        }
        private sealed class PoleturnerGoodsFrame
        {internal int Store,Amount,Before,Cargo;internal uint Global;internal eGoods Good;}
        private PoleturnerWorld poleturnerWorld;
        private static bool poleturnerCapacityRegistered,poleturnerMovesRegistered;
        private static volatile Fixture poleturnerCapacityFixture,poleturnerMoveFixture;
        private IEnumerable<AutomationStep> AutomaticPoleturnerSteps()
        {yield return new AutomationStep("automatic-natural-poleturner-moat-and-two-batches",TimeSpan.FromSeconds(900),AutomaticPoleturner,true);}

        private void RequestPoleturnerCapacity()
        {
            if(!poleturnerCapacityRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-poleturner-capacity",ReadPoleturnerCapacity,out string reason),reason);
                poleturnerCapacityRegistered=true;
            }
            poleturnerCapacityFixture=this;poleturnerWorld.CapacityPending=true;
        }
        private static unsafe void ReadPoleturnerCapacity(int tick)
        {
            var f=poleturnerCapacityFixture;var p=f?.poleturnerWorld;
            if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null)return;
            try
            {
                if(p.Batches==2 && !p.Complete)
                {
                    f.ValidatePoleturner(p.Actor);
                    Require(f.Resolve(p.Actor)->r_CarryGoodAmount==0 && p.Inputs==1 && p.HomeInputs==1 && p.WoodUsed==1 && p.Yields==1,
                        "Poleturner final deposit did not complete its original cargo/production cycle");
                    p.Complete=true;
                    f.log("automatic-poleturner-complete-readback tick="+tick+" unit="+p.Actor.Id+"/"+p.Actor.Global+" cargo=0 batches=2");
                }
                if(p.HomeInputs==1 && p.WoodUsed==0)
                {
                    var shop=f.ValidatePoleturner(p.Actor);var u=f.Resolve(p.Actor);
                    int wood=((int*)&shop->r_NullAmount)[(int)eGoods.STORED_WOOD_PLANKS];
                    if(wood!=p.LastWood)
                    {
                        //13AAD0 AI6 clears the actual local buffer directly.
                        // There is no B6100 debit event for this operation.
                        Require(p.LastWood==1 && wood==0 && u->r_AIState==5 && u->r_CarryGoodAmount==0 &&
                            u->r_CurrentTilePositionX==f.runtime.Native.Word(u,0x992) &&
                            u->r_CurrentTilePositionY==f.runtime.Native.Word(u,0x994),
                            "Poleturner local Wood consumption lost its original input/phase/position");
                        p.WoodUsed++;
                        f.log("automatic-poleturner-local-consumption tick="+tick+" unit="+p.Actor.Id+"/"+p.Actor.Global+" actualWood=1->0 ai=5");
                    }
                    p.LastWood=wood;
                }
                if(!p.CapacityPending)return;
                if(!f.MillerAddedYardsReady())
                {Require(++p.CapacityWait<=64,"Poleturner ingredient yards did not initialize");return;}
                Require(BuildingAccess.TryGetById(p.Armoury,out GameBuilding* armoury,out _) && armoury!=null &&
                    armoury->r_GlobalId==p.ArmouryGlobal && armoury->r_PlayerIdOwner==f.owner &&
                    armoury->r_BuildingType==eStructs.STRUCT_ARMOURY,"Poleturner setup Armoury lifetime changed");
                Require(BuildingAccess.TryGetById(p.Workshop,out GameBuilding* workshop,out _) && workshop!=null &&
                    workshop->r_GlobalId==p.WorkshopGlobal,"Poleturner setup workshop lifetime changed");
                if(armoury->r_AliveState==AliveState.NeedsInit || workshop->r_AliveState==AliveState.NeedsInit)
                {Require(++p.CapacityWait<=64,"Poleturner buildings did not initialize");return;}
                Require(armoury->r_AliveState==AliveState.IsAlive && workshop->r_AliveState==AliveState.IsAlive &&
                    workshop->r_ProducedGoodId==p.Product,
                    "Natural Poleturner default is not Spears; no workshop product switch is permitted");
                Require(AllyTransfers.TryGetCapacity(f.owner,eGoods.STORED_WOOD_PLANKS,out p.Wood,out string woodReason),woodReason);
                Require(AllyTransfers.TryGetCapacity(f.owner,p.Product,out p.Weapon,out string weaponReason),weaponReason);
                p.CapacityPending=false;
                f.log("automatic-poleturner-capacity tick="+tick+" woodFree="+p.Wood.FreeAmount+" sharedArmouryFree="+p.Weapon.FreeAmount);
            }
            catch(Exception error){f.PoleturnerFault(error.Message);p.CapacityPending=false;}
        }
        private unsafe void SetupPoleturnerWorld()
        {
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Poleturner fixture requires the actual human owner");
            FindSite();
            CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            poleturnerWorld.Workshop=CreateBakerPrefab(cx,cy,eMappers.MAPPER_POLETURNER,eStructs.STRUCT_POLETURNERS_WORKSHOP);
            Require(BuildingAccess.TryGetById(poleturnerWorld.Workshop,out GameBuilding* workshop,out _) && workshop!=null,
                "Created Workshop lifetime cannot be read back");poleturnerWorld.WorkshopGlobal=workshop->r_GlobalId;
            poleturnerWorld.Armoury=CreateBakerPrefab(cx-10,cy,eMappers.MAPPER_ARMOURY,eStructs.STRUCT_ARMOURY);
            Require(BuildingAccess.TryGetById(poleturnerWorld.Armoury,out GameBuilding* armoury,out _) && armoury!=null,
                "Created Poleturner armoury lifetime cannot be read back");poleturnerWorld.ArmouryGlobal=armoury->r_GlobalId;
            Require((byte*)&workshop->r_NextProducedGoodId-(byte*)workshop==0x2EA-0x5C &&
                (byte*)&workshop->r_ProducedGoodId-(byte*)workshop==0x2EC-0x5C,
                "Poleturner captured/selected production words differ from the audited layout");
            // Existing Miller helper holds only the dedicated yard identities here:
            // inactive, no mill or Miller observer/capacity request is created.
            Require(millerWorld==null || !millerWorld.Active,"Poleturner cannot overwrite an active Miller fixture");
            millerWorld=new MillerWorld{Active=false};ExtendMillerStorage();
            RequestPoleturnerCapacity();
            log("automatic-poleturner-world workshop="+poleturnerWorld.Workshop+"/"+poleturnerWorld.WorkshopGlobal+
                " armoury="+poleturnerWorld.Armoury+"/"+poleturnerWorld.ArmouryGlobal+" naturalEmployees=true rawWorkerWrites=false");
        }
        private unsafe GameBuilding* ValidatePoleturner(Actor actor)
        {
            var unit=Resolve(actor);var proof=poleturnerWorld;GameBuilding* workshop=null;
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_UnitChimp==eChimps.CHIMP_TYPE_POLETURNER &&
                unit->r_ControllableForPlayerId==owner && unit->r_LinkedProductionBuildingId==proof.Workshop &&
                unit->r_LinkedProductionBuildingGlobalId==proof.WorkshopGlobal &&
                BuildingAccess.TryGetById(proof.Workshop,out workshop,out _) && workshop!=null &&
                workshop->r_GlobalId==proof.WorkshopGlobal && workshop->r_AliveState==AliveState.IsAlive &&
                workshop->r_PlayerIdOwner==owner && workshop->r_BuildingType==eStructs.STRUCT_POLETURNERS_WORKSHOP,
                "Original Poleturner/workplace lifetime changed");
            Require(proof.CapturedKind==1 && (runtime.Native.Word(unit,0xA46)>>8)==proof.CapturedKind && workshop->r_NextProducedGoodId==proof.Product &&
                workshop->r_ProducedGoodId==proof.Product && proof.Product==eGoods.STORED_SPEARS,
                "Poleturner captured A47 kind or latched/selected product changed");
            return workshop;
        }
        private unsafe bool PoleturnerFutureAccessSafe(UnitSnapshot before,GameBuilding* workshop,int x,int y)
        {
            if(AppleStepContains(workshop->r_TileAccessPositionX,workshop->r_TileAccessPositionY,x,y,1) ||
                AppleStepContains(runtime.Native.Word(Resolve(poleturnerWorld.Actor),0x992),runtime.Native.Word(Resolve(poleturnerWorld.Actor),0x994),x,y,1) ||
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
        private unsafe Actor FindNaturalPoleturner()
        {
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=eChimps.CHIMP_TYPE_POLETURNER ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=poleturnerWorld.Workshop ||
                    u->r_LinkedProductionBuildingGlobalId!=poleturnerWorld.WorkshopGlobal || u->r_AIState!=8 || u->r_CarryGoodAmount==0)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                if(before==null || before.Unsupported!=null || before.Job==null || !before.OrdinaryStep || before.Substep>=8 ||
                    !TryStartedMoatAnchor(before,out int x,out int y))continue;
                if(!before.Job.TryGetValue(0xA47,out uint capturedKind) || capturedKind!=1)continue;
                poleturnerWorld.CapturedKind=(int)capturedKind;
                var actor=new Actor{Id=id,Global=u->r_GlobalId,Type=u->r_UnitChimp,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,
                    X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y};
                poleturnerWorld.Actor=actor;
                if(!PoleturnerFutureAccessSafe(before,ValidatePoleturner(actor),x,y)){poleturnerWorld.Actor=null;continue;}
                int store=runtime.Native.Word(u,0x98E);
                if(!BuildingAccess.TryGetById(store,out GameBuilding* armoury,out _) || armoury==null ||
                    armoury->r_GlobalId!=runtime.Native.JobDword(u,0x6F8) || armoury->r_AliveState!=AliveState.IsAlive ||
                    armoury->r_PlayerIdOwner!=owner || armoury->r_BuildingType!=eStructs.STRUCT_ARMOURY)
                {poleturnerWorld.Actor=null;continue;}
                var shop=ValidatePoleturner(actor);
                if(((int*)&shop->r_NullAmount)[(int)eGoods.STORED_WOOD_PLANKS]!=0)
                {poleturnerWorld.Actor=null;continue;}
                poleturnerWorld.Before=before;return actor;
            }
            return null;
        }
        private void ArmPoleturnerMoves()
        {
            if(!poleturnerMovesRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-poleturner-output",new PoleturnerMoveObserver(),out string reason,
                    order:200,enabled:()=>poleturnerMoveFixture!=null && !poleturnerMoveFixture.stopped && poleturnerMoveFixture.expectedEnabled &&
                        poleturnerMoveFixture.poleturnerWorld!=null && poleturnerMoveFixture.poleturnerWorld.Active && poleturnerMoveFixture.poleturnerWorld.Actor!=null &&
                        poleturnerMoveFixture.poleturnerWorld.Before!=null && poleturnerMoveFixture.poleturnerWorld.Batches==0 && poleturnerMoveFixture.poleturnerWorld.Fault==null),reason);
                poleturnerMovesRegistered=true;
            }
            poleturnerMoveFixture=this;
        }
        private sealed class PoleturnerMoveObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var f=poleturnerMoveFixture;var p=f?.poleturnerWorld;
                if(f==null || p?.Actor==null || context==null || context.SessionId!=f.session || context.UnitId!=p.Actor.Id)return null;
                try
                {
                    f.ValidatePoleturner(p.Actor);var u=f.Resolve(p.Actor);bool own=context.OriginOwnerGuid==Plugin.RescueOwnerGuid;
                    Require(u->r_AIState==8 && u->r_CarryGoodAmount==p.Remaining && context.Extra==0,
                        "Poleturner output move changed phase/cargo");
                    int store=f.runtime.Native.Word(u,0x98E);uint global=f.runtime.Native.JobDword(u,0x6F8);
                    Require(BuildingAccess.TryGetById(store,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==global &&
                        b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==f.owner && b->r_BuildingType==eStructs.STRUCT_ARMOURY,
                        "Poleturner output move lost its bound armoury");
                    if(own)
                    {
                        IntPtr pointer=(IntPtr)u;
                        Require(store==p.DeliveryStore && global==p.DeliveryGlobal && context.X==p.Before.RequestedX && context.Y==p.Before.RequestedY &&
                            u->r_TargetTilePositionX2==context.X && u->r_TargetTilePositionY2==context.Y &&
                            p.Before.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,
                                o=>f.runtime.Native.JobWord((GameUnit*)pointer,o),o=>f.runtime.Native.JobDword((GameUnit*)pointer,o)),
                            "Owned Poleturner continuation changed original job/binding/goal");
                    }
                    else
                    {
                        Require(p.OwnResume && p.Handoffs<16 && string.IsNullOrEmpty(context.OriginOwnerGuid) && context.ParentInvocationId==0 &&
                            u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY &&
                            context.X==b->r_TileAccessPositionX && context.Y==b->r_TileAccessPositionY,
                            "Poleturner armoury reselection lacks the original owned resume and exact new access");
                        Require(BuildingAccess.TryGetById(p.DeliveryStore,out GameBuilding* old,out _) && old!=null &&
                            old->r_GlobalId==p.DeliveryGlobal && old->r_AliveState==AliveState.IsAlive && old->r_PlayerIdOwner==f.owner &&
                            old->r_BuildingType==eStructs.STRUCT_ARMOURY &&
                            (Math.Abs(u->r_CurrentTilePositionX-old->r_TileAccessPositionX)>=3 ||
                             Math.Abs(u->r_CurrentTilePositionY-old->r_TileAccessPositionY)>=3 || !f.PoleturnerArmouryCanReceive(u,old)),
                            "Poleturner changed armoury without native C9080/shared-C6EA0 rejection");
                    }
                    return new PoleturnerMoveCompletion(f,p,context,own,store,global);
                }
                catch(Exception error){f.PoleturnerFault(error.Message);return null;}
            }
        }
        private sealed class PoleturnerMoveCompletion : IUnitMoveInvocation
        {
            private readonly Fixture f;private readonly PoleturnerWorld p;private readonly UnitMoveContext c;
            private readonly bool own;private readonly int store;private readonly uint global;private int completed;
            internal PoleturnerMoveCompletion(Fixture f,PoleturnerWorld p,UnitMoveContext c,bool own,int store,uint global)
            {this.f=f;this.p=p;this.c=c;this.own=own;this.store=store;this.global=global;}
            public unsafe void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || !p.Active || f.stopped || p.Fault!=null)return;
                try
                {
                    Require(result!=null && ReferenceEquals(result.Context,c) && result.IsCurrentWorld && result.NativeInvoked &&
                        result.NativeReturned && result.Failure==null,"Poleturner output move has no current native result");
                    f.ValidatePoleturner(p.Actor);var u=f.Resolve(p.Actor);
                    Require(u->r_AIState==8 && u->r_CarryGoodAmount==p.Remaining && f.runtime.Native.Word(u,0x98E)==store &&
                        f.runtime.Native.JobDword(u,0x6F8)==global,"Poleturner native output move changed original cargo/binding");
                    f.log("automatic-poleturner-output-move own="+own+" actualResult="+result.NativeResult+" store="+store+"/"+global+" goal="+c.X+","+c.Y);
                    if(own && result.NativeResult==0)return;
                    Require(result.NativeResult==1 && u->r_TargetTilePositionX2==c.X && u->r_TargetTilePositionY2==c.Y,
                        "Poleturner output handoff lacks an actual successful native route");
                    if(own)p.OwnResume=true;else p.Handoffs++;
                    p.DeliveryStore=store;p.DeliveryGlobal=global;p.DeliveryX=c.X;p.DeliveryY=c.Y;
                }
                catch(Exception error){f.PoleturnerFault(error.Message);}
            }
        }
        private void PoleturnerFault(string reason)
        {if(poleturnerWorld!=null && poleturnerWorld.Fault==null){poleturnerWorld.Fault=reason;log("automatic-poleturner-first-failure "+reason);}}
        private unsafe bool PoleturnerArmouryCanReceive(GameUnit* unit,GameBuilding* store)
        {
            if(Math.Abs(unit->r_CurrentTilePositionX-store->r_TileAccessPositionX)>=3 ||
                Math.Abs(unit->r_CurrentTilePositionY-store->r_TileAccessPositionY)>=3)return false;
            //C6EA0 ignores the requested product: all25 physical goods share50 slots.
            int* amounts=(int*)&store->r_NullAmount;long total=0;
            for(int good=(int)eGoods.STORED_NULL;good<(int)eGoods.Count;good++)
            {if(amounts[good]<0)return false;total+=amounts[good];}
            return total<50;
        }
        private unsafe void ObservePoleturnerGoods(AddGoodToGoodsyardEventArgs args)
        {
            var p=poleturnerWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id ||
                args.Good!=eGoods.STORED_WOOD_PLANKS && args.Good!=eGoods.STORED_SPEARS && args.Good!=eGoods.STORED_PIKES)return;
            string frame="phase="+args.Phase+" unit="+p.Actor.Id+"/"+p.Actor.Global+" building="+args.BuildingId+"/"+args.BuildingGlobalId+
                " good="+args.Good+" delta="+args.AddAmount+" capacity="+args.Capacity+" add="+args.Add;
            try
            {
                log("automatic-poleturner-goods-frame "+frame);ValidatePoleturner(p.Actor);var u=Resolve(p.Actor);
                bool local=args.BuildingId==p.Workshop,output=args.Good!=eGoods.STORED_WOOD_PLANKS;
                Require(BuildingAccess.TryGetById(args.BuildingId,out GameBuilding* store,out _) && store!=null &&
                    store->r_GlobalId==unchecked((uint)args.BuildingGlobalId) && store->r_AliveState==AliveState.IsAlive &&
                    store->r_PlayerIdOwner==owner && store->r_BuildingType==(local?eStructs.STRUCT_POLETURNERS_WORKSHOP:
                        output?eStructs.STRUCT_ARMOURY:eStructs.STRUCT_GOODS_YARD),
                    "Poleturner operation lost its exact original workplace/store lifetime");
                if(local)
                    Require(!output && store->r_GlobalId==p.WorkshopGlobal && u->r_CurrentTilePositionX==runtime.Native.Word(u,0x992) &&
                        u->r_CurrentTilePositionY==runtime.Native.Word(u,0x994),"Poleturner local material credit is not physically at its original home");
                else
                    Require(runtime.Native.Word(u,0x98E)==args.BuildingId && runtime.Native.JobDword(u,0x6F8)==store->r_GlobalId &&
                        Math.Abs(u->r_CurrentTilePositionX-store->r_TileAccessPositionX)<3 && Math.Abs(u->r_CurrentTilePositionY-store->r_TileAccessPositionY)<3,
                        "Poleturner store operation lacks exact active binding and native physical contact");
                int amount=((int*)&store->r_NullAmount)[(int)args.Good];
                if(args.Phase==EventHookPhase.Pre)
                {
                    Require(args.Add && args.Capacity==(output?50:48),"Poleturner native write/capacity contract changed");
                    if(output)
                    {
                        Require(!local && args.Good==p.Product && args.AddAmount==1 && u->r_AIState==8 && u->r_CarryGoodAmount>0 &&
                            PoleturnerArmouryCanReceive(u,store),"Poleturner output is not the captured product into a valid shared Armoury");
                        if(p.Remaining==0)
                        {
                            Require(p.Batches==1 && p.Inputs==1 && p.HomeInputs==1 && p.WoodUsed==1 && p.Yields==1 && !p.YieldPending,
                                "Poleturner second batch lacks exact fresh material consumption/production");
                            p.SecondCargo=p.Remaining=u->r_CarryGoodAmount;
                        }
                        Require(u->r_CarryGoodAmount==p.Remaining,"Poleturner cargo differs from its full original batch");
                        if(expectedEnabled && p.Batches==0)Require(p.OwnResume && args.BuildingId==p.DeliveryStore &&
                            store->r_GlobalId==p.DeliveryGlobal && u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY,
                            "Poleturner first batch lacks its original owned resume and native Armoury handoff");
                    }
                    else
                    {
                        Require(p.Batches==1 && p.Remaining==0 && u->r_AIState==3 && u->r_CarryGoodAmount==1,
                            "Poleturner Wood operation is not the committed fresh R3 input");
                        if(local)Require(args.AddAmount==1 && p.Inputs==1 && p.HomeInputs==0 && amount==0,
                            "Poleturner local Wood credit lacks its single fresh pickup");
                        else Require(args.AddAmount==-1 && p.Inputs==0,"Poleturner duplicated its fresh Wood pickup");
                    }
                    p.Goods.Push(new PoleturnerGoodsFrame{Store=args.BuildingId,Global=store->r_GlobalId,Good=args.Good,
                        Amount=args.AddAmount,Before=amount,Cargo=u->r_CarryGoodAmount});
                }
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.Goods.Count>0,"Poleturner goods Post lacks matching Pre");var old=p.Goods.Pop();
                    Require(old.Store==args.BuildingId && old.Global==store->r_GlobalId && old.Good==args.Good && old.Amount==args.AddAmount &&
                        amount-old.Before==old.Amount && u->r_CarryGoodAmount==old.Cargo,
                        "Poleturner physical goods delta/lifetime/cargo changed across native write");
                    if(output){p.Deposits++;if(--p.Remaining==0)p.Batches++;}
                    else if(local){p.HomeInputs++;p.LastWood=amount;}else p.Inputs++;
                    log("automatic-poleturner-native-goods "+frame+" product="+p.Product+" batches="+p.Batches+
                        " remaining="+p.Remaining+" freshWood="+p.Inputs+" homeInputs="+p.HomeInputs);
                }
            }
            catch(Exception error){PoleturnerFault(error.Message+" ["+frame+"]");}
        }
        private unsafe void ObservePoleturnerYield(UnitCalculateBonusYieldEventArgs args)
        {
            var p=poleturnerWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null || args.UnitId!=p.Actor.Id ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            try
            {
                ValidatePoleturner(p.Actor);var u=Resolve(p.Actor);
                //13AAD0 AI5 publishes output yield after the actual material
                // consumption and commits109/saved7 before the call.
                if(u->r_AIState!=109 || runtime.Native.Word(u,0x91A)!=7)return;
                Require(p.Batches==1 && p.Remaining==0 && p.Inputs==1 && p.HomeInputs==1 && p.WoodUsed==1 && p.Yields==0 && args.GoodAmount>0,
                    "Poleturner production did not follow one fresh Wood input and actual local consumption");
                if(args.Phase==EventHookPhase.Pre)
                {Require(!p.YieldPending,"Nested Poleturner production yield");p.YieldPending=true;p.YieldArgument=args.GoodAmount;}
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.YieldPending && args.GoodAmount==p.YieldArgument && args.ReturnValue>0,
                        "Poleturner natural production yield has no matching positive completion");
                    p.YieldPending=false;p.Yields++;
                    log("automatic-poleturner-natural-production unit="+p.Actor.Id+"/"+p.Actor.Global+
                        " inputOperations="+p.Inputs+" argument="+args.GoodAmount+" observedYield="+args.ReturnValue+" savedAI=7");
                }
            }
            catch(Exception error){PoleturnerFault(error.Message);}
        }
        private unsafe void LogPoleturnerAcquisition(int sample)
        {
            var facts=new List<string>();
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=eChimps.CHIMP_TYPE_POLETURNER ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=poleturnerWorld.Workshop)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                facts.Add(id+"/"+u->r_GlobalId+":ai="+u->r_AIState+",saved="+runtime.Native.Word(u,0x91A)+
                    ",flags="+u->r_PathPlanStateBitFlags+",ss="+u->r_MovementSubstep+",cargo="+u->r_CarryGoodAmount+
                    ",at="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+",next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+
                    ",goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+",unsupported="+(before?.Unsupported??"none"));
                if(facts.Count==3)break;
            }
            log("automatic-poleturner-acquire sample="+sample+" workshop="+poleturnerWorld.Workshop+"/"+poleturnerWorld.WorkshopGlobal+" facts="+string.Join(" | ",facts));
        }
        private unsafe AutomaticWorkerMutation PreparePoleturnerMutation(Actor actor)
        {
            var p=poleturnerWorld;var u=Resolve(actor);var acquired=p.Before;
            var before=runtime.SnapshotUnit(actor.Id,true,false);int x=0,y=0;
            Require(acquired!=null && before!=null && before.Unsupported==null && before.Job!=null && before.OrdinaryStep && before.AIState==8 &&
                before.GlobalId==actor.Global && before.X==acquired.X && before.Y==acquired.Y && before.NextX==acquired.NextX &&
                before.NextY==acquired.NextY && before.Substep==acquired.Substep && before.Flags==acquired.Flags &&
                before.RequestedX==acquired.RequestedX && before.RequestedY==acquired.RequestedY &&
                TryStartedMoatAnchor(before,out x,out y) && PoleturnerFutureAccessSafe(before,ValidatePoleturner(actor),x,y),
                "Natural loaded Poleturner's accepted source/goal/access changed before moat construction");
            p.Before=before;p.InitialCargo=p.Remaining=u->r_CarryGoodAmount;Require(p.InitialCargo>0,"Natural Poleturner lost its weapon cargo");
            p.DeliveryStore=runtime.Native.Word(u,0x98E);p.DeliveryGlobal=runtime.Native.JobDword(u,0x6F8);
            p.DeliveryX=before.RequestedX;p.DeliveryY=before.RequestedY;actors.Add(actor);ArmPoleturnerMoves();
            return ApplyAutomaticStartedWorkerMoat(actor,before,x,y);
        }
        private unsafe bool PoleturnerCycleComplete(Actor actor)
        {ValidatePoleturner(actor);return poleturnerWorld.Complete;}
        private IEnumerable<AutomationWait> AutomaticPoleturner(AutomationRun run)
        {
            poleturnerWorld=new PoleturnerWorld();var p=poleturnerWorld;
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObservePoleturnerGoods))
            using(var production=UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(ObservePoleturnerYield))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupPoleturnerWorld();});
                while(p.CapacityPending)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Require(p.Fault==null,p.Fault);
                }
                Execute(run,()=>{
                    Require(p.Wood!=null && p.Wood.HasStorage && p.Wood.FreeAmount>=12 && p.Weapon!=null &&
                        p.Weapon.HasStorage && p.Weapon.FreeAmount>=8,"Natural Poleturner lacks current ingredient/output capacity");
                    int prior=GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_WOOD_PLANKS);
                    Require(GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_WOOD_PLANKS,12) &&
                        GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_WOOD_PLANKS)==prior+12,
                        "Natural Poleturner real Wood input was not physically admitted");
                });
                Actor actor=null;
                for(int sample=0;sample<2000 && actor==null;sample++)
                {
                    if(sample==128)Director.instance.SetEngineFrameRate(50);
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,sample<128?16:1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{Require(p.Fault==null,p.Fault);actor=FindNaturalPoleturner();if(sample%128==0)LogPoleturnerAcquisition(sample);return true;});
                }
                Require(actor!=null,"No natural loaded Poleturner R8 qualified on a clear accepted step outside future access");
                Director.instance.SetEngineFrameRate(250);
                AutomaticWorkerMutation mutation=null;
                // Execute suppresses fixture construction: the tested mutation MUST
                // use only the ordinary simulation lock, without that suppression.
                run.InSimulation(run.CurrentMission,()=>{mutation=PreparePoleturnerMutation(actor);return true;});
                bool complete=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?6000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        Require(p.Fault==null,p.Fault);if(!AutomaticActorAlive(actor))return true;
                        complete=PoleturnerCycleComplete(actor);
                        return expectedEnabled && complete;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    Require(p.Fault==null && p.Goods.Count==0 && !p.YieldPending,"Poleturner native observation incomplete: "+p.Fault);
                    if(expectedEnabled)Require(complete,"Original Poleturner did not complete its full weapon batch, fresh Wood consumption, natural production and full second delivery");
                    log("automatic-poleturner-cycle enabled="+expectedEnabled+" initialCargo="+p.InitialCargo+" secondCargo="+p.SecondCargo+
                        " capturedA47="+p.CapturedKind+" product="+p.Product+" weaponCredits="+p.Deposits+" batches="+p.Batches+" freshWood="+p.Inputs+" naturalProduction="+p.Yields+" homeInputs="+p.HomeInputs+" consumedWood="+p.WoodUsed+
                        " ownOriginalResume="+p.OwnResume+" nativeArmouryHandoffs="+p.Handoffs+" complete="+complete);
                    EndAutomaticWorkerCase(mutation,complete);
                });
            }
            finally
            {
                p.Active=false;if(ReferenceEquals(poleturnerCapacityFixture,this))poleturnerCapacityFixture=null;
                if(ReferenceEquals(poleturnerMoveFixture,this))poleturnerMoveFixture=null;
            }
        }
    }
}
