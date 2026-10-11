using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Xml.Linq;
using System.Diagnostics;
using System.Text.Json;

string root = Path.GetFullPath(args[0]);
string game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
string framework = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1";
var references = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
foreach (string dir in new[] { framework, Path.Combine(framework,"Facades"),
    Path.Combine(game,"Stronghold Crusader Definitive Edition_Data","Managed"),
    Path.Combine(game,"BepInEx","core"), Path.Combine(game,"BepInEx","plugins","000shcdese") })
    foreach (string file in Directory.GetFiles(dir,"*.dll"))
        try {
            if (dir.EndsWith("Managed") && !new[] { "Assembly-CSharp.dll", "UnityEngine.dll", "UnityEngine.CoreModule.dll",
                "UnityEngine.InputLegacyModule.dll", "Noesis.NoesisGUI.dll", "com.rlabrecque.steamworks.net.dll" }.Contains(Path.GetFileName(file))) continue;
            if (new[] { "PresentationCore.dll", "PresentationFramework.dll", "WindowsBase.dll" }.Contains(Path.GetFileName(file))) continue;
            AssemblyName.GetAssemblyName(file); references[Path.GetFileName(file)] = file;
        }
        catch (BadImageFormatException) { }
string api = Path.Combine(game,"BepInEx","plugins","APIShared_Serp","APIShared.dll");
references["APIShared.dll"] = api;
// Compile the changed shared API into a metadata image in memory only. Runtime
// builds/installation remain exclusively in the build.bat drivers.
var apiProject = XDocument.Load(Path.Combine(root,"APIShared","APIShared.csproj"));
var apiRefs = new Dictionary<string,string>(references,StringComparer.OrdinalIgnoreCase);
apiRefs.Remove("APIShared.dll");
apiRefs.Remove("0Harmony20.dll");
foreach (var hint in apiProject.Descendants().Where(e=>e.Name.LocalName=="HintPath")) {
    string path=hint.Value.Replace("$(MSBuildThisFileDirectory)",Path.Combine(root,"APIShared")+Path.DirectorySeparatorChar).Replace("$(GameDir)",game).Replace("$(ExtenderDir)",Path.Combine(game,"BepInEx","plugins","000shcdese"));
    path=Path.GetFullPath(Path.Combine(root,"APIShared",path));
    if(File.Exists(path)) apiRefs[Path.GetFileName(path)]=path;
}
if(apiRefs.ContainsKey("Assembly-CSharp-publicized.dll")) apiRefs.Remove("Assembly-CSharp.dll");
var apiInput = EvaluateSources(Path.Combine(root,"APIShared","APIShared.csproj"));
var apiTrees = apiInput.Paths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path),
    CSharpParseOptions.Default.WithPreprocessorSymbols(apiInput.Symbols).WithDocumentationMode(DocumentationMode.Diagnose),path:path));
var apiCompilation=CSharpCompilation.Create("APIShared",apiTrees,apiRefs.Values.Select(p=>MetadataReference.CreateFromFile(p)),
    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true).WithSpecificDiagnosticOptions(
        new Dictionary<string,ReportDiagnostic>{{"CS1591",ReportDiagnostic.Error}}));
