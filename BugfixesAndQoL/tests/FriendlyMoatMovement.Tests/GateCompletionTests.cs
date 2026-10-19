using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class GateCompletionTests
{
    internal static void Validate(string root)
    {
        string directory = Path.Combine(root, "BugfixesAndQoL/src/UnitCommands");
        var selected = new Dictionary<string, string[]> {
            ["Attack/UnitCommandPathRuntime.ApproachFlood.cs"] = new[] { "ObserveAttackApproachFloodBuilder" },
            ["Attack/UnitCommandPathRuntime.BuildingCandidates.cs"] = new[] { "ObserveBuildingApproachBuilder", "ObserveBuildingCandidateConsumer" },
            ["Moat/DirectMoatCommandScopes.cs"] = new[] { "StageDirectCursorMoveWithOwnerRoute" },
            ["Integration/EnemyGatePolicyIntegration.cs"] = new[] { "BeginEnemyGateSearch", "EndEnemyGateSearch" }
        };
        var actual = new List<string>();
        foreach (var pair in selected)
        {
            var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(directory, pair.Key))).GetRoot();
            foreach (string name in pair.Value)
                actual.Add(syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == name).ToFullString());
        }
        string fixture = """
using System;
using System.Collections.Generic;
enum EnemyGateSearchKind { Attack, BuildingApproach, BuildingConsumer, CursorCommand }
interface IEnemyGatePathPolicy {
 bool HasPublishedMask {get;}
 object EnterNativeSearch(int player, EnemyGateSearchKind kind);
 void ExitNativeSearch(object token, EnemyGateSearchKind kind, bool completed, bool success);
}
static class EnemyGatePathPolicyBridge { public static IEnemyGatePathPolicy Current; }
// Same [Conditional] contract as Shared/Runtime/Diagnostics/DevDiag.cs (no SERP_DEVDIAG here).
namespace Shared { static class DevDiag {
 [System.Diagnostics.Conditional("SERP_DEVDIAG")] public static void Start(ref long started) {}
 [System.Diagnostics.Conditional("SERP_DEVDIAG")] public static void StopSpike(ref long started,string name,long spikeMicros) {}
} }
static class EnemyBridgeDiagnosticBridge {
 public static object BeginSearch(string kind,int player) => null;
 public static void EndSearch(object token,bool completed,int result) {}
}
class Policy : IEnemyGatePathPolicy {
 public bool HasPublishedMask => true;
 public int Depth, Enters, Exits; public bool ThrowOnExit;
 public List<bool> Completed = new List<bool>();
 public object EnterNativeSearch(int player,EnemyGateSearchKind kind) { Enters++;return ++Depth; }
 public void ExitNativeSearch(object token,EnemyGateSearchKind kind,bool completed,bool success) {
  if((int)token!=Depth || success) throw new Exception("scope order/void result");
  Depth--;Exits++;Completed.Add(completed);
  if(ThrowOnExit) throw new InvalidOperationException("observer failure");
 }
}
public class Fixture {
 int calls, errors; Action body; static int checks;
 static void Check(bool value) { checks++;if(!value)throw new Exception("gate wrapper assertion "+checks); }
 void Core() { calls++;body?.Invoke(); }
 int ResolveEnemyGateBuildingPlayer(int player,int tribe)=>player;
 int ResolveEnemyGateTribePlayer(int tribe)=>2;
 int ResolveEnemyGateCursorPlayer(int tribe)=>2;
 void TryLogDiagnosticFailure(string source,Exception error){errors++;}
 object BeginTemporaryAssassinScope(string kind,int tribe,int player,int building,int region)=>null;
 void EndTemporaryAssassinScope(object token,bool completed,int result){}
 void ObserveEnemyGateAssassinBuilding(IEnemyGatePathPolicy p,int t,int b,int r,int m){}
 void ObserveAttackApproachFloodBuilderWithMoat(IntPtr p,int t,int c,uint x,uint y,int n,int r,int m)=>Core();
 void ObserveBuildingApproachBuilderWithMoat(IntPtr p,int t,int b,int n,int r,int m)=>Core();
 void ObserveBuildingCandidateConsumerWithMoat(IntPtr p,int t,int v)=>Core();
 void StageDirectCursorMoveWithOwnerRouteCore(IntPtr p,int t,int x,int y,int c,int f)=>Core();
 /* ACTUAL */
 void Call(int index) {
  switch(index) {
   case 0:ObserveAttackApproachFloodBuilder(IntPtr.Zero,10,0,1,1,1,1,2);break;
   case 1:ObserveBuildingApproachBuilder(IntPtr.Zero,10,20,1,1,2);break;
   case 2:ObserveBuildingCandidateConsumer(IntPtr.Zero,10,0);break;
   default:StageDirectCursorMoveWithOwnerRoute(IntPtr.Zero,10,1,1,0,0);break;
  }
 }
 public static void Run() {
  for(int index=0;index<4;index++)for(int enabled=0;enabled<2;enabled++)for(int fail=0;fail<2;fail++) {
   var f=new Fixture();var p=new Policy();EnemyGatePathPolicyBridge.Current=enabled==1?p:null;
   var expected=new InvalidOperationException("native sentinel");
   if(fail==1) f.body=()=>throw expected;
   Exception observed=null;try{f.Call(index);}catch(Exception e){observed=e;}
   Check(ReferenceEquals(observed,fail==1?expected:null));Check(f.calls==1);
   Check(p.Depth==0 && p.Enters==enabled && p.Exits==enabled);
   if(enabled==1)Check(p.Completed.Count==1 && p.Completed[0]==(fail==0));
  }
  for(int index=0;index<4;index++) {
   var f=new Fixture();var p=new Policy();EnemyGatePathPolicyBridge.Current=p;
   f.body=()=>{f.body=null;f.Call((index+1)%4);};f.Call(index);
   Check(f.calls==2 && p.Enters==2 && p.Exits==2 && p.Depth==0);
   Check(p.Completed.TrueForAll(v=>v));
   p.ThrowOnExit=true;f.Call(index);Check(f.errors==1 && p.Depth==0 && f.calls==3);
  }
  Console.WriteLine("PASS: "+checks+" actual gate-wrapper checks; native calls/results/exceptions preserved, passive mode and nested scopes.");
 }
}
""";
        fixture = fixture.Replace("/* ACTUAL */", string.Join("\n", actual));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("GateCompletionActual", new[] { CSharpSyntaxTree.ParseText(fixture) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream(); var result = compilation.Emit(stream);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        Assembly.Load(stream.ToArray()).GetType("Fixture")!.GetMethod("Run")!.Invoke(null, null);
    }
}
