// Feature: Keep the Lord visible as its own entry in Vanilla's four control-group summary slots.
using System;

namespace BugfixesAndQoL
{
    internal static class LordControlGroupIconPolicy
    {
        internal const int EuropeanArcherSummaryType = 0;
        internal const int LordVisualType = -1;
        internal const int VisibleSlotCount = 4;
        internal const int SummaryTypeCount = 34;
        internal const int EmptySummaryType = 99;

        internal static bool TryGetSummaryType(int unitType, out int summaryType)
        {
            // This is the exact 34-class dispatch used by Vanilla's native group
            // summary builder. The Lord shares class zero with European Archers.
            if (unitType == 5)
                summaryType = 33;
            else if (unitType >= 0x16 && unitType <= 0x1E)
                summaryType = unitType - 0x16;
            else if (unitType == 0x25)
                summaryType = 9;
            else if (unitType >= 0x27 && unitType <= 0x29)
                summaryType = unitType - 0x27 + 10;
            else if (unitType == 0x37)
                summaryType = EuropeanArcherSummaryType;
            else if (unitType >= 0x3A && unitType <= 0x3D)
                summaryType = unitType - 0x3A + 13;
            else if (unitType >= 0x46 && unitType <= 0x55)
                summaryType = unitType - 0x46 + 17;
            else
            {
                summaryType = EmptySummaryType;
                return false;
            }

            return true;
        }

        internal static void SelectVisibleSummary(
            int[] categoryCounts,
            int[] visibleTypes,
            int[] visibleCounts)
        {
            if (categoryCounts == null)
                throw new ArgumentNullException(nameof(categoryCounts));
            if (visibleTypes == null)
                throw new ArgumentNullException(nameof(visibleTypes));
            if (visibleCounts == null)
                throw new ArgumentNullException(nameof(visibleCounts));
            if (categoryCounts.Length != SummaryTypeCount ||
                visibleTypes.Length != VisibleSlotCount ||
                visibleCounts.Length != VisibleSlotCount)
            {
                throw new ArgumentException("The control-group summary dimensions differ from Vanilla.");
            }

            var selected = new bool[SummaryTypeCount];
            for (int slot = 0; slot < VisibleSlotCount; slot++)
            {
                int bestType = EmptySummaryType;
                int bestCount = 0;
                for (int type = 0; type < SummaryTypeCount; type++)
                {
                    // Strictly-greater replacement preserves Vanilla's lower-type
                    // tie breaker because the classes are visited in ascending order.
                    if (!selected[type] && categoryCounts[type] > bestCount)
                    {
                        bestType = type;
                        bestCount = categoryCounts[type];
                    }
                }

                visibleTypes[slot] = bestType;
                visibleCounts[slot] = bestCount;
                if (bestType != EmptySummaryType)
                    selected[bestType] = true;
            }
        }

        internal static void InsertLord(int[] types, int[] counts)
        {
            if (types == null)
                throw new ArgumentNullException(nameof(types));
            if (counts == null)
                throw new ArgumentNullException(nameof(counts));
            if (types.Length != VisibleSlotCount || counts.Length != VisibleSlotCount)
                throw new ArgumentException("A control-group summary must contain exactly four slots.");

            int archerSlot = -1;
            for (int slot = 0; slot < VisibleSlotCount; slot++)
            {
                if (counts[slot] > 0 && types[slot] == EuropeanArcherSummaryType)
                {
                    archerSlot = slot;
                    break;
                }
            }

            // Native temporarily counts the Lord as an Archer. Split that shared count
            // without changing the underlying group or Vanilla's ordering of other types.
            if (archerSlot >= 0 && counts[archerSlot] == 1)
            {
                types[archerSlot] = LordVisualType;
                return;
            }
            if (archerSlot >= 0)
                counts[archerSlot]--;

            int lordSlot = -1;
            for (int slot = 0; slot < VisibleSlotCount; slot++)
            {
                if (counts[slot] <= 0)
                {
                    lordSlot = slot;
                    break;
                }
            }

            // Vanilla orders the four visible classes by count, so the final slot is the
            // least significant one when all four are occupied. Its units remain in +N.
            if (lordSlot < 0)
                lordSlot = VisibleSlotCount - 1;

            types[lordSlot] = LordVisualType;
            counts[lordSlot] = 1;
        }

        internal static int CalculateExtraCount(int total, int[] visibleCounts)
        {
            if (visibleCounts == null)
                throw new ArgumentNullException(nameof(visibleCounts));
            if (visibleCounts.Length != VisibleSlotCount)
                throw new ArgumentException("A control-group summary must contain exactly four slots.");

            int visible = 0;
            for (int slot = 0; slot < VisibleSlotCount; slot++)
                visible = checked(visible + Math.Max(visibleCounts[slot], 0));
            return Math.Max(total - visible, 0);
        }
    }
}
