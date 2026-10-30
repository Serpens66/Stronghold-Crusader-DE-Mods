using System;
using System.Collections.Generic;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static Canvas paperSurface, paperTop, paperLower;
        private static Image paperTopImage, paperLowerImage;
        private static readonly List<ToolbarRect> toolbarPaperPieces = new List<ToolbarRect>(3);

        private static void BindToolbarPaper()
        {
            paperSurface = humanScreen.FindName("SpectatorPaperSurface") as Canvas;
            paperTop = humanScreen.FindName("SpectatorPaperTop") as Canvas;
            paperLower = humanScreen.FindName("SpectatorPaperLower") as Canvas;
            paperTopImage = humanScreen.FindName("SpectatorPaperTopImage") as Image;
            paperLowerImage = humanScreen.FindName("SpectatorPaperLowerImage") as Image;
            if (paperSurface == null || paperTop == null || paperLower == null || paperTopImage == null || paperLowerImage == null)
                throw new MissingMemberException("Continuous Spectator paper layers missing.");
        }

        private static Rect PaperRect(ToolbarRect area) => new Rect(area.X, area.Y, area.Width, area.Height);

        private static void RenderToolbarPaper(float width, float height, bool floating, ToolbarRect nativePaper,
            float end, Image original)
        {
            paperSurface.Visibility = toolbarPaperPieces.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (toolbarPaperPieces.Count == 0) return;
            paperSurface.Width = paperTop.Width = paperLower.Width = width;
            paperSurface.Height = paperTop.Height = paperLower.Height = height;
            var clip = new GeometryGroup();
            foreach (var piece in toolbarPaperPieces)
                clip.Children.Add(new RectangleGeometry(PaperRect(piece), floating ? 4 : 1.5f, floating ? 4 : 1.5f));
            paperSurface.Clip = clip;
            if (original?.Source != null)
            {
                paperTopImage.Source = paperLowerImage.Source = original.Source;
                paperTopImage.FlowDirection = paperLowerImage.FlowDirection = original.FlowDirection;
            }
            if (floating || nativePaper.Empty)
            {
                Place(paperTopImage, new ToolbarRect(0, 0, width, height));
                paperTop.Clip = null;
                paperLower.Visibility = Visibility.Collapsed;
                return;
            }
            // The top uses the exact moving Vanilla image coordinates. Only its bottom
            // shading band is extended; independent wings never restart the texture.
            var slices = new ToolbarPaperSlices(nativePaper, end);
            float join = Math.Max(0, Math.Min(height, slices.Join));
            paperTop.Clip = new RectangleGeometry(new Rect(0, 0, width, join));
            paperLower.Clip = new RectangleGeometry(new Rect(0, join, width, height - join));
            Place(paperTopImage, slices.TopImage);
            Place(paperLowerImage, slices.LowerImage);
            paperLower.Visibility = Visibility.Visible;
        }

        private static void DetachToolbarPaper()
        {
            if (paperSurface != null) paperSurface.Visibility = Visibility.Collapsed;
            paperSurface = paperTop = paperLower = null;
            paperTopImage = paperLowerImage = null;
            toolbarPaperPieces.Clear();
        }
    }
}
