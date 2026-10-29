using System;

namespace Spectator
{
    // One navigation state for the floating and HUD-anchored presentation.
    internal sealed class ToolbarMenuState
    {
        internal bool Expanded { get; private set; }
        internal string View { get; private set; } = "Goods";
        internal bool Placing { get; private set; }
        internal void ToggleExpanded() { Expanded = !Expanded; }
        internal void ToggleView(string view) { if (view == "Goods" || view == "Orders") { Expanded = true; View = view; } }
        internal void SetPlacement(bool value) { Placing = value; }
        internal bool ShowDetails => Expanded && !Placing;
    }

    internal static class ToolbarGeometry
    {
        internal const float MenuFontSize = 14;
        internal static bool TryTooltipScale(float menuX, float menuY, float popupX, float popupY, out float x, out float y)
        {
            x = y = 0;
            if (!(menuX > 0) || !(menuY > 0) || !(popupX > 0) || !(popupY > 0)) return false;
            x = menuX / popupX; y = menuY / popupY;
            return !float.IsInfinity(x) && !float.IsInfinity(y) && x > 0 && y > 0;
        }

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
