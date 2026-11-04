// Pure selection rules for the shared human/AI enemy-proximity settings.
namespace ExtraFeatures
{
    internal static class EnemyProximityPolicy
    {
        internal const int VanillaMode = -1;
        internal const int MinimumRadius = -1;
        internal const int MaximumRadius = 100;
        internal const int OrdinaryHumanLargeRadius = 30;
        internal const int OrdinaryHumanSmallRadius = 15;

        internal static int SelectConfiguredRadius(
            bool isRealMultiplayer, int singleplayerRadius, int multiplayerRadius) =>
            isRealMultiplayer ? multiplayerRadius : singleplayerRadius;

        // Vanilla mode supplies no radius; the incoming (Vanilla or earlier) radius stays.
        internal static int? ConfiguredContribution(int configuredRadius) =>
            configuredRadius == VanillaMode ? (int?)null : configuredRadius;

        internal static bool IsOrdinaryHumanRadius(int radius) =>
            radius == OrdinaryHumanLargeRadius || radius == OrdinaryHumanSmallRadius;
    }
}
