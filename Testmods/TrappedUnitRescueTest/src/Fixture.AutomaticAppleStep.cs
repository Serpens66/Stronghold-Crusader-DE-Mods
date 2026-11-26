using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class AppleStepPlacement
        {
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal int X,Y,FarmX,FarmY,GranaryX,GranaryY;
        }
        private IEnumerable<AutomationStep> AutomaticAppleStepSteps()
        {yield return new AutomationStep("automatic-natural-started-apple-moat-and-cycle",TimeSpan.FromSeconds(900),AutomaticAppleStep,true);}

        private static bool AppleStepContains(int x,int y,int left,int top,int halo)=>
            x>=left-1-halo && x<=left+halo && y>=top-1-halo && y<=top+halo;

        private unsafe AppleStepPlacement FindNaturalAppleStep(int farmId,int state)
        {
            ValidateAppleWorld();GameBuilding* farm=null;GameBuilding* granary=null;
            Require(BuildingAccess.TryGetById(farmId,out farm,out _) && farm!=null &&
                BuildingAccess.TryGetById(appleWorld.Granary,out granary,out _) && granary!=null,
                "Natural apple started moat workplace/storage unavailable");
            int id=farm->r_WorkerId_1;
            if(id==0 || !UnitAccess.TryGetById(id,out GameUnit* unit,out _) || unit==null || !UnitAccess.IsReallyAlive(unit) ||
                unit->r_UnitChimp!=eChimps.CHIMP_TYPE_FARMER_APPLE || unit->r_ControllableForPlayerId!=owner ||
                unit->r_LinkedProductionBuildingId!=farmId || unit->r_LinkedProductionBuildingGlobalId!=appleWorld.FarmGlobal ||
                unit->r_AIState!=state || (state==7)!=(unit->r_CarryGoodAmount>0))return null;
            var before=runtime.SnapshotUnit(id,true,false);
            if(before==null || before.Unsupported!=null || before.Job==null || !before.OrdinaryStep || before.Substep>=8 ||
                (before.Flags!=2 && before.Flags!=4) || !runtime.Native.OrdinaryGround(before.TileId))return null;
            if(!TryStartedMoatAnchor(before,out int anchorX,out int anchorY))return null;
            if(AppleStepContains(farm->r_TileAccessPositionX,farm->r_TileAccessPositionY,anchorX,anchorY,1) ||
                AppleStepContains(granary->r_TileAccessPositionX,granary->r_TileAccessPositionY,anchorX,anchorY,1) ||
                AppleStepContains(before.RequestedX,before.RequestedY,anchorX,anchorY,1))return null;
            return new AppleStepPlacement { Before=before,X=anchorX,Y=anchorY,
                FarmX=farm->r_TileAccessPositionX,FarmY=farm->r_TileAccessPositionY,
                GranaryX=granary->r_TileAccessPositionX,GranaryY=granary->r_TileAccessPositionY,
                Actor=new Actor{Id=id,Global=unit->r_GlobalId,Health=unit->r_CurrentHealth,Type=unit->r_UnitChimp,
                    Tribe=unit->r_TribeId,X=before.X,Y=before.Y,SourceX=before.X,SourceY=before.Y} };
        }

        // IsMoat=40000000 selects69850.619C0 clears169x24 bytes per size,
        // fills four XY records for size2, and leaves ordinal4 at(0,0).
        //6FE90 iterates count5 and subtracts width/2=1: four unique tiles
        // [centerX-1..centerX] x [centerY-1..centerY], including one duplicate.
        private bool TryStartedMoatAnchor(UnitSnapshot before,out int x,out int y)
        {
            x=0;y=0;
            if(before==null || before.Unsupported!=null || before.Job==null || !before.OrdinaryStep ||
                before.Substep<0 || before.Substep>=8 || (before.Flags!=2 && before.Flags!=4) ||
                Math.Max(Math.Abs(before.NextX-before.X),Math.Abs(before.NextY-before.Y))!=1)return false;
            int centerX=Math.Max(before.X,before.NextX),centerY=Math.Max(before.Y,before.NextY);
            var tiles=GameTileManagerAPI.Instance;
            for(int yy=centerY-1;yy<=centerY;yy++)for(int xx=centerX-1;xx<=centerX;xx++)
            {
                if(!tiles.IsTileInsideMapBounds(xx,yy))return false;
                int tile=tiles.GetTileId(xx,yy),occupant=tiles.GetTileUnitId(tile);
                if(!MoatWorkerSourceEligible(tile) || occupant!=0 && occupant!=before.Id)return false;
            }
            x=centerX;y=centerY;return true;
        }

        private unsafe AutomaticWorkerMutation ApplyAutomaticStartedWorkerMoat(Actor actor,UnitSnapshot before,int anchorX,int anchorY)
        {
            Require(actor!=null && before!=null && actor.Id==before.Id && actor.Global==before.GlobalId &&
                TryStartedMoatAnchor(before,out int actualX,out int actualY) && actualX==anchorX && actualY==anchorY,
                "Started worker has no audited four-tile moat brush envelope");
            var current=runtime.SnapshotUnit(actor.Id,true,false);
            Require(current!=null && current.GlobalId==before.GlobalId && current.Unsupported==null && current.Job!=null &&
                current.OrdinaryStep && current.Substep==before.Substep && current.Flags==before.Flags &&
                current.X==before.X && current.Y==before.Y && current.NextX==before.NextX && current.NextY==before.NextY &&
                current.RequestedX==before.RequestedX && current.RequestedY==before.RequestedY,
                "Started worker source changed before actual native moat writer");
            Require(before.RequestedX!=before.X || before.RequestedY!=before.Y,"Started worker lacks a distinct original job goal");
            actor.Health=before.Health;actor.X=actor.SourceX=before.X;actor.Y=actor.SourceY=before.Y;
            BeginAutomaticWorkerMoves(actor,before.RequestedX,before.RequestedY);
            Plugin.SetGeneralFixtureSession(expectedEnabled);var observer=Plugin.GeneralObserver;
            Require(observer!=null && observer.IsActive==expectedEnabled,"Started moat session differs from requested activation");
            var mutation=new AutomaticWorkerMutation{Actor=actor,GoalX=before.RequestedX,GoalY=before.RequestedY,Cargo=WorkerCarry(actor),
                Captures=observer.CaptureCount,Attempts=observer.AttemptCount,Rescues=observer.RescueCount};
            var tiles=GameTileManagerAPI.Instance;
            EditorBrush.Call(tiles.GetTileManager(),owner,tiles.GetTileId(anchorX,anchorY),anchorY,2,
                unchecked((int)(uint)TilePropertyFlag.IsMoat),2,0);
            Require((tiles.GetTilePropertyFlag(before.TileId)&TilePropertyFlag.IsMoat)!=0 &&
                (tiles.GetTilePropertyFlag(before.NextTileId)&TilePropertyFlag.IsMoat)!=0,
                "Actual width2 moat brush did not invalidate both captured physical endpoints");
            for(int yy=anchorY-1;yy<=anchorY;yy++)for(int xx=anchorX-1;xx<=anchorX;xx++)
                Require((tiles.GetTilePropertyFlag(tiles.GetTileId(xx,yy))&TilePropertyFlag.IsMoat)!=0,
                    "Actual centered moat brush did not create its entire audited2x2 footprint");
            CheckActor(actor,true);
            Require(!HasStructuralExit(actor) && AtSource(actor) && WorkerCarry(actor)==mutation.Cargo &&
                Resolve(actor)->r_TargetTilePositionX2==mutation.GoalX && Resolve(actor)->r_TargetTilePositionY2==mutation.GoalY,
                "Completed moat did not preserve original source/job/cargo in a real trap");
            Require(observer.AttemptCount==mutation.Attempts && observer.RescueCount==mutation.Rescues,
                "Started moat rescue executed inside original construction");
            if(expectedEnabled)Require(observer.CaptureCount>mutation.Captures && observer.PendingCount>0,"Real started moat source produced no deferred candidate");
            else Require(observer.CaptureCount==mutation.Captures && observer.PendingCount==0,"Disabled started moat source captured a candidate");
            log("automatic-started-worker-moat-source type="+actor.Type+" enabled="+expectedEnabled+" unit="+actor.Id+"/"+actor.Global+
                " ai="+before.AIState+" substep="+before.Substep+" current="+before.X+","+before.Y+
                " next="+before.NextX+","+before.NextY+" originalGoal="+mutation.GoalX+","+mutation.GoalY+
                " brushWidth=2 brushGeometry=2x2-centered-square-five-ordinals bothEndpointsMoat=true cargo="+mutation.Cargo+
                " originalUnsuppressed=true directRescue=false deferred=true");
            return mutation;
        }

        private static bool AppleStepAccessHasExit(int x,int y)
        {
            var tiles=GameTileManagerAPI.Instance;var paths=GamePathingManagerAPI.Instance;
            for(int d=0;d<8;d++)
            {
                int nx=x+Dx[d],ny=y+Dy[d];
                if(tiles.IsTileInsideMapBounds(nx,ny) &&
                    (paths.HasPathEdge(x,y,(PackedPathDirection)d) || paths.HasPathEdge(nx,ny,(PackedPathDirection)(d^4))))return true;
            }
            return false;
        }
        private unsafe void ValidateAppleStepAccess(AppleStepPlacement placement)
        {
            ValidateAppleWorld();
            Require(BuildingAccess.TryGetById(appleWorld.Farm,out GameBuilding* farm,out _) && farm!=null &&
                BuildingAccess.TryGetById(appleWorld.Granary,out GameBuilding* granary,out _) && granary!=null &&
                farm->r_TileAccessPositionX==placement.FarmX && farm->r_TileAccessPositionY==placement.FarmY &&
                granary->r_TileAccessPositionX==placement.GranaryX && granary->r_TileAccessPositionY==placement.GranaryY,
                "Moat construction changed the original farm/granary access identity");
            Require(AppleStepAccessHasExit(placement.FarmX,placement.FarmY) &&
                AppleStepAccessHasExit(placement.GranaryX,placement.GranaryY),"Moat construction blocked a future farm/granary access");
        }
        private AutomaticWorkerMutation ApplyNaturalAppleStep(AppleStepPlacement placement,int farm)
        {
            var actor=placement.Actor;ValidateAppleStepAccess(placement);actors.Add(actor);
            BeginWorkerCycle(actor,farm,placement.Before.RequestedX,placement.Before.RequestedY);
            var mutation=ApplyAutomaticStartedWorkerMoat(actor,placement.Before,placement.X,placement.Y);
            ValidateAppleStepAccess(placement);return mutation;
        }
        private IEnumerable<AutomationWait> AutomaticAppleStep(AutomationRun run)
        {
            using(var proof=ObserveAppleWorld())
            {
                int farm=0;int state=scenario.Contains("-cargo-")?7:5;
                run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
                Execute(run,()=>farm=SetupNaturalAppleFarm());
                AppleStepPlacement placement=null;
                for(int sample=0;sample<4000 && placement==null;sample++)
                {
                    if(sample==256)Director.instance.SetEngineFrameRate(50);
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,sample<256?16:1+(sample&1));
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    run.InSimulation(run.CurrentMission,()=>{
                        placement=FindNaturalAppleStep(farm,state);
                        if(sample%128==0 || sample==3999)LogAppleAcquisition(farm,sample);return true;
                    });
                }
                Require(placement!=null,"No natural started Apple route had a clear four-tile moat footprint preserving both future job accesses");
                Director.instance.SetEngineFrameRate(250);
                var worker=placement.Actor;int firstCargoDrops=-1,firstCargoStockIncreases=-1;
                AutomaticWorkerMutation mutation=null;
                run.InSimulation(run.CurrentMission,()=>{mutation=ApplyNaturalAppleStep(placement,farm);return true;});
                bool reached=false,delivered=false;
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                for(int sample=0;sample<(expectedEnabled?5000:24);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    bool stop=run.InSimulation(run.CurrentMission,()=>{
                        ValidateAppleStepAccess(placement);
                        if(!AutomaticActorAlive(worker))return true;
                        CheckActor(worker,true);
                        var current=runtime.SnapshotUnit(worker.Id,true,false);
                        Require(current!=null && current.TribeGlobalId==placement.Before.TribeGlobalId,"Apple started moat changed original tribe lifetime");
                        reached|=WorkerAtRecordedTarget(worker,farm,mutation.GoalX,mutation.GoalY);
                        // Native drop callbacks count individual fruit, not journeys.
                        // A loaded source must finish its original batch, then pick
                        // fresh fruit and physically deposit that later batch too.
                        if(state==7 && firstCargoDrops<0 && workerCycle.Delivered)
                        {firstCargoDrops=workerCycle.DropEvents;firstCargoStockIncreases=workerCycle.DropsWithStockIncrease;}
                        bool actualFreshDelivery=state!=7 || firstCargoDrops>=0 && workerCycle.DropEvents>firstCargoDrops &&
                            workerCycle.DropsWithStockIncrease>firstCargoStockIncreases;
                        delivered=workerCycle.Delivered && workerCycle.CounterIncrements>0 && workerCycle.PickupEvents>0 &&
                            actualFreshDelivery && WorkerCarry(worker)==0;
                        return expectedEnabled && delivered;
                    });
                    if(stop)break;
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    workerCycle.Active=false;ValidateAppleStepAccess(placement);

                    log("automatic-apple-started-moat-cycle enabled="+expectedEnabled+" state="+state+" originalSegmentReached="+reached+
                        " completeHarvestDelivery="+delivered+" increments="+workerCycle.CounterIncrements+" pickups="+workerCycle.PickupEvents+
                        " drops="+workerCycle.DropEvents+" firstCargoDrops="+firstCargoDrops+" physicalStockIncreases="+workerCycle.DropsWithStockIncrease+
                        " remotePicking="+workerCycle.RemotePicking+" fault="+(workerCycle.Fault??"none")+" originalStationaryMoatCaseCovered=false");
                    if(expectedEnabled)
                    {
                        Require(workerCycle.Fault==null,"Apple started moat cycle observation failed: "+workerCycle.Fault);
                        Require(reached && delivered && workerCycle.RemotePicking==0 && workerCycle.PickupEvents>0 && workerCycle.DropsWithStockIncrease>0,
                            "Apple started moat rescue did not complete the original segment plus actual harvest/cargo/granary cycle");
                    }
                    EndAutomaticWorkerCase(mutation,delivered);return true;
                });
            }
        }
    }
}
