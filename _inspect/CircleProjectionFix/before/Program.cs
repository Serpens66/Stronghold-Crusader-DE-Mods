using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

string root=Path.GetFullPath(args.Length==0?".":args[0]);
SplitContracts.Validate(root);
string path=Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Runtime/ManualUnitCommands.cs");
var syntax=CSharpSyntaxTree.ParseText(File.ReadAllText(path));
var names=new HashSet<string> {"nativeManualProbe","manualCommandContexts","ManualCommandContext",
    "NativeProbeManagerBytes","NativeProbeGridBytes","nativeProbeGrid","nativeProbeRectangle",
    "nativeProbeManagerBackup","nativeProbeGridBackup","nativeProbeRectangleBackup","nativeProbeMemoryHelperBackup",
    "PushManualCommandContext","RestoreManualCommandContext","ProbeNativeManualPath",
    "PrepareNativeManualGroup","IsNativeManualGroupFlood","CaptureTargetCommandContext",
    "moveEventObservers","moveEventDepths","targetCommandParents","targetEventObservers",
    "DispatchMoveEvent","DispatchTargetEvent","InvalidateFastMoatData","LogAndResetFastMoatMetrics","ClearDeferredFastMoveScope"};
var permanent=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Native/PermanentCommandHooks.cs")));
var traversal=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"BugfixesAndQoL/src/UnitCommands/Movement/TraversalDispatch.cs")));
var members=new[]{syntax,permanent,traversal}.SelectMany(t=>t.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Single(c=>c.Identifier.Text=="UnitCommandPathRuntime").Members).Where(m=>m switch {
        MethodDeclarationSyntax method=>names.Contains(method.Identifier.Text),
        ClassDeclarationSyntax type=>names.Contains(type.Identifier.Text),
        FieldDeclarationSyntax field=>field.Declaration.Variables.Any(v=>names.Contains(v.Identifier.Text)),
        _=>false}).ToArray();
// Exercise the actual native fallback dispatch before the unrelated addon
// qualification branches. These original-first prefixes are the command seam.
var runtimeSyntax=CSharpSyntaxTree.ParseText(FeatureSourceModel.Read(root, "UnitCommandPathRuntime"));
var fallbackPrefixes=new List<MethodDeclarationSyntax>();
foreach(var (name,count,guard) in new[] {
    ("SelectOwnerSafeGroupMoatMode",4,"NativeCommonFallback"),
    ("AllowBuilderAfterFailedRegionSearch",2,"IsNativeManualGroupFlood") })
{
    var method=runtimeSyntax.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
        .Single(m=>m.Identifier.Text==name);
    if(!method.Body!.Statements[count-1].ToString().Contains(guard))
        throw new Exception("Native fallback dispatch order changed: "+name);
    fallbackPrefixes.Add(method.WithBody(method.Body.WithStatements(SyntaxFactory.List(
        method.Body.Statements.Take(count).Append(SyntaxFactory.ParseStatement("return vanillaResult;"))))));
}
string source="using System;using System.Collections.Generic;using System.Runtime.InteropServices;namespace CommandFixture { public unsafe partial class UnitCommandPathRuntime {"+
    string.Join("\n",members.Select(m=>m.ToFullString()).Concat(fallbackPrefixes.Select(m=>m.ToFullString()))).Replace("SHCDESE.EventAPI.Tribes.","")
        .Replace("SHCDESE.EventAPI.","")+"}}";
var trees=new[]{ CSharpSyntaxTree.ParseText(source), CSharpSyntaxTree.ParseText(File.ReadAllText(
    Path.Combine(root,"BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/ManualHarness.cs"))) };
var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator)
    .Select(p=>MetadataReference.CreateFromFile(p));
var compilation=CSharpCompilation.Create("ActualManualCommandFixture",trees,references,
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
using var stream=new MemoryStream();var emitted=compilation.Emit(stream);
if(!emitted.Success)throw new Exception(string.Join("\n",emitted.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
try { Assembly.Load(stream.ToArray()).GetType("CommandFixture.UnitCommandPathRuntime")!.GetMethod("Run")!.Invoke(null,null); }
catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException??e).Throw();}
MoatModeFlagContract.Validate();
NativeCommandDetourContract.Validate(root);
CursorPermissionNativeTests.Validate(root);
MovementLifeGeneratorTests.Validate(root);
RallyTerrainGeneratorTests.Validate(root);
CadenceSnapshotTests.Validate(root);
FormationStartupTests.Validate(root);
FormationMoveRuntimeTests.Validate(root);
FormationMarkerFallbackTests.Validate(root);
FormationMovementEligibilityTests.Validate(root);
Console.WriteLine("PASS: main command fixes use native probes and group fallback; actual installed RedBird contracts verified.");
