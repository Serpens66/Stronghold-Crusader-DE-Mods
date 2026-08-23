using SHCDESE.NoesisUtil;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace CastlePlanner
{
    internal sealed class BlueprintHudViewModel : INotifyPropertyChanged
    {
        private const string HudHostName = "CastlePlannerBlueprintHud";
        private const string DragHandleName =
            "CastlePlannerBlueprintDragHandle";
        private const string CastleComboBoxName =
            "CastlePlannerCastleComboBox";
        private const string RotationComboBoxName =
            "CastlePlannerRotationComboBox";
        private const double DesiredPanelWidth = 360.0;
        private const double NormalPanelHeight = 210.0;
        private const double PreviewPanelHeight = 310.0;
        private const double ScreenInset = 8.0;
        private const double DefaultPanelLeft = 44.0;
        private const double BaseButtonBottom = 34.0;
        private const double ButtonSlotHeight = 34.0;

        private readonly Action toggle;
        private readonly CastlePlannerSettingsViewModel settings;
        private readonly FreeCastlePreviewRuntime preview;
        private bool hudVisible;
        private bool canToggle;
        private bool blueprintVisible;
        private bool settingsPanelVisible;
        private bool vanillaButtonOccupiesFirstSlot;
        private bool isDragging;
        private double panelLeft;
        private double panelTop;
        private double viewportWidth;
        private double viewportHeight;
        private double dragStartPanelLeft;
        private double dragStartPanelTop;
        private int completedDepthCaptures;
        private int requestedDepthCaptures;
        private Noesis.Point dragStartPointer;
        private Noesis.FrameworkElement hudHost;
        private Noesis.FrameworkElement dragHandle;
        private Noesis.ComboBox castleComboBox;
        private Noesis.ComboBox rotationComboBox;
        private Noesis.UIElement dragCaptureElement;

        public BlueprintHudViewModel(
            Action toggle,
            CastlePlannerSettingsViewModel settings,
            FreeCastlePreviewRuntime preview)
        {
            this.toggle = toggle ?? throw new ArgumentNullException(nameof(toggle));
            this.settings = settings ??
                throw new ArgumentNullException(nameof(settings));
            this.preview = preview ?? throw new ArgumentNullException(nameof(preview));
            ToggleCommand = new RelayCommand(
                () => this.toggle(),
                () => CanToggle);
            ToggleSettingsPanelCommand =
                new ParameterRelayCommand(ToggleSettingsPanel);
            // The HUD proxies the shared settings so both UIs always show the
            // same selection and visual values.
            this.settings.PropertyChanged += OnSettingsPropertyChanged;
            this.preview.PropertyChanged += OnPreviewPropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ToggleCommand { get; }
        public ICommand ToggleSettingsPanelCommand { get; }
        public ICommand ConfirmCastleCommand => preview.ConfirmCommand;
        public bool PreviewVisible => preview.IsPreviewActive;
        public string PreviewTitleText => preview.TitleText;
        public string PreviewTimerText => preview.TimerText;
        public string PreviewStatusText => preview.StatusText;
        public string ConfirmCastleText => preview.ConfirmText;
        public string RotationText => preview.RotationText;
        public System.Collections.Generic.IReadOnlyList<string> RotationOptions =>
            preview.RotationChoices;
        public bool CanConfirmCastle => preview.CanConfirm;
        public bool CanSelectCastle => !PreviewVisible || preview.CanConfirm;
        public bool CanSelectRotation => preview.CanConfirm && preview.HasSelectedCastle;
        public double PanelWidth => ClampPanelExtent(
            DesiredPanelWidth,
            viewportWidth);

        public double PanelHeight => ClampPanelExtent(
            PreviewVisible ? PreviewPanelHeight : NormalPanelHeight,
            viewportHeight);
        public string SelectedRotation
        {
            get => preview.SelectedRotation;
            set => preview.SelectedRotation = value;
        }

        // Preserve the original working ObservableCollection binding. The
        // preview provides the same concrete collection type and adds None.
        public ObservableCollection<string> CastleOptions => PreviewVisible
            ? preview.CastleChoices
            : settings.CastleOptions;

        public string SelectedCastle
        {
            get => PreviewVisible ? preview.SelectedChoice : settings.SelectedCastle;
            set
            {
                if (PreviewVisible)
                    preview.SelectedChoice = value;
                else
                    settings.SelectedCastle = value;
            }
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

        public string SettingsTitleText =>
            SerpLocalization.Get("CastlePlanner.Hud.Settings");

        public string IconScaleText =>
            SerpLocalization.Get("CastlePlanner.Hud.IconScale");

        public string IconAlphaText =>
            SerpLocalization.Get("CastlePlanner.Hud.IconAlpha");

        public bool HudVisible
        {
            get => hudVisible;
            private set => SetField(
                ref hudVisible,
                value,
                nameof(HudVisible));
        }

        public bool SettingsPanelVisible
        {
            get => settingsPanelVisible;
            private set => SetField(
                ref settingsPanelVisible,
                value,
                nameof(SettingsPanelVisible));
        }

        public double PanelLeft
        {
            get => panelLeft;
            private set => SetDoubleField(
                ref panelLeft,
                value,
                nameof(PanelLeft));
        }

        public double PanelTop
        {
            get => panelTop;
            private set => SetDoubleField(
                ref panelTop,
                value,
                nameof(PanelTop));
        }

        public double TriggerVerticalOffset =>
            vanillaButtonOccupiesFirstSlot ? -ButtonSlotHeight : 0.0;

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
                ? SerpLocalization.Get("CastlePlanner.Hud.Unavailable")
                : BlueprintVisible && completedDepthCaptures < requestedDepthCaptures
                    ? string.Format(
                        SerpLocalization.Get("CastlePlanner.Hud.Loading"),
                        completedDepthCaptures,
                        requestedDepthCaptures)
                : BlueprintVisible
                    ? SerpLocalization.Get("CastlePlanner.Hud.On")
                    : SerpLocalization.Get("CastlePlanner.Hud.Off");

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
            if (!HudVisible)
                CloseSettingsPanel();
            OnPropertyChanged(nameof(StatusText));
        }

        public void UpdateVanillaButtonSlot(bool isOccupied)
        {
            if (vanillaButtonOccupiesFirstSlot == isOccupied)
                return;

            vanillaButtonOccupiesFirstSlot = isOccupied;
            OnPropertyChanged(nameof(TriggerVerticalOffset));
            if (!settings.TryGetBlueprintHudPosition(out _, out _))
                ApplyStoredOrDefaultPosition();
        }

        public void UpdateViewportSize(double width, double height)
        {
            if (!IsFinitePositive(width) || !IsFinitePositive(height) ||
                (Math.Abs(viewportWidth - width) < 0.001 &&
                 Math.Abs(viewportHeight - height) < 0.001))
            {
                return;
            }

            viewportWidth = width;
            viewportHeight = height;
            OnPropertyChanged(nameof(PanelWidth));
            OnPropertyChanged(nameof(PanelHeight));
            // Vanilla exposes the UI-scaled viewport dimensions used by the
            // full IngameUIScreens root, independent of physical resolution.
            settings.LogBlueprintHudMessage(
                $"Blueprint HUD viewport updated: " +
                $"width={viewportWidth:0.0}, height={viewportHeight:0.0}.");
            if (!isDragging)
                ApplyStoredOrDefaultPosition();
        }

        public void ResetForMapLifecycle()
        {
            FinishDrag(savePosition: true);
            CloseSettingsPanel();
            DetachHudElements();
        }

        public void EnsureInteractiveElementsAttached()
        {
            if (!HudVisible || !SettingsPanelVisible || hudHost != null)
                return;

            Noesis.FrameworkElement host =
                SHCDESE.API.GameXAMLManagerAPI.Instance.FindGlobalElement(
                    HudHostName);
            if (host != null)
                AttachHudElements(host);
        }

        public bool ShouldSuppressMapZoom()
        {
            return IsPointerOverOpenDropDown(castleComboBox) ||
                IsPointerOverOpenDropDown(rotationComboBox);
        }

        private void ToggleSettingsPanel(object parameter)
        {
            if (!HudVisible)
                return;

            Noesis.FrameworkElement host = FindAncestorByName(
                parameter as Noesis.DependencyObject,
                HudHostName);
            if (host != null)
            {
                AttachHudElements(host);
            }
            else
            {
                settings.LogBlueprintHudMessage(
                    $"Blueprint HUD host was not found from toggle parameter " +
                    $"'{parameter?.GetType().FullName ?? "<null>"}'.");
            }
            ApplyStoredOrDefaultPosition();
            SettingsPanelVisible = !SettingsPanelVisible;
        }

        private void CloseSettingsPanel()
        {
            if (SettingsPanelVisible)
                SettingsPanelVisible = false;
        }

        private void AttachHudElements(Noesis.FrameworkElement host)
        {
            if (host == null)
                return;

            if (!ReferenceEquals(hudHost, host))
            {
                DetachHudElements();
                hudHost = host;
                hudHost.SizeChanged += OnHudHostSizeChanged;
            }

            Noesis.FrameworkElement candidate =
                host.FindName(DragHandleName) as Noesis.FrameworkElement;
            if (candidate == null)
                candidate = FindDescendantByName(host, DragHandleName);
            if (candidate != null && !ReferenceEquals(dragHandle, candidate))
            {
                DetachDragHandle();
                dragHandle = candidate;
                // Direct CLR event subscriptions avoid the Noesis behavior bridge,
                // which did not dispatch the title-bar drag events in game.
                dragHandle.PreviewMouseDown += OnDragHandleMouseDown;
                dragHandle.PreviewMouseMove += OnDragHandleMouseMove;
                dragHandle.PreviewMouseUp += OnDragHandleMouseUp;
                dragHandle.LostMouseCapture += OnDragHandleLostMouseCapture;
                settings.LogBlueprintHudMessage(
                    $"Blueprint HUD drag handle attached to direct Noesis mouse " +
                    $"events: host={hudHost.ActualWidth:0.0}x" +
                    $"{hudHost.ActualHeight:0.0}, viewport={viewportWidth:0.0}x" +
                    $"{viewportHeight:0.0}.");
            }

            AttachComboBox(
                ref castleComboBox,
                FindElement<Noesis.ComboBox>(host, CastleComboBoxName));
            AttachComboBox(
                ref rotationComboBox,
                FindElement<Noesis.ComboBox>(host, RotationComboBoxName));
        }

        private void DetachHudElements()
        {
            FinishDrag(savePosition: true);
            DetachDragHandle();
            DetachComboBox(ref castleComboBox);
            DetachComboBox(ref rotationComboBox);
            if (hudHost != null)
            {
                hudHost.SizeChanged -= OnHudHostSizeChanged;
                hudHost = null;
            }
        }

        private void AttachComboBox(
            ref Noesis.ComboBox current,
            Noesis.ComboBox candidate)
        {
            if (candidate == null || ReferenceEquals(current, candidate))
                return;
            DetachComboBox(ref current);
            current = candidate;
            current.PreviewMouseWheel += OnComboBoxPreviewMouseWheel;
        }

        private void DetachComboBox(ref Noesis.ComboBox comboBox)
        {
            if (comboBox == null)
                return;
            comboBox.PreviewMouseWheel -= OnComboBoxPreviewMouseWheel;
            comboBox = null;
        }

        private static T FindElement<T>(
            Noesis.FrameworkElement host,
            string name)
            where T : Noesis.FrameworkElement
        {
            return host.FindName(name) as T ??
                FindDescendantByName(host, name) as T;
        }

        private static bool IsPointerOverOpenDropDown(Noesis.ComboBox comboBox)
        {
            if (comboBox == null || !comboBox.IsDropDownOpen)
                return false;

            comboBox.ApplyTemplate();
            Noesis.Popup popup = comboBox.GetTemplateChild("PART_Popup") as Noesis.Popup ??
                comboBox.Template?.FindName("PART_Popup", comboBox) as Noesis.Popup;
            return comboBox.IsMouseOver || popup?.Child?.IsMouseOver == true;
        }

        private static void OnComboBoxPreviewMouseWheel(
            object sender,
            Noesis.MouseWheelEventArgs args)
        {
            // A closed selector must not cycle values from the wheel. Leaving
            // the event untouched while open lets its popup scroll normally.
            if (!(sender is Noesis.ComboBox comboBox) ||
                !comboBox.IsDropDownOpen)
            {
                args.Handled = true;
            }
        }

        private void DetachDragHandle()
        {
            if (dragHandle == null)
                return;

            dragHandle.PreviewMouseDown -= OnDragHandleMouseDown;
            dragHandle.PreviewMouseMove -= OnDragHandleMouseMove;
            dragHandle.PreviewMouseUp -= OnDragHandleMouseUp;
            dragHandle.LostMouseCapture -= OnDragHandleLostMouseCapture;
            dragHandle = null;
        }

        private void OnHudHostSizeChanged(
            object sender,
            Noesis.SizeChangedEventArgs args)
        {
            if (!isDragging)
                ApplyStoredOrDefaultPosition();
        }

        private void OnDragHandleMouseDown(
            object sender,
            Noesis.MouseButtonEventArgs args)
        {
            if (args.ChangedButton != Noesis.MouseButton.Left ||
                hudHost == null ||
                !(sender is Noesis.UIElement source) ||
                !source.CaptureMouse())
            {
                return;
            }

            dragCaptureElement = source;
            dragStartPointer = args.GetPosition(hudHost);
            dragStartPanelLeft = PanelLeft;
            dragStartPanelTop = PanelTop;
            isDragging = true;
            args.Handled = true;
            settings.LogBlueprintHudMessage(
                $"Blueprint HUD drag started: " +
                $"left={PanelLeft:0.0}, top={PanelTop:0.0}, " +
                $"viewport={viewportWidth:0.0}x{viewportHeight:0.0}.");
        }

        private void OnDragHandleMouseMove(
            object sender,
            Noesis.MouseEventArgs args)
        {
            if (!isDragging ||
                hudHost == null)
            {
                return;
            }

            if (args.LeftButton != Noesis.MouseButtonState.Pressed)
            {
                FinishDrag(savePosition: true);
                return;
            }

            Noesis.Point current = args.GetPosition(hudHost);
            SetClampedPanelPosition(
                dragStartPanelLeft + current.X - dragStartPointer.X,
                dragStartPanelTop + current.Y - dragStartPointer.Y);
            args.Handled = true;
        }

        private void OnDragHandleMouseUp(
            object sender,
            Noesis.MouseButtonEventArgs args)
        {
            if (!isDragging || args.ChangedButton != Noesis.MouseButton.Left)
                return;

            FinishDrag(savePosition: true);
            args.Handled = true;
        }

        private void OnDragHandleLostMouseCapture(
            object sender,
            Noesis.MouseEventArgs args)
        {
            FinishDrag(savePosition: true);
        }

        private void FinishDrag(bool savePosition)
        {
            if (!isDragging)
                return;

            isDragging = false;
            Noesis.UIElement captureElement = dragCaptureElement;
            dragCaptureElement = null;
            captureElement?.ReleaseMouseCapture();
            if (savePosition)
                SaveCurrentPosition();
        }

        private void ApplyStoredOrDefaultPosition()
        {
            if (!TryGetMovementBounds(
                    out double minimumX,
                    out double maximumX,
                    out double minimumY,
                    out double maximumY))
            {
                return;
            }

            if (settings.TryGetBlueprintHudPosition(
                    out double normalizedX,
                    out double normalizedY))
            {
                PanelLeft = Lerp(minimumX, maximumX, normalizedX);
                PanelTop = Lerp(minimumY, maximumY, normalizedY);
                return;
            }

            PanelLeft = Clamp(DefaultPanelLeft, minimumX, maximumX);
            double buttonBottom = BaseButtonBottom +
                (vanillaButtonOccupiesFirstSlot ? ButtonSlotHeight : 0.0);
            PanelTop = Clamp(
                maximumY + ScreenInset - buttonBottom,
                minimumY,
                maximumY);
        }

        private void SetClampedPanelPosition(double left, double top)
        {
            if (!TryGetMovementBounds(
                    out double minimumX,
                    out double maximumX,
                    out double minimumY,
                    out double maximumY))
            {
                return;
            }

            PanelLeft = Clamp(left, minimumX, maximumX);
            PanelTop = Clamp(top, minimumY, maximumY);
        }

        private void SaveCurrentPosition()
        {
            if (!TryGetMovementBounds(
                    out double minimumX,
                    out double maximumX,
                    out double minimumY,
                    out double maximumY))
            {
                return;
            }

            double normalizedX = NormalizePosition(
                PanelLeft,
                minimumX,
                maximumX);
            double normalizedY = NormalizePosition(
                PanelTop,
                minimumY,
                maximumY);
            settings.SaveBlueprintHudPosition(normalizedX, normalizedY);
        }

        private bool TryGetMovementBounds(
            out double minimumX,
            out double maximumX,
            out double minimumY,
            out double maximumY)
        {
            minimumX = ScreenInset;
            minimumY = ScreenInset;
            maximumX = minimumX;
            maximumY = minimumY;
            double availableWidth = viewportWidth;
            double availableHeight = viewportHeight;
            if (!IsFinitePositive(availableWidth) ||
                !IsFinitePositive(availableHeight))
            {
                if (hudHost == null ||
                    !IsFinitePositive(hudHost.ActualWidth) ||
                    !IsFinitePositive(hudHost.ActualHeight))
                {
                    return false;
                }

                availableWidth = hudHost.ActualWidth;
                availableHeight = hudHost.ActualHeight;
            }

            maximumX = Math.Max(
                minimumX,
                availableWidth - PanelWidth - ScreenInset);
            maximumY = Math.Max(
                minimumY,
                availableHeight - PanelHeight - ScreenInset);
            return true;
        }

        private static bool IsFinitePositive(double value)
        {
            return value > 0.0 &&
                   !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }

        private static double ClampPanelExtent(double desired, double viewportExtent)
        {
            if (!IsFinitePositive(viewportExtent))
                return desired;
            return Math.Max(1.0, Math.Min(desired, viewportExtent - 2.0 * ScreenInset));
        }

        private static Noesis.FrameworkElement FindAncestorByName(
            Noesis.DependencyObject source,
            string name)
        {
            Noesis.DependencyObject current = source;
            while (current != null)
            {
                if (current is Noesis.FrameworkElement element &&
                    string.Equals(element.Name, name, StringComparison.Ordinal))
                {
                    return element;
                }

                current = Noesis.VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static Noesis.FrameworkElement FindDescendantByName(
            Noesis.DependencyObject parent,
            string name)
        {
            int childCount = Noesis.VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                Noesis.DependencyObject child =
                    Noesis.VisualTreeHelper.GetChild(parent, index);
                if (child is Noesis.FrameworkElement element &&
                    string.Equals(element.Name, name, StringComparison.Ordinal))
                {
                    return element;
                }

                Noesis.FrameworkElement nested =
                    FindDescendantByName(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private static double Lerp(double minimum, double maximum, double value)
        {
            return minimum + (maximum - minimum) * value;
        }

        private static double NormalizePosition(
            double value,
            double minimum,
            double maximum)
        {
            double range = maximum - minimum;
            return range <= 0.0001
                ? 0.0
                : Clamp((value - minimum) / range, 0.0, 1.0);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private void OnSettingsPropertyChanged(
            object sender,
            PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(CastlePlannerSettingsViewModel.SelectedCastle):
                    OnPropertyChanged(nameof(SelectedCastle));
                    break;
                case nameof(CastlePlannerSettingsViewModel.BlueprintIconScale):
                case nameof(CastlePlannerSettingsViewModel.BlueprintIconScaleText):
                    OnPropertyChanged(args.PropertyName);
                    break;
                case nameof(CastlePlannerSettingsViewModel.BlueprintIconAlpha):
                case nameof(CastlePlannerSettingsViewModel.BlueprintIconAlphaText):
                    OnPropertyChanged(args.PropertyName);
                    break;
            }
        }

        private void OnPreviewPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case nameof(FreeCastlePreviewRuntime.IsPreviewActive):
                    OnPropertyChanged(nameof(PreviewVisible));
                    OnPropertyChanged(nameof(CastleOptions));
                    OnPropertyChanged(nameof(SelectedCastle));
                    OnPropertyChanged(nameof(CanSelectCastle));
                    OnPropertyChanged(nameof(CanSelectRotation));
                    OnPropertyChanged(nameof(PanelHeight));
                    if (preview.IsPreviewActive)
                        SettingsPanelVisible = true;
                    if (!isDragging)
                        ApplyStoredOrDefaultPosition();
                    break;
                case nameof(FreeCastlePreviewRuntime.TimerText):
                    OnPropertyChanged(nameof(PreviewTimerText));
                    break;
                case nameof(FreeCastlePreviewRuntime.StatusText):
                case nameof(FreeCastlePreviewRuntime.IsLocalConfirmed):
                    OnPropertyChanged(nameof(PreviewStatusText));
                    break;
                case nameof(FreeCastlePreviewRuntime.CanConfirm):
                    OnPropertyChanged(nameof(CanConfirmCastle));
                    OnPropertyChanged(nameof(CanSelectCastle));
                    OnPropertyChanged(nameof(CanSelectRotation));
                    break;
                case nameof(FreeCastlePreviewRuntime.SelectedChoice):
                    OnPropertyChanged(nameof(SelectedCastle));
                    OnPropertyChanged(nameof(CanSelectRotation));
                    break;
                case nameof(FreeCastlePreviewRuntime.CastleChoices):
                    OnPropertyChanged(nameof(CastleOptions));
                    break;
                case nameof(FreeCastlePreviewRuntime.HasSelectedCastle):
                    OnPropertyChanged(nameof(CanSelectRotation));
                    break;
                case nameof(FreeCastlePreviewRuntime.SelectedRotation):
                    OnPropertyChanged(nameof(SelectedRotation));
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

        private bool SetDoubleField(
            ref double field,
            double value,
            string propertyName)
        {
            if (Math.Abs(field - value) < 0.001)
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

        private sealed class ParameterRelayCommand : ICommand
        {
            private readonly Action<object> execute;

            public ParameterRelayCommand(Action<object> execute)
            {
                this.execute =
                    execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                execute(parameter);
            }

            public event EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }
        }
    }
}
