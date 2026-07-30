using SHCDESE.NoesisUtil;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace SpawnCastle
{
    internal sealed class BlueprintHudViewModel : INotifyPropertyChanged
    {
        private readonly Action toggle;
        private bool hudVisible;
        private bool canToggle;
        private bool blueprintVisible;

        public BlueprintHudViewModel(Action toggle)
        {
            this.toggle = toggle ?? throw new ArgumentNullException(nameof(toggle));
            ToggleCommand = new RelayCommand(
                () => this.toggle(),
                () => CanToggle);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ToggleCommand { get; }

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
                : BlueprintVisible
                    ? "Blueprint: on"
                    : "Blueprint: off";

        public void Update(
            bool isBlueprintMode,
            bool isMapActive,
            bool isReady,
            bool isVisible)
        {
            HudVisible = isBlueprintMode && isMapActive;
            CanToggle = HudVisible && isReady;
            BlueprintVisible = isVisible;
            OnPropertyChanged(nameof(StatusText));
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
