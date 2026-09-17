using System;
using SHCDESE.Interop;

namespace ExtraFeatures
{
    internal static class ElevatedMoatNativeContract
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int DrawbridgeHeightFailureWriterRva = 0x7870B;
        internal const int DrawbridgeHeightFailureWriterLength = 20;
        internal const int AivMoatFunctionRva = 0x59730;
        internal const int AivMoatFunctionLength = 0x2F4;
        internal const int AivAudienceCaptureRva = AivMoatFunctionRva;
        internal const int AivAudienceCaptureLength = 15;
        internal const int AivHeightGateRva = 0x59827;
        internal const int AivHeightGateLength = 22;
        internal const int AivCreatePathRva = 0x599B3;
        internal const int AivCreatePathLength = 16;
        internal const int DrawbridgeFunctionRva = 0x739C0;
        internal const int DrawbridgeFunctionLength = 0x1E5;
        internal const int TileManagerRva = 0x405EDB0;
        internal const int TileDefaultHeightGridRva = 0x4E2B870;
        internal const int TileHeightGridOffset = 0xD7E5A0;
        internal const int TileDefaultHeightGridOffset = 0xDCCAC0;
        internal const int BuildingManagerRva = 0x64CCBB0;
        internal const int BuildingRecordStride = 0x32C;
        internal const int BuildingHeightOffset = 0x148;
        internal const int BuildingAllocatorFunctionRva = 0xB47E0;
        internal const int BuildingAllocatorFunctionLength = 0xCBF;
        internal const int BuildingHeightWriterRva = 0xB49DC;
        internal const int BuildingHeightWriterLength = 9;
        internal const int BuildingCreationFunctionRva = 0x6D580;
        internal const int BuildingCreationFunctionLength = 0xA68;
        internal const int BuildingCreationDefaultHeightRestoreRva = 0x6D71C;
        internal const int BuildingCreationDefaultHeightRestoreLength = 16;
        internal const int BuildingCreationMidpointRva = 0x6D9B6;
        internal const int BuildingCreationMidpointLength = 20;
        internal const int DrawbridgeCreationMidpointForwardRva = 0x6DDAF;
        internal const int DrawbridgeCreationMidpointForwardLength = 35;
        internal const int DrawbridgeBuildingHeightForwardRva = 0x73A08;
        internal const int DrawbridgeBuildingAllocatorCallRva = 0x73A19;
        internal const int MainRendererRva = 0x41D60;
        internal const int MainRendererLength = 0x39EA;
        internal const int DrawbridgeSpecialRendererRva = 0x45820;
        internal const int DrawbridgeSpecialRendererLength = 0x19F;
        internal const int DrawbridgeSpecialRendererHookLength = 19;
        internal const int DrawbridgeSpecialRendererContinuationRva = 0x45833;
        internal const int DrawbridgeSpecialRendererBuildingRecordRva = 0x45833;
        internal const int CurrentRenderedTileHeightRva = 0x42D8D8;
        internal const int DrawbridgeAnimatedRendererArgumentsRva = 0x43BA2;
        internal const int DrawbridgeAnimatedRendererArgumentsLength = 17;
        internal const int DrawbridgeAnimatedRendererCallRva = 0x43BB3;
        internal const int DrawbridgeAnimatedRendererRva = 0x488B0;
        internal const int DrawbridgeAnimatedRendererTypeCheckRva = 0x43B5B;
        internal const int DrawbridgeAnimatedRendererTileFlagsRva = 0x43B73;
        internal const int DrawbridgeAnimatedRendererBuildingArgumentRva = 0x43B91;
        internal const int DrawbridgeSpecialRendererCall1Rva = 0x44E3C;
        internal const int DrawbridgeSpecialRendererCall2Rva = 0x44EC3;
        internal const int DrawbridgeSpecialRendererCall1ArgumentsRva = 0x44E09;
        internal const int DrawbridgeSpecialRendererCall2ArgumentsRva = 0x44E8D;
        internal const int DrawbridgeSpecialRendererCall1BuildingArgumentRva = 0x44E32;
        internal const int DrawbridgeSpecialRendererCall2BuildingArgumentRva = 0x44EBC;
        internal const int DrawbridgeStaticRendererArgumentsRva = 0x44EDD;
        internal const int DrawbridgeStaticRendererArgumentsLength = 16;
        internal const int DrawbridgeStaticRendererHeightSubtractLength = 7;
        internal const int DrawbridgeStaticRendererContinuationRva = 0x44EED;
        internal const int DrawbridgeHeightAwareCallRva = 0x44F09;
        internal const int DrawbridgeHeightAwareRendererRva = 0x4C1D0;
        internal const int UnitType2SpriteQueueCall1Rva = 0x43EF3;
        internal const int UnitType2SpriteQueueCall2Rva = 0x44346;
        internal const int UnitSpriteQueueRva = 0x1A13C0;
        internal const int UnitType52HeightForwardingRva = 0x1A22C4;
        internal const int UnitHeightInitializationFunctionRva = 0x180A80;
        internal const int UnitHeightUpdateFunctionRva = 0x182B00;
        internal const int UnitHeightCorrectionFunctionRva = 0x184FD0;
        internal const int UnitHeightCorrectionFunctionLength = 0x1C0;
        internal const int UnitHeightInitializationCallRva = 0x180B56;
        internal const int UnitHeightUpdateCall1Rva = 0x184413;
        internal const int UnitHeightUpdateCall2Rva = 0x184538;
        internal const int UnitHeightUpdateCall3Rva = 0x184988;
        internal const int UnitHeightUpdateCall4Rva = 0x1849B3;
        internal const int UnitHeightCorrectionFunctionPrologueRva = 0x184FD0;
        internal const int UnitDrawbridgeTypeGateRva = 0x1850FF;
        internal const int UnitDrawbridgeHeightCorrectionRva = 0x18511C;
        internal const int UnitDrawbridgeHeightCorrectionLength = 19;
        internal const int UnitDrawbridgeHeightContinuationRva = 0x18512F;
        internal const int UnitHeightPostCorrectionRva = 0x18514E;
        internal const int UnitCurrentElevationOffset = 0x712;
        internal const int UnitVerticalCorrectionOffset = 0x714;
        internal const int MoatCommandValidationFunctionRva = 0x5CA40;
        internal const int MoatCommandValidationFunctionLength = 0x290;
        internal const int MoatCommandHeightGateRva = 0x5CC1E;
        internal const int MoatCommandHeightGateLength = 14;
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
        internal const int LowerDrawbridgeFunctionRva = 0x64460;
        internal const int LowerDrawbridgeFunctionLength = 0x15D;
        internal const int LowerDrawbridgeHeightWriteRva = 0x64546;
        internal const int LowerDrawbridgeHookRva = LowerDrawbridgeHeightWriteRva;
        internal const int LowerDrawbridgeHookLength = 15;
        internal const int LowerDrawbridgeHeightWriteLength = 8;
        internal const int LowerDrawbridgeImageBaseLeaRva =
            LowerDrawbridgeHeightWriteRva + LowerDrawbridgeHeightWriteLength;
        internal const int LowerDrawbridgeImageBaseLeaLength = 7;
        internal const int LowerDrawbridgeContinuationRva =
            LowerDrawbridgeHookRva + LowerDrawbridgeHookLength;
        internal const int LowerDrawbridgeGraphicRefreshCallRva = 0x6456E;
        internal const int LowerDrawbridgePathfindingRefreshCallRva = 0x6457D;
        internal const int DirectCompletedHeightRva = 0x705F7;
        internal const int DirectCompletedHeightLength = 22;
        internal const int CompletedDrawbridgeHookRva = 0x73B35;
        internal const int CompletedDrawbridgeHookLength = 17;
        internal const int CompletedDrawbridgeStateCallRva = CompletedDrawbridgeHookRva + 3;
        internal const int DrawbridgeStateUpdateRva = 0x725A0;
        internal const int CompletedDrawbridgeHeightWriteRva = CompletedDrawbridgeHookRva + 8;
        internal const int CompletedDrawbridgeHeightWriteLength = 9;
        internal const int CompletedDrawbridgeJumpRva =
            CompletedDrawbridgeHookRva + CompletedDrawbridgeHookLength;
        internal const int CompletedDrawbridgeHeightContinuationRva = 0x73B54;
        internal const int DrawbridgeConnectivityCallRva = 0x73B7B;
        internal const int DrawbridgePathfindingRefreshJumpRva = 0x73BA0;
        internal const int PlannedMoatCancellationRva = 0x70562;
        internal const int PlannedMoatCancellationLength = 15;
        internal const int DirectRemovalHeightRva = 0x70621;
        internal const int DirectRemovalHeightLength = 23;
        internal const int FootprintRemovalFunctionRva = 0x622D0;
        internal const int FootprintRemovalFunctionLength = 0x185;
        internal const int FootprintRemovalHeightRva = 0x623DA;
        internal const int FootprintRemovalHeightLength = 15;
        internal const int ObjectRemovalFunctionRva = 0x6CA30;
        internal const int ObjectRemovalFunctionLength = 0xE8;
        internal const int ObjectRemovalHeightRva = 0x6CAA1;
        internal const int ObjectRemovalHeightLength = 19;
        internal const int MoatWorkCompletionFunctionRva = 0x69470;
        internal const int MoatWorkCompletionFunctionLength = 0xED;
        internal const int MoatWorkCompletionHeightRva = 0x694BD;
        internal const int MoatWorkCompletionHeightLength = 22;
        internal const int AreaRemovalFunctionRva = 0xED2E0;
        internal const int AreaRemovalFunctionLength = 0x8F5;
        internal const int AreaRemovalHeightRva = 0xEDA77;
        internal const int AreaRemovalHeightLength = 21;
        internal const int MoatDepth = 8;
        internal const int MaximumVanillaTerrainHeight = 12;
        internal const int PlacementBlockedValue = 1;
        internal const int PlacementFailureReason = 24;
        internal const int PlacementBlockedOffset = 0x204E6FC;
        internal const int PlacementFailureReasonOffset = 0x204E704;

