using System;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace BugfixesAndQoL.Multiplayer.SaveLoading
{
    // All published hooks and registrations outlive the plugin's startup destruction.
    internal static class IngameSaveLoadFeature
    {
        internal const string LegacyGuid = "MultiplayerRestartTest_Serp";
        internal static SaveLoadRuntime Runtime { get; private set; }

        internal static bool HasLegacyPlugin()
        {
            if (Chainloader.PluginInfos.ContainsKey(LegacyGuid)) return true;
            // The old testmod depends on BQoL and may not have reached Awake yet.
            // Reuse BepInEx's metadata discovery/cache, without loading plugin assemblies.
            return TypeLoader.FindPluginTypes<PluginInfo>(Paths.PluginPath,
                    Chainloader.ToPluginInfo, null, "chainloader")
                .Values.SelectMany(plugins => plugins).Any(plugin =>
                    string.Equals(plugin.Metadata.GUID, LegacyGuid, StringComparison.Ordinal));
        }

        internal static void InitializeEarly(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            if (Runtime != null) return;
            try
            {
                if (HasLegacyPlugin())
                {
                    Shared.DebugLogHelper.LogWarningLimited(log, "ingame-save-load-legacy",
                        "Integrated multiplayer Load is inactive: remove MultiplayerRestartTest_Serp before using it.");
                    return;
                }
                // Install even when disabled: a later preference change must never patch
                // an already running simulation thread. Request/Release remain opt-in.
                var candidate = new SaveLoadRuntime(log, () => settings.EnableIngameMultiplayerSaveLoading);
                LoadBarrier.InstallEarly(log);
                Runtime = candidate;
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ingame-save-load-early", error.ToString());
            }
        }

        internal static void Initialize(ManualLogSource log)
        {
            try { Runtime?.Initialize(); }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ingame-save-load-initialize", error.ToString());
            }
        }
    }
}
