using BepInEx.Logging;
using Noesis;
using SHCDESE.API;
using System;
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
        private readonly FrameworkElement view;
        private readonly ComboBox comboBox;
        private float lastAppliedHeight = -1.0f;
        private bool invalidGeometryLogged;

        private CastleDropDownHeightController(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings,
            FrameworkElement view,
            ComboBox comboBox)
        {
            this.log = log;
            this.settings = settings;
            this.view = view;
            this.comboBox = comboBox;
            comboBox.PreviewMouseDown += OnPreviewMouseDown;
        }

        public static CastleDropDownHeightController Attach(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings)
        {
            FrameworkElement view = GameXAMLManagerAPI.Instance.RegisteredModSettings
                .FirstOrDefault(entry => ReferenceEquals(entry.ViewModel, settings))
                ?.View;
            ComboBox comboBox = view?.FindName(ComboBoxName) as ComboBox;
            if (view == null || comboBox == null)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "CastlePlanner castle dropdown height controller could not attach; " +
                    "the safe fixed height remains active.");
                return null;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "CastlePlanner castle dropdown height controller attached; " +
                $"desiredMaximum={DesiredMaximumHeight:0}, " +
                $"fallback={SafeFallbackHeight:0}.");
            return new CastleDropDownHeightController(log, settings, view, comboBox);
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            settings.RefreshCastleOptions();
            // Keep the popup below the control so Noesis cannot move it beneath
            // the opening click and treat that click's release as an item click.
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
