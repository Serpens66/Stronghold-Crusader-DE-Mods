// Pure selection rules for the shared human/AI enemy-proximity settings.
namespace ExtraFeatures
{
    internal static class EnemyProximityPolicy
    {
        internal const int VanillaMode = -1;
        internal const int MinimumRadius = -1;
        internal const int MaximumRadius = 100;
        internal const int VanillaHumanSingleplayerRadius = 30;
        internal const int VanillaHumanMultiplayerRadius = 15;

        internal static int SelectConfiguredRadius(
            bool isRealMultiplayer, int singleplayerRadius, int multiplayerRadius) =>
            isRealMultiplayer ? multiplayerRadius : singleplayerRadius;

        internal static int ResolveHumanImmediateRadius(int configuredRadius, int vanillaRadius) =>
            configuredRadius == VanillaMode ? vanillaRadius : configuredRadius;

        internal static int ApplyHumanPlacementRadius(int originalRadius, int configuredRadius)
        {
            if (configuredRadius == VanillaMode)
                return originalRadius;

            // The human placement validator also uses special radius-3 checks. Only its
            // ordinary SP/MP paths carry the normal Vanilla values and belong to this setting.
            return originalRadius == VanillaHumanSingleplayerRadius ||
                originalRadius == VanillaHumanMultiplayerRadius
                ? configuredRadius
                : originalRadius;
        }

        internal static int ApplyAIRadius(
            int originalRadius, int configuredRadius, bool isClassifiedRepairOrRebuild) =>
            isClassifiedRepairOrRebuild && configuredRadius != VanillaMode
                ? configuredRadius
                : originalRadius;
    }
}
