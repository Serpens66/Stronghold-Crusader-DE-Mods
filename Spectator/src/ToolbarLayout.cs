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
        internal bool Intersects(ToolbarRect other) => !Empty && !other.Empty &&
            X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;
    }

    internal static class ToolbarLayout
    {
        internal const float Gap = 4;
        internal const float Portrait = 28, Header = 32, Body = 56, Footer = 26;
        // Vanilla's detailed info host ends 125 units above the viewport bottom
        // (compact: 130). Keep a two-unit gap below both, in every toolbar mode.
        internal static ToolbarRect Dock(ToolbarRect alignment)
        {
            float scale = alignment.Width / 914;
            return new ToolbarRect(alignment.X + 130 * scale, alignment.Y + 43 * scale,
                496 * scale, (Header + Body + Footer) * scale);
        }

        internal static ToolbarRect Clamp(float x, float y, float width, float height,
            float viewportWidth, float viewportHeight) => new ToolbarRect(
                Math.Max(0, Math.Min(Math.Max(0, viewportWidth - width), x)),
                Math.Max(0, Math.Min(Math.Max(0, viewportHeight - height), y)), width, height);

        internal static bool Snap(float x, float y, ToolbarRect dock, float scaleX, float scaleY, bool attached)
        {
            double dx = (x - dock.X) * Math.Abs(scaleX), dy = (y - dock.Y) * Math.Abs(scaleY);
            float radius = attached ? 14 : 10;
            return dx * dx + dy * dy <= radius * radius;
        }

        // Uniform fit preserves sprite aspect ratios and transforms input with the same matrix.
        internal static float RasterScale(ToolbarRect source, ToolbarRect target) => source.Empty || target.Empty
            ? 1 : Math.Min(1, Math.Min(target.Width / source.Width, target.Height / source.Height));

        internal static ToolbarRect FitRaster(ToolbarRect source, ToolbarRect target)
        {
            float scale = RasterScale(source, target);
            return new ToolbarRect(target.X + (target.Width - source.Width * scale) / 2,
                target.Y + (target.Height - source.Height * scale) / 2,
                source.Width * scale, source.Height * scale);
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

    }
}
