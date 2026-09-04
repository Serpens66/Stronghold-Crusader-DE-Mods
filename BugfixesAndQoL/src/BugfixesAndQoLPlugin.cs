// Feature: Plugin bootstrap for the Bugfixes and QoL mod.
using BepInEx;
using BepInEx.Bootstrap;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;

namespace BugfixesAndQoL
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("ActiveAIVDetector_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(LegacySomeSettingsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility(LegacyTroopMovementFixGuid)]
    [BepInIncompatibility(TroopMovementFix2Guid)]
    [BepInIncompatibility(TroopMovementFix3Guid)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BugfixesAndQoLPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string LegacyTroopMovementFixGuid = "TroopMovementFix_Serp";
        private const string TroopMovementFix2Guid = "TroopMovementFix2_Serp";
        private const string TroopMovementFix3Guid = "TroopMovementFix3_Serp";
        private const string LegacySomeSettingsGuid = "SomeSettings_Serp";

        public const string PluginGuid = "BugfixesAndQoL_Serp";
        public const string PluginName = "Bugfixes and QoL";
        public const string PluginVersion = "1.0.122";

        private static DisplayResolutionPersistenceHook displayResolutionPersistenceHook;
        private static SteamLobbyInvitePrompt steamLobbyInvitePrompt;
        private static SteamInviteBlacklistStore steamInviteBlacklist;
        private BugfixesAndQoLRuntime runtime;
        private bool marketGoodsVisualRefreshFailureLogged;

        public BugfixesAndQoLViewModel Settings { get; private set; }

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
            steamInviteBlacklist = new SteamInviteBlacklistStore(SteamInviteBlacklistStore.GetDefaultPath());
            Settings = new BugfixesAndQoLViewModel(legacySomeSettingsLoaded, steamInviteBlacklist, Logger);
            try
            {
                // Install before FatControler.Start loads settings.cfg and begins screen monitoring.
                displayResolutionPersistenceHook =
                    new DisplayResolutionPersistenceHook(Logger, Settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL display-resolution persistence could not be initialized; Vanilla behavior remains active: {ex}");
            }
            runtime = new BugfixesAndQoLRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void InitializePersistentUiAndMapCallbacks()
        {
            // These publishers outlive the BepInEx component and retain the callbacks for the process lifetime.
            try
            {
                SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel.PropertyChanged +=
                    (_, __) =>
                    {
                        // The game fills MainViewModel.GameSprites after the script extender loads.
                        // A hub change, including opening the settings, is the safe point to retry visuals.
                        try
                        {
                            Settings.RefreshMarketGoodsOrderVisuals();
                        }
                        catch (Exception ex)
                        {
                            // A visual retry must never escape through PropertyChanged and abort tab registration.
                            if (!marketGoodsVisualRefreshFailureLogged)
                            {
                                marketGoodsVisualRefreshFailureLogged = true;
                                Shared.DebugLogHelper.LogError(
                                    Logger,
                                    $"Bugfixes and QoL market-goods visual refresh failed; text controls remain usable: {ex}");
                            }
                        }
                    };
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL market-goods visual callback could not be registered; other callbacks continue: {ex}");
            }

            try
            {
                MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(args =>
                {
                    if (args.Phase == EventHookPhase.Post)
                        steamLobbyInvitePrompt?.TryInitialize();
                });
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL Steam invite map callback could not be registered; other callbacks continue: {ex}");
            }
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            // Register packet types immediately after Script Extender and before settings can vary.
            try
            {
                runtime.InitializeNetwork();
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLAivSyncStatusHost",
                    runtime.MultiplayerAivSyncUi);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL Chore registration failed; synchronized game-speed changes remain unavailable: {ex}");
            }

            // Construct the editor rows before subscribing to hub changes, so even an early
            // settings event can only refresh an already complete 20-item collection.
            try
            {
                Settings.InitializeMarketGoodsOrderEditor(Logger);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL market-order editor initialization failed: {ex}");
            }

            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/BugfixesAndQoLSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return;
            }

            InitializePersistentUiAndMapCallbacks();

            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "SerpTroopAction_0200_BugfixesAndQoLAssassinClimb",
                    runtime.AssassinClimbButton);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL Assassin climb button binding failed: {ex}");
            }

            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLQuarryPileRelocationButtonHost",
                    runtime.QuarryPileRelocationButton);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL quarry button binding failed: {ex}");
            }

            try
            {
                runtime.InitializeSurrenderFeature();
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLSurrenderButtonHost",
                    runtime.SurrenderAndStatisticsUi);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLSpectatorStatisticsRefreshHost",
                    runtime.SurrenderAndStatisticsUi);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL surrender/statistics UI/network initialization failed; both buttons remain inactive: {ex}");
            }

            try
            {
                runtime.InitializeSelectedUnitHealthFeature();
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLSelectedUnitHealthHost",
                    runtime.SelectedUnitHealthUi);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL selected-unit health UI initialization failed; the display remains hidden: {ex}");
            }

            // Keep UI registration independent so one native feature cannot hide the whole mod.
            try
            {
                runtime.InitializeNative(
                    libraryHandle,
                    memory,
                    Shared.DebugLogHelper.IsCurrentNativeLibraryVersion());
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL native runtime initialization failed; unaffected features may continue: {ex}");
            }

            try
            {
                object allyGoodsAmountDisplay = runtime.AllyGoodsAmountDisplay;
                if (allyGoodsAmountDisplay != null)
                {
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount5", allyGoodsAmountDisplay);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount10", allyGoodsAmountDisplay);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount25", allyGoodsAmountDisplay);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount100", allyGoodsAmountDisplay);
                    GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAllyAmount500", allyGoodsAmountDisplay);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL ally amount display binding failed: {ex}");
            }

            try
            {
                steamLobbyInvitePrompt = new SteamLobbyInvitePrompt(Logger, Settings, steamInviteBlacklist);
                steamLobbyInvitePrompt.TryInitialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL Steam lobby-invite prompt could not be initialized; Vanilla invite handling remains active: {ex}");
            }

            try
            {
                runtime.ApplySettings();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Bugfixes and QoL initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL settings reconciliation failed; already initialized features remain active: {ex}");
            }
        }
    }
}
