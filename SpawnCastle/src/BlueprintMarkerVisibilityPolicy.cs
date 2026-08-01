namespace SpawnCastle
{
    internal static class BlueprintMarkerVisibilityPolicy
    {
        // Markers are available again, while the visibility thresholds keep
        // full-size opaque Blueprint icons uncluttered.
        public static readonly bool GroundMarkersEnabled = true;

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
