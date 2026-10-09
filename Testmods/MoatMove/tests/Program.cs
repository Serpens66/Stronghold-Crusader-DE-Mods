using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

try
{
// Compile actual runtime methods against an in-memory native-grid fixture. No game
// assembly is produced or installed by this standalone regression runner.
string root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
if (args.Contains("--native-only")) { FastNativeBackendTests.Validate(root); return; }
if (args.Contains("--redbird-only")) { InstalledRedBirdContract.Validate(); return; }
string sourceDir = Path.Combine(root, "Testmods", "MoatMove", "src");
string testDir = Path.Combine(root, "Testmods", "MoatMove", "tests");
string RuntimeSource(string name)
{
    string sharedName = name.Replace("FriendlyMoatMovementRuntime", "UnitCommandPathRuntime");
    string commandRoot = Path.Combine(root, "APIShared", "src", "UnitCommands");
    string[] files = sharedName == "UnitCommandPathRuntime.cs"
        ? Directory.GetFiles(commandRoot, "UnitCommandPathRuntime.*.cs", SearchOption.AllDirectories)
        : Directory.GetFiles(commandRoot, sharedName, SearchOption.AllDirectories);
    if (files.Length == 0) files = new[] { Path.Combine(sourceDir, name) };
    var units = files.OrderBy(p => p, StringComparer.Ordinal).Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p)).GetCompilationUnitRoot()).ToArray();
    string text = (string.Join("\n", units.SelectMany(u => u.Usings).Select(u => u.ToFullString()).Distinct()) +
        "\n" + string.Join("\n", units.SelectMany(u => u.Members).Select(m => m.ToFullString())))
        .Replace("namespace APIShared.UnitCommands", "namespace MoatMove")
        .Replace("using APIShared.UnitCommands;", "")
        .Replace("UnitCommandPathRuntime", "FriendlyMoatMovementRuntime")
        .Replace("FriendlyMoatTraversalProvider", "FriendlyMoatMovementRuntime")
        .Replace("internal override ", "private ").Replace("runtime.", "");
    text = text.Replace("using static APIShared.UnitCommands.FriendlyMoatMovementRuntime;", "using static MoatMove.FriendlyMoatMovementRuntime;");
    if (text.Contains("partial class FriendlyMoatMovementRuntime"))
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^        internal ", "        private ");
    return System.Text.RegularExpressions.Regex.Replace(text, @"(?m)^using SHCDESE[^;]*;", "");
}
string GitSource(string path)
{
    var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory=root,
        RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, CreateNoWindow=true };
    // Last accepted main-mod implementation before the APIShared split. HEAD
    // no longer owns these files and must not silently become its own oracle.
    const string preciseReferenceCommit = "7d5a5a9ef33fce47e09f545480829a5ebb13b060";
    start.ArgumentList.Add("show"); start.ArgumentList.Add(preciseReferenceCommit + ":" + path);
    using var process = System.Diagnostics.Process.Start(start);
    string text = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd();
    process.WaitForExit(); if(process.ExitCode != 0) throw new Exception(error); return text;
}

if (args.Contains("--fast-model-only"))
{
    var modelSources = new[] { "IFastRouteField.cs", "FastRouteField.cs", "FastCommandQueue.cs", "FastRoutePool.cs", "FastTraversalCache.cs" }
        .Select(name => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, name)))).ToList();
    var edgeDeclaration = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared/src/UnitCommands/Moat/MoatCandidateField.cs")))
        .GetRoot().DescendantNodes().OfType<DelegateDeclarationSyntax>().Single(d => d.Identifier.Text == "MoatSearchEdge");
    modelSources.Add(CSharpSyntaxTree.ParseText("namespace MoatMove {" + edgeDeclaration + "}"));
    foreach (string test in new[] { "FastRouteFieldTests.cs", "FastStateTests.cs" })
        modelSources.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, test))));
    var modelReferences = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
        .Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
    var model = CSharpCompilation.Create("MoatMoveFastModels", modelSources, modelReferences,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));
    using var bytes = new MemoryStream(); var result = model.Emit(bytes);
    if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
    var loaded = Assembly.Load(bytes.ToArray());
    foreach (string test in new[] { "FastRouteFieldTests", "FastStateTests" })
        loaded.GetType("MoatMove." + test).GetMethod("Run").Invoke(null, null);
    return;
}
if (args.Contains("--standalone-only"))
{
    SplitContracts.Validate(root);
    InstalledRedBirdContract.Validate();
    return;
}
string apiSharedPath=Environment.GetEnvironmentVariable("MOAT_TEST_API_SHARED_DLL") ??
    Path.Combine(root,"APIShared","BepInEx","plugins","APIShared_Serp","APIShared.dll");
