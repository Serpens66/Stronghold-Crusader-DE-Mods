namespace ExtraFeatures
{
    internal static class FearFactorNeutralizationPolicy
    {
        internal static bool IsEnabled(bool modEnabled, bool settingEnabled, bool contextAllowed) =>
            modEnabled && settingEnabled && contextAllowed;

        internal static int CalculateVanillaDamage(int baseDamage, int fearLevel) =>
            baseDamage * (fearLevel + 20) * 5 / 100;

        internal static int CalculateNeutralDamage(int baseDamage) => baseDamage;
    }
}
