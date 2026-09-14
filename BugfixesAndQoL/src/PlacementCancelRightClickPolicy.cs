// Feature: Decide whether Vanilla should receive a gameplay right-click.
namespace BugfixesAndQoL
{
    internal static class PlacementCancelRightClickPolicy
    {
        public static bool ShouldForwardRightDown(
            bool modEnabled,
            bool clientFeaturesEnabled,
            bool optionEnabled,
            int currentAction,
            bool sh1RtsControls)
        {
            bool suppress =
                modEnabled &&
                clientFeaturesEnabled &&
                optionEnabled &&
                currentAction == 5 &&
                !sh1RtsControls;
            return !suppress;
        }
    }
}
