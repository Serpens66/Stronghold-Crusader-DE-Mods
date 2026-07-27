using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace AIDefense
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AIDefensePlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "AIDefense_Serp";
        public const string PluginName = "AI Defense";
        public const string PluginVersion = "1.2.3";

        private static AIDefenseRuntime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool runtimeDisposed;

        private bool applicationQuitting;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");

            if (persistentRuntime == null)
                persistentRuntime = new AIDefenseRuntime(Logger);

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
                "AIDefensePlugin OnDestroy called during BepInEx manager cleanup; preserving the process-lifetime runtime and Script Extender subscriptions.");
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
            DisposeRuntime("OnApplicationQuit");
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                persistentRuntime?.Apply();
                Shared.DebugLogHelper.LogInfo(Logger, "Crusader library loaded; AI Defense runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogInfo(Logger, $"AI Defense initialization failed: {ex}");
            }
        }

        private void DisposeRuntime(string reason)
        {
            if (runtimeDisposed)
                return;

            Shared.DebugLogHelper.LogInfo(Logger, $"Disposing AI Defense runtime because of {reason}.");

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
