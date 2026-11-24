using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;

namespace CrenelatedWallTest
{
    public sealed class WallSettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true, adjustHeights = true, blockPlacements;
        public WallSettings()
        {
            ResetToDefaultCommand = new RelayCommand(() =>
            {
                if (!CanEditHostSettings) return;
                EnableMod = true; AdjustHeights = true; BlockPlacements = false;
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
        // Rejection of unenterable shapes; off by default while its rules are still open.
        [SyncHostOnly]
        public bool BlockPlacements
        {
            get => blockPlacements;
            set { if (!CanMutateSetting() || value == blockPlacements) return; blockPlacements = value; OnPropertyChanged(nameof(BlockPlacements)); }
        }
        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string ScopeText => SerpLocalization.Get("Crenel.Scope");
        public string AdjustHeightsText => SerpLocalization.Get("Crenel.AdjustHeights");
        public string AdjustHeightsHelpText => SerpLocalization.Get("Crenel.AdjustHeightsHelp");
        public string BlockPlacementsText => SerpLocalization.Get("Crenel.BlockPlacements");
        public string BlockPlacementsHelpText => SerpLocalization.Get("Crenel.BlockPlacementsHelp");
        public string HeightRuleText => SerpLocalization.Get("Crenel.HeightRule");
        public string BlockRuleText => SerpLocalization.Get("Crenel.BlockRule");
        protected override string ResolveSettingsUiText(string key, string fallback)
        {
            string text = SerpLocalization.Get(key); return text == key ? fallback : text;
        }
    }
}
