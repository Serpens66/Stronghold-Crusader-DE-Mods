using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace UnitCosts
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UnitCostsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "UnitCosts";
        public const string PluginName = "Unit Costs";
        public const string PluginVersion = "0.1.0";

        private UnitCostsRuntime runtime;
        private bool runtimeDisposed;

        public UnitCostsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");

            Settings = new UnitCostsLobbyViewModel();
            runtime = new UnitCostsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Logger.LogInfo("UnitCostsPlugin OnDestroy called; keeping runtime active until application quit.");
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            Logger.LogInfo("UnitCostsPlugin OnApplicationQuit called; disposing runtime.");
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
                    "UnitCosts",
                    Settings,
                    "ScriptExtenderUI/UnitCostsSettings.xaml");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitCostsNotificationOverlay",
                    runtime.Notification);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitCostsSiegeNotificationInlineHost",
                    runtime.Notification);
                RegisterRecruitmentCostTooltipBindings();

                runtime.InitializeAfterLibraryLoaded();
                Logger.LogInfo("Crusader library loaded; UnitCosts UI registered.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error while initializing UnitCosts after library load: {ex}");
            }
        }

        private void RegisterRecruitmentCostTooltipBindings()
        {
            string[] bindingTargets =
            {
                "UnitCostsTroopCostsInlineHost",
                "UnitCostsArabTroopCostsInlineHost",
                "UnitCostsBedouinTroopCostsInlineHost",
                "UnitCostsEngineersCostsInlineHost",
                "UnitCostsTunellersCostsInlineHost",
                "UnitCostsMonkCostsInlineHost",
                "UnitCostsSiegeBuildCostsInlineHost",
            };

            foreach (string bindingTarget in bindingTargets)
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    bindingTarget,
                    runtime.RecruitmentCostTooltip);
            }
        }
    }
}
