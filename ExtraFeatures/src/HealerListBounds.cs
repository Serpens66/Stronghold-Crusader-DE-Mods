namespace ExtraFeatures
{
    internal static class HealerListBounds
    {
        internal const int Capacity = 10000;

        // The native array has a sentinel at ID zero and valid game IDs below Capacity.
        internal static bool IsUsableNextUnitId(int nextUnitId) =>
            nextUnitId >= 1 && nextUnitId <= Capacity;

        internal static bool IsUsableListCount(int count) =>
            count >= 0 && count < Capacity;
    }
}
