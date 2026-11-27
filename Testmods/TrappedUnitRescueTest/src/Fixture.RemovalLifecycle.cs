using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using APIShared.Units;
using APIShared.WorldMutations;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class RemovalLifecycleProof
        {
            internal Fixture Fixture;
            internal bool Active=true,ReplaceArmed,Ended,Started,FirstTick;
            internal long SourceSession,SourceWorld,NewSession,NewWorld,Removals;
            internal int OwnMoves,NewTicks;
            internal string Fault;
        }
        private static volatile RemovalLifecycleProof removalLifecycle;
        private static bool removalLifecycleRegistered;
        private IEnumerable<AutomationStep> RemovalLifecycleSteps()
        {
            yield return new AutomationStep("causal-removal-lifetime-or-world-retirement",TimeSpan.FromSeconds(1200),RemovalLifecycleCase,false);
        }
        private void ArmRemovalLifecycle(RemovalLifecycleProof p)
        {
            if(!removalLifecycleRegistered)
            {
                Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out var missions,out var available),available.Reason);
                Require(missions.TryRegisterObserver("acceptance-removal-lifecycle",RemovalLifecycleStarted,RemovalLifecycleEnded,null,out var diagnostic),diagnostic.Reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-removal-lifecycle-first",RemovalLifecycleTick,out string reason,order:-1000),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-removal-lifecycle-no-reissue",new RemovalLifecycleMoves(),out reason,
                    enabled:()=>removalLifecycle!=null && removalLifecycle.Active),reason);
                removalLifecycleRegistered=true;
            }
            Require(removalLifecycle==null,"Another removal lifecycle acceptance is active");removalLifecycle=p;
        }
        private sealed class RemovalLifecycleMoves : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext c)
            {
                var p=removalLifecycle;var a=p?.Fixture.civilianRemoval?.Actor;
                if(p==null || !p.Active || a==null || c==null || c.OriginOwnerGuid!=Plugin.RescueOwnerGuid)return null;
                // There are no own commands in the fresh control world: count all of them,
                // rather than confusing a naturally reused ID/global with the old lifetime.
                if(p.Started && c.SessionId==p.NewSession){p.OwnMoves++;return null;}
                if(c.UnitId!=a.Id || c.SessionId!=p.SourceSession || c.WorldGeneration!=p.SourceWorld)return null;
                if(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==c.SessionId && world==c.WorldGeneration &&
                    UnitAccess.TryGetById(a.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==a.Global)p.OwnMoves++;
                return null;
            }
        }
        private static void RemovalLifecycleEnded(MissionLifecycleNotification n)
        {
            var p=removalLifecycle;
            if(p!=null && p.Active && n.Context.SessionId==p.SourceSession)p.Ended=true;
        }
        private static void RemovalLifecycleStarted(MissionLifecycleNotification n)
        {
            var p=removalLifecycle;
            if(p==null || !p.Active || !p.ReplaceArmed || n.IsReplay)return;
            try
            {
                Require(p.Ended && !p.Started && !n.Context.IsSave && n.Context.SessionId!=p.SourceSession,
                    "World retirement did not reach one genuine new-game lifecycle");
                p.NewSession=n.Context.SessionId;p.Started=true;
                Plugin.SetGeneralFixtureSession(true);
            }
            catch(Exception error){p.Fault=error.Message;}
        }
        private static void RemovalLifecycleTick(int tick)
        {
            var p=removalLifecycle;
            if(p==null || !p.Active || !p.Started || p.Fault!=null)return;
            try
            {
                Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.NewSession && world!=p.SourceWorld,
                    "Replacement tick is not bound to the new actual native world");
                if(p.FirstTick)Require(world==p.NewWorld,"Replacement world changed again during bounded observation");
                else
                {
                    p.NewWorld=world;p.FirstTick=true;
                    Require(Plugin.GeneralObserver.PendingCount==0,"First replacement tick retained an old cause");
                    p.Fixture.log("removal-lifecycle-new-first-tick tick="+tick+" oldSession="+p.SourceSession+" newSession="+sid+
                        " oldWorld="+p.SourceWorld+" newWorld="+world+" oldCauseAbsent=true");
                }
                Require(Plugin.GeneralObserver.IsActive && Plugin.GeneralObserver.PendingCount==0 &&
                    Plugin.GeneralObserver.RemovalCount==p.Removals && p.OwnMoves==0,
                    "Ended-world cause replayed or survived into the active fresh world");
                p.NewTicks++;
            }
            catch(Exception error){p.Fault=error.Message;}
        }
        private void CheckLifecyclePending(RemovalLifecycleProof p)
        {
            CivilianRemovalWorld();
            Require(Plugin.GeneralObserver.TryCapturePending(out var records) && records.Length==1,"Lifecycle source lacks exactly one actual pending cause");
            CheckUnstartedRemovalRecord(records[0]);
            Require(Plugin.GeneralObserver.RemovalCount==p.Removals && p.OwnMoves==0,
                "Lifecycle source has already performed its own action");
        }
        private unsafe void MarkLifecycleOriginal(RemovalLifecycleProof p)
        {
            CheckLifecyclePending(p);var a=civilianRemoval.Actor;
            Require(UnitAccess.TryGetById(a.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==a.Global &&
                UnitAccess.IsReallyAlive(u) && u->r_UnitChimp==a.Type && u->r_ControllableForPlayerId==owner && u->r_CurrentHealth==a.Health,
                "External deletion lost the exact original live worker");
            // The public API requests Life3 only. Native cleanup must happen later.
            Require(APIShared.UnitAccess.TryGetById(a.Id,out _,out _) && GameUnitManagerAPI.Instance.DeleteUnitSafe(a.Id),"Regular public deletion refused");
            Require(UnitAccess.TryGetById(a.Id,out u,out _) && u!=null && u->r_GlobalId==a.Global &&
                u->r_AliveState==AliveState.MarkedForDeletion,"Regular public deletion did not mark original Life3");
            civilianRemoval.SawLife3=true;
            Require(Plugin.GeneralObserver.RemovalCount==p.Removals && p.OwnMoves==0,"External deletion invoked own rescue");
            log("removal-lifecycle-external-mark unit="+a.Id+"/"+a.Global+" actualLife3=true ownMarks=0 cleanupNotYetClaimed=true");
        }
        private IEnumerable<AutomationWait> RemovalLifecycleCase(AutomationRun run)
        {
            Require(expectedEnabled,"Lifecycle negative controls require the rescue enabled");
            bool replace=scenario=="rescue-removal-worldchange-on";
            Require(replace || scenario=="rescue-removal-death-on","Unknown removal lifecycle scenario");
            var p=new RemovalLifecycleProof{Fixture=this,SourceSession=session};ArmRemovalLifecycle(p);
            try
            {
                run.InSimulation(run.CurrentMission,()=>
                {
                    civilianRemoval=new CivilianRemovalProof();
                    Require(WorldMutationEvents.TryGetCurrentWorld(out civilianRemoval.Session,out civilianRemoval.World) && civilianRemoval.Session==session,"No lifecycle source world");
                    p.SourceWorld=civilianRemoval.World;SetupCivilianRemoval(true);return true;
                });
                foreach(var w in AwaitCivilianPopulation(run))yield return w;
                for(int sample=0;sample<6000 && civilianRemoval.Before==null;sample++)
                {
                    run.InSimulation(run.CurrentMission,()=>{AcquireCivilianRemoval(true);return true;});
                    if(civilianRemoval.Before!=null)break;
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,2);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                }
                Require(civilianRemoval.Before!=null,"No natural employed farmer qualified for lifecycle source");
                run.InSimulation(run.CurrentMission,()=>
                {
                    ApplyCivilianRemovalMoat();p.Removals=Plugin.GeneralObserver.RemovalCount;CheckLifecyclePending(p);
                    if(!replace)MarkLifecycleOriginal(p);return true;
                });
                if(replace)
                {
                    foreach(var w in PersistenceQuitToMenu(run))yield return w;
                    Require(p.Ended && run.CurrentMission==null,"Original pending world did not actually end");
                    civilianRemoval.Active=false;appleWorld?.Dispose();
                    Require(Plugin.GeneralObserver.PendingCount==0 && Plugin.GeneralObserver.RemovalCount==p.Removals && p.OwnMoves==0,
                        "Quit did not retire the old pending cause without action");
                    // A fresh coordinator avoids reusing per-lobby settings/roster snapshots.
                    var fresh=new Fixture(map,scenario,log,runtime,enable,suppression,workerDeletionInstalled,peaceTimeMinutes);
                    p.ReplaceArmed=true;
                    foreach(var w in fresh.Start(run))yield return w;
                    Require(p.Started && p.Fault==null && run.CurrentMission!=null && run.CurrentMission.SessionId==p.NewSession,
                        "Fresh-world lifecycle was not observed: "+p.Fault);
                    foreach(var w in fresh.Prepare(run))yield return w;
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,64);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    run.InSimulation(run.CurrentMission,()=>
                    {
                        Require(p.Fault==null && p.FirstTick && p.NewTicks>=64,p.Fault??"Fresh world lacked bounded tick evidence");
                        Require(Plugin.GeneralObserver.PendingCount==0 && Plugin.GeneralObserver.RemovalCount==p.Removals && p.OwnMoves==0,"Fresh world replayed original removal");
                        log("removal-lifecycle-result kind=worldchange oldSession="+p.SourceSession+" newSession="+p.NewSession+
                            " oldWorld="+p.SourceWorld+" newWorld="+p.NewWorld+" observedNewTicks="+p.NewTicks+" ownMarks=0 ownMoves=0 pending=0 idReuseNotRequired=true");return true;
                    });
                }
                else
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,64);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    run.InSimulation(run.CurrentMission,()=>
                    {
                        CivilianRemovalWorld();Require(civilianRemoval.Fault==null,civilianRemoval.Fault);
                        Require(civilianRemoval.SawLife3 && civilianRemoval.OriginalGone && Plugin.GeneralObserver.PendingCount==0 &&
                            Plugin.GeneralObserver.RemovalCount==p.Removals && p.OwnMoves==0,"Externally marked original was rescued, retained or not cleaned by native code");
                        ValidateRemovedCivilianChains();RepeatCivilianRemovalNoop();return true;
                    });
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,8);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    run.InSimulation(run.CurrentMission,()=>
                    {
                        CivilianRemovalWorld();Require(civilianRemoval.Fault==null,civilianRemoval.Fault);
                        Require(Plugin.GeneralObserver.RemovalCount==p.Removals && Plugin.GeneralObserver.PendingCount==0 && p.OwnMoves==0,"Settled no-op replayed the dead lifetime");
                        ValidateRemovedCivilianChains();
                        log("removal-lifecycle-result kind=external-deletion actualLife3=true nativeGone=true fullOldChainsFree=true ownMarks=0 ownMoves=0 settledNoop=true idReuseNotRequired=true");return true;
                    });
                }
            }
            finally
            {
                p.Active=false;p.ReplaceArmed=false;if(civilianRemoval!=null)civilianRemoval.Active=false;appleWorld?.Dispose();
                if(ReferenceEquals(removalLifecycle,p))removalLifecycle=null;
            }
        }
    }
}
