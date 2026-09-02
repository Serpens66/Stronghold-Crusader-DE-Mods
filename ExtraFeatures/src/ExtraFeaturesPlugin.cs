// Feature: Plugin bootstrap for the Extra Features mod.
using BepInEx;
using BepInEx.Bootstrap;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace ExtraFeatures
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(BugfixesAndQoLGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(LegacySomeSettingsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ExtraFeaturesPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string BugfixesAndQoLGuid = "BugfixesAndQoL_Serp";
        private const string LegacySomeSettingsGuid = "SomeSettings_Serp";

        public const string PluginGuid = "ExtraFeatures_Serp";
        public const string PluginName = "Extra Features";
        public const string PluginVersion = "1.0.87";

        private ExtraFeaturesRuntime runtime;
        private bool marketGoodPriceVisualRefreshFailureLogged;

        public ExtraFeaturesViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");
            bool legacySomeSettingsLoaded = Chainloader.PluginInfos.ContainsKey(LegacySomeSettingsGuid);
            if (legacySomeSettingsLoaded)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"The obsolete mod '{LegacySomeSettingsGuid}' is loaded together with {PluginName}. " +
                    "Uninstall it to avoid duplicate or conflicting features.");
            }

            // Pass the startup result into the view model so the warning occupies no UI space otherwise.
            Settings = new ExtraFeaturesViewModel(legacySomeSettingsLoaded);
            runtime = new ExtraFeaturesRuntime(Logger, Settings);
            // This publisher roots the runtime after BepInEx destroys its manager component.
            // Hooks intentionally remain active until process exit; no component cleanup is reachable here.
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            // The Script Extender registers its built-in packet types in its earlier LibraryLoaded
            // handler. Register ours immediately afterwards, unconditionally and before any settings.
            try
            {
                runtime.InitializeNetwork();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Extra Features Chore registration failed; synchronized multiplayer actions remain unavailable: {ex}");
            }

            try
            {
                Settings.InitializeMarketGoodPriceEditor(Logger);
                SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel.PropertyChanged +=
                    (_, __) => RefreshMarketGoodPriceVisuals();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Extra Features market-price editor initialization failed: {ex}");
            }

            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/ExtraFeaturesSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Extra Features settings UI registration failed: {ex}");
                return;
            }

            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "SerpTroopAction_0100_ExtraFeaturesKnightTransform",
                    runtime.KnightDismountButton);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Extra Features knight button binding failed: {ex}");
            }

            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "ExtraFeaturesQuarryPileRelocationButtonHost",
                    runtime.QuarryPileRelocationButton);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Extra Features quarry button binding failed: {ex}");
            }

            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "ExtraFeaturesGatehouseAutomationButtonHost",
                    runtime.GatehouseAutomationButton);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Extra Features gatehouse automation button binding failed: {ex}");
            }

            try
            {
                runtime.InitializeNative(
                    libraryHandle,
                    memory,
                    Shared.DebugLogHelper.IsCurrentNativeLibraryVersion());
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Extra Features native runtime initialization failed; unaffected features may continue: {ex}");
            }

            try
            {
                runtime.ApplySettings();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Extra Features settings reconciliation failed; already initialized features remain active: {ex}");
            }

            try
            {
                runtime.InstallAIEconomyProtectionHook(libraryHandle, memory);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Extra Features AI economy protection initialization failed: {ex}");
            }

            try
            {
                runtime.InstallAIMarketVanillaPriceHook(libraryHandle, memory);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Extra Features AI Vanilla market-price initialization failed: {ex}");
            }

            Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Extra Features initialization stages completed.");
        }

        private void RefreshMarketGoodPriceVisuals()
        {
            try
            {
                Settings.RefreshMarketGoodPriceVisuals();
            }
            catch (Exception ex)
            {
                if (marketGoodPriceVisualRefreshFailureLogged)
                    return;

                marketGoodPriceVisualRefreshFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Extra Features market-price icon refresh failed; multiplier controls remain usable: {ex}");
            }
        }

        public bool TryRegisterVanillaFlagDisease(int projectileId) =>
            runtime != null &&
            runtime.TryRegisterVanillaFlagDisease(projectileId);
    }
}
