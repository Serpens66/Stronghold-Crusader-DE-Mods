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
        public const string PluginVersion = "1.0.9";

        private RandomEventsRuntime runtime;

        public RandomEventsSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            Settings = new RandomEventsSettingsViewModel();
            runtime = new RandomEventsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/RandomEventsSettings.xaml");

                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    Logger,
                    "Random Events native integration",
                    requireCurrentVersion: false);
                runtime.InitializeNative(libraryHandle, memory, referenceHashMatches);
                runtime.Initialize();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Random Events initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Random Events initialization failed: {ex}");
            }
        }

    }
}
