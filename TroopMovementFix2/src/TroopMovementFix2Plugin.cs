using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace TroopMovementFix
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInIncompatibility(LegacyPluginGuid)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class TroopMovementFix2Plugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string LegacyPluginGuid = "TroopMovementFix_Serp";

        public const string PluginGuid = "TroopMovementFix2_Serp";
        public const string PluginName = "Troop Movement Fix 2";
        public const string PluginVersion = "1.0.3";

        private static TroopMovementFix2Runtime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool runtimeDisposed;

        private bool applicationQuitting;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");

            if (persistentRuntime == null)
                persistentRuntime = new TroopMovementFix2Runtime(Logger);

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
                "TroopMovementFix2Plugin OnDestroy called during BepInEx manager cleanup; preserving the process-lifetime runtime and native hooks.");
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
                Shared.DebugLogHelper.LogInfo(
                    Logger,
                    "Crusader library loaded; Troop Movement Fix 2 runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Troop Movement Fix 2 initialization failed; no partial runtime remains active: {ex}");
            }
        }

        private void DisposeRuntime(string reason)
        {
            if (runtimeDisposed)
                return;

            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"Disposing Troop Movement Fix 2 runtime because of {reason}.");

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
