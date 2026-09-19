using BepInEx;
using BepInEx.Bootstrap;
using SHCDESE.API.LowLevel;
using System;

namespace KeepFlagRotationTest
{
    [BepInDependency(ScriptExtenderGuid, "2.8.0")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class KeepFlagRotationTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "KeepFlagRotationTest_Serp";
        private const string PluginName = "Keep Flag Rotation Test";
        private const string PluginVersion = "0.1.0";
        private const string AuditedNativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const string AuditedScriptExtenderCommit = "5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33";
        private static KeepFlagRotationRuntime persistentRuntime;
        private static bool subscribed;
        private static bool initialized;

        private void Awake()
        {
            Logger.LogInfo(
                $"KFR_TEST_PLUGIN: version={PluginVersion}; alwaysActive=true; NetworkMode=1; settings=false; " +
                $"scriptExtender={LoadedPluginVersion(ScriptExtenderGuid)}; fixes={LoadedPluginVersion("fixes")}; " +
                $"castlePlanner={LoadedPluginVersion("CastlePlanner_Serp")}; auditedNativeSha256={AuditedNativeHash}; " +
                $"auditedScriptExtenderCommit={AuditedScriptExtenderCommit}.");

            if (!initialized && !subscribed)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
                subscribed = true;
            }
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (initialized)
                return;

            try
            {
                KeepFlagRotationRuntime runtime = new KeepFlagRotationRuntime(Logger);
                runtime.Install();
                persistentRuntime = runtime;
                initialized = true;
                CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
                subscribed = false;
                Logger.LogInfo(
                    $"KFR_TEST_READY: module=0x{context.ModuleHandle.ToInt64():X}; " +
                    "event subscriptions and delayed loaded-map scanner installed.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"KFR_TEST_INITIALIZATION_FAILED: {ex}");
            }
        }

        private static string LoadedPluginVersion(string guid) =>
            Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo plugin)
                ? plugin.Metadata.Version.ToString()
                : "not-loaded";
    }
}
