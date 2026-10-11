using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static partial class Program
{
    private static void TestCapturedGateSetting(string[] args)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(args[0],
            "BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs"))).GetRoot();
        var prop = root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Single(p => p.Identifier.Text == "EnableAssassinCapturedGateProtectionFix");
        Check(prop.AttributeLists.ToString().Contains("SyncHostOnly"), "captured-gate setting is host-only");
        var field = root.DescendantNodes().OfType<FieldDeclarationSyntax>()
            .Single(f => f.Declaration.Variables.Any(v => v.Identifier.Text == "enableAssassinCapturedGateProtectionFix"));
        var setter = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "SetSetting");
        var reset = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "ResetToDefault");
        Check(reset.ToFullString().Contains("EnableAssassinCapturedGateProtectionFix = true;"), "host reset includes captured-gate default");
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
 if(!vm.EnableAssassinCapturedGateProtectionFix)throw new Exception("default");
 vm.EnableAssassinCapturedGateProtectionFix=false;
 if(vm.EnableAssassinCapturedGateProtectionFix||vm.Changes!=1)throw new Exception("stored false/setter");
 vm.Host=false;vm.EnableAssassinCapturedGateProtectionFix=true;
 if(vm.EnableAssassinCapturedGateProtectionFix||vm.Changes!=1)throw new Exception("client rejection");
 vm.Host=true;vm.EnableAssassinCapturedGateProtectionFix=true;
 if(!vm.EnableAssassinCapturedGateProtectionFix||vm.Changes!=2)throw new Exception("reset/reactivation");
 }
}
""";
        var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Select(p=>MetadataReference.CreateFromFile(p));
        var compilation=CSharpCompilation.Create("ActualCapturedGateSetting",
            new[]{CSharpSyntaxTree.ParseText(fixture.Replace("FIELD",field.ToFullString()).Replace("PROPERTY",prop.ToFullString()).Replace("SETTER",setter.ToFullString()))},
            references,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream=new MemoryStream();var result=compilation.Emit(stream);
        Check(result.Success,"actual captured-gate setter compiles: "+string.Join("\n",result.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)));
        try{Assembly.Load(stream.ToArray()).GetType("SettingFixture").GetMethod("Run").Invoke(null,null);}
        catch(TargetInvocationException ex){throw ex.InnerException;}
        Check(true,"production captured-gate setting default, false, client lock and reactivation");
    }
}
