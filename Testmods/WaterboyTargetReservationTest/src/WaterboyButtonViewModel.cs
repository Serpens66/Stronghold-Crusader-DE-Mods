using Noesis;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;

namespace WaterboyTargetReservationTest
{
    internal sealed class WaterboyButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private Visibility visibility = Visibility.Hidden;
        private bool isEnabled;
        private string modeGlyph = "1";
        private string tooltipText = string.Empty;

        internal WaterboyButtonViewModel(Action toggle)
        {
            ToggleCommand = new RelayCommand(toggle ?? throw new ArgumentNullException(nameof(toggle)));
        }

        public RelayCommand ToggleCommand { get; }
        public Visibility ButtonVisibility { get => visibility; private set { visibility = value; OnPropertyChanged(nameof(ButtonVisibility)); } }
        public bool IsButtonEnabled { get => isEnabled; private set { isEnabled = value; OnPropertyChanged(nameof(IsButtonEnabled)); } }
        public string ModeGlyph { get => modeGlyph; private set { modeGlyph = value; OnPropertyChanged(nameof(ModeGlyph)); } }
        public string TooltipText { get => tooltipText; private set { tooltipText = value; OnPropertyChanged(nameof(TooltipText)); } }

        internal void Update(bool visible, bool enabled, bool optimized, bool syncUnavailable)
        {
            ButtonVisibility = visible ? Visibility.Visible : Visibility.Hidden;
            IsButtonEnabled = enabled;
            ModeGlyph = optimized ? "1" : "∞";
            TooltipText = syncUnavailable
                ? "Water-carrier targeting cannot be changed because synchronized Chore transport is unavailable."
                : optimized
                    ? "Optimized: one nearest water carrier per fire. Click for Vanilla behavior."
                    : "Vanilla: water carriers may choose the same fire. Click for optimized targeting.";
        }
    }
}
