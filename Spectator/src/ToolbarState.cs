using System;

namespace Spectator
{
    // Each page owns its last result; an order must not erase a goods transaction.
    internal sealed class ToolbarFeedback
    {
        private string goods = "", orders = "";
        internal string ForPage(string page) => page == "Goods" ? goods : page == "Orders" ? orders : "";
        internal void Set(string page, string value)
        {
            if (page == "Goods") goods = value ?? "";
            else if (page == "Orders") orders = value ?? "";
        }
    }

    // One navigation state for the floating and HUD-anchored presentation.
    internal sealed class ToolbarMenuState
    {
        internal bool Expanded { get; private set; }
        internal string View { get; private set; } = "Goods";
        internal bool Placing { get; private set; }
        internal bool Floating { get; private set; }
        internal bool ToolsAvailable { get; private set; }
        internal bool SetToolsAvailable(bool value)
        {
            if (ToolsAvailable == value) return false;
            ToolsAvailable = value;
            if (!value) { Expanded = false; Placing = false; }
            return true;
        }
        internal bool CanInvoke(string command) => ToolsAvailable || command == "ToggleDesign";
        internal bool CanExpand => ToolsAvailable && !Placing;
        internal bool SetFloating(bool value)
        { if (Floating == value) return false; Floating = value; return true; }
        internal string ControlLabel(string toolsLabel, string backLabel) =>
            Floating ? (Expanded ? "\u2212" : "+") : Expanded ? backLabel : toolsLabel;
        internal string ControlHelpKey(bool ownBuildMenu) => !Expanded ? "Tools.CollapseHelp" :
            !Floating && ownBuildMenu ? "Tools.BackBuildHelp" : "Tools.BackHelp";
        internal float ControlFontSize => Floating ? 18 : 12;
        internal void ToggleExpanded(bool currentlyVisible = true, Func<bool> beforeDockedOpen = null)
        {
            if (!ToolsAvailable) return;
            if (!Floating && (!Expanded || !currentlyVisible) && beforeDockedOpen != null && !beforeDockedOpen()) return;
            if (Expanded && !currentlyVisible) return;
            Expanded = !Expanded;
        }
        internal void ToggleView(string view) { if (ToolsAvailable && (view == "Goods" || view == "Orders")) { Expanded = true; View = view; } }
        internal void SetPlacement(bool value) { Placing = ToolsAvailable && value; }
        internal bool ShowDetails => ToolsAvailable && Expanded && !Placing;
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
