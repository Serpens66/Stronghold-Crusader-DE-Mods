using System;

namespace AIAttackTest
{
    internal static class AIAttackNativeContract
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        internal const int RecruitContextRva = 0x2F2B8;
        internal const int RecruitImmediateRva = 0x2F2CB;
        internal const int RecruitImmediateOffset = RecruitImmediateRva - RecruitContextRva;
        internal const string RecruitContextPattern =
            "0D 13 B4 53 08 44 8B F2 E8 ?? ?? ?? ?? 81 3D ?? ?? ?? ?? " +
            "C0 12 00 00 7C 4D 44 3B F3 7C 48 41 8D 04 1F";

        internal const int LordContextRva = 0x3B5D0;
        internal const int LordBranchRva = 0x3B5DB;
        internal const int LordBranchOffset = LordBranchRva - LordContextRva;
        internal const string LordContextPattern =
            "66 42 89 84 1E 34 06 00 00 85 ED 74 12 4D 85 ED 0F 85 " +
            "?? ?? ?? ?? 49 3B F8 0F 8D ?? ?? ?? ?? 48 85 C9 0F 84";

        internal const int AiWallTargetingFixRva = 0x10ECC3;

        internal static readonly byte[] VanillaRecruitTicks = { 0xC0, 0x12, 0x00, 0x00 };
        internal static readonly byte[] VanillaLordBranch = { 0x74, 0x12 };
        internal static readonly byte[] AttackAllEligibleLordBranch = { 0xEB, 0x12 };

        internal static void ValidateLayout()
        {
            if (RecruitImmediateOffset != 19 || LordBranchOffset != 11)
                throw new InvalidOperationException("Native patch offsets differ from the audited instruction layout.");
            if (RangesOverlap(RecruitImmediateRva, VanillaRecruitTicks.Length,
                    LordBranchRva, VanillaLordBranch.Length) ||
                RangesOverlap(RecruitImmediateRva, VanillaRecruitTicks.Length,
                    AiWallTargetingFixRva, 2) ||
                RangesOverlap(LordBranchRva, VanillaLordBranch.Length,
                    AiWallTargetingFixRva, 2))
            {
                throw new InvalidOperationException("AI attack patches overlap another audited patch span.");
            }
        }

        internal static byte[] EncodeInt32(int value) =>
            new[]
            {
                unchecked((byte)value),
                unchecked((byte)(value >> 8)),
                unchecked((byte)(value >> 16)),
                unchecked((byte)(value >> 24)),
            };

        private static bool RangesOverlap(int left, int leftLength, int right, int rightLength) =>
            left < right + rightLength && right < left + leftLength;
    }
}
