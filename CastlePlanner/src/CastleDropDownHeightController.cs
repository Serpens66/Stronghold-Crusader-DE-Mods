using BepInEx.Logging;
using Noesis;
using SHCDESE.API;
using SHCDESE.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;

namespace CastlePlanner
{
    internal sealed class CastleDropDownHeightController
    {
        private const string ComboBoxName = "CastlePlannerSelectionComboBox";
        private const float DesiredMaximumHeight = 420.0f;

        private readonly ManualLogSource log;
        private readonly CastlePlannerSettingsViewModel settings;
        private readonly ComboBox comboBox;
        private readonly LobbyModSettingsHubViewModel settingsHub;

        private CastleDropDownHeightController(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings,
            ComboBox comboBox,
            LobbyModSettingsHubViewModel settingsHub)
        {
            this.log = log;
            this.settings = settings;
            this.comboBox = comboBox;
            this.settingsHub = settingsHub;
            comboBox.PreviewMouseDown += OnPreviewMouseDown;
            comboBox.SelectionChanged += OnSelectionChanged;
            settingsHub.PropertyChanged += OnSettingsHubPropertyChanged;
            comboBox.MaxDropDownHeight = DesiredMaximumHeight;
            if (settingsHub.WindowVisibility == Visibility.Visible &&
                ReferenceEquals(settingsHub.SelectedTab?.ViewModel, settings))
            {
                settings.EnsureCastleCatalogLoaded();
            }
            ApplySelectedIndex();
        }

        public static CastleDropDownHeightController Attach(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings)
        {
            FrameworkElement view = GameXAMLManagerAPI.Instance.RegisteredModSettings
                .FirstOrDefault(entry => ReferenceEquals(entry.ViewModel, settings))
                ?.View;
            ComboBox comboBox = view?.FindName(ComboBoxName) as ComboBox;
            LobbyModSettingsHubViewModel settingsHub =
                SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel;
            if (view == null || comboBox == null || settingsHub == null)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "CastlePlanner castle dropdown height controller could not attach; " +
                    "the safe fixed height remains active.");
                return null;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "CastlePlanner castle dropdown controller attached; " +
                $"maximumHeight={DesiredMaximumHeight:0}.");
            return new CastleDropDownHeightController(
                log,
                settings,
                comboBox,
                settingsHub);
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            // Keep the first opening identical to later openings. No catalog or item
            // rebuild is allowed from an item click bubbling through this control.
            comboBox.MaxDropDownHeight = DesiredMaximumHeight;
        }

        private void OnSelectionChanged(
            object sender,
            SelectionChangedEventArgs args)
        {
            settings.SelectedSpawnCastleOptionIndex = comboBox.SelectedIndex;
        }

        private void OnSettingsHubPropertyChanged(
            object sender,
            PropertyChangedEventArgs args)
        {
            bool openingOrSelectingTab =
                args.PropertyName == nameof(LobbyModSettingsHubViewModel.WindowVisibility) ||
                args.PropertyName == nameof(LobbyModSettingsHubViewModel.SelectedTab);
            if (!openingOrSelectingTab ||
                settingsHub.WindowVisibility != Visibility.Visible ||
                !ReferenceEquals(settingsHub.SelectedTab?.ViewModel, settings))
            {
                return;
            }

            bool loaded = settings.EnsureCastleCatalogLoaded();
            ApplySelectedIndex();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"CastlePlanner {(loaded ? "loaded" : "reused")} the cached AIVJSON catalog when mod settings opened; " +
                $"count={settings.AvailableFileCount}.");
        }

        private void ApplySelectedIndex()
        {
            int selectedIndex = settings.SelectedSpawnCastleOptionIndex;
            if (selectedIndex >= 0 && comboBox.SelectedIndex != selectedIndex)
                comboBox.SelectedIndex = selectedIndex;
        }
    }
}
