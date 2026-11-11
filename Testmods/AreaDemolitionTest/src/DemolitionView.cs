using System;
using System.Collections.Generic;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using UnityEngine;
using Brush = Noesis.Brush;
using Color = Noesis.Color;
using Grid = Noesis.Grid;
using Key = Noesis.Key;

namespace AreaDemolitionTest
{
    // Scene controls are replaceable; the owning runtime and its callbacks outlive the startup cleanup.
    internal sealed class DemolitionView
    {
        private readonly DemolitionSettings settings;
        private readonly ManualLogSource log;
        private readonly List<CheckBox> filters = new List<CheckBox>();
        private readonly Brush foreground = new SolidColorBrush(Color.FromArgb(255, 244, 232, 196));
        private HUD_Main hud;
        private Button button;
        private Grid host;
        private Canvas overlay;
        private Border backdrop, menu, statusBox;
        private TextBlock status;
        private bool menuOpen, closingClick, syncing, reportedFailure;
        private string statusText = "";

        internal DemolitionView(DemolitionSettings settings, ManualLogSource log)
        { this.settings = settings; this.log = log; }

        internal event Action MenuChanged;
        internal bool IsMenuOpen => menuOpen;
        internal bool IsInputShieldActive => menuOpen || closingClick;

        internal void RefreshHud()
        {
            try
            {
                if (!MainViewModel.viewModelLoaded) { Reset(); return; }
                MainViewModel main = MainViewModel.Instance;
                HUD_Main candidate = main.HUDmain;
                Grid candidateHost = main.IngameUI?.Content as Grid;
                Button candidateButton = candidate?.FindName("GameDeleteButton") as Button;
                if (candidate == null || candidateHost == null || candidateButton == null)
                { Reset(); return; }
                if (!ReferenceEquals(candidate, hud) || !ReferenceEquals(candidateHost, host) || !ReferenceEquals(candidateButton, button))
                {
                    Reset(); hud = candidate; host = candidateHost; button = candidateButton;
                    CreateOverlay();
                    button.MouseEnter += OnButtonMouseEnter;
                    button.PreviewMouseDown += OnButtonMouseDown;
                    button.PreviewMouseUp += OnButtonMouseUp;
                }
                if (!button.IsVisible || !button.IsEnabled) CloseMenu();
                UpdateLayout();
                if (menuOpen) SyncFilters();
            }
            catch (Exception error) { Report(error); Reset(); }
        }

        private void CreateOverlay()
        {
            overlay = new Canvas { Name = "AreaDemolitionOverlay", IsHitTestVisible = true };
            Grid.SetRowSpan(overlay, Math.Max(1, host.RowDefinitions.Count));
            Grid.SetColumnSpan(overlay, Math.Max(1, host.ColumnDefinitions.Count));
            Panel.SetZIndex(overlay, 100000);
            // Only this full-view surface intercepts outside clicks; a closed menu adds no hit target.
            backdrop = new Border { Background = Brushes.Transparent, Visibility = Visibility.Collapsed };
            backdrop.PreviewMouseDown += OnBackdropDown;
            backdrop.PreviewMouseUp += OnBackdropUp;
            overlay.Children.Add(backdrop);
            var contents = new StackPanel();
            contents.Children.Add(new TextBlock { Text = Text.Get("MenuTitle"), Foreground = foreground,
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6), FontSize = 17 });
            foreach (TargetGroup group in Enum.GetValues(typeof(TargetGroup)))
            {
                TargetGroup captured = group;
                string key = "Group" + group;
                var check = new CheckBox { Content = Text.Get(key), Foreground = foreground,
                    FontSize = 15, Margin = new Thickness(0, 2, 0, 2), Tag = group };
                check.Checked += (_, __) => ChangeFilter(captured, true);
                check.Unchecked += (_, __) => ChangeFilter(captured, false);
                var tip = Shared.MenuToolTip.Create(Text.Get(key + "Help"));
                ToolTipService.SetToolTip(check, tip);
                ToolTipService.SetShowDuration(check, 60000);
                filters.Add(check); contents.Children.Add(check);
            }
            contents.Children.Add(new TextBlock { Text = Text.Get("DragHelp"), TextWrapping = TextWrapping.Wrap,
                Foreground = foreground, FontSize = 13, Margin = new Thickness(0, 7, 0, 0) });
            contents.Children.Add(new TextBlock { Text = Text.Get("ShiftHelp"), TextWrapping = TextWrapping.Wrap,
                Foreground = foreground, FontSize = 13, Margin = new Thickness(0, 3, 0, 0) });
            menu = new Border { Name = "AreaDemolitionFilters", Width = 275, Padding = new Thickness(13, 10, 13, 10),
                Background = new SolidColorBrush(Color.FromArgb(245, 29, 23, 16)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 200, 164, 95)), BorderThickness = new Thickness(1),
                Child = new ScrollViewer { Content = contents, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, Visibility = Visibility.Collapsed };
            menu.MouseDown += ConsumeMenuMouse;
            menu.MouseUp += ConsumeMenuMouse;
            menu.KeyDown += OnMenuKeyDown;
            overlay.Children.Add(menu);
            status = new TextBlock { Foreground = foreground, FontSize = 15, TextWrapping = TextWrapping.Wrap };
            statusBox = new Border { Child = status, Padding = new Thickness(7, 4, 7, 4), MaxWidth = 420,
                Background = new SolidColorBrush(Color.FromArgb(220, 29, 23, 16)),
                IsHitTestVisible = false, Visibility = Visibility.Collapsed };
            overlay.Children.Add(statusBox); host.Children.Add(overlay);
            SyncFilters(); SetStatus(statusText);
        }

