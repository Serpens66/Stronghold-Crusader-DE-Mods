// Feature: Aggregate and page selected-unit health without UI or game-state dependencies.
using SHCDESE.Interop;
using System;

namespace BugfixesAndQoL
{
    internal enum SelectedUnitHealthBand
    {
        Red,
        Yellow,
        Green
    }

    internal struct SelectedUnitHealthSummary
    {
        private const int DisplayScale = 10;

        public long CurrentHealth { get; private set; }
        public long MaximumHealth { get; private set; }
        public int UnitCount { get; private set; }

        public void Add(long currentHealth, long maximumHealth)
        {
            if (maximumHealth <= 0 || currentHealth < 0)
                return;

            CurrentHealth += currentHealth;
            MaximumHealth += maximumHealth;
            UnitCount++;
        }

        public bool HasUnits => UnitCount > 0;

        public SelectedUnitHealthBand Band
        {
            get
            {
                if (!HasUnits || MaximumHealth <= 0)
                    return SelectedUnitHealthBand.Red;
                if (CurrentHealth * 100 >= MaximumHealth * 75)
                    return SelectedUnitHealthBand.Green;
                if (CurrentHealth * 100 >= MaximumHealth * 40)
                    return SelectedUnitHealthBand.Yellow;
                return SelectedUnitHealthBand.Red;
            }
        }

        public string FormatCurrent() => ScaleForDisplay(CurrentHealth).ToString();

        public string FormatMaximum() => ScaleForDisplay(MaximumHealth).ToString();

        internal static long ScaleForDisplay(long health) =>
            (long)Math.Round(health / (double)DisplayScale, MidpointRounding.AwayFromZero);
    }

    internal static class SelectedUnitHealthPageLayout
    {
        internal const int SlotCount = 8;

        internal static int GetPageCount(int[] selectedTypeCounts)
        {
            int typeCount = CountVisibleTypes(selectedTypeCounts);
            return Math.Max(1, (typeCount + SlotCount - 1) / SlotCount);
        }

        internal static int ClampPage(int currentPage, int[] selectedTypeCounts) =>
            Math.Max(0, Math.Min(currentPage, GetPageCount(selectedTypeCounts) - 1));

        internal static int CountVisibleTypes(int[] selectedTypeCounts)
        {
            if (selectedTypeCounts == null)
                return 0;

            int result = 0;
            for (int type = 0; type < selectedTypeCounts.Length; type++)
            {
                if (selectedTypeCounts[type] > 0)
                    result++;
            }
            return result;
        }

        internal static int[] GetVisibleTypes(int[] selectedTypeCounts, int currentPage)
        {
            var result = new int[SlotCount];
            FillVisibleTypes(selectedTypeCounts, currentPage, result);
            return result;
        }

        internal static void FillVisibleTypes(
            int[] selectedTypeCounts,
            int currentPage,
            int[] destination,
            int excludedType = -1)
        {
            if (destination == null || destination.Length < SlotCount)
                throw new ArgumentException($"A destination with at least {SlotCount} slots is required.", nameof(destination));

            for (int i = 0; i < destination.Length; i++)
                destination[i] = -1;

            if (selectedTypeCounts == null)
                return;

            int firstOrdinal = Math.Max(0, currentPage) * SlotCount;
            int selectedOrdinal = 0;
            int slot = 0;
            for (int type = 0; type < selectedTypeCounts.Length && slot < SlotCount; type++)
            {
                if (type == excludedType || selectedTypeCounts[type] <= 0)
                    continue;

                if (selectedOrdinal++ < firstOrdinal)
                    continue;

                destination[slot++] = type;
            }
        }
    }
}
