using System;
using System.Collections.Generic;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static bool toolbarLayoutDirty = true, toolbarMoved, layoutCached;
        private static int observedBuildPage = int.MinValue;
        private static bool observedMainHud;
        // Absolute floating-header position. It never depends on the docked bounding box.
        private static float toolbarDesiredX, toolbarDesiredY;
        private static FrameworkElement alignmentAnchor, frameAnchor, toolbarGoods;
        private static FrameworkElement dateAnchor, alliesAnchor, toolbarControl;
        private static Button toolbarDesign;
        private static readonly ToolbarHitTestLease dateHitTest = new ToolbarHitTestLease();
        private static Border toolbarHeader, rosterSurface, leftWing, rightWing, snapFrame, toolSnapFrame, snapGripFrame;
        private static UIElement toolbarNativeLayer;
        private static Image scrollAnchor;
        private static Brush rosterPaper, controlPaper;
        private static readonly Brush transparentToolbarBrush = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        private static Canvas toolbarWings, toolbarTools, toolbarOwners;
        private static DockHudDisplay toolbarHudDisplay;
        private static readonly List<FrameworkElement> protectedControls = new List<FrameworkElement>();
        private static readonly List<FrameworkElement> centerOwners = new List<FrameworkElement>();
        private static readonly List<FrameworkElement> buildCategories = new List<FrameworkElement>();
        private static readonly List<FrameworkElement> observedLayout = new List<FrameworkElement>();
        private static readonly List<ToolbarRect> observedBounds = new List<ToolbarRect>();
        private static readonly List<ToolbarRect> controlsBounds = new List<ToolbarRect>();
        private static readonly List<ToolbarRect> solidPieces = new List<ToolbarRect>(2);
        private static readonly List<ToolbarRect> paintedPieces = new List<ToolbarRect>(4);
        private static readonly List<ToolbarRect> footerSegments = new List<ToolbarRect>(8);
        private static readonly List<Border> footerCovers = new List<Border>(8);
        private static readonly Brush snapIdleBrush = new SolidColorBrush(Color.FromArgb(136, 151, 118, 69));
        private static readonly Brush snapReadyBrush = new SolidColorBrush(Color.FromRgb(239, 198, 112));
        private static ToolbarRect toolbarDockGrip, toolbarVisibleGrip;
        private static float toolbarFloatingScale = 1;
        private static readonly ToolbarDragAnchor toolbarDrag = new ToolbarDragAnchor();

        private static void BindToolbarLayout()
        {
            toolbarHeader = humanScreen.FindName("SpectatorHeader") as Border;
            rosterSurface = humanScreen.FindName("SpectatorRosterPanel") as Border;
            toolbarTools = humanScreen.FindName("SpectatorToolPanel") as Canvas;
            toolbarOwners = humanScreen.FindName("SpectatorEditorHumanOwners") as Canvas;
            toolbarControl = humanScreen.FindName("SpectatorControlSlot") as FrameworkElement;
            toolbarDesign = humanScreen.FindName("SpectatorDesign") as Button;
            toolbarWings = humanScreen.FindName("SpectatorWings") as Canvas;
            leftWing = humanScreen.FindName("SpectatorLeftWing") as Border;
            rightWing = humanScreen.FindName("SpectatorRightWing") as Border;
            toolbarGoods = humanScreen.FindName("SpectatorGoodsGrid") as FrameworkElement;
            snapFrame = humanScreen.FindName("SpectatorSnapFrame") as Border;
            toolSnapFrame = humanScreen.FindName("SpectatorToolSnapFrame") as Border;
            snapGripFrame = humanScreen.FindName("SpectatorSnapGrip") as Border;
            BindToolbarPaper();
            if (toolbarHeader == null || rosterSurface == null || toolbarTools == null || toolbarOwners == null ||
                toolbarWings == null || leftWing == null || rightWing == null || toolbarGoods == null ||
                snapFrame == null || toolSnapFrame == null || snapGripFrame == null || toolbarControl == null || toolbarDesign == null)
                throw new MissingMemberException("Spectator raster/tool surfaces missing.");
            ObserveLayout(toolbarHeader);
            // A reused view can still have our floating transparent background.
            // Resolve the declared resource instead of saving that transient value.
            rosterPaper = humanPanel.TryFindResource("SpectatorPaper") as Brush;
            if (rosterPaper == null) throw new MissingMemberException("Spectator paper resource missing.");
            controlPaper = rosterPaper;
            // These are siblings in IngameUIScreens. Only our own surface changes its draw order.
            toolbarNativeLayer = humanScreen.FindName("MainHUD") as UIElement;
            if (toolbarNativeLayer == null || !ReferenceEquals(VisualTreeHelper.GetParent(toolbarNativeLayer), VisualTreeHelper.GetParent(humanCanvas)))
                throw new MissingMemberException("Shared Spectator/native stacking parent missing.");
            RefreshToolbarLayer();
            toolbarLayoutDirty = true;
        }

        private static void ObserveLayout(FrameworkElement element)
        {
            if (element == null || observedLayout.Contains(element)) return;
            observedLayout.Add(element); observedBounds.Add(default);
            element.SizeChanged += OnToolbarLayoutSize;
            element.IsVisibleChanged += OnToolbarLayoutVisibility;
            element.IsEnabledChanged += OnToolbarLayoutVisibility;
        }
        private static void OnToolbarLayoutSize(object sender, SizeChangedEventArgs args) => toolbarLayoutDirty = true;
        private static void OnToolbarLayoutVisibility(object sender, DependencyPropertyChangedEventArgs args) => toolbarLayoutDirty = true;

        private static void ObserveCachedBounds()
        {
            // Native transforms/margins need not raise SizeChanged. No tree searches in this path.
            for (int i = 0; i < observedLayout.Count; i++)
            {
                var bounds = CanvasBounds(observedLayout[i]); var previous = observedBounds[i];
                if (bounds.X == previous.X && bounds.Y == previous.Y &&
                    bounds.Width == previous.Width && bounds.Height == previous.Height) continue;
                observedBounds[i] = bounds; toolbarLayoutDirty = true;
            }
        }

        private static void CacheVanillaLayout(DependencyObject root)
        {
            if (root is FrameworkElement element)
            {
                if (element.Name == "HUD_Troops" || element.Name == "HUD_Buildings") CacheCenterOwner(element);
                if (element.Name == "OST_Date") dateAnchor = element;
                if (element.Name == "ButtonAllies") alliesAnchor = element;
                if (element is RadioButton && element.Name != null &&
                    (element.Name.StartsWith("RadioButton", StringComparison.Ordinal) ||
                     element.Name.StartsWith("TabBuild", StringComparison.Ordinal)))
                { buildCategories.Add(element); ObserveLayout(element); }
                else if (element.Name == "OST_Date" || element.Name == "ButtonAllies" ||
                    element.Name == "ButtonMerit" || element.Name == "SideTabs" ||
                    element.Name == "RadarMapGrid" || element.Name == "ReportsControl")
                { protectedControls.Add(element); ObserveLayout(element); }
            }
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++) CacheVanillaLayout(VisualTreeHelper.GetChild(root, i));
        }

        private static void CacheCenterOwner(FrameworkElement element)
        {
            if (element == null || centerOwners.Contains(element)) return;
            centerOwners.Add(element); ObserveLayout(element);
        }

        private static ToolbarRect CanvasBounds(FrameworkElement element)
        {
            if (element == null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return default;
            Point a = element.TranslatePoint(new Point(0, 0), humanCanvas);
            Point b = element.TranslatePoint(new Point(element.ActualWidth, element.ActualHeight), humanCanvas);
            return new ToolbarRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        }

        private static void AddControlBounds(List<FrameworkElement> controls)
        {
            foreach (var control in controls)
            {
                var bounds = CanvasBounds(control);
                if (!bounds.Empty) controlsBounds.Add(bounds);
            }
        }

        private static void UpdatePlacementDate(bool active) =>
            dateHitTest.Update(dateAnchor, UIElement.IsHitTestVisibleProperty, active);

        private static void HideBlockedToolbar()
        {
            humanPanel.Opacity = 0; humanPanel.IsHitTestVisible = false;
            SuspendToolbarInteraction();
        }

        private static void RefreshToolbarLayer()
        {
            // Read cached references in the existing render path, never search the tree here.
            int nativeLayer = Panel.GetZIndex(toolbarNativeLayer);
            int layer = Math.Max(10031, nativeLayer == int.MaxValue ? int.MaxValue : nativeLayer + 1);
            if (Panel.GetZIndex(humanCanvas) != layer) Panel.SetZIndex(humanCanvas, layer);
        }

        private static void PositionToolbar()
        {
            if (humanPanel == null || humanPanel.Visibility != Visibility.Visible || humanCanvas == null ||
                !humanCanvas.IsVisible || humanCanvas.ActualWidth <= 0 || humanCanvas.ActualHeight <= 0) return;
            RefreshToolbarLayer();
            if (!layoutCached)
            {
                if (humanBoundHud?.IsLoaded != true || humanScreen?.IsLoaded != true) return;
                alignmentAnchor = humanBoundHud.FindName("AlignmentGrid") as FrameworkElement;
                frameAnchor = humanBoundHud.FindName("MainFrameBuildings") as FrameworkElement;
                scrollAnchor = humanBoundHud.FindName("MainScroll") as Image;
                ObserveLayout(alignmentAnchor); ObserveLayout(frameAnchor);
                ObserveLayout(scrollAnchor);
                CacheVanillaLayout(humanScreen.FindName("MainHUD") as DependencyObject ?? humanBoundHud);
                CacheCenterOwner(CrusaderDE.MainViewModel.Instance.HUDAlliesPanel?.FindName("LayoutRoot") as FrameworkElement);
                CacheCenterOwner(CrusaderDE.MainViewModel.Instance.HUDMeritPanel?.FindName("LayoutRoot") as FrameworkElement);
                CacheCenterOwner(CrusaderDE.MainViewModel.Instance.HUDMPChatPanel?.FindName("LayoutRoot") as FrameworkElement);
                layoutCached = true; toolbarLayoutDirty = true;
            }
            UpdatePlacementDate(humanPlacementMode);
            RefreshPortraitNumbers();
            ObserveCachedBounds();
            var currentView = CrusaderDE.MainViewModel.Instance;
            if (observedBuildPage != currentView.buildScreenID || observedMainHud != currentView.Show_HUD_Main)
            { observedBuildPage = currentView.buildScreenID; observedMainHud = currentView.Show_HUD_Main; toolbarLayoutDirty = true; }
            if (!toolbarLayoutDirty) return;
            toolbarLayoutDirty = false;
            float vw = humanCanvas.ActualWidth, vh = humanCanvas.ActualHeight;
            var alignment = CanvasBounds(alignmentAnchor);
            if (alignment.Empty) alignment = new ToolbarRect(Math.Max(0, vw - 914), vh - 166, 914, 166);
            float nativeScale = alignment.Width / 914;
            var frame = CanvasBounds(frameAnchor);
            bool frameVisible = !frame.Empty;
            if (frame.Empty) frame = new ToolbarRect(alignment.X + 114 * nativeScale,
                alignment.Bottom - 144 * nativeScale, 800 * nativeScale, 144 * nativeScale);
            var view = CrusaderDE.MainViewModel.Instance;
            bool foreign = false;
            var center = ToolbarLayout.Dock(alignment);
            foreach (var owner in centerOwners) if (CanvasBounds(owner).Intersects(center)) { foreign = true; break; }
            bool occupied = DockHudPolicy.CenterIsOccupied(view.Show_HUD_Main, view.buildScreenID, humanPlacementMode, foreign);
            toolbarHudDisplay = DockHudPolicy.Evaluate(true, true, occupied, humanPlacementMode);
            toolsModel.SetHudAvailable(DockHudPolicy.CanShowDetails(toolbarMoved, toolbarHudDisplay));
            bool details = toolsModel.ExpandedVisibility == Visibility.Visible;
            int count = 0;
            for (int player = 1; player <= 8; player++)
            {
                var button = humanOwnerButtons[player];
                if (button.Visibility != Visibility.Visible) continue;
                var cell = ToolbarLayout.OwnerCell(count++, toolbarMoved);
                Canvas.SetLeft(button, cell.X); Canvas.SetTop(button, cell.Y);
            }
            var allies = CanvasBounds(alliesAnchor);
            var dockControl = allies.Empty ? default : new ToolbarRect(
                allies.X - (ToolbarLayout.ControlWidth + 2) * nativeScale,
                allies.Bottom - (ToolbarLayout.ControlHeight + 2) * nativeScale,
                ToolbarLayout.ControlWidth * nativeScale, ToolbarLayout.ControlHeight * nativeScale);
            var dockPose = ToolbarLayout.Arrange(alignment, frame, count, details, false, dockControl);
            toolbarDockGrip = ToolbarLayout.DockDropTarget(alignment, frame);
            var floatingPose = ToolbarLayout.Arrange(alignment, frame, count, details, true, default, humanPlacementMode);
            var pose = toolbarMoved ? floatingPose : dockPose;
            float units = toolbarMoved ? 1 : nativeScale;
            float width = pose.Bounds.Width / units, height = pose.Bounds.Height / units;
            toolbarFloatingScale = Math.Min(nativeScale, Math.Min(vw / floatingPose.Bounds.Width, vh / floatingPose.Bounds.Height));
            float scale = toolbarMoved ? toolbarFloatingScale : Math.Min(nativeScale, Math.Min(vw / width, vh / height));
            humanPanel.Width = toolbarRoot.Width = width; humanPanel.Height = toolbarRoot.Height = height;
            toolsModel.SetFloatingTheme(toolbarMoved && ToolbarThemePreference.Classic);
            toolbarDesign.Visibility = toolbarMoved ? Visibility.Visible : Visibility.Collapsed;
            if (toolbarMoved) Place(toolbarDesign, ToolbarLayout.FloatingDesignRect(count, humanPlacementMode));
            humanPanel.RenderTransform = new ScaleTransform(scale, scale);
            var roster = Relative(pose.Roster, pose.Bounds, units);
            var tools = Relative(pose.Tools, pose.Bounds, units);
            var control = Relative(pose.Control, pose.Bounds, units);
            var grip = Relative(pose.Grip, pose.Bounds, units);
            var groupDock = new ToolbarRect(pose.Roster.X - roster.X * scale,
                alignment.Bottom - (roster.Y + roster.Height) * scale, width * scale, height * scale);
            var position = ToolbarLayout.Clamp(toolbarMoved ? toolbarDesiredX : groupDock.X,
                toolbarMoved ? toolbarDesiredY : groupDock.Y, width * scale, height * scale, vw, vh);
            toolbarOwners.Width = roster.Width; toolbarOwners.Height = roster.Height;
            Place(rosterSurface, roster); Place(toolbarControl, control); Place(humanDragHandle, grip);
            bool controlVisible = toolbarMoved || humanPlacementMode || toolbarHudDisplay != DockHudDisplay.Hidden;
            toolbarControl.Visibility = controlVisible ? Visibility.Visible : Visibility.Collapsed;
            rosterSurface.Background = toolbarMoved ? transparentToolbarBrush : rosterPaper;
            ((Border)toolbarControl).Background = toolbarMoved ? transparentToolbarBrush : controlPaper;
            if (details) Place(toolbarTools, tools);
            toolbarGoods.Width = 297;
            paintedPieces.Clear(); paintedPieces.Add(roster); paintedPieces.Add(grip);
            if (controlVisible) paintedPieces.Add(control);
            solidPieces.Clear(); solidPieces.Add(Scaled(roster, scale)); solidPieces.Add(Scaled(grip, scale));
            if (details) solidPieces.Add(Scaled(new ToolbarRect(tools.X, tools.Y, tools.Width,
                ToolbarLayout.Header + ToolbarLayout.Body + (toolbarMoved ? ToolbarLayout.Footer : 0)), scale));
            controlsBounds.Clear(); AddControlBounds(protectedControls);
            bool coverCategories = DockHudPolicy.CanCoverBuildCategories(toolbarHudDisplay, toolsModel.Expanded,
                view.Show_HUD_Main && view.buildScreenID == DockHudPolicy.BlankBuildScreen) && details;
            if (!coverCategories) AddControlBounds(buildCategories);
            if (!toolbarMoved && controlVisible)
            {
                var area = new ToolbarRect(position.X + control.X * scale, position.Y + control.Y * scale,
                    control.Width * scale, control.Height * scale);
                foreach (var reserved in controlsBounds)
                    if (area.Intersects(reserved)) { HideBlockedToolbar(); return; }
            }
            // The common control owns only its reserved native gap. The roster/grip never enter the build frame.
            if (frameVisible && toolbarHudDisplay != DockHudDisplay.Tools) controlsBounds.Add(frame);
            if (!ToolbarLayout.Position(toolbarMoved, position.X, position.Y, position.Width, position.Height,
                vw, vh, solidPieces, controlsBounds, ToolbarLayout.Gap * scale, out position) ||
                (!toolbarMoved && (Math.Abs(position.X - groupDock.X) > .01f || Math.Abs(position.Y - groupDock.Y) > .01f)))
            { HideBlockedToolbar(); return; }
            humanPanel.Opacity = 1; humanPanel.IsHitTestVisible = true;
            Canvas.SetLeft(humanPanel, position.X); Canvas.SetTop(humanPanel, position.Y);
            toolbarVisibleGrip = new ToolbarRect(position.X + grip.X * scale, position.Y + grip.Y * scale,
                grip.Width * scale, grip.Height * scale);
            toolbarPaperPieces.Clear();
            footerSegments.Clear();
            if (toolbarMoved) toolbarPaperPieces.Add(roster);
            float paperBottom = roster.Bottom;
            if (details)
            {
                ToolbarRect left, right;
                if (toolbarMoved)
                {
                    left = toolsModel.ActiveViewKey == "Orders" ? new ToolbarRect(0, 0, tools.Width, ToolbarLayout.Footer) : default;
                    right = toolsModel.ActiveViewKey == "Goods" ? new ToolbarRect(0, 0, tools.Width, ToolbarLayout.Footer) : default;
                }
                else
                {
                    if (controlVisible) controlsBounds.Add(new ToolbarRect(position.X + control.X * scale,
                        position.Y + control.Y * scale, control.Width * scale, control.Height * scale));
                    float toolX = position.X + tools.X * scale, toolY = position.Y + tools.Y * scale;
                    float bandY = toolY + (ToolbarLayout.Header + ToolbarLayout.Body) * scale;
                    var band = new ToolbarRect(toolX, bandY, tools.Width * scale, Math.Max(ToolbarLayout.Footer * scale, alignment.Bottom - bandY));
                    ToolbarLayout.FooterSegments(band, controlsBounds, footerSegments);
                    ToolbarRect a = default, b = default;
                    for (int i = 0; i < footerSegments.Count; i++)
                    {
                        var free = footerSegments[i];
                        var local = Relative(free, position, scale);
                        paintedPieces.Add(local); toolbarPaperPieces.Add(local);
                        paperBottom = Math.Max(paperBottom, local.Bottom);
                        footerSegments[i] = local;
                        if (free.Width > a.Width) { b = a; a = free; }
                        else if (free.Width > b.Width) b = free;
                    }
                    if (toolsModel.ActiveViewKey == "Orders")
                    { left = a.Width >= b.Width ? a : b; right = default; }
                    else { right = a.Width >= b.Width ? a : b; left = default; }
                    left = WingLocal(left, toolX, scale); right = WingLocal(right, toolX, scale);
                }
                PlaceWing(leftWing, left); PlaceWing(rightWing, right);
                var body = new ToolbarRect(tools.X, tools.Y, tools.Width, ToolbarLayout.Header + ToolbarLayout.Body);
                paintedPieces.Add(body);
                if (!toolbarMoved) toolbarPaperPieces.Add(body);
                else toolbarPaperPieces.Add(new ToolbarRect(tools.X, tools.Y, tools.Width, tools.Height));
                foreach (var wing in new[] { left, right })
                {
                    if (wing.Empty) continue;
                    var piece = new ToolbarRect(tools.X + wing.X, tools.Y + ToolbarLayout.Header + ToolbarLayout.Body,
                        wing.Width, ToolbarLayout.Footer);
                    paintedPieces.Add(piece);
                }
            }
            UpdateFooterCovers();
            var nativePaper = CanvasBounds(scrollAnchor);
            RenderToolbarPaper(width, height, toolbarMoved, nativePaper.Empty ? default : Relative(nativePaper, position, scale),
                Math.Max(paperBottom, tools.Empty ? 0 : tools.Bottom), scrollAnchor);
            PlaceGuide(snapFrame, dockPose.Roster, position, scale, true);
            PlaceGuide(toolSnapFrame, dockPose.Tools, position, scale, details);
            PlaceGuide(snapGripFrame, toolbarDockGrip, position, scale, true);
        }

        private static ToolbarRect Scaled(ToolbarRect area, float scale) =>
            new ToolbarRect(area.X * scale, area.Y * scale, area.Width * scale, area.Height * scale);
        private static ToolbarRect Relative(ToolbarRect area, ToolbarRect root, float scale) => area.Empty ? default :
            new ToolbarRect((area.X - root.X) / scale, (area.Y - root.Y) / scale, area.Width / scale, area.Height / scale);
        private static ToolbarRect WingLocal(ToolbarRect area, float origin, float scale) => area.Empty ? default :
            new ToolbarRect((area.X - origin) / scale, ToolbarLayout.Header + ToolbarLayout.Body, area.Width / scale, ToolbarLayout.Footer);
        private static void Place(FrameworkElement element, ToolbarRect bounds)
        {
            element.Width = bounds.Width; element.Height = bounds.Height;
            Canvas.SetLeft(element, bounds.X); Canvas.SetTop(element, bounds.Y);
        }
        private static void PlaceWing(Border element, ToolbarRect bounds)
        {
            element.Visibility = bounds.Empty ? Visibility.Collapsed : Visibility.Visible;
            // Wings are children of the footer Canvas, so its row offset is already supplied.
            element.Width = bounds.Width; Canvas.SetLeft(element, bounds.X);
        }
        private static void PlaceGuide(Border guide, ToolbarRect target, ToolbarRect position, float scale, bool visible)
        {
            // Targets always describe the docked layout, even while the live view is a row.
            guide.Width = target.Width / scale; guide.Height = target.Height / scale;
            Canvas.SetLeft(guide, (target.X - position.X) / scale);
            Canvas.SetTop(guide, (target.Y - position.Y) / scale);
            guide.Visibility = humanDragging && visible ? Visibility.Visible : Visibility.Collapsed;
            guide.BorderBrush = toolbarDrag.Preview ? snapReadyBrush : snapIdleBrush;
        }
        private static void HideDockGuides()
        {
            if (snapFrame != null) snapFrame.Visibility = Visibility.Collapsed;
            if (toolSnapFrame != null) toolSnapFrame.Visibility = Visibility.Collapsed;
            if (snapGripFrame != null) snapGripFrame.Visibility = Visibility.Collapsed;
        }
        private static void OnFooterCoverMouseDown(object sender, MouseButtonEventArgs args) => args.Handled = true;
        private static void UpdateFooterCovers()
        {
            while (footerCovers.Count < footerSegments.Count)
            {
                var cover = new Border { Background = transparentToolbarBrush };
                Panel.SetZIndex(cover, 1); cover.MouseDown += OnFooterCoverMouseDown;
                toolbarRoot.Children.Add(cover); footerCovers.Add(cover);
            }
            for (int i = 0; i < footerCovers.Count; i++)
            {
                footerCovers[i].Visibility = i < footerSegments.Count ? Visibility.Visible : Visibility.Collapsed;
                if (i < footerSegments.Count) Place(footerCovers[i], footerSegments[i]);
            }
        }
        private static void SnapToolbarDrag(float x, float y)
        {
            Point origin = humanCanvas.PointToScreen(new Point(0, 0));
            Point unit = humanCanvas.PointToScreen(new Point(100, 100));
            toolbarMoved = toolbarDrag.Move(x, y, toolbarDockGrip,
                (unit.X - origin.X) / 100, (unit.Y - origin.Y) / 100,
                toolbarFloatingScale, out toolbarDesiredX, out toolbarDesiredY);
            toolbarLayoutDirty = true;
        }
        private static void DetachToolbarLayout()
        {
            UpdatePlacementDate(false);
            HideDockGuides(); toolbarDrag.Cancel();
            foreach (var cover in footerCovers) { cover.MouseDown -= OnFooterCoverMouseDown; toolbarRoot?.Children.Remove(cover); }
            footerCovers.Clear(); footerSegments.Clear();
            foreach (var item in observedLayout)
            {
                item.SizeChanged -= OnToolbarLayoutSize;
                item.IsVisibleChanged -= OnToolbarLayoutVisibility;
                item.IsEnabledChanged -= OnToolbarLayoutVisibility;
            }
            observedLayout.Clear(); observedBounds.Clear(); protectedControls.Clear(); centerOwners.Clear(); buildCategories.Clear(); controlsBounds.Clear(); solidPieces.Clear(); paintedPieces.Clear();
            toolbarHeader = rosterSurface = leftWing = rightWing = snapFrame = toolSnapFrame = snapGripFrame = null;
            toolbarNativeLayer = null;
            alignmentAnchor = frameAnchor = toolbarGoods = dateAnchor = alliesAnchor = toolbarControl = null;
            toolbarDesign = null;
            toolbarWings = toolbarTools = toolbarOwners = null;
            scrollAnchor = null; rosterPaper = controlPaper = null;
            DetachToolbarPaper();
            observedBuildPage = int.MinValue;
            toolbarHudDisplay = DockHudDisplay.Hidden; layoutCached = false; toolbarLayoutDirty = true;
        }
    }
}
