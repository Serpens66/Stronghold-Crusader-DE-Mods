// Feature: Center the camera on a selected troop type when its HUD icon is middle-clicked.
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    public static class TroopHudMiddleClickBehavior
    {
        private static TroopHudMiddleClickCameraFeature feature;

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(TroopHudMiddleClickBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject value) =>
            value != null && value.GetValue(IsEnabledProperty) is bool enabled && enabled;

        public static void SetIsEnabled(DependencyObject value, bool enabled) =>
            value?.SetValue(IsEnabledProperty, enabled);

        internal static void Configure(TroopHudMiddleClickCameraFeature value) =>
            feature = value ?? throw new ArgumentNullException(nameof(value));

        private static void OnIsEnabledChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (!(dependencyObject is Button button))
                return;

            // Reattaching defensively keeps repeated XAML initialization idempotent.
            button.PreviewMouseDown -= OnPreviewMouseDown;
            if (args.NewValue is bool enabled && enabled)
                button.PreviewMouseDown += OnPreviewMouseDown;
        }

        private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (!(sender is Button button) ||
                args == null ||
                args.ChangedButton != MouseButton.Middle ||
                args.ClickCount != 1)
            {
                return;
            }

            feature?.JumpToSelectedTroop(button);
        }
    }

    internal sealed unsafe class TroopHudMiddleClickCameraFeature
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private bool commandFailureLogged;

        public TroopHudMiddleClickCameraFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            // The static attached behavior outlives the startup plugin component.
            TroopHudMiddleClickBehavior.Configure(this);
        }

        internal void JumpToSelectedTroop(Button sourceButton)
        {
            if (!settings.EnableClientFeatures ||
                !settings.EnableTroopHudMiddleClickCameraJump)
            {
                return;
            }

            try
            {
                if (!(sourceButton?.CommandParameter is string unitTypeName) ||
                    !Enum.TryParse(unitTypeName, false, out eChimps unitType) ||
                    !Enum.IsDefined(typeof(eChimps), unitType))
                {
                    return;
                }

                GameData gameData = GameData.Instance;
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
                if (gameData == null || unitApi == null || playerApi == null)
                {
                    return;
                }

                EngineInterface.PlayState state = gameData.lastGameState;
                if (state.numSelectedChimps <= 0 || state.selectedChimps == null)
                {
                    return;
                }

                int count = Math.Min(state.numSelectedChimps, state.selectedChimps.Length);
                for (int selectionIndex = 0; selectionIndex < count; selectionIndex++)
                {
                    int unitId = state.selectedChimps[selectionIndex];
                    if (unitId <= 0 ||
                        !unitApi.TryGetUnitById(unitId, out GameUnit* unit) ||
                        unit == null ||
                        unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_UnitChimp != unitType)
                    {
                        continue;
                    }

                    playerApi.SetScreenCenterToUnit(unitId);
                    return;
                }
            }
            catch (Exception ex)
            {
                if (commandFailureLogged)
                    return;

                commandFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL troop-HUD middle-click camera jump failed; the click keeps Vanilla behavior: {ex}");
            }
        }

    }
}