if (!File.Exists(apiSharedPath))
    throw new FileNotFoundException("APIShared test reference is required; build and install APIShared first.", apiSharedPath);
string[] runtimeSourceNames =
{
    "AssassinSelectionAdapters.cs",
    "CursorConnectivity.cs", "CursorRegionGraph.cs", "DirectMoatCommandScopes.cs",
    "FastIntegration.cs", "IFastRouteField.cs", "FastRouteField.cs", "FastCommandQueue.cs", "FastRoutePool.cs", "FastTraversalCache.cs",
    "FastMoatRouting.cs", "FastMovementScheduler.cs", "FastGroupDistribution.cs", "FillWeightedRoutes.cs", "FriendlyMoatMovementPolicy.cs",
    "FriendlyMoatMovementRuntime.cs",
    "MoatPlacement.cs", "MoatPlacementSearch.cs",
    "MoatSearchKernel.cs", "MovementOptionsSnapshot.cs",
    "MovementPathPublication.cs", "MovementSearchContext.cs", "NativeFormationSlots.cs",
    "NativeMovementCadenceResolver.cs", "NativeMovementRecovery.cs", "UnitMovementContext.cs",
    "WeightedMoatPublication.cs", "WeightedMoatRoutePlanner.cs"
};
var trees = runtimeSourceNames.Select(name => CSharpSyntaxTree.ParseText(RuntimeSource(name), path: name)).ToArray();
var syntaxErrors = trees.SelectMany(t => t.GetDiagnostics())
    .Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
if (syntaxErrors.Length > 0)
    throw new Exception(string.Join("\n", syntaxErrors.Select(d => d.ToString())));
if (args.Contains("--source-only")) { SplitContracts.Validate(root); return; }
ValidateSelectionMetadata();
ValidateDetailedDiagnostics();
SplitContracts.Validate(root);

