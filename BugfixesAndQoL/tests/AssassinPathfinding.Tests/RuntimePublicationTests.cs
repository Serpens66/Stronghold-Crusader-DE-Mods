using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static partial class Program
{
    private static void TestActualPublication(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/UnitCommands/Movement/MovementPathPublication.cs")));
        string[] names = { "BuildPathWithCompletedMoatRouteVariant", "BeginAssassinRoutePublication" };
        string methods = string.Join("\n", tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => names.Contains(m.Identifier.Text)).Select(m => m.ToFullString()));
        var movement = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/UnitCommands/Movement/UnitMovementContext.cs")));
        methods += movement.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.Text == "GetNativeMovementStart").ToFullString();
        string fixture = """
using System;
using System.Runtime.InteropServices;
using APIShared;
namespace APIShared.Internal { static class DebugLogHelper { internal static void LogDebug(object log,string text) {} } }
namespace BugfixesAndQoL.UnitCommands {
enum eChimps { CHIMP_TYPE_ARAB_ASSASIN=73 }
unsafe struct GameUnit { public uint r_GlobalId; public eChimps r_UnitChimp; public ushort r_ControllableForPlayerId; public int r_CurrentSpeed; public bool Alive; public int r_CurrentTilePositionX,r_CurrentTilePositionY,r_NextTilePositionX2,r_NextTilePositionY2,r_PathPlanStateBitFlags,r_MovementSubstep; }
unsafe static class UnitAccess {
 internal static GameUnit* Unit;
 internal static bool TryGetById(int id,out GameUnit* unit,out int index) { unit=Unit;index=id-1;return id==1; }
 internal static bool IsReallyAlive(GameUnit* unit)=>unit!=null&&unit->Alive;
}
enum EnemyGateSearchKind { Builder }
static class EnemyBridgeDiagnosticBridge { internal static object BeginSearch(string s,int p)=>null; internal static void EndSearch(object s,bool b,int r){} }
unsafe partial class UnitCommandPathRuntime {
 const int NativeUnitPathBufferOffset=0xB4FE78, NativeUnitPathBufferStride=1000, MaximumUnitCount=10000;
 const int PathManagerOutputBufferOffset=0x155F60, PathManagerOutputLengthOffset=0x155F68;
 private bool assassinExactPublicationLogged, nativeManualProbe;
 private IntPtr nativePathManager; private byte* nativeUnitManager; private object log;
 private Func<int> builder;
 private IEnemyGatePathPolicy BeginEnemyGateSearch(int p,EnemyGateSearchKind k,out object s){s=null;return null;}
 private object BeginTemporaryRouteReport(IntPtr p,string s)=>null;
 internal static void ReportTemporaryAssassinStage(string stage,string result,string detail) {}
 private void EndTemporaryRouteReport(IntPtr p,object s,bool b,int r){}
 private void EndEnemyGateSearch(object g,object s,EnemyGateSearchKind k,bool b,bool success){}
 private void TryLogDiagnosticFailure(string s,Exception e){}
 private int BuildPathWithCompletedMoatRouteVariantWithMoat(IntPtr p,int c,int v)=>builder();
 public static void Run() {
 var r=new UnitCommandPathRuntime();
 r.nativePathManager=Marshal.AllocHGlobal(PathManagerOutputLengthOffset+8);
 r.nativeUnitManager=(byte*)Marshal.AllocHGlobal(NativeUnitPathBufferOffset+3000);
 UnitAccess.Unit=(GameUnit*)Marshal.AllocHGlobal(sizeof(GameUnit));
 try {
 new Span<byte>((void*)r.nativePathManager,PathManagerOutputLengthOffset+8).Clear();
 *UnitAccess.Unit=new GameUnit {r_GlobalId=77,r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_ASSASIN,r_ControllableForPlayerId=2,Alive=true,r_CurrentTilePositionX=1,r_CurrentTilePositionY=2,r_MovementSubstep=8};
 byte* m=(byte*)r.nativePathManager;
 byte* path=r.nativeUnitManager+NativeUnitPathBufferOffset+1000;
 *(IntPtr*)(m+PathManagerOutputBufferOffset)=(IntPtr)path;
 *(int*)(m+8)=1;*(int*)(m+12)=2;*(int*)(m+16)=1;*(int*)(m+20)=1;*(int*)(m+0x88)=1;
 byte[] exact={2,6};
 void Assert(bool v,string s){if(!v)throw new Exception(s);}
 r.builder=()=> {path[0]=0;*(int*)(m+PathManagerOutputLengthOffset)=1;
 Assert(AssassinRouteHandoff.TryResolve(r.nativePathManager,1,2,1,1,out int player,out int speed)&&player==2&&speed==0,"bound player and speed");
 Assert(!AssassinRouteHandoff.TryResolve(r.nativePathManager,1,2,2,1,out _,out _),"different target rejected");
 Assert(AssassinRouteHandoff.Stage(r.nativePathManager,1,2,1,1,2,exact,3,()=>true),"real frame stage");return 1;};
 Assert(r.BuildPathWithCompletedMoatRouteVariant(r.nativePathManager,2,0)==3,"exact length returned to 196280");
 Assert(path[0]==2&&path[1]==6&&path[999]==0&&*(int*)(m+PathManagerOutputLengthOffset)==3,"real buffer replaced and length published");
 r.builder=()=> {path[0]=0;*(int*)(m+PathManagerOutputLengthOffset)=1;
 Assert(AssassinRouteHandoff.Stage(r.nativePathManager,1,2,1,1,2,exact,3,()=>true),"identity case staged");UnitAccess.Unit->r_GlobalId++;
 Assert(!AssassinRouteHandoff.TryResolve(r.nativePathManager,1,2,1,1,out _,out _),"reused ID cannot resolve old profile");return 1;};
 Assert(r.BuildPathWithCompletedMoatRouteVariant(r.nativePathManager,2,0)==1&&path[0]==0,"reused unit keeps native bytes");
 bool gate=true;
 r.builder=()=> {path[0]=0;*(int*)(m+PathManagerOutputLengthOffset)=1;
 Assert(AssassinRouteHandoff.Stage(r.nativePathManager,1,2,1,1,2,exact,3,()=>gate),"gate case staged");gate=false;return 1;};
 Assert(r.BuildPathWithCompletedMoatRouteVariant(r.nativePathManager,2,0)==1&&path[0]==0,"changed gate keeps native bytes");
 r.builder=()=> {path[0]=0;*(int*)(m+PathManagerOutputLengthOffset)=1;
 Assert(AssassinRouteHandoff.Stage(r.nativePathManager,1,2,1,1,2,exact,3,()=>true),"buffer case staged");*(IntPtr*)(m+PathManagerOutputBufferOffset)=(IntPtr)(path+1000);return 1;};
 Assert(r.BuildPathWithCompletedMoatRouteVariant(r.nativePathManager,2,0)==1&&path[0]==0,"changed pointer is never written");
 *(IntPtr*)(m+PathManagerOutputBufferOffset)=(IntPtr)path;
 r.builder=()=> {UnitAccess.Unit->r_CurrentSpeed=9;
 Assert(!AssassinRouteHandoff.TryResolve(r.nativePathManager,1,2,1,1,out _,out _),"changed speed invalidates profile");return 1;};
 r.BuildPathWithCompletedMoatRouteVariant(r.nativePathManager,2,0);
 r.builder=()=> {Assert(AssassinRouteHandoff.TryResolve(r.nativePathManager,1,2,1,1,out _,out int speed)&&speed==9,"current speed captured on subsequent search");
 UnitAccess.Unit->r_ControllableForPlayerId=0x0102;Assert(!AssassinRouteHandoff.TryResolve(r.nativePathManager,1,2,1,1,out _,out _),"changed full control word invalidates profile");return 1;};
 r.BuildPathWithCompletedMoatRouteVariant(r.nativePathManager,2,0);
 r.builder=()=> {Assert(AssassinRouteHandoff.TryResolve(r.nativePathManager,1,2,1,1,out int player,out _)&&player==258,"full control word preserved");
 UnitAccess.Unit->r_CurrentTilePositionX=2;Assert(!AssassinRouteHandoff.TryResolve(r.nativePathManager,1,2,1,1,out _,out _),"movement invalidates captured start");return 1;};
 r.BuildPathWithCompletedMoatRouteVariant(r.nativePathManager,2,0);
 UnitAccess.Unit->r_CurrentTilePositionX=1;UnitAccess.Unit->r_ControllableForPlayerId=2;
 UnitAccess.Unit->r_MovementSubstep=4;UnitAccess.Unit->r_NextTilePositionX2=1;UnitAccess.Unit->r_NextTilePositionY2=2;
 UnitAccess.Unit->r_CurrentTilePositionX=7;
 r.builder=()=> {Assert(AssassinRouteHandoff.TryResolve(r.nativePathManager,1,2,1,1,out _,out _),"moving unit binds native next tile instead of current tile");return 1;};
 r.BuildPathWithCompletedMoatRouteVariant(r.nativePathManager,2,0);
 r.builder=()=> {throw new Exception("native-test-failure");};
 try {r.BuildPathWithCompletedMoatRouteVariant(r.nativePathManager,2,0);throw new Exception("expected throw");}
 catch(Exception e){Assert(e.Message=="native-test-failure","native exception retained");}
 Assert(!AssassinRouteHandoff.HasFrame,"exception restores handoff stack");
 } finally {Marshal.FreeHGlobal((IntPtr)UnitAccess.Unit);Marshal.FreeHGlobal((IntPtr)r.nativeUnitManager);Marshal.FreeHGlobal(r.nativePathManager);}
 }
 PLACEHOLDER
} }
""";
        fixture = fixture.Replace("PLACEHOLDER", methods);
        var sources = new[] { CSharpSyntaxTree.ParseText(fixture), CSharpSyntaxTree.ParseText(
            File.ReadAllText(Path.Combine(root, "APIShared/src/Pathfinding/Assassin/AssassinRouteHandoff.cs"))),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared/src/Pathfinding/Assassin/AssassinGateTransitionPolicy.cs"))),
            CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared/src/Pathfinding/GateRoutes/TemporaryGateRouteAcceptanceBridge.cs"))) };
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ActualAssassinPublication", sources, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Check(emitted.Success, "production publication methods compile against fixture: " +
            string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(stream.ToArray()).GetType("BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime").GetMethod("Run").Invoke(null, null); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
        Check(true, "actual builder and buffer publication pass identity, policy, pointer and exception scenarios");
    }
}
