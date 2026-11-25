using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BugfixesAndQoL.Multiplayer.SaveLoading;
using Mono.Cecil;
using MonoMod.Utils;

internal static class FeatureGateTests
{
    private static readonly Dictionary<string, PluginInfo> loaded = new Dictionary<string, PluginInfo>();
    private static readonly Dictionary<string, List<PluginInfo>> discovered = new Dictionary<string, List<PluginInfo>>();
    private static Dictionary<string, PluginInfo> Loaded() => loaded;
    private static string PluginPath() => "fixture";
    private static Dictionary<string, List<PluginInfo>> Discover(string path, Func<TypeDefinition, PluginInfo> selector,
        Func<AssemblyDefinition, bool> filter, string cache) => discovered;
    private static PluginInfo Plugin(string guid)
    {
        var value = new PluginInfo();
        typeof(PluginInfo).GetProperty("Metadata").SetValue(value, new BepInPlugin(guid, "fixture", "1.0.0"), null);
        return value;
    }
    internal static void Run()
    {
        var target = typeof(IngameSaveLoadFeature).GetMethod("HasLegacyPlugin", BindingFlags.Static | BindingFlags.NonPublic);
        using (var fixture = new DynamicMethodDefinition(target))
        {
            foreach (var instruction in fixture.Definition.Body.Instructions)
            {
                if (!(instruction.Operand is MethodReference method)) continue;
                string replacement = method.DeclaringType.Name == "Chainloader" && method.Name == "get_PluginInfos" ? nameof(Loaded) :
                    method.DeclaringType.Name == "Paths" && method.Name == "get_PluginPath" ? nameof(PluginPath) :
                    method.DeclaringType.Name == "TypeLoader" && method.Name == "FindPluginTypes" ? nameof(Discover) : null;
                if (replacement != null) instruction.Operand = fixture.Definition.Module.ImportReference(
                    typeof(FeatureGateTests).GetMethod(replacement, BindingFlags.Static | BindingFlags.NonPublic));
            }
            var generated = fixture.Generate();
            Func<bool> run = () => (bool)generated.Invoke(null, null);
            if (run()) throw new Exception("Legacy-free installation was blocked.");
            discovered.Add("unrelated.dll", new List<PluginInfo> { Plugin("other-mod") });
            if (run()) throw new Exception("Unrelated plugin blocked integrated loading.");
            discovered.Add("renamed.dll", new List<PluginInfo> { Plugin(IngameSaveLoadFeature.LegacyGuid) });
            if (!run()) throw new Exception("A not-yet-loaded legacy plugin was missed.");
            discovered.Clear(); loaded.Add(IngameSaveLoadFeature.LegacyGuid, Plugin(IngameSaveLoadFeature.LegacyGuid));
            if (!run()) throw new Exception("An already loaded legacy plugin was missed.");
            loaded.Clear();
        }
        Console.WriteLine("Migration feature gates: 4 checks passed.");
    }
}
