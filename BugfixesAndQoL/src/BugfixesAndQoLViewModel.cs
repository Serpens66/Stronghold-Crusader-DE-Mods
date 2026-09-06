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
        private bool allowFullAiMultiplayerLobby = true;
        private bool rememberAiAivSettings = true;
        private bool enableCustomLordListEnhancements = true;
        private bool enableAiFixes = true;
        private bool enableAiDefensePatrolFix = true;
        private bool enableAivDefenderPositionFix = true;
        private bool fixAITowerRepair = true;
        private bool betterAIOverbuildRules = true;
        private bool enableTroopMovementFix = true;
        private int friendlyMoatMovementMode = FriendlyMoatMovementPolicy.DefaultMode;
        private bool enableImprovedMoatFilling = true;
        private bool enableMountedStockpileMovementFix = true;
        private bool enableHealerAttackCommandFix = true;
        private bool enableFastRecruitRallyMovement = true;
        private bool enableImprovedAssassinPathfinding = true;
        private bool enableAssassinCombatResumeFix = true;
        private bool enablePlaguePopularityFix = true;
        private bool enablePlagueCloudRemovalFix = true;
        private bool enableStuckApothecaryFix = true;
        private bool enablePlagueTargetReservationFix = true;
        private bool enableAssemblyPointPlacementFix = true;
        private bool enableFairSiegeAmmoRestock = true;
        private bool enableSurrenderAndStatistics = true;
        private bool enableLordUnitControls = true;
        private bool enableEliminatedPlayersBecomeSpectators = true;
        private bool enableAbruptHostMigrationFix = true;
        private bool enableResyncHostKick = true;
        private bool enableReturnToMultiplayerLobby = true;
        private bool enableCtrlSingleMarketTrade = true;
        private bool enableSingleBuildingPause = true;
        private bool enableShiftRepairAllBuildings = true;
        private bool enableQuarryPileRelocation = true;
        private bool enableAIQuarryPileTowardsKeep = true;
        private bool requireReachableEnemyForAutomaticGateClosing = true;
        private bool preventAIPause = true;
        private bool preventEmergencyDemolition = true;
        private bool preventHovelDeletion = true;
        private int inaccessibleAIBuildingDemolitionProtection =
            TemporaryGateBlockagePolicy.ImprovedReachabilityMode;
        private MultiplayerTimeControlPermission enableMultiplayerGameSpeedChanges =
            MultiplayerTimeControlPermission.OnlyHost;
        private bool enableShiftGameSpeedSteps = true;
        private bool enableAllyGoodsAmountModifiers = true;
        private bool enableCustomTrailExtremeGoldFix = true;
        private bool showVanillaMapsInEditor = true;
        private bool preserveDisplayResolution = true;
        private bool enableDisbandedUnitControlGroupCleanup = true;
        private readonly LocalPerPlayerSetting<bool> enableClientFeatures = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableMinimapCursorFollowFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableMarketKeyMainMenuFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableAutoTradeSellZeroFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableEnemyProximityBulldozeCursorFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableIngameSteamInvitePrompt = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> showSelectedUnitHealth = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableTroopHudMiddleClickCameraJump =
            new LocalPerPlayerSetting<bool>(true);
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
                nameof(EnableTroopHudMiddleClickCameraJump),
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
        public string AiEconomyProtectionTitleText => SerpLocalization.Get(SerpLocalization.AiEconomyProtectionTitle);
        public string PreventAIPauseText => SerpLocalization.Get(SerpLocalization.PreventAIPause);
        public string PreventAIPauseHelpText => SerpLocalization.Get(SerpLocalization.PreventAIPauseHelp);
        public string PreventEmergencyDemolitionText => SerpLocalization.Get(SerpLocalization.PreventEmergencyDemolition);
        public string PreventEmergencyDemolitionHelpText => SerpLocalization.Get(SerpLocalization.PreventEmergencyDemolitionHelp);
        public string PreventHovelDeletionText => SerpLocalization.Get(SerpLocalization.PreventHovelDeletion);
        public string PreventHovelDeletionHelpText => SerpLocalization.Get(SerpLocalization.PreventHovelDeletionHelp);
        public string InaccessibleAIBuildingDemolitionProtectionText => SerpLocalization.Get(SerpLocalization.InaccessibleAIBuildingDemolitionProtection);
        public string InaccessibleAIBuildingDemolitionProtectionHelpText => SerpLocalization.Get(SerpLocalization.InaccessibleAIBuildingDemolitionProtectionHelp);
        public string InaccessibleAIBuildingDemolitionProtectionValueText
        {
            get
            {
                switch (InaccessibleAIBuildingDemolitionProtection)
                {
                    case TemporaryGateBlockagePolicy.VanillaMode:
                        return SerpLocalization.Get(SerpLocalization.InaccessibleAIBuildingDemolitionModeVanilla);
                    case TemporaryGateBlockagePolicy.AlwaysPreventMode:
                        return SerpLocalization.Get(SerpLocalization.InaccessibleAIBuildingDemolitionModeAlways);
                    default:
                        return SerpLocalization.Get(SerpLocalization.InaccessibleAIBuildingDemolitionModeTemporary);
                }
            }
        }
        public string EnableAiFixesText => SerpLocalization.Get(SerpLocalization.EnableAiFixes);
        public string EnableAiFixesHelpText => SerpLocalization.Get(SerpLocalization.EnableAiFixesHelp);
        public string EnableAiDefensePatrolFixText => SerpLocalization.Get("BugfixesAndQoL.EnableAiDefensePatrolFix");
        public string EnableAiDefensePatrolFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableAiDefensePatrolFixHelp");
        public string EnableAivDefenderPositionFixText => SerpLocalization.Get("BugfixesAndQoL.EnableAivDefenderPositionFix");
        public string EnableAivDefenderPositionFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableAivDefenderPositionFixHelp");
        public string FixAITowerRepairText => SerpLocalization.Get("BugfixesAndQoL.FixAITowerRepair");
        public string FixAITowerRepairHelpText => SerpLocalization.Get("BugfixesAndQoL.FixAITowerRepairHelp");
        public string BetterAIOverbuildRulesText => SerpLocalization.Get("BugfixesAndQoL.BetterAIOverbuildRules");
        public string BetterAIOverbuildRulesHelpText => SerpLocalization.Get("BugfixesAndQoL.BetterAIOverbuildRulesHelp");
        public string TroopMovementTitleText => SerpLocalization.Get("BugfixesAndQoL.TroopMovementTitle");
        public string PlagueTitleText => SerpLocalization.Get("BugfixesAndQoL.PlagueTitle");
        public string GameplayTitleText => SerpLocalization.Get("BugfixesAndQoL.GameplayTitle");
        public string EnableSingleBuildingPauseText => SerpLocalization.Get(SerpLocalization.EnableSingleBuildingPause);
        public string EnableSingleBuildingPauseHelpText => SerpLocalization.Get(SerpLocalization.EnableSingleBuildingPauseHelp);
        public string EnableShiftRepairAllBuildingsText => SerpLocalization.Get("BugfixesAndQoL.EnableShiftRepairAllBuildings");
        public string EnableShiftRepairAllBuildingsHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableShiftRepairAllBuildingsHelp");
        public string EnableQuarryPileRelocationText => SerpLocalization.Get(SerpLocalization.EnableQuarryPileRelocation);
        public string EnableQuarryPileRelocationHelpText => SerpLocalization.Get(SerpLocalization.EnableQuarryPileRelocationHelp);
        public string RequireReachableEnemyForAutomaticGateClosingText => SerpLocalization.Get("BugfixesAndQoL.RequireReachableEnemyForAutomaticGateClosing");
        public string RequireReachableEnemyForAutomaticGateClosingHelpText => SerpLocalization.Get("BugfixesAndQoL.RequireReachableEnemyForAutomaticGateClosingHelp");
        public string MultiplayerTitleText => SerpLocalization.Get("BugfixesAndQoL.MultiplayerTitle");
        public string AllowFullAiMultiplayerLobbyText => SerpLocalization.Get("BugfixesAndQoL.AllowFullAiMultiplayerLobby");
        public string AllowFullAiMultiplayerLobbyHelpText => SerpLocalization.Get("BugfixesAndQoL.AllowFullAiMultiplayerLobbyHelp");
        public string EnableCtrlSingleMarketTradeText => SerpLocalization.Get(SerpLocalization.EnableCtrlSingleMarketTrade);
        public string EnableCtrlSingleMarketTradeHelpText => SerpLocalization.Get(SerpLocalization.EnableCtrlSingleMarketTradeHelp);
        public string EnableAllyGoodsAmountModifiersText => SerpLocalization.Get(SerpLocalization.EnableAllyGoodsAmountModifiers);
        public string EnableAllyGoodsAmountModifiersHelpText => SerpLocalization.Get(SerpLocalization.EnableAllyGoodsAmountModifiersHelp);
        public string EnableMultiplayerGameSpeedChangesText => SerpLocalization.Get(SerpLocalization.EnableMultiplayerGameSpeedChanges);
        public string EnableMultiplayerGameSpeedChangesHelpText => SerpLocalization.Get(SerpLocalization.EnableMultiplayerGameSpeedChangesHelp);
        public string[] MultiplayerTimeControlPermissionOptions => new[]
        {
            SerpLocalization.Get(SerpLocalization.MultiplayerTimeControlDisabled),
            SerpLocalization.Get(SerpLocalization.MultiplayerTimeControlOnlyHost),
            SerpLocalization.Get(SerpLocalization.MultiplayerTimeControlEveryone)
        };
        public string EnableShiftGameSpeedStepsText => SerpLocalization.Get(SerpLocalization.EnableShiftGameSpeedSteps);
        public string EnableShiftGameSpeedStepsHelpText => SerpLocalization.Get(SerpLocalization.EnableShiftGameSpeedStepsHelp);
        public string EnableResyncHostKickText => SerpLocalization.Get("BugfixesAndQoL.EnableResyncHostKick");
        public string EnableResyncHostKickHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableResyncHostKickHelp");
        public string EnableAbruptHostMigrationFixText => SerpLocalization.Get("BugfixesAndQoL.EnableAbruptHostMigrationFix");
        public string EnableAbruptHostMigrationFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableAbruptHostMigrationFixHelp");
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
        public string EnableTroopHudMiddleClickCameraJumpText =>
            SerpLocalization.Get("BugfixesAndQoL.EnableTroopHudMiddleClickCameraJump");
        public string EnableTroopHudMiddleClickCameraJumpHelpText =>
            SerpLocalization.Get("BugfixesAndQoL.EnableTroopHudMiddleClickCameraJumpHelp");
        public string EnableDisbandedUnitControlGroupCleanupText =>
            SerpLocalization.Get("BugfixesAndQoL.EnableDisbandedUnitControlGroupCleanup");
        public string EnableDisbandedUnitControlGroupCleanupHelpText =>
            SerpLocalization.Get("BugfixesAndQoL.EnableDisbandedUnitControlGroupCleanupHelp");
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
        public string ShowVanillaMapsInEditorText => SerpLocalization.Get("BugfixesAndQoL.ShowVanillaMapsInEditor");
        public string ShowVanillaMapsInEditorHelpText => SerpLocalization.Get("BugfixesAndQoL.ShowVanillaMapsInEditorHelp");
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
        public string FriendlyMoatMovementModeText => SerpLocalization.Get("BugfixesAndQoL.FriendlyMoatMovementMode");
        public string FriendlyMoatMovementModeHelpText => SerpLocalization.Get("BugfixesAndQoL.FriendlyMoatMovementModeHelp");
        public string[] FriendlyMoatMovementModeOptions => new[]
        {
            SerpLocalization.Get("BugfixesAndQoL.FriendlyMoatMovementDisabled"),
            SerpLocalization.Get("BugfixesAndQoL.FriendlyMoatMovementExact"),
            SerpLocalization.Get("BugfixesAndQoL.FriendlyMoatMovementRequiredOnly")
        };
        public string EnableImprovedMoatFillingText => SerpLocalization.Get(SerpLocalization.EnableImprovedMoatFilling);
        public string EnableImprovedMoatFillingHelpText => SerpLocalization.Get(SerpLocalization.EnableImprovedMoatFillingHelp);
        public string EnableMountedStockpileMovementFixText => SerpLocalization.Get("BugfixesAndQoL.EnableMountedStockpileMovementFix");
        public string EnableMountedStockpileMovementFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableMountedStockpileMovementFixHelp");
        public string EnableHealerAttackCommandFixText => SerpLocalization.Get("BugfixesAndQoL.EnableHealerAttackCommandFix");
        public string EnableHealerAttackCommandFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableHealerAttackCommandFixHelp");
        public string EnableFastRecruitRallyMovementText => SerpLocalization.Get(SerpLocalization.EnableFastRecruitRallyMovement);
        public string EnableFastRecruitRallyMovementHelpText => SerpLocalization.Get(SerpLocalization.EnableFastRecruitRallyMovementHelp);
        public string EnableAIQuarryPileTowardsKeepText => SerpLocalization.Get(SerpLocalization.EnableAIQuarryPileTowardsKeep);
        public string EnableAIQuarryPileTowardsKeepHelpText => SerpLocalization.Get(SerpLocalization.EnableAIQuarryPileTowardsKeepHelp);
        public string EnableImprovedAssassinPathfindingText => SerpLocalization.Get(SerpLocalization.EnableImprovedAssassinPathfinding);
        public string EnableImprovedAssassinPathfindingHelpText => SerpLocalization.Get(SerpLocalization.EnableImprovedAssassinPathfindingHelp);
        public string EnableAssassinCombatResumeFixText => SerpLocalization.Get(SerpLocalization.EnableAssassinCombatResumeFix);
        public string EnableAssassinCombatResumeFixHelpText => SerpLocalization.Get(SerpLocalization.EnableAssassinCombatResumeFixHelp);
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
        public bool[] EnableTroopHudMiddleClickCameraJumpData => enableTroopHudMiddleClickCameraJump.Data;
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
        public bool EnableTroopHudMiddleClickCameraJump
        {
            get => enableTroopHudMiddleClickCameraJump.Value;
            set => SetPlayerSetting(
                enableTroopHudMiddleClickCameraJump,
                value,
                nameof(EnableTroopHudMiddleClickCameraJump));
        }

        [Shared.PresetLocal]
        public bool EnableDisbandedUnitControlGroupCleanup
        {
            get => enableDisbandedUnitControlGroupCleanup;
            set => SetSetting(
                ref enableDisbandedUnitControlGroupCleanup,
                value,
                nameof(EnableDisbandedUnitControlGroupCleanup));
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
        public bool ShowVanillaMapsInEditor
        {
            get => showVanillaMapsInEditor;
            set => SetSetting(ref showVanillaMapsInEditor, value, nameof(ShowVanillaMapsInEditor));
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

        [SyncHostOnly]
        public bool AllowFullAiMultiplayerLobby
        {
            get => allowFullAiMultiplayerLobby;
            set => SetSetting(ref allowFullAiMultiplayerLobby, value, nameof(AllowFullAiMultiplayerLobby));
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
        public bool EnableAiDefensePatrolFix
        {
            get => enableAiDefensePatrolFix;
            set => SetSetting(ref enableAiDefensePatrolFix, value, nameof(EnableAiDefensePatrolFix));
        }

        [SyncHostOnly]
        public bool EnableAivDefenderPositionFix
        {
            get => enableAivDefenderPositionFix;
            set => SetSetting(ref enableAivDefenderPositionFix, value, nameof(EnableAivDefenderPositionFix));
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
        public int FriendlyMoatMovementMode
        {
            get => friendlyMoatMovementMode;
            set
            {
                int validated = FriendlyMoatMovementPolicy.Normalize(value);
                int previous = friendlyMoatMovementMode;
                SetSetting(ref friendlyMoatMovementMode, validated, nameof(FriendlyMoatMovementMode));
                if (previous != friendlyMoatMovementMode)
                    OnPropertyChanged(nameof(FriendlyMoatMovementModeIndex));
            }
        }

        // The transient index keeps Noesis binding separate from the synchronized setting.
        public int FriendlyMoatMovementModeIndex
        {
            get => FriendlyMoatMovementMode;
            set => FriendlyMoatMovementMode = value;
        }

        internal FriendlyMoatMovementMode GetFriendlyMoatMovementMode() =>
            (BugfixesAndQoL.FriendlyMoatMovementMode)
                FriendlyMoatMovementPolicy.Normalize(FriendlyMoatMovementMode);

        [SyncHostOnly]
        public bool EnableImprovedMoatFilling
        {
            get => enableImprovedMoatFilling;
            set => SetSetting(ref enableImprovedMoatFilling, value, nameof(EnableImprovedMoatFilling));
        }

        [SyncHostOnly]
        public bool EnableMountedStockpileMovementFix
        {
            get => enableMountedStockpileMovementFix;
            set => SetSetting(ref enableMountedStockpileMovementFix, value, nameof(EnableMountedStockpileMovementFix));
        }

        [SyncHostOnly]
        public bool EnableHealerAttackCommandFix
        {
            get => enableHealerAttackCommandFix;
            set => SetSetting(ref enableHealerAttackCommandFix, value, nameof(EnableHealerAttackCommandFix));
        }

        [SyncHostOnly]
        public bool EnableFastRecruitRallyMovement
        {
            get => enableFastRecruitRallyMovement;
            set => SetSetting(ref enableFastRecruitRallyMovement, value, nameof(EnableFastRecruitRallyMovement));
        }

        [SyncHostOnly]
        public bool EnableSingleBuildingPause
        {
            get => enableSingleBuildingPause;
            set => SetSetting(ref enableSingleBuildingPause, value, nameof(EnableSingleBuildingPause));
        }

        [SyncHostOnly]
        public bool EnableShiftRepairAllBuildings
        {
            get => enableShiftRepairAllBuildings;
            set => SetSetting(ref enableShiftRepairAllBuildings, value, nameof(EnableShiftRepairAllBuildings));
        }

        [SyncHostOnly]
        public bool EnableQuarryPileRelocation
        {
            get => enableQuarryPileRelocation;
            set => SetSetting(ref enableQuarryPileRelocation, value, nameof(EnableQuarryPileRelocation));
        }

        [SyncHostOnly]
        public bool EnableAIQuarryPileTowardsKeep
        {
            get => enableAIQuarryPileTowardsKeep;
            set => SetSetting(ref enableAIQuarryPileTowardsKeep, value, nameof(EnableAIQuarryPileTowardsKeep));
        }

        [SyncHostOnly]
        public bool RequireReachableEnemyForAutomaticGateClosing
        {
            get => requireReachableEnemyForAutomaticGateClosing;
            set => SetSetting(ref requireReachableEnemyForAutomaticGateClosing, value, nameof(RequireReachableEnemyForAutomaticGateClosing));
        }

        [SyncHostOnly]
        public bool PreventAIPause
        {
            get => preventAIPause;
            set => SetSetting(ref preventAIPause, value, nameof(PreventAIPause));
        }

        [SyncHostOnly]
        public bool PreventEmergencyDemolition
        {
            get => preventEmergencyDemolition;
            set => SetSetting(ref preventEmergencyDemolition, value, nameof(PreventEmergencyDemolition));
        }

        [SyncHostOnly]
        public bool PreventHovelDeletion
        {
            get => preventHovelDeletion;
            set => SetSetting(ref preventHovelDeletion, value, nameof(PreventHovelDeletion));
        }

        [SyncHostOnly]
        public int InaccessibleAIBuildingDemolitionProtection
        {
            get => inaccessibleAIBuildingDemolitionProtection;
            set => SetBoundedIntSetting(
                ref inaccessibleAIBuildingDemolitionProtection,
                value,
                TemporaryGateBlockagePolicy.VanillaMode,
                TemporaryGateBlockagePolicy.AlwaysPreventMode,
                nameof(InaccessibleAIBuildingDemolitionProtection),
                nameof(InaccessibleAIBuildingDemolitionProtectionValueText));
        }

        [SyncHostOnly]
        public bool EnableImprovedAssassinPathfinding
        {
            get => enableImprovedAssassinPathfinding;
            set => SetSetting(ref enableImprovedAssassinPathfinding, value, nameof(EnableImprovedAssassinPathfinding));
        }

        [SyncHostOnly]
        public bool EnableAssassinCombatResumeFix
        {
            get => enableAssassinCombatResumeFix;
            set => SetSetting(ref enableAssassinCombatResumeFix, value, nameof(EnableAssassinCombatResumeFix));
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
        public bool EnableAbruptHostMigrationFix
        {
            get => enableAbruptHostMigrationFix;
            set => SetSetting(ref enableAbruptHostMigrationFix, value, nameof(EnableAbruptHostMigrationFix));
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
        public MultiplayerTimeControlPermission EnableMultiplayerGameSpeedChanges
        {
            get => enableMultiplayerGameSpeedChanges;
            set
            {
                MultiplayerTimeControlPermission previous = enableMultiplayerGameSpeedChanges;
                SetSetting(ref enableMultiplayerGameSpeedChanges, value, nameof(EnableMultiplayerGameSpeedChanges));
                if (previous != enableMultiplayerGameSpeedChanges)
                    OnPropertyChanged(nameof(MultiplayerTimeControlPermissionIndex));
            }
        }

        // The ComboBox index is intentionally transient; the classified enum remains the sole stored value.
        public int MultiplayerTimeControlPermissionIndex
        {
            get => (int)EnableMultiplayerGameSpeedChanges;
            set
            {
                var permission = (MultiplayerTimeControlPermission)value;
                if (MultiplayerTimeControlPolicy.IsDefinedPermission(permission))
                    EnableMultiplayerGameSpeedChanges = permission;
            }
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
                AllowFullAiMultiplayerLobby = true;
                EnableAiFixes = true;
                EnableAiDefensePatrolFix = true;
                EnableAivDefenderPositionFix = true;
                FixAITowerRepair = true;
                BetterAIOverbuildRules = true;
                RememberAiAivSettings = true;
                EnableCustomLordListEnhancements = true;
                EnableTroopMovementFix = true;
                FriendlyMoatMovementMode = FriendlyMoatMovementPolicy.DefaultMode;
                EnableImprovedMoatFilling = true;
                EnableMountedStockpileMovementFix = true;
                EnableHealerAttackCommandFix = true;
                EnableFastRecruitRallyMovement = true;
                EnableSingleBuildingPause = true;
                EnableShiftRepairAllBuildings = true;
                EnableQuarryPileRelocation = true;
                EnableAIQuarryPileTowardsKeep = true;
                RequireReachableEnemyForAutomaticGateClosing = true;
                PreventAIPause = true;
                PreventEmergencyDemolition = true;
                PreventHovelDeletion = true;
                InaccessibleAIBuildingDemolitionProtection =
                    TemporaryGateBlockagePolicy.ImprovedReachabilityMode;
                EnableImprovedAssassinPathfinding = true;
                EnableAssassinCombatResumeFix = true;
                EnablePlaguePopularityFix = true;
                EnablePlagueCloudRemovalFix = true;
                EnableStuckApothecaryFix = true;
                EnablePlagueTargetReservationFix = true;
                EnableAssemblyPointPlacementFix = true;
                EnableFairSiegeAmmoRestock = true;
                EnableSurrenderAndStatistics = true;
                EnableLordUnitControls = true;
                EnableEliminatedPlayersBecomeSpectators = true;
                EnableAbruptHostMigrationFix = true;
                EnableResyncHostKick = true;
                EnableReturnToMultiplayerLobby = true;
                EnableCtrlSingleMarketTrade = true;
                EnableMultiplayerGameSpeedChanges = MultiplayerTimeControlPermission.OnlyHost;
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
            EnableTroopHudMiddleClickCameraJump = true;
            EnableDisbandedUnitControlGroupCleanup = true;
            EnableCustomTrailExtremeGoldFix = true;
            ShowVanillaMapsInEditor = true;
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

        private void SetBoundedIntSetting(
            ref int field,
            int value,
            int minimum,
            int maximum,
            string propertyName,
            string textPropertyName)
        {
            if (!CanMutateSettingWithDependents(propertyName, textPropertyName))
                return;

            int clamped = Math.Max(minimum, Math.Min(maximum, value));
            if (field == clamped)
                return;

            field = clamped;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
            OnPropertyChanged(textPropertyName);
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
