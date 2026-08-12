// Feature: Lobby settings model for the Bugfixes and QoL features.
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using Noesis;

namespace BugfixesAndQoL
{
    public sealed class BugfixesAndQoLViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        public event Action<string> SettingChanged;

        private bool enableMod = true;
        private bool rememberAiAivSettings = true;
        private bool enableTroopMovementFix = true;
        private bool enablePlaguePopularityFix = true;
        private bool enablePlagueCloudRemovalFix = true;
        private bool enableStuckApothecaryFix = true;
        private bool enablePlagueTargetReservationFix = true;
        private bool enableAssemblyPointPlacementFix = true;
        private readonly LocalPerPlayerSetting<bool> enableClientFeatures = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableMinimapCursorFollowFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableMarketKeyMainMenuFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableAutoTradeSellZeroFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> enableEnemyProximityBulldozeCursorFix = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> allowMinimapWhilePlacingBuilding = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> allowCameraMovementWithModifiers = new LocalPerPlayerSetting<bool>(true);
        private readonly LocalPerPlayerSetting<bool> hdMarketView = new LocalPerPlayerSetting<bool>(true);

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public BugfixesAndQoLViewModel(bool legacySomeSettingsLoaded)
        {
            LegacyModWarningVisibility = legacySomeSettingsLoaded ? Visibility.Visible : Visibility.Collapsed;
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public RelayCommand ResetToDefaultCommand { get; }
        public Visibility LegacyModWarningVisibility { get; }
        public string LegacyModWarningText => SerpLocalization.Get(SerpLocalization.LegacySomeSettingsWarning);
        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public string EnableClientFeaturesText => SerpLocalization.Get("BugfixesAndQoL.EnableClientFeatures");
        public string EnableClientFeaturesHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableClientFeaturesHelp");
        public string EnableHostFeaturesText => SerpLocalization.Get("BugfixesAndQoL.EnableHostFeatures");
        public string EnableHostFeaturesHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableHostFeaturesHelp");
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string ClientInterfaceTitleText => SerpLocalization.Get("BugfixesAndQoL.ClientInterfaceTitle");
        public string AiAivTitleText => SerpLocalization.Get("BugfixesAndQoL.AiAivTitle");
        public string TroopMovementTitleText => SerpLocalization.Get("BugfixesAndQoL.TroopMovementTitle");
        public string PlagueTitleText => SerpLocalization.Get("BugfixesAndQoL.PlagueTitle");
        public string GameplayTitleText => SerpLocalization.Get("BugfixesAndQoL.GameplayTitle");
        public string EnableMinimapCursorFollowFixText => SerpLocalization.Get("BugfixesAndQoL.EnableMinimapCursorFollowFix");
        public string EnableMinimapCursorFollowFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableMinimapCursorFollowFixHelp");
        public string EnableMarketKeyMainMenuFixText => SerpLocalization.Get("BugfixesAndQoL.EnableMarketKeyMainMenuFix");
        public string EnableMarketKeyMainMenuFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableMarketKeyMainMenuFixHelp");
        public string EnableAutoTradeSellZeroFixText => SerpLocalization.Get("BugfixesAndQoL.EnableAutoTradeSellZeroFix");
        public string EnableAutoTradeSellZeroFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableAutoTradeSellZeroFixHelp");
        public string EnableEnemyProximityBulldozeCursorFixText => SerpLocalization.Get("BugfixesAndQoL.EnableEnemyProximityBulldozeCursorFix");
        public string EnableEnemyProximityBulldozeCursorFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableEnemyProximityBulldozeCursorFixHelp");
        public string EnableAssemblyPointPlacementFixText => SerpLocalization.Get("BugfixesAndQoL.EnableAssemblyPointPlacementFix");
        public string EnableAssemblyPointPlacementFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnableAssemblyPointPlacementFixHelp");
        public string AllowMinimapWhilePlacingBuildingText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuilding);
        public string AllowMinimapWhilePlacingBuildingHelpText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuildingHelp);
        public string RememberAiAivSettingsText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettings);
        public string RememberAiAivSettingsHelpText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettingsHelp);
        public string EnableTroopMovementFixText => SerpLocalization.Get(SerpLocalization.EnableTroopMovementFix);
        public string EnableTroopMovementFixHelpText => SerpLocalization.Get(SerpLocalization.EnableTroopMovementFixHelp);
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

        public bool[] EnableMinimapCursorFollowFixData => enableMinimapCursorFollowFix.Data;
        public bool[] EnableMarketKeyMainMenuFixData => enableMarketKeyMainMenuFix.Data;
        public bool[] EnableAutoTradeSellZeroFixData => enableAutoTradeSellZeroFix.Data;
        public bool[] EnableEnemyProximityBulldozeCursorFixData => enableEnemyProximityBulldozeCursorFix.Data;
        public bool[] EnableClientFeaturesData => enableClientFeatures.Data;
        public bool[] AllowMinimapWhilePlacingBuildingData => allowMinimapWhilePlacingBuilding.Data;
        public bool[] AllowCameraMovementWithModifiersData => allowCameraMovementWithModifiers.Data;
        public bool[] HdMarketViewData => hdMarketView.Data;

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
        public bool EnableClientFeatures
        {
            get => enableClientFeatures.Value;
            set => SetPlayerSetting(enableClientFeatures, value, nameof(EnableClientFeatures));
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

        [SyncHostOnly]
        public bool RememberAiAivSettings
        {
            get => rememberAiAivSettings;
            set => SetSetting(ref rememberAiAivSettings, value, nameof(RememberAiAivSettings));
        }

        [SyncHostOnly]
        public bool EnableTroopMovementFix
        {
            get => enableTroopMovementFix;
            set => SetSetting(ref enableTroopMovementFix, value, nameof(EnableTroopMovementFix));
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

        private void ResetToDefault()
        {
            if (CanEditHostSettings)
            {
                EnableMod = true;
                RememberAiAivSettings = true;
                EnableTroopMovementFix = true;
                EnablePlaguePopularityFix = true;
                EnablePlagueCloudRemovalFix = true;
                EnableStuckApothecaryFix = true;
                EnablePlagueTargetReservationFix = true;
                EnableAssemblyPointPlacementFix = true;
            }

            // Every participant resets only their own per-player preferences.
            EnableClientFeatures = true;
            AllowMinimapWhilePlacingBuilding = true;
            AllowCameraMovementWithModifiers = true;
            HdMarketView = true;
            EnableMinimapCursorFollowFix = true;
            EnableMarketKeyMainMenuFix = true;
            EnableAutoTradeSellZeroFix = true;
            EnableEnemyProximityBulldozeCursorFix = true;
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
            allowMinimapWhilePlacingBuilding.TrySetLocalPlayerId(playerId);
            allowCameraMovementWithModifiers.TrySetLocalPlayerId(playerId);
            hdMarketView.TrySetLocalPlayerId(playerId);
            return true;
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
