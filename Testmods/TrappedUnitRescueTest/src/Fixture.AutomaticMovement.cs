using System;
using System.Collections.Generic;
using System.Linq;
using APIShared.Automation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationStep> AutomaticBridgeSteps()
        {
            yield return new AutomationStep("automatic-building-spearmen",TimeSpan.FromSeconds(180),
                run=>AutomaticBridge(run,eChimps.CHIMP_TYPE_SPEARMAN),true);
            yield return new AutomationStep("automatic-building-assassins",TimeSpan.FromSeconds(180),
                run=>AutomaticBridge(run,eChimps.CHIMP_TYPE_ARAB_ASSASIN),true);
        }
        // This exercises the real registered writer and later public simulation ticks.
        // It deliberately has no BeginMutation, direct Tick or Fixes-suppression scope.
        private IEnumerable<AutomationWait> AutomaticBridge(AutomationRun run,eChimps type)
        {
            run.InSimulation(run.CurrentMission,()=>{
                Plugin.SetGeneralFixtureSession(false);FindSite();SpawnMatrix(type);return true;
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,16);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            List<int> trapped=null;
            long attemptsBefore=0,rescuesBefore=0,capturesBefore=0;
            run.InSimulation(run.CurrentMission,()=>{
                VerifyLiveAndRemember();Plugin.SetGeneralFixtureSession(expectedEnabled);
                var observer=Plugin.GeneralObserver;
                Require(observer!=null && observer.IsActive==expectedEnabled,"Automatic source did not match requested activation");
                attemptsBefore=observer.AttemptCount;rescuesBefore=observer.RescueCount;capturesBefore=observer.CaptureCount;
                GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,eMappers.MAPPER_DRAWBRIDGE,
                    BuildingScales.GetScale(eMappers.MAPPER_DRAWBRIDGE),0,true,true);
                var tiles=GameTileManagerAPI.Instance;int bridge=tiles.GetTileBuildingId(tiles.GetTileId(cx,cy));
                Require(bridge>0 && BridgeType(bridge),"Automatic bridge writer did not create the required building");
                int occupied=0;
                for(int y=cy;y<cy+5;y++)for(int x=cx;x<cx+5;x++)
                    if(tiles.GetTileBuildingId(tiles.GetTileId(x,y))==bridge)occupied++;
                Require(occupied==25,"Automatic bridge footprint changed: "+occupied);
                trapped=actors.Where(a=>!HasStructuralExit(a)).Select(a=>a.Id).ToList();
                Require(trapped.Count>0,"Automatic writer did not establish a real trap");
                foreach(var actor in actors){CheckActor(actor,true);Require(AtSource(actor),"Recovery ran inside the original building wrapper");}
                Require(observer.AttemptCount==attemptsBefore && observer.RescueCount==rescuesBefore,
                    "Automatic source performed an early recovery");
                if(expectedEnabled)Require(observer.CaptureCount>capturesBefore && observer.PendingCount>0,
                    "Actual construction did not publish an automatic pending candidate");
                else Require(observer.CaptureCount==capturesBefore && observer.PendingCount==0,
                    "Disabled automatic source captured construction");
                log("automatic-bridge-before type="+type+" enabled="+expectedEnabled+" source=CreatePrefab"+
                    " owner="+owner+" building="+bridge+" footprint="+occupied+" trapped="+trapped.Count+
                    " captures="+(observer.CaptureCount-capturesBefore)+" pending="+observer.PendingCount+
                    " directRescueCalls=0 originalUnsuppressed=true deferred=true");return true;
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,8);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            run.InSimulation(run.CurrentMission,()=>{
                foreach(var actor in actors)
                {
                    CheckActor(actor,true);if(!trapped.Contains(actor.Id))continue;
                    if(expectedEnabled)Require(!AtSource(actor) && HasStructuralExit(actor),
                        "Automatic tick did not rescue trapped actor "+actor.Id);
                    else Require(AtSource(actor) && !HasStructuralExit(actor),
                        "Off control did not preserve the actual native trap "+actor.Id);
                }
                var observer=Plugin.GeneralObserver;
                if(expectedEnabled)Require(observer.RescueCount==rescuesBefore+trapped.Count,
                    "Automatic recovery count does not match actual trapped actors");
                else Require(observer.AttemptCount==attemptsBefore && observer.RescueCount==rescuesBefore,
                    "Disabled source performed automatic recovery");
                return true;
            });
            var sample=actors.Single(a=>a.Id==trapped[0]);int targetX=0,targetY=0;
            if(expectedEnabled)
            {
                run.InSimulation(run.CurrentMission,()=>{
                    FindMovementTarget(sample,out targetX,out targetY);
                    Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(sample.Tribe,targetX,targetY),
                        "Automatically rescued actor rejected subsequent movement");return true;
                });
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                bool arrived=false;
                for(int i=0;i<80 && !arrived;i++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,16);
                    arrived=run.InSimulation(run.CurrentMission,()=>MovementOutcome(sample,targetX,targetY));
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Require(arrived,"Automatically rescued actor did not physically reach subsequent target");
            }
            run.InSimulation(run.CurrentMission,()=>{
                var observer=Plugin.GeneralObserver;
                Require(observer.RescueCount==rescuesBefore+(expectedEnabled?trapped.Count:0),
                    "Coordinator counted the same automatic bridge actors more than once");
                foreach(var actor in actors)CheckActor(actor,true);
                log("automatic-bridge-result type="+type+" enabled="+expectedEnabled+" actors="+actors.Count+
                    " trapped="+trapped.Count+" actualAutomaticRescues="+(observer.RescueCount-rescuesBefore)+
                    " identity=true health=true tribe=true occupancy=true endpoint="+expectedEnabled+
                    " activeIntentContinuationCovered=false");
                Plugin.SetGeneralFixtureSession(false);RetireActors();return true;
            });
        }
    }
}
