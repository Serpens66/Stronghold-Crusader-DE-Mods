using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace UnitCosts
{
    [BepInDependency(ScriptExtenderGuid, "2.2.0")]
    [BepInDependency(UnitLimitGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UnitCostsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string UnitLimitGuid = "UnitLimit_Serp";

        public const string PluginGuid = "UnitCosts_Serp";
        public const string PluginName = "Unit Costs";
        public const string PluginVersion = "1.0.22";

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
                    "UnitCosts_Serp",
                    Settings,
                    "ScriptExtenderUI/UnitCostsSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"UnitCosts settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return;
            }

            TryInitializeStage("notification overlay binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitCostsNotificationOverlay",
                    runtime.Notification));
            TryInitializeStage("siege notification binding", () => GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitCostsSiegeNotificationInlineHost",
                    runtime.Notification));
            TryInitializeStage("recruitment tooltip bindings", RegisterRecruitmentCostTooltipBindings);

            try
            {
                runtime.InitializeAfterLibraryLoaded();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; UnitCosts runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing UnitCosts after library load: {ex}");
            }
        }

        private void TryInitializeStage(string stageName, Action initialize)
        {
            try { initialize(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"UnitCosts {stageName} failed; independent stages continue: {ex}");
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
                try
                {
                    GameXAMLManagerAPI.Instance.RegisterBinding(
                        bindingTarget,
                        runtime.RecruitmentCostTooltip);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(Logger, $"UnitCosts tooltip binding '{bindingTarget}' failed; remaining bindings continue: {ex}");
                }
            }
        }
    }
}
