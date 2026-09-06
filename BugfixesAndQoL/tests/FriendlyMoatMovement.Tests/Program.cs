using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

// Compile actual runtime methods against an in-memory native-grid fixture. No game
// assembly is produced or installed by this standalone regression runner.
string root = Path.GetFullPath(args.Length == 0 ? "." : args[0]);
string sourceDir = Path.Combine(root, "BugfixesAndQoL", "src");
string testDir = Path.Combine(root, "BugfixesAndQoL", "tests", "FriendlyMoatMovement.Tests");
string[] runtimeSourceNames =
{
    "CursorConnectivity.cs", "CursorRegionGraph.cs", "DirectMoatCommandScopes.cs",
    "FillWeightedRoutes.cs", "FriendlyMoatMovementPolicy.cs",
    "FriendlyMoatMovementRuntime.cs", "MoatPlacement.cs", "MoatPlacementSearch.cs",
    "MoatSearchKernel.cs", "MoatWorkTargetSelection.cs", "MovementOptionsSnapshot.cs",
    "MovementPathPublication.cs", "MovementSearchContext.cs", "NativeFormationSlots.cs",
    "MoveFormationSpacingPolicy.cs",
    "NativeMovementCadenceResolver.cs", "NativeMovementRecovery.cs", "UnitMovementContext.cs",
    "WeightedMoatPublication.cs", "WeightedMoatRoutePlanner.cs"
};
var trees = runtimeSourceNames.Select(name => Path.Combine(sourceDir, name))
    .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), path: p)).ToArray();
var syntaxErrors = trees.SelectMany(t => t.GetDiagnostics())
    .Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
if (syntaxErrors.Length > 0)
    throw new Exception(string.Join("\n", syntaxErrors.Select(d => d.ToString())));
ValidateSelectionMetadata();
ValidateDetailedDiagnostics();
ValidateScriptExtenderIntegration();
ValidateRuntimeSources();
ValidateModeSettings();

