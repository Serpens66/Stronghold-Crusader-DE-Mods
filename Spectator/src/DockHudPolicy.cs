namespace Spectator
{
    internal enum DockHudDisplay { Hidden, Tools, Placement }

    internal static class DockHudPolicy
    {
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
