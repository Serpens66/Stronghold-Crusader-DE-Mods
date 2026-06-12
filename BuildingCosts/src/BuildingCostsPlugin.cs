using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace BuildingCosts
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BuildingCostsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "BuildingCosts";
        public const string PluginName = "Building Costs";
        public const string PluginVersion = "0.1.0";

        internal static readonly BuildingCostTooltipViewModel BuildingCostTooltipViewModel = new BuildingCostTooltipViewModel();
        internal static readonly BuildingCostMissingNotificationViewModel BuildingCostMissingNotificationViewModel = new BuildingCostMissingNotificationViewModel();

        private BuildingCostsRuntime runtime;

        public BuildingCostsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");

            Settings = new BuildingCostsLobbyViewModel();
            runtime = new BuildingCostsRuntime(Logger, Settings);
            try
            {
                runtime.SubscribeHooks();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to install BuildingCosts hooks: {ex}");
            }

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void Update()
        {
            BuildingCostMissingNotificationViewModel.Update();
        }

        private void OnDestroy()
        {
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            runtime?.Dispose();
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding("BuildingCostsTooltipHost", BuildingCostTooltipViewModel);
                GameXAMLManagerAPI.Instance.RegisterBinding("BuildingCostsTooltipHostCompact", BuildingCostTooltipViewModel);
                GameXAMLManagerAPI.Instance.RegisterBinding("BuildingCostsMissingNotificationOverlay", BuildingCostMissingNotificationViewModel);
                Settings.RefreshLocalizedNames();
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    "BuildingCosts",
                    Settings,
                    "ScriptExtenderUI/BuildingCostsSettings.xaml");
                runtime.InitializeAfterLibraryLoaded();
                Logger.LogInfo("BuildingCosts XAML bindings registered.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to register BuildingCosts XAML bindings: {ex}");
            }
        }
    }
}
