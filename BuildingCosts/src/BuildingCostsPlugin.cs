using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace BuildingCosts
{
    [BepInDependency(ScriptExtenderGuid, "2.0.2")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BuildingCostsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "BuildingCosts_Serp";
        public const string PluginName = "Building Costs";
        public const string PluginVersion = "1.0.100";

        internal static readonly BuildingCostTooltipViewModel BuildingCostTooltipViewModel = new BuildingCostTooltipViewModel();

        private BuildingCostsRuntime runtime;
        private int libraryInitializationStarted;

        public BuildingCostsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

            Settings = new BuildingCostsLobbyViewModel();
            runtime = new BuildingCostsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            // A late subscription can race with the regular event raise; initialize only once.
            if (Interlocked.Exchange(ref libraryInitializationStarted, 1) != 0)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;

            TryInitializeStage("native version diagnostics", () => Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName));
            TryInitializeStage("localized names", Settings.RefreshLocalizedNames);
            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    "BuildingCosts_Serp",
                    Settings,
                    "ScriptExtenderUI/BuildingCostsSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"BuildingCosts settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return;
            }

            TryInitializeStage("detailed tooltip binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingCostsTooltipHost",
                    BuildingCostTooltipViewModel));
            TryInitializeStage("compact tooltip binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingCostsTooltipHostCompact",
                    BuildingCostTooltipViewModel));

            try
            {
                runtime.InitializeAfterLibraryLoaded();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; BuildingCosts runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing BuildingCosts after library load: {ex}");
            }
        }

        private void TryInitializeStage(string stageName, Action initialize)
        {
            try { initialize(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"BuildingCosts {stageName} failed; independent stages continue: {ex}");
            }
        }
    }
}
