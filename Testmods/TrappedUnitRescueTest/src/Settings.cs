using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;

namespace TrappedUnitRescueTest
{
    public sealed class Settings : PresetLobbyModSettingsViewModel
    {
        private bool enableMod = true;
        private bool enableExperimentalRescue;
        public Settings()
        {
            ResetToDefaultCommand = new RelayCommand(() => {
                if (!CanEditHostSettings) return;
                EnableMod = true; EnableExperimentalRescue = false;
            });
        }
        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set { if (!CanMutateSetting() || value == enableMod) return; enableMod = value; OnPropertyChanged(nameof(EnableMod)); }
        }
        [SyncHostOnly]
        public bool EnableExperimentalRescue
        {
            get => enableExperimentalRescue;
            set { if (!CanMutateSetting() || value == enableExperimentalRescue) return; enableExperimentalRescue = value; OnPropertyChanged(nameof(EnableExperimentalRescue)); }
        }
        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string RescueText => SerpLocalization.Get("Rescue.Enabled");
        public string RescueHelpText => SerpLocalization.Get("Rescue.Help");
        public string ScopeText => SerpLocalization.Get("Rescue.Scope");
        public string RuleText => SerpLocalization.Get("Rescue.Rule");
        protected override string ResolveSettingsUiText(string key, string fallback)
        {
            string value = SerpLocalization.Get(key); return value == key ? fallback : value;
        }
    }
}
