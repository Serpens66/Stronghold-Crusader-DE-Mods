using System.Xml.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Read-only semantic preflight. Never emits, builds or installs a runtime assembly.
string root = Directory.GetCurrentDirectory();
string game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
string framework = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1";
var projects = new[] { @"APIShared\APIShared.csproj", @"ExtremePowers\ExtremePowers.API.csproj" }
    .Concat(JsonSerializer.Deserialize<string[]>(File.ReadAllText(@"_inspect\MissionLifecycleProjects.json"))!)
    .Append(@"_inspect\APISharedTests\APISharedTests.csproj").Distinct().ToArray();
var compiled = new Dictionary<string, CSharpCompilation>(StringComparer.OrdinalIgnoreCase);
int errors = 0;
foreach (string relative in projects)
{
    string path = Path.Combine(root, relative), dir = Path.GetDirectoryName(path)!;
    var xml = XDocument.Load(path);
    var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
        ["GameDir"] = game, ["ExtenderDir"] = Path.Combine(game,@"BepInEx\plugins\000shcdese"),
        ["ApiSharedDir"] = Path.Combine(game,@"BepInEx\plugins\APIShared_Serp"), ["MSBuildThisFileDirectory"] = dir+"\\"
    };
    string Expand(string value)
    {
        for(int i=0;i<6;i++) foreach(var prop in props) value=value.Replace("$("+prop.Key+")",prop.Value,StringComparison.OrdinalIgnoreCase);
        return value;
    }
    foreach(var group in xml.Descendants().Where(e=>e.Name.LocalName=="PropertyGroup"))
        foreach(var item in group.Elements()) if(!props.ContainsKey(item.Name.LocalName)) props[item.Name.LocalName]=Expand(item.Value);
    string assembly = props["AssemblyName"];
    var refs = Directory.GetFiles(framework,"*.dll").Where(f=>!f.EndsWith(".Wrapper.dll") && !f.EndsWith(".Thunk.dll"))
        .Concat(Directory.GetFiles(Path.Combine(framework,"Facades"),"*.dll"))
        .Select(f=>(MetadataReference)MetadataReference.CreateFromFile(f)).ToList();
    foreach(var item in xml.Descendants().Where(e=>e.Name.LocalName=="Reference"))
    {
        string name=item.Attribute("Include")!.Value.Split(',')[0];
        if(compiled.TryGetValue(name,out var dependency)) { refs.Add(dependency.ToMetadataReference()); continue; }
        var hint=item.Elements().FirstOrDefault(e=>e.Name.LocalName=="HintPath");
        if(hint==null) continue;
        string target=Path.GetFullPath(Path.Combine(dir,Expand(hint.Value)));
        if(File.Exists(target)) refs.Add(MetadataReference.CreateFromFile(target));
        else { Console.WriteLine($"MISSING {relative}: {target}"); errors++; }
    }
    foreach(var item in xml.Descendants().Where(e=>e.Name.LocalName=="ProjectReference"))
    {
        string target=Path.GetFileNameWithoutExtension(item.Attribute("Include")!.Value);
        if(compiled.TryGetValue(target,out var dependency)) refs.Add(dependency.ToMetadataReference());
    }
    var sources=new List<SyntaxTree>();
    foreach(var item in xml.Descendants().Where(e=>e.Name.LocalName=="Compile"))
    {
        string include=Expand(item.Attribute("Include")!.Value);
        if(include.Contains('*')) throw new Exception("Explicit source inventory required: "+include);
        string file=Path.GetFullPath(Path.Combine(dir,include));
        sources.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file),new CSharpParseOptions(LanguageVersion.Latest,documentationMode:DocumentationMode.Diagnose,preprocessorSymbols:new[]{"DEBUG","TRACE"}),file));
    }
    var compilation=CSharpCompilation.Create(assembly,sources,refs,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
    compiled[assembly]=compilation;
    var failures=compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error || (assembly=="APIShared" && d.Id=="CS1591")).ToArray();
    foreach(var failure in failures) Console.WriteLine(failure.ToString());
    errors+=failures.Length;
    if (assembly == "APIShared")
    {
        int checkedHooks = 0;
        foreach (var tree in sources.Where(t => t.FilePath.EndsWith("MissionLifecycleCapability.cs")))
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (call.Expression is not GenericNameSyntax generic || generic.Identifier.ValueText != "Hook") continue;
                var invoke = ((INamedTypeSymbol)model.GetTypeInfo(generic.TypeArgumentList.Arguments[0]).Type!).DelegateInvokeMethod!;
                var target = (INamedTypeSymbol)model.GetTypeInfo(((TypeOfExpressionSyntax)call.ArgumentList.Arguments[0].Expression).Type).Type!;
                string methodName = (string)model.GetConstantValue(call.ArgumentList.Arguments[1].Expression).Value!;
                bool instance = invoke.Parameters.Length > 0 && SymbolEqualityComparer.Default.Equals(invoke.Parameters[0].Type, target);
                var expected = invoke.Parameters.Skip(instance ? 1 : 0).Select(p => p.Type).ToArray();
                bool match = target.GetMembers(methodName).OfType<IMethodSymbol>().Any(m =>
                    m.IsStatic != instance && SymbolEqualityComparer.Default.Equals(m.ReturnType, invoke.ReturnType) &&
                    m.Parameters.Select(p => p.Type).SequenceEqual(expected, SymbolEqualityComparer.Default));
                if (!match) { Console.WriteLine($"HOOK SIGNATURE MISMATCH: {target.Name}.{methodName}"); errors++; }
                checkedHooks++;
            }
        }
        Console.WriteLine($"HOOK CONTRACTS: {checkedHooks} signatures checked against installed-build managed metadata");
    }
    Console.WriteLine($"CHECK {relative}: {sources.Count} sources, {failures.Length} errors");
}
return errors==0?0:1;
