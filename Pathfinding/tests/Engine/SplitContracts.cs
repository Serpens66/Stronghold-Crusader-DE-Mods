using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Xml.Linq;

internal static class SplitContracts
{
    private static IEnumerable<string> ExpandSources(string directory, string include)
    {
        string path = Path.Combine(directory, include);
        if (!path.Contains('*')) return new[] { Path.GetFullPath(path) };
        string sourceDirectory = Path.GetFullPath(path.Substring(0, path.IndexOf('*'))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Directory.GetFiles(sourceDirectory, Path.GetFileName(path),
            path.Contains("**") ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }

    internal static void Validate(string root)
    {
        string Read(string path) => File.ReadAllText(Path.Combine(root, path));
        void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
        // The former main/addon split no longer exists: command owner and engine provider ship in one mod.
        // Still binding: process rooting, separate logical policies, the publication guard and the runtime rules below.
        string settings = Read("Pathfinding/src/Settings/PathfindingViewModel.cs");
        string api = Read("Pathfinding/src/UnitCommands/Runtime/UnitCommandPathAPI.cs");
        string policy = Read("Pathfinding/src/UnitCommands/Movement/TraversalDispatch.cs");
        Check(settings.Contains("enableImprovedManualUnitCommands = true") &&
            settings.Contains("EnableImprovedManualUnitCommands = true;"), "Manual setting incorrect");
        Check(api.Contains("Candidates.Add(runtime)") && api.Contains("RegisterTraversal") &&
            policy.Contains("Traversal?.Enabled == true") && policy.Contains("Traversal?.EngineActive == true") && policy.Contains("EnableImprovedManualUnitCommands"),
            "Process rooting or separate logical policies missing");
        string permanent = Read("Pathfinding/src/UnitCommands/Native/PermanentCommandHooks.cs");
        Check(permanent.Contains("publishedCommandTransactions") && permanent.Contains("RollbackUnpublished"),
            "Shared publication guard missing");
        foreach (string relative in new[] { "APIShared/APIShared.csproj", "Pathfinding/Pathfinding.csproj" })
        {
            string directory = Path.GetDirectoryName(Path.Combine(root, relative))!;
            foreach (string file in XDocument.Load(Path.Combine(root, relative)).Descendants().Where(e => e.Name.LocalName == "Compile" && e.Attribute("Include") != null).SelectMany(e => ExpandSources(directory, e.Attribute("Include")!.Value)))
            {
                string source = File.ReadAllText(file);
                Check(!source.Contains("System.Text.Json") && !source.Contains("Newtonsoft.Json") &&
                    !source.Contains("JavaScriptSerializer") && !source.Contains("JsonUtility"), "Forbidden runtime JSON: " + file);
                var tree = CSharpSyntaxTree.ParseText(source);
                foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                    Check(method.Identifier.Text is not ("OnDestroy" or "OnDisable" or "OnApplicationQuit"), "Runtime teardown: " + file);
                foreach (var type in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => c.BaseList?.ToString().Contains("BaseUnityPlugin") == true))
                    Check(!type.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text is "Update" or "LateUpdate" or "FixedUpdate"), "Plugin callback after cleanup: " + file);
            }
        }
        Console.WriteLine("PASS: shared hook ownership, separate settings, source closures, JSON/lifecycle contracts.");
    }
}
