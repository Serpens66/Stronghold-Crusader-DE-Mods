// Feature: Keep the Lord visible as its own entry in Vanilla's four control-group summary slots.
using System;

namespace BugfixesAndQoL
{
    internal static class LordControlGroupIconPolicy
    {
        internal const int EuropeanArcherSummaryType = 0;
        internal const int LordVisualType = -1;
        internal const int VisibleSlotCount = 4;

        internal static bool IsGroupMutationCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return false;

            return HasGroupSuffix(command, "Add_") ||
                HasGroupSuffix(command, "Create_") ||
                HasGroupSuffix(command, "Delete_");
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

        private static bool HasGroupSuffix(string command, string prefix) =>
            command.Length == prefix.Length + 1 &&
            command.StartsWith(prefix, StringComparison.Ordinal) &&
            command[command.Length - 1] >= '0' &&
            command[command.Length - 1] <= '9';
    }
}
