using System;
using System.Collections.Generic;

namespace Spectator
{
    internal enum ToolbarPaperFade { None, Right, Bottom, Corner }
    internal readonly struct ToolbarPaperRegion
    {
        internal readonly ToolbarRect Bounds;
        internal readonly ToolbarPaperFade Fade;
        internal ToolbarPaperRegion(ToolbarRect bounds, ToolbarPaperFade fade = ToolbarPaperFade.None)
        { Bounds = bounds; Fade = fade; }
    }

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
        internal readonly ToolbarRect Roster, Tools, Control, Grip, Bounds;
        internal ToolbarPose(ToolbarRect roster, ToolbarRect tools, ToolbarRect control, ToolbarRect grip)
        {
            Roster = roster; Tools = tools; Control = control; Grip = grip;
            float x = Math.Min(roster.X, grip.X), y = Math.Min(roster.Y, grip.Y);
            float right = Math.Max(roster.Right, grip.Right), bottom = Math.Max(roster.Bottom, grip.Bottom);
            if (!control.Empty) { x = Math.Min(x, control.X); y = Math.Min(y, control.Y); right = Math.Max(right, control.Right); bottom = Math.Max(bottom, control.Bottom); }
            if (!tools.Empty) { x = Math.Min(x, tools.X); y = Math.Min(y, tools.Y); right = Math.Max(right, tools.Right); bottom = Math.Max(bottom, tools.Bottom); }
            Bounds = new ToolbarRect(x, y, right - x, bottom - y);
        }
    }

    internal static class ToolbarLayout
    {
        internal const float Gap = 4;
        internal const float Portrait = 36, TargetPortrait = 42, Cell = 38, RosterWidth = 80;
        internal const float Header = 22, Body = 66, Footer = 33;
        internal const float GoodsWidth = 308, OrderSummaryWidth = 352;
        internal const float ControlWidth = 60, ControlHeight = 22, WindowWidth = 496, FloatingHeader = 40, FloatingGrip = 8;
        internal const float ToggleWidth = 24, DesignWidth = 22;
        internal static float FloatingWidth(int count, bool placement, bool toolControlAvailable = true) =>
            12 + Math.Max(0, Math.Min(8, count) * Cell - 2) + 2 + DesignWidth + 2 +
            (toolControlAvailable ? (placement ? ControlWidth : ToggleWidth) + 2 : 0);
        internal static ToolbarRect FloatingGripRect => new ToolbarRect(2, 2, FloatingGrip, Portrait);
        internal static ToolbarRect DockDropTarget(ToolbarRect alignment, ToolbarRect frame)
        {
            // Always the complete eight-player target, even for a sparse/small roster.
            float scale = alignment.Width / 914;
            return new ToolbarRect(frame.X - (6 + RosterWidth) * scale,
                alignment.Bottom - RosterHeight(8) * scale, RosterWidth * scale, RosterHeight(8) * scale);
        }

        internal static ToolbarRect ReachableDockDropTarget(ToolbarRect target, float floatingHeight,
            float floatingScale, float viewportHeight, float screenScaleY)
        {
            if (target.Empty) return target;
            float centre = (FloatingGripRect.Y + FloatingGripRect.Height / 2) * floatingScale;
            float minimum = centre, maximum = Math.Max(0, viewportHeight - floatingHeight) + centre;
            // Translate a complete copy into the reachable range. Merely clamping its endpoints
            // leaves today's expanded row with the same nine-unit strip as the original target.
            float shift = Math.Min(0, maximum - target.Bottom);
            shift = Math.Max(shift, minimum - target.Y);
            float top = Math.Max(minimum, target.Y + shift);
            float bottom = Math.Min(maximum, target.Bottom + shift);
            if (maximum <= minimum)
            {
                float tolerance = 14 / Math.Max(.001f, Math.Abs(screenScaleY));
                top = minimum - tolerance;
                bottom = minimum + tolerance;
            }
            // Keep the original target for pointer travel beyond the clamped window.
            top = Math.Min(target.Y, top); bottom = Math.Max(target.Bottom, bottom);
            return new ToolbarRect(target.X, top, target.Width, bottom - top);
        }
        internal static ToolbarRect FloatingDesignRect(int count, bool placement, bool toolControlAvailable = true) =>
            new ToolbarRect(FloatingWidth(count, placement, toolControlAvailable) -
                (toolControlAvailable ? (placement ? ControlWidth : ToggleWidth) + 2 : 0) - 2 - DesignWidth,
                8, DesignWidth, ToggleWidth);
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

        internal static bool WithinDetachThreshold(float x, float y, ToolbarRect dock, float scaleX, float scaleY)
        {
            double dx = (x - dock.X) * Math.Abs(scaleX), dy = (y - dock.Y) * Math.Abs(scaleY);
            return dx * dx + dy * dy <= 14 * 14;
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

        internal static bool Position(bool floating, float x, float y, float width, float height,
            float viewportWidth, float viewportHeight, IList<ToolbarRect> solids,
            IList<ToolbarRect> controls, float gap, out ToolbarRect position)
        {
            if (floating) { position = Clamp(x, y, width, height, viewportWidth, viewportHeight); return true; }
            return TryAvoidControls(x, y, width, height, viewportWidth, viewportHeight, solids, controls, gap, out position);
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

        internal static ToolbarRect OwnerCell(int ordinal, bool floating = false)
        {
            if (ordinal < 0 || ordinal >= 8) throw new ArgumentOutOfRangeException(nameof(ordinal));
            return floating ? new ToolbarRect(12 + ordinal * Cell, 2, Portrait, Portrait) :
                new ToolbarRect(4 + ordinal % 2 * Cell, ordinal / 2 * Cell, Portrait, Portrait);
        }

        internal static float RosterHeight(int count) => Math.Max(1, (Math.Max(0, Math.Min(8, count)) + 1) / 2) * Cell - 2;

        internal static ToolbarPose Arrange(ToolbarRect alignment, ToolbarRect frame, int count, bool details,
            bool floating = false, bool placement = false, bool toolControlAvailable = true)
        {
            details &= toolControlAvailable;
            if (floating)
            {
                float barWidth = FloatingWidth(count, placement, toolControlAvailable), buttonWidth = placement ? ControlWidth : ToggleWidth;
                return new ToolbarPose(new ToolbarRect(0, 0, barWidth, FloatingHeader),
                    details ? new ToolbarRect(0, FloatingHeader, WindowWidth, Header + Body + Footer) : default,
                    toolControlAvailable ? new ToolbarRect(barWidth - buttonWidth - 2, 8, buttonWidth, ToggleWidth) : default, FloatingGripRect);
            }
            float scale = alignment.Width / 914;
            var roster = new ToolbarRect(frame.X - (6 + RosterWidth) * scale,
                alignment.Bottom - RosterHeight(count) * scale, RosterWidth * scale, RosterHeight(count) * scale);
            var tools = details ? Dock(alignment) : default;
            var control = !toolControlAvailable ? default : new ToolbarRect(
                roster.X + (RosterWidth - ControlWidth) * scale / 2,
                roster.Y - ControlHeight * scale, ControlWidth * scale, ControlHeight * scale);
            // The doubled grip uses the old blank margin plus six units of HUD gap.
            var grip = new ToolbarRect(roster.Right - 2 * scale, roster.Y, FloatingGrip * scale, roster.Height);
            return new ToolbarPose(roster, tools, control, grip);
        }

        // Normalize the measured text, not its line box. Both contour layers use this offset.
        internal static void NumberOffset(float height, float inkX, float inkBottom, out float x, out float y)
        { x = 2 - inkX; y = height - 2 - inkBottom; }

        // The root-sized texture is translated into each clipping patch, so a point
        // on a body/footer join samples the same texel from either side.
        internal static ToolbarRect PaperTexture(ToolbarRect patch, float rootWidth, float rootHeight) =>
            new ToolbarRect(-patch.X, -patch.Y, rootWidth, rootHeight);

        internal static void PaperFadeGradient(ToolbarPaperFade fade, out float endX, out float endY, out float opaqueUntil)
        {
            endX = fade == ToolbarPaperFade.Bottom ? 0 : 1;
            endY = fade == ToolbarPaperFade.Right ? 0 : 1;
            opaqueUntil = fade == ToolbarPaperFade.Corner ? .5f : 0;
        }

        // Split visual paper only. A matching diagonal corner keeps opacity continuous
        // between the footer's right feather and the body's exposed bottom feather.
        internal static void PaperRegions(IList<ToolbarRect> pieces, bool feather, List<ToolbarPaperRegion> result)
        {
            result.Clear();
            int bodyIndex = -1, footerIndex = -1;
            if (feather)
                for (int upperIndex = 0; upperIndex < pieces.Count && bodyIndex < 0; upperIndex++)
                    for (int lowerIndex = 0; lowerIndex < pieces.Count; lowerIndex++)
                    {
                        var upper = pieces[upperIndex]; var lower = pieces[lowerIndex];
                        if (upperIndex == lowerIndex || upper.Empty || lower.Empty ||
                            Math.Abs(upper.Bottom - lower.Y) > .01f || lower.X < upper.X ||
                            lower.Right >= upper.Right || lower.Right <= upper.X) continue;
                        bodyIndex = upperIndex; footerIndex = lowerIndex; break;
                    }
            for (int pieceIndex = 0; pieceIndex < pieces.Count; pieceIndex++)
            {
                var area = pieces[pieceIndex];
                if (area.Empty) continue;
                if (bodyIndex < 0) { result.Add(new ToolbarPaperRegion(area)); continue; }
                var body = pieces[bodyIndex]; var footer = pieces[footerIndex];
                float edge = Math.Min(10, Math.Min(body.Height, footer.Width));
                if (pieceIndex == bodyIndex)
                {
                    AddPaperRegion(result, new ToolbarRect(body.X, body.Y, body.Width, body.Height-edge));
                    AddPaperRegion(result, new ToolbarRect(body.X, body.Bottom-edge, footer.Right-edge-body.X, edge));
                    AddPaperRegion(result, new ToolbarRect(footer.Right-edge, body.Bottom-edge, edge, edge), ToolbarPaperFade.Corner);
                    AddPaperRegion(result, new ToolbarRect(footer.Right, body.Bottom-edge, body.Right-footer.Right, edge), ToolbarPaperFade.Bottom);
                }
                else if (pieceIndex == footerIndex)
                {
                    AddPaperRegion(result, new ToolbarRect(footer.X, footer.Y, footer.Width-edge, footer.Height));
                    AddPaperRegion(result, new ToolbarRect(footer.Right-edge, footer.Y, edge, footer.Height), ToolbarPaperFade.Right);
                }
                else result.Add(new ToolbarPaperRegion(area));
            }
        }
        private static void AddPaperRegion(List<ToolbarPaperRegion> result, ToolbarRect area, ToolbarPaperFade fade = ToolbarPaperFade.None)
        { if (!area.Empty) result.Add(new ToolbarPaperRegion(area, fade)); }

        // Find free intervals before choosing the one actually used by footer content.
        internal static void FooterSegments(ToolbarRect band, IList<ToolbarRect> controls, List<ToolbarRect> result)
        {
            result.Clear();
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
                if (!free.Empty) result.Add(free);
                if (start >= band.Right) break;
                cursor = Math.Min(band.Right, Math.Max(cursor + 1, end));
            }
        }

        internal static void FooterContent(ToolbarRect band, IList<ToolbarRect> controls,
            float maximumWidth, List<ToolbarRect> result)
        {
            FooterSegments(new ToolbarRect(band.X, band.Y, Math.Min(band.Width, maximumWidth), band.Height), controls, result);
            ToolbarRect content = default;
            foreach (var segment in result) if (segment.Width > content.Width) content = segment;
            result.Clear();
            if (!content.Empty) result.Add(content);
        }

    }

    // During capture the view can detach, but only Drop may attach it again.
    // The same desired grip geometry drives preview and release, before HUD avoidance.
    internal sealed class ToolbarDragAnchor
    {
        private float startX, startY, grabX, grabY;
        internal bool Floating { get; private set; }
        internal bool Preview { get; private set; }
        internal void Begin(float x, float y, ToolbarRect grip, bool attached)
        {
            startX = x; startY = y; Floating = !attached; Preview = false;
            var small = ToolbarLayout.FloatingGripRect;
            grabX = small.X + Math.Max(0, Math.Min(1, (x - grip.X) / Math.Max(.001f, grip.Width))) * small.Width;
            grabY = small.Y + Math.Max(0, Math.Min(1, (y - grip.Y) / Math.Max(.001f, grip.Height))) * small.Height;
        }
        internal bool Move(float x, float y, ToolbarRect dockTarget, float screenScaleX, float screenScaleY,
            float floatingScale, out float wantedX, out float wantedY)
        {
            if (!Floating && !ToolbarLayout.WithinDetachThreshold(x, y, new ToolbarRect(startX, startY, 1, 1), screenScaleX, screenScaleY))
                Floating = true;
            wantedX = x - grabX * floatingScale; wantedY = y - grabY * floatingScale;
            var small = ToolbarLayout.FloatingGripRect;
            float cx = wantedX + (small.X + small.Width / 2) * floatingScale;
            float cy = wantedY + (small.Y + small.Height / 2) * floatingScale;
            Preview = Floating && !dockTarget.Empty && cx >= dockTarget.X && cx <= dockTarget.Right &&
                cy >= dockTarget.Y && cy <= dockTarget.Bottom;
            return Floating;
        }
        internal bool Drop(float x, float y, ToolbarRect dockTarget, float screenScaleX, float screenScaleY,
            float floatingScale, out float wantedX, out float wantedY)
        {
            Move(x, y, dockTarget, screenScaleX, screenScaleY, floatingScale, out wantedX, out wantedY);
            bool attach = !Floating || Preview;
            if (attach) Floating = false;
            Preview = false;
            return attach;
        }
        internal void Cancel() { Preview = false; }
    }

}
