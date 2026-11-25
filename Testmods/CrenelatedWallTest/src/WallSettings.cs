using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;

namespace CrenelatedWallTest
{
    public sealed class WallSettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true, adjustHeights = true;
        public WallSettings()
        {
            ResetToDefaultCommand = new RelayCommand(() =>
            {
                if (!CanEditHostSettings) return;
                EnableMod = true; AdjustHeights = true;
            });
        }
        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set { if (!CanMutateSetting() || value == enableMod) return; enableMod = value; OnPropertyChanged(nameof(EnableMod)); }
        }
        // Alternating heights (normalization and graphics). Frozen per session like EnableMod.
        [SyncHostOnly]
        public bool AdjustHeights
        {
            get => adjustHeights;
            set { if (!CanMutateSetting() || value == adjustHeights) return; adjustHeights = value; OnPropertyChanged(nameof(AdjustHeights)); }
        }
        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string ScopeText => SerpLocalization.Get("Crenel.Scope");
        public string AdjustHeightsText => SerpLocalization.Get("Crenel.AdjustHeights");
        public string AdjustHeightsHelpText => SerpLocalization.Get("Crenel.AdjustHeightsHelp");
        public string HeightRuleText => SerpLocalization.Get("Crenel.HeightRule");
        protected override string ResolveSettingsUiText(string key, string fallback)
        {
            string text = SerpLocalization.Get(key); return text == key ? fallback : text;
        }
    }
}
