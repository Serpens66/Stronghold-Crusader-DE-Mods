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
        // Acceptance-only harness: shared transport/storage receipts, explicit
        // role-specific production protocols. No AI, worker, cargo or yield writes.
        private sealed class IronWorkshopWorld
        {
            internal int Workshop,Armoury,InitialCargo,Remaining,Batches,Deposits,Inputs,Yields,SecondCargo,CapacityWait;
            internal uint WorkshopGlobal,ArmouryGlobal;
            internal bool Armourer;
            internal eGoods Product;
            internal eChimps WorkerType=>Armourer?eChimps.CHIMP_TYPE_ARMOURER:eChimps.CHIMP_TYPE_BLACKSMITH;
            internal eStructs BuildingType=>Armourer?eStructs.STRUCT_ARMOURERS_WORKSHOP:eStructs.STRUCT_BLACKSMITHS_WORKSHOP;
            internal eMappers Mapper=>Armourer?eMappers.MAPPER_ARMOURER:eMappers.MAPPER_BLACKSMITH;
            internal string Role=>Armourer?"armourer":"smith";
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal bool Active=true,CapacityPending,YieldPending,OwnResume,Complete;
            internal int YieldArgument,DeliveryStore,DeliveryX,DeliveryY,Handoffs,HomeInputs,IronUsed,LocalProduced,LocalConsumed;
            internal uint DeliveryGlobal;
            internal string Fault;
            internal AllyTransferCapacity Iron,Weapon;
            internal readonly Stack<IronWorkshopGoodsFrame> Goods=new Stack<IronWorkshopGoodsFrame>();
        }
        private sealed class IronWorkshopGoodsFrame
        {internal int Store,Amount,Before,Cargo;internal uint Global;internal eGoods Good;}
        private IronWorkshopWorld ironWorkshopWorld;
        private static bool ironWorkshopCapacityRegistered,ironWorkshopMovesRegistered;
        private static volatile Fixture ironWorkshopCapacityFixture,ironWorkshopMoveFixture;
        private IEnumerable<AutomationStep> AutomaticSmithSteps()
        {yield return new AutomationStep("automatic-natural-smith-moat-and-two-batches",TimeSpan.FromSeconds(900),run=>AutomaticIronWorkshop(run,false),true);}
        private IEnumerable<AutomationStep> AutomaticArmourerSteps()
        {yield return new AutomationStep("automatic-natural-armourer-moat-and-two-batches",TimeSpan.FromSeconds(900),run=>AutomaticIronWorkshop(run,true),true);}

        private void RequestIronWorkshopCapacity()
        {
            if(!ironWorkshopCapacityRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-ironWorkshop-capacity",ReadIronWorkshopCapacity,out string reason),reason);
                ironWorkshopCapacityRegistered=true;
            }
            ironWorkshopCapacityFixture=this;ironWorkshopWorld.CapacityPending=true;
        }
        private static unsafe void ReadIronWorkshopCapacity(int tick)
        {
            var f=ironWorkshopCapacityFixture;var p=f?.ironWorkshopWorld;
            if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null)return;
            try
            {
                if(p.Batches==2 && !p.Complete)
                {
                    f.ValidateIronWorkshop(p.Actor);
                    Require(f.Resolve(p.Actor)->r_CarryGoodAmount==0 && p.Inputs==1 && p.HomeInputs==1 && p.IronUsed==1 && p.Yields==1 &&
                        (!p.Armourer || p.LocalProduced==1 && p.LocalConsumed==1),
                        "IronWorkshop final deposit did not complete its original cargo/production cycle");
                    p.Complete=true;
                    f.log("automatic-ironWorkshop-complete-readback tick="+tick+" unit="+p.Actor.Id+"/"+p.Actor.Global+" cargo=0 batches=2");
                }
                if(!p.CapacityPending)return;
                if(!f.MillerAddedYardsReady())
                {Require(++p.CapacityWait<=64,"IronWorkshop ingredient yards did not initialize");return;}
                Require(BuildingAccess.TryGetById(p.Armoury,out GameBuilding* armoury,out _) && armoury!=null &&
                    armoury->r_GlobalId==p.ArmouryGlobal && armoury->r_PlayerIdOwner==f.owner &&
                    armoury->r_BuildingType==eStructs.STRUCT_ARMOURY,"IronWorkshop setup Armoury lifetime changed");
                Require(BuildingAccess.TryGetById(p.Workshop,out GameBuilding* workshop,out _) && workshop!=null &&
                    workshop->r_GlobalId==p.WorkshopGlobal,"IronWorkshop setup workshop lifetime changed");
                if(armoury->r_AliveState==AliveState.NeedsInit || workshop->r_AliveState==AliveState.NeedsInit)
                {Require(++p.CapacityWait<=64,"IronWorkshop buildings did not initialize");return;}
                Require(armoury->r_AliveState==AliveState.IsAlive && workshop->r_AliveState==AliveState.IsAlive,
                    "Natural iron workshop initialization failed");
                if(p.Armourer)p.Product=eGoods.STORED_METAL_ARMOUR;
                else
                {
                    p.Product=workshop->r_ProducedGoodId;
                    Require(p.Product==eGoods.STORED_MACES || p.Product==eGoods.STORED_SWORDS,
                        "Natural Smith selected product is neither Maces nor Swords; no product switch is permitted");
                }
                Require(AllyTransfers.TryGetCapacity(f.owner,eGoods.STORED_IRON_INGOTS,out p.Iron,out string ironReason),ironReason);
                Require(AllyTransfers.TryGetCapacity(f.owner,p.Product,out p.Weapon,out string weaponReason),weaponReason);
                p.CapacityPending=false;
                f.log("automatic-ironWorkshop-capacity tick="+tick+" ironFree="+p.Iron.FreeAmount+" sharedArmouryFree="+p.Weapon.FreeAmount);
            }
            catch(Exception error){f.IronWorkshopFault(error.Message);p.CapacityPending=false;}
        }
        private unsafe void SetupIronWorkshopWorld()
        {
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"IronWorkshop fixture requires the actual human owner");
            FindSite();
            CreateBakerPrefab(cx-10,cy+7,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL);
            ironWorkshopWorld.Workshop=CreateBakerPrefab(cx,cy,ironWorkshopWorld.Mapper,ironWorkshopWorld.BuildingType);
            Require(BuildingAccess.TryGetById(ironWorkshopWorld.Workshop,out GameBuilding* workshop,out _) && workshop!=null,
                "Created Workshop lifetime cannot be read back");ironWorkshopWorld.WorkshopGlobal=workshop->r_GlobalId;
            ironWorkshopWorld.Armoury=CreateBakerPrefab(cx-10,cy,eMappers.MAPPER_ARMOURY,eStructs.STRUCT_ARMOURY);
            Require(BuildingAccess.TryGetById(ironWorkshopWorld.Armoury,out GameBuilding* armoury,out _) && armoury!=null,
                "Created IronWorkshop armoury lifetime cannot be read back");ironWorkshopWorld.ArmouryGlobal=armoury->r_GlobalId;
            Require((byte*)&workshop->r_NextProducedGoodId-(byte*)workshop==0x2EA-0x5C &&
                (byte*)&workshop->r_ProducedGoodId-(byte*)workshop==0x2EC-0x5C,
                "IronWorkshop latched/desired production words differ from the audited layout");
            // Existing Miller helper holds only the dedicated yard identities here:
            // inactive, no mill or Miller observer/capacity request is created.
            Require(millerWorld==null || !millerWorld.Active,"IronWorkshop cannot overwrite an active Miller fixture");
            millerWorld=new MillerWorld{Active=false};ExtendMillerStorage();
            RequestIronWorkshopCapacity();
            log("automatic-ironWorkshop-world workshop="+ironWorkshopWorld.Workshop+"/"+ironWorkshopWorld.WorkshopGlobal+
                " armoury="+ironWorkshopWorld.Armoury+"/"+ironWorkshopWorld.ArmouryGlobal+" naturalEmployees=true rawWorkerWrites=false");
        }
        private unsafe GameBuilding* ValidateIronWorkshop(Actor actor)
        {
            var unit=Resolve(actor);var proof=ironWorkshopWorld;GameBuilding* workshop=null;
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_UnitChimp==proof.WorkerType &&
                unit->r_ControllableForPlayerId==owner && unit->r_LinkedProductionBuildingId==proof.Workshop &&
                unit->r_LinkedProductionBuildingGlobalId==proof.WorkshopGlobal &&
                BuildingAccess.TryGetById(proof.Workshop,out workshop,out _) && workshop!=null &&
                workshop->r_GlobalId==proof.WorkshopGlobal && workshop->r_AliveState==AliveState.IsAlive &&
                workshop->r_PlayerIdOwner==owner && workshop->r_BuildingType==proof.BuildingType,
                "Original IronWorkshop/workplace lifetime changed");
            // Native 13CF30 uses constant Metal Armour and does not read either
            // product word. Only Smith 13BDD0 owns desired2EC/latched2EA.
            if(!proof.Armourer)Require(workshop->r_NextProducedGoodId==proof.Product && workshop->r_ProducedGoodId==proof.Product &&
                (proof.Product==eGoods.STORED_MACES || proof.Product==eGoods.STORED_SWORDS),
                "Smith latched/selected original product changed");
            return workshop;
        }
        private unsafe bool IronWorkshopFutureAccessSafe(UnitSnapshot before,GameBuilding* workshop,int x,int y)
        {
            if(AppleStepContains(workshop->r_TileAccessPositionX,workshop->r_TileAccessPositionY,x,y,1) ||
                AppleStepContains(runtime.Native.Word(Resolve(ironWorkshopWorld.Actor),0x992),runtime.Native.Word(Resolve(ironWorkshopWorld.Actor),0x994),x,y,1) ||
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
        private unsafe Actor FindNaturalIronWorkshop()
        {
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=ironWorkshopWorld.WorkerType ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=ironWorkshopWorld.Workshop ||
                    u->r_LinkedProductionBuildingGlobalId!=ironWorkshopWorld.WorkshopGlobal || u->r_AIState!=8 || u->r_CarryGoodAmount==0)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                if(before==null || before.Unsupported!=null || before.Job==null || !before.OrdinaryStep || before.Substep>=8 ||
                    !TryStartedMoatAnchor(before,out int x,out int y))continue;
                var actor=new Actor{Id=id,Global=u->r_GlobalId,Type=u->r_UnitChimp,Health=u->r_CurrentHealth,Tribe=u->r_TribeId,
                    X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y};
                ironWorkshopWorld.Actor=actor;
                if(!IronWorkshopFutureAccessSafe(before,ValidateIronWorkshop(actor),x,y)){ironWorkshopWorld.Actor=null;continue;}
                int store=runtime.Native.Word(u,0x98E);
                if(!BuildingAccess.TryGetById(store,out GameBuilding* armoury,out _) || armoury==null ||
                    armoury->r_GlobalId!=runtime.Native.JobDword(u,0x6F8) || armoury->r_AliveState!=AliveState.IsAlive ||
                    armoury->r_PlayerIdOwner!=owner || armoury->r_BuildingType!=eStructs.STRUCT_ARMOURY ||
                    before.RequestedX!=armoury->r_TileAccessPositionX || before.RequestedY!=armoury->r_TileAccessPositionY)
                {ironWorkshopWorld.Actor=null;continue;}
                var shop=ValidateIronWorkshop(actor);
                if(((int*)&shop->r_NullAmount)[(int)eGoods.STORED_IRON_INGOTS]!=0 ||
                    ((int*)&shop->r_NullAmount)[(int)ironWorkshopWorld.Product]!=0)
                {ironWorkshopWorld.Actor=null;continue;}
                ironWorkshopWorld.Before=before;return actor;
            }
            return null;
        }
        private void ArmIronWorkshopMoves()
        {
            if(!ironWorkshopMovesRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-ironWorkshop-output",new IronWorkshopMoveObserver(),out string reason,
                    order:200,enabled:()=>ironWorkshopMoveFixture!=null && !ironWorkshopMoveFixture.stopped && ironWorkshopMoveFixture.expectedEnabled &&
                        ironWorkshopMoveFixture.ironWorkshopWorld!=null && ironWorkshopMoveFixture.ironWorkshopWorld.Active && ironWorkshopMoveFixture.ironWorkshopWorld.Actor!=null &&
                        ironWorkshopMoveFixture.ironWorkshopWorld.Before!=null && ironWorkshopMoveFixture.ironWorkshopWorld.Batches==0 && ironWorkshopMoveFixture.ironWorkshopWorld.Fault==null),reason);
                ironWorkshopMovesRegistered=true;
            }
            ironWorkshopMoveFixture=this;
        }
        private sealed class IronWorkshopMoveObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var f=ironWorkshopMoveFixture;var p=f?.ironWorkshopWorld;
                if(f==null || p?.Actor==null || context==null || context.SessionId!=f.session || context.UnitId!=p.Actor.Id)return null;
                try
                {
                    f.ValidateIronWorkshop(p.Actor);var u=f.Resolve(p.Actor);bool own=context.OriginOwnerGuid==Plugin.Guid;
                    Require(u->r_AIState==8 && u->r_CarryGoodAmount==p.Remaining && context.Extra==0,
                        "IronWorkshop output move changed phase/cargo");
                    int store=f.runtime.Native.Word(u,0x98E);uint global=f.runtime.Native.JobDword(u,0x6F8);
                    Require(BuildingAccess.TryGetById(store,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==global &&
                        b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==f.owner && b->r_BuildingType==eStructs.STRUCT_ARMOURY,
                        "IronWorkshop output move lost its bound armoury");
                    if(own)
                    {
                        IntPtr pointer=(IntPtr)u;
                        Require(store==p.DeliveryStore && global==p.DeliveryGlobal && context.X==p.Before.RequestedX && context.Y==p.Before.RequestedY &&
                            u->r_TargetTilePositionX2==context.X && u->r_TargetTilePositionY2==context.Y &&
                            p.Before.Job.MatchesContinuation(u->r_UnitChimp,u->r_AIState,
                                o=>f.runtime.Native.JobWord((GameUnit*)pointer,o),o=>f.runtime.Native.JobDword((GameUnit*)pointer,o)),
                            "Owned IronWorkshop continuation changed original job/binding/goal");
                    }
                    else
                    {
                        Require(p.OwnResume && p.Handoffs<16 && string.IsNullOrEmpty(context.OriginOwnerGuid) && context.ParentInvocationId==0 &&
                            u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY &&
                            context.X==b->r_TileAccessPositionX && context.Y==b->r_TileAccessPositionY,
                            "IronWorkshop armoury reselection lacks the original owned resume and exact new access");
                        Require(BuildingAccess.TryGetById(p.DeliveryStore,out GameBuilding* old,out _) && old!=null &&
                            old->r_GlobalId==p.DeliveryGlobal && old->r_AliveState==AliveState.IsAlive && old->r_PlayerIdOwner==f.owner &&
                            old->r_BuildingType==eStructs.STRUCT_ARMOURY &&
                            (Math.Abs(u->r_CurrentTilePositionX-old->r_TileAccessPositionX)>=3 ||
                             Math.Abs(u->r_CurrentTilePositionY-old->r_TileAccessPositionY)>=3 || !f.PoleturnerArmouryCanReceive(u,old)),
                            "IronWorkshop changed armoury without native C9080/shared-C6EA0 rejection");
                    }
                    return new IronWorkshopMoveCompletion(f,p,context,own,store,global);
                }
                catch(Exception error){f.IronWorkshopFault(error.Message);return null;}
            }
        }
        private sealed class IronWorkshopMoveCompletion : IUnitMoveInvocation
        {
            private readonly Fixture f;private readonly IronWorkshopWorld p;private readonly UnitMoveContext c;
            private readonly bool own;private readonly int store;private readonly uint global;private int completed;
            internal IronWorkshopMoveCompletion(Fixture f,IronWorkshopWorld p,UnitMoveContext c,bool own,int store,uint global)
            {this.f=f;this.p=p;this.c=c;this.own=own;this.store=store;this.global=global;}
            public unsafe void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || !p.Active || f.stopped || p.Fault!=null)return;
                try
                {
                    Require(result!=null && ReferenceEquals(result.Context,c) && result.IsCurrentWorld && result.NativeInvoked &&
                        result.NativeReturned && result.Failure==null,"IronWorkshop output move has no current native result");
                    f.ValidateIronWorkshop(p.Actor);var u=f.Resolve(p.Actor);
                    Require(u->r_AIState==8 && u->r_CarryGoodAmount==p.Remaining && f.runtime.Native.Word(u,0x98E)==store &&
                        f.runtime.Native.JobDword(u,0x6F8)==global,"IronWorkshop native output move changed original cargo/binding");
                    f.log("automatic-ironWorkshop-output-move own="+own+" actualResult="+result.NativeResult+" store="+store+"/"+global+" goal="+c.X+","+c.Y);
                    if(own && result.NativeResult==0)return;
                    Require(result.NativeResult==1 && u->r_TargetTilePositionX2==c.X && u->r_TargetTilePositionY2==c.Y,
                        "IronWorkshop output handoff lacks an actual successful native route");
                    if(own)p.OwnResume=true;else p.Handoffs++;
                    p.DeliveryStore=store;p.DeliveryGlobal=global;p.DeliveryX=c.X;p.DeliveryY=c.Y;
                }
                catch(Exception error){f.IronWorkshopFault(error.Message);}
            }
        }
        private void IronWorkshopFault(string reason)
        {if(ironWorkshopWorld!=null && ironWorkshopWorld.Fault==null){ironWorkshopWorld.Fault=reason;log("automatic-ironWorkshop-first-failure "+reason);}}
        private unsafe void ObserveIronWorkshopGoods(AddGoodToGoodsyardEventArgs args)
        {
            var p=ironWorkshopWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id ||
                args.Good!=eGoods.STORED_IRON_INGOTS && args.Good!=p.Product)return;
            string frame="role="+p.Role+" phase="+args.Phase+" unit="+p.Actor.Id+"/"+p.Actor.Global+" building="+args.BuildingId+"/"+args.BuildingGlobalId+
                " good="+args.Good+" delta="+args.AddAmount+" capacity="+args.Capacity+" add="+args.Add;
            try
            {
                log("automatic-ironWorkshop-goods-frame "+frame);ValidateIronWorkshop(p.Actor);var u=Resolve(p.Actor);
                bool local=args.BuildingId==p.Workshop,product=args.Good==p.Product,output=product && !local;
                Require(BuildingAccess.TryGetById(args.BuildingId,out GameBuilding* store,out _) && store!=null &&
                    store->r_GlobalId==unchecked((uint)args.BuildingGlobalId) && store->r_AliveState==AliveState.IsAlive &&
                    store->r_PlayerIdOwner==owner && store->r_BuildingType==(local?p.BuildingType:
                        output?eStructs.STRUCT_ARMOURY:eStructs.STRUCT_GOODS_YARD),
                    "Iron workshop operation lost its exact original workplace/store lifetime");
                if(local)
                    Require(store->r_GlobalId==p.WorkshopGlobal && u->r_CurrentTilePositionX==runtime.Native.Word(u,0x992) &&
                        u->r_CurrentTilePositionY==runtime.Native.Word(u,0x994),"Iron workshop local operation is not physically at its original home");
                else
                    Require(runtime.Native.Word(u,0x98E)==args.BuildingId && runtime.Native.JobDword(u,0x6F8)==store->r_GlobalId &&
                        Math.Abs(u->r_CurrentTilePositionX-store->r_TileAccessPositionX)<3 && Math.Abs(u->r_CurrentTilePositionY-store->r_TileAccessPositionY)<3,
                        "Iron workshop store operation lacks exact active binding and native physical contact");
                int amount=((int*)&store->r_NullAmount)[(int)args.Good];
                if(args.Phase==EventHookPhase.Pre)
                {
                    Require(args.Add && args.Capacity==(output?50:48),"Iron workshop native write/capacity contract changed");
                    if(output)
                    {
                        Require(args.AddAmount==1 && u->r_AIState==8 && u->r_CarryGoodAmount>0 && PoleturnerArmouryCanReceive(u,store),
                            "Iron workshop output is not its original product into a valid shared Armoury");
                        if(p.Remaining==0)
                        {
                            Require(p.Batches==1 && p.Inputs==1 && p.HomeInputs==1 && p.IronUsed==1 && p.Yields==1 && !p.YieldPending &&
                                (!p.Armourer || p.LocalProduced==1 && p.LocalConsumed==1),
                                "Iron workshop second batch lacks exact fresh Iron consumption/production");
                            p.SecondCargo=p.Remaining=u->r_CarryGoodAmount;
                        }
                        Require(u->r_CarryGoodAmount==p.Remaining,"Iron workshop cargo differs from its full original batch");
                        if(expectedEnabled && p.Batches==0)Require(p.OwnResume && args.BuildingId==p.DeliveryStore &&
                            store->r_GlobalId==p.DeliveryGlobal && u->r_TargetTilePositionX2==p.DeliveryX && u->r_TargetTilePositionY2==p.DeliveryY,
                            "Iron workshop first batch lacks its original owned resume and native Armoury handoff");
                    }
                    else
                    {
                        Require(p.Batches==1 && p.Remaining==0,"Iron workshop production preceded its full first delivery");
                        if(!local)
                            Require(!product && args.AddAmount==-1 && p.Inputs==0 && u->r_AIState==3 && u->r_CarryGoodAmount==1,
                                "Iron pickup is not the single committed fresh R3 input");
                        else if(!product && args.AddAmount==1)
                            Require(p.Inputs==1 && p.HomeInputs==0 && amount==0 && u->r_CarryGoodAmount==1 &&
                                (p.Armourer?u->r_AIState==109 && runtime.Native.Word(u,0x91A)==6:u->r_AIState==6),
                                "Iron home credit lost its role-specific committed phase and fresh pickup");
                        else if(!product)
                            Require(args.AddAmount==-1 && p.HomeInputs==1 && p.IronUsed==0 && amount==1 && u->r_AIState==6 && u->r_CarryGoodAmount==0,
                                "Local Iron consumption is not the exact fresh production input");
                        else if(args.AddAmount==1)
                            Require(p.Armourer && p.IronUsed==1 && p.LocalProduced==0 && p.LocalConsumed==0 && amount==0 &&
                                u->r_AIState==5 && u->r_CarryGoodAmount==0,
                                "Armourer local product credit lacks its actual fresh Iron production");
                        else
                            Require(p.Armourer && args.AddAmount==-1 && p.IronUsed==1 && p.LocalProduced==1 && p.LocalConsumed==0 && amount==1 &&
                                u->r_AIState==6 && u->r_CarryGoodAmount==0,
                                "Armourer local product withdrawal lacks its exact completed local production");
                    }
                    p.Goods.Push(new IronWorkshopGoodsFrame{Store=args.BuildingId,Global=store->r_GlobalId,Good=args.Good,
                        Amount=args.AddAmount,Before=amount,Cargo=u->r_CarryGoodAmount});
                }
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.Goods.Count>0,"Iron workshop goods Post lacks matching Pre");var old=p.Goods.Pop();
                    Require(old.Store==args.BuildingId && old.Global==store->r_GlobalId && old.Good==args.Good && old.Amount==args.AddAmount &&
                        amount-old.Before==old.Amount && u->r_CarryGoodAmount==old.Cargo,
                        "Iron workshop physical goods delta/lifetime/cargo changed across native write");
                    if(output){p.Deposits++;if(--p.Remaining==0)p.Batches++;}
                    else if(!local)p.Inputs++;
                    else if(!product && old.Amount==1)p.HomeInputs++;
                    else if(!product)p.IronUsed++;
                    else if(old.Amount==1)p.LocalProduced++;
                    else p.LocalConsumed++;
                    log("automatic-ironWorkshop-native-goods "+frame+" product="+p.Product+" batches="+p.Batches+
                        " remaining="+p.Remaining+" freshIron="+p.Inputs+" homeInputs="+p.HomeInputs+
                        " consumedIron="+p.IronUsed+" localProduced="+p.LocalProduced+" localConsumed="+p.LocalConsumed);
                }
            }
            catch(Exception error){IronWorkshopFault(error.Message+" ["+frame+"]");}
        }
        private unsafe void ObserveIronWorkshopYield(UnitCalculateBonusYieldEventArgs args)
        {
            var p=ironWorkshopWorld;
            if(p==null || !p.Active || p.Actor==null || p.Before==null || p.Batches>=2 || p.Fault!=null || args==null || args.UnitId!=p.Actor.Id ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=p.Actor.Id)return;
            try
            {
                ValidateIronWorkshop(p.Actor);var u=Resolve(p.Actor);
                // Smith commits7 directly after unhide. Armourer commits109/
                // saved7 only after its distinct local product credit and debit.
                bool productionPhase=p.Armourer?u->r_AIState==109 && runtime.Native.Word(u,0x91A)==7:u->r_AIState==7;
                if(!productionPhase)return;
                Require(p.Batches==1 && p.Remaining==0 && p.Inputs==1 && p.HomeInputs==1 && p.IronUsed==1 && p.Yields==0 &&
                    args.GoodAmount==1 && (!p.Armourer || p.LocalProduced==1 && p.LocalConsumed==1),
                    "Iron workshop yield did not follow its role-specific fresh Iron production");
                if(args.Phase==EventHookPhase.Pre)
                {Require(!p.YieldPending,"Nested IronWorkshop production yield");p.YieldPending=true;p.YieldArgument=args.GoodAmount;}
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(p.YieldPending && args.GoodAmount==p.YieldArgument && args.ReturnValue>0,
                        "IronWorkshop natural production yield has no matching positive completion");
                    p.YieldPending=false;p.Yields++;
                    log("automatic-ironWorkshop-natural-production unit="+p.Actor.Id+"/"+p.Actor.Global+
                        " inputOperations="+p.Inputs+" argument="+args.GoodAmount+" observedYield="+args.ReturnValue+" role="+p.Role+" actualAI="+u->r_AIState);
                }
            }
            catch(Exception error){IronWorkshopFault(error.Message);}
        }
        private unsafe void LogIronWorkshopAcquisition(int sample)
        {
            var facts=new List<string>();
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_UnitChimp!=ironWorkshopWorld.WorkerType ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=ironWorkshopWorld.Workshop)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                facts.Add(id+"/"+u->r_GlobalId+":ai="+u->r_AIState+",saved="+runtime.Native.Word(u,0x91A)+
                    ",flags="+u->r_PathPlanStateBitFlags+",ss="+u->r_MovementSubstep+",cargo="+u->r_CarryGoodAmount+
                    ",at="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+",next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+
                    ",goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+",unsupported="+(before?.Unsupported??"none"));
                if(facts.Count==3)break;
            }
            log("automatic-ironWorkshop-acquire sample="+sample+" workshop="+ironWorkshopWorld.Workshop+"/"+ironWorkshopWorld.WorkshopGlobal+" facts="+string.Join(" | ",facts));
        }
        private unsafe AutomaticWorkerMutation PrepareIronWorkshopMutation(Actor actor)
        {
            var p=ironWorkshopWorld;var u=Resolve(actor);var acquired=p.Before;
            var before=runtime.SnapshotUnit(actor.Id,true,false);int x=0,y=0;
            Require(acquired!=null && before!=null && before.Unsupported==null && before.Job!=null && before.OrdinaryStep && before.AIState==8 &&
                before.GlobalId==actor.Global && before.X==acquired.X && before.Y==acquired.Y && before.NextX==acquired.NextX &&
                before.NextY==acquired.NextY && before.Substep==acquired.Substep && before.Flags==acquired.Flags &&
                before.RequestedX==acquired.RequestedX && before.RequestedY==acquired.RequestedY &&
                TryStartedMoatAnchor(before,out x,out y) && IronWorkshopFutureAccessSafe(before,ValidateIronWorkshop(actor),x,y),
                "Natural loaded IronWorkshop's accepted source/goal/access changed before moat construction");
            p.Before=before;p.InitialCargo=p.Remaining=u->r_CarryGoodAmount;Require(p.InitialCargo>0,"Natural IronWorkshop lost its original product cargo");
            p.DeliveryStore=runtime.Native.Word(u,0x98E);p.DeliveryGlobal=runtime.Native.JobDword(u,0x6F8);
            p.DeliveryX=before.RequestedX;p.DeliveryY=before.RequestedY;actors.Add(actor);ArmIronWorkshopMoves();
            return ApplyAutomaticStartedWorkerMoat(actor,before,x,y);
        }
        private unsafe bool IronWorkshopCycleComplete(Actor actor)
        {ValidateIronWorkshop(actor);return ironWorkshopWorld.Complete;}
        private IEnumerable<AutomationWait> AutomaticIronWorkshop(AutomationRun run,bool armourer)
        {
            ironWorkshopWorld=new IronWorkshopWorld{Armourer=armourer};var p=ironWorkshopWorld;
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObserveIronWorkshopGoods))
            using(var production=UnitR3EventHooks.OnCalculateBonusYield.Observable.Subscribe(ObserveIronWorkshopYield))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupIronWorkshopWorld();});
                while(p.CapacityPending)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Require(p.Fault==null,p.Fault);
                }
                Execute(run,()=>{
                    Require(p.Iron!=null && p.Iron.HasStorage && p.Iron.FreeAmount>=12 && p.Weapon!=null &&
                        p.Weapon.HasStorage && p.Weapon.FreeAmount>=8,"Natural IronWorkshop lacks current ingredient/output capacity");
                    int prior=GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_IRON_INGOTS);
                    Require(GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_IRON_INGOTS,12) &&
                        GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_IRON_INGOTS)==prior+12,
                        "Natural IronWorkshop real Iron input was not physically admitted");
                });
                Actor actor=null;
                for(int sample=0;sample<2000 && actor==null;sample++)
                {
                    if(sample==128)Director.instance.SetEngineFrameRate(50);
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,sample<128?16:1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{Require(p.Fault==null,p.Fault);actor=FindNaturalIronWorkshop();if(sample%128==0)LogIronWorkshopAcquisition(sample);return true;});
                }
                Require(actor!=null,"No natural loaded IronWorkshop R8 qualified on a clear accepted step outside future access");
                Director.instance.SetEngineFrameRate(250);
                AutomaticWorkerMutation mutation=null;
                // Execute suppresses fixture construction: the tested mutation MUST
                // use only the ordinary simulation lock, without that suppression.
                run.InSimulation(run.CurrentMission,()=>{mutation=PrepareIronWorkshopMutation(actor);return true;});
                bool complete=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?6000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        Require(p.Fault==null,p.Fault);if(!AutomaticActorAlive(actor))return true;
                        complete=IronWorkshopCycleComplete(actor);
                        return expectedEnabled && complete;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    Require(p.Fault==null && p.Goods.Count==0 && !p.YieldPending,"IronWorkshop native observation incomplete: "+p.Fault);
                    if(expectedEnabled)Require(complete,"Original IronWorkshop did not complete its full weapon batch, fresh Iron consumption, role-specific natural production and full second delivery");
                    log("automatic-ironWorkshop-cycle enabled="+expectedEnabled+" initialCargo="+p.InitialCargo+" secondCargo="+p.SecondCargo+
                        " role="+p.Role+" product="+p.Product+" weaponCredits="+p.Deposits+" batches="+p.Batches+" freshIron="+p.Inputs+" naturalProduction="+p.Yields+" homeInputs="+p.HomeInputs+" consumedIron="+p.IronUsed+" localProduced="+p.LocalProduced+" localConsumed="+p.LocalConsumed+
                        " ownOriginalResume="+p.OwnResume+" nativeArmouryHandoffs="+p.Handoffs+" complete="+complete);
                    EndAutomaticWorkerCase(mutation,complete);
                });
            }
            finally
            {
                p.Active=false;if(ReferenceEquals(ironWorkshopCapacityFixture,this))ironWorkshopCapacityFixture=null;
                if(ReferenceEquals(ironWorkshopMoveFixture,this))ironWorkshopMoveFixture=null;
            }
        }
    }
}