var methods = new HashSet<string>(new[] {
    "LogDetailedInfo",
    "TryApplyBuildingConsumerFallback", "IsLegalBuildingCandidate", "BuildingCandidateEdge", "TryCaptureOrderedActiveGroupUnits", "CaptureBuildingApproachCandidates", "CaptureBuildingApproachBuffer", "RestoreBuildingApproachBuffer", "WriteBuildingApproachCandidates", "WriteBuildingApproachCandidate", "PublishBuildingApproachPairs", "TryGetPublishedBuildingFootprint", "MatchesSynchronousAttackMovementContext", "TryGetUnitAttackMoveTile", "IsValidBuildingApproachPair", "IsWalkableBuildingApproachEndpoint", "IsExactBuildingContextTile", "TryValidateHostileBuildingTarget",
    "GetReusableQualifiedRoute",
    "InvalidateMovementSearchData",
    "TryCaptureBuilderWeightedScope", "ObserveWeightedMoatShadowResult", "FindMoatWorkTargetWithOwnerRoute",
    "TryCreateMoatWorkSelectionScope", "TryCreatePendingDigMoatTarget", "ResolveMoatWorkTileWithOwnerRoute",
    "ValidatePendingDigTarget", "TryReadMoatRecordTile",
    "TryAllowDirectCursorMoveRegionPair", "SelectOwnerSafeGroupMoatMode", "ObserveCursorTilePairFallbackSelection", "TryProbeUnitApproachCursorRoute", "TryResolveHostileLivingUnitFromRawCursor", "TryGetHostileLivingUnitAtTile", "TryGetSelectedVanillaDigger", "AllowAttackCursorTilePairThroughCompletedMoat", "TryQualifySelectedGroupCursorRoute", "CreateCursorScopeForSnapshot", "TryQualifyCursorScope", "TryProbeDirectCursorRoute", "TryCaptureSelectedGroup", "CursorStartMatchesBoundSelection", "CursorScopeMatchesTargetTile", "EmitRecoveryAdapter", "SelectMoatWorkTarget", "AllowFillMoatApproachThroughFriendlyMoat", "TryGetMoatRecord", "TryReadMoatRecord", "TryFindBestFillMoatApproach", "IsOccupiedByOtherLivingUnit", "RestoreFailedRecovery", "ObserveNativeModeEntry", "TryRecoverBeforeBuilder", "RejectPreBuilder", "ValidateRecoveryEdges", "IsValidMoatRecordId", "PrepareMovementSearch", "TryDeferToNativeGroundPlan", "TryBuildTerminalFillRoute", "IsTerminalFillEdgeValid", "TryAllowUnitMoveRegion", "AllowBuilderAfterFailedRegionSearch", "CallVanillaBuilder", "TryReplaceUnsafeFallbackPath", "BuildReconstructedUnitPath", "TryPublishSafelyFasterWeightedRoute",
    "ObserveUnitMoveOrder", "GetCurrentUnitMoveFrame", "AbandonUnitMoveFrame", "ClearUnitMoveFrames",
    "GetUnitMovePlan", "CopyMovementPlan", "GetNativeMovementStart", "TryAuditFallbackPath", "TryAuditFallbackPathCore", "IsCompletedEnemyMoatForPlayer",
    "DescribeFallbackContractFailure",
    "EnableCompletedMoatModeForScopedMovement", "GetBuilderPlan", "MatchesBuilderPlan",
    "TryCaptureUnitFallbackPathBuffer", "RestoreFallbackPathBuffer",
    "BuildPathWithCompletedMoatRouteVariant", "BuildPathWithCompletedMoatRouteVariantCore", "IsValidAttackSourceRegionContext", "ValidatePendingFillApproach",
    "TryFindRequiredFriendlyCompletedMoatRouteForPlan", "TryGetCachedRequiredFriendlyRouteForPlan",
    "EnsureMoatWorkReachability", "TryGetMoatWorkRoute",
    "TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint",
    "EnsureReachabilityMap", "AdvanceReachabilityMap", "EnsureReachabilityStorage", "VisitNeighbour",
    "GetRouteVisitedMap", "GetRouteDistanceMap", "GetRouteDistance", "ObserveTraversedRegion",
    "GetCachedRouteSummaryForTarget", "GetCachedRouteSummaryForRegion"
});
var types = new HashSet<string>(new[] {
    "RedBirdDetour",
    "BuildingApproachCandidate", "BuildingConsumerFallbackResult", "BuildingConsumerPerformanceScope", "AttackApproachState",
    "QualifiedMovementRoute", "RouteDecisionKey", "RequiredRouteMetrics", "RequiredRouteCache",
    "PendingDigMoatTarget",
    "DirectCursorMoveScope", "BuildingCursorTarget", "BuildingHoverTileSource", "AttackCursorPairScope", "CursorPairFallbackKind", "CursorGroupRouteSummary", "SelectedCursorUnitSnapshot", "UnitMoveFrame", "PlanScope", "RouteProbeSummary", "TargetedRouteDecision", "MoatWorkSelectionScope", "MoatWorkApproach", "PendingFillMoatApproach"
});
var properties = new HashSet<string>(new[] { "CurrentOptions", "ExtensionsEnabled", "RequiredOnlyMode" });
var constants = new HashSet<string>(new[] {
    "DetailedDiagnosticsEnabled",
    "VanillaUnreachableCandidateScore", "buildingCandidateFields", "BuildingContextBlockingTileFlagMask", "VanillaAttackFloodResultCapacity", "PathManagerFloodResultTileOffset", "PathManagerFloodResultStride", "BuildingCandidateApproachTileOffset", "BuildingCandidateFootprintTileOffset", "BuildingCandidateScoreOffset",
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
string extracted = "using Iced.Intel; using static Iced.Intel.AssemblerRegisters; using RedBird.Abstractions.Hooks; using RedBird.Abstractions.Hooks.Transaction; using RedBird.X64.Hooks.Transaction; using System; using System.Collections.Generic; using System.Diagnostics; " +
    "using System.Runtime.InteropServices; namespace BugfixesAndQoL { " +
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
var referenceTree=CSharpSyntaxTree.ParseText("using System; using System.Collections.Generic; namespace BugfixesAndQoL {"+referenceClass+"}");
var compilation = CSharpCompilation.Create("Assembly-CSharp", new[] {
    referenceTree,
    CSharpSyntaxTree.ParseText(extracted),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "WeightedMoatRoutePlanner.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "MoatSearchKernel.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "MoatPlacementSearch.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "NativeFormationSlots.cs")).Replace("using SHCDESE.API;", "")),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "MoveFormationSpacingPolicy.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "FillWeightedRoutes.cs")).Replace("using SHCDESE.API;", "").Replace("using SHCDESE.Interop;", "").Replace("using SHCDESE.Interop.Enums;", "")),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "MoatPlacement.cs")).Replace("using SHCDESE.API;", "").Replace("using SHCDESE.EventAPI.Units;", "").Replace("using SHCDESE.Interop;", "").Replace("using SHCDESE.Interop.Enums;", "")),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "CursorRegionGraph.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "CursorConnectivity.cs")).Replace("using SHCDESE.API;", "").Replace("using SHCDESE.Interop;", "").Replace("using SHCDESE.Interop.Enums;", "")),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "CursorTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "PlacementTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "FillFormationTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "SearchKernelTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(testDir, "RuntimeHarness.cs")))
}, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
using var output = new MemoryStream();
var emitted = compilation.Emit(output);
if (!emitted.Success)
    throw new Exception(string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
var assembly = Assembly.Load(output.ToArray());
try
{
    assembly.GetType("BugfixesAndQoL.FriendlyMoatMovementRuntime").GetMethod("RunTests").Invoke(null, null);
    assembly.GetType("BugfixesAndQoL.SearchKernelTests").GetMethod("Run").Invoke(null, null);
    assembly.GetType("BugfixesAndQoL.CursorGraphTests").GetMethod("Run").Invoke(null, null);
    assembly.GetType("BugfixesAndQoL.FriendlyMoatMovementRuntime").GetMethod("RunMachineContract").Invoke(null,new object[]{root});
}
catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
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
        throw new Exception("DetailedDiagnosticsEnabled must remain a readonly false default.");

    string helper = moveMoatClasses.SelectMany(type => type.Members.OfType<MethodDeclarationSyntax>())
        .Single(method => method.Identifier.Text == "LogDetailedInfo").ToFullString();
    string buffer = moveMoatClasses.SelectMany(type => type.Members.OfType<MethodDeclarationSyntax>())
        .Single(method => method.Identifier.Text == "BufferOrLogCommandDiagnostic").ToFullString();
    if (!helper.Contains("if (DetailedDiagnosticsEnabled)", StringComparison.Ordinal) ||
        !buffer.Contains("if (!DetailedDiagnosticsEnabled)", StringComparison.Ordinal))
        throw new Exception("Detailed diagnostics are not guarded at both logging entry points.");
    Console.WriteLine("PASS: detailed diagnostics are code-gated and default to false.");
}

