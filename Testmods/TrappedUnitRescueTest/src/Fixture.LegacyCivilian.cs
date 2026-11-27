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
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private sealed class LegacyCivilianProof
        {
            internal Fixture Fixture;internal string Path,Fault;internal bool Active=true,LoadArmed,Ended,FirstTick,Marker,Preserved;
            internal long OriginalSession,LoadedSession,OriginalWorld,LoadedWorld;internal int Loaded,OwnMoves,SaveTick;
            internal int Quarry,Tether;internal uint TetherGlobal;internal byte[] Saved;
            internal Actor Human,Command,Ox;
            internal readonly List<WorkerResumeTicket> Tickets=new List<WorkerResumeTicket>();
            internal readonly Dictionary<int,int[]> Tiles=new Dictionary<int,int[]>();
        }
        private static volatile LegacyCivilianProof legacyCivilian;
        private static bool legacyCivilianRegistered;
        private IEnumerable<AutomationStep> LegacyCivilianSteps()
        {yield return new AutomationStep("historical-civilian-park-real-save-load-off-preservation",TimeSpan.FromSeconds(1200),LegacyCivilian,false);}
        private static void LegacyCivilianOff()
        {
            var field=typeof(Plugin).GetField("settings",BindingFlags.Static|BindingFlags.NonPublic);
            Require(field!=null && field.FieldType==typeof(BugfixesAndQoLViewModel),"Legacy fixture settings read contract changed");
            var value=(BugfixesAndQoLViewModel)field.GetValue(null);
            bool masterOff=legacyCivilian?.Fixture.scenario=="rescue-legacy-civilian-master-off";
            Require(value!=null && value.EnableMod==!masterOff && value.EnableTrappedUnitRescue==masterOff && !Plugin.GeneralObserver.IsActive,
                "Legacy migration acceptance must remain genuinely OFF, including restored save settings");
        }
        private void ArmLegacyCivilian(LegacyCivilianProof proof)
        {
            if(!legacyCivilianRegistered)
            {
                Require(ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out var missions,out var availability),availability.Reason);
                Require(missions.TryRegisterObserver("acceptance-legacy-civilian",LegacyCivilianStarted,LegacyCivilianEnded,null,out var diagnostic),diagnostic.Reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-legacy-civilian-before-recovery",LegacyCivilianTick,out string reason,order:-1000),reason);
                Require(SimulationTickEvents.TryRegister(Plugin.Guid,"acceptance-legacy-civilian-after-recovery",LegacyCivilianMarker,out reason,order:100),reason);
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-legacy-civilian-no-replay",new LegacyCivilianMoves(),out reason,
                    enabled:()=>legacyCivilian!=null && legacyCivilian.Active),reason);
                legacyCivilianRegistered=true;
            }
            Require(legacyCivilian==null,"Another legacy park acceptance is active");legacyCivilian=proof;
        }
        private sealed class LegacyCivilianMoves : IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var p=legacyCivilian;if(p==null || !p.Active || context==null || context.OriginOwnerGuid!=Plugin.RescueOwnerGuid || p.Tickets.Count!=3)return null;
                if(!WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) || sid!=context.SessionId || world!=context.WorldGeneration)return null;
                foreach(var t in p.Tickets)if(t.UnitId==context.UnitId && UnitAccess.TryGetById(t.UnitId,out GameUnit* u,out _) && u!=null && u->r_GlobalId==t.UnitGlobalId)
                    p.OwnMoves++;
                return null;
            }
        }
        private static void LegacyCivilianEnded(MissionLifecycleNotification notification)
        {var p=legacyCivilian;if(p!=null && p.Active && notification.Context.SessionId==p.OriginalSession)p.Ended=true;}
        private static void LegacyCivilianStarted(MissionLifecycleNotification notification)
        {
            var p=legacyCivilian;var c=notification.Context;
            if(p==null || !p.Active || !p.LoadArmed || notification.IsReplay || !c.IsSave || c.SessionId==p.OriginalSession || string.IsNullOrEmpty(c.FilePath))return;
            try
            {
                if(!string.Equals(Path.GetFullPath(c.FilePath),p.Path,StringComparison.OrdinalIgnoreCase))return;
                Require(p.Ended,"Legacy saved mission was not genuinely retired");
                p.LoadArmed=false;p.LoadedSession=c.SessionId;p.Fixture.session=c.SessionId;p.Fixture.stopped=false;
                Plugin.SetGeneralFixtureSession(false);LegacyCivilianOff();Volatile.Write(ref p.Loaded,1);
            }
            catch(Exception error){p.Fault=error.ToString();Volatile.Write(ref p.Loaded,1);}
        }
        private static void LegacyCivilianTick(int tick)
        {
            var p=legacyCivilian;
            if(p==null || !p.Active || Volatile.Read(ref p.Loaded)==0 || p.Fault!=null || p.Fixture.stopped)return;
            try
            {
                var f=p.Fixture;LegacyCivilianOff();
                Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.LoadedSession && sid==f.session,
                    "Legacy fixture is not in its exact loaded world");
                if(!p.FirstTick)
                {
                    p.LoadedWorld=world;Require(world!=p.OriginalWorld,"Legacy save did not establish a fresh world generation");
                    foreach(var t in p.Tickets)f.CheckLegacyControl(t,true);
                    Require(f.runtime.Workers.Ledger.TrySerialize(out var bytes) && ParkedSameBytes(bytes,p.Saved),
                        "Historical receipts were not restored byte-identically before the first recovery tick");
                    Require(p.OwnMoves==0,"An old job was replayed before the first loaded tick");p.FirstTick=true;
                    f.log("legacy-civilian-first-loaded-tick tick="+tick+" session="+sid+" world="+world+" settingsOFF=true original3Flags5=true archiveIdentical=true ownMoves=0");
                    return;
                }
                Require(world==p.LoadedWorld,"Legacy fixture world changed again");
                foreach(var ticket in p.Tickets)f.CheckLegacyControl(ticket,true);
                Require(f.runtime.Workers.Ledger.TrySerialize(out var retained) && ParkedSameBytes(retained,p.Saved) && p.OwnMoves==0,
                    "OFF host changed its historical archive or replayed an old job");
                p.Preserved=true;
            }
            catch(Exception error){p.Fault=error.ToString();}
        }
        private static unsafe void LegacyCivilianMarker(int tick)
        {
            var p=legacyCivilian;
            if(p==null || !p.Active || Volatile.Read(ref p.Loaded)==0 || !p.FirstTick || p.Marker || p.Fault!=null || p.Fixture.stopped)return;
            try
            {
                var f=p.Fixture;LegacyCivilianOff();
                Require(WorldMutationEvents.TryGetCurrentWorld(out long sid,out long world) && sid==p.LoadedSession && world==p.LoadedWorld,
                    "Legacy marker boundary has a different world");
                foreach(var ticket in p.Tickets)f.CheckLegacyControl(ticket,true);
                Require(f.runtime.Workers.Ledger.Count==3 && f.runtime.Workers.Ledger.LegacyCivilianCount==1 && p.OwnMoves==0,
                    "OFF host changed an owned receipt or issued a native move");
                CheckProductionScannerDisabled();p.Marker=true;
                f.log("legacy-civilian-marker tick="+tick+" originalLifePreserved=true oldReceiptRetained=true settingsOFF=true beforeNormalUnitPass=true commandOxPreserved=true");
            }
            catch(Exception error){p.Fault=error.ToString();}
        }
        private unsafe WorkerResumeTicket DecodeLegacyWorker(GameUnit* u,int id)
        {
            if(!WorkerTravelPolicy.TryDescribeHistorical(u->r_UnitChimp,u->r_AIState,out var rule) || !rule.HasOrdinarySchema)return null;
            var offsets=new int[rule.FieldCount];var widths=new int[rule.FieldCount];var values=new uint[rule.FieldCount];
            IntPtr address=(IntPtr)u;
            for(int i=0;i<values.Length;i++)
            {
                var field=rule.Field(i);offsets[i]=field.NativeOffset;widths[i]=field.Width;
                values[i]=field.Read(offset=>runtime.Native.JobWord((GameUnit*)address,offset),offset=>runtime.Native.JobDword((GameUnit*)address,offset));
            }
            if(!WorkerJourneySnapshot.TryRestoreKnownSnapshot(u->r_UnitChimp,u->r_AIState,offsets,widths,values,out var job))return null;
            var required=rule.RequiredExternalBindings;int home=0,store=0,tree=0;uint homeGlobal=0,storeGlobal=0,treeGlobal=0;
            if((required&WorkerExternalBindings.LinkedEntity)!=0)return null;
            if((required&WorkerExternalBindings.Workplace)!=0)
            {home=runtime.Native.Word(u,0x990);homeGlobal=runtime.Native.JobDword(u,0x9C0);if(!LegacyBuilding(home,homeGlobal))return null;}
            if((required&WorkerExternalBindings.Store)!=0)
            {store=runtime.Native.Word(u,0x98E);storeGlobal=runtime.Native.JobDword(u,0x6F8);if(!LegacyBuilding(store,storeGlobal))return null;}
            if((required&WorkerExternalBindings.Vegetation)!=0)
            {
                tree=runtime.Native.Word(u,0x9A0);
                if(!NativeVegetationAccess.TryGet(tree,out var vegetation) || !NativeVegetationAccess.TryReadAliveGlobal(vegetation,out treeGlobal))return null;
            }
            if(!runtime.Native.TryTribeGlobal(u->r_TribeId,out uint tribeGlobal))return null;
            var ticket=new WorkerResumeTicket(id,u->r_GlobalId,u->r_UnitChimp,u->r_AIState,u->r_ControllableForPlayerId,
                u->r_TribeId,tribeGlobal,u->r_TargetTilePositionX2,u->r_TargetTilePositionY2,job,
                new WorkerBindingSnapshot(required,home,homeGlobal,store,storeGlobal,tree,treeGlobal));
            return ticket.IsValid?ticket:null;
        }
        private unsafe bool LegacyBuilding(int id,uint global)=>BuildingAccess.TryGetById(id,out GameBuilding* b,out _) && b!=null &&
            b->r_GlobalId==global && b->r_AliveState==AliveState.IsAlive && b->r_PlayerIdOwner==owner;
        private unsafe Actor FindLegacyWorker(LegacyCivilianProof p,bool ox)
        {
            foreach(int id in UnitAccess.GetAllReallyAliveUnits())
            {
                if(!UnitAccess.TryGetById(id,out GameUnit* u,out _) || u==null || u->r_ControllableForPlayerId!=owner ||
                    u->r_UnitChimp!=(ox?eChimps.CHIMP_TYPE_QUARRY_OX:eChimps.CHIMP_TYPE_FARMER_APPLE) ||
                    u->r_LinkedProductionBuildingId!=(ox?p.Tether:civilianRemoval.Farm) ||
                    u->r_LinkedProductionBuildingGlobalId!=(ox?p.TetherGlobal:civilianRemoval.FarmGlobal))continue;
                var before=runtime.SnapshotUnit(id,true,false);
                if(before==null || before.Unsupported!=null || !runtime.Native.OrdinaryGround(before.TileId) ||
                    before.OrdinaryStep && !runtime.Native.OrdinaryGround(before.NextTileId) || !HasStructuralExit(CivilianActor(u,id)) ||
                    CountInTileChain(before.TileId,id)!=1 || DecodeLegacyWorker(u,id)==null)continue;
                return CivilianActor(u,id);
            }
            return null;
        }
        private unsafe void ParkHistoricalFixture(LegacyCivilianProof p,Actor actor,bool command)
        {
            var u=Resolve(actor);Require(UnitAccess.IsReallyAlive(u),"Historical source is not alive");
            WorkerResumeTicket ticket=null;var adapter=new NativeWorkerResumeWorld(runtime.Native);
            if(command)
            {
                var before=runtime.SnapshotUnit(actor.Id,true,false);
                Require(adapter.TryCapture(before,out ticket,out string reason),"Actual command receipt capture failed: "+reason);
            }
            else ticket=DecodeLegacyWorker(u,actor.Id);
            WorkerResumeToken token=default;
            Require(ticket!=null && ticket.IsValid && runtime.Workers.Ledger.TryReserve(ticket,out token),"Strict original receipt cannot reserve ownership");
            int x=u->r_CurrentTilePositionX,y=u->r_CurrentTilePositionY,tile=(int)u->r_CurrentPositionTileId;
            int oldNext=(int)u->r_NextPositionTileId2;uint health=u->r_CurrentHealth;
            Require(runtime.Native.OrdinaryGround(tile) && CountInTileChain(tile,actor.Id)==1,"Historical setup lacks ordinary occupied Current");
            // Explicit historical test setup, not production admission: normalize
            // the SAME occupied Current through the existing audited native call.
            // No raw flags/AI/cargo/job write and no reissue are used.
            Require(runtime.Native.Relocate(actor.Id,x,y),"Native same-Current historical setup normalization failed");
            u=Resolve(actor);
            Require(u->r_CurrentHealth==health && u->r_CurrentTilePositionX==x && u->r_CurrentTilePositionY==y &&
                CountInTileChain(tile,actor.Id)==1 && (oldNext==tile || CountInTileChain(oldNext,actor.Id)==0) &&
                adapter.TryPark(ticket,false) && runtime.Workers.Ledger.CommitPark(token,0),
                "Exact owned historical flags5 park failed; no raw-state fallback is allowed");
            actor.Health=health;actor.SourceX=actor.X=x;actor.SourceY=actor.Y=y;
            p.Tickets.Add(ticket);p.Tiles.Add(actor.Id,new[]{tile,oldNext});CheckLegacyControl(ticket,true);
        }
        private unsafe void CheckLegacyControl(WorkerResumeTicket t,bool parked)
        {
            Require(UnitAccess.TryGetById(t.UnitId,out GameUnit* u,out _) && u!=null && u->r_GlobalId==t.UnitGlobalId &&
                UnitAccess.IsReallyAlive(u) && u->r_UnitChimp==t.UnitType && u->r_ControllableForPlayerId==t.Owner &&
                u->r_TribeId==t.TribeId && u->r_TargetTilePositionX2==t.RequestedX && u->r_TargetTilePositionY2==t.RequestedY,
                "Legacy original identity/control/goal changed");
            var proof=legacyCivilian;var actor=t.UnitId==proof.Human.Id?proof.Human:t.UnitId==proof.Command.Id?proof.Command:proof.Ox;
            Require(actor!=null && u->r_CurrentHealth==actor.Health,"Legacy original health changed");
            Require(runtime.Workers.Ledger.TryGetParkedByUnit(t.UnitId,out var actual) && actual.UnitGlobalId==t.UnitGlobalId && actual.Kind==t.Kind && actual.IsValid,
                "Legacy original receipt lost");
            if(parked)Require(u->r_PathPlanStateBitFlags==5 && NativeRelocation.IsNormalizedPose(u,runtime.Native.Tile(u->r_CurrentTilePositionX,u->r_CurrentTilePositionY),runtime.Native.JobDword(u,0x740)) &&
                CountInTileChain((int)u->r_CurrentPositionTileId,t.UnitId)==1,"Legacy owned barrier or real occupancy changed");
        }
        private unsafe bool LegacyOriginalGone(LegacyCivilianProof p)
        {
            Require(UnitAccess.TryGetById(p.Human.Id,out GameUnit* u,out _) && u!=null,"Cannot read original slot to prove native clearing");
            return u->r_GlobalId!=p.Human.Global || u->r_AliveState==AliveState.None;
        }
        private unsafe void ValidateLegacyChains(LegacyCivilianProof p)
        {
            foreach(int tile in p.Tiles[p.Human.Id])
            {
                Require(tile>=0 && tile<NativeRelocation.TileCapacity,"Legacy source tile is out of bounds");
                int id=runtime.Native.Head(tile);var seen=new HashSet<int>();
                while(id!=0)
                {
                    GameUnit* u=null;
                    Require(id>0 && id<=NativeRelocation.MaximumUnitId && seen.Add(id) && seen.Count<=NativeRelocation.MaximumUnitId &&
                        UnitAccess.TryGetById(id,out u,out _) && u!=null,"Legacy source complete chain is invalid");
                    Require(id!=p.Human.Id || u->r_GlobalId!=p.Human.Global,"Old removed lifetime remains in complete source chain");
                    Require((int)u->r_CurrentPositionTileId==tile,"Legacy chain member has a different actual Current");id=runtime.Native.Next(u);
                }
            }
        }
        private void RequireLegacyQuarryTerrain()
        {
            var tiles=GameTileManagerAPI.Instance;int scale=BuildingScales.GetScale(eMappers.MAPPER_QUARRY);
            int seeds=0,full=0,paired=0;
            // Match the existing natural-Ox helper's documented setup envelope;
            // do not weaken it or paint resources to make an unsuitable map pass.
            for(int y=1;y<799-scale;y++)for(int x=1;x<799-scale;x++)
            {
                if(!tiles.IsTileInsideMapBounds(x,y))continue;
                int tile=tiles.GetTileId(x,y);
                if((tiles.GetTilePropertyFlag(tile)&TilePropertyFlag.HasStone)==0 || tiles.GetTileBuildingId(tile)!=0)continue;
                seeds++;bool free=true;
                for(int dy=0;dy<scale && free;dy++)for(int dx=0;dx<scale;dx++)
                {
                    int xx=x+dx,yy=y+dy;
                    if(!tiles.IsTileInsideMapBounds(xx,yy)){free=false;break;}
                    int cell=tiles.GetTileId(xx,yy);
                    if((tiles.GetTilePropertyFlag(cell)&TilePropertyFlag.HasStone)==0 || tiles.GetTileBuildingId(cell)!=0 || tiles.GetTileUnitId(cell)!=0)
                    {free=false;break;}
                }
                if(!free)continue;full++;
                if(TryNearbyOrdinarySite(x,y,2,out _,out _))paired++;
            }
            log("legacy-quarry-preflight map="+map+" helperScale="+scale+" stoneSeeds="+seeds+" freeStoneFootprints="+full+
                " pairedTetherSites="+paired+" nativeAttempts=0 resourcePainting=false");
            Require(paired>0,"Selected map lacks the natural Quarry/Tether geometry required by the existing Ox helper: "+map+
                "; use the verified 2 Penninsula.map via the runner Map parameter, not a timeout retry or artificial stone placement");
        }
        private unsafe bool LegacyOxReady(LegacyCivilianProof p)
        {
            Require(oxCapacityObservation!=null && oxCapacityObservation.Fault==null,oxCapacityObservation?.Fault??"Missing actual-tick Ox storage capacity request");
            if(oxCapacityObservation.Pending)return false;
            foreach(var pair in new[]{new KeyValuePair<int,uint>(oxQuarry,oxQuarryGlobal),new KeyValuePair<int,uint>(oxTether,oxTetherGlobal),
                new KeyValuePair<int,uint>(oxQuarryPile,oxQuarryPileGlobal)})
            {
                Require(BuildingAccess.TryGetById(pair.Key,out GameBuilding* b,out _) && b!=null && b->r_GlobalId==pair.Value && b->r_PlayerIdOwner==owner,
                    "Legacy original Ox production site changed during initialization");
                if(b->r_AliveState==AliveState.NeedsInit)return false;
                Require(b->r_AliveState==AliveState.IsAlive,"Legacy original Ox site is not alive");
            }
            ValidateOxSites();bool batch=false;OxProductionReadiness(out batch);return true;
        }
        private unsafe void SetupLegacyCivilianWorld(LegacyCivilianProof p)
        {
            Require(runtime.Workers.Ledger.Count==0,"Legacy setup requires no preexisting owned parks");
            civilianRemoval=new CivilianRemovalProof();Require(WorldMutationEvents.TryGetCurrentWorld(out civilianRemoval.Session,out civilianRemoval.World) && civilianRemoval.Session==session,"No original world");
            p.OriginalWorld=civilianRemoval.World;SetupCivilianRemoval(true);EnsureOxPrerequisites();
            RequireLegacyQuarryTerrain();GameBuilding* tether=null;
            Require(TryCreateNaturalQuarry(out p.Quarry,out p.Tether) && BuildingAccess.TryGetById(p.Tether,out tether,out _) && tether!=null,
                "Native Quarry/Tether creation failed after a verified matching terrain preflight; inspect the exact helper rejection counts");p.TetherGlobal=tether->r_GlobalId;
            // Same complete natural control-world contract as the accepted Ox case:
            // fresh real empty delivery yards, actual-tick capacity and original pile.
            EnsureOxDeliveryYard(p.Tether);CaptureOxSites(p.Quarry,p.Tether);LogOxAcquisition(-1);
            FindSite();SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,cx,cy);p.Command=actors[actors.Count-1];
        }
        private IEnumerable<AutomationWait> LegacyCivilian(AutomationRun run)
        {
            Require(!expectedEnabled,"Legacy OFF acceptance requires -off scenario");LegacyCivilianOff();
            var p=new LegacyCivilianProof{Fixture=this,OriginalSession=session,Path=PersistenceSavePath(run)};ArmLegacyCivilian(p);
            try
            {
                run.InSimulation(run.CurrentMission,()=>{SetupLegacyCivilianWorld(p);return true;});
                bool ready=false;
                for(int sample=0;sample<2400 && !ready;sample++)
                {
                    foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                    yield return run.WaitForTicks(run.CurrentMission,8);
                    foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                    ready=run.InSimulation(run.CurrentMission,()=>
                    {
                        bool sites=LegacyOxReady(p);p.Human=FindLegacyWorker(p,false);p.Ox=sites?FindLegacyWorker(p,true):null;
                        if(sample%128==0)log("legacy-natural-acquisition sample="+sample+" apple="+(p.Human!=null)+" ox="+(p.Ox!=null)+" sitesReady="+sites+
                            " capacityPending="+oxCapacityObservation.Pending+" actualCapacity="+(oxCapacityObservation.Capacity==null?"unavailable":oxCapacityObservation.Capacity.FreeAmount.ToString()));
                        return p.Human!=null && p.Ox!=null;
                    });
                }
                Require(ready,"No naturally employed Apple worker and tether-born travelling Ox qualified for strict historical receipts");
                yield return AutomationWait.Until("quiescent director before historical setup/save",()=>Director.instance.SafeToSave());Director.instance.FinishedSaving();
                run.InSimulation(run.CurrentMission,()=>
                {
                    LegacyCivilianOff();Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(p.Command.Tribe,p.Command.SourceX+10,p.Command.SourceY),"Control military move refused");
                    ParkHistoricalFixture(p,p.Human,false);ParkHistoricalFixture(p,p.Command,true);ParkHistoricalFixture(p,p.Ox,false);
                    // Reproduce the historical dormant save form through the actual
                    // ledger transition, with no native callback or state replay.
                    Require(runtime.Workers.Ledger.Drain(0,512,3,_=>WorkerResumeDecision.WaitForGeometry)==3,
                        "Historical parks did not enter their real Waiting metadata state");
                    Require(runtime.Workers.Ledger.Count==3 && runtime.Workers.Ledger.LegacyCivilianCount==1 && runtime.Workers.Ledger.TrySerialize(out p.Saved),
                        "Three actual historical parks did not enter the normal atomic archive");
                    Require(SimulationTickEvents.TryGetCurrentTick(out p.SaveTick),"Historical save native clock unavailable");
                    log("legacy-civilian-save-source human="+p.Human.Id+"/"+p.Human.Global+" command="+p.Command.Id+"/"+p.Command.Global+
                        " ox="+p.Ox.Id+"/"+p.Ox.Global+" settingsOFF=true waitingReceipts=true strictLiveFieldsDecoded=true sameCurrentNativeNormalization=true existingTryPark=true directFixtureFlagsWrites=false jobReplay=false");return true;
                });
                yield return AutomationWait.Until("quiescent director after historical normalization",()=>Director.instance.SafeToSave());Director.instance.FinishedSaving();
                var screen=CameraControls2D.instance.getScreenXYForSaveCentring(1,1);int centreX=-1,centreY=-1;GameMap.instance.getRotationCentre(ref centreX,ref centreY);
                Require(EngineInterface.SaveSaveGame(p.Path,screen.x,screen.y,centreX,centreY,false,false,false),"Normal native legacy save writer refused");
                Require(File.Exists(p.Path) && new FileInfo(p.Path).Length>0,"Native historical save was not materialized");
                run.InSimulation(run.CurrentMission,()=>{foreach(var t in p.Tickets)CheckLegacyControl(t,true);Require(SimulationTickEvents.TryGetCurrentTick(out int tick) && tick==p.SaveTick && runtime.Workers.Ledger.TrySerialize(out var bytes) && ParkedSameBytes(bytes,p.Saved),"Paused save changed historical receipts/native tick");return true;});
                foreach(var w in PersistenceQuitToMenu(run))yield return w;
                p.LoadArmed=true;foreach(var w in run.LoadSave(p.Path))yield return w;
                Require(Volatile.Read(ref p.Loaded)!=0 && p.Fault==null && run.CurrentMission!=null && run.CurrentMission.SessionId==p.LoadedSession,
                    "Actual legacy save lifecycle did not complete: "+p.Fault);
                yield return run.WaitForPlayState(run.CurrentMission);
                if(MainViewModel.Instance.Show_HUD_Briefing)MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
                yield return AutomationWait.Until("loaded legacy briefing closed",()=>!MainViewModel.Instance.Show_HUD_Briefing);
                foreach(var w in run.SetPause(run.CurrentMission,false))yield return w;
                for(int sample=0;sample<96 && !p.Preserved;sample++)yield return run.WaitForTicks(run.CurrentMission,4);
                yield return run.WaitForTicks(run.CurrentMission,32);
                foreach(var w in run.SetPause(run.CurrentMission,true))yield return w;
                run.InSimulation(run.CurrentMission,()=>
                {
                    Require(p.Fault==null && p.FirstTick && p.Marker && p.Preserved,"Legacy OFF preservation lacked actual first-tick/native-state proof: "+p.Fault);
                    LegacyCivilianOff();foreach(var ticket in p.Tickets)CheckLegacyControl(ticket,true);CheckProductionScannerDisabled();
                    Require(p.OwnMoves==0 && runtime.Workers.Ledger.Count==3 && runtime.Workers.Ledger.LegacyCivilianCount==1 &&
                        runtime.Workers.Ledger.TrySerialize(out var retained) && ParkedSameBytes(retained,p.Saved),
                        "OFF host replayed a job, destroyed a life or changed its protected archive");
                    log("legacy-civilian-result settingsOFF=true originalLifePreserved=true actualOccupancyPreserved=true oldWorkerReceiptRetained=true commandOxOriginalsPreserved=true ownMoves=0"+
                        " originalSession="+p.OriginalSession+" loadedSession="+p.LoadedSession+" originalWorld="+p.OriginalWorld+" loadedWorld="+p.LoadedWorld);return true;
                });
            }
            finally
            {
                p.Active=false;p.LoadArmed=false;if(civilianRemoval!=null)civilianRemoval.Active=false;
                appleWorld?.Dispose();
                if(ReferenceEquals(legacyCivilian,p))legacyCivilian=null;
            }
        }
    }
}
