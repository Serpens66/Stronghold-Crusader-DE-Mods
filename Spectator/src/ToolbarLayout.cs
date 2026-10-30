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

    internal readonly struct ToolbarPose
    {
        internal readonly ToolbarRect Roster, Tools, Control, Bounds;
        internal ToolbarPose(ToolbarRect roster, ToolbarRect tools, ToolbarRect control)
        {
            Roster = roster; Tools = tools; Control = control;
            float x = Math.Min(roster.X, control.X), y = Math.Min(roster.Y, control.Y);
            float right = Math.Max(roster.Right, control.Right), bottom = Math.Max(roster.Bottom, control.Bottom);
            if (!tools.Empty) { x = Math.Min(x, tools.X); y = Math.Min(y, tools.Y); right = Math.Max(right, tools.Right); bottom = Math.Max(bottom, tools.Bottom); }
            Bounds = new ToolbarRect(x, y, right - x, bottom - y);
        }
    }

    internal static class ToolbarLayout
    {
        internal const float Gap = 4;
        internal const float Portrait = 36, TargetPortrait = 28, Cell = 38, RosterWidth = 80;
        internal const float Header = 22, Body = 66, Footer = 26;
        internal const float ControlWidth = 64, ControlHeight = 22;
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

        // Only permanent functional HUD surfaces are supplied here, never hover text.
        // Footer wings have their own holes; the solid roster and tool body must stay clear.
        internal static bool TryAvoidControls(float x, float y, float width, float height,
            float viewportWidth, float viewportHeight, IList<ToolbarRect> solids,
            IList<ToolbarRect> controls, float gap, out ToolbarRect position)
        {
            position = Clamp(x, y, width, height, viewportWidth, viewportHeight);
            if (Clear(position, viewportWidth, viewportHeight, solids, controls)) return true;
            float wantedX = position.X, wantedY = position.Y;
            var xs = new List<float> { wantedX, 0, viewportWidth - width };
            var ys = new List<float> { wantedY, 0, viewportHeight - height };
            foreach (var control in controls)
                foreach (var solid in solids)
                {
                    if (control.Empty || solid.Empty) continue;
                    xs.Add(control.X - solid.Right - gap); xs.Add(control.Right - solid.X + gap);
                    ys.Add(control.Y - solid.Bottom - gap); ys.Add(control.Bottom - solid.Y + gap);
                }
            double best = double.MaxValue;
            foreach (float cx in xs)
                foreach (float cy in ys)
                {
                    double dx = cx - wantedX, dy = cy - wantedY, distance = dx * dx + dy * dy;
                    if (distance >= best) continue;
                    var candidate = new ToolbarRect(cx, cy, width, height);
                    if (!Clear(candidate, viewportWidth, viewportHeight, solids, controls)) continue;
                    position = candidate; best = distance;
                }
            return best != double.MaxValue;
        }

        private static bool Clear(ToolbarRect position, float width, float height,
            IList<ToolbarRect> solids, IList<ToolbarRect> controls)
        {
            if (position.X < 0 || position.Y < 0 || position.Right > width || position.Bottom > height) return false;
            foreach (var solid in solids)
            {
                var area = new ToolbarRect(position.X + solid.X, position.Y + solid.Y, solid.Width, solid.Height);
                foreach (var control in controls) if (area.Intersects(control)) return false;
            }
            return true;
        }

        internal static ToolbarRect OwnerCell(int ordinal)
        {
            if (ordinal < 0 || ordinal >= 8) throw new ArgumentOutOfRangeException(nameof(ordinal));
            return new ToolbarRect(4 + ordinal % 2 * Cell, ordinal / 2 * Cell, Portrait, Portrait);
        }

        internal static float RosterHeight(int count) => Math.Max(1, (Math.Max(0, Math.Min(8, count)) + 1) / 2) * Cell - 2;

        internal static ToolbarPose Arrange(ToolbarRect alignment, ToolbarRect frame, int count, bool details,
            bool floating = false, ToolbarRect dockControl = default)
        {
            float scale = alignment.Width / 914;
            var roster = new ToolbarRect(frame.X - (2 + RosterWidth) * scale,
                alignment.Bottom - RosterHeight(count) * scale, RosterWidth * scale, RosterHeight(count) * scale);
            var tools = details ? Dock(alignment) : default;
            var control = floating ? new ToolbarRect(roster.Right + 2 * scale,
                roster.Bottom - ControlHeight * scale, ControlWidth * scale, ControlHeight * scale) :
                dockControl.Empty ? new ToolbarRect(alignment.X + (553 - 2 - ControlWidth) * scale,
                    alignment.Bottom - (ControlHeight + 2) * scale, ControlWidth * scale, ControlHeight * scale) : dockControl;
            return new ToolbarPose(roster, tools, control);
        }

        // Normalize the measured text, not its line box. Both contour layers use this offset.
        internal static void NumberOffset(float height, float inkX, float inkBottom, out float x, out float y)
        { x = 2 - inkX; y = height - 2 - inkBottom; }

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
