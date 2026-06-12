using SHCDESE.Interop;

namespace BuildingCosts
{
    public static class BuildingCostGlobalAPI
    {
        public static void Building_SetGoodCost(eStructs building, eGoods good, int amount)
            => BuildingCostsAPI.SetGoodCost(building, good, amount);

        public static void Building_SetGoodCost(eStructs building, eGoods good, int amount, BuildingGoodCostTarget target)
            => BuildingCostsAPI.SetGoodCost(building, good, amount, target);

        public static int Building_GetModdedGoodCost(eStructs building, eGoods good)
            => BuildingCostsAPI.GetModdedGoodCost(building, good);

        public static int Building_GetModdedGoodCost(eStructs building, eGoods good, BuildingGoodCostTarget target)
            => BuildingCostsAPI.GetModdedGoodCost(building, good, target);

        public static BuildingGoodCost[] Building_GetModdedGoodCosts(eStructs building)
            => BuildingCostsAPI.GetModdedGoodCosts(building);

        public static BuildingGoodCost[] Building_GetModdedGoodCosts(eStructs building, BuildingGoodCostTarget target)
            => BuildingCostsAPI.GetModdedGoodCosts(building, target);

        public static void Building_ClearModdedGoodCost(eStructs building, eGoods good)
            => BuildingCostsAPI.ClearModdedGoodCost(building, good);

        public static void Building_ClearModdedGoodCost(eStructs building, eGoods good, BuildingGoodCostTarget target)
            => BuildingCostsAPI.ClearModdedGoodCost(building, good, target);

        public static void Building_ClearModdedGoodCosts(eStructs building)
            => BuildingCostsAPI.ClearModdedGoodCosts(building);

        public static void Building_ClearModdedGoodCosts(eStructs building, BuildingGoodCostTarget target)
            => BuildingCostsAPI.ClearModdedGoodCosts(building, target);

        public static void Building_ClearAllModdedGoodCosts()
            => BuildingCostsAPI.ClearAllModdedGoodCosts();
    }
}
