using System;
using Noesis;
using SHCDESE.API;

namespace SpectatorPerspectiveTest
{
    internal sealed class SpectatorPerspectiveHud
    {
        private const float Edge = 12f;
        private readonly Action<int> selectPlayer;
        private readonly Button[] buttons = new Button[9];
        private readonly RoutedEventHandler[] clickHandlers = new RoutedEventHandler[9];
        private Canvas canvas;
        private Border bar;
        private Border dragHandle;
        private bool dragging;
        private bool positioned;
        private bool userMoved;
        private Point dragStart;
        private float originLeft;
        private float originTop;
        private int selectedPlayer;

        internal SpectatorPerspectiveHud(Action<int> selectPlayer) { this.selectPlayer = selectPlayer; }

        internal void ResetForSession()
        {
            dragging = false;
            positioned = false;
            userMoved = false;
            selectedPlayer = 0;
            if (dragHandle != null && dragHandle.IsMouseCaptured) dragHandle.ReleaseMouseCapture();
            Hide();
            Detach();
        }

        internal void Hide()
        {
            if (bar != null) bar.Visibility = Visibility.Collapsed;
        }

        internal void Show(bool[] occupied, int selected)
        {
            if (!Resolve()) return;
            if (canvas.ActualWidth <= 0f || canvas.ActualHeight <= 0f) { Hide(); return; }
            int occupiedCount = 0;
            for (int player = 1; player <= 8; player++)
            {
                buttons[player].Visibility = occupied[player] ? Visibility.Visible : Visibility.Collapsed;
                if (occupied[player]) occupiedCount++;
            }
            bar.Width = 112f + occupiedCount * 42f;
            if (!positioned || !userMoved) PlaceAtTopRight();
            ClampPosition();
            bar.Visibility = Visibility.Visible;
            SetSelected(selected);
        }

        internal void SetSelected(int selected)
        {
            selectedPlayer = selected;
            for (int player = 1; player <= 8; player++)
                if (buttons[player] != null) buttons[player].Content = player == selectedPlayer ? "▶ " + player : player.ToString();
        }

        private bool Resolve()
        {
            if (canvas != null && bar != null && canvas.IsLoaded) return true;
            var nextCanvas = GameXAMLManagerAPI.Instance?.FindGlobalElement("SpectatorPerspectiveCanvas") as Canvas;
            if (nextCanvas == null) { Hide(); return false; }
            if (ReferenceEquals(nextCanvas, canvas) && bar != null) return true;
            Detach();
            canvas = nextCanvas;
            bar = GameXAMLManagerAPI.Instance.FindElementByName(canvas, "SpectatorPerspectiveBar") as Border;
            dragHandle = GameXAMLManagerAPI.Instance.FindElementByName(canvas, "SpectatorPerspectiveDrag") as Border;
            if (bar == null || dragHandle == null) { Detach(); return false; }
            dragHandle.MouseLeftButtonDown += OnDragDown;
            dragHandle.MouseMove += OnDragMove;
            dragHandle.MouseLeftButtonUp += OnDragUp;
            dragHandle.LostMouseCapture += OnLostCapture;
            for (int player = 1; player <= 8; player++)
            {
                int slot = player;
                buttons[player] = GameXAMLManagerAPI.Instance.FindElementByName(canvas, "SpectatorPerspectivePlayer" + player) as Button;
                if (buttons[player] == null) { Detach(); return false; }
                clickHandlers[player] = (sender, args) => selectPlayer(slot);
                buttons[player].Click += clickHandlers[player];
            }
            positioned = false;
            userMoved = false;
            return true;
        }

        private void Detach()
        {
            if (dragHandle != null)
            {
                dragHandle.MouseLeftButtonDown -= OnDragDown;
                dragHandle.MouseMove -= OnDragMove;
                dragHandle.MouseLeftButtonUp -= OnDragUp;
                dragHandle.LostMouseCapture -= OnLostCapture;
            }
            dragging = false;
            canvas = null;
            bar = null;
            dragHandle = null;
            for (int player = 1; player <= 8; player++)
            {
                if (buttons[player] != null && clickHandlers[player] != null)
                    buttons[player].Click -= clickHandlers[player];
                buttons[player] = null;
                clickHandlers[player] = null;
            }
        }

        private void PlaceAtTopRight()
        {
            if (canvas.ActualWidth <= 0f) return;
            Canvas.SetLeft(bar, Math.Max(Edge, canvas.ActualWidth - bar.Width - Edge));
            Canvas.SetTop(bar, Edge);
            positioned = true;
        }

        private void ClampPosition()
        {
            if (!positioned || canvas.ActualWidth <= 0f || canvas.ActualHeight <= 0f) return;
            float maxLeft = Math.Max(0f, canvas.ActualWidth - bar.Width);
            float maxTop = Math.Max(0f, canvas.ActualHeight - bar.Height);
            Canvas.SetLeft(bar, Math.Max(0f, Math.Min(maxLeft, (float)Canvas.GetLeft(bar))));
            Canvas.SetTop(bar, Math.Max(0f, Math.Min(maxTop, (float)Canvas.GetTop(bar))));
        }

        private void OnDragDown(object sender, MouseButtonEventArgs args)
        {
            if (canvas == null || bar == null) return;
            dragStart = args.GetPosition(canvas);
            originLeft = (float)Canvas.GetLeft(bar);
            originTop = (float)Canvas.GetTop(bar);
            dragging = dragHandle.CaptureMouse();
            if (dragging) userMoved = true;
            args.Handled = true;
        }

        private void OnDragMove(object sender, MouseEventArgs args)
        {
            if (!dragging || canvas == null || bar == null) return;
            Point current = args.GetPosition(canvas);
            Canvas.SetLeft(bar, originLeft + (float)(current.X - dragStart.X));
            Canvas.SetTop(bar, originTop + (float)(current.Y - dragStart.Y));
            ClampPosition();
            args.Handled = true;
        }

        private void OnDragUp(object sender, MouseButtonEventArgs args)
        {
            dragging = false;
            if (dragHandle != null && dragHandle.IsMouseCaptured) dragHandle.ReleaseMouseCapture();
            args.Handled = true;
        }

        private void OnLostCapture(object sender, MouseEventArgs args) { dragging = false; }
    }
}
