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
        internal const int AivMoatFunctionRva = 0x59730;
        internal const int AivMoatFunctionLength = 0x2F4;
        internal const int AivHeightGateRva = 0x59827;
        internal const int AivHeightGateLength = 22;
        internal const int AivCreatePathRva = 0x599B3;
        internal const int AivCreatePathLength = 16;
        internal const int HumanMoatFunctionRva = 0x739C0;
        internal const int HumanMoatFunctionLength = 0x1E5;
        internal const int HumanMoatWriterCallRva = 0x73B1F;
        internal const int HumanMoatWriterResultRva = 0x73B24;
        internal const int HumanMoatWriterResultLength = 15;
        internal const int MoatWriterRva = 0x59210;
        internal const int TileHeightGridOffset = 0xD7E5A0;
        internal const int TileDefaultHeightGridOffset = 0xDCCAC0;
        internal const int SharedTileFunctionRva = 0x6FE90;
        internal const int SharedTileFunctionLength = 0xB19;
        internal const int SharedHeightGateRva = 0x704CC;
        internal const int SharedHeightGateLength = 14;
        internal const int AivCompletedHeightRva = 0x599E7;
        internal const int AivCompletedHeightLength = 17;
        internal const int ExcavationFunctionRva = 0x639C0;
        internal const int ExcavationFunctionLength = 0x143;
        internal const int ExcavationCompletedHeightRva = 0x63A65;
        internal const int ExcavationCompletedHeightLength = 16;
        internal const int RebuildFunctionRva = 0x64460;
        internal const int RebuildFunctionLength = 0x15D;
        internal const int RebuildCompletedHeightRva = 0x64546;
        internal const int RebuildCompletedHeightLength = 15;
        internal const int DirectCompletedHeightRva = 0x705F7;
        internal const int DirectCompletedHeightLength = 22;
        internal const int GenericCompletedHeightRva = 0x73B35;
        internal const int GenericCompletedHeightLength = 17;
        internal const int DirectRemovalHeightRva = 0x70621;
        internal const int DirectRemovalHeightLength = 16;
        internal const int MoatDepth = 8;
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

        internal const string AivHeightGatePattern =
            "80 BE A0 E5 D7 00 0C 0F 86 7F 01 00 00 " +
            "41 0F B6 84 6B C0 B9 7F 00";

        internal const string AivCreatePathPattern =
            "0F B6 9C 24 90 00 00 00 45 8B CE C6 44 24 28 00 " +
            "45 8B C7 41 8B D4 89 5C 24 20 49 8B CB E8";

        internal const string HumanMoatWriterResultPattern =
            "42 81 A4 B3 00 84 89 00 FF FF FF BF 45 33 C0 " +
            "8B D7 48 8B CB E8 63 EA FF FF";

        internal const string SharedHeightGatePattern =
            "80 BC 3B A0 E5 D7 00 0C 0F 87 78 04 00 00 45 85 FF 75 73";

        internal const string AivCompletedHeightPattern =
            "0F BA E8 1E 89 87 00 84 89 00 C6 86 A0 E5 D7 00 00 EB 0A";

        internal const string ExcavationCompletedHeightPattern =
            "BA 02 00 00 00 48 63 C7 C6 84 18 A0 E5 D7 00 00 49 63 06";

        internal const string RebuildCompletedHeightPattern =
            "C6 84 1F A0 E5 D7 00 00 48 8D 3D AB BA F9 FF FF C5 49 83 C6 04";

        internal const string DirectCompletedHeightPattern =
            "41 81 26 FF BF FF FF 41 81 0E 00 00 00 40 " +
            "C6 84 3B A0 E5 D7 00 00 E9 2E 03 00 00";

        internal const string GenericCompletedHeightPattern =
            "48 8B CB E8 63 EA FF FF 41 C6 84 1E A0 E5 D7 00 00 EB 0C";

        internal const string DirectRemovalHeightPattern =
            "48 8B CF E8 A7 18 FF FF C6 84 3B A0 E5 D7 00 08 " +
            "41 81 26 FF BF FF BF";

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

        internal static readonly byte[] AivHeightGateBytes =
        {
            0x80, 0xBE, 0xA0, 0xE5, 0xD7, 0x00, 0x0C,
            0x0F, 0x86, 0x7F, 0x01, 0x00, 0x00,
            0x41, 0x0F, 0xB6, 0x84, 0x6B, 0xC0, 0xB9, 0x7F, 0x00
        };

        internal static readonly byte[] AivCreatePathBytes =
        {
            0x0F, 0xB6, 0x9C, 0x24, 0x90, 0x00, 0x00, 0x00,
            0x45, 0x8B, 0xCE,
            0xC6, 0x44, 0x24, 0x28, 0x00
        };

        internal static readonly byte[] AivCreatePathResolutionBytes =
        {
            0x0F, 0xB6, 0x9C, 0x24, 0x90, 0x00, 0x00, 0x00,
            0x45, 0x8B, 0xCE,
            0xC6, 0x44, 0x24, 0x28, 0x00,
            0x45, 0x8B, 0xC7, 0x41, 0x8B, 0xD4, 0x89, 0x5C, 0x24, 0x20,
            0x49, 0x8B, 0xCB, 0xE8
        };

        internal static readonly byte[] HumanMoatWriterResultBytes =
        {
            0x42, 0x81, 0xA4, 0xB3, 0x00, 0x84, 0x89, 0x00,
            0xFF, 0xFF, 0xFF, 0xBF,
            0x45, 0x33, 0xC0
        };

        internal static readonly byte[] HumanMoatWriterResultSuffix =
        {
            0x8B, 0xD7, 0x48, 0x8B, 0xCB, 0xE8, 0x63, 0xEA, 0xFF, 0xFF
        };

        internal static readonly byte[] HumanMoatWriterResultResolutionBytes =
        {
            0x42, 0x81, 0xA4, 0xB3, 0x00, 0x84, 0x89, 0x00,
            0xFF, 0xFF, 0xFF, 0xBF,
            0x45, 0x33, 0xC0,
            0x8B, 0xD7, 0x48, 0x8B, 0xCB, 0xE8, 0x63, 0xEA, 0xFF, 0xFF
        };

        internal static readonly byte[] SharedHeightGateBytes =
        {
            0x80, 0xBC, 0x3B, 0xA0, 0xE5, 0xD7, 0x00, 0x0C,
            0x0F, 0x87, 0x78, 0x04, 0x00, 0x00
        };

        internal static readonly byte[] AivCompletedHeightBytes =
        {
            0x0F, 0xBA, 0xE8, 0x1E, 0x89, 0x87, 0x00, 0x84, 0x89, 0x00,
            0xC6, 0x86, 0xA0, 0xE5, 0xD7, 0x00, 0x00
        };

        internal static readonly byte[] ExcavationCompletedHeightBytes =
        {
            0xBA, 0x02, 0x00, 0x00, 0x00, 0x48, 0x63, 0xC7,
            0xC6, 0x84, 0x18, 0xA0, 0xE5, 0xD7, 0x00, 0x00
        };

        internal static readonly byte[] RebuildCompletedHeightBytes =
        {
            0xC6, 0x84, 0x1F, 0xA0, 0xE5, 0xD7, 0x00, 0x00,
            0x48, 0x8D, 0x3D, 0xAB, 0xBA, 0xF9, 0xFF
        };

        internal static readonly byte[] DirectCompletedHeightBytes =
        {
            0x41, 0x81, 0x26, 0xFF, 0xBF, 0xFF, 0xFF,
            0x41, 0x81, 0x0E, 0x00, 0x00, 0x00, 0x40,
            0xC6, 0x84, 0x3B, 0xA0, 0xE5, 0xD7, 0x00, 0x00
        };

        internal static readonly byte[] GenericCompletedHeightBytes =
        {
            0x48, 0x8B, 0xCB, 0xE8, 0x63, 0xEA, 0xFF, 0xFF,
            0x41, 0xC6, 0x84, 0x1E, 0xA0, 0xE5, 0xD7, 0x00, 0x00
        };

        internal static readonly byte[] DirectRemovalHeightBytes =
        {
            0x48, 0x8B, 0xCF, 0xE8, 0xA7, 0x18, 0xFF, 0xFF,
            0xC6, 0x84, 0x3B, 0xA0, 0xE5, 0xD7, 0x00, 0x08
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

        internal static void ValidateAivHooks(
            ReadOnlySpan<byte> memory,
            int gateRva,
            int createPathRva)
        {
            int functionEnd = checked(AivMoatFunctionRva + AivMoatFunctionLength);
            if (gateRva < AivMoatFunctionRva ||
                gateRva + AivHeightGateLength > functionEnd ||
                createPathRva < AivMoatFunctionRva ||
                createPathRva + AivCreatePathLength > functionEnd ||
                functionEnd > memory.Length)
            {
                throw new InvalidOperationException("The AIV moat hooks lie outside their audited function.");
            }

            AssertBytes(memory, gateRva, AivHeightGateBytes, "AIV moat height gate");
            AssertBytes(memory, createPathRva, AivCreatePathBytes, "AIV moat creation path");
            int branchTarget = checked(gateRva + 13 + ReadInt32(memory, gateRva + 9));
            if (branchTarget != createPathRva)
                throw new InvalidOperationException("The AIV low-height branch target differs from the audited creation path.");
        }

        internal static void ValidateHumanMoatWriterResultHook(
            ReadOnlySpan<byte> memory,
            int hookRva)
        {
            int functionEnd = checked(HumanMoatFunctionRva + HumanMoatFunctionLength);
            int suffixRva = checked(hookRva + HumanMoatWriterResultLength);
            if (hookRva < HumanMoatFunctionRva ||
                suffixRva + HumanMoatWriterResultSuffix.Length > functionEnd ||
                functionEnd > memory.Length)
            {
                throw new InvalidOperationException("The human moat result hook lies outside its audited function.");
            }

            int callRva = checked(hookRva - 5);
            if (callRva != HumanMoatWriterCallRva || memory[callRva] != 0xE8)
                throw new InvalidOperationException("The human moat result hook is not after its audited CALL.");
            int callTarget = checked(hookRva + ReadInt32(memory, callRva + 1));
            if (callTarget != MoatWriterRva)
                throw new InvalidOperationException("The human moat writer CALL target differs.");

            AssertBytes(memory, hookRva, HumanMoatWriterResultBytes, "human moat writer result block");
            AssertBytes(memory, suffixRva, HumanMoatWriterResultSuffix, "human moat writer result continuation");
        }

        internal static void ValidateAdaptiveHeightHooks(ReadOnlySpan<byte> memory)
        {
            ValidateBlock(memory, SharedHeightGateRva, SharedHeightGateLength,
                SharedTileFunctionRva, SharedTileFunctionLength, SharedHeightGateBytes,
                "shared moat height gate");
            int rejectionTarget = checked(SharedHeightGateRva + SharedHeightGateLength +
                ReadInt32(memory, SharedHeightGateRva + 10));
            if (rejectionTarget != 0x70952)
                throw new InvalidOperationException("The shared moat height rejection target differs.");

            ValidateBlock(memory, AivCompletedHeightRva, AivCompletedHeightLength,
                AivMoatFunctionRva, AivMoatFunctionLength, AivCompletedHeightBytes,
                "AIV completed-moat height block");
            ValidateBlock(memory, ExcavationCompletedHeightRva, ExcavationCompletedHeightLength,
                ExcavationFunctionRva, ExcavationFunctionLength, ExcavationCompletedHeightBytes,
                "excavated-moat height block");
            ValidateBlock(memory, RebuildCompletedHeightRva, RebuildCompletedHeightLength,
                RebuildFunctionRva, RebuildFunctionLength, RebuildCompletedHeightBytes,
                "rebuilt-moat height block");
            ValidateBlock(memory, DirectCompletedHeightRva, DirectCompletedHeightLength,
                SharedTileFunctionRva, SharedTileFunctionLength, DirectCompletedHeightBytes,
                "direct completed-moat height block");
            ValidateBlock(memory, GenericCompletedHeightRva, GenericCompletedHeightLength,
                HumanMoatFunctionRva, HumanMoatFunctionLength, GenericCompletedHeightBytes,
                "generic completed-moat height block");
            ValidateBlock(memory, DirectRemovalHeightRva, DirectRemovalHeightLength,
                SharedTileFunctionRva, SharedTileFunctionLength, DirectRemovalHeightBytes,
                "direct moat-removal height block");

            int genericCallTarget = checked(GenericCompletedHeightRva + 8 +
                ReadInt32(memory, GenericCompletedHeightRva + 4));
            if (genericCallTarget != 0x725A0)
                throw new InvalidOperationException("The generic completed-moat visual call target differs.");
            int removalCallTarget = checked(DirectRemovalHeightRva + 8 +
                ReadInt32(memory, DirectRemovalHeightRva + 4));
            if (removalCallTarget != 0x61ED0)
                throw new InvalidOperationException("The direct moat-removal call target differs.");
        }

        internal static byte CalculateCompletedHeight(byte defaultHeight) =>
            defaultHeight > MoatDepth ? (byte)(defaultHeight - MoatDepth) : (byte)0;

        private static void ValidateBlock(
            ReadOnlySpan<byte> memory,
            int hookRva,
            int hookLength,
            int functionRva,
            int functionLength,
            byte[] expected,
            string description)
        {
            int functionEnd = checked(functionRva + functionLength);
            if (expected.Length != hookLength || hookRva < functionRva ||
                hookRva + hookLength > functionEnd || functionEnd > memory.Length)
            {
                throw new InvalidOperationException($"The audited {description} lies outside its function.");
            }

            AssertBytes(memory, hookRva, expected, description);
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
