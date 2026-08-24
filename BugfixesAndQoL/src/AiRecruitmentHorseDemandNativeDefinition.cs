// Audited native contract for the AI recruitment horse-demand fix.
namespace BugfixesAndQoL
{
    internal static class AiRecruitmentHorseDemandNativeDefinition
    {
        public const int RecruitEuropeanUnitRva = 0x190CA0;
        public const int ResultCodeOffset = 0x650;
        public const int MissingGoodIdOffset = 0x654;
        public const int MissingRequirementResultCode = 2;
        public const int KnightUnitType = 28;
        public const int SwordGoodId = 23;
        public const int MetalArmourGoodId = 25;

        // c_game_player_buy_eu_mercenary. The RIP-relative manager address is intentionally
        // wildcarded so an otherwise compatible relocated build can use the signature fallback.
        public const string RecruitEuropeanUnitPattern =
            "89 54 24 10 53 55 41 54 41 55 41 56 41 57 48 83 EC 38 " +
            "4C 8D 1D ?? ?? ?? ?? 49 63 E9 4C 63 CA 48 8B D9 33 C9 " +
            "89 8B 50 06 00 00 49 8D 41 EA";

        public static bool IsKnightHorseOnlyFailure(int unitType, int resultCode, int missingGoodId) =>
            unitType == KnightUnitType &&
            resultCode == MissingRequirementResultCode &&
            missingGoodId == 0;

        public static bool IsKnightEquipmentFailure(int unitType, int resultCode, int missingGoodId) =>
            unitType == KnightUnitType &&
            resultCode == MissingRequirementResultCode &&
            (missingGoodId == SwordGoodId || missingGoodId == MetalArmourGoodId);
    }
}
