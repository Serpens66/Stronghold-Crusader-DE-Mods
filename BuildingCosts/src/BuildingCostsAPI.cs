using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BuildingCosts
{
    public static class BuildingCostsAPI
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<eStructs, Dictionary<BuildingGoodCostKey, int>> BuildingModdedGoodCosts =
            new Dictionary<eStructs, Dictionary<BuildingGoodCostKey, int>>();

        private static readonly eGoods[] ConstructionTooltipGoodOrder = CreateConstructionTooltipGoodOrder();

        public static void SetGoodCost(eStructs building, eGoods good, int amount, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
        {
            if (!IsValidGoodCost(good))
                throw new ArgumentOutOfRangeException(nameof(good), good, "Good is not a storable good.");

            if (!IsValidGoodCostTarget(target))
                throw new ArgumentOutOfRangeException(nameof(target), target, "Target must be All, Human, or AI.");

            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "Amount must be zero or greater.");

            lock (SyncRoot)
            {
                if (!BuildingModdedGoodCosts.TryGetValue(building, out Dictionary<BuildingGoodCostKey, int> costs))
                {
                    costs = new Dictionary<BuildingGoodCostKey, int>();
                    BuildingModdedGoodCosts[building] = costs;
                }

                costs[new BuildingGoodCostKey(good, target)] = amount;
            }
        }

        public static void Building_SetGoodCost(eStructs building, eGoods good, int amount, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
            => SetGoodCost(building, good, amount, target);

        public static int GetModdedGoodCost(eStructs building, eGoods good, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
        {
            if (!IsValidGoodCost(good) || !IsValidGoodCostTarget(target))
                return 0;

            lock (SyncRoot)
            {
                if (!BuildingModdedGoodCosts.TryGetValue(building, out Dictionary<BuildingGoodCostKey, int> costs))
                    return 0;

                return costs.TryGetValue(new BuildingGoodCostKey(good, target), out int amount) ? amount : 0;
            }
        }

        public static int Building_GetModdedGoodCost(eStructs building, eGoods good, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
            => GetModdedGoodCost(building, good, target);

        public static BuildingGoodCost[] GetModdedGoodCosts(eStructs building, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
        {
            if (!IsValidGoodCostTarget(target))
                return Array.Empty<BuildingGoodCost>();

            lock (SyncRoot)
            {
                if (!BuildingModdedGoodCosts.TryGetValue(building, out Dictionary<BuildingGoodCostKey, int> costs))
                    return Array.Empty<BuildingGoodCost>();

                return costs
                    .Where(cost => cost.Key.Target == target)
                    .Select(cost => new BuildingGoodCost(cost.Key.Good, cost.Value, cost.Key.Target))
                    .ToArray();
            }
        }

        public static BuildingGoodCost[] Building_GetModdedGoodCosts(eStructs building, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
            => GetModdedGoodCosts(building, target);

        public static void ClearModdedGoodCost(eStructs building, eGoods good, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
        {
            if (!IsValidGoodCost(good) || !IsValidGoodCostTarget(target))
                return;

            lock (SyncRoot)
            {
                if (!BuildingModdedGoodCosts.TryGetValue(building, out Dictionary<BuildingGoodCostKey, int> costs))
                    return;

                costs.Remove(new BuildingGoodCostKey(good, target));
                if (costs.Count == 0)
                    BuildingModdedGoodCosts.Remove(building);
            }
        }

        public static void Building_ClearModdedGoodCost(eStructs building, eGoods good, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
            => ClearModdedGoodCost(building, good, target);

        public static void ClearModdedGoodCosts(eStructs building, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
        {
            if (!IsValidGoodCostTarget(target))
                return;

            lock (SyncRoot)
            {
                RemoveGoodCostsForTarget(building, target);
            }
        }

        public static void Building_ClearModdedGoodCosts(eStructs building, BuildingGoodCostTarget target = BuildingGoodCostTarget.All)
            => ClearModdedGoodCosts(building, target);

        public static void ClearAllModdedGoodCosts()
        {
            lock (SyncRoot)
            {
                BuildingModdedGoodCosts.Clear();
            }
        }

        public static void Building_ClearAllModdedGoodCosts()
            => ClearAllModdedGoodCosts();

        public static void ClearAllModdedGoodCostsForTarget(BuildingGoodCostTarget target)
        {
            if (!IsValidGoodCostTarget(target))
                return;

            lock (SyncRoot)
            {
                List<eStructs> buildings = new List<eStructs>(BuildingModdedGoodCosts.Keys);
                foreach (eStructs building in buildings)
                    RemoveGoodCostsForTarget(building, target);
            }
        }

        public static bool HasModdedGoodCosts(eStructs building)
        {
            lock (SyncRoot)
            {
                return BuildingModdedGoodCosts.TryGetValue(building, out Dictionary<BuildingGoodCostKey, int> costs) &&
                    costs.Count > 0;
            }
        }

        public static bool HasModdedGoodCosts(eStructs building, int playerId)
        {
            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);

            lock (SyncRoot)
            {
                if (!BuildingModdedGoodCosts.TryGetValue(building, out Dictionary<BuildingGoodCostKey, int> costs))
                    return false;

                return costs.Keys.Any(cost => IsGoodCostTargetApplicable(cost.Target, isAI));
            }
        }

        public static bool HasEffectiveGoodCostsAvailable(eStructs building, int playerId)
        {
            foreach (BuildingGoodCost cost in GetEffectiveGoodCosts(building, playerId))
            {
                if (!GamePlayerManagerAPI.Instance.HasGoodsAmount(playerId, cost.Good, cost.Amount))
                    return false;
            }

            return true;
        }

        public static bool HasMissingNonVanillaEffectiveGoodCosts(eStructs building, int playerId)
        {
            foreach (BuildingGoodCost cost in GetEffectiveGoodCosts(building, playerId))
            {
                if (!IsVanillaConstructionGood(cost.Good) &&
                    !GamePlayerManagerAPI.Instance.HasGoodsAmount(playerId, cost.Good, cost.Amount))
                    return true;
            }

            return false;
        }

        public static void RemoveEffectiveGoodCosts(eStructs building, int playerId)
        {
            foreach (BuildingGoodCost cost in GetEffectiveGoodCosts(building, playerId))
                GamePlayerManagerAPI.Instance.RemoveGood(playerId, cost.Good, cost.Amount);
        }

        public static BuildingGoodCost[] GetAllGoodCostsForTooltip(eStructs building, int playerId)
            => SortGoodCostsForTooltip(GetEffectiveGoodCosts(building, playerId));

        public static BuildingGoodCost[] GetEffectiveGoodCosts(eStructs building, int playerId)
        {
            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            Dictionary<eGoods, int> costs = GetNativeConstructionCosts(building);
            ApplyModdedGoodCosts(building, costs, BuildingGoodCostTarget.All);
            ApplyModdedGoodCosts(building, costs, isAI ? BuildingGoodCostTarget.AI : BuildingGoodCostTarget.Human);
            return costs
                .Where(cost => cost.Value > 0)
                .Select(cost => new BuildingGoodCost(cost.Key, cost.Value))
                .ToArray();
        }

        private static void ApplyModdedGoodCosts(eStructs building, Dictionary<eGoods, int> costs, BuildingGoodCostTarget target)
        {
            lock (SyncRoot)
            {
                if (!BuildingModdedGoodCosts.TryGetValue(building, out Dictionary<BuildingGoodCostKey, int> moddedCosts))
                    return;

                foreach (KeyValuePair<BuildingGoodCostKey, int> cost in moddedCosts)
                {
                    if (cost.Key.Target != target)
                        continue;

                    if (cost.Value > 0)
                        costs[cost.Key.Good] = cost.Value;
                    else
                        costs.Remove(cost.Key.Good);
                }
            }
        }

        private static BuildingGoodCost[] SortGoodCostsForTooltip(BuildingGoodCost[] costs)
        {
            Dictionary<eGoods, int> costLookup = costs.ToDictionary(cost => cost.Good, cost => cost.Amount);
            List<BuildingGoodCost> result = new List<BuildingGoodCost>(costLookup.Count);
            HashSet<eGoods> emittedGoods = new HashSet<eGoods>();

            foreach (eGoods good in ConstructionTooltipGoodOrder)
            {
                if (costLookup.TryGetValue(good, out int amount) && amount > 0)
                {
                    result.Add(new BuildingGoodCost(good, amount));
                    emittedGoods.Add(good);
                }
            }

            foreach (KeyValuePair<eGoods, int> cost in costLookup.OrderBy(cost => cost.Key))
            {
                if (cost.Value > 0 && !emittedGoods.Contains(cost.Key))
                    result.Add(new BuildingGoodCost(cost.Key, cost.Value));
            }

            return result.ToArray();
        }

        private static bool IsValidGoodCost(eGoods good)
        {
            return good > eGoods.STORED_NULL && good < eGoods.Count;
        }

        private static eGoods[] CreateConstructionTooltipGoodOrder()
        {
            List<eGoods> goods = new List<eGoods>();

            AddConstructionTooltipGood(goods, eGoods.STORED_WOOD_PLANKS);
            AddConstructionTooltipGood(goods, eGoods.STORED_STONE_BLOCKS);
            AddConstructionTooltipGood(goods, eGoods.STORED_IRON_INGOTS);
            AddConstructionTooltipGood(goods, eGoods.STORED_PITCH_RAW);
            AddConstructionTooltipGood(goods, eGoods.STORED_GOLD);

            foreach (eGoods good in Enum.GetValues(typeof(eGoods)))
                AddConstructionTooltipGood(goods, good);

            return goods.ToArray();
        }

        private static void AddConstructionTooltipGood(List<eGoods> goods, eGoods good)
        {
            if (!IsConfigurableStoredGood(good) || goods.Contains(good))
                return;

            goods.Add(good);
        }

        private static bool IsConfigurableStoredGood(eGoods good)
        {
            return good.IsGoodsyardGood() ||
                   good.IsGranaryFood() ||
                   good.IsArmouryGood() ||
                   good == eGoods.STORED_FOOD_ALE;
        }

        private static bool IsValidGoodCostTarget(BuildingGoodCostTarget target)
        {
            return target == BuildingGoodCostTarget.All ||
                target == BuildingGoodCostTarget.Human ||
                target == BuildingGoodCostTarget.AI;
        }

        private static bool IsVanillaConstructionGood(eGoods good)
        {
            return good == eGoods.STORED_WOOD_PLANKS ||
                good == eGoods.STORED_STONE_BLOCKS ||
                good == eGoods.STORED_IRON_INGOTS ||
                good == eGoods.STORED_PITCH_RAW ||
                good == eGoods.STORED_GOLD;
        }

        private static bool IsGoodCostTargetApplicable(BuildingGoodCostTarget target, bool isAI)
        {
            switch (target)
            {
                case BuildingGoodCostTarget.All:
                    return true;
                case BuildingGoodCostTarget.Human:
                    return !isAI;
                case BuildingGoodCostTarget.AI:
                    return isAI;
                default:
                    return false;
            }
        }

        private static void AddCost(Dictionary<eGoods, int> costs, eGoods good, int amount)
        {
            if (!IsValidGoodCost(good) || amount <= 0)
                return;

            costs[good] = amount;
        }

        private static Dictionary<eGoods, int> GetNativeConstructionCosts(eStructs building)
        {
            Dictionary<eGoods, int> costs = new Dictionary<eGoods, int>();
            AddCost(costs, eGoods.STORED_WOOD_PLANKS, GameBuildingManagerAPI.Instance.GetWoodCost(building));
            AddCost(costs, eGoods.STORED_STONE_BLOCKS, GameBuildingManagerAPI.Instance.GetStoneCost(building));
            AddCost(costs, eGoods.STORED_IRON_INGOTS, GameBuildingManagerAPI.Instance.GetIronIngotCost(building));
            AddCost(costs, eGoods.STORED_PITCH_RAW, GameBuildingManagerAPI.Instance.GetRawPitchCost(building));
            AddCost(costs, eGoods.STORED_GOLD, GameBuildingManagerAPI.Instance.GetGoldCost(building));
            return costs;
        }

        private static void RemoveGoodCostsForTarget(eStructs building, BuildingGoodCostTarget target)
        {
            if (!BuildingModdedGoodCosts.TryGetValue(building, out Dictionary<BuildingGoodCostKey, int> costs))
                return;

            List<BuildingGoodCostKey> costsToRemove = costs
                .Where(cost => cost.Key.Target == target)
                .Select(cost => cost.Key)
                .ToList();

            foreach (BuildingGoodCostKey cost in costsToRemove)
                costs.Remove(cost);

            if (costs.Count == 0)
                BuildingModdedGoodCosts.Remove(building);
        }

        private readonly struct BuildingGoodCostKey : IEquatable<BuildingGoodCostKey>
        {
            public readonly eGoods Good;
            public readonly BuildingGoodCostTarget Target;

            public BuildingGoodCostKey(eGoods good, BuildingGoodCostTarget target)
            {
                Good = good;
                Target = target;
            }

            public bool Equals(BuildingGoodCostKey other)
            {
                return Good == other.Good && Target == other.Target;
            }

            public override bool Equals(object obj)
            {
                return obj is BuildingGoodCostKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)Good * 397) ^ (int)Target;
                }
            }
        }
    }
}
