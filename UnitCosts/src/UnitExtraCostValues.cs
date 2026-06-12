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

        public static int ClampCost(eGoods good, int value, int currentGoldCost = 0)
        {
            if (good == eGoods.STORED_GOLD)
            {
                int minGold = -System.Math.Max(0, currentGoldCost);
                if (value < minGold)
                    return minGold;
            }
            else if (value < 0)
            {
                return 0;
            }

            if (value > 1000)
                return 1000;
            return value;
        }
    }
}
