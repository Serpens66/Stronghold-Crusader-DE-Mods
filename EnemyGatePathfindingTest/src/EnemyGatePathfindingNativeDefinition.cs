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

        // UPDATE REVIEW (CrusaderDE.dll + Script Extender): 2.0.2 already detours
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

        // UPDATE REVIEW (CrusaderDE.dll): these former functional boundaries and their
        // Win64 ABIs were audited only for the reference DLL. They remain documentation;
        // the crash-safe build installs neither planner nor builder detours.
        public const int CentralMovementPlanRva = 0x18E1E0;
        public const string CentralMovementPlanPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC 38 04 00 00 " +
            "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 20 04 00 00 4C 63 FA " +
            "4C 8D 35 ?? ?? ?? ?? 49 69 DF 90 04 00 00 49 63 E8 48 03 D9 49 63 F1";
        public const int MainPathBuilderRva = 0xF4930;
        public const string MainPathBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 " +
            "48 63 41 0C 48 8B D9 41 8B F0 44 8B D2";
        // UPDATE REVIEW (CrusaderDE.dll): F4930 dispatches among these six primary
        // searches. 79C0 is only the distance helper used to size search budgets.
        // DB650 is a conditional post-search flood and E1640 reconstructs the route.
        // None is hooked and no global grid overlay is permitted.
        public const int BuilderSearchVariantF32B0Rva = 0xF32B0;
        public const int BuilderSearchVariantF3060Rva = 0xF3060;
        public const int BuilderSearchVariantDA590Rva = 0xDA590;
        public const int BuilderSearchVariantDAAC0Rva = 0xDAAC0;
        public const int BuilderSearchVariantD9C40Rva = 0xD9C40;
        public const int BuilderSearchVariantDAFD0Rva = 0xDAFD0;
        public const int BuilderConditionalPostSearchDB650Rva = 0xDB650;
        public const int BuilderRouteReconstructionE1640Rva = 0xE1640;
        public const int BuilderDistanceHelper79C0Rva = 0x79C0;
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

        // UPDATE REVIEW (CrusaderDE.dll + RedBird 2.0.2): this is the ordinary movement
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
        // UPDATE REVIEW (CrusaderDE.dll): audited read sites from the exact-hash native
        // baseline. These are documentation for a future local filter, not hook sites.
        public const int DirectionReadF32B0Rva = 0xF33F5;
        public const int DirectionReadF3060Rva = 0xF31A8;
        public const int DirectionReadD9C40Rva = 0xD9EA6;
        public const int DirectionReadDA590Rva = 0xDA783;
        public const int DirectionReadDAAC0Rva = 0xDACB2;
        public const int DirectionReadDAFD0Rva = 0xDB242;
        public const int DirectionTestDB650FirstRva = 0xDB860;
        public const int DirectionTestDB650SecondRva = 0xDB950;
        public const int DirectionTestDB650ThirdRva = 0xDBA3F;
        public const int DirectionTestDB650FourthRva = 0xDBB2F;
        public const int DirectionReadE1640Rva = 0xE1777;

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

        // UPDATE REVIEW (CrusaderDE.dll): both CMPs are reached only for a hostile
        // owner. Vanilla accepts every non-zero r_CapturedByPlayerId. The callbacks
        // change ZF only for an unrelated capturer and use different player registers.
        public const int PclGraphCapturedByCompareRva = 0xE2710;
        public const int PclGraphCapturedByCompareHookLength = 9;
        public const string PclGraphCapturedByComparePattern =
            "49 63 49 F4 48 69 D1 2C 03 00 00 66 83 BC 02 D2 CE 4C 06 00 74 11";
        public const int PclGraphCapturedByCompareOffsetInPattern = 11;
        public const int BuilderPrecheckCapturedByCompareRva = 0xE302F;
        public const int BuilderPrecheckCapturedByCompareHookLength = 9;
        public const string BuilderPrecheckCapturedByComparePattern =
            "49 63 49 F4 48 69 D1 2C 03 00 00 66 42 39 84 2A D2 CE 4C 06 74 0D";
        public const int BuilderPrecheckCapturedByCompareOffsetInPattern = 11;

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
        public const string AuditedScriptExtenderVersion = "2.0.2";
        public const string AuditedScriptExtenderCommit =
            "6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79";
    }
}