var methods = new HashSet<string>(new[] {
    "EmitSelectionCallAdapter",
    "LogDetailedInfo",
    "EnsureMoveCommandGroupSummary",
    "TryApplyBuildingConsumerFallback", "IsLegalBuildingCandidate", "BuildingCandidateEdge", "TryCaptureOrderedActiveGroupUnits", "CaptureBuildingApproachCandidates", "CaptureBuildingApproachBuffer", "RestoreBuildingApproachBuffer", "WriteBuildingApproachCandidates", "WriteBuildingApproachCandidate", "PublishBuildingApproachPairs", "TryGetPublishedBuildingFootprint", "MatchesSynchronousAttackMovementContext", "TryGetUnitAttackMoveTile", "IsValidBuildingApproachPair", "IsWalkableBuildingApproachEndpoint", "IsExactBuildingContextTile", "TryValidateHostileBuildingTarget",
    "GetReusableQualifiedRoute",
    "InvalidateMovementSearchData",
    "TryCaptureBuilderWeightedScope", "ObserveWeightedMoatShadowResult", "FindMoatWorkTargetWithOwnerRoute",
    "TryCreateMoatWorkSelectionScope", "TryCreatePendingDigMoatTarget", "ResolveMoatWorkTileWithOwnerRoute",
    "ValidatePendingDigTarget", "TryReadMoatRecordTile",
    "TryAllowDirectCursorMoveRegionPair", "SelectOwnerSafeGroupMoatMode", "ObserveCursorTilePairFallbackSelection", "TryProbeUnitApproachCursorRoute", "TryResolveHostileLivingUnitFromRawCursor", "TryGetHostileLivingUnitAtTile", "TryGetSelectedVanillaDigger", "AllowAttackCursorTilePairThroughCompletedMoat", "TryQualifySelectedGroupCursorRoute", "CreateCursorScopeForSnapshot", "TryQualifyCursorScope", "TryProbeDirectCursorRoute", "TryCaptureSelectedGroup", "CursorStartMatchesBoundSelection", "CursorScopeMatchesTargetTile", "EmitRecoveryAdapter", "SelectMoatWorkTarget", "AllowFillMoatApproachThroughFriendlyMoat", "TryGetMoatRecord", "TryReadMoatRecord", "TryFindBestFillMoatApproach", "IsOccupiedByOtherLivingUnit", "RestoreFailedRecovery", "ObserveNativeModeEntry", "TryRecoverBeforeBuilder", "RejectPreBuilder", "ValidateRecoveryEdges", "IsValidMoatRecordId", "PrepareMovementSearch", "TryDeferToNativeGroundPlan", "TryBuildTerminalFillRoute", "IsTerminalFillEdgeValid", "TryAllowUnitMoveRegion", "AllowBuilderAfterFailedRegionSearch", "CallVanillaBuilder", "TryReplaceUnsafeFallbackPath", "BuildReconstructedUnitPath", "TryPublishSafelyFasterWeightedRoute",
    "RentBuildingFallbackWorkBuffers", "ReturnBuildingFallbackWorkBuffers", "BuildPathWithCompletedMoatRouteVariantWithMoat", "ObserveUnitMoveOrder", "GetCurrentUnitMoveFrame", "AbandonUnitMoveFrame", "ClearUnitMoveFrames",
    "GetUnitMovePlan", "CopyMovementPlan", "GetNativeMovementStart", "TryAuditFallbackPath", "TryAuditFallbackPathCore", "IsCompletedEnemyMoatForPlayer",
    "DescribeFallbackContractFailure",
    "EnableCompletedMoatModeForScopedMovement", "GetBuilderPlan", "MatchesBuilderPlan", "BeginAssassinRoutePublication",
    "TryCaptureUnitFallbackPathBuffer", "RestoreFallbackPathBuffer",
    "CaptureAttackApproachState",
    "TryHandleVanillaLadderRegionPair", "RestoreVanillaLadderBuildingCandidates", "GetBuildingApproachPairKey",
    "BuildPathWithCompletedMoatRouteVariant", "BuildPathWithCompletedMoatRouteVariantCore", "IsValidAttackSourceRegionContext", "ValidatePendingFillApproach",
    "TryFindRequiredFriendlyCompletedMoatRouteForPlan", "TryGetCachedRequiredFriendlyRouteForPlan",
    "EnsureMoatWorkReachability", "TryGetMoatWorkRoute",
    "TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint",
    "EnsureReachabilityMap", "AdvanceReachabilityMap", "EnsureReachabilityStorage", "VisitNeighbour",
    "GetRouteVisitedMap", "GetRouteDistanceMap", "GetRouteDistance", "ObserveTraversedRegion",
    "GetCachedRouteSummaryForTarget", "GetCachedRouteSummaryForRegion"
});
var types = new HashSet<string>(new[] {
    "BuildingFallbackWorkBuffers", "RedBirdDetour",
    "BuildingApproachCandidate", "BuildingConsumerFallbackResult", "BuildingConsumerPerformanceScope", "AttackApproachState",
    "AttackApproachKind", "LadderAttackProbeScope", "LadderRegionTransition", "LadderBuildingCandidateRestoreResult",
    "QualifiedMovementRoute", "RouteDecisionKey", "RequiredRouteMetrics", "RequiredRouteCache",
    "PendingDigMoatTarget",
    "DirectCursorMoveScope", "BuildingCursorTarget", "BuildingHoverTileSource", "AttackCursorPairScope", "CursorPairFallbackKind", "CursorGroupRouteSummary", "SelectedCursorUnitSnapshot", "UnitMoveFrame", "PlanScope", "RouteProbeSummary", "TargetedRouteDecision", "MoatWorkSelectionScope", "MoatWorkApproach", "PendingFillMoatApproach"
});
var properties = new HashSet<string>(new[] { "CurrentOptions", "ExtensionsEnabled", "RequiredOnlyMode" });
var constants = new HashSet<string>(new[] {
    "buildingFallbackWorkBuffers", "DetailedDiagnosticsEnabled", "assassinExactPublicationLogged",
    "VanillaUnreachableCandidateScore", "buildingCandidateFields", "BuildingContextBlockingTileFlagMask", "VanillaAttackFloodResultCapacity", "PathManagerFloodGenerationOffset", "PathManagerFloodDepthOffset", "PathManagerFloodQueueHeadOffset", "PathManagerFloodQueueTailOffset", "PathManagerFloodResultTileOffset", "PathManagerFloodResultStride", "BuildingCandidateApproachTileOffset", "BuildingCandidateFootprintTileOffset", "BuildingCandidateScoreOffset",
    "SelectedMoatTileIdOffset", "SelectedMoatApproachXOffset", "SelectedMoatApproachYOffset",
    "TribeRecordSize", "TribeLeadUnitIdOffset", "TribeUnitCountOffset", "UnitGroupInactiveStateOffset", "MaximumTribeCount", "MoatRecordArrayOffset", "MoatRecordCountOffset", "MoatRecordSize", "MoatRecordTileIdOffset", "MoatRecordXOffset", "MoatRecordYOffset", "NativeUnitSlotDataOffset", "MaximumMoatRecordId", "MaximumRegionId", "MaximumUnitCount", "MapWidth", "MapCellCount", "NativeTileCount",
    "RouteStateShift", "RouteCellMask", "GroundRouteState", "FriendlyMoatRouteState", "EnemyMoatRouteState",
    "MovementBlockedLowTileFlagMask", "CompletedMoatTileFlag", "CursorSpecialStructureTileFlagMask", "PathManagerOutputBufferOffset",
    "PathManagerOutputLengthOffset", "NativeUnitPathBufferOffset", "NativeUnitPathBufferStride",
    "PathManagerRouteVariantOffset", "OrdinaryWalkableTileFlag", "MoatWorkNeighbourX", "MoatWorkNeighbourY"
    , "WeightedPublicationSafetyMarginTicks", "weightedPhaseTimingActive", "attackQualificationTimingDepth", "requiredPublicationTimingDepth"
});
var selected = new List<MemberDeclarationSyntax>();
foreach (var tree in trees)
foreach (var cls in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Where(c => c.Identifier.Text == "FriendlyMoatMovementRuntime"))
foreach (var member in cls.Members)
{
    if (member is MethodDeclarationSyntax m && methods.Contains(m.Identifier.Text) ||
        member is BaseTypeDeclarationSyntax t && types.Contains(t.Identifier.Text) ||
        member is PropertyDeclarationSyntax p && properties.Contains(p.Identifier.Text) ||
        member is FieldDeclarationSyntax f && f.Declaration.Variables.Any(v => constants.Contains(v.Identifier.Text)))
        selected.Add(member);
}
foreach (string name in methods)
    if (!selected.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == name))
        throw new Exception("Missing runtime method: " + name);
