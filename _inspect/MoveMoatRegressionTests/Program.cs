using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

// Compile actual runtime methods against an in-memory native-grid fixture. No game
// assembly is produced or installed by this standalone regression runner.
string root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
string sourceDir = Path.Combine(root, "MoveMoatTest", "src");
var trees = Directory.GetFiles(sourceDir, "*.cs")
    .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), path: p)).ToArray();
var syntaxErrors = trees.SelectMany(t => t.GetDiagnostics())
    .Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
if (syntaxErrors.Length > 0)
    throw new Exception(string.Join("\n", syntaxErrors.Select(d => d.ToString())));

var methods = new HashSet<string>(new[] {
    "EnableCompletedMoatModeForScopedMovement", "GetBuilderPlan", "MatchesBuilderPlan",
    "TryCaptureUnitFallbackPathBuffer", "RestoreFallbackPathBuffer",
    "BuildPathWithCompletedMoatRouteVariantCore", "ValidatePendingFillApproach",
    "TryFindRequiredFriendlyCompletedMoatRouteForPlan", "TryGetCachedRequiredFriendlyRouteForPlan",
    "EnsureMoatWorkReachability", "TryGetMoatWorkRoute",
    "TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint",
    "EnsureReachabilityMap", "EnsureReachabilityStorage", "VisitNeighbour",
    "GetRouteVisitedMap", "GetRouteDistanceMap", "GetRouteDistance", "ObserveTraversedRegion",
    "GetCachedRouteSummaryForTarget", "GetCachedRouteSummaryForRegion"
});
var types = new HashSet<string>(new[] {
    "PlanScope", "RouteProbeSummary", "TargetedRouteDecision", "MoatWorkSelectionScope", "MoatWorkApproach", "PendingFillMoatApproach"
});
var constants = new HashSet<string>(new[] {
    "MaximumRegionId", "MaximumUnitCount", "MapWidth", "MapCellCount", "NativeTileCount",
    "RouteStateShift", "RouteCellMask", "GroundRouteState", "FriendlyMoatRouteState", "EnemyMoatRouteState",
    "CompletedMoatTileFlag", "CursorSpecialStructureTileFlagMask", "PathManagerOutputBufferOffset",
    "PathManagerOutputLengthOffset", "NativeUnitPathBufferOffset", "NativeUnitPathBufferStride",
    "PathManagerRouteVariantOffset", "OrdinaryWalkableTileFlag", "MoatWorkNeighbourX", "MoatWorkNeighbourY"
});
var selected = new List<MemberDeclarationSyntax>();
foreach (var tree in trees)
foreach (var cls in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Where(c => c.Identifier.Text == "MoveMoatPathTest"))
foreach (var member in cls.Members)
{
    if (member is MethodDeclarationSyntax m && methods.Contains(m.Identifier.Text) ||
        member is BaseTypeDeclarationSyntax t && types.Contains(t.Identifier.Text) ||
        member is FieldDeclarationSyntax f && f.Declaration.Variables.Any(v => constants.Contains(v.Identifier.Text)))
        selected.Add(member);
}
foreach (string name in methods)
    if (!selected.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == name))
        throw new Exception("Missing runtime method: " + name);
string extracted = "using System; using System.Collections.Generic; using System.Diagnostics; " +
    "using System.Runtime.InteropServices; namespace MoveMoatTest { " +
    "internal sealed unsafe partial class MoveMoatPathTest {\n" +
    string.Join("\n", selected.Select(m => m.ToFullString())) + "\n} }";
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
    .Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("MoveMoatRegressionFixture", new[] {
    CSharpSyntaxTree.ParseText(extracted),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "WeightedMoatRoutePlanner.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "_inspect", "MoveMoatRegressionTests", "RuntimeHarness.cs")))
}, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
using var output = new MemoryStream();
var emitted = compilation.Emit(output);
if (!emitted.Success)
    throw new Exception(string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
var assembly = Assembly.Load(output.ToArray());
try
{
    assembly.GetType("MoveMoatTest.MoveMoatPathTest").GetMethod("RunTests").Invoke(null, null);
}
catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
Console.WriteLine($"PASS: syntax of {trees.Length} runtime files; {selected.Count} actual runtime members compiled and exercised.");
