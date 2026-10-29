using System;
using System.Collections.Generic;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static bool toolbarLayoutDirty = true, toolbarMoved, layoutCached;
        private static float toolbarDesiredX, toolbarDesiredY;
        private static FrameworkElement alignmentAnchor, toolbarGoods;
        private static Border toolbarHeader, leftWing, rightWing, snapFrame, paperSurface;
        private static Canvas toolbarWings;
        private static readonly List<FrameworkElement> protectedControls = new List<FrameworkElement>();
        private static readonly List<FrameworkElement> observedLayout = new List<FrameworkElement>();
        private static readonly List<ToolbarRect> observedBounds = new List<ToolbarRect>();
        private static readonly List<ToolbarRect> controlsBounds = new List<ToolbarRect>();
        private static readonly List<ToolbarRect> paintedPieces = new List<ToolbarRect>(3);
        private static ToolbarRect toolbarDock;
        private static FrameworkElement placementRaster, placementRasterHost;
        private static ToolbarProperty placementRasterTransform;

        private static void BindToolbarLayout()
        {
            toolbarHeader = humanScreen.FindName("SpectatorHeader") as Border;
            toolbarWings = humanScreen.FindName("SpectatorWings") as Canvas;
            leftWing = humanScreen.FindName("SpectatorLeftWing") as Border;
            rightWing = humanScreen.FindName("SpectatorRightWing") as Border;
            toolbarGoods = humanScreen.FindName("SpectatorGoodsGrid") as FrameworkElement;
            snapFrame = humanScreen.FindName("SpectatorSnapFrame") as Border;
            paperSurface = humanScreen.FindName("SpectatorPaperSurface") as Border;
            if (toolbarHeader == null || toolbarWings == null || leftWing == null || rightWing == null ||
                toolbarGoods == null || snapFrame == null || paperSurface == null)
                throw new MissingMemberException("Spectator dock parts missing.");
            ObserveLayout(toolbarHeader);
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
            // Translated HUD anchors do not necessarily raise SizeChanged. No per-frame tree scan.
            for (int i = 0; i < observedLayout.Count; i++)
            {
                var bounds = CanvasBounds(observedLayout[i]);
                var previous = observedBounds[i];
                if (bounds.X == previous.X && bounds.Y == previous.Y &&
                    bounds.Width == previous.Width && bounds.Height == previous.Height) continue;
                observedBounds[i] = bounds; toolbarLayoutDirty = true;
            }
        }

        // Only permanent functional HUD controls determine footer holes. Hover surfaces never
        // participate: opening key, crown or building information cannot move this toolbar.
        private static void CacheVanillaLayout(DependencyObject root)
        {
            if (root is FrameworkElement element &&
                ((element is RadioButton && element.Name != null &&
                    (element.Name.StartsWith("RadioButton", StringComparison.Ordinal) ||
                     element.Name.StartsWith("TabBuild", StringComparison.Ordinal))) ||
                 element.Name == "OST_Date" || element.Name == "ButtonAllies" ||
                 element.Name == "ButtonMerit" || element.Name == "SideTabs"))
            { protectedControls.Add(element); ObserveLayout(element); }
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

        private static void PositionToolbar()
        {
            if (humanPanel == null || !humanPanel.IsVisible || humanCanvas == null ||
                humanCanvas.ActualWidth <= 0 || humanCanvas.ActualHeight <= 0)
            { RestorePlacementRaster(); return; }
            if (!layoutCached)
            {
                if (humanBoundHud?.IsLoaded != true || humanScreen?.IsLoaded != true) return;
                alignmentAnchor = humanBoundHud.FindName("AlignmentGrid") as FrameworkElement;
                placementRaster = humanBoundHud.FindName("BuildMenuGrid") as FrameworkElement;
                placementRasterHost = placementRaster == null ? null : VisualTreeHelper.GetParent(placementRaster) as FrameworkElement;
                ObserveLayout(alignmentAnchor);
                CacheVanillaLayout(humanBoundHud);
                layoutCached = true; toolbarLayoutDirty = true;
            }
            ObserveCachedBounds();
            if (!humanPlacementMode || toolbarMoved) RestorePlacementRaster();
            if (!toolbarLayoutDirty) return;
            toolbarLayoutDirty = false;
            float vw = humanCanvas.ActualWidth, vh = humanCanvas.ActualHeight;
            var alignment = CanvasBounds(alignmentAnchor);
            if (alignment.Empty) alignment = new ToolbarRect(Math.Max(0, vw - 914), vh - 166, 914, 166);
            var dock = ToolbarLayout.Dock(alignment);
            bool details = humanDetails.Visibility == Visibility.Visible;
            int count = 0;
            for (int i = 1; i <= 8; i++) if (humanOwnerButtons[i].Visibility == Visibility.Visible) count++;
            float width = details ? 496 : 36 + count * 30 + (humanPlacementMode ? 144 : 0);
            float height = details ? ToolbarLayout.Header + ToolbarLayout.Body + ToolbarLayout.Footer : ToolbarLayout.Header;
            // A single uniformly scaled surface keeps every control reachable on small viewports.
            float surfaceScale = Math.Min(alignment.Width / 914, Math.Min(vw / width, vh / height));
            humanPanel.Width = width;
            var position = ToolbarLayout.Clamp(toolbarMoved ? toolbarDesiredX : dock.X,
                toolbarMoved ? toolbarDesiredY : dock.Y, width * surfaceScale, height * surfaceScale, vw, vh);
            toolbarDock = ToolbarLayout.Clamp(dock.X, dock.Y, width * surfaceScale, height * surfaceScale, vw, vh);
            humanPanel.RenderTransform = new ScaleTransform(surfaceScale, surfaceScale);
            Canvas.SetLeft(humanPanel, position.X); Canvas.SetTop(humanPanel, position.Y);
            paperSurface.Visibility = toolbarMoved ? Visibility.Visible : Visibility.Collapsed;
            humanDetails.Height = ToolbarLayout.Body;
            toolbarGoods.Width = 297;
            controlsBounds.Clear(); paintedPieces.Clear();
            foreach (var item in protectedControls)
            {
                if (item is ButtonBase button && !button.IsEnabled) continue;
                var bounds = CanvasBounds(item);
                if (!bounds.Empty) controlsBounds.Add(bounds);
            }
            if (details)
            {
                var band = new ToolbarRect(position.X, position.Y + (ToolbarLayout.Header + ToolbarLayout.Body) * surfaceScale,
                    width * surfaceScale, ToolbarLayout.Footer * surfaceScale);
                ToolbarRect left, right;
                if (!toolbarMoved) ToolbarLayout.Wings(band, controlsBounds, out left, out right);
                else { left = new ToolbarRect(band.X, band.Y, band.Width * 0.58f, band.Height);
                    right = new ToolbarRect(left.Right, band.Y, band.Width - left.Width, band.Height); }
                // The checkbox goes in the wider free interval, independently of the date position.
                if (toolsModel.ActiveViewKey == "Orders" && left.Width < right.Width)
                { var swap = left; left = right; right = swap; }
                if (toolsModel.ActiveViewKey == "Goods")
                { right = left.Width >= right.Width ? left : right; left = default; }
                left = LocalWing(left, position, surfaceScale); right = LocalWing(right, position, surfaceScale);
                PlaceWing(leftWing, left); PlaceWing(rightWing, right);
                paintedPieces.Add(new ToolbarRect(0, 0, width, ToolbarLayout.Header + ToolbarLayout.Body));
                if (!left.Empty) paintedPieces.Add(left);
                if (!right.Empty) paintedPieces.Add(right);
            }
            else paintedPieces.Add(new ToolbarRect(0, 0, width, ToolbarLayout.Header));
            snapFrame.Width = width * surfaceScale; snapFrame.Height = height * surfaceScale;
            Canvas.SetLeft(snapFrame, toolbarDock.X); Canvas.SetTop(snapFrame, toolbarDock.Y);
            snapFrame.Visibility = humanDragging ? Visibility.Visible : Visibility.Collapsed;
            if (humanPlacementMode && !toolbarMoved)
                FitPlacementRaster(new ToolbarRect(position.X, position.Y + (ToolbarLayout.Header + 2) * surfaceScale,
                    width * surfaceScale, (ToolbarLayout.Body - 4) * surfaceScale));
        }

        private static ToolbarRect LocalWing(ToolbarRect bounds, ToolbarRect position, float scale) => bounds.Empty
            ? default : new ToolbarRect((bounds.X - position.X) / scale, ToolbarLayout.Header + ToolbarLayout.Body,
                bounds.Width / scale, ToolbarLayout.Footer);

        private static void PlaceWing(Border wing, ToolbarRect bounds)
        {
            wing.Visibility = bounds.Empty ? Visibility.Collapsed : Visibility.Visible;
            wing.Width = bounds.Width; Canvas.SetLeft(wing, bounds.X);
        }

        private static void SnapToolbarDrag(float x, float y)
        {
            Point origin = humanCanvas.PointToScreen(new Point(0, 0));
            Point unit = humanCanvas.PointToScreen(new Point(100, 100));
            bool attached = ToolbarLayout.Snap(x, y, toolbarDock,
                (unit.X - origin.X) / 100, (unit.Y - origin.Y) / 100, !toolbarMoved);
            toolbarMoved = !attached;
            toolbarDesiredX = x; toolbarDesiredY = y;
            toolbarLayoutDirty = true;
        }

        private static void FitPlacementRaster(ToolbarRect target)
        {
            if (placementRasterHost == null || placementRaster == null || humanBoundHud == null) return;
            // Restore before measuring, so neither our previous scale nor a page change feeds back
            // into the next fit. Never modify BuildMenuGrid's animated child TranslateTransform.
            if (placementRasterTransform == null)
                placementRasterTransform = new ToolbarProperty(placementRasterHost, UIElement.RenderTransformProperty);
            placementRasterTransform.Restore();
            var originalTransform = placementRasterHost.RenderTransform;
            var buttons = (Button[])buildButtonsField.GetValue(humanBoundHud);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            float swish = (placementRaster.RenderTransform as TranslateTransform)?.X ?? 16;
            foreach (var button in buttons)
            {
                if (button == null || button.Visibility != Visibility.Visible || button.ActualWidth <= 0 || button.ActualHeight <= 0) continue;
                Point a = button.TranslatePoint(new Point(0, 0), placementRasterHost);
                Point b = button.TranslatePoint(new Point(button.ActualWidth, button.ActualHeight), placementRasterHost);
                // Both Vanilla Swish storyboards settle at X=16. Fit that resting raster while
                // leaving its current animation offset intact (and scaled together with input).
                minX = Math.Min(minX, a.X - swish + 16); maxX = Math.Max(maxX, b.X - swish + 16);
                minY = Math.Min(minY, a.Y); maxY = Math.Max(maxY, b.Y);
            }
            if (minX == float.MaxValue) { toolbarLayoutDirty = true; return; }
            Point start = humanCanvas.TranslatePoint(new Point(target.X, target.Y), placementRasterHost);
            Point end = humanCanvas.TranslatePoint(new Point(target.Right, target.Bottom), placementRasterHost);
            var available = new ToolbarRect(start.X, start.Y, end.X - start.X, end.Y - start.Y);
            var source = new ToolbarRect(minX, minY, maxX - minX, maxY - minY);
            float scale = ToolbarLayout.RasterScale(source, available);
            var fitted = ToolbarLayout.FitRaster(source, available);
            var transform = new TransformGroup();
            transform.Children.Add(new ScaleTransform(scale, scale));
            transform.Children.Add(new TranslateTransform(fitted.X - minX * scale, fitted.Y - minY * scale));
            if (originalTransform != null) transform.Children.Add(originalTransform);
            placementRasterHost.RenderTransform = transform;
        }

        private static void RestorePlacementRaster()
        {
            placementRasterTransform?.Restore(); placementRasterTransform = null;
        }

        private static void DetachToolbarLayout()
        {
            RestorePlacementRaster();
            if (snapFrame != null) snapFrame.Visibility = Visibility.Collapsed;
            foreach (var item in observedLayout)
            {
                item.SizeChanged -= OnToolbarLayoutSize;
                item.IsVisibleChanged -= OnToolbarLayoutVisibility;
                item.IsEnabledChanged -= OnToolbarLayoutVisibility;
            }
            observedLayout.Clear(); observedBounds.Clear(); protectedControls.Clear(); controlsBounds.Clear(); paintedPieces.Clear();
            toolbarHeader = leftWing = rightWing = snapFrame = paperSurface = null;
            alignmentAnchor = placementRaster = placementRasterHost = toolbarGoods = null; toolbarWings = null;
            layoutCached = false; toolbarLayoutDirty = true;
        }
    }
}
