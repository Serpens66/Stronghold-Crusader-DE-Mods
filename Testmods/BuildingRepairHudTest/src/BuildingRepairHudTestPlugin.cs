using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;

namespace BuildingRepairHudTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class BuildingRepairHudTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "BuildingRepairHudTest_Serp";
        public const string Name = "Building Repair HUD Test";
        public const string Version = "0.1.0";

        private static RepairHudRuntime runtime;
        private static ManualLogSource log;
        private static bool registered;

        private void Awake()
        {
            log = Logger;
            LogInfo("Loaded; waiting for the native library.");
            if (registered) return;
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
            registered = true;
        }

        private static void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null) return;
            try
            {
                // Static ownership survives SHCDE's normal destruction of the plugin component.
                runtime = RepairHudRuntime.Install(context);
                LogInfo("Permanent repair HUD hooks installed; waiting for post-startup HUD activity.");
            }
            catch (Exception ex)
            {
                LogError("Repair HUD remains disabled: " + ex);
            }
        }

        internal static void LogInfo(string message) =>
            log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        internal static void LogError(string message) =>
            log?.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
