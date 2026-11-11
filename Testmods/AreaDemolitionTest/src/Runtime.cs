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
        private IUiFrameWorkRegistration work;
        private IMissionLifecycleCapability lifecycle;
        private IMissionPlayStateReadinessCapability readiness;
        private DemolitionView view;
        private SelectionRenderer renderer;
        private DemolitionNetwork network;
        private MissionContext mission;
        private int actor, lastFrame=-1;
        private long capturedSession, nextQuote;
        private DemolitionTarget[] targets=Array.Empty<DemolitionTarget>();
        private bool hasQuote;
        private ScreenRect quotedArea;
        private Matrix4x4 quotedProjection, quotedView;
        private int quotedRotation, quotedWidth, quotedHeight, primaryReleaseFrame=-1;
        private bool quotedFlattened;
        private bool missionMarker;
        private string lastNetworkStatus="";
        private long statusUntil, localStatusUntil, nextPendingCheck;
        private string localStatus="";
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
            renderer.Failed+=reason=>{CancelSelection();view.SetStatus(Text.Get("PreviewUnavailable"));log.LogError(Plugin.Stamp("AREA_DEMOLITION_PREVIEW_DISABLED: "+reason));Wake();};
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
        {gesture.Reset();primaryReleaseFrame=-1;pendingCommit=null;pendingPresented=false;targets=Array.Empty<DemolitionTarget>();hasQuote=false;lastNetworkStatus=localStatus="";localStatusUntil=0;renderer.Clear();view.Reset();view.SetStatus("");}
        private void GuiChecks(PresentationPostEventArgs note)
        { if(note.OriginalCompleted && MainViewModel.viewModelLoaded) view.RefreshHud(); }
        private void Wake()=>work?.Request();
        private void MenuChanged(){if(view.IsMenuOpen) CancelSelection();Wake();}
        private void FocusChanged(bool focus){if(!focus){CancelSelection();view.CloseMenu();if(network.IsBusy)network.CancelPending();Wake();}}
        internal bool CancelWithEscape()
        {
            bool consumed=gesture.Owned || pendingCommit!=null || view.IsMenuOpen || network.IsBusy;
            if(consumed){CancelSelection();view.CloseMenu();network.CancelPending();Wake();}
            return consumed;
        }
        private void CancelSelection(){gesture.Cancel();pendingCommit=null;pendingPresented=false;targets=Array.Empty<DemolitionTarget>();hasQuote=false;renderer.Clear();view.SetSelectionFrame(null);}
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
            if(state==1 && pendingCommit!=null)CancelSelection();
            if(state!=1 || !CanCapture(director,out int owner) || !TryPointerTile(director,out int x,out int y)) return false;
            actor=owner;capturedSession=mission.SessionId;
            bool shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
            DemolitionTarget start=native.AtTile(x,y,actor);
            var mouse=Input.mousePosition;
            primaryReleaseFrame=-1;
            localStatusUntil=0;
            gesture.Begin(mouse.x,mouse.y,new SelectionSpec(settings.Mask,shift,start));
            director.clearMouseStateForEngine(); hasQuote=false;nextQuote=0; Wake();
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
            if(gesture.SetSameType(Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift))) hasQuote=false;
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
            if(pendingCommit==null){renderer.Clear();view.SetSelectionFrame(null);}
            targets=Array.Empty<DemolitionTarget>();hasQuote=false;Wake();
        }
        internal void AfterInput(EditorDirector director)
        {
            view.AfterInputFrame();
            if(!gesture.Owned) return;
            if(Input.GetMouseButtonDown(1)){CancelSelection();return;}
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
        private DemolitionTarget[] Capture(ScreenRect area,SelectionSpec selection)
        {
            var candidates=native.CaptureCandidates(actor,selection);
            bool ground=selection.SameType ? selection.Start!=null && (selection.Start.Kind==TargetKind.Wall || selection.Start.Kind==TargetKind.DugMoat) :
                (selection.Filters&((1<<(int)TargetGroup.Fortifications)|(1<<(int)TargetGroup.DugMoats)))!=0;
            var cells=screenSelection.CaptureTileIds(area,candidates,ground);
            return native.CaptureTiles(cells,actor,selection);
        }
        private void Commit(ScreenRect area,SelectionSpec selection)
        {
            if(mission?.SessionId!=capturedSession)return;
            if(!renderer.IsReady){ShowLocalStatus("PreviewUnavailable");return;}
            if(network.IsBusy){ShowLocalStatus("Busy");return;}
            if(selection.SameType && selection.Start==null){ShowLocalStatus("StartOnObject");return;}
            DemolitionTarget[] frozen;
            try {frozen=DemolitionPolicy.Canonicalize(Capture(area,selection));}
            catch(Exception error)
            {
                CancelSelection();ShowLocalStatus("PreviewUnavailable");
                log.LogError(Plugin.Stamp("AREA_DEMOLITION_CAPTURE_FAILED: "+error));return;
            }
            if(frozen.Length==0){ShowLocalStatus("NoTargets");return;}
            // Present the final immutable quote once, including any endpoint change on release.
            // A camera failure therefore cannot turn unshown targets into a network operation.
            pendingCommit=frozen;pendingArea=area;pendingSession=capturedSession;pendingActor=actor;pendingPresented=false;nextPendingCheck=0;
            renderer.SetSelection(area,frozen);view.SetSelectionFrame(area);Wake();
        }
        private void ShowLocalStatus(string key)
        {
            localStatus=Text.Get(key);localStatusUntil=Stopwatch.GetTimestamp()+Stopwatch.Frequency*3;
            view.SetStatus(localStatus);work.RequestAfter(TimeSpan.FromSeconds(3));Wake();
        }
        private void Present()
        {
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            try
            {
                view.RefreshHud();network.Pump();
                if(mission==null){renderer.Clear();view.SetSelectionFrame(null);work.SetContinuous(view.IsInputShieldActive);return;}
                if(!missionMarker){missionMarker=true;log.LogInfo(Plugin.Stamp("AREA_DEMOLITION_MISSION_RENDER session="+mission.SessionId+" editor="+mission.IsEditor));}
                if(pendingCommit!=null)
                {
                    var director=EditorDirector.instance;
                    int currentActor=director==null?0:director.ActivePlayerID;
                    if(currentActor<=0)currentActor=PlayerPerspectiveAPI.GetInputControlledPlayerId();
                    if(pendingSession!=mission.SessionId || !renderer.IsReady || !Application.isFocused || currentActor!=pendingActor || MainControls.instance==null || MainControls.instance.CurrentAction!=BulldozerAction){CancelSelection();}
                    else if(Stopwatch.GetTimestamp()>=nextPendingCheck && !PendingStillCurrent())
                    {CancelSelection();ShowLocalStatus("StatusCancelled");}
                    else if(pendingPresented)
                    {
                        var frozen=pendingCommit;pendingCommit=null;pendingPresented=false;
                        renderer.Clear();view.SetSelectionFrame(null);network.Submit(pendingActor,frozen);
                    }
                    else {renderer.SetSelection(pendingArea,pendingCommit);view.SetSelectionFrame(pendingArea);view.SetStatus(QuoteStatus(gesture.Selection,pendingCommit));}
                }
                else if(gesture.Stage==GestureStage.Dragging)
                {
                    var area=gesture.Area;
                    var camera=Camera.main;
                    bool moved=!hasQuote || area!=quotedArea || camera==null || camera.projectionMatrix!=quotedProjection || camera.worldToCameraMatrix!=quotedView ||
                        quotedRotation!=(int)GameMap.instance.CurrentRotation() || quotedFlattened!=EngineInterface.FlattenedLandscape || quotedWidth!=Screen.width || quotedHeight!=Screen.height;
                    long now=Stopwatch.GetTimestamp();
                    if(moved || now>=nextQuote)
                    {
                        targets=network.IsBusy?Array.Empty<DemolitionTarget>():Capture(area,gesture.Selection);
                        quotedArea=area;hasQuote=true;nextQuote=now+Stopwatch.Frequency/10;
                        if(camera!=null){quotedProjection=camera.projectionMatrix;quotedView=camera.worldToCameraMatrix;}
                        quotedRotation=(int)GameMap.instance.CurrentRotation();quotedFlattened=EngineInterface.FlattenedLandscape;quotedWidth=Screen.width;quotedHeight=Screen.height;
                    }
                    renderer.SetSelection(area,targets);
                    view.SetSelectionFrame(area);
                    view.SetStatus(QuoteStatus(gesture.Selection,targets));
                }
                else
                {
                    view.SetSelectionFrame(null);
                    string currentStatus=network.Status??"";
                    if(currentStatus!=lastNetworkStatus)
                    {
                        lastNetworkStatus=currentStatus;statusUntil=Stopwatch.GetTimestamp()+Stopwatch.Frequency*3;
                        work.RequestAfter(TimeSpan.FromSeconds(3));
                    }
                    bool ordinary=currentStatus=="Ready." || currentStatus.StartsWith("Waiting for",StringComparison.Ordinal);
                    view.SetStatus(network.IsBusy ? Text.NetworkStatus(currentStatus) : Stopwatch.GetTimestamp()<localStatusUntil ? localStatus :
                        !ordinary && Stopwatch.GetTimestamp()<statusUntil ? Text.NetworkStatus(currentStatus) : "");
                }
                work.SetContinuous(gesture.Owned || pendingCommit!=null || view.IsInputShieldActive || network.IsBusy);
            }
            catch(Exception error){CancelSelection();log.LogError(Plugin.Stamp("AREA_DEMOLITION_PRESENTATION_FAILED: "+error));work.SetContinuous(false);}
        }
        private bool PendingStillCurrent()
        {
            nextPendingCheck=Stopwatch.GetTimestamp()+Stopwatch.Frequency/10;
            return native.AreIdentitiesCurrent(pendingCommit);
        }
        private string QuoteStatus(SelectionSpec selection,DemolitionTarget[] snapshot)
        {
            string filter=Text.Get("ByFilters");
            if(selection.SameType)
            {
                var start=selection.Start;
                if(start==null)filter=Text.Get("StartOnObject");
                else if(start.Group==TargetGroup.PositiveFear || start.Group==TargetGroup.NegativeFear)filter=Text.Get("Group"+start.Group);
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
