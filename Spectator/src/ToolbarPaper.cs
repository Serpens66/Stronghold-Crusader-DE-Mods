using System;
using System.Collections.Generic;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static Canvas paperSurface;
        private static ImageSource parchmentSource;
        private static readonly List<PaperPatch> paperPatches = new List<PaperPatch>();
        private static readonly List<ToolbarRect> toolbarPaperPieces = new List<ToolbarRect>(8);
        private static readonly Brush paperBase = new SolidColorBrush(Color.FromRgb(235, 204, 150));
        // Same layered colours as the released BFQoL bar. Geometry belongs to Spectator.
        private static readonly Brush classicOuter = new SolidColorBrush(Color.FromArgb(118, 12, 9, 7));
        private static readonly Brush classicInner = new SolidColorBrush(Color.FromArgb(84, 10, 8, 7));
        private static readonly Brush classicRed = new SolidColorBrush(Color.FromArgb(204, 123, 23, 20));
        private static readonly Brush classicBlack = new SolidColorBrush(Color.FromArgb(208, 0, 0, 0));

        private sealed class PaperPatch
        {
            internal readonly Border Surface, Inner, Core;
            internal readonly Canvas Top = new Canvas(), Lower = new Canvas();
            internal readonly Image TopImage = new Image { Stretch = Stretch.Fill }, LowerImage = new Image { Stretch = Stretch.Fill };
            internal PaperPatch()
            {
                var images = new Grid();
                Top.Children.Add(TopImage); Lower.Children.Add(LowerImage);
                images.Children.Add(Top); images.Children.Add(Lower);
                Core = new Border { Child = images };
                Inner = new Border { Child = Core };
                Surface = new Border { Child = Inner, IsHitTestVisible = false, ClipToBounds = true };
            }
        }

        private static void BindToolbarPaper()
        {
            paperSurface = humanScreen.FindName("SpectatorPaperSurface") as Canvas;
            parchmentSource = humanPanel.TryFindResource("UI-HUD 007") as ImageSource;
            if (paperSurface == null || parchmentSource == null)
                throw new MissingMemberException("Spectator parchment surface/resource missing.");
        }

        private static void RenderToolbarPaper(float width, float height, bool floating, ToolbarRect nativePaper,
            float end, Image original)
        {
            paperSurface.Visibility = toolbarPaperPieces.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            paperSurface.Width = width; paperSurface.Height = height;
            bool classic = toolsModel.ClassicTheme;
            // Every patch samples one common image transform. Clipping is local and never
            // depends on a shared composite geometry or a negative stacking layer.
            var full = new ToolbarRect(0, 0, width, height);
            var slices = new ToolbarPaperSlices(nativePaper, end);
            bool sliced = !floating && !nativePaper.Empty;
            var upper = sliced ? slices.TopImage : full;
            var lower = slices.LowerImage;
            float join = sliced ? Math.Max(0, Math.Min(height, slices.Join)) : height;
            while (paperPatches.Count < toolbarPaperPieces.Count)
            {
                var patch = new PaperPatch(); paperPatches.Add(patch); paperSurface.Children.Add(patch.Surface);
            }
            for (int i = 0; i < paperPatches.Count; i++)
            {
                var patch = paperPatches[i];
                patch.Surface.Visibility = i < toolbarPaperPieces.Count ? Visibility.Visible : Visibility.Collapsed;
                if (i >= toolbarPaperPieces.Count) continue;
                var area = toolbarPaperPieces[i];
                Place(patch.Surface, area);
                patch.Surface.CornerRadius = new CornerRadius(floating ? 4 : 1.5f);
                patch.Surface.Clip = new RectangleGeometry(new Rect(0, 0, area.Width, area.Height),
                    floating ? 4 : 1.5f, floating ? 4 : 1.5f);
                patch.Surface.Background = classic ? classicOuter : paperBase;
                patch.Surface.BorderBrush = classic ? classicBlack : transparentToolbarBrush;
                patch.Surface.BorderThickness = new Thickness(classic ? 2 : 0);
                patch.Inner.Background = classic ? classicInner : transparentToolbarBrush;
                patch.Inner.BorderBrush = classic ? classicRed : transparentToolbarBrush;
                patch.Inner.BorderThickness = new Thickness(classic ? 2 : 0);
                patch.Core.Background = classic ? classicInner : transparentToolbarBrush;
                patch.Core.BorderBrush = classic ? classicBlack : transparentToolbarBrush;
                patch.Core.BorderThickness = new Thickness(classic ? 1 : 0);
                patch.Top.Visibility = classic ? Visibility.Collapsed : Visibility.Visible;
                patch.Lower.Visibility = !classic && sliced ? Visibility.Visible : Visibility.Collapsed;
                if (classic) continue;
                patch.Top.Width = patch.Lower.Width = area.Width;
                patch.Top.Height = patch.Lower.Height = area.Height;
                patch.TopImage.Source = patch.LowerImage.Source = parchmentSource;
                patch.TopImage.FlowDirection = patch.LowerImage.FlowDirection = original?.FlowDirection ?? FlowDirection.LeftToRight;
                Place(patch.TopImage, new ToolbarRect(upper.X - area.X, upper.Y - area.Y, upper.Width, upper.Height));
                Place(patch.LowerImage, new ToolbarRect(lower.X - area.X, lower.Y - area.Y, lower.Width, lower.Height));
                float cut = Math.Max(0, Math.Min(area.Height, join - area.Y));
                patch.Top.Clip = new RectangleGeometry(new Rect(0, 0, area.Width, cut));
                patch.Lower.Clip = new RectangleGeometry(new Rect(0, cut, area.Width, area.Height - cut));
            }
        }

        private static void DetachToolbarPaper()
        {
            if (paperSurface != null) { paperSurface.Visibility = Visibility.Collapsed; paperSurface.Children.Clear(); }
            paperSurface = null; parchmentSource = null;
            paperPatches.Clear(); toolbarPaperPieces.Clear();
        }
    }
}
