using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using SerpNativeAPI;
using System;

namespace APITest
{
    [BepInDependency(ScriptExtenderGuid, "2.0.2")]
    [BepInDependency(ApiGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInIncompatibility(BugfixesGuid)]
    [BepInIncompatibility(ExtraFeaturesGuid)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class APITestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ApiGuid = "SerpNativeAPI_Serp";
        private const string BugfixesGuid = "BugfixesAndQoL_Serp";
        private const string ExtraFeaturesGuid = "ExtraFeatures_Serp";
        public const string PluginGuid = "APITest_Serp";
        public const string PluginName = "Serp Native API Test";
        public const string PluginVersion = "0.1.0";

        private static ManualLogSource rootedLog;
        private static ISelectedUnitCommandRegistration selectedRegistration;
        private static AssassinClimbCancellationTest assassinTest;
        private static bool blocked;

        private void Awake()
        {
            rootedLog = Logger;
            Log($"{PluginName} {PluginVersion} loaded; gatehouse and Assassin tests are fixed-enabled.");
            blocked = Chainloader.PluginInfos.ContainsKey(BugfixesGuid) ||
                Chainloader.PluginInfos.ContainsKey(ExtraFeaturesGuid);
            if (blocked)
            {
                Log($"APITEST_BLOCKED: incompatible main mod detected; bugfixesLoaded={Chainloader.PluginInfos.ContainsKey(BugfixesGuid)}, extraFeaturesLoaded={Chainloader.PluginInfos.ContainsKey(ExtraFeaturesGuid)}. No API capability will be acquired.");
                return;
            }

            assassinTest = new AssassinClimbCancellationTest(Logger);
            SerpNativeApi.WhenReady(OnApiReady);
        }

        private static void OnApiReady(ISerpNativeApi api)
        {
            if (blocked)
                return;
            Log($"API_READY: state={api.State}.");
            ApplyGatehouse(api);
            RegisterAssassin(api);
        }

        private static void ApplyGatehouse(ISerpNativeApi api)
        {
            if (!api.TryGetGatehouseTiming(PluginGuid, out IGatehouseTimingCapability capability, out NativeCapabilityDiagnostic diagnostic))
            {
                LogDiagnostic("GATEHOUSE_UNAVAILABLE", diagnostic);
                return;
            }
            LogDiagnostic("GATEHOUSE_ACQUIRED", diagnostic);
            var settings = new GatehouseTimingSettings(true, 0.0, 0.0, 5.0, 5.0);
            if (!capability.TryApply(settings, out diagnostic))
            {
                LogDiagnostic("GATEHOUSE_APPLY_FAILED", diagnostic);
                return;
            }
            LogDiagnostic("GATEHOUSE_APPLIED", diagnostic);
            Log("GATEHOUSE_TEST_ACTIVE: humanReopen=0s/0ticks, aiReopen=0s/0ticks, humanClose=5tiles/40units, aiClose=5tiles/40units.");
        }

        private static void RegisterAssassin(ISerpNativeApi api)
        {
            if (!api.TryGetSelectedUnitCommand(PluginGuid, out ISelectedUnitCommandCapability capability, out NativeCapabilityDiagnostic diagnostic))
            {
                LogDiagnostic("ASSASSIN_HOOK_UNAVAILABLE", diagnostic);
                return;
            }
            LogDiagnostic("ASSASSIN_HOOK_ACQUIRED", diagnostic);
            if (!capability.TryRegisterBefore(assassinTest.OnSelectedUnitCommand, out selectedRegistration, out diagnostic))
            {
                LogDiagnostic("ASSASSIN_HOOK_REGISTER_FAILED", diagnostic);
                return;
            }
            LogDiagnostic("ASSASSIN_HOOK_REGISTERED", diagnostic);
        }

        private static void LogDiagnostic(string marker, NativeCapabilityDiagnostic diagnostic)
        {
            if (diagnostic == null)
            {
                Log(marker + ": diagnostic=null.");
                return;
            }
            Log($"{marker}: capability={diagnostic.CapabilityId}, state={diagnostic.State}, build={diagnostic.BinaryHash}, conflictOwner={diagnostic.ConflictOwnerGuid ?? "none"}, reason={diagnostic.Reason}");
        }

        internal static void Log(string message) =>
            rootedLog?.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
