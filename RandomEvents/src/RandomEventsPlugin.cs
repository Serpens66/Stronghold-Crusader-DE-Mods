using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace RandomEvents
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class RandomEventsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "RandomEvents_Serp";
        public const string PluginName = "Random Events";
        public const string PluginVersion = "1.0.18";

        private RandomEventsRuntime runtime;
        private bool disposed;

        public RandomEventsSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");
            Settings = new RandomEventsSettingsViewModel();
            runtime = new RandomEventsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/RandomEventsSettings.xaml");

                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    Logger,
                    "Random Events native integration",
                    requireCurrentVersion: false);
                runtime.InitializeNative(libraryHandle, memory, referenceHashMatches);
                runtime.Initialize();
                Shared.DebugLogHelper.LogInfo(Logger, "Crusader library loaded; Random Events initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Random Events initialization failed: {ex}");
            }
        }

        // The plugin component is destroyed during startup; process quit is the safe cleanup point.
        private void OnApplicationQuit()
        {
            if (disposed) return;
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            runtime?.Dispose();
            disposed = true;
        }
    }
}
