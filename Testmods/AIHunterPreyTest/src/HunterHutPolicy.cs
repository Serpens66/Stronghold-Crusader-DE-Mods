using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIHunterPreyTest
{
    internal static class HunterHutPolicy
    {
        internal static bool IsHut(in GameBuilding building, int owner) => building.r_PlayerIdOwner == owner &&
            building.r_BuildingType == eStructs.STRUCT_HUNTERS_HUT &&
            (building.r_AliveState == AliveState.IsAlive || building.r_AliveState == AliveState.NeedsInit);

        internal static int Count(ReadOnlySpan<GameBuilding> buildings, int owner)
        {
            int count = 0;
            for (int i = 0; i < buildings.Length; i++) if (IsHut(in buildings[i], owner)) count++;
            return count;
        }

        internal static bool HasSpawnSite(ReadOnlySpan<GameBuilding> buildings, int owner)
        {
            for (int i = 0; i < buildings.Length; i++)
            {
                ref readonly GameBuilding building = ref buildings[i];
                if (building.r_PlayerIdOwner == owner && building.r_AliveState == AliveState.IsAlive &&
                    (building.r_BuildingType == eStructs.STRUCT_GRANARY || building.r_BuildingType == eStructs.STRUCT_MILL ||
                     building.r_BuildingType == eStructs.STRUCT_BAKERS_WORKSHOP)) return true;
            }
            return false;
        }

        // Reconstructed from world state after loading; no persisted or process-global hut ownership.
        internal static uint SelectBaseline(ReadOnlySpan<GameBuilding> buildings, int owner, int granaryId)
        {
            bool anchor = granaryId > 0 && granaryId <= buildings.Length &&
                buildings[granaryId - 1].r_PlayerIdOwner == owner &&
                buildings[granaryId - 1].r_BuildingType == eStructs.STRUCT_GRANARY &&
                buildings[granaryId - 1].r_AliveState == AliveState.IsAlive;
            uint selected = 0; long best = long.MaxValue;
            for (int i = 0; i < buildings.Length; i++)
            {
                ref readonly GameBuilding building = ref buildings[i];
                if (!IsHut(in building, owner) || building.r_GlobalId == 0) continue;
                long dx = anchor ? building.r_TilePositionXBegin - buildings[granaryId - 1].r_TilePositionXBegin : 0;
                long dy = anchor ? building.r_TilePositionYBegin - buildings[granaryId - 1].r_TilePositionYBegin : 0;
                long distance = dx * dx + dy * dy;
                if (distance < best || (distance == best && (selected == 0 || building.r_GlobalId < selected)))
                { best = distance; selected = building.r_GlobalId; }
            }
            return selected;
        }

        internal static bool AllowsBuild(bool otherPrey, bool baselineFood, int huts) => otherPrey || (baselineFood && huts == 0);
        internal static bool Keep(bool otherPrey, bool baselineFood, uint hut, uint baseline) =>
            otherPrey || (baselineFood && hut != 0 && hut == baseline);
    }
}
