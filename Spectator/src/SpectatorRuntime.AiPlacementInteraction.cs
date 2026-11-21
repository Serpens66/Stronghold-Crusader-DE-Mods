using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static void AttachAiPanelInteraction()
        {
            aiPlacementGrip.MouseLeftButtonDown += OnAiPanelDragDown;
            aiPlacementGrip.MouseMove += OnAiPanelDragMove;
            aiPlacementGrip.MouseLeftButtonUp += OnAiPanelDragUp;
            aiPlacementGrip.LostMouseCapture += OnAiPanelCaptureLost;
            aiPlacementPanel.SizeChanged += OnAiPanelSizeChanged;
            aiPlacementPanel.IsVisibleChanged += OnAiPanelVisibilityChanged;
            aiPlacementPanel.Unloaded += OnAiPanelUnloaded;
            aiInputRoot = aiPlacementHost;
            while (VisualTreeHelper.GetParent(aiInputRoot) is UIElement ancestor) aiInputRoot = ancestor;
            aiInputRoot.PreviewKeyDown += OnAiPanelKeyDown;
            UnityEngine.Application.focusChanged += OnAiPanelFocusChanged;
        }

        private static void DetachAiPanelInteraction()
        {
            CancelAiPanelDrag();
            aiPlacementGrip.MouseLeftButtonDown -= OnAiPanelDragDown;
            aiPlacementGrip.MouseMove -= OnAiPanelDragMove;
            aiPlacementGrip.MouseLeftButtonUp -= OnAiPanelDragUp;
            aiPlacementGrip.LostMouseCapture -= OnAiPanelCaptureLost;
            aiPlacementPanel.SizeChanged -= OnAiPanelSizeChanged;
            aiPlacementPanel.IsVisibleChanged -= OnAiPanelVisibilityChanged;
            aiPlacementPanel.Unloaded -= OnAiPanelUnloaded;
            if (aiInputRoot != null) aiInputRoot.PreviewKeyDown -= OnAiPanelKeyDown;
            aiInputRoot = null;
            UnityEngine.Application.focusChanged -= OnAiPanelFocusChanged;
        }

        private static void HideAiPlacementPanel()
        {
            CancelAiPanelDrag();
            if (aiRolePicker != null) aiRolePicker.IsDropDownOpen = false;
            if (aiPlacementPanel != null) aiPlacementPanel.Visibility = Visibility.Collapsed;
        }

        private static void OnAiPanelDragDown(object sender, MouseButtonEventArgs args)
        {
            if (args.ChangedButton != MouseButton.Left || aiPlacementPanel?.IsVisible != true ||
                !IsCurrentSession(CurrentSessionId) || !aiPlacementGrip.CaptureMouse()) return;
            aiRolePicker.IsDropDownOpen = false;
            var point = args.GetPosition(aiPlacementHost);
            aiPanelPosition.Begin(point.X, point.Y, aiPanelBounds);
            if (ToolTipService.GetToolTip(aiPlacementGrip) is ToolTip tip) tip.IsOpen = false;
            args.Handled = true;
        }

        private static void OnAiPanelDragMove(object sender, MouseEventArgs args)
        {
            if (!aiPanelPosition.Dragging) return;
            if (args.LeftButton != MouseButtonState.Pressed || aiPlacementPanel?.IsVisible != true ||
                !IsCurrentSession(CurrentSessionId)) { CancelAiPanelDrag(); return; }
            var point = args.GetPosition(aiPlacementHost);
            aiPanelPosition.Move(point.X, point.Y);
            PositionAiPlacementPanel();
            args.Handled = true;
        }

        private static void OnAiPanelDragUp(object sender, MouseButtonEventArgs args)
        {
            if (args.ChangedButton != MouseButton.Left || !aiPanelPosition.Dragging) return;
            var point = args.GetPosition(aiPlacementHost);
            aiPanelPosition.Move(point.X, point.Y);
            var origin = aiPlacementHost.PointToScreen(new Point(0, 0));
            var unit = aiPlacementHost.PointToScreen(new Point(1, 1));
            aiPanelPosition.Drop(aiPanelDock, aiPanelBounds.Width, aiPanelBounds.Height,
                aiPlacementHost.ActualWidth, aiPlacementHost.ActualHeight, unit.X - origin.X, unit.Y - origin.Y);
            if (aiPlacementGrip.IsMouseCaptured) aiPlacementGrip.ReleaseMouseCapture();
            PositionAiPlacementPanel(); args.Handled = true;
        }

        private static void OnAiPanelKeyDown(object sender, KeyEventArgs args)
        { if (args.Key == Key.Escape && aiPanelPosition.Dragging) { CancelAiPanelDrag(); args.Handled = true; } }

        private static void OnAiPanelCaptureLost(object sender, MouseEventArgs args) => CancelAiPanelDrag();
        private static void OnAiPanelVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        { if (aiPlacementPanel?.IsVisible != true) CancelAiPanelDrag(); }
        private static void OnAiPanelSizeChanged(object sender, SizeChangedEventArgs args) => RequestRender();
        private static void OnAiPanelUnloaded(object sender, RoutedEventArgs args) => CancelAiPanelDrag();
        private static void OnAiPanelFocusChanged(bool focused)
        { if (!focused) PostPresentation(CancelAiPanelDrag); }

        private static void CancelAiPanelDrag()
        {
            bool dragging = aiPanelPosition.Dragging;
            aiPanelPosition.Cancel();
            if (aiPlacementGrip?.IsMouseCaptured == true) aiPlacementGrip.ReleaseMouseCapture();
            if (dragging) RequestRender();
        }
    }
}
