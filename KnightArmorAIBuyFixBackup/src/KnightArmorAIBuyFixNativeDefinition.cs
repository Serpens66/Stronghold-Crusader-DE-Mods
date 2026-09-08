namespace KnightArmorAIBuyFixBackup
{
    internal static class KnightArmorAIBuyFixNativeDefinition
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const string AuditedScriptExtenderCommit =
            "a0cd52993b44a6909d4f7f6a92f82fa5888a8e63";
        internal const int RecruitEuropeanUnitRva = 0x190CA0;

        // c_game_player_buy_eu_mercenary. The manager displacement is relocation-dependent.
        internal const string RecruitEuropeanUnitPattern =
            "89 54 24 10 53 55 41 54 41 55 41 56 41 57 48 83 EC 38 " +
            "4C 8D 1D ?? ?? ?? ?? 49 63 E9 4C 63 CA 48 8B D9 33 C9 " +
            "89 8B 50 06 00 00 49 8D 41 EA";
    }
}
