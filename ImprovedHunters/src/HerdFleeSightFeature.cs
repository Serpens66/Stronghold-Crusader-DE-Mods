using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;

namespace ImprovedHunters
{
    /// <summary>
    /// Deer and goat herds flee from any unit in their 10x10 sector, even behind a wall. Here a herd
    /// stays calm when no unit there can both see and reach it. The tick scan decides per herd, on
    /// simulation data only; a small native gate in each herd update reads the result. Predators in the sector
    /// remain Vanilla's reason to run, and being shot at does not pass through here. Vanilla counts a fleeing
    /// herd in the same counter as units (0x126810), so in crowded land herds keep each other running. A second
    /// setting takes that reason away. The two work alone or together, because the gate replaces the whole counter.
    /// </summary>
    internal sealed unsafe class HerdFleeSightFeature : IDisposable
    {
        private const int DeerHerdSiteRva = 0x124F20;
        private const string DeerHerdSitePattern =
            "8B 7C 08 28 03 7C 08 48 44 8B 7C 08 40 44 8B 64 08 44 66 44 89 AB F8 05 00 00 83 FA 1E 7E 17 " +
            "45 8D 45 1D 8B D5 48 8B CE E8 13 65 FF FF";
        private const int GoatHerdSiteRva = 0x125330;
        private const string GoatHerdSitePattern =
            "8B 7C 08 28 03 7C 08 48 44 8B 7C 08 40 44 8B 64 08 44 66 44 89 AB F8 05 00 00 83 FA 1E 7E 17 " +
            "45 8D 45 1D 8B D5 48 8B CE E8 03 61 FF FF";
        private const int SiteDisplacedBytes = 18;
        // lea rcx, [rip + grid] sits 0x26 bytes before the site; its operand starts three bytes in.
        private const int GridLeaOperandBeforeSite = 0x23;

        private const int HerdCount = 0x1194;
        private const int HerdFleeingOffset = 0x5F4;
        private const int SectorSize = 10;
        private const int SectorsPerSide = 80;
        private const int SectorStride = 0xA0;
        private const int SectorPredatorsOffset = 0x28;
        private const int SectorThreatsOffset = 0x48;
        private const int NonAnimalMarkerOffset = 0x328;

        private readonly WildlifeNativeAddresses addresses;
        private readonly HunterNativeVisibilityProbe visibility;
        private readonly PermanentNativeGate gate;
        private readonly byte* calmHerds;
        private readonly byte* grid;
        private readonly int[] herdSeen = new int[HerdCount];
        private readonly int[] herdList = new int[HerdCount];
        private readonly int[] sectorStamp = new int[SectorsPerSide * SectorsPerSide];
        private readonly int[] sectorFirstUnit = new int[SectorsPerSide * SectorsPerSide];
        private readonly int[] spookedStamp = new int[SectorsPerSide * SectorsPerSide];
        private int[] nextUnitInSector = new int[0];
        private int generation;
        private bool enabled;
        private bool sightRule;
        private bool ignoreFleeingHerds;

