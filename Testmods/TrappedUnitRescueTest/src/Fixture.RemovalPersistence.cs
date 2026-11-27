using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Savegames;
using APIShared.Simulation;
using APIShared.Units;
using APIShared.WorldMutations;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class RemovalPersistenceProof
        {
            internal Fixture Fixture;internal string Path,Fault,LoadOutcome;
            internal bool Active=true,LoadArmed,Ended,FirstTick,MarkerBoundary,NativeGone;
            internal int Loaded,SaveTick,OwnMoves;internal long OriginalSession,LoadedSession,OriginalWorld,LoadedWorld,Removals,Attempts;
            internal byte[] PendingBytes;
        }
        private static volatile RemovalPersistenceProof removalPersistence;
        private static bool removalPersistenceRegistered;
        private IEnumerable<AutomationStep> RemovalPersistenceSteps()
        {yield return new AutomationStep("actual-v6-removal-pending-save-load",TimeSpan.FromSeconds(1200),RemovalPersistence,false);}
        private void CheckRemovalPersistenceSettings()
        {
            var field=typeof(Plugin).GetField("settings",BindingFlags.Static|BindingFlags.NonPublic);
            Require(field!=null && field.FieldType==typeof(Settings),"Removal persistence settings read contract changed");
            var value=(Settings)field.GetValue(null);
            Require(value!=null && value.EnableMod && value.EnableExperimentalRescue==expectedEnabled && Plugin.GeneralObserver.IsActive==expectedEnabled,
                "Removal persistence restored a different actual feature setting");
        }
        private void ArmRemovalPersistence(RemovalPersistenceProof p)
        {
            if(!removalPersistenceRegistered)
            {
                Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out var missions,out var availability),availability.Reason);
                Require(missions.TryRegisterObserver("acceptance-removal-persistence",RemovalPersistenceStarted,RemovalPersistenceEnded,null,out var diagnostic),diagnostic.Reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-removal-persistence-before",RemovalPersistenceBeforeTick,out string reason,order:-1000),reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-removal-persistence-after",RemovalPersistenceAfterTick,out reason,order:100),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-removal-persistence-no-replay",new RemovalPersistenceMoves(),out reason,
                    enabled:()=>removalPersistence!=null && removalPersistence.Active),reason);
                removalPersistenceRegistered=true;
            }
            Require(removalPersistence==null,"Another removal persistence acceptance is active");removalPersistence=p;
        }
        private sealed class RemovalPersistenceMoves : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext c)
            {
                var p=removalPersistence;var f=p?.Fixture;var actor=f?.civilianRemoval?.Actor;
                if(p==null || !p.Active || c==null || actor==null || c.UnitId!=actor.Id || c.OriginOwnerGuid!=Plugin.Guid)return null;
                if(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==c.SessionId && world==c.WorldGeneration &&
                    UnitAccess.TryGetById(actor.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==actor.Global)p.OwnMoves++;
                return null;
            }
        }
        private static void RemovalPersistenceEnded(MissionLifecycleNotification n)
        {var p=removalPersistence;if(p!=null && p.Active && n.Context.SessionId==p.OriginalSession)p.Ended=true;}
        private static void RemovalPersistenceStarted(MissionLifecycleNotification n)
        {
            var p=removalPersistence;var c=n.Context;
            if(p==null || !p.Active || !p.LoadArmed || n.IsReplay || !c.IsSave || c.SessionId==p.OriginalSession || string.IsNullOrEmpty(c.FilePath))return;
            try
            {
                if(!string.Equals(Path.GetFullPath(c.FilePath),p.Path,StringComparison.OrdinalIgnoreCase))return;
                Require(p.Ended,"Pending removal save did not retire the original native session");
                p.LoadArmed=false;p.LoadedSession=c.SessionId;p.Fixture.session=c.SessionId;p.Fixture.stopped=false;
                p.Fixture.civilianRemoval.Session=c.SessionId;
                Plugin.SetGeneralFixtureSession(p.Fixture.expectedEnabled);p.Fixture.CheckRemovalPersistenceSettings();Volatile.Write(ref p.Loaded,1);
            }
            catch(Exception error){p.Fault=error.ToString();Volatile.Write(ref p.Loaded,1);}
        }
        private static unsafe void RemovalPersistenceBeforeTick(int tick)
        {
            var p=removalPersistence;var f=p?.Fixture;
            if(p==null || !p.Active || Volatile.Read(ref p.Loaded)==0 || f.stopped || p.Fault!=null)return;
            try
            {
                Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.LoadedSession && sid==f.session,
                    "Pending removal is not bound to the actual loaded native world");
                f.civilianRemoval.World=world;
                if(p.FirstTick)
                {
                    Require(world==p.LoadedWorld,"Loaded removal world changed again");
                    if(f.RemovalPersistenceOriginalGone())p.NativeGone=true;
                    return;
                }
                p.LoadedWorld=world;Require(world!=p.OriginalWorld,"Save load retained the old native world generation");f.CheckRemovalPersistenceSettings();
                Require(Plugin.GeneralObserver.RemovalCount==p.Removals && p.OwnMoves==0,"Own removal/reissue occurred before the first loaded tick");
                f.CheckRemovalPendingBytes(p,true);
                var a=f.civilianRemoval.Actor;
                Require(UnitAccess.TryGetById(a.Id,out GameUnit* u,out _) && u!=null,"Loaded original slot is unreadable; this is not proof of native removal");
                bool same=u->r_GlobalId==a.Global;
                if(!same || u->r_AliveState==AliveState.None || u->r_AliveState==AliveState.MarkedForDeletion)
                    p.LoadOutcome="native-load-retired-original";
                else
                {
                    Require(UnitAccess.IsReallyAlive(u) && u->r_UnitChimp==a.Type && u->r_ControllableForPlayerId==f.owner && u->r_CurrentHealth==a.Health,
                        "Load left an unproven changed original identity/life/health");
                    int tile=(int)u->r_CurrentPositionTileId;
                    Require(f.runtime.Native.Tile(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY)==tile && CountInTileChain(tile,a.Id)==1,
                        "Load left incoherent original position or occupancy");
                    if(f.runtime.Native.OrdinaryGround(tile) && f.HasStructuralExit(a))
                    {f.CheckLoadedRemovalChains(false);p.LoadOutcome="native-load-restored-legal-current";}
                    else
                    {
                        var before=f.civilianRemoval.Before;
                        bool original=tile==before.TileId && u->r_CurrentTilePositionX==before.X && u->r_CurrentTilePositionY==before.Y;
                        bool next=before.OrdinaryStep && tile==before.NextTileId && u->r_CurrentTilePositionX==before.NextX && u->r_CurrentTilePositionY==before.NextY;
                        Require((original || next) && !f.runtime.Native.OrdinaryGround(tile) && !f.HasStructuralExit(a) &&
                            f.runtime.HasChangedTrapCandidate(before),"Load outcome is neither a demonstrated repair nor the original unsafe causal source");
                        // Read-only actual admission; do not repair flags, AI or packed caches.
                        bool midpoint=next && (u->r_PathPlanStateBitFlags==2 || u->r_PathPlanStateBitFlags==4) && u->r_MovementSubstep<8 &&
                            u->r_NextTilePositionX2==u->r_CurrentTilePositionX && u->r_NextTilePositionY2==u->r_CurrentTilePositionY &&
                            u->r_NextPositionTileId2==u->r_CurrentPositionTileId && f.runtime.Native.JobDword(u,0x740)==0;
                        Require(CivilianRemovalPolicy.GetExclusion(u,before.OrdinaryStep,midpoint)==null,"Load left a protected/unproven phase on the unsafe source");
                        p.LoadOutcome="same-admissible-unsafe-original";
                    }
                }
                p.FirstTick=true;
                f.log("removal-persistence-first-loaded-tick tick="+tick+" enabled="+f.expectedEnabled+" outcome="+p.LoadOutcome+
                    " oldWorld="+p.OriginalWorld+" loadedWorld="+world+" pendingBytesIdentical="+f.expectedEnabled+" ownMarks=0 ownMoves=0");
            }
            catch(Exception error){p.Fault=error.ToString();}
        }
        private static unsafe void RemovalPersistenceAfterTick(int tick)
        {
            var p=removalPersistence;var f=p?.Fixture;
            if(p==null || !p.Active || !p.FirstTick || p.MarkerBoundary || p.Fault!=null || f.stopped)return;
            try
            {
                f.CheckRemovalPersistenceSettings();long delta=Plugin.GeneralObserver.RemovalCount-p.Removals;
                bool expect=f.expectedEnabled && p.LoadOutcome=="same-admissible-unsafe-original";
                Require(delta==(expect?1:0) && p.OwnMoves==0,"Loaded removal action differs from its actual native-load outcome");
                if(expect)
                {
                    var a=f.civilianRemoval.Actor;
                    Require(UnitAccess.TryGetById(a.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==a.Global &&
                        u->r_AliveState==AliveState.MarkedForDeletion,"One counted own mark lacks actual original Life3 before the normal unit pass");
                }
                p.MarkerBoundary=true;
                f.log("removal-persistence-loaded-boundary tick="+tick+" enabled="+f.expectedEnabled+" outcome="+p.LoadOutcome+" ownMarks="+delta+
                    " actualOriginalLife3="+expect+" ownMoves=0 completedCleanupNotYetClaimed=true");
            }
            catch(Exception error){p.Fault=error.ToString();}
        }
        // Both lifecycle fixtures inspect the actual production record, not a
        // handwritten v6 body. Civilian removal deliberately has no BFS field.
        private byte[] CheckUnstartedRemovalRecord(RescuePendingCandidate<UnitSnapshot> record)
        {
            var a=civilianRemoval.Actor;var before=civilianRemoval.Before;var u=record.Snapshot;
            Require(u!=null && before!=null && u.Id==a.Id && u.GlobalId==a.Global && u.Type==(int)a.Type && u.Owner==owner &&
                u.Health==a.Health && u.Tribe==before.Tribe && u.TribeGlobalId==before.TribeGlobalId && u.AIState==before.AIState &&
                u.X==before.X && u.Y==before.Y && u.TileId==before.TileId &&
                u.NextX==before.NextX && u.NextY==before.NextY && u.NextTileId==before.NextTileId &&
                u.RequestedX==before.RequestedX && u.RequestedY==before.RequestedY &&
                u.Flags==before.Flags && u.Substep==before.Substep && u.OrdinaryStep==before.OrdinaryStep &&
                u.Connections==before.Connections && u.GroundFlags==before.GroundFlags && u.NextGroundFlags==before.NextGroundFlags &&
                u.CurrentWasOrdinary==before.CurrentWasOrdinary && u.NextWasOrdinary==before.NextWasOrdinary &&
                u.CurrentHadExit==before.CurrentHadExit && u.NextHadExit==before.NextHadExit &&
                u.AffectedCurrent && u.AffectedNext==before.OrdinaryStep && u.CivilianRemoval && u.Unsupported==null &&
                u.Job==null && u.Command==null && !u.HasCapturedVegetation && u.CapturedVegetationGlobal==0 &&
                u.LocalField==null && u.Candidates.Count==0 && u.CrewCount==0 && u.CrewIdentity!=null && u.CrewIdentity.Length==0 && u.GeometryWitness==null,
                "Actual removal record differs from the full original causal source or contains continuation authority");
            Require(record.LifetimeMode==RescueQueueLifetimeMode.ActiveWindow && record.ActiveAge==-1 && !record.AwaitingResources &&
                record.Attempts==0 && record.Age==0 && record.RetryIn==0,
                "Paused removal record already consumed/refreshed source age or an active work window");
            byte[] bytes=null;
            Require(RescuePendingCodec.TryEncode(record,out bytes) && bytes.Length>=6 && BitConverter.ToUInt16(bytes,4)==6 &&
                RescuePendingCodec.TryDecode(bytes,out var decoded) && RescuePendingCodec.TryEncode(decoded,out var canonical) && ParkedSameBytes(bytes,canonical),
                "Actual unstarted removal proof/window is not a canonical production v6 roundtrip");
            return bytes;
        }
        private void CheckRemovalPendingBytes(RemovalPersistenceProof p,bool loaded)
        {
            Require(Plugin.GeneralObserver.TryCapturePending(out var records),"Cannot capture current causal queue metadata");
            Require(records.Length==(expectedEnabled?1:0),"Pending removal record count differs from requested ON/OFF source");
            if(!expectedEnabled)return;
            byte[] bytes=CheckUnstartedRemovalRecord(records[0]);
            if(p.PendingBytes==null)p.PendingBytes=bytes;
            else Require(ParkedSameBytes(p.PendingBytes,bytes),loaded?"Loaded v6 causal proof/window changed before first tick":"Paused v6 causal proof/window changed before native save");
        }
        private static void CheckRemovalSavedArchive(RemovalPersistenceProof p,bool enabled)
        {
            Require(MapArchive.TryLoad(p.Path,out var archive) && archive!=null,"Saved native file has no readable real mod archive");
            byte[] raw;using(archive)raw=archive.TryReadBinaryFile("_SE_ModData_TrappedUnitRescueOwnedWorkers.msgpack",true);
            Require(raw!=null && raw.Length>0,"Actual native save omitted the registered rescue archive");
            // Invoke the actual selected SDK's bounded CRC-validating decoder.
            // No copied envelope parser, raw ZIP rewrite or second registration.
            var codec=typeof(VersionedSaveData).Assembly.GetType("APIShared.Savegames.VersionedSaveRecordCodec",true);
            var decode=codec.GetMethod("Decode",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(byte[]),typeof(int)},null);
            Require(decode!=null,"Selected SDK archive decoder contract changed");var decoded=decode.Invoke(null,new object[]{raw,1});
            Require(decoded!=null,"Selected SDK archive decoder returned null");var type=decoded.GetType();
            var status=type.GetProperty("Status",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            var entries=type.GetProperty("Entries",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            Require(status!=null && status.PropertyType.IsEnum && entries!=null && entries.PropertyType==typeof(VersionedSaveEntries) &&
                status.GetValue(decoded,null).ToString()=="Valid","Actual saved rescue envelope was not valid under the selected SDK");
            var values=(VersionedSaveEntries)entries.GetValue(decoded,null);Require(values!=null,"Actual saved rescue entries are unavailable");
            var owned=new Dictionary<string,byte[]>(StringComparer.Ordinal);int oldPending=0,oldGeometry=0,compactEntries=0;
            foreach(string name in values.Names)
            {
                if(name.StartsWith("pending/",StringComparison.Ordinal))oldPending++;
                if(name.StartsWith("geometry/",StringComparison.Ordinal))oldGeometry++;
                if(name.StartsWith("compact/",StringComparison.Ordinal))compactEntries++;
                Require(values.TryGet(name,out var entry) && entry!=null,"Actual SDK rescue archive entry is unreadable");
                if(name=="owned-workers" || name.StartsWith("compact/",StringComparison.Ordinal))owned.Add(name,entry);
            }
            Require(oldPending==0 && oldGeometry==0,"New removal save unexpectedly mixes legacy per-unit entries with its current archive");
            int pendingCount=0;
            if(enabled)
            {
                RescuePendingCandidate<UnitSnapshot>[] pending=null;RescuePendingGeometry[] geometry=null;string reason=null;
                Require(RescueCompactArchiveCodec.HasManifest(owned) &&
                    RescueCompactArchiveCodec.TryDecode(owned,out pending,out geometry,out reason) && pending.Length==1 && geometry.Length==0,
                    "Actual compact removal archive did not decode to exactly one causal pending record and no geometry-only record");
                pendingCount=pending.Length;
                Require(ParkedSameBytes(p.Fixture.CheckUnstartedRemovalRecord(pending[0]),p.PendingBytes),
                    "Actual compact row changed the full canonical v6 original causal proof/window");
                Dictionary<string,byte[]> canonical=null;
                Require(owned.TryGetValue("owned-workers",out var ledger) &&
                    RescueCompactArchiveCodec.TryEncode(pending,geometry,ledger,out canonical,out reason) && canonical.Count==owned.Count,
                    "Actual compact archive is not canonically reproducible by its production codec");
                foreach(var entry in owned)Require(canonical.TryGetValue(entry.Key,out var bytes) && ParkedSameBytes(entry.Value,bytes),
                    "Actual compact source/manifest/chunk/ledger bytes differ from their canonical production encoding");
            }
            else Require(compactEntries==0 && !RescueCompactArchiveCodec.HasManifest(owned),"Disabled removal save unexpectedly contains compact pending authority");
            p.Fixture.log("removal-persistence-actual-archive enabled="+enabled+" envelopeValidatedBySelectedSDK=true pendingRecords="+pendingCount+
                " savedCanonicalPendingMatches="+enabled+" namespace="+(enabled?"compact":"empty-owned-only")+
                " mode="+(enabled?"ActiveWindow":"none")+" unstartedActiveAge=-1 compactRemovalHasNoBank=true recordBytes="+raw.Length+" path="+p.Path);
        }
        private unsafe void CheckLoadedRemovalChains(bool gone)
        {
            var a=civilianRemoval.Actor;var before=civilianRemoval.Before;int originalLinks=0;
            var tiles=before.TileId==before.NextTileId?new[]{before.TileId}:new[]{before.TileId,before.NextTileId};
            foreach(int tile in tiles)
            {
                Require(tile>=0 && tile<NativeRelocation.TileCapacity,"Saved source tile is out of bounds");
                int id=runtime.Native.Head(tile);var seen=new HashSet<int>();
                while(id!=0)
                {
                    GameUnit* unit=null;
                    Require(id>0 && id<=NativeRelocation.MaximumUnitId && seen.Add(id) && seen.Count<=NativeRelocation.MaximumUnitId &&
                        UnitAccess.TryGetById(id,out unit,out _) && unit!=null,"Loaded saved-source chain is unreadable or cyclic");
                    Require(unit->r_GlobalId!=0 && (int)unit->r_CurrentPositionTileId==tile &&
                        runtime.Native.Tile(unit->r_CurrentTilePositionX,unit->r_CurrentTilePositionY)==tile,"Loaded source retains a stale physical chain member");
                    if(id==a.Id && unit->r_GlobalId==a.Global)originalLinks++;
                    id=runtime.Native.Next(unit);
                }
            }
            Require(originalLinks<=1 && (!gone || originalLinks==0),"Loaded source has duplicate/stale links for the original worker life");
        }
        private unsafe bool RemovalPersistenceOriginalGone()
        {
            var a=civilianRemoval.Actor;
            Require(UnitAccess.TryGetById(a.Id,out GameUnit* u,out _) && u!=null,"Original slot unreadable after load");
            return u->r_GlobalId!=a.Global || u->r_AliveState==AliveState.None;
        }
        private unsafe void CheckRemovalSaveSource(RemovalPersistenceProof p)
        {
            var a=civilianRemoval.Actor;var b=civilianRemoval.Before;
            Require(UnitAccess.TryGetById(a.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==a.Global && UnitAccess.IsReallyAlive(u) &&
                u->r_UnitChimp==a.Type && u->r_ControllableForPlayerId==owner && u->r_CurrentHealth==a.Health &&
                (int)u->r_CurrentPositionTileId==b.TileId && u->r_CurrentTilePositionX==b.X && u->r_CurrentTilePositionY==b.Y &&
                !runtime.Native.OrdinaryGround(b.TileId) && !HasStructuralExit(a) && CountInTileChain(b.TileId,a.Id)==1,
                "Paused native save source lost original Life2/unsafe Current/complete real occupancy");
            Require(Plugin.GeneralObserver.RemovalCount==p.Removals && Plugin.GeneralObserver.AttemptCount==p.Attempts && p.OwnMoves==0,
                "Recovery ran before the saved pending source reached its first admitted tick");CheckRemovalPendingBytes(p,false);
        }
        private IEnumerable<AutomationWait> RemovalPersistence(AutomationRun run)
        {
            var p=new RemovalPersistenceProof{Fixture=this,OriginalSession=session,Path=PersistenceSavePath(run)};ArmRemovalPersistence(p);
            try
            {
                run.InSimulation(run.CurrentMission,()=>
                {
                    civilianRemoval=new CivilianRemovalProof();Require(WorldMutationEvents.TryGetCurrentWorld(out civilianRemoval.Session,out civilianRemoval.World) && civilianRemoval.Session==session,"No source native world");
                    p.OriginalWorld=civilianRemoval.World;SetupCivilianRemoval(true);return true;
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
                Require(civilianRemoval.Before!=null,"No genuine natural Apple source qualified for pending removal save");
                yield return AutomationWait.Until("quiescent director before real pending removal source",()=>Director.instance.SafeToSave());Director.instance.FinishedSaving();
                run.InSimulation(run.CurrentMission,()=>
                {
                    ApplyCivilianRemovalMoat();CheckRemovalPersistenceSettings();p.Removals=Plugin.GeneralObserver.RemovalCount;p.Attempts=Plugin.GeneralObserver.AttemptCount;
                    Require(SimulationTickEvents.TryGetCurrentTick(out p.SaveTick),"Pending removal source clock unavailable");CheckRemovalSaveSource(p);return true;
                });
                yield return AutomationWait.Until("quiescent director before actual v6 save",()=>Director.instance.SafeToSave());Director.instance.FinishedSaving();
                var screen=CameraControls2D.instance.getScreenXYForSaveCentring(1,1);int centreX=-1,centreY=-1;GameMap.instance.getRotationCentre(ref centreX,ref centreY);
                Require(EngineInterface.SaveSaveGame(p.Path,screen.x,screen.y,centreX,centreY,false,false,false),"Actual pending removal save writer refused");
                Require(File.Exists(p.Path) && new FileInfo(p.Path).Length>0,"Actual pending removal save was not materialized");
                run.InSimulation(run.CurrentMission,()=>{Require(SimulationTickEvents.TryGetCurrentTick(out int tick) && tick==p.SaveTick,"Native tick advanced during paused pending save");CheckRemovalSaveSource(p);return true;});
                CheckRemovalSavedArchive(p,expectedEnabled);
                foreach(var w in PersistenceQuitToMenu(run))yield return w;
                p.LoadArmed=true;foreach(var w in run.LoadSave(p.Path))yield return w;
                Require(Volatile.Read(ref p.Loaded)!=0 && p.Fault==null && run.CurrentMission!=null && run.CurrentMission.SessionId==p.LoadedSession,
                    "Real pending-removal save did not complete its new lifecycle: "+p.Fault);
                yield return run.WaitForPlayState(run.CurrentMission);
                if(MainViewModel.Instance.Show_HUD_Briefing)MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
                yield return AutomationWait.Until("loaded removal briefing closed",()=>!MainViewModel.Instance.Show_HUD_Briefing);
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                yield return run.WaitForTicks(run.CurrentMission,64);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                Require(p.Fault==null && p.FirstTick && p.MarkerBoundary,"No valid first loaded removal boundaries: "+p.Fault);
                bool retired=run.InSimulation(run.CurrentMission,()=>RemovalPersistenceOriginalGone());
                if(retired)
                {
                    foreach(var w in AwaitCivilianFarmReplacement(run))yield return w;
                }
                run.InSimulation(run.CurrentMission,()=>
                {
                    Require(p.Fault==null && civilianRemoval.Fault==null,p.Fault??civilianRemoval.Fault);CheckRemovalPersistenceSettings();
                    bool own=expectedEnabled && p.LoadOutcome=="same-admissible-unsafe-original";
                    Require(Plugin.GeneralObserver.RemovalCount-p.Removals==(own?1:0) && p.OwnMoves==0 && Plugin.GeneralObserver.PendingCount==0,
                        "Pending save/load produced a duplicate mark, replay or retained stale cause");
                    if(own)Require(retired && p.NativeGone,"One own marker did not reach actual native destruction");
                    if(retired){CheckLoadedRemovalChains(true);ValidateRemovedCivilianChains();ObserveCivilianFarmReplacement();Require(civilianRemoval.FarmSlotFreed && civilianRemoval.Replacement,"Native removal did not release/re-employ the original farm slot naturally");}
                    log("removal-persistence-result enabled="+expectedEnabled+" outcome="+p.LoadOutcome+" ownMarks="+(Plugin.GeneralObserver.RemovalCount-p.Removals)+
                        " originalGone="+retired+" completeOldChainsFree="+retired+" farmReemployed="+civilianRemoval.Replacement+" ownMoves=0 actualV6Archive="+expectedEnabled+
                        " noOldFlags5Receipt=true originalSession="+p.OriginalSession+" loadedSession="+p.LoadedSession);return true;
                });
            }
            finally
            {
                p.Active=false;p.LoadArmed=false;if(civilianRemoval!=null)civilianRemoval.Active=false;appleWorld?.Dispose();
                if(ReferenceEquals(removalPersistence,p))removalPersistence=null;
            }
        }
    }
}
