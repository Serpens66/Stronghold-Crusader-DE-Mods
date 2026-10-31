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
        internal static bool CanCoverBuildCategories(DockHudDisplay display, bool expanded, bool blankMainPage) =>
            display == DockHudDisplay.Tools && expanded && blankMainPage;

        internal static bool CanShowDetails(bool floating, DockHudDisplay display) => floating || display == DockHudDisplay.Tools;

        internal static bool CenterIsOccupied(bool main, int page, bool placement, bool foreign) =>
            foreign || (main && !placement && page != BlankBuildScreen);

        internal static DockHudDisplay Evaluate(bool eligible, bool uiVisible, bool occupied, bool placement)
        {
            if (!eligible || !uiVisible || occupied) return DockHudDisplay.Hidden;
            if (placement) return DockHudDisplay.Placement;
            return DockHudDisplay.Tools;
        }
    }
}
