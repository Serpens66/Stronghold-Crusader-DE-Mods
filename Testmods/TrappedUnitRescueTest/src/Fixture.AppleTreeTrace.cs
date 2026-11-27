using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.WorldMutations;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class AppleTraceFrame
        {
            internal int Tick,AI,Flags,Substep,X,Y,NextX,NextY,GoalX,GoalY,Counter,Selected,TreeX,TreeY,Resume,Timer;
            internal uint TreeGlobal,TreeStage,TreeProgress,OriginalStage,OriginalProgress;
            internal bool SelectedAlive;
            internal string Describe()=>"tick="+Tick+" ai="+AI+" flags="+Flags+" ss="+Substep+
                " current="+X+","+Y+" next="+NextX+","+NextY+" goal="+GoalX+","+GoalY+
                " counter="+Counter+" selected="+Selected+"/"+TreeGlobal+" selectedAlive="+SelectedAlive+
                " selectedXY="+TreeX+","+TreeY+" selectedGrowth="+TreeStage+"/"+TreeProgress+
                " originalGrowth="+OriginalStage+"/"+OriginalProgress+" resume9CC="+Resume+" timer920="+Timer;
            // Inspect every native tick; log semantic/whole-tile transitions rather
            // than eight near-identical movement substeps per traversed tile.
            internal bool ChangedFrom(AppleTraceFrame p)=>p==null || AI!=p.AI || Flags!=p.Flags ||
                X!=p.X || Y!=p.Y || NextX!=p.NextX || NextY!=p.NextY || GoalX!=p.GoalX || GoalY!=p.GoalY ||
                Counter!=p.Counter || Selected!=p.Selected || TreeGlobal!=p.TreeGlobal || SelectedAlive!=p.SelectedAlive ||
                TreeStage!=p.TreeStage || OriginalStage!=p.OriginalStage || Resume!=p.Resume;
        }
        private sealed class AppleNativeTrace
        {
            internal AppleTreeTransit Transit;
            internal AppleTraceFrame Last;
            internal long Session,World;
            internal bool Active,Control,FirstPick,OriginalApproachReached,ReifeRejectionObserved;
            internal int Lines,FirstPickedTree;internal uint FirstPickedGlobal;
            internal string Fault;
        }
        private AppleNativeTrace appleNativeTrace;
        private static volatile Fixture appleTraceFixture;
        private static bool appleTraceRegistered;
        private const int AppleTraceMaximumLines=512;

        private void ArmAppleNativeTrace(AppleTreeTransit transit,bool control)
        {
            if(!appleTraceRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-apple-native-tree-trace",
                    ObserveAppleNativeTrace,out string reason,order:90),reason);appleTraceRegistered=true;
            }
            var proof=new AppleNativeTrace{Transit=transit,Control=control,Active=true};
            Require(WorldMutationEvents.TryGetCurrentWorld(out proof.Session,out proof.World) && proof.Session==session,
                "Apple trace has no exact original native world");
            Require(SimulationTickEvents.TryGetCurrentTick(out int tick),"Apple trace has no original native tick");
            proof.Last=ReadAppleTraceFrame(proof,tick);appleNativeTrace=proof;appleTraceFixture=this;
            log("apple-native-trace-start controlNoMutation="+control+" originalTree="+transit.TreeId+"/"+transit.TreeGlobal+
                " originalGoal="+transit.Step.Before.RequestedX+","+transit.Step.Before.RequestedY+" "+proof.Last.Describe());
        }
        private static void ObserveAppleNativeTrace(int tick)
        {
            var f=appleTraceFixture;var p=f?.appleNativeTrace;
            if(f==null || f.stopped || p==null || !p.Active || p.Fault!=null || p.FirstPick)return;
            try{f.ReadAppleNativeTraceTick(p,tick);}
            catch(Exception error){p.Fault=error.ToString();}
        }
        private unsafe AppleTraceFrame ReadAppleTraceFrame(AppleNativeTrace proof,int tick)
        {
            Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==proof.Session && world==proof.World,
                "Apple diagnostic trace crossed its original world");
            var transit=proof.Transit;var actor=transit.Step.Actor;var unit=Resolve(actor);
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth==actor.Health && unit->r_TribeId==actor.Tribe,
                "Apple diagnostic actor health/tribe lifetime changed");
            Require(runtime.Native.TryTribeGlobal(actor.Tribe,out uint tribeGlobal) && tribeGlobal==transit.Step.Before.TribeGlobalId,
                "Apple diagnostic tribe lifetime changed");
            Require(actor.Tribe==0 || runtime.Native.JobDword(unit,0x93C)==tribeGlobal,
                "Apple diagnostic actor cached tribe stamp changed");
            Require(NativeVegetationAccess.TryGet(transit.TreeId,out var original) && original!=null &&
                original->r_GlobalId==transit.TreeGlobal && original->r_AliveState==AliveState.IsAlive &&
                original->r_VegetationType==VegetationType.AppleTree && original->r_TilePositionX==transit.TreeX &&
                original->r_TilePositionY==transit.TreeY && original->r_TileId==(uint)runtime.Native.Tile(transit.TreeX,transit.TreeY) &&
                GameTileManagerAPI.Instance.GetTileVegetationId((int)original->r_TileId)==transit.TreeId,
                "Apple diagnostic original tree died, moved or changed lifetime");
            var frame=new AppleTraceFrame{Tick=tick,AI=unit->r_AIState,Flags=unit->r_PathPlanStateBitFlags,
                Substep=unit->r_MovementSubstep,X=unit->r_CurrentTilePositionX,Y=unit->r_CurrentTilePositionY,
                NextX=unit->r_NextTilePositionX2,NextY=unit->r_NextTilePositionY2,
                GoalX=unit->r_TargetTilePositionX2,GoalY=unit->r_TargetTilePositionY2,
                Counter=runtime.Native.Word(unit,0x9EC),Selected=unit->r_TargetVegetationId,
                OriginalStage=original->r_GrowthStage,OriginalProgress=original->r_GrowthProgress,
                Resume=runtime.Native.Word(unit,0x9CC),Timer=runtime.Native.Word(unit,0x920)};
            if(NativeVegetationAccess.TryGet(frame.Selected,out var selected) && selected!=null)
            {
                frame.TreeGlobal=selected->r_GlobalId;frame.TreeX=selected->r_TilePositionX;frame.TreeY=selected->r_TilePositionY;
                frame.TreeStage=selected->r_GrowthStage;frame.TreeProgress=selected->r_GrowthProgress;
                frame.SelectedAlive=selected->r_AliveState==AliveState.IsAlive && selected->r_VegetationType==VegetationType.AppleTree;
            }
            return frame;
        }
        private void ReadAppleNativeTraceTick(AppleNativeTrace proof,int tick)
        {
            var current=ReadAppleTraceFrame(proof,tick);var previous=proof.Last;
            if(previous!=null)Require(unchecked((uint)(tick-previous.Tick))<=1,
                "Apple diagnostic lost an actual native tick between successive frames");
            var t=proof.Transit;var before=t.Step.Before;
            if(current.X==before.RequestedX && current.Y==before.RequestedY && current.Flags==0)
                proof.OriginalApproachReached=true;
            if(previous!=null && (previous.AI==2 && current.AI==5 || previous.AI==8 && current.AI==4) &&
                current.Selected==t.TreeId && current.TreeGlobal==t.TreeGlobal && current.OriginalStage!=3 &&
                current.Resume==2 && current.X==before.RequestedX && current.Y==before.RequestedY)
                proof.ReifeRejectionObserved=true;
            if(current.ChangedFrom(previous))
            {
                Require(proof.Lines++<AppleTraceMaximumLines,"Apple diagnostic transition log budget exhausted; no complete trace claimed");
                log("apple-native-trace-change controlNoMutation="+proof.Control+
                    " previous={"+(previous?.Describe()??"none")+"} current={"+current.Describe()+"}"+
                    " originalApproachReached="+proof.OriginalApproachReached+" nativeReifeRejectObserved="+proof.ReifeRejectionObserved);
            }
            // AI0 may legitimately reset the completed/abandoned picking cycle
            // before selecting another tree. Preserve that trace; an increment,
            // rather than any difference from the captured counter, proves a pick.
            if(previous!=null && current.Counter>previous.Counter && !proof.FirstPick)
            {
                Require(previous.AI==3 && current.Counter==previous.Counter+1 && previous.SelectedAlive &&
                    Math.Abs(previous.X-previous.TreeX)<=2 && Math.Abs(previous.Y-previous.TreeY)<=2 &&
                    Math.Abs(current.X-previous.TreeX)<=2 && Math.Abs(current.Y-previous.TreeY)<=2,
                    "Diagnostic first picking increment lacks a genuine previous physical selected-tree contact");
                proof.FirstPick=true;proof.FirstPickedTree=previous.Selected;proof.FirstPickedGlobal=previous.TreeGlobal;
                log("apple-native-trace-first-pick controlNoMutation="+proof.Control+" actualPickedTree="+previous.Selected+"/"+previous.TreeGlobal+
                    " originallyCapturedTree="+t.TreeId+"/"+t.TreeGlobal+" physicalContact=true"+
                    " originalApproachReached="+proof.OriginalApproachReached+" nativeReifeRejectObserved="+proof.ReifeRejectionObserved);
            }
            proof.Last=current;
        }
        private void StopAppleNativeTrace()
        {
            if(appleNativeTrace!=null)appleNativeTrace.Active=false;
            if(ReferenceEquals(appleTraceFixture,this))appleTraceFixture=null;
        }
        private IEnumerable<AutomationStep> AppleTreeControlSteps()
        {yield return new AutomationStep("natural-outer-apple-tree-no-mutation-control",TimeSpan.FromSeconds(900),AppleTreeControl,true);}
        private IEnumerable<AutomationWait> AppleTreeControl(AutomationRun run)
        {
            try
            {
                using(var world=ObserveAppleWorld())
                {
                    int farm=0,state=scenario.Contains("-appletree8-")?8:2;AppleTreeTransit selected=null;
                    run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
                    foreach(var wait in SetupNaturalOuterAppleWorld(run))yield return wait;
                    farm=appleWorld.Farm;
                    Director.instance.SetEngineFrameRate(50);
                    for(int sample=0;sample<5000 && selected==null;sample++)
                    {
                        foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                        yield return run.WaitForTicks(run.CurrentMission,1+(sample&1));
                        foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                        run.InSimulation(run.CurrentMission,()=>{
                            selected=FindNaturalAppleTreeTransit(farm,state);
                            if(sample%128==0 || sample==4999){LogAppleAcquisition(farm,sample);LogAppleTreeRejectSummary(sample);}return true;
                        });
                    }
                    Require(selected!=null,"Control missed its genuine originally selected external-tree transit");
                    run.InSimulation(run.CurrentMission,()=>{
                        actors.Add(selected.Step.Actor);ArmAppleNativeTrace(selected,true);
                        BeginWorkerCycle(selected.Step.Actor,farm,selected.Step.Before.RequestedX,selected.Step.Before.RequestedY);
                        BeginAutomaticWorkerMoves(selected.Step.Actor,selected.Step.Before.RequestedX,selected.Step.Before.RequestedY);
                        Plugin.SetGeneralFixtureSession(true);
                        log("apple-control-original-source state="+state+" noMutation=true originalTree="+selected.TreeId+"/"+selected.TreeGlobal+
                            " source="+selected.Step.Before.X+","+selected.Step.Before.Y+" next="+selected.Step.Before.NextX+","+selected.Step.Before.NextY);
                        return true;
                    });
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    for(int sample=0;sample<2400 && !appleNativeTrace.FirstPick;sample++)
                    {
                        yield return run.WaitForTicks(run.CurrentMission,2);
                        run.InSimulation(run.CurrentMission,()=>{
                            Require(appleNativeTrace.Fault==null && workerCycle.Fault==null && automaticWorkerMoves.Fault==null,
                                appleNativeTrace.Fault??workerCycle.Fault??automaticWorkerMoves.Fault);return true;
                        });
                    }
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{
                        var trace=appleNativeTrace;CheckActor(selected.Step.Actor,true);
                        Require(trace.Fault==null && trace.FirstPick && trace.OriginalApproachReached &&
                            workerCycle.Fault==null && workerCycle.RemotePicking==0 && automaticWorkerMoves.OwnBegins==0,
                            "No-mutation control lacks complete physical/tick/lifetime evidence or made own recovery calls");
                        log("apple-control-result state="+state+" originalTree="+selected.TreeId+"/"+selected.TreeGlobal+
                            " firstPickedTree="+trace.FirstPickedTree+"/"+trace.FirstPickedGlobal+
                            " originalApproachReached="+trace.OriginalApproachReached+
                            " nativeReifeRejectObserved="+trace.ReifeRejectionObserved+" ownRecoveryCalls=0"+
                            " noFixtureMoatOrWallMutation=true rescueAcceptanceClaim=false");
                        Plugin.SetGeneralFixtureSession(false);return true;
                    });
                }
            }
            finally
            {
                StopAppleNativeTrace();if(workerCycle!=null)workerCycle.Active=false;
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;
                Director.instance.SetEngineFrameRate(250);
            }
        }
    }
}
