namespace ShieldTowerTest
{
    internal static class PortableShieldClimbSelectionPolicy
    {
        public static bool ShouldOverrideVanilla(
            bool featureEnabled,
            int vanillaResult,
            int ownMovableShieldCount,
            int ownOtherCount,
            int foreignCount,
            int nonMovableShieldCount) =>
            featureEnabled &&
            vanillaResult == 0 &&
            ownMovableShieldCount > 0 &&
            ownOtherCount == 0 &&
            foreignCount == 0 &&
            nonMovableShieldCount == 0;
    }
}
