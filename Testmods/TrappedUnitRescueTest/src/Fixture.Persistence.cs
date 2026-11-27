using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.WorldMutations;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class PersistenceProof
        {
            internal Fixture Fixture;
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal string Path,Fault;
            internal long OriginalSession,LoadedSession,OldGeneration,LoadedGeneration,Attempts,Rescues,Captures;
            internal int SaveTick,Loaded,FirstTickObserved;
            internal bool Active,LoadArmed,OriginalEnded;
        }
        private static volatile PersistenceProof persistenceProof;
        private static bool persistenceLifecycleRegistered,persistenceTickRegistered;

        // One run-owned save is allowed. Off/on persistence cases therefore use
        // separate runner invocations, each with its own actual AutomationRun ID.
        private IEnumerable<AutomationStep> PersistenceSteps()
        {yield return new AutomationStep("actual-pending-construction-save-reload",TimeSpan.FromSeconds(480),Persistence,false);}

        private void ArmPersistence(PersistenceProof proof)
        {
            if(!persistenceLifecycleRegistered)
            {
                Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out var missions,out var availability),
                    "Persistence lifecycle capability unavailable: "+availability.Reason);
                Require(missions.TryRegisterObserver("acceptance-persistence",PersistenceStarted,PersistenceEnded,null,out var diagnostic),
                    "Persistence lifecycle observer unavailable: "+diagnostic.Reason);
                persistenceLifecycleRegistered=true;
            }
            if(!persistenceTickRegistered)
            {
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-persistence-before-recovery",ObservePersistenceFirstTick,
                    out string reason,order:-1000),"Persistence first-tick observer unavailable: "+reason);
                persistenceTickRegistered=true;
            }
            Require(persistenceProof==null,"Another save lifecycle proof is still active");
            persistenceProof=proof;
        }
        private static void PersistenceEnded(MissionLifecycleNotification notification)
        {
            var proof=persistenceProof;
            if(proof!=null && proof.Active && notification.Context.SessionId==proof.OriginalSession)proof.OriginalEnded=true;
        }
        private static void PersistenceStarted(MissionLifecycleNotification notification)
        {
            var proof=persistenceProof;var context=notification.Context;
            if(proof==null || !proof.Active || !proof.LoadArmed || notification.IsReplay || !context.IsSave ||
                context.SessionId==proof.OriginalSession || string.IsNullOrEmpty(context.FilePath))return;
            try
            {
                if(!string.Equals(System.IO.Path.GetFullPath(context.FilePath),proof.Path,StringComparison.OrdinalIgnoreCase))return;
                proof.LoadArmed=false;
                Require(proof.OriginalEnded,"Owned save started without retiring the original fixture mission");
                // Only fixture metadata is rebound. The real Plugin lifecycle owns
                // archive staging, world identity and the restored rescue receipts.
                var fixture=proof.Fixture;
                fixture.session=context.SessionId;fixture.stopped=false;proof.LoadedSession=context.SessionId;
                fixture.BeginAutomaticWorkerMoves(proof.Actor,proof.Before.RequestedX,proof.Before.RequestedY);
                Plugin.SetGeneralFixtureSession(fixture.expectedEnabled);
                Require(Plugin.GeneralObserver.IsActive==fixture.expectedEnabled,"Saved rescue settings were not restored to the requested mode");
                // OnStart observer ordering is deliberately irrelevant: the Plugin
                // may stage its archive before OR after this callback. Only the
                // first actual tick below inspects that staged record.
                Volatile.Write(ref proof.Loaded,1);
                fixture.log("persistence-owned-start oldSession="+proof.OriginalSession+" loadedSession="+context.SessionId+
                    " restoredFeatureActive="+Plugin.GeneralObserver.IsActive+" fixtureMetadataRebound=true");
            }
            catch(Exception error){proof.Fault=error.ToString();Volatile.Write(ref proof.Loaded,1);}
        }
        private static void ObservePersistenceFirstTick(int tick)
        {
            var proof=persistenceProof;
            if(proof==null || !proof.Active || Volatile.Read(ref proof.Loaded)==0 || Volatile.Read(ref proof.FirstTickObserved)!=0)return;
            try
            {
                var fixture=proof.Fixture;
                Require(proof.Fault==null && !fixture.stopped && fixture.session==proof.LoadedSession,
                    "Owned save fixture failed before its first real loaded tick");
                Require(WorldMutationEvents.TryGetCurrentWorld(out long actualSession,out long generation) && actualSession==proof.LoadedSession,
                    "Loaded causal candidate was not rebound to the actual new native world");
                proof.LoadedGeneration=generation;
                fixture.CheckPersistenceOriginal(proof,true);
                var observer=Plugin.GeneralObserver;
                Require(observer.IsActive==fixture.expectedEnabled && observer.PendingCount==(fixture.expectedEnabled?1:0),
                    "Loaded causal pending record/settings did not survive until the first recovery tick");
                Require(observer.AttemptCount==proof.Attempts && observer.RescueCount==proof.Rescues && fixture.automaticWorkerMoves.OwnBegins==0,
                    "Recovery ran before the registered first-loaded-tick assertion");
                fixture.log("persistence-first-loaded-tick tick="+tick+" oldSession="+proof.OriginalSession+
                    " newSession="+actualSession+" newGeneration="+generation+" pending="+observer.PendingCount+
                    " originalIdentityHealthTribeGoalOccupancy=true recoveryAttempts=0");
            }
            catch(Exception error){proof.Fault=error.ToString();}
            finally{Volatile.Write(ref proof.FirstTickObserved,1);}
        }
        private static string PersistenceSavePath(AutomationRun run)
        {
            // Pinned SDK test metadata only; no production API or inferred run ID.
            var id=typeof(AutomationRun).GetField("id",BindingFlags.Instance|BindingFlags.NonPublic);
            Require(id!=null && !id.IsStatic && id.FieldType==typeof(Guid),"AutomationRun ID reflection contract changed");
            var value=(Guid)id.GetValue(run);Require(value!=Guid.Empty,"AutomationRun has an empty save ownership ID");
            string directory=System.IO.Path.GetFullPath(ConfigSettings.GetSavesPath());
            string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(directory,"SerpAcceptance-"+value.ToString("N")+".sav"));
            Require(path.StartsWith(directory.TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase),"Owned acceptance save escaped the configured Saves directory");
            Require(!File.Exists(path),"Owned acceptance save already exists; no overwrite or repeated save in this run is allowed");
            return path;
        }
        private IEnumerable<AutomationWait> Persistence(AutomationRun run)
        {
            var proof=new PersistenceProof{Fixture=this,OriginalSession=session,Path=PersistenceSavePath(run),Active=true};
            ArmPersistence(proof);
            try
            {
                run.InSimulation(run.CurrentMission,()=>{
                    Plugin.SetGeneralFixtureSession(false);FindSite();SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,cx,cy);return true;
                });
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,16);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                // Drain Director's outstanding queue outside EngineInterface's
                // monitor, before creating the pending construction proof.
                yield return AutomationWait.Until("quiescent paused director before pending construction",()=>Director.instance.SafeToSave());
                Director.instance.FinishedSaving();
                run.InSimulation(run.CurrentMission,()=>{
                    VerifyLiveAndRemember();proof.Actor=actors[0];
                    Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(proof.Actor.Tribe,cx+10,cy),
                        "Original public command was rejected before pending construction");
                    proof.Before=runtime.SnapshotUnit(proof.Actor.Id);
                    var before=proof.Before;
                    Require(before!=null && before.Unsupported==null && before.Job==null && before.Command!=null &&
                        (before.Flags==2 || before.Flags==4) && before.Substep==8 && before.X==before.NextX && before.Y==before.NextY &&
                        (before.RequestedX!=before.X || before.RequestedY!=before.Y),
                        "Actual public move did not publish a qualified original ordinary command route");
                    BeginAutomaticWorkerMoves(proof.Actor,before.RequestedX,before.RequestedY);
                    Plugin.SetGeneralFixtureSession(expectedEnabled);var observer=Plugin.GeneralObserver;
                    Require(observer.IsActive==expectedEnabled,"Pending-save source activation mismatch");
                    proof.Attempts=observer.AttemptCount;proof.Rescues=observer.RescueCount;proof.Captures=observer.CaptureCount;
                    Require(WorldMutationEvents.TryGetCurrentWorld(out long actualSession,out proof.OldGeneration) && actualSession==session,
                        "Pending-save original native world is unavailable");
                    GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,eMappers.MAPPER_DRAWBRIDGE,
                        BuildingScales.GetScale(eMappers.MAPPER_DRAWBRIDGE),0,true,true);
                    Require(!HasStructuralExit(proof.Actor),"Actual construction did not establish the pending command source trap");
                    CheckPersistenceOriginal(proof,true);
                    Require(observer.AttemptCount==proof.Attempts && observer.RescueCount==proof.Rescues && automaticWorkerMoves.OwnBegins==0,
                        "Pending command was recovered synchronously inside its writer");
                    Require(observer.PendingCount==(expectedEnabled?1:0) && (!expectedEnabled || observer.CaptureCount>proof.Captures),
                        "Actual construction did not produce exactly the expected pending source");
                    Require(SimulationTickEvents.TryGetCurrentTick(out proof.SaveTick),"Native save source clock is unavailable");
                    log("persistence-pending-before-save enabled="+expectedEnabled+" unit="+proof.Actor.Id+"/"+proof.Actor.Global+
                        " tick="+proof.SaveTick+" pending="+observer.PendingCount+" originalGoal="+before.RequestedX+","+before.RequestedY+
                        " actualWriter=true directRescue=false nativeTicksAfterMutation=0");return true;
                });
                yield return AutomationWait.Until("quiescent paused director for actual pending save",()=>Director.instance.SafeToSave());
                Director.instance.FinishedSaving();
                var screen=CameraControls2D.instance.getScreenXYForSaveCentring(1,1);
                int centreX=-1,centreY=-1;GameMap.instance.getRotationCentre(ref centreX,ref centreY);
                // This ordinary writer owns its own SafeToSave and native threadLock.
                // No outer InSimulation lock or raw archive writer is used.
                Require(EngineInterface.SaveSaveGame(proof.Path,screen.x,screen.y,centreX,centreY,false,false,false),
                    "Actual native pending-save writer refused the atomic fixture archive");
                Require(File.Exists(proof.Path) && new FileInfo(proof.Path).Length>0,"Owned native save was not materialized");
                run.InSimulation(run.CurrentMission,()=>{
                    CheckPersistenceOriginal(proof,true);
                    Require(SimulationTickEvents.TryGetCurrentTick(out int tick) && tick==proof.SaveTick &&
                        Plugin.GeneralObserver.AttemptCount==proof.Attempts && Plugin.GeneralObserver.RescueCount==proof.Rescues &&
                        Plugin.GeneralObserver.PendingCount==(expectedEnabled?1:0),"Simulation/pending proof changed while writing the paused save");
                    log("persistence-native-save path="+proof.Path+" unchangedTick="+tick+" pending="+Plugin.GeneralObserver.PendingCount);return true;
                });
                foreach(var wait in PersistenceQuitToMenu(run))yield return wait;
                proof.LoadArmed=true;
                foreach(var wait in run.LoadSave(proof.Path))yield return wait;
                Require(Volatile.Read(ref proof.Loaded)!=0 && proof.Fault==null && run.CurrentMission!=null &&
                    run.CurrentMission.SessionId==proof.LoadedSession && proof.LoadedSession!=proof.OriginalSession,
                    "Owned save did not complete its genuine new managed lifecycle: "+proof.Fault);
                yield return run.WaitForPlayState(run.CurrentMission);
                if(MainViewModel.Instance.Show_HUD_Briefing)MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
                yield return AutomationWait.Until("loaded pending-save briefing closed",()=>!MainViewModel.Instance.Show_HUD_Briefing);
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                bool arrived=false;
                for(int sample=0;sample<(expectedEnabled?160:8);sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    arrived=run.InSimulation(run.CurrentMission,()=>{
                        Require(proof.Fault==null,"Loaded pending proof failed: "+proof.Fault);
                        return MovementOutcome(proof.Actor,proof.Before.RequestedX,proof.Before.RequestedY);
                    });
                    if(expectedEnabled && arrived)break;
                }
                yield return run.WaitForTicks(run.CurrentMission,8);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    Require(Volatile.Read(ref proof.FirstTickObserved)!=0 && proof.Fault==null,"No valid first-loaded-tick proof: "+proof.Fault);
                    CheckActor(proof.Actor,true);var observer=Plugin.GeneralObserver;var moves=automaticWorkerMoves;
                    if(expectedEnabled)Require(arrived && moves.Fault==null && moves.OwnBegins==1 && moves.OwnSuccesses==1 &&
                        observer.AttemptCount==proof.Attempts+1 && observer.RescueCount==proof.Rescues+1,
                        "Restored pending command did not produce exactly one own rescue and physical original-goal completion");
                    else Require(moves.OwnBegins==0 && observer.AttemptCount==proof.Attempts && observer.RescueCount==proof.Rescues,
                        "Disabled save/load inferred an own recovery without a causal record");
                    log("persistence-result enabled="+expectedEnabled+" oldSession="+proof.OriginalSession+" newSession="+proof.LoadedSession+
                        " oldGeneration="+proof.OldGeneration+" newGeneration="+proof.LoadedGeneration+" ownCalls="+moves.OwnBegins+
                        " originalGoalArrived="+arrived+" baselineAtSource="+AtSource(proof.Actor)+" baselineExit="+HasStructuralExit(proof.Actor)+
                        " identityHealthTribeOccupancy=true nakedOnLoadRepairCovered=false");
                    moves.Active=false;Plugin.SetGeneralFixtureSession(false);RetireActors();return true;
                });
            }
            finally
            {
                proof.Active=false;proof.LoadArmed=false;
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(persistenceProof,proof))persistenceProof=null;
            }
        }
        private unsafe void CheckPersistenceOriginal(PersistenceProof proof,bool atOriginalSource)
        {
            CheckActor(proof.Actor,true);var before=proof.Before;var unit=Resolve(proof.Actor);
            Require(unit->r_TribeId==before.Tribe && runtime.Native.TryTribeGlobal(before.Tribe,out uint global) && global==before.TribeGlobalId,
                "Saved original tribe lifetime changed");
            Require(unit->r_TargetTilePositionX2==before.RequestedX && unit->r_TargetTilePositionY2==before.RequestedY,
                "Saved original authoritative native goal changed");
            IntPtr pointer=(IntPtr)unit;
            Require(before.Command!=null && before.Command.Matches(unit->r_UnitChimp,unit->r_AIState,
                offset=>runtime.Native.JobWord((GameUnit*)pointer,offset),offset=>runtime.Native.JobDword((GameUnit*)pointer,offset)),
                "Saved original command purpose/AI metadata changed before recovery");
            if(atOriginalSource)Require(AtSource(proof.Actor) && !HasStructuralExit(proof.Actor) &&
                unit->r_PathPlanStateBitFlags==before.Flags && unit->r_MovementSubstep==before.Substep,
                "Pending source/active route advanced before its saved recovery boundary");
        }
        private IEnumerable<AutomationWait> PersistenceQuitToMenu(AutomationRun run)
        {
            Require(MainViewModel.viewModelLoaded && MainViewModel.Instance.HUDmain!=null && MainViewModel.Instance.HUDIngameMenu!=null,
                "Actual pending-save mission menu unavailable");
            var view=MainViewModel.Instance;
            if(view.HUDIngameMenu.restartSkirmishMapInfo!=null)view.HUDIngameMenu.restartSkirmishMapInfo=null;
            view.HUDmain.InGameOptions(null,null);
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(300));
            view.ButtonIngameMenuCommand.Execute("6");
            yield return AutomationWait.Until("actual pending-save quit confirmation",PersistenceConfirmationVisible);
            view.ButtonConfirmationCommand.Execute("1");
            yield return AutomationWait.Until("actual pending-save mission ended",()=>run.CurrentMission==null && FatControler.currentScene==Enums.SceneIDS.FrontEnd);
            for(int attempt=0;attempt<10 && !RescueScenarioSeries.MenuReady(run);attempt++)
            {
                yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
                if(!MainViewModel.viewModelLoaded)continue;
                view=MainViewModel.Instance;
                if(PersistenceConfirmationVisible())view.ButtonConfirmationCommand.Execute("1");
                else if(view.FRONTMultiplayer!=null && view.FRONTMultiplayer.panelActive && view.Show_MPGameCreation)view.FRONTMultiplayer.ButtonClicked("Back");
                else if(view.FrontEndMenu!=null)view.FrontEndMenu.ButtonClicked("BackMain");
            }
            yield return AutomationWait.Until("ready main menu before owned save load",()=>RescueScenarioSeries.MenuReady(run));
        }
        private static bool PersistenceConfirmationVisible()
        {
            if(!MainViewModel.viewModelLoaded)return false;var view=MainViewModel.Instance;
            return view.Show_HUD_Confirmation || view.Show_HUD_ConfirmationMP || view.Show_HUD_ConfirmationSM || view.Show_HUD_ConfirmationSands;
        }
    }
}
