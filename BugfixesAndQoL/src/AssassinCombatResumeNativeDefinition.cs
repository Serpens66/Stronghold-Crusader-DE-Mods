// Audited native contract for Assassin movement-order resumption after combat.
namespace BugfixesAndQoL
{
    internal static class AssassinCombatResumeNativeDefinition
    {
        public const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        public const int ResumeOldOrderRva = 0x122800;
        public const string ResumeOldOrderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 DA";

        public const int ResumeNativeUnitIndexAddressingRva = 0x12281C;
        public const string ResumeNativeUnitIndexAddressingPattern =
            "48 63 DA 48 8D 05 DA 5B 6C 06 4C 69 FB 90 04 00 00 33 F6 33 ED 33 FF 4C";

        public const int AssassinPathContextFlagRva = 0x60AD6E8;
        public const int CommonPathRequestRva = 0x196280;
        public const string CommonPathRequestPattern =
            "48 89 5C 24 20 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 F2 45 33 D2 48 69 FE 90 04 00 00 4D 63 F0";
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
        public const int PostCombatPathRequestCallOffset = 40;
        public const int PostCombatMovementStateLoadOffset = 59;
        public const string PostCombatPathRequestSequence =
            "48 63 15 E6 2F 7C 00 48 69 CA 90 04 00 00 44 89 64 24 20 46 0F BF 8C 39 36 09 00 00 46 0F BF 84 39 34 09 00 00 49 8B CF E8 7C 8F 02 00 48 63 05 B9 2F 7C 00 48 69 C8 90 04 00 00 B8 65 00 00 00 66 42 89 84 39 18 09 00 00";

        public const int ResumePathRequestSequenceRva = 0x122AF7;
        public const int ResumePathRequestCallOffset = 24;
        public const string ResumePathRequestSequence =
            "E8 A4 1B 07 00 85 C0 75 14 44 8B CD 89 44 24 20 44 8B C6 8B D3 49 8B CE E8 6C 37 07 00 B8 01 00";

        public const int MoveHereContextSequenceRva = 0x11BFA2;
        public const int MoveHereContextClearOffset = 8;
        public const int MoveHereContextSetOffset = 28;
        public const string MoveHereContextSequence =
            "42 89 8C 13 00 0A 00 00 89 0D 38 17 F9 05 39 8C 24 D0 00 00 00 74 0D B8 01 00 00 00 89 05 24 17 F9 05 EB 21 39 4C 24 40 75 15 39 0D BE E6 E5 02";

        public const int MoveHerePathRequestSequenceRva = 0x11C050;
        public const int MoveHerePathRequestCallOffset = 7;
        public const string MoveHerePathRequestSequence =
            "89 4C 24 20 49 8B CA E8 24 A2 07 00 85 C0 0F 85 93 00 00 00";

        public const int DispatcherAssassinBranchRva = 0xF4B0C;
        public const int DispatcherAssassinBuilderCallOffset = 27;
        public const string DispatcherAssassinBranchPattern =
            "39 AB 88 00 00 00 74 1D 89 6C 24 30 48 8B CB C7 44 24 28 80 1A 06 00 89 44 24 20 E8 14 51 FE FF";
    }
}
