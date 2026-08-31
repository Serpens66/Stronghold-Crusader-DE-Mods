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

        // UPDATE REVIEW (CrusaderDE.dll): these functional boundaries and their Win64
        // ABIs were audited only for the reference DLL. MoveMoatTest_Serp owns overlapping
        // planner/builder/cursor code, so this mod must never install them then.
        public const int CentralMovementPlanRva = 0x18E1E0;
        public const string CentralMovementPlanPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC 38 04 00 00 " +
            "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 20 04 00 00 4C 63 FA " +
            "4C 8D 35 ?? ?? ?? ?? 49 69 DF 90 04 00 00 49 63 E8 48 03 D9 49 63 F1";
        public const int MainPathBuilderRva = 0xF4930;
        public const string MainPathBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 " +
            "48 63 41 0C 48 8B D9 41 8B F0 44 8B D2";
        // E32B0 is reconstruction-only. It remains documented for update audits but is
        // deliberately not hooked by the functional fix.
        public const int AlternatePathBuilderRva = 0xE32B0;
        public const string AlternatePathBuilderPattern =
            "40 53 48 83 EC 30 44 8B 49 10 33 C0 44 8B 41 0C 48 8B D9 8B 51 08 " +
            "89 44 24 28 89 81 68 5F 15 00 8B 41 14 89 44 24 20 E8 ?? ?? ?? ??";
        public const int CursorReachabilityRva = 0xE9FF0;
        public const string CursorReachabilityPattern =
            "44 89 4C 24 20 44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 50 48 63 F2 45 33 ED 33 D2 49 63 E8 49 63 C1 48 8B D9";

        // UPDATE REVIEW (CrusaderDE.dll + Zhuqiaomon): this is the ordinary movement
        // cursor's PCL-result decision documented by MoveMoatTest. The callback must run
        // before the relocated TEST so changing EAX to zero affects Vanilla's CMOV path.
        public const int CursorPclDecisionRva = 0x8F1C4;
        public const int CursorPclDecisionHookLength = 14;
        public const string CursorPclDecisionPattern =
            "E8 ?? ?? ?? ?? 85 C0 48 8D 3D E3 FB FC 03 B8 01 00 00 00";
        public const int CursorPclDecisionOffsetInPattern = 5;

        // UPDATE REVIEW (CrusaderDE.dll): one byte per native tile, one bit per one of
        // the eight audited neighbor directions. Vanilla maintains opposite edge bits.
        public const int PathDirectionGridRva = 0x51890D0;

        // UPDATE REVIEW (CrusaderDE.dll): a second shared order path accepts positive
        // PCL results here before E9D90/E9FF0. The span contains an internal branch and
        // is intentionally audited/logged but not hooked in this crash-safe build.
        public const int CommandPclDecisionRva = 0x11B75A;
        public const int CommandPclDecisionHookLength = 14;
        public const string CommandPclDecisionPattern =
            "E8 ?? ?? ?? ?? 41 8B D7 85 C0 75 52 48 8D 0D ?? ?? ?? ?? E8";
        public const int CommandPclDecisionOffsetInPattern = 5;

        // UPDATE REVIEW (CrusaderDE.dll): offsets in the native PathManager used by
        // both builders. The direction buffer stores low nibble first, then high.
        public const int PathStartXOffset = 0x08;
        public const int PathStartYOffset = 0x0C;
        public const int PathTargetXOffset = 0x10;
        public const int PathTargetYOffset = 0x14;
        public const int PathDirectionBufferOffset = 0x155F60;
        public const int PathLengthOffset = 0x155F68;
        public const int MaximumDecodedPathLength = 2000;
        public const int MaximumTileIdExclusive = 320800;
        public const int MapGridWidth = 800;

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
