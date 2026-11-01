using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using System;

namespace Spectator
{
    public sealed partial class SpectatorViewModel : PresetLobbyModSettingsViewModel
    {
        private bool enableClientFeatures = true, enableMod = true, enableSpectatorPerspective = true, enableStatistics = true,
            enableEliminatedPlayersBecomeSpectators = true, enableTestTools = true;
        private bool enableSingleplayerTestMode;
        public ToolsText Text { get; } = new ToolsText();
        public RelayCommand ResetToDefaultCommand { get; }
        public string ResetToDefaultText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string ResetToDefaultHelpText => SerpLocalization.Get(SerpLocalization.ResetToDefaultHelp);
        public event Action SettingsChanged;
        internal bool IsRestoringSnapshot => IsApplyingSettingsSnapshot;

        public SpectatorViewModel() { Overview = new LordOverview.OverviewSettings(this); RefreshActivation(); ResetToDefaultCommand = new RelayCommand(ResetDefaults); }
        protected override string ResolveSettingsUiText(string key, string fallback) => SerpLocalization.Get(key);

        [PresetLocal]
        public bool EnableClientFeatures { get => enableClientFeatures; set => Set(ref enableClientFeatures, value, nameof(EnableClientFeatures)); }
        [PresetLocal]
        public bool EnableTestTools { get => enableTestTools; set => Set(ref enableTestTools, value, nameof(EnableTestTools)); }
        [PresetLocal]
        public bool EnableSingleplayerTestMode { get => enableSingleplayerTestMode; set => Set(ref enableSingleplayerTestMode, value, nameof(EnableSingleplayerTestMode)); }
        internal SpectatorFeatureActivation Activation { get; private set; }
        private void RefreshActivation() => Activation = new SpectatorFeatureActivation(EnableMod,
            EnableClientFeatures, EnableSpectatorPerspective, EnableStatistics,
            EnableEliminatedPlayersBecomeSpectators, EnableTestTools, EnableSingleplayerTestMode, EnableLordOverview);
        [SyncHostOnly, MissionIndependent]
        public bool EnableMod { get => enableMod; set => Set(ref enableMod, value, nameof(EnableMod)); }
        [PresetLocal]
        public bool EnableSpectatorPerspective { get => enableSpectatorPerspective; set => Set(ref enableSpectatorPerspective, value, nameof(EnableSpectatorPerspective)); }
        [PresetLocal]
        public bool EnableStatistics { get => enableStatistics; set => Set(ref enableStatistics, value, nameof(EnableStatistics)); }
        [SyncHostOnly, MissionIndependent]
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
            System_LoadModDefaults();
        }
    }
}
