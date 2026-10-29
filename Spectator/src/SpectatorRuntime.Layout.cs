using System;
using System.Collections.Generic;
using APIShared.Presentation;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static bool toolbarLayoutDirty = true, toolbarMoved, layoutCached;
        private static float toolbarDesiredX, toolbarDesiredY;
        private static FrameworkElement alignmentAnchor;
        private static Border toolbarHeader, leftWing, rightWing;
        private static Canvas toolbarWings;
        private static FrameworkElement toolbarGoods;
        private static FrameworkElement inlineStatus;
        private static readonly List<FrameworkElement> protectedControls = new List<FrameworkElement>();
        private static readonly List<FrameworkElement> infoRoots = new List<FrameworkElement>();
        private static readonly List<FrameworkElement> infoBackgrounds = new List<FrameworkElement>();
        private static readonly List<FrameworkElement> observedLayout = new List<FrameworkElement>();
        private static readonly List<ToolbarRect> observedBounds = new List<ToolbarRect>();
        private static readonly List<ToolbarRect> controlsBounds = new List<ToolbarRect>();
        private static readonly List<ToolbarRect> obstacles = new List<ToolbarRect>();
        private static readonly List<ToolbarRect> paintedPieces = new List<ToolbarRect>(3);

        private static void BindToolbarLayout()
        {
            toolbarHeader = humanScreen.FindName("SpectatorHeader") as Border;
            toolbarWings = humanScreen.FindName("SpectatorWings") as Canvas;
            leftWing = humanScreen.FindName("SpectatorLeftWing") as Border;
            rightWing = humanScreen.FindName("SpectatorRightWing") as Border;
            toolbarGoods = humanScreen.FindName("SpectatorGoodsGrid") as FrameworkElement;
            inlineStatus = humanScreen.FindName("SpectatorInlineStatus") as FrameworkElement;
            if (toolbarHeader == null || toolbarWings == null || leftWing == null || rightWing == null || toolbarGoods == null || inlineStatus == null)
                throw new MissingMemberException("Spectator header/wings missing.");
            ObserveLayout(toolbarHeader);
            toolbarLayoutDirty = true;
        }

        private static void ObserveLayout(FrameworkElement element)
        {
            if (element == null || observedLayout.Contains(element)) return;
            observedLayout.Add(element);
            observedBounds.Add(default);
            element.SizeChanged += OnToolbarLayoutSize;
            element.IsVisibleChanged += OnToolbarLayoutVisibility;
            element.IsEnabledChanged += OnToolbarLayoutVisibility;
        }
        private static void OnToolbarLayoutSize(object sender, SizeChangedEventArgs args) => toolbarLayoutDirty = true;
        private static void OnToolbarLayoutVisibility(object sender, DependencyPropertyChangedEventArgs args) => toolbarLayoutDirty = true;

        private static void ObserveCachedBounds()
        {
            // RenderTransform and bound Margin changes need not raise SizeChanged. Comparing this
            // bounded set of cached anchors also catches those moves without a visual-tree scan.
            for (int i = 0; i < observedLayout.Count; i++)
            {
                var bounds = CanvasBounds(observedLayout[i]);
                var previous = observedBounds[i];
                if (bounds.X == previous.X && bounds.Y == previous.Y &&
                    bounds.Width == previous.Width && bounds.Height == previous.Height) continue;
                observedBounds[i] = bounds;
                toolbarLayoutDirty = true;
            }
        }

        // One tree pass per loaded HUD. During rendering only the cached elements are measured.
        private static void CacheVanillaLayout(DependencyObject root)
        {
            if (root is FrameworkElement element)
            {
                if ((element is RadioButton && element.Name != null && element.Name.StartsWith("RadioButton", StringComparison.Ordinal)) ||
                    element.Name == "OST_Date" || element.Name == "ButtonAllies" ||
                    element.Name == "ButtonMerit" || element.Name == "SideTabs")
                {
                    protectedControls.Add(element);
                    ObserveLayout(element);
                }
                // The compact bar remains useful while inspecting a foreign HUD, but it must
                // move outside that surface rather than cover its orders, goods or chat controls.
                string type = element.GetType().Name;
                if (type == "HUD_AlliesPanel" || type == "HUD_MeritPanel" ||
                    type == "HUD_Troops" || type == "HUD_Buildings" || type == "HUD_MPChat")
                { protectedControls.Add(element); ObserveLayout(element); }
                if (element is TextBlock text)
                {
                    string anchor = TooltipAnchor.GetAnchor(text);
                    if (text.Name == "GameHoverText" || anchor == "BuildingDetailed" ||
                        anchor == "BuildingDescription" || anchor == "Recruitment" || anchor == "SiegeConstruction")
                    {
                        // APIShared inserts rows into this same StackPanel, including the -250 transform.
                        // Traverse only these small hosts when geometry changes, never the full view per frame.
                        var host = VisualTreeHelper.GetParent(text) as FrameworkElement ?? text;
                        if (!infoRoots.Contains(host)) { infoRoots.Add(host); ObserveLayout(host); }
                        if ((anchor == "BuildingDetailed" || anchor == "BuildingDescription") &&
                            VisualTreeHelper.GetParent(host) is Grid background &&
                            !infoBackgrounds.Contains(background))
                        { infoBackgrounds.Add(background); ObserveLayout(background); }
                        ObserveLayout(text);
                    }
                }
            }
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++) CacheVanillaLayout(VisualTreeHelper.GetChild(root, i));
        }

        private static ToolbarRect CanvasBounds(FrameworkElement element)
        {
            if (element == null || !element.IsVisible || element.ActualWidth <= 0 || element.ActualHeight <= 0) return default;
            // Translate all corners: the canvas and original HUD can have different effective scales.
            Point a = element.TranslatePoint(new Point(0, 0), humanCanvas);
            Point b = element.TranslatePoint(new Point(element.ActualWidth, element.ActualHeight), humanCanvas);
            return new ToolbarRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
        }

        private static void AddInfoBounds(DependencyObject root)
        {
            if (root is FrameworkElement element && !element.IsVisible) return;
            // Children carry RenderTransforms that are intentionally outside their parent's layout box.
            if (root is TextBlock text && !string.IsNullOrWhiteSpace(text.Text))
            {
                var bounds = CanvasBounds(text);
                if (!bounds.Empty) obstacles.Add(bounds);
            }
            // Cost TextBlocks may consist entirely of Runs, leaving Text empty.
            else if (root is TextBlock rich && rich.Inlines.Count > 0)
            {
                var bounds = CanvasBounds(rich);
                if (!bounds.Empty) obstacles.Add(bounds);
            }
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++) AddInfoBounds(VisualTreeHelper.GetChild(root, i));
        }

        private static void PositionToolbar()
        {
            if (humanPanel == null || !humanPanel.IsVisible || humanCanvas == null ||
                humanCanvas.ActualWidth <= 0 || humanCanvas.ActualHeight <= 0) return;
            if (!layoutCached)
            {
                if (humanBoundHud?.IsLoaded != true || humanScreen?.IsLoaded != true) return;
                // Other HUDs have an AlignmentGrid in their own namescope as well.
                alignmentAnchor = humanBoundHud.FindName("AlignmentGrid") as FrameworkElement;
                ObserveLayout(alignmentAnchor);
                CacheVanillaLayout(humanScreen);
                layoutCached = true;
                toolbarLayoutDirty = true;
            }
            ObserveCachedBounds();
            if (!toolbarLayoutDirty) return;
            toolbarLayoutDirty = false;
            float viewportWidth = humanCanvas.ActualWidth, viewportHeight = humanCanvas.ActualHeight;
            var alignment = CanvasBounds(alignmentAnchor);
            if (alignment.Empty) alignment = new ToolbarRect(Math.Max(0, viewportWidth - 914), viewportHeight - 166, 914, 166);
            var dock = ToolbarLayout.Dock(alignment);
            bool details = humanDetails.Visibility == Visibility.Visible;
            int count = 0;
            for (int i = 1; i <= 8; i++) if (humanOwnerButtons[i].Visibility == Visibility.Visible) count++;
            float width = Math.Min(viewportWidth, details ? dock.Width : 40 + count * 26 + (humanPlacementMode ? 148 : 0));
            if (Math.Abs(humanPanel.Width - width) > 0.1f)
            {
                humanPanel.Width = width;
                // Let the attached Noesis view complete layout before measuring the wrapping header.
                toolbarLayoutDirty = true;
                return;
            }
            float headerHeight = Math.Max(30, toolbarHeader.ActualHeight);
            controlsBounds.Clear(); obstacles.Clear(); paintedPieces.Clear();
            foreach (var item in protectedControls)
            {
                if (item is Button button && !button.IsEnabled) continue;
                var bounds = CanvasBounds(item);
                if (!bounds.Empty) { controlsBounds.Add(bounds); obstacles.Add(bounds); }
            }
            foreach (var host in infoRoots) AddInfoBounds(host);
            foreach (var background in infoBackgrounds)
            {
                var bounds = CanvasBounds(background);
                if (!bounds.Empty) obstacles.Add(bounds);
            }
            float height = headerHeight;
            if (details)
            {
                toolbarGoods.Width = Math.Max(1, width - 10);
                float bandTop = ToolbarLayout.ContentBottom(dock, controlsBounds) + ToolbarLayout.Gap;
                var band = new ToolbarRect(dock.X, bandTop, width, 26);
                ToolbarLayout.Wings(band, controlsBounds, out var left, out var right);
                // Never truncate the input/send controls into a notch. With a short left interval,
                // keep the complete footer above the controls and give the content a scrollbar.
                if (left.Width < 230)
                {
                    bandTop = ToolbarLayout.ContentBottom(dock, controlsBounds) - 26;
                    band = new ToolbarRect(dock.X, bandTop, width, 26);
                    ToolbarLayout.Wings(band, controlsBounds, out left, out right);
                }
                float bodyHeight = Math.Max(12, bandTop - dock.Y - headerHeight);
                bodyHeight = Math.Min(bodyHeight, Math.Max(12, viewportHeight - headerHeight - 26));
                humanDetails.Height = bodyHeight;
                detailsScroll.MaxHeight = Math.Max(1, bodyHeight - 2);
                height = headerHeight + bodyHeight + 26;
                toolbarWings.Width = width;
                PlaceWing(leftWing, left, dock.X);
                PlaceWing(rightWing, right, dock.X);
                inlineStatus.Visibility = right.Empty && toolsModel.StatusVisibility == Visibility.Visible
                    ? Visibility.Visible : Visibility.Collapsed;
                paintedPieces.Add(new ToolbarRect(0, 0, width, headerHeight + bodyHeight));
                if (!left.Empty) paintedPieces.Add(new ToolbarRect(left.X - dock.X, headerHeight + bodyHeight, left.Width, 26));
                if (!right.Empty) paintedPieces.Add(new ToolbarRect(right.X - dock.X, headerHeight + bodyHeight, right.Width, 26));
            }
            else paintedPieces.Add(new ToolbarRect(0, 0, width, headerHeight));
            float desiredX = toolbarMoved ? toolbarDesiredX : dock.X;
            float desiredY = toolbarMoved ? toolbarDesiredY : details ? dock.Y : dock.Y - headerHeight - ToolbarLayout.Gap;
            if (ToolbarLayout.TryPlace(desiredX, desiredY, width, height, viewportWidth, viewportHeight,
                paintedPieces, obstacles, out var position))
            {
                Canvas.SetLeft(humanPanel, position.X);
                Canvas.SetTop(humanPanel, position.Y);
            }
            // Desired coordinates are never overwritten by automatic tooltip avoidance.
        }

        private static void PlaceWing(Border wing, ToolbarRect bounds, float origin)
        {
            wing.Visibility = bounds.Empty ? Visibility.Collapsed : Visibility.Visible;
            wing.Width = bounds.Width;
            Canvas.SetLeft(wing, bounds.X - origin);
        }

        private static void DetachToolbarLayout()
        {
            foreach (var item in observedLayout)
            {
                item.SizeChanged -= OnToolbarLayoutSize;
                item.IsVisibleChanged -= OnToolbarLayoutVisibility;
                item.IsEnabledChanged -= OnToolbarLayoutVisibility;
            }
            observedLayout.Clear(); observedBounds.Clear(); protectedControls.Clear(); infoRoots.Clear();
            infoBackgrounds.Clear();
            controlsBounds.Clear(); obstacles.Clear(); paintedPieces.Clear();
            toolbarHeader = leftWing = rightWing = null;
            alignmentAnchor = null; toolbarWings = null;
            toolbarGoods = null;
            inlineStatus = null;
            layoutCached = false; toolbarLayoutDirty = true;
        }
    }
}
