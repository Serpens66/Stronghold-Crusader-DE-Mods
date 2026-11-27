using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
        private sealed class BurstPersistenceProof
        {
            internal Fixture Fixture;internal string Path,Fault;
            internal long OriginalSession,OriginalWorld,LoadedSession,LoadedWorld;
            internal int Loaded,SaveTick,OwnCalls,NativeRepaired,StillUnsafe;
            internal bool Active=true,LoadArmed,Ended,FirstTick;
            internal readonly Dictionary<int,Actor> Actors=new Dictionary<int,Actor>();
            internal readonly Dictionary<int,byte[]> Pending=new Dictionary<int,byte[]>();
            internal readonly Dictionary<int,UnitSnapshot> Before=new Dictionary<int,UnitSnapshot>();
        }
        private static volatile BurstPersistenceProof burstPersistence;
        private static bool burstPersistenceRegistered;
        private IEnumerable<AutomationStep> BurstPersistenceSteps()
        {yield return new AutomationStep("actual-complete600-burst-save-load",TimeSpan.FromSeconds(1500),BurstPersistenceCase,false);}
        private void ArmBurstPersistence(BurstPersistenceProof p)
        {
            if(!burstPersistenceRegistered)
            {
                Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out var missions,out var available),available.Reason);
                Require(missions.TryRegisterObserver("acceptance-burst-persistence",BurstPersistenceStarted,BurstPersistenceEnded,null,out var diagnostic),diagnostic.Reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-burst-persistence-loaded",BurstPersistenceBeforeTick,out string reason,order:-1000),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-burst-persistence-own",new BurstPersistenceMoveObserver(),out reason,
                    enabled:()=>burstPersistence!=null && burstPersistence.Active),reason);
                burstPersistenceRegistered=true;
            }
            Require(burstPersistence==null,"Another burst persistence proof is active");burstPersistence=p;
        }
        private sealed class BurstPersistenceMoveObserver : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext c)
            {
                var p=burstPersistence;var f=p?.Fixture;
                if(p==null || !p.Active || f==null || f.burst==null || !f.burst.Written || c==null || c.OriginOwnerGuid!=Plugin.RescueOwnerGuid ||
                    !p.Actors.TryGetValue(c.UnitId,out var a) || c.SessionId!=f.session)return null;
                if(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==c.SessionId && world==c.WorldGeneration &&
                    UnitAccess.TryGetById(a.Id,out GameUnit* u,out _) && u!=null && u->r_GlobalId==a.Global)p.OwnCalls++;
                return null;
            }
        }
        private static void BurstPersistenceEnded(MissionLifecycleNotification n)
        {var p=burstPersistence;if(p!=null && p.Active && n.Context.SessionId==p.OriginalSession)p.Ended=true;}
        private static void BurstPersistenceStarted(MissionLifecycleNotification n)
        {
            var p=burstPersistence;var c=n.Context;
            if(p==null || !p.Active || !p.LoadArmed || n.IsReplay || !c.IsSave || c.SessionId==p.OriginalSession || string.IsNullOrEmpty(c.FilePath))return;
            try
            {
                if(!string.Equals(Path.GetFullPath(c.FilePath),p.Path,StringComparison.OrdinalIgnoreCase))return;
                Require(p.Ended,"Burst save started before original session retirement");
                p.LoadArmed=false;p.LoadedSession=c.SessionId;
                // Metadata rebind only for this exact successful owned-save start.
                // Plugin continues to own real archive loading and native lifecycle.
                p.Fixture.session=c.SessionId;p.Fixture.stopped=false;p.Fixture.burst.Session=c.SessionId;
                Plugin.SetGeneralFixtureSession(p.Fixture.expectedEnabled);p.Fixture.CheckRemovalPersistenceSettings();
                Volatile.Write(ref p.Loaded,1);
            }
            catch(Exception e){p.Fault=e.ToString();Volatile.Write(ref p.Loaded,1);}
        }
        private static void BurstPersistenceBeforeTick(int tick)
        {
            var p=burstPersistence;var f=p?.Fixture;
            if(p==null || !p.Active || Volatile.Read(ref p.Loaded)==0 || p.FirstTick || p.Fault!=null)return;
            try
            {
                Require(!f.stopped && f.session==p.LoadedSession && p.Ended,"Burst loaded lifecycle is not owned");
                Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.LoadedSession && world!=p.OriginalWorld,
                    "Burst save did not reach a distinct actual loaded native world");
                p.LoadedWorld=world;f.CheckRemovalPersistenceSettings();
                Require(Plugin.GeneralObserver.AttemptCount==f.burst.Attempts && Plugin.GeneralObserver.RescueCount==f.burst.Rescues && p.OwnCalls==0,
                    "Burst rescue ran before first loaded native tick verification");
                f.CheckBurstPersistencePending(p,true);f.ClassifyLoadedBurst(p,tick);
                f.burst.World=world;p.FirstTick=true;
                f.log("burst-persistence-first-loaded tick="+tick+" records="+p.Pending.Count+" canonicalPendingIdentical=true oldWorld="+p.OriginalWorld+
                    " loadedWorld="+world+" nativeRepaired="+p.NativeRepaired+" originalUnsafe="+p.StillUnsafe+" ownCallsBefore=0");
            }
            catch(Exception e){p.Fault=e.ToString();}
        }
        private void CheckBurstPersistencePending(BurstPersistenceProof p,bool loaded)
        {
            Require(Plugin.GeneralObserver.TryCapturePending(out var records) && records.Length==600,"Burst save lost or invented pending records before first evaluation");
            var seen=new HashSet<int>();var fields=new HashSet<RescueLocalField>();
            foreach(var r in records)
            {
                var s=r.Snapshot;Actor a=null;Require(s!=null && p.Actors.TryGetValue(s.Id,out a) && seen.Add(s.Id),"Burst pending manifest has missing/foreign/duplicate actor");
                Require(s.GlobalId==a.Global && s.Type==(int)a.Type && s.Owner==owner && s.Health==a.Health && s.Tribe==a.Tribe &&
                    s.X==a.SourceX && s.Y==a.SourceY && s.AffectedCurrent && s.CurrentWasOrdinary && !s.CivilianRemoval &&
                    s.Command!=null && s.Job==null && s.Unsupported==null && s.LocalField!=null && s.GeometryWitness==null && s.Candidates.Count==0 &&
                    s.LocalField.OriginX==s.X && s.LocalField.OriginY==s.Y && s.LocalField.OriginTile==s.TileId && s.LocalField.OriginWasOrdinary,
                    "Burst pending proof differs from original actor/frame/compact command geometry: "+s.Id);
                Require(r.LifetimeMode==RescueQueueLifetimeMode.ActiveWindow && r.Age==0 && r.ActiveAge==-1 && r.Attempts==0 &&
                    r.RetryIn==0 && !r.AwaitingResources,"Burst save changed original unstarted source/window metadata");
                byte[] bytes=null;Require(RescuePendingCodec.TryEncode(r,out bytes) && BitConverter.ToUInt16(bytes,4)==6,"Burst pending proof is not production-v6 serializable");
                if(!loaded && p.Pending.Count<600 && !p.Pending.ContainsKey(s.Id)){p.Pending.Add(s.Id,bytes);p.Before.Add(s.Id,s);}
                else Require(p.Pending.TryGetValue(s.Id,out var expected) && ParkedSameBytes(expected,bytes),"Burst original v6 proof/window changed for "+s.Id);
                if(loaded && fields.Add(s.LocalField))Require(s.LocalField.TryFindCandidates(runtime.Native.Tile,out _,out _),"Loaded compact field failed actual map/layout binding");
            }
            Require(seen.Count==600 && p.Pending.Count==600,"Burst canonical manifest is incomplete");
        }
        private unsafe void ClassifyLoadedBurst(BurstPersistenceProof p,int tick)
        {
            int batch=0;var line=new StringBuilder();
            foreach(var a in actors)
            {
                GameUnit* u=null;bool found=UnitAccess.TryGetById(a.Id,out u,out _) && u!=null;
                log("burst-persistence-loaded-actor tick="+tick+" unit="+a.Id+"/"+a.Global+" readable="+found+
                    (found?" actualGlobal="+u->r_GlobalId+" type="+u->r_UnitChimp+" life="+u->r_AliveState+" hp="+u->r_CurrentHealth+
                        " current="+u->r_CurrentTilePositionX+","+u->r_CurrentTilePositionY+" flags="+u->r_PathPlanStateBitFlags+" ss="+u->r_MovementSubstep:""));
                Require(found && u->r_GlobalId==a.Global && UnitAccess.IsReallyAlive(u) && u->r_UnitChimp==a.Type &&
                    u->r_ControllableForPlayerId==owner && u->r_CurrentHealth==a.Health && u->r_TribeId==a.Tribe,
                    "Native load lost/reused/changed original soldier "+a.Id+"; absence is not a repair");
                CheckActor(a,true);var before=p.Before[a.Id];
                Require(runtime.Native.TryTribeGlobal(a.Tribe,out uint tribe) && tribe==before.TribeGlobalId && runtime.Native.JobDword(u,0x93C)==tribe,
                    "Native load changed original soldier tribe lifetime");
                Require(u->r_TargetTilePositionX2==before.RequestedX && u->r_TargetTilePositionY2==before.RequestedY,
                    "Native load changed an original authoritative goal; no replacement order is fabricated");
                int current=(int)u->r_CurrentPositionTileId;string outcome;
                if(runtime.Native.OrdinaryGround(current) && HasStructuralExit(a)){p.NativeRepaired++;outcome="native-legal";}
                else
                {
                    bool atCurrent=current==before.TileId && a.X==before.X && a.Y==before.Y;
                    bool atNext=before.OrdinaryStep && current==before.NextTileId && a.X==before.NextX && a.Y==before.NextY;
                    IntPtr pointer=(IntPtr)u;
                    Require((atCurrent || atNext) && !runtime.Native.OrdinaryGround(current) && !HasStructuralExit(a) &&
                        before.Command.Matches(u->r_UnitChimp,u->r_AIState,at=>runtime.Native.JobWord((GameUnit*)pointer,at),at=>runtime.Native.JobDword((GameUnit*)pointer,at)),
                        "Native load left an unsupported altered unsafe pose/intent for "+a.Id);
                    p.StillUnsafe++;outcome="original-unsafe";
                }
                if(line.Length!=0)line.Append(';');line.Append(a.Id).Append('/').Append(a.Global).Append(':').Append(outcome);
                if(++batch%32==0 || batch==600){log("burst-persistence-load-manifest through="+batch+" actors="+line);line.Clear();}
            }
            Require(p.NativeRepaired+p.StillUnsafe==600,"Loaded burst classification did not cover every original soldier");
        }
        private void CheckBurstSavedArchive(BurstPersistenceProof p)
        {
            Require(MapArchive.TryLoad(p.Path,out var archive) && archive!=null,"Burst save has no real readable mod archive");
            byte[] raw;using(archive)raw=archive.TryReadBinaryFile("_SE_ModData_BugfixesAndQoLTrappedUnitRescue.msgpack",true);
            Require(raw!=null && raw.Length>0,"Actual burst native save omitted rescue archive");
            var codec=typeof(VersionedSaveData).Assembly.GetType("APIShared.Savegames.VersionedSaveRecordCodec",true);
            var decode=codec.GetMethod("Decode",BindingFlags.Static|BindingFlags.NonPublic,null,new[]{typeof(byte[]),typeof(int)},null);
            Require(decode!=null,"Selected SDK versioned archive decoder contract changed");var decoded=decode.Invoke(null,new object[]{raw,1});
            Require(decoded!=null,"SDK returned no burst archive decode");var type=decoded.GetType();
            var status=type.GetProperty("Status",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            var entries=type.GetProperty("Entries",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            Require(status!=null && status.PropertyType.IsEnum && status.GetValue(decoded,null).ToString()=="Valid" && entries!=null &&
                entries.PropertyType==typeof(VersionedSaveEntries),"Actual burst archive is not valid under selected SDK CRC decoder");
            var values=(VersionedSaveEntries)entries.GetValue(decoded,null);Require(values!=null,"Burst archive entries absent");
            var owned=new Dictionary<string,byte[]>(StringComparer.Ordinal);
            foreach(string name in values.Names)
            {
                Require(!name.StartsWith("pending/",StringComparison.Ordinal) && !name.StartsWith("geometry/",StringComparison.Ordinal),"Burst archive mixes legacy bank namespaces");
                Require(values.TryGet(name,out var entry) && entry!=null,"Burst archive entry unreadable");
                if(name=="owned-workers" || name.StartsWith("compact/",StringComparison.Ordinal))owned.Add(name,entry);
            }
            RescuePendingCandidate<UnitSnapshot>[] pending=null;RescuePendingGeometry[] geometry=null;string reason=null;
            Require(RescueCompactArchiveCodec.HasManifest(owned) && RescueCompactArchiveCodec.TryDecode(owned,out pending,out geometry,out reason) &&
                pending.Length==600 && geometry.Length==0,"Actual burst compact archive lacks complete600 pending records");
            var seen=new HashSet<int>();
            foreach(var r in pending)
            {
                byte[] encoded=null;Require(r.Snapshot!=null && seen.Add(r.Snapshot.Id) && p.Pending.TryGetValue(r.Snapshot.Id,out var expected) &&
                    RescuePendingCodec.TryEncode(r,out encoded) && ParkedSameBytes(expected,encoded),"Actual saved compact row differs from original canonical v6 proof");
            }
            Dictionary<string,byte[]> canonical=null;
            Require(owned.TryGetValue("owned-workers",out var ledger) && RescueCompactArchiveCodec.TryEncode(pending,geometry,ledger,out canonical,out reason) &&
                canonical.Count==owned.Count,"Actual600-record archive cannot be reproduced canonically");
            foreach(var entry in owned)Require(canonical.TryGetValue(entry.Key,out var bytes) && ParkedSameBytes(entry.Value,bytes),"Actual compact burst bytes differ from production canonical encoding");
            log("burst-persistence-actual-archive pending=600 geometry=0 selectedSDKEnvelopeValid=true compactCanonical=true nativeSaveBytes="+
                new FileInfo(p.Path).Length+" envelopeBytes="+raw.Length+" entries="+owned.Count+" path="+p.Path);
        }
        private IEnumerable<AutomationWait> BurstPersistenceCase(AutomationRun run)
        {
            Require(expectedEnabled,"Burst persistence initially supports the explicit ON case only");
            var p=new BurstPersistenceProof{Fixture=this,OriginalSession=session,Path=PersistenceSavePath(run)};ArmBurstPersistence(p);
            burst=new BurstProof{Session=session,Population=600};
            try
            {
                run.InSimulation(run.CurrentMission,()=>{
                    Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out burst.World) && sid==session,"Burst persistence source native world unavailable");
                    p.OriginalWorld=burst.World;RequireBurstWorld();Plugin.SetGeneralFixtureSession(false);ArmBurst();FindBurstSites(1);SpawnBurst();
                    foreach(var a in actors)p.Actors.Add(a.Id,a);return true;
                });
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                yield return run.WaitForTicks(run.CurrentMission,12);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                yield return AutomationWait.Until("quiescent director before full600 source",()=>Director.instance.SafeToSave());Director.instance.FinishedSaving();
                run.InSimulation(run.CurrentMission,()=>{
                    CommandBurst();WriteBurst();CheckRemovalPersistenceSettings();CheckBurstPersistencePending(p,false);
                    Require(SimulationTickEvents.TryGetCurrentTick(out p.SaveTick) && p.OwnCalls==0,"Burst save source clock/early call proof differs");return true;
                });
                yield return AutomationWait.Until("quiescent director before full600 save",()=>Director.instance.SafeToSave());Director.instance.FinishedSaving();
                var screen=CameraControls2D.instance.getScreenXYForSaveCentring(1,1);int cx=-1,cy=-1;GameMap.instance.getRotationCentre(ref cx,ref cy);
                Require(EngineInterface.SaveSaveGame(p.Path,screen.x,screen.y,cx,cy,false,false,false),"Normal600 pending save writer refused");
                Require(File.Exists(p.Path) && new FileInfo(p.Path).Length>0,"Normal600 pending save did not materialize");
                run.InSimulation(run.CurrentMission,()=>{
                    RequireBurstWorld();Require(SimulationTickEvents.TryGetCurrentTick(out int tick) && tick==p.SaveTick && p.OwnCalls==0 &&
                        Plugin.GeneralObserver.AttemptCount==burst.Attempts && Plugin.GeneralObserver.RescueCount==burst.Rescues,"Burst advanced or recovered during paused actual save");
                    CheckBurstPersistencePending(p,false);foreach(var a in actors){CheckActor(a,true);Require(AtSource(a),"Burst save moved an original source actor");}return true;
                });
                CheckBurstSavedArchive(p);
                foreach(var w in PersistenceQuitToMenu(run))yield return w;
                p.LoadArmed=true;foreach(var w in run.LoadSave(p.Path))yield return w;
                Require(Volatile.Read(ref p.Loaded)!=0 && p.Fault==null && run.CurrentMission!=null && run.CurrentMission.SessionId==p.LoadedSession,
                    "Burst owned save did not complete exact new managed start: "+p.Fault);
                yield return run.WaitForPlayState(run.CurrentMission);
                if(MainViewModel.Instance.Show_HUD_Briefing)MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
                yield return AutomationWait.Until("loaded burst briefing closed",()=>!MainViewModel.Instance.Show_HUD_Briefing);
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                bool arrived=false;
                for(int sample=0;sample<320;sample++)
                {
                    yield return run.WaitForTicks(run.CurrentMission,32);
                    arrived=run.InSimulation(run.CurrentMission,()=>{Require(p.Fault==null && p.FirstTick,"Loaded burst verification failed: "+p.Fault);return BurstProgress(false);});
                    if(arrived && Plugin.GeneralObserver.PendingCount==0)break;
                }
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                run.InSimulation(run.CurrentMission,()=>{
                    Require(p.Fault==null && p.FirstTick,"Missing exact first-loaded600 proof: "+p.Fault);CheckRemovalPersistenceSettings();
                    Require(BurstProgress(true) && Plugin.GeneralObserver.PendingCount==0,"Loaded600 burst did not complete every original goal and drain restored proof");
                    log("burst-persistence-result population=600 originalManifestComplete=true savedAndLoadedCanonicalPending=600 nativeRepaired="+p.NativeRepaired+
                        " originalUnsafe="+p.StillUnsafe+" oldSession="+p.OriginalSession+" loadedSession="+p.LoadedSession+" oldWorld="+p.OriginalWorld+
                        " loadedWorld="+p.LoadedWorld+" actualOwnMoveCalls="+p.OwnCalls+" originalGoalsReached=600 noOwnRescuePerActorAssumption=true");
                    Plugin.SetGeneralFixtureSession(false);RetireActors();return true;
                });
            }
            finally
            {
                p.Active=false;p.LoadArmed=false;if(burst!=null)burst.Active=false;
                if(ReferenceEquals(burstPersistence,p))burstPersistence=null;if(ReferenceEquals(burstFixture,this))burstFixture=null;
            }
        }
    }
}
