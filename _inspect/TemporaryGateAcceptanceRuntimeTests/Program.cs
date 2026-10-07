// TEMP_GATE_ROUTE_ACCEPTANCE: execute actual diagnostic modules and actual weighted-edge producer with isolated API/memory fixtures.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
string root = Path.GetFullPath(args[0]);
string[] files = {
    "APIShared/src/TemporaryGateRouteAcceptanceBridge.cs", "APIShared/src/UnitAccess.cs", "Shared/TemporaryPackedRouteInspection.cs",
    "BugfixesAndQoL/src/TemporaryGateRouteReporting.cs", "Testmods/EnemyGatePathfindingTest/src/TemporaryGateRouteAcceptance.cs",
    "Testmods/EnemyGatePathfindingTest/src/TemporaryGateAcceptanceAggregate.cs", "Testmods/EnemyGatePathfindingTest/src/RouteTilePolicySnapshot.cs",
    "Testmods/EnemyGatePathfindingTest/src/GateEdgeOwnership.cs"
};
var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, f)), path: f)).ToList();
var production = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/AssassinPathfindingRuntime.cs")));
string producer = production.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == "ObservePreparedAssassinRoute").ToFullString();
string searchWrapper = production.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == "BuildWeightedPath").ToFullString();
string fixture = File.ReadAllText(Path.Combine(root, "_inspect/TemporaryGateAcceptanceRuntimeTests/Fixture.cs"))
    .Replace("/* ACTUAL_WEIGHTED_PRODUCER */", producer).Replace("/* ACTUAL_SEARCH_WRAPPER */", searchWrapper);
trees.Add(CSharpSyntaxTree.ParseText(fixture, path: "Fixture.cs"));
var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(p => MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("TemporaryGateAcceptanceActualRuntime", trees, refs,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
using var output = new MemoryStream();
var result = compilation.Emit(output);
if (!result.Success) throw new Exception(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
var assembly = Assembly.Load(output.ToArray());
assembly.GetType("RuntimeAcceptanceTests", true)!.GetMethod("Run")!.Invoke(null, null);
Console.WriteLine("PASS: actual gate diagnostic, actual packed-publication helper and production weighted-edge loop; isolated SDK fixtures, no game hooks.");
