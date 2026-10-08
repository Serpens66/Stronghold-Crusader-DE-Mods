using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static partial class Program
{
    private static void TestActualRequestIndex(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        var runtime = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/AssassinPathfindingRuntime.cs"))).GetRoot();
        string methods = string.Join("\n", runtime.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text is "BuildRequestIndex" or "GetCoordinateIndex")
            .Select(m => m.ToFullString()));
        string info = runtime.DescendantNodes().OfType<StructDeclarationSyntax>()
            .Single(s => s.Identifier.Text == "AssassinRequestInfo").ToFullString();
        var access = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "APIShared/src/Units/UnitAccess.cs"))).GetRoot();
        string alive = access.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.Text == "IsReallyAlive" &&
                m.ParameterList.Parameters[0].Modifiers.Any(SyntaxKind.InKeyword)).ToFullString();
        string fixture = """
using System;
using System.Collections.Generic;
enum AliveState { None=0, IsAlive=2, MarkedForDeletion=3 }
enum eChimps { Other=1, CHIMP_TYPE_ARAB_ASSASIN=73 }
struct GameUnit {
 public AliveState r_AliveState;
 public eChimps r_UnitChimp;
 public ushort r_IsKilledByProjectile;
 public int r_CurrentTilePositionX,r_CurrentTilePositionY,r_ControllableForPlayerId,r_CurrentSpeed;
}
static class UnitAccess { ALIVE }
sealed class GameUnitManagerAPI {
 public static readonly GameUnitManagerAPI Instance=new();
 public GameUnit[] Units;
 public Span<GameUnit> GetUnitsAsSpan()=>Units;
}
static class AssassinGateRoutePolicy {
 public static int ReadControlPlayer(int control)=>control;
}
public static class RequestIndexFixture {
 const int MapWidth=800;
 METHODS
 INFO
 public static void Run() {
 void Assert(bool value,string label){if(!value)throw new Exception(label);}
 GameUnit Unit(int player,int delay,uint death=0,AliveState state=AliveState.IsAlive)=>
  new() { r_AliveState=state,r_UnitChimp=eChimps.CHIMP_TYPE_ARAB_ASSASIN,
   r_CurrentTilePositionX=10,r_CurrentTilePositionY=20,r_ControllableForPlayerId=player,
   r_CurrentSpeed=delay,r_IsKilledByProjectile=(ushort)death };
 var live=Unit(2,4);
 GameUnitManagerAPI.Instance.Units=new[]{live,Unit(3,99,1),Unit(2,88,2),Unit(4,77,0,AliveState.MarkedForDeletion),Unit(5,66,0,AliveState.None)};
 var result=BuildRequestIndex(); var value=result[GetCoordinateIndex(10,20)];
 Assert(result.Count==1&&!value.Ambiguous&&!value.GateAmbiguous,"corpses do not make live player ambiguous");
 Assert(value.PlayerId==2&&value.GatePlayerId==2&&value.SpeedDelay==4,"corpses do not change owner or weight");
 GameUnitManagerAPI.Instance.Units=new[]{Unit(3,99,1),Unit(2,88,2)};
 Assert(BuildRequestIndex().Count==0,"corpse-only coordinate has no request");
 GameUnitManagerAPI.Instance.Units=new[]{live,Unit(2,9,0x10000)};
 value=BuildRequestIndex()[GetCoordinateIndex(10,20)];
 Assert(!value.Ambiguous&&value.SpeedDelay==9,"unrelated upper word preserves live slowest speed");
 GameUnitManagerAPI.Instance.Units=new[]{live,Unit(3,7)};
 value=BuildRequestIndex()[GetCoordinateIndex(10,20)];
 Assert(value.Ambiguous&&value.GateAmbiguous,"different living players remain ambiguous");
 }
}
""";
        fixture = fixture.Replace("ALIVE", alive).Replace("METHODS", methods).Replace("INFO", info);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ActualAssassinRequestIndex",
            new[] { CSharpSyntaxTree.ParseText(fixture) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Check(emitted.Success, "production request index and life predicate compile: " +
            string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(stream.ToArray()).GetType("RequestIndexFixture").GetMethod("Run").Invoke(null, null); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
        Check(true, "actual request index excludes corpses while preserving live ambiguity and weights");
    }
}
