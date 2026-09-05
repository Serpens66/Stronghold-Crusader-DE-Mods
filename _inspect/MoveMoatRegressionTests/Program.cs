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
ValidateSelectionMetadata();
ValidateRuntimeSources();

var methods = new HashSet<string>(new[] {
    "ObserveCursorTilePairFallbackSelection", "TryProbeUnitApproachCursorRoute", "TryResolveHostileLivingUnitFromRawCursor", "TryGetHostileLivingUnitAtTile", "TryGetSelectedVanillaDigger", "AllowAttackCursorTilePairThroughCompletedMoat", "TryQualifySelectedGroupCursorRoute", "CreateCursorScopeForSnapshot", "TryQualifyCursorScope", "TryProbeDirectCursorRoute", "TryCaptureSelectedGroup", "CursorStartMatchesBoundSelection", "CursorScopeMatchesTargetTile", "EmitRecoveryAdapter", "SelectMoatWorkTarget", "AllowFillMoatApproachThroughFriendlyMoat", "TryGetMoatRecord", "TryReadMoatRecord", "TryFindBestFillMoatApproach", "IsOccupiedByOtherLivingUnit", "RestoreFailedRecovery", "ObserveNativeModeEntry", "TryRecoverBeforeBuilder", "RejectPreBuilder", "ValidateRecoveryEdges", "IsValidMoatRecordId", "PrepareMovementSearch", "TryDeferToNativeGroundPlan", "TryBuildTerminalFillRoute", "IsTerminalFillEdgeValid", "TryAllowUnitMoveRegion", "AllowBuilderAfterFailedRegionSearch", "CallVanillaBuilder", "TryReplaceUnsafeFallbackPath", "BuildReconstructedUnitPath", "TryPublishSafelyFasterWeightedRoute",
    "ObserveUnitMoveOrder", "GetCurrentUnitMoveFrame", "AbandonUnitMoveFrame", "ClearUnitMoveFrames",
    "GetUnitMovePlan", "CopyMovementPlan", "GetNativeMovementStart", "TryAuditFallbackPath", "IsCompletedEnemyMoatForPlayer",
    "DescribeFallbackContractFailure",
    "EnableCompletedMoatModeForScopedMovement", "GetBuilderPlan", "MatchesBuilderPlan",
    "TryCaptureUnitFallbackPathBuffer", "RestoreFallbackPathBuffer",
    "BuildPathWithCompletedMoatRouteVariant", "BuildPathWithCompletedMoatRouteVariantCore", "ValidatePendingFillApproach",
    "TryFindRequiredFriendlyCompletedMoatRouteForPlan", "TryGetCachedRequiredFriendlyRouteForPlan",
    "EnsureMoatWorkReachability", "TryGetMoatWorkRoute",
    "TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint",
    "EnsureReachabilityMap", "AdvanceReachabilityMap", "EnsureReachabilityStorage", "VisitNeighbour",
    "GetRouteVisitedMap", "GetRouteDistanceMap", "GetRouteDistance", "ObserveTraversedRegion",
    "GetCachedRouteSummaryForTarget", "GetCachedRouteSummaryForRegion"
});
var types = new HashSet<string>(new[] {
    "BuildingCursorTarget", "BuildingHoverTileSource", "AttackCursorPairScope", "CursorPairFallbackKind", "CursorGroupRouteSummary", "SelectedCursorUnitSnapshot", "UnitMoveFrame", "PlanScope", "RouteProbeSummary", "TargetedRouteDecision", "MoatWorkSelectionScope", "MoatWorkApproach", "PendingFillMoatApproach"
});
var constants = new HashSet<string>(new[] {
    "MoatRecordArrayOffset", "MoatRecordCountOffset", "MoatRecordSize", "MoatRecordTileIdOffset", "MoatRecordXOffset", "MoatRecordYOffset", "NativeUnitSlotDataOffset", "MaximumMoatRecordId", "MaximumRegionId", "MaximumUnitCount", "MapWidth", "MapCellCount", "NativeTileCount",
    "RouteStateShift", "RouteCellMask", "GroundRouteState", "FriendlyMoatRouteState", "EnemyMoatRouteState",
    "MovementBlockedLowTileFlagMask", "CompletedMoatTileFlag", "CursorSpecialStructureTileFlagMask", "PathManagerOutputBufferOffset",
    "PathManagerOutputLengthOffset", "NativeUnitPathBufferOffset", "NativeUnitPathBufferStride",
    "PathManagerRouteVariantOffset", "OrdinaryWalkableTileFlag", "MoatWorkNeighbourX", "MoatWorkNeighbourY"
    , "WeightedPublicationSafetyMarginTicks"
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
string extracted = "using Iced.Intel; using static Iced.Intel.AssemblerRegisters; using System; using System.Collections.Generic; using System.Diagnostics; " +
    "using System.Runtime.InteropServices; namespace MoveMoatTest { " +
    "internal sealed unsafe partial class MoveMoatPathTest {\n" +
    string.Join("\n", selected.Select(m => m.ToFullString())) + "\n} }";
var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
    .Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p)).Concat(new[] {
        MetadataReference.CreateFromFile(Path.Combine(root,"shcde-script-extender","src","SHCDESE.BepInEx","bin","net481","Iced.dll")) });
