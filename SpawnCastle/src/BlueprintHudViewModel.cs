using SHCDESE.NoesisUtil;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace SpawnCastle
{
    internal sealed class BlueprintHudViewModel : INotifyPropertyChanged
    {
        private readonly Action toggle;
        private readonly SpawnCastleSettingsViewModel settings;
        private bool hudVisible;
        private bool canToggle;
        private bool blueprintVisible;
        private int completedDepthCaptures;
        private int requestedDepthCaptures;

        public BlueprintHudViewModel(
            Action toggle,
            SpawnCastleSettingsViewModel settings)
        {
            this.toggle = toggle ?? throw new ArgumentNullException(nameof(toggle));
            this.settings = settings ??
                throw new ArgumentNullException(nameof(settings));
            ToggleCommand = new RelayCommand(
                () => this.toggle(),
                () => CanToggle);
            // The HUD proxies the shared settings so both UIs always show the
            // same selection and visual values.
            this.settings.PropertyChanged += OnSettingsPropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ToggleCommand { get; }

        public ObservableCollection<string> CastleOptions =>
            settings.CastleOptions;

        public string SelectedCastle
        {
            get => settings.SelectedCastle;
            set => settings.SelectedCastle = value;
        }

        public double BlueprintIconScale
        {
            get => settings.BlueprintIconScale;
            set => settings.BlueprintIconScale = value;
        }

        public double BlueprintIconAlpha
        {
            get => settings.BlueprintIconAlpha;
            set => settings.BlueprintIconAlpha = value;
        }

        public string BlueprintIconScaleText =>
            settings.BlueprintIconScaleText;

        public string BlueprintIconAlphaText =>
            settings.BlueprintIconAlphaText;

        public bool HudVisible
        {
            get => hudVisible;
            private set => SetField(ref hudVisible, value, nameof(HudVisible));
        }

        public bool CanToggle
        {
            get => canToggle;
            private set
            {
                if (!SetField(ref canToggle, value, nameof(CanToggle)))
                    return;

                ((RelayCommand)ToggleCommand).RaiseCanExecuteChanged();
            }
        }

        public bool BlueprintVisible
        {
            get => blueprintVisible;
            private set
            {
                if (!SetField(
                        ref blueprintVisible,
                        value,
                        nameof(BlueprintVisible)))
                {
                    return;
                }

                OnPropertyChanged(nameof(StatusText));
            }
        }

        public string StatusText =>
            !CanToggle
                ? "Blueprint: unavailable"
                : BlueprintVisible && completedDepthCaptures < requestedDepthCaptures
                    ? $"Blueprint: loading {completedDepthCaptures}/{requestedDepthCaptures}"
                : BlueprintVisible
                    ? "Blueprint: on"
                    : "Blueprint: off";

        public void Update(
            bool isBlueprintMode,
            bool isMapActive,
            bool isReady,
            bool isVisible,
            int completedDepthCaptures,
            int requestedDepthCaptures)
        {
            this.completedDepthCaptures = Math.Max(0, completedDepthCaptures);
            this.requestedDepthCaptures = Math.Max(
                this.completedDepthCaptures,
                requestedDepthCaptures);
            HudVisible = isBlueprintMode && isMapActive;
            CanToggle = HudVisible && isReady;
            BlueprintVisible = isVisible;
            OnPropertyChanged(nameof(StatusText));
        }

        private void OnSettingsPropertyChanged(
            object sender,
            PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(SpawnCastleSettingsViewModel.SelectedCastle):
                    OnPropertyChanged(nameof(SelectedCastle));
                    break;
                case nameof(SpawnCastleSettingsViewModel.BlueprintIconScale):
                case nameof(SpawnCastleSettingsViewModel.BlueprintIconScaleText):
                    OnPropertyChanged(args.PropertyName);
                    break;
                case nameof(SpawnCastleSettingsViewModel.BlueprintIconAlpha):
                case nameof(SpawnCastleSettingsViewModel.BlueprintIconAlphaText):
                    OnPropertyChanged(args.PropertyName);
                    break;
            }
        }

        private bool SetField(
            ref bool field,
            bool value,
            string propertyName)
        {
            if (field == value)
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