        internal const string DrawbridgeHeightFailureWriterPattern =
            "C7 83 FC E6 04 02 01 00 00 00 " +
            "C7 83 04 E7 04 02 18 00 00 00";

        internal const string AivHeightGatePattern =
            "80 BE A0 E5 D7 00 0C 0F 86 7F 01 00 00 " +
            "41 0F B6 84 6B C0 B9 7F 00";

        internal const string AivAudienceCapturePattern =
            "48 89 5C 24 10 48 89 6C 24 18 57 41 54 41 55 " +
            "41 56 41 57 48 83 EC 40 49 63 E8";

        internal const string AivCreatePathPattern =
            "0F B6 9C 24 90 00 00 00 45 8B CE C6 44 24 28 00 " +
            "45 8B C7 41 8B D4 89 5C 24 20 49 8B CB E8";

        internal const string SharedHeightGatePattern =
            "80 BC 3B A0 E5 D7 00 0C 0F 87 78 04 00 00 45 85 FF 75 73";

        internal const string MoatCommandHeightGatePattern =
            "80 BC 3B A0 E5 D7 00 0C 0F 87 83 00 00 00 " +
            "41 83 FE 6A 75 57 A9 00 40 00 40";

        internal const string AivCompletedHeightPattern =
            "0F BA E8 1E 89 87 00 84 89 00 C6 86 A0 E5 D7 00 00 EB 0A";

        internal const string ExcavationCompletedHeightPattern =
            "BA 02 00 00 00 48 63 C7 C6 84 18 A0 E5 D7 00 00 49 63 06";

        internal const string LowerDrawbridgeHookPattern =
            "C6 84 1F A0 E5 D7 00 00 48 8D 3D AB BA F9 FF FF C5 49 83 C6 04";

        internal const string DirectCompletedHeightPattern =
            "41 81 26 FF BF FF FF 41 81 0E 00 00 00 40 " +
            "C6 84 3B A0 E5 D7 00 00 E9 2E 03 00 00";

        internal const string CompletedDrawbridgeHookPattern =
            "48 8B CB E8 63 EA FF FF 41 C6 84 1E A0 E5 D7 00 00 " +
            "EB 0C 42 81 A4 B3 00 84 89 00";

        internal const string PlannedMoatCancellationPattern =
            "0F BA F2 0E 45 8B C4 41 89 16 48 8B CF 8B D6 " +
            "E8 5A 19 FF FF E9 C5 03 00 00";

        internal const string DirectRemovalHeightPattern =
            "48 8B CF E8 A7 18 FF FF C6 84 3B A0 E5 D7 00 08 " +
            "41 81 26 FF BF FF BF";

        internal const string FootprintRemovalHeightPattern =
            "C6 84 1E A0 E5 D7 00 08 8B 8C B3 00 84 89 00 " +
            "EB 0D 8B CD 0F BA E9 1E 89";

        internal const string ObjectRemovalHeightPattern =
            "81 A4 B3 00 84 89 00 FF FF FF BF " +
            "C6 84 1E A0 E5 D7 00 08 E8 A7 CA FF FF 85 C0 74";

        internal const string MoatWorkCompletionHeightPattern =
            "C6 84 08 A0 E5 D7 00 08 48 63 03 " +
            "81 A4 81 00 84 89 00 FF FF FF BF " +
            "48 8D 0D 86 41 04 06 44";

