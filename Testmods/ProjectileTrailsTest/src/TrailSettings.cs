using System;
using APIShared.ModSettings;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;

namespace ProjectileTrailsTest
{
    public sealed class TrailSettings : PresetLobbyModSettingsViewModel
    {
        private bool enabled;
        private bool replaceStandardAfterimages = true;
        private int afterimageCount = TrailBuffer.Lifetime;
        private int maximumVisible = TrailBuffer.MaximumVisibleCopies;

        internal TrailSettings(bool enabled)
        {
            this.enabled = enabled;
            ResetCommand = new RelayCommand(Reset);
        }

        [PresetLocal]
        public bool EnableClientFeatures
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;
                OnPropertyChanged(nameof(EnableClientFeatures));
            }
        }

        [SyncHostOnly]
        public bool ReplaceStandardAfterimages
        {
            get => replaceStandardAfterimages;
            set
            {
                if (replaceStandardAfterimages == value) return;
                replaceStandardAfterimages = value;
                OnPropertyChanged(nameof(ReplaceStandardAfterimages));
            }
        }

        [PresetLocal]
        public int AfterimageCount
        {
            get => afterimageCount;
            set
            {
                int count = Math.Max(0, Math.Min(TrailBuffer.Lifetime, value));
                if (count == afterimageCount) return;
                afterimageCount = count;
                OnPropertyChanged(nameof(AfterimageCount));
            }
        }

        [PresetLocal]
        public int MaximumVisible
        {
            get => maximumVisible;
            set
            {
                int maximum = Math.Max(0, Math.Min(TrailBuffer.MaximumVisibleCopies, value));
                if (maximum == maximumVisible) return;
                maximumVisible = maximum;
                OnPropertyChanged(nameof(MaximumVisible));
            }
        }

        public int AfterimageUpperBound => TrailBuffer.Lifetime;
        public int VisibleUpperBound => TrailBuffer.MaximumVisibleCopies;
        public RelayCommand ResetCommand { get; }

        public string EnableText => SerpLocalization.Get(SerpLocalization.EnableMod);
        public string EnableHelp => SerpLocalization.Get("ProjectileTrails.EnableHelp");
        public string ReplaceStandardText => SerpLocalization.Get("ProjectileTrails.ReplaceStandard");
        public string ReplaceStandardHelp => SerpLocalization.Get("ProjectileTrails.ReplaceStandardHelp");
        public string CountText => SerpLocalization.Get("ProjectileTrails.Count");
        public string CountHelp => SerpLocalization.Get("ProjectileTrails.CountHelp");
        public string MaximumText => SerpLocalization.Get("ProjectileTrails.Maximum");
        public string MaximumHelp => SerpLocalization.Get("ProjectileTrails.MaximumHelp");
        public string ResetText => SerpLocalization.Get(SerpLocalization.ResetToDefault);
        public string ResetHelp => SerpLocalization.Get(SerpLocalization.ResetToDefaultHelp);
        public string PresetCaption => SerpLocalization.Get(SerpLocalization.Preset);
        public string PresetDescription => SerpLocalization.Get(SerpLocalization.PresetHelp);

        protected override string ResolveSettingsUiText(string key, string fallback) => SerpLocalization.Get(key);

        private void Reset()
        {
            if (!CanResetSettings) return;
            EnableClientFeatures = true;
            if (CanEditHostSettings) ReplaceStandardAfterimages = true;
            AfterimageCount = TrailBuffer.Lifetime;
            MaximumVisible = TrailBuffer.MaximumVisibleCopies;
        }
    }
}
