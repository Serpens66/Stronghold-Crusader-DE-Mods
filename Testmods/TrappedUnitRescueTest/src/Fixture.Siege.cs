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
                FindMovementTarget(device,out targetX,out targetY);
                issued=GameTribeManagerAPI.Instance.IssueMoveHereCommand(device.Tribe,targetX,targetY);
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            bool arrived=false;
            for(int sample=0;sample<(expectedEnabled?120:8);sample++)
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
                Require(GameTribeManagerAPI.Instance.DissolveSiegeEquipmentEx(device.Tribe,device.Id,checked((int)device.Global)),"Native crew dismount rejected");
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,48);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                CheckDismountedCrew(device,crew);
                log("siege-result enabled="+expectedEnabled+" syntheticEntrapment=true nativeManning=true nativeDismount=true "+
                    "deviceIdentityPreserved=true originalEngineerIdentitiesPreserved=true realMovementEndpoint="+arrived+" crewCount=2 recreatedUnits=0");
            });
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
                if(mounted && (unit->r_AIState!=5 || *(ushort*)((byte*)unit+0x9B0-0x65C)==0 || *(int*)((byte*)unit+0x974-0x65C)>120))return false;
                ids.Add(id);
            }
            return ids.Count==2 && *(ushort*)((byte*)machine+0xA0C-0x65C)==2;
        }
        private unsafe void CheckDismountedCrew(Actor device,Actor[] crew)
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
                Require(Math.Max(Math.Abs(unit->r_CurrentTilePositionX-machine->r_CurrentTilePositionX),Math.Abs(unit->r_CurrentTilePositionY-machine->r_CurrentTilePositionY))<=16,
                    "Dismount used historical crew coordinates instead of the moved device");
            }
        }
    }
}