        private void ChangeFilter(TargetGroup group, bool value)
        {
            if (syncing || settings.Get(group) == value) return;
            settings.Set(group, value); MenuChanged?.Invoke();
        }

        private void SyncFilters()
        {
            syncing = true;
            try { foreach (CheckBox check in filters) check.IsChecked = settings.Get((TargetGroup)check.Tag); }
            finally { syncing = false; }
        }

        private void OnButtonMouseEnter(object sender, MouseEventArgs args)
        {
            // Vanilla's earlier MouseEnter handler has filled HoverString. Do not replace its Tag binding.
            string hint = Text.Get("RightClickSettings");
            if (hud != null && !hud.HoverString.EndsWith(hint, StringComparison.Ordinal))
                hud.HoverString = string.IsNullOrEmpty(hud.HoverString) ? hint : hud.HoverString + " · " + hint;
        }

        private void OnButtonMouseDown(object sender, MouseButtonEventArgs args)
        {
            if (args.ChangedButton != MouseButton.Right) return;
            args.Handled = true;
            if (menuOpen) { CloseForPointer(); return; }
            closingClick = false; menuOpen = true;
            backdrop.Visibility = Visibility.Visible; menu.Visibility = Visibility.Visible;
            SyncFilters(); UpdateLayout();
            if (hud != null) hud.HoverString = "";
            MenuChanged?.Invoke();
        }

        private void OnButtonMouseUp(object sender, MouseButtonEventArgs args)
        { if (args.ChangedButton == MouseButton.Right) args.Handled = true; }

        private void OnBackdropDown(object sender, MouseButtonEventArgs args)
        { args.Handled = true; CloseForPointer(); }

        private void OnBackdropUp(object sender, MouseButtonEventArgs args)
        { args.Handled = true; }

        private static void ConsumeMenuMouse(object sender, MouseButtonEventArgs args) => args.Handled = true;
        private void OnMenuKeyDown(object sender, KeyEventArgs args)
        { if (args.Key == Key.Escape) { args.Handled = true; CloseMenu(); } }

        private void CloseForPointer()
        {
            closingClick = true;
            bool changed = menuOpen; menuOpen = false;
            if (menu != null) menu.Visibility = Visibility.Collapsed;
            if (backdrop != null) backdrop.Visibility = Visibility.Visible;
            if (changed) MenuChanged?.Invoke();
        }

        internal void AfterInputFrame()
        {
            // Called after EditorDirector.Update consumed this frame's physical input. The closing
            // mouse-up stays shielded for that whole call, including Native before/after input work.
            if (closingClick && !Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2))
            { closingClick = false; if (!menuOpen && backdrop != null) backdrop.Visibility = Visibility.Collapsed; }
        }

        internal void CloseMenu()
        {
            bool changed = menuOpen; menuOpen = false;
            if (menu != null) menu.Visibility = Visibility.Collapsed;
            if (!closingClick && backdrop != null) backdrop.Visibility = Visibility.Collapsed;
            if (changed) MenuChanged?.Invoke();
        }

        internal void SetStatus(string text)
        {
            statusText = text ?? "";
            if (status == null) return;
            status.Text = statusText;
            statusBox.Visibility = statusText.Length == 0 || menuOpen ? Visibility.Collapsed : Visibility.Visible;
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (host == null || overlay == null) return;
            float width = host.ActualWidth, height = host.ActualHeight;
            if (width <= 0 || height <= 0) return;
            backdrop.Width = width; backdrop.Height = height;
            menu.Width = Math.Min(275, Math.Max(1, width - 12));
            menu.MaxHeight = Math.Max(1, height - 12);
            float right = width - 8, top = height - 55;
            if (button != null)
            {
                Noesis.Rect bounds = button.TransformToAncestor(host).TransformBounds(new Noesis.Rect(0, 0, button.ActualWidth, button.ActualHeight));
                right = bounds.Right; top = bounds.Top - 7;
            }
            menu.Measure(new Noesis.Size(Math.Max(1, width - 12), Math.Max(1, height - 12)));
            float menuHeight = menu.DesiredSize.Height;
            Canvas.SetLeft(menu, Math.Max(6, Math.Min(width - (float)menu.Width - 6, right - (float)menu.Width)));
            Canvas.SetTop(menu, Math.Max(6, Math.Min(height - menuHeight - 6, top - menuHeight)));
            statusBox.Measure(new Noesis.Size(Math.Max(1, width - 12), Math.Max(1, height - 12)));
            Canvas.SetLeft(statusBox, Math.Max(6, Math.Min(width - statusBox.DesiredSize.Width - 6, right - statusBox.DesiredSize.Width)));
            Canvas.SetTop(statusBox, Math.Max(6, top - statusBox.DesiredSize.Height));
            statusBox.Visibility = statusText.Length == 0 || menuOpen ? Visibility.Collapsed : Visibility.Visible;
        }

        internal void Reset()
        {
            bool changed = menuOpen || closingClick;
            menuOpen = closingClick = false;
            if (button != null)
            {
                button.MouseEnter -= OnButtonMouseEnter;
                button.PreviewMouseDown -= OnButtonMouseDown;
                button.PreviewMouseUp -= OnButtonMouseUp;
            }
            if (host != null && overlay != null) host.Children.Remove(overlay);
            filters.Clear(); hud = null; button = null; host = null; overlay = null;
            backdrop = menu = statusBox = null; status = null;
            if (changed) MenuChanged?.Invoke();
        }

        private void Report(Exception error)
        {
            if (reportedFailure) return;
            reportedFailure = true;
            log.LogError(DateTime.Now.ToString("HH:mm:ss.fff") + " Area demolition menu: " + error);
        }
    }
}
