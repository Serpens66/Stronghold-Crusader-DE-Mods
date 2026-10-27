using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

// Executes the production renderer against private native data and a fake sprite sink.
// Only hook installation and the external native/Unity boundaries are replaced.
internal static class MoveMarkerRendererTests
{
    internal static void Validate(string root)
    {
        var source = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/Formations/Markers/LargeMoveTargetMarkerRenderer.cs"))).GetRoot()
            .DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "LargeMoveTargetMarkerRenderer");
        source = source.WithMembers(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.List(source.Members.Where(m =>
            m is not MethodDeclarationSyntax method || method.Identifier.Text != "Install")));
        string tests = """
        public static void Run()
        {
            int checks=0, originals=0, sprites=0, highestIdentity=0;
            void Check(bool condition,string message) {checks++;if(!condition)throw new Exception(message);}
            IntPtr image=Marshal.AllocHGlobal(0x60AD550);
            IntPtr manager=Marshal.AllocHGlobal(0x62224C);
            try
            {
                // Only data read by the renderer is initialized; no executable memory.
                new Span<ushort>((void*)(image+TileDrawHeadRva),LargeMoveTargetOverflowModel.NativeTileCount).Clear();
                Marshal.WriteInt32(image+CurrentTerrainHeightRva,0);
                Marshal.WriteInt32(image+DetailedTerrainRenderingRva,0);
                Marshal.WriteInt32(image+AnimationFrameRva,1);
                LargeMoveTargetMarkerRenderer Make()
                {
                    var renderer=new LargeMoveTargetMarkerRenderer(new BepInEx.Logging.ManualLogSource(),()=>true);
                    renderer.libraryHandle=image;renderer.installed=true;
                    renderer.visibleTileHook=new X64InlineHook {IsInstalled=true};
                    renderer.resetDrawListHook.Success=true;
                    renderer.resetDrawListHook.Original=_=>originals++;
                    renderer.spriteBuilder=(m,id,tile,secondary,mode,category,sprite,x,unused,player,layer,y,flags,owner)=>{
                        sprites++;highestIdentity=Math.Max(highestIdentity,id);
                        Check(id>=250&&id<4250,"Native sprite identity escaped its audited cache");
                    };
                    return renderer;
                }
                var r=Make();
                r.BeginOverlayPass(true);
                Check(!r.TryAddOverflowMarker(manager,107,82,12,6,-1,2),"Negative native marker must be skipped");
                Check(!r.TryAddOverflowMarker(manager,107,82,12,6,320800,2),"Out-of-range tile must be skipped");
                Check(r.ReplacementAvailable&&r.stagingBuffer.Count==0,"Invalid marker disabled the renderer");
                Marshal.WriteInt16(image+TileDrawHeadRva+42*2,1);
                Marshal.WriteInt32(manager+DrawRecordBaseOffset+DrawRecordSize,82);
                Marshal.WriteInt32(manager+DrawRecordBaseOffset+DrawRecordSize+4,107);
                Marshal.WriteInt32(manager+DrawRecordBaseOffset+DrawRecordSize+DrawRecordNextOffset,0);
                Check(r.TryAddOverflowMarker(manager,107,82,12,6,42,2)&&r.stagingBuffer.Count==0,
                    "Native sprite/category record was not recognized as a duplicate");
                Marshal.WriteInt16(image+TileDrawHeadRva+42*2,0);
                for(int tile=0;tile<4000;tile++)Check(r.TryAddOverflowMarker(manager,107,82,12,6,tile,2),"Valid marker rejected");
                Check(!r.TryAddOverflowMarker(manager,107,82,12,6,4000,2),"Overflow must be bounded");
                Check(r.TryAddOverflowMarker(manager,107,82,12,6,0,2),"Duplicate in full buffer rejected");
                r.EndOverlayPass(true);
                Check(r.ReplacementAvailable&&r.publishedOverflow.Count==4000,"Budget exhaustion disabled LineUp or discarded good markers");
                X64SmartCPUContext registers=default;
                var context=new NativePointer<X64SmartCPUContext>{Pointer=&registers};
                for(int tile=0;tile<4000;tile++){registers.RBX=(ulong)tile;r.RenderVisibleLargeMoveTarget(context);}
                Check(sprites==4000&&highestIdentity==4249,"Published bounded markers were not rendered");
                r.ResetDrawList(manager);Check(originals==1,"Vanilla reset did not run exactly once");
                r.BeginOverlayPass(true);Check(r.TryAddOverflowMarker(manager,107,82,12,6,4000,2),"Next healthy frame did not recover");r.EndOverlayPass(true);
                r.ResetDrawList(manager);
                r.SetPreviewMarkerTiles(new[]{12},()=>true);registers.RBX=12;
                int throws=0;
                SpriteBuilderDelegate good=r.spriteBuilder;
                r.spriteBuilder=(m,id,t,s,mode,c,sp,x,u,p,l,y,f,o)=>{throws++;throw new InvalidOperationException("temporary sprite failure");};
                r.RenderVisibleLargeMoveTarget(context);r.RenderVisibleLargeMoveTarget(context);
                Check(throws==1&&r.ReplacementAvailable,"A failed frame retried per tile or disabled the feature");
                r.ResetDrawList(manager);r.ResetDrawList(manager);
                Check(r.renderFailures.ConsecutiveFailures==1,"Idle frames must not reset a failed drawing attempt");
                r.spriteBuilder=good;r.RenderVisibleLargeMoveTarget(context);r.ResetDrawList(manager);
                Check(r.renderFailures.ConsecutiveFailures==0&&r.ReplacementAvailable,"Healthy render did not clear the failure streak");
                r.spriteBuilder=(m,id,t,s,mode,c,sp,x,u,p,l,y,f,o)=>throw new InvalidOperationException("temporary sprite failure");
                for(int attempt=0;attempt<3;attempt++){
                    r.RenderVisibleLargeMoveTarget(context);
                    Check(r.ReplacementAvailable==(attempt<2),"Renderer disabled at the wrong independent failure boundary");
                    r.ResetDrawList(manager);
                }
                var critical=Make();critical.visibleTileHook.IsInstalled=false;
                try{critical.SetRenderingActiveCore(true);throw new Exception("Missing installed hook was accepted");}
                catch(FormationNativeContractException e){critical.FailOpen(e);}
                Check(!critical.ReplacementAvailable&&critical.failed,"Critical native contract did not disable immediately");
                Console.WriteLine("PASS production Move marker renderer: "+checks+" assertions; real native record layout, invalid tiles, 4000-marker cap, frame recovery, three-failure stop and one original reset.");
            }
            finally {Marshal.FreeHGlobal(manager);Marshal.FreeHGlobal(image);}
        }
""";
        source = source.AddMembers(SyntaxFactory.ParseMemberDeclaration(tests)!);
        string fixture = """
using System;using System.Collections.Generic;using System.Runtime.InteropServices;
using BepInEx.Logging;
namespace BepInEx.Logging {internal class ManualLogSource {}}
namespace Shared {internal static class UnityMainThreadDispatch {internal static void TryRunInlineOrEnqueue(Action action)=>action();}}
namespace Shared {internal static class DebugLogHelper {
internal static void LogErrorLimited(object logger,string site,string message){}
internal static void LogWarningLimited(object logger,string site,string message){}
}}
namespace SHCDESE.Interop {internal enum eStructs {STRUCT_GATE_WOOD=47}}
namespace BugfixesAndQoL {
internal class X64InlineHook {internal bool IsInstalled;}
internal class HookTransaction {}
internal class DetourHandle<T> {internal bool Success;internal T Original;}
internal unsafe struct NativePointer<T> where T:unmanaged {internal T* Pointer;}
internal struct X64SmartCPUContext {internal ulong RBX,RSP;}
__CLASS__
}
""".Replace("__CLASS__", source.ToFullString());
        var trees = new[] { CSharpSyntaxTree.ParseText(fixture),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/Formations/Markers/LargeMoveTargetOverflowModel.cs"))),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/Formations/FormationFailurePolicy.cs"))) };
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ProductionMoveMarkerRenderer", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe:true));
        using var output = new MemoryStream();var result=compilation.Emit(output);
        if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
        try{Assembly.Load(output.ToArray()).GetType("BugfixesAndQoL.LargeMoveTargetMarkerRenderer")!.GetMethod("Run")!.Invoke(null,null);}
        catch(TargetInvocationException e){throw e.InnerException??e;}
        ValidateSelection(root);
    }

    private static void ValidateSelection(string root)
    {
        var runtime = CSharpSyntaxTree.ParseText(FeatureSourceModel.Read(root,"FormationRuntime",
            "BugfixesAndQoL/src/Formations")).GetRoot();
        var members = runtime.DescendantNodes().Where(n =>
            n is MethodDeclarationSyntax m && m.Identifier.Text=="TryCaptureSelection" ||
            n is StructDeclarationSyntax s && s.Identifier.Text=="SelectionIdentity" ||
            n is FieldDeclarationSyntax f && f.Declaration.Variables.Any(v=>v.Identifier.Text=="MaximumUnitCount"));
        string fixture = """
using System;using System.Linq;using System.Runtime.InteropServices;
using SHCDESE.API;using SHCDESE.Interop;
public class MainViewModel {public static bool viewModelLoaded=true;public static MainViewModel Instance=new();public bool IsMapEditorMode;}
public class EditorDirector {public static EditorDirector instance=new();public int ActivePlayerID;}
namespace SHCDESE.Interop {
public enum AliveState {IsAlive=2} public enum eChimps {CHIMP_TYPE_MACEMAN}
public struct GameUnit {public int r_TribeId,r_CurrentTilePositionX,r_CurrentTilePositionY;public uint r_GlobalId;public eChimps r_UnitChimp;}
public struct GameTribe {public AliveState r_AliveState;public int r_PlayerIdOwner;}
}
namespace APIShared {
public static class LocalSelectionAPI {public static int Count;public static bool TryCapture(int player,out LocalSelectionSnapshot s){s=new();return true;}}
public class LocalSelectionSnapshot {public int Count=>LocalSelectionAPI.Count;public Entry this[int i]=>new(){UnitId=i+1};public struct Entry{public int UnitId;}}
public static unsafe class UnitAccess {public static GameUnit* Units;public static bool TryGetById(int id,out GameUnit* u,out int failure){u=Units+id;failure=0;return id>0&&id<=10000;}public static bool IsReallyAlive(GameUnit* unit)=>unit!=null;}
}
namespace SHCDESE.API {
public class GamePlayerManagerAPI {public static GamePlayerManagerAPI Instance=new();public int Player=1;public bool Ai;public int GetLocalPlayerId()=>Player;public bool IsAIPlayer(int player)=>Ai;}
public unsafe class GameTribeManagerAPI {public static GameTribeManagerAPI Instance=new();public GameTribe* Tribe;public bool IsValidId(int id)=>id==2;public bool TryGetTribeById(int id,out GameTribe* t){t=Tribe;return id==2;}}
}
public unsafe class SelectionFixture {
__MEMBERS__
public static void Run(){
 int checks=0;void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
 APIShared.UnitAccess.Units=(GameUnit*)NativeMemory.AllocZeroed((nuint)(10001*sizeof(GameUnit)));
 GameTribe tribe=new(){r_AliveState=AliveState.IsAlive};GameTribeManagerAPI.Instance.Tribe=&tribe;
 try{
  for(int id=1;id<=10000;id++)APIShared.UnitAccess.Units[id]=new(){r_TribeId=2,r_GlobalId=(uint)id,r_CurrentTilePositionX=5,r_CurrentTilePositionY=5};
  MainViewModel.Instance.IsMapEditorMode=true;GamePlayerManagerAPI.Instance.Ai=true;
  foreach(int player in Enumerable.Range(1,8)){
   EditorDirector.instance.ActivePlayerID=player;GamePlayerManagerAPI.Instance.Player=player;tribe.r_PlayerIdOwner=player;
   foreach(int count in new[]{4000,4001,10000}){
    APIShared.LocalSelectionAPI.Count=count;
    Check(TryCaptureSelection(out var units,out int group)&&units.Length==count&&group==2&&units[^1].UnitId==count,
      "Production selection rejected a large manually controlled editor group");
   }
   APIShared.LocalSelectionAPI.Count=10001;Check(!TryCaptureSelection(out _,out _),"Selection above native unit capacity accepted");
  }
  MainViewModel.Instance.IsMapEditorMode=false;APIShared.LocalSelectionAPI.Count=2;
  Check(!TryCaptureSelection(out _,out _),"Regular AI group was admitted outside the editor");
  GamePlayerManagerAPI.Instance.Ai=false;Check(TryCaptureSelection(out _,out _),"Regular human group was rejected");
  Console.WriteLine("PASS production selection: "+checks+" assertions; 4000/4001/10000/10001, all eight editor players, unchanged game AI exclusion.");
 }finally{NativeMemory.Free(APIShared.UnitAccess.Units);}
}}
""".Replace("__MEMBERS__",string.Join("\n",members.Select(n=>n.ToFullString())));
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p=>MetadataReference.CreateFromFile(p));
        var compilation=CSharpCompilation.Create("ProductionFormationSelection",new[]{CSharpSyntaxTree.ParseText(fixture)},refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
        using var output=new MemoryStream();var result=compilation.Emit(output);
        if(!result.Success)throw new Exception(string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
        try{Assembly.Load(output.ToArray()).GetType("SelectionFixture")!.GetMethod("Run")!.Invoke(null,null);}
        catch(TargetInvocationException e){throw e.InnerException??e;}
    }
}
