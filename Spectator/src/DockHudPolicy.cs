namespace Spectator
{
    internal enum DockHudDisplay { Hidden, Tools, Placement }

    internal static class DockHudPolicy
    {
        // The release toolbar followed the Extras parent, independently of child visibility.
        internal static bool CanShowToolbar(bool eligible, bool ui, bool ingame, bool extras,
            bool briefing, bool statistics, bool menu, int occupants) =>
            eligible && ui && ingame && extras && !briefing && !statistics && !menu && occupants > 0;

        // Managed HUD_Main contract: NewBuildScreenBlank selects page 22. It has no build icons.
        internal const int BlankBuildScreen = 22;
        internal static bool CanCoverBuildCategories(DockHudDisplay display, bool expanded) =>
            display == DockHudDisplay.Tools && expanded;

        internal static DockHudDisplay Evaluate(bool eligible, bool uiVisible, bool main,
            bool occupied, bool editorTerrain, bool placement, int buildScreen)
        {
            if (!eligible || !uiVisible || !main || occupied || editorTerrain) return DockHudDisplay.Hidden;
            if (placement) return DockHudDisplay.Placement;
            return buildScreen == BlankBuildScreen ? DockHudDisplay.Tools : DockHudDisplay.Hidden;
        }
    }
}
