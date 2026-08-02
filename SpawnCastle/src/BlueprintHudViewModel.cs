using SHCDESE.NoesisUtil;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace SpawnCastle
{
    internal sealed class BlueprintHudViewModel : INotifyPropertyChanged
    {
        private const string HudHostName = "SpawnCastleBlueprintHud";
        private const string DragHandleName =
            "SpawnCastleBlueprintDragHandle";
        private const double PanelWidth = 360.0;
        private const double PanelHeight = 196.0;
        private const double ScreenInset = 8.0;
        private const double DefaultPanelLeft = 44.0;
        private const double BaseButtonBottom = 34.0;
        private const double ButtonSlotHeight = 34.0;

        private readonly Action toggle;
        private readonly SpawnCastleSettingsViewModel settings;
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
        private Noesis.UIElement dragCaptureElement;

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
            ToggleSettingsPanelCommand =
                new ParameterRelayCommand(ToggleSettingsPanel);
            // The HUD proxies the shared settings so both UIs always show the
            // same selection and visual values.
            this.settings.PropertyChanged += OnSettingsPropertyChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand ToggleCommand { get; }
        public ICommand ToggleSettingsPanelCommand { get; }

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
            if (candidate == null || ReferenceEquals(dragHandle, candidate))
                return;

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

        private void DetachHudElements()
        {
            FinishDrag(savePosition: true);
            DetachDragHandle();
            if (hudHost != null)
            {
                hudHost.SizeChanged -= OnHudHostSizeChanged;
                hudHost = null;
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
