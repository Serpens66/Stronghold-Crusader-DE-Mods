using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class StandaloneContracts
{
    internal static void Validate(string root, string sourceDir)
    {
        string modDir = Path.GetDirectoryName(sourceDir)!;
        using var provenance = JsonDocument.Parse(File.ReadAllText(Path.Combine(modDir, "SOURCE_PROVENANCE.json")));
        int count = 0;
        foreach (var item in provenance.RootElement.GetProperty("files").EnumerateArray())
        {
            string name = item.GetProperty("file").GetString()!;
            string original = Path.Combine(root, "BugfixesAndQoL", "src", name);
            string copied = Path.Combine(sourceDir, name);
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(original))) == item.GetProperty("sourceSha256").GetString(), "Source changed since extraction: " + name);
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copied))) == item.GetProperty("copySha256").GetString(), "Copy changed since extraction: " + name);
            string expected = File.ReadAllText(original).Replace("BugfixesAndQoLViewModel", "MoatMoveOptions")
                .Replace("BugfixesAndQoL", "MoatMove").Replace("Bugfixes and QoL", "MoatMove");
            Check(expected == File.ReadAllText(copied), "Unexpected behavioral edit: " + name);
            count++;
        }
        Check(count == 22, "Incomplete source closure");

        string[] names = { "MoatMoveOptions.cs", "MoatMoveConflictPolicy.cs", "MovementOptionsSnapshot.cs", "FriendlyMoatMovementPolicy.cs", "MoveFormationSpacingPolicy.cs" };
        var trees = names.Select(name => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(sourceDir, name)))).ToArray();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("MoatMoveStandaloneContracts", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var bytes = new MemoryStream();
        var emit = compilation.Emit(bytes);
        Check(emit.Success, string.Join("\n", emit.Diagnostics));
        var assembly = Assembly.Load(bytes.ToArray());
        var optionType = assembly.GetType("MoatMove.MoatMoveOptions")!;
        object options = Activator.CreateInstance(optionType, nonPublic: true)!;
        foreach (var property in optionType.GetProperties()) Check(!property.CanWrite, "Mutable runtime setting: " + property.Name);
        Check((bool)optionType.GetProperty("EnableMod")!.GetValue(options)!, "Precise is not enabled");
        foreach (string property in new[] { "EnableImprovedMoatFilling", "EnableLadderAttackPathfindingFix", "EnableMoveFormationEnhancements" })
            Check(!(bool)optionType.GetProperty(property)!.GetValue(options)!, "Unrelated feature enabled: " + property);
        Check((int)optionType.GetProperty("FriendlyMoatMovementMode")!.GetValue(options)! == 1, "Wrong persisted mode mapping");
        const BindingFlags internalStatic = BindingFlags.Static | BindingFlags.NonPublic;
        const BindingFlags internalInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var snapshotType = assembly.GetType("MoatMove.MovementOptionsSnapshot")!;
        object snapshot = snapshotType.GetMethod("Capture", internalStatic)!.Invoke(null, new[] { options })!;
        Check((bool)snapshotType.GetProperty("Enabled", internalInstance)!.GetValue(snapshot)!, "Snapshot disabled");
        Check(!(bool)snapshotType.GetProperty("RequiredOnly", internalInstance)!.GetValue(snapshot)!, "Fast mode reachable");
        var conflict = assembly.GetType("MoatMove.MoatMoveConflictPolicy")!.GetMethod("FindConflict", internalStatic)!;
        foreach (string guid in new[] { "BugfixesAndQoL_Serp", "EnemyGatePathfindingTest_Serp" })
            Check((string)conflict.Invoke(null, new object[] { new[] { "000shcdese", "APIShared_Serp", guid } })! == guid, "Conflicting hook owner permitted");
        foreach (var guids in new[] { Array.Empty<string>(), new[] { "000shcdese" }, new[] { "000shcdese", "APIShared_Serp" } })
            Check(conflict.Invoke(null, new object[] { guids }) == null, "Standalone/APIShared configuration rejected");

        string plugin = File.ReadAllText(Path.Combine(sourceDir, "MoatMovePlugin.cs"));
        var pluginTree = CSharpSyntaxTree.ParseText(plugin);
        Check(!pluginTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call => call.Expression.ToString().EndsWith(".Dispose", StringComparison.Ordinal)), "Plugin tears down a process runtime");
        var libraryInit = pluginTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "OnLibraryLoaded").ToString();
        Check(libraryInit.IndexOf("ReportConflict()", StringComparison.Ordinal) < libraryInit.IndexOf("new FriendlyMoatMovementRuntime", StringComparison.Ordinal), "Conflict checked after hook installation");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(modDir, "info.json")));
        Check(manifest.RootElement.GetProperty("GUID").GetString() == "MoatMove_Serp" && manifest.RootElement.GetProperty("Version").GetString() == "0.1.0" && manifest.RootElement.GetProperty("NetworkMode").GetInt32() == 1, "Wrong plugin identity/network contract");
        Console.WriteLine("PASS: 22-source exact copy, immutable precise options, actual snapshot, unrelated-feature gates, conflict policy, process lifetime and manifest.");
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }
}
