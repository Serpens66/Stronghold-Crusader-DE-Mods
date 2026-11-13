using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

// Execute the production publication method in both compilation modes: an unchanged topology
// must retain its immutable policy identity, while changed and explicitly cleared policies publish.
internal static class GateTopologyPublicationTests
{
    internal static void Run(string root)
    {
        string source = File.ReadAllText(Path.Combine(root, "Pathfinding/src/GateRouting/GateTopologySnapshotProvider.cs"));
        foreach (bool diagnostic in new[] { false, true })
        {
            var options = diagnostic ? CSharpParseOptions.Default.WithPreprocessorSymbols("SERP_DEVDIAG") : CSharpParseOptions.Default;
            var method = CSharpSyntaxTree.ParseText(source, options).GetRoot().DescendantNodes()
                .OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "RefreshTopologyIfDue");
            string activeMethod = method.ReplaceTrivia(method.DescendantTrivia()
                .Where(t => t.IsDirective || t.IsKind(SyntaxKind.DisabledTextTrivia)), (old, _) => default).ToFullString();
            string fixture = Fixture.Replace("/* PRODUCTION_REFRESH */", activeMethod);
            var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
                .Select(p => MetadataReference.CreateFromFile(p));
            var compilation = CSharpCompilation.Create("GateTopologyPublication" + diagnostic,
                new[] { CSharpSyntaxTree.ParseText(fixture, options) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
            using var stream = new MemoryStream();
            var result = compilation.Emit(stream);
            if (!result.Success) throw new Exception(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            Assembly.Load(stream.ToArray()).GetType("PublicationFixture")!.GetMethod("Run")!.Invoke(null, null);
        }
        Console.WriteLine("PASS: actual gate topology publication in normal and DEVDIAG builds; unchanged, changed and fail-open republish cases.");
    }

    private const string Fixture = """
using System;
using System.Threading;
namespace APIShared { public static class GateAccessRules { public static long Version; } }
namespace Shared {
 public static class DevDiag {
  public static void Start(ref long t) { } public static void Stop(ref long t,string s) { }
 }
 public static class DebugLogHelper { public static bool DiagnosticsEnabled; public static void LogDebug(object log,string text) { } }
}
public sealed class PublicationFixture {
 private const int TopologySafetyIntervalTicks=40;
 private long nextSnapshotAt,topologyBuilds,topologyChanges;
 private ulong lastTopologySignature,lastTopologyFingerprint;
 private int routePolicyWasCleared,peakMissingRequiredMasks,snapshotErrors,epochNumber;
 private bool detailedDiagnostics;
 private readonly object snapshotLock=new object(),log=new object();
 private sealed class RouteTilePolicySnapshot {
  public static readonly RouteTilePolicySnapshot Empty=new RouteTilePolicySnapshot();
  public int[] MissingRequiredGateMasks=Array.Empty<int>();
  public int NonEmptyPlayerMaskCount,MaskedDirectedEdges;
  public string DirectionMaskDiagnostics="fixture";
 }
 private sealed class Rejections { public string Format()=>"fixture"; }
 private sealed class TopologySnapshot {
  public static readonly TopologySnapshot Empty=new TopologySnapshot();
  public ulong Fingerprint; public string Detail; public RouteTilePolicySnapshot RoutePolicy=new RouteTilePolicySnapshot();
  public int[] Combinations=Array.Empty<int>(); public Rejections Rejections=new Rejections();
 }
 private sealed class AccessSnapshot { public ulong TopologyFingerprint; }
 private AccessSnapshot lastStableAccessSnapshot=new AccessSnapshot();
 private TopologySnapshot snapshot=TopologySnapshot.Empty,lastStableTopologySnapshot=TopologySnapshot.Empty;
 private TopologySnapshot prepared=new TopologySnapshot{Fingerprint=123};
 private ulong signature=7;
 private int publications;
 private Action<RouteTilePolicySnapshot> routePolicyConsumer;
 private ulong ComputeTopologySignature(ulong access)=>signature;
 private TopologySnapshot BuildTopologySnapshot(bool detail,TopologySnapshot previous)=>prepared;
 private static long Read(ref long value)=>value;
 private void RecordTopologyCoverage(TopologySnapshot value) { }
 private static string FormatTopologyChanges(TopologySnapshot previous,TopologySnapshot current,int maximum)=>"fixture";
 private void LogTestCases(TopologySnapshot value) { }
 private void LogBoundedError(ref int errors,string message,Exception exception) { errors++; }
 /* PRODUCTION_REFRESH */
 private static void Check(bool success,string reason) { if(!success)throw new Exception(reason); }
 public static void Run() {
  var f=new PublicationFixture();
  f.routePolicyConsumer=_=>f.publications++;
  f.RefreshTopologyIfDue(0);
  Check(f.lastTopologyFingerprint==123 && f.publications>0,"initial publication lost fingerprint");
  int before=f.publications;
  f.RefreshTopologyIfDue(40);
  Check(f.publications==before,"unchanged topology republishes policy");
  f.prepared=new TopologySnapshot{Fingerprint=456};f.signature++;
  f.RefreshTopologyIfDue(80);
  Check(f.publications>before && f.lastTopologyFingerprint==456,"changed topology does not publish");
  before=f.publications;f.routePolicyWasCleared=1;
  f.RefreshTopologyIfDue(120);
  Check(f.publications==before+1,"equivalent policy is not republished after a fail-open clear");
  before=f.publications;f.RefreshTopologyIfDue(160);
  Check(f.publications==before && f.snapshotErrors==0,"stable policy republishes after recovery");
 }
}
""";
}
