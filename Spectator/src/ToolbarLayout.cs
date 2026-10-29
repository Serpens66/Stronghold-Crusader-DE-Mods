using System;
using System.Collections.Generic;

namespace Spectator
{
    // Canvas coordinates, not physical pixels. Also used by the production-geometry tests.
    internal readonly struct ToolbarRect
    {
        internal readonly float X, Y, Width, Height;
        internal ToolbarRect(float x, float y, float width, float height)
        { X = x; Y = y; Width = Math.Max(0, width); Height = Math.Max(0, height); }
        internal float Right => X + Width;
        internal float Bottom => Y + Height;
        internal bool Empty => Width <= 0 || Height <= 0;
        internal ToolbarRect Move(float x, float y) => new ToolbarRect(x, y, Width, Height);
        internal bool Intersects(ToolbarRect other) => !Empty && !other.Empty &&
            X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
    }

    internal static class ToolbarLayout
    {
        internal const float Gap = 4;
        // The paper's existing usable centre, relative to Vanilla's AlignmentGrid.
        // Read-only placement, matching the previous additive dock. No Vanilla layout is written.
        internal static ToolbarRect Dock(ToolbarRect alignment) =>
            new ToolbarRect(alignment.X + 130, alignment.Y + 28, 496, 130);

        internal static float ContentBottom(ToolbarRect dock, IList<ToolbarRect> controls)
        {
            float bottom = dock.Bottom - 26;
            foreach (var item in controls)
                if (!item.Empty && item.X < dock.Right && item.Right > dock.X && item.Y > dock.Y)
                    bottom = Math.Min(bottom, item.Y - Gap);
            return bottom;
        }

        // Find the two largest usable footer intervals. The gaps have no visual or hit surface.
        internal static void Wings(ToolbarRect band, IList<ToolbarRect> controls,
            out ToolbarRect left, out ToolbarRect right)
        {
            left = right = default;
            float cursor = band.X;
            while (cursor < band.Right)
            {
                float start = band.Right, end = band.Right;
                foreach (var item in controls)
                {
                    if (item.Empty || item.Y >= band.Bottom || item.Bottom <= band.Y ||
                        item.Right + Gap <= cursor || item.X - Gap >= band.Right) continue;
                    if (item.X - Gap < start)
                    { start = Math.Max(cursor, item.X - Gap); end = Math.Max(cursor, item.Right + Gap); }
                }
                var free = new ToolbarRect(cursor, band.Y, Math.Max(0, start - cursor), band.Height);
                if (free.Width > left.Width) { right = left; left = free; }
                else if (free.Width > right.Width) right = free;
                if (start >= band.Right) break;
                cursor = Math.Min(band.Right, Math.Max(cursor + 1, end));
            }
            if (right.Width > 0 && left.X > right.X) { var swap = left; left = right; right = swap; }
        }

        // Test the actual painted pieces, not their enclosing rectangle: the date notch stays open.
        internal static bool Fits(float x, float y, float width, float height, float viewportWidth,
            float viewportHeight, IList<ToolbarRect> pieces, IList<ToolbarRect> obstacles)
        {
            if (x < 0 || y < 0 || x + width > viewportWidth || y + height > viewportHeight) return false;
            foreach (var piece in pieces)
                foreach (var obstacle in obstacles)
                    if (piece.Move(x + piece.X, y + piece.Y).Intersects(obstacle)) return false;
            return true;
        }

        internal static bool TryPlace(float desiredX, float desiredY, float width, float height,
            float viewportWidth, float viewportHeight, IList<ToolbarRect> pieces,
            IList<ToolbarRect> obstacles, out ToolbarRect result)
        {
            float x = Math.Max(0, Math.Min(Math.Max(0, viewportWidth - width), desiredX));
            float y = Math.Max(0, Math.Min(Math.Max(0, viewportHeight - height), desiredY));
            result = new ToolbarRect(x, y, width, height);
            if (Fits(x, y, width, height, viewportWidth, viewportHeight, pieces, obstacles)) return true;
            // Called only for changed layout. Candidate edges cover multi-obstacle corners as well.
            var xs = new List<float> { x, 0, viewportWidth - width };
            var ys = new List<float> { y, 0, viewportHeight - height };
            foreach (var obstacle in obstacles)
                foreach (var piece in pieces)
                {
                    xs.Add(obstacle.X - piece.Right - Gap); xs.Add(obstacle.Right - piece.X + Gap);
                    ys.Add(obstacle.Y - piece.Bottom - Gap); ys.Add(obstacle.Bottom - piece.Y + Gap);
                }
            double best = double.MaxValue;
            foreach (float cx in xs)
                foreach (float cy in ys)
                {
                    double score = (cx - x) * (cx - x) + (cy - y) * (cy - y);
                    if (score >= best || !Fits(cx, cy, width, height, viewportWidth, viewportHeight, pieces, obstacles)) continue;
                    result = new ToolbarRect(cx, cy, width, height); best = score;
                }
            return best != double.MaxValue;
        }
    }
}
