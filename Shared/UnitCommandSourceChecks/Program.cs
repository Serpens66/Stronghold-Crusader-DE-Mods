using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Compiler diagnostics only. No mod assembly is emitted, copied or installed.
var root = Path.GetFullPath(args[0]);
var game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
var framework = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1";
var projects = new[] {"APIShared/APIShared.csproj", "BugfixesAndQoL/BugfixesAndQoL.csproj", "Testmods/MoatMove/MoatMove.csproj"};
var compilations = new Dictionary<string, CSharpCompilation>();
int errors = 0;
foreach (var relative in projects)
{
    var project = Path.Combine(root, relative);
    var folder = Path.GetDirectoryName(project);
    var xml = XDocument.Load(project);
    var name = xml.Descendants().First(e => e.Name.LocalName == "AssemblyName").Value;
    var defines = xml.Descendants().Where(e => e.Name.LocalName == "DefineConstants").SelectMany(e => e.Value.Split(';')).Where(s => !s.Contains('$') && s.Length > 0);
    var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: defines);
    var sources = xml.Descendants().Where(e => e.Name.LocalName == "Compile").Select(e => Path.GetFullPath(Path.Combine(folder, (string)e.Attribute("Include")))).Distinct().ToArray();
    var trees = sources.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), parse, p)).ToArray();
    var refs = new List<MetadataReference>();
    foreach (var path in Directory.GetFiles(framework, "*.dll").Where(p => !p.Contains(".Thunk.") && !p.Contains(".Wrapper."))) refs.Add(MetadataReference.CreateFromFile(path));
    foreach (var path in Directory.GetFiles(Path.Combine(framework, "Facades"), "*.dll")) refs.Add(MetadataReference.CreateFromFile(path));
    foreach (var entry in xml.Descendants().Where(e => e.Name.LocalName == "Reference"))
    {
        var include = ((string)entry.Attribute("Include")).Split(',')[0];
        if (compilations.TryGetValue(include, out var dependency)) { refs.Add(dependency.ToMetadataReference()); continue; }
        var hint = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "HintPath")?.Value;
        if (hint == null) continue;
        hint = hint.Replace("$(GameDir)", game).Replace("$(ExtenderDir)", game + @"\BepInEx\plugins\000shcdese")
            .Replace("$(ApiSharedDir)", game + @"\BepInEx\plugins\APIShared_Serp").Replace("$(MSBuildThisFileDirectory)", folder + "\\");
        var resolved = Path.GetFullPath(Path.Combine(folder, hint));
        if (!File.Exists(resolved)) { Console.WriteLine("Missing reference: " + resolved); errors++; continue; }
        if (args.Contains("--real") && include == "Assembly-CSharp") resolved = Path.Combine(game, "Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll");
        refs.Add(MetadataReference.CreateFromFile(resolved));
    }
    var compilation = CSharpCompilation.Create(name, trees, refs.DistinctBy(r => r is CompilationReference c ? c.Compilation.AssemblyName : r.Display),
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    compilations.Add(name, compilation);
    var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    Console.WriteLine(name + ": " + sources.Length + " sources, " + diagnostics.Length + " errors");
    foreach (var diagnostic in diagnostics) Console.WriteLine(diagnostic);
    if (args.Contains("--real") && name == "APIShared")
    {
        var baseline = diagnostics.Where(d => (d.Id == "CS0122" || d.Id == "CS1061") && d.Location.IsInSource &&
            Path.GetFileName(d.Location.SourceTree.FilePath) == "MissionLifecycleCapability.cs" &&
            d.ToString().Contains("gameLocalPlayerID")).ToArray();
        if (baseline.Length == 1)
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git") {
                WorkingDirectory = root, RedirectStandardOutput = true,
                ArgumentList = {"show", "HEAD:APIShared/src/MissionLifecycleCapability.cs"} });
            var old = process.StandardOutput.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0 || !old.Contains("EditorDirector.instance.gameLocalPlayerID") ||
                File.ReadAllText(Path.Combine(root, "APIShared/src/MissionLifecycleCapability.cs")).Replace("\r\n", "\n") != old.Replace("\r\n", "\n"))
                throw new Exception("The documented preexisting private access changed.");
            Console.WriteLine("Known unchanged HEAD access: MissionLifecycleCapability gameLocalPlayerID; all new accesses checked against real Assembly-CSharp.");
            diagnostics = diagnostics.Except(baseline).ToArray();
        }
    }
    errors += diagnostics.Length;
}
return errors == 0 ? 0 : 1;
