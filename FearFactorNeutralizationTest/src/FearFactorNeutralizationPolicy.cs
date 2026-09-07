namespace FearFactorNeutralizationTest
{
    internal static class FearFactorNeutralizationPolicy
    {
        internal static int CalculateVanillaDamage(int baseDamage, int fearLevel) =>
            baseDamage * (fearLevel + 20) * 5 / 100;

        internal static int CalculateNeutralDamage(int baseDamage) => baseDamage;
    }
}
