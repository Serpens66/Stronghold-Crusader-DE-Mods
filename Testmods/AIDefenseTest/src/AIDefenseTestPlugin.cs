using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace AIDefenseTest
{
    [BepInDependency(ScriptExtenderGuid, "2.4.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AIDefenseTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "AIDefenseTest_Serp";
        public const string PluginName = "AI Defense Test";
        public const string PluginVersion = "1.2.10";

        private static AIDefenseTestRuntime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;
        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");

            if (persistentRuntime == null)
                persistentRuntime = new AIDefenseTestRuntime(Logger);

            if (!libraryLoadedSubscriptionInstalled)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
                libraryLoadedSubscriptionInstalled = true;
            }
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                "AIDefenseTestPlugin OnDestroy called during BepInEx manager cleanup; preserving the process-lifetime runtime and Script Extender subscriptions.");
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(
                        Logger,
                        PluginName,
                        requireCurrentVersion: true))
                {
                    return;
                }

                persistentRuntime?.Apply();
                Shared.DebugLogHelper.LogInfo(Logger, "Crusader library loaded; AI Defense Test runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"AI Defense Test initialization failed: {ex}");
            }
        }

    }
}
