// Audited native contract for Assassin state-106 combat-order resumption.
namespace AssassinCombatFix
{
    internal static class AssassinCombatResumeNativeDefinition
    {
        public const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        public const int AssassinPathContextFlagRva = 0x60AD6E8;
        public const int CommonPathRequestRva = 0x196280;
        public const int PostPathRequestRva = 0x196810;
        public const int AssassinPathBuilderRva = 0xD9C40;

        public const int AssassinStateMachineRva = 0x16CD70;
        public const int State106CombatFinishCallSequenceRva = 0x16DFCE;
        public const int State106CombatFinishCallOffset = 5;
        public const int State106CombatFinishCallRva = 0x16DFD3;
        public const int State106CombatFinishReturnRva = 0x16DFD8;
        public const int CombatFinishHelperRva = 0x1853F0;
        public const string State106CombatFinishCallSequence =
            "8B D7 49 8B CF E8 ? ? ? ? E9 6E 03 00 00 66 46 89 A4 3B 70 0A 00 00";

        public const int CombatFinishHelperSequenceRva = 0x1853F0;
        public const int CombatFinishResumeCallOffset = 29;
        public const int CombatFinishResumeCallRva = 0x18540D;
        public const int CombatFinishResumeReturnRva = 0x185412;
        public const int PostCombatRepathRva = 0x1976C0;
        public const string CombatFinishHelperSequence =
            "40 53 48 83 EC 20 48 63 C2 48 69 D8 90 04 00 00 48 03 D9 " +
            "66 83 BB 96 09 00 00 00 75 14 E8 ? ? ? ? 33 C0 " +
            "66 89 83 96 09 00 00 89 83 98 09 00 00";

        public const int InlineHookMinimumOverwriteLength = 14;
        public const int CombatFinishDiagnosticHookRva = 0x1853F0;
        public const int CombatFinishDiagnosticHookLength = 16;
        public const int CombatFinishStackDeltaAtCallback = 0x28;
        public const int CombatFinishCallerReturnAddressStackOffset = 0x28;
        public static readonly byte[] CombatFinishDiagnosticHookBytes =
        {
            0x40, 0x53,
            0x48, 0x83, 0xEC, 0x20,
            0x48, 0x63, 0xC2,
            0x48, 0x69, 0xD8, 0x90, 0x04, 0x00, 0x00
        };

        public const int CommonPathPrologueRva = 0x196280;
        public const int CommonPathPrologueLength = 20;
        public const int CommonPathStackDeltaAtCallback = 0x68;
        public static readonly byte[] CommonPathPrologueBytes =
        {
            0x48, 0x89, 0x5C, 0x24, 0x20,
            0x55,
            0x56,
            0x57,
            0x41, 0x54,
            0x41, 0x55,
            0x41, 0x56,
            0x41, 0x57,
            0x48, 0x83, 0xEC, 0x30
        };

        public const int CommonPathDiagnosticHookRva = 0x196294;
        public const int CommonPathDiagnosticHookLength = 16;
        public const int CommonPathCallerReturnAddressStackOffset = 0x68;
        public const int CommonPathOptionStackOffset = 0x90;
        public static readonly byte[] CommonPathDiagnosticHookBytes =
        {
            0x48, 0x63, 0xF2,
            0x45, 0x33, 0xD2,
            0x48, 0x69, 0xFE, 0x90, 0x04, 0x00, 0x00,
            0x4D, 0x63, 0xF0
        };

        public const int PostCombatRepathPrologueRva = 0x1976C0;
        public const int PostCombatCallerReturnAddressStackOffset = 0x38;
        public const string PostCombatRepathPrologueSequence =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 30 48 63 FA 48 8B F1 " +
            "48 69 DF 90 04 00 00 48 03 D9 66 83 BB F8 08 00 00 00";

        // This sequence restores the saved state and secondary target immediately
        // before Vanilla requests a replacement path after combat.
        public const int PostCombatPathRequestSequenceRva = 0x197702;
        public const int PostCombatPathRequestCallOffset = 41;
        public const int PostCombatPathRequestCallRva = 0x19772B;
        public const int PostCombatFinalizeCallOffset = 51;
        public const int PostCombatFinalizeCallRva = 0x197735;
        public const string PostCombatPathRequestSequence =
            "33 C9 44 0F BF 8B 46 07 00 00 8B D7 44 0F BF 83 44 07 00 00 " +
            "66 89 8B 4E 07 00 00 89 4C 24 20 48 8B CE " +
            "66 89 83 18 09 00 00 E8 ? ? ? ? 8B D7 48 8B CE E8 ? ? ? ?";
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
