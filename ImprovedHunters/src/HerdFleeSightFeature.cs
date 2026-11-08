using APIShared.Wildlife;
using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// Deer and goat herds flee from any unit in their 10x10 sector, even behind a wall. Here a herd
    /// stays calm when no unit there can both see and reach it. The tick scan decides per herd, on
    /// simulation data only, and marks calm herds through APIShared's HerdFlightRules, whose native gate in
    /// each herd update reads the mark. Predators in the sector remain Vanilla's reason to run, and being shot
    /// at does not pass through here. Vanilla counts a fleeing herd in the same counter as units (0x126810), so in
    /// crowded land herds keep each other running. A second setting takes that reason away. The two work alone or
    /// together, because a calm mark replaces the whole counter.
    /// </summary>
    internal sealed unsafe class HerdFleeSightFeature
    {
        private const int HerdCount = WildlifeOriginTable.HerdCapacity;
        private const int SectorSize = 10;
        private const int SectorsPerSide = 80;
        private const int NonAnimalMarkerOffset = 0x328;

        private readonly HunterNativeVisibilityProbe visibility;
        private readonly HerdFlightRule rule;
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

        public HerdFleeSightFeature(ManualLogSource log, HunterNativeVisibilityProbe visibility)
        {
            this.visibility = visibility;
            // Process-lived; registered during startup, when APIShared can still install its gate.
            if (!HerdFlightRules.TryRegister(ImprovedHuntersPlugin.PluginGuid, "herd-sight", out rule, out string reason))
                throw new InvalidOperationException(reason);

            Shared.DebugLogHelper.LogInfo(log, "Improved Hunters herd sight rule registered with APIShared's herd flight rules.");
        }

        public void Configure(bool onlyInSight, bool ignoreFleeing)
        {
            sightRule = onlyInSight;
            ignoreFleeingHerds = ignoreFleeing;
            enabled = onlyInSight || ignoreFleeing;
            if (!enabled)
                rule.ClearAll();
        }

        /// <summary>Vanilla's count of units and fleeing herds in the sector of a tile, or -1 outside the grid.</summary>
        public int SectorThreats(int x, int y) =>
            HerdFlightRules.TryGetSectorThreats(x, y, out HerdSectorThreats threats) ? threats.Units : -1;

        // Simulation tick: the marks change on all peers in the same tick.
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
                rule.TrySetCalm(herd, IsHerdCalm(units, herd));
            }
        }

        private int CollectHerdsAndUnits(SimpleNativeArray<GameUnit> units)
        {
            int herds = 0;
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

                    if (!ignoreFleeingHerds && Herds.TryGet(herd, out HerdInfo info) && info.IsFleeing)
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
            if (!Herds.TryGet(herd, out HerdInfo info))
                return false;

            int leaderId = info.LeaderUnitId;
            if (leaderId <= 0 || leaderId > units.Length)
                return false;

            GameUnit* leader = units.GetValuePointer(leaderId - 1);
            int sector = SectorOf(leader);
            if (sector < 0 || !HerdFlightRules.TryGetSectorThreats(leader->r_CurrentTilePositionX, leader->r_CurrentTilePositionY, out HerdSectorThreats threats) ||
                threats.Predators != 0)
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
    }
}
