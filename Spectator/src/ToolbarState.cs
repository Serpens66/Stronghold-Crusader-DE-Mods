using System;

namespace Spectator
{
    // Shared by the view model and its behavior tests; submenu visibility is independent of placement.
    internal sealed class ToolbarMenuState
    {
        internal bool Expanded { get; private set; }
        internal string View { get; private set; } = "";
        internal void ToggleExpanded() { Expanded = !Expanded; }
        internal void ToggleView(string view) { Expanded = true; View = View == view ? "" : view; }
        internal void Placement() { View = ""; }
    }

    internal static class ToolbarGeometry
    {
        internal const float MenuFontSize = 14;
        // A compact rank for each occupied slot; absent slots never consume a row/cell.
        internal static int[] PortraitPositions(int mask)
        {
            var result = new int[9]; result[0] = -1;
            int position = 0;
            for (int player = 1; player <= 8; player++)
                result[player] = (mask & (1 << (player - 1))) != 0 ? position++ : -1;
            return result;
        }
        internal static bool TryTooltipScale(float menuX, float menuY, float popupX, float popupY, out float x, out float y)
        {
            x = y = 0;
            if (!(menuX > 0) || !(menuY > 0) || !(popupX > 0) || !(popupY > 0)) return false;
            x = menuX / popupX; y = menuY / popupY;
            return !float.IsInfinity(x) && !float.IsInfinity(y) && x > 0 && y > 0;
        }
        // Only the detail rectangle is fitted. Opening it must never relocate the compact bar.
        internal static float DetailLeft(float barLeft, float barWidth, float width, float canvasWidth, bool right) =>
            Math.Max(0, Math.Min(Math.Max(0, canvasWidth - width), right ? barLeft + barWidth - width : barLeft));
        internal static float DetailWidth(float canvasWidth) => Math.Max(1, Math.Min(640, canvasWidth - 20));
        internal static float DetailHeight(float canvasHeight, float top) => Math.Max(0, canvasHeight - top - 10);
    }

    internal readonly struct PresentedAiOrder : IEquatable<PresentedAiOrder>
    {
        internal readonly int Mode, Target;
        private PresentedAiOrder(int mode, int target) { Mode = mode; Target = target; }
        // Vanilla PlayState wire values: 0=no order, 1=attack, 2=defend. Arrays are one-based.
        internal static PresentedAiOrder Read(int player, short[] modes, short[] targets, short[] requesters)
        {
            if (player < 1 || player > 8 || modes == null || player >= modes.Length) return default;
            int mode = modes[player];
            if (mode != 1 && mode != 2) return default;
            var source = mode == 1 ? targets : requesters;
            int target = source != null && player < source.Length ? source[player] : 0;
            return new PresentedAiOrder(mode, target >= 1 && target <= 8 ? target : 0);
        }
        public bool Equals(PresentedAiOrder other) => Mode == other.Mode && Target == other.Target;
        public override bool Equals(object other) => other is PresentedAiOrder value && Equals(value);
        public override int GetHashCode() => Mode * 31 + Target;
    }
}
