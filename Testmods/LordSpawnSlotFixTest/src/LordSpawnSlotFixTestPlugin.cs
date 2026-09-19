using BepInEx;
using BepInEx.Bootstrap;
using SHCDESE.API.LowLevel;
using System;

namespace LordSpawnSlotFixTest
{
    [BepInDependency(ScriptExtenderGuid, "2.8.0")]
    [BepInDependency(ApiSharedGuid, "0.3.7")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class LordSpawnSlotFixTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ApiSharedGuid = "APIShared_Serp";
        private const string PluginGuid = "LordSpawnSlotFixTest_Serp";
        private const string PluginName = "Lord Spawn Slot Fix Test";
        private const string PluginVersion = "0.1.0";
        private const string AuditedNativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const string AuditedScriptExtenderCommit = "5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33";
        private static LordSpawnSlotFixTestRuntime persistentRuntime;
        private static bool subscribed;
        private static bool initialized;

        private void Awake()
        {
            Logger.LogInfo(
                $"LSS_TEST_PLUGIN: version={PluginVersion}; activeCorrection=true; NetworkMode=1; settings=false; " +
                $"scriptExtender={LoadedPluginVersion(ScriptExtenderGuid)}; apiShared={LoadedPluginVersion(ApiSharedGuid)}; " +
                $"fixes={LoadedPluginVersion("fixes")}; auditedNativeSha256={AuditedNativeHash}; " +
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
                LordSpawnSlotFixTestRuntime candidate = new LordSpawnSlotFixTestRuntime(Logger);
                candidate.Install();
                persistentRuntime = candidate;
                initialized = true;
                CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
                subscribed = false;
                Logger.LogInfo(
                    $"LSS_TEST_READY: module=0x{context.ModuleHandle.ToInt64():X}; " +
                    "observation=APIShared mission lifecycle plus GameTimeManagerAPI.OnTick; nativeHooks=0.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"LSS_TEST_INITIALIZATION_FAILED: {ex}");
            }
        }

        private static string LoadedPluginVersion(string guid) =>
            Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo plugin)
                ? plugin.Metadata.Version.ToString()
                : "not-loaded";
    }
}