        public HerdFleeSightFeature(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches,
            WildlifeNativeAddresses addresses,
            HunterNativeVisibilityProbe visibility)
        {
            this.addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));
            this.visibility = visibility;
            int deer = Shared.NativePatternResolver.ResolveUnique(
                memory, DeerHerdSitePattern, DeerHerdSiteRva, referenceHashMatches, "deer herd threat read", log).Rva;
            int goats = Shared.NativePatternResolver.ResolveUnique(
                memory, GoatHerdSitePattern, GoatHerdSiteRva, referenceHashMatches, "goat herd threat read", log).Rva;
            grid = (byte*)(imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, deer - GridLeaOperandBeforeSite, deer - GridLeaOperandBeforeSite + 4));

            // Never freed: published stubs read it until the process ends.
            calmHerds = (byte*)Marshal.AllocHGlobal(HerdCount);
            new Span<byte>(calmHerds, HerdCount).Clear();
            ulong table = (ulong)calmHerds;

            PermanentNativeGate candidate = new PermanentNativeGate(region, imageBase);
            try
            {
                ulong flag = candidate.FlagAddress;
                candidate.AddSite(memory, deer, SiteDisplacedBytes, (a, i) => HunterNativeStubs.EmitHerdFleeGate(a, i, flag, table));
                candidate.AddSite(memory, goats, SiteDisplacedBytes, (a, i) => HunterNativeStubs.EmitHerdFleeGate(a, i, flag, table));
                candidate.Commit();
                gate = candidate;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters herd sight rule installed: deer=0x{deer:X}, goats=0x{goats:X}.");
        }

        public bool IsAvailable => gate?.IsInstalled == true;

        public void Configure(bool onlyInSight, bool ignoreFleeing)
        {
            sightRule = onlyInSight;
            ignoreFleeingHerds = ignoreFleeing;
            enabled = onlyInSight || ignoreFleeing;
            gate?.SetEnabled(enabled);
        }

        /// <summary>Vanilla's count of units and fleeing herds in the sector of a tile, or -1 outside the grid.</summary>
        public int SectorThreats(int x, int y)
        {
            int sectorX = x / SectorSize, sectorY = y / SectorSize;
            if (sectorX < 0 || sectorX >= SectorsPerSide || sectorY < 0 || sectorY >= SectorsPerSide)
                return -1;

            return *(int*)(grid + (sectorX * SectorsPerSide + sectorY) * SectorStride + SectorThreatsOffset);
        }

        public void ResetForMap() => new Span<byte>(calmHerds, HerdCount).Clear();

        public void Update(SimpleNativeArray<GameUnit> units)
        {
            if (!enabled)
                return;

            if (nextUnitInSector.Length < units.Length)
                nextUnitInSector = new int[units.Length];

            generation++;
            int herds = CollectHerdsAndUnits(units);
            for (int index = 0; index < herds; index++)
            {
                int herd = herdList[index];
                calmHerds[herd] = (byte)(IsHerdCalm(units, herd) ? 1 : 0);
            }
        }

        private int CollectHerdsAndUnits(SimpleNativeArray<GameUnit> units)
        {
            int herds = 0;
            byte* herdManager = (byte*)addresses.HerdManager;
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth == 0)
                    continue;

                int sector = SectorOf(unit);
                if (sector < 0)
                    continue;

                if (unit->r_UnitChimp == eChimps.CHIMP_TYPE_DEER || unit->r_UnitChimp == eChimps.CHIMP_TYPE_GOAT)
                {
                    int herd = unit->r_TribeId;
                    if (herd <= 0 || herd >= HerdCount)
                        continue;

                    if (herdSeen[herd] != generation)
                    {
                        herdSeen[herd] = generation;
                        herdList[herds++] = herd;
                    }

                    if (!ignoreFleeingHerds && *(short*)(herdManager + herd * WildlifeNativeAddresses.HerdStride + HerdFleeingOffset) != 0)
                        spookedStamp[sector] = generation;
                }
                else if (*((byte*)unit + NonAnimalMarkerOffset) == 0)
                {
                    if (sectorStamp[sector] != generation)
                    {
                        sectorStamp[sector] = generation;
                        sectorFirstUnit[sector] = -1;
                    }

                    nextUnitInSector[index] = sectorFirstUnit[sector];
                    sectorFirstUnit[sector] = index;
                }
            }

            return herds;
        }

        private bool IsHerdCalm(SimpleNativeArray<GameUnit> units, int herd)
        {
            byte* record = (byte*)addresses.HerdManager + herd * WildlifeNativeAddresses.HerdStride;
            int leaderId = *(short*)(record + WildlifeNativeAddresses.HerdLeaderOffset);
            if (leaderId <= 0 || leaderId > units.Length)
                return false;

            GameUnit* leader = units.GetValuePointer(leaderId - 1);
            int sector = SectorOf(leader);
            if (sector < 0 || *(int*)(grid + sector * SectorStride + SectorPredatorsOffset) != 0)
                return false;

            if (!ignoreFleeingHerds && spookedStamp[sector] == generation)
                return false;

            if (sectorStamp[sector] != generation)
                return true;

            // Without the sight rule any unit in the sector counts, as in Vanilla.
            if (!sightRule)
                return false;

            for (int index = sectorFirstUnit[sector]; index >= 0; index = nextUnitInSector[index])
            {
                if (SeesAndReaches(units.GetValuePointer(index), leader))
                    return false;
            }

            return true;
        }

        private bool SeesAndReaches(GameUnit* unit, GameUnit* herdLeader)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int unitTile = tiles.GetTileId(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
            int herdTile = tiles.GetTileId(herdLeader->r_CurrentTilePositionX, herdLeader->r_CurrentTilePositionY);
            if (!tiles.IsValidTileId(unitTile) || !tiles.IsValidTileId(herdTile) ||
                (uint)unitTile >= (uint)components.Length || (uint)herdTile >= (uint)components.Length)
            {
                return true;
            }

            if (components[unitTile] != components[herdTile] &&
                GamePathingManagerAPI.Instance.FindNextComponentTowardDestination(
                    unit->r_ControllableForPlayerId,
                    components[unitTile],
                    components[herdTile],
                    (PathConnectionQueryMode)unit->r_PathConnectionMode) == 0)
            {
                return false;
            }

            // Without a sight check connected units count, which keeps Vanilla's flight.
            return visibility?.IsAvailable != true || visibility.SeesFromUnit(unit, herdLeader);
        }

        private static int SectorOf(GameUnit* unit)
        {
            int sectorX = unit->r_CurrentTilePositionX / SectorSize, sectorY = unit->r_CurrentTilePositionY / SectorSize;
            if (sectorX < 0 || sectorX >= SectorsPerSide || sectorY < 0 || sectorY >= SectorsPerSide)
                return -1;

            return sectorX * SectorsPerSide + sectorY;
        }

        public void Dispose() => gate?.Dispose();
    }
}
