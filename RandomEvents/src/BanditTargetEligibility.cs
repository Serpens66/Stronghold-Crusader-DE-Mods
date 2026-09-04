using SHCDESE.Interop;

namespace RandomEvents
{
    internal static class BanditTargetEligibility
    {
        public static bool IsEligibleStructureType(eStructs buildingType)
        {
            // Keeps and the indestructible stockpile cannot serve as meaningful raid destinations.
            return buildingType != eStructs.STRUCT_GOODS_YARD &&
                   buildingType != eStructs.STRUCT_KEEP_ONE &&
                   buildingType != eStructs.STRUCT_KEEP_TWO &&
                   buildingType != eStructs.STRUCT_KEEP_THREE &&
                   buildingType != eStructs.STRUCT_KEEP_FOUR &&
                   buildingType != eStructs.STRUCT_KEEP_FIVE;
        }
    }
}
