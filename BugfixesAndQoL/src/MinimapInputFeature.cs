// Feature: Handle the two minimap improvements only while a real minimap gesture is active.
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using NoesisApp;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    public static class MinimapInputBehavior
    {
        private static MinimapInputFeature feature;
        private static UIElement gestureElement;

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(MinimapInputBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static bool GetIsEnabled(DependencyObject value) =>
            value != null && value.GetValue(IsEnabledProperty) is bool enabled && enabled;

        public static void SetIsEnabled(DependencyObject value, bool enabled) =>
            value?.SetValue(IsEnabledProperty, enabled);

        internal static void Configure(MinimapInputFeature value)
        {
            CancelGesture();
            feature = value ?? throw new ArgumentNullException(nameof(value));
        }

        internal static void Deactivate(MinimapInputFeature value)
        {
            if (!ReferenceEquals(feature, value))
                return;

            CancelGesture();
            feature = null;
        }

        internal static void CancelGesture()
        {
            UIElement element = gestureElement;
            gestureElement = null;
            if (element == null)
                return;

            element.PreviewMouseMove -= OnPreviewMouseMove;
            element.PreviewMouseUp -= OnPreviewMouseUp;
            element.LostMouseCapture -= OnLostMouseCapture;
            feature?.EndGesture();
            if (element.IsMouseCaptured)
                element.ReleaseMouseCapture();
        }

        private static void OnIsEnabledChanged(
            DependencyObject dependencyObject,
            DependencyPropertyChangedEventArgs args)
        {
            if (!(dependencyObject is UIElement element))
                return;

            element.PreviewMouseDown -= OnPreviewMouseDown;
            if (args.NewValue is bool enabled && enabled)
                element.PreviewMouseDown += OnPreviewMouseDown;
        }

        private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (gestureElement != null || feature == null ||
                !feature.TryBeginGesture(sender, args, out UIElement captureElement))
            {
                return;
            }

            gestureElement = captureElement;
            captureElement.PreviewMouseMove += OnPreviewMouseMove;
            captureElement.PreviewMouseUp += OnPreviewMouseUp;
            captureElement.LostMouseCapture += OnLostMouseCapture;
            if (!captureElement.CaptureMouse())
                CancelGesture();
        }

        private static void OnPreviewMouseMove(object sender, MouseEventArgs args)
        {
            if (args == null || args.LeftButton != MouseButtonState.Pressed)
            {
                CancelGesture();
                return;
            }

            feature?.ContinueGesture(args);
        }

        private static void OnPreviewMouseUp(object sender, MouseButtonEventArgs args)
        {
            if (args != null && args.ChangedButton == MouseButton.Left)
                CancelGesture();
        }

        private static void OnLostMouseCapture(object sender, MouseEventArgs args) =>
            CancelGesture();
    }

    internal sealed class MinimapInputFeature
    {
        private enum RadarOverlayState
        {
            Clear,
            NormalizedStale,
            ActiveVideoOrUnavailable
        }

        private static readonly BindingFlags InstanceMembers =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo RadarClickDelayField = FindField("radarClickDelay");
        private static readonly FieldInfo RadarClickDelayTimeField = FindField("radarClickDelayTime");
        private static readonly FieldInfo RadarScrollTriggeredField = FindField("radarScrollTrigged");

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private UIElement radarImage;
        private Point gestureStart;
        private bool placementGesture;
        private bool followCursorGesture;
        private bool active;
        private bool failureLogged;

        public MinimapInputFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            MinimapInputBehavior.Configure(this);
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL event-driven minimap input installed.");
        }

        internal bool TryBeginGesture(
            object sender,
            MouseButtonEventArgs args,
            out UIElement captureElement)
        {
            captureElement = null;
            if (args == null || args.ChangedButton != MouseButton.Left ||
                !settings.EnableClientFeatures)
            {
                return false;
            }

            try
            {
                FatControler controller = FatControler.instance;
                MainViewModel main = MainViewModel.Instance;
                UIElement currentRadarImage = main?.HUDRoot?.RefRadarMapImage;
                if (controller == null || currentRadarImage == null ||
                    !IsGameplayRadarAvailable(main) || IsRadarClickDelayed(controller))
                {
                    return false;
                }

                if (!(sender is Image) && !(sender is MediaElement))
                    return false;

                int currentAction = MainControls.instance.CurrentAction;
                bool placingBuilding = currentAction == (int)Enums.editorActions.placingBuilding;
                placementGesture = placingBuilding && settings.AllowMinimapWhilePlacingBuilding;
                followCursorGesture = settings.EnableMinimapCursorFollowFix;
                bool vanillaRadarGestureAllowed =
                    currentAction != (int)Enums.editorActions.placingBuilding &&
                    currentAction != (int)Enums.editorActions.troopSelection &&
                    currentAction != (int)Enums.editorActions.troopSelectionEnding;
                if (!placementGesture && (!followCursorGesture || !vanillaRadarGestureAllowed))
                    return false;

                RadarOverlayState overlayState = InspectAndNormalizeRadarOverlay(main);
                if (overlayState == RadarOverlayState.ActiveVideoOrUnavailable)
                    return false;

                Point point = args.GetPosition(currentRadarImage);
                if (IsOutsideRadar(controller, point))
                    return false;

                active = true;
                radarImage = currentRadarImage;
                gestureStart = point;
                SuppressVanillaDrag(controller);
                StopHeldRadarMovement();
                bool replacedStaleClick = overlayState == RadarOverlayState.NormalizedStale;
                if (replacedStaleClick)
                    FatControler.MouseIsDownStroke = false;
                if (placementGesture || replacedStaleClick)
                    MoveCameraToRadarPoint(controller, point);
                captureElement = currentRadarImage;
                return true;
            }
            catch (Exception ex)
            {
                FailClosed(ex);
                EndGesture();
                return false;
            }
        }

        internal void ContinueGesture(MouseEventArgs args)
        {
            if (!active || args == null || radarImage == null)
                return;

            try
            {
                FatControler controller = FatControler.instance;
                MainViewModel main = MainViewModel.Instance;
                if (controller == null || !IsGameplayRadarAvailable(main))
                {
                    MinimapInputBehavior.CancelGesture();
                    return;
                }

                Point point = args.GetPosition(radarImage);
                SuppressVanillaDrag(controller);
                if (followCursorGesture && settings.EnableMinimapCursorFollowFix)
                {
                    StopHeldRadarMovement();
                    if (!IsOutsideRadar(controller, point))
                        MoveCameraToRadarPoint(controller, point);
                    return;
                }

                if (!placementGesture || !settings.AllowMinimapWhilePlacingBuilding ||
                    MainControls.instance == null ||
                    MainControls.instance.CurrentAction != (int)Enums.editorActions.placingBuilding)
                {
                    MinimapInputBehavior.CancelGesture();
                    return;
                }

                ApplyVanillaStyleDrag(point);
            }
            catch (Exception ex)
            {
                FailClosed(ex);
                MinimapInputBehavior.CancelGesture();
            }
        }

        internal void EndGesture()
        {
            active = false;
            placementGesture = false;
            followCursorGesture = false;
            radarImage = null;
            StopHeldRadarMovement();
            FatControler controller = FatControler.instance;
            if (controller != null)
                SuppressVanillaDrag(controller);
        }

        internal void Deactivate()
        {
            MinimapInputBehavior.Deactivate(this);
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL event-driven minimap input deactivated.");
        }

        private static bool IsGameplayRadarAvailable(MainViewModel main) =>
            MainViewModel.viewModelLoaded &&
            main != null &&
            main.HUDRoot != null &&
            !main.Show_HUD_Briefing &&
            MainControls.instance != null &&
            MainControls.instance.IsUIVisible &&
            GameData.Instance?.lastGameState != null &&
            FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
            main.RadarLoaded &&
            main.MainUILoaded;

        private static bool IsRadarClickDelayed(FatControler controller) =>
            (bool)RadarClickDelayField.GetValue(controller) &&
            DateTime.UtcNow < (DateTime)RadarClickDelayTimeField.GetValue(controller);

        private static RadarOverlayState InspectAndNormalizeRadarOverlay(MainViewModel main)
        {
            if (main?.HUDRoot?.RefRadarME == null)
                return RadarOverlayState.ActiveVideoOrUnavailable;

            var radarMedia = main.HUDRoot.RefRadarME;
            SFXManager sfxManager = SFXManager.instance;
            if (radarMedia.Opacity == 0f)
                return RadarOverlayState.Clear;
            if (sfxManager == null || sfxManager.requestBinkPlayState != 0 || sfxManager.binkIsPlaying)
                return RadarOverlayState.ActiveVideoOrUnavailable;

            radarMedia.Opacity = 0f;
            return RadarOverlayState.NormalizedStale;
        }

        private static void SuppressVanillaDrag(FatControler controller) =>
            RadarScrollTriggeredField.SetValue(controller, false);

        private static void StopHeldRadarMovement()
        {
            KeyManager keys = KeyManager.instance;
            if (keys == null)
                return;

            keys.RadarHeldX = 0f;
            keys.RadarHeldY = 0f;
        }

        private static void MoveCameraToRadarPoint(FatControler controller, Point point)
        {
            EngineInterface.GameAction(
                Enums.GameActionCommand.RadarClicked,
                (int)(point.X * controller.SHRadarScalar),
                (int)(point.Y * controller.SHRadarScalar));
        }

        private void ApplyVanillaStyleDrag(Point point)
        {
            KeyManager keys = KeyManager.instance;
            if (keys == null)
                return;

            float deltaX = point.X - gestureStart.X;
            float deltaY = gestureStart.Y - point.Y;
            float magnitude = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
            if (magnitude <= 0f)
            {
                keys.RadarHeldX = 0f;
                keys.RadarHeldY = 0f;
                return;
            }

            keys.RadarHeldX = deltaX / magnitude;
            keys.RadarHeldY = deltaY / magnitude;
        }

        private static bool IsOutsideRadar(FatControler controller, Point point) =>
            point.X < 0f || point.X >= controller.SHRadarRectSize ||
            point.Y < 0f || point.Y >= controller.SHRadarRectSize;

        private static FieldInfo FindField(string fieldName)
        {
            FieldInfo field = typeof(FatControler).GetField(fieldName, InstanceMembers);
            if (field == null)
                throw new MissingFieldException(typeof(FatControler).FullName, fieldName);
            return field;
        }

        private void FailClosed(Exception ex)
        {
            if (failureLogged)
                return;

            failureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Bugfixes and QoL event-driven minimap input failed closed; Vanilla handling continues: {ex}");
        }
    }
}
