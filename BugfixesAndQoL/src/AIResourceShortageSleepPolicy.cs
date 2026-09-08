using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal static unsafe class AIResourceShortageSleepPolicy
    {
        internal const int PlayerStride = 0x583C;
        internal const int SleepStateTableOffset = 0x130D54;
        internal const int PlannerEnabledOffset = 0x130EC0;
        internal const int MinimumPlayerId = 1;
        internal const int MaximumPlayerId = 8;

        internal static readonly eStructs[] AffectedBuildingTypes =
        {
            eStructs.STRUCT_WOODCUTTERS_HUT,
            eStructs.STRUCT_OXEN_BASE,
            eStructs.STRUCT_IRON_MINE,
            eStructs.STRUCT_PITCH_DIGGER,
            eStructs.STRUCT_HUNTERS_HUT,
            eStructs.STRUCT_FLETCHERS_WORKSHOP,
            eStructs.STRUCT_BLACKSMITHS_WORKSHOP,
            eStructs.STRUCT_POLETURNERS_WORKSHOP,
            eStructs.STRUCT_ARMOURERS_WORKSHOP,
            eStructs.STRUCT_TANNERS_WORKSHOP,
            eStructs.STRUCT_BAKERS_WORKSHOP,
            eStructs.STRUCT_BREWERS_WORKSHOP,
            eStructs.STRUCT_QUARRY,
            eStructs.STRUCT_INN,
            eStructs.STRUCT_WELL,
            eStructs.STRUCT_WHEATFARM,
            eStructs.STRUCT_HOPSFARM,
            eStructs.STRUCT_APPLEFARM,
            eStructs.STRUCT_CATTLEFARM,
            eStructs.STRUCT_MILL,
            eStructs.STRUCT_WATERPOT,
        };

        internal static bool TryClearSleepRequests(byte* playerManager, int playerId)
        {
            if (playerManager == null || playerId < MinimumPlayerId || playerId > MaximumPlayerId)
                return false;

            // The native AI routine indexes this manager-owned table directly with
            // the one-based player id. Do not translate it to a zero-based array index.
            byte* playerRecord = playerManager + playerId * PlayerStride;
            if (*(int*)(playerRecord + PlannerEnabledOffset) == 0)
                return true;

            byte* sleepStates = playerRecord + SleepStateTableOffset;
            for (int index = 0; index < AffectedBuildingTypes.Length; index++)
                sleepStates[(int)AffectedBuildingTypes[index]] = 0;

            return true;
        }
    }
}
