using BepInEx.Logging;
using RedBird.Core.Memory;
using System;
using System.Runtime.InteropServices;

namespace ImprovedHunters
{
    /// <summary>
    /// Lets a Hunter go for a free corpse the tick scan chose for him. Vanilla only knows this for deer
    /// and goats, and only right after a shot; it then points him at the corpse and walks there. Both places
    /// here use that same Vanilla code, so there is no second pickup path. The stubs check the choice again
    /// when they use it and reserve the corpse at once, so two Hunters cannot take the same one.
    /// </summary>
    internal sealed unsafe class HunterCorpseChoicePatch : IDisposable
    {
        // After Vanilla's search and its check that the hut still stands, ahead of its "found prey" branch.
        // The two hooks of HunterTargetSearchFallbackDiagnostic sit directly before this and must stay clear.
        private const int SearchSiteRva = 0x12FE07;
        private const string SearchSitePattern =
            "85 C0 0F 84 ?? ?? ?? ?? 48 63 15 ?? ?? ?? ?? 41 BE 02 00 00 00 48 69 CA 90 04 00 00";
        private const int SearchDisplaced = 15;
        private const int SearchNoPreyOperand = 4;
        private const int SearchCurrentHunterOperand = 11;

        private const int AfterShotSiteRva = 0x13053A;
        private const string AfterShotSitePattern =
            "8B D3 49 8B CD E8 ?? ?? ?? ?? 85 C0 74 74 48 63 1D ?? ?? ?? ?? 48 69 D3 90 04 00 00 66 42 89 84 2A 9C 09 00 00";
        private const int AfterShotDisplaced = 14;
        private const int AfterShotCallOperand = 6;
        private const int AfterShotBranchOperand = 13;

        private const int UnitCapacity = 10000;
        private const int EntrySize = HunterNativeStubs.CorpseEntrySize;
        private const int GlobalIdOffset = HunterNativeStubs.CorpseEntryGlobalId;
        private const int UnitIdOffset = HunterNativeStubs.CorpseEntryUnitId;

        private readonly PermanentNativeGate gate;
        private readonly byte* entries;

        public HunterCorpseChoicePatch(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            if (!referenceHashMatches)
                throw new InvalidOperationException("The corpse choice is bound to the audited native hash.");

            int search = Shared.NativePatternResolver.ResolveUnique(
                memory, SearchSitePattern, SearchSiteRva, referenceHashMatches, "Hunter target search", log).Rva;
            int afterShot = Shared.NativePatternResolver.ResolveUnique(
                memory, AfterShotSitePattern, AfterShotSiteRva, referenceHashMatches, "Hunter corpse search", log).Rva;

            ulong noPrey = imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, search + SearchNoPreyOperand, search + SearchNoPreyOperand + 4);
            ulong currentHunter = imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, search + SearchCurrentHunterOperand, search + SearchCurrentHunterOperand + 4);
            ulong corpseSearch = imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, afterShot + AfterShotCallOperand, afterShot + AfterShotCallOperand + 4);
            ulong noCorpse = imageBase + (ulong)(afterShot + AfterShotBranchOperand + 1 + (sbyte)memory[afterShot + AfterShotBranchOperand]);
            ulong walkToCorpse = imageBase + (ulong)(afterShot + AfterShotDisplaced);

            // Never freed: published stubs read it until the process ends.
            entries = (byte*)Marshal.AllocHGlobal(UnitCapacity * EntrySize);
            Clear();
            ulong table = (ulong)entries;

            PermanentNativeGate candidate = new PermanentNativeGate(region, imageBase);
            try
            {
                ulong flag = candidate.FlagAddress;
                candidate.AddSite(memory, search, SearchDisplaced,
                    (a, i) => HunterNativeStubs.EmitCorpseChoiceInSearch(a, i, flag, table, currentHunter, noPrey, walkToCorpse));
                candidate.AddSite(memory, afterShot, AfterShotDisplaced,
                    (a, i) => HunterNativeStubs.EmitCorpseChoiceAfterShot(a, i, flag, table, corpseSearch, noCorpse));
                candidate.Commit();
                gate = candidate;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters corpse choice installed: search=0x{search:X}, afterShot=0x{afterShot:X}.");
        }

        public bool IsAvailable => gate?.IsInstalled == true;

        public void SetEnabled(bool enabled) => gate?.SetEnabled(enabled);

        public void Clear() => new Span<byte>(entries, UnitCapacity * EntrySize).Clear();

        public bool HasChoice(int hunterUnitId) =>
            hunterUnitId > 0 && hunterUnitId < UnitCapacity && *(ushort*)(entries + hunterUnitId * EntrySize + UnitIdOffset) != 0;

        public bool TryGetChoice(int hunterUnitId, out int corpseUnitId, out uint corpseGlobalId)
        {
            corpseUnitId = 0;
            corpseGlobalId = 0;
            if (!HasChoice(hunterUnitId))
                return false;

            byte* entry = entries + hunterUnitId * EntrySize;
            corpseUnitId = *(ushort*)(entry + UnitIdOffset);
            corpseGlobalId = *(uint*)(entry + GlobalIdOffset);
            return true;
        }

        public void Set(int hunterUnitId, int corpseUnitId, uint corpseGlobalId)
        {
            if (hunterUnitId <= 0 || hunterUnitId >= UnitCapacity || corpseUnitId < 0 || corpseUnitId > ushort.MaxValue)
                return;

            byte* entry = entries + hunterUnitId * EntrySize;
            *(uint*)(entry + GlobalIdOffset) = corpseGlobalId;
            *(ushort*)(entry + UnitIdOffset) = (ushort)corpseUnitId;
        }

        public void Dispose() => gate?.Dispose();
    }
}
