using SHCDESE.API.Components.Network;

namespace WaterboyTargetReservationTest
{
    public sealed class WaterboySettings : Shared.PresetLobbyModSettingsViewModel
    {
        private readonly PerPlayerModeState targetingMode = new PerPlayerModeState();

        [SyncPerPlayer]
        public bool EnableNearestWaterboyTargeting
        {
            get => targetingMode.LocalValue;
            set
            {
                if (!targetingMode.SetLocalValue(value))
                    return;
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

        internal bool ResolveEffectiveMode(bool realMultiplayer, int playerId,
            int localPlayerId) =>
            WaterboyModePolicy.Resolve(realMultiplayer, playerId, localPlayerId,
                targetingMode.LocalValue, targetingMode.Data);

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
