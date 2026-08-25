// Feature: Defines the tradeable goods and validates their individual price multipliers.
using SHCDESE.Interop;
using System;

namespace ExtraFeatures
{
    internal static class MarketGoodPriceDefinition
    {
        private static readonly int[] TradeableGoodsOrder =
        {
            (int)eGoods.STORED_FOOD_MEAT,
            (int)eGoods.STORED_FOOD_CHEESE,
            (int)eGoods.STORED_FOOD_FRUIT,
            (int)eGoods.STORED_FOOD_BREAD,
            (int)eGoods.STORED_RAW_WHEAT,
            (int)eGoods.STORED_FLOUR,
            (int)eGoods.STORED_WOOD_PLANKS,
            (int)eGoods.STORED_STONE_BLOCKS,
            (int)eGoods.STORED_IRON_INGOTS,
            (int)eGoods.STORED_PITCH_REFINED,
            (int)eGoods.STORED_SPEARS,
            (int)eGoods.STORED_BOWS,
            (int)eGoods.STORED_MACES,
            (int)eGoods.STORED_CROSSBOWS,
            (int)eGoods.STORED_PIKES,
            (int)eGoods.STORED_SWORDS,
            (int)eGoods.STORED_LEATHER_ARMOUR,
            (int)eGoods.STORED_METAL_ARMOUR,
            (int)eGoods.STORED_FOOD_ALE,
            (int)eGoods.STORED_RAW_HOPS
        };

        public const double MinimumMultiplier = 0.0;
        public const double MaximumMultiplier = 5.0;

        public static int Count => TradeableGoodsOrder.Length;

        public static int GetGood(int index) => TradeableGoodsOrder[index];

        public static int FindGoodIndex(int good) => Array.IndexOf(TradeableGoodsOrder, good);

        public static double[] CreateDefaultMultipliers()
        {
            double[] multipliers = new double[Count];
            for (int index = 0; index < multipliers.Length; index++)
                multipliers[index] = 1.0;
            return multipliers;
        }

        public static double[] NormalizeMultipliers(double[] multipliers)
        {
            if (multipliers == null || multipliers.Length != Count)
                return CreateDefaultMultipliers();

            double[] normalized = new double[Count];
            for (int index = 0; index < normalized.Length; index++)
                normalized[index] = NormalizeMultiplier(multipliers[index]);
            return normalized;
        }

        public static double NormalizeMultiplier(double multiplier)
        {
            if (double.IsNaN(multiplier) || double.IsInfinity(multiplier))
                return 1.0;

            return Math.Max(
                MinimumMultiplier,
                Math.Min(
                    MaximumMultiplier,
                    Math.Round(multiplier, 1, MidpointRounding.AwayFromZero)));
        }

        public static double CombineMultipliers(double generalMultiplier, double goodMultiplier) =>
            generalMultiplier * NormalizeMultiplier(goodMultiplier);
    }
}
