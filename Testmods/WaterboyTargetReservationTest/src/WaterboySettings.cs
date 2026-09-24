using SHCDESE.API.Components.Network;

namespace WaterboyTargetReservationTest
{
    public sealed class WaterboySettings : Shared.PresetLobbyModSettingsViewModel
    {
        private readonly PerPlayerModeState targetingMode = new PerPlayerModeState();

        public event System.Action<string> SettingChanged;

        [SyncPerPlayer]
        public bool EnableNearestWaterboyTargeting
        {
            get => targetingMode.LocalValue;
            set
            {
                if (!targetingMode.SetLocalValue(value))
                    return;
                SettingChanged?.Invoke(nameof(EnableNearestWaterboyTargeting));
                OnPropertyChanged(nameof(EnableNearestWaterboyTargeting));
                OnPropertyChanged(nameof(EnableNearestWaterboyTargetingData));
            }
        }

        public bool[] EnableNearestWaterboyTargetingData => targetingMode.Data;
        public string EnableNearestWaterboyTargetingText => "Nearest water carrier per fire";
        public string EnableNearestWaterboyTargetingHelpText =>
            "Assigns one water carrier to each fire and lets a closer idle carrier take over from one that is still walking.";

        protected override void ConfigurePerPlayerLobbySettings(
            Shared.PerPlayerLobbySettingsBuilder settings)
        {
            settings
                .ResetSlotsWith(nameof(EnableNearestWaterboyTargeting), () => true)
                .WhenLocalPlayerResolved(SetLocalPlayerId);
        }

        internal bool IsEnabledForPlayer(int playerId) =>
            IsValidPlayerId(playerId) && targetingMode.Data[playerId];

        internal void ApplyChoreValue(int playerId, bool enabled, bool isLocalPlayer)
        {
            if (!IsValidPlayerId(playerId))
                return;
            targetingMode.SetPlayerValue(playerId, enabled, isLocalPlayer);
            if (isLocalPlayer)
            {
                SettingChanged?.Invoke(nameof(EnableNearestWaterboyTargeting));
                OnPropertyChanged(nameof(EnableNearestWaterboyTargeting));
            }
            OnPropertyChanged(nameof(EnableNearestWaterboyTargetingData));
        }

        private void SetLocalPlayerId(int playerId)
        {
            if (!IsValidPlayerId(playerId))
                return;
            targetingMode.ResolveLocalPlayer(playerId);
            OnPropertyChanged(nameof(EnableNearestWaterboyTargetingData));
        }

        private static bool IsValidPlayerId(int playerId) => playerId >= 1 && playerId <= 8;
    }
}
