// TEMP_GATE_ROUTE_ACCEPTANCE: execute actual diagnostic modules and actual weighted-edge producer with isolated API/memory fixtures.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
string root = Path.GetFullPath(args[0]);
string[] files = {
    "APIShared/src/Pathfinding/GateRoutes/GateAccessRules.cs",
    "BugfixesAndQoL/src/RaidAttackFieldEvaluation.cs",
    "APIShared/src/Pathfinding/Assassin/AssassinGateTransitionPolicy.cs", "APIShared/src/Pathfinding/GateRoutes/TemporaryGateRouteAcceptanceBridge.cs", "APIShared/src/Units/UnitAccess.cs", "Shared/Runtime/Native/TemporaryPackedRouteInspection.cs",
    "BugfixesAndQoL/src/UnitCommands/Movement/TemporaryGateRouteReporting.cs", "BugfixesAndQoL/src/GateRouting/TemporaryGateRouteAcceptance.DevDiag.cs",
    "BugfixesAndQoL/src/GateRouting/NativeAssassinTrace.DevDiag.cs", "BugfixesAndQoL/src/GateRouting/TemporaryGateAcceptanceAggregate.DevDiag.cs", "BugfixesAndQoL/src/GateRouting/RouteTilePolicySnapshot.cs",
    "BugfixesAndQoL/src/GateRouting/GateEdgeOwnership.cs", "BugfixesAndQoL/src/GateRouting/TemporaryGateCaptureTimeline.DevDiag.cs",
    // Real development-diagnostics facade instead of a copied stub (all overloads, no drift).
    "Shared/Runtime/Diagnostics/DevDiag.cs"
};
var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, f)), path: f)).ToList();
var production = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/AssassinPathfindingRuntime.cs")));
string producer = production.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == "ObservePreparedAssassinRoute").ToFullString();
string searchWrapper = production.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == "BuildWeightedPath").ToFullString();
var correlation = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/GateRouting/AttackOrderCorrelationDiagnostics.DevDiag.cs")));
string correlationMethods = string.Join("\n", correlation.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => m.Identifier.ValueText is "ObserveTemporaryBuilderResult" or "TemporaryIdentityValue").Select(m => m.ToFullString()));
string correlationFrames = string.Join("\n", correlation.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Where(c => c.Identifier.ValueText is "Frame" or "NativeFrame").Select(c => c.ToFullString()));
var standalone = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/GateRouting/SamePclGateRouteRuntime.cs")));
string standaloneMethods = string.Join("\n", standalone.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => m.Identifier.ValueText is "FilterBuilding" or "FilterConsumer" or "FilterAlternateConsumer").Select(m => m.ToFullString()));
// The alternate consumer mirrors its diagnostic scope through DEVDIAG partial methods.
var standaloneDiag = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/GateRouting/SamePclGateRouteRuntime.DevDiag.cs")));
standaloneMethods += "\n" + string.Join("\n", standaloneDiag.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => m.Identifier.ValueText is "DiagBeginAlternateConsumer" or "DiagEndAlternateConsumer").Select(m => m.ToFullString().Replace("partial void", "void")));
var sharedBuildings=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Attack/UnitCommandPathRuntime.BuildingCandidates.cs")));
standaloneMethods += string.Join("\n",sharedBuildings.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m=>m.Identifier.ValueText is "ObserveBuildingApproachBuilder" or "ObserveBuildingCandidateConsumer").Select(m=>m.ToFullString()));
string fixture = File.ReadAllText(Path.Combine(root, "_inspect/TemporaryGateAcceptanceRuntimeTests/Fixture.cs"))
    .Replace("/* ACTUAL_WEIGHTED_PRODUCER */", producer).Replace("/* ACTUAL_SEARCH_WRAPPER */", searchWrapper)
    .Replace("/* ACTUAL_CORRELATION_METHODS */", correlationMethods).Replace("/* ACTUAL_CORRELATION_FRAMES */", correlationFrames)
    .Replace("/* ACTUAL_STANDALONE_METHODS */", standaloneMethods);
trees.Add(CSharpSyntaxTree.ParseText(fixture, path: "Fixture.cs"));
var nativeHooks = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/GateRouting/NativeAssassinDiagnosticHooks.DevDiag.cs")));
string observeNative = nativeHooks.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Observe").ToFullString();
trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"_inspect/TemporaryGateAcceptanceRuntimeTests/NativeTraceFixture.cs"))
    .Replace("/* ACTUAL_NATIVE_OBSERVER */",observeNative),path:"NativeTraceFixture.cs"));
var raid = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/AiRaidRetargetFixRuntime.cs"))).GetRoot();
string retry = raid.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "ProcessRaidRetries").ToFullString();
string retryTypes = string.Join("\n", raid.DescendantNodes().OfType<TypeDeclarationSyntax>()
    .Where(t => t.Identifier.ValueText is "RaidGroupKey" or "RaidRetry" or "BuildingTarget").Select(t => t.ToFullString()));
trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "_inspect/TemporaryGateAcceptanceRuntimeTests/RaidChainFixture.cs"))
    .Replace("/* ACTUAL_RETRY */", retry).Replace("/* ACTUAL_RETRY_TYPES */", retryTypes)
    .Replace("/* ACTUAL_BUILDING_WRAPPER */", Extract("Attack/UnitCommandPathRuntime.BuildingCandidates.cs", "ObserveBuildingApproachBuilder")
        + Extract("Integration/EnemyGatePolicyIntegration.cs", "BeginEnemyGateSearch", "EndEnemyGateSearch", "ResolveEnemyGateBuildingPlayer")), path: "RaidChainFixture.cs"));
string Extract(string path, params string[] names) => string.Join("\n", CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands",path)))
    .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>m.ToFullString()));
var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Select(p => MetadataReference.CreateFromFile(p));
var compilation = CSharpCompilation.Create("TemporaryGateAcceptanceActualRuntime", trees, refs,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
using var output = new MemoryStream();
var result = compilation.Emit(output);
if (!result.Success) throw new Exception(string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
var assembly = Assembly.Load(output.ToArray());
assembly.GetType("RuntimeAcceptanceTests", true)!.GetMethod("Run")!.Invoke(null, null);
assembly.GetType("NativeTracePublicationTests", true)!.GetMethod("Run")!.Invoke(null, null);
Console.WriteLine("PASS: actual gate diagnostic, actual packed-publication helper and production weighted-edge loop; isolated SDK fixtures, no game hooks.");
