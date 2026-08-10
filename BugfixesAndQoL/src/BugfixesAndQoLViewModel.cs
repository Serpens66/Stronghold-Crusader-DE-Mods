// Feature: Lobby settings model for the Bugfixes and QoL features.
using SHCDESE.API;
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
        private readonly bool[] enableClientFeaturesData = new bool[9];
        private readonly bool[] enableMinimapCursorFollowFixData = new bool[9];
        private readonly bool[] enableMarketKeyMainMenuFixData = new bool[9];
        private readonly bool[] enableAutoTradeSellZeroFixData = new bool[9];
        private readonly bool[] enableEnemyProximityBulldozeCursorFixData = new bool[9];
        private readonly bool[] allowMinimapWhilePlacingBuildingData = new bool[9];
        private readonly bool[] allowCameraMovementWithModifiersData = new bool[9];
        private readonly bool[] hdMarketViewData = new bool[9];

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public BugfixesAndQoLViewModel(bool legacySomeSettingsLoaded)
        {
            LegacyModWarningVisibility = legacySomeSettingsLoaded ? Visibility.Visible : Visibility.Collapsed;
            SetClientFeatureDefaults();
            SetClientBugfixDefaults();
            SetAllowMinimapDefaults();
            SetAllowCameraMovementWithModifiersDefaults();
            SetHdMarketViewDefaults();
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

        public bool[] AllowMinimapWhilePlacingBuildingData => allowMinimapWhilePlacingBuildingData;
        public bool[] AllowCameraMovementWithModifiersData => allowCameraMovementWithModifiersData;
        public bool[] HdMarketViewData => hdMarketViewData;

        [SyncPerPlayer]
        public bool EnableMinimapCursorFollowFix
        {
            get => enableMinimapCursorFollowFixData[LocalPlayerIdOrOne];
            set => SetPlayerSetting(enableMinimapCursorFollowFixData, value, nameof(EnableMinimapCursorFollowFix));
        }

        [SyncPerPlayer]
        public bool EnableMarketKeyMainMenuFix
        {
            get => enableMarketKeyMainMenuFixData[LocalPlayerIdOrOne];
            set => SetPlayerSetting(enableMarketKeyMainMenuFixData, value, nameof(EnableMarketKeyMainMenuFix));
        }

        [SyncPerPlayer]
        public bool EnableAutoTradeSellZeroFix
        {
            get => enableAutoTradeSellZeroFixData[LocalPlayerIdOrOne];
            set => SetPlayerSetting(enableAutoTradeSellZeroFixData, value, nameof(EnableAutoTradeSellZeroFix));
        }

        [SyncPerPlayer]
        public bool EnableEnemyProximityBulldozeCursorFix
        {
            get => enableEnemyProximityBulldozeCursorFixData[LocalPlayerIdOrOne];
            set => SetPlayerSetting(enableEnemyProximityBulldozeCursorFixData, value, nameof(EnableEnemyProximityBulldozeCursorFix));
        }

        [SyncPerPlayer]
        public bool EnableClientFeatures
        {
            get => enableClientFeaturesData[LocalPlayerIdOrOne];
            set
            {
                int playerId = LocalPlayerIdOrOne;
                if (enableClientFeaturesData[playerId] == value)
                    return;

                enableClientFeaturesData[playerId] = value;
                SettingChanged?.Invoke(nameof(EnableClientFeatures));
                OnPropertyChanged(nameof(EnableClientFeatures));
            }
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
            get => allowMinimapWhilePlacingBuildingData[LocalPlayerIdOrOne];
            set
            {
                int playerId = LocalPlayerIdOrOne;
                if (allowMinimapWhilePlacingBuildingData[playerId] == value)
                    return;

                allowMinimapWhilePlacingBuildingData[playerId] = value;
                SettingChanged?.Invoke(nameof(AllowMinimapWhilePlacingBuilding));
                OnPropertyChanged(nameof(AllowMinimapWhilePlacingBuilding));
            }
        }

        [SyncPerPlayer]
        public bool AllowCameraMovementWithModifiers
        {
            get => allowCameraMovementWithModifiersData[LocalPlayerIdOrOne];
            set
            {
                int playerId = LocalPlayerIdOrOne;
                if (allowCameraMovementWithModifiersData[playerId] == value)
                    return;

                allowCameraMovementWithModifiersData[playerId] = value;
                SettingChanged?.Invoke(nameof(AllowCameraMovementWithModifiers));
                OnPropertyChanged(nameof(AllowCameraMovementWithModifiers));
            }
        }

        [SyncPerPlayer]
        public bool HdMarketView
        {
            get => hdMarketViewData[LocalPlayerIdOrOne];
            set
            {
                int playerId = LocalPlayerIdOrOne;
                if (hdMarketViewData[playerId] == value)
                    return;

                hdMarketViewData[playerId] = value;
                SettingChanged?.Invoke(nameof(HdMarketView));
                OnPropertyChanged(nameof(HdMarketView));
            }
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
            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private void SetPlayerSetting(bool[] values, bool value, string propertyName)
        {
            int playerId = LocalPlayerIdOrOne;
            if (values[playerId] == value)
                return;

            values[playerId] = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private static int LocalPlayerIdOrOne => Math.Max(1, GameNetworkAPI.GetLocalPlayerId());

        private void SetClientFeatureDefaults()
        {
            for (int i = 1; i < enableClientFeaturesData.Length; i++)
                enableClientFeaturesData[i] = true;
        }

        private void SetClientBugfixDefaults()
        {
            for (int i = 1; i < enableMinimapCursorFollowFixData.Length; i++)
            {
                enableMinimapCursorFollowFixData[i] = true;
                enableMarketKeyMainMenuFixData[i] = true;
                enableAutoTradeSellZeroFixData[i] = true;
                enableEnemyProximityBulldozeCursorFixData[i] = true;
            }
        }

        private void SetAllowMinimapDefaults()
        {
            for (int i = 1; i < allowMinimapWhilePlacingBuildingData.Length; i++)
                allowMinimapWhilePlacingBuildingData[i] = true;
        }

        private void SetAllowCameraMovementWithModifiersDefaults()
        {
            for (int i = 1; i < allowCameraMovementWithModifiersData.Length; i++)
                allowCameraMovementWithModifiersData[i] = true;
        }

        private void SetHdMarketViewDefaults()
        {
            for (int i = 1; i < hdMarketViewData.Length; i++)
                hdMarketViewData[i] = true;
        }
    }
}