        internal const string AreaRemovalHeightPattern =
            "41 C6 84 0E A0 E5 D7 00 08 " +
            "42 81 A4 B1 00 84 89 00 FF BF FF BF " +
            "66 41 89 07 41 0F B7 C1";

        // The 0x69 immediate is the native representation of eMappers.MAPPER_DRAWBRIDGE.
        // These bytes prove that the writer is reached only after that mapper comparison
        // and maxHeight > 12. Keeping the branch bytes makes the validation fail closed.
        internal static readonly byte[] DrawbridgeHeightFailurePrefix =
        {
            0x66, 0x83, 0xBC, 0x24, 0xC0, 0x00, 0x00, 0x00, 0x69,
            0x75, 0x1D,
            0x83, 0xBB, 0x2C, 0xE7, 0x04, 0x02, 0x0C,
            0x7E, 0x14
        };

        internal static readonly byte[] DrawbridgeHeightFailureWriterBytes =
        {
            0xC7, 0x83, 0xFC, 0xE6, 0x04, 0x02, 0x01, 0x00, 0x00, 0x00,
            0xC7, 0x83, 0x04, 0xE7, 0x04, 0x02, 0x18, 0x00, 0x00, 0x00
        };

        internal static readonly byte[] DrawbridgeHeightFailureSuffix =
        {
            0x44, 0x0F, 0xB6, 0xBC, 0x24, 0xD0, 0x00, 0x00, 0x00
        };

        internal static readonly byte[] AivHeightGateBytes =
        {
            0x80, 0xBE, 0xA0, 0xE5, 0xD7, 0x00, 0x0C,
            0x0F, 0x86, 0x7F, 0x01, 0x00, 0x00,
            0x41, 0x0F, 0xB6, 0x84, 0x6B, 0xC0, 0xB9, 0x7F, 0x00
        };

        internal static readonly byte[] AivAudienceCaptureBytes =
        {
            0x48, 0x89, 0x5C, 0x24, 0x10,
            0x48, 0x89, 0x6C, 0x24, 0x18,
            0x57, 0x41, 0x54, 0x41, 0x55
        };

        internal static readonly byte[] AivCreatePathBytes =
        {
            0x0F, 0xB6, 0x9C, 0x24, 0x90, 0x00, 0x00, 0x00,
            0x45, 0x8B, 0xCE,
            0xC6, 0x44, 0x24, 0x28, 0x00
        };

        internal static readonly byte[] SharedHeightGateBytes =
        {
            0x80, 0xBC, 0x3B, 0xA0, 0xE5, 0xD7, 0x00, 0x0C,
            0x0F, 0x87, 0x78, 0x04, 0x00, 0x00
        };

        internal static readonly byte[] MoatCommandHeightGateBytes =
        {
            0x80, 0xBC, 0x3B, 0xA0, 0xE5, 0xD7, 0x00, 0x0C,
            0x0F, 0x87, 0x83, 0x00, 0x00, 0x00
        };

