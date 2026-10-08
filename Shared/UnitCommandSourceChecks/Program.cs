using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

// Compiler diagnostics only. No mod assembly is emitted, copied or installed.
var root = Path.GetFullPath(args[0]);
if (args.Contains("--compare-refactor"))
{
    string GitRead(string relative)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git") {
            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true,
            ArgumentList = { "show", "HEAD:" + relative }
        });
        string text = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException(process.StandardError.ReadToEnd());
        // Git stores LF; the Windows checkout baseline uses CRLF, including literal strings.
        return text.Replace("\r\n", "\n").Replace("\n", "\r\n");
    }
    string Normalize(string value)
    {
        value = System.Text.RegularExpressions.Regex.Replace(value, @"APIShared\.(?:GameModes|ModSettings|SerpsMods)\.", "Shared.");
        return value.Replace("SerpsModProfiles.GetProfile", "GameplayModModePolicy.GetProfile");
    }
    IEnumerable<string> Bodies(string text) => CSharpSyntaxTree.ParseText(text,
        new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: new[] { "API_SHARED_LOBBY_OBSERVER", "API_SHARED_INTERNAL_JSON" }))
        .GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseMethodDeclarationSyntax>()
        .Select(m => Normalize(m.Body?.NormalizeWhitespace().ToFullString() ?? m.ExpressionBody?.NormalizeWhitespace().ToFullString() ?? ""));
    IEnumerable<string> StoredMembers(string text) => CSharpSyntaxTree.ParseText(text,
        new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: new[] { "API_SHARED_LOBBY_OBSERVER", "API_SHARED_INTERNAL_JSON" }))
        .GetRoot().DescendantNodes().Where(n => n is Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax ||
            n is Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax || n is Microsoft.CodeAnalysis.CSharp.Syntax.EventDeclarationSyntax)
        .Select(n => Normalize(n.WithoutTrivia().NormalizeWhitespace().ToFullString())).OrderBy(x => x);
    var differences = new List<string>();
    int matched = 0;
    var allowed = new HashSet<string> { "ApiSharedRuntime.cs", "GameplayModModePolicy.cs", "Contracts.cs" };
    foreach (string source in Directory.GetFiles(Path.Combine(root, "APIShared/src"), "*.cs", SearchOption.AllDirectories))
    {
        string filename = Path.GetFileName(source);
        if (new[] { "ModApiClient.cs", "SerpsModProfiles.cs" }.Contains(filename) ||
            source.Contains("\\ModSettings\\")) continue;
        string original;
        string originalPath = source.Contains("\\UnitCommands\\") ? "APIShared/src/UnitCommands/" + filename : "APIShared/src/" + filename;
        try { original = GitRead(originalPath); } catch { continue; }
        if (filename != "GameplayModModePolicy.cs" && !StoredMembers(original).SequenceEqual(StoredMembers(File.ReadAllText(source))))
            differences.Add(filename + ": fields, properties or events changed");
        if (Bodies(original).SequenceEqual(Bodies(File.ReadAllText(source)))) matched++;
        else if (!allowed.Contains(filename))
        {
            differences.Add(filename);
            File.WriteAllText(Path.Combine(root, "_inspect/APISharedRefactor/" + filename + ".old-methods.txt"), string.Join("\n===METHOD===\n", Bodies(original)));
            File.WriteAllText(Path.Combine(root, "_inspect/APISharedRefactor/" + filename + ".new-methods.txt"), string.Join("\n===METHOD===\n", Bodies(File.ReadAllText(source))));
        }
    }
    // Partial extraction preserves all original method bodies across their new files.
    string oldPreset = GitRead("APIShared/src/PresetLobbyModSettingsViewModel.cs");
    var extractedNames = new[] { "PresetLobbyModSettingsViewModel.cs", "PresetLobbyModSettingsViewModel.Persistence.cs",
        "PresetLobbyModSettingsViewModel.Sources.cs", "PerPlayerLobbySettings.cs", "LobbyModSettingsPresetRegistration.cs" };
    var oldBodies = Bodies(oldPreset).OrderBy(x => x).ToArray();
    var newBodies = extractedNames.SelectMany(n => Bodies(File.ReadAllText(Path.Combine(root, "APIShared/src/ModSettings", n)))).OrderBy(x => x).ToArray();
    if (!StoredMembers(oldPreset).SequenceEqual(extractedNames.SelectMany(n => StoredMembers(File.ReadAllText(Path.Combine(root, "APIShared/src/ModSettings", n)))).OrderBy(x => x)))
        differences.Add("Preset extraction changed fields, properties or events");
    if (!oldBodies.SequenceEqual(newBodies))
    {
        differences.Add("Preset extraction changed method bodies");
        File.WriteAllText(Path.Combine(root, "_inspect/APISharedRefactor/preset.old-methods.txt"), string.Join("\n===METHOD===\n", oldBodies));
        File.WriteAllText(Path.Combine(root, "_inspect/APISharedRefactor/preset.new-methods.txt"), string.Join("\n===METHOD===\n", newBodies));
    }
    int consumers = 0;
    using (var changed = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git") {
        WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
        UseShellExecute = false, CreateNoWindow = true,
        ArgumentList = { "diff", "--name-only", "--diff-filter=AM", "--", "*.cs" }
    }))
    {
        string paths = changed.StandardOutput.ReadToEnd();
        changed.WaitForExit();
        if (changed.ExitCode != 0) throw new InvalidOperationException(changed.StandardError.ReadToEnd());
        foreach (string path in paths.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (path.StartsWith("APIShared/") || path.StartsWith("_inspect/") || path.Contains("/tests/") ||
                path.Contains(".Tests/") || path.StartsWith("Shared/UnitCommandSourceChecks/")) continue;
            string original = GitRead(path);
            string current = File.ReadAllText(Path.Combine(root, path));
            if (!Bodies(original).SequenceEqual(Bodies(current)) || !StoredMembers(original).SequenceEqual(StoredMembers(current)))
                differences.Add(path + ": consumer executable members changed");
            consumers++;
        }
    }
    foreach (string difference in differences) Console.WriteLine("FAIL: " + difference);
    Console.WriteLine($"Git executable-member comparison: {matched} unchanged feature files; {consumers} migrated consumer files; preset methods {oldBodies.Length} -> {newBodies.Length}; unexpected differences={differences.Count}.");
    return differences.Count == 0 ? 0 : 1;
}
var game = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
var framework = @"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1";
var projects = new[] {"APIShared/APIShared.csproj", "BugfixesAndQoL/BugfixesAndQoL.csproj", "Testmods/MoatMove/MoatMove.csproj"};
if (args.Contains("--formation-tests"))
    projects = projects.Concat(new[] {
        "BugfixesAndQoL/tests/Formations.Tests/Formations.Tests.csproj",
        "BugfixesAndQoL/tests/ExtendedShiftCommandQueue.Tests/ExtendedShiftCommandQueue.Tests.csproj"
    }).ToArray();