string extracted = "using APIShared; using Iced.Intel; using static Iced.Intel.AssemblerRegisters; using RedBird.Abstractions.Hooks; using RedBird.Abstractions.Hooks.Transaction; using RedBird.X64.Hooks.Transaction; using System; using System.Collections.Generic; using System.Diagnostics; " +
    "using System.Runtime.InteropServices; namespace MoatMove { " +
    "internal sealed unsafe partial class FriendlyMoatMovementRuntime {\n" +
    string.Join("\n", selected.Select(m => m.ToFullString())) + "\n} }";
string installedExtender = Path.Combine(
    @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition",
    "BepInEx", "plugins", "000shcdese");
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
    .Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p)).Concat(new[] {
        MetadataReference.CreateFromFile(Path.Combine(installedExtender,"Iced.dll")),
        MetadataReference.CreateFromFile(Path.Combine(installedExtender,"RedBird.Abstractions.dll")),
        MetadataReference.CreateFromFile(Path.Combine(installedExtender,"RedBird.Core.dll")),
        MetadataReference.CreateFromFile(Path.Combine(installedExtender,"RedBird.X64.dll")) });
foreach (string redBirdReference in new[]{"Iced.dll","RedBird.Abstractions.dll","RedBird.Core.dll","RedBird.X64.dll"})
    Assembly.LoadFrom(Path.Combine(installedExtender,redBirdReference));
// Pinned pre-optimization blob; read only, compiled exclusively into this test process.
var referenceStart = new System.Diagnostics.ProcessStartInfo("git") {
    WorkingDirectory=root, RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, CreateNoWindow=true };
