using SHCDESE.Interop;
using System.Collections.Generic;

namespace UnitCosts
{
    public sealed class UnitExtraCostValues
    {
        private readonly Dictionary<eGoods, int> costs;

        public UnitExtraCostValues(Dictionary<eGoods, int> costs)
        {
            this.costs = costs ?? new Dictionary<eGoods, int>();
        }

        public int GetCost(eGoods good)
        {
            return costs.TryGetValue(good, out int amount) ? amount : 0;
        }

        public bool HasAnyCost()
        {
            foreach (int amount in costs.Values)
            {
                if (amount != 0)
                    return true;
            }

            return false;
        }

        public IReadOnlyDictionary<eGoods, int> Costs => costs;

        internal Dictionary<eGoods, int> CostEntries => costs;

        public static int ClampCost(eGoods good, int value)
        {
            if (good == eGoods.STORED_GOLD)
            {
                if (value < -10000)
                    return -10000;
            }
            else if (value < 0)
            {
                return 0;
            }

            int maximum = good == eGoods.STORED_GOLD ? 10000 : 100;
            if (value > maximum)
                return maximum;
            return value;
        }
    }
}
