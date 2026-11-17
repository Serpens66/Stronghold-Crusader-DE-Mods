using System;
using System.Collections.Generic;

namespace Spectator
{
    // Geometry is expressed in the owning canvas, independently of Noesis/physical pixel scale.
    internal static class AiPlacementLayout
    {
        internal static bool TryDock(ToolbarRect menu, float width, float height, float vw, float vh,
            IList<ToolbarRect> obstacles, out ToolbarRect result)
        {
            result = default;
            if (menu.Empty || width <= 0 || height <= 0 || width > vw || height > vh) return false;
            // Keep the entire top edge search attached to the menu. Prefer its right corner.
            float x = Math.Max(0, Math.Min(vw - width, menu.Right - width));
            float y = menu.Y - height;
            double best = double.MaxValue;
            Consider(x, y, x, y, width, height, vw, vh, menu, obstacles, ref best, ref result, true);
            Consider(menu.X, y, x, y, width, height, vw, vh, menu, obstacles, ref best, ref result, true);
            foreach (var obstacle in obstacles)
            {
                Consider(obstacle.X - width, y, x, y, width, height, vw, vh, menu, obstacles, ref best, ref result, true);
                Consider(obstacle.Right, y, x, y, width, height, vw, vh, menu, obstacles, ref best, ref result, true);
            }
            if (best != double.MaxValue) return true;
            Consider(menu.Right, menu.Y, x, y, width, height, vw, vh, menu, obstacles, ref best, ref result);
            Consider(menu.X - width, menu.Y, x, y, width, height, vw, vh, menu, obstacles, ref best, ref result);
            Consider(menu.Right - width, menu.Bottom, x, y, width, height, vw, vh, menu, obstacles, ref best, ref result);
            if (best != double.MaxValue) return true;
            // A constrained HUD can use a screen edge, never an arbitrary floating fallback.
            Consider(vw - width, Math.Max(0, y), x, y, width, height, vw, vh, menu, obstacles, ref best, ref result);
            Consider(0, Math.Max(0, y), x, y, width, height, vw, vh, menu, obstacles, ref best, ref result);
            return best != double.MaxValue;
        }

        private static void Consider(float x, float y, float preferredX, float preferredY, float width, float height,
            float vw, float vh, ToolbarRect menu, IList<ToolbarRect> obstacles, ref double best, ref ToolbarRect result, bool topAttached = false)
        {
            if (x < 0 || y < 0 || x + width > vw || y + height > vh) return;
            // Top candidates must still touch the menu rather than slide out into open space.
            if (topAttached && (x >= menu.Right || x + width <= menu.X)) return;
            var candidate = new ToolbarRect(x, y, width, height);
            if (candidate.Intersects(menu)) return;
            foreach (var obstacle in obstacles) if (candidate.Intersects(obstacle)) return;
            double dx = x - preferredX, dy = y - preferredY, distance = dx * dx + dy * dy;
            if (distance >= best) return;
            best = distance; result = candidate;
        }
    }

    internal sealed class AiPlacementPosition
    {
        private float anchorX, anchorY, grabX, grabY, previewX, previewY;
        internal bool Floating { get; private set; }
        internal bool Dragging { get; private set; }

        internal ToolbarRect Arrange(ToolbarRect dock, float width, float height, float vw, float vh) =>
            Dragging ? ToolbarLayout.Clamp(previewX, previewY, width, height, vw, vh) :
            Floating ? ToolbarLayout.Clamp(anchorX * Math.Max(0, vw - width), anchorY * Math.Max(0, vh - height), width, height, vw, vh) : dock;

        internal void Begin(float x, float y, ToolbarRect panel)
        { grabX = x - panel.X; grabY = y - panel.Y; previewX = panel.X; previewY = panel.Y; Dragging = true; }

        internal void Move(float x, float y)
        { if (Dragging) { previewX = x - grabX; previewY = y - grabY; } }

        internal void Drop(ToolbarRect dock, float width, float height, float vw, float vh, float screenScaleX, float screenScaleY)
        {
            if (!Dragging) return;
            var pose = Arrange(dock, width, height, vw, vh);
            double dx = (pose.X - dock.X) * Math.Abs(screenScaleX), dy = (pose.Y - dock.Y) * Math.Abs(screenScaleY);
            Floating = dock.Empty || dx * dx + dy * dy > 14 * 14;
            if (Floating)
            {
                anchorX = vw > width ? pose.X / (vw - width) : 0;
                anchorY = vh > height ? pose.Y / (vh - height) : 0;
            }
            Dragging = false;
        }

        internal void Cancel() { Dragging = false; }
        internal void Reset() { Cancel(); Floating = false; anchorX = anchorY = 0; }
    }
}
