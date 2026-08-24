using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace RandomEvents
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class RandomEventsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "RandomEvents_Serp";
        public const string PluginName = "Random Events";
        public const string PluginVersion = "1.0.26";

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
                // Register unconditionally and before settings so every peer receives the same packet ID.
                runtime.InitializeNetwork();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Random Events packet registration failed; synchronized events remain unavailable: {ex}");
            }

            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/RandomEventsSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Random Events settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return;
            }

            bool referenceHashMatches = false;
            try
            {
                referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    Logger,
                    "Random Events native integration",
                    requireCurrentVersion: false);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Random Events native version check failed; compatible managed features continue: {ex}");
            }

            try
            {
                runtime.InitializeNative(libraryHandle, memory, referenceHashMatches);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Random Events native initialization stage failed; lifecycle initialization continues: {ex}");
            }

            try
            {
                runtime.Initialize();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Random Events initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Random Events lifecycle initialization failed; already initialized event types remain available: {ex}");
            }
        }

    }
}
