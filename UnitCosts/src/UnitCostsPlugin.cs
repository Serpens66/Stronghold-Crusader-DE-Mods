using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace UnitCosts
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(UnitLimitGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UnitCostsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string UnitLimitGuid = "UnitLimit_Serp";

        public const string PluginGuid = "UnitCosts_Serp";
        public const string PluginName = "Unit Costs";
        public const string PluginVersion = "1.0.5";

        private UnitCostsRuntime runtime;
        private bool runtimeDisposed;

        public UnitCostsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

            Settings = new UnitCostsLobbyViewModel();
            runtime = new UnitCostsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogDebug(Logger, "UnitCostsPlugin OnDestroy called; keeping runtime active until application quit.");
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            Shared.DebugLogHelper.LogDebug(Logger, "UnitCostsPlugin OnApplicationQuit called; disposing runtime.");
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
                runtime.InitializeAfterLibraryLoaded();
                Settings.RefreshLocalizedNames();
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    "UnitCosts_Serp",
                    Settings,
                    "ScriptExtenderUI/UnitCostsSettings.xaml");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitCostsNotificationOverlay",
                    runtime.Notification);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitCostsSiegeNotificationInlineHost",
                    runtime.Notification);
                RegisterRecruitmentCostTooltipBindings();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; UnitCosts UI registered.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing UnitCosts after library load: {ex}");
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
