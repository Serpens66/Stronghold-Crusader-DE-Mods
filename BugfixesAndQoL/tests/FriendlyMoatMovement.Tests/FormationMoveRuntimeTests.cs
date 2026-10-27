using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class FormationMoveRuntimeTests
{
    internal static void Validate(string root)
    {
        var runtime = CSharpSyntaxTree.ParseText(FeatureSourceModel.Read(root, "FormationRuntime", "BugfixesAndQoL/src/Formations")).GetRoot()
            .DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.Text == "FormationRuntime");
        string[] methods = { "TryStartDrag", "CompleteDragStart", "TryCompletePendingStart", "TryCaptureCommandTarget", "TryCaptureTarget",
            "EvaluateFormationTargetBounds", "IsTargetInsideNativeMap", "EvaluateCommandMode",
            "ValidateActiveDrag", "RefreshGestureSelection", "RefreshGestureSelectionLocked", "AuthorizePreview", "ObserveNativeGroundFeedback", "CapturePreviewAdmission", "EngineRunHook",
            "RunOriginalOnce", "RunOriginalAfterReleaseConsumed", "DiscardDeferredRelease", "AbortDrag", "OnKeyDown", "TryGetMouseButton", "PrepareFormationRelease", "RunOriginalWithDeferredRelease", "ConsumeGestureRelease", "UpdateGesture",
            "OnKeyHeld", "OnKeyUp", "PublishPreview", "PublishPreviewLocked", "ResolveDirectionAndWidth",
            "HasExplicitDirection", "ClearPreview", "FailOpen", "TryCreatePacket", "ValidatePacket", "ComputeSelectionHash" };
        string[] types = { "ActiveDrag", "PendingDragStart", "GroundTarget", "SelectionIdentity", "NativeDestination",
            "DispatchDisposition", "ReleaseConsumptionWatch", "FormationReleaseDisposition", "GestureSelectionState" };
        var members = runtime.Members.Where(m => m is MethodDeclarationSyntax method &&
                methods.Contains(method.Identifier.Text) || m is BaseTypeDeclarationSyntax type &&
                types.Contains(type.Identifier.Text));
        var authorization = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/UnitCommands/Cursor/GroundMovePreviewAuthorization.cs"))).GetRoot();
        var proofs = authorization.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Where(t => t.Identifier.Text != "NativeGroundMoveFeedbackReader");
        string fixture = """
using System; using System.Linq; using System.Collections.Generic; using SHCDESE.Interop.Enums;
using UnityEngine; using SHCDESE.API; using SHCDESE.Interop; using SHCDESE.EventAPI;
namespace SHCDESE.Interop.Enums { internal enum TribeMoveType {NoChange=0,DefaultInSync=1,Fast=-255} public enum AliveState {IsAlive=2} }
namespace UnityEngine {
    public struct Vector3 {public float x,y;public static Vector3 zero=>default;}
    public struct Vector3Int {public int x,y;public static Vector3Int zero=>default;}
    public enum KeyCode {Mouse0,Mouse1}
    public static class Input {public static Vector3 mousePosition;public static (float x,float y) mouseScrollDelta;}
    public static class Time {public static int frameCount;}
}
namespace SHCDESE.EventAPI {
    public enum EventHookPhase {Pre,Post}
    public class UnityInputEventArgs {public EventHookPhase Phase=EventHookPhase.Post;public KeyCode Key;}
}
namespace SHCDESE.Interop {
    public enum eChimps {Unit}
    public struct GameUnit {public uint r_GlobalId;public int r_TribeId,r_ControllableForPlayerId;public eChimps r_UnitChimp;public ushort r_CurrentTilePositionX,r_CurrentTilePositionY;public bool Alive;public ushort DeathMarker;public int TargetX,TargetY,PathProgress,MovementState;public ulong PackedPath;}
    public struct GameTribe {public int r_PlayerIdOwner;public SHCDESE.Interop.Enums.AliveState r_AliveState;}
    public struct GameCursorManager {public uint r_IsCursorInGame,r_MouseTileX,r_MouseTileY;}
    public class GameTileManagerView {
        public int[] TileUnitIdGrid=new int[10000],StructureGrid=new int[10000];
        public ushort[] PathConnectionGrid=new ushort[10000];
    }
}
namespace APIShared {
    public enum UnitLookupFailure {None,ManagerUnavailable,Unavailable}
    public static unsafe class UnitAccess {
        public static GameUnit* Units=(GameUnit*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(GameUnit)*1301);
        public static bool[] Missing=new bool[1301];public static bool Ready=true;
        public static bool TryGetById(int id,out GameUnit* unit,out UnitLookupFailure failure) {
            unit=Ready&&id>0&&id<1301&&!Missing[id]?Units+id:null;
            failure=unit!=null?UnitLookupFailure.None:!Ready?UnitLookupFailure.ManagerUnavailable:UnitLookupFailure.Unavailable;return unit!=null;
        }
        public static bool IsReallyAlive(GameUnit* u)=>u!=null&&u->Alive&&u->DeathMarker==0;
    }
    public class LocalSelectionSnapshot {
        public int Count=>LocalSelectionAPI.Count();public Entry this[int i]=>new Entry{UnitId=LocalSelectionAPI.Id(i)};
        public struct Entry {public int UnitId;}
    }
    public static class LocalSelectionAPI {
        public static Func<int> Count;public static Func<int,int> Id;public static bool Ready=true;private static LocalSelectionSnapshot snapshot=new();
        public static bool TryCapture(int p,out LocalSelectionSnapshot s){s=Ready?snapshot:null;return Ready;}
    }
}
namespace SHCDESE.API {
    public unsafe class GameTribeManagerAPI {
        public static GameTribeManagerAPI Instance=new();public bool Ready=true;
        private GameTribe* tribe=(GameTribe*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(GameTribe));
        public bool TryGetTribeById(int id,out GameTribe* p){*tribe=new GameTribe{r_PlayerIdOwner=1,r_AliveState=AliveState.IsAlive};p=Ready?tribe:null;return Ready&&id>0;}
    }

    public class GameTileManagerAPI {
        public static GameTileManagerAPI Instance=new GameTileManagerAPI();
        public GameTileManagerView TileManager=new GameTileManagerView();
        public int GetTileId(int x,int y)=>y*100+x;
    }
    public unsafe struct CursorView {public GameCursorManager* Pointer;}
    public unsafe class GamePlayerManagerAPI {
        public static GamePlayerManagerAPI Instance=new GamePlayerManagerAPI();
        public GameCursorManager* Cursor=(GameCursorManager*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(GameCursorManager));
        public int Player=1;public int GetLocalPlayerId()=>Player;
        public CursorView GetCursorManager()=>new CursorView {Pointer=Cursor};
    }
}
public class GameMapTile {public int gameMapX,gameMapY;}
public class GameMap {
    public static GameMap instance=new GameMap();public int X=10,Y=20;public bool Available=true;
    public void CalcMapTileFromMousePos(Vector3 mouse,ref Vector3 map,ref Vector3Int tile,ref int depth) {tile.x=X;tile.y=Y;}
    public GameMapTile getMapTile(int x,int y)=>Available?new GameMapTile {gameMapX=X,gameMapY=Y}:null;
}
public class FatControler {public static FatControler instance=new FatControler();public bool UI;public bool overNoesisGUI()=>UI;}
public class EditorDirector {
    public static EditorDirector instance=new EditorDirector();
    public int Left;public bool RightDown,RightUp,StateRead,UpPending;
    public void clearMouseStateForEngine() {Left=0;StateRead=true;UpPending=false;}
}
namespace Shared { public static class DevDiag {public static void Count(string name){}} public static class UnityMainThreadDispatch {public static bool Deferred;public static readonly Queue<Action> Pending=new();public static void TryRunInlineOrEnqueue(Action action){if(Deferred)Pending.Enqueue(action);else action();}} public static class DebugLogHelper { public static bool DiagnosticsEnabled => true; public static void LogErrorLimited(object log,string site,string message) {} public static void LogDebugLimited(object log,string site,params object[] parts) { APIShared.Internal.DebugLogHelper.LogDebug(log,System.String.Concat(parts)); } public static void LogWarningLimited(object log,string site,params object[] parts) { APIShared.Internal.DebugLogHelper.LogWarning(log,System.String.Concat(parts)); } } }
namespace APIShared.Internal {
    public class NativeTroopCommandModeReader {public int Mode=1;public int Read()=>Mode;}
    public static class DebugLogHelper { public static bool DiagnosticsEnabled => true; public static void LogDebugLimited(object l, string site, params object[] parts) { LogDebug(l, System.String.Concat(parts)); } public static void LogDebug(object l,string m) {} public static void LogWarningLimited(object l, string site, params object[] parts) { LogWarning(l, System.String.Concat(parts)); } public static void LogWarning(object l,string m) {}}
    public static class GroundMovePreviewEligibility {
        public static GroundMovePreviewRejection EvaluateCommandMode(int mode)=>mode==1?GroundMovePreviewRejection.None:GroundMovePreviewRejection.NonMoveCommandMode;
    }
    public enum GroundMovePreviewRejection {None,OutsideMap,NonMoveCommandMode}
}
namespace BugfixesAndQoL.UnitCommands {
    __PROOFS__
    public class NativeGroundMoveFeedbackReader {internal GroundMoveFeedback Feedback;internal GroundMoveFeedback Read()=>Feedback;}
    internal sealed class Marker {
        internal bool ReplacementAvailable=true;internal Func<bool> Authorization;
        internal void ClearPreviewMarkerTiles() {Tiles=null;Authorization=null;}
        internal int[] Tiles;internal void SetPreviewMarkerTiles(IEnumerable<int> tiles,Func<bool> authorization) {
            Tiles=tiles?.ToArray();Authorization=authorization;
        }
    }
    internal sealed class Menu {
        internal bool Allowed;internal int Remembers;internal FormationPreviewPoint[] Points;
        internal void CloseMenu() {} internal int GetRememberedRows(FormationKind kind)=>0;
        internal void RefreshHostState() {}
        internal void SetPreviewAuthorization(bool value)=>Allowed=value;
        internal void PublishPreview(FormationPreviewPoint[] points,FormationDirectionIndicator d)=>Points=points;
        internal void ClearPreview() {Allowed=false;Points=null;}
        internal void RememberSelectedRows(FormationKind kind,int rows) {Remembers++;}
    }
    internal sealed class Entry<T> {internal T Value;internal Entry(T value) {Value=value;}}
    internal sealed class FrameObserver {internal bool Active;internal void SetActive(bool active)=>Active=active;}
    internal sealed class Commands {internal int mapEpoch;internal long placementRevision;internal int Reads,Allowed=-1;
        internal FormationUnit[] CapturePreviewFormationAdmission(FormationUnit[] units,int tribe,int x,int y,out long revision) {
            Reads++;revision=placementRevision;return Allowed<0?units:units.Take(Allowed).ToArray();
        }}
    internal static class FormationPreviewMarkerModel {internal const int MaximumMarkers=10000;}
    public unsafe class RuntimeFixture {
        // Only external world/input/packet/placement boundaries are simulated.
        // Candidate, preview publication, authorization and release orchestration
        // below are compiled directly from the production runtime.
        private object speechEngineLock=new object();
        private object log=new object();private readonly object stateSync=new object();
        private const int MapWidth=100,MinimumDragTileDistance=2,MaximumTribeCount=4500,MaximumUnitCount=10000;__PROTOCOL__private int nextOperationId;
        private readonly FormationFailurePolicy inputFailures=new(),cameraFailures=new();
        private long inputAttempt,cameraAttempt;private bool failed,localInputFailed;
        private readonly Queue<object> speechQueue=new();
        private ActiveDrag drag;private PendingDragStart pendingStart;private ReleaseConsumptionWatch releaseConsumptionWatch;
        private FrameObserver groundFrameObserver=new FrameObserver();
        private Marker markerRenderer=new Marker();private Menu menuViewModel=new Menu();
        private Commands commandRuntime=new Commands();private long nativeFeedbackGeneration;
        private long previewPublicationVersion; private long nativeFeedbackRunGeneration=>FormationNativeRunContext.Generation(this);private bool nativeFeedbackRunActive=>FormationNativeRunContext.IsActive(this);
        private NativeGroundMoveFeedbackReader groundFeedbackReader=new NativeGroundMoveFeedbackReader();
        private APIShared.Internal.NativeTroopCommandModeReader commandModeReader=new APIShared.Internal.NativeTroopCommandModeReader();
        private Entry<FormationKind> formationConfig=new Entry<FormationKind>(FormationKind.Line);
        private Entry<int> densityConfig=new Entry<int>(2);
        private Entry<RangedPlacementMode> placementModeConfig=new Entry<RangedPlacementMode>(RangedPlacementMode.Off);
        private bool Enabled=true,initialized=true,startupConfirmed;private int lastWheelFrame=-1;
        private bool LocalInputEnabled=>Enabled&&!localInputFailed;
        private static SelectionIdentity[] selection;private static bool mapValid=true,shift;private static int button,selectionTribe=2;
        private int Originals,VanillaReleases,Dispatched;private int OriginalResult=1;private bool RejectPacket,RenderInsideOriginal;private bool PreviewFails;private bool ThrowAfterAccept,AbortInOriginal,BecomeReadyInsideOriginal;
        private FormationOrderPacket LastPacket;
        private bool HasValidMap()=>mapValid;
        private bool IsShiftHeld()=>shift;
        private int GetCommandMouseButton()=>button;
        private KeyCode ToKeyCode(int b)=>b==0?KeyCode.Mouse0:KeyCode.Mouse1;
        private static bool TryCaptureSelection(out SelectionIdentity[] units,out int tribe) {units=selection?.ToArray();tribe=selectionTribe;return units!=null&&units.Length>=2;}
        private void ResetTransientState() {var old=drag;drag=null;DiscardDeferredRelease(old);ClearPreview();}
        private void RequireMainThread(string context) {}
        private void LogErrorNoThrow(string message) {}
        // Speech is an independent side effect in this movement fixture. Its
        // production capture/frame/queue/error paths execute in FormationSpeech.Tests.
        private sealed class FormationSpeechRequest {}
        private FormationSpeechRequest CaptureSpeech(ActiveDrag state,TribeMoveType mode)=>null; private void RefreshNativeObserverDemand()=>groundFrameObserver.SetActive(drag!=null);
        private void QueueSpeech(FormationSpeechRequest request) {}
        private void AgeReleaseConsumptionWatch() {}
        private void LogDebugNoThrow(string text) {} private void LogWarningNoThrow(string text) {}
        private void LogTargetRejection(APIShared.Internal.GroundMovePreviewRejection r,GroundTarget t) {}
        private string ToRejectionReason(APIShared.Internal.GroundMovePreviewRejection r)=>r.ToString();
        private FormationMouseState CaptureMouseState(EditorDirector d)=>new FormationMouseState(d.Left,d.RightDown,d.RightUp,d.StateRead,d.UpPending);
        private void ApplyMouseState(EditorDirector d,FormationMouseState s) {d.Left=s.LeftState;d.RightDown=s.RightDown;d.RightUp=s.RightUp;d.StateRead=s.StateRead;d.UpPending=s.UpPending;}
        private int engineRunOriginal(bool skip) {Originals++;if(BecomeReadyInsideOriginal){BecomeReadyInsideOriginal=false;APIShared.LocalSelectionAPI.Ready=true;Kill(1);}if(FormationReleaseStateModel.HasCommandRelease(CaptureMouseState(EditorDirector.instance),button)){VanillaReleases++;MutateNativeOrder(GameMap.instance.X,GameMap.instance.Y);}if(OriginalResult>0&&groundFrameObserver.Active)lock(speechEngineLock)ObserveNativeGroundFeedback();if(RenderInsideOriginal&&OriginalResult>0)Render();if(AbortInOriginal)AbortDrag("fixture-mid-run");return OriginalResult;}
        private DispatchDisposition TryDispatch(FormationOrderPacket p,out string rejection) {
            Check(ValidatePacket(p,out rejection),"Actual packet rejected: "+rejection);
            if(RejectPacket)return DispatchDisposition.Rejected;
            Dispatched++;LastPacket=p;MutateNativeOrder(p.TargetX,p.TargetY);if(ThrowAfterAccept)throw new InvalidOperationException("accepted transport failed");return DispatchDisposition.Accepted;
        }
        private static void MutateNativeOrder(int x,int y) {
            foreach(var member in selection??Array.Empty<SelectionIdentity>()) {
                if(!APIShared.UnitAccess.TryGetById(member.UnitId,out GameUnit* u,out _)||!APIShared.UnitAccess.IsReallyAlive(u))continue;
                u->TargetX=x;u->TargetY=y;u->PackedPath=0;u->PathProgress=0;u->MovementState=2;
            }
        }
        private FormationRole Classify(eChimps type)=>FormationRole.Neutral;
        private ulong ComputePlanHash(FormationUnit[] units,NativeDestination[] slots,RangedPlacementMode placement,int rows)=>1;
        private NativeDestination[] BuildManagedDestinations(int x,int y,FormationKind kind,int density,int direction,int rows,RangedPlacementMode placement,
            FormationUnit[] units,bool explicitDirection,out FormationDirectionIndicator indicator) {
            if(PreviewFails)throw new InvalidOperationException("fixture slot shortage");indicator=FormationDirectionIndicator.Hidden;return units.Select((u,i)=>new NativeDestination(y*MapWidth+x+i,x+i,y,u.Role)).ToArray();
        }
        __MEMBERS__
        private static int checks;private static void Check(bool value,string message) {checks++;if(!value)throw new Exception(message);}
        private static RuntimeFixture Fresh(int commandButton=1,int count=31) {
            button=commandButton;selectionTribe=2;shift=false;mapValid=true;selection=Enumerable.Range(1,count).Select(i=>new SelectionIdentity(i,(uint)i,0,1,1)).ToArray();
            APIShared.UnitAccess.Ready=true;Array.Clear(APIShared.UnitAccess.Missing);APIShared.LocalSelectionAPI.Ready=true;
            GameTribeManagerAPI.Instance.Ready=true;
            for(int i=1;i<=count;i++)APIShared.UnitAccess.Units[i]=new GameUnit {r_GlobalId=(uint)i,r_TribeId=2,r_ControllableForPlayerId=1,Alive=true,TargetX=2,TargetY=3,PackedPath=12345,PathProgress=7,MovementState=9};
            APIShared.LocalSelectionAPI.Count=()=>selection?.Length??0;APIShared.LocalSelectionAPI.Id=i=>selection[i].UnitId;
            GameMap.instance=new GameMap();FatControler.instance=new FatControler();EditorDirector.instance=new EditorDirector();
            GameTileManagerAPI.Instance=new GameTileManagerAPI();
            GamePlayerManagerAPI.Instance.Player=1;var f=new RuntimeFixture();
            f.SetFeedback(unit:42,count:count);return f;
        }
        private void SetFeedback(int x=10,int y=20,int kind=3,int command=1,int detail=0,int mode=1,int player=1,int tribe=2,int count=31,int unit=0,int building=0,int wall=0,int file=0x6B,int image=0,bool coherent=true) {
            groundFeedbackReader.Feedback=new GroundMoveFeedback(player,tribe,count,mode,x,y,kind,file,image,command,detail,unit,building,wall);
            *GamePlayerManagerAPI.Instance.Cursor=new GameCursorManager {r_IsCursorInGame=coherent?1u:0u,r_MouseTileX=(uint)x,r_MouseTileY=(uint)y};
        }
        private bool Render()=>markerRenderer.Authorization?.Invoke()??false;
        private void Tick()=>EngineRunHook(false);
        private void Release() {var d=EditorDirector.instance;if(button==0)d.Left=3;else d.RightUp=true;OnKeyUp(new UnityInputEventArgs {Key=ToKeyCode(button)});Tick();}
        private static void Kill(int id,bool keepSnapshot=false,bool removed=false,bool animation=false) {
            if(animation)APIShared.UnitAccess.Units[id].DeathMarker=1;else APIShared.UnitAccess.Units[id].Alive=false;
            if(!keepSnapshot)selection=selection.Where(u=>u.UnitId!=id).ToArray();
            if(removed)APIShared.UnitAccess.Missing[id]=true;
        }
        public static void Run() {
            foreach(int commandButton in new[]{0,1}) {
                var f=Fresh(commandButton);GameTileManagerAPI.Instance.TileManager.TileUnitIdGrid[2010]=42;
                GameTileManagerAPI.Instance.TileManager.StructureGrid[2010]=9;
                // Candidate capture deliberately makes no blanket unit/object/PCL decision.
                f.TryStartDrag(commandButton);Check(f.drag!=null,"Occupied start was discarded");
                Check(!f.Render()&&!f.menuViewModel.Allowed,"Old feedback authorized new gesture");
                f.Tick();Check(f.Render()&&f.menuViewModel.Allowed,"Fresh Move over unit not approved");
                int oldRows=f.drag.Rows;int density=f.drag.Density;
                GameMap.instance.X=20;Time.frameCount++;Input.mouseScrollDelta=(0,-2);
                f.OnKeyHeld(new UnityInputEventArgs {Key=f.ToKeyCode(commandButton)});
                Check(f.drag.Rows==oldRows+2&&f.drag.Density==density&&f.menuViewModel.Remembers==1,"Wheel state not preserved");
                int direction=f.drag.Geometry.Direction;
                f.SetFeedback(x:20,kind:5,detail:1,unit:66);f.Tick();Check(f.Render(),"Later enemy hover replaced anchor");
                Check(f.drag.Geometry.Direction==direction,"Feedback changed facing");
                f.Release();Check(f.Dispatched==1&&f.VanillaReleases==0&&f.LastPacket.TargetX==10&&f.LastPacket.TargetY==20,"Release did not use fixed anchor exactly once");
                f.Tick();Check(f.Dispatched==1&&f.VanillaReleases==0,"Consumed release returned");
                Check(f.Originals==4,"Original run count differs");
            }
            foreach(var rejected in new (int kind,int command,int detail,int mode,int file,int image)[]{(3,9,1,1,0x6B,0x20),
                (3,9,3,1,0x6B,0x20),(5,17,-10,1,0xAC,0x41),(3,1,0,5,0x6B,0)}) {
                var f=Fresh();f.TryStartDrag(1);f.SetFeedback(kind:rejected.kind,command:rejected.command,detail:rejected.detail,mode:rejected.mode,file:rejected.file,image:rejected.image);
                f.Tick();Check(!f.Render(),"Attack/interaction/rejection approved");f.Release();
                Check(f.Dispatched==0&&f.VanillaReleases==1,"Rejected Move consumed release");
            }
            foreach(bool missingMarkers in new[]{false,true}) {
                var f=Fresh();f.PreviewFails=!missingMarkers;f.TryStartDrag(1);if(missingMarkers)f.markerRenderer.ClearPreviewMarkerTiles();
                Check(f.drag!=null&&f.markerRenderer.Tiles==null,"Preview failure discarded gesture");
                f.Tick();Check(f.drag.Authorization.IsConfirmed,"No-marker native render did not confirm gesture");
                GameMap.instance.X=28;f.Release();
                Check(f.Dispatched==1&&f.VanillaReleases==0&&f.LastPacket.TargetX==10,"Preview failure moved anchor to release");
                Check(!f.groundFrameObserver.Active,"Released gesture left native demand armed");
            }
            var admitted=Fresh(count:1000);admitted.commandRuntime.Allowed=500;admitted.TryStartDrag(1);admitted.Tick();
            Check(admitted.drag.PreviewUnits.Length==500&&admitted.menuViewModel.Points.Length==500,"Preview used full blocked selection");
            admitted.Tick();admitted.Tick();admitted.OnKeyHeld(new UnityInputEventArgs{Key=KeyCode.Mouse1});
            Check(admitted.commandRuntime.Reads==1,"Preview repeated regional classification per frame");
            admitted.commandRuntime.placementRevision++;admitted.PublishPreview(admitted.drag,false);
            Check(admitted.markerRenderer.Tiles==null&&admitted.commandRuntime.Reads==1,"Invalidated preview reused or re-queried admission");
            admitted.Release();Check(admitted.Dispatched==1,"Preview invalidation blocked authoritative dispatch");
            var deferred=Fresh(count:1000);deferred.commandRuntime.Allowed=500;
            deferred.TryStartDrag(1);deferred.drag.Geometry.UpdateDirection(20,0,2);deferred.drag.Geometry.ApplyWheel(-2,54321);
            int manualRows=deferred.drag.Rows;int manualDirection=deferred.drag.Geometry.Direction;
            deferred.speechEngineLock=new object();deferred.Tick();
            Check(deferred.commandRuntime.Reads==1&&deferred.drag.PreviewUnits.Length==500,"Busy UI capture did not defer once to native engine-lock scope");
            Check(deferred.drag.Geometry.ExplicitDirection&&deferred.drag.Geometry.Direction==manualDirection&&deferred.drag.Rows==Math.Min(manualRows,500),"Deferred admission lost explicit facing/rows");
            deferred.Release();Check(deferred.Dispatched==1,"Deferred preview broke gesture dispatch");
            // A press during a running tick must neither wait for the engine lock nor lose the
            // formation: the next engine run completes the start under the lock it holds.
            foreach(string busyCase in new[]{"complete","released","map"}) {
                var busy=Fresh();var held=new System.Threading.ManualResetEventSlim();var leave=new System.Threading.ManualResetEventSlim();
                var owner=new System.Threading.Thread(()=>{lock(busy.speechEngineLock){held.Set();leave.Wait();}});owner.Start();held.Wait();
                var watch=System.Diagnostics.Stopwatch.StartNew();busy.TryStartDrag(1);watch.Stop();
                PendingDragStart queued=busy.pendingStart;
                leave.Set();owner.Join();
                Check(busy.drag==null&&queued!=null&&watch.ElapsedMilliseconds<1000,"Busy engine lock blocked or discarded the gesture start ("+busyCase+")");
                if(busyCase=="complete") {
                    busy.Tick();
                    Check(busy.drag!=null&&busy.pendingStart==null&&busy.drag.Target.NativeX==queued.Target.NativeX&&
                        busy.drag.Target.NativeY==queued.Target.NativeY,"Deferred start was not completed at the clicked target");
                    Check(busy.Render(),"Deferred start missed its native confirmation");
                    busy.Release();Check(busy.Dispatched==1&&busy.VanillaReleases==0,"Deferred start did not dispatch the formation");
                } else if(busyCase=="released") {
                    busy.Release();
                    Check(busy.drag==null&&busy.pendingStart==null&&busy.Dispatched==0&&busy.VanillaReleases==1,"Click released before deferred start did not stay Vanilla");
                } else {
                    busy.commandRuntime.mapEpoch++;busy.Tick();
                    Check(busy.drag==null&&busy.pendingStart==null,"Deferred start survived a map change");
                }
            }
            var delayedClear=Fresh();delayedClear.TryStartDrag(1);delayedClear.Tick();
            Shared.UnityMainThreadDispatch.Deferred=true;delayedClear.ClearPreview();Shared.UnityMainThreadDispatch.Deferred=false;
            delayedClear.PublishPreview(delayedClear.drag,true);
            while(Shared.UnityMainThreadDispatch.Pending.Count>0)Shared.UnityMainThreadDispatch.Pending.Dequeue()();
            Check(delayedClear.menuViewModel.Points!=null,"Delayed clear erased a newer preview");
            var pending=Fresh();pending.TryStartDrag(1);pending.Release();
            Check(pending.Dispatched==0&&pending.VanillaReleases==1,"Unconfirmed fast click replaced Vanilla");
            var nested=Fresh();nested.RenderInsideOriginal=true;nested.TryStartDrag(1);nested.Tick();
            Check(nested.menuViewModel.Allowed,"Fresh first in-tick render did not confirm Move");
            GameMap.instance.X=20;nested.SetFeedback(x:20,kind:5,detail:1,unit:66);nested.Tick();
            Check(nested.menuViewModel.Allowed,"Immediate drag lost first in-tick proof");
            nested.Release();Check(nested.Dispatched==1&&nested.VanillaReleases==0,"In-tick proof did not authorize release");
            var anchored=Fresh();anchored.TryStartDrag(1);anchored.Tick();Check(anchored.Render(),"Anchor setup failed");
            anchored.SetFeedback(kind:3,command:9,detail:1,image:0x20,unit:99);anchored.Tick();
            Check(anchored.Render(),"Later enemy hover on anchor revoked initial Move");
            anchored.Release();Check(anchored.Dispatched==1&&anchored.VanillaReleases==0,"Anchor hover changed command");
            var stale=Fresh();stale.TryStartDrag(1);stale.OriginalResult=0;stale.Tick();Check(!stale.Render(),"No-buffer run counted as fresh");
            stale.OriginalResult=1;stale.SetFeedback(coherent:false);stale.Tick();Check(!stale.Render(),"Incoherent cursor approved");
            stale.SetFeedback();stale.Tick();Check(stale.Render(),"Matching newer snapshot did not recover");
            foreach(string change in new[]{"map","selection","selectionTribe","button","shift","mode","player","localPlayer","tribe","count","ui"}) {
                var f=Fresh();f.TryStartDrag(1);f.Tick();Check(f.Render(),"Setup approval failed");
                switch(change) {
                    case "map":f.commandRuntime.mapEpoch++;break;
                    case "selection":selection[0]=new SelectionIdentity(1,999,0,1,1);APIShared.UnitAccess.Units[1].r_GlobalId=999;break;
                    case "selectionTribe":selectionTribe=4;foreach(var member in selection)APIShared.UnitAccess.Units[member.UnitId].r_TribeId=4;break;
                    case "button":button=0;break;
                    case "shift":shift=true;break;
                    case "mode":f.commandModeReader.Mode=5;break;
                    case "player":f.SetFeedback(player:2);f.Tick();f.Render();break;
                    case "localPlayer":GamePlayerManagerAPI.Instance.Player=2;break;
                    case "tribe":f.SetFeedback(tribe:4);f.Tick();f.Render();break;
                    case "count":f.SetFeedback(count:2);f.Tick();f.Render();break;
                    case "ui":FatControler.instance.UI=true;break;
                }
                f.Release();Check(f.Dispatched==0&&f.VanillaReleases==1,"Invalid context consumed release: "+change);
            }
            foreach(int mode in new[]{5,0x14,0x16,99}) {var f=Fresh();f.commandModeReader.Mode=mode;f.TryStartDrag(1);Check(f.drag==null,"Special mode captured");}
            var ui=Fresh();FatControler.instance.UI=true;ui.TryStartDrag(1);Check(ui.drag==null,"UI start captured");
            var outside=Fresh();GameMap.instance.X=-1;outside.TryStartDrag(1);Check(outside.drag==null,"Out-of-map start captured");
            var fail=Fresh();fail.TryStartDrag(1);fail.Tick();fail.Render();fail.RejectPacket=true;fail.Release();Check(fail.Dispatched==0&&fail.VanillaReleases==0,"Owned packet failure retargeted native release");
            foreach(int commandButton in new[]{0,1}) {
                foreach(bool keyUpEvent in new[]{false,true}) {
                    var f=Fresh(commandButton);f.TryStartDrag(commandButton);
                    f.SetFeedback(kind:5,command:17,detail:-10,file:0xAC,image:0x41);f.Tick();
                    Check(f.drag.Authorization.OwnsGroundGesture&&!f.drag.Authorization.IsConfirmed,"Ground rejection not distinguished from unknown input");
                    GameMap.instance.X=20;Time.frameCount++;Input.mouseScrollDelta=(0,0);
                    f.OnKeyHeld(new UnityInputEventArgs{Key=f.ToKeyCode(commandButton)});
                    f.SetFeedback(x:20);f.Tick();
                    if(keyUpEvent)f.Release();else {if(commandButton==0)EditorDirector.instance.Left=3;else EditorDirector.instance.RightUp=true;f.Tick();}
                    Check(f.Dispatched==0&&f.VanillaReleases==0&&f.drag==null,"Rejected anchor retargeted release / no-key-up");
                    Check(selection.All(member=>APIShared.UnitAccess.Units[member.UnitId].TargetX==2&&APIShared.UnitAccess.Units[member.UnitId].TargetY==3&&
                        APIShared.UnitAccess.Units[member.UnitId].PackedPath==12345&&APIShared.UnitAccess.Units[member.UnitId].PathProgress==7&&
                        APIShared.UnitAccess.Units[member.UnitId].MovementState==9),"Cancelled gesture altered old order/path state");
                    f.Tick();Check(f.VanillaReleases==0,"Cancelled native edge replayed");
                }
                foreach(string scenario in new[]{"before-proof","during","at-release","corpse","removed","cleared","leader-group","single"}) {
                    var f=Fresh(commandButton,scenario=="single"?2:6);f.TryStartDrag(commandButton);
                    if(scenario!="before-proof")f.Tick();
                    int desiredRows=3;f.drag.Geometry.ApplyWheel(f.drag.Rows-desiredRows,986);
                    int requested=f.drag.Geometry.RequestedRows;
                    Kill(scenario=="during"?6:1,keepSnapshot:scenario=="corpse"||scenario=="cleared",removed:scenario=="removed",animation:scenario=="corpse");
                    if(scenario=="cleared")APIShared.UnitAccess.Units[1].r_GlobalId=0;
                    if(scenario=="leader-group") {
                        selectionTribe=4;foreach(var member in selection)APIShared.UnitAccess.Units[member.UnitId].r_TribeId=4;
                        // Last completed feedback can still describe the old group.
                    }
                    if(scenario!="at-release")f.Tick();
                    GameMap.instance.X=20;f.Release();
                    Check(f.Dispatched==1&&f.VanillaReleases==0&&f.LastPacket.TargetX==10&&f.LastPacket.TargetY==20,"Death lost or retargeted order: "+scenario);
                    Check(f.LastPacket.UnitCount==(scenario=="single"?1:5),"Dead members still in packet: "+scenario);
                    Check(selection.Where(member=>APIShared.UnitAccess.IsReallyAlive(APIShared.UnitAccess.Units+member.UnitId)&&!APIShared.UnitAccess.Missing[member.UnitId])
                        .All(member=>APIShared.UnitAccess.Units[member.UnitId].TargetX==10&&APIShared.UnitAccess.Units[member.UnitId].TargetY==20&&
                            APIShared.UnitAccess.Units[member.UnitId].PackedPath==0&&APIShared.UnitAccess.Units[member.UnitId].MovementState==2),"Survivor order state not applied at anchor");
                    int dead=scenario=="during"?6:1;Check(APIShared.UnitAccess.Units[dead].TargetX==2&&APIShared.UnitAccess.Units[dead].PackedPath==12345,"Dead original received new order");
                    bool vanilla=scenario=="removed"||scenario=="cleared"||scenario=="leader-group";
                    Check(f.LastPacket.Formation==(byte)(vanilla?FormationKind.Vanilla:FormationKind.Line)&&
                        f.LastPacket.RequestedRows==(vanilla?0:Math.Min(requested,f.LastPacket.UnitCount)),"Survivor formation/fallback intent lost: "+scenario);
                    if(vanilla)Check(f.LastPacket.Density==1&&f.LastPacket.PlacementMode==0,"Vanilla fallback must use native standard placement");
                    f.Tick();Check(f.Dispatched==1,"Survivor command repeated");
                }
                var empty=Fresh(commandButton,2);empty.TryStartDrag(commandButton);empty.Tick();Kill(1);Kill(2);GameMap.instance.X=20;empty.Release();
                Check(empty.Dispatched==0&&empty.VanillaReleases==0&&empty.drag==null,"All deaths did not consume the gesture cleanly");
                var unavailable=Fresh(commandButton,6);unavailable.TryStartDrag(commandButton);unavailable.Tick();
                APIShared.LocalSelectionAPI.Ready=false;GameMap.instance.X=20;unavailable.Release();
                Check(unavailable.Dispatched==0&&unavailable.VanillaReleases==0&&unavailable.drag!=null,"Temporary snapshot gap lost or retargeted release");
                APIShared.LocalSelectionAPI.Ready=true;Kill(1);unavailable.Tick();
                Check(unavailable.Dispatched==1&&unavailable.VanillaReleases==0&&unavailable.LastPacket.UnitCount==5&&unavailable.LastPacket.TargetX==10,"Deferred release not resumed at original anchor");
                var delayedPreview=Fresh(commandButton,6);delayedPreview.TryStartDrag(commandButton);delayedPreview.Tick();
                APIShared.LocalSelectionAPI.Ready=false;delayedPreview.BecomeReadyInsideOriginal=true;delayedPreview.Release();
                Check(delayedPreview.Dispatched==0&&delayedPreview.drag!=null&&delayedPreview.menuViewModel.Points==null&&delayedPreview.markerRenderer.Tiles==null,
                    "Native survivor refresh republished a released preview");
                delayedPreview.Tick();Check(delayedPreview.Dispatched==1&&delayedPreview.LastPacket.UnitCount==5,"In-frame ready snapshot lost deferred command");
                var interrupted=Fresh(commandButton,6);interrupted.TryStartDrag(commandButton);interrupted.Tick();
                APIShared.LocalSelectionAPI.Ready=false;interrupted.AbortInOriginal=true;interrupted.Release();interrupted.Tick();
                Check(interrupted.drag==null&&interrupted.Dispatched==0&&interrupted.VanillaReleases==0,"Mid-run abort revived deferred release in finally");
                var mapAbort=Fresh(commandButton,6);mapAbort.TryStartDrag(commandButton);mapAbort.Tick();APIShared.LocalSelectionAPI.Ready=false;mapAbort.Release();
                mapAbort.commandRuntime.mapEpoch++;mapAbort.Tick();
                Check(mapAbort.drag==null&&mapAbort.VanillaReleases==0,"Map abort retargeted old deferred release");
                var newPress=Fresh(commandButton,6);newPress.TryStartDrag(commandButton);newPress.Tick();APIShared.LocalSelectionAPI.Ready=false;
                if(commandButton==0)EditorDirector.instance.Left=3;else EditorDirector.instance.RightUp=true;
                newPress.Tick();var oldDrag=newPress.drag;
                Check(oldDrag.ReleaseGate.ReleaseEventSeen,"Native-only deferred release left held controls active");
                if(commandButton==0)EditorDirector.instance.Left=1;else EditorDirector.instance.RightDown=true;
                newPress.OnKeyDown(new UnityInputEventArgs{Key=newPress.ToKeyCode(commandButton)});
                Check(!ReferenceEquals(newPress.drag,oldDrag)&&!FormationReleaseStateModel.HasCommandRelease(newPress.CaptureMouseState(EditorDirector.instance),commandButton)&&
                    (commandButton==0?EditorDirector.instance.Left==1:EditorDirector.instance.RightDown),"New down lost or inherited deferred old release");
                var deselected=Fresh(commandButton,6);deselected.TryStartDrag(commandButton);deselected.Tick();
                selection=selection.Skip(1).ToArray();deselected.Release();
                Check(deselected.Dispatched==0&&deselected.VanillaReleases==1,"Live deselection acquired death exception");
            }
            foreach(var special in new (int kind,int command,int detail,int mode,int file,int image,int unit,int building,int wall)[]{
                (3,9,1,1,0x6B,0x20,0,0,0),(3,9,3,1,0x6B,0x20,0,0,0),
                (5,17,-10,1,0xAC,0x41,1,0,0),(5,17,-10,1,0xAC,0x41,0,1,0),(5,17,-10,1,0xAC,0x41,0,0,1)}) {
                var f=Fresh();f.TryStartDrag(1);f.SetFeedback(kind:special.kind,command:special.command,detail:special.detail,mode:special.mode,file:special.file,image:special.image,unit:special.unit,building:special.building,wall:special.wall);f.Tick();
                GameMap.instance.X=20;f.Release();Check(f.Dispatched==0&&f.VanillaReleases==1,"Interaction/reject symbol misclassified as ground drag");
            }
            var acceptedError=Fresh();acceptedError.TryStartDrag(1);acceptedError.Tick();acceptedError.ThrowAfterAccept=true;
            GameMap.instance.X=20;acceptedError.Release();acceptedError.Tick();
            Check(acceptedError.Dispatched==1&&acceptedError.VanillaReleases==0&&selection.All(member=>APIShared.UnitAccess.Units[member.UnitId].TargetX==10),
                "Exception after claimed dispatch replayed/retargeted the native release");
            var retry=Fresh();
            var transient=new InvalidOperationException("temporary gesture error");
            retry.inputAttempt=1;retry.FailOpen("input-down",transient);retry.FailOpen("input-down",transient);
            Check(retry.LocalInputEnabled&&retry.inputFailures.ConsecutiveFailures==1,"One failed gesture must not disable LineUp");
            retry.inputAttempt=2;retry.TryStartDrag(1);retry.Tick();retry.Release();
            Check(retry.Dispatched==1&&retry.inputFailures.ConsecutiveFailures==0,"A later healthy gesture recovers and clears the streak");
            for(int attempt=3;attempt<=5;attempt++){retry.inputAttempt=attempt;retry.FailOpen("input-down",transient);}
            Check(!retry.LocalInputEnabled&&retry.Enabled,"Three independent failures disable local gestures without changing synchronized execution");
            var fatal=Fresh();fatal.FailOpen("native",new FormationNativeContractException("invalid contract"));
            Check(!fatal.LocalInputEnabled,"Critical native contract failure stops immediately");
            var limits=Fresh();limits.TryStartDrag(1);limits.Tick();limits.TryCreatePacket(limits.drag,out var largePacket);
            foreach(ushort size in new ushort[]{4000,4001,10000}){largePacket.UnitCount=size;Check(ValidatePacket(largePacket,out _),"Large selection rejected by marker budget");}
            largePacket.UnitCount=10001;Check(!ValidatePacket(largePacket,out _),"Selection beyond native unit capacity accepted");
            foreach(int count in new[]{100,200,550,1000,1300}) {
                var f=Fresh(count:count);f.TryStartDrag(1);f.Tick();Kill(1);f.Tick();
                Check(f.drag.Selection.Length==count-1&&f.commandRuntime.Reads==1,"Death triggered another regional preview classification");
                lock(f.speechEngineLock) {
                for(int warm=0;warm<10;warm++)f.RefreshGestureSelection(f.drag);
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int pass=0;pass<100;pass++)Check(f.RefreshGestureSelection(f.drag)==GestureSelectionState.Unchanged,"Survivor identity not stable");
                Check(GC.GetAllocatedBytesForCurrentThread()==before,"Unchanged active survivor pass allocated managed buffers");
                }
                f.Release();Check(f.Dispatched==1,"Large survivor command rejected");
            }
            // Strict legacy authorization remains object-free, while Formation
            // accepts only known coherent terminal Move pairs.
            var legacy=new GroundMovePreviewAuthorization(1,2,31,10,20);
            Check(!legacy.Observe(new GroundMoveFeedback(1,2,31,1,10,20,3,0x6B,0,1,0,42)),"Queue policy weakened");
            foreach(int detail in Enumerable.Range(-12,26)) foreach(int kind in new[]{-1,0,1,2,3,4,5}) {
                var proof=new FormationMoveAuthorization(1,2,31,10,20,0);
                var feedback=new GroundMoveFeedback(1,2,31,1,10,20,kind,0x6B,0,1,detail,42);
                Check(proof.Observe(feedback,1,true)==(kind==3&&detail==0),"Move classifier accepted other command");
            }
            Console.WriteLine("PASS anchored release/survivors: "+checks+" production assertions, both controls, 100/200/550/1000/1300, zero allocations in unchanged survivor passes.");
        }
    }
}
""";
        fixture = fixture.Replace("__PROTOCOL__", runtime.Members.OfType<FieldDeclarationSyntax>().Single(f=>f.Declaration.Variables.Any(v=>v.Identifier.Text=="ProtocolVersion")).ToFullString()).Replace("__MEMBERS__", string.Join("\n", members.Select(m => m.ToFullString())))
            .Replace("__PROOFS__", string.Join("\n", proofs.Select(p => p.ToFullString())));
        string[] paths = { "BugfixesAndQoL/src/Formations/FormationModel.cs", "BugfixesAndQoL/src/Formations/FormationOrderPacket.cs",
            "BugfixesAndQoL/src/Formations/FormationPresentation.cs",
            "BugfixesAndQoL/src/Formations/FormationNativeRunContext.cs", "BugfixesAndQoL/src/Formations/FormationReleaseStateModel.cs",
            "BugfixesAndQoL/src/Formations/FormationFailurePolicy.cs" };
        var trees = paths.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,p))))
            .Append(CSharpSyntaxTree.ParseText(fixture));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)).Append(MetadataReference.CreateFromFile(typeof(MessagePack.MessagePackWriter).Assembly.Location));
        var compilation = CSharpCompilation.Create("ProductionFormationMoveFixture", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
        using var output = new MemoryStream(); var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity==DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("BugfixesAndQoL.UnitCommands.RuntimeFixture")!.GetMethod("Run")!.Invoke(null,null); }
        catch(TargetInvocationException e) { throw e.InnerException??e; }
        Console.WriteLine("PASS: production Formation candidate, held input, preview publication/authorization and release paths executed; occupied/hovered starts, fresh/coherent proof, fixed anchor, exclusions, epoch/identity changes and one consumed Move; strict queue unchanged.");
    }
}
