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
        private const float SafeFallbackHeight = 120.0f;
        private const float PopupSafetyMargin = 12.0f;

        private readonly ManualLogSource log;
        private readonly CastlePlannerSettingsViewModel settings;
        private readonly LobbyModSettingsHubViewModel settingsHub;
        private readonly FrameworkElement view;
        private readonly ComboBox comboBox;
        private Popup popup;
        private float lastAppliedHeight = -1.0f;
        private bool invalidGeometryLogged;
        private bool missingPopupLogged;

        private CastleDropDownHeightController(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings,
            FrameworkElement view,
            ComboBox comboBox,
            LobbyModSettingsHubViewModel settingsHub)
        {
            this.log = log;
            this.settings = settings;
            this.settingsHub = settingsHub;
            this.view = view;
            this.comboBox = comboBox;
            settingsHub.PropertyChanged += OnSettingsHubPropertyChanged;
            comboBox.PreviewMouseDown += OnPreviewMouseDown;
            comboBox.ItemContainerGenerator.StatusChanged +=
                OnItemContainerGeneratorStatusChanged;
            comboBox.ItemContainerGenerator.ItemsChanged +=
                OnItemContainerGeneratorItemsChanged;
            comboBox.MaxDropDownHeight = SafeFallbackHeight;
            EnsurePopupPlacementBelow();
            if (settingsHub.WindowVisibility == Visibility.Visible &&
                ReferenceEquals(settingsHub.SelectedTab?.ViewModel, settings))
            {
                settings.EnsureCastleCatalogLoaded();
                UpdateDropDownHeight();
            }
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
                $"desiredMaximum={DesiredMaximumHeight:0}, " +
                $"fallback={SafeFallbackHeight:0}.");
            return new CastleDropDownHeightController(
                log,
                settings,
                view,
                comboBox,
                settingsHub);
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            // Force the documented workaround direction before Noesis performs
            // placement, then limit the popup to the space in that direction.
            EnsurePopupPlacementBelow();
            UpdateDropDownHeight();
        }

        private void OnItemContainerGeneratorStatusChanged(
            object sender,
            Noesis.EventArgs args)
        {
            ApplyOptionAvailabilityToRealizedContainers();
        }

        private void OnItemContainerGeneratorItemsChanged(
            object sender,
            ItemsChangedEventArgs args)
        {
            ApplyOptionAvailabilityToRealizedContainers();
        }

        private void ApplyOptionAvailabilityToRealizedContainers()
        {
            CastlePlannerSpawnCastleOption[] options = settings.SpawnCastleOptions;
            for (int index = 0; index < options.Length; index++)
            {
                ComboBoxItem container = comboBox.ItemContainerGenerator
                    .ContainerFromIndex(index) as ComboBoxItem;
                if (container != null)
                    container.IsEnabled = options[index].IsEnabled;
            }
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
            EnsurePopupPlacementBelow();
            UpdateDropDownHeight();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"CastlePlanner {(loaded ? "loaded" : "reused")} the cached AIVJSON catalog when mod settings opened; " +
                $"count={settings.AvailableFileCount}.");
        }

        private void EnsurePopupPlacementBelow()
        {
            if (popup == null)
            {
                comboBox.ApplyTemplate();
                popup = comboBox.GetTemplateChild("PART_Popup") as Popup ??
                    comboBox.Template?.FindName("PART_Popup", comboBox) as Popup;
            }

            if (popup == null)
            {
                if (!missingPopupLogged)
                {
                    missingPopupLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "CastlePlanner could not resolve the ComboBox PART_Popup; " +
                        "the directional height limit remains active without an explicit Bottom placement.");
                }
                return;
            }

            missingPopupLogged = false;
            popup.PlacementTarget = comboBox;
            popup.Placement = PlacementMode.Bottom;
        }

        private void UpdateDropDownHeight()
        {
            float viewHeight = view.ActualHeight;
            float comboHeight = comboBox.ActualHeight;
            if (!IsFinitePositive(viewHeight) || !IsFinitePositive(comboHeight))
            {
                ApplyFallback(viewHeight, comboHeight, float.NaN);
                return;
            }

            Point comboBottom = comboBox.TranslatePoint(
                new Point(0.0f, comboHeight),
                view);
            float availableHeight =
                viewHeight - comboBottom.Y - PopupSafetyMargin;
            if (!IsFinitePositive(comboBottom.Y) ||
                !IsFinitePositive(availableHeight))
            {
                ApplyFallback(viewHeight, comboHeight, availableHeight);
                return;
            }

            float height = Math.Min(
                DesiredMaximumHeight,
                (float)Math.Floor(availableHeight));
            ApplyHeight(height, viewHeight, comboBottom.Y, availableHeight);
        }

        private void ApplyFallback(
            float viewHeight,
            float comboHeight,
            float availableHeight)
        {
            comboBox.MaxDropDownHeight = SafeFallbackHeight;
            lastAppliedHeight = SafeFallbackHeight;
            if (invalidGeometryLogged)
                return;

            invalidGeometryLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                "CastlePlanner castle dropdown used its safe height fallback because " +
                $"Noesis geometry was not ready: viewHeight={viewHeight:0.0}, " +
                $"comboHeight={comboHeight:0.0}, available={availableHeight:0.0}.");
        }

        private void ApplyHeight(
            float height,
            float viewHeight,
            float comboBottom,
            float availableHeight)
        {
            comboBox.MaxDropDownHeight = height;
            invalidGeometryLogged = false;
            if (Math.Abs(lastAppliedHeight - height) < 0.5f)
                return;

            lastAppliedHeight = height;
            Shared.DebugLogHelper.LogInfo(
                log,
                "CastlePlanner castle dropdown height updated from current Noesis " +
                $"geometry: viewHeight={viewHeight:0.0}, " +
                $"comboBottom={comboBottom:0.0}, available={availableHeight:0.0}, " +
                $"applied={height:0.0}.");
        }

        private static bool IsFinitePositive(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value) &&
                   value > 0.0f;
        }

    }
}
