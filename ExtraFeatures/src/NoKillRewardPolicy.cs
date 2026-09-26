namespace ExtraFeatures
{
    internal static class NoKillRewardPolicy
    {
        internal static int ComposeMask(bool human, bool ai) =>
            (human ? 1 : 0) | (ai ? 2 : 0);

        internal static bool Suppresses(int mask, bool isAI) =>
            (mask & (isAI ? 2 : 1)) != 0;
    }
}
