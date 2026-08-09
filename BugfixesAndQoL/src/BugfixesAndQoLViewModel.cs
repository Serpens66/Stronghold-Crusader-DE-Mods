// Feature: Lobby settings model for the Bugfixes and QoL features.
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace BugfixesAndQoL
{
    public sealed class BugfixesAndQoLViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        public event Action<string> SettingChanged;

        private bool enableMod = true;
        private bool rememberAiAivSettings = true;
        private bool enableTroopMovementFix = true;
        private readonly bool[] allowMinimapWhilePlacingBuildingData = new bool[9];
        private readonly bool[] allowCameraMovementWithModifiersData = new bool[9];
        private readonly bool[] hdMarketViewData = new bool[9];

        public BugfixesAndQoLViewModel()
        {
            SetAllowMinimapDefaults();
            SetAllowCameraMovementWithModifiersDefaults();
            SetHdMarketViewDefaults();
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public RelayCommand ResetToDefaultCommand { get; }
        public string EnableModText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string AlwaysActiveTitleText => SerpLocalization.Get(SerpLocalization.AlwaysActiveTitle);
        public string AlwaysActiveHelpText => SerpLocalization.Get(SerpLocalization.AlwaysActiveHelp);
        public string AllowMinimapWhilePlacingBuildingText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuilding);
        public string AllowMinimapWhilePlacingBuildingHelpText => SerpLocalization.Get(SerpLocalization.AllowMinimapWhilePlacingBuildingHelp);
        public string RememberAiAivSettingsText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettings);
        public string RememberAiAivSettingsHelpText => SerpLocalization.Get(SerpLocalization.RememberAiAivSettingsHelp);
        public string EnableTroopMovementFixText => SerpLocalization.Get(SerpLocalization.EnableTroopMovementFix);
        public string EnableTroopMovementFixHelpText => SerpLocalization.Get(SerpLocalization.EnableTroopMovementFixHelp);
        public string AllowCameraMovementWithModifiersText => SerpLocalization.Get(SerpLocalization.AllowCameraMovementWithModifiers);
        public string AllowCameraMovementWithModifiersHelpText => SerpLocalization.Get(SerpLocalization.AllowCameraMovementWithModifiersHelp);
        public string HdMarketViewText => SerpLocalization.Get(SerpLocalization.HdMarketView);
        public string HdMarketViewHelpText => SerpLocalization.Get(SerpLocalization.HdMarketViewHelp);

        public bool[] AllowMinimapWhilePlacingBuildingData => allowMinimapWhilePlacingBuildingData;
        public bool[] AllowCameraMovementWithModifiersData => allowCameraMovementWithModifiersData;
        public bool[] HdMarketViewData => hdMarketViewData;

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

        private void ResetToDefault()
        {
            RememberAiAivSettings = true;
            EnableTroopMovementFix = true;
            SetAllowMinimapDefaults();
            SetAllowCameraMovementWithModifiersDefaults();
            SetHdMarketViewDefaults();
            SettingChanged?.Invoke(nameof(AllowMinimapWhilePlacingBuilding));
            OnPropertyChanged(nameof(AllowMinimapWhilePlacingBuilding));
            SettingChanged?.Invoke(nameof(AllowCameraMovementWithModifiers));
            OnPropertyChanged(nameof(AllowCameraMovementWithModifiers));
            SettingChanged?.Invoke(nameof(HdMarketView));
            OnPropertyChanged(nameof(HdMarketView));
        }

        private void SetSetting<T>(ref T field, T value, string propertyName)
        {
            if (Equals(field, value))
                return;

            field = value;
            SettingChanged?.Invoke(propertyName);
            OnPropertyChanged(propertyName);
        }

        private static int LocalPlayerIdOrOne => Math.Max(1, GameNetworkAPI.GetLocalPlayerId());

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
