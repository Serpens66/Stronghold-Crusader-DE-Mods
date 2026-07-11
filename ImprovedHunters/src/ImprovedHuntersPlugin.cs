using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace ImprovedHunters
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ImprovedHuntersPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "ImprovedHunters_Serp";
        public const string PluginName = "Improved Hunters";
        public const string PluginVersion = "1.1.15";

        private static ImprovedHuntersRuntime persistentRuntime;
        private static ImprovedHuntersViewModel persistentSettings;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool runtimeDisposed;

        private bool applicationQuitting;

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");

            if (persistentSettings == null)
                persistentSettings = new ImprovedHuntersViewModel();

            if (persistentRuntime == null)
                persistentRuntime = new ImprovedHuntersRuntime(Logger, persistentSettings);

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

            Logger.LogDebug("Preserving persistent runtime across BepInEx manager destruction.");
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
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    PluginGuid,
                    persistentSettings,
                    "ScriptExtenderUI/ImprovedHuntersSettings.xaml");

                persistentRuntime?.Apply(memory, (ulong)libraryHandle.ToInt64());
                Logger.LogInfo("Improved Hunters settings UI registered and runtime applied.");
            }
            catch (Exception exception)
            {
                Logger.LogError($"Error while initializing Improved Hunters after library load: {exception}");
            }
        }

        private void DisposeRuntime(string reason)
        {
            if (runtimeDisposed)
                return;

            Logger.LogInfo($"Disposing runtime because of {reason}.");
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
