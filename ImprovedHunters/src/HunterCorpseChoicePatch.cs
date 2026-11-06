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
        private const int SearchSiteRva = 0x12FDB7;
        private const string SearchSitePattern =
            "8B D3 49 8B CD E8 ?? ?? ?? ?? 49 0F BF 8C 3E FC 01 00 00 48 69 D1 90 04 00 00";
        private const int SearchDisplaced = 19;
        private const int SearchCallOperand = 6;

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

            ulong liveSearch = imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, search + SearchCallOperand, search + SearchCallOperand + 4);
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
                    (a, i) => HunterNativeStubs.EmitCorpseChoiceInSearch(a, i, flag, table, liveSearch, walkToCorpse));
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
