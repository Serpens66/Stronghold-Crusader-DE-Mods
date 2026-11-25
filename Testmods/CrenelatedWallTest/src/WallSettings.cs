using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;

namespace CrenelatedWallTest
{
    public sealed class WallSettings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true, adjustHeights = true, existingInEditor, existingOnSaveLoad, existingOnMapStart;
        public WallSettings()
        {
            ResetToDefaultCommand = new RelayCommand(() =>
            {
                if (!CanEditHostSettings) return;
                EnableMod = true; AdjustHeights = true;
                AdjustExistingInEditor = false; AdjustExistingOnSaveLoad = false; AdjustExistingOnMapStart = false;
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
        // Align crenels that already exist when a session starts (designer, save or pre-placed walls),
        // separately per start kind. Frozen per session like the other host settings.
        [SyncHostOnly]
        public bool AdjustExistingInEditor
        {
            get => existingInEditor;
            set { if (!CanMutateSetting() || value == existingInEditor) return; existingInEditor = value; OnPropertyChanged(nameof(AdjustExistingInEditor)); }
        }
        [SyncHostOnly]
        public bool AdjustExistingOnSaveLoad
        {
            get => existingOnSaveLoad;
            set { if (!CanMutateSetting() || value == existingOnSaveLoad) return; existingOnSaveLoad = value; OnPropertyChanged(nameof(AdjustExistingOnSaveLoad)); }
        }
        [SyncHostOnly]
        public bool AdjustExistingOnMapStart
        {
            get => existingOnMapStart;
            set { if (!CanMutateSetting() || value == existingOnMapStart) return; existingOnMapStart = value; OnPropertyChanged(nameof(AdjustExistingOnMapStart)); }
        }
        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string ScopeText => SerpLocalization.Get("Crenel.Scope");
        public string AdjustHeightsText => SerpLocalization.Get("Crenel.AdjustHeights");
        public string AdjustHeightsHelpText => SerpLocalization.Get("Crenel.AdjustHeightsHelp");
        public string HeightRuleText => SerpLocalization.Get("Crenel.HeightRule");
        public string AdjustExistingText => SerpLocalization.Get("Crenel.AdjustExisting");
        public string AdjustExistingInEditorText => SerpLocalization.Get("Crenel.AdjustExistingInEditor");
        public string AdjustExistingInEditorHelpText => SerpLocalization.Get("Crenel.AdjustExistingInEditorHelp");
        public string AdjustExistingOnSaveLoadText => SerpLocalization.Get("Crenel.AdjustExistingOnSaveLoad");
        public string AdjustExistingOnSaveLoadHelpText => SerpLocalization.Get("Crenel.AdjustExistingOnSaveLoadHelp");
        public string AdjustExistingOnMapStartText => SerpLocalization.Get("Crenel.AdjustExistingOnMapStart");
        public string AdjustExistingOnMapStartHelpText => SerpLocalization.Get("Crenel.AdjustExistingOnMapStartHelp");
        protected override string ResolveSettingsUiText(string key, string fallback)
        {
            string text = SerpLocalization.Get(key); return text == key ? fallback : text;
        }
    }
}
