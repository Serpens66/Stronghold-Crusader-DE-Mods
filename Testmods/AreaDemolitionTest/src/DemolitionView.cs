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
        private static readonly TargetGroup[] FilterOrder={TargetGroup.Buildings,TargetGroup.Stockpiles,TargetGroup.Fortifications,TargetGroup.Walls,
            TargetGroup.PositiveFear,TargetGroup.NegativeFear,TargetGroup.DogCages,TargetGroup.KillingPits,TargetGroup.Pitch,TargetGroup.DugMoats,TargetGroup.Decorations};
        private readonly DemolitionSettings settings;
        private readonly ManualLogSource log;
        private readonly Func<bool> consumePrimaryRelease;
        private readonly Action primaryPressed;
        private readonly HitTestFilterCallback pointerFilter;
        private readonly HitTestResultCallback pointerResult;
        private readonly List<string> pointerTags=new List<string>();
        private DependencyObject pointerHit;
        private readonly List<CheckBox> filters = new List<CheckBox>();
        private readonly Brush foreground = new SolidColorBrush(Color.FromArgb(255, 244, 232, 196));
        private HUD_Main hud;
        private Button button;
        private Grid host;
        private Canvas overlay;
        private Border backdrop, menu, selectionBox;
        private ScreenRect? selectionFrame;
        private bool menuOpen, closingClick, syncing, reportedFailure;
        private bool layoutDirty=true;
        private float lastWidth=-1,lastHeight=-1,lastRight,lastTop;

        internal DemolitionView(DemolitionSettings settings, ManualLogSource log,Func<bool> consumePrimaryRelease,Action primaryPressed)
        {
            this.settings = settings; this.log = log; this.consumePrimaryRelease=consumePrimaryRelease;this.primaryPressed=primaryPressed;
            pointerFilter=FilterPointerVisual;pointerResult=CapturePointerHit;
        }

        internal event Action MenuChanged;
        internal bool IsMenuOpen => menuOpen;
        internal bool IsInputShieldActive => menuOpen || closingClick;

        internal bool TryIsPointerOverUi(Vector3 mouse,out bool overUi)
        {
            overUi=true;
            if(host==null || overlay==null || !MainViewModel.viewModelLoaded)return false;
            try
            {
                var root=VisualTreeHelper.GetRoot(host) as Visual;
                if(root==null)return false;
                var point=root.PointFromScreen(new Point(mouse.x,Screen.height-mouse.y));
                pointerHit=null;
                // Keep our visuals blocking Vanilla's raw query; this filtered query can still
                // see a real HUD control underneath the selection frame.
                VisualTreeHelper.HitTest(root,pointerFilter,pointerResult,new PointHitTestParameters(point));
                if(pointerHit==null){overUi=false;return true;}
                pointerTags.Clear();
                DependencyObject current=pointerHit;
                while(current is FrameworkElement element)
                {
                    pointerTags.Add(element.Tag as string);
                    current=element.Parent??VisualTreeHelper.GetParent(element);
                }
                var main=MainViewModel.Instance;
                overUi=PointerUiPolicy.IsBlocking(pointerTags,main.IsMapEditorMode && main.MEMode==0);
                return true;
            }
            catch(Exception error){Report(error);return false;}
            finally {pointerHit=null;pointerTags.Clear();}
        }

        private HitTestFilterBehavior FilterPointerVisual(Visual visual)
        {
            var role=ReferenceEquals(visual,selectionBox) ? PointerVisualRole.Decoration :
                ReferenceEquals(visual,overlay) ? PointerVisualRole.Overlay : PointerVisualRole.Ordinary;
            switch(PointerUiPolicy.Filter(role))
            {
                case PointerHitDisposition.SkipSubtree:return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
                case PointerHitDisposition.SkipSelf:return HitTestFilterBehavior.ContinueSkipSelf;
                default:return HitTestFilterBehavior.Continue;
            }
        }

        private HitTestResultBehavior CapturePointerHit(HitTestResult hit)
        {pointerHit=hit.VisualHit;return pointerHit==null?HitTestResultBehavior.Continue:HitTestResultBehavior.Stop;}

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
                    host.PreviewMouseUp += OnHostMouseUp;
                    host.PreviewMouseDown += OnHostMouseDown;
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
            selectionBox = new Border { IsHitTestVisible=false, Visibility=Visibility.Collapsed,
                BorderBrush=new SolidColorBrush(Color.FromArgb(190,180,170,150)), BorderThickness=new Thickness(1),
                Background=new SolidColorBrush(Color.FromArgb(25,130,25,25)) };
            overlay.Children.Add(selectionBox);
            var contents = new StackPanel();
            contents.Children.Add(new TextBlock { Text = Text.Get("MenuTitle"), Foreground = foreground,
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6), FontSize = 17 });
            foreach (TargetGroup group in FilterOrder)
            {
                TargetGroup captured = group;
                string key = "Group" + group;
                var check = new CheckBox { Content = Text.Get(key), Foreground = foreground,
                    FontSize = 15, Margin = new Thickness(0, 2, 0, 2), Tag = group };
                check.Checked += (_, __) => ChangeFilter(captured, true);
                check.Unchecked += (_, __) => ChangeFilter(captured, false);
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
            host.Children.Add(overlay);
            SyncFilters();
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
            closingClick = false; menuOpen = true;layoutDirty=true;
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
        private void OnHostMouseUp(object sender,MouseButtonEventArgs args)
        {
            // Preview routing reaches the common HUD ancestor before a button can act on release.
            // The runtime also remembers a native-input completion for the rest of this frame.
            if(args.ChangedButton==MouseButton.Left && consumePrimaryRelease()) args.Handled=true;
        }
        private void OnHostMouseDown(object sender,MouseButtonEventArgs args)
        { if(args.ChangedButton==MouseButton.Left) primaryPressed(); }
        private void OnMenuKeyDown(object sender, KeyEventArgs args)
        { if (args.Key == Key.Escape) { args.Handled = true; CloseMenu(); } }

        private void CloseForPointer()
        {
            closingClick = true;
            bool changed = menuOpen; menuOpen = false;layoutDirty=true;
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
            if(changed)layoutDirty=true;
            if (menu != null) menu.Visibility = Visibility.Collapsed;
            if (!closingClick && backdrop != null) backdrop.Visibility = Visibility.Collapsed;
            if (changed) MenuChanged?.Invoke();
        }

        internal void SetSelectionFrame(ScreenRect? rectangle)
        { selectionFrame=rectangle;UpdateSelectionFrame(); }

        private void UpdateSelectionFrame()
        {
            if(selectionBox==null || overlay==null) return;
            if(!selectionFrame.HasValue || !selectionFrame.Value.IsFinite || menuOpen)
            {selectionBox.Visibility=Visibility.Collapsed;return;}
            var rect=selectionFrame.Value;
            var topLeft=overlay.PointFromScreen(new Point(rect.MinX,Screen.height-rect.MaxY));
            var bottomRight=overlay.PointFromScreen(new Point(rect.MaxX,Screen.height-rect.MinY));
            Canvas.SetLeft(selectionBox,Math.Min(topLeft.X,bottomRight.X));
            Canvas.SetTop(selectionBox,Math.Min(topLeft.Y,bottomRight.Y));
            selectionBox.Width=Math.Abs(bottomRight.X-topLeft.X);
            selectionBox.Height=Math.Abs(bottomRight.Y-topLeft.Y);
            selectionBox.Visibility=Visibility.Visible;
        }

        private void UpdateLayout()
        {
            if (host == null || overlay == null) return;
            float width = host.ActualWidth, height = host.ActualHeight;
            if (width <= 0 || height <= 0) return;
            float right = width - 8, top = height - 55;
            if (button != null)
            {
                Noesis.Rect bounds = button.TransformToAncestor(host).TransformBounds(new Noesis.Rect(0, 0, button.ActualWidth, button.ActualHeight));
                right = bounds.Right; top = bounds.Top - 7;
            }
            if(!layoutDirty && width==lastWidth && height==lastHeight && right==lastRight && top==lastTop)return;
            layoutDirty=false;lastWidth=width;lastHeight=height;lastRight=right;lastTop=top;
            backdrop.Width = width; backdrop.Height = height;
            if(menuOpen)
            {
                menu.Width = Math.Min(275, Math.Max(1, width - 12));menu.MaxHeight = Math.Max(1, height - 12);
                menu.Measure(new Noesis.Size(Math.Max(1, width - 12), Math.Max(1, height - 12)));
                float menuHeight = menu.DesiredSize.Height;
                Canvas.SetLeft(menu, Math.Max(6, Math.Min(width - (float)menu.Width - 6, right - (float)menu.Width)));
                Canvas.SetTop(menu, Math.Max(6, Math.Min(height - menuHeight - 6, top - menuHeight)));
            }
            UpdateSelectionFrame();
        }

        internal void Reset()
        {
            bool changed = menuOpen || closingClick;
            menuOpen = closingClick = false;
            layoutDirty=true;lastWidth=lastHeight=-1;
            if (button != null)
            {
                button.MouseEnter -= OnButtonMouseEnter;
                button.PreviewMouseDown -= OnButtonMouseDown;
                button.PreviewMouseUp -= OnButtonMouseUp;
            }
            if(host!=null){host.PreviewMouseUp-=OnHostMouseUp;host.PreviewMouseDown-=OnHostMouseDown;if(overlay!=null)host.Children.Remove(overlay);}
            filters.Clear(); hud = null; button = null; host = null; overlay = null;
            backdrop = menu = selectionBox = null;selectionFrame=null;
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
