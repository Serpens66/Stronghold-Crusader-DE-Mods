// Feature: Lobby settings model for the Bugfixes and QoL features.
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace BugfixesAndQoL
{
    public sealed class BugfixesAndQoLViewModel : LobbyModSettingsBaseViewModel
    {
        public event Action<string> SettingChanged;

        private bool enableMod = true;
        private bool rememberAiAivSettings = true;
        private bool enableTroopMovementFix = true;
        private readonly bool[] allowMinimapWhilePlacingBuildingData = new bool[9];

        public BugfixesAndQoLViewModel()
        {
            SetAllowMinimapDefaults();
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

        public bool[] AllowMinimapWhilePlacingBuildingData => allowMinimapWhilePlacingBuildingData;

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
            SettingChanged?.Invoke(nameof(AllowMinimapWhilePlacingBuilding));
            OnPropertyChanged(nameof(AllowMinimapWhilePlacingBuilding));
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
    }
}
