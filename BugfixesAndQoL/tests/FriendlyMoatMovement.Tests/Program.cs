using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

string root=Path.GetFullPath(args.Length==0?".":args[0]);
SplitContracts.Validate(root);
GateCompletionTests.Validate(root);
string path=Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Runtime/ManualUnitCommands.cs");
foreach (bool diagnostic in new[] { false, true })
{
var options = new CSharpParseOptions(preprocessorSymbols: diagnostic ? new[] { "SERP_DEVDIAG" } : Array.Empty<string>());
var syntax=CSharpSyntaxTree.ParseText(File.ReadAllText(path), options);
var names=new HashSet<string> {"nativeManualProbe","manualCommandContexts","ManualCommandContext",
    "nativeCursorAnswers","nativeCursorAnswerTick","NativeCursorProbeKey","ProbeNativeCursorPath","FreezeProbeContext","TryQualifyNativeSelection",
    "NativeProbeManagerBytes","NativeProbeGridBytes","nativeProbeGrid","nativeProbeRectangle",
    "nativeProbeManagerBackup","nativeProbeGridBackup","nativeProbeRectangleBackup","nativeProbeMemoryHelperBackup",
    "PushManualCommandContext","RestoreManualCommandContext","ProbeNativeManualPath",
    "nativeProbeBatchDepth","nativeProbeBatchSnapshot","BeginNativeProbeBatch","EndNativeProbeBatch",
    "PrepareNativeManualGroup","IsNativeManualGroupFlood","CaptureTargetCommandContext",
    "moveEventObservers","moveEventDepths","targetCommandParents","targetEventObservers",
    "DispatchMoveEvent","DispatchTargetEvent","InvalidateFastMoatData","LogAndResetFastMoatMetrics","ClearDeferredFastMoveScope"};
var permanent=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Native/PermanentCommandHooks.cs")));
var traversal=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Movement/TraversalDispatch.cs")));
var adapter=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Runtime/GateAttackFreezeRuntime.DevDiag.cs")), options);
var proof=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Cursor/CursorRegionRejectProof.cs")), options, "CursorRegionRejectProof.cs");
var proofDiag=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Cursor/CursorRegionRejectProof.DevDiag.cs")), options, "CursorRegionRejectProof.DevDiag.cs");
var ground=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Cursor/CursorGroundRejectRuntime.cs")), options, "CursorGroundRejectRuntime.cs");
var region=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Attack/UnitCommandPathRuntime.RegionFallback.cs")), options);
var recovery=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Runtime/ManualGroupAdmission.cs")), options);
var admissionNames = new HashSet<string> { "BeginManualGroupAdmission", "EndManualGroupAdmission", "IsMixedManualGroup", "QueryManualGroupRegion",
    "AddManualAdmissionMember", "QueryGroupRegionPreservingState", "QueryPreviewGroupRegion", "CapturePreviewFormationAdmission",
    "FilterFormationMembers", "ApplyDefaultGroupDestination", "PrepareDefaultGroupDestinations", "TryRejectPreparedFormationWithoutSlots", "RejectPreparedMove", "manualAdmissionReady", "manualAdmissionPool", "manualAdmission", "manualAdmissionGeneration", "AdmitFormationMembers", "manualPlayerMove", "ManualPlayerMoveFrame" };
names.Add("moveAdmissionParents");
names.Add("queueDefersMove");
var unitMovement=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Movement/UnitMovementContext.cs")), options);
names.Add("ObserveScopedRegionPairReachability");
var members=(diagnostic ? new[]{syntax,permanent,traversal,adapter,proof,proofDiag,region,ground,recovery} : new[]{syntax,permanent,traversal,proof,region,ground,recovery}).SelectMany(t=>t.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Single(c=>c.Identifier.Text=="UnitCommandPathRuntime").Members).Where(m=>m switch {
        _ when m.SyntaxTree==proof || m.SyntaxTree==proofDiag || m.SyntaxTree==ground=>true,
        MethodDeclarationSyntax method when m.SyntaxTree==recovery=>admissionNames.Contains(method.Identifier.Text),
        FieldDeclarationSyntax field when m.SyntaxTree==recovery=>field.Declaration.Variables.Any(v=>admissionNames.Contains(v.Identifier.Text)),
        StructDeclarationSyntax type when m.SyntaxTree==recovery=>admissionNames.Contains(type.Identifier.Text),
        MethodDeclarationSyntax method=>names.Contains(method.Identifier.Text),
        ClassDeclarationSyntax type=>names.Contains(type.Identifier.Text),
        StructDeclarationSyntax type=>names.Contains(type.Identifier.Text),
        FieldDeclarationSyntax field=>field.Declaration.Variables.Any(v=>names.Contains(v.Identifier.Text)),
        _=>false}).ToArray();
var selectionSyntax=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Cursor/UnitCommandPathRuntime.Selection.cs")));
members=members.Concat(selectionSyntax.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"TryQualifySameRegionSelection","TryGetOwnCursorUnit","TryBindFriendlyGroundCursor"}.Contains(m.Identifier.Text))).ToArray();
members = members.Append(unitMovement.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="GetNativeMovementStart")).ToArray();
// Exercise the actual native fallback dispatch before the unrelated addon
// qualification branches. These original-first prefixes are the command seam.
var runtimeSyntax=CSharpSyntaxTree.ParseText(FeatureSourceModel.Read(root, "UnitCommandPathRuntime"));
var fallbackPrefixes=new List<MethodDeclarationSyntax>();
foreach(var (name,count,guard) in new[] {
    ("SelectOwnerSafeGroupMoatMode",5,"NativeCommonFallback"),
    ("AllowBuilderAfterFailedRegionSearch",3,"IsNativeManualGroupFlood") })
{
    var method=runtimeSyntax.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
        .Single(m=>m.Identifier.Text==name);
    if(!method.Body!.Statements[count-1].ToString().Contains(guard))
        throw new Exception("Native fallback dispatch order changed: "+name);
    fallbackPrefixes.Add(method.WithBody(method.Body.WithStatements(SyntaxFactory.List(
        method.Body.Statements.Take(count).Append(SyntaxFactory.ParseStatement("return vanillaResult;"))))));
}
string source="using BugfixesAndQoL;using BugfixesAndQoL.UnitCommands;using APIShared;using SHCDESE.Interop.Enums;using System;using System.Collections.Generic;using System.Runtime.InteropServices;namespace CommandFixture { public unsafe partial class UnitCommandPathRuntime {"+
    string.Join("\n",members.Select(m=>m.ToFullString()).Concat(fallbackPrefixes.Select(m=>m.ToFullString()))).Replace("SHCDESE.EventAPI.Units.","").Replace("SHCDESE.EventAPI.Tribes.","")
        .Replace("SHCDESE.EventAPI.","")+"}}";
