// Feature: Pure arithmetic for deterministic Lord health scaling.
using System;

namespace ExtraFeatures
{
    internal static class LordHealthMultiplierPolicy
    {
        public const int MinimumPercent = 10;
        public const int MaximumPercent = 500;
        public const int DefaultPercent = 100;

        public static int NormalizePercent(int percent) =>
            Math.Max(MinimumPercent, Math.Min(MaximumPercent, percent));

        public static uint CalculateVanillaMaximum(
            uint baseLordHealth,
            int aiHealthPercent,
            int enemyHealthPercent = DefaultPercent)
        {
            int normalizedAI = aiHealthPercent > 0 ? aiHealthPercent : DefaultPercent;
            uint aiScaled = Scale(baseLordHealth, normalizedAI);
            return Scale(aiScaled, enemyHealthPercent > 0 ? enemyHealthPercent : DefaultPercent);
        }

        public static uint CalculateMaximum(uint vanillaMaximum, int settingPercent) =>
            Scale(vanillaMaximum, NormalizePercent(settingPercent));

        public static uint CalculateCurrent(uint currentHealth, uint currentMaximum, uint targetMaximum)
        {
            if (targetMaximum == 0)
                return 1;
            if (currentMaximum == 0)
                return targetMaximum;

            ulong boundedCurrent = Math.Min((ulong)currentHealth, currentMaximum);
            ulong scaled = (boundedCurrent * targetMaximum + currentMaximum / 2UL) / currentMaximum;
            return (uint)Math.Max(1UL, Math.Min((ulong)targetMaximum, scaled));
        }

        public static ushort CalculateHealthPercent(uint currentHealth, uint maximumHealth)
        {
            if (maximumHealth == 0)
                return 0;

            ulong boundedCurrent = Math.Min((ulong)currentHealth, maximumHealth);
            ulong percent = (boundedCurrent * 100UL + maximumHealth / 2UL) / maximumHealth;
            return (ushort)Math.Min(100UL, percent);
        }

        private static uint Scale(uint value, int percent)
        {
            if (value == 0)
                return 1;

            ulong scaled = ((ulong)value * (ulong)Math.Max(1, percent) + 50UL) / 100UL;
            return (uint)Math.Max(1UL, Math.Min(uint.MaxValue, scaled));
        }
    }
}