Assembly.LoadFrom(Path.Combine(root,"shcde-script-extender","src","SHCDESE.BepInEx","bin","net481","Iced.dll"));
var compilation = CSharpCompilation.Create("Assembly-CSharp", new[] {
    CSharpSyntaxTree.ParseText(extracted),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "WeightedMoatRoutePlanner.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "MoatSearchKernel.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "CursorRegionGraph.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, "CursorConnectivity.cs")).Replace("using SHCDESE.API;", "").Replace("using SHCDESE.Interop;", "").Replace("using SHCDESE.Interop.Enums;", "")),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "_inspect", "MoveMoatRegressionTests", "CursorTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "_inspect", "MoveMoatRegressionTests", "SearchKernelTests.cs"))),
    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "_inspect", "MoveMoatRegressionTests", "RuntimeHarness.cs")))
}, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, optimizationLevel: OptimizationLevel.Release));
using var output = new MemoryStream();
var emitted = compilation.Emit(output);
if (!emitted.Success)
    throw new Exception(string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
var assembly = Assembly.Load(output.ToArray());
try
{
    assembly.GetType("MoveMoatTest.MoveMoatPathTest").GetMethod("RunTests").Invoke(null, null);
    assembly.GetType("MoveMoatTest.SearchKernelTests").GetMethod("Run").Invoke(null, null);
    assembly.GetType("MoveMoatTest.CursorGraphTests").GetMethod("Run").Invoke(null, null);
    assembly.GetType("MoveMoatTest.MoveMoatPathTest").GetMethod("RunMachineContract").Invoke(null,new object[]{root});
}
catch (TargetInvocationException ex) { throw ex.InnerException ?? ex; }
Console.WriteLine($"PASS: syntax of {trees.Length} runtime files; {selected.Count} actual runtime members compiled and exercised.");

void ValidateRuntimeSources()
{
    string framework=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        "Reference Assemblies", "Microsoft", "Framework", ".NETFramework", "v4.8.1");
    string game=@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
    string extender=Path.Combine(root,"shcde-script-extender","src","SHCDESE.BepInEx","bin","net481");
    if(!File.Exists(Path.Combine(extender,"SHCDESE.dll")))
        throw new Exception("Verified local 1.42.0 test references are required; no installed-version fallback.");
    string productVersion=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(extender,"SHCDESE.dll")).ProductVersion;
    if(productVersion==null||!productVersion.EndsWith("+171d68e155a8f98c5f8c4ee154d9af154c9a2443",StringComparison.Ordinal))
        throw new Exception("Local reference is not built from the verified v1.42.0 commit.");
    var paths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
    void Include(string path)
    {
        try { AssemblyName.GetAssemblyName(path); paths[Path.GetFileName(path)]=path; }
        catch(BadImageFormatException) { /* Framework also ships native COM helper DLLs. */ }
    }
    foreach(string path in Directory.GetFiles(framework,"*.dll"))Include(path);
    foreach(string path in Directory.GetFiles(Path.Combine(framework,"Facades"),"*.dll"))Include(path);
    foreach(string path in Directory.GetFiles(Path.Combine(game,"BepInEx","core"),"*.dll"))Include(path);
    foreach(string file in new[]{"SHCDESE.dll","R3.dll","System.Memory.dll","Zhuqiaomon.dll","Iced.dll",
        "Microsoft.Extensions.Logging.Abstractions.dll","System.Threading.Tasks.Extensions.dll","System.Runtime.CompilerServices.Unsafe.dll"})
        Include(Path.Combine(extender,file));
    foreach(string file in new[]{"UnityEngine.dll","UnityEngine.CoreModule.dll"})
        Include(Path.Combine(game,"Stronghold Crusader Definitive Edition_Data","Managed",file));
    var sources=trees.Concat(new[]{"DebugLogHelper.cs","NativePatternResolver.cs"}.Select(file=>
        CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"Shared",file)),path:file)));
    var check=CSharpCompilation.Create("MoveMoatSourceContract142",sources,
        paths.Values.Select(p=>MetadataReference.CreateFromFile(p)),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
    var diagnostics=check.GetDiagnostics();
    foreach(var group in diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Warning).GroupBy(d=>d.Id))
        Console.WriteLine($"SOURCE WARNING {group.Key}: {group.Count()} occurrences; {group.First()}");
    var failures=diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
    if(failures.Length>0)throw new Exception(string.Join("\n",failures.Select(d=>d.ToString())));
    foreach (string file in new[]{"SHCDESE.dll","R3.dll","System.Memory.dll","Zhuqiaomon.dll","Iced.dll",
        "Microsoft.Extensions.Logging.Abstractions.dll","System.Threading.Tasks.Extensions.dll","System.Runtime.CompilerServices.Unsafe.dll"})
        Include(Path.Combine(game,"BepInEx","plugins","000shcdese",file));
    string installedVersion=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(game,"BepInEx","plugins","000shcdese","SHCDESE.dll")).ProductVersion;
    if(installedVersion?.Split('+')[0]!="1.42.0") throw new Exception("Installed extender is not release 1.42.0: "+installedVersion);
    var installed=CSharpCompilation.Create("MoveMoatInstalledContract142",sources,
        paths.Values.Select(p=>MetadataReference.CreateFromFile(p)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
    var installedFailures=installed.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
    if(installedFailures.Length>0)throw new Exception(string.Join("\n",installedFailures.Select(d=>d.ToString())));
    Console.WriteLine("PASS: installed 1.42.0 assembly API surface also matches all runtime sources.");
    Console.WriteLine("PASS: complete runtime semantic source check against verified local Script Extender 1.42.0; no mod assembly emitted.");
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
