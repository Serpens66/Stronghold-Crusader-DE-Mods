using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using APIShared;
using APIShared.Automation;
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
        private readonly int editorAcceptanceThread=Thread.CurrentThread.ManagedThreadId;
        private FieldInfo editorAcceptanceMonitor;
        private sealed class EditorSourceProof
        {
            internal Fixture Fixture;
            internal WorldMutationSource Source;
            internal Actor Actor;
            internal long Session,Generation,Batch;
            internal int Begins,Completes,Batches,OwnMoves;
            internal string Fault;
            internal bool Armed,WatchActor;
        }
        private static volatile EditorSourceProof editorSourceProof;
        private static bool editorSourceRegistered,editorMoveRegistered;
        private sealed class EditorSourceObserver:IWorldMutationObserver
        {
            public IWorldMutationInvocation TryBegin(WorldMutationContext context)
            {
                var p=editorSourceProof;if(p==null || !p.Armed)return null;
                try
                {
                    Require(context.Source==p.Source && context.SessionId==p.Session && context.WorldGeneration==p.Generation,
                        "Editor writer changed source/world");
                    Require(Thread.CurrentThread.ManagedThreadId==p.Fixture.editorAcceptanceThread &&
                        Monitor.IsEntered(p.Fixture.editorAcceptanceMonitor.GetValue(null)),"Editor writer lost its engine monitor/thread");
                    Require(p.Begins++==0,"Unexpected editor mirror/nested original in single-source proof");p.Batch=context.BatchId;
                    return new EditorSourceReceipt(p,context);
                }
                catch(Exception error){p.Fault=error.ToString();return null;}
            }
            public void OnBatchCompleted(WorldMutationBatchCompletion completion)
            {
                var p=editorSourceProof;if(p==null || !p.Armed)return;
                if(completion.BatchId!=p.Batch || completion.SessionId!=p.Session || completion.WorldGeneration!=p.Generation ||
                    !completion.ReturnedNormally || completion.ContainsOriginalFailure || !completion.IsCurrentWorld)
                    p.Fault="Editor outer batch did not complete its original current world";
                p.Batches++;
            }
        }
        private sealed class EditorSourceReceipt:IWorldMutationInvocation
        {
            private readonly EditorSourceProof proof;private readonly WorldMutationContext context;
            internal EditorSourceReceipt(EditorSourceProof proof,WorldMutationContext context){this.proof=proof;this.context=context;}
            public void Complete(WorldMutationCompletion completion)
            {
                if(!ReferenceEquals(completion.Context,context) || !completion.OriginalInvoked || !completion.OriginalReturned || completion.Failure!=null)
                    proof.Fault="Editor original was skipped or failed";
                proof.Completes++;
            }
        }
        private sealed class EditorMoveObserver:IUnitMoveObserver
        {
            public unsafe IUnitMoveInvocation TryBegin(UnitMoveContext context)
            {
                var p=editorSourceProof;
                if(p==null || !p.WatchActor || context.SessionId!=p.Session || context.UnitId!=p.Actor.Id || context.OriginOwnerGuid!=Plugin.Guid)return null;
                if(UnitAccess.TryGetById(p.Actor.Id,out GameUnit* unit,out _) && unit!=null && unit->r_GlobalId==p.Actor.Global)p.OwnMoves++;
                return null;
            }
        }
        private sealed class EditorToolsScope:IDisposable
        {
            private readonly List<Tuple<object,FieldInfo,bool>> saved=new List<Tuple<object,FieldInfo,bool>>();
            internal EditorToolsScope()
            {
                try
                {
                    var type=typeof(GameTileManagerAPI).Assembly.GetType("SHCDESE.DebugMenu.DebugMenuManager",true);
                    var property=type.GetProperty("Instance",BindingFlags.Public|BindingFlags.Static);
                    Require(property!=null,"SE editor tools singleton contract changed");object instance=property.GetValue(null,null);
                    foreach(string name in new[]{"_mirrorTool","_tilePropertyMutatorTool"})
                    {
                        var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
                        Require(field!=null && !field.IsStatic,"SE editor tool field contract changed: "+name);
                        object tool=field.GetValue(instance);Require(tool!=null,"SE editor tool unavailable: "+name);
                        string[] flags=name=="_mirrorTool"?new[]{"VerticalMirrorEnabled","HorizontalMirrorsEnabled","TileTypeMirrorEnabled",
                            "TileHeightMirrorEnabled","WallMirrorEnabled","BuildingMirrorEnabled","UnitMirrorEnabled","VegetationMirrorEnabled",
                            "AnimalMirrorEnabled","DeleteMirrorEnabled"}:new[]{"Enabled","ForceSetEnabled"};
                        foreach(string flagName in flags)
                        {
                            var flag=tool.GetType().GetField(flagName,BindingFlags.Public|BindingFlags.Instance);
                            Require(flag!=null && !flag.IsStatic && flag.FieldType==typeof(bool),"SE editor tool boolean changed: "+flagName);
                            saved.Add(Tuple.Create(tool,flag,(bool)flag.GetValue(tool)));flag.SetValue(tool,false);
                        }
                    }
                }
                catch{Dispose();throw;}
            }
            public void Dispose(){for(int i=saved.Count-1;i>=0;i--)saved[i].Item2.SetValue(saved[i].Item1,saved[i].Item3);saved.Clear();}
        }
        private IEnumerable<AutomationStep> EditorAcceptanceSteps()
        {
            yield return new AutomationStep("editor-owned-new-or-load-seven-real-writers",TimeSpan.FromMinutes(12),EditorAcceptance,false);
        }
        private T EditorSimulation<T>(AutomationRun run,Func<T> action)
        {
            Require(Thread.CurrentThread.ManagedThreadId==editorAcceptanceThread,"Editor acceptance requires Unity thread");
            var context=run.CurrentMission;
            Require(!stopped && context!=null && context.SessionId==session && context.IsEditor && context.Mode.IsMapEditor &&
                !context.Mode.IsRealMultiplayer,"Editor acceptance lost its exact local lifecycle");
            if(editorAcceptanceMonitor==null)
            {
                editorAcceptanceMonitor=typeof(EngineInterface).GetField("threadLock",BindingFlags.NonPublic|BindingFlags.Static);
                Require(editorAcceptanceMonitor!=null && editorAcceptanceMonitor.IsPrivate && editorAcceptanceMonitor.IsStatic &&
                    editorAcceptanceMonitor.FieldType==typeof(object),"Private engine monitor contract changed");
            }
            object monitor=editorAcceptanceMonitor.GetValue(null);Require(monitor!=null,"Editor engine monitor missing");
            Require(Monitor.TryEnter(monitor,100),"Editor engine monitor busy");
            try
            {
                Require(run.CurrentMission?.SessionId==session && !stopped && ReferenceEquals(editorAcceptanceMonitor.GetValue(null),monitor) &&
                    Director.instance!=null && Director.instance.SimRunning && GameData.Instance.lastGameState!=null,"Editor changed while acquiring its engine monitor");
                return action();
            }
            finally{Monitor.Exit(monitor);}
        }
        private IEnumerable<AutomationWait> EditorPause(AutomationRun run,bool value)
        {
            Require(!Director.instance.Paused,"Editor managed pause must be closed before native pause");
            if(EditorSimulation(run,()=>GamePlayerManagerAPI.Instance.IsLocalPaused())!=value)
                EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused,value?1:0,value?1:0);
            yield return AutomationWait.Until("editor native pause="+value,()=>EditorSimulation(run,()=>GamePlayerManagerAPI.Instance.IsLocalPaused())==value);
        }
        private void BindEditor(AutomationRun run,MissionStartKind kind,long oldSession,string path=null)
        {
            var context=run.CurrentMission;
            Require(context!=null && context.SessionId!=oldSession && context.StartKind==kind && context.IsEditor &&
                context.Mode.IsMapEditor && !context.Mode.IsRealMultiplayer,"Unexpected editor lifecycle provenance");
            if(path!=null)Require(!string.IsNullOrEmpty(context.FilePath) && string.Equals(Path.GetFullPath(context.FilePath),path,
                StringComparison.OrdinalIgnoreCase),"Editor loaded a different map");
            // The old Plugin.OnEnd may have called Stop. Rebind ONLY after the exact new editor lifecycle is proven.
            session=context.SessionId;stopped=false;
            owner=EditorDirector.instance.ActivePlayerID;Require(owner>=1 && owner<=8,"Editor player is outside the game ID domain");
            Require(EditorSimulation(run,()=>GameTileManagerAPI.Instance.GetCurrentMapSize())==400,"Editor did not create/load the requested400 map");
            EditorSimulation(run,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
            log("editor-bound kind="+kind+" session="+session+" owner="+owner+" size=400 previous="+oldSession);
        }
        private static string OwnedEditorMapPath(AutomationRun run)
        {
            string save=PersistenceSavePath(run),stem=Path.GetFileNameWithoutExtension(save);Guid id=Guid.Empty;
            Require(stem.StartsWith("SerpAcceptance-",StringComparison.Ordinal) && Guid.TryParseExact(stem.Substring(15),"N",out id) && id!=Guid.Empty,
                "Owned editor map has no exact automation run ID");
            string directory=Path.GetFullPath(BepInEx.Paths.CachePath);
            string path=Path.GetFullPath(Path.Combine(directory,"SerpEditor-"+id.ToString("N")+".map"));
            Require(path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) && !File.Exists(path),
                "Owned editor map already exists or escaped BepInEx cache");return path;
        }
        private IEnumerable<AutomationWait> EditorAcceptance(AutomationRun run)
        {
            yield return AutomationWait.Until("main menu before owned editor creation",()=>RescueScenarioSeries.MenuReady(run));
            Require(Owns(scenario),"Unowned editor scenario");PrepareDeterministicModSettings();
            enable(expectedEnabled);string ownMap=null;var tools=new EditorToolsScope();
            try
            {
                MainViewModel.Instance.ButtonMainMenuFunction("MapEditor");
                yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
                MainViewModel.Instance.MapEditorSetupFunction("400");MainViewModel.Instance.MapEditorSetupFunction("Freebuild");
                long oldSession=session;MainViewModel.Instance.MapEditorSetupFunction("Start");
                yield return AutomationWait.Until("genuine editor-created lifecycle",()=>run.CurrentMission?.StartKind==MissionStartKind.EditorCreated);
                yield return run.WaitForPlayState(run.CurrentMission);
                yield return AutomationWait.Until("editor native/render readiness",()=>EditorDirector.instance!=null && Director.instance!=null &&
                    Director.instance.SimRunning && GameData.Instance.lastGameState!=null);
                BindEditor(run,MissionStartKind.EditorCreated,oldSession);
                foreach(var wait in EditorPause(run,true))yield return wait;
                if(scenario.Contains("-load-"))
                {
                    ownMap=OwnedEditorMapPath(run);log("editor-owned-map path="+ownMap+" userMaps=false");
                    yield return AutomationWait.Until("owned editor map save safe",()=>Director.instance.SafeToSave());Director.instance.FinishedSaving();
                    int width=(int)((float)(UnityEngine.Screen.width/64)/PerfectPixelWithZoom.instance.getZoom());
                    int height=(int)((float)(UnityEngine.Screen.height/16)/PerfectPixelWithZoom.instance.getZoom());
                    var screen=CameraControls2D.instance.getScreenXYForSaveCentring(width,height);int x=-1,y=-1;GameMap.instance.getRotationCentre(ref x,ref y);
                    Require(EngineInterface.SaveSaveGame(ownMap,screen.x,screen.y,x,y,false,false,true),"Owned native editor-map save failed");
                    Require(File.Exists(ownMap) && new FileInfo(ownMap).Length>0,"Owned editor map was not materialized");
                    oldSession=session;foreach(var wait in PersistenceQuitToMenu(run))yield return wait;
                    var header=MapFileManager.Instance.GetFileInfoFromFileName(ownMap,ownMap,0,true);
                    Require(header!=null && header.isMapEditable(),"Owned map header is not editor-loadable");
                    GameData.Instance.SetMissionTextFromHeader(header);MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
                    Director.instance.SetPausedState(false);
                    Require(EditorDirector.instance.loadMapIntoEditor(header.filePath,header.standAlone_filename),"Owned map load refused");
                    yield return AutomationWait.Until("genuine editor-loaded lifecycle",()=>run.CurrentMission?.StartKind==MissionStartKind.EditorLoaded && run.CurrentMission.SessionId!=oldSession);
                    yield return run.WaitForPlayState(run.CurrentMission);
                    yield return AutomationWait.Until("loaded editor native/render readiness",()=>Director.instance.SimRunning && GameData.Instance.lastGameState!=null);
                    BindEditor(run,MissionStartKind.EditorLoaded,oldSession,ownMap);
                    foreach(var wait in EditorPause(run,true))yield return wait;
                }
                foreach(EditorAcceptanceWriter writer in Enum.GetValues(typeof(EditorAcceptanceWriter)))
                    foreach(var wait in EditorWriterCase(run,writer))yield return wait;
                EditorSimulation(run,()=>{Plugin.SetGeneralFixtureSession(false);return true;});
                foreach(var wait in PersistenceQuitToMenu(run))yield return wait;
                log("editor-acceptance-complete enabled="+expectedEnabled+" loaded="+scenario.Contains("-load-")+
                    " sources=7 realOriginal=true noops=7 intentionalDeletion=true heightOnlyTrapCoverage=false activeNextCoverage=false");
            }
            finally
            {
                var proof=editorSourceProof;if(proof!=null && ReferenceEquals(proof.Fixture,this)){proof.Armed=false;proof.WatchActor=false;editorSourceProof=null;}
                if(!stopped && run.CurrentMission?.SessionId==session && run.CurrentMission.IsEditor)
                {
                    try{EditorSimulation(run,()=>{Plugin.SetGeneralFixtureSession(false);return true;});}
                    catch(Exception error){log("editor-cleanup-session-disable-failed "+error);}
                }
                tools.Dispose();
                if(ownMap!=null && File.Exists(ownMap)){File.Delete(ownMap);Require(!File.Exists(ownMap),"Owned editor map cleanup failed");}
            }
        }
        private sealed class EditorCell
        {
            internal int Tile,Height,Type,Building,Vegetation;internal uint Flags;internal byte Connections;
            public override bool Equals(object other)=>other is EditorCell cell && Tile==cell.Tile && Height==cell.Height && Type==cell.Type &&
                Building==cell.Building && Vegetation==cell.Vegetation && Flags==cell.Flags && Connections==cell.Connections;
            public override int GetHashCode()=>Tile;
        }
        private EditorCell[] EditorCells(int x,int y)
        {
            var result=new List<EditorCell>();var tiles=GameTileManagerAPI.Instance;
            for(int yy=y-2;yy<=y+2;yy++)for(int xx=x-2;xx<=x+2;xx++)
            {
                int tile=runtime.Native.Tile(xx,yy);Require(tile>=0,"Editor logical observation escaped active map");
                result.Add(new EditorCell{Tile=tile,Height=tiles.GetTileHeight(tile),Type=(int)tiles.GetTileType(tile),
                    Flags=(uint)tiles.GetTilePropertyFlag(tile),Building=tiles.GetTileBuildingId(tile),Vegetation=tiles.GetTileVegetationId(tile),
                    Connections=runtime.Native.Connections(tile)});
            }
            return result.ToArray();
        }
        private static bool SameEditorCells(EditorCell[] a,EditorCell[] b)
        {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))return false;return true;}
        private EditorSourceProof ArmEditorSource(EditorAcceptanceWriter kind,Actor actor)
        {
            if(!editorSourceRegistered)
            {
                const WorldMutationSource mask=WorldMutationSource.EditorTileType|WorldMutationSource.EditorTileTypeClear|WorldMutationSource.EditorHeightPreset|
                    WorldMutationSource.EditorHeightRaise|WorldMutationSource.EditorHeightNumber|WorldMutationSource.EditorHeightEven|WorldMutationSource.EditorDelete;
                Require((WorldMutationEvents.AvailableSources&mask)==mask,"Not all seven audited editor sources are available");
                Require(WorldMutationEvents.TryRegister(Plugin.Guid,"acceptance-editor-sources",new EditorSourceObserver(),out string reason,
                    sources:mask,enabled:()=>editorSourceProof!=null && editorSourceProof.Armed),"Editor source observer unavailable: "+reason);
                editorSourceRegistered=true;
            }
            if(!editorMoveRegistered)
            {
                Require(UnitMoveEvents.TryRegister(Plugin.Guid,"acceptance-editor-own-moves",new EditorMoveObserver(),out string reason,
                    enabled:()=>editorSourceProof!=null && editorSourceProof.WatchActor),"Editor own-move observer unavailable: "+reason);
                editorMoveRegistered=true;
            }
            Require(WorldMutationEvents.TryGetCurrentWorld(out long actualSession,out long generation) && actualSession==session,"Editor native world unavailable");
            return editorSourceProof=new EditorSourceProof{Fixture=this,Actor=actor,Source=EditorAcceptanceBrushes.Source(kind),Session=session,Generation=generation,WatchActor=true};
        }
        private void EditorOriginal(EditorSourceProof proof,EditorAcceptanceWriter writer,int width,int value)
        {
            proof.Begins=proof.Completes=proof.Batches=0;proof.Batch=0;proof.Armed=true;
            try{EditorAcceptanceBrushes.Call(writer,owner,cx,cy,width,value);}
            finally{proof.Armed=false;}
            Require(proof.Fault==null && proof.Begins==1 && proof.Completes==1 && proof.Batches==1,"Editor source original/outer-batch proof failed: "+proof.Fault);
        }
        private IEnumerable<AutomationWait> EditorWriterCase(AutomationRun run,EditorAcceptanceWriter writer)
        {
            int width=writer==EditorAcceptanceWriter.Even?2:1,value=writer==EditorAcceptanceWriter.Preset?4:writer==EditorAcceptanceWriter.Raise?1:0;
            Actor actor=null;EditorSourceProof proof=null;long rescues=0,attempts=0,noopAttempts=0;int noopMoves=0;EditorCell[] changed=null;
            EditorSimulation(run,()=>{
                Plugin.SetGeneralFixtureSession(false);Require(actors.Count==0,"Previous editor actors were not retired");
                cx=352+16*(int)writer;cy=400;
                Require(FlatSquare(cx,cy,6,out elevation) && elevation==8,"Owned editor case requires its exact empty flat13x13 atheight8");
                if(writer==EditorAcceptanceWriter.Clear)EditorAcceptanceBrushes.Call(EditorAcceptanceWriter.Type,owner,cx,cy,1);
                if(writer==EditorAcceptanceWriter.Number)EditorAcceptanceBrushes.Call(EditorAcceptanceWriter.Raise,owner,cx,cy,1,1);
                if(writer==EditorAcceptanceWriter.Even)EditorAcceptanceBrushes.Call(EditorAcceptanceWriter.Raise,owner,cx,cy-1,1,2);
                // Height writers are effect/no-op controls with an untouched nearby actor. Type and Delete operate ON their actor.
                int actorX=writer==EditorAcceptanceWriter.Type || writer==EditorAcceptanceWriter.Delete?cx:cx+4;
                SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN,actorX,cy);actor=actors[0];return true;
            });
            foreach(var wait in EditorPause(run,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,8);
            foreach(var wait in EditorPause(run,true))yield return wait;
            EditorSimulation(run,()=>{
                VerifyLiveAndRemember();Require(runtime.Native.OrdinaryGround(runtime.Native.Tile(actor.SourceX,actor.SourceY)) && HasStructuralExit(actor),"Editor actor has no initial ordinary exit");
                proof=ArmEditorSource(writer,actor);Plugin.SetGeneralFixtureSession(expectedEnabled);
                Require(Plugin.GeneralObserver.IsActive==expectedEnabled,"Editor feature activation mismatch");
                rescues=Plugin.GeneralObserver.RescueCount;attempts=Plugin.GeneralObserver.AttemptCount;
                var before=EditorCells(cx,cy);EditorOriginal(proof,writer,width,value);changed=EditorCells(cx,cy);
                if(writer==EditorAcceptanceWriter.Delete)RequireEditorDeleted(actor,false);
                else Require(!SameEditorCells(before,changed),"Real editor writer produced no logical geometry delta: "+writer);
                if(writer==EditorAcceptanceWriter.Type)Require(!runtime.Native.OrdinaryGround(runtime.Native.Tile(cx,cy)),"Real Sea brush did not invalidate actorCurrent");
                Require(Plugin.GeneralObserver.RescueCount==rescues && Plugin.GeneralObserver.AttemptCount==attempts && proof.OwnMoves==0,
                    "Editor recovery ran synchronously inside the writer");
                log("editor-source writer="+writer+" enabled="+expectedEnabled+" unit="+actor.Id+"/"+actor.Global+
                    " current="+actor.SourceX+","+actor.SourceY+" delta=true original=1 batch=1 deferred=true");return true;
            });
            foreach(var wait in EditorPause(run,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,16);
            foreach(var wait in EditorPause(run,true))yield return wait;
            EditorSimulation(run,()=>{
                if(writer==EditorAcceptanceWriter.Delete)RequireEditorDeleted(actor);
                else
                {
                    if(writer!=EditorAcceptanceWriter.Type || expectedEnabled || EditorActorAlive(actor))CheckEditorActor(actor);
                    if(writer==EditorAcceptanceWriter.Type && expectedEnabled)Require(!AtSource(actor) &&
                        runtime.Native.OrdinaryGround(runtime.Native.Tile(actor.X,actor.Y)) && HasStructuralExit(actor),"Editor Sea actor did not reach a safe ordinary target");
                    else if(writer!=EditorAcceptanceWriter.Type)Require(AtSource(actor) && HasStructuralExit(actor),"Unaffected editor control was moved or blocked");
                }
                long expectedRescues=writer==EditorAcceptanceWriter.Type && expectedEnabled?1:0;
                Require(Plugin.GeneralObserver.RescueCount==rescues+expectedRescues,"Unexpected editor spatial recovery count");
                if(expectedRescues==0)Require(proof.OwnMoves==0,"Editor no-rescue control received an own native move");
                long noopRescues=Plugin.GeneralObserver.RescueCount;noopAttempts=Plugin.GeneralObserver.AttemptCount;int ownMoves=proof.OwnMoves;noopMoves=ownMoves;
                var before=EditorCells(cx,cy);EditorOriginal(proof,writer,width,writer==EditorAcceptanceWriter.Raise?0:value);
                Require(SameEditorCells(before,EditorCells(cx,cy)),"Repeated editor writer was not a logical no-op: "+writer);
                Require(Plugin.GeneralObserver.RescueCount==noopRescues && Plugin.GeneralObserver.AttemptCount==noopAttempts && proof.OwnMoves==ownMoves,
                    "Editor no-op changed recovery synchronously");changed=EditorCells(cx,cy);return true;
            });
            foreach(var wait in EditorPause(run,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,16);
            foreach(var wait in EditorPause(run,true))yield return wait;
            EditorSimulation(run,()=>{
                Require(SameEditorCells(changed,EditorCells(cx,cy)),"Editor logical geometry drifted after its no-op");
                Require(Plugin.GeneralObserver.RescueCount==rescues+(writer==EditorAcceptanceWriter.Type && expectedEnabled?1:0) &&
                    Plugin.GeneralObserver.AttemptCount==noopAttempts && proof.OwnMoves==noopMoves,"Editor no-op scheduled an extra recovery attempt/rescue");
                bool outcomeAlive=EditorActorAlive(actor);
                if(writer==EditorAcceptanceWriter.Delete){RequireEditorDeleted(actor);Require(proof.OwnMoves==0,"Intentional deletion received an own native move");actors.Clear();}
                else if(writer==EditorAcceptanceWriter.Type && !expectedEnabled && !EditorActorAlive(actor))actors.Clear();
                else{CheckEditorActor(actor);RetireActors();}
                Require(Plugin.GeneralObserver.PendingCount==0,"Editor case retained causal rescue evidence");
                log("editor-result writer="+writer+" enabled="+expectedEnabled+" sourceOriginal=true noopOriginal=true ownMoves="+proof.OwnMoves+
                    " intentionalDelete="+(writer==EditorAcceptanceWriter.Delete)+" originalAlive="+outcomeAlive+
                    " baselineFateObserved=true activeNextCoverage=false");
                proof.WatchActor=false;Plugin.SetGeneralFixtureSession(false);return true;
            });
            foreach(var wait in EditorPause(run,false))yield return wait;
            yield return run.WaitForTicks(run.CurrentMission,8);
            foreach(var wait in EditorPause(run,true))yield return wait;
        }
        private unsafe bool EditorActorAlive(Actor actor)
        {
            return UnitAccess.TryGetById(actor.Id,out GameUnit* unit,out _) && unit!=null && unit->r_GlobalId==actor.Global && UnitAccess.IsReallyAlive(unit);
        }
        private unsafe void CheckEditorActor(Actor actor)
        {
            CheckActor(actor,true);var unit=Resolve(actor);
            int next=runtime.Native.Tile(unit->r_NextTilePositionX2,unit->r_NextTilePositionY2);
            Require(next>=0 && unit->r_MovementSubstep<=8 &&
                (unit->r_NextPositionTileId2==0 || unit->r_NextPositionTileId2==(uint)next),"Editor actor acceptedNext/pose became incoherent");
            if(unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4)
                Require(runtime.SnapshotUnit(actor.Id,true,false)?.OrdinaryStep==true,"Editor actor acquired an unsupported active ordinary step");
        }
        private unsafe void RequireEditorDeleted(Actor actor,bool unlinked=true)
        {
            bool same=UnitAccess.TryGetById(actor.Id,out GameUnit* unit,out _) && unit!=null && unit->r_GlobalId==actor.Global;
            if(!unlinked)Require(same && unit->r_AliveState==AliveState.MarkedForDeletion && unit->r_CurrentHealth==actor.Health &&
                unit->r_TribeId==actor.Tribe && unit->r_CurrentTilePositionX==actor.SourceX && unit->r_CurrentTilePositionY==actor.SourceY,
                "Immediate editor Delete did not merely mark the original physical actor for deletion");
            if(same)Require(!UnitAccess.IsReallyAlive(unit),"Intentional editor deletion revived or retained its original alive unit");
            if(unlinked)Require(CountInTileChain(runtime.Native.Tile(actor.SourceX,actor.SourceY),actor.Id)==0,"Editor-deleted actor remains in its old physical tile chain");
        }
    }
}
