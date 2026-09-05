namespace StartConditions
{
    internal static class StartGoldPolicy
    {
        internal const int MaximumGold = 1000000;

        internal static bool IsValidConfiguredValue(int value) =>
            value == -1 || (value >= 0 && value <= MaximumGold);
    }
}