if (args.Contains("--update-plan"))
{
    // Compile the reviewed update inventory without emitting or installing mods.
    string planPath = args[Array.IndexOf(args, "--update-plan") + 1];
    using var plan = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, planPath)));
    using var inventory = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Shared/ScriptExtenderUpdate/mods.json")));
    var selected = plan.RootElement.GetProperty("ImpactReview").GetProperty("BuildMods").EnumerateArray()
        .Select(x => x.GetString()).ToHashSet();
    var reviewed = inventory.RootElement.EnumerateArray().Where(m => selected.Contains(m.GetProperty("Name").GetString()))
        .OrderBy(m => m.GetProperty("BuildOrder").GetInt32()).ThenBy(m => m.GetProperty("Name").GetString())
        .Select(m => m.GetProperty("Project").GetString()).ToList();
    foreach (var name in plan.RootElement.GetProperty("TestMods").EnumerateArray())
        reviewed.Add(Path.GetRelativePath(root, Directory.GetFiles(Path.Combine(root, "Testmods", name.GetString()), "*.csproj")
            .Single(p => !Path.GetFileName(p).Contains(".PolicyTests."))));
    projects = reviewed.ToArray();
}
if (args.Contains("--really-alive"))
    projects = new[] {
        "APIShared/APIShared.csproj", "BugfixesAndQoL/BugfixesAndQoL.csproj",
        "ExtraFeatures/ExtraFeatures.csproj", "ImprovedHunters/ImprovedHunters.csproj",
        "RandomEvents/RandomEvents.csproj", "StartConditions/StartConditions.csproj",
        "Testmods/AIDefenseTest/AIDefenseTest.csproj",
        "Testmods/EnemyGatePathfindingTest/EnemyGatePathfindingTest.csproj",
        "Testmods/EngineerSiegeFixTest/EngineerSiegeFixTest.csproj",
        "Testmods/MoatMove/MoatMove.csproj",
        "Testmods/OutpostTest/OutpostTest.csproj",
        "Testmods/SpectatorEditorBuildTest/SpectatorEditorBuildTest.csproj",
        "Testmods/StockpileAccessFixTest/StockpileAccessFixTest.csproj",
        "Testmods/VirtualUnitsPrototype/VirtualUnitsPrototype.csproj"
    };
