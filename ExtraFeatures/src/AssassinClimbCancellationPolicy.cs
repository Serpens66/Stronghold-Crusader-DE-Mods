// Feature: Pure routing rules for cancelling Assassin climbing through Vanilla Stop.
namespace ExtraFeatures
{
    internal static class AssassinClimbCancellationPolicy
    {
        public const uint UnitStopCommand = 31;
        public const int ThrowingHookState = 126;
        public const int ClimbingUpState = 127;
        public const int StartClimbingDownState = 128;
        public const int ClimbingDownState = 129;

        public static bool ShouldHandleCommand(
            bool modEnabled,
            bool improvedPathfindingEnabled,
            bool nativeHookInstalled,
            uint command)
        {
            return modEnabled && improvedPathfindingEnabled && nativeHookInstalled &&
                command == UnitStopCommand;
        }

        public static bool IsClimbingState(int state)
        {
            return state >= ThrowingHookState && state <= ClimbingDownState;
        }

    }
}
