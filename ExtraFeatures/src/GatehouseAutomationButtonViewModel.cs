// Feature: HUD toggle button for per-building manual gatehouse/drawbridge automation.
using Noesis;
using SHCDESE.NoesisUtil;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed class GatehouseAutomationButtonViewModel : LobbyModSettingsBaseViewModel
    {
        private ImageSource iconImageSource;
        private Visibility buttonVisibility = Visibility.Hidden;
        private Visibility manualIndicatorVisibility = Visibility.Hidden;
        private double iconOpacity = 1.0;
        private string toolTipText = string.Empty;

        public GatehouseAutomationButtonViewModel(Action toggle)
        {
            ToggleCommand = new RelayCommand(toggle ?? throw new ArgumentNullException(nameof(toggle)));
        }

        public RelayCommand ToggleCommand { get; }
        public ImageSource IconImageSource { get => iconImageSource; private set => Set(ref iconImageSource, value, nameof(IconImageSource)); }
        public Visibility ButtonVisibility { get => buttonVisibility; private set => Set(ref buttonVisibility, value, nameof(ButtonVisibility)); }
        public Visibility ManualIndicatorVisibility { get => manualIndicatorVisibility; private set => Set(ref manualIndicatorVisibility, value, nameof(ManualIndicatorVisibility)); }
        public double IconOpacity { get => iconOpacity; private set => Set(ref iconOpacity, value, nameof(IconOpacity)); }
        public string ToolTipText { get => toolTipText; private set => Set(ref toolTipText, value, nameof(ToolTipText)); }

        public void SetIcon(ImageSource icon)
        {
            IconImageSource = icon;
        }

        public void Show(bool automaticEnabled, bool drawbridge)
        {
            ButtonVisibility = Visibility.Visible;
            ManualIndicatorVisibility = automaticEnabled ? Visibility.Hidden : Visibility.Visible;
            IconOpacity = automaticEnabled ? 1.0 : 0.48;
            ToolTipText = SerpLocalization.Get(drawbridge
                ? (automaticEnabled ? "SomeSettings.DrawbridgeAutomaticEnabledTooltip" : "SomeSettings.DrawbridgeManualOnlyTooltip")
                : (automaticEnabled ? "SomeSettings.GatehouseAutomaticEnabledTooltip" : "SomeSettings.GatehouseManualOnlyTooltip"));
        }

        public void Hide()
        {
            ButtonVisibility = Visibility.Hidden;
        }

        private void Set<T>(ref T field, T value, string propertyName)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            OnPropertyChanged(propertyName);
        }
    }
}