using var apiImage=new MemoryStream();
using var apiXml=new MemoryStream();
var apiResult=apiCompilation.Emit(apiImage,xmlDocumentationStream:apiXml);
if(!apiResult.Success) { foreach(var d in apiResult.Diagnostics.Where(d=>d.Severity==DiagnosticSeverity.Error)) Console.Error.WriteLine(d); return 1; }
var changedApi=MetadataReference.CreateFromImage(apiImage.ToArray());
Console.WriteLine("PASS: APIShared source and installed assembly visibility/signature contract (memory only, no DLL file emitted).");
foreach (string mod in args.Length > 1 ? args.Skip(1).Where(a=>!a.StartsWith("--")) : new[] { "EnemyGatePathfindingTest", "EnemyBridgePathTest", "EnemyBridgePathTest.PolicyTests" })
{
    string projectDir = mod == "BugfixesAndQoL" ? Path.Combine(root,mod) : Path.Combine(root,"Testmods",mod.Replace(".PolicyTests",""));
    var xml = XDocument.Load(Path.Combine(projectDir,mod+".csproj"));
    var modReferences = new Dictionary<string,string>(references, StringComparer.OrdinalIgnoreCase);
    // Honor explicit runtime references; do not import the compatibility Harmony assembly alongside 0Harmony.
    if (mod == "BugfixesAndQoL") modReferences.Remove("0Harmony20.dll");
    foreach (var hint in xml.Descendants().Where(e=>e.Name.LocalName=="HintPath"))
    {
        string path = hint.Value.Replace("$(MSBuildThisFileDirectory)",projectDir+Path.DirectorySeparatorChar).Replace("$(GameDir)", game)
            .Replace("$(ExtenderDir)", Path.Combine(game,"BepInEx","plugins","000shcdese"))
            .Replace("$(ApiSharedDir)", Path.Combine(game,"BepInEx","plugins","APIShared_Serp"));
        if (path.Contains("$(")) continue;
        path = Path.GetFullPath(Path.Combine(projectDir,path));
        if (File.Exists(path)) modReferences[Path.GetFileName(path)] = path;
    }
    if(modReferences.ContainsKey("Assembly-CSharp-publicized.dll")) modReferences.Remove("Assembly-CSharp.dll");
    var input = EvaluateSources(Path.Combine(projectDir,mod+".csproj"));
    var trees = input.Paths.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path),
        CSharpParseOptions.Default.WithPreprocessorSymbols(input.Symbols),path:path)).ToList();
    foreach (var provider in args.Contains("--source-api-only") ? new[] { changedApi } : new[] { changedApi, MetadataReference.CreateFromFile(api) })
    {
        var compilation = CSharpCompilation.Create(mod == "BugfixesAndQoL" ? "BugfixesAndQoL" : mod+"StaticContract",trees,
            modReferences.Values.Where(p=>!Path.GetFileName(p).Equals("APIShared.dll",StringComparison.OrdinalIgnoreCase)).Select(p=>MetadataReference.CreateFromFile(p)).Cast<MetadataReference>().Append(provider),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,allowUnsafe:true));
        var errors = compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
        foreach (var error in errors) Console.Error.WriteLine(error);
        if (errors.Length>0) return 1;
    }
    Console.WriteLine("PASS: evaluated project sources compile against " + (args.Contains("--source-api-only") ? "source APIShared" : "source and installed APIShared") + ": " + mod + " (no runtime assembly emitted)");
}
return 0;

(string[] Paths, string[] Symbols) EvaluateSources(string project)
{
    var info = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    foreach (string argument in new[] { "msbuild", project, "-nologo", "-getItem:Compile", "-getProperty:DefineConstants", "-p:GameDir="+game,
        "-p:ExtenderDir="+Path.Combine(game,"BepInEx","plugins","000shcdese"), "-p:ApiSharedDir="+Path.GetDirectoryName(api) }) info.ArgumentList.Add(argument);
    using var process = Process.Start(info)!;
    var output = process.StandardOutput.ReadToEndAsync();
    var errors = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException(errors.GetAwaiter().GetResult()+output.GetAwaiter().GetResult());
    using var document = JsonDocument.Parse(output.GetAwaiter().GetResult());
    string[] paths = document.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray().Select(item=>item.GetProperty("FullPath").GetString()!).ToArray();
    if (paths.Length == 0 || paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length) throw new InvalidOperationException("Missing/duplicate evaluated sources: "+project);
    string[] symbols = document.RootElement.GetProperty("Properties").GetProperty("DefineConstants").GetString()!.Split(';',StringSplitOptions.RemoveEmptyEntries);
    Console.WriteLine("Evaluated "+Path.GetFileName(project)+": "+paths.Length+" sources");
    return (paths,symbols);
}
