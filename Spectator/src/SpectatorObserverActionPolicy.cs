using CrusaderDE;

namespace Spectator
{
    internal static class SpectatorObserverActionPolicy
    {
        // Audited DLL_GameAction branches only: local presentation/camera/pause state, no economic
        // or troop command. Saves, menus and report-mode selection use other managed entry points.
        // Unknown actions remain denied; CloseTroopsPanel is intentionally not treated as pure UI.
        internal static bool IsLocalPresentationCommand(Enums.GameActionCommand command)
        {
            switch (command)
            {
                case Enums.GameActionCommand.CycleBookmarks:
                case Enums.GameActionCommand.RadarClicked:
                case Enums.GameActionCommand.AlignSprites:
                case Enums.GameActionCommand.ResizeRadarMapBuffer:
                case Enums.GameActionCommand.CentreMarker:
                case Enums.GameActionCommand.HideObjectiveProgress:
                case Enums.GameActionCommand.SetRadarMapRotationType:
                case Enums.GameActionCommand.Game_Paused:
                case Enums.GameActionCommand.ShowPlannedMoat:
                case Enums.GameActionCommand.SpectatorMode:
                    return true;
                default:
                    return false;
            }
        }
    }
}
