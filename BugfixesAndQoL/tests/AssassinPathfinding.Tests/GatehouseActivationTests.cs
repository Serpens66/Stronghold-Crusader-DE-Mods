using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static partial class Program
{
    private static void TestGatehouseActivation(string[] args)
    {
        var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(args[0],
            "BugfixesAndQoL/src/BugfixesAndQoLRuntime.cs")));
        string method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.Text == "ApplyAssassinGatehouseClimbFix").ToFullString();
        string fixture = """
using System;
namespace APIShared { static class AssassinPathAPI {
 public static bool Active; public static int Calls; public static string Owner;
 public static void SetDirectGatehouseClimbing(string owner,bool enabled){Calls++;Owner=owner;Active=enabled;}
} }
static class BugfixesAndQoLPlugin { public const string PluginGuid="BugfixesAndQoL_Serp"; }
sealed class Settings { public bool EnableMod,EnableAssassinGatehouseClimbFix; }
public sealed class ActivationFixture {
 private bool nativeLibraryAvailable;
 private Settings settings=new();
 METHOD
 public static void Run() {
 var r=new ActivationFixture();
 r.settings.EnableMod=r.settings.EnableAssassinGatehouseClimbFix=true;
 r.ApplyAssassinGatehouseClimbFix();
 if(APIShared.AssassinPathAPI.Calls!=0)throw new Exception("pre-native setting touched hook owner");
 r.nativeLibraryAvailable=true;
 foreach(bool mod in new[]{false,true})foreach(bool fix in new[]{false,true}) {
 r.settings.EnableMod=mod;r.settings.EnableAssassinGatehouseClimbFix=fix;
 r.ApplyAssassinGatehouseClimbFix();
 if(APIShared.AssassinPathAPI.Active!=(mod&&fix)||APIShared.AssassinPathAPI.Owner!=BugfixesAndQoLPlugin.PluginGuid)
  throw new Exception("activation/ownership mismatch");
 }
 r.settings.EnableAssassinGatehouseClimbFix=false;r.ApplyAssassinGatehouseClimbFix();
 r.settings.EnableAssassinGatehouseClimbFix=true;r.ApplyAssassinGatehouseClimbFix();
 if(!APIShared.AssassinPathAPI.Active)throw new Exception("reactivation failed");
 }
}
""";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
            .Split(Path.PathSeparator).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ActualGatehouseActivation",
            new[] { CSharpSyntaxTree.ParseText(fixture.Replace("METHOD", method)) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Check(emitted.Success, "production gatehouse activation compiles: " +
            string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(stream.ToArray()).GetType("ActivationFixture").GetMethod("Run").Invoke(null, null); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
        Check(true, "actual gatehouse setting respects native readiness, owner, both switches and reactivation");
    }
}
