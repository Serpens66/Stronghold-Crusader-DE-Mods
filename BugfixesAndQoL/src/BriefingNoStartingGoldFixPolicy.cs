namespace BugfixesAndQoL
{
    internal static class BriefingNoStartingGoldFixPolicy
    {
        internal static bool IsEnabled(
            bool enableClientFeatures,
            bool enableFix) =>
            enableClientFeatures && enableFix;

        internal static int Apply(
            int currentGold,
            int effectiveVanillaGold,
            bool isHuman,
            bool hasNoStartingGoldState,
            bool noStartingGoldEnabled)
        {
            if (!hasNoStartingGoldState || !isHuman || !noStartingGoldEnabled)
                return currentGold;

            return effectiveVanillaGold;
        }
    }
}
