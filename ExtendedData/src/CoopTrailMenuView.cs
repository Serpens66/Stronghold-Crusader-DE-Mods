using Noesis;
using SHCDESE.NoesisUtil;

namespace ExtendedData
{
    internal sealed partial class CoopTrailMenuViewModel : INoesisElementBindingAware
    {
        private FrameworkElement host;
        private Border inputShield;
        private ComboBox dropdown;

        void INoesisElementBindingAware.OnNoesisElementBound(FrameworkElement element)
        {
            if (ReferenceEquals(host, element)) return;
            DetachView();
            host = element;
            inputShield = host.FindName("ExtendedDataCoopTrailInputShield") as Border;
            dropdown = host.FindName("ExtendedDataCoopTrailDropdown") as ComboBox;
            host.PreviewMouseDown += OnPreviewMouseDown;
            host.PreviewMouseUp += OnPreviewMouseUp;
            host.PreviewKeyDown += OnPreviewKeyDown;
            host.IsVisibleChanged += OnVisibleChanged;
            host.Unloaded += OnUnloaded;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (!IsInputBlocked || dropdown == null || inputShield == null) return;
            if (!IsDismissingClick && (ContainsPointer(dropdown, args) || ContainsPopupPointer(args))) return;
            args.Handled = true;
            BeginDismissClick();
            // Closing ComboBox releases its capture. Capture the shield through MouseUp so
            // moving onto Vanilla's oversized Co-op button cannot complete a second click.
            inputShield.CaptureMouse();
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs args)
        {
            if (!IsDismissingClick) return;
            args.Handled = true;
            ReleaseShieldCapture();
            EndDismissClick();
        }

        private bool ContainsPopupPointer(MouseEventArgs args)
        {
            var popup = dropdown.Template?.FindName("PART_Popup", dropdown) as Popup;
            return popup?.IsOpen == true && ContainsPointer(popup.Child as FrameworkElement, args);
        }

        private static bool ContainsPointer(FrameworkElement element, MouseEventArgs args)
        {
            if (element == null || !element.IsVisible) return false;
            Point point = args.GetPosition(element);
            return point.X >= 0 && point.Y >= 0 && point.X < element.ActualWidth && point.Y < element.ActualHeight;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs args)
        {
            if (args.Key != Key.Escape || !IsInputBlocked) return;
            args.Handled = true;
            ReleaseShieldCapture();
            ResetView();
        }

        private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(args.NewValue is bool visible) || visible) return;
            ReleaseShieldCapture();
            ResetView();
        }

        private void OnUnloaded(object sender, RoutedEventArgs args) => DetachView();

        private void ReleaseShieldCapture()
        {
            if (inputShield?.IsMouseCaptured == true) inputShield.ReleaseMouseCapture();
        }

        private void DetachView()
        {
            ReleaseShieldCapture();
            ResetView();
            if (host != null)
            {
                host.PreviewMouseDown -= OnPreviewMouseDown;
                host.PreviewMouseUp -= OnPreviewMouseUp;
                host.PreviewKeyDown -= OnPreviewKeyDown;
                host.IsVisibleChanged -= OnVisibleChanged;
                host.Unloaded -= OnUnloaded;
            }
            host = null;
            inputShield = null;
            dropdown = null;
        }
    }
}
