using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static partial class Program
{
    private static void TestCapturedGateSetting(string[] args)
    {
        TestHostBoolSetting(args, "EnableAssassinCapturedGateProtectionFix", "enableAssassinCapturedGateProtectionFix", "captured-gate");
        // Weighted Assassin pathfinding for AI lords: separate host setting, enabled by default.
        TestHostBoolSetting(args, "EnableImprovedAssassinPathfindingAI", "enableImprovedAssassinPathfindingAI", "AI Assassin pathfinding");
    }

    private static void TestHostBoolSetting(string[] args, string propertyName, string fieldName, string label)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(args[0],
            "BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs"))).GetRoot();
        var prop = root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Single(p => p.Identifier.Text == propertyName);
        Check(prop.AttributeLists.ToString().Contains("SyncHostOnly"), label + " setting is host-only");
        var field = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Single(f => f.Declaration.Variables.Any(v => v.Identifier.Text == fieldName));
        var setter = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "SetSetting");
        var reset = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "ResetToDefault");
        Check(reset.ToFullString().Contains(propertyName + " = true;"), "host reset includes " + label + " default");
        string fixture = """
using System;
sealed class SyncHostOnlyAttribute:Attribute{}
public sealed class SettingFixture {
 public bool Host=true; public int Changes;
 public event Action<string> SettingChanged;
 bool CanMutateSetting(string name)=>Host;
 void OnPropertyChanged(string name){Changes++;}
 FIELD
 PROPERTY
 SETTER
 public static void Run(){
 var vm=new SettingFixture();
 if(!vm.NAME)throw new Exception("default");
 vm.NAME=false;
 if(vm.NAME||vm.Changes!=1)throw new Exception("stored false/setter");
 vm.Host=false;vm.NAME=true;
 if(vm.NAME||vm.Changes!=1)throw new Exception("client rejection");
 vm.Host=true;vm.NAME=true;
 if(!vm.NAME||vm.Changes!=2)throw new Exception("reset/reactivation");
 }
}
""";
        var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Select(p=>MetadataReference.CreateFromFile(p));
        var compilation=CSharpCompilation.Create("ActualHostBoolSetting",
            new[]{CSharpSyntaxTree.ParseText(fixture.Replace("NAME",propertyName).Replace("FIELD",field.ToFullString())
                .Replace("PROPERTY",prop.ToFullString()).Replace("SETTER",setter.ToFullString()))},
            references,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream=new MemoryStream();var result=compilation.Emit(stream);
        Check(result.Success,"actual " + label + " setter compiles: "+string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
        try{Assembly.Load(stream.ToArray()).GetType("SettingFixture").GetMethod("Run").Invoke(null,null);}
        catch(TargetInvocationException ex){throw ex.InnerException;}
        Check(true,"production " + label + " setting default, false, client lock and reactivation");
    }
}
