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
        private AreaRect quotedArea;
        private bool missionMarker;
        private string lastNetworkStatus="";
        private long statusUntil;
        private DemolitionTarget[] pendingCommit;
        private AreaRect pendingArea;
        private long pendingSession;
        private int pendingActor;
        private bool pendingPresented;
        internal Runtime(ModApiClient api,NativeDemolition native,DemolitionSettings settings,ManualLogSource log)
        {this.api=api;this.native=native;this.settings=settings;this.log=log;}
        internal void Register()
        {
            if(!api.Ui.TryRegisterBeforeRender("presentation",Present,out work,out string reason)) throw new InvalidOperationException(reason);
            view=new DemolitionView(settings,log); renderer=new SelectionRenderer(log);
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
        {gesture.Reset();pendingCommit=null;pendingPresented=false;targets=Array.Empty<DemolitionTarget>();hasQuote=false;lastNetworkStatus="";renderer.Clear();view.Reset();view.SetStatus("");}
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
        private void CancelSelection(){gesture.Cancel();pendingCommit=null;pendingPresented=false;targets=Array.Empty<DemolitionTarget>();hasQuote=false;renderer.Clear();}
        internal bool HandlePrimary(EditorDirector director,int state)
        {
            if(gesture.Owned)
            {
                if(state==2) UpdatePointer(director);
                if(state==3 || state==0)
                {
                    if(state==3) UpdatePointer(director);
                    var area=gesture.Area; var spec=gesture.Selection;
                    var result=gesture.Release(state==0 || view.IsInputShieldActive);
                    if(result==GestureRelease.Single) SingleClick(director);
                    else if(result==GestureRelease.Area) Commit(area,spec);
                    if(pendingCommit==null)renderer.Clear(); targets=Array.Empty<DemolitionTarget>(); hasQuote=false; Wake();
                }
                return true;
            }
            if(view.IsInputShieldActive) return true;
            if(state==1 && pendingCommit!=null)CancelSelection();
            if(state!=1 || !CanCapture(director,out int owner) || !TryPointerTile(director,out int x,out int y)) return false;
            actor=owner;capturedSession=mission.SessionId;
            bool shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
            DemolitionTarget start=shift?native.AtTile(x,y,actor):null;
            var mouse=Input.mousePosition;
            gesture.Begin(x,y,mouse.x,mouse.y,new SelectionSpec(settings.Mask,shift,start));
            director.clearMouseStateForEngine(); hasQuote=false;nextQuote=0; Wake();
            return true;
        }
        private bool CanCapture(EditorDirector director,out int owner)
        {
            owner=0;
            var current=Volatile.Read(ref mission);
            if(!native.Available || !renderer.IsReady || current==null || readiness.Current?.SessionId!=current.SessionId || !Application.isFocused ||
                MainControls.instance==null || MainControls.instance.CurrentAction!=BulldozerAction || director.overUI()) return false;
            owner=director.ActivePlayerID;
            if(owner<=0) owner=PlayerPerspectiveAPI.GetInputControlledPlayerId();
            return owner>=1 && owner<=8;
        }
        private bool UpdatePointer(EditorDirector director)
        {
            if(mission?.SessionId!=capturedSession || !Application.isFocused || MainControls.instance==null || MainControls.instance.CurrentAction!=BulldozerAction ||
                !CanCapture(director,out int currentActor) || currentActor!=actor)
            {CancelSelection();return false;}
            if(!TryPointerTile(director,out int x,out int y)) return false;
            var mouse=Input.mousePosition;
            gesture.Move(x,y,mouse.x,mouse.y,Math.Max(.5f,Screen.height/1080f));
            return true;
        }
        internal void AfterInput(EditorDirector director)
        {
            view.AfterInputFrame();
            if(!gesture.Owned) return;
            if(Input.GetMouseButtonDown(1)){CancelSelection();return;}
            if(gesture.Stage!=GestureStage.Cancelled) UpdatePointer(director);
            if(!Input.GetMouseButton(0)){gesture.Release(true);renderer.Clear();targets=Array.Empty<DemolitionTarget>();hasQuote=false;Wake();}
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
        private void Commit(AreaRect area,SelectionSpec selection)
        {
            if(mission?.SessionId!=capturedSession)return;
            if(!renderer.IsReady){view.SetStatus(Text.Get("PreviewUnavailable"));return;}
            if(network.IsBusy){view.SetStatus(Text.Get("Busy"));return;}
            if(selection.SameType && selection.Start==null){view.SetStatus(Text.Get("StartOnObject"));return;}
            var frozen=DemolitionPolicy.Canonicalize(native.Capture(area,actor,selection));
            if(frozen.Length==0){view.SetStatus(Text.Get("NoTargets"));return;}
            // Present the final immutable quote once, including any endpoint change on release.
            // A camera failure therefore cannot turn unshown targets into a network operation.
            pendingCommit=frozen;pendingArea=area;pendingSession=capturedSession;pendingActor=actor;pendingPresented=false;
            renderer.SetSelection(area,frozen);Wake();
        }
        private void Present()
        {
            if(lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
            try
            {
                view.RefreshHud();network.Pump();
                if(mission==null){renderer.Clear();work.SetContinuous(view.IsInputShieldActive);return;}
                if(!missionMarker){missionMarker=true;log.LogInfo(Plugin.Stamp("AREA_DEMOLITION_MISSION_RENDER session="+mission.SessionId+" editor="+mission.IsEditor));}
                if(pendingCommit!=null)
                {
                    var director=EditorDirector.instance;
                    int currentActor=director==null?0:director.ActivePlayerID;
                    if(currentActor<=0)currentActor=PlayerPerspectiveAPI.GetInputControlledPlayerId();
                    if(pendingSession!=mission.SessionId || !renderer.IsReady || !Application.isFocused || currentActor!=pendingActor || MainControls.instance==null || MainControls.instance.CurrentAction!=BulldozerAction){CancelSelection();}
                    else if(pendingPresented)
                    {
                        var frozen=pendingCommit;pendingCommit=null;pendingPresented=false;
                        renderer.Clear();network.Submit(pendingActor,frozen);
                    }
                    else {renderer.SetSelection(pendingArea,pendingCommit);view.SetStatus(QuoteStatus(gesture.Selection,pendingCommit));}
                }
                else if(gesture.Stage==GestureStage.Dragging)
                {
                    var area=gesture.Area;
                    bool moved=!hasQuote || area.MinX!=quotedArea.MinX || area.MinY!=quotedArea.MinY || area.MaxX!=quotedArea.MaxX || area.MaxY!=quotedArea.MaxY;
                    long now=Stopwatch.GetTimestamp();
                    if(moved || now>=nextQuote)
                    {
                        targets=network.IsBusy?Array.Empty<DemolitionTarget>():native.Capture(area,actor,gesture.Selection);
                        quotedArea=area;hasQuote=true;nextQuote=now+Stopwatch.Frequency/10;
                    }
                    renderer.SetSelection(area,targets);
                    view.SetStatus(QuoteStatus(gesture.Selection,targets));
                }
                else
                {
                    string currentStatus=network.Status??"";
                    if(currentStatus!=lastNetworkStatus)
                    {
                        lastNetworkStatus=currentStatus;statusUntil=Stopwatch.GetTimestamp()+Stopwatch.Frequency*3;
                        work.RequestAfter(TimeSpan.FromSeconds(3));
                    }
                    bool ordinary=currentStatus=="Ready." || currentStatus.StartsWith("Waiting for",StringComparison.Ordinal);
                    view.SetStatus(network.IsBusy || (!ordinary && Stopwatch.GetTimestamp()<statusUntil) ? Text.NetworkStatus(currentStatus) : "");
                }
                work.SetContinuous(gesture.Owned || pendingCommit!=null || view.IsInputShieldActive || network.IsBusy);
            }
            catch(Exception error){CancelSelection();log.LogError(Plugin.Stamp("AREA_DEMOLITION_PRESENTATION_FAILED: "+error));work.SetContinuous(false);}
        }
        private static string QuoteStatus(SelectionSpec selection,DemolitionTarget[] snapshot)
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
            return filter+" · "+snapshot.Count(t=>t.Kind==TargetKind.Building)+" "+Text.Get("BuildingsCount")+" · "+snapshot.Count(t=>t.Kind!=TargetKind.Building)+" "+Text.Get("TilesCount");
        }
    }
}
