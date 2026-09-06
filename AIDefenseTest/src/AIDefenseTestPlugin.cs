using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace AIDefenseTest
{
    [BepInDependency(ScriptExtenderGuid, "2.2.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AIDefenseTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "AIDefenseTest_Serp";
        public const string PluginName = "AI Defense Test";
        public const string PluginVersion = "1.2.7";

        private static AIDefenseTestRuntime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool runtimeDisposed;

        private bool applicationQuitting;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");

            if (persistentRuntime == null)
                persistentRuntime = new AIDefenseTestRuntime(Logger);

            runtimeDisposed = false;

            if (!libraryLoadedSubscriptionInstalled)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
                libraryLoadedSubscriptionInstalled = true;
            }
        }

        private void OnDestroy()
        {
            if (applicationQuitting)
            {
                DisposeRuntime("OnDestroy during application quit");
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                Logger,
                "AIDefenseTestPlugin OnDestroy called during BepInEx manager cleanup; preserving the process-lifetime runtime and Script Extender subscriptions.");
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
            DisposeRuntime("OnApplicationQuit");
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

        private void DisposeRuntime(string reason)
        {
            if (runtimeDisposed)
                return;

            Shared.DebugLogHelper.LogInfo(Logger, $"Disposing AI Defense Test runtime because of {reason}.");

            if (libraryLoadedSubscriptionInstalled)
            {
                CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
                libraryLoadedSubscriptionInstalled = false;
            }

            persistentRuntime?.Dispose();
            persistentRuntime = null;
            runtimeDisposed = true;
        }
    }
}
