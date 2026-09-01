// Audited native contract for Assassin state-107 combat-order resumption.
namespace AssassinCombatFix
{
    internal static class AssassinCombatResumeNativeDefinition
    {
        public const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        public const int AssassinPathContextFlagRva = 0x60AD6E8;
        public const int CurrentContextUnitIndexRva = 0x9302C4;
        public const int CommonPathRequestRva = 0x196280;
        public const int AssassinPathBuilderRva = 0xD9C40;

        public const int State107TargetCheckRva = 0x7EB00;
        public const int State107TargetCheckSequenceRva = 0x16D52F;
        public const int State107TargetCheckCallOffset = 30;
        public const int State107TargetCheckCallRva = 0x16D54D;
        public const int State107TargetResultHookOffset = 35;
        public const int State107TargetResultHookRva = 0x16D552;
        public const int State107TargetResultHookLength = 4;
        public const string State107TargetCheckSequence =
            "44 89 64 24 40 44 89 6C 24 38 89 4C 24 30 48 8B CB 41 0F BE C3 " +
            "89 44 24 28 44 89 54 24 20 E8 AE 15 F1 FF 85 C0 74 6C 85 ED 74 19";
        public static readonly byte[] State107TargetResultHookBytes =
        {
            0x85, 0xC0, 0x74, 0x6C
        };

        public const int GeneralResumeRva = 0x122800;
        public const int GeneralResumeReturnAddressStackOffset = 0x58;
        public const int GeneralResumePrologueRva = 0x122800;
        public const string GeneralResumePrologueSequence =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 30 " +
            "48 63 DA 48 8D 05 ? ? ? ? 4C 69 FB 90 04 00 00";

        // Both calls are inside the Assassin state-107 handler. Their return
        // addresses are the narrow runtime discriminator used inside 0x122800.
        public const int AssassinCombatResumeCall1Rva = 0x16D599;
        public const int AssassinCombatResumeReturn1Rva = 0x16D59E;
        public const int AssassinCombatResumeCall1SequenceRva = 0x16D573;
        public const int AssassinCombatResumeCall1Offset = 38;
        public const string AssassinCombatResumeCall1Sequence =
            "48 63 15 4A 2D 7C 00 4C 69 C2 90 04 00 00 47 89 A4 38 00 0A 00 00 " +
            "47 0F BF 84 38 5A 09 00 00 48 8D 0D 87 91 B5 07 E8 62 52 FB FF " +
            "85 C0 0F 85 2D 01 00 00 4C 63 0D 17 2D 7C 00";

        public const int AssassinCombatResumeCall2Rva = 0x16D642;
        public const int AssassinCombatResumeReturn2Rva = 0x16D647;
        public const int AssassinCombatResumeCall2SequenceRva = 0x16D62F;
        public const int AssassinCombatResumeCall2Offset = 19;
        public const string AssassinCombatResumeCall2Sequence =
            "47 0F BF 84 3A 5A 09 00 00 48 8D 0D E1 90 B5 07 41 8B D1 E8 B9 51 FB FF " +
            "85 C0 0F 85 FC 0C 00 00 8B 15 6F 2C 7C 00 45 8B C5 49 8B CF";

        public const int ShortResumeRva = 0x1946A0;
        public const int ResumeDecisionSequenceRva = 0x122AF2;
        public const int ShortResumeCallOffset = 5;
        public const int ShortResumeDecisionHookOffset = 10;
        public const int ShortResumeDecisionHookRva = 0x122AFC;
        public const int ShortResumeDecisionHookLength = 4;
        public const int FullRepathCallOffset = 29;
        public const int FullRepathCallRva = 0x122B0F;
        public const int FullRepathResultHookOffset = 34;
        public const int FullRepathResultHookRva = 0x122B14;
        public const int FullRepathResultHookLength = 7;
        public const string ResumeDecisionSequence =
            "8B D3 49 8B CE E8 ? ? ? ? 85 C0 75 14 44 8B CD 89 44 24 20 44 8B C6 8B D3 49 8B CE " +
            "E8 ? ? ? ? B8 01 00 00 00 EB 02 33 C0";
        public static readonly byte[] ShortResumeDecisionHookBytes =
        {
            0x85, 0xC0, 0x75, 0x14
        };
        public static readonly byte[] FullRepathResultHookBytes =
        {
            0xB8, 0x01, 0x00, 0x00, 0x00, 0xEB, 0x02
        };

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