referenceStart.ArgumentList.Add("show");
referenceStart.ArgumentList.Add("5c772900aba0db1a742fe95786f4d468f8068772");
using var referenceProcess=System.Diagnostics.Process.Start(referenceStart);
string referenceSource=referenceProcess.StandardOutput.ReadToEnd();
string referenceError=referenceProcess.StandardError.ReadToEnd();referenceProcess.WaitForExit();
if(referenceProcess.ExitCode!=0)throw new Exception("Missing pinned benchmark reference: "+referenceError);
var referenceClass=CSharpSyntaxTree.ParseText(referenceSource).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Single(c=>c.Identifier.Text=="MoatSearchKernel").ToFullString().Replace("MoatSearchKernel","ReferenceMoatSearchKernel");
var referenceTree=CSharpSyntaxTree.ParseText("using System; using System.Collections.Generic; namespace MoatMove {"+referenceClass+"}");
// The accepted precise copy, not the older benchmark blob, is the optimization oracle.
var comparisonClass = CSharpSyntaxTree.ParseText(GitSource("BugfixesAndQoL/src/MoatSearchKernel.cs")).GetRoot().DescendantNodes()
    .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "MoatSearchKernel")
    .ToFullString().Replace("MoatSearchKernel", "ComparisonMoatSearchKernel");
var comparisonTree = CSharpSyntaxTree.ParseText("using APIShared; using System; using System.Collections.Generic; namespace MoatMove {" + comparisonClass + "}");
var comparisonPlannerClass = CSharpSyntaxTree.ParseText(GitSource("BugfixesAndQoL/src/WeightedMoatRoutePlanner.cs")).GetRoot().DescendantNodes()
    .OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "WeightedMoatRoutePlanner")
    .ToFullString().Replace("WeightedMoatRoutePlanner", "ComparisonWeightedMoatRoutePlanner")
    .Replace("MoatSearchKernel", "ComparisonMoatSearchKernel");