// DEVDIAG measurement calls compile to nothing in production; the fixture only needs the API shape.
const string devDiagStub="namespace Shared { internal static class DebugLogHelper {internal static void LogWarningLimited(object l,string k,string t){} } internal static class DevDiag { internal static void StopSpike(ref long t,string n,int threshold,System.Func<string> detail){} internal static void Log(System.Func<string> text){} internal static void Start(ref long t){} internal static void Stop(ref long t,string n){} internal static void Count(string n){} internal static void Count(string n,long a){} } }";
var trees=new List<SyntaxTree> { CSharpSyntaxTree.ParseText(source, options), CSharpSyntaxTree.ParseText(devDiagStub, options), CSharpSyntaxTree.ParseText(File.ReadAllText(
    Path.Combine(root,"BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/ManualHarness.cs")), options) };
var destinationSyntax=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/Formations/FormationRuntime.Destinations.cs")));
var slotNames=new HashSet<string>{"CaptureGroundSlots","TryEnqueueVanillaCandidate","RentVisited","ReturnVisited","NativeDestination","VanillaSearchNode","visitedScratch","vanillaQueueScratch"};
var slotMembers=destinationSyntax.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.Text=="FormationRuntime").Members.Where(m=>m switch {
    MethodDeclarationSyntax method=>slotNames.Contains(method.Identifier.Text),
    StructDeclarationSyntax type=>slotNames.Contains(type.Identifier.Text),
    FieldDeclarationSyntax field=>field.Declaration.Variables.Any(v=>slotNames.Contains(v.Identifier.Text)),_=>false});
var modelSyntax=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/Formations/FormationModel.cs")));
var slotModel=modelSyntax.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>new[]{"NormalizeDensity","IsNativeVanillaSlotCandidate"}.Contains(m.Identifier.Text));
trees.Add(CSharpSyntaxTree.ParseText("using System;using System.Collections.Generic;using BugfixesAndQoL.UnitCommands;namespace BugfixesAndQoL.UnitCommands {internal enum FormationRole { Neutral }} namespace CommandFixture {internal static class FormationModel {"+string.Join("\n",slotModel.Select(m=>m.ToFullString()))+"} internal unsafe sealed partial class FormationRuntime {private const int MapWidth=800,MaximumVanillaSelectorCandidates=4001,MaximumUnitCount=10000;"+string.Join("\n",slotMembers.Select(m=>m.ToFullString()))+"}}",options));
trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Cursor/CursorRegionRejectPool.cs")), options));
trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Cursor/CursorGroundRejectProof.cs")), options));
trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/ManualRejectHarness.cs")), options));
trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/ManualGroundHarness.cs")), options));
trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/ManualRecoveryHarness.cs")), options));
trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Runtime/ManualRegionAdmissionBatch.cs")), options));
if (diagnostic)
{
    trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Runtime/ManualProbeCapture.DevDiag.cs")), options));
    trees.Add(CSharpSyntaxTree.ParseText("namespace BugfixesAndQoL.UnitCommands {"+proofDiag.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c=>c.Identifier.Text=="CursorGroundRejectProof").ToFullString()+"}", options));
}
var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator)
    .Select(p=>MetadataReference.CreateFromFile(p));
var compilation=CSharpCompilation.Create("ActualManualCommandFixture",trees,references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
using var stream=new MemoryStream();var emitted=compilation.Emit(stream);
if(!emitted.Success)throw new Exception(string.Join("\n",emitted.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
try { Assembly.Load(stream.ToArray()).GetType("CommandFixture.UnitCommandPathRuntime")!.GetMethod("Run")!.Invoke(null,null); }
catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException??e).Throw();}
}
MoatModeFlagContract.Validate();
NativeCommandDetourContract.Validate(root);
ManualAdmissionMachineTests.Validate();
GroundFrameMachineTests.Validate(root);
CursorPermissionNativeTests.Validate(root);
MovementLifeGeneratorTests.Validate(root);
RallyTerrainGeneratorTests.Validate(root);
CadenceSnapshotTests.Validate(root);
FormationStartupTests.Validate(root);
FormationMoveRuntimeTests.Validate(root);
MoveMarkerRendererTests.Validate(root);
FormationMarkerFallbackTests.Validate(root);
CircleProjectionTests.Validate(root);
FormationMovementEligibilityTests.Validate(root);
FormationCommandPipelineTests.Validate(root);
FormationDispatchOutcomeTests.Validate(root);
Console.WriteLine("PASS: main command fixes use native probes and group fallback; actual installed RedBird contracts verified.");
