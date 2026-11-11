using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using APIShared;
using APIShared.Presentation;
using BepInEx.Logging;
using CrusaderDE;
using UnityEngine;

namespace AreaDemolitionTest
{
    internal sealed partial class Runtime
    {
        private const int BulldozerAction=6; // Vanilla MainControls action state, not a building/mapper ID.
        private readonly ModApiClient api;
        private readonly NativeDemolition native;
        private readonly DemolitionSettings settings;
        private readonly ManualLogSource log;
        private readonly SelectionGesture gesture=new SelectionGesture();
        private readonly ScreenSelection screenSelection=new ScreenSelection();
        private readonly PreviewWorkPolicy previewPolicy=new PreviewWorkPolicy(Stopwatch.Frequency);
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
        private ScreenRect quoteArea, finalArea;
        private SelectionSpec quoteSelection, finalSelection;
        private bool quoteIsFinal, finalCaptureRequested, finalStartAttempted;
        private Matrix4x4 quoteProjection, quoteView;
        private int quoteRotation, quoteWidth, quoteHeight, primaryReleaseFrame=-1;
        private bool quoteFlattened;
        private string selectionStatus="";
        private long finalRequestedAt;
        private bool missionMarker;
        private long nextPendingCheck;
        private DemolitionTarget[] pendingCommit;
        private ScreenRect pendingArea;
        private long pendingSession;
        private int pendingActor;
        private bool pendingPresented;
        internal Runtime(ModApiClient api,NativeDemolition native,DemolitionSettings settings,ManualLogSource log)
        {this.api=api;this.native=native;this.settings=settings;this.log=log;}
        internal void Register()
        {
            if(!api.Ui.TryRegisterBeforeRender("presentation",Present,out work,out string reason)) throw new InvalidOperationException(reason);
            view=new DemolitionView(settings,log,()=>gesture.Owned || primaryReleaseFrame==Time.frameCount,()=>primaryReleaseFrame=-1);
            renderer=new SelectionRenderer(log);
            renderer.Failed+=reason=>{CancelSelection();log.LogError(Plugin.Stamp("AREA_DEMOLITION_PREVIEW_DISABLED: "+reason));Wake();};
            renderer.Presented+=snapshot=>{if(ReferenceEquals(snapshot,pendingCommit)){pendingPresented=true;Wake();}};
            network=new DemolitionNetwork(api,native,log);
            native.DiagnosticProduced+=message=>api.Ui.TryPost(()=>log.LogError(message));
            network.Changed+=Wake;
            settings.Changed+=Wake;
            view.MenuChanged+=MenuChanged;
            if(!PresentationEvents.TryRegister(PresentationOperation.GuiChecks,Plugin.Guid,"hud",null,GuiChecks,out reason)) throw new InvalidOperationException(reason);
            if(!api.TryGetMissionPlayStateReadiness(out readiness,out var diagnostic) ||
               !readiness.TryRegisterObserver("play-state",Ready,out diagnostic) ||
               !api.TryGetMissionLifecycle(out lifecycle,out diagnostic) ||
               !lifecycle.TryRegisterObserver("mission",Started,Ended,null,out diagnostic)) throw new InvalidOperationException(diagnostic?.Reason);
            Application.focusChanged+=FocusChanged;
        }
        private void Started(MissionLifecycleNotification note)
        {
            Volatile.Write(ref mission,note.Context); missionMarker=false;
            long session=note.Context.SessionId;
            api.Ui.TryPost(()=>{if(mission?.SessionId!=session)return;ResetScene();Wake();});
            Wake();
        }
        private void Ready(MissionPlayStateReadyNotification note)
        {
            if(readiness.Current?.SessionId!=note.Context.SessionId) return;
            mission=note.Context; network.Start(note.Context); Wake();
        }
        private void Ended(MissionLifecycleNotification note)
        {
            if(mission?.SessionId!=note.Context.SessionId) return;
            Volatile.Write(ref mission,null); network.End();
            // A replacement can publish Start before this deferred UI action executes.
            api.Ui.TryPost(()=>{if(Volatile.Read(ref mission)!=null)return;ResetScene();work.SetContinuous(false);});
        }
        private void ResetScene()
        {gesture.Reset();primaryReleaseFrame=-1;CancelQuote();previewPolicy.Reset();pendingCommit=null;pendingPresented=false;targets=Array.Empty<DemolitionTarget>();selectionStatus="";renderer.Clear();view.Reset();view.SetStatus("");}
        private void GuiChecks(PresentationPostEventArgs note)
        { if(note.OriginalCompleted && MainViewModel.viewModelLoaded) view.RefreshHud(); }
        private void Wake()=>work?.Request();
        private void MenuChanged(){if(view.IsMenuOpen) CancelSelection();Wake();}
        private void FocusChanged(bool focus){if(!focus){CancelSelection();view.CloseMenu();if(network.IsBusy)network.CancelPending();Wake();}}
        internal bool CancelWithEscape()
        {
            bool consumed=gesture.Owned || pendingCommit!=null || finalCaptureRequested || view.IsMenuOpen || network.IsBusy;
            if(consumed){CancelSelection();view.CloseMenu();network.CancelPending();Wake();}
            return consumed;
        }
        private void CancelQuote(){previewQuote=null;finalCaptureRequested=false;}
        private void CancelSelection(){gesture.Cancel();CancelQuote();pendingCommit=null;pendingPresented=false;targets=Array.Empty<DemolitionTarget>();selectionStatus="";renderer.Clear();view.SetSelectionFrame(null);view.SetStatus("");}
        internal bool HandlePrimary(EditorDirector director,int state)
        {
            if(gesture.Owned)
            {
                if(state==2) UpdatePointer(director);
                if((state==3 || state==0) && Input.GetMouseButtonUp(0)) CompletePrimary(director,true);
                else if((state==3 || state==0) && !Input.GetMouseButton(0)) CompletePrimary(director,false);
                return true;
            }
            if(view.IsInputShieldActive) return true;
            if(state==1 && (pendingCommit!=null || finalCaptureRequested))CancelSelection();
            if(state!=1 || !CanCapture(director,out int owner) || !TryPointerTile(director,out int x,out int y)) return false;
            actor=owner;capturedSession=mission.SessionId;
            bool shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
            DemolitionTarget start=native.AtTile(x,y,actor);
            var mouse=Input.mousePosition;
            primaryReleaseFrame=-1;
            previewPolicy.Reset();selectionStatus="";
            gesture.Begin(mouse.x,mouse.y,new SelectionSpec(settings.Mask,shift,start));
            director.clearMouseStateForEngine();Wake();
            return true;
        }
        private bool CanCapture(EditorDirector director,out int owner) => CanContinue(director,out owner) && !director.overUI();
        private bool CanContinue(EditorDirector director,out int owner)
        {
            owner=0;
            var current=Volatile.Read(ref mission);
            if(!native.Available || !renderer.IsReady || current==null || readiness.Current?.SessionId!=current.SessionId || !Application.isFocused ||
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
            {previewQuote=null;targets=Array.Empty<DemolitionTarget>();selectionStatus="";renderer.Clear();}
            return true;
        }
        private void CompletePrimary(EditorDirector director,bool actualRelease)
        {
            if(!gesture.Owned) return;
            if(actualRelease && gesture.Stage!=GestureStage.Cancelled) UpdatePointer(director);
            if(!actualRelease) gesture.Cancel();
            var area=gesture.Area;var selection=gesture.Selection;
            var result=gesture.Release(director==null || director.overUI() || view.IsInputShieldActive);
            primaryReleaseFrame=Time.frameCount;
            if(result==GestureRelease.Single) SingleClick(director);
            else if(result==GestureRelease.Area) Commit(area,selection);
            if(pendingCommit==null){renderer.Clear();if(!finalCaptureRequested)view.SetSelectionFrame(null);}
            targets=Array.Empty<DemolitionTarget>();selectionStatus="";Wake();
        }
        internal void AfterInput(EditorDirector director)
        {
            view.AfterInputFrame();
            if(Input.GetMouseButtonDown(1) && (gesture.Owned || pendingCommit!=null || finalCaptureRequested)){CancelSelection();return;}
            if(!gesture.Owned) return;
            if(gesture.Stage!=GestureStage.Cancelled) UpdatePointer(director);
            if(Input.GetMouseButtonUp(0)) CompletePrimary(director,true);
            else if(!Input.GetMouseButton(0)) CompletePrimary(director,false);
        }
        private static bool TryPointerTile(EditorDirector director,out int x,out int y)
        {
            x=y=0;
            if(director==null || director.overUI() || MainControls.instance==null || GameMap.instance==null) return false;
            var mouse=Input.mousePosition;
            if(mouse.x<0 || mouse.y<0 || mouse.x>=Screen.width || mouse.y>=Screen.height) return false;
            float tx=0,ty=0;MainControls.instance.getMouseMapTilePosition(ref tx,ref ty);
            var tile=GameMap.instance.getMapTile((int)tx,(int)ty);
            if(tile==null)return false;x=tile.gameMapX;y=tile.gameMapY;return true;
        }
        private void SingleClick(EditorDirector director)
        {
            if(mission?.SessionId!=capturedSession || !CanCapture(director,out int currentActor) || currentActor!=actor ||
                !TryPointerTile(director,out int x,out int y)) return;
            // Real current-pointer single action, not an arbitrary-coordinate batch; preserves other managed prefixes.
            EngineInterface.DeleteBuilding(x,y,actor,!mission.IsEditor,1);
        }
        private void Commit(ScreenRect area,SelectionSpec selection)
        {
            if(mission?.SessionId!=capturedSession)return;
            if(!renderer.IsReady || network.IsBusy || selection.SameType && selection.Start==null)return;
            // Release freezes the gesture, not a long synchronous native scan on the input callback.
            previewQuote=null;finalArea=area;finalSelection=selection;finalCaptureRequested=true;finalStartAttempted=false;
            finalRequestedAt=Stopwatch.GetTimestamp();view.SetSelectionFrame(area);Wake();
        }
        private bool BeginPreview(ScreenRect area,SelectionSpec selection,bool final)
        {
            // A busy second monitor acquisition must not repeat snapshot preparation at frame rate.
            previewPolicy.Began(Stopwatch.GetTimestamp());
            if(!native.TryCreatePreviewSnapshot(actor,selection,out var snapshot)) return false;
            int[] ground=null;
            bool includeGround=selection.SameType ? selection.Start!=null && (selection.Start.Kind==TargetKind.Wall || selection.Start.Kind==TargetKind.DugMoat) :
                (selection.Filters&((1<<(int)TargetGroup.Fortifications)|(1<<(int)TargetGroup.DugMoats)))!=0;
            if(includeGround)
            {
                var broad=screenSelection.CaptureGroundTileIds(area);
                if(!native.TryCaptureGroundCandidateTiles(broad,actor,selection,out ground))return false;
            }
            var cells=screenSelection.CaptureTileIds(area,snapshot.Candidates,ground);
            previewQuote=native.BeginQuote(snapshot,cells,actor,selection);
            quoteArea=area;quoteSelection=selection;quoteIsFinal=final;
            var camera=Camera.main;quoteProjection=camera.projectionMatrix;quoteView=camera.worldToCameraMatrix;
            quoteRotation=(int)GameMap.instance.CurrentRotation();quoteFlattened=EngineInterface.FlattenedLandscape;
            quoteWidth=Screen.width;quoteHeight=Screen.height;
            return true;
        }
        private bool SameProjection()
        {
            var camera=Camera.main;
            return camera!=null && GameMap.instance!=null && camera.projectionMatrix==quoteProjection && camera.worldToCameraMatrix==quoteView &&
                quoteRotation==(int)GameMap.instance.CurrentRotation() && quoteFlattened==EngineInterface.FlattenedLandscape && quoteWidth==Screen.width && quoteHeight==Screen.height;
        }
        private void AdvancePreview()
        {
            bool final=finalCaptureRequested;
            // After release the selected world targets stay frozen even if the camera keeps moving.
            if(previewQuote!=null && !quoteIsFinal && !SameProjection()){previewQuote=null;targets=Array.Empty<DemolitionTarget>();selectionStatus="";renderer.Clear();}
            long now=Stopwatch.GetTimestamp();
            if(previewQuote==null && previewPolicy.CanBegin(now,final && !finalStartAttempted))
            {
                if(final)finalStartAttempted=true;
                if(!BeginPreview(final?finalArea:gesture.Area,final?finalSelection:gesture.Selection,final))return;
            }
            if(previewQuote==null)return;
            if(!native.TryStepQuote(previewQuote,PreviewWorkPolicy.MaximumRootsPerStep,previewPolicy.Deadline(Stopwatch.GetTimestamp()),out bool complete,out var snapshot) || !complete)return;
            previewQuote=null;
            targets=snapshot;selectionStatus=QuoteStatus(quoteSelection,snapshot);
            if(!quoteIsFinal)return;
            finalCaptureRequested=false;
            if(snapshot.Length==0){renderer.Clear();view.SetSelectionFrame(null);return;}
            // The exact completed immutable quote must still cross one successful camera render.
            pendingCommit=snapshot;pendingArea=quoteArea;pendingSession=capturedSession;pendingActor=actor;pendingPresented=false;nextPendingCheck=0;
            renderer.SetSelection(pendingArea,pendingCommit);
            if(renderer.IsReady && pendingCommit!=null)view.SetSelectionFrame(pendingArea);
        }
        private void Present()
        {
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            try
            {
                view.RefreshHud();network.Pump();
                if(mission==null){renderer.Clear();view.SetSelectionFrame(null);work.SetContinuous(view.IsInputShieldActive);return;}
                if(!missionMarker){missionMarker=true;log.LogInfo(Plugin.Stamp("AREA_DEMOLITION_MISSION_RENDER session="+mission.SessionId+" editor="+mission.IsEditor));}
                if(pendingCommit!=null || finalCaptureRequested)
                {
                    var director=EditorDirector.instance;
                    if(mission.SessionId!=capturedSession || !CanContinue(director,out int currentActor) || currentActor!=actor){CancelSelection();}
                    else if(finalCaptureRequested)
                    {AdvancePreview();view.SetStatus(pendingCommit!=null?selectionStatus:finalCaptureRequested && Stopwatch.GetTimestamp()-finalRequestedAt>Stopwatch.Frequency/5?Text.Get("StatusWaiting"):"");}
                    else PresentPending();
                }
                else if(gesture.Stage==GestureStage.Dragging)
                {
                    var area=gesture.Area;
                    if(!network.IsBusy)AdvancePreview();
                    renderer.SetSelection(area,targets);
                    if(renderer.IsReady){view.SetSelectionFrame(area);view.SetStatus(selectionStatus);}
                }
                else
                {
                    view.SetSelectionFrame(null);
                    view.SetStatus(network.IsBusy?Text.Get("StatusWaiting"):"");
                }
                work.SetContinuous(gesture.Owned || finalCaptureRequested || pendingCommit!=null || view.IsInputShieldActive || network.IsBusy);
            }
            catch(Exception error){CancelSelection();log.LogError(Plugin.Stamp("AREA_DEMOLITION_PRESENTATION_FAILED: "+error));work.SetContinuous(false);}
        }
        private void PresentPending()
        {
            if(pendingSession!=mission.SessionId){CancelSelection();return;}
            if(Stopwatch.GetTimestamp()>=nextPendingCheck)
            {
                // The simulation may own this monitor for a long tick. UI work waits for a later frame.
                if(!Monitor.TryEnter(native.EngineGate))return;
                try
                {
                    nextPendingCheck=Stopwatch.GetTimestamp()+Stopwatch.Frequency/10;
                    if(!native.AreIdentitiesCurrent(pendingCommit)){CancelSelection();return;}
                }
                finally {Monitor.Exit(native.EngineGate);}
            }
            if(pendingPresented)
            {
                var frozen=pendingCommit;pendingCommit=null;pendingPresented=false;
                renderer.Clear();view.SetSelectionFrame(null);view.SetStatus("");network.Submit(pendingActor,frozen);
            }
            else
            {
                renderer.SetSelection(pendingArea,pendingCommit);
                if(renderer.IsReady && pendingCommit!=null){view.SetSelectionFrame(pendingArea);view.SetStatus(selectionStatus);}
            }
        }
        private string QuoteStatus(SelectionSpec selection,DemolitionTarget[] snapshot)
        {
            if(snapshot.Length==0 || selection.SameType && selection.Start==null)return "";
            string filter=Text.Get("ByFilters");
            if(selection.SameType)
            {
                var start=selection.Start;
                if(start.Group==TargetGroup.PositiveFear || start.Group==TargetGroup.NegativeFear)filter=Text.Get("Group"+start.Group);
                else
                {
                    string name=Text.Get("Group"+start.Group);
                    if(start.Kind==TargetKind.Building && MainViewModel.viewModelLoaded && MainViewModel.Instance.HUDBuildingPanel!=null)
                    {
                        string key=MainViewModel.Instance.HUDBuildingPanel.GetBuildingName(start.Type);
                        if(!string.IsNullOrEmpty(key))name=Translate.Instance.lookUpText(key);
                    }
                    filter=Text.Get("SameType")+": "+name;
                }
            }
            int decorations=snapshot.Count(t=>t.Kind==TargetKind.Decoration);
            return filter+" · "+native.CountLogicalBuildings(snapshot)+" "+Text.Get("BuildingsCount")+
                (decorations==0?"":" · "+decorations+" "+Text.Get("DecorationsCount"))+" · "+snapshot.Count(t=>t.Kind!=TargetKind.Building && t.Kind!=TargetKind.Decoration)+" "+Text.Get("TilesCount");
        }
    }
}
