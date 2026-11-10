using APIShared.ModSettings;
// Feature: Plugin bootstrap for the Extra Features mod.
using BepInEx;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace ExtraFeatures
{
    [BepInDependency(ScriptExtenderGuid, "2.14.0")]
    [BepInDependency(ApiSharedGuid, "0.45.0")]
    [BepInDependency(LegacySomeSettingsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("FearFactorNeutralizationTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("UnitLimit_Serp", BepInDependency.DependencyFlags.SoftDependency)]

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ExtraFeaturesPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ApiSharedGuid = "APIShared_Serp";
        private const string LegacySomeSettingsGuid = "SomeSettings_Serp";

        public const string PluginGuid = "ExtraFeatures_Serp";
        public const string PluginName = "Extra Features";
        public const string PluginVersion = "1.0.133";

        private ExtraFeaturesRuntime runtime;
        private bool marketGoodPriceVisualRefreshFailureLogged;
        private bool buildingRepairIconRefreshFailureLogged;

        public ExtraFeaturesViewModel Settings { get; private set; }

        /// <summary>Read-only prediction for a regular/customized skirmish lobby, before its mode gate opens.</summary>
        public static bool TryGetLobbyKeepRangeOverride(int currentOverride, out int range) =>
            ExtraFeaturesRuntime.TryGetLobbyKeepRangeOverride(currentOverride, out range);

        private void Awake()
        {
            Shared.DebugLogHelper.InitializeDiagnostics(Config, Logger);
            Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
            Shared.CrashBreadcrumbDiagnostics.Initialize(
                Logger,
                PluginGuid,
                PluginName,
                PluginVersion);
            bool legacySomeSettingsLoaded = Chainloader.PluginInfos.ContainsKey(LegacySomeSettingsGuid);
            if (legacySomeSettingsLoaded)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/Awake", $"The obsolete mod '{LegacySomeSettingsGuid}' is loaded together with {PluginName}. " +
                    "Uninstall it to avoid duplicate or conflicting features.");
            }

            // Pass the startup result into the view model so the warning occupies no UI space otherwise.
            Settings = new ExtraFeaturesViewModel(legacySomeSettingsLoaded);
            runtime = new ExtraFeaturesRuntime(Logger, Settings);
            // This publisher roots the runtime after BepInEx destroys its manager component.
            // Hooks intentionally remain active until process exit; no component cleanup is reachable here.
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            // Initial native installation window (see ExtraFeaturesHookInfrastructure.DetourBackend).
            ExtraFeaturesHookInfrastructure.InitialLibraryLoad = true;
            try { InitializeLibraryRuntime(context); }
            finally { ExtraFeaturesHookInfrastructure.InitialLibraryLoad = false; }
        }

        private void InitializeLibraryRuntime(CrusaderLibraryLoadContext context)
        {
            // The Script Extender registers its built-in packet types in its earlier LibraryLoaded
            // handler. Register ours immediately afterwards, unconditionally and before any settings.
            try
            {
                runtime.InitializeNetwork();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/InitializeLibraryRuntime.chore-registration-failed-synchronized", $"Extra Features Chore registration failed; synchronized multiplayer actions remain unavailable: {ex}");
            }

            try
            {
                Settings.InitializeMarketGoodPriceEditor(Logger);
                SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel.PropertyChanged +=
                    (_, __) => RefreshSettingsVisuals();
                RefreshSettingsVisuals();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/InitializeLibraryRuntime.market-price-editor-initialization", $"Extra Features market-price editor initialization failed: {ex}");
            }

            try
            {
                Shared.DirectLaunchSettingsNotice.Configure(Settings, PluginGuid);
                if (!Settings.TryCaptureEnemyProximityStartup(Info.Location, PluginGuid, PluginGuid, out Exception migrationFailure))
                    Shared.DebugLogHelper.LogWarningLimited(Logger, "ExtraFeatures/enemy-proximity-migration",
                        "Extra Features could not read the previous enemy-proximity setting; the stored new values or defaults apply: " + migrationFailure);
                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Shared.DebugLogHelper.ForExternalCode(Logger),
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/ExtraFeaturesSettings.xaml",
                    logRoutineActivity: Shared.DebugLogHelper.DiagnosticsEnabled);
                Settings.CompleteKillRewardRegistration();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/InitializeLibraryRuntime.settings-ui-registration-failed", $"Extra Features settings UI registration failed: {ex}");
                return;
            }

            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "ExtraFeaturesGatehouseAutomationButtonHost",
                    runtime.GatehouseAutomationButton);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "ExtraFeaturesDrawbridgeAutomationButtonHost",
                    runtime.GatehouseAutomationButton);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/InitializeLibraryRuntime.gatehouse-automation-button-binding", $"Extra Features gatehouse automation button binding failed: {ex}");
            }

            try
            {
                runtime.InitializeNative(
                    context,
                    Shared.DebugLogHelper.IsCurrentNativeLibraryVersion());
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/InitializeLibraryRuntime.native-runtime-initialization-failed", $"Extra Features native runtime initialization failed; unaffected features may continue: {ex}");
            }

            try
            {
                runtime.ApplySettings();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/InitializeLibraryRuntime.settings-reconciliation-failed-already", $"Extra Features settings reconciliation failed; already initialized features remain active: {ex}");
            }

            try
            {
                runtime.InstallAIMarketVanillaPriceHook();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/InitializeLibraryRuntime.ai-vanilla-market-price", $"Extra Features AI Vanilla market-price initialization failed: {ex}");
            }

            Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Extra Features initialization stages completed.");
        }

        private void RefreshMarketGoodPriceVisuals()
        {
            try
            {
                Settings.RefreshMarketGoodPriceVisuals();
                Settings.RefreshSettingsSearchSource(Logger);
            }
            catch (Exception ex)
            {
                if (marketGoodPriceVisualRefreshFailureLogged)
                    return;

                marketGoodPriceVisualRefreshFailureLogged = true;
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/RefreshMarketGoodPriceVisuals", $"Extra Features market-price visual/search refresh failed; multiplier controls remain usable: {ex}");
            }
        }

        private void RefreshSettingsVisuals()
        {
            RefreshMarketGoodPriceVisuals();
            try
            {
                Settings.RefreshBuildingRepairIcon();
            }
            catch (Exception ex)
            {
                if (buildingRepairIconRefreshFailureLogged)
                    return;

                buildingRepairIconRefreshFailureLogged = true;
                Shared.DebugLogHelper.LogErrorLimited(Logger, "ExtraFeatures/ExtraFeaturesPlugin/RefreshSettingsVisuals", $"Extra Features building repair settings icon refresh failed: {ex}");
            }
        }

        public bool TryRegisterVanillaFlagDisease(int projectileId) =>
            runtime != null &&
            runtime.TryRegisterVanillaFlagDisease(projectileId);
    }
}
