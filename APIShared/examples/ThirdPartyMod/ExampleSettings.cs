using APIShared.ModSettings;
using SHCDESE.API.Components.Network;

namespace ThirdPartyMod
{
    public sealed class ExampleSettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        private bool showOverlay = true;

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set { if (!CanMutateSetting() || value == enableMod) return; enableMod = value; OnPropertyChanged(nameof(EnableMod)); }
        }

        [PresetLocal]
        public bool ShowOverlay
        {
            get => showOverlay;
            set { if (!CanMutateSetting() || value == showOverlay) return; showOverlay = value; OnPropertyChanged(nameof(ShowOverlay)); }
        }
    }
}