void ValidateScriptExtenderIntegration()
{
    string plugin = File.ReadAllText(Path.Combine(sourceDir, "BugfixesAndQoLPlugin.cs"));
    string orchestrator = File.ReadAllText(Path.Combine(sourceDir, "BugfixesAndQoLRuntime.cs"));
    string runtime = string.Join("\n", trees.Select(tree => tree.ToString()));
    string project = File.ReadAllText(Path.Combine(root, "BugfixesAndQoL", "BugfixesAndQoL.csproj"));
    string build = File.ReadAllText(Path.Combine(root, "BugfixesAndQoL", "build.bat"));

    if (!plugin.Contains("[BepInDependency(ScriptExtenderGuid, \"2.2.0\")]", StringComparison.Ordinal) ||
        !plugin.Contains("[BepInIncompatibility(LegacyMoveMoatGuid)]", StringComparison.Ordinal) ||
        !orchestrator.Contains("new FriendlyMoatMovementRuntime(", StringComparison.Ordinal) ||
        !orchestrator.Contains("log, settings, context, referenceHashMatches", StringComparison.Ordinal))
        throw new Exception("BugfixesAndQoL does not declare and own the integrated friendly-moat runtime.");
    foreach (string forbidden in new[]{"MonoMod.RuntimeDetour", "NativeDetour", "Zhuqiaomon", "GenerateTrampoline", ".Apply()", ".Undo()"})
        if (runtime.Contains(forbidden, StringComparison.Ordinal))
            throw new Exception("Legacy hook contract remains: " + forbidden);
    foreach (string required in new[]{"RedBird.Abstractions.dll", "RedBird.Core.dll", "RedBird.X64.dll", "RedBird.Backends.NativeX64.dll"})
        if (!project.Contains(required, StringComparison.Ordinal))
            throw new Exception("Missing RedBird reference shipped with Script Extender 2.2.0: " + required);
    if (!runtime.Contains("FailureMode = TransactionFailureMode.RollbackAndThrow", StringComparison.Ordinal) ||
        !runtime.Contains("OwnsHooks = true", StringComparison.Ordinal) ||
        !runtime.Contains("Handle.Failure == null", StringComparison.Ordinal) ||
        !runtime.Contains("Handle.ResolvedAddress == targetAddress", StringComparison.Ordinal) ||
        !runtime.Contains("Handle.IsInstalled", StringComparison.Ordinal) ||
        !runtime.Contains("GetSelectedChimps() ?? Array.Empty<SelectedUnitInfo>()", StringComparison.Ordinal))
        throw new Exception("Friendly moat movement is missing a required transaction or selection guard.");
    if (runtime.Contains("RegisterImprovedMoatFillingProvider", StringComparison.Ordinal) ||
        Directory.Exists(Path.Combine(root, "MoveMoatTest")) ||
        File.Exists(Path.Combine(sourceDir, "MoveMoatCompatibility.cs")))
        throw new Exception("A standalone MoveMoat bridge or project remains active.");

    var installMethods = new[]{
        "TryInstallBuildingCursorReachability",
        "TryInstallAttackApproachDiagnostics",
        "TryInstallMoatWorkTargetSelection",
        "InstallConnectivityAndRecovery"
    };
    foreach (string methodName in installMethods)
    {
        string source = trees.SelectMany(tree => tree.GetRoot().DescendantNodes()
                .OfType<MethodDeclarationSyntax>())
            .Single(method => method.Identifier.Text == methodName).ToFullString();
        int commit = source.IndexOf(".Commit()", StringComparison.Ordinal);
        int original = source.IndexOf(".Original", StringComparison.Ordinal);
        int rollback = source.IndexOf("pendingTransaction?.Dispose()", StringComparison.Ordinal);
        if (commit < 0 || original < commit || rollback < commit)
            throw new Exception("Atomic RedBird commit/original/rollback order is invalid in " + methodName);
    }
    string constructor = trees.SelectMany(tree => tree.GetRoot().DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>())
        .Single(item => item.Identifier.Text == "FriendlyMoatMovementRuntime").ToFullString();
    int constructorCommit = constructor.IndexOf(".Commit()", StringComparison.Ordinal);
    int constructorOriginal = constructor.IndexOf(".Original", StringComparison.Ordinal);
    int constructorRollback = constructor.IndexOf("pendingTransaction?.Dispose()", StringComparison.Ordinal);
    if (constructorCommit < 0 || constructorOriginal < constructorCommit ||
        constructorRollback < constructorCommit ||
        !constructor.Contains("DisposeConnectivityHooks()", StringComparison.Ordinal) ||
        !constructor.Contains("DisposeMoatWorkTargetSelection()", StringComparison.Ordinal))
        throw new Exception("Central RedBird constructor rollback is incomplete.");
    Console.WriteLine("PASS: integrated ownership, RedBird references, selection, atomic commits and rollbacks.");
}

