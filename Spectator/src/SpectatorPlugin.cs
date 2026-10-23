using System;
using System.Threading;
using APIShared.ModSettings;
using BepInEx;
using CrusaderDE;
using SHCDESE.API.LowLevel;

namespace Spectator
{
    [BepInPlugin(PluginGuid, "Spectator", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.12.2")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("CheatMod_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ExtremePowers_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class SpectatorPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "Spectator_Serp";
        private static SpectatorPlugin processRoot;
        private static SpectatorStatistics statistics;
        private static SpectatorDefeat spectatorDefeat;
        private int initialized;
        public SpectatorViewModel Settings { get; private set; }

        private void Awake()
        {
            processRoot = this;
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            SerpLocalization.SetRoutineLoggingEnabled(false);
            Settings = new SpectatorViewModel();
            SpectatorRuntime.Initialize(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0) return;
            InitializeFeature("settings", () => LobbyModSettingsPresetRegistration.Register(this,
                Shared.DebugLogHelper.ForExternalCode(Logger), PluginGuid, Settings,
                "ScriptExtenderUI/SpectatorSettings.xaml", logRoutineActivity: false));
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
