using System;
using System.Collections.Generic;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static bool toolbarLayoutDirty = true, toolbarMoved, layoutCached;
        // Desired translation of the entire group, retained across compact/placement transitions.
        private static float toolbarDesiredX, toolbarDesiredY;
        private static FrameworkElement alignmentAnchor, frameAnchor, toolbarGoods;
        private static Border toolbarHeader, rosterSurface, leftWing, rightWing, snapFrame, toolSnapFrame, paperSurface;
        private static Canvas toolbarWings, toolbarTools, toolbarOwners;
        private static DockHudDisplay toolbarHudDisplay;
        private static readonly List<FrameworkElement> protectedControls = new List<FrameworkElement>();
        private static readonly List<FrameworkElement> buildCategories = new List<FrameworkElement>();
        private static readonly List<FrameworkElement> observedLayout = new List<FrameworkElement>();
        private static readonly List<ToolbarRect> observedBounds = new List<ToolbarRect>();
        private static readonly List<ToolbarRect> controlsBounds = new List<ToolbarRect>();
        private static readonly List<ToolbarRect> solidPieces = new List<ToolbarRect>(2);
        private static readonly List<ToolbarRect> paintedPieces = new List<ToolbarRect>(4);
        private static ToolbarRect toolbarDock;

        private static void BindToolbarLayout()
        {
            toolbarHeader = humanScreen.FindName("SpectatorHeader") as Border;
            rosterSurface = humanScreen.FindName("SpectatorRosterPanel") as Border;
            toolbarTools = humanScreen.FindName("SpectatorToolPanel") as Canvas;
            toolbarOwners = humanScreen.FindName("SpectatorEditorHumanOwners") as Canvas;
            toolbarWings = humanScreen.FindName("SpectatorWings") as Canvas;
            leftWing = humanScreen.FindName("SpectatorLeftWing") as Border;
            rightWing = humanScreen.FindName("SpectatorRightWing") as Border;
            toolbarGoods = humanScreen.FindName("SpectatorGoodsGrid") as FrameworkElement;
            snapFrame = humanScreen.FindName("SpectatorSnapFrame") as Border;
            toolSnapFrame = humanScreen.FindName("SpectatorToolSnapFrame") as Border;
            paperSurface = humanScreen.FindName("SpectatorPaperSurface") as Border;
            if (toolbarHeader == null || rosterSurface == null || toolbarTools == null || toolbarOwners == null ||
                toolbarWings == null || leftWing == null || rightWing == null || toolbarGoods == null ||
                snapFrame == null || toolSnapFrame == null || paperSurface == null)
                throw new MissingMemberException("Spectator raster/tool surfaces missing.");
            ObserveLayout(toolbarHeader);
            // These are siblings in IngameUIScreens. Only our own surface changes its draw order.
            var main = humanScreen.FindName("MainHUD") as UIElement;
            if (main != null && ReferenceEquals(VisualTreeHelper.GetParent(main), VisualTreeHelper.GetParent(humanCanvas)))
                Panel.SetZIndex(humanCanvas, Math.Max(10031, Panel.GetZIndex(main) + 1));
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

        private static void PositionToolbar()
        {
            if (humanPanel == null || humanPanel.Visibility != Visibility.Visible || humanCanvas == null ||
                humanCanvas.ActualWidth <= 0 || humanCanvas.ActualHeight <= 0) return;
            if (!layoutCached)
            {
                if (humanBoundHud?.IsLoaded != true || humanScreen?.IsLoaded != true) return;
                alignmentAnchor = humanBoundHud.FindName("AlignmentGrid") as FrameworkElement;
                frameAnchor = humanBoundHud.FindName("MainFrameBuildings") as FrameworkElement;
                ObserveLayout(alignmentAnchor); ObserveLayout(frameAnchor);
                CacheVanillaLayout(humanScreen.FindName("MainHUD") as DependencyObject ?? humanBoundHud);
                layoutCached = true; toolbarLayoutDirty = true;
            }
            ObserveCachedBounds();
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
            bool details = toolsModel.ExpandedVisibility == Visibility.Visible;
            int count = 0;
            for (int player = 1; player <= 8; player++)
            {
                var button = humanOwnerButtons[player];
                if (button.Visibility != Visibility.Visible) continue;
                var cell = ToolbarLayout.OwnerCell(count++);
                Canvas.SetLeft(button, cell.X); Canvas.SetTop(button, cell.Y);
            }
            toolbarOwners.Height = ((count + 1) / 2) * ToolbarLayout.Cell;
            var pose = ToolbarLayout.Arrange(alignment, frame, count, details);
            float width = pose.Bounds.Width / nativeScale, height = pose.Bounds.Height / nativeScale;
            float scale = Math.Min(nativeScale, Math.Min(vw / width, vh / height));
            humanPanel.Width = width; humanPanel.Height = height;
            humanPanel.RenderTransform = new ScaleTransform(scale, scale);
            toolbarDock = ToolbarLayout.Clamp(pose.Bounds.X, pose.Bounds.Y, width * scale, height * scale, vw, vh);
            var position = ToolbarLayout.Clamp(toolbarDock.X + (toolbarMoved ? toolbarDesiredX : 0),
                toolbarDock.Y + (toolbarMoved ? toolbarDesiredY : 0), width * scale, height * scale, vw, vh);
            var roster = Relative(pose.Roster, pose.Bounds, nativeScale);
            var tools = Relative(pose.Tools, pose.Bounds, nativeScale);
            Place(rosterSurface, roster);
            if (details) Place(toolbarTools, tools);
            toolbarGoods.Width = 297;
            paintedPieces.Clear(); paintedPieces.Add(roster);
            solidPieces.Clear();
            solidPieces.Add(Scaled(roster, scale));
            if (details) solidPieces.Add(Scaled(new ToolbarRect(tools.X, tools.Y, tools.Width, ToolbarLayout.Header + ToolbarLayout.Body), scale));
            controlsBounds.Clear(); AddControlBounds(protectedControls);
            bool coverCategories = DockHudPolicy.CanCoverBuildCategories(toolbarHudDisplay, toolsModel.Expanded) && details;
            if (!coverCategories) AddControlBounds(buildCategories);
            if (frameVisible && toolbarHudDisplay != DockHudDisplay.Tools) controlsBounds.Add(frame);
            if (!ToolbarLayout.TryAvoidControls(position.X, position.Y, position.Width, position.Height,
                vw, vh, solidPieces, controlsBounds, ToolbarLayout.Gap * scale, out position))
            {
                // A blocking native surface owns the viewport. Keep no invisible input mask.
                humanPanel.Opacity = 0; humanPanel.IsHitTestVisible = false;
                amountInput?.EndEditing(); ClosePlayerTooltips();
                humanDragging = false;
                if (humanDragHandle.IsMouseCaptured) humanDragHandle.ReleaseMouseCapture();
                return;
            }
            humanPanel.Opacity = 1; humanPanel.IsHitTestVisible = true;
            paperSurface.Visibility = toolbarMoved || position.X != toolbarDock.X || position.Y != toolbarDock.Y
                ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetLeft(humanPanel, position.X); Canvas.SetTop(humanPanel, position.Y);
            if (details)
            {
                float toolX = position.X + tools.X * scale, toolY = position.Y + tools.Y * scale;
                var band = new ToolbarRect(toolX, toolY + (ToolbarLayout.Header + ToolbarLayout.Body) * scale,
                    tools.Width * scale, ToolbarLayout.Footer * scale);
                ToolbarLayout.Wings(band, controlsBounds, out var left, out var right);
                if (toolsModel.ActiveViewKey == "Orders" && left.Width < right.Width)
                { var swap = left; left = right; right = swap; }
                if (toolsModel.ActiveViewKey == "Goods")
                { right = left.Width >= right.Width ? left : right; left = default; }
                var localLeft = WingLocal(left, toolX, scale); var localRight = WingLocal(right, toolX, scale);
                PlaceWing(leftWing, localLeft); PlaceWing(rightWing, localRight);
                paintedPieces.Add(new ToolbarRect(tools.X, tools.Y, tools.Width, ToolbarLayout.Header + ToolbarLayout.Body));
                if (!left.Empty) paintedPieces.Add(new ToolbarRect(tools.X + localLeft.X, tools.Y + localLeft.Y, localLeft.Width, localLeft.Height));
                if (!right.Empty) paintedPieces.Add(new ToolbarRect(tools.X + localRight.X, tools.Y + localRight.Y, localRight.Width, localRight.Height));
            }
            PlaceGuide(snapFrame, roster, position, scale, true);
            PlaceGuide(toolSnapFrame, tools, position, scale, details);
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
        private static void PlaceGuide(Border guide, ToolbarRect bounds, ToolbarRect position, float scale, bool visible)
        {
            // Children of the common surface inherit hiding/opacity, while their inverse
            // translation leaves the guides at the default target during a drag.
            guide.Width = bounds.Width; guide.Height = bounds.Height;
            Canvas.SetLeft(guide, bounds.X + (toolbarDock.X - position.X) / scale);
            Canvas.SetTop(guide, bounds.Y + (toolbarDock.Y - position.Y) / scale);
            guide.Visibility = humanDragging && visible ? Visibility.Visible : Visibility.Collapsed;
        }
        private static void SnapToolbarDrag(float x, float y)
        {
            Point origin = humanCanvas.PointToScreen(new Point(0, 0));
            Point unit = humanCanvas.PointToScreen(new Point(100, 100));
            toolbarMoved = !ToolbarLayout.Snap(x, y, toolbarDock,
                (unit.X - origin.X) / 100, (unit.Y - origin.Y) / 100, !toolbarMoved);
            toolbarDesiredX = x - toolbarDock.X; toolbarDesiredY = y - toolbarDock.Y;
            toolbarLayoutDirty = true;
        }
        private static void DetachToolbarLayout()
        {
            if (snapFrame != null) snapFrame.Visibility = Visibility.Collapsed;
            if (toolSnapFrame != null) toolSnapFrame.Visibility = Visibility.Collapsed;
            foreach (var item in observedLayout)
            {
                item.SizeChanged -= OnToolbarLayoutSize;
                item.IsVisibleChanged -= OnToolbarLayoutVisibility;
                item.IsEnabledChanged -= OnToolbarLayoutVisibility;
            }
            observedLayout.Clear(); observedBounds.Clear(); protectedControls.Clear(); buildCategories.Clear(); controlsBounds.Clear(); solidPieces.Clear(); paintedPieces.Clear();
            toolbarHeader = rosterSurface = leftWing = rightWing = snapFrame = toolSnapFrame = paperSurface = null;
            alignmentAnchor = frameAnchor = toolbarGoods = null;
            toolbarWings = toolbarTools = toolbarOwners = null;
            toolbarHudDisplay = DockHudDisplay.Hidden; layoutCached = false; toolbarLayoutDirty = true;
        }
    }
}
