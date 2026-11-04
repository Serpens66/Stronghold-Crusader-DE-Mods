using System;
using System.Collections.Generic;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static Canvas paperSurface;
        private static readonly List<PaperPatch> paperPatches = new List<PaperPatch>();
        private static readonly List<ToolbarRect> toolbarPaperPieces = new List<ToolbarRect>(8);
        private static readonly List<ToolbarPaperRegion> paperRegions = new List<ToolbarPaperRegion>(8);
        private static Brush paperRightFade, paperBottomFade, paperCornerFade;
        private static readonly Brush paperBase = new SolidColorBrush(Color.FromRgb(235, 204, 150));
        // Same layered colours as the released BFQoL bar. Geometry belongs to Spectator.
        private static readonly Brush classicOuter = new SolidColorBrush(Color.FromArgb(118, 12, 9, 7));
        private static readonly Brush classicInner = new SolidColorBrush(Color.FromArgb(84, 10, 8, 7));
        private static readonly Brush classicRed = new SolidColorBrush(Color.FromArgb(204, 123, 23, 20));
        private static readonly Brush classicBlack = new SolidColorBrush(Color.FromArgb(208, 0, 0, 0));

        private sealed class PaperPatch
        {
            internal readonly Border Surface, Inner, Core;
            internal readonly Canvas TextureHost = new Canvas();
            internal readonly Border Texture = new Border { IsHitTestVisible = false };
            internal PaperPatch()
            {
                TextureHost.Children.Add(Texture);
                Core = new Border { Child = TextureHost };
                Inner = new Border { Child = Core };
                Surface = new Border { Child = Inner, IsHitTestVisible = false, ClipToBounds = true };
            }
        }

        private static void BindToolbarPaper()
        {
            paperSurface = humanScreen.FindName("SpectatorPaperSurface") as Canvas;
            if (paperSurface == null)
                throw new MissingMemberException("Spectator parchment surface missing.");
            if (paperRightFade == null)
            {
                paperRightFade = CreatePaperFade(ToolbarPaperFade.Right);
                paperBottomFade = CreatePaperFade(ToolbarPaperFade.Bottom);
                paperCornerFade = CreatePaperFade(ToolbarPaperFade.Corner);
            }
        }

        private static Brush CreatePaperFade(ToolbarPaperFade fade)
        {
            ToolbarLayout.PaperFadeGradient(fade, out float x, out float y, out float opaqueUntil);
            var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(x, y),
                MappingMode = BrushMappingMode.RelativeToBoundingBox };
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = 0 });
            if (opaqueUntil > 0)
                brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(255, 255, 255, 255), Offset = opaqueUntil });
            brush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0, 255, 255, 255), Offset = 1 });
            return brush;
        }

        private static void RenderToolbarPaper(float width, float height, bool floating)
        {
            paperSurface.Visibility = toolbarPaperPieces.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            paperSurface.Width = width; paperSurface.Height = height;
            bool classic = toolsModel.ClassicTheme;
            ToolbarLayout.PaperRegions(toolbarPaperPieces, !floating && !classic, paperRegions);
            // Every clipped patch samples the same cropped image in toolbar coordinates.
            // Restarting/stretching the brush per patch creates a visible footer seam.
            while (paperPatches.Count < paperRegions.Count)
            {
                var patch = new PaperPatch(); paperPatches.Add(patch); paperSurface.Children.Add(patch.Surface);
            }
            for (int i = 0; i < paperPatches.Count; i++)
            {
                var patch = paperPatches[i];
                patch.Surface.Visibility = i < paperRegions.Count ? Visibility.Visible : Visibility.Collapsed;
                if (i >= paperRegions.Count) continue;
                var area = paperRegions[i].Bounds;
                Place(patch.Surface, area);
                // Internal split edges must meet without rounded clipping gaps.
                patch.Surface.CornerRadius = new CornerRadius(floating ? 4 : 0);
                patch.Surface.Clip = new RectangleGeometry(new Rect(0, 0, area.Width, area.Height),
                    floating ? 4 : 0, floating ? 4 : 0);
                switch (paperRegions[i].Fade)
                {
                    case ToolbarPaperFade.Right: patch.Surface.OpacityMask = paperRightFade; break;
                    case ToolbarPaperFade.Bottom: patch.Surface.OpacityMask = paperBottomFade; break;
                    case ToolbarPaperFade.Corner: patch.Surface.OpacityMask = paperCornerFade; break;
                    default: patch.Surface.OpacityMask = null; break;
                }
                patch.Surface.Background = classic ? classicOuter : paperBase;
                patch.Surface.BorderBrush = classic ? classicBlack : transparentToolbarBrush;
                patch.Surface.BorderThickness = new Thickness(classic ? 2 : 0);
                patch.Inner.Background = classic ? classicInner : transparentToolbarBrush;
                patch.Inner.BorderBrush = classic ? classicRed : transparentToolbarBrush;
                patch.Inner.BorderThickness = new Thickness(classic ? 2 : 0);
                patch.Core.Background = classic ? classicInner : transparentToolbarBrush;
                patch.Core.BorderBrush = classic ? classicBlack : transparentToolbarBrush;
                patch.Core.BorderThickness = new Thickness(classic ? 1 : 0);
                patch.TextureHost.Visibility = classic ? Visibility.Collapsed : Visibility.Visible;
                if (!classic)
                {
                    patch.Texture.Background = rosterPaper;
                    Place(patch.Texture, ToolbarLayout.PaperTexture(area, width, height));
                }
            }
        }

        private static void DetachToolbarPaper()
        {
            if (paperSurface != null) { paperSurface.Visibility = Visibility.Collapsed; paperSurface.Children.Clear(); }
            paperSurface = null;
            paperPatches.Clear(); toolbarPaperPieces.Clear(); paperRegions.Clear();
        }
    }
}
