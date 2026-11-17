using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using BepInEx.Bootstrap;
using R3;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Extensions;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private static readonly eChimps[] KeepRiders={eChimps.CHIMP_TYPE_KNIGHT,eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL};
        private int mountedStationaryVerified,mountedCommittedVerified,mountedOwnRescued,mountedBlockedVerified;
        private sealed class KeepRecoveryPose
        {
            internal int Keep,Camp,ExitX,ExitY,ExitTile;internal uint KeepGlobal,CampGlobal;internal int Rotation;
        }
        private sealed class MountedOrigin
        {
            internal Actor Actor;internal int Current,Next,Aux;internal bool OwnRescued;internal int LocalX,LocalY;
        }
        // Invoked by Fixture.Steps after the ordinary public automation mission setup.
        private IEnumerable<AutomationStep> MountedKeepSteps()
        {
            yield return new AutomationStep("mounted-keep-installed-prerequisite",TimeSpan.FromSeconds(30),MountedKeepPrerequisite,true);
            foreach(eMappers mapper in new[]{eMappers.MAPPER_KEEP1,eMappers.MAPPER_KEEP2,eMappers.MAPPER_KEEP3})
                foreach(int rotation in new[]{0,2,4,6})
                {
                    eMappers keep=mapper;int direction=rotation;
                    yield return new AutomationStep("mounted-keep-stationary-"+keep+"-"+direction,TimeSpan.FromSeconds(90),
                        run=>MountedKeepCase(run,keep,direction,false,KeepRiders[0],false),true);
                    foreach(eChimps rider in KeepRiders)
                    {
                        eChimps type=rider;
                        yield return new AutomationStep("mounted-keep-committed-"+keep+"-"+direction+"-"+type,TimeSpan.FromSeconds(90),
                            run=>MountedKeepCase(run,keep,direction,true,type,false),true);
                    }
                }
            yield return new AutomationStep("mounted-keep-blocked-exit",TimeSpan.FromSeconds(90),
                run=>MountedKeepCase(run,eMappers.MAPPER_KEEP1,0,true,KeepRiders[0],true),true);
            yield return new AutomationStep("local-rescue-before-fixes-tick",TimeSpan.FromSeconds(90),MountedLocalRescueControl,true);
            yield return new AutomationStep("mounted-keep-coverage",TimeSpan.FromSeconds(15),MountedKeepCoverage,true);
        }
        private IEnumerable<AutomationWait> MountedKeepPrerequisite(AutomationRun run)
        {
            Execute(run,()=>{
                RequireMountedKeepHooks();
                foreach(eChimps type in KeepRiders)
                    Require(GamePathingManagerAPI.Instance.GetUnitTypePathfindingProfile(type)==PathfindingProfile.Large,
                        "Actual rider profile is not Large: "+type);
                log("mounted-keep-prerequisite installedFixesHooks=true foreignSettingsChanged=false manualUnitState=false enabled="+expectedEnabled);
            });
            yield break;
        }
        private unsafe void RequireMountedKeepHooks()
        {
            Require(Chainloader.PluginInfos.TryGetValue("fixes",out var plugin) && plugin.Instance!=null,
                "Mounted keep acceptance requires the installed Fixes plugin; no fixture changes its setting");
            Assembly assembly=plugin.Instance.GetType().Assembly;
            Type managerType=assembly.GetType("Fixes.Detours.DetourManager",true);
            object manager=managerType.GetProperty("Instance",BindingFlags.Static|BindingFlags.Public).GetValue(null);
            var owners=managerType.GetField("_detours",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(manager) as IEnumerable;
            Require(owners!=null,"Fixes permanent detour owner inventory unavailable");
            object unitOwner=null;
            foreach(object ownerObject in owners)if(ownerObject!=null && ownerObject.GetType().FullName=="Fixes.Detours.FixesUnitDetours")unitOwner=ownerObject;
            Require(unitOwner!=null,"Fixes unit detour owner was not retained");
            var handles=unitOwner.GetType().GetField("_mountedKeepRecoveryHooks",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(unitOwner) as IEnumerable;
            Require(handles!=null,"Installed Fixes does not expose the audited mounted-keep hook inventory");
            var table=GameGlobalsManager.Instance.GameUnitFunctionsVTable.Pointer;
            Require(table!=null,"Native unit update table unavailable");
            var expected=new HashSet<ulong>(new[]{(ulong)table->KnightUpdate,(ulong)table->ArabHorsemanUpdate,
                (ulong)table->BedouinCamelLancerUpdate,(ulong)table->BedouinHeavyCamelUpdate});
            Require(!expected.Contains(0),"An actual mounted keep handler is null");
            var actual=new HashSet<ulong>();
            foreach(object value in handles)
            {
                var handle=value as HookHandle<X64InlineHook>;
                Require(handle!=null && handle.Failure==null && handle.IsInstalled && handle.Hook!=null && handle.Hook.IsInstalled,
                    "A mounted keep recovery hook was not actually published");
                Require(handle.ResolvedAddress==handle.Hook.TargetAddress && expected.Contains(handle.Hook.TargetAddress),
                    "Mounted keep recovery is attached to a different native handler");
                actual.Add(handle.Hook.TargetAddress);
            }
            Require(actual.SetEquals(expected),"Actual mounted keep hooks do not cover all four rider update handlers");
        }
        private IEnumerable<AutomationWait> MountedKeepCase(AutomationRun run,eMappers mapper,int rotation,bool committed,eChimps type,bool blocked)
        {
            var origins=new List<MountedOrigin>();KeepRecoveryPose pose=null;
            int scale=BuildingScales.GetScale(mapper),startX=0,startY=0;
            Execute(run,()=>{
                RequireMountedKeepHooks();Require(actors.Count==0,"Previous fixture actors were not retired");FindSite();
                startX=cx-1;startY=cy+scale/2;
                if(committed)SpawnSingle(type,startX,startY);
                else for(int i=0;i<KeepRiders.Length;i++)SpawnSingle(KeepRiders[i],cx+2+i%2,cy+2+i/2);
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,16);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>VerifyLiveAndRemember());
            if(committed)
            {
                Execute(run,()=>Require(runtime.Native.Reissue(actors[0].Id,cx+scale+2,startY),"Native mounted approach route was rejected"));
                bool acquired=false;
                for(int tick=0;tick<96 && !acquired;tick++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    acquired=run.InSimulation(run.CurrentMission,()=>NaturalCommittedKeepApproach(actors[0],cx,cy,scale));
                }
                Require(acquired,"No actual committed step crossed the future keep boundary; no unit state was fabricated");
            }
            uint exitFlags=0;int exitHeight=0;
            Execute(run,()=>{
                foreach(Actor actor in actors)origins.Add(CaptureMountedOrigin(actor));
                using(var mutation=runtime.BeginMutation("native-keep-mounted-"+mapper+"-"+rotation,cx+scale/2,cy+scale/2,scale+2))
                {
                    pose=BuildMountedKeep(mapper,rotation);
                    foreach(var origin in origins)Require(MountedKeepPredicate(origin.Actor,pose.Keep),"Actual keep build did not create the intended rider overlap");
                    if(blocked)
                    {
                        // Explicit negative-case geometry seam. No rider, route or foreign setting is altered.
                        var tiles=GameTileManagerAPI.Instance;exitFlags=(uint)tiles.GetTilePropertyFlag(pose.ExitTile);exitHeight=tiles.GetTileHeight(pose.ExitTile);
                        tiles.SetTilePropertyFlag(pose.ExitTile,(TilePropertyFlag)(exitFlags|(uint)TilePropertyFlag.IsWall));
                        tiles.SetTileHeight(pose.ExitTile,90);GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(pose.ExitX,pose.ExitY);
                    }
                    var results=mutation.Complete(false);
                    foreach(var origin in origins)
                    {
                        RescueResult result=results.Single(r=>r.UnitId==origin.Actor.Id);
                        origin.OwnRescued=result.Status==RescueStatus.Rescued;
                        Require(expectedEnabled || !origin.OwnRescued,"Disabled local rescue moved a rider");
                        if(committed)Require(!origin.OwnRescued,"Existing local runtime unexpectedly accepted a committed step");
                        if(origin.OwnRescued)RememberMountedLocalRescue(origin);
                        log("mounted-keep-before-fixes rider="+origin.Actor.Type+" unit="+origin.Actor.Id+"/"+origin.Actor.Global+
                            " keep="+pose.Keep+"/"+pose.KeepGlobal+" rotation="+rotation+" committed="+committed+" blockedExit="+blocked+" localResult="+result.Status+
                            " exit="+pose.ExitX+","+pose.ExitY+" hiddenWord9B0="+MountedHiddenWord(origin.Actor));
                    }
                }
            });
            if(blocked)
            {
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    foreach(var origin in origins){CheckBaselineIdentity(origin.Actor);Require(!AtMountedExit(origin.Actor,pose),"Fixes teleported onto the blocked campground exit");
                        Require(MountedKeepPredicate(origin.Actor,pose.Keep),"Rider left the keep before blocked-exit recovery could be checked");}
                    var tiles=GameTileManagerAPI.Instance;tiles.SetTilePropertyFlag(pose.ExitTile,(TilePropertyFlag)exitFlags);tiles.SetTileHeight(pose.ExitTile,exitHeight);
                    GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(pose.ExitX,pose.ExitY);mountedBlockedVerified++;
                    log("mounted-keep-blocked-exit rejected=true obstacleRemoved=true syntheticObstacleOnly=true");
                });
            }
            bool recovered=false;
            for(int tick=0;tick<24 && !recovered;tick++)
            {
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                recovered=run.InSimulation(run.CurrentMission,()=>MountedRecoveryOutcome(origins,pose));
            }
            Require(recovered,"Installed Fixes did not recover every original rider to the actual campground exit");
            // A further native unit update must not apply a second teleport to an already recovered/local-rescued actor.
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,8);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                Require(MountedRecoveryOutcome(origins,pose),"Rider moved again after its successful recovery");
                if(committed)mountedCommittedVerified+=origins.Count;else mountedStationaryVerified+=origins.Count;
                log("mounted-keep-result mapper="+mapper+" rotation="+rotation+" committed="+committed+" riderCount="+origins.Count+
                    " originalIdentityHealthTribe=true exactDestination=true oldCurrentNextAuxUnlinked=true noSecondMove=true ownEnabled="+expectedEnabled);
                RetireActors();
            });
        }
        private unsafe void RememberMountedLocalRescue(MountedOrigin origin)
        {
            var unit=Resolve(origin.Actor);mountedOwnRescued++;
            origin.LocalX=unit->r_CurrentTilePositionX;origin.LocalY=unit->r_CurrentTilePositionY;
            Require(runtime.Native.OrdinaryGround((int)unit->r_CurrentPositionTileId),"Locally rescued rider is not on ordinary ground before its actual Fixes update");
        }
        private unsafe ushort MountedHiddenWord(Actor actor)=>runtime.Native.Word(Resolve(actor),0x9B0);
        private unsafe MountedOrigin CaptureMountedOrigin(Actor actor)
        {
            var unit=Resolve(actor);Require(UnitAccess.IsReallyAlive(unit),"Mounted actor was not alive");
            actor.Health=unit->r_CurrentHealth;actor.SourceX=unit->r_CurrentTilePositionX;actor.SourceY=unit->r_CurrentTilePositionY;
            return new MountedOrigin{Actor=actor,Current=(int)unit->r_CurrentPositionTileId,Next=(int)unit->r_NextPositionTileId2,
                Aux=unchecked((int)unit->N0000010A)};
        }
        private unsafe bool NaturalCommittedKeepApproach(Actor actor,int x,int y,int scale)
        {
            var unit=Resolve(actor);
            bool currentInside=unit->r_CurrentTilePositionX>=x && unit->r_CurrentTilePositionX<x+scale && unit->r_CurrentTilePositionY>=y && unit->r_CurrentTilePositionY<y+scale;
            return !currentInside && unit->r_MovementSubstep<8 && (unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4) &&
                unit->r_NextTilePositionX2>=x && unit->r_NextTilePositionX2<x+scale && unit->r_NextTilePositionY2>=y && unit->r_NextTilePositionY2<y+scale;
        }
        private unsafe KeepRecoveryPose BuildMountedKeep(eMappers mapper,int rotation)
        {
            int keepId=0,campId=0;
            using(var observer=BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args=>{
                if(args.Phase!=EventHookPhase.Post || args.PlayerId!=owner || args.ReturnValue<=0)return;
                if(args.Building==mapper.ConvertToEStructs())keepId=checked((int)args.ReturnValue);
                if(args.Building==eStructs.STRUCT_CAMPGROUND)campId=checked((int)args.ReturnValue);
            }))GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,mapper,BuildingScales.GetScale(mapper),rotation,true,true);
            GameBuilding* keep=null;GameBuilding* camp=null;GamePlayerResources* player=null;
            Require(keepId>0 && campId>0 && BuildingAccess.TryGetById(keepId,out keep,out _) && keep!=null &&
                BuildingAccess.TryGetById(campId,out camp,out _) && camp!=null && keep->r_PlayerIdOwner==owner && camp->r_PlayerIdOwner==owner &&
                keep->r_BuildingType==mapper.ConvertToEStructs() && camp->r_BuildingType==eStructs.STRUCT_CAMPGROUND,"Native keep/campground pair was not materialized");
            // Native Fixes reads player-manager12ED94 + playerId*583C, the public resource field at+1D4.
            Require(GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner,out player) && player!=null && player->r_KeepDoorId==(uint)campId,
                "Actual player campground reference differs from the newly spawned native campground");
            Require(camp->N00012042_2==rotation,"Native campground direction differs from the requested rotation");
            int x=camp->r_TilePositionXBegin,y=camp->r_TilePositionYBegin;
            if(rotation==2){x++;y+=3;}else if(rotation==6){x+=5;y+=3;}else{x+=3;y+=rotation==4?5:1;}
            int tile=runtime.Native.Tile(x,y);Require(tile>=0 && ((uint)GameTileManagerAPI.Instance.GetTilePropertyFlag(tile)&0x5A5015B1u)==0,
                "Actual native campground exit is initially obstructed");
            return new KeepRecoveryPose{Keep=keepId,Camp=campId,KeepGlobal=keep->r_GlobalId,CampGlobal=camp->r_GlobalId,ExitX=x,ExitY=y,ExitTile=tile,Rotation=rotation};
        }
        private unsafe bool MountedKeepPredicate(Actor actor,int keepId)
        {
            var unit=Resolve(actor);var tiles=GameTileManagerAPI.Instance;int current=(int)unit->r_CurrentPositionTileId;
            if(current>=0 && current<NativeRelocation.TileCapacity && (tiles.GetTilePropertyFlag(current)&TilePropertyFlag.IsElevated)!=0)
                return tiles.GetTileBuildingId(current)==keepId;
            int next=(int)unit->r_NextPositionTileId2;
            return unit->r_MovementSubstep<8 && (unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4) &&
                next>=0 && next<NativeRelocation.TileCapacity && (tiles.GetTilePropertyFlag(next)&TilePropertyFlag.IsElevated)!=0 && tiles.GetTileBuildingId(next)==keepId;
        }
        private unsafe bool AtMountedExit(Actor actor,KeepRecoveryPose pose)
        {var unit=Resolve(actor);return unit->r_CurrentPositionTileId==(uint)pose.ExitTile && unit->r_CurrentTilePositionX==pose.ExitX && unit->r_CurrentTilePositionY==pose.ExitY;}
        private unsafe bool MountedRecoveryOutcome(List<MountedOrigin> origins,KeepRecoveryPose pose)
        {
            Require(BuildingAccess.TryGetById(pose.Camp,out GameBuilding* camp,out _) && camp!=null && camp->r_GlobalId==pose.CampGlobal &&
                camp->r_PlayerIdOwner==owner && camp->r_BuildingType==eStructs.STRUCT_CAMPGROUND,"Original campground identity disappeared");
            Require(BuildingAccess.TryGetById(pose.Keep,out GameBuilding* keep,out _) && keep!=null && keep->r_GlobalId==pose.KeepGlobal &&
                keep->r_PlayerIdOwner==owner,"Original keep identity disappeared");
            bool all=true;
            foreach(var origin in origins)
            {
                var unit=Resolve(origin.Actor);
                Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth==origin.Actor.Health && unit->r_TribeId==origin.Actor.Tribe,"Original mounted identity/health/tribe did not survive");
                bool destination=origin.OwnRescued?unit->r_CurrentTilePositionX==origin.LocalX && unit->r_CurrentTilePositionY==origin.LocalY:AtMountedExit(origin.Actor,pose);
                if(!destination){all=false;continue;}
                int tile=(int)unit->r_CurrentPositionTileId;
                Require(tile==runtime.Native.Tile(unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY),"Recovered rider packed tile and coordinates differ");
                Require(CountInTileChain(tile,origin.Actor.Id)==1,"Recovered mounted actor occupancy is not exactly once");
                foreach(int old in new[]{origin.Current,origin.Next,origin.Aux})
                    if(old>0 && old<NativeRelocation.TileCapacity && old!=tile)Require(CountInTileChain(old,origin.Actor.Id)==0,"Mounted recovery retained old current/next/aux occupancy");
            }
            return all;
        }
        private IEnumerable<AutomationWait> MountedLocalRescueControl(AutomationRun run)
        {
            List<int> trapped=null;var positions=new Dictionary<int,Tuple<int,int>>();
            Execute(run,()=>{RequireMountedKeepHooks();FindSite();SpawnMatrix(eChimps.CHIMP_TYPE_ARAB_ASSASIN);});
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,12);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{VerifyLiveAndRemember();trapped=BuildBridgeAndCheck(eChimps.CHIMP_TYPE_ARAB_ASSASIN);
                foreach(var actor in actors.Where(a=>trapped.Contains(a.Id)))positions.Add(actor.Id,Tuple.Create(actor.X,actor.Y));});
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,8);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                foreach(var actor in actors.Where(a=>trapped.Contains(a.Id)))
                {CheckActor(actor,true);Require(actor.X==positions[actor.Id].Item1 && actor.Y==positions[actor.Id].Item2,"Already locally rescued Assassin moved again during Fixes ticks");}
                log("local-rescue-fixes-next-tick type=Assassin actualMountedFixesStillInstalled=true ownEnabled="+expectedEnabled+
                    " realBridgeTrapCount="+trapped.Count+" noSecondMove=true mountedOwnRescueCount="+mountedOwnRescued);
                RetireActors();
            });
        }
        private IEnumerable<AutomationWait> MountedKeepCoverage(AutomationRun run)
        {
            Require(mountedStationaryVerified==48 && mountedCommittedVerified==49 && mountedBlockedVerified==1,
                "Mounted keep matrix incomplete: stationary="+mountedStationaryVerified+" committed="+mountedCommittedVerified+" blocked="+mountedBlockedVerified);
            if(expectedEnabled)Require(mountedOwnRescued>0,
                "Coverage open: no actual rider was rescued locally before its natural Fixes rider update; Assassin bridge control cannot establish that interaction");
            log("mounted-keep-coverage stationary="+mountedStationaryVerified+" committed="+mountedCommittedVerified+" keeps=3 campgroundRotations=4 riderTypes=4 blockedExit=1"+
                " ownEnabled="+expectedEnabled+" mountedOwnRescueCount="+mountedOwnRescued+" additionalAssassinBridgeControl=true foreignSettingsChanged=false");
            yield break;
        }
    }
}
