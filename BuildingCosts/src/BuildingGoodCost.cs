using SHCDESE.Interop;

namespace BuildingCosts
{
    public readonly struct BuildingGoodCost
    {
        public readonly eGoods Good;
        public readonly int Amount;
        public readonly BuildingGoodCostTarget Target;

        public BuildingGoodCost(eGoods good, int amount, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
        {
            Good = good;
            Amount = amount;
            Target = target;
        }
    }
}