var comparisonPlannerTree = CSharpSyntaxTree.ParseText("using APIShared; using System; using System.Diagnostics; namespace MoatMove {" + comparisonPlannerClass + "}");
var compilation = CSharpCompilation.Create("Assembly-CSharp", new[] {
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared", "src", "Units", "UnitAccess.cs"))
        .Replace("using SHCDESE.API;", "using GameUnitManagerAPI = MoatMove.GameUnitManagerAPI;")
        .Replace("using SHCDESE.Interop;", "using GameUnit = MoatMove.GameUnit;")
        .Replace("using SHCDESE.Interop.Enums;", "using AliveState = MoatMove.AliveState;")
        .Replace("public static unsafe class UnitAccess", "internal static unsafe class UnitAccess")),
    CSharpSyntaxTree.ParseText("namespace BepInEx.Logging { public class ManualLogSource { public void LogDebug(object message) { } } }"),
    
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared", "src", "Pathfinding", "GateRoutes", "EnemyGatePathPolicyBridge.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared", "src", "Pathfinding", "GateRoutes", "TemporaryGateRouteAcceptanceBridge.cs"))),
    CSharpSyntaxTree.ParseText("namespace APIShared {" + CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
        "APIShared/src/Pathfinding/Assassin/AssassinGateTransitionPolicy.cs"))).GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>()
        .Single(n => n.Identifier.Text == "AssassinTransitionKind") + "}"),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared", "src", "Pathfinding", "Assassin", "AssassinRouteHandoff.cs"))),
    referenceTree,
    comparisonTree,
    comparisonPlannerTree,
    CSharpSyntaxTree.ParseText(extracted),
    CSharpSyntaxTree.ParseText(RuntimeSource("WeightedMoatRoutePlanner.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("MoatSearchKernel.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("WeightedGridSearchKernel.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("MoatCandidateField.cs")),
    CSharpSyntaxTree.ParseText("namespace MoatMove {" + CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared/src/UnitCommands/Runtime/UnitCommandContracts.cs"))).GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>().Single(n => n.Identifier.Text == "IMoatSearchKernel") + "}"),
    CSharpSyntaxTree.ParseText(RuntimeSource("FastNativeKernel.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("FastNativeRouteField.cs")),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "FastNativeFixtures.cs"))),
    CSharpSyntaxTree.ParseText(RuntimeSource("IFastRouteField.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("FastRouteField.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("FastCommandQueue.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("FastRoutePool.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("FastTraversalCache.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("FastMoatRouting.cs").Replace("using SHCDESE.API;", "").Replace("using SHCDESE.Interop;", "")),
    CSharpSyntaxTree.ParseText(RuntimeSource("FastGroupDistribution.cs").Replace("using SHCDESE.API;", "")),
    CSharpSyntaxTree.ParseText(RuntimeSource("FastIntegration.cs").Replace("using SHCDESE.API;", "")),
    CSharpSyntaxTree.ParseText(RuntimeSource("TraversalCommandState.cs")),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "APIShared/src/Pathfinding/GateRoutes/EnemyBridgeDiagnosticBridge.cs"))),
    CSharpSyntaxTree.ParseText(RuntimeSource("EnemyGatePolicyIntegration.cs")),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "FastStateTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "FastRouteFieldTests.cs"))),
    CSharpSyntaxTree.ParseText(RuntimeSource("MoatPlacementSearch.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("NativeFormationSlots.cs").Replace("using SHCDESE.API;", "")),
    CSharpSyntaxTree.ParseText(RuntimeSource("FillWeightedRoutes.cs").Replace("using SHCDESE.API;", "").Replace("using SHCDESE.Interop;", "").Replace("using SHCDESE.Interop.Enums;", "")),
    CSharpSyntaxTree.ParseText(RuntimeSource("MoatPlacement.cs").Replace("using SHCDESE.API;", "").Replace("using SHCDESE.EventAPI.Units;", "").Replace("using SHCDESE.Interop;", "").Replace("using SHCDESE.Interop.Enums;", "")),
    CSharpSyntaxTree.ParseText(RuntimeSource("CursorRegionGraph.cs")),
    CSharpSyntaxTree.ParseText(RuntimeSource("CursorConnectivity.cs").Replace("using SHCDESE.API;", "").Replace("using SHCDESE.Interop;", "").Replace("using SHCDESE.Interop.Enums;", "")),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "CursorTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "PlacementTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "FormationBoundaryFixture.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "FillFormationTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "SearchKernelTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "RuntimeHarness.cs")), path: "RuntimeHarness.cs")
}, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
using var output = new MemoryStream();
compilation = compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(compilation.SyntaxTrees.Select(tree =>
    CSharpSyntaxTree.ParseText(tree.ToString(), path: tree.FilePath, encoding: System.Text.Encoding.UTF8)));
using var symbols = new MemoryStream();
var emitted = compilation.Emit(output, symbols, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(
    debugInformationFormat: Microsoft.CodeAnalysis.Emit.DebugInformationFormat.PortablePdb));
if (!emitted.Success)
    throw new Exception(string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
var assembly = Assembly.Load(output.ToArray(), symbols.ToArray());
try
{
    assembly.GetType("MoatMove.FriendlyMoatMovementRuntime").GetMethod(args.Contains("--runtime-native") ? "RunNativeTests" : "RunTests").Invoke(null, null);
    assembly.GetType("MoatMove.SearchKernelTests").GetMethod("Run").Invoke(null, null);
    assembly.GetType("MoatMove.FastRouteFieldTests").GetMethod("Run").Invoke(null, null);
    assembly.GetType("MoatMove.FastStateTests").GetMethod("Run").Invoke(null, null);
    assembly.GetType("MoatMove.CursorGraphTests").GetMethod("Run").Invoke(null, null);
    assembly.GetType("MoatMove.FriendlyMoatMovementRuntime").GetMethod("RunMachineContract").Invoke(null,new object[]{root});
}
catch (TargetInvocationException ex)
{
    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
    throw;
}
Console.WriteLine($"PASS: syntax of {trees.Length} runtime files; {selected.Count} actual runtime members compiled and exercised.");

void ValidateDetailedDiagnostics()
{
    var moveMoatClasses = trees.SelectMany(tree => tree.GetRoot().DescendantNodes()
        .OfType<ClassDeclarationSyntax>())
        .Where(type => type.Identifier.Text == "FriendlyMoatMovementRuntime").ToArray();
    var field = moveMoatClasses.SelectMany(type => type.Members.OfType<FieldDeclarationSyntax>())
        .Single(member => member.Declaration.Variables.Any(variable =>
            variable.Identifier.Text == "DetailedDiagnosticsEnabled"));
    var variable = field.Declaration.Variables.Single(item =>
        item.Identifier.Text == "DetailedDiagnosticsEnabled");
    if (!field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) ||
        variable.Initializer?.Value.IsKind(SyntaxKind.FalseLiteralExpression) != true)
        throw new Exception("DetailedDiagnosticsEnabled must remain disabled for production logging.");

    string helper = moveMoatClasses.SelectMany(type => type.Members.OfType<MethodDeclarationSyntax>())
        .Single(method => method.Identifier.Text == "LogDetailedInfo").ToFullString();
    string buffer = moveMoatClasses.SelectMany(type => type.Members.OfType<MethodDeclarationSyntax>())
        .Single(method => method.Identifier.Text == "BufferOrLogCommandDiagnostic").ToFullString();
    if (!helper.Contains("if (DetailedDiagnosticsEnabled)", StringComparison.Ordinal) ||
        !buffer.Contains("if (!DetailedDiagnosticsEnabled)", StringComparison.Ordinal))
        throw new Exception("Detailed diagnostics are not guarded at both logging entry points.");
    string ladderFix = RuntimeSource("FriendlyMoatMovementRuntime.LadderAttackFix.cs");
    if (!ladderFix.Contains("if (DetailedDiagnosticsEnabled)", StringComparison.Ordinal) ||
        !ladderFix.Contains("if (!DetailedDiagnosticsEnabled || activeAttackCommand == null)",
            StringComparison.Ordinal))
        throw new Exception("Ladder attack diagnostics bypass the production logging gate.");
    Console.WriteLine("PASS: detailed movement and ladder diagnostics are disabled for production logging.");
}







(string Minimum, string Maximum) ReadExtenderRange()
{
    string manifest = File.ReadAllText(Path.Combine(root, "Testmods", "MoatMove", "info.json"));
    string Read(string property)
    {
        string marker = "\"" + property + "\"";
        int propertyIndex = manifest.IndexOf(marker, StringComparison.Ordinal);
        if (propertyIndex < 0) return string.Empty;
        int colon = manifest.IndexOf(':', propertyIndex + marker.Length);
        int openingQuote = colon < 0 ? -1 : manifest.IndexOf('"', colon + 1);
        int closingQuote = openingQuote < 0 ? -1 : manifest.IndexOf('"', openingQuote + 1);
        return closingQuote < 0 ? string.Empty : manifest.Substring(openingQuote + 1, closingQuote - openingQuote - 1);
    }
    return (Read("MinimumScriptExtenderVersion"), Read("MaximumScriptExtenderVersion"));
}

bool IsExtenderVersionAllowed(string actual, string minimum, string maximum)
{
    if (minimum.Length == 0 && maximum.Length == 0) return true;
    if (!Version.TryParse(actual, out Version parsed)) return false;
    if (minimum.Length != 0 && (!Version.TryParse(minimum, out Version min) || parsed < min)) return false;
    if (maximum.Length != 0 && (!Version.TryParse(maximum, out Version max) || parsed > max)) return false;
    return true;
}

void ValidateSelectionMetadata()
{
    string file=@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll";
    using var stream=File.OpenRead(file);
    using var pe=new System.Reflection.PortableExecutable.PEReader(stream);
    var metadata=System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
    if(metadata.GetString(metadata.GetAssemblyDefinition().Name)!="Assembly-CSharp") throw new Exception("Selection assembly mismatch");
    var types=metadata.TypeDefinitions.Select(h=>metadata.GetTypeDefinition(h));
    var engine=types.Single(t=>metadata.GetString(t.Name)=="EngineInterface" && metadata.GetString(t.Namespace)=="");
    var field=engine.GetFields().Select(h=>metadata.GetFieldDefinition(h)).Single(f=>metadata.GetString(f.Name)=="selectedChimps");
    if((field.Attributes & FieldAttributes.Static)==0 || !metadata.GetBlobBytes(field.Signature).SequenceEqual(new byte[]{6,0x1D,8}))
        throw new Exception("Selection field must be static int[]");
    Console.WriteLine($"PASS installed metadata: Assembly-CSharp / global EngineInterface / selectedChimps static int[] ({field.Attributes}).");
}


}
catch (FileNotFoundException exception)
{
    Console.Error.WriteLine("MoatMove regression cannot start: " + exception.Message +
        " Path: " + exception.FileName);
    Environment.ExitCode = 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine("MoatMove regression failed: " + exception);
    Environment.ExitCode = 1;
}
