using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

string root=Path.GetFullPath(args.Length==0?".":args[0]);
SplitContracts.Validate(root);
string path=Path.Combine(root,"APIShared/src/UnitCommands/ManualUnitCommands.cs");
var syntax=CSharpSyntaxTree.ParseText(File.ReadAllText(path));
var names=new HashSet<string> {"nativeManualProbe","manualCommandContexts","ManualCommandContext",
    "NativeProbeManagerBytes","NativeProbeGridBytes","nativeProbeGrid","nativeProbeRectangle",
    "nativeProbeManagerBackup","nativeProbeGridBackup","nativeProbeRectangleBackup","nativeProbeMemoryHelperBackup",
    "PushManualCommandContext","RestoreManualCommandContext","ProbeNativeManualPath",
    "PrepareNativeManualGroup","IsNativeManualGroupFlood","CaptureTargetCommandContext",
    "moveFormationParents","moveEventObservers","moveEventDepths","targetCommandParents","targetEventObservers",
    "DispatchMoveEvent","DispatchTargetEvent","InvalidateFastMoatData","LogAndResetFastMoatMetrics","ClearDeferredFastMoveScope"};
var permanent=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"APIShared/src/UnitCommands/PermanentCommandHooks.cs")));
var traversal=CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,"APIShared/src/UnitCommands/TraversalDispatch.cs")));
var members=new[]{syntax,permanent,traversal}.SelectMany(t=>t.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
    .Single(c=>c.Identifier.Text=="UnitCommandPathRuntime").Members).Where(m=>m switch {
        MethodDeclarationSyntax method=>names.Contains(method.Identifier.Text),
        ClassDeclarationSyntax type=>names.Contains(type.Identifier.Text),
        FieldDeclarationSyntax field=>field.Declaration.Variables.Any(v=>names.Contains(v.Identifier.Text)),
        _=>false}).ToArray();
string source="using System;using System.Collections.Generic;using System.Runtime.InteropServices;namespace CommandFixture { public unsafe partial class UnitCommandPathRuntime {"+
    string.Join("\n",members.Select(m=>m.ToFullString())).Replace("SHCDESE.EventAPI.Tribes.","")
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
Console.WriteLine("PASS: main command fixes use native probes and group fallback; actual installed RedBird contracts verified.");
