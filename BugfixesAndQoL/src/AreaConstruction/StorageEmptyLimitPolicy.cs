namespace BugfixesAndQoL.AreaConstruction
{
    internal static class StorageEmptyLimitPolicy
    {
        internal const int Vanilla = -1;
        internal const int SliderMaximum = 20;
        internal const int Maximum = 100;
        internal const int RecordsPerStockpile = 4;
        private const int VanillaSections = 8;

        internal static bool IsValid(int value) => value == Vanilla || (value >= 1 && value <= Maximum);

        internal static int RecordLimit(int setting, bool stockpile)
        {
            int sections = IsValid(setting) && setting != Vanilla ? setting : VanillaSections;
            return stockpile ? sections * RecordsPerStockpile : sections;
        }

        // The audited callers only compare the result against their fixed Vanilla threshold.
        internal static int CountForVanillaComparison(int count, int recordLimit, int vanillaRecordLimit)
        {
            if (recordLimit == vanillaRecordLimit)
                return count;
            return count < recordLimit ? 0 : vanillaRecordLimit;
        }
    }
}
