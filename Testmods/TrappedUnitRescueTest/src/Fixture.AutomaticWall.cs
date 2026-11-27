using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Detours;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationStep> AutomaticWallSteps()
        { yield return new AutomationStep("automatic-ai-wall-accepted-next",TimeSpan.FromSeconds(180),AutomaticWall,true); }

        private IEnumerable<AutomationWait> AutomaticWall(AutomationRun run)
        {
            Actor actor=null;int goalX=0,goalY=0;
            UnitSnapshot acceptedStep=null;
            long captures=0,attempts=0,rescues=0;
            try
            {
                run.InSimulation(run.CurrentMission,()=>{
                    Plugin.SetGeneralFixtureSession(false);FindSite();SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,cx-1,cy);
                    actor=actors[0];goalX=cx+10;goalY=cy;return true;
                });
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,12);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Director.instance.SetEngineFrameRate(10);
                run.InSimulation(run.CurrentMission,()=>{
                    VerifyLiveAndRemember();
                    Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(actor.Tribe,goalX,goalY),"Wall actor rejected its original group command");
                    var before=runtime.SnapshotUnit(actor.Id);
                    Require(before!=null && before.Unsupported==null && before.Command!=null && before.Substep==8 &&
                        before.RequestedX==goalX && before.RequestedY==goalY,"Wall actor lacks its actual original ordinary command");
                    return true;
                });
                for(int sample=0;sample<12 && acceptedStep==null;sample++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    acceptedStep=run.InSimulation(run.CurrentMission,()=>CaptureAcceptedBridgeNext());
                }
                Require(acceptedStep!=null,"Wall fixture missed its natural first accepted step");
                run.InSimulation(run.CurrentMission,()=>{
                    var tiles=GameTileManagerAPI.Instance;
                    Require(CountInTileChain(acceptedStep.TileId,actor.Id)==1 && tiles.GetTileUnitId(acceptedStep.NextTileId)==0,
                        "Wall Next must be actually unoccupied while Current retains its original chain");
                    BeginAutomaticWorkerMoves(actor,goalX,goalY);NextResumeProbe.Arm(this,actor,goalX,goalY);
                    Plugin.SetGeneralFixtureSession(expectedEnabled);
                    var observer=Plugin.GeneralObserver;
                    Require(observer.IsActive==expectedEnabled,"Automatic wall activation differs from requested setting");
                    captures=observer.CaptureCount;attempts=observer.AttemptCount;rescues=observer.RescueCount;
                    // Fixes' earlier Pre sees the empty Next, so its existing
                    // Current protection remains untouched. The final-original
                    // capture discovers the accepted step via its Current halo.
                    BulkAIDetours.c_game_ai_build_wall_hook_impl(GameTileManagerAPI.Instance.GetTileManager(),owner,cx,cy,eMappers.MAPPER_WALL);
                    int tile=GameTileManagerAPI.Instance.GetTileId(cx,cy);
                    Require((GameTileManagerAPI.Instance.GetTilePropertyFlag(tile)&TilePropertyFlag.IsWall)!=0,"Actual AI-wall writer did not build the wall");
                    Require(observer.AttemptCount==attempts && observer.RescueCount==rescues,"Wall recovery ran inside the source wrapper");
                    if(expectedEnabled)
                    {
                        CheckActor(actor,true);
                        Require(AtSource(actor) && runtime.Native.OrdinaryGround(acceptedStep.TileId) &&
                            !runtime.Native.OrdinaryGround(acceptedStep.NextTileId),"Wall must invalidate only the accepted Next, retaining legal Current");
                        Require(observer.CaptureCount>captures && observer.PendingCount==1,"AI-wall writer did not publish exactly one automatic candidate");
                    }
                    else
                    {
                        CheckBaselineIdentity(actor);
                        Require(observer.CaptureCount==captures && observer.PendingCount==0 && automaticWorkerMoves.OwnBegins==0,"Disabled wall source captured or recovered a candidate");
                    }
                    log("automatic-wall-source enabled="+expectedEnabled+" originalUnsuppressed=true directRescue=false pending="+observer.PendingCount+
                        " ownCalls="+automaticWorkerMoves.OwnBegins+" currentOrdinary=true nextInvalid=true fixesCurrentProtectionRetained=true");return true;
                });
                Director.instance.SetEngineFrameRate(250);
                bool arrived=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?120:2);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,8);
                    arrived=run.InSimulation(run.CurrentMission,()=>{
                        Require(automaticWorkerMoves.Fault==null,automaticWorkerMoves.Fault);
                        return expectedEnabled && MovementOutcome(actor,goalX,goalY);
                    });
                    if(arrived)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    var observer=Plugin.GeneralObserver;var moves=automaticWorkerMoves;
                    if(expectedEnabled)
                    {
                        CheckActor(actor,true);
                        Require(arrived && observer.RescueCount==rescues+1 && moves.OwnBegins==1 && moves.OwnSuccesses==1 && moves.ProbeBegins==1 &&
                            NextResumeProbe.FirstNormalized && NextResumeProbe.FirstNative==1,
                            "Automatic Next-wall rescue did not normalize at original Current exactly once and complete its original command");
                    }
                    else Require(observer.RescueCount==rescues && observer.AttemptCount==attempts && moves.OwnBegins==0,"Disabled AI wall performed an own recovery");
                    log("automatic-wall-result enabled="+expectedEnabled+" actualAutomaticRescues="+(observer.RescueCount-rescues)+
                        " ownCalls="+moves.OwnBegins+" originalGoalArrived="+arrived+" noDoubleRescue=true");return true;
                });
            }
            finally
            {
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(automaticWorkerObserverFixture,this))automaticWorkerObserverFixture=null;
                NextResumeProbe.Release(this);Director.instance.SetEngineFrameRate(250);
                Plugin.SetGeneralFixtureSession(false);
            }
        }
    }
}
