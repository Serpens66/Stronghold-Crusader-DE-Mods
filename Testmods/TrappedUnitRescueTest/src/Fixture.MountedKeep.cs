using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.WorldMutations;
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
        private bool mountedHookCallsLogged;
        private int mountedStationaryVerified,mountedCommittedVerified,mountedOwnRescued,mountedBlockedVerified,mountedDistinctAuxObserved,mountedPositiveAuxRecovered,mountedNegativeStationary,mountedNegativeCommitted,mountedRiderBridgeOwnRescued,mountedApproachHandoffs;
        private readonly Dictionary<uint,MountedApproachState> mountedApproachStates=new Dictionary<uint,MountedApproachState>();
        private sealed class MountedApproachState { internal int Current,Next; }
        private sealed class KeepRecoveryPose
        {
            internal int Keep,Camp,ExitX,ExitY,ExitTile;internal uint KeepGlobal,CampGlobal;internal int Rotation;
        }
        private sealed class MountedOrigin
        {
            internal Actor Actor;internal int Current,Next,Aux;internal bool DistinctOccupiedAux,OwnRescued;internal int LocalX,LocalY;
        }
        private sealed class MountedCommittedBuild
        {
            internal Fixture Fixture;
            internal Actor Actor;
            internal long Session;
            internal eMappers Mapper;
            internal int Rotation,X,Y,Scale,TargetY,Ticks,Retries,Claimed,Finished;
            internal bool Blocked;
            internal List<MountedOrigin> Origins;
            internal KeepRecoveryPose Pose;
            internal uint ExitFlags;
            internal int ExitHeight;
            internal string Fault;
        }
        private static bool mountedCommittedBuildRegistered;
        private static volatile MountedCommittedBuild mountedCommittedBuild;
        private MountedCommittedBuild ArmMountedCommittedBuild(eMappers mapper,int rotation,int targetY,bool blocked,List<MountedOrigin> origins)
        {
            if(!mountedCommittedBuildRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-mounted-committed-build",ObserveMountedCommittedBuild,out string reason),
                    "Mounted committed construction tick unavailable: "+reason);
                mountedCommittedBuildRegistered=true;
            }
            Require(mountedCommittedBuild==null,"A preceding committed Keep construction is still armed");
            var request=new MountedCommittedBuild{Fixture=this,Actor=actors[0],Session=session,Mapper=mapper,Rotation=rotation,
                X=cx,Y=cy,Scale=BuildingScales.GetScale(mapper),TargetY=targetY,Blocked=blocked,Origins=origins};
            mountedCommittedBuild=request;return request;
        }
        private static void ObserveMountedCommittedBuild(int tick)
        {
            var request=mountedCommittedBuild;if(request==null || Volatile.Read(ref request.Finished)!=0)return;
            var fixture=request.Fixture;
            try
            {
                Require(!fixture.stopped && fixture.session==request.Session &&
                    WorldMutationEvents.TryGetCurrentWorld(out long actualSession,out _) && actualSession==request.Session,
                    "Committed Keep request lost its original fixture world");
                // Published ticks already exclude pause; no Unity pause or UI work belongs here.
                if(GamePlayerManagerAPI.Instance.IsLocalPaused())return;
                Require(fixture.cx==request.X && fixture.cy==request.Y,"Committed Keep fixture anchor changed while armed");
                bool acquired=fixture.ObserveMountedApproach(request.Actor,request.X,request.Y,request.Scale,request.TargetY,request.Ticks++,ref request.Retries);
                if(!acquired)
                {
                    Require(request.Ticks<256,"No native pre-midpoint Keep entry in 256 actual simulation callbacks");
                    return;
                }
                // Claim before any native construction. A partial/throwing writer is never retried.
                Require(Interlocked.CompareExchange(ref request.Claimed,1,0)==0,"Committed Keep writer was already claimed");
                try
                {
                    fixture.suppression(fixture.expectedEnabled);
                    request.Origins.Add(fixture.CaptureMountedOrigin(request.Actor,true));
                    using(var mutation=fixture.runtime.BeginMutation("native-keep-pending-step-"+request.Mapper+"-"+request.Rotation,
                        request.X+request.Scale/2,request.Y+request.Scale/2,request.Scale+2))
                    {
                        request.Pose=fixture.BuildMountedKeep(request.Mapper,request.Rotation);
                        var results=mutation.Complete(false);
                        foreach(var origin in request.Origins)
                        {
                            RescueResult result=results.Single(r=>r.UnitId==origin.Actor.Id);
                            Require(result.Status!=RescueStatus.Rescued,"Existing local runtime unexpectedly accepted a committed step");
                            fixture.log("mounted-keep-local-observation unit="+origin.Actor.Id+" result="+result.Status+" source=native-build-over-committed-next-tile");
                        }
                    }
                    fixture.CompleteMountedPreFixes(request.Origins,request.Pose,true,request.Mapper==eMappers.MAPPER_KEEP1,
                        request.Blocked,out request.ExitFlags,out request.ExitHeight);
                }
                finally{fixture.suppression(false);}
                Volatile.Write(ref request.Finished,1);
                if(ReferenceEquals(mountedCommittedBuild,request))mountedCommittedBuild=null;
            }
            catch(Exception error)
            {
                request.Fault=error.ToString();Volatile.Write(ref request.Finished,1);
                if(ReferenceEquals(mountedCommittedBuild,request))mountedCommittedBuild=null;
            }
        }
        // Invoked by Fixture.Steps after the ordinary public automation mission setup.
        private IEnumerable<AutomationStep> MountedKeepSteps()
        {
            Require(RescueScenarioSelection.TryMountedKeepPart(scenario,out int keepNumber,out int direction),
                "Mounted keep aggregate must expand into fresh-mission partitions before execution");
            yield return new AutomationStep("mounted-keep-installed-prerequisite",TimeSpan.FromSeconds(30),MountedKeepPrerequisite,true);
            if(keepNumber==0)
            {
                foreach(eChimps rider in KeepRiders)
                {
                    eChimps type=rider;
                    yield return new AutomationStep("rider-local-rescue-before-fixes-tick-"+type,TimeSpan.FromSeconds(90),
                        run=>MountedRiderLocalControl(run,type),true);
                }
                yield return new AutomationStep("mounted-keep-blocked-exit",TimeSpan.FromSeconds(90),
                    run=>MountedKeepCase(run,eMappers.MAPPER_KEEP2,0,true,KeepRiders[0],true),true);
            }
            else
            {
                eMappers keep=keepNumber==2?eMappers.MAPPER_KEEP2:keepNumber==3?eMappers.MAPPER_KEEP3:eMappers.MAPPER_KEEP1;
                yield return new AutomationStep("mounted-keep-stationary-"+keep+"-"+direction,TimeSpan.FromSeconds(90),
                    run=>MountedKeepCase(run,keep,direction,false,KeepRiders[0],false),true);
                foreach(eChimps rider in KeepRiders)
                {
                    eChimps type=rider;
                    yield return new AutomationStep("mounted-keep-committed-"+keep+"-"+direction+"-"+type,TimeSpan.FromSeconds(90),
                        run=>MountedKeepCase(run,keep,direction,true,type,false),true);
                }
            }
            yield return new AutomationStep("mounted-keep-coverage",TimeSpan.FromSeconds(15),MountedKeepCoverage,true);
        }
        private IEnumerable<AutomationWait> MountedKeepPrerequisite(AutomationRun run)
        {
            Execute(run,()=>{
                RequireMountedKeepHooks();
                SelectMountedAiOwner();
                foreach(eChimps type in KeepRiders)
                    Require(GamePathingManagerAPI.Instance.GetUnitTypePathfindingProfile(type)==PathfindingProfile.Large,
                        "Actual rider profile is not Large: "+type);
                log("mounted-keep-prerequisite installedFixesHooks=true foreignSettingsChanged=false manualUnitState=false enabled="+expectedEnabled);
            });
            yield break;
        }
        private unsafe void SelectMountedAiOwner()
        {
            int localOwner=owner;
            var players=GamePlayerManagerAPI.Instance;
            for(int candidate=1;candidate<=GamePlayerManagerAPI.MAX_PLAYERS;candidate++)
            {
                if(!players.IsAIPlayer(candidate))continue;
                int keepId=players.GetPlayerKeepId(candidate);
                if(!BuildingAccess.TryGetById(keepId,out GameBuilding* keep,out _) || keep==null || keep->r_AliveState!=AliveState.IsAlive ||
                    keep->r_PlayerIdOwner!=candidate || keep->r_GlobalId==0)continue;
                if(keep->r_BuildingType!=eStructs.STRUCT_KEEP_ONE && keep->r_BuildingType!=eStructs.STRUCT_KEEP_TWO &&
                    keep->r_BuildingType!=eStructs.STRUCT_KEEP_THREE && keep->r_BuildingType!=eStructs.STRUCT_KEEP_FOUR && keep->r_BuildingType!=eStructs.STRUCT_KEEP_FIVE)continue;
                // Fixes'74DA0 wrapper overrides human rotations from its lobby VM. The real AI branch
                // forwards our requested native direction unchanged, while all foreign hooks stay active.
                owner=candidate;
                log("mounted-owner originalLocal="+localOwner+" actualFixtureOwner="+owner+" isAI=true lord="+players.GetAILord(owner)+
                    " existingKeep="+keepId+"/"+keep->r_GlobalId+" nativeSpawnOwnership=true nativeTribeOwnership=true foreignSettingsChanged=false");
                return;
            }
            throw new InvalidOperationException("No actual living AI keep owner is available for the controlled mounted overbuild matrix");
        }
        private unsafe void RequireMountedKeepHooks()
        {
            // Unity has destroyed early plugin components by mission start; their managed
            // instances/assemblies and statically retained native detours remain alive.
            Require(Chainloader.PluginInfos.TryGetValue("fixes",out var plugin) && !ReferenceEquals(plugin.Instance,null),
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
                if(!mountedHookCallsLogged)LogMountedHookCalls(handle.Hook);
                actual.Add(handle.Hook.TargetAddress);
            }
            Require(actual.SetEquals(expected),"Actual mounted keep hooks do not cover all four rider update handlers");
            mountedHookCallsLogged=true;
        }
        private void LogMountedHookCalls(X64InlineHook hook)
        {
            ulong stub=unchecked((ulong)hook.StubAddress.ToInt64());
            ulong image=unchecked((ulong)runtime.Native.ImageBase.ToInt64());
            byte[] bytes=Shared.CommittedMemory.ReadUpTo(hook.StubAddress,4096);
            var decoder=Iced.Intel.Decoder.Create(64,new Iced.Intel.ByteArrayCodeReader(bytes));decoder.IP=stub;
            while(decoder.IP<stub+(ulong)bytes.Length)
            {
                var instruction=decoder.Decode();
                if(instruction.Mnemonic==Iced.Intel.Mnemonic.Call && instruction.Op0Kind==Iced.Intel.OpKind.NearBranch64)
                    log("mounted-hook-call updateRva="+(hook.TargetAddress-image).ToString("X")+
                        " callTargetRva="+unchecked(instruction.NearBranchTarget-image).ToString("X")+
                        " expectedTraversalRva=187110 source=actual-published-stub boundedRead=true");
                if(instruction.Mnemonic==Iced.Intel.Mnemonic.Jmp && instruction.Op0Kind==Iced.Intel.OpKind.NearBranch64 &&
                    instruction.NearBranchTarget==hook.TargetAddress+(uint)hook.DisplacedByteCount)return;
                if(instruction.Mnemonic==Iced.Intel.Mnemonic.Jmp && instruction.IsIPRelativeMemoryOperand)
                {
                    ulong slot=instruction.IPRelativeMemoryAddress;
                    if(slot>=stub && slot+8<=stub+(ulong)bytes.Length &&
                        BitConverter.ToUInt64(bytes,(int)(slot-stub))==hook.TargetAddress+(uint)hook.DisplacedByteCount)return;
                }
                if(instruction.Code==Iced.Intel.Code.INVALID)break;
            }
            log("mounted-hook-call decodeStoppedWithoutResume=true updateRva="+(hook.TargetAddress-image).ToString("X"));
        }
        private IEnumerable<AutomationWait> MountedKeepCase(AutomationRun run,eMappers mapper,int rotation,bool committed,eChimps type,bool blocked)
        {
            var origins=new List<MountedOrigin>();KeepRecoveryPose pose=null;
            int scale=BuildingScales.GetScale(mapper),startX=0,startY=0;
            Execute(run,()=>{
                RequireMountedKeepHooks();Require(actors.Count==0,"Previous fixture actors were not retired");FindMountedSite();
                startX=cx-3;startY=cy+scale/2;
                if(committed)SpawnSingle(type,startX,startY);
                else
                {
                    pose=BuildMountedKeep(mapper,rotation);
                    //6D580 immediately clears selectable AI cavalry in its occupied build footprint.
                    // Initialize real original actors outside the full compound, then use the audited native placement routine.
                    for(int i=0;i<KeepRiders.Length;i++)SpawnSingle(KeepRiders[i],cx-16,cy+i*2);
                }
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,64);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            foreach(var wait in WaitMountedIdleBoundary(run))yield return wait;
            Execute(run,()=>PrepareMountedActors());
            MountedCommittedBuild pending=null;
            bool noRoof=mapper==eMappers.MAPPER_KEEP1;
            uint exitFlags=0;int exitHeight=0;
            try
            {
            if(committed)
            {
                Execute(run,()=>{
                    StageMountedApproach(actors[0],startX,startY);
                    pending=ArmMountedCommittedBuild(mapper,rotation,startY,blocked,origins);
                    Require(runtime.Native.Reissue(actors[0].Id,cx+scale+2,startY),"Native mounted approach route was rejected");
                });
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return AutomationWait.Until("actual committed Keep construction in its owning simulation callback",
                    ()=>Volatile.Read(ref pending.Finished)!=0);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Require(pending.Fault==null,"Committed Keep simulation capture/build failed: "+pending.Fault);
                Require(pending.Claimed==1 && pending.Pose!=null && origins.Count==1,"Committed Keep construction did not complete exactly once");
                pose=pending.Pose;exitFlags=pending.ExitFlags;exitHeight=pending.ExitHeight;
            }
            else
                Execute(run,()=>{
                    for(int i=0;i<actors.Count;i++)
                    {
                        Actor actor=actors[i];
                        Require(runtime.Native.Relocate(actor.Id,cx+2+i%2,cy+2+i/2),"Audited native placement onto the existing keep failed");
                        CheckActor(actor,true); // Exact original identity/HP/tribe, old ground occupancy removed.
                        origins.Add(CaptureMountedOrigin(actor,false));
                    }
                    CompleteMountedPreFixes(origins,pose,false,noRoof,blocked,out exitFlags,out exitHeight);
                });
            if(noRoof)
            {
                // Canonical74DA0 writes IsBuilding for KEEP1, never an elevated roof.
                // Observe every initialization tick too, then one actual update with a live
                // campground. Otherwise NeedsInit could conceal a false positive recovery.
                bool campReady=run.InSimulation(run.CurrentMission,()=>MountedCampIsAlive(pose));
                for(int tick=0;tick<16 && !campReady;tick++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    Execute(run,()=>CheckMountedNoRoof(origins,pose));
                    campReady=run.InSimulation(run.CurrentMission,()=>MountedCampIsAlive(pose));
                }
                Require(campReady,"Native campground never initialized for the KEEP1 negative case");
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>CheckMountedNoRoof(origins,pose));
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,8);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    CheckMountedNoRoof(origins,pose);
                    if(committed){mountedNegativeCommitted+=origins.Count;mountedCommittedVerified+=origins.Count;}
                    else{mountedNegativeStationary+=origins.Count;mountedStationaryVerified+=origins.Count;}
                    log("mounted-keep-negative mapper="+mapper+" rotation="+rotation+" committed="+committed+" riderCount="+origins.Count+
                        " nativeNoRoof=true firstTickNoInvalidCampTeleport=true identityHealthOccupancy=true actualTribeBinding=true ownLocalNoDouble="+origins.Count(o=>o.OwnRescued));
                    RetireActors();
                });
                yield break;
            }
            if(blocked)
            {
                // Campground starts NeedsInit. Keep the obstacle until a real initialized target exists,
                // then perform a further unit-update tick while it remains blocked.
                bool campReady=run.InSimulation(run.CurrentMission,()=>MountedCampIsAlive(pose));
                for(int tick=0;tick<16 && !campReady;tick++)
                {
                    foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                    yield return run.WaitForTicks(run.CurrentMission,1);
                    foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                    campReady=run.InSimulation(run.CurrentMission,()=>MountedCampIsAlive(pose));
                }
                Require(campReady,"Native campground never initialized for the blocked-exit negative case");
                Execute(run,()=>{foreach(var origin in origins)Require(MountedKeepPredicate(origin.Actor,pose.Keep),
                    "Committed rider left the keep before an initialized blocked exit could be exercised");});
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                Execute(run,()=>{
                    foreach(var origin in origins){CheckBaselineIdentity(origin.Actor);Require(!AtMountedExit(origin.Actor,pose),"Fixes teleported onto the blocked campground exit");
                        Require(MountedKeepPredicate(origin.Actor,pose.Keep),"Rider left the keep before blocked-exit recovery could be checked");}
                    var tiles=GameTileManagerAPI.Instance;tiles.SetTilePropertyFlag(pose.ExitTile,(TilePropertyFlag)exitFlags);tiles.SetTileHeight(pose.ExitTile,checked((byte)exitHeight));
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
                if(!recovered && (tick==0 || tick==23))
                    Execute(run,()=>LogMountedRecoveryState(origins,pose));
            }
            Require(recovered,"Installed Fixes did not recover every original rider to the actual campground exit");
            // A further native unit update must not apply a second teleport to an already recovered/local-rescued actor.
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,8);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                Require(MountedRecoveryOutcome(origins,pose),"Rider moved again after its successful recovery");
                mountedPositiveAuxRecovered+=origins.Count(o=>o.DistinctOccupiedAux);
                if(committed)mountedCommittedVerified+=origins.Count;else mountedStationaryVerified+=origins.Count;
                log("mounted-keep-result mapper="+mapper+" rotation="+rotation+" committed="+committed+" riderCount="+origins.Count+
                    " originalIdentityHealthTribe=true exactDestination=true oldCurrentNextUnlinked=true distinctOccupiedAuxObserved="+origins.Count(o=>o.DistinctOccupiedAux)+" noSecondMove=true ownEnabled="+expectedEnabled);
                RetireActors();
            });
            }
            finally
            {
                if(pending!=null && ReferenceEquals(mountedCommittedBuild,pending))mountedCommittedBuild=null;
            }
        }
        private void CompleteMountedPreFixes(List<MountedOrigin> origins,KeepRecoveryPose pose,bool committed,bool noRoof,bool blocked,
            out uint exitFlags,out int exitHeight)
        {
            exitFlags=0;exitHeight=0;
            foreach(var origin in origins)
            {
                LogMountedPostBuild(origin,pose);
                if(noRoof)Require(!MountedKeepPredicate(origin.Actor,pose.Keep),"Native KEEP1 unexpectedly acquired an elevated roof predicate");
                else Require(MountedKeepPredicate(origin.Actor,pose.Keep),"Actual keep surface did not create the intended rider overlap");
                log("mounted-keep-before-fixes rider="+origin.Actor.Type+" unit="+origin.Actor.Id+"/"+origin.Actor.Global+
                    " keep="+pose.Keep+"/"+pose.KeepGlobal+" rotation="+pose.Rotation+" committed="+committed+" blockedExit="+blocked+
                    " source="+(committed?"native-construction-over-pending-step":"controlled-native-placement-onto-existing-keep")+
                    " exit="+pose.ExitX+","+pose.ExitY+" hiddenWord9B0="+MountedHiddenWord(origin.Actor));
            }
            if(blocked)
            {
                // Same negative-case geometry seam, before the first rider update.
                var tiles=GameTileManagerAPI.Instance;exitFlags=(uint)tiles.GetTilePropertyFlag(pose.ExitTile);exitHeight=tiles.GetTileHeight(pose.ExitTile);
                tiles.SetTilePropertyFlag(pose.ExitTile,(TilePropertyFlag)(exitFlags|(uint)TilePropertyFlag.IsWall));
                tiles.SetTileHeight(pose.ExitTile,90);GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(pose.ExitX,pose.ExitY);
            }
        }
        private void FindMountedSite()
        {
            var keep=GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            int size=GameTileManagerAPI.Instance.GetCurrentMapSize();
            // Canonical74DA0 compound tables2D30F0/2D3210/2D3270: all tested roots,
            // doors,7x7campgrounds and5x5yards lie inside anchor offsets[-12,+18].
            for(int radius=24;radius<size;radius+=16)
                for(int dy=-radius;dy<=radius;dy+=16)
                    for(int dx=-radius;dx<=radius;dx+=16)
                    {
                        int x=keep.X+dx,y=keep.Y+dy;
                        if(!FlatSquare(x,y,20,out int height))continue;
                        cx=x;cy=y;elevation=height;log("mounted-site anchor="+x+","+y+" validatedFlatRadius=20");return;
                    }
            throw new InvalidOperationException("No clear flat41x41 site for the audited native Keep compound");
        }
        private unsafe void LogMountedPostBuild(MountedOrigin origin,KeepRecoveryPose pose)
        {
            if(UnitAccess.TryGetById(origin.Actor.Id,out GameUnit* observed,out var failure) && observed!=null)
                log("mounted-post-build-record unit="+origin.Actor.Id+" expectedGlobal="+origin.Actor.Global+
                    " actualGlobal="+observed->r_GlobalId+" expectedType="+origin.Actor.Type+" actualType="+observed->r_UnitChimp+
                    " expectedOwner="+owner+" actualOwner="+observed->r_ControllableForPlayerId+" alive="+observed->r_AliveState+
                    " health="+observed->r_CurrentHealth+" tribe="+observed->r_TribeId);
            else log("mounted-post-build-record unit="+origin.Actor.Id+" unavailable="+failure);
            var unit=Resolve(origin.Actor);var tiles=GameTileManagerAPI.Instance;
            int current=(int)unit->r_CurrentPositionTileId,next=(int)unit->r_NextPositionTileId2;
            log("mounted-post-build unit="+origin.Actor.Id+"/"+origin.Actor.Global+" type="+origin.Actor.Type+" keep="+pose.Keep+
                " current="+current+" flags="+((uint)tiles.GetTilePropertyFlag(current)).ToString("X8")+" building="+tiles.GetTileBuildingId(current)+
                " next="+next+" flags="+(next>=0 && next<NativeRelocation.TileCapacity?((uint)tiles.GetTilePropertyFlag(next)).ToString("X8"):"invalid")+
                " nextBuilding="+(next>=0 && next<NativeRelocation.TileCapacity?tiles.GetTileBuildingId(next):-1)+" substep="+unit->r_MovementSubstep+
                " predicate="+MountedKeepPredicate(origin.Actor,pose.Keep));
        }
        private readonly HashSet<string> mountedNativeTribeReassignments=new HashSet<string>();
        private unsafe void ValidateMountedTribeBinding(Actor actor,string phase,bool allowNativeReassignment)
        {
            var unit=Resolve(actor);int tribeId=unit->r_TribeId;
            var tribes=GameTribeManagerAPI.Instance;
            GameTribe* tribe=null;
            Require(tribeId>0 && tribes.TryGetTribeById(tribeId,out tribe) && tribe!=null &&
                tribe->r_AliveState==AliveState.IsAlive && tribe->r_GlobalId!=0 && tribes.GetOwner(tribeId)==owner,
                "Mounted actor is not bound to a live tribe owned by its real AI player: "+actor.Id+" tribe="+tribeId);
            var members=new List<int>();
            Require(tribes.GetUnits(tribeId,members) && members.Count(id=>id==actor.Id)==1,
                "Mounted actor tribe bitmap does not contain its actual unit ID exactly once: "+actor.Id+" tribe="+tribeId);
            Require(allowNativeReassignment || tribeId==actor.Tribe,"Mounted recovery changed frozen tribe membership: "+actor.Id);
            if(tribeId!=actor.Tribe && mountedNativeTribeReassignments.Add(phase+":"+actor.Id+":"+unit->r_GlobalId+":"+tribeId))
                log("mounted-native-tribe-binding phase="+phase+" unit="+actor.Id+"/"+actor.Global+
                    " frozenTribe="+actor.Tribe+" actualTribe="+tribeId+" tribeGlobal="+tribe->r_GlobalId+
                    " owner="+owner+" bitmapMember=true frozenBaselineUnchanged=true");
        }
        private IEnumerable<AutomationWait> WaitMountedIdleBoundary(AutomationRun run)
        {
            for(int tick=0;tick<=256;tick++)
            {
                bool ready=run.InSimulation(run.CurrentMission,()=>MountedActorsAtIdleBoundary(tick==0 || tick==256));
                if(ready)yield break;
                if(tick==0)Execute(run,()=>StopMountedPreparationGroups());
                Require(tick<256,"No natural inactive mounted boundary after 256 additional ticks; no actor motion state was fabricated");
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            }
        }
        private unsafe void StopMountedPreparationGroups()
        {
            // This is a real game command in the disposable AI-owned fixture world.
            // Native11E960/31 cancels the route through197950; the existing interpolation
            // finishes naturally. Never interrupt an active aux chain with19BA40.
            var tribes=GameTribeManagerAPI.Instance;var groups=new HashSet<int>();
            foreach(var actor in actors)
            {
                ValidateMountedTribeBinding(actor,"before-native-stop-command",true);
                groups.Add(Resolve(actor)->r_TribeId);
            }
            foreach(int tribeId in groups)
            {
                var members=new List<int>();
                Require(tribes.GetUnits(tribeId,members) && members.Count>0,"Preparation tribe has no actual members");
                Require(tribes.Stop(tribeId),"Native stop command rejected the real mounted preparation tribe");
                log("mounted-native-preparation-stop tribe="+tribeId+" owner="+owner+" actualGroupMembers="+members.Count+
                    " command=UnitStop nativeInterpolationCompletionRequired=true manualUnitState=false");
            }
        }
        private unsafe bool MountedActorsAtIdleBoundary(bool describe)
        {
            bool ready=true;
            foreach(var actor in actors)
            {
                var unit=Resolve(actor);
                Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth>0,"Mounted actor did not survive natural preparation");
                bool idle=unit->r_PathPlanStateBitFlags==0 && unit->r_MovementSubstep==8;
                ready &= idle;
                if(describe)log("mounted-pretest-motion unit="+actor.Id+"/"+actor.Global+" idleBoundary="+idle+
                    " flags="+unit->r_PathPlanStateBitFlags+" substep="+unit->r_MovementSubstep+
                    " current="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                    " inactiveNext="+unit->r_NextTilePositionX2+","+unit->r_NextTilePositionY2+" ai="+unit->r_AIState);
            }
            return ready;
        }
        private unsafe void PrepareMountedActors()
        {
            // Capture native AI assignment BEFORE the tested placement/construction, never after recovery.
            foreach(var actor in actors)
            {
                ValidateMountedTribeBinding(actor,"ordinary-ground-pretest",true);
                var unit=Resolve(actor);int originallyAssigned=actor.Tribe;
                actor.Tribe=unit->r_TribeId;
                log("mounted-pretest-baseline unit="+actor.Id+"/"+actor.Global+" assignedTribe="+originallyAssigned+
                    " actualTribe="+actor.Tribe+" health="+unit->r_CurrentHealth+" minimumNaturalGroundTicks=64 bitmapMember=true");
                Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth>0 && unit->r_PathPlanStateBitFlags==0 && unit->r_MovementSubstep==8,
                    "Mounted pretest requires a real inactive native movement boundary");
                actor.Health=unit->r_CurrentHealth;
                actor.SourceX=actor.X=unit->r_CurrentTilePositionX;actor.SourceY=actor.Y=unit->r_CurrentTilePositionY;
                CheckActor(actor,true);
            }
        }
        private unsafe void StageMountedApproach(Actor actor,int x,int y)
        {
            var unit=Resolve(actor);
            Require(unit->r_PathPlanStateBitFlags==0 && unit->r_MovementSubstep==8,
                "Controlled approach staging requires a naturally inactive movement boundary");
            int oldX=unit->r_CurrentTilePositionX,oldY=unit->r_CurrentTilePositionY,ai=unit->r_AIState;
            int tile=runtime.Native.Tile(x,y);
            Require(tile>=0 && runtime.Native.OrdinaryGround(tile) &&
                (runtime.Native.Head(tile)==0 || (int)unit->r_CurrentPositionTileId==tile && CountInTileChain(tile,actor.Id)==1),
                "Original approach start is no longer free ordinary ground");
            Require(runtime.Native.Relocate(actor.Id,x,y),"Controlled native pretest staging failed");
            actor.SourceX=actor.X=x;actor.SourceY=actor.Y=y;
            CheckActor(actor,true);ValidateMountedTribeBinding(actor,"controlled-pretest-staging",true);
            unit=Resolve(actor);
            Require(unit->r_AIState==ai && unit->r_PathPlanStateBitFlags==0 && unit->r_MovementSubstep==8,
                "Native pretest staging changed the recorded idle AI state or did not retain an inactive movement boundary");
            log("mounted-controlled-pretest-staging unit="+actor.Id+"/"+actor.Global+" from="+oldX+","+oldY+
                " start="+x+","+y+" sameIdentityHealthTribe=true idleNativeRelocation=true manualAIOrPathState=false");
        }
        private unsafe void CheckMountedInactiveBaseline(Actor actor)
        {
            var unit=Resolve(actor);
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth==actor.Health,"Inactive rider baseline changed identity/health");
            ValidateMountedTribeBinding(actor,"inactive-native-bridge",true);
            actor.X=unit->r_CurrentTilePositionX;actor.Y=unit->r_CurrentTilePositionY;
            int tile=runtime.Native.Tile(actor.X,actor.Y);
            Require(unit->r_CurrentPositionTileId==(uint)tile && CountInTileChain(tile,actor.Id)==1,
                "Inactive rider baseline has inconsistent native position/occupancy");
        }
        private unsafe void CheckMountedNoRoof(List<MountedOrigin> origins,KeepRecoveryPose pose)
        {
            RequireMountedKeepHooks();
            foreach(var origin in origins)
            {
                var unit=Resolve(origin.Actor);
                Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth==origin.Actor.Health,
                    "KEEP1 negative changed rider identity/health: unit="+origin.Actor.Id+" alive="+unit->r_AliveState+
                    " health="+unit->r_CurrentHealth+" expectedHealth="+origin.Actor.Health+" tribe="+unit->r_TribeId+
                    " expectedTribe="+origin.Actor.Tribe+" ai="+unit->r_AIState);
                ValidateMountedTribeBinding(origin.Actor,"KEEP1-native-negative",true);
                Require(!MountedKeepPredicate(origin.Actor,pose.Keep),"KEEP1 negative unexpectedly became an elevated roof");
                int tile=(int)unit->r_CurrentPositionTileId;
                Require(tile==runtime.Native.Tile(unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY) && CountInTileChain(tile,origin.Actor.Id)==1,
                    "KEEP1 negative left inconsistent actual position/occupancy");
                if(origin.OwnRescued)
                    Require(unit->r_CurrentTilePositionX==origin.LocalX && unit->r_CurrentTilePositionY==origin.LocalY,
                        "Actual rider moved again after local KEEP1 rescue and a natural Fixes rider update");
                else Require(!AtMountedExit(origin.Actor,pose),"Nonmatching KEEP1 rider was teleported to the campground exit");
            }
        }
        private unsafe void RememberMountedLocalRescue(MountedOrigin origin)
        {
            var unit=Resolve(origin.Actor);mountedOwnRescued++;
            origin.LocalX=unit->r_CurrentTilePositionX;origin.LocalY=unit->r_CurrentTilePositionY;
            Require(runtime.Native.OrdinaryGround((int)unit->r_CurrentPositionTileId),"Locally rescued rider is not on ordinary ground before its actual Fixes update");
        }
        private unsafe ushort MountedHiddenWord(Actor actor)=>runtime.Native.Word(Resolve(actor),0x9B0);
        private unsafe bool MountedCampIsAlive(KeepRecoveryPose pose) =>
            BuildingAccess.TryGetById(pose.Camp,out GameBuilding* camp,out _) && camp!=null && camp->r_GlobalId==pose.CampGlobal && camp->r_AliveState==AliveState.IsAlive;
        private unsafe MountedOrigin CaptureMountedOrigin(Actor actor,bool committed)
        {
            var unit=Resolve(actor);Require(UnitAccess.IsReallyAlive(unit),"Mounted actor was not alive");
            actor.Health=unit->r_CurrentHealth;actor.SourceX=unit->r_CurrentTilePositionX;actor.SourceY=unit->r_CurrentTilePositionY;
            var origin=new MountedOrigin{Actor=actor,Current=(int)unit->r_CurrentPositionTileId,Next=(int)unit->r_NextPositionTileId2,
                Aux=unchecked((int)unit->N0000010A)};
            int currentCount=MountedOriginalOccupancy(origin.Current,actor.Id),nextCount=MountedOriginalOccupancy(origin.Next,actor.Id),auxCount=MountedOriginalOccupancy(origin.Aux,actor.Id);
            origin.DistinctOccupiedAux=committed && origin.Aux>0 && origin.Aux!=origin.Current && origin.Aux<NativeRelocation.TileCapacity && auxCount==1;
            if(origin.DistinctOccupiedAux)mountedDistinctAuxObserved++;
            if(committed)Require(origin.Current!=origin.Next && currentCount==1 && nextCount==0,
                "Natural pre-midpoint approach did not have exactly one current occupancy and no next occupancy");
            log("mounted-origin unit="+actor.Id+"/"+actor.Global+" type="+actor.Type+" committed="+committed+
                " flags="+unit->r_PathPlanStateBitFlags+" substep="+unit->r_MovementSubstep+
                " current="+origin.Current+"/count="+currentCount+" next="+origin.Next+"/count="+nextCount+
                " aux="+origin.Aux+"/count="+auxCount+" distinctOccupiedAux="+origin.DistinctOccupiedAux);
            return origin;
        }
        private int MountedOriginalOccupancy(int tile,int unitId)=>tile>0 && tile<NativeRelocation.TileCapacity?CountInTileChain(tile,unitId):-1;
        private unsafe bool ObserveMountedApproach(Actor actor,int x,int y,int scale,int targetY,int tick,ref int retries)
        {
            var unit=Resolve(actor);int targetX=x+scale+2;
            int current=(int)unit->r_CurrentPositionTileId,next=(int)unit->r_NextPositionTileId2;
            if(mountedApproachStates.TryGetValue(actor.Global,out var prior) && prior.Current!=current && prior.Next==current)
            {
                Require(MountedOriginalOccupancy(prior.Current,actor.Id)==0 && MountedOriginalOccupancy(current,actor.Id)==1,
                    "Natural mounted midpoint handoff retained old occupancy or lost new occupancy");
                mountedApproachHandoffs++;
                log("mounted-natural-approach-handoff unit="+actor.Id+"/"+actor.Global+" oldCurrent="+prior.Current+
                    " current="+current+" next="+next+" substep="+unit->r_MovementSubstep+" oldCount=0 currentCount=1 source=ordinary-native-movement");
            }
            mountedApproachStates[actor.Global]=new MountedApproachState{Current=current,Next=next};
            bool acquired=NaturalCommittedKeepApproach(actor,x,y,scale);
            if(acquired || tick%16==0 || tick==255)
                log("mounted-approach-observation unit="+actor.Id+"/"+actor.Global+" tick="+tick+" acquired="+acquired+
                    " current="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                    " next="+unit->r_NextTilePositionX2+","+unit->r_NextTilePositionY2+
                    " requested="+unit->r_TargetTilePositionX2+","+unit->r_TargetTilePositionY2+
                    " expected="+targetX+","+targetY+" flags="+unit->r_PathPlanStateBitFlags+
                    " substep="+unit->r_MovementSubstep+" ai="+unit->r_AIState+" tribe="+unit->r_TribeId+
                    " aux="+unit->N0000010A+" commandRetries="+retries);
            if(!acquired && tick>0 && tick%16==0 && retries<4 &&
                (unit->r_TargetTilePositionX2!=targetX || unit->r_TargetTilePositionY2!=targetY ||
                 unit->r_PathPlanStateBitFlags==0 && (unit->r_CurrentTilePositionX!=targetX || unit->r_CurrentTilePositionY!=targetY)))
            {
                retries++;
                Require(runtime.Native.Reissue(actor.Id,targetX,targetY),"Native mounted approach retry was rejected");
                log("mounted-approach-native-retry unit="+actor.Id+" retry="+retries+" realCommand=true manualUnitState=false");
            }
            return acquired;
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
            var tiles=GameTileManagerAPI.Instance;
            bool oldOverride=tiles.TileManager.UsePlacementBlockedOverride,oldOverrideValue=tiles.TileManager.PlacementBlockedOverrideValue;
            try
            {
                using(var observer=BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args=>{
                    if(args.Phase!=EventHookPhase.Post || args.PlayerId!=owner || args.ReturnValue<=0)return;
                    if(args.Building==mapper.ConvertToEStructs())keepId=checked((int)args.ReturnValue);
                    if(args.Building==eStructs.STRUCT_CAMPGROUND)campId=checked((int)args.ReturnValue);
                }))GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,mapper,BuildingScales.GetScale(mapper),rotation,true,true);
            }
            finally
            {
                tiles.TileManager.PlacementBlockedOverrideValue=oldOverrideValue;
                tiles.TileManager.UsePlacementBlockedOverride=oldOverride;
            }
            GameBuilding* keep=null;GameBuilding* camp=null;GamePlayerResources* player=null;
            Require(keepId>0 && campId>0 && BuildingAccess.TryGetById(keepId,out keep,out _) && keep!=null &&
                BuildingAccess.TryGetById(campId,out camp,out _) && camp!=null && keep->r_PlayerIdOwner==owner && camp->r_PlayerIdOwner==owner &&
                keep->r_BuildingType==mapper.ConvertToEStructs() && camp->r_BuildingType==eStructs.STRUCT_CAMPGROUND,"Native keep/campground pair was not materialized");
            // Native Fixes reads player-manager12ED94 + playerId*583C, the public resource field at+1D4.
            Require(GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner,out player) && player!=null && player->r_KeepDoorId==(uint)campId,
                "Actual player campground reference differs from the newly spawned native campground");
            log("mounted-keep-created requested="+rotation+" keep="+keepId+" root92="+keep->N00012042_2+
                " root102="+keep->r_SpriteVariationIndex+" camp="+campId+" camp92="+camp->N00012042_2+
                " camp102="+camp->r_SpriteVariationIndex+" origin="+camp->r_TilePositionXBegin+","+camp->r_TilePositionYBegin);
            Require(camp->N00012042_2==rotation,"Native campground direction differs from the requested rotation: requested="+
                rotation+" root92="+keep->N00012042_2+" root102="+keep->r_SpriteVariationIndex+
                " camp92="+camp->N00012042_2+" camp102="+camp->r_SpriteVariationIndex);
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
        private unsafe void LogMountedRecoveryState(List<MountedOrigin> origins,KeepRecoveryPose pose)
        {
            foreach(var origin in origins)
            {
                var unit=Resolve(origin.Actor);
                log("mounted-recovery-observed unit="+origin.Actor.Id+" type="+origin.Actor.Type+
                    " actual="+unit->r_CurrentTilePositionX+","+unit->r_CurrentTilePositionY+
                    " expected="+pose.ExitX+","+pose.ExitY+" currentTile="+unit->r_CurrentPositionTileId+
                    " next="+unit->r_NextTilePositionX2+","+unit->r_NextTilePositionY2+
                    " flags="+unit->r_PathPlanStateBitFlags+" substep="+unit->r_MovementSubstep+" ai="+unit->r_AIState+
                    " tribe="+unit->r_TribeId+" campAlive="+MountedCampIsAlive(pose)+
                    " exitFlags="+((uint)GameTileManagerAPI.Instance.GetTilePropertyFlag(pose.ExitTile)).ToString("X8")+
                    " keepPredicate="+MountedKeepPredicate(origin.Actor,pose.Keep));
            }
        }
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
        private IEnumerable<AutomationWait> MountedRiderLocalControl(AutomationRun run,eChimps type)
        {
            RescueStatus status=RescueStatus.Unsupported;int postX=0,postY=0;
            Execute(run,()=>{RequireMountedKeepHooks();Require(actors.Count==0,"Previous actors were not retired");FindSite();SpawnSingle(type,cx+2,cy+2);});
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,64);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            foreach(var wait in WaitMountedIdleBoundary(run))yield return wait;
            Execute(run,()=>{
                PrepareMountedActors();Actor actor=actors[0];
                // The four bridge corners are the actual impassable cells; its center
                // retains exits. Prepare the live rider on ordinary ground at that corner
                // before opening the causally observed construction scope.
                Require(runtime.Native.Relocate(actor.Id,cx,cy),"Native rider placement at the bridge corner failed");
                CheckActor(actor,true);actor.SourceX=actor.X;actor.SourceY=actor.Y;
                using(var mutation=runtime.BeginMutation("native-drawbridge-single-rider-"+type,cx+2,cy+2,5))
                {
                    GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,eMappers.MAPPER_DRAWBRIDGE,BuildingScales.GetScale(eMappers.MAPPER_DRAWBRIDGE),0,true,true);
                    int bridge=GameTileManagerAPI.Instance.GetTileBuildingId(GameTileManagerAPI.Instance.GetTileId(cx,cy));
                    Require(bridge>0 && BridgeType(bridge) && !HasStructuralExit(actor),"Real native bridge did not create an intact rider trap");
                    RescueResult result=mutation.Complete(false).Single(r=>r.UnitId==actor.Id);status=result.Status;
                    Require(expectedEnabled || status==RescueStatus.Inactive,"Disabled local runtime executed rider rescue");
                    if(status==RescueStatus.Rescued)
                    {
                        Require(expectedEnabled && result.IdentityPreserved && !AtSource(actor) && HasStructuralExit(actor),"Native rider local rescue postconditions failed");
                        mountedRiderBridgeOwnRescued++;
                    }
                    else Require(AtSource(actor),"Rider moved without a confirmed local rescue");
                    CheckActor(actor,true);postX=actor.X;postY=actor.Y;
                    log("rider-local-rescue result="+result+" source=single-rider-native-drawbridge manualUnitState=false");
                }
            });
            foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,8);
            foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
            Execute(run,()=>{
                RequireMountedKeepHooks();Actor actor=actors[0];
                if(status!=RescueStatus.Rescued)CheckMountedInactiveBaseline(actor);
                else CheckActor(actor,true);
                if(status==RescueStatus.Rescued)Require(actor.X==postX && actor.Y==postY,"Actually rescued rider moved again during subsequent mounted Fixes handler ticks");
                log("rider-local-next-fixes-tick type="+type+" unit="+actor.Id+"/"+actor.Global+" ownEnabled="+expectedEnabled+
                    " localStatus="+status+" noDoubleProof="+(status==RescueStatus.Rescued)+" actualPosition="+actor.X+","+actor.Y+
                    " priorPosition="+postX+","+postY+" actualMountedHandlerInstalled=true identityHealthOccupancy=true");
                RetireActors();
            });
        }
        private IEnumerable<AutomationWait> MountedKeepCoverage(AutomationRun run)
        {
            Require(RescueScenarioSelection.TryMountedKeepPart(scenario,out int keepNumber,out int direction),"Unknown mounted keep coverage partition");
            bool controls=keepNumber==0;
            Require(mountedStationaryVerified==(controls?0:4) && mountedCommittedVerified==(controls?1:4) && mountedBlockedVerified==(controls?1:0),
                "Mounted keep partition incomplete: stationary="+mountedStationaryVerified+" committed="+mountedCommittedVerified+" blocked="+mountedBlockedVerified);
            Require(mountedNegativeStationary==(keepNumber==1?4:0) && mountedNegativeCommitted==(keepNumber==1?4:0),
                "KEEP1 no-roof negative partition incomplete");
            // Ordinary riding updates only current/next occupancy; no natural nonzero6B8
            // producer is established. Preserve observations and validate actual aux entries
            // when present, without manufacturing an auxiliary occupancy test state.
            if(expectedEnabled && controls)Require(mountedOwnRescued+mountedRiderBridgeOwnRescued>0,
                "Coverage open: no actual rider was rescued locally before its natural Fixes rider update; Assassin bridge control cannot establish that interaction");
            log("mounted-keep-coverage stationary="+mountedStationaryVerified+" committed="+mountedCommittedVerified+" keep="+keepNumber+" campgroundRotation="+direction+" riderTypes=4 blockedExit="+(controls?1:0)+" freshMissionPartition=true aggregatePartitions=13"+
                " positiveStationary="+(mountedStationaryVerified-mountedNegativeStationary)+" positiveCommitted="+(mountedCommittedVerified-mountedNegativeCommitted)+
                " noRoofNegativeStationary="+mountedNegativeStationary+" noRoofNegativeCommitted="+mountedNegativeCommitted+
                " ownEnabled="+expectedEnabled+" keepOwnRescueCount="+mountedOwnRescued+" riderBridgeOwnRescueCount="+mountedRiderBridgeOwnRescued+" distinctOccupiedAuxObserved="+mountedDistinctAuxObserved+" positiveOccupiedAuxRecovered="+mountedPositiveAuxRecovered+
                " occupiedAuxCleanup="+(mountedPositiveAuxRecovered>0?"executed":"not-executed")+
                " ordinaryApproachHandoffs="+mountedApproachHandoffs+
                " ordinaryApproachHandoffCoverage="+(mountedApproachHandoffs>0?"executed":"not-executed")+
                " additionalAssassinBridgeControl=false foreignSettingsChanged=false");
            yield break;
        }
    }
}
