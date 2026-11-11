// TEMP_GATE_ROUTE_ACCEPTANCE: execute actual diagnostic modules and actual weighted-edge producer with isolated API/memory fixtures.
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
string root = Path.GetFullPath(args[0]);
GateTopologyPublicationTests.Run(root);
// This fixture executes retained development-only diagnostics and their producers.
var parseOptions = CSharpParseOptions.Default.WithPreprocessorSymbols("SERP_DEVDIAG");
SyntaxTree Parse(string text, string path = "") => CSharpSyntaxTree.ParseText(text, parseOptions, path);
// Extracted members must not carry boundary directives from surrounding, unselected
// members. Keep the selected active syntax and remove inactive preprocessor trivia.
string Render(SyntaxNode node) => node.ReplaceTrivia(node.DescendantTrivia()
    .Where(t => t.IsDirective || t.IsKind(SyntaxKind.DisabledTextTrivia)), (old, _) => default).ToFullString();
string[] files = {
    "APIShared/src/Pathfinding/GateRoutes/GateAccessRules.cs", "APIShared/src/Core/Events/OrderedRegistrations.cs",
    "BugfixesAndQoL/src/AI/Raid/RaidAttackFieldEvaluation.cs",
    "APIShared/src/Pathfinding/Assassin/AssassinGateTransitionPolicy.cs", "APIShared/src/Pathfinding/GateRoutes/TemporaryGateRouteAcceptanceBridge.cs", "APIShared/src/Units/Selection/UnitAccess.cs", "Shared/Runtime/Native/TemporaryPackedRouteInspection.cs",
    "Pathfinding/src/UnitCommands/Movement/TemporaryGateRouteReporting.cs", "Pathfinding/src/GateRouting/TemporaryGateRouteAcceptance.DevDiag.cs",
    "Pathfinding/src/GateRouting/NativeAssassinTrace.DevDiag.cs", "Pathfinding/src/GateRouting/TemporaryGateAcceptanceAggregate.DevDiag.cs", "Pathfinding/src/GateRouting/RouteTilePolicySnapshot.cs",
    "Pathfinding/src/GateRouting/GateEdgeOwnership.cs", "Pathfinding/src/GateRouting/TemporaryGateCaptureTimeline.DevDiag.cs",
    // Real development-diagnostics facade instead of a copied stub (all overloads, no drift).
    "Shared/Runtime/Diagnostics/DevDiag.cs"
};
// Raid sources stay in BugfixesAndQoL; the fixture compiles everything into one namespace.
var trees = files.Select(f => Parse(File.ReadAllText(Path.Combine(root, f)).Replace("BugfixesAndQoL", "Pathfinding"), path: f)).ToList();
var production = Parse(File.ReadAllText(Path.Combine(root, "Pathfinding/src/Assassins/AssassinPathfindingRuntime.cs")));
string producer = Render(production.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == "ObservePreparedAssassinRoute"));
string searchWrapper = Render(production.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Single(m => m.Identifier.ValueText == "BuildWeightedPath"));
var correlation = Parse(File.ReadAllText(Path.Combine(root, "Pathfinding/src/GateRouting/AttackOrderCorrelationDiagnostics.DevDiag.cs")));
string correlationMethods = string.Join("\n", correlation.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => m.Identifier.ValueText is "ObserveTemporaryBuilderResult" or "TemporaryIdentityValue").Select(m => Render(m)));
string correlationFrames = string.Join("\n", correlation.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Where(c => c.Identifier.ValueText is "Frame" or "NativeFrame").Select(c => Render(c)));
var standalone = Parse(File.ReadAllText(Path.Combine(root, "Pathfinding/src/GateRouting/SamePclGateRouteRuntime.cs")));
string standaloneMethods = string.Join("\n", standalone.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => m.Identifier.ValueText is "FilterBuilding" or "FilterConsumer" or "FilterAlternateConsumer" or "CountIntegrityError").Select(m => Render(m)));
// The alternate consumer mirrors its diagnostic scope through DEVDIAG partial methods.
var standaloneDiag = Parse(File.ReadAllText(Path.Combine(root, "Pathfinding/src/GateRouting/SamePclGateRouteRuntime.DevDiag.cs")));
standaloneMethods += "\n" + string.Join("\n", standaloneDiag.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m => m.Identifier.ValueText is "DiagBeginAlternateConsumer" or "DiagEndAlternateConsumer").Select(m => Render(m).Replace("partial void", "void")));
var sharedBuildings=Parse(File.ReadAllText(Path.Combine(root,"Pathfinding/src/UnitCommands/Attack/UnitCommandPathRuntime.BuildingCandidates.cs")));
standaloneMethods += string.Join("\n",sharedBuildings.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
    .Where(m=>m.Identifier.ValueText is "ObserveBuildingApproachBuilder" or "ObserveBuildingCandidateConsumer" or "DiagFinishBuildingApproach").Select(m=>Render(m)));
