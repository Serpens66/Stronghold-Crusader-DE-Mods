namespace EnemyGatePathfindingTest
{
    internal static class EnemyGatePathfindingNativeDefinition
    {
        // UPDATE REVIEW (CrusaderDE.dll): re-audit the hash, function ABI, RVAs,
        // complete record layout, caller inventory and both semantic signatures.
        public const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        public const int GetNextReachablePclRva = 0xE2610;
        public const int GetNextReachablePclEndRva = 0xE2C45;
        public const string GetNextReachablePclPattern =
            "40 55 41 54 41 55 41 56 48 8D AC 24";

        // UPDATE REVIEW (CrusaderDE.dll + Script Extender): 1.42.0 already detours
        // this exact function and exposes UnitR3EventHooks.OnUnitMoveHere. Do not add
        // a second overlapping detour. PCL/cursor validation precedes this command;
        // revalidate the event's Pre/Post placement, ABI, coordinates and signature.
        public const int MoveHereRva = 0x196280;
        public const string MoveHerePattern =
            "48 89 5C 24 20 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 F2";

        // UPDATE REVIEW (CrusaderDE.dll): the cursor query loads Y and X through
        // RIP-relative MOVs in this signature. Resolve them; never trust the RVAs alone.
        public const int CursorTargetSignatureRva = 0x8F3A8;
        public const string CursorTargetPattern =
            "44 8B 0D ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 44 8B 05 ?? ?? ?? ?? 41 8B D6 E8 ?? ?? ?? ?? 85 C0 74 11 44 8B BC 24 C0 00 00 00";
        public const int CursorTargetYDisplacementOffset = 3;
        public const int CursorTargetYNextInstructionOffset = 7;
        public const int CursorTargetXDisplacementOffset = 17;
        public const int CursorTargetXNextInstructionOffset = 21;
        public const int CursorTargetXRva = 0x3A11E2C;
        public const int CursorTargetYRva = 0x3A11E30;

        // This CMP is reached only for a hostile owner. Vanilla accepts every non-zero
        // r_CapturedByPlayerId; the callback changes ZF only for an unrelated capturer.
        public const int CapturedByCompareRva = 0xE2710;
        public const int CapturedByCompareHookLength = 9;
        public const string CapturedByComparePattern =
            "49 63 49 F4 48 69 D1 2C 03 00 00 66 83 BC 02 D2 CE 4C 06 00 74 11";
        public const int CapturedByCompareOffsetInPattern = 11;

        // UPDATE REVIEW (CrusaderDE.dll): these offsets are relative to native R9 at
        // the compare callback. The third PCL is the drawbridge-capable connection.
        public const int NativeRecordStride = 0x204;
        public const int RecordBuildingIdOffset = -0x0C;
        public const int RecordOwnerPlayerIdOffset = 0x1CC;
        public const int RecordFirstPclOffset = -0x1E8;
        public const int RecordSecondPclOffset = -0x1E4;
        public const int RecordThirdPclOffset = -0x34;

        // Audited direct call inventory for diagnostic attribution. Return addresses,
        // rather than CALL instruction RVAs, are classified into these function ranges.
        public const int AuditedDirectCallerCount = 84;
        public const ulong HumanCursorCommandStartRva = 0x8C5F0;
        public const ulong HumanCursorCommandEndRva = 0x92F31;
        public const ulong CommonPathBuilderStartRva = 0x195E30;
        public const ulong CommonPathBuilderEndRva = 0x19678D;

        // UPDATE REVIEW (Script Extender): rebuild and re-test against this exact source.
        public const string AuditedScriptExtenderVersion = "1.42.0";
        public const string AuditedScriptExtenderCommit =
            "171d68e155a8f98c5f8c4ee154d9af154c9a2443";
    }
}
