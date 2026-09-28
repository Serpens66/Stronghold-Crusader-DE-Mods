using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace ExpandedHealerTargetsTest
{
    [BepInDependency("000shcdese", "2.12.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class ExpandedHealerTargetsPlugin : BaseUnityPlugin
    {
        public const string Guid = "ExpandedHealerTargetsTest_Serp";
        public const string Name = "Expanded Healer Targets Test";
        public const string Version = "0.1.0";

        private static ExpandedHealerTargetsRuntime runtime;
        private static ManualLogSource log;
        private static ConfigEntry<bool> healSiege;
        private static ConfigEntry<bool> healCivilians;
        private static bool registered;

        private void Awake()
        {
            log = Logger;
            healSiege = Config.Bind("Targets", "HealSiegeEngines", true,
                "Allow Bedouin healers to heal owned siege engines. Takes effect on the next game launch.");
            healCivilians = Config.Bind("Targets", "HealHumanCivilians", true,
                "Allow Bedouin healers to heal owned human civilian units. Takes effect on the next game launch.");
            LogInfo("Loaded; waiting for the native library.");
            if (registered) return;
            // The publisher and the static runtime survive SHCDE's startup component cleanup.
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                runtime = ExpandedHealerTargetsRuntime.Install(context, log, healSiege, healCivilians);
            }
            catch (Exception ex)
            {
                LogError("Expanded healer targets remain disabled: " + ex);
            }
        }

        private static void LogInfo(string message) =>
            log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        private static void LogError(string message) =>
            log?.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
