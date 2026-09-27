using BepInEx;
using BepInEx.Logging;
using APIShared;
using System;

namespace BuildingRepairHudTest
{
    [BepInDependency("000shcdese", "2.11.0")]
    [BepInDependency("APIShared_Serp", "0.4.3")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class BuildingRepairHudTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "BuildingRepairHudTest_Serp";
        public const string Name = "Building Repair HUD Test";
        public const string Version = "0.1.1";

        private static RepairHudRuntime runtime;
        private static ManualLogSource log;
        private static bool registered;

        private void Awake()
        {
            log = Logger;
            LogInfo("Loaded; waiting for the shared building repair capability.");
            if (registered) return;
            ApiShared.WhenReady(OnApiReady);
            registered = true;
        }

        private static void OnApiReady(IApiShared api)
        {
            if (runtime != null) return;
            IBuildingRepairCapability repair = null;
            try
            {
                if (!api.TryGetBuildingRepair(Guid, out repair,
                    out NativeCapabilityDiagnostic diagnostic))
                {
                    LogError("Shared building repair unavailable: " + diagnostic?.Reason);
                    return;
                }
                runtime = RepairHudRuntime.Install(repair);
                LogInfo("Permanent repair HUD hooks installed; waiting for post-startup HUD activity.");
            }
            catch (Exception ex)
            {
                repair?.SetActive(false);
                LogError("Repair HUD remains disabled: " + ex);
            }
        }

        internal static void LogInfo(string message) =>
            log?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        internal static void LogError(string message) =>
            log?.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
