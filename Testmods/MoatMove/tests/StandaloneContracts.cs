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
        using var optimization = JsonDocument.Parse(File.ReadAllText(Path.Combine(modDir, "OPTIMIZATION_PROVENANCE.json")));
        using var fast = JsonDocument.Parse(File.ReadAllText(Path.Combine(modDir, "FAST_PROVENANCE.json")));
        using var se = JsonDocument.Parse(File.ReadAllText(Path.Combine(modDir, "SE26_PROVENANCE.json")));
        using var native = JsonDocument.Parse(File.ReadAllText(Path.Combine(modDir, "FAST_NATIVE_PROVENANCE.json")));
        using var current = JsonDocument.Parse(File.ReadAllText(Path.Combine(modDir, "CURRENT_SOURCE_PROVENANCE.json")));
        var currentChanges = current.RootElement.GetProperty("files").EnumerateArray().ToDictionary(
            item => item.GetProperty("file").GetString()!, item => item);
        Check(currentChanges.Keys.ToHashSet().SetEquals(new[] { "FastMovementScheduler.cs", "FriendlyMoatMovementRuntime.cs", "MoatMovePlugin.cs", "NativeMovementRecovery.cs", "MoatWorkTargetSelection.cs", "CursorConnectivity.cs" }),
            "Current source provenance scope changed");
        var nativeChanges = native.RootElement.GetProperty("files").EnumerateArray().ToDictionary(
            item => item.GetProperty("file").GetString()!, item => item.GetProperty("sha256").GetString()!);
        Check(nativeChanges.Keys.ToHashSet().SetEquals(new[] { "IFastRouteField.cs", "FastNativeKernel.cs", "FastNativeRouteField.cs", "FastRouteField.cs", "FastRoutePool.cs", "FastMoatRouting.cs", "FastIntegration.cs", "FastMovementScheduler.cs", "MoatMoveOptions.cs", "MoatMovePlugin.cs", "FriendlyMoatMovementRuntime.cs" }), "Native backend scope changed");
        foreach (var entry in nativeChanges)
        {
            byte[] source = currentChanges.ContainsKey(entry.Key)
                ? ReadHistoricalMoatSource(root, currentChanges[entry.Key].GetProperty("historicalCommit").GetString()!, entry.Key)
                : File.ReadAllBytes(Path.Combine(sourceDir, entry.Key));
            Check(Convert.ToHexString(SHA256.HashData(source)) == entry.Value,
                "Unreviewed historical FastNative source change: " + entry.Key);
        }
        foreach (var entry in currentChanges)
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(sourceDir, entry.Key)))) == entry.Value.GetProperty("sha256").GetString(),
                "Unreviewed current source change: " + entry.Key);
        var seChanges = se.RootElement.GetProperty("files").EnumerateArray().ToDictionary(
            item => item.GetProperty("file").GetString()!, item => item.GetProperty("sha256").GetString()!);
        Check(seChanges.Keys.ToHashSet().SetEquals(new[] { "AssassinSelectionAdapters.cs", "FriendlyMoatMovementRuntime.cs", "MoatMovePlugin.cs" }),
            "SE 2.6 change allowance expanded beyond adapters and plugin integration");
        foreach (var entry in seChanges.Where(entry => !nativeChanges.ContainsKey(entry.Key)))
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(sourceDir, entry.Key)))) == entry.Value,
                "Unreviewed SE 2.6 adapter change: " + entry.Key);
        var fastChanges = fast.RootElement.GetProperty("files").EnumerateArray().ToDictionary(
            item => item.GetProperty("file").GetString()!, item => item.GetProperty("sha256").GetString()!);
        var removed = fast.RootElement.GetProperty("deleted").EnumerateArray().Select(item => item.GetString()!).ToHashSet();
        Check(removed.SetEquals(new[] { "FastMoatBridge.cs" }), "Unexpected removed copy source");
        foreach (var entry in fastChanges.Where(entry => !seChanges.ContainsKey(entry.Key) && !nativeChanges.ContainsKey(entry.Key)))
        {
            byte[] source = currentChanges.ContainsKey(entry.Key)
                ? ReadHistoricalMoatSource(root, currentChanges[entry.Key].GetProperty("historicalCommit").GetString()!, entry.Key)
                : File.ReadAllBytes(Path.Combine(sourceDir, entry.Key));
            Check(Convert.ToHexString(SHA256.HashData(source)) == entry.Value,
                "Unreviewed historical Fast source change: " + entry.Key);
        }
        int count = 0;
        foreach (var item in provenance.RootElement.GetProperty("files").EnumerateArray())
        {
            string name = item.GetProperty("file").GetString()!;
            string copied = Path.Combine(sourceDir, name);
            byte[] original = ReadHistoricalSource(root, se.RootElement.GetProperty("originalSourceCommit").GetString()!, name);
            Check(Convert.ToHexString(SHA256.HashData(original)) == item.GetProperty("sourceSha256").GetString(), "Historical extraction hash mismatch: " + name);
            if (removed.Contains(name)) { Check(!File.Exists(copied), "Legacy Fast source retained"); count++; continue; }
            bool optimized = name == "MoatSearchKernel.cs";
            string expectedHash = currentChanges.TryGetValue(name, out JsonElement currentReviewed) ? currentReviewed.GetProperty("sha256").GetString()! : nativeChanges.TryGetValue(name, out string? nativeReviewed) ? nativeReviewed : seChanges.TryGetValue(name, out string? seReviewed) ? seReviewed : fastChanges.TryGetValue(name, out string? reviewed) ? reviewed :
                optimized ? optimization.RootElement.GetProperty("kernelSha256").GetString()! : item.GetProperty("copySha256").GetString()!;
            Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(copied))) == expectedHash, "Unreviewed source change: " + name);
            string expected = System.Text.Encoding.UTF8.GetString(original).TrimStart('\uFEFF').Replace("BugfixesAndQoLViewModel", "MoatMoveOptions")
                .Replace("BugfixesAndQoL", "MoatMove").Replace("Bugfixes and QoL", "MoatMove");
            if (currentChanges.ContainsKey(name) && !nativeChanges.ContainsKey(name) && !fastChanges.ContainsKey(name) && !seChanges.ContainsKey(name))
                Check(Convert.ToHexString(SHA256.HashData(ReadHistoricalMoatSource(root, currentChanges[name].GetProperty("historicalCommit").GetString()!, name))) == item.GetProperty("copySha256").GetString(),
                    "Unreviewed historical copied source change: " + name);
            if (!optimized && !fastChanges.ContainsKey(name) && !seChanges.ContainsKey(name) && !currentChanges.ContainsKey(name)) Check(expected == File.ReadAllText(copied), "Unexpected behavioral edit: " + name);
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
        Check(!(bool)snapshotType.GetProperty("RequiredOnly", internalInstance)!.GetValue(snapshot)!, "Default must remain precise");
        foreach (string configured in new[] { "precise", "fast", "FAST", "FastNative", "fastnative", "FASTNATIVE" })
        {
            object configuredOptions = optionType.GetConstructor(internalInstance, null, new[] { typeof(string) }, null)!.Invoke(new object[] { configured });
            object configuredSnapshot = snapshotType.GetMethod("Capture", internalStatic)!.Invoke(null, new[] { configuredOptions })!;
            Check((bool)snapshotType.GetProperty("Enabled", internalInstance)!.GetValue(configuredSnapshot)!, "Configured movement disabled");
            Check((bool)snapshotType.GetProperty("RequiredOnly", internalInstance)!.GetValue(configuredSnapshot)! == !configured.Equals("precise", StringComparison.OrdinalIgnoreCase), "Configured mode mapping");
            foreach (string property in new[] { "EnableImprovedMoatFilling", "EnableLadderAttackPathfindingFix", "EnableMoveFormationEnhancements" })
                Check(!(bool)optionType.GetProperty(property)!.GetValue(configuredOptions)!, "Config enables unrelated feature");
        }
        var conflict = assembly.GetType("MoatMove.MoatMoveConflictPolicy")!.GetMethod("FindConflict", internalStatic)!;
        foreach (string guid in new[] { "BugfixesAndQoL_Serp", "EnemyGatePathfindingTest_Serp" })
            Check((string)conflict.Invoke(null, new object[] { new[] { "000shcdese", "APIShared_Serp", guid } })! == guid, "Conflicting hook owner permitted");
        foreach (var guids in new[] { Array.Empty<string>(), new[] { "000shcdese" }, new[] { "000shcdese", "APIShared_Serp" } })
            Check(conflict.Invoke(null, new object[] { guids }) == null, "Standalone/APIShared configuration rejected");

        string plugin = File.ReadAllText(Path.Combine(sourceDir, "MoatMovePlugin.cs"));
        string cursor = File.ReadAllText(Path.Combine(sourceDir, "CursorConnectivity.cs"));
        Check(cursor.Contains("GamePlayerManagerAPI.Instance.GetSelectedChimps()") &&
            cursor.Contains("player >= 1 && player <= 8 && player != localPlayerId"),
            "Local selection must reject a different active player");
        var pluginTree = CSharpSyntaxTree.ParseText(plugin);
        Check(plugin.Contains("Config.Bind(\"Movement\", \"Mode\", \"precise\"") && plugin.Contains("FastNative:"), "Missing validated persistent mode config");
        Check(!pluginTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call => call.Expression.ToString().EndsWith(".Dispose", StringComparison.Ordinal)), "Plugin tears down a process runtime");
        var libraryInit = pluginTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "OnLibraryLoaded").ToString();
        Check(libraryInit.IndexOf("ReportConflict()", StringComparison.Ordinal) < libraryInit.IndexOf("new FriendlyMoatMovementRuntime", StringComparison.Ordinal), "Conflict checked after hook installation");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(modDir, "info.json")));
        string requiredExtender = manifest.RootElement.GetProperty("MinimumScriptExtenderVersion").GetString()!;
        string modVersion = manifest.RootElement.GetProperty("Version").GetString()!;
        Check(plugin.Contains("BepInDependency(\"000shcdese\", \"" + requiredExtender + "\")"), "Script Extender dependency mismatch");
        Check(manifest.RootElement.GetProperty("GUID").GetString() == "MoatMove_Serp" &&
            plugin.Contains("PluginVersion = \"" + modVersion + "\"") &&
            manifest.RootElement.GetProperty("NetworkMode").GetInt32() == 1, "Wrong plugin identity/network contract");
        Console.WriteLine("PASS: original source hashes, explicit Fast replacement inventory, pinned Precise kernel, startup config, unrelated-feature gates, conflicts, process lifetime and manifest.");
    }

    private static byte[] ReadHistoricalMoatSource(string root, string commit, string name)
    {
        return ReadGitSource(root, commit, "Testmods/MoatMove/src/" + name);
    }

    private static byte[] ReadHistoricalSource(string root, string commit, string name)
    {
        return ReadGitSource(root, commit, "BugfixesAndQoL/src/" + name);
    }

    private static byte[] ReadGitSource(string root, string commit, string path)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") {
            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("show"); start.ArgumentList.Add(commit + ":" + path);
        using var process = System.Diagnostics.Process.Start(start)!;
        using var output = new MemoryStream(); process.StandardOutput.BaseStream.CopyTo(output);
        string error = process.StandardError.ReadToEnd(); process.WaitForExit();
        Check(process.ExitCode == 0, "Missing historical extraction source: " + error);
        string text = System.Text.Encoding.UTF8.GetString(output.ToArray()).Replace("\r\n", "\n").Replace("\n", "\r\n");
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }
}
