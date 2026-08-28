// Feature: Lobby settings model for the Bugfixes and QoL features.
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BepInEx.Logging;
using Noesis;

namespace BugfixesAndQoL
{
    public sealed class BugfixesAndQoLViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        public event Action<string> SettingChanged;

        private bool enableMod = true;
        private bool rememberAiAivSettings = true;
        private bool enableCustomLordListEnhancements = true;
        private bool enableAiFixes = true;
        private bool fixAITowerRepair = true;
        private bool betterAIOverbuildRules = true;
        private bool enableTroopMovementFix = true;
        private bool enableImprovedAssassinPathfinding = true;
        private bool enablePlaguePopularityFix = true;
        private bool enablePlagueCloudRemovalFix = true;
        private bool enableStuckApothecaryFix = true;
        private bool enablePlagueTargetReservationFix = true;
        private bool enableAssemblyPointPlacementFix = true;
        private bool enableFairSiegeAmmoRestock = true;
        private bool enableSurrenderAndStatistics = true;
        private bool enableLordUnitControls = true;
        private bool enableEliminatedPlayersBecomeSpectators = true;
        private bool enableResyncHostKick = true;
        private bool enableReturnToMultiplayerLobby = true;
        private bool enableCtrlSingleMarketTrade = true;
        private bool enableMultiplayerGameSpeedChanges = true;
        private bool enableShiftGameSpeedSteps = true;
        private bool enableAllyGoodsAmountModifiers = true;
        private bool enableCustomTrailExtremeGoldFix = true;
        private bool preserveDisplayResolution = true;
        private readonly LocalPerPlayerSetting<bool> enableClientFeatures = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableMinimapCursorFollowFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableMarketKeyMainMenuFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableAutoTradeSellZeroFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableEnemyProximityBulldozeCursorFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableIngameSteamInvitePrompt = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> showSelectedUnitHealth = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> allowMinimapWhilePlacingBuilding = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> allowCameraMovementWithModifiers = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> hdMarketView = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<int[]> marketGoodsOrder =
            new LocalPerPlayerSetting<int[]>(
                MarketGoodsOrderDefinition.CreateHdOrder(),
                MarketGoodsOrderDefinition.CloneOrDefault);
        private readonly Dictionary<int, string> marketGoodNames = new Dictionary<int, string>();
        private readonly Dictionary<int, ImageSource> marketGoodIcons = new Dictionary<int, ImageSource>();
        private readonly SteamInviteBlacklistStore steamInviteBlacklist;
        private readonly ManualLogSource log;
        private ManualLogSource marketGoodsLog;
        private bool marketGoodsVisualsResolved;
        private bool marketGoodsVisualsDeferredLogged;
        private bool marketGoodsVisualsResolvedLogged;

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        protected override void ConfigurePerPlayerLobbySettings(
            Shared.PerPlayerLobbySettingsBuilder settings)
        {
            string[] enabledByDefault =
            {
                nameof(EnableMinimapCursorFollowFix),
                nameof(EnableMarketKeyMainMenuFix),
                nameof(EnableAutoTradeSellZeroFix),
                nameof(EnableEnemyProximityBulldozeCursorFix),
                nameof(EnableIngameSteamInvitePrompt),
                nameof(ShowSelectedUnitHealth),
                nameof(EnableClientFeatures),
                nameof(AllowMinimapWhilePlacingBuilding),
                nameof(AllowCameraMovementWithModifiers),
                nameof(HdMarketView),
            };
            foreach (string propertyName in enabledByDefault)
                settings.ResetSlotsWith(propertyName, () => true);

            settings
                .ResetSlotsWith(
                    nameof(MarketGoodsOrder),
                    () => MarketGoodsOrderDefinition.CreateHdOrder())
                .WhenLocalPlayerResolved(playerId => TrySetLocalPlayerId(playerId));
        }

        internal BugfixesAndQoLViewModel(
            bool legacySomeSettingsLoaded,
            SteamInviteBlacklistStore steamInviteBlacklist,
            ManualLogSource log)
        {
            this.steamInviteBlacklist = steamInviteBlacklist ?? throw new ArgumentNullException(nameof(steamInviteBlacklist));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            LegacyModWarningVisibility = legacySomeSettingsLoaded ? Visibility.Visible : Visibility.Collapsed;
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
            RestoreHdMarketOrderCommand = new RelayCommand(RestoreHdMarketOrder);
            ClearSteamInviteBlacklistCommand = new RelayCommand(
                ConfirmClearSteamInviteBlacklist,
                CanClearSteamInviteBlacklist);
            steamInviteBlacklist.Changed += OnSteamInviteBlacklistChanged;
        }