void ValidateRuntimeSources()
{
    string framework=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        "Reference Assemblies", "Microsoft", "Framework", ".NETFramework", "v4.8.1");
    string game=@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
    string extender=Path.Combine(game,"BepInEx","plugins","000shcdese");
    if(!File.Exists(Path.Combine(extender,"SHCDESE.dll")))
        throw new Exception("Installed Script Extender 2.2.0 test references are required.");
    string productVersion=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(extender,"SHCDESE.dll")).ProductVersion;
    string referenceVersion=productVersion?.Split('+')[0];
    if(referenceVersion!="2.2.0")
        throw new Exception("Installed reference is not Script Extender 2.2.0: "+productVersion);
    var paths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    void Include(string path)
    {
        try { AssemblyName.GetAssemblyName(path); paths[Path.GetFileName(path)]=path; }
        catch(BadImageFormatException) { /* Framework also ships native COM helper DLLs. */ }
    }
    foreach(string path in Directory.GetFiles(framework,"*.dll"))Include(path);
    foreach(string path in Directory.GetFiles(Path.Combine(framework,"Facades"),"*.dll"))Include(path);
    foreach(string path in Directory.GetFiles(Path.Combine(game,"BepInEx","core"),"*.dll"))Include(path);
    foreach(string file in new[]{"SHCDESE.dll","R3.dll","System.Memory.dll","RedBird.Abstractions.dll","RedBird.Core.dll","RedBird.X64.dll","Iced.dll",
        "Microsoft.Extensions.Logging.Abstractions.dll","System.Threading.Tasks.Extensions.dll","System.Runtime.CompilerServices.Unsafe.dll","MessagePack.dll","MessagePack.Annotations.dll"})
        Include(Path.Combine(extender,file));
    foreach(string file in new[]{"UnityEngine.dll","UnityEngine.CoreModule.dll","UnityEngine.InputLegacyModule.dll","Assembly-CSharp.dll","Noesis.NoesisGUI.dll","com.rlabrecque.steamworks.net.dll"})
        Include(Path.Combine(game,"Stronghold Crusader Definitive Edition_Data","Managed",file));
    var settingsStub = CSharpSyntaxTree.ParseText(
        "namespace BugfixesAndQoL { internal sealed class BugfixesAndQoLViewModel { " +
        "public bool EnableMod { get; set; } = true; public bool EnableImprovedMoatFilling { get; set; } = true; " +
        "public bool EnableMoveFormationEnhancements { get; set; } = true; " +
        "public int MoveFormationSpacing { get; set; } = MoveFormationSpacingPolicy.Default; " +
        "public int FriendlyMoatMovementMode { get; set; } = FriendlyMoatMovementPolicy.DefaultMode; " +
        "internal FriendlyMoatMovementMode GetFriendlyMoatMovementMode() => " +
        "(FriendlyMoatMovementMode)FriendlyMoatMovementPolicy.Normalize(FriendlyMoatMovementMode); } }");
    var sources=trees.Concat(new[]{settingsStub}).Concat(new[]{"DebugLogHelper.cs","NativePatternResolver.cs","SerpLocalization.cs","PresetLobbyModSettingsViewModel.cs","ModSettingsSearch.cs","ToolTipPresentation.cs","GameModeHelper.cs"}.Select(file=>
        CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"Shared",file)),path:file))).ToArray();
    var check=CSharpCompilation.Create("FriendlyMoatMovementSourceContract220",sources,
        paths.Values.Select(p=>MetadataReference.CreateFromFile(p)),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
    var diagnostics=check.GetDiagnostics();
    foreach(var group in diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Warning).GroupBy(d=>d.Id))
        Console.WriteLine($"SOURCE WARNING {group.Key}: {group.Count()} occurrences; {group.First()}");
    var failures=diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
    if(failures.Length>0)throw new Exception(string.Join("\n",failures.Select(d=>d.ToString())));
    foreach (string file in new[]{"SHCDESE.dll","R3.dll","System.Memory.dll","RedBird.Abstractions.dll","RedBird.Core.dll","RedBird.X64.dll","Iced.dll",
        "Microsoft.Extensions.Logging.Abstractions.dll","System.Threading.Tasks.Extensions.dll","System.Runtime.CompilerServices.Unsafe.dll","MessagePack.dll","MessagePack.Annotations.dll"})
        Include(Path.Combine(game,"BepInEx","plugins","000shcdese",file));
    string installedVersion=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(game,"BepInEx","plugins","000shcdese","SHCDESE.dll")).ProductVersion;
    string installedRelease=installedVersion?.Split('+')[0];
    if(installedRelease!="2.2.0") throw new Exception("Installed extender is not Script Extender 2.2.0: "+installedVersion);
    var installed=CSharpCompilation.Create("FriendlyMoatMovementInstalledContract220",sources,
        paths.Values.Select(p=>MetadataReference.CreateFromFile(p)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
    var installedFailures=installed.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
    if(installedFailures.Length>0)throw new Exception(string.Join("\n",installedFailures.Select(d=>d.ToString())));
    Console.WriteLine($"PASS: installed Script Extender {installedRelease} assembly API surface matches all runtime sources.");
    Console.WriteLine($"PASS: complete runtime semantic source check against Script Extender {referenceVersion}; no mod assembly emitted.");
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

void ValidateModeSettings()
{
    string policy = File.ReadAllText(Path.Combine(sourceDir, "FriendlyMoatMovementPolicy.cs"));
    string snapshot = File.ReadAllText(Path.Combine(sourceDir, "MovementOptionsSnapshot.cs"));
    string moatWork = File.ReadAllText(Path.Combine(sourceDir, "MoatWorkTargetSelection.cs"));
    string viewModel = File.ReadAllText(Path.Combine(sourceDir, "BugfixesAndQoLViewModel.cs"));
    string xaml = File.ReadAllText(Path.Combine(root, "BugfixesAndQoL", "Override",
        "ScriptExtenderUI", "BugfixesAndQoLSettings.xaml"));
    if (!policy.Contains("Disabled = 0", StringComparison.Ordinal) ||
        !policy.Contains("Exact = 1", StringComparison.Ordinal) ||
        !policy.Contains("RequiredOnly = 2", StringComparison.Ordinal) ||
        !policy.Contains("DefaultMode = (int)FriendlyMoatMovementMode.RequiredOnly", StringComparison.Ordinal) ||
        !policy.Contains(": (int)FriendlyMoatMovementMode.Disabled", StringComparison.Ordinal))
        throw new Exception("The three-mode default and fail-closed normalization contract is incomplete.");
    if (!snapshot.Contains("settings.EnableMod && mode != FriendlyMoatMovementMode.Disabled", StringComparison.Ordinal) ||
        !snapshot.Contains("mode == FriendlyMoatMovementMode.RequiredOnly", StringComparison.Ordinal))
        throw new Exception("Off does not fully gate friendly movement or mode mapping is incorrect.");
    if (!moatWork.Contains("relationshipMode == 1 && !friendlyMovementEnabled", StringComparison.Ordinal) ||
        !moatWork.Contains("!ExtensionsEnabled ||", StringComparison.Ordinal) ||
        !moatWork.Contains("if (!ExtensionsEnabled)", StringComparison.Ordinal))
        throw new Exception("Off does not gate friendly moat traversal in the Dig/Fill work path.");
    if (!viewModel.Contains("[SyncHostOnly]\n        public int FriendlyMoatMovementMode", StringComparison.Ordinal) &&
        !viewModel.Contains("[SyncHostOnly]\r\n        public int FriendlyMoatMovementMode", StringComparison.Ordinal))
        throw new Exception("FriendlyMoatMovementMode is not a synchronized host setting.");
    if (!viewModel.Contains("FriendlyMoatMovementMode = FriendlyMoatMovementPolicy.DefaultMode", StringComparison.Ordinal) ||
        !viewModel.Contains("public int FriendlyMoatMovementSliderValue", StringComparison.Ordinal) ||
        !xaml.Contains("Value=\"{Binding FriendlyMoatMovementSliderValue, Mode=TwoWay}\"", StringComparison.Ordinal) ||
        !xaml.Contains("Text=\"{Binding FriendlyMoatMovementModeValueText}\"", StringComparison.Ordinal) ||
        xaml.Contains("FriendlyMoatMovementModeOptions", StringComparison.Ordinal) ||
        xaml.Contains("FriendlyMoatMovementModeIndex", StringComparison.Ordinal))
        throw new Exception("Friendly moat default/reset or XAML binding is incomplete.");
    foreach (string locale in Directory.GetFiles(Path.Combine(root, "BugfixesAndQoL", "Locales"), "*.txt"))
    {
        string localeText = File.ReadAllText(locale);
        if (!localeText.Contains("BugfixesAndQoL.FriendlyMoatMovementRequiredOnly=", StringComparison.Ordinal) ||
            !localeText.Contains("BugfixesAndQoL.FriendlyMoatMovementExact=", StringComparison.Ordinal) ||
            (!localeText.Contains("noticeable lag when commanding large groups", StringComparison.Ordinal) &&
             !localeText.Contains("beim Kommandieren großer Gruppen spürbare Lags", StringComparison.Ordinal)))
            throw new Exception("Missing friendly moat locale keys: " + locale);
    }
    Console.WriteLine("PASS: Off/Exact/Required-only mapping, fail-closed invalid values, default/reset, host classification and XAML/locales.");
}
