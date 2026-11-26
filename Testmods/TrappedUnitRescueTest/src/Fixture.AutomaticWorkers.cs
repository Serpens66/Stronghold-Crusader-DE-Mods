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
                bool own=context.OriginOwnerGuid==Plugin.Guid;
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
        private void BeginAutomaticWorkerMoves(Actor actor,int x,int y)
        {
            if(!automaticWorkerObserverRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-automatic-worker-moves",new AutomaticWorkerMoveObserver(),out string reason,
                    enabled:()=>automaticWorkerObserverFixture!=null && !automaticWorkerObserverFixture.stopped &&
                        automaticWorkerObserverFixture.automaticWorkerMoves!=null && automaticWorkerObserverFixture.automaticWorkerMoves.Active),"Automatic worker native receipt unavailable: "+reason);
                automaticWorkerObserverRegistered=true;
            }
            automaticWorkerMoves=new AutomaticWorkerMoves{Actor=actor,Session=session,GoalX=x,GoalY=y,Active=true};
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
                Actor worker=null;
                for(int sample=0;sample<4000 && worker==null;sample++)
                {
                    if(sample==256)Director.instance.SetEngineFrameRate(50);
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,sample<256?16:1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Execute(run,()=>{worker=FindMoatWorker(farm);if(sample==0 || sample==255 || sample==1199 || sample==3999)LogAppleAcquisition(farm,sample);});
                }
                Require(worker!=null,"No natural Apple5/7 route reached the actual moat-eligible clear source; automatic coverage not obtained");
                Director.instance.SetEngineFrameRate(250);
                AutomaticWorkerMutation mutation=null;int dropsRequired=1;
                run.InSimulation(run.CurrentMission,()=>{
                    actors.Add(worker);var before=runtime.SnapshotUnit(worker.Id);
                    Require(before!=null && before.Unsupported==null,"Natural Apple source changed before automatic preparation");
                    dropsRequired=WorkerCarry(worker)>0?2:1;
                    BeginWorkerCycle(worker,farm,before.RequestedX,before.RequestedY);
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
            Actor ox=null;bool ready=false;
            for(int sample=0;sample<512 && ox==null && !ready;sample++)
            {
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,32);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{ValidateOxSites();ox=FindAutomaticLoadedOx(tether);OxProductionReadiness(out ready);if(sample==0 || sample==127 || sample==511)LogOxAcquisition(sample);});
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
                        Execute(run,()=>{ValidateOxSites();ox=FindAutomaticLoadedOx(tether);if(sample==0 || sample%512==0 || ox!=null)LogOxAcquisition(512+sample);});
                    }
                }
                finally{Director.instance.SetEngineFrameRate(250);}
            }
            Require(ox!=null,"No natural loaded Ox1 reached the clear moat-eligible source; no AI/cargo state was fabricated");
            AutomaticWorkerMutation mutation=null;
            run.InSimulation(run.CurrentMission,()=>{
                actors.Add(ox);VerifyLiveAndRemember();BeginOxObservation(ox,tether,OxCargo(ox));
                mutation=ApplyAutomaticWorkerMoat(ox);return true;
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
        private Actor FindAutomaticLoadedOx(int tether)
        {
            var actor=FindLoadedOx(tether);
            return actor!=null && MoatWorkerSourceEligible(runtime.Native.Tile(actor.X,actor.Y))?actor:null;
        }
    }
}
