// Feature: Defines the tradeable goods and validates their individual price multipliers.
using System;

namespace ExtraFeatures
{
    internal static class MarketGoodPriceDefinition
    {
        private static readonly int[] TradeableGoodsOrder =
        {
            12, 11, 13, 10, 9, 16, 2, 4, 6, 8,
            19, 17, 21, 18, 20, 22, 23, 24, 14, 3
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
