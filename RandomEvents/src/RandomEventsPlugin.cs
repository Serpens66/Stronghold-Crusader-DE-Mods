using APIShared.ModSettings;
using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace RandomEvents
{
    [BepInDependency(ScriptExtenderGuid, "2.14.0")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("APIShared_Serp", "0.16.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class RandomEventsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "RandomEvents_Serp";
        public const string PluginName = "Random Events";
        public const string PluginVersion = "1.0.56";

        private RandomEventsRuntime runtime;

        public RandomEventsSettingsViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            Settings = new RandomEventsSettingsViewModel();
            runtime = new RandomEventsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            try
            {
                // Register unconditionally and before settings so every peer receives the same packet ID.
                runtime.InitializeNetwork();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/src/RandomEventsPlugin.cs:41", $"Random Events packet registration failed; synchronized events remain unavailable: {ex}");
            }

            try
            {
                Shared.DirectLaunchSettingsNotice.Configure(Settings, PluginGuid);
                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Shared.DebugLogHelper.ForExternalCode(Logger),
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/RandomEventsSettings.xaml",
                    logRoutineActivity: Shared.DebugLogHelper.DiagnosticsEnabled);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/src/RandomEventsPlugin.cs:56", $"Random Events settings registration failed; gameplay runtime stopped fail-closed: {ex}");
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
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/src/RandomEventsPlugin.cs:70", $"Random Events native version check failed; compatible managed features continue: {ex}");
            }

            try
            {
                runtime.InitializeNative(context.ModuleHandle, context.Memory, referenceHashMatches);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/src/RandomEventsPlugin.cs:79", $"Random Events native initialization stage failed; lifecycle initialization continues: {ex}");
            }

            try
            {
                runtime.Initialize();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Random Events initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "RandomEvents/src/RandomEventsPlugin.cs:89", $"Random Events lifecycle initialization failed; already initialized event types remain available: {ex}");
            }
        }

    }
}
