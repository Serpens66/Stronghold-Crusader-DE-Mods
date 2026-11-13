using System;
using System.Threading;
using APIShared.ModSettings;
using BepInEx;
using CrusaderDE;
using SHCDESE.API.LowLevel;

namespace Spectator
{
    [BepInPlugin(PluginGuid, "Spectator", PluginVersion)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.49.0")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("CheatMod_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ExtremePowers_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed partial class SpectatorPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Spectator_Serp";
        public const string PluginVersion = "0.2.4";
        private static SpectatorPlugin processRoot;
        private static SpectatorStatistics statistics;
        private static SpectatorDefeat spectatorDefeat;
        private int initialized;
        public SpectatorViewModel Settings { get; private set; }

        private void Awake()
        {
            processRoot = this;
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            SerpLocalization.SetRoutineLoggingEnabled(false);
            ToolbarThemePreference.Initialize(Config);
            SpectatorRuntime.ConfigureHiddenDate(Config);
            Settings = new SpectatorViewModel();
            SpectatorRuntime.Initialize(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0) return;
            InitializeFeature("legacy-settings", PrepareLegacySettings);
            InitializeFeature("settings", () => {
                if (legacySettings?.RegistrationSafe == false)
                    throw new InvalidOperationException("Settings registration deferred: the initial settings/migration journal could not be read or saved safely. Retry after fixing the logged file error.");
                LobbyModSettingsPresetRegistration.Register(this,
                    Shared.DebugLogHelper.ForExternalCode(Logger), PluginGuid, Settings,
                    "ScriptExtenderUI/SpectatorSettings.xaml", logRoutineActivity: false);
                settingsRegistered = true;
                TryMigrateLegacySettings();
            });
            InitializeFeature("overview", () => LordOverview.OverviewRuntime.Start(Logger, Settings.Overview));
            InitializeFeature("perspective", () => SpectatorPerspectiveRuntime.Initialize(Logger, Settings));
            InitializeFeature("statistics", () => {
                statistics = new SpectatorStatistics(Logger, Settings);
                statistics.Initialize();
                SpectatorRuntime.Statistics = statistics;
            });
            InitializeFeature("defeat", () => {
                spectatorDefeat = new SpectatorDefeat(Logger, Settings);
                spectatorDefeat.Initialize();
            });
        }
        private void InitializeFeature(string name, Action initialize)
        {
            try { initialize(); }
            catch (Exception error) { Shared.DebugLogHelper.LogError(Logger, "SPECTATOR_" + name + "_INITIALIZATION_FAILED: " + error); }
        }
    }
}