        public RelayCommand ResetToDefaultCommand { get; }
        public RelayCommand RestoreHdMarketOrderCommand { get; }
        public RelayCommand ClearSteamInviteBlacklistCommand { get; }
        public ObservableCollection<MarketGoodOrderItemViewModel> MarketGoodsOrderItems { get; } =
            new ObservableCollection<MarketGoodOrderItemViewModel>();
        public Visibility LegacyModWarningVisibility { get; }
        public string LegacyModWarningText => SerpLocalization.Get(SerpLocalization.LegacySomeSettingsWarning);
        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public string EnableClientFeaturesText => SerpLocalization.Get("BugfixesAndQoL.EnableClientFeatures");
        public string EnableClientFeaturesHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableClientFeaturesHelp");
        public string EnableHostFeaturesText => SerpLocalization.Get("BugfixesAndQoL.EnableHostFeatures");
        public string EnableHostFeaturesHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableHostFeaturesHelp");
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string ClientInterfaceTitleText => SerpLocalization.Get("BugfixesAndQoL.ClientInterfaceTitle");
        public string DisplayTitleText => SerpLocalization.Get("BugfixesAndQoL.DisplayTitle");
        public string AiAivTitleText => SerpLocalization.Get("BugfixesAndQoL.AiAivTitle");
        public string EnableAiFixesText => SerpLocalization.Get(SerpLocalization.EnableAiFixes);
        public string EnableAiFixesHelpText => SerpLocalization.Get(SerpLocalization.EnableAiFixesHelp);
        public string FixAITowerRepairText => SerpLocalization.Get("BugfixesAndQoL.FixAITowerRepair");
        public string FixAITowerRepairHelpText => SerpLocalization.Get("BugfixesAndQoL.FixAITowerRepairHelp");
        public string BetterAIOverbuildRulesText => SerpLocalization.Get("BugfixesAndQoL.BetterAIOverbuildRules");
        public string BetterAIOverbuildRulesHelpText => SerpLocalization.Get("BugfixesAndQoL.BetterAIOverbuildRulesHelp");
        public string TroopMovementTitleText => SerpLocalization.Get("BugfixesAndQoL.TroopMovementTitle");
        public string PlagueTitleText => SerpLocalization.Get("BugfixesAndQoL.PlagueTitle");
        public string GameplayTitleText => SerpLocalization.Get("BugfixesAndQoL.GameplayTitle");
        public string MultiplayerTitleText => SerpLocalization.Get("BugfixesAndQoL.MultiplayerTitle");
        public string EnableCtrlSingleMarketTradeText => SerpLocalization.Get(SerpLocalization.EnableCtrlSingleMarketTrade);
        public string EnableCtrlSingleMarketTradeHelpText => SerpLocalization.Get(SerpLocalization.EnableCtrlSingleMarketTradeHelp);
        public string EnableAllyGoodsAmountModifiersText => SerpLocalization.Get(SerpLocalization.EnableAllyGoodsAmountModifiers);
        public string EnableAllyGoodsAmountModifiersHelpText => SerpLocalization.Get(SerpLocalization.EnableAllyGoodsAmountModifiersHelp);
        public string EnableMultiplayerGameSpeedChangesText => SerpLocalization.Get(SerpLocalization.EnableMultiplayerGameSpeedChanges);
        public string EnableMultiplayerGameSpeedChangesHelpText => SerpLocalization.Get(SerpLocalization.EnableMultiplayerGameSpeedChangesHelp);
        public string EnableShiftGameSpeedStepsText => SerpLocalization.Get(SerpLocalization.EnableShiftGameSpeedSteps);
        public string EnableShiftGameSpeedStepsHelpText => SerpLocalization.Get(SerpLocalization.EnableShiftGameSpeedStepsHelp);
        public string EnableResyncHostKickText => SerpLocalization.Get("BugfixesAndQoL.EnableResyncHostKick");
        public string EnableResyncHostKickHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableResyncHostKickHelp");
        public string EnableReturnToMultiplayerLobbyText => SerpLocalization.Get("BugfixesAndQoL.EnableReturnToMultiplayerLobby");
        public string EnableReturnToMultiplayerLobbyHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableReturnToMultiplayerLobbyHelp");
        public string CustomTrailsTitleText => SerpLocalization.Get("BugfixesAndQoL.CustomTrailsTitle");
        public string EnableMinimapCursorFollowFixText => SerpLocalization.Get("BugfixesAndQoL.EnableMinimapCursorFollowFix");
        public string EnableMinimapCursorFollowFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableMinimapCursorFollowFixHelp");
        public string EnableMarketKeyMainMenuFixText => SerpLocalization.Get("BugfixesAndQoL.EnableMarketKeyMainMenuFix");
        public string EnableMarketKeyMainMenuFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableMarketKeyMainMenuFixHelp");
        public string EnableAutoTradeSellZeroFixText => SerpLocalization.Get("BugfixesAndQoL.EnableAutoTradeSellZeroFix");
        public string EnableAutoTradeSellZeroFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableAutoTradeSellZeroFixHelp");
        public string EnableEnemyProximityBulldozeCursorFixText => SerpLocalization.Get("BugfixesAndQoL.EnableEnemyProximityBulldozeCursorFix");
        public string EnableEnemyProximityBulldozeCursorFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableEnemyProximityBulldozeCursorFixHelp");
        public string EnableIngameSteamInvitePromptText => SerpLocalization.Get("BugfixesAndQoL.EnableIngameSteamInvitePrompt");
        public string EnableIngameSteamInvitePromptHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableIngameSteamInvitePromptHelp");
        public string ClearSteamInviteBlacklistText => SerpLocalization.Get("BugfixesAndQoL.ClearSteamInviteBlacklist");
        public string ClearSteamInviteBlacklistHelpText => SerpLocalization.Get("BugfixesAndQoL.ClearSteamInviteBlacklistHelp");
        public string ShowSelectedUnitHealthText => SerpLocalization.Get("BugfixesAndQoL.ShowSelectedUnitHealth");
        public string ShowSelectedUnitHealthHelpText => SerpLocalization.Get("BugfixesAndQoL.ShowSelectedUnitHealthHelp");
        public string EnableAssemblyPointPlacementFixText => SerpLocalization.Get("BugfixesAndQoL.EnableAssemblyPointPlacementFix");
        public string EnableAssemblyPointPlacementFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableAssemblyPointPlacementFixHelp");
        public string EnableFairSiegeAmmoRestockText => SerpLocalization.Get("BugfixesAndQoL.EnableFairSiegeAmmoRestock");
        public string EnableFairSiegeAmmoRestockHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableFairSiegeAmmoRestockHelp");
        public string EnableSurrenderAndStatisticsText => SerpLocalization.Get("BugfixesAndQoL.EnableSurrenderAndStatistics");
        public string EnableSurrenderAndStatisticsHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableSurrenderAndStatisticsHelp");
        public string EnableLordUnitControlsText => SerpLocalization.Get("BugfixesAndQoL.EnableLordUnitControls");
        public string EnableLordUnitControlsHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableLordUnitControlsHelp");
        public string EnableEliminatedPlayersBecomeSpectatorsText => SerpLocalization.Get("BugfixesAndQoL.EnableEliminatedPlayersBecomeSpectators");
        public string EnableEliminatedPlayersBecomeSpectatorsHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableEliminatedPlayersBecomeSpectatorsHelp");
        public string EnableCustomTrailExtremeGoldFixText => SerpLocalization.Get("BugfixesAndQoL.EnableCustomTrailExtremeGoldFix");
        public string EnableCustomTrailExtremeGoldFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableCustomTrailExtremeGoldFixHelp");
        public string PreserveDisplayResolutionText => SerpLocalization.Get("BugfixesAndQoL.PreserveDisplayResolution");
        public string PreserveDisplayResolutionHelpText => SerpLocalization.Get("BugfixesAndQoL.PreserveDisplayResolutionHelp");
        public string AllowMinimapWhilePlacingBuildingText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuilding);
        public string AllowMinimapWhilePlacingBuildingHelpText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuildingHelp);
        public string RememberAiAivSettingsText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettings);
        public string RememberAiAivSettingsHelpText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettingsHelp);
        public string EnableCustomLordListEnhancementsText => SerpLocalization.Get("BugfixesAndQoL.EnableCustomLordListEnhancements");
        public string EnableCustomLordListEnhancementsHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableCustomLordListEnhancementsHelp");
        public string EnableTroopMovementFixText => SerpLocalization.Get(SerpLocalization.EnableTroopMovementFix);
        public string EnableTroopMovementFixHelpText => SerpLocalization.Get(SerpLocalization.EnableTroopMovementFixHelp);
        public string EnableImprovedAssassinPathfindingText => SerpLocalization.Get(SerpLocalization.EnableImprovedAssassinPathfinding);
        public string EnableImprovedAssassinPathfindingHelpText => SerpLocalization.Get(SerpLocalization.EnableImprovedAssassinPathfindingHelp);
        public string EnablePlaguePopularityFixText => SerpLocalization.Get(SerpLocalization.EnablePlaguePopularityFix);
        public string EnablePlaguePopularityFixHelpText => SerpLocalization.Get(SerpLocalization.EnablePlaguePopularityFixHelp);
        public string EnablePlagueCloudRemovalFixText => SerpLocalization.Get(SerpLocalization.EnablePlagueCloudRemovalFix);
        public string EnablePlagueCloudRemovalFixHelpText => SerpLocalization.Get(SerpLocalization.EnablePlagueCloudRemovalFixHelp);
        public string EnableStuckApothecaryFixText => SerpLocalization.Get(SerpLocalization.EnableStuckApothecaryFix);
        public string EnableStuckApothecaryFixHelpText => SerpLocalization.Get(SerpLocalization.EnableStuckApothecaryFixHelp);
        public string EnablePlagueTargetReservationFixText => SerpLocalization.Get(SerpLocalization.EnablePlagueTargetReservationFix);
        public string EnablePlagueTargetReservationFixHelpText => SerpLocalization.Get(SerpLocalization.EnablePlagueTargetReservationFixHelp);
        public string AllowCameraMovementWithModifiersText => SerpLocalization.Get(SerpLocalization.AllowCameraMovementWithModifiers);
        public string AllowCameraMovementWithModifiersHelpText => SerpLocalization.Get(SerpLocalization.AllowCameraMovementWithModifiersHelp);
        public string HdMarketViewText => SerpLocalization.Get(SerpLocalization.HdMarketView);
        public string HdMarketViewHelpText => SerpLocalization.Get(SerpLocalization.HdMarketViewHelp);
        public string MarketGoodsOrderTitleText => SerpLocalization.Get(SerpLocalization.MarketGoodsOrderTitle);
        public string MarketGoodsOrderHelpText => SerpLocalization.Get(SerpLocalization.MarketGoodsOrderHelp);
        public string RestoreHdMarketOrderText => SerpLocalization.Get(SerpLocalization.MarketGoodsOrderRestoreHd);
        public string RestoreHdMarketOrderHelpText => SerpLocalization.Get(SerpLocalization.MarketGoodsOrderRestoreHdHelp);

        public bool[] EnableMinimapCursorFollowFixData => enableMinimapCursorFollowFix.Data;
        public bool[] EnableMarketKeyMainMenuFixData => enableMarketKeyMainMenuFix.Data;
        public bool[] EnableAutoTradeSellZeroFixData => enableAutoTradeSellZeroFix.Data;
        public bool[] EnableEnemyProximityBulldozeCursorFixData => enableEnemyProximityBulldozeCursorFix.Data;
        public bool[] EnableIngameSteamInvitePromptData => enableIngameSteamInvitePrompt.Data;
        public bool[] ShowSelectedUnitHealthData => showSelectedUnitHealth.Data;
        public bool[] EnableClientFeaturesData => enableClientFeatures.Data;
        public bool[] AllowMinimapWhilePlacingBuildingData => allowMinimapWhilePlacingBuilding.Data;
        public bool[] AllowCameraMovementWithModifiersData => allowCameraMovementWithModifiers.Data;
        public bool[] HdMarketViewData => hdMarketView.Data;
        public int[][] MarketGoodsOrderData => marketGoodsOrder.Data;

        private bool CanClearSteamInviteBlacklist() =>
            steamInviteBlacklist.Count > 0 || !steamInviteBlacklist.IsUsable;

        private void ConfirmClearSteamInviteBlacklist()
        {
            HUD_ConfirmationPopup.ShowConfirmation(
                SerpLocalization.Get("BugfixesAndQoL.ClearSteamInviteBlacklistConfirm"),
                ClearSteamInviteBlacklist,
                () => { },
                MPConf: MainViewModel.Instance.Show_MultiplayerSetup);
        }

        private void ClearSteamInviteBlacklist()
        {
            if (steamInviteBlacklist.TryClear(out string error))
            {
                Shared.DebugLogHelper.LogDebug(log, "Cleared the local Steam invite blacklist.");
                return;
            }

            Shared.DebugLogHelper.LogError(log, $"Could not clear the local Steam invite blacklist: {error}");
        }

        private void OnSteamInviteBlacklistChanged()
        {
            ClearSteamInviteBlacklistCommand.RaiseCanExecuteChanged();
        }

        [SyncPerPlayer]
        public bool EnableMinimapCursorFollowFix
        {
            get => enableMinimapCursorFollowFix.Value;
            set => SetPlayerSetting(enableMinimapCursorFollowFix, value, nameof(EnableMinimapCursorFollowFix));
        }

        [SyncPerPlayer]
        public bool EnableMarketKeyMainMenuFix
        {
            get => enableMarketKeyMainMenuFix.Value;
            set => SetPlayerSetting(enableMarketKeyMainMenuFix, value, nameof(EnableMarketKeyMainMenuFix));
        }

        [SyncPerPlayer]
        public bool EnableAutoTradeSellZeroFix
        {
            get => enableAutoTradeSellZeroFix.Value;
            set => SetPlayerSetting(enableAutoTradeSellZeroFix, value, nameof(EnableAutoTradeSellZeroFix));
        }

        [SyncPerPlayer]
        public bool EnableEnemyProximityBulldozeCursorFix
        {
            get => enableEnemyProximityBulldozeCursorFix.Value;
            set => SetPlayerSetting(enableEnemyProximityBulldozeCursorFix, value, nameof(EnableEnemyProximityBulldozeCursorFix));
        }

        [SyncPerPlayer]
        public bool EnableIngameSteamInvitePrompt
        {
            get => enableIngameSteamInvitePrompt.Value;
            set => SetPlayerSetting(enableIngameSteamInvitePrompt, value, nameof(EnableIngameSteamInvitePrompt));
        }

        [SyncPerPlayer]
        public bool ShowSelectedUnitHealth
        {
            get => showSelectedUnitHealth.Value;
            set => SetPlayerSetting(showSelectedUnitHealth, value, nameof(ShowSelectedUnitHealth));
        }

        [SyncPerPlayer]
        public bool EnableClientFeatures
        {
            get => enableClientFeatures.Value;
            set => SetPlayerSetting(enableClientFeatures, value, nameof(EnableClientFeatures));
        }

        [Shared.PresetLocal]
        public bool EnableCustomTrailExtremeGoldFix
        {
            get => enableCustomTrailExtremeGoldFix;
            set => SetSetting(ref enableCustomTrailExtremeGoldFix, value, nameof(EnableCustomTrailExtremeGoldFix));
        }

        [Shared.PresetLocal]
        public bool PreserveDisplayResolution
        {
            get => preserveDisplayResolution;
            set => SetSetting(ref preserveDisplayResolution, value, nameof(PreserveDisplayResolution));
        }

        [Shared.PresetLocal]
        public bool EnableAllyGoodsAmountModifiers
        {
            get => enableAllyGoodsAmountModifiers;
            set => SetSetting(ref enableAllyGoodsAmountModifiers, value, nameof(EnableAllyGoodsAmountModifiers));
        }

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set => SetSetting(ref enableMod, value, nameof(EnableMod));
        }

        [SyncPerPlayer]
        public bool AllowMinimapWhilePlacingBuilding
        {
            get => allowMinimapWhilePlacingBuilding.Value;
            set => SetPlayerSetting(allowMinimapWhilePlacingBuilding, value, nameof(AllowMinimapWhilePlacingBuilding));
        }

        [SyncPerPlayer]
        public bool AllowCameraMovementWithModifiers
        {
            get => allowCameraMovementWithModifiers.Value;
            set => SetPlayerSetting(allowCameraMovementWithModifiers, value, nameof(AllowCameraMovementWithModifiers));
        }

        [SyncPerPlayer]
        public bool HdMarketView
        {
            get => hdMarketView.Value;
            set => SetPlayerSetting(hdMarketView, value, nameof(HdMarketView));
        }

        [SyncPerPlayer]
        public int[] MarketGoodsOrder
        {
            get => MarketGoodsOrderDefinition.CloneOrDefault(marketGoodsOrder.Value);
            set
            {
                bool valid = MarketGoodsOrderDefinition.IsValid(value);
                int[] normalized = MarketGoodsOrderDefinition.CloneOrDefault(value);
                if (!CanMutateSetting(nameof(MarketGoodsOrder)))
                    return;

                if (!valid && value != null)
                {
                    Shared.DebugLogHelper.LogWarning(
                        marketGoodsLog,
                        "Bugfixes and QoL rejected an invalid market-goods order and restored the HD order.");
                }

                if (MarketGoodsOrderDefinition.AreEqual(marketGoodsOrder.Value, normalized))
                {
                    RefreshMarketGoodsOrderItems();
                    return;
                }

                marketGoodsOrder.SetValue(normalized);
                RefreshMarketGoodsOrderItems();
                SettingChanged?.Invoke(nameof(MarketGoodsOrder));
                OnPropertyChanged(nameof(MarketGoodsOrder));
            }
        }

        [SyncHostOnly]
        public bool EnableAiFixes
        {
            get => enableAiFixes;
            set => SetSetting(ref enableAiFixes, value, nameof(EnableAiFixes));
        }

        [SyncHostOnly]
        public bool FixAITowerRepair
        {
            get => fixAITowerRepair;
            set => SetSetting(ref fixAITowerRepair, value, nameof(FixAITowerRepair));
        }

        [SyncHostOnly]
        public bool BetterAIOverbuildRules
        {
            get => betterAIOverbuildRules;
            set => SetSetting(ref betterAIOverbuildRules, value, nameof(BetterAIOverbuildRules));
        }

        [SyncHostOnly]
        public bool RememberAiAivSettings
        {
            get => rememberAiAivSettings;
            set => SetSetting(ref rememberAiAivSettings, value, nameof(RememberAiAivSettings));
        }

        [SyncHostOnly]
        public bool EnableCustomLordListEnhancements
        {
            get => enableCustomLordListEnhancements;
            set => SetSetting(ref enableCustomLordListEnhancements, value, nameof(EnableCustomLordListEnhancements));
        }

        [SyncHostOnly]
        public bool EnableTroopMovementFix
        {
            get => enableTroopMovementFix;
            set => SetSetting(ref enableTroopMovementFix, value, nameof(EnableTroopMovementFix));
        }

        [SyncHostOnly]
        public bool EnableImprovedAssassinPathfinding
        {
            get => enableImprovedAssassinPathfinding;
            set => SetSetting(ref enableImprovedAssassinPathfinding, value, nameof(EnableImprovedAssassinPathfinding));
        }

        [SyncHostOnly]
        public bool EnablePlaguePopularityFix
        {
            get => enablePlaguePopularityFix;
            set => SetSetting(ref enablePlaguePopularityFix, value, nameof(EnablePlaguePopularityFix));
        }

        [SyncHostOnly]
        public bool EnablePlagueCloudRemovalFix
        {
            get => enablePlagueCloudRemovalFix;
            set => SetSetting(ref enablePlagueCloudRemovalFix, value, nameof(EnablePlagueCloudRemovalFix));
        }

        [SyncHostOnly]
        public bool EnableStuckApothecaryFix
        {
            get => enableStuckApothecaryFix;
            set => SetSetting(ref enableStuckApothecaryFix, value, nameof(EnableStuckApothecaryFix));
        }

        [SyncHostOnly]
        public bool EnablePlagueTargetReservationFix
        {
            get => enablePlagueTargetReservationFix;
            set => SetSetting(ref enablePlagueTargetReservationFix, value, nameof(EnablePlagueTargetReservationFix));
        }

        [SyncHostOnly]
        public bool EnableAssemblyPointPlacementFix
        {
            get => enableAssemblyPointPlacementFix;
            set => SetSetting(ref enableAssemblyPointPlacementFix, value, nameof(EnableAssemblyPointPlacementFix));
        }

        [SyncHostOnly]
        public bool EnableFairSiegeAmmoRestock
        {
            get => enableFairSiegeAmmoRestock;
            set => SetSetting(ref enableFairSiegeAmmoRestock, value, nameof(EnableFairSiegeAmmoRestock));
        }

        [SyncHostOnly]
        public bool EnableSurrenderAndStatistics
        {
            get => enableSurrenderAndStatistics;
            set => SetSetting(ref enableSurrenderAndStatistics, value, nameof(EnableSurrenderAndStatistics));
        }

        [SyncHostOnly]
        public bool EnableLordUnitControls
        {
            get => enableLordUnitControls;
            set => SetSetting(ref enableLordUnitControls, value, nameof(EnableLordUnitControls));
        }

        [SyncHostOnly]
        public bool EnableEliminatedPlayersBecomeSpectators
        {
            get => enableEliminatedPlayersBecomeSpectators;
            set => SetSetting(ref enableEliminatedPlayersBecomeSpectators, value, nameof(EnableEliminatedPlayersBecomeSpectators));
        }

        [SyncHostOnly]
        public bool EnableResyncHostKick
        {
            get => enableResyncHostKick;
            set => SetSetting(ref enableResyncHostKick, value, nameof(EnableResyncHostKick));
        }

        [SyncHostOnly]
        public bool EnableReturnToMultiplayerLobby
        {
            get => enableReturnToMultiplayerLobby;
            set => SetSetting(ref enableReturnToMultiplayerLobby, value, nameof(EnableReturnToMultiplayerLobby));
        }

        [SyncHostOnly]
        public bool EnableCtrlSingleMarketTrade
        {
            get => enableCtrlSingleMarketTrade;
            set => SetSetting(ref enableCtrlSingleMarketTrade, value, nameof(EnableCtrlSingleMarketTrade));
        }

        [SyncHostOnly]
        public bool EnableMultiplayerGameSpeedChanges
        {
            get => enableMultiplayerGameSpeedChanges;
            set => SetSetting(ref enableMultiplayerGameSpeedChanges, value, nameof(EnableMultiplayerGameSpeedChanges));
        }

        [SyncHostOnly]
        public bool EnableShiftGameSpeedSteps
        {
            get => enableShiftGameSpeedSteps;
            set => SetSetting(ref enableShiftGameSpeedSteps, value, nameof(EnableShiftGameSpeedSteps));
        }

        private void ResetToDefault()
        {
            if (CanEditHostSettings)
            {
                EnableMod = true;
                EnableAiFixes = true;
                FixAITowerRepair = true;
                BetterAIOverbuildRules = true;
                RememberAiAivSettings = true;
                EnableCustomLordListEnhancements = true;
                EnableTroopMovementFix = true;
                EnableImprovedAssassinPathfinding = true;
                EnablePlaguePopularityFix = true;
                EnablePlagueCloudRemovalFix = true;
                EnableStuckApothecaryFix = true;
                EnablePlagueTargetReservationFix = true;
                EnableAssemblyPointPlacementFix = true;
                EnableFairSiegeAmmoRestock = true;
                EnableSurrenderAndStatistics = true;
                EnableLordUnitControls = true;
                EnableEliminatedPlayersBecomeSpectators = true;
                EnableResyncHostKick = true;
                EnableReturnToMultiplayerLobby = true;
                EnableCtrlSingleMarketTrade = true;
                EnableMultiplayerGameSpeedChanges = true;
                EnableShiftGameSpeedSteps = true;
            }

            // Every participant resets only their own per-player preferences.
            EnableClientFeatures = true;
            AllowMinimapWhilePlacingBuilding = true;
            AllowCameraMovementWithModifiers = true;
            HdMarketView = true;
            MarketGoodsOrder = MarketGoodsOrderDefinition.CreateHdOrder();
            EnableMinimapCursorFollowFix = true;
            EnableMarketKeyMainMenuFix = true;
            EnableAutoTradeSellZeroFix = true;
            EnableEnemyProximityBulldozeCursorFix = true;
            EnableIngameSteamInvitePrompt = true;
            ShowSelectedUnitHealth = true;
            EnableCustomTrailExtremeGoldFix = true;
            PreserveDisplayResolution = true;
            EnableAllyGoodsAmountModifiers = true;
        }

        private void SetSetting<T>(ref T field, T value, string propertyName)
        {
            if (!CanMutateSetting(propertyName))
                return;

            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        internal bool TrySetLocalPlayerId(int playerId)
        {
            if (!enableClientFeatures.TrySetLocalPlayerId(playerId))
                return false;

            enableMinimapCursorFollowFix.TrySetLocalPlayerId(playerId);
            enableMarketKeyMainMenuFix.TrySetLocalPlayerId(playerId);
            enableAutoTradeSellZeroFix.TrySetLocalPlayerId(playerId);
            enableEnemyProximityBulldozeCursorFix.TrySetLocalPlayerId(playerId);
            enableIngameSteamInvitePrompt.TrySetLocalPlayerId(playerId);
            showSelectedUnitHealth.TrySetLocalPlayerId(playerId);
            allowMinimapWhilePlacingBuilding.TrySetLocalPlayerId(playerId);
            allowCameraMovementWithModifiers.TrySetLocalPlayerId(playerId);
            hdMarketView.TrySetLocalPlayerId(playerId);
            marketGoodsOrder.TrySetLocalPlayerId(playerId);
            return true;
        }

        internal void InitializeMarketGoodsOrderEditor(ManualLogSource log)
        {
            marketGoodsLog = log;

            // MainViewModel.Instance constructs the game view model on first access. During
            // plugin startup that constructor is not ready yet, so build text-only rows first.
            foreach (int good in MarketGoodsOrderDefinition.CreateHdOrder())
            {
                marketGoodNames[good] = ((Enums.Goods)good).ToString();
                marketGoodIcons[good] = null;
            }

            while (MarketGoodsOrderItems.Count < MarketGoodsOrderDefinition.Count)
                MarketGoodsOrderItems.Add(new MarketGoodOrderItemViewModel(MoveMarketGood));

            RefreshMarketGoodsOrderItems();
            RefreshMarketGoodsOrderVisuals();
        }

        internal void RefreshMarketGoodsOrderVisuals()
        {
            if (marketGoodsVisualsResolved)
                return;

            // Never touch Instance before the game confirms that its view model is constructed.
            // Its getter is a factory and can throw while the frontend is still initializing.
            if (!MainViewModel.viewModelLoaded)
            {
                LogMarketGoodsVisualsDeferred(
                    mainViewModelReady: false,
                    spriteCount: 0,
                    resolvedIconCount: 0,
                    resolvedNameCount: 0);
                return;
            }

            MainViewModel viewModel;
            try
            {
                viewModel = MainViewModel.Instance;
            }
            catch (Exception ex)
            {
                LogMarketGoodsVisualsDeferred(
                    mainViewModelReady: false,
                    spriteCount: 0,
                    resolvedIconCount: 0,
                    resolvedNameCount: 0,
                    detail: ex.GetType().Name);
                return;
            }

            if (viewModel?.GameSprites == null)
            {
                LogMarketGoodsVisualsDeferred(
                    mainViewModelReady: viewModel != null,
                    spriteCount: 0,
                    resolvedIconCount: 0,
                    resolvedNameCount: 0);
                return;
            }

            int resolvedIconCount = 0;
            int resolvedNameCount = 0;
            foreach (int good in MarketGoodsOrderDefinition.CreateHdOrder())
            {
                if (TryResolveMarketGoodName(good, out string name))
                    resolvedNameCount++;
                marketGoodNames[good] = name;

                if (TryResolveMarketGoodIcon(viewModel, good, out ImageSource icon))
                    resolvedIconCount++;
                marketGoodIcons[good] = icon;
            }

            RefreshMarketGoodsOrderItems();
            marketGoodsVisualsResolved =
                resolvedIconCount == MarketGoodsOrderDefinition.Count &&
                resolvedNameCount == MarketGoodsOrderDefinition.Count;

            int spriteCount = viewModel.GameSprites.Count;
            if (marketGoodsVisualsResolved)
            {
                if (!marketGoodsVisualsResolvedLogged)
                {
                    marketGoodsVisualsResolvedLogged = true;
                    Shared.DebugLogHelper.LogDebug(
                        marketGoodsLog,
                        () =>
                            $"Bugfixes and QoL market-goods visuals resolved: icons={resolvedIconCount}/{MarketGoodsOrderDefinition.Count}, " +
                            $"localizedNames={resolvedNameCount}/{MarketGoodsOrderDefinition.Count}, gameSprites={spriteCount}.");
                }

                return;
            }

            LogMarketGoodsVisualsDeferred(
                mainViewModelReady: true,
                spriteCount,
                resolvedIconCount,
                resolvedNameCount);
        }

        private void LogMarketGoodsVisualsDeferred(
            bool mainViewModelReady,
            int spriteCount,
            int resolvedIconCount,
            int resolvedNameCount,
            string detail = null)
        {
            if (marketGoodsVisualsDeferredLogged)
                return;

            marketGoodsVisualsDeferredLogged = true;
            Shared.DebugLogHelper.LogDebug(
                marketGoodsLog,
                () =>
                    $"Bugfixes and QoL deferred market-goods visuals until the game UI resources are ready: " +
                    $"mainViewModelReady={mainViewModelReady}, gameSprites={spriteCount}, " +
                    $"icons={resolvedIconCount}/{MarketGoodsOrderDefinition.Count}, " +
                    $"localizedNames={resolvedNameCount}/{MarketGoodsOrderDefinition.Count}" +
                    (string.IsNullOrEmpty(detail) ? "." : $", detail={detail}."));
        }

        private void MoveMarketGood(int good, int direction)
        {
            if (!CanEditClientSettings || !CanMutateSetting(nameof(MarketGoodsOrder)))
                return;

            MarketGoodsOrder = MarketGoodsOrderDefinition.SwapGoodWithNeighbor(
                marketGoodsOrder.Value,
                good,
                direction);
        }

        private void RestoreHdMarketOrder()
        {
            if (!CanEditClientSettings || !CanMutateSetting(nameof(MarketGoodsOrder)))
                return;

            MarketGoodsOrder = MarketGoodsOrderDefinition.CreateHdOrder();
        }

        private void RefreshMarketGoodsOrderItems()
        {
            if (MarketGoodsOrderItems.Count != MarketGoodsOrderDefinition.Count)
                return;

            int[] order = MarketGoodsOrderDefinition.CloneOrDefault(marketGoodsOrder.Value);
            for (int index = 0; index < order.Length; index++)
            {
                int good = order[index];
                marketGoodNames.TryGetValue(good, out string name);
                marketGoodIcons.TryGetValue(good, out ImageSource icon);
                MarketGoodsOrderItems[index].Update(
                    index,
                    good,
                    string.IsNullOrWhiteSpace(name) ? ((Enums.Goods)good).ToString() : name,
                    icon);
            }
        }

        private static bool TryResolveMarketGoodName(int good, out string name)
        {
            try
            {
                name = Translate.Instance?.lookUpText(Enums.eTextSections.TEXT_GOODS, good);
                if (!string.IsNullOrWhiteSpace(name))
                    return true;
            }
            catch
            {
            }

            name = ((Enums.Goods)good).ToString();
            return false;
        }

        private static bool TryResolveMarketGoodIcon(
            MainViewModel viewModel,
            int good,
            out ImageSource icon)
        {
            icon = null;
            try
            {
                if (viewModel == null || viewModel.GameSprites == null)
                    return false;

                int spriteId = (int)viewModel.goodsSpriteEnumFromGoodsEnum((Enums.Goods)good);
                if (spriteId < 0 || spriteId >= viewModel.GameSprites.Count)
                    return false;

                icon = viewModel.GameSprites[spriteId];
                return (BaseComponent)(object)icon != (BaseComponent)null;
            }
            catch
            {
                icon = null;
                return false;
            }
        }

        private void SetPlayerSetting(LocalPerPlayerSetting<bool> setting, bool value, string propertyName)
        {
            if (!setting.SetValue(value))
                return;

            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }
    }
}
