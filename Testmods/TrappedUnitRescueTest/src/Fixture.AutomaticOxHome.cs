using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using CrusaderDE;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class AutomaticOxHomeProof
        {
            internal Actor Actor;
            internal int Tether,GoalX,GoalY,NextCargo;
            internal uint TetherGlobal;
            internal bool Active,ReachedHome,Refilled;
            internal string Fault;
        }
        private AutomaticOxHomeProof automaticOxHomeProof;
        private static volatile Fixture automaticOxHomeFixture;
        private static bool automaticOxHomeTickRegistered;

        private IEnumerable<AutomationStep> AutomaticOxHomeSteps()
        {yield return new AutomationStep("automatic-natural-empty-ox-home-refill-and-delivery",TimeSpan.FromSeconds(1200),AutomaticOxHome,true);}

        private IEnumerable<AutomationWait> AutomaticOxHome(AutomationRun run)
        {
            int quarry=0,tether=0;
            run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
            Execute(run,()=>{
                EnsureOxPrerequisites();
                Require(TryCreateNaturalQuarry(out quarry,out tether),"No real quarry/tether world for empty Ox home acceptance");
                EnsureOxDeliveryYard(tether);CaptureOxSites(quarry,tether);LogOxAcquisition(-1);
            });
            Actor ox=null;bool ready=false;
            for(int sample=0;sample<512 && ox==null && !ready;sample++)
            {
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,32);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{ValidateOxSites();ox=CaptureLoadedOxForHomeObservation(tether);OxProductionReadiness(out ready);
                    if(sample%64==0 || ox!=null)LogOxAcquisition(sample);});
            }
            Director.instance.SetEngineFrameRate(50);
            try
            {
                for(int sample=0;sample<4096 && ox==null;sample++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Execute(run,()=>{ValidateOxSites();ox=CaptureLoadedOxForHomeObservation(tether);
                        if(sample%64==0 || ox!=null)LogOxAcquisition(512+sample);});
                }
                Require(ox!=null,"No natural original loaded Ox; no empty-home coverage established");
                run.InSimulation(run.CurrentMission,()=>{
                    actors.Add(ox);CheckActor(ox,true);BeginOxObservation(ox,tether,OxCargo(ox));return true;
                });
                bool firstDelivered=false;
                for(int sample=0;sample<4096 && !firstDelivered;sample++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    firstDelivered=run.InSimulation(run.CurrentMission,()=>{
                        ValidateOxSites();bool result=ObserveOxOutcome();
                        Require(!oxObservation.Lost,"Original Ox was lost before the actual first delivery");
                        if(sample%64==0 || result)LogAutomaticOxHomeProgress(ox,"first-delivery",sample);
                        return result;
                    });
                }
                Require(firstDelivered,"Empty-home test requires this same Ox's first complete physical stone delivery");
                run.InSimulation(run.CurrentMission,()=>{
                    log("automatic-oxhome-first-delivery unit="+ox.Id+"/"+ox.Global+" accepted="+oxObservation.AcceptedQuantity+
                        " originalCargo="+oxObservation.InitialCargo+" physicalContact="+oxObservation.PhysicallyNearStorage+
                        " cargoEmpty="+oxObservation.CargoEmpty);
                    oxObservation.Active=false;return true;
                });
                OxTransitPlacement home=null;
                for(int sample=0;sample<4096 && home==null;sample++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    home=run.InSimulation(run.CurrentMission,()=>{
                        ValidateOxSites();CheckActor(ox,true);
                        var value=CaptureAutomaticEmptyOxHome(ox,tether);
                        if(sample%64==0 || value!=null)LogAutomaticOxHomeProgress(ox,"empty-home-acquisition",sample);
                        return value;
                    });
                }
                Require(home!=null,"No natural empty AI3 route at a clear coherent transit boundary; do not fabricate home intent");
                AutomaticWorkerMutation mutation=null;
                run.InSimulation(run.CurrentMission,()=>{
                    // The existing pure native reader validates current job and active external lifetimes.
                    var world=new NativeWorkerResumeWorld(runtime.Native);
                    Require(world.TryCapture(home.Before,out var receipt,out string reason),"Empty-home receipt unavailable: "+reason);
                    Require(receipt.Bindings.Required==WorkerExternalBindings.Workplace && receipt.Bindings.WorkplaceId==tether &&
                        receipt.Bindings.WorkplaceGlobal==oxTetherGlobal,"AI3 acquired a fabricated active store or different tether");
                    BeginAutomaticOxHomeProof(ox,tether,home.Before.RequestedX,home.Before.RequestedY);
                    mutation=ApplyAutomaticOxTransit(home);
                    Require(mutation.Cargo==0,"Empty-home mutation changed or fabricated cargo");return true;
                });
                Director.instance.SetEngineFrameRate(250);
                bool nextDelivered=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?4000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,expectedEnabled?4:2);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        var proof=automaticOxHomeProof;
                        if(!AutomaticActorAlive(ox))return true;
                        if(expectedEnabled)Require(proof.Fault==null,"Empty-home tick observation failed: "+proof.Fault);
                        if(proof.Refilled)nextDelivered=ObserveOxOutcome();
                        if(sample%64==0 || nextDelivered)LogAutomaticOxHomeProgress(ox,"after-mutation",sample);
                        return nextDelivered;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    var proof=automaticOxHomeProof;proof.Active=false;
                    if(proof.Refilled)oxObservation.Active=false;
                    if(expectedEnabled)Require(proof.Fault==null && proof.ReachedHome && proof.Refilled && nextDelivered &&
                        !oxObservation.Lost && oxObservation.AcceptedQuantity==proof.NextCargo && oxObservation.CargoEmpty,
                        "Rescued empty Ox did not physically return, naturally refill and deliver its exact next real charge");
                    log("automatic-oxhome-result enabled="+expectedEnabled+" sameOx="+ox.Id+"/"+ox.Global+
                        " originalCargo=0 reachedOriginalHome="+proof.ReachedHome+" naturallyRefilled="+proof.Refilled+
                        " nextRealCargo="+proof.NextCargo+" nextPhysicalDelivery="+nextDelivered+
                        " nextAccepted="+(proof.Refilled?oxObservation.AcceptedQuantity:0)+" fault="+(proof.Fault??"none"));
                    EndAutomaticWorkerCase(mutation,nextDelivered);return true;
                });
            }
            finally
            {
                if(automaticOxHomeProof!=null)automaticOxHomeProof.Active=false;
                if(ReferenceEquals(automaticOxHomeFixture,this))automaticOxHomeFixture=null;
                Director.instance.SetEngineFrameRate(250);
            }
        }
        private unsafe Actor CaptureLoadedOxForHomeObservation(int tetherId)
        {
            // This first trip is observed without changing geometry. A healthy
            // moving Ox need not publish the SS8 pose required by old trap tests.
            if(!BuildingAccess.TryGetById(tetherId,out GameBuilding* tether,out _) || tether==null ||
                tether->r_GlobalId!=oxTetherGlobal || tether->r_PlayerIdOwner!=owner ||
                tether->r_BuildingType!=eStructs.STRUCT_OXEN_BASE || tether->r_AliveState!=AliveState.IsAlive)return null;
            for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* unit,out _) || unit==null || !UnitAccess.IsReallyAlive(unit) ||
                    unit->r_UnitChimp!=eChimps.CHIMP_TYPE_QUARRY_OX || unit->r_ControllableForPlayerId!=owner ||
                    unit->r_LinkedProductionBuildingId!=tetherId || unit->r_LinkedProductionBuildingGlobalId!=oxTetherGlobal ||
                    unit->r_AIState!=1 || unit->r_CarryGoodAmount<=0)continue;
                int storeId=runtime.Native.Word(unit,0x98E);
                uint storeGlobal=runtime.Native.JobDword(unit,0x6F8);
                if(storeId<=0 || !BuildingAccess.TryGetById(storeId,out GameBuilding* store,out _) || store==null ||
                    store->r_GlobalId!=storeGlobal || store->r_PlayerIdOwner!=owner ||
                    store->r_BuildingType!=eStructs.STRUCT_GOODS_YARD || store->r_AliveState!=AliveState.IsAlive)continue;
                return new Actor{Id=id,Global=unit->r_GlobalId,Health=unit->r_CurrentHealth,Type=unit->r_UnitChimp,
                    Tribe=unit->r_TribeId,X=unit->r_CurrentTilePositionX,Y=unit->r_CurrentTilePositionY,
                    SourceX=unit->r_CurrentTilePositionX,SourceY=unit->r_CurrentTilePositionY};
            }
            return null;
        }
        private OxTransitPlacement CaptureAutomaticEmptyOxHome(Actor actor,int tether)=>CaptureAutomaticOxTransit(actor,tether,3);
        private void BeginAutomaticOxHomeProof(Actor actor,int tether,int x,int y)
        {
            if(!automaticOxHomeTickRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-automatic-ox-home",
                    tick=>automaticOxHomeFixture?.ObserveAutomaticOxHomeTick(),out string reason),"Ox-home tick observation unavailable: "+reason);
                automaticOxHomeTickRegistered=true;
            }
            automaticOxHomeProof=new AutomaticOxHomeProof{Actor=actor,Tether=tether,TetherGlobal=oxTetherGlobal,GoalX=x,GoalY=y,Active=true};
            automaticOxHomeFixture=this;
        }
        private unsafe void ObserveAutomaticOxHomeTick()
        {
            var proof=automaticOxHomeProof;
            if(stopped || proof==null || !proof.Active || proof.Fault!=null)return;
            try
            {
                if(!AutomaticActorAlive(proof.Actor))return;
                CheckActor(proof.Actor,true);var unit=Resolve(proof.Actor);
                Require(unit->r_LinkedProductionBuildingId==proof.Tether && unit->r_LinkedProductionBuildingGlobalId==proof.TetherGlobal,
                    "Ox changed original tether during empty-home continuation");
                // Another native worker may refill this record later in the same
                // unit pass as the Ox's3->4 arrival; zero cargo here is not required.
                if(!proof.ReachedHome && unit->r_AIState==4)
                {
                    Require(Math.Abs(unit->r_CurrentTilePositionX-proof.GoalX)<=1 && Math.Abs(unit->r_CurrentTilePositionY-proof.GoalY)<=1,
                        "Native home phase4 occurred away from the recorded original endpoint");
                    Require(BuildingAccess.TryGetById(proof.Tether,out GameBuilding* tether,out _) && tether!=null &&
                        tether->r_GlobalId==proof.TetherGlobal && tether->r_AliveState==AliveState.IsAlive &&
                        Math.Abs(unit->r_CurrentTilePositionX-tether->r_TileAccessPositionX)<3 &&
                        Math.Abs(unit->r_CurrentTilePositionY-tether->r_TileAccessPositionY)<3,"Ox home arrival lacks real original tether contact");
                    proof.ReachedHome=true;
                }
                if(proof.ReachedHome && !proof.Refilled && unit->r_CarryGoodAmount>0)
                {
                    proof.NextCargo=unit->r_CarryGoodAmount;
                    BeginOxObservation(proof.Actor,proof.Tether,proof.NextCargo);proof.Refilled=true;
                }
            }
            catch(Exception error){proof.Fault=error.ToString();}
        }
        private unsafe void LogAutomaticOxHomeProgress(Actor actor,string phase,int sample)
        {
            if(!AutomaticActorAlive(actor)){log("automatic-oxhome-progress phase="+phase+" sample="+sample+" originalAlive=false");return;}
            var unit=Resolve(actor);var proof=automaticOxHomeProof;
            log("automatic-oxhome-progress phase="+phase+" sample="+sample+" unit="+actor.Id+"/"+actor.Global+
                " ai="+unit->r_AIState+" flags="+unit->r_PathPlanStateBitFlags+" substep="+unit->r_MovementSubstep+
                " cargo="+unit->r_CarryGoodAmount+" tile="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                " reachedHome="+(proof!=null && proof.ReachedHome)+" refilled="+(proof!=null && proof.Refilled));
        }
    }
}
