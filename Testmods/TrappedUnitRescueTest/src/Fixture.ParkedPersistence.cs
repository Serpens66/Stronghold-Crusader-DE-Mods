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
using APIShared.Units;
using APIShared.WorldMutations;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class ParkedPersistenceProof
        {
            internal Fixture Fixture;
            internal Actor Actor;
            internal UnitSnapshot Before;
            internal string Path,Fault;
            internal bool Active,HoldVeto,HasBank,LoadArmed,OriginalEnded,BarrierVerified;
            internal int BankX,BankY,SaveTick,Loaded,LoadedReads,FirstLoadedTick,VetoesBeforeLoad,ReleasedPres,ReleasedPosts;
            internal long OriginalSession,LoadedSession,OldGeneration,LoadedGeneration,Attempts,Rescues,Captures;
            internal byte[] SavedOwned;
            internal ParkedMoveScope Scope;
            internal readonly List<UnitMoveRunResult> VetoResults=new List<UnitMoveRunResult>();
            internal readonly HashSet<long> VetoFrames=new HashSet<long>();
        }
        // Read-only, structurally checked contract of the selected0.74 SDK.
        // Legacy Pre precedes public final-original observers, so those observers
        // cannot identify a vetoed call. A nested foreign frame has no bound Run.
        private sealed class ParkedMoveScope
        {
            private readonly object broker;
            private readonly MethodInfo existing;
            private readonly FieldInfo stateFrame,stateRun,frameParent,frameRun,frameWorld,frameId;
            private readonly FieldInfo runParent,runOwner,runUnit,runX,runY,runExtra,runWrappers,runOriginals,runResult;
            private readonly FieldInfo worldSession,worldGeneration;
            private static FieldInfo Field(Type type,string name,Type expected)
            {
                var field=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                Require(field!=null && !field.IsStatic && field.FieldType==expected,"Parked-save SDK scope field changed: "+type.FullName+"."+name);
                return field;
            }
            internal ParkedMoveScope()
            {
                var assembly=typeof(UnitMoveEvents).Assembly;
                var runtimeType=assembly.GetType("APIShared.Units.UnitMoveRuntime",true);
                var property=runtimeType.GetProperty("Broker",BindingFlags.Static|BindingFlags.NonPublic);
                Require(property!=null && property.GetIndexParameters().Length==0,"Parked-save SDK Broker accessor changed");
                broker=property.GetValue(null);Require(broker!=null,"Parked-save SDK Broker unavailable");
                var brokerType=assembly.GetType("APIShared.Units.UnitMoveBroker",true);
                Require(broker.GetType()==brokerType && property.PropertyType==brokerType,"Parked-save SDK broker identity changed");
                var frameType=assembly.GetType("APIShared.Units.UnitMoveBroker+Frame",true);
                var runType=assembly.GetType("APIShared.Units.UnitMoveBroker+Run",true);
                var worldType=assembly.GetType("APIShared.Units.UnitMoveWorld",true);
                existing=brokerType.GetMethod("Existing",BindingFlags.Instance|BindingFlags.NonPublic);
                Require(existing!=null && existing.GetParameters().Length==0 &&
                    existing.ReturnType.FullName=="APIShared.Units.UnitMoveBroker+ThreadState","Parked-save readonly thread lookup changed");
                stateFrame=Field(existing.ReturnType,"Frame",frameType);stateRun=Field(existing.ReturnType,"Run",runType);
                frameParent=Field(frameType,"Parent",frameType);frameRun=Field(frameType,"Run",runType);
                frameWorld=Field(frameType,"World",worldType);frameId=Field(frameType,"Id",typeof(long));
                runParent=Field(runType,"Parent",frameType);runOwner=Field(runType,"Owner",typeof(string));
                runUnit=Field(runType,"Unit",typeof(int));runX=Field(runType,"X",typeof(int));runY=Field(runType,"Y",typeof(int));
                runExtra=Field(runType,"Extra",typeof(int));runWrappers=Field(runType,"Wrappers",typeof(int));
                runOriginals=Field(runType,"Originals",typeof(int));runResult=Field(runType,"Result",typeof(UnitMoveRunResult));
                Require(runResult.IsInitOnly,"Parked-save SDK result must remain the immutable run-owned reference");
                worldSession=Field(worldType,"Session",typeof(long));worldGeneration=Field(worldType,"Generation",typeof(long));
                // Existing() only reads the current thread dictionary; never call Thread(),
                // replace a broker, enter a frame or mutate any SDK-private state.
                existing.Invoke(broker,null);
            }
            internal bool TryRead(UnitMoveHereEventArgs args,ParkedPersistenceProof proof,out long invocation,out UnitMoveRunResult result)
            {
                invocation=0;result=null;
                var state=existing.Invoke(broker,null);if(state==null)return false;
                var frame=stateFrame.GetValue(state);if(frame==null || frameParent.GetValue(frame)!=null)return false;
                var run=frameRun.GetValue(frame);
                if(run==null || !ReferenceEquals(run,stateRun.GetValue(state)) || runParent.GetValue(run)!=null ||
                    (string)runOwner.GetValue(run)!=Plugin.RescueOwnerGuid || (int)runUnit.GetValue(run)!=proof.Actor.Id ||
                    (int)runX.GetValue(run)!=proof.Before.RequestedX || (int)runY.GetValue(run)!=proof.Before.RequestedY ||
                    (int)runExtra.GetValue(run)!=0 || args.UnitId!=proof.Actor.Id || args.TileX!=proof.Before.RequestedX ||
                    args.TileY!=proof.Before.RequestedY || args.Unknown!=0 || (int)runWrappers.GetValue(run)!=1 ||
                    (int)runOriginals.GetValue(run)!=(args.Phase==EventHookPhase.Pre?0:1))return false;
                var world=frameWorld.GetValue(frame);
                if(!WorldMutationEvents.TryGetCurrentWorld(out long session,out long generation) ||
                    session!=proof.Fixture.session || (long)worldSession.GetValue(world)!=session ||
                    (long)worldGeneration.GetValue(world)!=generation)return false;
                invocation=(long)frameId.GetValue(frame);result=(UnitMoveRunResult)runResult.GetValue(run);return result!=null;
            }
        }
        private static volatile ParkedPersistenceProof parkedPersistence;
        private static bool parkedPersistenceRegistered;
        private IEnumerable<AutomationStep> ParkedPersistenceSteps()
        {yield return new AutomationStep("actual-owned-flags5-save-reload",TimeSpan.FromSeconds(480),ParkedPersistence,false);}

        private void ArmParkedPersistence(ParkedPersistenceProof proof)
        {
            if(!parkedPersistenceRegistered)
            {
                Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out var missions,out var available),available.Reason);
                Require(missions.TryRegisterObserver("acceptance-parked-persistence",ParkedPersistenceStarted,ParkedPersistenceEnded,null,out var diagnostic),diagnostic.Reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-parked-persistence-before-recovery",ObserveParkedLoadedTick,
                    out string reason,order:-1000),reason);
                parkedPersistenceRegistered=true;
            }
            Require(parkedPersistence==null,"Another parked-save acceptance is active");parkedPersistence=proof;
        }
        private void ObserveParkedLegacyMove(UnitMoveHereEventArgs args,ParkedPersistenceProof proof)
        {
            if(!proof.Active || proof.Actor==null || proof.Before==null || args==null ||
                !runtime.Workers.IsExecutingNativeCall || args.UnitId!=proof.Actor.Id ||
                args.Phase!=EventHookPhase.Pre && args.Phase!=EventHookPhase.Post)return;
            try
            {
                if(!proof.Scope.TryRead(args,proof,out long invocation,out var result))return;
                if(args.Phase==EventHookPhase.Post){proof.ReleasedPosts++;return;}
                CheckParkedPersistenceActor(proof,false);
                if(!proof.HoldVeto){proof.ReleasedPres++;return;}
                Require(proof.VetoResults.Count<32 && proof.VetoFrames.Add(invocation),"Unbounded or duplicate parked-save veto frame");
                // This is an actual public cancellation. Do not set ReturnValue,
                // fabricate Native0, throw or alter args/AI/goal/pose/flags.
                args.SkipOriginalFunction=true;proof.VetoResults.Add(result);
                log("parked-save-public-veto frame="+invocation+" session="+session+" unit="+proof.Actor.Id+"/"+proof.Actor.Global+
                    " originalGoal="+proof.Before.RequestedX+","+proof.Before.RequestedY+" ownBoundRoot=true nativeResultNotInvented=true");
            }
            catch(Exception error){proof.Fault=error.ToString();}
        }
        private unsafe void CheckParkedPersistenceActor(ParkedPersistenceProof proof,bool committed)
        {
            CheckActor(proof.Actor,true);var before=proof.Before;var unit=Resolve(proof.Actor);var ledger=runtime.Workers.Ledger;
            WorkerResumeTicket ticket=null;
            Require(ledger.Count==1 && ledger.TryGetOwnedByUnit(proof.Actor.Id,out ticket) && ticket.Kind==ResumeReceiptKind.Command &&
                ticket.UnitGlobalId==proof.Actor.Global && ticket.RequestedX==before.RequestedX && ticket.RequestedY==before.RequestedY,
                "Parked-save lost the exact original owned command receipt");
            Require(!committed || ledger.ReservedCount==0 && ledger.TryGetParkedByUnit(proof.Actor.Id,out _),"Parked-save receipt is not durably committed");
            IntPtr pointer=(IntPtr)unit;
            Require(before.Command!=null && ticket.Command!=null && before.Command.Matches(unit->r_UnitChimp,unit->r_AIState,
                offset=>runtime.Native.JobWord((GameUnit*)pointer,offset),offset=>runtime.Native.JobDword((GameUnit*)pointer,offset)) &&
                ticket.Command.Matches(unit->r_UnitChimp,unit->r_AIState,
                offset=>runtime.Native.JobWord((GameUnit*)pointer,offset),offset=>runtime.Native.JobDword((GameUnit*)pointer,offset)),
                "Parked-save original AI/command purpose changed");
            Require(unit->r_TargetTilePositionX2==before.RequestedX && unit->r_TargetTilePositionY2==before.RequestedY &&
                runtime.Native.TryTribeGlobal(before.Tribe,out uint stamp) && stamp==before.TribeGlobalId,
                "Parked-save goal or native tribe lifetime changed");
            Require(unit->r_PathPlanStateBitFlags==5 && NativeRelocation.IsNormalizedPose(unit,(int)unit->r_CurrentPositionTileId,
                runtime.Native.JobDword(unit,0x740)),"Parked-save flags5 barrier is not a coherent native normalized pose");
            // Packed Next73C is inactive after19BA40; SS8/NextXY/raw740 are authoritative.
            if(!proof.HasBank)
            {
                Require(!AtSource(proof.Actor) && HasStructuralExit(proof.Actor) &&
                    Math.Max(Math.Abs(unit->r_CurrentTilePositionX-before.RequestedX),Math.Abs(unit->r_CurrentTilePositionY-before.RequestedY))>2,
                    "Parked-save needs the actual relocated local bank separated from the original goal");
                proof.BankX=unit->r_CurrentTilePositionX;proof.BankY=unit->r_CurrentTilePositionY;proof.HasBank=true;
            }
            Require(unit->r_CurrentTilePositionX==proof.BankX && unit->r_CurrentTilePositionY==proof.BankY,
                "Owned parked unit moved from its recorded bank before veto release");
        }
        private static void CheckParkedVetoResults(ParkedPersistenceProof proof)
        {
            Require(proof.VetoResults.Count>0,"No actual bound public Pre veto occurred");
            foreach(var result in proof.VetoResults)
                Require(result.Status==UnitMoveRunStatus.NoOriginal && !result.NativeInvoked && !result.NativeReturned &&
                    result.WrapperReturned && result.WrapperFailure==null && result.Failure==null && result.IsCurrentWorld,
                    "Public cancellation was not a completed current NoOriginal wrapper; never count it as Native0");
        }
        private static bool ParkedSameBytes(byte[] left,byte[] right)
        {if(left==null || right==null || left.Length!=right.Length)return false;for(int i=0;i<left.Length;i++)if(left[i]!=right[i])return false;return true;}
        private static void ParkedPersistenceEnded(MissionLifecycleNotification notification)
        {var proof=parkedPersistence;if(proof!=null && proof.Active && notification.Context.SessionId==proof.OriginalSession)proof.OriginalEnded=true;}
        private static void ParkedPersistenceStarted(MissionLifecycleNotification notification)
        {
            var proof=parkedPersistence;var context=notification.Context;
            if(proof==null || !proof.Active || !proof.LoadArmed || notification.IsReplay || !context.IsSave ||
                context.SessionId==proof.OriginalSession || string.IsNullOrEmpty(context.FilePath))return;
            try
            {
                if(!string.Equals(System.IO.Path.GetFullPath(context.FilePath),proof.Path,StringComparison.OrdinalIgnoreCase))return;
                Require(proof.OriginalEnded,"Parked-save load did not end the original mission");
                proof.LoadArmed=false;proof.LoadedSession=context.SessionId;
                var fixture=proof.Fixture;fixture.session=context.SessionId;fixture.stopped=false;
                fixture.BeginAutomaticWorkerMoves(proof.Actor,proof.Before.RequestedX,proof.Before.RequestedY);
                Plugin.SetGeneralFixtureSession(true);
                Volatile.Write(ref proof.Loaded,1);
            }
            catch(Exception error){proof.Fault=error.ToString();Volatile.Write(ref proof.Loaded,1);}
        }
        private static void ObserveParkedLoadedTick(int tick)
        {
            var proof=parkedPersistence;
            if(proof==null || !proof.Active || Volatile.Read(ref proof.Loaded)==0 || proof.LoadedReads>=2 || proof.Fault!=null)return;
            try
            {
                var fixture=proof.Fixture;fixture.CheckParkedPersistenceActor(proof,true);
                long session=0,generation=0;
                Require(!fixture.stopped && fixture.session==proof.LoadedSession &&
                    WorldMutationEvents.TryGetCurrentWorld(out session,out generation) && session==proof.LoadedSession,
                    "Parked-save was not restored to its genuine new native world");
                proof.LoadedGeneration=generation;
                var observer=Plugin.GeneralObserver;var moves=fixture.automaticWorkerMoves;
                Require(observer.IsActive && observer.PendingCount==0 && observer.AttemptCount==proof.Attempts+1 &&
                    observer.RescueCount==proof.Rescues && observer.CaptureCount==proof.Captures+1 && moves.OwnBegins==0,
                    "Loaded owned park caused an early original, another causal capture or another relocation attempt");
                if(proof.LoadedReads==0)
                {
                    Require(fixture.runtime.Workers.Ledger.TrySerialize(out var loaded) && ParkedSameBytes(loaded,proof.SavedOwned),
                        "Actual save/load changed the original serialized owned command/park before its first tick");
                    proof.FirstLoadedTick=tick;proof.LoadedReads=1;
                    fixture.log("parked-save-first-loaded-tick tick="+tick+" session="+session+" generation="+generation+
                        " flags5=true originalAICommandGoal=true bank="+proof.BankX+","+proof.BankY+" serializedOwnedIdentical=true nativeOriginals=0");
                }
                else
                {
                    Require(unchecked((uint)(tick-proof.FirstLoadedTick))==1,"Parked-save barrier did not observe the consecutive actual loaded tick");
                    Require(proof.VetoResults.Count>proof.VetoesBeforeLoad,"No actual loaded retry was protected by the public veto");
                    CheckParkedVetoResults(proof);
                    proof.LoadedReads=2;proof.BarrierVerified=true;proof.HoldVeto=false;
                    fixture.log("parked-save-barrier-verified tick="+tick+" flags5=true unchangedAI101=true unchangedGoal=true"+
                        " bankOccupancy=true actualNativeTickElapsed=true releasedOnlyManagedVeto=true");
                }
            }
            catch(Exception error){proof.Fault=error.ToString();}
        }
        private IEnumerable<AutomationWait> ParkedPersistence(AutomationRun run)
        {
            Require(expectedEnabled,"Owned flags5 persistence is an enabled-feature acceptance; no artificial OFF park is created");
            var proof=new ParkedPersistenceProof{Fixture=this,OriginalSession=session,Path=PersistenceSavePath(run),Active=true,Scope=new ParkedMoveScope()};
            ArmParkedPersistence(proof);
            using(var veto=UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(args=>ObserveParkedLegacyMove(args,proof)))
            try
            {
                run.InSimulation(run.CurrentMission,()=>{Plugin.SetGeneralFixtureSession(false);FindSite();SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,cx,cy);proof.Actor=actors[0];return true;});
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,12);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                yield return AutomationWait.Until("quiescent director before actual owned-park source",()=>Director.instance.SafeToSave());
                Director.instance.FinishedSaving();
                run.InSimulation(run.CurrentMission,()=>{
                    VerifyLiveAndRemember();Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(proof.Actor.Tribe,cx+10,cy),"Native command before owned park was rejected");
                    proof.Before=runtime.SnapshotUnit(proof.Actor.Id);
                    Require(proof.Before!=null && proof.Before.Unsupported==null && proof.Before.Job==null && proof.Before.Command!=null &&
                        (proof.Before.Flags==2 || proof.Before.Flags==4) && proof.Before.Substep==8,
                        "Owned-park source lacks its actual ordinary command");
                    BeginAutomaticWorkerMoves(proof.Actor,proof.Before.RequestedX,proof.Before.RequestedY);
                    Plugin.SetGeneralFixtureSession(true);proof.HoldVeto=true;
                    var observer=Plugin.GeneralObserver;proof.Attempts=observer.AttemptCount;proof.Rescues=observer.RescueCount;proof.Captures=observer.CaptureCount;
                    Require(WorldMutationEvents.TryGetCurrentWorld(out long actualSession,out proof.OldGeneration) && actualSession==session,"Owned-park original world unavailable");
                    GameBuildingManagerAPI.Instance.CreatePrefab(owner,cx,cy,eMappers.MAPPER_DRAWBRIDGE,BuildingScales.GetScale(eMappers.MAPPER_DRAWBRIDGE),0,true,true);
                    Require(!HasStructuralExit(proof.Actor) && observer.PendingCount==1 && observer.AttemptCount==proof.Attempts,
                        "Real bridge did not establish exactly one deferred owned-park candidate");return true;
                });
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                yield return run.WaitForTicks(run.CurrentMission,1);
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                yield return AutomationWait.Until("quiescent director for actual flags5 save",()=>Director.instance.SafeToSave());
                Director.instance.FinishedSaving();
                run.InSimulation(run.CurrentMission,()=>{
                    Require(proof.Fault==null,proof.Fault);CheckParkedPersistenceActor(proof,true);CheckParkedVetoResults(proof);
                    Require(proof.VetoResults.Count<=5,"Too many retries elapsed before pausing; do not save an exhausted waiting receipt");
                    var observer=Plugin.GeneralObserver;
                    Require(observer.PendingCount==0 && observer.AttemptCount==proof.Attempts+1 && observer.RescueCount==proof.Rescues &&
                        observer.CaptureCount==proof.Captures+1 && automaticWorkerMoves.OwnBegins==0 && proof.ReleasedPosts==0,
                        "Owned park was not produced by one real deferred relocation plus genuine NoOriginal cancellations");
                    Require(runtime.Workers.Ledger.TrySerialize(out proof.SavedOwned),"Committed flags5 command could not serialize");
                    Require(SimulationTickEvents.TryGetCurrentTick(out proof.SaveTick),"Owned-park native save tick unavailable");
                    proof.VetoesBeforeLoad=proof.VetoResults.Count;
                    log("parked-save-before-save tick="+proof.SaveTick+" unit="+proof.Actor.Id+"/"+proof.Actor.Global+
                        " bank="+proof.BankX+","+proof.BankY+" flags5=true committedOwned=1 reserved=0 pending=0"+
                        " noOriginalVetoes="+proof.VetoesBeforeLoad+" actualNative0=0 originalGoal="+proof.Before.RequestedX+","+proof.Before.RequestedY);return true;
                });
                var screen=CameraControls2D.instance.getScreenXYForSaveCentring(1,1);int centreX=-1,centreY=-1;
                GameMap.instance.getRotationCentre(ref centreX,ref centreY);
                Require(EngineInterface.SaveSaveGame(proof.Path,screen.x,screen.y,centreX,centreY,false,false,false),"Actual flags5 native save was refused");
                Require(File.Exists(proof.Path) && new FileInfo(proof.Path).Length>0,"Owned flags5 save was not materialized");
                run.InSimulation(run.CurrentMission,()=>{
                    CheckParkedPersistenceActor(proof,true);CheckParkedVetoResults(proof);
                    Require(SimulationTickEvents.TryGetCurrentTick(out int tick) && tick==proof.SaveTick &&
                        runtime.Workers.Ledger.TrySerialize(out var unchanged) && ParkedSameBytes(unchanged,proof.SavedOwned),
                        "Simulation or committed receipt changed while writing the actual paused save");return true;
                });
                foreach(var wait in PersistenceQuitToMenu(run))yield return wait;
                proof.LoadArmed=true;
                foreach(var wait in run.LoadSave(proof.Path))yield return wait;
                Require(Volatile.Read(ref proof.Loaded)!=0 && proof.Fault==null && run.CurrentMission!=null &&
                    run.CurrentMission.SessionId==proof.LoadedSession,"Actual flags5 save did not complete its new lifecycle: "+proof.Fault);
                yield return run.WaitForPlayState(run.CurrentMission);
                if(MainViewModel.Instance.Show_HUD_Briefing)MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
                yield return AutomationWait.Until("loaded flags5 briefing closed",()=>!MainViewModel.Instance.Show_HUD_Briefing);
                foreach(var wait in run.SetPause(run.CurrentMission,false))yield return wait;
                bool arrived=false;
                for(int sample=0;sample<180 && !arrived;sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,4);
                    arrived=run.InSimulation(run.CurrentMission,()=>{
                        Require(proof.Fault==null,"Loaded flags5 proof failed: "+proof.Fault);
                        return proof.BarrierVerified && MovementOutcome(proof.Actor,proof.Before.RequestedX,proof.Before.RequestedY);
                    });
                }
                foreach(var wait in run.SetPause(run.CurrentMission,true))yield return wait;
                run.InSimulation(run.CurrentMission,()=>{
                    var observer=Plugin.GeneralObserver;var moves=automaticWorkerMoves;
                    Require(proof.Fault==null && proof.BarrierVerified && arrived && moves.Fault==null && moves.OwnBegins==1 &&
                        moves.OwnSuccesses==1 && moves.OwnRefusals==0 && proof.ReleasedPres==1 && proof.ReleasedPosts==1,
                        "Restored flags5 original command did not resume through exactly one real Native1 and physical arrival");
                    Require(runtime.Workers.Ledger.Count==0 && observer.PendingCount==0 && observer.AttemptCount==proof.Attempts+1 &&
                        observer.CaptureCount==proof.Captures+1 && observer.RescueCount==proof.Rescues,
                        "Flags5 replay caused a second rescue/capture or retained stale ownership");
                    CheckActor(proof.Actor,true);CheckParkedVetoResults(proof);
                    log("parked-save-result oldSession="+proof.OriginalSession+" newSession="+proof.LoadedSession+
                        " oldGeneration="+proof.OldGeneration+" newGeneration="+proof.LoadedGeneration+" savedFlags5=true barrierSurvivedActualTick=true"+
                        " noOriginalVetoes="+proof.VetoResults.Count+" actualNative1="+moves.OwnSuccesses+" actualNative0="+moves.OwnRefusals+
                        " originalGoalArrived=true ledgerEmpty=true secondRescue=false workerJobSaveCovered=false genuineNative0SaveCovered=false");
                    moves.Active=false;Plugin.SetGeneralFixtureSession(false);RetireActors();return true;
                });
            }
            finally
            {
                proof.Active=false;proof.HoldVeto=false;proof.LoadArmed=false;
                if(automaticWorkerMoves!=null)automaticWorkerMoves.Active=false;
                if(ReferenceEquals(parkedPersistence,proof))parkedPersistence=null;
            }
        }
    }
}
