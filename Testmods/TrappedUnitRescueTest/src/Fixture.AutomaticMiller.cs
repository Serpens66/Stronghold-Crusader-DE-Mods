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
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class MillerWorld
        {
            internal int Mill,Slot,InitialStore,Deposits,InputsAfterFirst,DepositedAmount,Extensions;
            internal uint MillGlobal,InitialStoreGlobal;
            internal int DeliveryStore,DeliveryGoalX,DeliveryGoalY,DeliveryHandoffs;
            internal uint DeliveryStoreGlobal;
            internal bool InitialResumeConfirmed;
            internal Actor Actor;
            internal UnitSnapshot StartedBefore;
            internal int AcquisitionUnit,AcquisitionBegins,AcquisitionOnes,AcquisitionZeros,AcquisitionAborted,AcquisitionLogs,CapacityTick;
            internal uint AcquisitionGlobal;
            internal bool CapacityDiagnostics;
            internal int YardWaitTicks;
            internal readonly List<MillerYard> AddedYards=new List<MillerYard>();
            internal string Fault;
            internal bool Active=true,CapacityPending,ProducedAgain;
            internal AllyTransferCapacity Capacity;
            internal readonly Stack<MillerGoodsFrame> Goods=new Stack<MillerGoodsFrame>();
        }
        private sealed class MillerYard
        {internal int Id;internal uint Global;internal bool AccessVerified;}
        private sealed class MillerGoodsFrame
        {
            internal int Store,Before,Amount;
            internal uint Global;
            internal eGoods Good;
        }
        private MillerWorld millerWorld;
        private static bool millerCapacityRegistered;
        private static volatile Fixture millerCapacityFixture;
        private static bool millerAcquisitionRegistered;
        private static volatile Fixture millerAcquisitionFixture;
        private IEnumerable<AutomationStep> AutomaticMillerSteps()
        {yield return new AutomationStep("automatic-natural-miller-moat-and-second-cycle",TimeSpan.FromSeconds(900),AutomaticMiller,true);}

        private void RequestMillerCapacity()
        {
            if(!millerCapacityRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-miller-capacity",ReadMillerCapacity,out string reason),reason);
                millerCapacityRegistered=true;
            }
            millerCapacityFixture=this;millerWorld.CapacityPending=true;
        }
        private static void ReadMillerCapacity(int tick)
        {
            var fixture=millerCapacityFixture;var proof=fixture?.millerWorld;
            if(fixture==null || fixture.stopped || proof==null || !proof.Active || !proof.CapacityPending)return;
            bool waiting=false;
            try
            {
                // OnTick may precede C60F0's initialization of the freshly built
                // yards. Read their actual lives/access before querying capacity;
                // waiting performs no capacity call and assumes no fixed delay.
                if(!fixture.MillerAddedYardsReady())
                {waiting=true;Require(++proof.YardWaitTicks<=64,"New Miller stockpile did not initialize a physical access");return;}
                if(!AllyTransfers.TryGetCapacity(fixture.owner,eGoods.STORED_FLOUR,out proof.Capacity,out string reason))
                    proof.Fault="native flour capacity unavailable: "+reason;
                proof.CapacityTick=tick;
                if(proof.CapacityDiagnostics)
                    fixture.log("automatic-miller-native-capacity tick="+tick+" hasStorage="+(proof.Capacity?.HasStorage??false)+
                        " free="+(proof.Capacity?.FreeAmount??-1)+" stored="+(proof.Capacity?.AmountStored??-1)+
                        " inventory="+fixture.MillerStorageInventory());
            }
            catch(Exception error){proof.Fault="native flour capacity failed: "+error.GetType().FullName;}
            finally{if(!waiting || proof.Fault!=null){proof.CapacityPending=false;proof.CapacityDiagnostics=false;}}
        }
        private unsafe bool MillerAddedYardsReady()
        {
            bool ready=true;
            foreach(var identity in millerWorld.AddedYards)
            {
                Require(BuildingAccess.TryGetById(identity.Id,out GameBuilding* yard,out _) && yard!=null &&
                    yard->r_GlobalId==identity.Global && yard->r_PlayerIdOwner==owner && yard->r_BuildingType==eStructs.STRUCT_GOODS_YARD,
                    "Added Miller stockpile lifetime was lost/reused");
                if(yard->r_AliveState==AliveState.NeedsInit){ready=false;continue;}
                Require(yard->r_AliveState==AliveState.IsAlive,"Added Miller stockpile disappeared before acquisition");
                int x=yard->r_TileAccessPositionX,y=yard->r_TileAccessPositionY;
                if(runtime.Native.Tile(x,y)<0 || !AppleStepAccessHasExit(x,y)){ready=false;continue;}
                if(!identity.AccessVerified)
                {
                    identity.AccessVerified=true;
                    log("automatic-miller-added-yard id="+identity.Id+"/"+identity.Global+" life="+yard->r_AliveState+
                        " access="+x+","+y+" physicalExit=true stack="+yard->r_CurrentGoodStackAmount+
                        " empty="+(yard->r_CurrentGoodStackAmount==0));
                }
            }
            return ready;
        }
        private void ArmMillerAcquisition()
        {
            if(!millerAcquisitionRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-miller-acquisition",new MillerAcquisitionObserver(),out string reason,
                    order:200,enabled:()=>millerAcquisitionFixture!=null && !millerAcquisitionFixture.stopped &&
                        millerAcquisitionFixture.millerWorld!=null && millerAcquisitionFixture.millerWorld.Active && millerAcquisitionFixture.millerWorld.Fault==null &&
                        (millerAcquisitionFixture.millerWorld.Actor==null || millerAcquisitionFixture.millerWorld.Deposits==0)),reason);
                millerAcquisitionRegistered=true;
            }
            millerAcquisitionFixture=this;
        }
        private sealed class MillerAcquisitionObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var fixture=millerAcquisitionFixture;var proof=fixture?.millerWorld;
                if(fixture==null || fixture.stopped || proof==null || !proof.Active || proof.Fault!=null || context==null ||
                    context.SessionId!=fixture.session)return null;
                if(proof.Actor!=null)return fixture.CaptureMillerDeliveryMove(context);
                if(!UnitAccess.TryGetById(context.UnitId,out GameUnit* unit,out _) ||
                    unit==null || !UnitAccess.IsReallyAlive(unit) || unit->r_UnitChimp!=eChimps.CHIMP_TYPE_MILLER ||
                    unit->r_ControllableForPlayerId!=fixture.owner || unit->r_LinkedProductionBuildingId!=proof.Mill ||
                    unit->r_LinkedProductionBuildingGlobalId!=proof.MillGlobal)return null;
                if(proof.AcquisitionUnit==0){proof.AcquisitionUnit=context.UnitId;proof.AcquisitionGlobal=unit->r_GlobalId;}
                if(context.UnitId!=proof.AcquisitionUnit || unit->r_GlobalId!=proof.AcquisitionGlobal)return null;
                proof.AcquisitionBegins++;bool logged=proof.AcquisitionLogs++<16;
                if(logged)fixture.log("automatic-miller-acquisition-move phase=before unit="+context.UnitId+"/"+proof.AcquisitionGlobal+
                    " callGoal="+context.X+","+context.Y+" extra="+context.Extra+" origin="+context.OriginOwnerGuid+
                    " "+fixture.MillerAcquisitionFacts(context.UnitId,proof.AcquisitionGlobal));
                return new MillerAcquisitionCompletion(fixture,proof,context,logged);
            }
        }
        private sealed class MillerAcquisitionCompletion : IUnitMoveInvocation
        {
            private readonly Fixture fixture;private readonly MillerWorld proof;private readonly UnitMoveContext context;
            private readonly bool logged;private int completed;
            internal MillerAcquisitionCompletion(Fixture fixture,MillerWorld proof,UnitMoveContext context,bool logged)
            {this.fixture=fixture;this.proof=proof;this.context=context;this.logged=logged;}
            public void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || result==null || !ReferenceEquals(result.Context,context))return;
                if(result.IsCurrentWorld && result.NativeInvoked && result.NativeReturned && result.Failure==null)
                {if(result.NativeResult==0)proof.AcquisitionZeros++;else if(result.NativeResult==1)proof.AcquisitionOnes++;else proof.AcquisitionAborted++;}
                else proof.AcquisitionAborted++;
                if(logged)fixture.log("automatic-miller-acquisition-move phase=complete unit="+context.UnitId+"/"+proof.AcquisitionGlobal+
                    " callGoal="+context.X+","+context.Y+" nativeInvoked="+result.NativeInvoked+" nativeReturned="+result.NativeReturned+
                    " actualResult="+result.NativeResult+" currentWorld="+result.IsCurrentWorld+" "+
                    fixture.MillerAcquisitionFacts(context.UnitId,proof.AcquisitionGlobal));
            }
        }
        private unsafe bool MillerOriginalOutputJobMatches(GameUnit* unit,bool allowStoreHandoff)
        {
            var snapshot=millerWorld.StartedBefore?.Job;if(snapshot==null)return false;
            IntPtr pointer=(IntPtr)unit;
            for(int i=0;i<snapshot.Rule.FieldCount;i++)
            {
                var field=snapshot.Rule.Field(i);
                // Native1377C0's R8 handoff replaces exactly98E/6F8 before
                //196280. This is a read-only comparison, never a field restore.
                if(allowStoreHandoff && (field.NativeOffset==0x98E || field.NativeOffset==0x6F8))continue;
                if(snapshot.Rule.MayAdvanceWhileWaiting(i))continue;
                if(!snapshot.TryGetValue(field.NativeOffset,out uint expected) || field.Read(
                    offset=>runtime.Native.JobWord((GameUnit*)pointer,offset),
                    offset=>runtime.Native.JobDword((GameUnit*)pointer,offset))!=expected)return false;
            }
            return true;
        }
        private unsafe bool MillerStoreCanReceiveFlour(GameUnit* unit,GameBuilding* store)
        {
            // Exact read-only C9080 + BD490(Flour,32) condition from1377C0.
            // No native selector/capacity/proximity function is invoked here.
            if(Math.Abs(unit->r_CurrentTilePositionX-store->r_TileAccessPositionX)>=3 ||
                Math.Abs(unit->r_CurrentTilePositionY-store->r_TileAccessPositionY)>=3)return false;
            int* amounts=(int*)&store->r_NullAmount;
            for(int good=(int)eGoods.STORED_WOOD_LOGS;good<(int)eGoods.Count;good++)
                if(good!=(int)eGoods.STORED_FLOUR && amounts[good]!=0)return false;
            return amounts[(int)eGoods.STORED_FLOUR]<32;
        }
        private unsafe IUnitMoveInvocation CaptureMillerDeliveryMove(UnitMoveContext context)
        {
            var proof=millerWorld;
            // OFF observes the real vanilla/Fixes outcome through the shared
            // worker observer; it cannot have an owned rescue receipt. Keep the
            // stricter original-goal/handoff chain exclusive to the positive case.
            if(!expectedEnabled)return null;
            if(proof.Actor==null || proof.Deposits!=0 || context.UnitId!=proof.Actor.Id)return null;
            try
            {
                ValidateMillerBinding(proof.Actor);var unit=Resolve(proof.Actor);
                bool own=string.Equals(context.OriginOwnerGuid,Plugin.Guid,StringComparison.Ordinal);
                Require(unit->r_AIState==8 && unit->r_CarryGoodAmount>0 && context.Extra==0 &&
                    MillerOriginalOutputJobMatches(unit,!own),"First Miller output move lost its original live job/cargo");
                int storeId=runtime.Native.Word(unit,0x98E);uint global=runtime.Native.JobDword(unit,0x6F8);
                Require(BuildingAccess.TryGetById(storeId,out GameBuilding* store,out _) && store!=null &&
                    store->r_GlobalId==global && store->r_AliveState==AliveState.IsAlive &&
                    store->r_PlayerIdOwner==owner && store->r_BuildingType==eStructs.STRUCT_GOODS_YARD,
                    "Miller output move has no exact current stockpile lifetime");
                if(own)
                {
                    Require(context.X==proof.StartedBefore.RequestedX && context.Y==proof.StartedBefore.RequestedY &&
                        unit->r_TargetTilePositionX2==proof.StartedBefore.RequestedX && unit->r_TargetTilePositionY2==proof.StartedBefore.RequestedY &&
                        storeId==proof.InitialStore && global==proof.InitialStoreGlobal,
                        "Owned Miller resume changed its original delivery goal or stockpile binding");
                }
                else
                {
                    Require(proof.InitialResumeConfirmed && proof.DeliveryHandoffs<16 &&
                        string.IsNullOrEmpty(context.OriginOwnerGuid) && context.ParentInvocationId==0,
                        "Miller output reselection lacks the preceding owned original-goal success");
                    Require(unit->r_TargetTilePositionX2==proof.DeliveryGoalX && unit->r_TargetTilePositionY2==proof.DeliveryGoalY &&
                        context.X==store->r_TileAccessPositionX && context.Y==store->r_TileAccessPositionY,
                        "Miller reselection does not follow its confirmed goal to the new native store access");
                    Require(BuildingAccess.TryGetById(proof.DeliveryStore,out GameBuilding* oldStore,out _) && oldStore!=null &&
                        oldStore->r_GlobalId==proof.DeliveryStoreGlobal && oldStore->r_AliveState==AliveState.IsAlive &&
                        oldStore->r_PlayerIdOwner==owner && oldStore->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                        !MillerStoreCanReceiveFlour(unit,oldStore),
                        "Miller changed output target without the native old-store rejection condition");
                }
                log("automatic-miller-output-move phase=before own="+own+" unit="+proof.Actor.Id+"/"+proof.Actor.Global+
                    " oldStore="+proof.DeliveryStore+"/"+proof.DeliveryStoreGlobal+" newStore="+storeId+"/"+global+
                    " oldGoal="+proof.DeliveryGoalX+","+proof.DeliveryGoalY+" newGoal="+context.X+","+context.Y+
                    " originalGoal="+proof.StartedBefore.RequestedX+","+proof.StartedBefore.RequestedY+
                    " "+MillerAcquisitionFacts(proof.Actor.Id,proof.Actor.Global));
                return new MillerDeliveryCompletion(this,proof,context,own,storeId,global);
            }
            catch(Exception error)
            {if(proof.Fault==null){proof.Fault=error.Message;log("automatic-miller-output-first-failure "+proof.Fault);}return null;}
        }
        private sealed class MillerDeliveryCompletion : IUnitMoveInvocation
        {
            private readonly Fixture fixture;private readonly MillerWorld proof;private readonly UnitMoveContext context;
            private readonly bool own;private readonly int store;private readonly uint global;private int completed;
            internal MillerDeliveryCompletion(Fixture fixture,MillerWorld proof,UnitMoveContext context,bool own,int store,uint global)
            {this.fixture=fixture;this.proof=proof;this.context=context;this.own=own;this.store=store;this.global=global;}
            public unsafe void Complete(UnitMoveCompletion result)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || proof.Fault!=null || !proof.Active || fixture.stopped ||
                    !ReferenceEquals(fixture.millerWorld,proof))return;
                try
                {
                    Require(result!=null && ReferenceEquals(result.Context,context) && result.IsCurrentWorld &&
                        result.NativeInvoked && result.NativeReturned && result.Failure==null,
                        "Miller output move has no matching current-world native result");
                    fixture.ValidateMillerBinding(proof.Actor);var unit=fixture.Resolve(proof.Actor);
                    Require(unit->r_AIState==8 && unit->r_CarryGoodAmount>0 &&
                        fixture.MillerOriginalOutputJobMatches(unit,!own) && fixture.runtime.Native.Word(unit,0x98E)==store &&
                        fixture.runtime.Native.JobDword(unit,0x6F8)==global,
                        "Miller output move changed its job/cargo/binding during the native original");
                    fixture.log("automatic-miller-output-move phase=complete own="+own+" unit="+proof.Actor.Id+"/"+proof.Actor.Global+
                        " nativeResult="+result.NativeResult+" store="+store+"/"+global+" goal="+context.X+","+context.Y);
                    if(own && result.NativeResult==0)return; // Functional probe refusal is expected, not a handoff.
                    Require(result.NativeResult==1 && unit->r_TargetTilePositionX2==context.X && unit->r_TargetTilePositionY2==context.Y,
                        "Miller output target lacks an actual successful native move receipt");
                    if(own)proof.InitialResumeConfirmed=true;else proof.DeliveryHandoffs++;
                    proof.DeliveryStore=store;proof.DeliveryStoreGlobal=global;
                    proof.DeliveryGoalX=context.X;proof.DeliveryGoalY=context.Y;
                }
                catch(Exception error)
                {if(proof.Fault==null){proof.Fault=error.Message;fixture.log("automatic-miller-output-first-failure "+proof.Fault);}}
            }
        }
        private unsafe string MillerAcquisitionFacts(int id,uint global)
        {
            if(!UnitAccess.TryGetById(id,out GameUnit* unit,out _) || unit==null || unit->r_GlobalId!=global)return "originalLifetime=missing-or-replaced";
            return "ai="+unit->r_AIState+" flags="+unit->r_PathPlanStateBitFlags+" ss="+unit->r_MovementSubstep+
                " at="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+" next="+unit->r_NextTilePositionX2+","+unit->r_NextTilePositionY2+
                " requested="+unit->r_TargetTilePositionX2+","+unit->r_TargetTilePositionY2+" cargo="+unit->r_CarryGoodAmount+
                " timer908="+runtime.Native.JobDword(unit,0x908)+" selector9D8="+runtime.Native.Word(unit,0x9D8)+
                " store="+runtime.Native.Word(unit,0x98E)+"/"+runtime.Native.JobDword(unit,0x6F8);
        }
        private unsafe string MillerStorageInventory()
        {
            var result=new List<string>();var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int index=0;index<buildings.Length;index++)
            {
                var b=buildings.GetValuePointer(index);
                if(b->r_PlayerIdOwner!=owner || b->r_BuildingType!=eStructs.STRUCT_GOODS_YARD || b->r_AliveState==AliveState.None)continue;
                int x=b->r_TileAccessPositionX,y=b->r_TileAccessPositionY,tile=runtime.Native.Tile(x,y);
                result.Add((index+1)+"/"+b->r_GlobalId+":life="+b->r_AliveState+",access="+x+","+y+
                    ",stack="+b->r_CurrentGoodStackAmount+"/"+b->r_MaxGoodStackAmount+",kind="+b->r_LocalStorageGoodType+
                    ",flour="+((int*)&b->r_NullAmount)[(int)eGoods.STORED_FLOUR]+",wheat="+((int*)&b->r_NullAmount)[(int)eGoods.STORED_RAW_WHEAT]+
                    ",flags="+(tile<0?"invalid":runtime.Native.Flags(tile).ToString("X8")));
            }
            return string.Join(";",result);
        }
        private unsafe int CreateMillerPrefab(int x,int y,eMappers mapper,eStructs kind)
        {
            int result=0;
            using(var subscription=BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args=>{
                if(args.Phase==EventHookPhase.Post && args.PlayerId==owner && args.Building==kind && args.ReturnValue>0)
                    result=checked((int)args.ReturnValue);
            }))
                GameBuildingManagerAPI.Instance.CreatePrefab(owner,x,y,mapper,BuildingScales.GetScale(mapper),0,true,false);
            if(result==0)return 0;
            Require(BuildingAccess.TryGetById(result,out GameBuilding* building,out _) && building!=null &&
                building->r_PlayerIdOwner==owner && building->r_BuildingType==kind,"Miller prefab returned an unrelated native building");
            return result;
        }
        private unsafe void SetupMillerWorld()
        {
            Require(!GamePlayerManagerAPI.Instance.IsAIPlayer(owner),"Natural Miller requires the actual human owner");
            FindSite();
            Require(CreateMillerPrefab(cx-10,cy+6,eMappers.MAPPER_HOVEL,eStructs.STRUCT_HOVEL)>0,"Natural Miller housing was not created");
            millerWorld.Mill=CreateMillerPrefab(cx,cy,eMappers.MAPPER_MILL,eStructs.STRUCT_MILL);
            GameBuilding* mill=null;
            Require(millerWorld.Mill>0 && BuildingAccess.TryGetById(millerWorld.Mill,out mill,out _),"Natural mill was not created");
            millerWorld.MillGlobal=mill->r_GlobalId;
        }
        private unsafe void ExtendMillerStorage(int? requestedX=null,int? requestedY=null)
        {
            Require(millerWorld.Extensions<8,"Eight actual Miller dedicated yards did not preserve output capacity");
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            int scale=BuildingScales.GetScale(eMappers.MAPPER_STORES),x=0,y=0;bool found=false;
            // Same controlled-world preflight as the proven Ox delivery yard.
            // Scale5 is the validator parameter, not a claimed physical width.
            // The extra flat envelope covers the constructor's full 0..5 union.
            Require(requestedX.HasValue==requestedY.HasValue,"Explicit dedicated yard needs both coordinates");
            if(requestedX.HasValue)
            {
                x=requestedX.Value;y=requestedY.Value;
                int centre=GameTileManagerAPI.Instance.GetTileId(x+scale/2,y+scale/2);
                found=FlatSquare(x+scale/2,y+scale/2,scale/2+1,out _) && MoatWorkerWithinOwnKeepRange(centre);
            }
            else for(int radius=12;radius<=28 && !found;radius+=2)
                for(int direction=0;direction<8;direction++)
                {
                    int trialX=keep.X+Dx[direction]*radius,trialY=keep.Y+Dy[direction]*radius;
                    if(!FlatSquare(trialX+scale/2,trialY+scale/2,scale/2+1,out _))continue;
                    int centre=GameTileManagerAPI.Instance.GetTileId(trialX+scale/2,trialY+scale/2);
                    if(!MoatWorkerWithinOwnKeepRange(centre))continue;
                    x=trialX;y=trialY;found=true;break;
                }
            Require(found,"No flat free own-Keep site for the controlled dedicated Miller stockpile");
            var created=new List<MillerYard>();
            var placement=GameTileManagerAPI.Instance.TileManager;
            Require(!placement.UsePlacementBlockedOverride,"Miller yard cannot nest a foreign placement override");
            bool oldOverrideValue=placement.PlacementBlockedOverrideValue;
            using(var observed=BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args=>{
                if(args.Phase!=EventHookPhase.Post || args.PlayerId!=owner || args.Building!=eStructs.STRUCT_GOODS_YARD || args.ReturnValue<=0)return;
                int idCreated=checked((int)args.ReturnValue);
                Require(BuildingAccess.TryGetById(idCreated,out GameBuilding* yard,out _) && yard!=null &&
                    yard->r_PlayerIdOwner==owner && yard->r_BuildingType==eStructs.STRUCT_GOODS_YARD && yard->r_GlobalId!=0,
                    "Miller dedicated yard spawn has no exact native identity");
                created.Add(new MillerYard{Id=idCreated,Global=yard->r_GlobalId});
            }))
            try
            {
                // Ordinary native prefab constructor, free fixture building with
                // explicit placement bypass like Ox. This is NOT an assertion of
                // human stockpile adjacency and never writes goods or workers.
                GameBuildingManagerAPI.Instance.CreatePrefab(owner,x,y,eMappers.MAPPER_STORES,scale,0,true,true);
            }
            finally
            {
                // The SDK resets this on its normal return; also contain a failed
                // test construction without leaving a process-wide override set.
                placement.UsePlacementBlockedOverride=false;
                placement.PlacementBlockedOverrideValue=oldOverrideValue;
            }
            var distinct=new HashSet<int>();
            Require(created.Count==4,"Dedicated Miller prefab did not create four actual native stockpile records");
            foreach(var identity in created)
            {
                GameBuilding* yard=null;
                Require(distinct.Add(identity.Id) && BuildingAccess.TryGetById(identity.Id,out yard,out _) && yard!=null &&
                    yard->r_GlobalId==identity.Global && yard->r_PlayerIdOwner==owner && yard->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                    (yard->r_AliveState==AliveState.IsAlive || yard->r_AliveState==AliveState.NeedsInit) &&
                    yard->r_CurrentGoodStackAmount==0 && yard->r_LocalStorageGoodType==eGoods.STORED_NULL &&
                    ((int*)&yard->r_NullAmount)[(int)eGoods.STORED_FLOUR]==0 && ((int*)&yard->r_NullAmount)[(int)eGoods.STORED_RAW_WHEAT]==0,
                    "Dedicated Miller stockpile is not an exact new empty original lifetime");
                log("automatic-miller-dedicated-yard id="+identity.Id+"/"+identity.Global+" at="+x+","+y+
                    " ownKeep="+keep.X+","+keep.Y+" stack="+yard->r_CurrentGoodStackAmount+" life="+yard->r_AliveState+
                    " setup=controlled-real-dedicated-yard humanAdjacencyClaim=false goodsWrites=false");
            }
            millerWorld.AddedYards.AddRange(created);millerWorld.YardWaitTicks=0;millerWorld.Extensions++;
            log("automatic-miller-storage-extended count="+millerWorld.Extensions+" newYards="+created.Count+
                " at="+x+","+y+" dedicated=true requeryAfterActualInitialization=true");
        }
        private unsafe GameBuilding* ValidateMillerBinding(Actor actor)
        {
            var unit=Resolve(actor);var proof=millerWorld;GameBuilding* mill=null;
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_UnitChimp==eChimps.CHIMP_TYPE_MILLER &&
                unit->r_LinkedProductionBuildingId==proof.Mill && unit->r_LinkedProductionBuildingGlobalId==proof.MillGlobal &&
                BuildingAccess.TryGetById(proof.Mill,out mill,out _) && mill!=null && mill->r_GlobalId==proof.MillGlobal &&
                mill->r_AliveState==AliveState.IsAlive && mill->r_PlayerIdOwner==owner && mill->r_BuildingType==eStructs.STRUCT_MILL,
                "Original Miller/workplace lifetime changed");
            int slot=runtime.Native.Word(unit,0x9EE);
            Require(slot==proof.Slot && slot>=0 && slot<3,"Original Miller's native employee slot changed");
            //1377C0 writes native building214 + unit9EE*2 on every update.
            // GameBuilding starts at native5C (Global134 -> publicD8,
            // access15A -> publicFE). These are the mill's three registration
            // slots, separate from the generic public employment fields.
            int employee=*(ushort*)((byte*)mill+0x214-0x5C+slot*2);
            Require(employee==actor.Id,"Mill registration slot no longer contains the original Miller");
            return mill;
        }
        private unsafe bool MillerTransitAccessSafe(GameUnit* unit,GameBuilding* mill)
        {
            // Current/adjacent Next's centered2x2 brush plus one-cell halo is
            // contained within Current +/-2; distance>=4 preserves these accesses.
            int x=unit->r_CurrentTilePositionX,y=unit->r_CurrentTilePositionY;
            if(Math.Max(Math.Abs(x-mill->r_TileAccessPositionX),Math.Abs(y-mill->r_TileAccessPositionY))<4 ||
                Math.Max(Math.Abs(x-runtime.Native.Word(unit,0x992)),Math.Abs(y-runtime.Native.Word(unit,0x994)))<4)return false;
            var buildings=GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for(int index=0;index<buildings.Length;index++)
            {
                var b=buildings.GetValuePointer(index);
                if(b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner && b->r_BuildingType==eStructs.STRUCT_GOODS_YARD &&
                    Math.Max(Math.Abs(x-b->r_TileAccessPositionX),Math.Abs(y-b->r_TileAccessPositionY))<4)return false;
            }
            return true;
        }
        private unsafe Actor FindNaturalMiller()
        {
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u->r_UnitChimp!=eChimps.CHIMP_TYPE_MILLER ||
                    u->r_ControllableForPlayerId!=owner || u->r_LinkedProductionBuildingId!=millerWorld.Mill ||
                    u->r_AIState!=8 || u->r_CarryGoodAmount==0 || u->r_MovementSubstep>=8 ||
                    (u->r_PathPlanStateBitFlags!=2 && u->r_PathPlanStateBitFlags!=4))continue;
                var before=runtime.SnapshotUnit(id,true,false);
                if(before==null || before.Unsupported!=null || before.Job==null ||
                    !before.OrdinaryStep || before.Substep>=8 || !TryStartedMoatAnchor(before,out _,out _))continue;
                var actor=new Actor{Id=id,Global=u->r_GlobalId,Health=u->r_CurrentHealth,Type=u->r_UnitChimp,Tribe=u->r_TribeId,
                    X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y};
                millerWorld.Slot=runtime.Native.Word(u,0x9EE);
                var mill=ValidateMillerBinding(actor);
                if(!MillerTransitAccessSafe(u,mill))continue;
                int storeId=runtime.Native.Word(u,0x98E);
                if(!BuildingAccess.TryGetById(storeId,out GameBuilding* store,out _) || store==null ||
                    store->r_GlobalId!=runtime.Native.JobDword(u,0x6F8) || store->r_PlayerIdOwner!=owner ||
                    store->r_BuildingType!=eStructs.STRUCT_GOODS_YARD || store->r_AliveState!=AliveState.IsAlive)continue;
                millerWorld.StartedBefore=before;return actor;
            }
            return null;
        }
        private unsafe void ObserveMillerGoods(AddGoodToGoodsyardEventArgs args)
        {
            var proof=millerWorld;
            if(proof==null || proof.Fault!=null || !proof.Active || proof.Actor==null || args==null ||
                GameUnitManagerAPI.Instance.GetCurrentContextUnitId()!=proof.Actor.Id ||
                args.Good!=eGoods.STORED_FLOUR && args.Good!=eGoods.STORED_RAW_WHEAT)return;
            // A rejected Pre must remain the first failure: SE still emits Post
            // after this observer returns, even when no matching frame was pushed.
            string frame="phase="+args.Phase+" unit="+proof.Actor.Id+"/"+proof.Actor.Global+
                " context="+GameUnitManagerAPI.Instance.GetCurrentContextUnitId()+
                " store="+args.BuildingId+"/"+unchecked((uint)args.BuildingGlobalId)+
                " good="+args.Good+" amount="+args.AddAmount+" capacity="+args.Capacity+" add="+args.Add+
                " pendingPre="+proof.Goods.Count;
            try
            {
                log("automatic-miller-goods-frame "+frame);
                var unit=Resolve(proof.Actor);
                bool hasStore=BuildingAccess.TryGetById(args.BuildingId,out GameBuilding* store,out _) && store!=null;
                log("automatic-miller-goods-live "+frame+" ai="+unit->r_AIState+" cargo="+unit->r_CarryGoodAmount+
                    " at="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                    " boundStore="+runtime.Native.Word(unit,0x98E)+"/"+runtime.Native.JobDword(unit,0x6F8)+
                    " initialStore="+proof.InitialStore+"/"+proof.InitialStoreGlobal+
                    " confirmedDelivery="+proof.DeliveryStore+"/"+proof.DeliveryStoreGlobal+
                    " initialResume="+proof.InitialResumeConfirmed+" handoffs="+proof.DeliveryHandoffs+
                    " actualStore="+(hasStore?store->r_GlobalId+":life="+store->r_AliveState+",type="+store->r_BuildingType+
                        ",owner="+store->r_PlayerIdOwner+",access="+store->r_TileAccessPositionX+","+store->r_TileAccessPositionY+
                        ",goodAmount="+((int*)&store->r_NullAmount)[(int)args.Good]:"missing"));
                ValidateMillerBinding(proof.Actor);
                Require(hasStore &&
                    store->r_GlobalId==unchecked((uint)args.BuildingGlobalId) && store->r_PlayerIdOwner==owner &&
                    store->r_BuildingType==eStructs.STRUCT_GOODS_YARD && store->r_AliveState==AliveState.IsAlive &&
                    runtime.Native.Word(unit,0x98E)==args.BuildingId && runtime.Native.JobDword(unit,0x6F8)==store->r_GlobalId,
                    "Miller goods event lacks its exact native bound stockpile lifetime");
                Require(Math.Abs(unit->r_CurrentTilePositionX-store->r_TileAccessPositionX)<3 &&
                    Math.Abs(unit->r_CurrentTilePositionY-store->r_TileAccessPositionY)<3,
                    "Miller goods operation occurred outside actual C9080 body proximity");
                int amount=((int*)&store->r_NullAmount)[(int)args.Good];
                if(args.Phase==EventHookPhase.Pre)
                {
                    //1377C0 sets AI4 before its successful R3 wheat debit;
                    // output stays AI8 with cargo until B6100 returns.
                    Require(args.Add && (args.Good==eGoods.STORED_FLOUR?args.AddAmount>0 && unit->r_AIState==8 && unit->r_CarryGoodAmount>0:
                        args.AddAmount<0 && unit->r_AIState==4),"Miller event is not the native output/input operation");
                    if(expectedEnabled && args.Good==eGoods.STORED_FLOUR && proof.Deposits==0)
                        Require(proof.InitialResumeConfirmed && args.BuildingId==proof.DeliveryStore &&
                            store->r_GlobalId==proof.DeliveryStoreGlobal &&
                            unit->r_TargetTilePositionX2==proof.DeliveryGoalX && unit->r_TargetTilePositionY2==proof.DeliveryGoalY,
                            "First Miller deposit lacks its owned original resume and exact observed native delivery handoff");
                    proof.Goods.Push(new MillerGoodsFrame{Store=args.BuildingId,Global=store->r_GlobalId,Good=args.Good,Before=amount,Amount=args.AddAmount});
                }
                else if(args.Phase==EventHookPhase.Post)
                {
                    Require(proof.Goods.Count>0,"Miller goods Post has no matching Pre");var before=proof.Goods.Pop();
                    Require(before.Store==args.BuildingId && before.Global==store->r_GlobalId && before.Good==args.Good &&
                        before.Amount==args.AddAmount,"Miller goods Pre/Post identity changed");
                    int delta=amount-before.Before;
                    Require(delta==before.Amount,"Miller physical stock delta differs from its actual native operation");
                    if(args.Good==eGoods.STORED_FLOUR){proof.Deposits++;proof.DepositedAmount+=delta;}
                    else if(proof.Deposits>0)proof.InputsAfterFirst++;
                    log("automatic-miller-native-goods unit="+proof.Actor.Id+"/"+proof.Actor.Global+" slot="+proof.Slot+
                        " mill="+proof.Mill+"/"+proof.MillGlobal+" store="+args.BuildingId+"/"+store->r_GlobalId+
                        " good="+args.Good+" physicalDelta="+delta+" deposits="+proof.Deposits+" freshInputs="+proof.InputsAfterFirst);
                }
            }
            catch(Exception error)
            {
                proof.Fault=error.Message+" ["+frame+"]";
                log("automatic-miller-goods-first-failure "+proof.Fault);
            }
        }
        private unsafe AutomaticWorkerMutation PrepareMillerMutation(Actor worker)
        {
            millerWorld.Actor=worker;actors.Add(worker);
            var unit=Resolve(worker);
            Require(MillerTransitAccessSafe(unit,ValidateMillerBinding(worker)),"Miller source entered protected future workplace/store access");
            var before=runtime.SnapshotUnit(worker.Id,true,false);var acquired=millerWorld.StartedBefore;
            Require(before!=null && acquired!=null && before.GlobalId==worker.Global && before.Unsupported==null && before.Job!=null &&
                before.AIState==8 && unit->r_CarryGoodAmount>0 && before.OrdinaryStep && before.Substep<8 &&
                before.X==acquired.X && before.Y==acquired.Y && before.NextX==acquired.NextX && before.NextY==acquired.NextY &&
                before.Substep==acquired.Substep && before.Flags==acquired.Flags &&
                before.RequestedX==acquired.RequestedX && before.RequestedY==acquired.RequestedY,
                "Original loaded Miller's accepted step or goal changed before real moat construction");
            Require(TryStartedMoatAnchor(before,out int anchorX,out int anchorY),"Loaded Miller lacks the audited four-cell started moat envelope");
            millerWorld.InitialStore=runtime.Native.Word(unit,0x98E);
            millerWorld.InitialStoreGlobal=runtime.Native.JobDword(unit,0x6F8);
            millerWorld.DeliveryStore=millerWorld.InitialStore;millerWorld.DeliveryStoreGlobal=millerWorld.InitialStoreGlobal;
            millerWorld.DeliveryGoalX=before.RequestedX;millerWorld.DeliveryGoalY=before.RequestedY;
            return ApplyAutomaticStartedWorkerMoat(worker,before,anchorX,anchorY);
        }
        private unsafe bool ObserveMillerCycle(Actor worker)
        {
            var proof=millerWorld;ValidateMillerBinding(worker);var unit=Resolve(worker);
            if(proof.InputsAfterFirst>0 && unit->r_AIState==5)proof.ProducedAgain=true;
            return proof.Deposits>=2 && proof.InputsAfterFirst>0 && proof.ProducedAgain && unit->r_CarryGoodAmount==0;
        }
        private unsafe void LogMillerAcquisition(int sample,bool found)
        {
            int count=0;var facts=new List<string>();
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u->r_UnitChimp!=eChimps.CHIMP_TYPE_MILLER ||
                    u->r_ControllableForPlayerId!=owner)continue;
                count++;if(facts.Count>=3)continue;
                var b=runtime.SnapshotUnit(id,true,false);
                facts.Add(id+"/"+u->r_GlobalId+":ai="+u->r_AIState+",flags="+u->r_PathPlanStateBitFlags+
                    ",ss="+u->r_MovementSubstep+",at="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+
                    ",next="+u->r_NextTilePositionX2+","+u->r_NextTilePositionY2+",goal="+u->r_TargetTilePositionX2+","+u->r_TargetTilePositionY2+
                    ",cargo="+u->r_CarryGoodAmount+",mill="+u->r_LinkedProductionBuildingId+"/"+u->r_LinkedProductionBuildingGlobalId+
                    ",slot="+runtime.Native.Word(u,0x9EE)+",home="+runtime.Native.Word(u,0x992)+","+runtime.Native.Word(u,0x994)+
                    ",store="+runtime.Native.Word(u,0x98E)+"/"+runtime.Native.JobDword(u,0x6F8)+",context700="+runtime.Native.JobDword(u,0x700)+
                    ",timer908="+runtime.Native.JobDword(u,0x908)+",selector9D8="+runtime.Native.Word(u,0x9D8)+
                    ",unsupported="+(b?.Unsupported??"none")+",ordinaryStep="+(b?.OrdinaryStep??false)+
                    ",eligibleMoat="+(b!=null && TryStartedMoatAnchor(b,out _,out _)));
            }
            log("automatic-miller-acquire sample="+sample+" mill="+millerWorld.Mill+" found="+found+
                " livingMillers="+count+" capacityTick="+millerWorld.CapacityTick+" flourFree="+(millerWorld.Capacity?.FreeAmount??-1)+
                " tracked="+millerWorld.AcquisitionUnit+"/"+millerWorld.AcquisitionGlobal+
                " nativeMoves="+millerWorld.AcquisitionBegins+" actual1="+millerWorld.AcquisitionOnes+
                " actual0="+millerWorld.AcquisitionZeros+" aborted="+millerWorld.AcquisitionAborted+
                " facts="+string.Join(" | ",facts));
        }
        private IEnumerable<AutomationWait> AutomaticMiller(AutomationRun run)
        {
            millerWorld=new MillerWorld();var proof=millerWorld;
            using(var goods=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(ObserveMillerGoods))
            try
            {
                Execute(run,()=>{Plugin.SetGeneralFixtureSession(false);SetupMillerWorld();ArmMillerAcquisition();RequestMillerCapacity();});
                while(true)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    bool ready=run.InSimulation(run.CurrentMission,()=>{
                        Require(proof.Fault==null,proof.Fault);
                        if(proof.CapacityPending)return false;
                        if(proof.Capacity!=null && proof.Capacity.HasStorage && proof.Capacity.FreeAmount>=64)return true;
                        ExtendMillerStorage();RequestMillerCapacity();return false;
                    });
                    if(ready)break;
                }
                Execute(run,()=>{
                    Require(GamePlayerManagerAPI.Instance.TryAddGood(owner,eGoods.STORED_RAW_WHEAT,30),"Real stockpile rejected natural Miller wheat input");
                    Require(GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_RAW_WHEAT)>=30,"Natural Miller input reserve was not physically admitted");
                    log("automatic-miller-world mill="+proof.Mill+"/"+proof.MillGlobal+" flourFree="+proof.Capacity.FreeAmount+
                        " wheat="+GamePlayerManagerAPI.Instance.GetGoodAmount(owner,eGoods.STORED_RAW_WHEAT)+" realHousing=true syntheticWorker=false");
                });
                Actor worker=null;
                // Bound the diagnostic retry:104 showed all three genuine R7
                // millers unchanged for minutes. Capture actual gates instead
                // of blindly repeating thousands of identical samples.
                int acquisitionLimit=512;
                for(int sample=0;sample<=acquisitionLimit && worker==null;sample++)
                {
                    if(sample==256)Director.instance.SetEngineFrameRate(50);
                    if(sample%256==0)run.InSimulation(run.CurrentMission,()=>{
                        proof.CapacityDiagnostics=true;RequestMillerCapacity();return true;
                    });
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,sample<256?16:1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Execute(run,()=>{
                        Require(proof.Fault==null,proof.Fault);
                        if(!proof.CapacityPending && (proof.Capacity==null || !proof.Capacity.HasStorage || proof.Capacity.FreeAmount<64))
                        {
                            //106 proved delayed starting wood consumes the initial
                            // room. Repair this actual world precondition through
                            // ordinary construction, never through worker writes.
                            ExtendMillerStorage();proof.CapacityDiagnostics=true;RequestMillerCapacity();
                            acquisitionLimit=Math.Max(acquisitionLimit,sample+256);
                        }
                        if(!proof.CapacityPending)worker=FindNaturalMiller();
                        if(sample%256==0)LogMillerAcquisition(sample,worker!=null);
                    });
                }
                Require(worker!=null,"No natural loaded Miller R8 reached an admitted clear transit source outside protected future access");
                Director.instance.SetEngineFrameRate(250);
                AutomaticWorkerMutation mutation=null;
                // Execute also suppresses replacement paths for synthetic setup.
                // The actual source must exercise normal, unsuppressed observers.
                run.InSimulation(run.CurrentMission,()=>{mutation=PrepareMillerMutation(worker);return true;});
                bool complete=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?5000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        Require(proof.Fault==null,"Miller goods observation failed: "+proof.Fault);
                        if(!AutomaticActorAlive(worker))return true;
                        complete=ObserveMillerCycle(worker);
                        return expectedEnabled && complete;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    Require(proof.Fault==null && proof.Goods.Count==0,"Miller native goods observation incomplete: "+proof.Fault);
                    if(expectedEnabled)Require(complete,"Original Miller did not deposit, fetch fresh wheat, manufacture and deliver a second natural batch");
                    log("automatic-miller-cycle enabled="+expectedEnabled+" deposits="+proof.Deposits+" actualFlour="+proof.DepositedAmount+
                        " freshInputOperations="+proof.InputsAfterFirst+" freshProduction="+proof.ProducedAgain+
                        " originalResumeConfirmed="+proof.InitialResumeConfirmed+" nativeOutputHandoffs="+proof.DeliveryHandoffs+" complete="+complete);
                    EndAutomaticWorkerCase(mutation,complete);
                });
            }
            finally
            {
                proof.Active=false;
                if(ReferenceEquals(millerCapacityFixture,this))millerCapacityFixture=null;
                if(ReferenceEquals(millerAcquisitionFixture,this))millerAcquisitionFixture=null;
            }
        }
    }
}
