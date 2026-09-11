using System;

namespace EnemyGatePathfindingTest
{
    internal static class EnemyGatePathfindingNativeDefinition
    {
        public const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        // Resolve both cursor-coordinate globals through their RIP-relative loads.
        public const int CursorTargetSignatureRva = 0x8F3A8;
        public const string CursorTargetPattern =
            "44 8B 0D ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? 44 8B 05 ?? ?? ?? ?? 41 8B D6 E8 ?? ?? ?? ?? 85 C0 74 11 44 8B BC 24 C0 00 00 00";
        public const int CursorTargetYDisplacementOffset = 3;
        public const int CursorTargetYNextInstructionOffset = 7;
        public const int CursorTargetXDisplacementOffset = 17;
        public const int CursorTargetXNextInstructionOffset = 21;
        public const int CursorTargetXRva = 0x3A11E2C;
        public const int CursorTargetYRva = 0x3A11E30;

        // The cursor callback runs before this relocated integer-only block and may clear
        // EAX before its TEST/CMOV flow. No XMM/SIMD value is live across the span.
        public const int CursorPclDecisionRva = 0x8F1C4;
        public const int CursorPclDecisionHookLength = 14;
        public const string CursorPclDecisionPattern =
            "E8 ?? ?? ?? ?? 85 C0 48 8D 3D E3 FB FC 03 B8 01 00 00 00";
        public const int CursorPclDecisionOffsetInPattern = 5;

        public const int PathDirectionGridRva = 0x51890D0;
        public const int MaximumTileIdExclusive = 320800;
        public const int MapGridWidth = 800;

        // Shared capture table addressed by both displaced CMP instructions. The
        // preceding IMUL has already converted the one-based building id to its stride.
        public const int CapturedByPlayerTableDisplacement = 0x64CCED2;

        // Each capturer hook covers a complete MOVSXD/IMUL/CMP basic block. Both the
        // predecessor and successor branch targets remain outside the displaced span.
        public const int PclGraphPredecessorJumpRva = 0xE2703;
        public const int PclGraphCapturedByFilterRva = 0xE2705;
        public const int PclGraphCapturedByFilterHookLength = 20;
        public const int PclGraphCapturedByFilterEndRva = 0xE2719;
        public const int PclGraphSuccessorJumpRva = 0xE2719;
        public const int PclGraphAllowedRecordTargetRva = 0xE271B;
        public const string PclGraphCapturedByComparePattern =
            "49 63 49 F4 48 69 D1 2C 03 00 00 66 83 BC 02 D2 CE 4C 06 00 74 11";
        public const int PclGraphCapturedByCompareOffsetInPattern = 0;

        public const int BuilderPrecheckPredecessorJumpRva = 0xE3022;
        public const int BuilderPrecheckCapturedByFilterRva = 0xE3024;
        public const int BuilderPrecheckCapturedByFilterHookLength = 20;
        public const int BuilderPrecheckCapturedByFilterEndRva = 0xE3038;
        public const int BuilderPrecheckSuccessorJumpRva = 0xE3038;
        public const int BuilderPrecheckAllowedRecordTargetRva = 0xE303A;
        public const string BuilderPrecheckCapturedByComparePattern =
            "49 63 49 F4 48 69 D1 2C 03 00 00 66 42 39 84 2A D2 CE 4C 06 74 0D";
        public const int BuilderPrecheckCapturedByCompareOffsetInPattern = 0;

        // These offsets are relative to native R9 at the compare callback.
        public const int NativeRecordStride = 0x204;
        public const int RecordBuildingIdOffset = -0x0C;
        public const int RecordOwnerPlayerIdOffset = 0x1CC;
        public const int RecordFirstPclOffset = -0x1E8;
        public const int RecordSecondPclOffset = -0x1E4;
        public const int RecordThirdPclOffset = -0x34;

        public const string AuditedScriptExtenderCommit =
            "5f02af6d074af7c741ebdaaccb48add39eba1bf4";

        internal static bool PclGraphCaptureCompareIsEqual(ushort nativeCapturedByPlayerId) =>
            nativeCapturedByPlayerId == 0;

        internal static bool BuilderPrecheckCaptureCompareIsEqual(
            ushort nativeCapturedByPlayerId,
            ushort accumulatorValue) => nativeCapturedByPlayerId == accumulatorValue;

        internal static void ValidateNativeHookContracts(ReadOnlySpan<byte> memory)
        {
            ValidateBytes(memory, CursorPclDecisionRva,
                new byte[] { 0x85, 0xC0, 0x48, 0x8D, 0x3D, 0xE3, 0xFB, 0xFC,
                    0x03, 0xB8, 0x01, 0x00, 0x00, 0x00 },
                "cursor PCL decision block");

            ValidateBytes(memory, PclGraphPredecessorJumpRva,
                new byte[] { 0x74, 0x16, 0x49, 0x63, 0x49, 0xF4, 0x48, 0x69,
                    0xD1, 0x2C, 0x03, 0x00, 0x00, 0x66, 0x83, 0xBC, 0x02, 0xD2,
                    0xCE, 0x4C, 0x06, 0x00, 0x74, 0x11, 0xFF, 0xC3 },
                "PCL-graph block and branch boundaries");
            ValidateBytes(memory, BuilderPrecheckPredecessorJumpRva,
                new byte[] { 0x74, 0x16, 0x49, 0x63, 0x49, 0xF4, 0x48, 0x69,
                    0xD1, 0x2C, 0x03, 0x00, 0x00, 0x66, 0x42, 0x39, 0x84, 0x2A,
                    0xD2, 0xCE, 0x4C, 0x06, 0x74, 0x0D, 0xFF, 0xC3 },
                "builder-precheck block and branch boundaries");

            if (PclGraphCapturedByFilterRva + PclGraphCapturedByFilterHookLength !=
                    PclGraphCapturedByFilterEndRva ||
                PclGraphSuccessorJumpRva != PclGraphCapturedByFilterEndRva ||
                PclGraphAllowedRecordTargetRva < PclGraphCapturedByFilterEndRva ||
                BuilderPrecheckCapturedByFilterRva + BuilderPrecheckCapturedByFilterHookLength !=
                    BuilderPrecheckCapturedByFilterEndRva ||
                BuilderPrecheckSuccessorJumpRva != BuilderPrecheckCapturedByFilterEndRva ||
                BuilderPrecheckAllowedRecordTargetRva < BuilderPrecheckCapturedByFilterEndRva)
            {
                throw new InvalidOperationException("Enemy-gate hook boundaries are inconsistent.");
            }
        }

        private static void ValidateBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string name)
        {
            if (rva < 0 || expected == null || memory.Length - rva < expected.Length)
                throw new InvalidOperationException($"{name} lies outside the native image.");
            for (int index = 0; index < expected.Length; index++)
            {
                if (memory[rva + index] != expected[index])
                {
                    throw new InvalidOperationException(
                        $"{name} differs at RVA 0x{rva + index:X}: " +
                        $"expected 0x{expected[index]:X2}, found 0x{memory[rva + index]:X2}.");
                }
            }
        }
    }
}
