using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace ImprovedHunters
{
    [BepInDependency(ScriptExtenderGuid, "2.2.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ImprovedHuntersPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "ImprovedHunters_Serp";
        public const string PluginName = "Improved Hunters";
        public const string PluginVersion = "1.1.78";

        private static ImprovedHuntersRuntime persistentRuntime;
        private static ImprovedHuntersViewModel persistentSettings;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool runtimeDisposed;

        private bool applicationQuitting;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");

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

            Shared.DebugLogHelper.LogDebug(Logger, "Preserving persistent runtime across BepInEx manager destruction.");
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
            DisposeRuntime("OnApplicationQuit");
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            bool referenceHashMatches = false;
            try
            {
                referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    Logger,
                    PluginName,
                    requireCurrentVersion: false);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Improved Hunters native version diagnostics failed; signature-validated features may continue: {exception}");
            }

            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    persistentSettings,
                    "ScriptExtenderUI/ImprovedHuntersSettings.xaml");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Improved Hunters settings registration failed; gameplay runtime stopped fail-closed: {exception}");
                return;
            }

            try
            {
                persistentRuntime?.Apply(context, referenceHashMatches);
                Shared.DebugLogHelper.LogInfo(Logger, "Improved Hunters settings UI registered and runtime applied.");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Improved Hunters runtime initialization failed; successfully initialized independent features remain available: {exception}");
            }
        }

        private void DisposeRuntime(string reason)
        {
            if (runtimeDisposed)
                return;

            Shared.DebugLogHelper.LogInfo(Logger, $"Disposing runtime because of {reason}.");
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
