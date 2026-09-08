using System;

namespace ElevatedMoatTest
{
    internal static class ElevatedMoatNativeContract
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int HeightWriterRva = 0x7870B;
        internal const int HeightWriterLength = 20;
        internal const int TileValidationResultRva = 0x7888E;
        internal const int TileValidationResultLength = 14;
        internal const int TileValidatorCallRva = 0x78889;
        internal const int TileValidatorRva = 0x7B060;
        internal const int MapperMoat = 105;
        internal const int MaximumVanillaTerrainHeight = 12;
        internal const int PlacementBlockedValue = 1;
        internal const int PlacementFailureReason = 24;
        internal const int PlacementBlockedOffset = 0x204E6FC;
        internal const int PlacementFailureReasonOffset = 0x204E704;
        internal const int MaximumFootprintHeightOffset = 0x204E72C;
        internal const int MinimumFootprintHeightOffset = 0x204E728;
        internal const int EffectiveMinimumFootprintHeightOffset = 0x204E730;
        internal const int FootprintTileXOffset = 0x204E760;
        internal const int FootprintTileYOffset = 0x204E764;

        internal const string HeightWriterPattern =
            "C7 83 FC E6 04 02 01 00 00 00 " +
            "C7 83 04 E7 04 02 18 00 00 00";

        internal const string TileValidationResultPattern =
            "85 C0 74 0A C7 83 FC E6 04 02 01 00 00 00 " +
            "8B 44 24 58 8B 8B 58 E7 04 02";

        // These bytes prove that the writer is reached only after MAPPER_MOAT == 105
        // and maxHeight > 12. Keeping the branch bytes makes the validation fail closed.
        internal static readonly byte[] RequiredPrefix =
        {
            0x66, 0x83, 0xBC, 0x24, 0xC0, 0x00, 0x00, 0x00, 0x69,
            0x75, 0x1D,
            0x83, 0xBB, 0x2C, 0xE7, 0x04, 0x02, 0x0C,
            0x7E, 0x14
        };

        internal static readonly byte[] HeightWriterBytes =
        {
            0xC7, 0x83, 0xFC, 0xE6, 0x04, 0x02, 0x01, 0x00, 0x00, 0x00,
            0xC7, 0x83, 0x04, 0xE7, 0x04, 0x02, 0x18, 0x00, 0x00, 0x00
        };

        internal static readonly byte[] RequiredSuffix =
        {
            0x44, 0x0F, 0xB6, 0xBC, 0x24, 0xD0, 0x00, 0x00, 0x00
        };

        internal static readonly byte[] TileValidationResultBytes =
        {
            0x85, 0xC0,
            0x74, 0x0A,
            0xC7, 0x83, 0xFC, 0xE6, 0x04, 0x02, 0x01, 0x00, 0x00, 0x00
        };

        internal static readonly byte[] TileValidationResultSuffix =
        {
            0x8B, 0x44, 0x24, 0x58,
            0x8B, 0x8B, 0x58, 0xE7, 0x04, 0x02
        };

        internal static readonly byte[] TileValidationResultResolutionBytes =
        {
            0x85, 0xC0,
            0x74, 0x0A,
            0xC7, 0x83, 0xFC, 0xE6, 0x04, 0x02, 0x01, 0x00, 0x00, 0x00,
            0x8B, 0x44, 0x24, 0x58,
            0x8B, 0x8B, 0x58, 0xE7, 0x04, 0x02
        };

        internal static void Validate(ReadOnlySpan<byte> memory, int writerRva)
        {
            int prefixStart = checked(writerRva - RequiredPrefix.Length);
            int suffixStart = checked(writerRva + HeightWriterLength);
            int requiredEnd = checked(suffixStart + RequiredSuffix.Length);
            if (prefixStart < 0 || requiredEnd > memory.Length)
                throw new InvalidOperationException("The elevated-moat validation window lies outside the native image.");

            AssertBytes(memory, prefixStart, RequiredPrefix, "moat/max-height comparison prefix");
            AssertBytes(memory, writerRva, HeightWriterBytes, "height failure writer");
            AssertBytes(memory, suffixStart, RequiredSuffix, "instruction following the writer");
        }

        internal static void ValidateTileValidationResultHook(ReadOnlySpan<byte> memory, int hookRva)
        {
            int callRva = checked(hookRva - 5);
            int suffixRva = checked(hookRva + TileValidationResultLength);
            int requiredEnd = checked(suffixRva + TileValidationResultSuffix.Length);
            if (callRva < 0 || requiredEnd > memory.Length)
                throw new InvalidOperationException("The tile-validation result window lies outside the native image.");
            if (callRva != TileValidatorCallRva || memory[callRva] != 0xE8)
                throw new InvalidOperationException("The tile-validation result hook is not immediately after its audited CALL.");

            int relativeDisplacement = ReadInt32(memory, callRva + 1);
            int callTargetRva = checked(hookRva + relativeDisplacement);
            if (callTargetRva != TileValidatorRva)
                throw new InvalidOperationException("The audited tile-validation CALL target differs.");

            AssertBytes(memory, hookRva, TileValidationResultBytes, "tile-validation result block");
            AssertBytes(memory, suffixRva, TileValidationResultSuffix, "tile-validation result continuation");
            int branchTargetRva = checked(hookRva + 4 + unchecked((sbyte)memory[hookRva + 3]));
            if (branchTargetRva != suffixRva)
                throw new InvalidOperationException("The tile-validation success branch does not land at the hook boundary.");
        }

        private static int ReadInt32(ReadOnlySpan<byte> memory, int offset) =>
            memory[offset] |
            memory[offset + 1] << 8 |
            memory[offset + 2] << 16 |
            memory[offset + 3] << 24;

        private static void AssertBytes(
            ReadOnlySpan<byte> memory,
            int offset,
            byte[] expected,
            string description)
        {
            for (int index = 0; index < expected.Length; index++)
            {
                if (memory[offset + index] != expected[index])
                {
                    throw new InvalidOperationException(
                        $"The audited {description} differs at RVA 0x{offset + index:X}.");
                }
            }
        }
    }
}
