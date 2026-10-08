using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class FormationMoveRuntimeTests
{
    internal static void Validate(string root)
    {
        var runtime = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "APIShared/src/UnitCommands/FormationRuntime.cs"))).GetRoot()
            .DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Single(c => c.Identifier.Text == "FormationRuntime");
        string[] methods = { "TryStartDrag", "TryCaptureCommandTarget", "TryCaptureTarget",
            "EvaluateFormationTargetBounds", "IsTargetInsideNativeMap", "EvaluateCommandMode",
            "ValidateActiveDrag", "SelectionMatches", "AuthorizePreview", "EngineRunHook",
            "RunOriginalOnce", "RunOriginalAfterReleaseConsumed", "UpdateGesture",
            "OnKeyHeld", "OnKeyUp", "PublishPreview", "ResolveDirectionAndWidth",
            "HasExplicitDirection", "ClearPreview" };
        string[] types = { "ActiveDrag", "GroundTarget", "SelectionIdentity", "NativeDestination",
            "DispatchDisposition", "ReleaseConsumptionWatch" };
        var members = runtime.Members.Where(m => m is MethodDeclarationSyntax method &&
                methods.Contains(method.Identifier.Text) || m is BaseTypeDeclarationSyntax type &&
                types.Contains(type.Identifier.Text));
        var authorization = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "APIShared/src/UnitCommands/GroundMovePreviewAuthorization.cs"))).GetRoot();
        var proofs = authorization.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
            .Where(t => t.Identifier.Text != "NativeGroundMoveFeedbackReader");
        string fixture = """
using System; using System.Linq; using System.Collections.Generic;
using UnityEngine; using SHCDESE.API; using SHCDESE.Interop; using SHCDESE.EventAPI;
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
    public struct GameCursorManager {public uint r_IsCursorInGame,r_MouseTileX,r_MouseTileY;}
    public class GameTileManagerView {
        public int[] TileUnitIdGrid=new int[10000],StructureGrid=new int[10000];
        public ushort[] PathConnectionGrid=new ushort[10000];
    }
}
namespace SHCDESE.API {
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
namespace APIShared.Internal {
    public class NativeTroopCommandModeReader {public int Mode=1;public int Read()=>Mode;}
    public static class DebugLogHelper {public static void LogDebug(object l,string m) {} public static void LogWarning(object l,string m) {}}
    public static class GroundMovePreviewEligibility {
        public static GroundMovePreviewRejection EvaluateCommandMode(int mode)=>mode==1?GroundMovePreviewRejection.None:GroundMovePreviewRejection.NonMoveCommandMode;
    }
    public enum GroundMovePreviewRejection {None,OutsideMap,NonMoveCommandMode}
}
namespace APIShared.UnitCommands {
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
        internal void SetPreviewAuthorization(bool value)=>Allowed=value;
        internal void PublishPreview(FormationPreviewPoint[] points,FormationDirectionIndicator d)=>Points=points;
        internal void ClearPreview() {Allowed=false;Points=null;}
        internal void RememberSelectedRows(FormationKind kind,int rows) {Remembers++;}
    }
    internal sealed class Entry<T> {internal T Value;internal Entry(T value) {Value=value;}}
    internal sealed class Commands {internal int mapEpoch;}
    internal struct FormationOrderPacket {
        internal int OperationId,TribeId,TargetX,TargetY,Formation,Density,PlacementMode,DirectionSector,Width,Rows,UnitCount;
        internal ulong PlanHash;
    }
    public unsafe class RuntimeFixture {
        // Only external world/input/packet/placement boundaries are simulated.
        // Candidate, preview publication, authorization and release orchestration
        // below are compiled directly from the production runtime.
        private object log=new object();private readonly object stateSync=new object();
        private const int MapWidth=100,MinimumDragTileDistance=2;
        private ActiveDrag drag;private ReleaseConsumptionWatch releaseConsumptionWatch;
        private Marker markerRenderer=new Marker();private Menu menuViewModel=new Menu();
        private Commands commandRuntime=new Commands();private long nativeFeedbackGeneration;
        private long nativeFeedbackRunGeneration;private bool nativeFeedbackRunActive;
        private NativeGroundMoveFeedbackReader groundFeedbackReader=new NativeGroundMoveFeedbackReader();
        private APIShared.Internal.NativeTroopCommandModeReader commandModeReader=new APIShared.Internal.NativeTroopCommandModeReader();
        private Entry<FormationKind> formationConfig=new Entry<FormationKind>(FormationKind.Line);
        private Entry<int> densityConfig=new Entry<int>(2);
        private Entry<RangedPlacementMode> placementModeConfig=new Entry<RangedPlacementMode>(RangedPlacementMode.Off);
        private bool Enabled=true,initialized=true,startupConfirmed;private int lastWheelFrame=-1;
        private static SelectionIdentity[] selection;private static bool mapValid=true,shift;private static int button,selectionTribe=2;
        private int Originals,VanillaReleases,Dispatched;private int OriginalResult=1;private bool RejectPacket,RenderInsideOriginal;
        private FormationOrderPacket LastPacket;
        private bool HasValidMap()=>mapValid;
        private bool IsShiftHeld()=>shift;
        private int GetCommandMouseButton()=>button;
        private KeyCode ToKeyCode(int b)=>b==0?KeyCode.Mouse0:KeyCode.Mouse1;
        private static bool TryCaptureSelection(out SelectionIdentity[] units,out int tribe) {units=selection?.ToArray();tribe=selectionTribe;return units!=null&&units.Length>=2;}
        private void AbortDrag(string reason) {drag=null;ClearPreview();}
        private void ResetTransientState() {drag=null;ClearPreview();}
        private void FailOpen(string stage,Exception error) {Enabled=false;ResetTransientState();throw new Exception(stage,error);}
        private void RequireMainThread(string context) {}
        private void AgeReleaseConsumptionWatch() {}
        private void LogDebugNoThrow(string text) {} private void LogWarningNoThrow(string text) {}
        private void LogTargetRejection(APIShared.Internal.GroundMovePreviewRejection r,GroundTarget t) {}
        private string ToRejectionReason(APIShared.Internal.GroundMovePreviewRejection r)=>r.ToString();
        private FormationMouseState CaptureMouseState(EditorDirector d)=>new FormationMouseState(d.Left,d.RightDown,d.RightUp,d.StateRead,d.UpPending);
        private void ApplyMouseState(EditorDirector d,FormationMouseState s) {d.Left=s.LeftState;d.RightDown=s.RightDown;d.RightUp=s.RightUp;d.StateRead=s.StateRead;d.UpPending=s.UpPending;}
        private int engineRunOriginal(bool skip) {Originals++;if(FormationReleaseStateModel.HasCommandRelease(CaptureMouseState(EditorDirector.instance),button))VanillaReleases++;if(RenderInsideOriginal&&OriginalResult>0)Render();return OriginalResult;}
        private bool TryCreatePacket(ActiveDrag state,out FormationOrderPacket p) {
            p=new FormationOrderPacket {OperationId=1,TribeId=state.TribeId,TargetX=state.Target.NativeX,TargetY=state.Target.NativeY,
                Formation=(int)state.Kind,Density=state.Density,Rows=state.Rows,UnitCount=state.Selection.Length};
            return !RejectPacket;
        }
        private DispatchDisposition TryDispatch(FormationOrderPacket p,out string rejection) {Dispatched++;LastPacket=p;rejection=null;return DispatchDisposition.Accepted;}
        private FormationRole Classify(eChimps type)=>FormationRole.Neutral;
        private ulong ComputePlanHash(FormationUnit[] units,NativeDestination[] slots,RangedPlacementMode placement,int rows)=>1;
        private NativeDestination[] BuildManagedDestinations(int x,int y,FormationKind kind,int density,int direction,int rows,RangedPlacementMode placement,
            FormationUnit[] units,bool explicitDirection,out FormationDirectionIndicator indicator) {
            indicator=FormationDirectionIndicator.Hidden;return units.Select((u,i)=>new NativeDestination(y*MapWidth+x+i,x+i,y,u.Role)).ToArray();
        }
        __MEMBERS__
        private static void Check(bool value,string message) {if(!value)throw new Exception(message);}
        private static RuntimeFixture Fresh(int commandButton=1,int count=31) {
            button=commandButton;selectionTribe=2;shift=false;mapValid=true;selection=Enumerable.Range(1,count).Select(i=>new SelectionIdentity(i,(uint)i,0,1,1)).ToArray();
            GameMap.instance=new GameMap();FatControler.instance=new FatControler();EditorDirector.instance=new EditorDirector();
            GameTileManagerAPI.Instance=new GameTileManagerAPI();
            GamePlayerManagerAPI.Instance.Player=1;var f=new RuntimeFixture();
            f.SetFeedback(unit:42);return f;
        }
        private void SetFeedback(int x=10,int y=20,int kind=3,int command=1,int detail=0,int mode=1,int player=1,int tribe=2,int count=31,int unit=0,int building=0,int wall=0,int file=0x6B,int image=0,bool coherent=true) {
            groundFeedbackReader.Feedback=new GroundMoveFeedback(player,tribe,count,mode,x,y,kind,file,image,command,detail,unit,building,wall);
            *GamePlayerManagerAPI.Instance.Cursor=new GameCursorManager {r_IsCursorInGame=coherent?1u:0u,r_MouseTileX=(uint)x,r_MouseTileY=(uint)y};
        }
        private bool Render()=>markerRenderer.Authorization?.Invoke()??false;
        private void Tick()=>EngineRunHook(false);
        private void Release() {var d=EditorDirector.instance;if(button==0)d.Left=3;else d.RightUp=true;OnKeyUp(new UnityInputEventArgs {Key=ToKeyCode(button)});Tick();}
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
                    case "selection":selection[0]=new SelectionIdentity(1,999,0,1,1);break;
                    case "selectionTribe":selectionTribe=4;break;
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
            var fail=Fresh();fail.TryStartDrag(1);fail.Tick();fail.Render();fail.RejectPacket=true;fail.Release();Check(fail.Dispatched==0&&fail.VanillaReleases==1,"Packet failure consumed release");
            // Strict legacy authorization remains object-free, while Formation
            // accepts only known coherent terminal Move pairs.
            var legacy=new GroundMovePreviewAuthorization(1,2,31,10,20);
            Check(!legacy.Observe(new GroundMoveFeedback(1,2,31,1,10,20,3,0x6B,0,1,0,42)),"Queue policy weakened");
            foreach(int detail in Enumerable.Range(-12,26)) foreach(int kind in new[]{-1,0,1,2,3,4,5}) {
                var proof=new FormationMoveAuthorization(1,2,31,10,20,0);
                var feedback=new GroundMoveFeedback(1,2,31,1,10,20,kind,0x6B,0,1,detail,42);
                Check(proof.Observe(feedback,1,true)==(kind==3&&detail==0),"Move classifier accepted other command");
            }
        }
    }
}
""";
        fixture = fixture.Replace("__MEMBERS__", string.Join("\n", members.Select(m => m.ToFullString())))
            .Replace("__PROOFS__", string.Join("\n", proofs.Select(p => p.ToFullString())));
        string[] paths = { "APIShared/src/UnitCommands/FormationModel.cs",
            "APIShared/src/UnitCommands/FormationPresentation.cs",
            "APIShared/src/UnitCommands/FormationReleaseStateModel.cs" };
        var trees = paths.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,p))))
            .Append(CSharpSyntaxTree.ParseText(fixture));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ProductionFormationMoveFixture", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
        using var output = new MemoryStream(); var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity==DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("APIShared.UnitCommands.RuntimeFixture")!.GetMethod("Run")!.Invoke(null,null); }
        catch(TargetInvocationException e) { throw e.InnerException??e; }
        Console.WriteLine("PASS: production Formation candidate, held input, preview publication/authorization and release paths executed; occupied/hovered starts, fresh/coherent proof, fixed anchor, exclusions, epoch/identity changes and one consumed Move; strict queue unchanged.");
    }
}
