using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace TroopMovementFix
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class TroopMovementFixPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "TroopMovementFix_Serp";
        public const string PluginName = "Troop Movement Fix";
        public const string PluginVersion = "1.0.18";

        private static TroopMovementFixRuntime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool runtimeDisposed;

        private bool applicationQuitting;

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

            if (persistentRuntime == null)
                persistentRuntime = new TroopMovementFixRuntime(Logger);

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

            Shared.DebugLogHelper.LogDebug(
                Logger,
                "TroopMovementFixPlugin OnDestroy called during BepInEx manager cleanup; preserving the process-lifetime runtime and native hook.");
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
                persistentRuntime?.Apply(libraryHandle, memory);
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Troop Movement Fix runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Troop Movement Fix initialization failed: {ex}");
            }
        }

        private void DisposeRuntime(string reason)
        {
            if (runtimeDisposed)
                return;

            Shared.DebugLogHelper.LogDebug(Logger, $"Disposing Troop Movement Fix runtime because of {reason}.");

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
