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
        private IEnumerable<AutomationWait> Worker(AutomationRun run)
        {
            int farm=0;
            Execute(run,()=>{
                FindSite();
                GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,eMappers.MAPPER_APPLEFARM,10,0,true,true);
                farm=GameTileManagerAPI.Instance.GetTileBuildingId(GameTileManagerAPI.Instance.GetTileId(cx,cy));
                Require(farm>0,"Native apple farm did not spawn");
                log("worker-fixture naturalFarm="+farm+" input=native-farm syntheticPart=occupied-source-wall-only");
            });
            Actor worker=null;
            // Observe a real employee's own travel state, never inject an AI state or worker cargo.
            for(int attempt=0;attempt<160 && worker==null;attempt++)
            {
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,attempt<20?16:1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>worker=FindWorkingAppleFarmer(farm));
            }
            Require(worker!=null,"No actual apple employee acquired an ordinary active travel segment; job coverage unavailable");
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
                }
                finally{scope.Dispose();}
            });
            bool reached=false;
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            for(int sample=0;sample<(expectedEnabled?120:8);sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission,4);
                reached |= run.InSimulation(run.CurrentMission,()=>WorkerAtRecordedTarget(worker,farm,requestedX,requestedY));
                if(expectedEnabled && reached)break;
            }
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                CheckActor(worker,true);
                if(expectedEnabled)Require(reached,"Naturally working farmer did not physically reach the recorded segment target after rescue");
                else Require(AtSource(worker),"Baseline worker escaped the controlled trap");
                log("worker-result enabled="+expectedEnabled+" naturalEmployee=true nativeActiveRoute=true syntheticEntrapment=true "+
                    "sameIdentity=true sameWorkplace=true cargoBefore="+carryBefore+" cargoAfter="+WorkerCarry(worker)+
                    " originalSegmentReached="+reached+" fullHarvestDeliveryCycleVerified=false");
            });
            // The entire disposable mission is closed by the automation runner; no user save is modified.
        }
        private unsafe Actor FindWorkingAppleFarmer(int farm)
        {
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
