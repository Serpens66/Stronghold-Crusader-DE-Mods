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

        private BuildingCostsRuntime runtime;
        private bool runtimeDisposed;

        public BuildingCostsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Logger.LogDebug($"{PluginName} {PluginVersion} loaded.");

            Settings = new BuildingCostsLobbyViewModel();
            runtime = new BuildingCostsRuntime(Logger, Settings);
            runtime.SubscribeHooks();
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Logger.LogDebug("BuildingCostsPlugin OnDestroy called; keeping runtime active until application quit.");
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            Logger.LogDebug("BuildingCostsPlugin OnApplicationQuit called; disposing runtime.");
            DisposeRuntime();
        }

        private void DisposeRuntime()
        {
            if (runtimeDisposed)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            runtime?.Dispose();
            runtimeDisposed = true;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                Settings.RefreshLocalizedNames();
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    "BuildingCosts",
                    Settings,
                    "ScriptExtenderUI/BuildingCostsSettings.xaml");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingCostsTooltipHost",
                    BuildingCostTooltipViewModel);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BuildingCostsTooltipHostCompact",
                    BuildingCostTooltipViewModel);

                Logger.LogDebug("Crusader library loaded; BuildingCosts UI registered.");
                runtime.InitializeAfterLibraryLoaded();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error while initializing BuildingCosts after library load: {ex}");
            }
        }
    }
}
