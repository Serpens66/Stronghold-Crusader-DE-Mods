using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
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
        private sealed class MasonJourneyProof
        {
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal int Slot,AnchorX,AnchorY,QuarryX,QuarryY,ProductionEvents,Produced;
            internal uint PendingStone;
            internal int PendingAmount;
            internal bool Active,Arrived,Registered,Pending;
            internal string Fault;
        }
        private MasonJourneyProof masonJourney;
        private static volatile Fixture masonFixture;
        private static bool masonObserversRegistered;
        private static IDisposable masonProductionObserver;

        private IEnumerable<AutomationStep> AutomaticMasonSteps()
        {yield return new AutomationStep("automatic-natural-mason2-workplace-and-production",TimeSpan.FromSeconds(1200),AutomaticMason,true);}

        private unsafe MasonJourneyProof FindNaturalMasonJourney()
        {
            // Shared tick notification precedes building initialization. Observe
            // the original NeedsInit records without calling them lost lifetimes.
            foreach(int site in new[]{oxQuarry,oxTether,oxQuarryPile})
            {
                Require(BuildingAccess.TryGetById(site,out GameBuilding* original,out _) && original!=null,
                    "Original Mason setup building disappeared before initialization");
                uint expected=site==oxQuarry?oxQuarryGlobal:site==oxTether?oxTetherGlobal:oxQuarryPileGlobal;
                Require(original->r_GlobalId==expected,"Original Mason setup slot changed lifetime before initialization");
                if(original->r_AliveState==AliveState.NeedsInit)return null;
            }
            ValidateOxSites();
            Require(BuildingAccess.TryGetById(oxQuarry,out GameBuilding* quarry,out _) && quarry!=null,"Original quarry unavailable");
            foreach(int id in new[]{(int)quarry->r_WorkerId_1,(int)quarry->r_WorkerId_2,(int)quarry->r_WorkerId_3})
            {
                if(id==0 || !UnitAccess.TryGetById(id,out GameUnit* unit,out _) || unit==null || !UnitAccess.IsReallyAlive(unit) ||
                    unit->r_UnitChimp!=eChimps.CHIMP_TYPE_QUARRY_MASON || unit->r_ControllableForPlayerId!=owner ||
                    unit->r_LinkedProductionBuildingId!=oxQuarry || unit->r_LinkedProductionBuildingGlobalId!=oxQuarryGlobal ||
                    unit->r_AIState!=2)continue;
                var before=runtime.SnapshotUnit(id,true,false);
                if(before==null || before.Unsupported!=null || before.Job==null || !before.OrdinaryStep || before.Substep>=8 ||
                    !TryStartedMoatAnchor(before,out int x,out int y))continue;
                int slot=runtime.Native.Word(unit,0x9EE);
                if(slot>2 || *(ushort*)((byte*)quarry+0x1B8+slot*2)!=0)continue;
                int qx=quarry->r_TileAccessPositionX,qy=quarry->r_TileAccessPositionY;
                // Preserve the physical workplace and requested home, including
                // a margin around the real width2 footprint. Never paint stone.
                if(AppleStepContains(qx,qy,x,y,1) || AppleStepContains(before.RequestedX,before.RequestedY,x,y,1))continue;
                var world=new NativeWorkerResumeWorld(runtime.Native);
                if(!world.TryCapture(before,out var receipt,out _) || receipt.Bindings.Required!=WorkerExternalBindings.Workplace ||
                    receipt.Bindings.WorkplaceId!=oxQuarry || receipt.Bindings.WorkplaceGlobal!=oxQuarryGlobal)continue;
                return new MasonJourneyProof{Before=before,Slot=slot,AnchorX=x,AnchorY=y,QuarryX=qx,QuarryY=qy,
                    Actor=new Actor{Id=id,Global=unit->r_GlobalId,Health=unit->r_CurrentHealth,Type=unit->r_UnitChimp,
                        Tribe=unit->r_TribeId,X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y}};
            }
            return null;
        }
        private void ArmMasonJourney(MasonJourneyProof proof)
        {
            if(!masonObserversRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-mason-workplace",ObserveMasonJourneyTick,
                    out string reason,order:100),reason);
                masonProductionObserver=BuildingR3EventHooks.OnGoodsyardAddGood.Observable.Subscribe(args=>{
                    var fixture=masonFixture;
                    if(fixture!=null && !fixture.stopped)fixture.ObserveMasonProduction(args);
                });
                masonObserversRegistered=true;
            }
            masonJourney=proof;proof.Active=true;masonFixture=this;
        }
        private unsafe bool MasonWorkplaceRegistered(MasonJourneyProof proof)
        {
            ValidateOxSites();
            var unit=Resolve(proof.Actor);
            bool sameTribe=runtime.Native.TryTribeGlobal(proof.Actor.Tribe,out uint tribeGlobal) && tribeGlobal==proof.Before.TribeGlobalId &&
                (proof.Actor.Tribe==0 || runtime.Native.JobDword(unit,0x93C)==tribeGlobal);
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth==proof.Actor.Health && unit->r_TribeId==proof.Actor.Tribe &&
                unit->r_LinkedProductionBuildingId==oxQuarry && unit->r_LinkedProductionBuildingGlobalId==oxQuarryGlobal &&
                runtime.Native.Word(unit,0x9EE)==proof.Slot && sameTribe,
                "Original Mason identity, health, tribe lifetime, quarry or employee slot changed");
            Require(BuildingAccess.TryGetById(oxQuarry,out GameBuilding* quarry,out _) && quarry!=null &&
                quarry->r_TileAccessPositionX==proof.QuarryX && quarry->r_TileAccessPositionY==proof.QuarryY,
                "Original quarry access changed");
            if(runtime.Native.Word(unit,0x9B0)==0)CheckActor(proof.Actor,true);
            //131430 R2 checks C9080(<3 on both axes), then196F80 hides
            // and unlinks Current. AI1 subsequently writes this WORD slot.
            if(unit->r_AIState!=1 || runtime.Native.Word(unit,0x9B0)!=1)return false;
            Require(Math.Abs(unit->r_CurrentTilePositionX-proof.QuarryX)<3 && Math.Abs(unit->r_CurrentTilePositionY-proof.QuarryY)<3 &&
                Math.Abs(unit->r_CurrentTilePositionX-proof.Before.RequestedX)<=1 && Math.Abs(unit->r_CurrentTilePositionY-proof.Before.RequestedY)<=1,
                "Mason registered away from the actual original workplace and route endpoint");
            int current=runtime.Native.Tile(unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY);
            Require(current>=0 && unit->r_CurrentPositionTileId==(uint)current && runtime.Native.HasOccupancyCount(current,proof.Actor.Id,0),
                "Native hidden workplace retained a visible Current occupancy");
            proof.Arrived=true;
            bool registered=*(ushort*)((byte*)quarry+0x1B8+proof.Slot*2)==proof.Actor.Id;
            if(registered)proof.Registered=true;
            return registered;
        }
        private static void ObserveMasonJourneyTick(int tick)
        {
            var fixture=masonFixture;var proof=fixture?.masonJourney;
            if(fixture==null || fixture.stopped || proof==null || !proof.Active || proof.Fault!=null)return;
            try
            {
                if(!fixture.AutomaticActorAlive(proof.Actor))return;
                bool first=!proof.Registered;
                if(fixture.MasonWorkplaceRegistered(proof) && first)fixture.log("automatic-mason-registered tick="+tick+
                    " unit="+proof.Actor.Id+"/"+proof.Actor.Global+" quarry="+fixture.oxQuarry+"/"+fixture.oxQuarryGlobal+
                    " slot="+proof.Slot+" physicalWorkplace=true hiddenByNative=true currentOccupancy=0");
            }
            catch(Exception error){proof.Fault=error.Message;}
        }
        private unsafe void ObserveMasonProduction(AddGoodToGoodsyardEventArgs args)
        {
            var proof=masonJourney;
            if(proof==null || !proof.Active || proof.Fault!=null || args.BuildingId!=oxQuarryPile ||
                args.Good!=eGoods.STORED_STONE_BLOCKS || !args.Add || args.AddAmount<=0 ||
                GameBuildingManagerAPI.Instance.GetCurrentContextBuildingId()!=oxQuarry)return;
            try
            {
                if(!AutomaticActorAlive(proof.Actor) || !MasonWorkplaceRegistered(proof))return;
                Require(BuildingAccess.TryGetById(oxQuarryPile,out GameBuilding* pile,out _) && pile!=null &&
                    pile->r_GlobalId==unchecked((uint)args.BuildingGlobalId) && pile->r_GlobalId==oxQuarryPileGlobal,
                    "Quarry production changed its actual selected pile lifetime");
                if(args.Phase==EventHookPhase.Pre)
                {
                    Require(!proof.Pending,"Nested quarry production invalidated the matched stock write");
                    proof.Pending=true;proof.PendingStone=pile->r_StoneBlocksAmount;proof.PendingAmount=args.AddAmount;
                }
                else
                {
                    Require(proof.Pending && proof.PendingAmount==args.AddAmount && pile->r_StoneBlocksAmount==proof.PendingStone+proof.PendingAmount,
                        "Actual original quarry production has no matched positive physical pile write");
                    proof.Pending=false;proof.ProductionEvents++;proof.Produced+=proof.PendingAmount;
                    log("automatic-mason-native-production unit="+proof.Actor.Id+"/"+proof.Actor.Global+" slot="+proof.Slot+
                        " quarry="+oxQuarry+"/"+oxQuarryGlobal+" pile="+oxQuarryPile+"/"+oxQuarryPileGlobal+
                        " actualStone="+proof.PendingStone+"->"+pile->r_StoneBlocksAmount+" workerDeliveryClaim=false");
                }
            }
            catch(Exception error){proof.Fault=error.Message;}
        }
        private IEnumerable<AutomationWait> AutomaticMason(AutomationRun run)
        {
            MasonJourneyProof proof=null;AutomaticWorkerMutation mutation=null;
            try
            {
                int quarry=0,tether=0;
                run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
                Execute(run,()=>{
                    EnsureOxPrerequisites();Require(TryCreateNaturalQuarry(out quarry,out tether),"No actual natural quarry/tether on the selected resource map");
                    CaptureOxSites(quarry,tether);
                });
                // Acquire the initial R2 before waiting for quarry production.
                Director.instance.SetEngineFrameRate(50);
                for(int sample=0;sample<5000 && proof==null;sample++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{
                        proof=FindNaturalMasonJourney();
                        if(sample%128==0)LogOxAcquisition(sample);return true;
                    });
                }
                Require(proof!=null,"No natural Mason2 initial started journey reached a clear legal Keep-range moat source");
                run.InSimulation(run.CurrentMission,()=>{
                    actors.Add(proof.Actor);CheckActor(proof.Actor,true);ArmMasonJourney(proof);
                    mutation=ApplyAutomaticStartedWorkerMoat(proof.Actor,proof.Before,proof.AnchorX,proof.AnchorY);
                    log("automatic-mason-source unit="+proof.Actor.Id+"/"+proof.Actor.Global+" slot="+proof.Slot+
                        " quarry="+quarry+"/"+oxQuarryGlobal+" originalGoal="+proof.Before.RequestedX+","+proof.Before.RequestedY+
                        " nativeR2=true manualAIJobCargoWrites=0");return true;
                });
                Director.instance.SetEngineFrameRate(250);
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?8000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,expectedEnabled?4:2);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        if(expectedEnabled)Require(proof.Fault==null,"Mason workplace/production observation failed: "+proof.Fault);
                        if(!AutomaticActorAlive(proof.Actor))return true;
                        if(sample%128==0)log("automatic-mason-progress sample="+sample+" arrived="+proof.Arrived+
                            " registered="+proof.Registered+" produced="+proof.Produced+" fault="+(proof.Fault??"none"));
                        return expectedEnabled && proof.Registered && proof.ProductionEvents>0 && proof.Produced>0;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    proof.Active=false;automaticWorkerMoves.Active=false;
                    var observer=Plugin.GeneralObserver;bool alive=AutomaticActorAlive(proof.Actor);
                    if(expectedEnabled)
                    {
                        Require(alive && proof.Fault==null && MasonWorkplaceRegistered(proof) && proof.Arrived && proof.Registered &&
                            proof.Produced>0 && proof.ProductionEvents>0 && !proof.Pending && automaticWorkerMoves.Fault==null &&
                            automaticWorkerMoves.OwnSuccesses>0 && observer.RescueCount>mutation.Rescues,
                            "Rescued original Mason did not physically register and participate in subsequent actual quarry production");
                    }
                    else Require(observer.CaptureCount==mutation.Captures && observer.AttemptCount==mutation.Attempts &&
                        observer.RescueCount==mutation.Rescues && automaticWorkerMoves.OwnBegins==0 &&
                        (alive || automaticWorkerMoves.DeletionObserved),"Disabled Mason outcome lacks native lifetime evidence or executed own recovery");
                    log("automatic-mason-result enabled="+expectedEnabled+" unit="+proof.Actor.Id+"/"+proof.Actor.Global+
                        " originalAlive="+alive+" actualWorkplaceArrival="+proof.Arrived+" originalSlotRegistered="+proof.Registered+
                        " actualProductionWrites="+proof.ProductionEvents+" actualProducedStone="+proof.Produced+
                        " ownNative1="+automaticWorkerMoves.OwnSuccesses+" nativeDeletionObserved="+automaticWorkerMoves.DeletionObserved+
                        " hiddenWorkplaceOccupancyExpected=0 ownMasonGoodsDeliveryClaim=false fault="+(proof.Fault??"none"));
                    Plugin.SetGeneralFixtureSession(false);return true;
                });
            }
            finally
            {
                if(proof!=null)proof.Active=false;
                if(ReferenceEquals(masonFixture,this))masonFixture=null;
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;
                Director.instance.SetEngineFrameRate(250);
            }
        }
    }
}
