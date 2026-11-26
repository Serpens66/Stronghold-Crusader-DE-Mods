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
        private IEnumerable<AutomationStep> AutomaticGroupSteps()
        {
            yield return new AutomationStep("automatic-human-group-leader",TimeSpan.FromSeconds(180),run=>AutomaticGroup(run,false,true),true);
            yield return new AutomationStep("automatic-human-group-member",TimeSpan.FromSeconds(180),run=>AutomaticGroup(run,false,false),true);
            yield return new AutomationStep("automatic-ai-group-leader",TimeSpan.FromSeconds(180),run=>AutomaticGroup(run,true,true),true);
            yield return new AutomationStep("automatic-ai-group-member",TimeSpan.FromSeconds(180),run=>AutomaticGroup(run,true,false),true);
        }

        // Native group commands publish the intent before the real construction event.
        // No explicit mutation scope or direct coordinator call participates in this case.
        private IEnumerable<AutomationWait> AutomaticGroup(AutomationRun run,bool aiOwner,bool trapLeader)
        {
            int previousOwner=owner,tribe=0,leader=0;uint tribeGlobal=0;
            Actor trapped=null;var goals=new List<GroupMemberGoal>();var progressed=new HashSet<int>();
            long capturesBefore=0,attemptsBefore=0,rescuesBefore=0;bool allArrived=false;
            string role=trapLeader?"leader":"member",kind=aiOwner?"ai":"human";
            run.InSimulation(run.CurrentMission,()=>{
                Plugin.SetGeneralFixtureSession(false);
                if(aiOwner)SelectMountedAiOwner();
                Require(GamePlayerManagerAPI.Instance.IsAIPlayer(owner)==aiOwner,"Group fixture ownership does not match requested human/AI case");
                if(aiOwner)Require(owner==2,"Deterministic group fixture requires the actual AI in player slot2");
                FindSite();tribe=SpawnSharedSpearmanGroup();return true;
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,12);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            run.InSimulation(run.CurrentMission,()=>{
                VerifyLiveAndRemember();leader=ValidateSharedGroup(tribe);
                trapped=trapLeader?actors.Single(a=>a.Id==leader):actors.First(a=>a.Id!=leader);
                int targetX,targetY;FindSharedGroupTarget(out targetX,out targetY);
                bool accepted=GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe,targetX,targetY);
                Require(ValidateSharedGroup(tribe)==leader,"Ordinary group command changed the selected leader role");
                foreach(var actor in actors)
                {
                    var goal=CaptureGroupMemberGoal(actor,targetX,targetY);goals.Add(goal);
                    var before=runtime.SnapshotUnit(actor.Id,true,false);
                    log("automatic-group-precondition ownerKind="+kind+" role="+role+" accepted="+accepted+
                        " unit="+actor.Id+" flags="+before?.Flags+" substep="+before?.Substep+
                        " current="+before?.X+","+before?.Y+" next="+before?.NextX+","+before?.NextY+
                        " requested="+goal.X+","+goal.Y+" unsupported="+before?.Unsupported);
                    Require(accepted && goal.Assigned && before!=null && before.Unsupported==null && before.Command!=null &&
                        (before.Flags==2 || before.Flags==4) && runtime.Native.OrdinaryGround(before.TileId),
                        "Real group command did not publish a supported ordinary active intent for unit "+actor.Id+"; no fabricated AI/path state is substituted");
                    if(tribeGlobal==0)tribeGlobal=before.TribeGlobalId;
                    Require(tribeGlobal!=0 && before.TribeGlobalId==tribeGlobal,"Shared tribe lifetime was not coherent before construction");
                }
                Plugin.SetGeneralFixtureSession(expectedEnabled);
                var observer=Plugin.GeneralObserver;
                Require(observer!=null && observer.IsActive==expectedEnabled,"Automatic group activation differs from requested setting");
                capturesBefore=observer.CaptureCount;attemptsBefore=observer.AttemptCount;rescuesBefore=observer.RescueCount;
                // Direction0 has a blocked corner at the exact origin. Other members
                // lie on the top edge or outside; their actual exits are checked below.
                GameBuildingManagerAPI.Instance.CreatePrefab(owner,trapped.X,trapped.Y,eMappers.MAPPER_DRAWBRIDGE,
                    BuildingScales.GetScale(eMappers.MAPPER_DRAWBRIDGE),0,true,true);
                var tiles=GameTileManagerAPI.Instance;int bridge=tiles.GetTileBuildingId(tiles.GetTileId(trapped.X,trapped.Y));
                Require(bridge>0 && BridgeType(bridge),"Automatic group writer did not create a real drawbridge");
                Require(!HasStructuralExit(trapped),"Real drawbridge did not trap the selected group member's Current");
                foreach(var actor in actors)
                {
                    CheckActor(actor,true);Require(AtSource(actor),"Recovery or movement ran inside the paused construction wrapper");
                    if(actor.Id!=trapped.Id)Require(HasStructuralExit(actor),"Real drawbridge also trapped an intended free group member");
                }
                Require(ValidateSharedGroup(tribe)==leader,"Construction changed native group leader or membership");
                Require(observer.AttemptCount==attemptsBefore && observer.RescueCount==rescuesBefore,"Group recovery executed before a later simulation tick");
                if(expectedEnabled)Require(observer.CaptureCount>capturesBefore && observer.PendingCount>0,"Actual construction produced no automatic group candidate");
                else Require(observer.CaptureCount==capturesBefore && observer.PendingCount==0,"Disabled construction captured an automatic group candidate");
                log("automatic-group-before ownerKind="+kind+" owner="+owner+" role="+role+" enabled="+expectedEnabled+
                    " tribe="+tribe+"/"+tribeGlobal+" leader="+leader+" trapped="+trapped.Id+" building="+bridge+
                    " activeOriginalGoals=3 freeStructuralExits=2 directRescueCalls=0 originalUnsuppressed=true deferred=true");return true;
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            for(int sample=0;sample<160;sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission,8);
                allArrived=run.InSimulation(run.CurrentMission,()=>{
                    Require(ValidateSharedGroup(tribe)==leader,"Automatic recovery changed the actual group leader");
                    bool all=true;
                    foreach(var goal in goals)
                    {
                        goal.Arrived=GroupMemberArrived(goal);all &= goal.Arrived;
                        if(!AtSource(goal.Actor))progressed.Add(goal.Actor.Id);
                        var state=runtime.SnapshotUnit(goal.Actor.Id,true,false);
                        Require(state!=null && state.TribeGlobalId==tribeGlobal,"Automatic group continuation changed tribe lifetime");
                    }
                    return all;
                });
                if(allArrived)break;
            }
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            run.InSimulation(run.CurrentMission,()=>{
                var observer=Plugin.GeneralObserver;
                Require(ValidateSharedGroup(tribe)==leader,"Final group leader/membership differs from the original");
                foreach(var goal in goals)
                {
                    CheckActor(goal.Actor,true);
                    log("automatic-group-member ownerKind="+kind+" role="+role+" enabled="+expectedEnabled+
                        " unit="+goal.Actor.Id+"/"+goal.Actor.Global+" trapped="+(goal.Actor.Id==trapped.Id)+
                        " originalGoal="+goal.X+","+goal.Y+" final="+goal.Actor.X+","+goal.Actor.Y+
                        " progressed="+progressed.Contains(goal.Actor.Id)+" arrived="+goal.Arrived);
                }
                int freeProgress=goals.Count(g=>g.Actor.Id!=trapped.Id && progressed.Contains(g.Actor.Id));
                if(expectedEnabled)
                {
                    Require(observer.RescueCount==rescuesBefore+1,"Selected trapped group actor was not automatically rescued exactly once");
                    Require(freeProgress==2 && allArrived,"Automatic recovery did not preserve free-member progress and every original native goal");
                }
                else Require(observer.AttemptCount==attemptsBefore && observer.RescueCount==rescuesBefore,"Disabled automatic group fixture executed rescue");
                log("automatic-group-result ownerKind="+kind+" role="+role+" enabled="+expectedEnabled+
                    " allOriginalGoalsReached="+allArrived+" freeMembersProgressed="+freeProgress+"/2"+
                    " actualRescues="+(observer.RescueCount-rescuesBefore)+
                    " identity=true health=true leader=true tribeLifetime=true occupancy=true baselineEscapeNotAssumed=true");
                Plugin.SetGeneralFixtureSession(false);RetireActors();owner=previousOwner;return true;
            });
        }
    }
}
