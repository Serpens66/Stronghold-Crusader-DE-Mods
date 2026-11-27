using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using System.Linq;
using APIShared;
using APIShared.Automation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationWait> EntryGroup(AutomationRun run)
        {
            foreach(var wait in GroupCase(run,true))yield return wait;
            foreach(var wait in GroupCase(run,false))yield return wait;
        }

        private IEnumerable<AutomationWait> GroupCase(AutomationRun run,bool trapLeader)
        {
            int tribe=0;
            Execute(run,()=>{ FindSite();tribe=SpawnSharedSpearmanGroup(); });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,12);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Actor trapped=null;int leaderBefore=0;
            Execute(run,()=>{
                VerifyLiveAndRemember();
                leaderBefore=ValidateSharedGroup(tribe);
                trapped=trapLeader?actors.Single(a=>a.Id==leaderBefore):actors.First(a=>a.Id!=leaderBefore);
                var scope=BeginMutation("synthetic-shared-group-"+(trapLeader?"leader":"nonleader"),trapped.X,trapped.Y,1);
                try
                {
                    PaintSyntheticTrap(trapped.X,trapped.Y);
                    Require(!HasStructuralExit(trapped),"Selected shared-group member retained a structural exit");
                    foreach(var actor in actors.Where(a=>a.Id!=trapped.Id))
                        Require(HasStructuralExit(actor),"Synthetic group seam also trapped an intended free member");
                    var result=scope.Complete(false).Single(r=>r.UnitId==trapped.Id);
                    log("group-rescue trapRole="+(trapLeader?"leader":"nonleader")+" actualLeader="+leaderBefore+" "+result);
                    if(expectedEnabled)Require(result.Status==RescueStatus.Rescued && result.IdentityPreserved && !AtSource(trapped),"Shared-group member rescue failed");
                    else Require(AtSource(trapped),"Disabled rescue moved a shared-group member");
                }
                finally{scope.Dispose();}
                Require(ValidateSharedGroup(tribe)==leaderBefore,"Rescue changed the actual group leader");
                foreach(var actor in actors)CheckActor(actor,true);
            });
            int targetX=0,targetY=0;bool accepted=false;
            var goals=new List<GroupMemberGoal>();
            Execute(run,()=>{
                int actualLeader=ValidateSharedGroup(tribe);
                Require((trapped.Id==actualLeader)==trapLeader,"Selected trap role changed before group command");
                FindSharedGroupTarget(out targetX,out targetY);
                accepted=GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe,targetX,targetY);
                Require(ValidateSharedGroup(tribe)==actualLeader,"Group command changed leader; requested leader-role comparison is unavailable");
                foreach(var actor in actors)goals.Add(CaptureGroupMemberGoal(actor,targetX,targetY));
                log("group-command trapRole="+(trapLeader?"leader":"nonleader")+" tribe="+tribe+" actualLeader="+actualLeader+
                    " trapped="+trapped.Id+" commonTarget="+targetX+","+targetY+" accepted="+accepted+" members="+actors.Count+
                    " ownerKind=local-human nativeSharedTribe=true selectionUiTest=false autonomousAiTest=false");
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            bool allArrived=false;
            for(int sample=0;sample<160;sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission,8);
                allArrived=run.InSimulation(run.CurrentMission,()=>{
                    bool all=true;
                    foreach(var goal in goals){goal.Arrived=GroupMemberArrived(goal);all &= goal.Arrived;}
                    return all;
                });
                if(expectedEnabled && allArrived)break;
            }
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                ValidateSharedGroup(tribe);
                foreach(var goal in goals)
                {
                    CheckActor(goal.Actor,true);
                    log("group-member-result trapRole="+(trapLeader?"leader":"nonleader")+" enabled="+expectedEnabled+
                        " unit="+goal.Actor.Id+" trapped="+(goal.Actor.Id==trapped.Id)+" source="+goal.Actor.SourceX+","+goal.Actor.SourceY+
                        " final="+goal.Actor.X+","+goal.Actor.Y+" nativeRequested="+goal.X+","+goal.Y+
                        " nativeDestinationAssigned="+goal.Assigned+" arrived="+goal.Arrived+" identityHpTribeOccupancyPreserved=true");
                }
                int freeArrivals=goals.Count(g=>g.Actor.Id!=trapped.Id && g.Arrived);
                log("group-case-result trapRole="+(trapLeader?"leader":"nonleader")+" enabled="+expectedEnabled+
                    " accepted="+accepted+" allArrived="+allArrived+" freeMembersArrived="+freeArrivals+"/2");
                if(expectedEnabled)Require(accepted && goals.All(g=>g.Assigned) && allArrived,"Rescued shared tribe did not deliver every original member to its real native destination");
                else Require(AtSource(trapped),"Disabled baseline trapped group member escaped the synthetic wall");
                // Baseline free-member progress is an observation, not a presumed universal failure.
                RetireActors();
            });
        }

        private sealed class GroupMemberGoal
        {
            internal Actor Actor;internal int X,Y;internal bool Assigned,Arrived;
        }

        private unsafe int SpawnSharedSpearmanGroup()
        {
            Require(actors.Count==0,"Previous group actors were not retired");
            int tribe=checked((int)GameTribeManagerAPI.Instance.Create(owner));
            Require(tribe>0,"Shared fixture tribe was not created");
            for(int i=0;i<3;i++)
            {
                int x=cx+(i-1)*3,y=cy;
                int id=checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner,owner,x,y,elevation,eChimps.CHIMP_TYPE_SPEARMAN));
                GameUnit* unit=null;
                Require(id>0 && UnitAccess.TryGetById(id,out unit,out _) && unit!=null,"Shared-group actor spawn failed");
                Require(unit->r_UnitChimp==eChimps.CHIMP_TYPE_SPEARMAN && unit->r_CurrentTilePositionX==x && unit->r_CurrentTilePositionY==y,
                    "Shared-group actor was projected or has a different type");
                Require(GameTribeManagerAPI.Instance.AssignUnit(tribe,id) && unit->r_TribeId==tribe,"Native shared-tribe assignment was not observed");
                actors.Add(new Actor { Id=id,Global=unit->r_GlobalId,Type=eChimps.CHIMP_TYPE_SPEARMAN,Tribe=tribe,X=x,Y=y,SourceX=x,SourceY=y });
            }
            return tribe;
        }

        private int ValidateSharedGroup(int tribe)
        {
            var ids=new List<int>();
            Require(GameTribeManagerAPI.Instance.GetUnits(tribe,ids) && ids.Count==3 && new HashSet<int>(ids).SetEquals(actors.Select(a=>a.Id)),
                "Native shared tribe does not contain exactly the three original fixture members");
            foreach(var actor in actors)Require(actor.Tribe==tribe,"Fixture records do not share one tribe");
            int leader=GameTribeManagerAPI.Instance.GetLeaderUnitId(tribe);
            Require(ids.Contains(leader),"Actual native leader is not an original member");
            return leader;
        }

        private void FindSharedGroupTarget(out int x,out int y)
        {
            for(int d=0;d<8;d++)
            {
                int nx=cx+Dx[d]*8,ny=cy+Dy[d]*8;
                if(!FlatSquare(nx,ny,3,out int height) || height!=elevation)continue;
                if(actors.Any(a=>Math.Max(Math.Abs(nx-a.X),Math.Abs(ny-a.Y))<6))continue;
                x=nx;y=ny;return;
            }
            throw new InvalidOperationException("No free flat7x7 shared-group destination separated from every source");
        }

        private unsafe GroupMemberGoal CaptureGroupMemberGoal(Actor actor,int commonX,int commonY)
        {
            GameUnit* unit=Resolve(actor);
            int x=unit->r_TargetTilePositionX2,y=unit->r_TargetTilePositionY2;
            bool assigned=unit->r_AIState==101 && Math.Max(Math.Abs(x-commonX),Math.Abs(y-commonY))<=6 &&
                Math.Max(Math.Abs(x-unit->r_CurrentTilePositionX),Math.Abs(y-unit->r_CurrentTilePositionY))>=3;
            log("group-member-command unit="+actor.Id+" state="+unit->r_AIState+" flags="+unit->r_PathPlanStateBitFlags+
                " current="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+" nativeRequested="+x+","+y+" assigned="+assigned);
            return new GroupMemberGoal { Actor=actor,X=x,Y=y,Assigned=assigned };
        }

        private unsafe bool GroupMemberArrived(GroupMemberGoal goal)
        {
            CheckActor(goal.Actor,true);
            GameUnit* unit=Resolve(goal.Actor);
            return goal.Assigned && unit->r_PathPlanStateBitFlags==0 && unit->r_MovementSubstep==8 &&
                Math.Abs(unit->r_CurrentTilePositionX-goal.X)<=1 && Math.Abs(unit->r_CurrentTilePositionY-goal.Y)<=1;
        }
    }
}
