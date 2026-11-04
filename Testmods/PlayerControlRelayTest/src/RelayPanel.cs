using APIShared;
using Noesis;
using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace PlayerControlRelayTest
{
    internal readonly struct RelayMarker
    {
        internal RelayMarker(float x, float y) { X = x; Y = y; }
        internal float X { get; }
        internal float Y { get; }
    }

    // Presentation snapshots contain no native pointers or mutable game objects.
    internal sealed class RelayPlayerChoice
    {
        internal RelayPlayerChoice(int playerId, string label)
        { PlayerId = playerId; Label = label ?? string.Empty; }
        internal int PlayerId { get; }
        internal string Label { get; }
    }

    internal sealed class RelayPanelState
    {
        internal RelayPanelState(bool available, string status, RelayPlayerChoice[] players,
            int selectedPlayerId, int selectedCount, string selectionSummary, bool moveArmed, int selectionLimit = 64)
        {
            Available = available; Status = status ?? string.Empty;
            Players = players == null ? new RelayPlayerChoice[0] : (RelayPlayerChoice[])players.Clone();
            SelectedPlayerId = selectedPlayerId; SelectedCount = Math.Max(0, selectedCount);
            SelectionSummary = selectionSummary ?? string.Empty; MoveArmed = moveArmed;
            SelectionLimit = selectionLimit > 0 ? selectionLimit : 64;
        }
        internal bool Available { get; }
        internal string Status { get; }
        internal RelayPlayerChoice[] Players { get; }
        internal int SelectedPlayerId { get; }
        internal int SelectedCount { get; }
        internal string SelectionSummary { get; }
        internal bool MoveArmed { get; }
        internal int SelectionLimit { get; }
    }

    // The permanent side-HUD registration roots this plain managed object after plugin
    // cleanup. Its popup belongs to the factory's fresh button, never to a cached HUD root.
    // All methods and supplied actions are Unity-thread only; the host owns input and gameplay.
    internal sealed class RelayPanel
    {
        private readonly string ownerGuid;
        private readonly Func<bool> useGerman;
        private readonly Action<int> selectPlayer;
        private readonly Action armMove, stop, clear;
        private IHudExtrasButtonRegistration registration;
        private RelayPanelState state = EmptyState();
        private Button anchor, moveButton, stopButton, clearButton, closeButton;
        private Popup popup;
        private Border panel;
        private FrameworkElement viewportReference;
        private Grid worldSurface;
        private Canvas markerCanvas;
        private Border dragRectangle;
        private readonly List<Border> markerShapes = new List<Border>();
        private UIElement capturedSurface;
        private Point dragStart;
        private bool dragging;
        private StackPanel playersPanel;
        private TextBlock title, playersTitle, selection, instructions, status;
        private Style tooltipStyle;
        private RelayPlayerChoice[] displayedPlayers;
        private int displayedPlayer = -1;
        private bool displayedGerman;
        private bool labelsReady;
        private bool publishedOpen;

        internal RelayPanel(string ownerGuid, Func<bool> useGerman, Action<int> selectPlayer,
            Action armMove, Action stop, Action clear)
        {
            if (string.IsNullOrWhiteSpace(ownerGuid)) throw new ArgumentException("An owner GUID is required.", nameof(ownerGuid));
            this.ownerGuid = ownerGuid;
            this.useGerman = useGerman ?? throw new ArgumentNullException(nameof(useGerman));
            this.selectPlayer = selectPlayer ?? throw new ArgumentNullException(nameof(selectPlayer));
            this.armMove = armMove ?? throw new ArgumentNullException(nameof(armMove));
            this.stop = stop ?? throw new ArgumentNullException(nameof(stop));
            this.clear = clear ?? throw new ArgumentNullException(nameof(clear));
        }

        internal bool IsOpen => popup?.IsOpen == true;
        internal bool IsPointerOverPanel => IsOpen && panel?.IsMouseOver == true || anchor?.IsMouseOver == true;
        internal int LastPointerFrame { get; private set; } = -1;
        internal event Action<float, float, float, float, bool> SelectionGesture;
        internal event Action<float, float> WorldTarget;
        internal event Action CancelRequested;
        internal event Action<bool> OpenChanged;
        // The host confirms that no Vanilla drag/placement gesture is in flight.
        // A missing admission callback keeps proxy input closed.
        internal Func<bool> OpenAllowed { get; set; }

        internal void SetViewport(int width, int height)
        {
            if (worldSurface == null) return;
            // The factory's actual Noesis root is authoritative for logical layout;
            // the supplied screen extent is only a fallback while it is loading.
            float w = viewportReference?.ActualWidth > 0 ? viewportReference.ActualWidth : width;
            float h = viewportReference?.ActualHeight > 0 ? viewportReference.ActualHeight : height;
            if (w > 0 && worldSurface.Width != w) worldSurface.Width = w;
            if (h > 0 && worldSurface.Height != h) worldSurface.Height = h;
        }

        internal void SetMarkers(RelayMarker[] markers)
        {
            if (markerCanvas == null || worldSurface?.IsLoaded != true) return;
            // Selection is bounded by the host. Only this bounded presentation list
            // is traversed; this component never scans units or touches game memory.
            int count = markers == null ? 0 : Math.Min(markers.Length, state.SelectionLimit);
            for (int i = 0; i < count; i++)
            {
                var point = worldSurface.PointFromScreen(new Point(markers[i].X, markers[i].Y));
                if (i == markerShapes.Count)
                {
                    var created = GUI.ParseXaml("<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Width='18' Height='18' CornerRadius='9' BorderThickness='2' BorderBrush='#FF77FFAA' Background='#3377FFAA' IsHitTestVisible='False'/>") as Border;
                    markerShapes.Add(created); markerCanvas.Children.Add(created);
                }
                var marker = markerShapes[i]; marker.Visibility = Visibility.Visible;
                Canvas.SetLeft(marker, point.X - 9); Canvas.SetTop(marker, point.Y - 9);
            }
            for (int i = count; i < markerShapes.Count; i++) markerShapes[i].Visibility = Visibility.Collapsed;
        }

        internal void Initialize()
        {
            if (registration != null) return;
            if (!ApiShared.ForMod(ownerGuid).TryGetHudExtrasButtons(out var buttons, out var diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
            var definition = new HudExtrasButtonDefinition("relay-test-panel", new ToggleCommand(this),
                Text("Gemeinsame Steuerung: Testfenster", "Shared control: test panel"), CreateButton, 350);
            if (!buttons.TryRegisterButton(definition, out registration, out diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
            registration.SetVisible(false);
            registration.SetEnabled(false);
        }

        internal void Refresh(RelayPanelState snapshot)
        {
            state = snapshot ?? EmptyState();
            registration?.SetVisible(state.Available);
            registration?.SetEnabled(state.Available);
            if (!state.Available) Close();
            if (panel != null) UpdateContent();
        }

        internal void Reset()
        {
            Close();
            Refresh(EmptyState());
            LastPointerFrame = -1;
        }

        private Button CreateButton(HudExtrasButtonContext context)
        {
            Close();
            tooltipStyle = context.Hud.TryFindResource("APISharedHudExtrasTooltipStyle") as Style ??
                throw new InvalidOperationException("The side-HUD mod tooltip style is unavailable.");
            var button = new Button();
            var label = new TextBlock {
                Text = "PC", FontSize = 18, TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false };
            var content = new Grid();
            content.Children.Add(label);
            button.Content = content;
            BuildPanel();
            viewportReference = context.Hud;
            while (VisualTreeHelper.GetParent(viewportReference) is FrameworkElement parent) viewportReference = parent;
            worldSurface = GUI.ParseXaml("<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='Transparent' Focusable='True'/>") as Grid;
            markerCanvas = new Canvas { IsHitTestVisible = false };
            markerShapes.Clear();
            dragRectangle = GUI.ParseXaml("<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' BorderBrush='#FF77FFAA' BorderThickness='1' Background='#2277FFAA' Visibility='Collapsed' IsHitTestVisible='False'/>") as Border;
            // The transparent world surface intercepts proxy-mode gestures; the real
            // panel sits above it and consumes only its own ordinary UI interactions.
            var worldHitSurface = GUI.ParseXaml("<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='Transparent'/>") as Grid;
            worldHitSurface.MouseLeftButtonDown += OnWorldDown;
            worldHitSurface.MouseMove += OnWorldMove;
            worldHitSurface.MouseLeftButtonUp += OnWorldUp;
            worldHitSurface.MouseRightButtonDown += (_, args) => { CancelGesture(); CancelRequested?.Invoke(); args.Handled = true; };
            worldHitSurface.LostMouseCapture += (_, __) => CancelGesture();
            markerCanvas.Children.Add(dragRectangle);
            worldSurface.Children.Add(worldHitSurface);
            worldSurface.Children.Add(markerCanvas);
            panel.HorizontalAlignment = HorizontalAlignment.Right; panel.VerticalAlignment = VerticalAlignment.Top;
            panel.Margin = new Thickness(0, 24, 24, 0);
            worldSurface.Children.Add(panel);
            worldSurface.PreviewKeyDown += (_, args) => {
                if (args.Key != Key.Escape) return;
                Close(); args.Handled = true;
            };
            SetViewport(0, 0);
            var currentPopup = new Popup {
                Placement = PlacementMode.Relative, PlacementTarget = viewportReference, StaysOpen = true,
                HorizontalOffset = 0, VerticalOffset = 0, AllowsTransparency = true, Child = worldSurface
            };
            content.Children.Add(currentPopup);
            currentPopup.Opened += (_, __) => { if (ReferenceEquals(popup, currentPopup)) PublishOpenState(); };
            currentPopup.Closed += (_, __) => { if (ReferenceEquals(popup, currentPopup)) PublishOpenState(); };
            // The closure closes only its own visual during HUD recreation; it cannot
            // close the replacement popup owned by a later factory invocation.
            button.Unloaded += (_, __) => currentPopup.IsOpen = false;
            button.IsVisibleChanged += (_, __) => { if (!button.IsVisible) currentPopup.IsOpen = false; };
            button.PreviewMouseDown += RecordPointer;
            button.PreviewMouseUp += RecordPointer;
            anchor = button; popup = currentPopup;
            displayedPlayers = null; displayedPlayer = -1; labelsReady = false;
            UpdateContent();
            return button;
        }

        private void BuildPanel()
        {
            panel = GUI.ParseXaml(PanelXaml) as Border ?? throw new InvalidOperationException("Relay panel XAML failed to load.");
            title = Require<TextBlock>("RelayTitle");
            playersTitle = Require<TextBlock>("RelayPlayersTitle");
            playersPanel = Require<StackPanel>("RelayPlayers");
            selection = Require<TextBlock>("RelaySelection");
            instructions = Require<TextBlock>("RelayInstructions");
            status = Require<TextBlock>("RelayStatus");
            moveButton = Require<Button>("RelayMove");
            stopButton = Require<Button>("RelayStop");
            clearButton = Require<Button>("RelayClear");
            closeButton = Require<Button>("RelayClose");
            moveButton.Click += (_, __) => { if (CanCommand()) armMove(); };
            stopButton.Click += (_, __) => { if (CanCommand()) stop(); };
            clearButton.Click += (_, __) => { if (state.Available) clear(); };
            closeButton.Click += (_, __) => Close();
            panel.PreviewMouseDown += RecordPointer;
            panel.PreviewMouseUp += RecordPointer;
        }

        private T Require<T>(string name) where T : FrameworkElement => panel.FindName(name) as T ??
            throw new InvalidOperationException("Relay panel element is unavailable: " + name);

        private bool CanCommand() => state.Available && state.SelectedCount > 0 && state.SelectedCount <= state.SelectionLimit;
        private void RecordPointer(object sender, MouseButtonEventArgs args) => LastPointerFrame = UnityEngine.Time.frameCount;
        private string Text(string german, string english) => useGerman() ? german : english;
        private void Close() { CancelGesture(); SetOpen(false); }

        private void SetOpen(bool open)
        {
            if (popup != null) popup.IsOpen = open;
            PublishOpenState();
        }

        private void PublishOpenState()
        {
            bool open = IsOpen;
            if (publishedOpen == open) return;
            publishedOpen = open;
            OpenChanged?.Invoke(open);
            if (!open) { CancelGesture(); CancelRequested?.Invoke(); }
        }

        private void OnWorldDown(object sender, MouseButtonEventArgs args)
        {
            if (!state.Available || !IsOpen || !(sender is UIElement source)) return;
            RecordPointer(sender, args);
            if (!source.CaptureMouse()) return;
            capturedSurface = source;
            dragStart = args.GetPosition(worldSurface); dragging = true;
            worldSurface.Focus(); args.Handled = true;
        }

        private void OnWorldMove(object sender, MouseEventArgs args)
        {
            if (!dragging || state.MoveArmed) return;
            var point = args.GetPosition(worldSurface);
            Canvas.SetLeft(dragRectangle, Math.Min(dragStart.X, point.X));
            Canvas.SetTop(dragRectangle, Math.Min(dragStart.Y, point.Y));
            dragRectangle.Width = Math.Abs(point.X - dragStart.X);
            dragRectangle.Height = Math.Abs(point.Y - dragStart.Y);
            dragRectangle.Visibility = Visibility.Visible; args.Handled = true;
        }

        private void OnWorldUp(object sender, MouseButtonEventArgs args)
        {
            if (!dragging || !(sender is UIElement source)) return;
            RecordPointer(sender, args);
            var point = args.GetPosition(worldSurface);
            var from = worldSurface.PointToScreen(dragStart);
            var to = worldSurface.PointToScreen(point);
            bool add = (worldSurface.Keyboard.Modifiers & ModifierKeys.Shift) != 0;
            CancelGesture(); args.Handled = true;
            // Mouse capture keeps delivering to the world surface when the pointer
            // ends over our panel. Never interpret that UI-covered point as map input.
            if (!state.Available || !IsOpen || IsPanelPoint(to)) return;
            if (state.MoveArmed) WorldTarget?.Invoke(to.X, to.Y);
            else SelectionGesture?.Invoke(from.X, from.Y, to.X, to.Y, add);
        }

        private bool IsPanelPoint(Point screen)
        {
            if (panel?.IsVisible != true) return false;
            Point local = panel.PointFromScreen(screen);
            return local.X >= 0 && local.Y >= 0 && local.X < panel.ActualWidth && local.Y < panel.ActualHeight;
        }

        private void CancelGesture()
        {
            dragging = false;
            if (dragRectangle != null) dragRectangle.Visibility = Visibility.Collapsed;
            var release = capturedSurface; capturedSurface = null;
            if (release?.IsMouseCaptured == true) release.ReleaseMouseCapture();
        }

        private void Toggle()
        {
            if (!state.Available || popup == null || anchor?.IsLoaded != true) return;
            if (popup.IsOpen) { Close(); return; }
            if (OpenAllowed == null || !OpenAllowed()) return;
            UpdateContent();
            SetOpen(true);
            worldSurface?.Focus();
        }

        private void UpdateContent()
        {
            bool german = useGerman();
            bool languageChanged = !labelsReady || german != displayedGerman;
            if (languageChanged)
            {
                displayedGerman = german; labelsReady = true;
                title.Text = Text("Gemeinsame Steuerung · Test", "Shared control · test");
                playersTitle.Text = Text("Objektbesitzer", "Object owner");
                moveButton.Content = Text("Bewegen: Ziel wählen", "Move: choose target");
                stopButton.Content = Text("Stopp", "Stop");
                clearButton.Content = Text("Auswahl leeren", "Clear selection");
                closeButton.Content = Text("Schließen", "Close");
                SetTooltip(moveButton, Text("Danach auf ein Kartenziel klicken. Der Besitzer gibt den synchronen Auftrag frei; bei KI übernimmt der Host.",
                    "Then click a map destination. The owner authorizes the synchronized order; the host handles AI owners."));
                SetTooltip(stopButton, Text("Stopp für die Proxy-Auswahl anfordern.", "Request Stop for the proxy selection."));
                SetTooltip(clearButton, Text("Nur die lokale Proxy-Auswahl leeren.", "Clear the local proxy selection."));
                SetTooltip(closeButton, Text("Dieses Testfenster schließen.", "Close this test panel."));
                registration?.SetTooltip(Text("Gemeinsame Steuerung: Testfenster", "Shared control: test panel"));
            }
            if (languageChanged || displayedPlayer != state.SelectedPlayerId || !SamePlayers(displayedPlayers, state.Players))
            {
                playersPanel.Children.Clear();
                displayedPlayers = (RelayPlayerChoice[])state.Players.Clone(); displayedPlayer = state.SelectedPlayerId;
                foreach (var player in state.Players)
                {
                    if (player == null) continue;
                    int id = player.PlayerId;
                    var choice = new Button {
                        Content = (id == state.SelectedPlayerId ? "● " : "○ ") + player.Label,
                        Style = panel.Resources["RelayButtonStyle"] as Style,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(8, 4, 8, 4),
                        IsEnabled = state.Available
                    };
                    SetTooltip(choice, Text("Diesen Besitzer für die lokale Proxy-Auswahl wählen.", "Choose this owner for the local proxy selection."));
                    choice.Click += (_, __) => { if (state.Available) selectPlayer(id); };
                    playersPanel.Children.Add(choice);
                }
            }
            string selected = Text("Ausgewählt: ", "Selected: ") + state.SelectedCount + " / " + state.SelectionLimit;
            if (!string.IsNullOrWhiteSpace(state.SelectionSummary)) selected += "\n" + state.SelectionSummary;
            if (selection.Text != selected) selection.Text = selected;
            string help = state.MoveArmed
                ? Text("Zielmodus: auf die Karte klicken. Esc oder Rechtsklick bricht ab.", "Target mode: click the map. Esc or right-click cancels.")
                : Text("Einheiten auf der Karte anklicken oder mit einem Rahmen wählen. Umschalt fügt zur Proxy-Auswahl hinzu.",
                    "Click units or drag a selection box on the map. Shift adds to the proxy selection.");
            if (instructions.Text != help) instructions.Text = help;
            if (status.Text != state.Status) status.Text = state.Status;
            moveButton.IsEnabled = stopButton.IsEnabled = CanCommand();
            clearButton.IsEnabled = state.Available && state.SelectedCount > 0;
        }

        private void SetTooltip(FrameworkElement target, string text)
        {
            // Reuse the explicit shared modoptions template; no Vanilla lion popup.
            var tip = new ToolTip { Style = tooltipStyle, Content = text };
            ToolTipService.SetToolTip(target, tip);
            ToolTipService.SetShowDuration(target, 60000);
        }

        private static bool SamePlayers(RelayPlayerChoice[] left, RelayPlayerChoice[] right)
        {
            if (left == null || left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++)
                if (left[i]?.PlayerId != right[i]?.PlayerId || left[i]?.Label != right[i]?.Label) return false;
            return true;
        }
        private static RelayPanelState EmptyState() => new RelayPanelState(false, string.Empty, null, 0, 0, string.Empty, false);

        private sealed class ToggleCommand : ICommand
        {
            private readonly RelayPanel owner;
            internal ToggleCommand(RelayPanel owner) { this.owner = owner; }
            public event System.EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => owner.state.Available;
            public void Execute(object parameter) { if (CanExecute(parameter)) owner.Toggle(); }
        }

        private const string PanelXaml = @"<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Width='370' MaxHeight='530' Padding='14'
 Background='#FF1D1710' BorderBrush='#FFF2D48A' BorderThickness='1'>
 <Border.Resources>
  <Style x:Key='RelayButtonStyle' TargetType='{x:Type Button}'>
   <Setter Property='Background' Value='#FF443522'/><Setter Property='Foreground' Value='White'/>
   <Setter Property='BorderBrush' Value='#FFF2D48A'/><Setter Property='BorderThickness' Value='1'/>
   <Setter Property='FontSize' Value='16'/><Setter Property='Padding' Value='8,5'/>
   <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type Button}'>
    <Border Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' Padding='{TemplateBinding Padding}'>
     <ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/>
    </Border>
   </ControlTemplate></Setter.Value></Setter>
   <Style.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter Property='Background' Value='#FF6A5232'/></Trigger>
    <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger></Style.Triggers>
  </Style>
 </Border.Resources>
 <ScrollViewer HorizontalScrollBarVisibility='Auto' VerticalScrollBarVisibility='Auto'>
  <StackPanel>
   <TextBlock x:Name='RelayTitle' Foreground='#FFF2D48A' FontSize='20' TextWrapping='Wrap' Margin='0,0,0,10'/>
   <TextBlock x:Name='RelayPlayersTitle' Foreground='White' FontSize='16' Margin='0,0,0,4'/>
   <StackPanel x:Name='RelayPlayers'/>
   <TextBlock x:Name='RelaySelection' Foreground='White' FontSize='16' TextWrapping='Wrap' Margin='0,10,0,6'/>
   <TextBlock x:Name='RelayInstructions' Foreground='#FFD9CBB3' FontSize='15' TextWrapping='Wrap' Margin='0,0,0,8'/>
   <Button x:Name='RelayMove' Style='{StaticResource RelayButtonStyle}' Margin='0,2'/>
   <Button x:Name='RelayStop' Style='{StaticResource RelayButtonStyle}' Margin='0,2'/>
   <Button x:Name='RelayClear' Style='{StaticResource RelayButtonStyle}' Margin='0,2'/>
   <TextBlock x:Name='RelayStatus' Foreground='#FFF2D48A' FontSize='15' TextWrapping='Wrap' Margin='0,10,0,8'/>
   <Button x:Name='RelayClose' Style='{StaticResource RelayButtonStyle}' Margin='0,2'/>
  </StackPanel>
 </ScrollViewer>
</Border>";
    }
}
