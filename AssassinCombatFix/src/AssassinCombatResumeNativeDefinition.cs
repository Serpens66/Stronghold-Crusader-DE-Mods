// Audited native contract for the Assassin state-122 path request after combat.
namespace AssassinCombatFix
{
    internal static class AssassinCombatResumeNativeDefinition
    {
        public const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        public const int AssassinPathContextFlagRva = 0x60AD6E8;
        public const int CommonPathRequestRva = 0x196280;
        public const int AssassinPathBuilderRva = 0xD9C40;

        public const int AssassinStateRemapSequenceRva = 0x16E428;
        public const int PostCombatStateRemapOffset = 10;
        public const byte PostCombatStateRemapIndex = 13;
        public const string AssassinStateRemapSequence =
            "12 0B 0C 0C 0C 12 12 12 12 12 0D 12 12 12 0E 0F 10 11 CC CC CC CC CC CC";

        public const int AssassinStateJumpTableSequenceRva = 0x16E39C;
        public const int PostCombatStateJumpTargetOffset = 4;
        public const int PostCombatStateHandlerRva = 0x16D21C;
        public const string AssassinStateJumpTableSequence =
            "20 E2 16 00 1C D2 16 00 F1 D6 16 00 A9 D9 16 00 E7 D8 16 00 5F DB 16 00 4B E3 16 00";

        public const int PostCombatPathRequestSequenceRva = 0x16D2D7;
        public const int PostCombatPreHookOffset = 19;
        public const int PostCombatPreHookRva = 0x16D2EA;
        public const int PostCombatPreHookLength = 18;
        public const int PostCombatPathRequestCallOffset = 40;
        public const int PostCombatPathRequestCallRva = 0x16D2FF;
        public const int PostCombatPostHookOffset = 45;
        public const int PostCombatPostHookRva = 0x16D304;
        public const int PostCombatPostHookLength = 14;
        public const int PostCombatMovementStateLoadOffset = 59;
        public const string PostCombatPathRequestSequence =
            "48 63 15 E6 2F 7C 00 48 69 CA 90 04 00 00 44 89 64 24 20 " +
            "46 0F BF 8C 39 36 09 00 00 46 0F BF 84 39 34 09 00 00 " +
            "49 8B CF E8 7C 8F 02 00 48 63 05 B9 2F 7C 00 48 69 C8 90 04 00 00 " +
            "B8 65 00 00 00 66 42 89 84 39 18 09 00 00";
        public static readonly byte[] PostCombatPreHookBytes =
        {
            0x46, 0x0F, 0xBF, 0x8C, 0x39, 0x36, 0x09, 0x00, 0x00,
            0x46, 0x0F, 0xBF, 0x84, 0x39, 0x34, 0x09, 0x00, 0x00
        };
        public static readonly byte[] PostCombatPostHookBytes =
        {
            0x48, 0x63, 0x05, 0xB9, 0x2F, 0x7C, 0x00,
            0x48, 0x69, 0xC8, 0x90, 0x04, 0x00, 0x00
        };

        // Vanilla's working Assassin branch sets the same flag immediately before
        // requesting a path. State 122 omits this seven-byte write.
        public const int WorkingAssassinContextSequenceRva = 0x16CFE2;
        public const int WorkingAssassinContextCallOffset = 12;
        public const string WorkingAssassinContextSequence =
            "44 89 2D FF 06 F4 05 44 89 64 24 20 E8 8D 92 02 00";

        public const int CommonPathContextReadRva = 0x1964EE;
        public const string CommonPathContextReadSequence =
            "44 39 15 F3 71 F1 05 0F 85 8A 00 00 00 45 85 ED 0F 85 81 00 00 00";

        public const int CommonPathSuccessClearSequenceRva = 0x196734;
        public const int CommonPathSuccessFlagClearOffset = 15;
        public const string CommonPathSuccessClearSequence =
            "44 89 05 A9 6F F1 05 66 44 89 87 2A 09 00 00 44 89 05 9E 6F F1 05";

        public const int CommonPathFailureClearRva = 0x19676C;
        public const string CommonPathFailureClearSequence =
            "44 89 15 75 6F F1 05 33 C0 48 8B 9C 24 88 00 00 00";

        public const int DispatcherAssassinBranchRva = 0xF4B0C;
        public const int DispatcherAssassinBuilderCallOffset = 27;
        public const string DispatcherAssassinBranchPattern =
            "39 AB 88 00 00 00 74 1D 89 6C 24 30 48 8B CB C7 44 24 28 80 1A 06 00 89 44 24 20 E8 14 51 FE FF";
    }
}
