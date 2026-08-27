namespace CastlePlanner
{
    internal static class CastleSpawnContentPolicy
    {
        internal const bool DefaultFortifications = true;
        internal const bool DefaultBuildings = true;
        internal const bool DefaultDefensiveGroundFeatures = true;
        internal const bool DefaultFearFactorBuildings = false;
        internal const bool DefaultSiegeEngines = false;

        internal static bool ShouldResetBeforeEnabling(
            bool currentSpawnCastle,
            bool requestedSpawnCastle,
            bool canEditHostSettings)
        {
            return !currentSpawnCastle &&
                requestedSpawnCastle &&
                canEditHostSettings;
        }

        internal static bool ShouldDisableBeforeContentChange(
            bool currentSpawnCastle,
            bool canEditHostSettings,
            bool fortifications,
            bool buildings,
            bool defensiveGroundFeatures,
            bool fearFactorBuildings,
            bool siegeEngines,
            bool braziersAndFlags)
        {
            return currentSpawnCastle &&
                canEditHostSettings &&
                !HasAnyEnabled(
                    fortifications,
                    buildings,
                    defensiveGroundFeatures,
                    fearFactorBuildings,
                    siegeEngines,
                    braziersAndFlags);
        }

        internal static bool HasAnyEnabled(
            bool fortifications,
            bool buildings,
            bool defensiveGroundFeatures,
            bool fearFactorBuildings,
            bool siegeEngines,
            bool braziersAndFlags)
        {
            return fortifications ||
                buildings ||
                defensiveGroundFeatures ||
                fearFactorBuildings ||
                siegeEngines ||
                braziersAndFlags;
        }
    }
}
