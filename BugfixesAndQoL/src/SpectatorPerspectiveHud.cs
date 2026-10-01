using System;
using System.ComponentModel;
using CrusaderDE;
using Noesis;

namespace BugfixesAndQoL
{
    internal sealed class SpectatorPerspectiveHud
    {
        private const float DefaultRightInset = 52f;
        private readonly Action<int> selectPlayer;
        private readonly Action<int, MouseButton> jumpToPlayer;
        private readonly Action uiUnavailable;
        private readonly Action uiAvailable;
        private readonly Action<bool> briefingChanged;
        private readonly Button[] buttons = new Button[9];
        private readonly TextBlock[] numbers = new TextBlock[9];
        private readonly RoutedEventHandler[] clickHandlers = new RoutedEventHandler[9];
        private readonly MouseButtonEventHandler[] jumpHandlers = new MouseButtonEventHandler[9];
        private static readonly SolidColorBrush normalBorder = new SolidColorBrush(Noesis.Color.FromArgb(176, 114, 36, 28));
        private static readonly SolidColorBrush selectedBorder = new SolidColorBrush(Noesis.Color.FromRgb(239, 198, 112));
        private static readonly SolidColorBrush normalBackground = new SolidColorBrush(Noesis.Color.FromArgb(120, 15, 12, 10));
        private static readonly SolidColorBrush selectedBackground = new SolidColorBrush(Noesis.Color.FromArgb(160, 74, 37, 24));
        private MainViewModel viewModel;
        private IngameUIScreens screen;
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
        private bool briefingVisible;

        internal SpectatorPerspectiveHud(Action<int> selectPlayer, Action<int, MouseButton> jumpToPlayer,
            Action uiUnavailable, Action uiAvailable, Action<bool> briefingChanged)
        {
            this.selectPlayer = selectPlayer;
            this.jumpToPlayer = jumpToPlayer;
            this.uiUnavailable = uiUnavailable;
            this.uiAvailable = uiAvailable;
            this.briefingChanged = briefingChanged;
        }

        internal bool IsBriefingVisible => viewModel?.Show_HUD_Briefing == true;

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
            if (IsBriefingVisible) { Hide(); return false; }
            if (!canvas.IsLoaded || canvas.ActualWidth <= 0f || canvas.ActualHeight <= 0f) { Hide(); return false; }
            int occupiedCount = 0;
            for (int player = 1; player <= 8; player++)
            {
                buttons[player].Visibility = occupied[player] ? Visibility.Visible : Visibility.Collapsed;
                if (!occupied[player]) continue;
                occupiedCount++;
                int[] colourMapping = SpriteMapping.remapColours;
                int colourIndex = player < colourMapping.Length ? colourMapping[player] : 0;
                UnityEngine.Color colour = UnityEngine.Color.white;
                if (colourIndex > 0 && colourIndex < colourMapping.Length && spriteLoader.instance != null)
                    spriteLoader.instance.GetGMMaterial(Enums.GM.GM_BODY_ARCHER, colourIndex, 0, out colour, 0);
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
            MainViewModel nextViewModel = MainViewModel.Instance;
            IngameUIScreens nextScreen = nextViewModel?.IngameUI;
            if (nextViewModel == null) { Hide(); Detach(); return false; }
            if (ReferenceEquals(nextViewModel, viewModel) && nextScreen == null && screen == null)
                return false;
            if (ReferenceEquals(nextViewModel, viewModel) && ReferenceEquals(nextScreen, screen) &&
                canvas != null && bar != null &&
                ReferenceEquals(screen.FindName("SpectatorPerspectiveCanvas"), canvas)) return true;
            Hide();
            Detach();
            viewModel = nextViewModel;
            briefingVisible = viewModel.Show_HUD_Briefing;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            if (nextScreen == null) return false;
            screen = nextScreen;
            screen.Loaded += OnScreenLoaded;
            canvas = screen.FindName("SpectatorPerspectiveCanvas") as Canvas;
            // Keep the view-model event while the XAML tree is still being created.
            if (canvas == null) return false;
            canvas.SizeChanged += OnCanvasSizeChanged;
            canvas.Loaded += OnCanvasLoaded;
            canvas.Unloaded += OnCanvasUnloaded;
            bar = screen.FindName("SpectatorPerspectiveBar") as Border;
            dragHandle = screen.FindName("SpectatorPerspectiveDrag") as Border;
            if (bar == null || dragHandle == null) return false;
            dragHandle.MouseLeftButtonDown += OnDragDown;
            dragHandle.MouseMove += OnDragMove;
            dragHandle.MouseLeftButtonUp += OnDragUp;
            dragHandle.LostMouseCapture += OnLostCapture;
            for (int player = 1; player <= 8; player++)
            {
                int slot = player;
                buttons[player] = screen.FindName("SpectatorPerspectivePlayer" + player) as Button;
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
            if (viewModel != null) viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            if (screen != null) screen.Loaded -= OnScreenLoaded;
            if (canvas != null)
            {
                canvas.SizeChanged -= OnCanvasSizeChanged;
                canvas.Loaded -= OnCanvasLoaded;
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
            briefingVisible = false;
            viewModel = null;
            screen = null;
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
            Canvas.SetLeft(bar, Math.Max(0f, canvas.ActualWidth - bar.Width - DefaultRightInset));
            Canvas.SetTop(bar, 0f);
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

        private void OnScreenLoaded(object sender, RoutedEventArgs args)
        {
            if (ReferenceEquals(sender, screen)) uiAvailable();
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!ReferenceEquals(sender, viewModel) ||
                (!string.IsNullOrEmpty(args.PropertyName) &&
                 args.PropertyName != nameof(MainViewModel.Show_HUD_Briefing))) return;
            bool visible = viewModel.Show_HUD_Briefing;
            if (visible == briefingVisible) return;
            briefingVisible = visible;
            if (visible) Hide();
            briefingChanged(visible);
        }

        private void OnCanvasLoaded(object sender, RoutedEventArgs args)
        {
            if (ReferenceEquals(sender, canvas)) uiAvailable();
        }

        private void OnCanvasUnloaded(object sender, RoutedEventArgs args)
        {
            Hide();
            uiUnavailable();
        }
    }
}
