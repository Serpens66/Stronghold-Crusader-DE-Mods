using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData.Core
{
    internal static class CoopTrailSlotSelection
    {
        internal static List<int> GetActiveSlots(IReadOnlyList<int> savedMembers)
        {
            // RestartSkirmishMapInfo.importMembers uses -9999 for an unoccupied slot.
            var slots = Enumerable.Range(0, Math.Min(8, savedMembers?.Count ?? 0))
                .Where(index => savedMembers[index] != -9999).ToList();
            if (slots.Count < 2)
                throw new InvalidDataException("A Coop mission needs at least two occupied player slots.");
            return slots;
        }

        internal static ISet<int> GetAiPlayerIds(IReadOnlyList<int> activeSlots) =>
            new HashSet<int>(activeSlots.Skip(2).Select(index => index + 1));

        internal static IReadOnlyList<TrailLordSlot> RemapForCoop(
            IReadOnlyList<TrailLordSlot> slots, List<int> activeSlots)
        {
            return slots.Where(slot => activeSlots.IndexOf(slot.PlayerId - 1) >= 2)
                .Select(slot =>
                {
                    slot.PlayerId = activeSlots.IndexOf(slot.PlayerId - 1) + 1;
                    return slot;
                }).ToArray();
        }
    }
}
