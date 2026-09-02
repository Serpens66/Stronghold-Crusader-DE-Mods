using System;
using System.Collections.Generic;

namespace UnitCosts
{
    internal sealed class UnitGoldCostSnapshot<TKey>
    {
        private readonly Dictionary<TKey, int> values = new Dictionary<TKey, int>();

        public IEnumerable<KeyValuePair<TKey, int>> Entries => values;

        public bool TryGetValue(TKey key, out int value) => values.TryGetValue(key, out value);

        public bool CaptureIfMissing(TKey key, Func<int> readValue, out Exception error)
        {
            error = null;
            if (values.ContainsKey(key))
                return true;

            try
            {
                values.Add(key, readValue());
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }
    }

    internal static class UnitGoldCostSnapshotPolicy
    {
        public static int SelectVanillaCost(bool usesSiegeTent, int currentUnitCost, int defaultSiegeTentCost)
        {
            int selectedCost = usesSiegeTent ? defaultSiegeTentCost : currentUnitCost;
            return Math.Max(0, selectedCost);
        }
    }
}
