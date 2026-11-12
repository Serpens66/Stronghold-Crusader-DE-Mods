using System;
using System.Diagnostics;
using System.Threading;
using APIShared;
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using UnityEngine;

namespace BugfixesAndQoL.AreaDemolition
{
    internal sealed class Runtime
    {
        private const int BulldozerAction=6; // Vanilla MainControls action state, not a building/mapper ID.
        private readonly ModApiClient api;
        private readonly NativeDemolition native;
        private readonly DemolitionSettings settings;
        private readonly ManualLogSource log;
        private readonly Func<bool> enabled;
        private bool presentationReady,localActive;
        private bool LocalEnabled => presentationReady && enabled();
        private readonly SelectionGesture gesture=new SelectionGesture();
        private readonly ScreenSelection screenSelection=new ScreenSelection();
        private readonly PreviewWorkPolicy previewPolicy=new PreviewWorkPolicy(Stopwatch.Frequency);
        private readonly PreviewRootSelection rootSelection=new PreviewRootSelection();
        private ScreenHitGeometry quoteGeometry;
        private ProjectionStamp quoteStamp,completedStamp;
        private IUiFrameWorkRegistration work;
        private IMissionLifecycleCapability lifecycle;
        private IMissionPlayStateReadinessCapability readiness;
        private DemolitionView view;
        private SelectionRenderer renderer;
        private DemolitionNetwork network;
        private MissionContext mission;
        private int actor, lastFrame=-1;
        private long capturedSession;
        private DemolitionTarget[] targets=Array.Empty<DemolitionTarget>();
        private NativeDemolition.PreviewQuote previewQuote;
        private NativeDemolition.PreviewPreparation preparation;
        private NativeDemolition.GroundPreparation groundPreparation;
        private NativeDemolition.PreviewSnapshot preparingSnapshot;
        private ScreenSelection.ProjectionContext projectionContext;
        private ScreenSelection.GroundTilePreparation groundCapture;
        private ScreenSelection.HitGeometryPreparation geometryPreparation;
        private enum PreviewPhase { None,Snapshot,GroundCapture,GroundRoots,Geometry,Quote }
        private PreviewPhase previewPhase;
        private ScreenRect quoteArea, finalArea;
        private SelectionSpec quoteSelection, finalSelection;
        private bool quoteIsFinal, finalCaptureRequested, finalStartAttempted;
        private int primaryReleaseFrame=-1,primaryDownObservedFrame=-1;
        private DemolitionTarget[] pendingCommit;
        private ScreenRect pendingArea;
        private long pendingSession;
        private int pendingActor;
        private bool pendingPresented;
        internal Runtime(ModApiClient api,NativeDemolition native,DemolitionSettings settings,DemolitionNetwork network,Func<bool> enabled,ManualLogSource log)
        {this.api=api;this.native=native;this.settings=settings;this.network=network;this.enabled=enabled;this.log=log;}
        internal void Register()
        {
            if(!api.Ui.TryRegisterBeforeRender("area-demolition.presentation",Present,out work,out string reason)) throw new InvalidOperationException(reason);
            view=new DemolitionView(settings,log,()=>LocalEnabled,()=>gesture.Owned || primaryReleaseFrame==Time.frameCount,()=>primaryReleaseFrame=-1);
            renderer=new SelectionRenderer(log);
            renderer.Failed+=reason=>{CancelSelection();log.LogError(AreaDemolitionFeature.Stamp("AREA_DEMOLITION_PREVIEW_DISABLED: "+reason));Wake();};
            renderer.Presented+=snapshot=>{if(ReferenceEquals(snapshot,pendingCommit)){pendingPresented=true;Wake();}};
            network.Changed+=Wake;
            settings.Changed+=Wake;
            view.MenuChanged+=MenuChanged;
            if(!PresentationEvents.TryRegister(PresentationOperation.GuiChecks,BugfixesAndQoLPlugin.PluginGuid,"area-demolition.hud",null,GuiChecks,out reason)) throw new InvalidOperationException(reason);
            if(!api.TryGetMissionPlayStateReadiness(out readiness,out var diagnostic) ||
               !readiness.TryRegisterObserver("area-demolition.play-state",Ready,out diagnostic) ||
               !api.TryGetMissionLifecycle(out lifecycle,out diagnostic) ||
               !lifecycle.TryRegisterObserver("area-demolition.mission",Started,Ended,null,out diagnostic)) throw new InvalidOperationException(diagnostic?.Reason);
            Application.focusChanged+=FocusChanged;
            presentationReady=true;Wake();
        }
        internal void RequestRefresh()=>Wake();
        internal void DisablePresentation(){presentationReady=false;work?.SetContinuous(false);view?.Reset();renderer?.Clear();}
        private void Started(MissionLifecycleNotification note)
        {
            Volatile.Write(ref mission,note.Context);
            long session=note.Context.SessionId;
            api.Ui.TryPost(()=>{if(mission?.SessionId!=session)return;ResetScene();Wake();});
            Wake();
        }
        private void Ready(MissionPlayStateReadyNotification note)
        {
            if(readiness.Current?.SessionId!=note.Context.SessionId) return;
            mission=note.Context; Wake();
        }
        private void Ended(MissionLifecycleNotification note)
        {
            if(mission?.SessionId!=note.Context.SessionId) return;
            Volatile.Write(ref mission,null);
            // A replacement can publish Start before this deferred UI action executes.
            api.Ui.TryPost(()=>{if(Volatile.Read(ref mission)!=null)return;ResetScene();work.SetContinuous(false);});
        }
        private void ResetScene()
        {gesture.Reset();primaryReleaseFrame=primaryDownObservedFrame=-1;CancelQuote();previewPolicy.Reset();pendingCommit=null;pendingPresented=false;targets=Array.Empty<DemolitionTarget>();renderer.Clear();view.Reset();}
        private void GuiChecks(PresentationPostEventArgs note)
        { if(LocalEnabled && note.OriginalCompleted && MainViewModel.viewModelLoaded) view.RefreshHud(); }
        private void Wake()=>work?.Request();
        private void MenuChanged(){if(view.IsMenuOpen) CancelSelection();Wake();}
        private void FocusChanged(bool focus){if(presentationReady && !focus){CancelSelection();view.CloseMenu();if(network.IsBusy)network.CancelPending();Wake();}}
        internal bool CancelWithEscape()
        {
            if(!presentationReady || !LocalEnabled && !gesture.Owned)return false;
            bool consumed=gesture.Owned || pendingCommit!=null || finalCaptureRequested || view.IsMenuOpen || network.IsBusy;
            if(consumed){CancelSelection();view.CloseMenu();network.CancelPending();Wake();}
            return consumed;
        }
        private void ClearPreviewWork()
        {previewQuote=null;preparation=null;groundPreparation=null;preparingSnapshot=null;groundCapture=null;geometryPreparation=null;projectionContext=null;previewPhase=PreviewPhase.None;quoteGeometry=null;}
        private void CancelQuote(){ClearPreviewWork();finalCaptureRequested=false;rootSelection.Clear();completedStamp=null;}
        private void CancelSelection(){gesture.Cancel();CancelQuote();pendingCommit=null;pendingPresented=false;targets=Array.Empty<DemolitionTarget>();renderer.Clear();view.SetSelectionFrame(null);}
        internal bool HandlePrimary(EditorDirector director,int state)
        {
            if(!presentationReady)return false;
            if(state==1)primaryDownObservedFrame=Time.frameCount;
            if(gesture.Owned)
            {
                if(!LocalEnabled)CancelSelection();
                if(state==2) UpdatePointer(director);
                if((state==3 || state==0) && Input.GetMouseButtonUp(0)) CompletePrimary(director,true);
                else if((state==3 || state==0) && !Input.GetMouseButton(0)) CompletePrimary(director,false);
                return true;
            }
            if(!LocalEnabled)return false;
            if(view.IsInputShieldActive) return true;
            if(state==1 && MainControls.instance!=null && MainControls.instance.CurrentAction==BulldozerAction && IsPointerOverUi())return true;
            if(state==1 && (pendingCommit!=null || finalCaptureRequested))CancelSelection();
            if(state!=1 || !CanCapture(director,out int owner) || !TryPointerTile(director,out int x,out int y,true)) return false;
            actor=owner;capturedSession=mission.SessionId;
            bool shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
            DemolitionTarget start=native.AtTile(x,y,actor);
            var mouse=Input.mousePosition;
            primaryReleaseFrame=-1;
            CancelQuote();targets=Array.Empty<DemolitionTarget>();renderer.Clear();previewPolicy.Reset();
            gesture.Begin(mouse.x,mouse.y,new SelectionSpec(settings.Mask,shift,start));
            director.clearMouseStateForEngine();Wake();
            return true;
        }
        private bool IsPointerOverUi() => !view.TryIsPointerOverUi(Input.mousePosition,out bool overUi) || overUi;
        private bool CanCapture(EditorDirector director,out int owner) => CanContinue(director,out owner) && !IsPointerOverUi();
        private bool CanContinue(EditorDirector director,out int owner)
        {
            owner=0;
            var current=Volatile.Read(ref mission);
            if(!LocalEnabled || !native.Available || !renderer.IsReady || current==null || readiness.Current?.SessionId!=current.SessionId || !Application.isFocused ||
                director==null || MainControls.instance==null || MainControls.instance.CurrentAction!=BulldozerAction) return false;
            owner=director.ActivePlayerID;
            if(owner<=0) owner=PlayerPerspectiveAPI.GetInputControlledPlayerId();
            return owner>=1 && owner<=8;
        }
        private bool UpdatePointer(EditorDirector director)
        {
            if(mission?.SessionId!=capturedSession || !Application.isFocused || MainControls.instance==null || MainControls.instance.CurrentAction!=BulldozerAction ||
                !CanContinue(director,out int currentActor) || currentActor!=actor)
            {CancelSelection();return false;}
            var mouse=Input.mousePosition;
            bool valid=TryPointerTile(director,out _,out _);
            gesture.Move(mouse.x,mouse.y,valid,Math.Max(.5f,Screen.height/1080f));
            if(gesture.SetSameType(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)))
            {ClearPreviewWork();rootSelection.Clear();completedStamp=null;targets=Array.Empty<DemolitionTarget>();renderer.Clear();}
            return true;
        }
        private void CompletePrimary(EditorDirector director,bool actualRelease)
        {
            if(!gesture.Owned) return;
            if(actualRelease && gesture.Stage!=GestureStage.Cancelled) UpdatePointer(director);
            if(!actualRelease) gesture.Cancel();
            var area=gesture.Area;var selection=gesture.Selection;
            var result=gesture.Release(director==null || IsPointerOverUi() || view.IsInputShieldActive);
            primaryReleaseFrame=Time.frameCount;
            if(result==GestureRelease.Single) SingleClick(director);
            else if(result==GestureRelease.Area) Commit(area,selection);
            if(pendingCommit==null){renderer.Clear();if(!finalCaptureRequested)view.SetSelectionFrame(null);}
            targets=Array.Empty<DemolitionTarget>();Wake();
        }
        internal void AfterInput(EditorDirector director)
        {
            if(!presentationReady)return;
            view.AfterInputFrame();
            if(!LocalEnabled && gesture.Owned)CancelSelection();
            if(Input.GetMouseButtonDown(1) && (gesture.Owned || pendingCommit!=null || finalCaptureRequested)){CancelSelection();return;}
            // Vanilla may suppress the setter entirely on our raw visual hit. Recover only an
            // otherwise unobserved physical down, never a down already passed to Vanilla.
            if(LocalEnabled && !gesture.Owned && Input.GetMouseButtonDown(0) && primaryDownObservedFrame!=Time.frameCount &&
                MainControls.instance!=null && MainControls.instance.CurrentAction==BulldozerAction && director.overUI() && !IsPointerOverUi())
                HandlePrimary(director,1);
            if(!gesture.Owned) return;
            if(gesture.Stage!=GestureStage.Cancelled) UpdatePointer(director);
            if(Input.GetMouseButtonUp(0)) CompletePrimary(director,true);
            else if(!Input.GetMouseButton(0)) CompletePrimary(director,false);
        }
        private bool TryPointerTile(EditorDirector director,out int x,out int y,bool fresh=false)
            => TryPointerTile(director,out x,out y,out _,fresh);
        private bool TryPointerTile(EditorDirector director,out int x,out int y,out int clickDepth,bool fresh)
        {
            x=y=0;clickDepth=-32767;
            if(director==null || IsPointerOverUi() || MainControls.instance==null || GameMap.instance==null) return false;
            var mouse=Input.mousePosition;
            if(mouse.x<0 || mouse.y<0 || mouse.x>=Screen.width || mouse.y>=Screen.height) return false;
            float tx=0,ty=0;
            if(fresh)
            {
                Vector3 world=default;Vector3Int cell=default;
                GameMap.instance.CalcMapTileFromMousePos(mouse,ref world,ref cell,ref clickDepth,true,true);
                tx=cell.x;ty=cell.y;
            }
            else MainControls.instance.getMouseMapTilePosition(ref tx,ref ty);
            var tile=GameMap.instance.getMapTile((int)tx,(int)ty);
            if(tile==null)return false;x=tile.gameMapX;y=tile.gameMapY;return true;
        }
        private void SingleClick(EditorDirector director)
        {
            var context=Volatile.Read(ref mission);
            if(context==null || context.SessionId!=capturedSession || !CanCapture(director,out int currentActor) || currentActor!=actor ||
                !TryPointerTile(director,out int x,out int y,out int buildingDepth,true) || !ManagedInput.TryGetSelectionOn(director,out bool selectionOn)) return;
            int[] units=null;int troopDepth=-32767;
            var mouse=Input.mousePosition;
            GameMap.instance.grabTroopsOnScreen(Vector2.zero,Vector2.zero,ref units,new Vector2(mouse.x,mouse.y),ref troopDepth);
            int firstUnit=units==null || units.Length==0?0:units[0];
            int issuer=actor;bool inGameNotEditor=!context.IsEditor;
            // Preserve the public single-command wrapper and its mod hooks. Refresh only the
            // point cursor it consumes; do not replay selection, orders or a whole input frame.
            native.VanillaSingle(x,y,issuer,firstUnit,buildingDepth-49,troopDepth,selectionOn,
                ()=>EngineInterface.DeleteBuilding(x,y,issuer,inGameNotEditor,1));
        }
        private void Commit(ScreenRect area,SelectionSpec selection)
        {
            if(mission?.SessionId!=capturedSession)return;
            if(!renderer.IsReady || network.IsBusy || selection.SameType && selection.Start==null)return;
            // Release freezes the gesture, not a long synchronous native scan on the input callback.
            ClearPreviewWork();finalArea=area;finalSelection=selection;finalCaptureRequested=true;finalStartAttempted=false;
            view.SetSelectionFrame(area);Wake();
        }
        private bool BeginPreview(ScreenRect area,SelectionSpec selection,bool final)
        {
            previewPolicy.Began(Stopwatch.GetTimestamp());
            quoteArea=area;quoteSelection=selection;quoteIsFinal=final;
            quoteStamp=new ProjectionStamp();
            projectionContext=screenSelection.CaptureProjection();
            preparation=native.BeginPreviewPreparation(actor,selection);
            previewPhase=PreviewPhase.Snapshot;
            return true;
        }
        private bool NeedsGround(SelectionSpec selection) => selection.SameType ?
            selection.Start!=null && (selection.Start.Kind==TargetKind.Wall || selection.Start.Kind==TargetKind.DugMoat) :
            (selection.Filters&((1<<(int)TargetGroup.Walls)|(1<<(int)TargetGroup.Fortifications)|(1<<(int)TargetGroup.DugMoats)))!=0;
        private bool PreparePreview(long deadline)
        {
            while(Stopwatch.GetTimestamp()<deadline)
            {
                switch(previewPhase)
                {
                    case PreviewPhase.Snapshot:
                        if(!native.TryStepPreviewPreparation(preparation,deadline,out bool snapshotDone,out var snapshot) || !snapshotDone)return false;
                        preparingSnapshot=snapshot;preparation=null;
                        if(!quoteIsFinal)quoteArea=gesture.Area;
                        if(NeedsGround(quoteSelection))
                        {groundCapture=screenSelection.BeginGroundTileCapture(quoteArea,projectionContext);previewPhase=PreviewPhase.GroundCapture;}
                        else
                        {geometryPreparation=screenSelection.BeginHitGeometry(snapshot.HitCandidates,projectionContext,quoteArea);previewPhase=PreviewPhase.Geometry;}
                        break;
                    case PreviewPhase.GroundCapture:
                        if(!screenSelection.StepGroundTileCapture(groundCapture,deadline,out var ids))return false;
                        groundPreparation=native.BeginGroundPreparation(preparingSnapshot,ids,actor,quoteSelection);
                        groundCapture=null;previewPhase=PreviewPhase.GroundRoots;break;
                    case PreviewPhase.GroundRoots:
                        if(!native.TryStepGroundPreparation(groundPreparation,deadline,out bool groundDone) || !groundDone)return false;
                        groundPreparation=null;
                        if(!quoteIsFinal)quoteArea=gesture.Area;
                        geometryPreparation=screenSelection.BeginHitGeometry(preparingSnapshot.HitCandidates,projectionContext,quoteArea);previewPhase=PreviewPhase.Geometry;break;
                    case PreviewPhase.Geometry:
                        if(!screenSelection.StepHitGeometry(geometryPreparation,deadline,out quoteGeometry))return false;
                        previewQuote=native.BeginQuote(preparingSnapshot,geometryPreparation.SelectedRoots,actor,quoteSelection);
                        geometryPreparation=null;
                        preparingSnapshot=null;previewPhase=PreviewPhase.Quote;return true;
                    case PreviewPhase.Quote:return true;
                    default:return false;
                }
            }
            return false;
        }
        private sealed class ProjectionStamp
        {
            private readonly Camera camera=Camera.main;
            private readonly GameMap map=GameMap.instance;
            private readonly Matrix4x4 projection,view;
            private readonly int rotation,width=Screen.width,height=Screen.height;
            private readonly bool flattened=EngineInterface.FlattenedLandscape;
            internal ProjectionStamp(){projection=camera.projectionMatrix;view=camera.worldToCameraMatrix;rotation=(int)map.CurrentRotation();}
            internal bool IsCurrent() => camera!=null && camera==Camera.main && ReferenceEquals(map,GameMap.instance) &&
                camera.projectionMatrix==projection && camera.worldToCameraMatrix==view && rotation==(int)map.CurrentRotation() &&
                width==Screen.width && height==Screen.height && flattened==EngineInterface.FlattenedLandscape;
        }
        private void AdvancePreview()
        {
            bool final=finalCaptureRequested;
            // A camera-only move may not change a released area's frozen projection. A changed
            // map generation/rotation cannot safely reuse managed cells captured in earlier frames.
            if(previewPhase!=PreviewPhase.None && !projectionContext.IsMapCurrent) {CancelSelection();return;}
            if(previewPhase!=PreviewPhase.None && !quoteIsFinal && !quoteStamp.IsCurrent())ClearPreviewWork();
            if(!final && completedStamp!=null && !completedStamp.IsCurrent())
            {rootSelection.Clear();completedStamp=null;targets=Array.Empty<DemolitionTarget>();renderer.Clear();}
            long now=Stopwatch.GetTimestamp();
            long deadline=previewPolicy.Deadline(now);
            if(previewPhase==PreviewPhase.None && previewPolicy.CanBegin(now,final && !finalStartAttempted))
            {
                if(final)finalStartAttempted=true;
                if(!BeginPreview(final?finalArea:gesture.Area,final?finalSelection:gesture.Selection,final))return;
            }
            if(previewPhase==PreviewPhase.None || !PreparePreview(deadline))return;
            if(!native.TryStepQuote(previewQuote,PreviewWorkPolicy.MaximumRootsPerStep,deadline,out bool complete,out var snapshot))return;
            if(!quoteIsFinal)
            {
                rootSelection.UpdateProgress(quoteGeometry,previewQuote.OrderedRoots,previewQuote.NextRoot,previewQuote.CompletedRoots);completedStamp=quoteStamp;
                if(complete)ClearPreviewWork();
                return;
            }
            if(!complete)
            {
                rootSelection.UpdateProgress(quoteGeometry,previewQuote.OrderedRoots,previewQuote.NextRoot,previewQuote.CompletedRoots);
                rootSelection.Refresh(quoteArea);targets=rootSelection.Targets;
                renderer.SetSelection(quoteArea,targets);
                if(renderer.IsReady)view.SetSelectionFrame(quoteArea);
                return;
            }
            // Preserve the approved dependency graph before clearing the quote. A missing
            // parent later must not turn its surviving decoration into an independent job.
            snapshot=DemolitionPolicy.FreezeComponents(previewQuote.CompletedRoots);
            ClearPreviewWork();
            targets=snapshot;
            finalCaptureRequested=false;
            if(snapshot.Length==0){renderer.Clear();view.SetSelectionFrame(null);return;}
            // The exact completed immutable quote must still cross one successful camera render.
            pendingCommit=snapshot;pendingArea=quoteArea;pendingSession=capturedSession;pendingActor=actor;pendingPresented=false;
            renderer.SetSelection(pendingArea,pendingCommit);
            if(renderer.IsReady && pendingCommit!=null)view.SetSelectionFrame(pendingArea);
        }
        private void Present()
        {
            if(!presentationReady)return;
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            try
            {
                if(!LocalEnabled)
                {
                    if(localActive){localActive=false;CancelSelection();view.CloseMenu();network.CancelPending();}
                    // Preserve the owned release shield through its whole Noesis input frame.
                    if(!gesture.Owned && primaryReleaseFrame!=Time.frameCount)view.Reset();
                    work.SetContinuous(gesture.Owned || primaryReleaseFrame==Time.frameCount);return;
                }
                localActive=true;view.RefreshHud();
                if(mission==null){renderer.Clear();view.SetSelectionFrame(null);work.SetContinuous(view.IsInputShieldActive);return;}
                if(pendingCommit!=null || finalCaptureRequested)
                {
                    var director=EditorDirector.instance;
                    if(mission.SessionId!=capturedSession || !CanContinue(director,out int currentActor) || currentActor!=actor){CancelSelection();}
                    else if(finalCaptureRequested)
                    {AdvancePreview();}
                    else PresentPending();
                }
                else if(gesture.Stage==GestureStage.Dragging)
                {
                    var area=gesture.Area;
                    if(!network.IsBusy)AdvancePreview();
                    // Preparation may cancel on a changed map/rotation. Never recreate its
                    // old frame later in this same presentation callback.
                    if(gesture.Stage==GestureStage.Dragging)
                    {
                        if(rootSelection.Refresh(area)){targets=rootSelection.Targets;}
                        renderer.SetSelection(area,targets);
                        if(renderer.IsReady)view.SetSelectionFrame(area);
                    }
                }
                else
                {
                    view.SetSelectionFrame(null);
                }
                work.SetContinuous(gesture.Owned || finalCaptureRequested || pendingCommit!=null || view.IsInputShieldActive || network.IsBusy);
            }
            catch(Exception error){CancelSelection();log.LogError(AreaDemolitionFeature.Stamp("AREA_DEMOLITION_PRESENTATION_FAILED: "+error));work.SetContinuous(false);}
        }
        private void PresentPending()
        {
            if(pendingSession!=mission.SessionId){CancelSelection();return;}
            // Current identity/permission checks belong to each frozen component in Apply.
            // One stale target must not cancel unrelated, already presented targets.
            if(pendingPresented)
            {
                var frozen=pendingCommit;pendingCommit=null;pendingPresented=false;
                renderer.Clear();view.SetSelectionFrame(null);network.Submit(pendingActor,frozen);
            }
            else
            {
                renderer.SetSelection(pendingArea,pendingCommit);
                if(renderer.IsReady && pendingCommit!=null){view.SetSelectionFrame(pendingArea);}
            }
        }
    }
}
