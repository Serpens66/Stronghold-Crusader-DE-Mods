using System;
using System.Collections.Generic;
using System.Linq;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using APIShared.Simulation;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private WorkerCycleObservation workerCycle;
        private AppleWorldProof appleWorld;
        private sealed class AppleWorldProof : IDisposable
        {
            internal int Farm,Granary,DamageEvents,DeleteEvents,BuildEvents;
            internal uint FarmGlobal,GranaryGlobal;
            internal string LastEvidence="none";
            internal readonly List<IDisposable> Subscriptions=new List<IDisposable>();
            public void Dispose(){foreach(var subscription in Subscriptions)subscription.Dispose();Subscriptions.Clear();}
        }
        private static bool workerObserversRegistered;
        private static volatile Fixture workerObserverFixture;
        private static IDisposable applePickupObserver,appleDropObserver;
        private sealed class WorkerCycleObservation
        {
            internal Actor Actor;internal int Farm,RequestedX,RequestedY,Samples,LastTick,CounterIncrements,RemotePicking,PickupEvents,DropEvents,DropsWithStockIncrease;
            internal bool Active,HasPrevious,Reached,HadCargo,Delivered;internal string Fault;
            internal WorkerFacts Previous;
        }
        private struct WorkerFacts
        {
            internal int X,Y,Counter,TreeId,TreeX,TreeY,Carry,StorageId,AIState,PathFlags;
            internal uint TreeGlobal,StorageGlobal,StorageFruit;
            internal bool TreeValid,StorageValid;
        }
        private IEnumerable<AutomationWait> Worker(AutomationRun run)
        {
            using(var proof=ObserveAppleWorld())
            {
            int farm=0;
            Execute(run,()=>{
                farm=SetupNaturalAppleFarm();
                log("worker-fixture naturalFarm="+farm+" input=native-farm syntheticPart=occupied-source-wall-only");
            });
            Actor worker=null;
            // Observe a real employee's own travel state, never inject an AI state or worker cargo.
            for(int attempt=0;attempt<4000 && worker==null;attempt++)
            {
                // Fine sampling must be slower than Unity polling so native step boundaries remain observable.
                if(attempt==256)Director.instance.SetEngineFrameRate(50);
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,attempt<256?16:1+(attempt&1));
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{ worker=FindWorkingAppleFarmer(farm); if(attempt==0 || attempt==31 || attempt==127 || attempt==255 || attempt==599 || attempt==1199 || attempt==3999)LogAppleAcquisition(farm,attempt); });
            }
            Require(worker!=null,"No actual apple employee acquired an ordinary active travel segment; job coverage unavailable");
            Director.instance.SetEngineFrameRate(250);
            int requestedX=0,requestedY=0,carryBefore=0;
            Execute(run,()=>{
                actors.Add(worker);
                var before=runtime.SnapshotUnit(worker.Id);
                Require(before!=null && before.Unsupported==null && (before.Flags==2 || before.Flags==4),"Worker travel changed before capture");
                requestedX=before.RequestedX;requestedY=before.RequestedY;carryBefore=WorkerCarry(worker);
                worker.Health=before.Health;worker.SourceX=worker.X=before.X;worker.SourceY=worker.Y=before.Y;
                var scope=runtime.BeginMutation("synthetic-wall-under-naturally-working-apple-farmer",before.X,before.Y,1);
                try
                {
                    PaintSyntheticTrap(before.X,before.Y);
                    Require(!HasStructuralExit(worker),"Synthetic worker seam did not close all physical ground exits");
                    var results=scope.Complete(true);
                    var result=results.Single(r=>r.UnitId==worker.Id);
                    log("worker-rescue "+result+" initialCarry="+carryBefore+" requested="+requestedX+","+requestedY);
                    if(expectedEnabled)
                        Require(result.Status==RescueStatus.Rescued && result.IdentityPreserved && result.ResumeAttempted && result.ResumeSucceeded,
                            "Worker relocation/reissue failed; continuation must not be reported as passed");
                    else Require(AtSource(worker),"Disabled runtime moved the worker");
                    Require(WorkerCarry(worker)==carryBefore,"Rescue immediately changed worker cargo");
                    CheckActor(worker,true);
                    BeginWorkerCycle(worker,farm,requestedX,requestedY);
                }
                finally{scope.Dispose();}
            });
            bool reached=false;
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            for(int sample=0;sample<(expectedEnabled?600:8);sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission,4);
                reached |= run.InSimulation(run.CurrentMission,()=>WorkerAtRecordedTarget(worker,farm,requestedX,requestedY));
                if(expectedEnabled && run.InSimulation(run.CurrentMission,()=>workerCycle.Delivered))break;
            }
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                CheckActor(worker,true);
                log("worker-observation-before-assert enabled="+expectedEnabled+" reached="+reached+" samples="+workerCycle.Samples+
                    " pickingIncrements="+workerCycle.CounterIncrements+" remotePicking="+workerCycle.RemotePicking+
                    " pickupEvents="+workerCycle.PickupEvents+" dropEvents="+workerCycle.DropEvents+
                    " stockIncreases="+workerCycle.DropsWithStockIncrease+" hadCargo="+workerCycle.HadCargo+
                    " delivered="+workerCycle.Delivered+" fault="+(workerCycle.Fault??"none"));
                Require(workerCycle.Fault==null,"Worker cycle observation failed: "+workerCycle.Fault);
                if(expectedEnabled)Require(reached,"Naturally working farmer did not physically reach the recorded segment target after rescue");
                else Require(AtSource(worker),"Baseline worker escaped the controlled trap");
                if(expectedEnabled)
                {
                    Require(workerCycle.CounterIncrements>0 && workerCycle.RemotePicking==0,"Picking was unobserved or occurred away from the actual previously selected apple tree");
                    Require(workerCycle.HadCargo && workerCycle.PickupEvents>0 && workerCycle.DropEvents>0 && workerCycle.DropsWithStockIncrease>0 && workerCycle.Delivered,
                        "No complete actual harvest/cargo/granary-deposit/empty-cargo cycle was observed");
                }
                log("worker-result enabled="+expectedEnabled+" naturalEmployee=true nativeActiveRoute=true syntheticEntrapment=true "+
                    "sameIdentity=true sameWorkplace=true cargoBefore="+carryBefore+" cargoAfter="+WorkerCarry(worker)+
                    " originalSegmentReached="+reached+" tickSamples="+workerCycle.Samples+" pickingIncrements="+workerCycle.CounterIncrements+
                    " remotePicking="+workerCycle.RemotePicking+" pickupEvents="+workerCycle.PickupEvents+" nativeDepositEvents="+workerCycle.DropEvents+
                    " depositsWithGranaryFruitIncrease="+workerCycle.DropsWithStockIncrease+" fullHarvestDeliveryCycleVerified="+(expectedEnabled && workerCycle.Delivered));
                workerCycle.Active=false;
            });
            // The entire disposable mission is closed by the automation runner; no user save is modified.
            }
        }
        private void BeginWorkerCycle(Actor actor,int farm,int x,int y)
        {
            Require(System.Runtime.InteropServices.Marshal.OffsetOf(typeof(GameUnit),nameof(GameUnit.r_TargetVegetationId)).ToInt32()==0x344 &&
                System.Runtime.InteropServices.Marshal.OffsetOf(typeof(GameUnit),nameof(GameUnit.N000000C5)).ToInt32()==0x390 &&
                System.Runtime.InteropServices.Marshal.OffsetOf(typeof(GameUnit),nameof(GameUnit.r_CarryGoodAmount)).ToInt32()==0x384 &&
                System.Runtime.InteropServices.Marshal.OffsetOf(typeof(GameVegetation),nameof(GameVegetation.r_TilePositionX)).ToInt32()==0x6E &&
                System.Runtime.InteropServices.Marshal.OffsetOf(typeof(GameVegetation),nameof(GameVegetation.r_TilePositionY)).ToInt32()==0x70,
                "Apple observer typed layout differs from the hash-bound native tree/counter/cargo contracts");
            if(!workerObserversRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"apple-worker-cycle",tick=>workerObserverFixture?.ObserveWorkerTick(tick),out string reason),"Worker tick observation unavailable: "+reason);
                applePickupObserver=UnitR3EventHooks.OnAppleFarmerPickUpApple.Observable.Subscribe(args=>workerObserverFixture?.ObserveAppleEvent(args.UnitId,false));
                appleDropObserver=UnitR3EventHooks.OnAppleFarmerDropOffApple.Observable.Subscribe(args=>workerObserverFixture?.ObserveAppleEvent(args.UnitId,true));
                workerObserversRegistered=true;
            }
            workerCycle=new WorkerCycleObservation { Actor=actor,Farm=farm,RequestedX=x,RequestedY=y,Active=true };
            workerCycle.Previous=CaptureWorkerFacts(workerCycle);
            workerCycle.HasPrevious=SimulationTickEvents.TryGetCurrentTick(out workerCycle.LastTick);
            workerCycle.HadCargo=workerCycle.Previous.Carry>0;
            workerObserverFixture=this;
            WorkerFacts initial=workerCycle.Previous;
            log("worker-observation-initial tick="+workerCycle.LastTick+" state="+initial.AIState+" flags="+initial.PathFlags+
                " tile="+initial.X+","+initial.Y+" counter="+initial.Counter+" selectedTree="+initial.TreeId+"/"+initial.TreeGlobal+
                " treeTile="+initial.TreeX+","+initial.TreeY+" treeValid="+initial.TreeValid+" carry="+initial.Carry+
                " selectedTreeDomain=native-array-index sdkTreeId="+(initial.TreeId+1)+
                " typedTreeOffset=344 typedCounterOffset=390 nativeCounter=9EC-lowWORD");
        }
        private void ObserveWorkerTick(int tick)
        {
            WorkerCycleObservation observation=workerCycle;
            if(stopped || observation==null || !observation.Active || observation.Fault!=null)return;
            try
            {
                WorkerFacts now=CaptureWorkerFacts(observation);
                if(observation.HasPrevious)
                {
                    int delta=unchecked(tick-observation.LastTick);
                    // No catch-up is invented: a missed publication invalidates the per-tick picking proof.
                    if(delta!=0 && delta!=1)throw new InvalidOperationException("map-tick observation gap "+delta);
                    if(now.Counter>observation.Previous.Counter)
                    {
                        if(now.Counter!=observation.Previous.Counter+1)throw new InvalidOperationException("unobserved picking counter steps");
                        observation.CounterIncrements++;
                        WorkerFacts tree=observation.Previous;
                        if(observation.CounterIncrements<=16)
                            log("worker-picking-counter tick="+tick+" before="+tree.Counter+" after="+now.Counter+
                                " previousState="+tree.AIState+" currentState="+now.AIState+" unitTile="+now.X+","+now.Y+
                                " previousTree="+tree.TreeId+"/"+tree.TreeGlobal+" treeTile="+tree.TreeX+","+tree.TreeY+
                                " currentTree="+now.TreeId+"/"+now.TreeGlobal+" currentTreeTile="+now.TreeX+","+now.TreeY+
                                " previousTreeValid="+tree.TreeValid+" distance="+Math.Max(Math.Abs(now.X-tree.TreeX),Math.Abs(now.Y-tree.TreeY)));
                        Require(SameObservedAppleTree(tree),"Previously selected apple-tree identity changed before the picking-counter write");
                        if(!tree.TreeValid || Math.Abs(now.X-tree.TreeX)>2 || Math.Abs(now.Y-tree.TreeY)>2)observation.RemotePicking++;
                    }
                }
                observation.Samples++;
                observation.Reached |= Math.Abs(now.X-observation.RequestedX)<=1 && Math.Abs(now.Y-observation.RequestedY)<=1;
                observation.HadCargo |= now.Carry>0;
                observation.Delivered |= observation.HadCargo && observation.DropEvents>0 && observation.DropsWithStockIncrease>0 && now.Carry==0;
                observation.Previous=now;observation.HasPrevious=true;observation.LastTick=tick;
            }
            catch(Exception error){observation.Fault=error.ToString();}
        }
        // Native108040 returns an array index, written unchanged to unit9A0 by135E30.
        // The public SDK getter instead accepts a one-based array ID and subtracts one.
        // Keep the stored native index in WorkerFacts; translate only at this boundary.
        private static unsafe bool TryGetNativeAppleTree(int nativeIndex,out GameVegetation* tree)
            =>NativeVegetationAccess.TryGet(nativeIndex,out tree);
        private static unsafe bool SameObservedAppleTree(WorkerFacts facts) => facts.TreeValid &&
            TryGetNativeAppleTree(facts.TreeId,out GameVegetation* tree) &&
            tree->r_AliveState==AliveState.IsAlive && tree->r_GlobalId==facts.TreeGlobal &&
            tree->r_TilePositionX==facts.TreeX && tree->r_TilePositionY==facts.TreeY;
        private void ObserveAppleEvent(int id,bool drop)
        {
            WorkerCycleObservation observation=workerCycle;
            if(stopped || observation==null || !observation.Active || observation.Fault!=null || observation.Actor.Id!=id)return;
            try
            {
                WorkerFacts now=CaptureWorkerFacts(observation);
                if(!drop){observation.PickupEvents++;return;}
                // SE's event site136901 is AFTER B6100's granary fruit write, BEFORE carry decrement.
                // Require matching storage identity and physical vicinity, not merely the event name.
                if(!now.StorageValid || now.Carry<1)throw new InvalidOperationException("Apple drop event lacks valid storage or positive cargo");
                observation.DropEvents++;
                WorkerFacts previous=observation.Previous;
                if(previous.StorageValid && previous.StorageId==now.StorageId && previous.StorageGlobal==now.StorageGlobal && now.StorageFruit>previous.StorageFruit)
                    observation.DropsWithStockIncrease++;
            }
            catch(Exception error){observation.Fault=error.ToString();}
        }
        private unsafe WorkerFacts CaptureWorkerFacts(WorkerCycleObservation observation)
        {
            ValidateAppleWorld();
            GameUnit* unit=Resolve(observation.Actor);
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth==observation.Actor.Health && unit->r_LinkedProductionBuildingId==observation.Farm,
                "Observed farmer identity, health or workplace changed");
            // Hash-bound135E30: counter native9EC(lowWORD), selected tree9A0, storage98E/global6F8.
            var facts=new WorkerFacts { X=unit->r_CurrentTilePositionX,Y=unit->r_CurrentTilePositionY,Carry=unit->r_CarryGoodAmount,
                AIState=unit->r_AIState,PathFlags=unit->r_PathPlanStateBitFlags,
                Counter=*(ushort*)((byte*)unit+0x9EC-0x65C),TreeId=unit->r_TargetVegetationId,
                StorageId=*(ushort*)((byte*)unit+0x98E-0x65C),StorageGlobal=*(uint*)((byte*)unit+0x6F8-0x65C) };
            if(TryGetNativeAppleTree(facts.TreeId,out GameVegetation* tree) && tree->r_AliveState==AliveState.IsAlive)
            { facts.TreeValid=true;facts.TreeGlobal=tree->r_GlobalId;facts.TreeX=tree->r_TilePositionX;facts.TreeY=tree->r_TilePositionY; }
            if(facts.StorageId>0 && APIShared.BuildingAccess.TryGetById(facts.StorageId,out GameBuilding* storage,out _) && storage!=null &&
                storage->r_GlobalId==facts.StorageGlobal && storage->r_PlayerIdOwner==owner && storage->r_AliveState==AliveState.IsAlive)
            {
                facts.StorageValid=true;facts.StorageFruit=storage->r_FruitAmount;
                if(unit->r_AIState==7 && facts.Carry>0 && unit->r_PathPlanStateBitFlags==0)
                    Require(Math.Abs(facts.X-storage->r_TileAccessPositionX)<3 && Math.Abs(facts.Y-storage->r_TileAccessPositionY)<3,
                        "Empty route falsely represented arrival at a distant granary");
            }
            return facts;
        }
        private AppleWorldProof ObserveAppleWorld()
        {
            var proof=new AppleWorldProof();appleWorld=proof;
            proof.Subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(args=>{
                if(stopped || args.BuildingId<=0 || args.Phase!=EventHookPhase.Pre || args.BuildingId!=proof.Farm && args.BuildingId!=proof.Granary)return;
                if(proof.DeleteEvents++<8)LogAppleWorldEvent(proof,"delete",args.BuildingId,0,0);
            }));
            proof.Subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable.Subscribe(args=>{
                if(stopped || args.BuildingId<=0 || args.Phase!=EventHookPhase.Pre || args.BuildingId!=proof.Farm && args.BuildingId!=proof.Granary)return;
                if(proof.DeleteEvents++<8)LogAppleWorldEvent(proof,"bulldoze",args.BuildingId,0,0);
            }));
            proof.Subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(args=>{
                if(stopped || args.Phase!=EventHookPhase.Pre)return;
                int id=GameTileManagerAPI.Instance.GetTileBuildingId(args.TileId);
                if(id<=0 || id!=proof.Farm && id!=proof.Granary)return;
                if(proof.DamageEvents++<12)LogAppleWorldEvent(proof,"damage",id,args.PlayerIdSource,args.Damage);
            }));
            proof.Subscriptions.Add(BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(args=>{
                if(stopped || args.Phase!=EventHookPhase.Pre || proof.Farm==0 || Math.Abs(args.TileX-cx)>15 || Math.Abs(args.TileY-cy)>15)return;
                if(proof.BuildEvents++<12){proof.LastEvidence="nearby-build player="+args.PlayerId+" mapper="+args.Mappers+" tile="+args.TileX+","+args.TileY;log("apple-world "+proof.LastEvidence);}
            }));
            return proof;
        }
        private unsafe void LogAppleWorldEvent(AppleWorldProof proof,string kind,int id,int source,int damage)
        {
            string identity="unavailable";
            if(BuildingAccess.TryGetById(id,out GameBuilding* b,out _) && b!=null)
                identity="global="+b->r_GlobalId+" owner="+b->r_PlayerIdOwner+" type="+b->r_BuildingType+" life="+b->r_AliveState;
            proof.LastEvidence=kind+" building="+id+" "+identity+" sourcePlayer="+source+" damage="+damage;
            log("apple-world "+proof.LastEvidence);
        }
        private unsafe void ValidateAppleWorld()
        {
            AppleWorldProof proof=appleWorld;
            Require(proof!=null,"Apple fixture world proof was not armed");
            Require(BuildingAccess.TryGetById(proof.Farm,out GameBuilding* farm,out _) && farm!=null &&
                farm->r_GlobalId==proof.FarmGlobal && farm->r_PlayerIdOwner==owner && farm->r_BuildingType==eStructs.STRUCT_APPLEFARM &&
                (farm->r_AliveState==AliveState.IsAlive || farm->r_AliveState==AliveState.NeedsInit),
                "Natural farm disappeared or slot was reused; lastEvidence="+proof.LastEvidence);
            Require(BuildingAccess.TryGetById(proof.Granary,out GameBuilding* granary,out _) && granary!=null &&
                granary->r_GlobalId==proof.GranaryGlobal && granary->r_PlayerIdOwner==owner && granary->r_BuildingType==eStructs.STRUCT_GRANARY &&
                (granary->r_AliveState==AliveState.IsAlive || granary->r_AliveState==AliveState.NeedsInit),
                "Natural granary disappeared or slot was reused; lastEvidence="+proof.LastEvidence);
        }
        private unsafe int SetupNaturalAppleFarm()
        {
            var players=GamePlayerManagerAPI.Instance;
            int keepId=players.GetPlayerKeepId(owner);
            Require(!players.IsAIPlayer(owner),"Worker fixture local owner is AI: "+owner+" lord="+players.GetAILord(owner));
            Require(BuildingAccess.TryGetById(keepId,out GameBuilding* keep,out _) && keep!=null && keep->r_AliveState==AliveState.IsAlive &&
                keep->r_PlayerIdOwner==owner,"Worker fixture keep does not belong to the actual human owner");
            log("apple-owner owner="+owner+" isAI="+players.IsAIPlayer(owner)+" lord="+players.GetAILord(owner)+" keep="+keepId+"/"+keep->r_GlobalId+" keepOwner="+keep->r_PlayerIdOwner);
            FindSite();
            // Real prefab jobs, housing and storage; no population, worker-state or yield writes.
            using(var spawn=BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args=>{
                if(args.Phase==EventHookPhase.Post && args.PlayerId==owner && args.ReturnValue>0 && args.Building==eStructs.STRUCT_GRANARY)
                    appleWorld.Granary=checked((int)args.ReturnValue);
            }))
                GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx-10,cy,eMappers.MAPPER_GRANARY,4,0,true,true);
            GameBuilding* granary=null;
            Require(appleWorld.Granary>0 && BuildingAccess.TryGetById(appleWorld.Granary,out granary,out _) && granary!=null &&
                granary->r_PlayerIdOwner==owner && granary->r_BuildingType==eStructs.STRUCT_GRANARY,"Actual native granary core/owner was not observed");
            appleWorld.GranaryGlobal=granary->r_GlobalId;
            GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx-10,cy+6,eMappers.MAPPER_HOVEL,BuildingScales.GetScale(eMappers.MAPPER_HOVEL),0,true,true);
            int farm=0;
            using(var spawn=BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args=>{
                if(args.Phase==EventHookPhase.Post && args.PlayerId==owner && args.ReturnValue>0 &&
                    args.Building==eStructs.STRUCT_APPLEFARM)farm=checked((int)args.ReturnValue);
            }))
                GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,eMappers.MAPPER_APPLEFARM,10,0,true,true);
            Require(farm>0,"Native apple-farm core spawn was not observed");
            appleWorld.Farm=farm;
            Require(BuildingAccess.TryGetById(farm,out GameBuilding* core,out _) && core!=null && core->r_PlayerIdOwner==owner &&
                core->r_BuildingType==eStructs.STRUCT_APPLEFARM,"Actual native apple farm core/owner was not observed");
            appleWorld.FarmGlobal=core->r_GlobalId;
            ValidateAppleWorld();
            LogAppleAcquisition(farm,-1);
            return farm;
        }
        private unsafe void LogAppleAcquisition(int farm,int attempt)
        {
            string building="missing",population="missing";
            if(BuildingAccess.TryGetById(farm,out GameBuilding* b,out _) && b!=null)
                building="global="+b->r_GlobalId+" expectedGlobal="+appleWorld.FarmGlobal+" owner="+b->r_PlayerIdOwner+" type="+b->r_BuildingType+" life="+b->r_AliveState+" workers="+b->r_TotalCurrentWorkers+"/"+b->r_TotalWorkersRequired+
                    " missing="+b->r_TotalMissingWorkers+" employee="+b->r_WorkerId_1;
            if(GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner,out GamePlayerResources* r) && r!=null)
                population="ready="+r->r_ReadyPeasants+" peasants="+r->r_ExistingPeasants+" civilians="+r->r_TotalCivilians+
                    " housing="+r->r_CivilianHousingSpace+" popularity="+r->r_CurrentPopularity;
            int employees=0,travel=0,boundary=0,supported=0;string first="none";
            for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
                if(UnitAccess.TryGetById(id,out GameUnit* u,out _) && u!=null && UnitAccess.IsReallyAlive(u) &&
                    u->r_UnitChimp==eChimps.CHIMP_TYPE_FARMER_APPLE && u->r_ControllableForPlayerId==owner && u->r_LinkedProductionBuildingId==farm)
                {
                    employees++;bool route=(u->r_AIState==2 || u->r_AIState==8) && (u->r_PathPlanStateBitFlags==2 || u->r_PathPlanStateBitFlags==4);
                    if(route)travel++;if(route && u->r_MovementSubstep==8)boundary++;
                    var snap=runtime.SnapshotUnit(id);
                    if(snap!=null && snap.Unsupported==null && snap.Candidates.Count>0)supported++;
                    if(first=="none")first=id+" state="+u->r_AIState+" flags="+u->r_PathPlanStateBitFlags+" substep="+u->r_MovementSubstep+
                        " tile="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+" unsupported="+(snap==null?"snapshot-null":snap.Unsupported??"none");
                }
            int trees=0,ripe=0;
            int vegetationCapacity=GameVegetationManagerAPI.Instance.GetVegetationAsSpan().Length;
            for(int id=1;id<=vegetationCapacity;id++)
                if(GameVegetationManagerAPI.Instance.TryGetVegetationById(id,out GameVegetation* tree) && tree!=null && tree->r_AliveState==AliveState.IsAlive &&
                    tree->r_TilePositionX>=cx && tree->r_TilePositionX<cx+10 && tree->r_TilePositionY>=cy && tree->r_TilePositionY<cy+10)
                { trees++;if(tree->r_GrowthStage==3)ripe++; }
            log("apple-acquisition attempt="+attempt+" farm="+farm+" "+building+" "+population+" linkedEmployees="+employees+
                " nativeTravel="+travel+" travelAtBoundary="+boundary+" supported="+supported+" trees="+trees+" ripe="+ripe+" first="+first);
        }
        private unsafe Actor FindWorkingAppleFarmer(int farm)
        {
            ValidateAppleWorld();
            for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
                if(UnitAccess.TryGetById(id,out GameUnit* unit,out _) && unit!=null && UnitAccess.IsReallyAlive(unit) &&
                    unit->r_UnitChimp==eChimps.CHIMP_TYPE_FARMER_APPLE && unit->r_ControllableForPlayerId==owner &&
                    unit->r_LinkedProductionBuildingId==farm && (unit->r_AIState==2 || unit->r_AIState==8) &&
                    unit->r_MovementSubstep==8 && (unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4))
                {
                    var snapshot=runtime.SnapshotUnit(id);
                    if(snapshot.Unsupported!=null || snapshot.Candidates.Count==0)continue;
                    return new Actor{Id=id,Global=unit->r_GlobalId,Health=unit->r_CurrentHealth,Type=unit->r_UnitChimp,
                        Tribe=unit->r_TribeId,X=unit->r_CurrentTilePositionX,Y=unit->r_CurrentTilePositionY};
                }
            return null;
        }
        private unsafe bool WorkerAtRecordedTarget(Actor actor,int farm,int x,int y)
        {
            ValidateAppleWorld();
            GameUnit* unit=Resolve(actor);
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth==actor.Health,"Worker identity/health did not survive continuation");
            Require(unit->r_LinkedProductionBuildingId==farm,"Worker changed workplace during continuation");
            return Math.Abs(unit->r_CurrentTilePositionX-x)<=1 && Math.Abs(unit->r_CurrentTilePositionY-y)<=1;
        }
        private unsafe int WorkerCarry(Actor actor)=>*(short*)((byte*)Resolve(actor)+0x9E0-0x65C);
        private void PaintSyntheticTrap(int x,int y)
        {
            var tiles=GameTileManagerAPI.Instance;int tile=tiles.GetTileId(x,y);
            tiles.SetTilePropertyFlag(tile,tiles.GetTilePropertyFlag(tile)|TilePropertyFlag.IsWall);
            tiles.SetTileHeight(tile,90);
            GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(x,y);
        }
    }
}
