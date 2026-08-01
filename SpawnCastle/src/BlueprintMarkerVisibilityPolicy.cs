namespace SpawnCastle
{
    internal static class BlueprintMarkerVisibilityPolicy
    {
        // Test switch: keep this false to avoid creating any colored marker
        // objects. Changing this one value restores the retained marker policy.
        public static readonly bool GroundMarkersEnabled = false;

        private const float FullIconScale = 1f;
        private const float LowIconAlpha = 0.25f;
        private const float ComparisonTolerance = 0.0001f;

        public static bool ShouldShow(float iconScale, float iconAlpha)
        {
            return GroundMarkersEnabled &&
                ShouldShowWhenEnabled(iconScale, iconAlpha);
        }

        public static bool ShouldShowWhenEnabled(float iconScale, float iconAlpha)
        {
            // Mark the footprint whenever reduced size or opacity makes the
            // building icon alone less explicit.
            return iconScale < FullIconScale - ComparisonTolerance ||
                iconAlpha <= LowIconAlpha + ComparisonTolerance;
        }
    }
}
