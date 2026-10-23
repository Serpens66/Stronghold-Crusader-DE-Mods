using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;

namespace CrenelatedWallTest
{
    public sealed class WallSettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        public WallSettings() { ResetToDefaultCommand = new RelayCommand(() => { if (CanEditHostSettings) EnableMod = true; }); }
        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set { if (!CanMutateSetting() || value == enableMod) return; enableMod = value; OnPropertyChanged(nameof(EnableMod)); }
        }
        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string ScopeText => SerpLocalization.Get("Crenel.Scope");
        public string RuleText => SerpLocalization.Get("Crenel.Rule");
        protected override string ResolveSettingsUiText(string key, string fallback)
        {
            string text = SerpLocalization.Get(key); return text == key ? fallback : text;
        }
    }
}
