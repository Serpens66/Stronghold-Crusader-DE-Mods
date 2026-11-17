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
        private IEnumerable<AutomationWait> Siege(AutomationRun run)
        {
            Actor device=null;Actor[] crew=null;
            Execute(run,()=>{
                FindSite();SpawnSingle(eChimps.CHIMP_TYPE_CATAPULT,cx,cy);
                SpawnSingle(eChimps.CHIMP_TYPE_ENGINEER,cx-2,cy-1);SpawnSingle(eChimps.CHIMP_TYPE_ENGINEER,cx-2,cy+1);
                device=actors[0];crew=new[]{actors[1],actors[2]};
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,12);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                VerifyLiveAndRemember();
                foreach(var engineer in crew)
                    Require(GameTribeManagerAPI.Instance.ManSiegeEquipmentEx(engineer.Tribe,device.Id,checked((int)device.Global)),"Native man-siege command rejected");
            });
            bool mounted=false;
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            for(int sample=0;sample<80;sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission,8);
                mounted=run.InSimulation(run.CurrentMission,()=>CrewMatches(device,crew,true));if(mounted)break;
            }
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Require(mounted,"Two original engineers never became a genuinely mounted native crew");
            Execute(run,()=>{
                CaptureMountedCrewPositions(device,crew);
                // Mounted engineers are intentionally absent from occupancy and must not be handled as independent roots.
                actors.Remove(crew[0]);actors.Remove(crew[1]);
                VerifyLiveAndRemember();
                var scope=runtime.BeginMutation("synthetic-wall-under-natively-manned-catapult",device.X,device.Y,1);
                try
                {
                    PaintSyntheticTrap(device.X,device.Y);
                    Require(!HasStructuralExit(device),"Siege fixture did not close all native ground exits");
                    var result=scope.Complete(false).Single(r=>r.UnitId==device.Id);
                    log("siege-rescue "+result);
                    if(expectedEnabled)Require(result.Status==RescueStatus.Rescued && !AtSource(device),"Manned device root was not locally relocated");
                    else Require(AtSource(device),"Disabled runtime relocated the device");
                    CheckActor(device,true);Require(CrewMatches(device,crew,true),"Relocation changed mounted crew identities/state");
                }
                finally{scope.Dispose();}
            });
            int targetX=0,targetY=0;bool issued=false;
            Execute(run,()=>{
                FindDistantSiegeTarget(device,crew,out targetX,out targetY);
                issued=GameTribeManagerAPI.Instance.IssueMoveHereCommand(device.Tribe,targetX,targetY);
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            bool arrived=false;
            for(int sample=0;sample<(expectedEnabled?240:8);sample++)
            {
                yield return run.WaitForTicks(run.CurrentMission,8);
                arrived=run.InSimulation(run.CurrentMission,()=>{
                    Require(CrewMatches(device,crew,true),"Mounted crew identity/state lost during real device movement");
                    return MovementOutcome(device,targetX,targetY);
                });
                if(expectedEnabled && arrived)break;
            }
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            if(expectedEnabled)Require(issued && arrived,"Rescued manned machine never completed real native movement");
            else Require(!arrived,"Baseline manned machine escaped; fixture no longer demonstrates trapping");
            Execute(run,()=>{
                if(expectedEnabled)RequireDeviceFarFromHistoricalCrew(device,crew);
                Require(GameTribeManagerAPI.Instance.DissolveSiegeEquipmentEx(device.Tribe,device.Id,checked((int)device.Global)),"Native crew dismount rejected");
                // Action17 performs placement synchronously. Inspect it before the regular
                // engineer109->0 continuation can move the same surviving units again.
                CheckDismountedCrew(device,crew,true);
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,48);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                CheckDismountedCrew(device,crew,false);
                log("siege-result enabled="+expectedEnabled+" syntheticEntrapment=true nativeManning=true nativeDismount=true "+
                    "deviceIdentityPreserved=true originalEngineerIdentitiesPreserved=true realMovementEndpoint="+arrived+" crewCount=2 recreatedUnits=0");
            });
        }
        private unsafe void CaptureMountedCrewPositions(Actor device,Actor[] crew)
        {
            Require(CrewMatches(device,crew,true),"Crew binding changed before the historical-position snapshot");
            foreach(var actor in crew)
            {
                Require(UnitAccess.TryGetById(actor.Id,out GameUnit* unit,out _) && unit!=null && unit->r_GlobalId==actor.Global,"Mounted crew identity missing");
                actor.SourceX=unit->r_CurrentTilePositionX;actor.SourceY=unit->r_CurrentTilePositionY;
                log("siege-mounted-crew-position unit="+actor.Id+"/"+actor.Global+" historical="+actor.SourceX+","+actor.SourceY);
            }
        }
        private void FindDistantSiegeTarget(Actor device,Actor[] crew,out int targetX,out int targetY)
        {
            var tiles=GameTileManagerAPI.Instance;
            for(int radius=32;radius<=48;radius+=4)
                for(int d=0;d<8;d++)
                {
                    int x=device.X+Dx[d]*radius,y=device.Y+Dy[d]*radius;
                    if(!tiles.IsTileInsideMapBounds(x,y))continue;
                    int tile=tiles.GetTileId(x,y);
                    if(tiles.GetTileBuildingId(tile)!=0 || tiles.GetTileUnitId(tile)!=0 || !tiles.IsTileWalkableAndUnoccupied(tile) ||
                        ((uint)tiles.GetTilePropertyFlag(tile)&0x5A7015B1u)!=0)continue;
                    bool separated=true;
                    foreach(var actor in crew)
                        if(Math.Max(Math.Abs(x-actor.SourceX),Math.Abs(y-actor.SourceY))<=20){separated=false;break;}
                    if(!separated)continue;
                    targetX=x;targetY=y;
                    log("siege-distant-target target="+x+","+y+" requestedRadius="+radius+" minimumHistoricalCrewSeparation=21");
                    return;
                }
            throw new InvalidOperationException("No free ordinary native siege endpoint32..48 tiles away and more than20 tiles from every mounted crew history point");
        }
        private unsafe void RequireDeviceFarFromHistoricalCrew(Actor device,Actor[] crew)
        {
            GameUnit* machine=Resolve(device);
            foreach(var actor in crew)
            {
                int distance=Math.Max(Math.Abs(machine->r_CurrentTilePositionX-actor.SourceX),Math.Abs(machine->r_CurrentTilePositionY-actor.SourceY));
                Require(distance>18,"Actual machine movement did not separate dismount from historical crew coordinates");
                log("siege-pre-dismount-separation crew="+actor.Id+" distanceFromHistorical="+distance);
            }
        }
        private unsafe bool CrewMatches(Actor device,Actor[] crew,bool mounted)
        {
            GameUnit* machine=Resolve(device);
            var ids=new HashSet<int>();
            for(int slot=0;slot<4;slot++)
            {
                int id=*(ushort*)((byte*)machine+0x96C-0x65C+slot*2);
                uint global=*(uint*)((byte*)machine+0x974-0x65C+slot*4);
                if(id==0)continue;
                Actor engineer=Array.Find(crew,a=>a.Id==id && a.Global==global);if(engineer==null)return false;
                if(!UnitAccess.TryGetById(id,out GameUnit* unit,out _) || unit==null || !UnitAccess.IsReallyAlive(unit) || unit->r_GlobalId!=global ||
                    unit->r_UnitChimp!=eChimps.CHIMP_TYPE_ENGINEER || unit->r_ControllableForPlayerId!=owner || unit->r_CurrentHealth!=engineer.Health)return false;
                // Binding proof uses identity, bound state and mounting, not the timeout's current phase.
                if(mounted && (unit->r_AIState!=5 || *(ushort*)((byte*)unit+0x9B0-0x65C)==0))return false;
                ids.Add(id);
            }
            return ids.Count==2 && *(ushort*)((byte*)machine+0xA0C-0x65C)==2;
        }
        private unsafe void CheckDismountedCrew(Actor device,Actor[] crew,bool immediatePlacement)
        {
            GameUnit* machine=Resolve(device);
            Require(*(ushort*)((byte*)machine+0xA0C-0x65C)==0,"Native dismount left crew count nonzero");
            foreach(var actor in crew)
            {
                Require(UnitAccess.TryGetById(actor.Id,out GameUnit* unit,out _) && unit!=null && UnitAccess.IsReallyAlive(unit) &&
                    unit->r_GlobalId==actor.Global && unit->r_UnitChimp==eChimps.CHIMP_TYPE_ENGINEER && unit->r_CurrentHealth==actor.Health,
                    "Dismount replaced/lost an original engineer");
                Require(*(ushort*)((byte*)unit+0x9B0-0x65C)==0,"Original engineer remained mounted after dismount");
                Require(CountInTileChain((int)unit->r_CurrentPositionTileId,actor.Id)==1,"Dismounted original engineer occupancy invalid");
                int distance=Math.Max(Math.Abs(unit->r_CurrentTilePositionX-machine->r_CurrentTilePositionX),Math.Abs(unit->r_CurrentTilePositionY-machine->r_CurrentTilePositionY));
                log("siege-dismount-observation immediate="+immediatePlacement+" enabled="+expectedEnabled+" crew="+actor.Id+"/"+actor.Global+
                    " crewXY="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+" deviceXY="+machine->r_CurrentTilePositionX+","+machine->r_CurrentTilePositionY+
                    " historicalCrew="+actor.SourceX+","+actor.SourceY+" distance="+distance+" ai="+unit->r_AIState+" flags="+unit->r_PathPlanStateBitFlags);
                // F0030 searches the component without a16-tile radius bound;0x10 is its
                // height tolerance. The controlled moved-device case retains its immediate
                // proximity proof. An unmoved trapped baseline and later AI motion do not.
                if(immediatePlacement && expectedEnabled)Require(distance<=16,
                    "Immediate native dismount did not place crew near the actually moved device");
            }
        }
    }
}
