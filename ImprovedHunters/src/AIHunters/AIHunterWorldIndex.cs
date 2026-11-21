using System;
using System.Collections.Generic;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace ImprovedHunters
{
    internal readonly struct AIHunterHutRecord
    {
        internal readonly int Id, Owner, X, Y;
        internal readonly uint GlobalId;
        internal AIHunterHutRecord(int id, in GameBuilding building)
        { Id = id; Owner = building.r_PlayerIdOwner; GlobalId = building.r_GlobalId; X = building.r_TilePositionXBegin; Y = building.r_TilePositionYBegin; }
    }

    // One building traversal collects all players together. Retain IDs only, never native pointers.
    internal sealed class AIHunterWorldIndex
    {
        internal const int PlayerSlots = GamePlayerManagerAPI.MAX_PLAYERS + 1;
        internal readonly List<AIHunterHutRecord> Huts = new List<AIHunterHutRecord>(64);
        internal readonly List<AIHunterHutRecord> SpawnBuildings = new List<AIHunterHutRecord>(32);
        internal readonly uint[] Baseline = new uint[PlayerSlots];
        internal readonly bool[] AI = new bool[PlayerSlots];
        private readonly int[] granaryId = new int[PlayerSlots], granaryX = new int[PlayerSlots], granaryY = new int[PlayerSlots];
        private readonly bool[] granaryKnown = new bool[PlayerSlots];
        private readonly long[] bestDistance = new long[PlayerSlots];
        private readonly Dictionary<uint, AIHunterHutRecord> hutGlobals = new Dictionary<uint, AIHunterHutRecord>();
        private readonly HashSet<uint> spawnGlobals = new HashSet<uint>();
        internal readonly HashSet<uint> BusyHuts = new HashSet<uint>();

        internal void Clear()
        {
            Huts.Clear(); SpawnBuildings.Clear(); hutGlobals.Clear(); spawnGlobals.Clear(); BusyHuts.Clear();
            Array.Clear(Baseline, 0, Baseline.Length); Array.Clear(AI, 0, AI.Length);
            Array.Clear(granaryId, 0, granaryId.Length); Array.Clear(granaryKnown, 0, granaryKnown.Length);
        }
        internal void SetPlayer(int player, bool ai, int firstGranary)
        {
            if (granaryId[player] != firstGranary) { granaryKnown[player] = false; Baseline[player] = 0; }
            AI[player] = ai; granaryId[player] = firstGranary;
        }

        internal void ObserveBuilding(int id, in GameBuilding building)
        {
            int owner = building.r_PlayerIdOwner;
            if (owner < 1 || owner >= PlayerSlots || !AI[owner]) return;
            if (building.r_GlobalId != 0 && IsSpawnType(building.r_BuildingType) &&
                (building.r_AliveState == AliveState.IsAlive || building.r_AliveState == AliveState.NeedsInit) && spawnGlobals.Add(building.r_GlobalId))
                SpawnBuildings.Add(new AIHunterHutRecord(id, in building));
            // The spawn observer sees NeedsInit before the first live tick; its coordinates are
            // already final. Keep the nearest-hut ranking valid across that transition.
            if (id == granaryId[owner] && building.r_BuildingType == eStructs.STRUCT_GRANARY &&
                (building.r_AliveState == AliveState.IsAlive || building.r_AliveState == AliveState.NeedsInit))
            {
                if (!granaryKnown[owner] || granaryX[owner] != building.r_TilePositionXBegin || granaryY[owner] != building.r_TilePositionYBegin)
                    Baseline[owner] = 0;
                granaryKnown[owner] = true; granaryX[owner] = building.r_TilePositionXBegin; granaryY[owner] = building.r_TilePositionYBegin;
            }
            if (building.r_GlobalId != 0 && HunterHutPolicy.IsHut(in building, owner) && !hutGlobals.ContainsKey(building.r_GlobalId))
            {
                var record = new AIHunterHutRecord(id, in building); hutGlobals.Add(building.r_GlobalId, record); Huts.Add(record);
                Baseline[owner] = 0;
            }
        }
        internal static bool IsSpawnType(eStructs type) => type == eStructs.STRUCT_GRANARY || type == eStructs.STRUCT_MILL || type == eStructs.STRUCT_BAKERS_WORKSHOP;
        internal bool TryGetHut(uint global, out AIHunterHutRecord hut) => hutGlobals.TryGetValue(global, out hut);

        internal void SelectBaselines()
        {
            Array.Clear(Baseline, 0, Baseline.Length);
            for (int p = 1; p < PlayerSlots; p++) bestDistance[p] = long.MaxValue;
            for (int i = 0; i < Huts.Count; i++)
            {
                var hut = Huts[i]; int owner = hut.Owner;
                long dx = granaryKnown[owner] ? hut.X - granaryX[owner] : 0;
                long dy = granaryKnown[owner] ? hut.Y - granaryY[owner] : 0;
                long distance = dx * dx + dy * dy;
                if (distance < bestDistance[owner] || (distance == bestDistance[owner] && (Baseline[owner] == 0 || hut.GlobalId < Baseline[owner])))
                { bestDistance[owner] = distance; Baseline[owner] = hut.GlobalId; }
            }
        }

        internal long Distance(in AIHunterHutRecord hut)
        {
            long dx = granaryKnown[hut.Owner] ? hut.X - granaryX[hut.Owner] : 0;
            long dy = granaryKnown[hut.Owner] ? hut.Y - granaryY[hut.Owner] : 0;
            return dx * dx + dy * dy;
        }

        internal void ObserveWorker(in GameUnit unit)
        {
            if (unit.r_AliveState != AliveState.None && unit.r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER &&
                unit.r_LinkedProductionBuildingGlobalId != 0 && !HunterRetirementNative.IsIdleAndEmpty(in unit))
                BusyHuts.Add(unit.r_LinkedProductionBuildingGlobalId);
        }
    }
}
