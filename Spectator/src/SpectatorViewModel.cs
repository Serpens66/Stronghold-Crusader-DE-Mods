using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using System;

namespace Spectator
{
    public sealed class SpectatorViewModel : PresetLobbyModSettingsViewModel
    {
        private bool enableClientFeatures = true, enableMod = true, enableSpectatorPerspective = true, enableStatistics = true,
            enableEliminatedPlayersBecomeSpectators = true, enableTestTools = true;
        public ToolsText Text { get; } = new ToolsText();
        public RelayCommand ResetToDefaultCommand { get; }
        public event Action SettingsChanged;

        public SpectatorViewModel() { RefreshActivation(); ResetToDefaultCommand = new RelayCommand(ResetDefaults); }
        protected override string ResolveSettingsUiText(string key, string fallback) => SerpLocalization.Get(key);

        [PresetLocal]
        public bool EnableClientFeatures { get => enableClientFeatures; set => Set(ref enableClientFeatures, value, nameof(EnableClientFeatures)); }
        [PresetLocal]
        public bool EnableTestTools { get => enableTestTools; set => Set(ref enableTestTools, value, nameof(EnableTestTools)); }
        internal SpectatorFeatureActivation Activation { get; private set; }
        private void RefreshActivation() => Activation = new SpectatorFeatureActivation(EnableMod,
            EnableClientFeatures, EnableSpectatorPerspective, EnableStatistics,
            EnableEliminatedPlayersBecomeSpectators, EnableTestTools);
        [SyncHostOnly]
        public bool EnableMod { get => enableMod; set => Set(ref enableMod, value, nameof(EnableMod)); }
        [SyncHostOnly]
        public bool EnableSpectatorPerspective { get => enableSpectatorPerspective; set => Set(ref enableSpectatorPerspective, value, nameof(EnableSpectatorPerspective)); }
        [SyncHostOnly]
        public bool EnableStatistics { get => enableStatistics; set => Set(ref enableStatistics, value, nameof(EnableStatistics)); }
        [SyncHostOnly]
        public bool EnableEliminatedPlayersBecomeSpectators { get => enableEliminatedPlayersBecomeSpectators; set => Set(ref enableEliminatedPlayersBecomeSpectators, value, nameof(EnableEliminatedPlayersBecomeSpectators)); }

        private void Set(ref bool field, bool value, string name)
        {
            if (!CanMutateSetting(name) || field == value) return;
            field = value;
            RefreshActivation();
            OnPropertyChanged(name);
            SettingsChanged?.Invoke();
        }
        private void ResetDefaults()
        {
            if (!CanResetSettings) return;
            if (CanEditClientSettings) { EnableClientFeatures = true; EnableTestTools = true; }
            if (CanEditHostSettings) { EnableMod = true; EnableSpectatorPerspective = true; EnableStatistics = true; EnableEliminatedPlayersBecomeSpectators = true; }
        }
    }
}
