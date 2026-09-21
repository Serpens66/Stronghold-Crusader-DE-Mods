namespace StartConditions
{
    internal static class StartGoldPolicy
    {
        internal const int MaximumGold = 1000000;

        internal static bool IsValidConfiguredValue(int value) =>
            value == -1 || (value >= 0 && value <= MaximumGold);

        internal static int CalculateGold(
            int effectiveVanillaGold,
            int setGold,
            int addGold)
        {
            if (effectiveVanillaGold < 0)
                throw new System.ArgumentOutOfRangeException(nameof(effectiveVanillaGold));
            if (!IsValidConfiguredValue(setGold))
                throw new System.ArgumentOutOfRangeException(nameof(setGold));

            long basis = setGold < 0 ? effectiveVanillaGold : setGold;
            long result = basis + addGold;
            if (result < 0)
                return 0;

            if (result > int.MaxValue)
                throw new System.OverflowException("Calculated start gold exceeds Int32.MaxValue.");
            return (int)result;
        }
    }
}
