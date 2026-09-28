using System;
using Noesis;
using SHCDESE.API;

namespace SpectatorPerspectiveTest
{
    internal sealed class SpectatorPerspectiveHud
    {
        private const float Edge = 12f;
        private readonly Action<int> selectPlayer;
        private readonly Action<int, MouseButton> jumpToPlayer;
        private readonly Action uiUnavailable;
        private readonly Button[] buttons = new Button[9];
        private readonly TextBlock[] numbers = new TextBlock[9];
        private readonly RoutedEventHandler[] clickHandlers = new RoutedEventHandler[9];
        private readonly MouseButtonEventHandler[] jumpHandlers = new MouseButtonEventHandler[9];
        private static readonly SolidColorBrush normalBorder = new SolidColorBrush(Noesis.Color.FromArgb(176, 114, 36, 28));
        private static readonly SolidColorBrush selectedBorder = new SolidColorBrush(Noesis.Color.FromRgb(239, 198, 112));
        private static readonly SolidColorBrush normalBackground = new SolidColorBrush(Noesis.Color.FromArgb(120, 15, 12, 10));
        private static readonly SolidColorBrush selectedBackground = new SolidColorBrush(Noesis.Color.FromArgb(160, 74, 37, 24));
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

        internal SpectatorPerspectiveHud(Action<int> selectPlayer, Action<int, MouseButton> jumpToPlayer, Action uiUnavailable)
        {
            this.selectPlayer = selectPlayer;
            this.jumpToPlayer = jumpToPlayer;
            this.uiUnavailable = uiUnavailable;
        }

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

        internal bool TryShow(bool[] occupied, int selected)
        {
            if (!Resolve()) return false;
            if (!canvas.IsLoaded || canvas.ActualWidth <= 0f || canvas.ActualHeight <= 0f) { Hide(); return false; }
            int occupiedCount = 0;
            for (int player = 1; player <= 8; player++)
            {
                buttons[player].Visibility = occupied[player] ? Visibility.Visible : Visibility.Collapsed;
                if (!occupied[player]) continue;
                occupiedCount++;
                int mappedPlayer = SpriteMapping.RemapMPLoadedColour(player);
                int[] colourMapping = SpriteMapping.remapColours;
                int colourIndex = mappedPlayer >= 0 && mappedPlayer < colourMapping.Length ? colourMapping[mappedPlayer] : 0;
                UnityEngine.Color colour = colourIndex > 0 && colourIndex < OnScreenText.Instance.MPTeamColours.Length
                    ? OnScreenText.Instance.MPTeamColours[colourIndex] : UnityEngine.Color.white;
                numbers[player].Foreground = new SolidColorBrush(Noesis.Color.FromRgb(
                    (byte)(colour.r * 255f), (byte)(colour.g * 255f), (byte)(colour.b * 255f)));
            }
            bar.Width = 12f + occupiedCount * 34f;
            if (!positioned || !userMoved) PlaceAtTopRight();
            ClampPosition();
            bar.Visibility = Visibility.Visible;
            SetSelected(selected);
            return true;
        }

        internal void SetSelected(int selected)
        {
            if (selectedPlayer == selected) return;
            if (selectedPlayer > 0 && buttons[selectedPlayer] != null)
            {
                buttons[selectedPlayer].BorderBrush = normalBorder;
                buttons[selectedPlayer].BorderThickness = new Thickness(1);
                buttons[selectedPlayer].Background = normalBackground;
            }
            selectedPlayer = selected;
            if (selected > 0 && buttons[selected] != null)
            {
                buttons[selected].BorderBrush = selectedBorder;
                buttons[selected].BorderThickness = new Thickness(2);
                buttons[selected].Background = selectedBackground;
            }
        }

        private bool Resolve()
        {
            if (canvas != null && bar != null && canvas.IsLoaded) return true;
            var nextCanvas = GameXAMLManagerAPI.Instance?.FindGlobalElement("SpectatorPerspectiveCanvas") as Canvas;
            if (nextCanvas == null) { Hide(); return false; }
            if (ReferenceEquals(nextCanvas, canvas) && bar != null) return true;
            Detach();
            canvas = nextCanvas;
            canvas.SizeChanged += OnCanvasSizeChanged;
            canvas.Unloaded += OnCanvasUnloaded;
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
                numbers[player] = buttons[player]?.Content as TextBlock;
                if (buttons[player] == null || numbers[player] == null) { Detach(); return false; }
                clickHandlers[player] = (sender, args) => selectPlayer(slot);
                buttons[player].Click += clickHandlers[player];
                jumpHandlers[player] = (sender, args) =>
                {
                    if (args.ChangedButton != MouseButton.Right && args.ChangedButton != MouseButton.Middle) return;
                    args.Handled = true;
                    jumpToPlayer(slot, args.ChangedButton);
                };
                buttons[player].PreviewMouseDown += jumpHandlers[player];
            }
            positioned = false;
            userMoved = false;
            return true;
        }

        private void Detach()
        {
            if (canvas != null)
            {
                canvas.SizeChanged -= OnCanvasSizeChanged;
                canvas.Unloaded -= OnCanvasUnloaded;
            }
            if (dragHandle != null)
            {
                dragHandle.MouseLeftButtonDown -= OnDragDown;
                dragHandle.MouseMove -= OnDragMove;
                dragHandle.MouseLeftButtonUp -= OnDragUp;
                dragHandle.LostMouseCapture -= OnLostCapture;
            }
            dragging = false;
            selectedPlayer = 0;
            canvas = null;
            bar = null;
            dragHandle = null;
            for (int player = 1; player <= 8; player++)
            {
                if (buttons[player] != null && clickHandlers[player] != null)
                    buttons[player].Click -= clickHandlers[player];
                if (buttons[player] != null && jumpHandlers[player] != null)
                    buttons[player].PreviewMouseDown -= jumpHandlers[player];
                buttons[player] = null;
                numbers[player] = null;
                clickHandlers[player] = null;
                jumpHandlers[player] = null;
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

        private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs args)
        {
            if (canvas == null || bar == null || !positioned) return;
            if (!userMoved) PlaceAtTopRight();
            ClampPosition();
        }

        private void OnCanvasUnloaded(object sender, RoutedEventArgs args)
        {
            Hide();
            Detach();
            uiUnavailable();
        }
    }
}
