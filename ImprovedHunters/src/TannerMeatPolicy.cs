using System;

namespace ImprovedHunters
{
    /// <summary>
    /// How a Tannery divides the three hides of a cow. The first runs make what the workshop
    /// normally makes, the last ones carry meat to a granary.
    /// </summary>
    internal static class TannerMeatPolicy
    {
        // Vanilla's slaughter step in 0x13E0B0 adds exactly this many hides.
        public const int HidesPerCow = 3;
        public const int DefaultMeatPerTrip = 5;
        public const int MinimumMeatPerTrip = 1;
        public const int MaximumMeatPerTrip = 100;

        public static int ClampMeatPerTrip(int value) =>
            Math.Max(MinimumMeatPerTrip, Math.Min(MaximumMeatPerTrip, value));

        public static bool IsValidMeatRuns(int value) => value >= 0 && value <= HidesPerCow;

        /// <summary>The panel's slider runs from only meat on the left to no meat on the right.</summary>
        public static int SliderValueFromMeatRuns(int meatRuns) => HidesPerCow - meatRuns;

        public static int MeatRunsFromSliderValue(int value) =>
            HidesPerCow - Math.Max(0, Math.Min(HidesPerCow, value));

        /// <summary>A run carries meat while fewer hides than meat runs remain after it took its own hide.</summary>
        public static bool RunCarriesMeat(int remainingHides, int meatRuns) => remainingHides < meatRuns;
    }
}
