using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace UnitCosts
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(UnitLimitGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UnitCostsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string UnitLimitGuid = "UnitLimit_Serp";

        public const string PluginGuid = "UnitCosts_Serp";
        public const string PluginName = "Unit Costs";
        public const string PluginVersion = "1.0.14";

        private UnitCostsRuntime runtime;
        private int libraryInitializationStarted;

        public UnitCostsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

            Settings = new UnitCostsLobbyViewModel();
            runtime = new UnitCostsRuntime(Logger, Settings);
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
                runtime.InitializeAfterLibraryLoaded();
                Settings.RefreshLocalizedNames();
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
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
