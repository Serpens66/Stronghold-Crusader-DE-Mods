// Feature: Pure routing rules for cancelling Assassin climbing through Vanilla's Stop order.
namespace ExtraFeatures
{
    internal static class AssassinClimbCancellationPolicy
    {
        public const uint UnitStopCommand = 31;
        public const int ThrowingHookState = 126;
        public const int ClimbingUpState = 127;
        public const int StartClimbingDownState = 128;
        public const int ClimbingDownState = 129;
        public const int TileCount = 320800;

        public static bool ShouldInspectOrder(
            bool modEnabled,
            bool improvedPathfindingEnabled,
            bool fixedLayoutValidated,
            uint command,
            int issuedByPlayer)
        {
            return modEnabled && improvedPathfindingEnabled && fixedLayoutValidated &&
                command == UnitStopCommand && issuedByPlayer == 1;
        }

        public static bool IsClimbingState(int state)
        {
            return state >= ThrowingHookState && state <= ClimbingDownState;
        }

        public static bool IsValidTileId(uint tileId)
        {
            return tileId < TileCount;
        }
    }
}
