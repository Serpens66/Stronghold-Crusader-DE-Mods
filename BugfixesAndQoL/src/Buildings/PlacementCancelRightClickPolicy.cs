// Feature: Decide whether Vanilla should receive a gameplay right-click.
namespace BugfixesAndQoL
{
    internal static class PlacementCancelRightClickPolicy
    {
        public static bool ShouldForwardRightDown(
            bool clientFeaturesEnabled,
            bool optionEnabled,
            int currentAction,
            bool sh1RtsControls)
        {
            bool suppress =
                clientFeaturesEnabled &&
                optionEnabled &&
                currentAction == 5 &&
                !sh1RtsControls;
            return !suppress;
        }

        public static bool BeginRightClickGesture(
            bool clientFeaturesEnabled,
            bool optionEnabled,
            int currentAction,
            bool sh1RtsControls,
            ref bool suppressNextRightUp)
        {
            bool forwardRightDown = ShouldForwardRightDown(
                clientFeaturesEnabled,
                optionEnabled,
                currentAction,
                sh1RtsControls);
            suppressNextRightUp = !forwardRightDown;
            return forwardRightDown;
        }

        public static bool CompleteRightClickGesture(ref bool suppressNextRightUp)
        {
            bool forwardRightUp = !suppressNextRightUp;
            suppressNextRightUp = false;
            return forwardRightUp;
        }
    }
}
