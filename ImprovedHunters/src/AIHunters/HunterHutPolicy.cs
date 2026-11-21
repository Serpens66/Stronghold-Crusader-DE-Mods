using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace ImprovedHunters
{
    internal static class HunterHutPolicy
    {
        internal static bool IsHut(in GameBuilding building, int owner) => building.r_PlayerIdOwner == owner &&
            building.r_BuildingType == eStructs.STRUCT_HUNTERS_HUT &&
            (building.r_AliveState == AliveState.IsAlive || building.r_AliveState == AliveState.NeedsInit);

        internal static bool AllowsBuild(bool otherPrey, bool baselineFood, int huts) => otherPrey || (baselineFood && huts == 0);
        internal static bool Keep(bool otherPrey, bool baselineFood, uint hut, uint baseline) =>
            otherPrey || (baselineFood && hut != 0 && hut == baseline);
    }
}
