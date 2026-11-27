using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Units;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class AutomaticWorkerMoves
        {
            internal Actor Actor;
            internal long Session;
            internal int GoalX,GoalY,OwnBegins,OwnSuccesses,OwnRefusals,ProbeBegins,RelocatedBegins,ExternalRefusals;
            internal bool Active,DeletionObserved;
            internal string Fault;
            internal Action<string> Log;internal int RefusalLogs;
        }
        private AutomaticWorkerMoves automaticWorkerMoves;
        private static bool automaticWorkerObserverRegistered;
        private static volatile Fixture automaticWorkerObserverFixture;
        private sealed class AutomaticWorkerMoveObserver : IUnitMoveObserver
        {
            public IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var fixture=automaticWorkerObserverFixture;
                if(fixture==null || fixture.stopped)return null;
                var proof=fixture.automaticWorkerMoves;
                if(proof==null || !proof.Active || context==null || context.UnitId!=proof.Actor.Id || context.SessionId!=proof.Session)return null;
                bool own=context.OriginOwnerGuid==Plugin.RescueOwnerGuid;
                if(own)
                {
                    proof.OwnBegins++;
                    if(context.X!=proof.GoalX || context.Y!=proof.GoalY || context.Extra!=0)proof.Fault="own continuation changed the original native job goal";
                    try
                    {
                        fixture.CaptureAutomaticMoveStart(proof);
                    }
                    catch(Exception error){proof.Fault="own move capture failed: "+error.GetType().FullName;}
                }
                return new AutomaticWorkerMoveCompletion(proof,context,own);
            }
        }
        private sealed class AutomaticWorkerMoveCompletion : IUnitMoveInvocation
        {
            private readonly AutomaticWorkerMoves proof;
            private readonly UnitMoveContext context;
            private readonly bool own;
            private int completed;
            internal AutomaticWorkerMoveCompletion(AutomaticWorkerMoves proof,UnitMoveContext context,bool own){this.proof=proof;this.context=context;this.own=own;}
            public void Complete(UnitMoveCompletion value)
            {
                if(Interlocked.Exchange(ref completed,1)!=0 || value==null || !ReferenceEquals(value.Context,context))return;
                if(!value.NativeInvoked || !value.NativeReturned || !value.IsCurrentWorld)
                {if(own)proof.Fault="own move did not complete its actual current native original";return;}
                if(own){if(value.NativeResult==1)proof.OwnSuccesses++;else if(value.NativeResult==0)proof.OwnRefusals++;else proof.Fault="unsupported own native move result";}
                else if(value.NativeResult==0)proof.ExternalRefusals++;
                ObserveAutomaticDeletion(proof);
                if(value.NativeResult==0 && proof.RefusalLogs++<8)LogAutomaticRefusal(proof,context);
            }
        }
        private unsafe void CaptureAutomaticMoveStart(AutomaticWorkerMoves proof)
        {
            GameUnit* unit=null;
            Require(UnitAccess.TryGetById(proof.Actor.Id,out unit,out _) && unit!=null && unit->r_GlobalId==proof.Actor.Global,
                "Automatic move observer lost the original unit lifetime");
            if(unit->r_CurrentTilePositionX==proof.Actor.SourceX && unit->r_CurrentTilePositionY==proof.Actor.SourceY)proof.ProbeBegins++;
            else proof.RelocatedBegins++;
        }
        private static unsafe void ObserveAutomaticDeletion(AutomaticWorkerMoves proof)
        {
            GameUnit* unit=null;
            if(UnitAccess.TryGetById(proof.Actor.Id,out unit,out _) && unit!=null && unit->r_GlobalId==proof.Actor.Global &&
                unit->r_AliveState==AliveState.MarkedForDeletion)proof.DeletionObserved=true;
        }
        private static unsafe void LogAutomaticRefusal(AutomaticWorkerMoves proof,UnitMoveContext context)
        {
            string state="missing";
            if(UnitAccess.TryGetById(proof.Actor.Id,out GameUnit* unit,out _) && unit!=null)
                state="actualGlobal="+unit->r_GlobalId+" life="+unit->r_AliveState+" ai="+unit->r_AIState+
                    " flags="+unit->r_PathPlanStateBitFlags+" substep="+unit->r_MovementSubstep+
                    " current="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                    " requested="+unit->r_TargetTilePositionX2+","+unit->r_TargetTilePositionY2+
                    " cargo="+unit->r_CarryGoodAmount+" workplace="+unit->r_LinkedProductionBuildingId;
            proof.Log?.Invoke("automatic-native-refusal unit="+proof.Actor.Id+"/"+proof.Actor.Global+
                " origin="+context.OriginOwnerGuid+" callGoal="+context.X+","+context.Y+" "+state);
        }
        private void BeginAutomaticWorkerMoves(Actor actor,int x,int y)
        {
            if(!automaticWorkerObserverRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-automatic-worker-moves",new AutomaticWorkerMoveObserver(),out string reason,
                    enabled:()=>automaticWorkerObserverFixture!=null && !automaticWorkerObserverFixture.stopped &&
                        automaticWorkerObserverFixture.automaticWorkerMoves!=null && automaticWorkerObserverFixture.automaticWorkerMoves.Active),"Automatic worker native receipt unavailable: "+reason);
                automaticWorkerObserverRegistered=true;
            }
            automaticWorkerMoves=new AutomaticWorkerMoves{Actor=actor,Session=session,GoalX=x,GoalY=y,Active=true,Log=log};
            automaticWorkerObserverFixture=this;
        }
        private sealed class AutomaticWorkerMutation
        {
            internal Actor Actor;
            internal int GoalX,GoalY,Cargo;
            internal long Captures,Attempts,Rescues;
        }
        // Test construction deliberately does NOT use Execute, BeginMutation, the
        // Apple pilot receipt or a manual reissue. The public mutation/tick owners act.
        private unsafe AutomaticWorkerMutation ApplyAutomaticWorkerMoat(Actor actor)
        {
            var before=runtime.SnapshotUnit(actor.Id);
            Require(before!=null && before.Unsupported==null && before.Job!=null && (before.Flags==2 || before.Flags==4) &&
                before.Substep==8 && before.X==before.NextX && before.Y==before.NextY && MoatWorkerSourceEligible(before.TileId),
                "Natural actor no longer qualifies for the audited automatic moat source");
            Require(before.RequestedX!=before.X || before.RequestedY!=before.Y,"Automatic worker source has no distinct original job target");
            Require(before.Candidates.Any(c=>runtime.Native.OrdinaryGround(c.Tile) && runtime.Native.Head(c.Tile)==0),
                "Automatic source has no old reachable free local bank");
            actor.Health=before.Health;actor.X=actor.SourceX=before.X;actor.Y=actor.SourceY=before.Y;
            var result=new AutomaticWorkerMutation{Actor=actor,GoalX=before.RequestedX,GoalY=before.RequestedY,Cargo=WorkerCarry(actor)};
            BeginAutomaticWorkerMoves(actor,result.GoalX,result.GoalY);
            Plugin.SetGeneralFixtureSession(expectedEnabled);
            var observer=Plugin.GeneralObserver;
            Require(observer!=null && observer.IsActive==expectedEnabled,"Automatic general session differs from the requested fixture mode");
            result.Captures=observer.CaptureCount;result.Attempts=observer.AttemptCount;result.Rescues=observer.RescueCount;
            var tiles=GameTileManagerAPI.Instance;
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            log("automatic-worker-before-writer type="+actor.Type+" unit="+actor.Id+"/"+actor.Global+" ai="+before.AIState+
                " source="+before.X+","+before.Y+" next="+before.NextX+","+before.NextY+" flags="+before.Flags+" substep="+before.Substep+
                " goal="+before.RequestedX+","+before.RequestedY+" cargo="+result.Cargo+
                " tileFlags="+((uint)tiles.GetTilePropertyFlag(before.TileId)).ToString("X8")+" height="+tiles.GetTileHeight(before.TileId)+
                " building="+tiles.GetTileBuildingId(before.TileId)+" vegetation="+tiles.GetTileVegetationId(before.TileId)+
                " ownKeep="+keep.X+","+keep.Y+" keepDistance="+Math.Max(Math.Abs(before.X-keep.X),Math.Abs(before.Y-keep.Y)));
            EditorBrush.Call(tiles.GetTileManager(),owner,before.TileId,before.Y,1,unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require((tiles.GetTilePropertyFlag(before.TileId)&TilePropertyFlag.IsMoat)!=0,"Actual editor writer did not create completed moat on the qualified occupied source");
            CheckActor(actor,true);
            Require(AtSource(actor) && WorkerCarry(actor)==result.Cargo && Resolve(actor)->r_TargetTilePositionX2==result.GoalX &&
                Resolve(actor)->r_TargetTilePositionY2==result.GoalY,"Construction synchronously changed the original actor/job/cargo before recovery");
            Require(observer.AttemptCount==result.Attempts && observer.RescueCount==result.Rescues,"Automatic rescue ran inside construction instead of the admitted recovery callback");
            if(expectedEnabled)Require(observer.CaptureCount>result.Captures && observer.PendingCount>0,"Real editor source did not enqueue its automatically captured causal proof");
            else Require(observer.CaptureCount==result.Captures && observer.PendingCount==0,"Disabled general observer captured the test construction");
            log("automatic-worker-source type="+actor.Type+" unit="+actor.Id+"/"+actor.Global+" ai="+before.AIState+" cargo="+result.Cargo+
                " source="+before.X+","+before.Y+" originalGoal="+result.GoalX+","+result.GoalY+" realEditorMoat=true directRescue=false suppression=false deferred=true enabled="+expectedEnabled);
            return result;
        }
        private unsafe bool AutomaticActorAlive(Actor actor)
        {
            GameUnit* unit=null;
            return UnitAccess.TryGetById(actor.Id,out unit,out _) && unit!=null && unit->r_GlobalId==actor.Global && UnitAccess.IsReallyAlive(unit);
        }
        private void RequireAutomaticContinuation(AutomaticWorkerMutation mutation)
        {
            var observer=Plugin.GeneralObserver;var moves=automaticWorkerMoves;
            Require(moves!=null && moves.Fault==null,moves?.Fault??"missing automatic native move observation");
            Require(observer.AttemptCount>mutation.Attempts && observer.RescueCount>mutation.Rescues && moves.OwnBegins>0 && moves.OwnSuccesses>0,
                "No own automatically queued recovery published this original worker's actual native continuation");
            CheckActor(mutation.Actor,true);
        }
        private void EndAutomaticWorkerCase(AutomaticWorkerMutation mutation,bool delivered)
        {
            var moves=automaticWorkerMoves;var observer=Plugin.GeneralObserver;bool alive=AutomaticActorAlive(mutation.Actor);
            if(expectedEnabled)RequireAutomaticContinuation(mutation);
            else
            {
                Require(observer.CaptureCount==mutation.Captures && observer.AttemptCount==mutation.Attempts && observer.RescueCount==mutation.Rescues && moves.OwnBegins==0,
                    "Disabled automatic fixture executed own capture/recovery");
                Require(alive || moves.DeletionObserved,"Baseline actor was lost without an observed native deletion receipt; outcome is unqualified");
                if(alive)CheckActor(mutation.Actor,true);
            }
            bool source=alive && AtSource(mutation.Actor),exit=alive && HasStructuralExit(mutation.Actor);
            string outcome=expectedEnabled?"own-original-segment-completed":!alive?"native-deletion-observed":delivered?"legal-native-delivery":
                !source?"native-movement":exit?"legal-egress-available":"actual-blocked-source";
            log("automatic-worker-result type="+mutation.Actor.Type+" enabled="+expectedEnabled+" outcome="+outcome+" originalAlive="+alive+
                " atOriginalSource="+source+" structuralExit="+exit+" delivered="+delivered+" ownSuccess="+moves.OwnSuccesses+" ownRefusals="+moves.OwnRefusals+
                " ownProbeStarts="+moves.ProbeBegins+" ownRelocatedStarts="+moves.RelocatedBegins+" externalRefusals="+moves.ExternalRefusals+
                " baselineTrapReproduced="+(!expectedEnabled && alive && source && !exit)+" deferredObserverRescues="+(observer.RescueCount-mutation.Rescues));
            moves.Active=false;
            if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;
            Plugin.SetGeneralFixtureSession(false);
        }
        private int automaticAppleAccessSkips;
        private unsafe bool AutomaticAppleTransitEligible(Actor actor)
        {
            // A positive full-cycle fixture must not turn its own future granary or
            // workplace access into moat. Inspect actual native access positions.
            ValidateAppleWorld();var unit=Resolve(actor);GameBuilding* granary=null;GameBuilding* farm=null;
            Require(BuildingAccess.TryGetById(appleWorld.Granary,out granary,out _) && granary!=null &&
                BuildingAccess.TryGetById(appleWorld.Farm,out farm,out _) && farm!=null,"Apple job access objects unavailable");
            int gx=granary->r_TileAccessPositionX,gy=granary->r_TileAccessPositionY;
            int fx=farm->r_TileAccessPositionX,fy=farm->r_TileAccessPositionY;
            int x=unit->r_CurrentTilePositionX,y=unit->r_CurrentTilePositionY;
            bool eligible=Math.Max(Math.Abs(x-gx),Math.Abs(y-gy))>=3 && Math.Max(Math.Abs(x-fx),Math.Abs(y-fy))>=3;
            if(!eligible && automaticAppleAccessSkips++<16)
                log("automatic-apple-source-skipped unit="+actor.Id+" source="+x+","+y+" granaryAccess="+gx+","+gy+
                    " farmAccess="+fx+","+fy+" reason=protect-future-job-access");
            return eligible;
        }
        private IEnumerable<AutomationStep> AutomaticWorkerSteps()
        {yield return new AutomationStep("automatic-natural-apple-moat-and-cycle",TimeSpan.FromSeconds(600),AutomaticWorker,true);}
        private IEnumerable<AutomationStep> AutomaticOxSteps()
        {yield return new AutomationStep("automatic-natural-loaded-ox-moat-and-delivery",TimeSpan.FromSeconds(780),AutomaticOx,true);}
        private IEnumerable<AutomationWait> AutomaticWorker(AutomationRun run)
        {
            using(var proof=ObserveAppleWorld())
            {
                int farm=0;run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
                Execute(run,()=>farm=SetupNaturalAppleFarm());
                Actor worker=null;int requiredState=scenario.Contains("-cargo-")?7:5; // Native135E30 delivery/empty return.
                for(int sample=0;sample<4000 && worker==null;sample++)
                {
                    if(sample==256)Director.instance.SetEngineFrameRate(50);
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,sample<256?16:1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Execute(run,()=>{worker=FindMoatWorker(farm,requiredState);if(worker!=null && !AutomaticAppleTransitEligible(worker))worker=null;
                        if(sample%128==0 || sample==3999)LogAppleAcquisition(farm,sample);});
                }
                Require(worker!=null,"No natural Apple5/7 route reached the actual moat-eligible clear source; automatic coverage not obtained");
                Director.instance.SetEngineFrameRate(250);
                AutomaticWorkerMutation mutation=null;int dropsRequired=1;
                run.InSimulation(run.CurrentMission,()=>{
                    actors.Add(worker);var before=runtime.SnapshotUnit(worker.Id);
                    Require(before!=null && before.Unsupported==null,"Natural Apple source changed before automatic preparation");
                    dropsRequired=WorkerCarry(worker)>0?2:1;
                    BeginWorkerCycle(worker,farm,before.RequestedX,before.RequestedY);
                    Require(AutomaticAppleTransitEligible(worker),"Automatic Apple source entered a protected job access area");
                    mutation=ApplyAutomaticWorkerMoat(worker);return true;
                });
                bool reached=false,delivered=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?1600:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        if(!AutomaticActorAlive(worker))return true;
                        reached|=WorkerAtRecordedTarget(worker,farm,mutation.GoalX,mutation.GoalY);
                        delivered=workerCycle.Delivered && workerCycle.CounterIncrements>0 && workerCycle.DropEvents>=dropsRequired;
                        return expectedEnabled && delivered;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    workerCycle.Active=false;
                    log("automatic-apple-observation enabled="+expectedEnabled+" alive="+AutomaticActorAlive(worker)+
                        " originalSegmentReached="+reached+" samples="+workerCycle.Samples+" delivered="+delivered+
                        " counterIncrements="+workerCycle.CounterIncrements+" remotePicking="+workerCycle.RemotePicking+
                        " pickup="+workerCycle.PickupEvents+" drops="+workerCycle.DropEvents+" stockIncreases="+workerCycle.DropsWithStockIncrease+
                        " observationFault="+(workerCycle.Fault??"none"));
                    if(expectedEnabled)
                    {
                        Require(workerCycle.Fault==null,"Automatic apple observation failed: "+workerCycle.Fault);
                        Require(reached && delivered && workerCycle.RemotePicking==0 && workerCycle.PickupEvents>0 && workerCycle.DropsWithStockIncrease>0,
                            "Automatic Apple rescue did not complete its original segment plus a physically valid harvest/cargo/granary cycle");
                    }
                    log("automatic-apple-cycle enabled="+expectedEnabled+" originalSegmentReached="+reached+" completeHarvestDelivery="+delivered+
                        " counterIncrements="+workerCycle.CounterIncrements+" remotePicking="+workerCycle.RemotePicking+" pickup="+workerCycle.PickupEvents+
                        " drops="+workerCycle.DropEvents+" nativeStockIncreases="+workerCycle.DropsWithStockIncrease+" observationFault="+(workerCycle.Fault??"none"));
                    EndAutomaticWorkerCase(mutation,delivered);return true;
                });
            }
        }
        private IEnumerable<AutomationWait> AutomaticOx(AutomationRun run)
        {
            int quarry=0,tether=0;run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
            Execute(run,()=>{
                EnsureOxPrerequisites();Require(TryCreateNaturalQuarry(out quarry,out tether),"No natural quarry/tether site for automatic loaded-Ox test");
                EnsureOxDeliveryYard(tether);CaptureOxSites(quarry,tether);LogOxAcquisition(-1);
            });
            Actor ox=null;OxTransitPlacement placement=null;bool ready=false;
            for(int sample=0;sample<512 && ox==null && !ready;sample++)
            {
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,32);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{ValidateOxSites();placement=FindAutomaticOxTransit(tether,1);ox=placement?.Actor;
                    OxProductionReadiness(out ready);if(sample%64==0 || ox!=null)LogOxAcquisition(sample);});
            }
            if(ox==null)
            {
                Director.instance.SetEngineFrameRate(50);
                try
                {
                    for(int sample=0;sample<4096 && ox==null;sample++)
                    {
                        foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                        yield return run.WaitForTicks(run.CurrentMission,1);
                        foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                        Execute(run,()=>{ValidateOxSites();placement=FindAutomaticOxTransit(tether,1);ox=placement?.Actor;
                            if(sample%64==0 || ox!=null)LogOxAcquisition(512+sample);});
                    }
                }
                finally{Director.instance.SetEngineFrameRate(250);}
            }
            Require(ox!=null,"No natural loaded Ox1 reached the clear moat-eligible source; no AI/cargo state was fabricated");
            AutomaticWorkerMutation mutation=null;
            run.InSimulation(run.CurrentMission,()=>{
                actors.Add(ox);CheckActor(ox,true);BeginOxObservation(ox,tether,OxCargo(ox));
                mutation=ApplyAutomaticOxTransit(placement);return true;
            });
            bool delivered=false;
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            for(int sample=0;sample<(expectedEnabled?2000:24);sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission,expectedEnabled?4:2);
                bool stop=run.InSimulation(run.CurrentMission,()=>{delivered=ObserveOxOutcome();return delivered || oxObservation.Lost;});
                if(stop)break;
            }
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            run.InSimulation(run.CurrentMission,()=>{
                oxObservation.Active=false;
                if(expectedEnabled)Require(oxObservation.Fault==null && !oxObservation.Lost && delivered && oxObservation.CargoEmpty &&
                    oxObservation.AcceptedQuantity==oxObservation.InitialCargo,"Automatically rescued Ox did not deliver exactly its original real cargo batch");
                log("automatic-ox-delivery enabled="+expectedEnabled+" initialCargo="+oxObservation.InitialCargo+" nativeAccepted="+oxObservation.AcceptedQuantity+
                    " cargoEmpty="+oxObservation.CargoEmpty+" originalLost="+oxObservation.Lost+" physicalStorageContact="+oxObservation.PhysicallyNearStorage+
                    " observationFault="+(oxObservation.Fault??"none"));
                EndAutomaticWorkerCase(mutation,delivered);return true;
            });
        }
        private sealed class OxTransitPlacement
        {
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal int AnchorX,AnchorY,StoreId;
            internal uint StoreGlobal;
            internal bool Started;
        }
        private unsafe OxTransitPlacement FindAutomaticOxTransit(int tether,int state)
        {
            for(int id=1;id<=NativeRelocation.MaximumUnitId;id++)
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* unit,out _) || unit==null || !UnitAccess.IsReallyAlive(unit) ||
                    unit->r_UnitChimp!=eChimps.CHIMP_TYPE_QUARRY_OX || unit->r_ControllableForPlayerId!=owner ||
                    unit->r_LinkedProductionBuildingId!=tether || unit->r_LinkedProductionBuildingGlobalId!=oxTetherGlobal ||
                    unit->r_AIState!=state || state==1 && unit->r_CarryGoodAmount<=0 || state==3 && unit->r_CarryGoodAmount!=0)continue;
                var actor=new Actor{Id=id,Global=unit->r_GlobalId,Health=unit->r_CurrentHealth,Type=unit->r_UnitChimp,Tribe=unit->r_TribeId,
                    X=unit->r_CurrentTilePositionX,Y=unit->r_CurrentTilePositionY,SourceX=unit->r_CurrentTilePositionX,SourceY=unit->r_CurrentTilePositionY};
                var result=CaptureAutomaticOxTransit(actor,tether,state);
                if(result!=null)return result;
            }
            return null;
        }
        private unsafe OxTransitPlacement CaptureAutomaticOxTransit(Actor actor,int tether,int state)
        {
            var unit=Resolve(actor);
            if(!UnitAccess.IsReallyAlive(unit) || unit->r_UnitChimp!=eChimps.CHIMP_TYPE_QUARRY_OX ||
                unit->r_LinkedProductionBuildingId!=tether || unit->r_LinkedProductionBuildingGlobalId!=oxTetherGlobal ||
                unit->r_AIState!=state || state==1 && unit->r_CarryGoodAmount<=0 || state==3 && unit->r_CarryGoodAmount!=0)return null;
            var before=runtime.SnapshotUnit(actor.Id,true,false);
            if(before==null || before.Unsupported!=null || before.Job==null || (before.Flags!=2 && before.Flags!=4) ||
                Math.Max(Math.Abs(before.RequestedX-before.X),Math.Abs(before.RequestedY-before.Y))<3)return null;
            bool started=before.Substep<8;
            int anchorX=before.X,anchorY=before.Y;
            if(started)
            {
                if(!before.OrdinaryStep || !TryStartedMoatAnchor(before,out anchorX,out anchorY))return null;
            }
            else if(before.Substep!=8 || before.X!=before.NextX || before.Y!=before.NextY || !MoatWorkerSourceEligible(before.TileId))return null;
            var result=new OxTransitPlacement{Actor=actor,Before=before,Started=started,AnchorX=anchorX,AnchorY=anchorY};
            if(!AutomaticOxAccessesSafe(result,tether))return null;
            // This pure existing adapter closes actual active Workplace/Store
            // lifetimes. AI3 does not acquire an inactive stale Store binding.
            var world=new NativeWorkerResumeWorld(runtime.Native);
            return world.TryCapture(before,out _,out _)?result:null;
        }
        private static bool OxMoatContainsAccess(OxTransitPlacement placement,int x,int y)
        {
            if(placement.Started)return AppleStepContains(x,y,placement.AnchorX,placement.AnchorY,1);
            return Math.Abs((long)x-placement.AnchorX)<=1 && Math.Abs((long)y-placement.AnchorY)<=1;
        }
        private unsafe bool AutomaticOxAccessesSafe(OxTransitPlacement placement,int tetherId)
        {
            var unit=Resolve(placement.Actor);
            if(!BuildingAccess.TryGetById(tetherId,out GameBuilding* tether,out _) || tether==null ||
                tether->r_GlobalId!=oxTetherGlobal || tether->r_PlayerIdOwner!=owner ||
                tether->r_BuildingType!=eStructs.STRUCT_OXEN_BASE || tether->r_AliveState!=AliveState.IsAlive)return false;
            if(OxMoatContainsAccess(placement,tether->r_TileAccessPositionX,tether->r_TileAccessPositionY) ||
                OxMoatContainsAccess(placement,runtime.Native.Word(unit,0x992),runtime.Native.Word(unit,0x994)) ||
                OxMoatContainsAccess(placement,placement.Before.RequestedX,placement.Before.RequestedY))return false;
            int storeId=runtime.Native.Word(unit,0x98E);uint storeGlobal=runtime.Native.JobDword(unit,0x6F8);
            GameBuilding* store=null;
            bool liveStore=storeId>0 && BuildingAccess.TryGetById(storeId,out store,out _) && store!=null &&
                store->r_GlobalId==storeGlobal && store->r_PlayerIdOwner==owner && store->r_AliveState==AliveState.IsAlive;
            if(placement.Before.AIState==1 && !liveStore)return false;
            if(liveStore)
            {
                // Capture only the existing reference. It is a required lifetime
                // for outbound1 and an access-preservation guard for home3.
                if(!BuildingAccess.TryGetById(storeId,out store,out _) || store==null || store->r_GlobalId!=storeGlobal ||
                    OxMoatContainsAccess(placement,store->r_TileAccessPositionX,store->r_TileAccessPositionY))return false;
                if(placement.StoreId!=0 && (placement.StoreId!=storeId || placement.StoreGlobal!=storeGlobal))return false;
                placement.StoreId=storeId;placement.StoreGlobal=storeGlobal;
            }
            return true;
        }
        private unsafe AutomaticWorkerMutation ApplyAutomaticOxTransit(OxTransitPlacement placement)
        {
            Require(placement!=null && AutomaticOxAccessesSafe(placement,oxTether),"Ox moat envelope touches a future home/store/goal access");
            var before=placement.Before;var unit=Resolve(placement.Actor);
            var current=runtime.SnapshotUnit(placement.Actor.Id,true,false);
            Require(current!=null && current.Unsupported==null && current.Job!=null && current.GlobalId==before.GlobalId &&
                current.AIState==before.AIState && current.Flags==before.Flags && current.Substep==before.Substep &&
                current.X==before.X && current.Y==before.Y && current.NextX==before.NextX && current.NextY==before.NextY &&
                current.RequestedX==before.RequestedX && current.RequestedY==before.RequestedY,
                "Natural Ox accepted step/job goal changed before its actual moat writer");
            var world=new NativeWorkerResumeWorld(runtime.Native);
            Require(world.TryCapture(before,out _,out string reason),"Original Ox job/bindings changed before construction: "+reason);
            int cargo=unit->r_CarryGoodAmount;
            var result=placement.Started ? ApplyAutomaticStartedWorkerMoat(placement.Actor,before,placement.AnchorX,placement.AnchorY) :
                ApplyAutomaticWorkerMoat(placement.Actor);
            Require(AutomaticOxAccessesSafe(placement,oxTether) && WorkerCarry(placement.Actor)==cargo,"Ox moat construction changed original access or cargo");
            log("automatic-ox-transit-mutation ai="+before.AIState+" actualStartedStep="+placement.Started+" substep="+before.Substep+
                " current="+before.X+","+before.Y+" next="+before.NextX+","+before.NextY+" originalCargo="+cargo+
                " brushWidth="+(placement.Started?2:1)+" futureHomeStoreAndGoalAccessesPreserved=true");
            return result;
        }
    }
}