        internal static readonly byte[] MoatCommandMapperBytes =
        {
            0x41, 0x83, 0xFE, 0x6A,
            0x75, 0x57,
            0xA9, 0x00, 0x40, 0x00, 0x40
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

        internal static readonly byte[] LowerDrawbridgeHookBytes =
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

        internal static readonly byte[] CompletedDrawbridgeHookBytes =
        {
            0x48, 0x8B, 0xCB,
            0xE8, 0x63, 0xEA, 0xFF, 0xFF,
            0x41, 0xC6, 0x84, 0x1E, 0xA0, 0xE5, 0xD7, 0x00, 0x00
        };

        internal static readonly byte[] DrawbridgeSpecialRendererHookBytes =
        {
            0x48, 0x8B, 0xC4,
            0x48, 0x89, 0x58, 0x20,
            0x55,
            0x41, 0x54,
            0x41, 0x56,
            0x48, 0x81, 0xEC, 0x80, 0x00, 0x00, 0x00
        };

        internal static readonly byte[] DrawbridgeSpecialRendererBuildingRecordBytes =
        {
            // movsxd RBP,EDX; lea R12,imageBase; imul RBX,RBP,0x32C; mov R14D,R9D
            0x48, 0x63, 0xEA,
            0x4C, 0x8D, 0x25, 0xC3, 0xA7, 0xFB, 0xFF,
            0x48, 0x69, 0xDD, 0x2C, 0x03, 0x00, 0x00,
            0x45, 0x8B, 0xF1
        };

        internal static readonly byte[] DrawbridgeAnimatedRendererArgumentsBytes =
        {
            // mov RCX,[RSP+0x140]; mov [RSP+0x28],ESI; mov [RSP+0x20],R15D
            0x48, 0x8B, 0x8C, 0x24, 0x40, 0x01, 0x00, 0x00,
            0x89, 0x74, 0x24, 0x28,
            0x44, 0x89, 0x7C, 0x24, 0x20
        };

        internal static readonly byte[] DrawbridgeAnimatedRendererTypeCheckBytes =
        {
            // cmp word ptr [RCX+RAX+0x12E],0x31; jne non-drawbridge
            0x66, 0x83, 0xBC, 0x01, 0x2E, 0x01, 0x00, 0x00, 0x31, 0x75, 0x54
        };

        internal static readonly byte[] DrawbridgeAnimatedRendererTileFlagsBytes =
        {
            // load tile flags; load argument 5; mask 0xC; require value 4
            0x42, 0x0F, 0xB6, 0x84, 0x6A, 0x40, 0x5F, 0xF0, 0x00,
            0x44, 0x8B, 0xBC, 0x24, 0x88, 0x00, 0x00, 0x00,
            0x24, 0x0C, 0x3C, 0x04, 0x75, 0x38
        };

        internal static readonly byte[] DrawbridgeSpecialRendererCall1ArgumentsBytes =
        {
            0x44, 0x8B, 0x0D, 0xC4, 0x8A, 0x3E, 0x00,
            0x41, 0x8D, 0x4A, 0x18,
            0x44, 0x8B, 0x05, 0xB5, 0x8A, 0x3E, 0x00,
            0x41, 0x83, 0xC1, 0x18,
            0x89, 0x4C, 0x24, 0x30,
            0x45, 0x03, 0xCA
        };

        internal static readonly byte[] DrawbridgeSpecialRendererCall2ArgumentsBytes =
        {
            0x44, 0x8B, 0x0D, 0x40, 0x8A, 0x3E, 0x00,
            0x41, 0x8D, 0x4A, 0x28,
            0x44, 0x8B, 0x05, 0x31, 0x8A, 0x3E, 0x00,
            0x41, 0x83, 0xC1, 0x28,
            0x89, 0x4C, 0x24, 0x30
        };

        internal static readonly byte[] DrawbridgeAnimatedRendererBuildingArgumentBytes =
        {
            // mov EDX,R12D
            0x41, 0x8B, 0xD4
        };

        internal static readonly byte[] DrawbridgeStaticRendererArgumentsBytes =
        {
            // sub R10D,[CurrentRenderedTileHeight]; mov EDX,EDI;
            // mov R9D,[CurrentRenderBase]
            0x44, 0x2B, 0x15, 0xF4, 0x89, 0x3E, 0x00,
            0x8B, 0xD7,
            0x44, 0x8B, 0x0D, 0xE7, 0x89, 0x3E, 0x00
        };

        internal static readonly byte[] BuildingHeightWriterBytes =
        {
            // mov word ptr [R8 + R14 + BuildingHeightOffset],AX
            0x66, 0x43, 0x89, 0x84, 0x30, 0x48, 0x01, 0x00, 0x00
        };

        internal static readonly byte[] BuildingCreationMidpointBytes =
        {
            // (maximum - minimum) / 2 + minimum -> ESI
            0x2B, 0xF1,
            0x48, 0x63, 0xBC, 0x24, 0xB8, 0x00, 0x00, 0x00,
            0x8B, 0xC6,
            0x99,
            0x2B, 0xC2,
            0xD1, 0xF8,
            0x8D, 0x34, 0x01
        };

        internal static readonly byte[] DrawbridgeCreationMidpointForwardBytes =
        {
            // Forward ESI as argument 8 to the drawbridge creator.
            0x89, 0x74, 0x24, 0x38,
            0x45, 0x8B, 0xCE,
            0x89, 0x6C, 0x24, 0x30,
            0x45, 0x8B, 0xC7,
            0x44, 0x89, 0x6C, 0x24, 0x28,
            0x8B, 0xD7,
            0x48, 0x8B, 0xCB,
            0x66, 0x44, 0x89, 0x64, 0x24, 0x20,
            0xE8, 0xEE, 0x5B, 0x00, 0x00
        };

        internal static readonly byte[] DrawbridgeBuildingHeightForwardBytes =
        {
            // Read argument 8 and forward it as the allocator's height argument.
            0x8B, 0x8C, 0x24, 0xD8, 0x00, 0x00, 0x00,
            0x89, 0x48, 0x88
        };

        internal static readonly byte[] DrawbridgeSpecialRendererBuildingArgumentBytes =
        {
            // mov EDX,EDI
            0x8B, 0xD7
        };

        internal static readonly byte[] UnitDrawbridgeTypeGateBytes =
        {
            // test EDI,EDI; je fallback; locate building record; require type 0x31.
            0x85, 0xFF,
            0x74, 0x2E,
            0x48, 0x69, 0xCF, 0x2C, 0x03, 0x00, 0x00,
            0x48, 0x8D, 0x35, 0x9F, 0x7A, 0x34, 0x06,
            0x66, 0x83, 0xBC, 0x31, 0x2E, 0x01, 0x00, 0x00, 0x31,
            0x75, 0x32
        };

        internal static readonly byte[] UnitHeightCorrectionFunctionPrologueBytes =
        {
            0x40, 0x53,
            0x48, 0x83, 0xEC, 0x20,
            0x48, 0x63, 0xC2,
            0x33, 0xD2,
            0x48, 0x69, 0xD8, 0x90, 0x04, 0x00, 0x00,
            0x48, 0x89, 0x6C, 0x24, 0x30,
            0x48, 0x8D, 0x2D, 0x12, 0xB0, 0xE7, 0xFF,
            0x48, 0x03, 0xD9
        };

        internal static readonly byte[] UnitDrawbridgeHeightCorrectionBytes =
        {
            0xB8, 0x08, 0x00, 0x00, 0x00,
            0x66, 0x2B, 0x83, 0x12, 0x07, 0x00, 0x00,
            0x66, 0x89, 0x83, 0x14, 0x07, 0x00, 0x00
        };

        internal static readonly byte[] UnitDrawbridgeHeightContinuationBytes =
        {
            // Preserve Vanilla's jump to the shared post-height path.
            0xEB, 0x1D
        };

        internal static readonly byte[] BuildingCreationDefaultHeightRestoreBytes =
        {
            // movzx EAX,byte ptr [RSI + R8 + TileDefaultHeightGridRva]
            0x42, 0x0F, 0xB6, 0x84, 0x06, 0x70, 0xB8, 0xE2, 0x04,
            // mov byte ptr [RSI + RBX + TileHeightGridOffset],AL
            0x88, 0x84, 0x1E, 0xA0, 0xE5, 0xD7, 0x00
        };

        internal static readonly byte[] UnitHeightPostCorrectionBytes =
        {
            // RAX and RCX are rebuilt before use; TEST AX replaces incoming flags.
            0x48, 0x63, 0x05, 0x6F, 0xB1, 0x7A, 0x00,
            0x48, 0x8B, 0x7C, 0x24, 0x40,
            0x48, 0x8B, 0x74, 0x24, 0x38,
            0x48, 0x69, 0xC8, 0x90, 0x04, 0x00, 0x00,
            0x0F, 0xB7, 0x84, 0x29, 0x70, 0x8E, 0x7E, 0x06,
            0x48, 0x8B, 0x6C, 0x24, 0x30,
            0x66, 0x85, 0xC0
        };

        internal static readonly byte[] UnitType52HeightForwardingBytes =
        {
            // Keep param8 in R10W unless Vanilla's debug/override source is active,
            // then store the resulting value in the type-52 record height field.
            0x39, 0x35, 0x82, 0xB1, 0xF0, 0x05,
            0x48, 0x8B, 0x03,
            0x75, 0x09,
            0x46, 0x0F, 0xB7, 0x94, 0x07, 0x62, 0xC3, 0x30, 0x07,
            0x66, 0x44, 0x89, 0x54, 0x01, 0x0A
        };

        internal static readonly byte[] PlannedMoatCancellationBytes =
        {
            0x0F, 0xBA, 0xF2, 0x0E,
            0x45, 0x8B, 0xC4,
            0x41, 0x89, 0x16,
            0x48, 0x8B, 0xCF,
            0x8B, 0xD6
        };

        internal static readonly byte[] PlannedMoatCancellationPrefixBytes =
        {
            0x41, 0x83, 0xFF, 0x01,
            0x75, 0x23,
            0x0F, 0xBA, 0xE2, 0x0E,
            0x0F, 0x83, 0xDE, 0x03, 0x00, 0x00
        };

        internal static readonly byte[] DirectRemovalHeightBytes =
        {
            0x48, 0x8B, 0xCF, 0xE8, 0xA7, 0x18, 0xFF, 0xFF,
            0xC6, 0x84, 0x3B, 0xA0, 0xE5, 0xD7, 0x00, 0x08,
            0x41, 0x81, 0x26, 0xFF, 0xBF, 0xFF, 0xBF
        };

        internal static readonly byte[] FootprintRemovalHeightBytes =
        {
            0xC6, 0x84, 0x1E, 0xA0, 0xE5, 0xD7, 0x00, 0x08,
            0x8B, 0x8C, 0xB3, 0x00, 0x84, 0x89, 0x00
        };

        internal static readonly byte[] FootprintRemovalPrefixBytes =
        {
            0x81, 0xA4, 0xB3, 0x00, 0x84, 0x89, 0x00, 0xFF, 0xFF, 0xFF, 0xBF,
            0x80, 0x8C, 0x1E, 0x00, 0x25, 0x9D, 0x00, 0x02,
            0xC6, 0x84, 0x1E, 0xE0, 0x86, 0xBA, 0x00, 0x00
        };

        internal static readonly byte[] ObjectRemovalHeightBytes =
        {
            0x81, 0xA4, 0xB3, 0x00, 0x84, 0x89, 0x00, 0xFF, 0xFF, 0xFF, 0xBF,
            0xC6, 0x84, 0x1E, 0xA0, 0xE5, 0xD7, 0x00, 0x08
        };

        internal static readonly byte[] MoatWorkCompletionHeightBytes =
        {
            0xC6, 0x84, 0x08, 0xA0, 0xE5, 0xD7, 0x00, 0x08,
            0x48, 0x63, 0x03,
            0x81, 0xA4, 0x81, 0x00, 0x84, 0x89, 0x00, 0xFF, 0xFF, 0xFF, 0xBF
        };

        internal static readonly byte[] AreaRemovalHeightBytes =
        {
            0x41, 0xC6, 0x84, 0x0E, 0xA0, 0xE5, 0xD7, 0x00, 0x08,
            0x42, 0x81, 0xA4, 0xB1, 0x00, 0x84, 0x89, 0x00, 0xFF, 0xBF, 0xFF, 0xBF
        };

        internal static void ValidateDrawbridgeHeightFailure(
            ReadOnlySpan<byte> memory,
            int writerRva)
        {
            int prefixStart = checked(writerRva - DrawbridgeHeightFailurePrefix.Length);
            int suffixStart = checked(writerRva + DrawbridgeHeightFailureWriterLength);
            int requiredEnd = checked(suffixStart + DrawbridgeHeightFailureSuffix.Length);
            if (prefixStart < 0 || requiredEnd > memory.Length)
                throw new InvalidOperationException("The elevated-drawbridge validation window lies outside the native image.");

            AssertBytes(memory, prefixStart, DrawbridgeHeightFailurePrefix, "drawbridge/max-height comparison prefix");
            AssertBytes(memory, writerRva, DrawbridgeHeightFailureWriterBytes, "drawbridge height failure writer");
            AssertBytes(memory, suffixStart, DrawbridgeHeightFailureSuffix, "instruction following the drawbridge writer");
            if (memory[prefixStart + 8] != checked((byte)eMappers.MAPPER_DRAWBRIDGE))
                throw new InvalidOperationException("The native mapper immediate is not eMappers.MAPPER_DRAWBRIDGE.");
        }

        internal static void ValidateAivHooks(
            ReadOnlySpan<byte> memory,
            int audienceCaptureRva,
            int gateRva,
            int createPathRva)
        {
            int functionEnd = checked(AivMoatFunctionRva + AivMoatFunctionLength);
            if (audienceCaptureRva != AivAudienceCaptureRva ||
                audienceCaptureRva + AivAudienceCaptureLength > functionEnd ||
                gateRva < AivMoatFunctionRva ||
                gateRva + AivHeightGateLength > functionEnd ||
                createPathRva < AivMoatFunctionRva ||
                createPathRva + AivCreatePathLength > functionEnd ||
                functionEnd > memory.Length)
            {
                throw new InvalidOperationException("The AIV moat hooks lie outside their audited function.");
            }

            AssertBytes(memory, audienceCaptureRva, AivAudienceCaptureBytes, "AIV player-audience capture");
            AssertBytes(memory, gateRva, AivHeightGateBytes, "AIV moat height gate");
            AssertBytes(memory, createPathRva, AivCreatePathBytes, "AIV moat creation path");
            int branchTarget = checked(gateRva + 13 + ReadInt32(memory, gateRva + 9));
            if (branchTarget != createPathRva)
                throw new InvalidOperationException("The AIV low-height branch target differs from the audited creation path.");
        }

        internal static void ValidateAdaptiveHeightHooks(ReadOnlySpan<byte> memory)
        {
            if (TileDefaultHeightGridRva != TileManagerRva + TileDefaultHeightGridOffset)
            {
                throw new InvalidOperationException(
                    "The image-relative DefaultHeightGrid address no longer matches its manager-relative offset.");
            }
            ValidateBlock(memory,
                BuildingHeightWriterRva,
                BuildingHeightWriterLength,
                BuildingAllocatorFunctionRva,
                BuildingAllocatorFunctionLength,
                BuildingHeightWriterBytes,
                "Vanilla building-height writer");
            ValidateBlock(memory,
                BuildingCreationMidpointRva,
                BuildingCreationMidpointLength,
                BuildingCreationFunctionRva,
                BuildingCreationFunctionLength,
                BuildingCreationMidpointBytes,
                "Vanilla footprint-height midpoint calculation");
            AssertBytes(memory,
                DrawbridgeCreationMidpointForwardRva,
                DrawbridgeCreationMidpointForwardBytes,
                "drawbridge midpoint forwarding");
            ValidateRelativeBranch(memory,
                DrawbridgeCreationMidpointForwardRva +
                    DrawbridgeCreationMidpointForwardLength - 5,
                0xE8,
                DrawbridgeFunctionRva,
                "drawbridge creator call");
            AssertBytes(memory,
                DrawbridgeBuildingHeightForwardRva,
                DrawbridgeBuildingHeightForwardBytes,
                "drawbridge building-height forwarding");
            ValidateRelativeBranch(memory,
                DrawbridgeBuildingAllocatorCallRva,
                0xE8,
                BuildingAllocatorFunctionRva,
                "drawbridge building allocator call");
            ValidateBlock(memory,
                BuildingCreationDefaultHeightRestoreRva,
                BuildingCreationDefaultHeightRestoreLength,
                BuildingCreationFunctionRva,
                BuildingCreationFunctionLength,
                BuildingCreationDefaultHeightRestoreBytes,
                "building-creation DefaultHeightGrid restore block");
            ValidateBlock(memory, MoatCommandHeightGateRva, MoatCommandHeightGateLength,
                MoatCommandValidationFunctionRva, MoatCommandValidationFunctionLength,
                MoatCommandHeightGateBytes, "MAPPER_MOAT/MAPPER_ANTIMOAT command height gate");
            AssertBytes(memory, MoatCommandHeightGateRva + MoatCommandHeightGateLength,
                MoatCommandMapperBytes, "MAPPER_MOAT command dispatch following the height gate");
            if (memory[MoatCommandHeightGateRva + MoatCommandHeightGateLength + 3] !=
                checked((byte)eMappers.MAPPER_MOAT) ||
                memory[MoatCommandHeightGateRva + 0x6E] != checked((byte)eMappers.MAPPER_ANTIMOAT))
            {
                throw new InvalidOperationException(
                    "The native moat-command immediates do not match eMappers.MAPPER_MOAT/MAPPER_ANTIMOAT.");
            }
            int commandRejectionTarget = checked(MoatCommandHeightGateRva + MoatCommandHeightGateLength +
                ReadInt32(memory, MoatCommandHeightGateRva + 10));
            if (commandRejectionTarget != 0x5CCAF)
                throw new InvalidOperationException("The moat-command height rejection target differs.");

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
            ValidateBlock(memory, LowerDrawbridgeHookRva, LowerDrawbridgeHookLength,
                LowerDrawbridgeFunctionRva, LowerDrawbridgeFunctionLength,
                LowerDrawbridgeHookBytes, "lowered-drawbridge hook block");
            ValidateLoweredDrawbridgeRewriteContract(memory);
            ValidateBlock(memory, DirectCompletedHeightRva, DirectCompletedHeightLength,
                SharedTileFunctionRva, SharedTileFunctionLength, DirectCompletedHeightBytes,
                "direct completed-moat height block");
            ValidateBlock(memory, CompletedDrawbridgeHookRva,
                CompletedDrawbridgeHookLength, DrawbridgeFunctionRva,
                DrawbridgeFunctionLength, CompletedDrawbridgeHookBytes,
                "completed-drawbridge hook block");
            ValidateCompletedDrawbridgeRewriteContract(memory);
            ValidateDrawbridgeRendererContract(memory);
            ValidateUnitDrawbridgeHeightContract(memory);
            if (memory[CompletedDrawbridgeJumpRva] != 0xEB ||
                checked(CompletedDrawbridgeJumpRva + 2 +
                    (sbyte)memory[CompletedDrawbridgeJumpRva + 1]) !=
                        CompletedDrawbridgeHeightContinuationRva)
            {
                throw new InvalidOperationException(
                    "The completed-drawbridge height continuation differs.");
            }
            ValidateBlock(memory, PlannedMoatCancellationRva, PlannedMoatCancellationLength,
                SharedTileFunctionRva, SharedTileFunctionLength, PlannedMoatCancellationBytes,
                "planned moat cancellation block");
            AssertBytes(memory, PlannedMoatCancellationRva - PlannedMoatCancellationPrefixBytes.Length,
                PlannedMoatCancellationPrefixBytes, "planned moat cancellation mode and moat-flag prefix");
            ValidateBlock(memory, DirectRemovalHeightRva, DirectRemovalHeightLength,
                SharedTileFunctionRva, SharedTileFunctionLength, DirectRemovalHeightBytes,
                "direct moat-removal height block");
            ValidateBlock(memory, FootprintRemovalHeightRva, FootprintRemovalHeightLength,
                FootprintRemovalFunctionRva, FootprintRemovalFunctionLength,
                FootprintRemovalHeightBytes, "footprint moat-removal height block");
            AssertBytes(memory, FootprintRemovalHeightRva - FootprintRemovalPrefixBytes.Length,
                FootprintRemovalPrefixBytes, "footprint completed-moat flag removal prefix");
            ValidateBlock(memory, ObjectRemovalHeightRva, ObjectRemovalHeightLength,
                ObjectRemovalFunctionRva, ObjectRemovalFunctionLength,
                ObjectRemovalHeightBytes, "object moat-removal height block");
            ValidateBlock(memory, MoatWorkCompletionHeightRva, MoatWorkCompletionHeightLength,
                MoatWorkCompletionFunctionRva, MoatWorkCompletionFunctionLength,
                MoatWorkCompletionHeightBytes, "moat work-completion height block");
            ValidateBlock(memory, AreaRemovalHeightRva, AreaRemovalHeightLength,
                AreaRemovalFunctionRva, AreaRemovalFunctionLength,
                AreaRemovalHeightBytes, "area moat-removal height block");

            ValidateRelativeBranch(memory, CompletedDrawbridgeStateCallRva, 0xE8, 0x725A0,
                "completed drawbridge state update");
            ValidateRefreshOrder(
                memory,
                LowerDrawbridgeHookRva,
                LowerDrawbridgeGraphicRefreshCallRva,
                0x6E620,
                LowerDrawbridgePathfindingRefreshCallRva,
                0x725E0,
                LowerDrawbridgeFunctionRva + LowerDrawbridgeFunctionLength,
                false,
                "lowered drawbridge");
            ValidateRefreshOrder(
                memory,
                CompletedDrawbridgeHookRva,
                DrawbridgeConnectivityCallRva,
                0x6CDD0,
                DrawbridgePathfindingRefreshJumpRva,
                0x725E0,
                DrawbridgeFunctionRva + DrawbridgeFunctionLength,
                true,
                "completed drawbridge",
                "connectivity update");
            int cancellationCallRva = checked(PlannedMoatCancellationRva + PlannedMoatCancellationLength);
            if (memory[cancellationCallRva] != 0xE8 ||
                checked(cancellationCallRva + 5 + ReadInt32(memory, cancellationCallRva + 1)) != 0x61ED0)
            {
                throw new InvalidOperationException("The planned moat cancellation CALL target differs.");
            }
            int removalCallTarget = checked(DirectRemovalHeightRva + 8 +
                ReadInt32(memory, DirectRemovalHeightRva + 4));
            if (removalCallTarget != 0x61ED0)
                throw new InvalidOperationException("The direct moat-removal call target differs.");
        }

        internal static byte CalculateCompletedHeight(byte defaultHeight) =>
            defaultHeight > MoatDepth ? (byte)(defaultHeight - MoatDepth) : (byte)0;

        internal static byte CalculateRestoredHeight(byte defaultHeight) => defaultHeight;

        private static void ValidateLoweredDrawbridgeRewriteContract(ReadOnlySpan<byte> memory)
        {
            // mov byte ptr [RBX + RDI + TileHeightGridOffset], 0
            if (memory[LowerDrawbridgeHeightWriteRva] != 0xC6 ||
                memory[LowerDrawbridgeHeightWriteRva + 1] != 0x84 ||
                memory[LowerDrawbridgeHeightWriteRva + 2] != 0x1F ||
                ReadInt32(memory, LowerDrawbridgeHeightWriteRva + 3) != TileHeightGridOffset ||
                memory[LowerDrawbridgeHeightWriteRva + 7] != 0)
            {
                throw new InvalidOperationException(
                    "The lowered-drawbridge height write is not [RBX+RDI] with Vanilla height zero.");
            }

            // lea RDI, [image base]
            if (memory[LowerDrawbridgeImageBaseLeaRva] != 0x48 ||
                memory[LowerDrawbridgeImageBaseLeaRva + 1] != 0x8D ||
                memory[LowerDrawbridgeImageBaseLeaRva + 2] != 0x3D ||
                checked(LowerDrawbridgeImageBaseLeaRva + LowerDrawbridgeImageBaseLeaLength +
                    ReadInt32(memory, LowerDrawbridgeImageBaseLeaRva + 3)) != 0)
            {
                throw new InvalidOperationException(
                    "The lowered-drawbridge continuation no longer restores RDI to the image base.");
            }
        }

        private static void ValidateCompletedDrawbridgeRewriteContract(ReadOnlySpan<byte> memory)
        {
            // mov RCX, RBX; call FUN_1800725A0
            if (memory[CompletedDrawbridgeHookRva] != 0x48 ||
                memory[CompletedDrawbridgeHookRva + 1] != 0x8B ||
                memory[CompletedDrawbridgeHookRva + 2] != 0xCB)
            {
                throw new InvalidOperationException(
                    "The completed-drawbridge hook no longer loads the manager into RCX before the state call.");
            }

            // mov byte ptr [RBX + R14 + TileHeightGridOffset], 0
            if (memory[CompletedDrawbridgeHeightWriteRva] != 0x41 ||
                memory[CompletedDrawbridgeHeightWriteRva + 1] != 0xC6 ||
                memory[CompletedDrawbridgeHeightWriteRva + 2] != 0x84 ||
                memory[CompletedDrawbridgeHeightWriteRva + 3] != 0x1E ||
                ReadInt32(memory, CompletedDrawbridgeHeightWriteRva + 4) != TileHeightGridOffset ||
                memory[CompletedDrawbridgeHeightWriteRva + 8] != 0)
            {
                throw new InvalidOperationException(
                    "The completed-drawbridge height write is not [RBX+R14] with Vanilla height zero.");
            }
        }

        private static void ValidateDrawbridgeRendererContract(ReadOnlySpan<byte> memory)
        {
            AssertBytes(memory, DrawbridgeAnimatedRendererTypeCheckRva,
                DrawbridgeAnimatedRendererTypeCheckBytes,
                "drawbridge animated-renderer type gate");
            AssertBytes(memory, DrawbridgeAnimatedRendererTileFlagsRva,
                DrawbridgeAnimatedRendererTileFlagsBytes,
                "drawbridge animated-renderer tile-flags gate");
            AssertBytes(memory, DrawbridgeAnimatedRendererBuildingArgumentRva,
                DrawbridgeAnimatedRendererBuildingArgumentBytes,
                "drawbridge animated-renderer building argument");
            ValidateBlock(memory, DrawbridgeAnimatedRendererArgumentsRva,
                DrawbridgeAnimatedRendererArgumentsLength, MainRendererRva,
                MainRendererLength, DrawbridgeAnimatedRendererArgumentsBytes,
                "drawbridge animated-renderer arguments");
            if (DrawbridgeAnimatedRendererArgumentsRva +
                    DrawbridgeAnimatedRendererArgumentsLength !=
                DrawbridgeAnimatedRendererCallRva)
            {
                throw new InvalidOperationException(
                    "The drawbridge animated-renderer continuation differs.");
            }
            ValidateRelativeBranch(memory, DrawbridgeAnimatedRendererCallRva, 0xE8,
                DrawbridgeAnimatedRendererRva, "drawbridge animated-renderer call");

            ValidateBlock(memory, DrawbridgeSpecialRendererRva,
                DrawbridgeSpecialRendererHookLength, DrawbridgeSpecialRendererRva,
                DrawbridgeSpecialRendererLength, DrawbridgeSpecialRendererHookBytes,
                "drawbridge special-renderer prologue");
            if (DrawbridgeSpecialRendererRva + DrawbridgeSpecialRendererHookLength !=
                DrawbridgeSpecialRendererContinuationRva)
            {
                throw new InvalidOperationException(
                    "The drawbridge special-renderer continuation differs.");
            }
            AssertBytes(memory, DrawbridgeSpecialRendererBuildingRecordRva,
                DrawbridgeSpecialRendererBuildingRecordBytes,
                "drawbridge special-renderer building-record setup");
            if (ReadInt32(memory, DrawbridgeSpecialRendererBuildingRecordRva + 13) !=
                BuildingRecordStride)
            {
                throw new InvalidOperationException(
                    "The drawbridge special renderer no longer derives its record from EDX using stride 0x32C.");
            }

            AssertBytes(memory, DrawbridgeSpecialRendererCall1ArgumentsRva,
                DrawbridgeSpecialRendererCall1ArgumentsBytes,
                "first height-blind drawbridge renderer arguments");
            AssertBytes(memory, DrawbridgeSpecialRendererCall2ArgumentsRva,
                DrawbridgeSpecialRendererCall2ArgumentsBytes,
                "second height-blind drawbridge renderer arguments");
            AssertBytes(memory, DrawbridgeSpecialRendererCall1BuildingArgumentRva,
                DrawbridgeSpecialRendererBuildingArgumentBytes,
                "first drawbridge special-renderer building argument");
            AssertBytes(memory, DrawbridgeSpecialRendererCall2BuildingArgumentRva,
                DrawbridgeSpecialRendererBuildingArgumentBytes,
                "second drawbridge special-renderer building argument");
            ValidateRelativeBranch(memory, DrawbridgeSpecialRendererCall1Rva, 0xE8,
                DrawbridgeSpecialRendererRva, "first drawbridge special-renderer call");
            ValidateRelativeBranch(memory, DrawbridgeSpecialRendererCall2Rva, 0xE8,
                DrawbridgeSpecialRendererRva, "second drawbridge special-renderer call");

            ValidateBlock(memory, DrawbridgeStaticRendererArgumentsRva,
                DrawbridgeStaticRendererArgumentsLength, MainRendererRva,
                MainRendererLength, DrawbridgeStaticRendererArgumentsBytes,
                "drawbridge static-renderer arguments");
            if (DrawbridgeStaticRendererArgumentsRva +
                    DrawbridgeStaticRendererArgumentsLength !=
                DrawbridgeStaticRendererContinuationRva)
            {
                throw new InvalidOperationException(
                    "The drawbridge static-renderer continuation differs.");
            }
            int heightAddress = checked(DrawbridgeStaticRendererArgumentsRva +
                DrawbridgeStaticRendererHeightSubtractLength +
                ReadInt32(memory, DrawbridgeStaticRendererArgumentsRva + 3));
            if (heightAddress != CurrentRenderedTileHeightRva)
                throw new InvalidOperationException("The rendered tile-height source differs.");
            ValidateRelativeBranch(memory, DrawbridgeHeightAwareCallRva, 0xE8,
                DrawbridgeHeightAwareRendererRva, "height-aware drawbridge renderer call");

            ValidateRelativeBranch(memory, UnitType2SpriteQueueCall1Rva, 0xE8,
                UnitSpriteQueueRva, "first type-2 unit sprite-queue call");
            ValidateRelativeBranch(memory, UnitType2SpriteQueueCall2Rva, 0xE8,
                UnitSpriteQueueRva, "second type-2 unit sprite-queue call");
            AssertBytes(memory, UnitType52HeightForwardingRva,
                UnitType52HeightForwardingBytes,
                "type-52 unit interpolation height forwarding");
        }

        private static void ValidateUnitDrawbridgeHeightContract(ReadOnlySpan<byte> memory)
        {
            AssertBytes(memory,
                UnitHeightCorrectionFunctionPrologueRva,
                UnitHeightCorrectionFunctionPrologueBytes,
                "unit height-correction function prologue");
            const int ImageBaseLeaOffset = 23;
            int imageBaseTarget = checked(UnitHeightCorrectionFunctionPrologueRva +
                ImageBaseLeaOffset + 7 +
                ReadInt32(memory, UnitHeightCorrectionFunctionPrologueRva +
                    ImageBaseLeaOffset + 3));
            if (imageBaseTarget != 0)
                throw new InvalidOperationException("The unit height writer no longer keeps the image base in RBP.");

            ValidateBlock(memory,
                UnitDrawbridgeHeightCorrectionRva,
                UnitDrawbridgeHeightCorrectionLength,
                UnitHeightCorrectionFunctionRva,
                UnitHeightCorrectionFunctionLength,
                UnitDrawbridgeHeightCorrectionBytes,
                "unit drawbridge vertical-correction block");
            AssertBytes(memory,
                UnitDrawbridgeTypeGateRva,
                UnitDrawbridgeTypeGateBytes,
                "unit drawbridge building-type gate");
            AssertBytes(memory,
                UnitDrawbridgeHeightContinuationRva,
                UnitDrawbridgeHeightContinuationBytes,
                "unit drawbridge vertical-correction continuation");
            AssertBytes(memory,
                UnitHeightPostCorrectionRva,
                UnitHeightPostCorrectionBytes,
                "unit post-height register and flag reinitialization");

            if (UnitDrawbridgeTypeGateRva + UnitDrawbridgeTypeGateBytes.Length !=
                    UnitDrawbridgeHeightCorrectionRva ||
                UnitDrawbridgeHeightCorrectionRva + UnitDrawbridgeHeightCorrectionLength !=
                    UnitDrawbridgeHeightContinuationRva)
            {
                throw new InvalidOperationException(
                    "The unit drawbridge height gate, rewrite span, or continuation differs.");
            }

            ValidateRelativeBranch(memory, UnitHeightInitializationCallRva, 0xE9,
                UnitHeightCorrectionFunctionRva, "unit initialization height-update tail jump");
            ValidateRelativeBranch(memory, UnitHeightUpdateCall1Rva, 0xE8,
                UnitHeightCorrectionFunctionRva, "first unit-loop height-update call");
            ValidateRelativeBranch(memory, UnitHeightUpdateCall2Rva, 0xE8,
                UnitHeightCorrectionFunctionRva, "second unit-loop height-update call");
            ValidateRelativeBranch(memory, UnitHeightUpdateCall3Rva, 0xE8,
                UnitHeightCorrectionFunctionRva, "third unit-loop height-update call");
            ValidateRelativeBranch(memory, UnitHeightUpdateCall4Rva, 0xE8,
                UnitHeightCorrectionFunctionRva, "fourth unit-loop height-update call");
            if (UnitHeightInitializationCallRva < UnitHeightInitializationFunctionRva ||
                UnitHeightInitializationCallRva >= UnitHeightUpdateFunctionRva ||
                UnitHeightUpdateCall1Rva < UnitHeightUpdateFunctionRva ||
                UnitHeightUpdateCall4Rva >= UnitHeightCorrectionFunctionRva)
            {
                throw new InvalidOperationException(
                    "The unit height-writer caller functions or call-site ranges differ.");
            }
        }

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

        private static void ValidateRefreshOrder(
            ReadOnlySpan<byte> memory,
            int heightRva,
            int operationRva,
            int operationTargetRva,
            int pathfindingRefreshRva,
            int pathfindingRefreshTargetRva,
            int functionEndRva,
            bool pathfindingIsJump,
            string description,
            string firstOperation = "graphic refresh")
        {
            if (heightRva >= operationRva || operationRva >= pathfindingRefreshRva ||
                pathfindingRefreshRva + 5 > functionEndRva)
            {
                throw new InvalidOperationException(
                    $"The audited {description} height/{firstOperation}/pathfinding order differs.");
            }

            ValidateRelativeBranch(memory, operationRva, 0xE8, operationTargetRva,
                $"{description} {firstOperation}");
            ValidateRelativeBranch(memory, pathfindingRefreshRva, pathfindingIsJump ? (byte)0xE9 : (byte)0xE8,
                pathfindingRefreshTargetRva, $"{description} pathfinding refresh");
        }

        private static void ValidateRelativeBranch(
            ReadOnlySpan<byte> memory,
            int branchRva,
            byte opcode,
            int expectedTargetRva,
            string description)
        {
            if (branchRva < 0 || branchRva + 5 > memory.Length || memory[branchRva] != opcode ||
                checked(branchRva + 5 + ReadInt32(memory, branchRva + 1)) != expectedTargetRva)
            {
                throw new InvalidOperationException($"The audited {description} target differs.");
            }
        }

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

    // Native mode protocol from FUN_18006FE90; Script Extender 2.4.0 still exposes no enum for this operation mode.
    internal enum ElevatedMoatNativeMode
    {
        Plan = 0,
        CancelPlan = 1,
        DirectCreate = 2,
        DirectRemove = 3,
    }
}

