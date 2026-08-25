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
        internal static int[] GetVisibleTypes(int[] selectedTypeCounts, int currentPage)
        {
            var result = new int[SlotCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = -1;

            if (selectedTypeCounts == null)
                return result;

            // These page starts deliberately mirror HUD_Troops.SetupSelectedTroops exactly.
            int firstOrdinal = currentPage <= 0 ? 0 : 8 + ((currentPage - 1) * 9);
            int selectedOrdinal = 0;
            int slot = 0;
            for (int type = 0; type < selectedTypeCounts.Length && slot < SlotCount; type++)
            {
                if (type == (int)eChimps.CHIMP_TYPE_LORD || selectedTypeCounts[type] <= 0)
                    continue;

                if (selectedOrdinal++ < firstOrdinal)
                    continue;

                result[slot++] = type;
            }

            return result;
        }
    }
}
