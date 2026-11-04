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
            // Copies: Maker sidecar slots keep their original IDs while the playable Coop
            // file receives the compact numbering.
            return slots.Where(slot => activeSlots.IndexOf(slot.PlayerId - 1) >= 2)
                .Select(slot => new TrailLordSlot
                {
                    PlayerId = activeSlots.IndexOf(slot.PlayerId - 1) + 1,
                    LordType = slot.LordType,
                    LordName = slot.LordName,
                    Presentation = slot.Presentation,
                    ConfigName = slot.ConfigName,
                    ConfigChecksum = slot.ConfigChecksum,
                    AivChecksums = slot.AivChecksums?.ToArray(),
                    RequiresInstalledPackage = slot.RequiresInstalledPackage,
                    PackageDigest = slot.PackageDigest,
                    ModLordJson = slot.ModLordJson,
                    FixesJson = slot.FixesJson,
                    FixesSources = slot.FixesSources,
                    EmbeddedValuesOnly = slot.EmbeddedValuesOnly,
                }).ToArray();
        }
    }
}
