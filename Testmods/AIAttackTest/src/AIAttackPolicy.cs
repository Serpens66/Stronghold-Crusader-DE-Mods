using System;
using System.Collections.Generic;

namespace AIAttackTest
{
    internal static class AIAttackPolicy
    {
        internal const string VanillaMode = "Vanilla";
        internal const string RelativeMode = "Relative";
        internal const int MinimumGrowthPercent = 0;
        internal const int MaximumGrowthPercent = 300;
        internal const int MinimumDefenseMonths = 0;
        internal const int MaximumDefenseMonths = 30;
        internal const int SimulationTicksPerMonth = 800;

        internal static int ClampGrowthPercent(int value) =>
            Math.Max(MinimumGrowthPercent, Math.Min(MaximumGrowthPercent, value));

        internal static int ClampDefenseMonths(int value) =>
            Math.Max(MinimumDefenseMonths, Math.Min(MaximumDefenseMonths, value));

        internal static int CalculateNormalWaveMultiplier(int siegeTriggerLevel, int percent)
        {
            if (siegeTriggerLevel <= 0)
                return 0;

            long product = checked((long)siegeTriggerLevel * ClampGrowthPercent(percent));
            return checked((int)RoundHalfUp(product, 100));
        }

        internal static int CalculateHighGoldWaveMultiplier(int normalWaveMultiplier)
        {
            if (normalWaveMultiplier <= 0)
                return 0;

            return checked((int)RoundHalfUp(checked((long)normalWaveMultiplier * 7), 5));
        }

        internal static int CalculateInitialDefenseTicks(int months) =>
            checked(ClampDefenseMonths(months) * SimulationTicksPerMonth);

        internal static int[] ResolveUniqueAicIndices(IEnumerable<int> lordValues, int aicCount)
        {
            var unique = new SortedSet<int>();
            if (lordValues == null || aicCount <= 1)
                return Array.Empty<int>();

            foreach (int lordValue in lordValues)
            {
                if (lordValue > 0 && lordValue < aicCount)
                    unique.Add(lordValue);
            }
            int[] result = new int[unique.Count];
            unique.CopyTo(result);
            return result;
        }

        private static long RoundHalfUp(long nonNegativeValue, int divisor)
        {
            if (nonNegativeValue < 0)
                throw new ArgumentOutOfRangeException(nameof(nonNegativeValue));
            if (divisor <= 0)
                throw new ArgumentOutOfRangeException(nameof(divisor));
            return checked((nonNegativeValue + divisor / 2) / divisor);
        }
    }
}
