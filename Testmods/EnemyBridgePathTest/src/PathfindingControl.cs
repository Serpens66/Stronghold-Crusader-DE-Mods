using BepInEx.Bootstrap;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace EnemyBridgePathTest
{
    // Selects the scenario mode through Pathfinding's public settings (as the other test mods
    // do) and reads the closure state and counters of Pathfinding's internal
    // GateRouting.DrawbridgeDeckClosure by reflection. Changed settings are restored when the
    // run ends, so the player's own choice survives an automated run.
    internal static class PathfindingControl
    {
        private const string PluginGuid = "Pathfinding_Serp", ClosureType = "Pathfinding.GateRouting.DrawbridgeDeckClosure";
        private static readonly string[] Required = { "EnableMod", "EnableEnemyGatePathfindingFix" };
        private const string Feature = "EnableEnemyDrawbridgeClosure";
        private static readonly Dictionary<string, bool> originals = new Dictionary<string, bool>(StringComparer.Ordinal);
        private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        // Reference comparison: the SHCDE startup cleanup destroys the plugin component, so
        // Unity's overloaded == reports it as null although the managed object stays usable.
        private static object Plugin() =>
            Chainloader.PluginInfos.TryGetValue(PluginGuid, out var info) && (object)info.Instance != null
                ? info.Instance : throw new InvalidOperationException("Pathfinding is not loaded.");

        private static object Settings()
        {
            object plugin = Plugin();
            return plugin.GetType().GetProperty("Settings")?.GetValue(plugin) ?? throw new MissingMemberException("Pathfinding settings unavailable.");
        }

        private static PropertyInfo Property(object settings, string name) =>
            settings.GetType().GetProperty(name) ?? throw new MissingMemberException("Pathfinding setting " + name);

        private static bool Get(object settings, string name) => (bool)Property(settings, name).GetValue(settings);

        private static void Set(object settings, string name, bool value, Action<string> log)
        {
            bool current = Get(settings, name);
            if (current == value) return;
            if (!originals.ContainsKey(name)) originals[name] = current;
            Property(settings, name).SetValue(settings, value);
            if (Get(settings, name) != value) throw new InvalidOperationException("Pathfinding rejected setting " + name + "=" + value);
            log("SETTING " + name + "=" + value + " (was " + current + ")");
        }

        internal static void Configure(bool closure, Action<string> log)
        {
            object settings = Settings();
            foreach (string name in Required) Set(settings, name, true, log);
            Set(settings, Feature, closure, log);
        }

        internal static void RestoreSettings(Action<string> log)
        {
            try
            {
                object settings = Settings();
                foreach (var pair in originals) Property(settings, pair.Key).SetValue(settings, pair.Value);
                log("SETTINGS restored " + originals.Count + " value(s)");
                originals.Clear();
            }
            catch (Exception error) { log("SETTINGS restore failed: " + error.Message); }
        }

        private static Type Closure() =>
            Plugin().GetType().Assembly.GetType(ClosureType) ?? throw new MissingMemberException(ClosureType);

        internal static bool IsActive() =>
            (bool)(Closure().GetProperty("IsActive", StaticMembers) ?? throw new MissingMemberException("DrawbridgeDeckClosure.IsActive")).GetValue(null);

        internal static string Summary()
        {
            try
            {
                Type type = Closure();
                object instance = type.GetField("instance", StaticMembers)?.GetValue(null);
                if (instance == null) return "closure not installed";
                return (string)type.GetMethod("Summary", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
            }
            catch (Exception error) { return "summary unavailable: " + error.Message; }
        }

        internal static string Describe()
        {
            object settings = Settings();
            return "EnableMod=" + Get(settings, "EnableMod") + " EnableEnemyGatePathfindingFix=" + Get(settings, "EnableEnemyGatePathfindingFix") +
                " " + Feature + "=" + Get(settings, Feature) + " pathfindingVersion=" + Chainloader.PluginInfos[PluginGuid].Metadata.Version;
        }
    }
}
