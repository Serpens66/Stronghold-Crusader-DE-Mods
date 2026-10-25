using System;
using System.Collections.Generic;
using Noesis;
using SHCDESE.NoesisUtil;

namespace ExtendedData
{
    internal sealed partial class CoopTrailMenuViewModel : INoesisElementBindingAware
    {
        private FrameworkElement host;
        private Border inputShield;
        private ComboBox dropdown;
        private FrameworkElement viewRoot;
        private Popup popup;
        private FrameworkElement popupContent;
        private ScrollViewer scroll;
        private FrameworkElement viewport;
        private ComboBoxItem hoveredItem;
        private Border hoverLayer;
        private bool layoutPending;
        private bool fitting;
        private bool pointerValid;
        private Point lastPointer;
        private readonly List<FrameworkElement> ancestors = new List<FrameworkElement>();

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
            host.Loaded += OnLoaded;
            AttachAncestors();
            FitDropdown();
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            AttachAncestors();
            dropdown?.ApplyTemplate();
            FitDropdown();
        }

        private void AttachAncestors()
        {
            DetachAncestors();
            // Only the ancestor path, once per view attachment; never search the whole view.
            DependencyObject current = dropdown ?? (DependencyObject)host;
            for (int depth = 0; current != null && depth < 64; depth++)
            {
                if (current is FrameworkElement element)
                {
                    ancestors.Add(element);
                    element.SizeChanged += OnSizeChanged;
                    viewRoot = element;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            if (viewRoot != null)
            {
                viewRoot.PreviewMouseMove += OnPointerMoved;
                viewRoot.MouseLeave += OnPointerLeft;
            }
        }

        private void DetachAncestors()
        {
            if (viewRoot != null)
            {
                viewRoot.PreviewMouseMove -= OnPointerMoved;
                viewRoot.MouseLeave -= OnPointerLeft;
            }
            foreach (FrameworkElement element in ancestors) element.SizeChanged -= OnSizeChanged;
            ancestors.Clear();
            viewRoot = null;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs args)
        {
            FitDropdown();
            if (IsDropDownOpen) SchedulePopupLayout();
        }

        private void FitDropdown()
        {
            if (fitting || dropdown == null || viewRoot == null || host.ActualWidth <= 0 || viewRoot.ActualHeight <= 0) return;
            fitting = true;
            try
            {
                // Convert the real view bounds through the existing Viewbox transform.
                Point origin = viewRoot.TranslatePoint(new Point(0, 0), host);
                Point end = viewRoot.TranslatePoint(new Point(viewRoot.ActualWidth, viewRoot.ActualHeight), host);
                float left = Math.Max(0, origin.X), right = Math.Min(host.ActualWidth, end.X);
                float top = Math.Max(0, origin.Y), bottom = Math.Min(host.ActualHeight, end.Y);
                if (right <= left || bottom <= top) return;
                float width = CoopTrailMenuGeometry.Width(right - left);
                float y = Math.Max(top, Math.Min(150, bottom - 40));
                // Left alignment makes the visible-region centre explicit, including letterboxing.
                dropdown.HorizontalAlignment = HorizontalAlignment.Left;
                dropdown.Margin = new Thickness(left + (right - left - width) / 2, y, 0, 0);
                dropdown.Width = width;
                float below = bottom - y - dropdown.ActualHeight;
                float above = y - top;
                bool useAbove = below < CoopTrailMenuGeometry.RowHeight + CoopTrailMenuGeometry.Chrome && above > below;
                dropdown.MaxDropDownHeight = CoopTrailMenuGeometry.Height(useAbove ? above : below, Options.Length);
                var currentPopup = dropdown.Template?.FindName("PART_Popup", dropdown) as Popup;
                if (!ReferenceEquals(popup, currentPopup))
                {
                    DetachPopup(true);
                    popup = currentPopup;
                    if (popup != null)
                    {
                        popup.Opened += OnDropDownOpened;
                        popup.Closed += OnDropDownClosed;
                    }
                }
                if (popup != null) popup.Placement = useAbove ? PlacementMode.Top : PlacementMode.Bottom;
            }
            finally { fitting = false; }
        }

        private void OnDropDownOpened(object sender, Noesis.EventArgs args)
        {
            FitDropdown();
            popupContent = popup?.Child as FrameworkElement;
            scroll = dropdown.Template?.FindName("CoopTrailScroll", dropdown) as ScrollViewer;
            if (popupContent != null)
            {
                popupContent.PreviewMouseMove += OnPointerMoved;
                popupContent.MouseLeave += OnPointerLeft;
            }
            if (scroll != null) scroll.ScrollChanged += OnScrollChanged;
            SchedulePopupLayout();
        }

        private void OnDropDownClosed(object sender, Noesis.EventArgs args) => DetachPopup();

        private void OnScrollChanged(object sender, ScrollChangedEventArgs args)
        {
            // Recycled containers may already represent a different item; never retain their old hover.
            ClearHover();
            SchedulePopupLayout();
        }

        private void SchedulePopupLayout()
        {
            if (layoutPending || host == null) return;
            layoutPending = true;
            host.LayoutUpdated += OnPopupLayout;
        }

        private void OnPopupLayout(object sender, Noesis.EventArgs args)
        {
            host.LayoutUpdated -= OnPopupLayout;
            layoutPending = false;
            viewport = scroll?.Template?.FindName("ItemsViewport", scroll) as FrameworkElement;
            pointerValid = false;
            RefreshHover();
        }

        private void OnPointerMoved(object sender, MouseEventArgs args)
        {
            if (IsDropDownOpen) RefreshHover();
        }

        private void OnPointerLeft(object sender, MouseEventArgs args)
        {
            if (IsDropDownOpen) RefreshHover();
        }

        private void RefreshHover()
        {
            if (!IsDropDownOpen || popup?.IsOpen != true || viewport == null || !viewport.IsVisible)
            { ClearHover(); return; }
            Point point = Mouse.GetPosition(viewport);
            // Popup and view routes can report the same movement; do not hit-test twice.
            if (pointerValid && point.X == lastPointer.X && point.Y == lastPointer.Y) return;
            ComboBoxItem item = null;
            if (CoopTrailMenuGeometry.Contains(point.X, point.Y, viewport.ActualWidth, viewport.ActualHeight))
            {
                // Hit-test the clipped, virtualized viewport, not MouseOver/Source under capture.
                DependencyObject hit = VisualTreeHelper.HitTest(viewport, point).VisualHit;
                for (int depth = 0; hit != null && !ReferenceEquals(hit, viewport) && depth < 32; depth++)
                {
                    if (hit is ComboBoxItem candidate) { item = candidate; break; }
                    hit = VisualTreeHelper.GetParent(hit);
                }
            }
            if (ReferenceEquals(item, hoveredItem))
            {
                lastPointer = point;
                pointerValid = true;
                return;
            }
            ClearHover();
            lastPointer = point;
            pointerValid = true;
            if (item == null) return;
            hoverLayer = item.Template?.FindName("PointerHover", item) as Border;
            if (hoverLayer == null) return;
            hoveredItem = item;
            hoverLayer.Visibility = Visibility.Visible;
            item.Unloaded += OnHoveredItemUnloaded;
            item.DataContextChanged += OnHoveredItemChanged;
        }

        private void OnHoveredItemUnloaded(object sender, RoutedEventArgs args) => ClearHover();
        private void OnHoveredItemChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            ClearHover();
            if (IsDropDownOpen) SchedulePopupLayout();
        }

        private void ClearHover()
        {
            pointerValid = false;
            if (hoverLayer != null) hoverLayer.Visibility = Visibility.Collapsed;
            if (hoveredItem != null)
            {
                hoveredItem.Unloaded -= OnHoveredItemUnloaded;
                hoveredItem.DataContextChanged -= OnHoveredItemChanged;
            }
            hoverLayer = null;
            hoveredItem = null;
        }

        private void DetachPopup(bool retire = false)
        {
            ClearHover();
            if (layoutPending && host != null) host.LayoutUpdated -= OnPopupLayout;
            layoutPending = false;
            if (popupContent != null)
            {
                popupContent.PreviewMouseMove -= OnPointerMoved;
                popupContent.MouseLeave -= OnPointerLeft;
            }
            if (scroll != null) scroll.ScrollChanged -= OnScrollChanged;
            popupContent = null;
            scroll = null;
            viewport = null;
            if (retire && popup != null)
            {
                popup.Opened -= OnDropDownOpened;
                popup.Closed -= OnDropDownClosed;
                popup = null;
            }
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (!IsDropDownOpen) FitDropdown();
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
            DetachPopup(true);
            DetachAncestors();
            ReleaseShieldCapture();
            ResetView();
            if (host != null)
            {
                host.PreviewMouseDown -= OnPreviewMouseDown;
                host.PreviewMouseUp -= OnPreviewMouseUp;
                host.PreviewKeyDown -= OnPreviewKeyDown;
                host.IsVisibleChanged -= OnVisibleChanged;
                host.Unloaded -= OnUnloaded;
                host.Loaded -= OnLoaded;
            }
            host = null;
            inputShield = null;
            dropdown = null;
        }
    }

    internal static class CoopTrailMenuGeometry
    {
        internal const float RowHeight = 48;
        // Authored popup border (2) and vertical padding (12), in the same logical units as rows.
        internal const float Chrome = 14;
        internal static float Width(float available) => Math.Max(0, Math.Min(520, available - 16));
        internal static float Height(float available, int count)
        {
            if (float.IsNaN(available) || available <= 0) return 0;
            float rows = Math.Min(10, Math.Max(0, count));
            float fittingRows = (float)Math.Floor(Math.Max(0, available - Chrome) / RowHeight);
            // Extremely small windows still stay clipped to the actual available space.
            if (fittingRows < 1) return Math.Min(available, rows * RowHeight + Chrome);
            return Math.Min(available, Math.Min(rows, fittingRows) * RowHeight + Chrome);
        }
        internal static bool Contains(float x, float y, float width, float height) =>
            x >= 0 && y >= 0 && x < width && y < height;
    }
}