string fixture = File.ReadAllText(Path.Combine(root, "Pathfinding/tests/TemporaryGateAcceptanceRuntimeTests/Fixture.cs"))
    .Replace("/* ACTUAL_WEIGHTED_PRODUCER */", producer).Replace("/* ACTUAL_SEARCH_WRAPPER */", searchWrapper)
    .Replace("/* ACTUAL_CORRELATION_METHODS */", correlationMethods).Replace("/* ACTUAL_CORRELATION_FRAMES */", correlationFrames)
    .Replace("/* ACTUAL_STANDALONE_METHODS */", standaloneMethods);
trees.Add(Parse(fixture, path: "Fixture.cs"));
var nativeHooks = Parse(File.ReadAllText(Path.Combine(root,"Pathfinding/src/GateRouting/NativeAssassinDiagnosticHooks.DevDiag.cs")));
string observeNative = Render(nativeHooks.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Observe"));
trees.Add(Parse(File.ReadAllText(Path.Combine(root,"Pathfinding/tests/TemporaryGateAcceptanceRuntimeTests/NativeTraceFixture.cs"))
    .Replace("/* ACTUAL_NATIVE_OBSERVER */",observeNative),path:"NativeTraceFixture.cs"));
var raid = Parse(File.ReadAllText(Path.Combine(root, "BugfixesAndQoL/src/AI/Raid/AiRaidRetargetFixRuntime.cs")).Replace("BugfixesAndQoL", "Pathfinding")).GetRoot();
string retry = Render(raid.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "ProcessRaidRetries"));
string retryTypes = string.Join("\n", raid.DescendantNodes().OfType<TypeDeclarationSyntax>()
    .Where(t => t.Identifier.ValueText is "RaidGroupKey" or "RaidRetry" or "BuildingTarget").Select(t => Render(t)));
trees.Add(Parse(File.ReadAllText(Path.Combine(root, "Pathfinding/tests/TemporaryGateAcceptanceRuntimeTests/RaidChainFixture.cs"))
    .Replace("/* ACTUAL_RETRY */", retry).Replace("/* ACTUAL_RETRY_TYPES */", retryTypes)
    .Replace("/* ACTUAL_BUILDING_WRAPPER */", Extract("Attack/UnitCommandPathRuntime.BuildingCandidates.cs", "ObserveBuildingApproachBuilder", "DiagFinishBuildingApproach")
        + Extract("Integration/EnemyGatePolicyIntegration.cs", "BeginEnemyGateSearch", "EndEnemyGateSearch", "ResolveEnemyGateBuildingPlayer")), path: "RaidChainFixture.cs"));
string Extract(string path, params string[] names) => string.Join("\n", Parse(File.ReadAllText(Path.Combine(root,"Pathfinding/src/UnitCommands",path)))
    .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>names.Contains(m.Identifier.ValueText)).Select(m=>Render(m)));
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
