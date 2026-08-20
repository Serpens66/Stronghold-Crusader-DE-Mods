using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace BuildingCosts
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BuildingCostsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "BuildingCosts_Serp";
        public const string PluginName = "Building Costs";
        public const string PluginVersion = "1.0.93";

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

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            // A late subscription can race with the regular event raise; initialize only once.
            if (Interlocked.Exchange(ref libraryInitializationStarted, 1) != 0)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;

            try
            {
                Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
                Settings.RefreshLocalizedNames();
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    "BuildingCosts_Serp",
                    Settings,
                    "ScriptExtenderUI/BuildingCostsSettings.xaml");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingCostsTooltipHost",
                    BuildingCostTooltipViewModel);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingCostsTooltipHostCompact",
                    BuildingCostTooltipViewModel);

                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; BuildingCosts UI registered.");
                runtime.InitializeAfterLibraryLoaded();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing BuildingCosts after library load: {ex}");
            }
        }
    }
}
