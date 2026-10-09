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
        string main = Read("BugfixesAndQoL/BugfixesAndQoL.csproj");
        string addon = Read("Testmods/MoatMove/MoatMove.csproj");
        string plugin = Read("Testmods/MoatMove/src/MoatMovePlugin.cs");
        string settings = Read("BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs");
        string api = Read("APIShared/src/UnitCommands/Runtime/UnitCommandPathAPI.cs");
        string policy = Read("APIShared/src/UnitCommands/Movement/TraversalDispatch.cs");
        Check(plugin.Contains("BepInDependency(\"BugfixesAndQoL_Serp\"") &&
            plugin.Contains("BepInDependency(\"APIShared_Serp\""), "Addon hard dependencies missing");
        Check(!plugin.Contains("ReportConflict") && !plugin.Contains("MoatMoveConflictPolicy"), "Obsolete addon conflict remains");
        Check(!main.Contains("src\\MoatSearchKernel.cs") && !main.Contains("src\\FastMoatBridge.cs") &&
            addon.Contains("src\\MoatSearchKernel.cs"), "Traversal search compiled into main");
        Check(!addon.Contains("src\\FriendlyMoatMovementRuntime.cs") &&
            !addon.Contains("src\\CursorConnectivity.cs"), "Duplicate command engine compiled into addon");
        Check(settings.Contains("private bool enableImprovedManualUnitCommands = true;") &&
            settings.Contains("EnableImprovedManualUnitCommands = true;") &&
            !settings.Contains("public int FriendlyMoatMovementMode"), "Manual setting or retirement incorrect");
        Check(api.Contains("Candidates.Add(runtime)") && api.Contains("RegisterTraversal") &&
            policy.Contains("Traversal?.Enabled == true") && policy.Contains("EnableImprovedManualUnitCommands"),
            "Process rooting or separate logical policies missing");
        string permanent = Read("APIShared/src/UnitCommands/Native/PermanentCommandHooks.cs");
        Check(permanent.Contains("publishedCommandTransactions") && permanent.Contains("RollbackUnpublished"),
            "Shared publication guard missing");
        foreach (string relative in new[] { "APIShared/APIShared.csproj", "BugfixesAndQoL/BugfixesAndQoL.csproj", "Testmods/MoatMove/MoatMove.csproj" })
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
        Console.WriteLine("PASS: shared hook ownership, addon dependencies, separate settings, source closures, JSON/lifecycle contracts.");
    }
}