if (args.Contains("--projects-file"))
    projects = File.ReadAllLines(Path.Combine(root, args[Array.IndexOf(args, "--projects-file") + 1])).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
var compilations = new Dictionary<string, CSharpCompilation>();
var orderedProjects = new List<string>();
void AddProject(string relative)
{
    relative = Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(root, relative)));
    if (orderedProjects.Contains(relative)) return;
    var path = Path.Combine(root, relative);
    foreach (var entry in XDocument.Load(path).Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
        AddProject(Path.GetRelativePath(root, Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), (string)entry.Attribute("Include")))));
    orderedProjects.Add(relative);
}
foreach (var relative in projects) AddProject(relative);
int errors = 0;
foreach (var relative in orderedProjects)
{
    var project = Path.Combine(root, relative);
    var folder = Path.GetDirectoryName(project);
    var xml = XDocument.Load(project);
    var name = xml.Descendants().FirstOrDefault(e => e.Name.LocalName == "AssemblyName")?.Value ?? Path.GetFileNameWithoutExtension(project);
    var properties = new Dictionary<string, string> {
        ["GameDir"] = game, ["ExtenderDir"] = game + @"\BepInEx\plugins\000shcdese",
        ["ApiSharedDir"] = game + @"\BepInEx\plugins\APIShared_Serp", ["MSBuildThisFileDirectory"] = folder + "\\",
        ["MSBuildSDKsPath"] = Directory.GetDirectories(@"C:\Program Files\dotnet\sdk")
            .OrderByDescending(p => Version.Parse(Path.GetFileName(p))).First() + @"\Sdks"
    };
    foreach (var property in xml.Descendants().Where(e => e.Parent?.Name.LocalName == "PropertyGroup"))
        if (!properties.ContainsKey(property.Name.LocalName)) properties[property.Name.LocalName] = property.Value;
    string Expand(string value)
    {
        for (int pass = 0; pass < 16 && value.Contains("$("); pass++)
            foreach (var property in properties) value = value.Replace("$(" + property.Key + ")", property.Value);
        return value;
    }
    var defines = xml.Descendants().Where(e => e.Name.LocalName == "DefineConstants").SelectMany(e => e.Value.Split(';')).Where(s => !s.Contains('$') && s.Length > 0);
    var parse = new CSharpParseOptions(LanguageVersion.Preview, preprocessorSymbols: defines);
    var sources = xml.Descendants().Where(e => e.Name.LocalName == "Compile" && e.Attribute("Include") != null).SelectMany(e => {
        string path = Path.GetFullPath(Path.Combine(folder, (string)e.Attribute("Include")));
        return path.Contains('*') ? Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path)) : new[] { path };
    }).Distinct().ToArray();
    bool coreProject = xml.Root.Attribute("Sdk") != null;
    if (coreProject)
        sources = sources.Concat(Directory.GetFiles(folder, "*.cs", SearchOption.TopDirectoryOnly)).Distinct().ToArray();
    var removedSources = xml.Descendants().Where(e => e.Name.LocalName == "Compile" && e.Attribute("Remove") != null)
        .Select(e => Path.GetFullPath(Path.Combine(folder, (string)e.Attribute("Remove")))).ToHashSet(StringComparer.OrdinalIgnoreCase);
    sources = sources.Where(p => !removedSources.Contains(p)).ToArray();
    var trees = sources.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), parse, p)).ToList();
    if (coreProject && xml.Descendants().Any(e => e.Name.LocalName == "ImplicitUsings" && e.Value == "enable"))
        trees.Add(CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; global using System.Net.Http; global using System.Threading; global using System.Threading.Tasks;", parse, "ImplicitUsings.g.cs"));
    var refs = new List<MetadataReference>();
    string referenceFramework = coreProject ? Directory.GetDirectories(@"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref")
        .OrderByDescending(p => Version.Parse(Path.GetFileName(p))).SelectMany(p => Directory.GetDirectories(Path.Combine(p, "ref"))).First() : framework;
    var frameworkNames = xml.Descendants().Where(e => e.Name.LocalName == "Reference")
        .Select(e => ((string)e.Attribute("Include")).Split(',')[0]).Append("mscorlib").ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var path in Directory.GetFiles(referenceFramework, "*.dll").Where(p => !p.Contains(".Thunk.") && !p.Contains(".Wrapper.") &&
        (coreProject || frameworkNames.Contains(Path.GetFileNameWithoutExtension(p))))) refs.Add(MetadataReference.CreateFromFile(path));
    if (!coreProject)
        foreach (var path in Directory.GetFiles(Path.Combine(framework, "Facades"), "*.dll")) refs.Add(MetadataReference.CreateFromFile(path));
    foreach (var entry in xml.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
    {
        var dependencyPath = Path.GetFullPath(Path.Combine(folder, (string)entry.Attribute("Include")));
        var dependencyName = XDocument.Load(dependencyPath).Descendants().First(e => e.Name.LocalName == "AssemblyName").Value;
        refs.Add(compilations[dependencyName].ToMetadataReference());
    }
    foreach (var entry in xml.Descendants().Where(e => e.Name.LocalName == "Reference"))
    {
        var include = ((string)entry.Attribute("Include")).Split(',')[0];
        if (compilations.TryGetValue(include, out var dependency)) { refs.Add(dependency.ToMetadataReference()); continue; }
        var hint = entry.Elements().FirstOrDefault(e => e.Name.LocalName == "HintPath")?.Value;
        if (hint == null) continue;
        hint = Expand(hint);
        var resolved = Path.GetFullPath(Path.Combine(folder, hint));
        if (!File.Exists(resolved)) { Console.WriteLine("Missing reference: " + resolved); errors++; continue; }
        if (args.Contains("--real") && include == "Assembly-CSharp") resolved = Path.Combine(game, "Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll");
        if (include == "SHCDESE" || include == "Assembly-CSharp") Console.WriteLine(name + " verified reference " + include + ": " + resolved);
        refs.Add(MetadataReference.CreateFromFile(resolved));
    }
    var compilation = CSharpCompilation.Create(name, trees, refs.DistinctBy(r => r is CompilationReference c ? c.Compilation.AssemblyName : r.Display),
        new CSharpCompilationOptions(coreProject && xml.Descendants().Any(e => e.Name.LocalName == "OutputType" && e.Value == "Exe")
            ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    compilations.Add(name, compilation);
    var diagnostics = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
    Console.WriteLine(name + ": " + sources.Length + " sources, " + diagnostics.Length + " errors");
    foreach (var diagnostic in diagnostics) Console.WriteLine(diagnostic);
    errors += diagnostics.Length;
    if (name == "ThirdPartyMod.Examples" && diagnostics.Length == 0)
    {
        var settings = compilation.GetTypeByMetadataName("ThirdPartyMod.ExampleSettings");
        var publicMembers = new HashSet<string>();
        for (var type = settings; type != null; type = type.BaseType)
            foreach (var member in type.GetMembers().Where(m => m.DeclaredAccessibility == Accessibility.Public))
                publicMembers.Add(member.Name);
        var xaml = XDocument.Load(Path.Combine(folder, "Override/ScriptExtenderUI/APISharedExample.xaml"));
        foreach (var attribute in xaml.Descendants().Attributes())
        {
            var match = System.Text.RegularExpressions.Regex.Match(attribute.Value, @"^\{Binding\s+([^,}\s]+)");
            if (match.Success && !publicMembers.Contains(match.Groups[1].Value))
            {
                Console.WriteLine("Invalid public example XAML binding: " + match.Groups[1].Value);
                errors++;
            }
        }
        Console.WriteLine("Public example XAML bindings verified against compiled inherited contracts.");
    }
}
return errors == 0 ? 0 : 1;
