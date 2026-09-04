using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Zhuqiaomon.Assembly;

namespace MoveMoatTest
{
    internal sealed unsafe class MoveMoatPathTest : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int UnitStandingOnCompletedMoatDelegate(IntPtr unitManager, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int RegionReachabilityDelegate(
            IntPtr pathManager, int movementClass, int targetRegion, int startX, int startY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int TribeFloodFillMembershipDelegate(
            IntPtr tribeManager, int tribeId, int floodFillStamp);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int FirstGroupUnitOnCompletedMoatDelegate(
            IntPtr tribeManager, int tribeId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetGroupUnitIdDelegate(
            IntPtr tribeManager, int tribeId, int ordinal);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorReachabilityDelegate(
            IntPtr pathManager, int nativeUnitIndex, int targetX, int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorTilePairFallbackSelectionDelegate(IntPtr selectionState);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int SelectionCanDigMoatDelegate(IntPtr selectionState);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorTilePairReachabilityDelegate(
            IntPtr pathManager, int targetTileId, int selectedUnitTileId, byte useCache);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetRepresentativeSelectedUnitDelegate(IntPtr unitManager, int startIndex);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorRegionPrecheckDelegate(IntPtr pathManager, int nativeUnitIndex);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CentralMovementPlanDelegate(
            IntPtr unitManager, int unitId, int targetX, int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PathBuilderDelegate(
            IntPtr pathManager, int movementClass, int movementProfile);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetMoatIdAtTileDelegate(IntPtr tileManager, int tileId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void AttackApproachFloodBuilderDelegate(
            IntPtr pathManager,
            int tribeId,
            int targetContext,
            uint targetX,
            uint targetY,
            int requestedResults,
            int sourceRegion,
            int movementClass);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void BuildingApproachBuilderDelegate(
            IntPtr pathManager,
            int tribeId,
            int buildingId,
            int requestedResults,
            int sourceRegion,
            int movementClass);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void BuildingCandidateConsumerDelegate(
            IntPtr tribeManager, int tribeId, int builderVariant);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AllSelectedUnitsAssassinsDelegate(IntPtr tribeManager, int tribeId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int RegionPairReachabilityDelegate(
            IntPtr pathManager,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int BuildingCursorReachabilityDelegate(
            IntPtr buildingManager, int buildingId, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void CombatFinishResumeDelegate(IntPtr unitManager, int unitId);

        private const int CentralMovementPlanRva = 0x18E1E0;
        private const int TribeFloodFillMembershipRva = 0x124740;
        private const int FirstGroupUnitOnCompletedMoatRva = 0x117BC0;
        private const int GetGroupUnitIdRva = 0x119F90;
        private const int GroupMoatModeCallRva = 0x11B666;
        private const int UnitStandingOnCompletedMoatRva = 0x196840;
        private const int RegionReachabilityRva = 0xE7C40;
        private const int CursorReachabilityRva = 0xE9FF0;
        private const int CursorTilePairFallbackSelectionRva = 0x196870;
        private const int SelectionCanDigMoatRva = 0x191C00;
        private const int SelectionCanDigMoatCallRva = 0x8D3CE;
        private const int CursorTilePairReachabilityRva = 0xE2CA0;
        private const int GetRepresentativeSelectedUnitRva = 0x18D460;
        private const int CursorRegionPrecheckRva = 0xE9D90;
        private const int PathBuilderRva = 0xF4930;
        private const int GroundPathBuilderRva = 0xDA590;
        // Read-only call-target validation for the existing Vanilla building consumer.
        // MoveMoatTest neither detours nor otherwise changes this Assassin builder.
        private const int AssassinPathBuilderRva = 0xD9C40;
        private const int AlternativePathBuilderRva = 0xDB650;
        private const int GetMoatIdAtTileRva = 0x69560;
        private const int AttackApproachFloodBuilderRva = 0xDBC60;
        private const int BuildingApproachBuilderRva = 0xDA020;
        private const int BuildingCandidateConsumerRva = 0x123090;
        private const int AllSelectedUnitsAssassinsRva = 0x117820;
        private const int RegionPairReachabilityRva = 0xE2610;
        private const int AttackApproachFloodCallRva = 0x11EE47;
        private const int AttackApproachFloodAlternativeCallRva = 0x11F46B;
        private const int BuildingApproachCallRva = 0x11FF9A;
        private const int BuildingCandidateConsumerCallRva = 0x11FFA7;
        private const int BuildingCandidateConsumerAlternativeCallRva = 0x1206DF;
        private const int BuildingCandidateConsumerForceCallRva = 0x120CCD;
        private const int AttackFloodAssassinSelectionCallRva = 0xDBC89;
        private const int AttackFloodRegionPairCallRva = 0xDBF0D;
        private const int AttackFloodTilePairCallRva = 0xDBF33;
        private const int BuildingApproachAssassinSelectionCallRva = 0xDA0B8;
        private const int BuildingApproachRegionPairCallRva = 0xDA1F9;
        private const int BuildingApproachTilePairCallRva = 0xDA232;
        private const int BuildingApproachAlternativeRegionPairCallRva = 0xDA47C;
        private const int BuildingApproachAlternativeTilePairCallRva = 0xDA4B1;
        private const int BuildingConsumerAssassinSelectionCallRva = 0x1230AA;
        private const int BuildingConsumerFallbackBuilderCallRva = 0x123102;
        private const int BuildingConsumerAssassinBuilderCallRva = 0x123125;
        private const int BuildingConsumerGroundBuilderCallRva = 0x12312C;
        private const int BuildingCursorReachabilityRva = 0xB70C0;
        private const int BuildingCursorReachabilityCallRva = 0x8DFF6;
        private const int CombatFinishResumeRva = 0x1853F0;
        private const int CombatFinishPostCombatCallRva = 0x18540D;
        private const int PostCombatRepathRva = 0x1976C0;
        private const int PostCombatMoveHereCallRva = 0x19772B;
        private const int MovementTerrainPhaseRva = 0x19B506;
        private const int MovementCadenceRva = 0x184203;
        private const int MovementSubstepRva = 0x1855A0;
        private const int MovementAdditionalSubstepsRva = 0x1857AA;
        private const int MoatPathConsumptionReadRva = 0x185934;
        private const int MoatPathConsumptionPersistRva = 0x19670C;
        private const int CursorCurrentTileFlagGateRva = 0x8F388;
        private const int CursorCurrentTileFlagGateJumpRva = 0x8F393;
        private const int AttackUnitPairGateJumpRva = 0x8D72B;
        private const int AttackBuildingPairGateJumpRva = 0x8E2C6;
        private const int AttackAlternativePairGateJumpRva = 0x8E557;
        private const int TileFlagsRva = 0x48F71B0;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int NativeMovementMaskRva = 0x51890D0;
        private const int RowLookupRva = 0x402FF2C;
        private const int NativeBuildingLayerRva = 0x4B6AA50;
        private const int NativeHeightLayerRva = 0x4DDD350;
        private const int NativeDirectionMaskRva = 0x312620;
        private const int CursorTargetXRva = 0x3A11E2C;
        private const int CursorTargetYRva = 0x3A11E30;
        private const int PathRegionGridRva = 0x50EC690;
        private const int MoatPathModeRva = 0x60AD6E4;
        private const int NativePathManagerRva = 0x60AD660;
        private const int NativeUnitManagerRva = 0x67E8400;
        private const int NativeTribeManagerRva = 0x7CC6720;

        private const int TribeRecordSize = 0x688;
        private const int TribeLeadUnitIdOffset = 0x5A;
        private const int TribeUnitCountOffset = 0x5C;
        private const int UnitGroupInactiveStateOffset = 0x29C;
        private const int UnitMoatSlowdownPhaseOffset = 0x6C;
        private const int UnitPostCombatMovementStateOffset = 0x88;
        private const int UnitCombatFinishGateOffset = 0x33A;
        // 0x1855A0 reads this currently unnamed short in addition to r_SpeedBonus.
        private const int UnitAdditionalMovementSubstepsOffset = 0x3CE;
        private const int MaximumTribeCount = 4500;
        private const int MaximumUnitCount = 10000;
        private static readonly int[] EndpointNeighbourX = { -1, 1, 0, 0, -1, 1, -1, 1 };
        private static readonly int[] EndpointNeighbourY = { 0, 0, -1, 1, -1, -1, 1, 1 };
        private static readonly byte[] EndpointSourceEdgeMasks =
            { 0x04, 0x40, 0x10, 0x01, 0x08, 0x20, 0x02, 0x80 };

        private const int MoatRecordArrayOffset = 0x1F3EE30;
        private const int MoatRecordCountOffset = 0x2038E30;
        private const int MoatRecordSize = 0x10;
        private const int MoatOwnerOffset = 0x0C;

        private const int MaximumRegionId = short.MaxValue;
        private const int MaximumFloodFillStamp = 0x7D00;
        private const int MapWidth = 800;
        private const int MapCellCount = MapWidth * MapWidth;
        // Tile-indexed native arrays contain 0x4E520 entries. MapCellCount is only
        // the rectangular coordinate-cell count used by the managed BFS buffers.
        private const int NativeTileCount = 0x4E520;
        private const int MoatStateBit = 1 << 20;
        private const uint CompletedMoatTileFlag = 0x40000000;
        private const uint AlternativeTerrainDelayTileFlag = 0x00200000;
        private const uint OrdinaryWalkableTileFlag = 0x00008000;
        private const uint CursorSpecialStructureTileFlagMask = 0x10000300;
        private const uint BuildingContextBlockingTileFlagMask = 0x0F000000;
        private const int PathManagerRouteVariantOffset = 0x80;
        private const int PathManagerMovementVariantOffset = 0x84;
        private const int PathManagerAssassinModeOffset = 0x88;
        private const int PathManagerFloodGenerationOffset = 0x04;
        private const int PathManagerFloodDepthOffset = 0x155F38;
        private const int PathManagerFloodQueueHeadOffset = 0x155F3C;
        private const int PathManagerFloodQueueTailOffset = 0x155F44;
        private const int PathManagerFloodResultTileOffset = 0x1B344;
        private const int PathManagerFloodResultStride = 0x0C;
        private const int PathManagerOutputBufferOffset = 0x155F60;
        private const int PathManagerOutputLengthOffset = 0x155F68;
        private const int NativeUnitPathBufferOffset = 0xB4FE78;
        private const int NativeUnitPathBufferStride = 1000;
        private const int NativeUnitStride = 0x490;
        private const int NativeUnitSlotDataOffset = 0x65C;
        private const int NativeMoatPathConsumptionModeOffset = 0x9C8;
        private const int UnitMoatPathConsumptionModeOffset =
            NativeMoatPathConsumptionModeOffset - NativeUnitSlotDataOffset;
        private const int VanillaAttackFloodResultCapacity = 500;
        private const int DiagnosticStallTickThreshold = 100;
        private const int WeightedPublicationSafetyMarginTicks = 40;
        private const int BuildingCandidateApproachTileOffset = 0x00;
        private const int BuildingCandidateFootprintTileOffset = 0x04;
        private const int BuildingCandidateScoreOffset = 0x08;
        private const int VanillaUnreachableCandidateScore = 10000000;
        private const ulong RouteFingerprintOffsetBasis = 14695981039346656037UL;
        private const ulong RouteFingerprintPrime = 1099511628211UL;

        private const string TribeFloodFillMembershipPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 " +
            "48 83 EC 20 48 63 F2 33 DB 4C 69 CE 88 06 00 00 45 8B F0 48 8B E9";

        private const string FirstGroupUnitOnCompletedMoatPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 " +
            "41 56 48 83 EC 20 48 63 F2 33 DB 4C 69 C6 88 06 00 00 48 8B E9 " +
            "41 0F BF 7C 08 5C 85 FF 7E 58 4C 8D 35 ?? ?? ?? ??";

        private const string GetGroupUnitIdPattern =
            "48 89 5C 24 08 48 63 C2 45 33 C9 48 69 D0 88 06 00 00 4C 8B D9 " +
            "66 83 7C 0A 40 02 75 5A 0F BF 44 0A 5C 44 3B C0 7D 50 45 85 C0 " +
            "78 4B 48 83 C1 60";

        private const string CentralMovementPlanPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC 38 04 00 00 " +
            "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 20 04 00 00 4C 63 FA " +
            "4C 8D 35 ?? ?? ?? ?? 49 69 DF 90 04 00 00 49 63 E8 48 03 D9 49 63 F1";

        private const string UnitStandingOnCompletedMoatPattern =
            "48 63 C2 48 69 D0 90 04 00 00 48 63 84 0A 2C 07 00 00 " +
            "48 8D 0D ?? ?? ?? ?? 8B 04 81 C1 E8 1E 83 E0 01 C3";

        private const string RegionReachabilityPattern =
            "44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 38 45 33 D2 49 63 F9 4C 89 51 48 48 8B D9";

        private const string PathBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 " +
            "48 63 41 0C 48 8B D9 41 8B F0 44 8B D2";

        private const string GetMoatIdAtTilePattern =
            "48 63 C2 0F B7 84 41 ?? ?? ?? ?? C3 CC CC CC";

        private const string AttackApproachFloodBuilderPattern =
            "44 89 4C 24 20 53 56 41 54 41 55 41 56 48 83 EC 60 " +
            "48 8B D9 4D 63 E9 45 33 F6 48 8D 0D ?? ?? ?? ?? 45 8B E6 " +
            "44 89 74 24 3C E8 ?? ?? ?? ?? 48 63 F0 41 81 FD 1F 03 00 00";

        private const string BuildingApproachBuilderPattern =
            "48 89 4C 24 08 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 " +
            "48 8D 0D ?? ?? ?? ?? 4C 63 D2 49 69 D2 88 06 00 00 4D 63 F0 " +
            "4C 8D 25 ?? ?? ?? ?? 4D 69 FE 2C 03 00 00 33 ED 41 8B D9 44 8B ED";

        private const string BuildingCandidateConsumerPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 40 48 63 DA 41 8B F0 " +
            "8B D3 48 8B F9 E8 ?? ?? ?? ?? 4C 69 CB 88 06 00 00 " +
            "48 8D 1D ?? ?? ?? ?? 49 0F BF 4C 39 5A";

        private const string AllSelectedUnitsAssassinsPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 " +
            "41 56 48 83 EC 20 48 63 F2 33 DB 4C 69 C6 88 06 00 00 48 8B E9 " +
            "41 0F BF 7C 08 5C 85 FF 7E 4D 4C 8D 35 ?? ?? ?? ?? " +
            "66 0F 1F 44 00 00 44 8B C3 8B D6 48 8B CD E8 ?? ?? ?? ?? " +
            "48 98 FF C3 48 69 C8 90 04 00 00 66 42 83 BC 31 E4 06 00 00 02 " +
            "75 18 66 42 83 BC 31 F8 08 00 00 00 75 0C " +
            "66 42 83 BC 31 E6 06 00 00 49";

        private const string RegionPairReachabilityPattern =
            "40 55 41 54 41 55 41 56 48 8D AC 24 78 F7 FF FF " +
            "48 81 EC 88 09 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 " +
            "48 89 85 70 08 00 00 4C 63 F2 45 8B E9 4C 8B E1";

        private const string CursorReachabilityFunctionPattern =
            "44 89 4C 24 20 44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 50 48 63 F2 45 33 ED 33 D2 49 63 E8 49 63 C1 48 8B D9";

        private const string CursorTilePairFallbackSelectionPattern =
            "83 B9 BC 05 00 00 00 74 27 33 C0 48 81 C1 64 05 00 00 48 83 F8 16 " +
            "74 05 83 39 00 75 13 48 FF C0 48 83 C1 04 48 83 F8 23 7C E8 B8 01 00 00 00 C3";

        private const string SelectionCanDigMoatPattern =
            "83 B9 80 05 00 00 00 75 54 83 B9 B4 05 00 00 00 75 4B " +
            "83 B9 68 05 00 00 00 75 42 83 B9 64 05 00 00 00 75 39 " +
            "83 B9 6C 05 00 00 00 75 30 83 B9 E8 05 00 00 00 75 27 " +
            "83 B9 EC 05 00 00 00 75 1E 83 B9 E0 05 00 00 00 75 15 " +
            "83 B9 D8 05 00 00 00 75 0C 83 B9 74 05 00 00 00 75 03 " +
            "33 C0 C3 B8 01 00 00 00 C3";

        private const string SelectionCanDigMoatCallPattern =
            "44 39 25 ?? ?? ?? ?? 74 3C 48 8B CE E8 ?? ?? ?? ?? 85 C0 74 30 B8 01 00 00 00";

        private const string BuildingCursorReachabilityPattern =
            "48 89 5C 24 08 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 40 " +
            "4C 8B E1 85 D2 0F 84 ?? ?? ?? ?? 81 FA A0 0F 00 00 0F 8D ?? ?? ?? ?? " +
            "48 63 C2 48 69 D0 2C 03 00 00";

        private const string CombatFinishResumePattern =
            "40 53 48 83 EC 20 48 63 C2 48 69 D8 90 04 00 00 48 03 D9 " +
            "66 83 BB 96 09 00 00 00 75 14 E8 ?? ?? ?? ?? 33 C0 " +
            "66 89 83 96 09 00 00 89 83 98 09 00 00";

        private const string PostCombatRepathPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 30 48 63 FA 48 8B F1 " +
            "48 69 DF 90 04 00 00 48 03 D9 66 83 BB F8 08 00 00 00";

        private const string CursorRegionPrecheckPattern =
            "40 53 55 57 41 54 41 56 48 83 EC 20 FF 41 04 48 8B D9 81 79 04 00 7D 00 00 " +
            "41 BC 01 00 00 00 48 63 FA 7E 1F 44 89 61 04";

        private const string CursorCurrentTileFlagGatePattern =
            "F7 84 97 00 84 89 00 00 01 00 10 74 45 41 8B D6";

        private const string CursorTilePairReachabilityPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 " +
            "48 83 EC 40 4C 8D 3D ?? ?? ?? ?? 4C 63 DA 45 0F B6 F1 48 8B D9 " +
            "4D 63 C8 4F 8D 04 1B 43 0F BF B4 38 90 C6 0E 05";

        private const string GetRepresentativeSelectedUnitPattern =
            "48 89 5C 24 18 55 48 8D 6C 24 80 48 81 EC 80 01 00 00 " +
            "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 70 45 33 DB 48 8D 04 24 " +
            "4C 8B C9 0F 57 C0 45 33 C0 41 8D 4B 02";

        private const string AttackUnitPairGatePattern =
            "85 C0 75 48 49 8B CC E8 ?? ?? ?? ?? 85 C0 74 23 46 8B 84 26 2C 07 00 00 " +
            "48 8D 0D ?? ?? ?? ?? 42 8B 94 23 2C 07 00 00 41 B1 01";

        private const string AttackBuildingPairGatePattern =
            "48 8B CE E8 ?? ?? ?? ?? 48 8D 15 ?? ?? ?? ?? 85 C0 74 63 48 63 C7 " +
            "48 69 C8 2C 03 00 00 0F BF 84 19 2E 01 00 00 83 C0 D3";

        private const string AttackAlternativePairGatePattern =
            "85 C0 75 48 49 8B CC E8 ?? ?? ?? ?? 85 C0 74 23 45 8B 84 2C 2C 07 00 00 " +
            "48 8D 0D ?? ?? ?? ?? 43 8B 94 26 2C 07 00 00 41 B1 01";

        private static readonly byte[] CursorGateJumpOriginal = { 0x74, 0x45 };
        private static readonly byte[] AttackUnitPairGateOriginal = { 0x74, 0x23 };
        private static readonly byte[] AttackBuildingPairGateOriginal = { 0x74, 0x63 };
        private static readonly byte[] AttackAlternativePairGateOriginal = { 0x74, 0x23 };

        private readonly ManualLogSource log;
        private readonly int* moatPathMode;
        private readonly int* cursorTargetX;
        private readonly int* cursorTargetY;
        private readonly byte* nativeUnitManager;
        private readonly uint* tileFlags;
        private readonly byte* movementTargetAvailability;
        private readonly byte* nativeMovementMasks;
        private readonly int* nativeRowLookup;
        private readonly ushort* nativeBuildingLayer;
        private readonly byte* nativeHeightLayer;
        private readonly byte* nativeDirectionMasks;
        private readonly short* pathRegionGrid;
        private readonly IntPtr nativePathManager;
        private readonly IntPtr nativeTribeManager;
        private readonly WeightedMoatRoutePlanner weightedMoatRoutePlanner;
        private readonly NativeMovementCadenceResolver nativeMovementCadenceResolver;

        [ThreadStatic]
        private static MoveCommandScope activeMoveCommand;
        [ThreadStatic]
        private static PlanScope activePlan;
        [ThreadStatic]
        private static PlanScope pendingPlan;
        [ThreadStatic]
        private static AttackCursorPairScope pendingAttackCursorPair;
        [ThreadStatic]
        private static CursorSelectionDiagnosticScope pendingCursorSelectionDiagnostic;
        [ThreadStatic]
        private static AttackCommandScope activeAttackCommand;
        [ThreadStatic]
        private static AttackApproachDiagnosticScope activeAttackApproachDiagnostic;
        [ThreadStatic]
        private static BuildingApproachPerformanceScope activeBuildingApproachPerformance;
        [ThreadStatic]
        private static BuildingConsumerPerformanceScope activeBuildingConsumerPerformance;
        private CentralMovementPlanDelegate originalCentralMovementPlan;
        private CentralMovementPlanDelegate rootedCentralMovementPlan;
        private PathBuilderDelegate originalPathBuilder;
        private PathBuilderDelegate rootedPathBuilder;
        private UnitStandingOnCompletedMoatDelegate originalUnitStandingOnCompletedMoat;
        private UnitStandingOnCompletedMoatDelegate rootedUnitStandingOnCompletedMoat;
        private RegionReachabilityDelegate originalRegionReachability;
        private RegionReachabilityDelegate rootedRegionReachability;
        private TribeFloodFillMembershipDelegate originalTribeFloodFillMembership;
        private TribeFloodFillMembershipDelegate rootedTribeFloodFillMembership;
        private FirstGroupUnitOnCompletedMoatDelegate originalFirstGroupUnitOnCompletedMoat;
        private FirstGroupUnitOnCompletedMoatDelegate rootedFirstGroupUnitOnCompletedMoat;
        private GetGroupUnitIdDelegate getGroupUnitId;
        private CursorReachabilityDelegate originalCursorReachability;
        private CursorReachabilityDelegate rootedCursorReachability;
        private CursorTilePairFallbackSelectionDelegate originalCursorTilePairFallbackSelection;
        private CursorTilePairFallbackSelectionDelegate rootedCursorTilePairFallbackSelection;
        private SelectionCanDigMoatDelegate selectionCanDigMoat;
        private CursorTilePairReachabilityDelegate originalCursorTilePairReachability;
        private CursorTilePairReachabilityDelegate rootedCursorTilePairReachability;
        private GetRepresentativeSelectedUnitDelegate getRepresentativeSelectedUnit;
        private CursorRegionPrecheckDelegate originalCursorRegionPrecheck;
        private CursorRegionPrecheckDelegate rootedCursorRegionPrecheck;
        private GetMoatIdAtTileDelegate getMoatIdAtTile;
        private AttackApproachFloodBuilderDelegate originalAttackApproachFloodBuilder;
        private AttackApproachFloodBuilderDelegate rootedAttackApproachFloodBuilder;
        private BuildingApproachBuilderDelegate originalBuildingApproachBuilder;
        private BuildingApproachBuilderDelegate rootedBuildingApproachBuilder;
        private BuildingCandidateConsumerDelegate originalBuildingCandidateConsumer;
        private BuildingCandidateConsumerDelegate rootedBuildingCandidateConsumer;
        private AllSelectedUnitsAssassinsDelegate allSelectedUnitsAssassins;
        private RegionPairReachabilityDelegate originalRegionPairReachability;
        private RegionPairReachabilityDelegate rootedRegionPairReachability;
        private BuildingCursorReachabilityDelegate originalBuildingCursorReachability;
        private BuildingCursorReachabilityDelegate rootedBuildingCursorReachability;
        private CombatFinishResumeDelegate originalCombatFinishResume;
        private CombatFinishResumeDelegate rootedCombatFinishResume;

        private NativeDetour centralMovementPlanDetour;
        private NativeDetour pathBuilderDetour;
        private NativeDetour unitStandingOnCompletedMoatDetour;
        private NativeDetour regionReachabilityDetour;
        private NativeDetour tribeFloodFillMembershipDetour;
        private NativeDetour firstGroupUnitOnCompletedMoatDetour;
        private NativeDetour cursorReachabilityDetour;
        private NativeDetour cursorTilePairFallbackSelectionDetour;
        private NativeDetour cursorTilePairReachabilityDetour;
        private NativeDetour cursorRegionPrecheckDetour;
        private NativeDetour attackApproachFloodBuilderDetour;
        private NativeDetour buildingApproachBuilderDetour;
        private NativeDetour buildingCandidateConsumerDetour;
        private NativeDetour regionPairReachabilityDetour;
        private NativeDetour buildingCursorReachabilityDetour;
        private NativeDetour combatFinishResumeDetour;
        private IDisposable tribeMoveSubscription;
        private IDisposable tribeTargetSubscription;
        private IDisposable mapLoadSubscription;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;
        private bool attackTickSubscribed;

        private int[] visitedWithoutMoat;
        private int[] visitedWithMoat;
        private int[] distanceWithoutMoat;
        private int[] distanceWithMoat;
        private int[] rejectedMoat;
        private int[] queue;
        private int gridGeneration;
        private int mapEpoch;
        private int cacheMapEpoch = -1;
        private int cacheUnitIndex = -1;
        private int cacheStartX = -1;
        private int cacheStartY = -1;
        private int cacheTargetRegion = -1;
        private int cachePlayerId = -1;
        private RouteProbeSummary cachedRouteSummary;
        private int lastCursorRegionPositiveGeneration = -1;
        private int lastCursorDirectPositiveGeneration = -1;
        private int lastCursorRegionBlockGeneration = -1;
        private int lastCursorDirectBlockGeneration = -1;
        private string lastAttackCursorDecision;
        private string lastCursorSelectionDiagnostic;
        private string lastCursorTilePairDiagnostic;
        private string lastCursorGroupRouteDiagnostic;
        private readonly HashSet<string> loggedBuildingCursorReachabilityDecisions =
            new HashSet<string>(StringComparer.Ordinal);
        private string cursorGroupRouteCacheKey;
        private CursorGroupRouteSummary cachedCursorGroupRoute;
        private readonly Dictionary<int, string> lastUnscopedAttackModes = new Dictionary<int, string>();
        private readonly Dictionary<int, string> lastAttackCommandCandidates = new Dictionary<int, string>();
        private readonly Dictionary<int, AttackUnitTracker> trackedAttackUnits =
            new Dictionary<int, AttackUnitTracker>();
        private readonly Dictionary<int, MoatMoveTracker> trackedMoatMoves =
            new Dictionary<int, MoatMoveTracker>();
        private readonly HashSet<string> reportedDiagnosticFailureStages =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> loggedDiggerDecisions =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> lastWeightedShadowDecisionByUnit =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> lastWeightedPublicationDecisionByUnit =
            new Dictionary<int, string>();
        private int moveCommandSequence;
        private int attackCommandSequence;
        private bool callbackFailureReported;
        private bool weightedShadowBusy;
        private bool disposed;

        public MoveMoatPathTest(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The completed-moat movement test requires the validated CrusaderDE.dll layout.");
            }

            Shared.NativeResolution floodResolution = Resolve(
                memory, TribeFloodFillMembershipPattern, TribeFloodFillMembershipRva,
                "Tribe flood-fill membership helper");
            Shared.NativeResolution groupMoatResolution = Resolve(
                memory, FirstGroupUnitOnCompletedMoatPattern, FirstGroupUnitOnCompletedMoatRva,
                "first active group unit standing on completed moat helper");
            Shared.NativeResolution groupUnitResolution = Resolve(
                memory, GetGroupUnitIdPattern, GetGroupUnitIdRva,
                "group unit iterator");
            Shared.NativeResolution planResolution = Resolve(
                memory, CentralMovementPlanPattern, CentralMovementPlanRva,
                "central ordinary-movement planner");
            Shared.NativeResolution combatFinishResumeResolution = Resolve(
                memory, CombatFinishResumePattern, CombatFinishResumeRva,
                "combat-finish movement-resume helper");
            Shared.NativeResolution postCombatRepathResolution = Resolve(
                memory, PostCombatRepathPattern, PostCombatRepathRva,
                "post-combat saved-target repath helper");
            Shared.NativeResolution modeResolution = Resolve(
                memory, UnitStandingOnCompletedMoatPattern, UnitStandingOnCompletedMoatRva,
                "unit-standing-on-completed-moat helper");
            Shared.NativeResolution regionResolution = Resolve(
                memory, RegionReachabilityPattern, RegionReachabilityRva,
                "moat-aware region reachability");
            Shared.NativeResolution builderResolution = Resolve(
                memory, PathBuilderPattern, PathBuilderRva,
                "central tile path builder");
            Shared.NativeResolution moatLookupResolution = Resolve(
                memory, GetMoatIdAtTilePattern, GetMoatIdAtTileRva,
                "moat ID lookup by tile");
            Shared.NativeResolution cursorResolution = Resolve(
                memory, CursorReachabilityFunctionPattern, CursorReachabilityRva,
                "ordinary-movement cursor reachability function");
            Shared.NativeResolution cursorModeResolution = Resolve(
                memory, CursorTilePairFallbackSelectionPattern, CursorTilePairFallbackSelectionRva,
                "cursor tile-pair fallback selection gate");
            Shared.NativeResolution selectionCanDigResolution = Resolve(
                memory, SelectionCanDigMoatPattern, SelectionCanDigMoatRva,
                "Vanilla selection-can-dig-moat helper");
            Resolve(
                memory, SelectionCanDigMoatCallPattern, SelectionCanDigMoatCallRva - 0x0C,
                "DigMoat cursor selection call context");
            Shared.NativeResolution cursorTilePairResolution = Resolve(
                memory, CursorTilePairReachabilityPattern, CursorTilePairReachabilityRva,
                "cursor tile-pair reachability helper");
            Shared.NativeResolution representativeUnitResolution = Resolve(
                memory, GetRepresentativeSelectedUnitPattern, GetRepresentativeSelectedUnitRva,
                "representative selected-unit helper");
            Shared.NativeResolution cursorRegionResolution = Resolve(
                memory, CursorRegionPrecheckPattern, CursorRegionPrecheckRva,
                "ordinary-movement cursor region precheck");
            Shared.NativeResolution cursorGateResolution = Resolve(
                memory, CursorCurrentTileFlagGatePattern, CursorCurrentTileFlagGateRva,
                "ordinary-movement current-tile cursor gate");
            Resolve(memory, AttackUnitPairGatePattern, AttackUnitPairGateJumpRva - 0x0E,
                "attack-unit cursor tile-pair gate context");
            Resolve(memory, AttackBuildingPairGatePattern, AttackBuildingPairGateJumpRva - 0x11,
                "attack-building cursor tile-pair gate context");
            Resolve(memory, AttackAlternativePairGatePattern, AttackAlternativePairGateJumpRva - 0x0E,
                "alternative attack cursor tile-pair gate context");

            ValidateExactBytes(
                memory,
                CursorCurrentTileFlagGateRva,
                new byte[] { 0xF7, 0x84, 0x97, 0x00, 0x84, 0x89, 0x00, 0x00, 0x01, 0x00, 0x10 },
                "ordinary-movement current-tile cursor gate");
            ValidateExactBytes(
                memory,
                CombatFinishResumeRva,
                new byte[]
                {
                    0x40, 0x53, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x63,
                    0xC2, 0x48, 0x69, 0xD8, 0x90, 0x04, 0x00, 0x00,
                    0x48, 0x03, 0xD9, 0x66, 0x83, 0xBB, 0x96, 0x09,
                    0x00, 0x00, 0x00, 0x75, 0x14
                },
                "combat-finish movement-resume detour entry");
            ValidateCallTarget(
                memory, CombatFinishPostCombatCallRva, PostCombatRepathRva,
                new byte[] { 0xE8, 0xAE, 0x22, 0x01, 0x00 },
                "combat-finish post-combat repath call");
            ValidateExactBytes(
                memory,
                PostCombatRepathRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74,
                    0x24, 0x10, 0x57, 0x48, 0x83, 0xEC, 0x30, 0x48,
                    0x63, 0xFA, 0x48, 0x8B, 0xF1, 0x48, 0x69, 0xDF,
                    0x90, 0x04, 0x00, 0x00, 0x48, 0x03, 0xD9
                },
                "post-combat saved-target repath entry");
            ValidateCallTarget(
                memory, PostCombatMoveHereCallRva, 0x196280,
                new byte[] { 0xE8, 0x50, 0xEB, 0xFF, 0xFF },
                "post-combat saved-target MoveHere call");
            ValidateExactBytes(
                memory,
                CursorCurrentTileFlagGateJumpRva,
                CursorGateJumpOriginal,
                "ordinary-movement current-tile cursor-gate jump");
            ValidateExactBytes(memory, AttackUnitPairGateJumpRva,
                AttackUnitPairGateOriginal, "attack-unit cursor tile-pair gate jump");
            ValidateExactBytes(memory, AttackBuildingPairGateJumpRva,
                AttackBuildingPairGateOriginal, "attack-building cursor tile-pair gate jump");
            ValidateExactBytes(memory, AttackAlternativePairGateJumpRva,
                AttackAlternativePairGateOriginal, "alternative attack cursor tile-pair gate jump");
            ValidateExactBytes(
                memory,
                CursorTilePairReachabilityRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C,
                    0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x57,
                    0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x40
                },
                "cursor tile-pair reachability detour span");
            ValidateExactBytes(
                memory,
                GetRepresentativeSelectedUnitRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x18, 0x55, 0x48, 0x8D,
                    0x6C, 0x24, 0x80, 0x48, 0x81, 0xEC, 0x80, 0x01,
                    0x00, 0x00
                },
                "representative selected-unit helper entry");
            ValidateExactBytes(memory, SelectionCanDigMoatRva,
                new byte[]
                {
                    0x83, 0xB9, 0x80, 0x05, 0x00, 0x00, 0x00, 0x75, 0x54,
                    0x83, 0xB9, 0xB4, 0x05, 0x00, 0x00, 0x00, 0x75, 0x4B,
                    0x83, 0xB9, 0x68, 0x05, 0x00, 0x00, 0x00, 0x75, 0x42,
                    0x83, 0xB9, 0x64, 0x05, 0x00, 0x00, 0x00, 0x75, 0x39,
                    0x83, 0xB9, 0x6C, 0x05, 0x00, 0x00, 0x00, 0x75, 0x30,
                    0x83, 0xB9, 0xE8, 0x05, 0x00, 0x00, 0x00, 0x75, 0x27,
                    0x83, 0xB9, 0xEC, 0x05, 0x00, 0x00, 0x00, 0x75, 0x1E,
                    0x83, 0xB9, 0xE0, 0x05, 0x00, 0x00, 0x00, 0x75, 0x15,
                    0x83, 0xB9, 0xD8, 0x05, 0x00, 0x00, 0x00, 0x75, 0x0C,
                    0x83, 0xB9, 0x74, 0x05, 0x00, 0x00, 0x00, 0x75, 0x03,
                    0x33, 0xC0, 0xC3, 0xB8, 0x01, 0x00, 0x00, 0x00, 0xC3
                }, "Vanilla selection-can-dig-moat helper body");
            ValidateCallTarget(memory, SelectionCanDigMoatCallRva, SelectionCanDigMoatRva,
                new byte[] { 0xE8, 0x2D, 0x48, 0x10, 0x00 },
                "DigMoat cursor selection-helper call");
            ValidateExactBytes(
                memory,
                FirstGroupUnitOnCompletedMoatRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C,
                    0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x48,
                    0x89, 0x7C, 0x24, 0x20, 0x41, 0x56, 0x48, 0x83,
                    0xEC, 0x20
                },
                "first group unit on completed moat detour span");
            ValidateExactBytes(
                memory,
                GetGroupUnitIdRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x63, 0xC2,
                    0x45, 0x33, 0xC9, 0x48, 0x69, 0xD0, 0x88, 0x06,
                    0x00, 0x00
                },
                "group unit iterator entry");
            ValidateCallTarget(
                memory, GroupMoatModeCallRva, FirstGroupUnitOnCompletedMoatRva,
                new byte[] { 0xE8, 0x55, 0xC5, 0xFF, 0xFF },
                "MoveHere group moat-mode helper call");
            ValidateExactBytes(
                memory, GroupMoatModeCallRva - 3,
                new byte[]
                {
                    0x48, 0x8B, 0xCF,
                    0xE8, 0x55, 0xC5, 0xFF, 0xFF,
                    0x44, 0x3B, 0xF8, 0x75, 0x72
                },
                "MoveHere group moat-mode decision sequence");
            ValidateExactBytes(
                memory, MovementTerrainPhaseRva,
                new byte[]
                {
                    0x0F, 0xB6, 0x83, 0xC8, 0x06, 0x00, 0x00, 0x45,
                    0x85, 0xC9, 0x74, 0x42, 0x3C, 0x18, 0x7D, 0x08,
                    0x04, 0x04, 0x88, 0x83, 0xC8, 0x06, 0x00, 0x00,
                    0x0F, 0xB7, 0x8B, 0xA2, 0x09, 0x00, 0x00, 0x3C
                },
                "movement terrain-delay phase contract");
            ValidateExactBytes(
                memory, MovementCadenceRva,
                new byte[]
                {
                    0x41, 0x0F, 0xBF, 0x80, 0x16, 0x09, 0x00, 0x00,
                    0x41, 0x0F, 0xBF, 0x88, 0xA2, 0x09, 0x00, 0x00,
                    0x45, 0x8B, 0x90, 0xA8, 0x09, 0x00, 0x00, 0x03,
                    0xC8, 0x41, 0x0F, 0xBF, 0x80, 0x4C, 0x07, 0x00,
                    0x00, 0x41, 0x2B, 0xD2, 0x03, 0xC1, 0x41, 0x8B,
                    0x88, 0xAC, 0x09, 0x00, 0x00, 0x3B, 0xD0
                },
                "movement cadence runtime-field contract");
            ValidateExactBytes(
                memory, MovementSubstepRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x6C,
                    0x24, 0x18, 0x48, 0x89, 0x74, 0x24, 0x20, 0x57,
                    0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
                    0x48, 0x83, 0xEC, 0x40, 0x48, 0x63, 0xDA, 0x41,
                    0x0F, 0xB6, 0xE9, 0x4C, 0x69, 0xE3, 0x90, 0x04,
                    0x00, 0x00, 0x45, 0x8B, 0xE8
                },
                "movement substep speed-bonus contract");
            ValidateExactBytes(
                memory, MovementAdditionalSubstepsRva,
                new byte[]
                {
                    0x41, 0x0F, 0xBF, 0x84, 0x3C, 0x2A, 0x0A, 0x00,
                    0x00, 0x44, 0x03, 0xE8, 0x0F, 0x88, 0xD3, 0x04,
                    0x00, 0x00
                },
                "movement additional-substeps contract");
            ValidateExactBytes(
                memory, MoatPathConsumptionReadRva,
                new byte[]
                {
                    0x66, 0x45, 0x85, 0xC9, 0x0F, 0x85, 0xC2, 0x04,
                    0x00, 0x00, 0x41, 0x0F, 0xBF, 0x84, 0x3C, 0xC8,
                    0x09, 0x00, 0x00, 0x48, 0x8D, 0x0D, 0x12, 0x7D,
                    0xF2, 0x05, 0x45, 0x0F, 0xBF, 0x8C, 0x3C, 0x1E,
                    0x07, 0x00, 0x00, 0x45, 0x8B, 0x84, 0x3C, 0x2C,
                    0x07, 0x00, 0x00, 0x89, 0x44, 0x24, 0x30, 0x89,
                    0x54, 0x24, 0x28, 0x8B, 0xD3, 0x44, 0x89, 0x74,
                    0x24, 0x20, 0xE8, 0xED, 0x74, 0xF5, 0xFF, 0x85,
                    0xC0
                },
                "movement moat-path consumption contract");
            ValidateExactBytes(
                memory, MoatPathConsumptionPersistRva,
                new byte[]
                {
                    0x41, 0x0F, 0xB7, 0xC0, 0x44, 0x39, 0x05, 0xCD,
                    0x6F, 0xF1, 0x05, 0x66, 0x0F, 0x45, 0xC3, 0x66,
                    0x89, 0x87, 0xC8, 0x09, 0x00, 0x00, 0x8B, 0xC3,
                    0x4C, 0x89, 0x05, 0xB9, 0x6F, 0xF1, 0x05, 0x4C,
                    0x89, 0x87, 0xD0, 0x0A, 0x00, 0x00
                },
                "MoveHere moat-path mode persistence contract");
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_PathPlanRelated1), 0xF0);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_TargetTilePositionX2), 0xE8);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_TargetTilePositionY2), 0xEA);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_AIState), 0x2BC);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_SpeedBonus), 0x2BA);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_CurrentSpeed2), 0x346);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_CurrentSpeed), 0x348);
            ValidateGameUnitFieldOffset(nameof(GameUnit.N000001CE), UnitMoatPathConsumptionModeOffset);
            ValidateGameUnitFieldOffset(
                nameof(GameUnit.UnknownRelevant1), UnitMoatPathConsumptionModeOffset + 1);
            ValidateStructFieldOffset(
                typeof(GameUnitManager), nameof(GameUnitManager.LastOrderedUnit), NativeUnitSlotDataOffset);
            ValidateStructFieldOffset(
                typeof(GameCursorManager), nameof(GameCursorManager.r_HoverOverUnitId), 0x30);
            if (Marshal.SizeOf(typeof(GameUnit)) != NativeUnitStride)
            {
                throw new InvalidOperationException(
                    $"Unexpected GameUnit size 0x{Marshal.SizeOf(typeof(GameUnit)):X}; " +
                    $"expected native stride 0x{NativeUnitStride:X}.");
            }
            ValidateStructFieldOffset(
                typeof(GameUnitManager), nameof(GameUnitManager.GameUnitArray),
                NativeUnitSlotDataOffset + NativeUnitStride);
            if (Marshal.SizeOf(typeof(GameUnit)) <= UnitAdditionalMovementSubstepsOffset + 1)
            {
                throw new InvalidOperationException(
                    "GameUnit is too small for the native additional-substeps field.");
            }

            moatPathMode = (int*)(libraryBase + MoatPathModeRva);
            cursorTargetX = (int*)(libraryBase + CursorTargetXRva);
            cursorTargetY = (int*)(libraryBase + CursorTargetYRva);
            nativeUnitManager = (byte*)(libraryBase + NativeUnitManagerRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);
            movementTargetAvailability = (byte*)(libraryBase + MovementTargetAvailabilityRva);
            nativeMovementMasks = (byte*)(libraryBase + NativeMovementMaskRva);
            nativeRowLookup = (int*)(libraryBase + RowLookupRva);
            nativeBuildingLayer = (ushort*)(libraryBase + NativeBuildingLayerRva);
            nativeHeightLayer = (byte*)(libraryBase + NativeHeightLayerRva);
            nativeDirectionMasks = (byte*)(libraryBase + NativeDirectionMaskRva);
            pathRegionGrid = (short*)(libraryBase + PathRegionGridRva);
            nativePathManager = (IntPtr)(libraryBase + NativePathManagerRva);
            nativeTribeManager = (IntPtr)(libraryBase + NativeTribeManagerRva);
            getMoatIdAtTile = Marshal.GetDelegateForFunctionPointer<GetMoatIdAtTileDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)moatLookupResolution.Rva)));
            getRepresentativeSelectedUnit = Marshal.GetDelegateForFunctionPointer<GetRepresentativeSelectedUnitDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)representativeUnitResolution.Rva)));
            selectionCanDigMoat = Marshal.GetDelegateForFunctionPointer<SelectionCanDigMoatDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)selectionCanDigResolution.Rva)));
            getGroupUnitId = Marshal.GetDelegateForFunctionPointer<GetGroupUnitIdDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)groupUnitResolution.Rva)));
            weightedMoatRoutePlanner = new WeightedMoatRoutePlanner(
                movementTargetAvailability,
                nativeRowLookup,
                tileFlags,
                nativeBuildingLayer,
                nativeHeightLayer,
                nativeMovementMasks,
                nativeDirectionMasks,
                IsFriendlyCompletedMoatForWeightedShadow);
            nativeMovementCadenceResolver = new NativeMovementCadenceResolver(
                memory,
                libraryBase,
                unchecked((ulong)nativeUnitManager),
                log);
            rootedCentralMovementPlan = RunCentralMovementPlanWithContext;
            rootedPathBuilder = BuildPathWithCompletedMoatRouteVariant;
            rootedTribeFloodFillMembership = AllowTribeFloodFillForMoveOrder;
            rootedFirstGroupUnitOnCompletedMoat = NormalizeMixedGroupMoatMode;
            rootedUnitStandingOnCompletedMoat = EnableCompletedMoatModeForScopedMovement;
            rootedRegionReachability = AllowBuilderAfterFailedRegionSearch;
            rootedCursorReachability = AllowCursorReachabilityThroughCompletedMoat;
            rootedCursorTilePairFallbackSelection = ObserveCursorTilePairFallbackSelection;
            rootedCursorTilePairReachability = AllowAttackCursorTilePairThroughCompletedMoat;
            rootedCursorRegionPrecheck = AllowCursorRegionThroughCompletedMoat;
            rootedCombatFinishResume = ResumeMovementAfterCombatWithMoatContext;

            NativeDetour pendingPlanDetour = null;
            NativeDetour pendingCombatFinishResume = null;
            NativeDetour pendingBuilder = null;
            NativeDetour pendingFlood = null;
            NativeDetour pendingGroupMoat = null;
            NativeDetour pendingMode = null;
            NativeDetour pendingRegion = null;
            NativeDetour pendingCursor = null;
            NativeDetour pendingCursorMode = null;
            NativeDetour pendingCursorTilePair = null;
            NativeDetour pendingCursorRegion = null;
            bool planApplied = false;
            bool combatFinishResumeApplied = false;
            bool builderApplied = false;
            bool floodApplied = false;
            bool groupMoatApplied = false;
            bool modeApplied = false;
            bool regionApplied = false;
            bool cursorApplied = false;
            bool cursorModeApplied = false;
            bool cursorTilePairApplied = false;
            bool cursorRegionApplied = false;
            try
            {
                pendingPlanDetour = CreateDetour(
                    libraryBase + unchecked((ulong)planResolution.Rva),
                    rootedCentralMovementPlan);
                originalCentralMovementPlan =
                    pendingPlanDetour.GenerateTrampoline<CentralMovementPlanDelegate>();
                pendingCombatFinishResume = CreateDetour(
                    libraryBase + unchecked((ulong)combatFinishResumeResolution.Rva),
                    rootedCombatFinishResume);
                originalCombatFinishResume =
                    pendingCombatFinishResume.GenerateTrampoline<CombatFinishResumeDelegate>();
                pendingBuilder = CreateDetour(
                    libraryBase + unchecked((ulong)builderResolution.Rva),
                    rootedPathBuilder);
                originalPathBuilder = pendingBuilder.GenerateTrampoline<PathBuilderDelegate>();
                pendingFlood = CreateDetour(libraryBase + unchecked((ulong)floodResolution.Rva), rootedTribeFloodFillMembership);
                originalTribeFloodFillMembership = pendingFlood.GenerateTrampoline<TribeFloodFillMembershipDelegate>();
                pendingGroupMoat = CreateDetour(
                    libraryBase + unchecked((ulong)groupMoatResolution.Rva),
                    rootedFirstGroupUnitOnCompletedMoat);
                originalFirstGroupUnitOnCompletedMoat =
                    pendingGroupMoat.GenerateTrampoline<FirstGroupUnitOnCompletedMoatDelegate>();
                pendingMode = CreateDetour(libraryBase + unchecked((ulong)modeResolution.Rva), rootedUnitStandingOnCompletedMoat);
                originalUnitStandingOnCompletedMoat = pendingMode.GenerateTrampoline<UnitStandingOnCompletedMoatDelegate>();
                pendingRegion = CreateDetour(libraryBase + unchecked((ulong)regionResolution.Rva), rootedRegionReachability);
                originalRegionReachability = pendingRegion.GenerateTrampoline<RegionReachabilityDelegate>();
                pendingCursor = CreateDetour(libraryBase + unchecked((ulong)cursorResolution.Rva), rootedCursorReachability);
                originalCursorReachability = pendingCursor.GenerateTrampoline<CursorReachabilityDelegate>();
                pendingCursorMode = CreateDetour(libraryBase + unchecked((ulong)cursorModeResolution.Rva), rootedCursorTilePairFallbackSelection);
                originalCursorTilePairFallbackSelection = pendingCursorMode.GenerateTrampoline<CursorTilePairFallbackSelectionDelegate>();
                pendingCursorTilePair = CreateDetour(
                    libraryBase + unchecked((ulong)cursorTilePairResolution.Rva),
                    rootedCursorTilePairReachability);
                originalCursorTilePairReachability =
                    pendingCursorTilePair.GenerateTrampoline<CursorTilePairReachabilityDelegate>();
                pendingCursorRegion = CreateDetour(libraryBase + unchecked((ulong)cursorRegionResolution.Rva), rootedCursorRegionPrecheck);
                originalCursorRegionPrecheck = pendingCursorRegion.GenerateTrampoline<CursorRegionPrecheckDelegate>();

                pendingPlanDetour.Apply();
                planApplied = true;
                pendingCombatFinishResume.Apply();
                combatFinishResumeApplied = true;
                pendingBuilder.Apply();
                builderApplied = true;
                pendingFlood.Apply();
                floodApplied = true;
                pendingGroupMoat.Apply();
                groupMoatApplied = true;
                pendingMode.Apply();
                modeApplied = true;
                pendingRegion.Apply();
                regionApplied = true;
                pendingCursor.Apply();
                cursorApplied = true;
                pendingCursorMode.Apply();
                cursorModeApplied = true;
                pendingCursorTilePair.Apply();
                cursorTilePairApplied = true;
                pendingCursorRegion.Apply();
                cursorRegionApplied = true;

                centralMovementPlanDetour = pendingPlanDetour;
                combatFinishResumeDetour = pendingCombatFinishResume;
                pathBuilderDetour = pendingBuilder;
                tribeFloodFillMembershipDetour = pendingFlood;
                firstGroupUnitOnCompletedMoatDetour = pendingGroupMoat;
                unitStandingOnCompletedMoatDetour = pendingMode;
                regionReachabilityDetour = pendingRegion;
                cursorReachabilityDetour = pendingCursor;
                cursorTilePairFallbackSelectionDetour = pendingCursorMode;
                cursorTilePairReachabilityDetour = pendingCursorTilePair;
                cursorRegionPrecheckDetour = pendingCursorRegion;

                tribeMoveSubscription = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable.Subscribe(ObserveTribeMoveOrder);
                tribeTargetSubscription = TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable.Subscribe(ObserveTribeTargetOrder);
                mapLoadSubscription = MapLoaderR3EventHooks.OnLoadMap.Observable.Subscribe(_ => ResetMapState());
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(_ => ResetMapState());
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(_ => ResetMapState());
                GameTimeManagerAPI.Instance.OnTick += ObserveTrackedAttackStates;
                attackTickSubscribed = true;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Move Moat Test final candidate installed: " +
                    $"cursorGate=0x{cursorGateResolution.Rva:X}/jump=0x{CursorCurrentTileFlagGateJumpRva:X}(vanilla), " +
                    $"cursorRegion=0x{cursorRegionResolution.Rva:X}, cursorDirect=0x{cursorResolution.Rva:X}, " +
                    $"cursorPair=0x{cursorTilePairResolution.Rva:X}, representativeUnit=0x{representativeUnitResolution.Rva:X}, " +
                    $"attackPairGates=0x{AttackUnitPairGateJumpRva:X}/0x{AttackBuildingPairGateJumpRva:X}/" +
                    $"0x{AttackAlternativePairGateJumpRva:X}(all-vanilla), semanticSelectionGate=true, " +
                    $"plan=0x{planResolution.Rva:X}, mode=0x{modeResolution.Rva:X}, " +
                    $"region=0x{regionResolution.Rva:X}, builder=0x{builderResolution.Rva:X}, " +
                    $"postCombatResume=0x{combatFinishResumeResolution.Rva:X}->" +
                    $"0x{postCombatRepathResolution.Rva:X}->0x196280, " +
                    $"selectionCanDigMoat=0x{selectionCanDigResolution.Rva:X}/call=0x{SelectionCanDigMoatCallRva:X}, " +
                    $"tribeFloodFill=0x{floodResolution.Rva:X}, moatLookup=0x{moatLookupResolution.Rva:X}; " +
                    $"groupMoatMode=0x{groupMoatResolution.Rva:X}/iterator=0x{groupUnitResolution.Rva:X}/" +
                    $"call=0x{GroupMoatModeCallRva:X}; " +
                    "friendlyAndAlliedCompletedMoats=true, enemyMoats=fail-closed-experimental, " +
                    $"weightedMoatRouting=functional/speedContracts=0x{MovementTerrainPhaseRva:X}/" +
                    $"0x{MovementCadenceRva:X}/0x{MovementSubstepRva:X}/" +
                    $"0x{MovementAdditionalSubstepsRva:X}/consumerContracts=" +
                    $"0x{MoatPathConsumptionPersistRva:X}/0x{MoatPathConsumptionReadRva:X}.");

                // Optional cursor/diagnostic groups fail closed without rolling back the proven
                // ordinary movement hooks above.
                TryInstallBuildingCursorReachability(memory, libraryBase);
                TryInstallAttackApproachDiagnostics(memory, libraryBase);
            }
            catch
            {
                tribeMoveSubscription?.Dispose();
                tribeTargetSubscription?.Dispose();
                mapLoadSubscription?.Dispose();
                mapStartSubscription?.Dispose();
                mapUnloadSubscription?.Dispose();
                if (attackTickSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick -= ObserveTrackedAttackStates;
                    attackTickSubscribed = false;
                }
                UndoAndDispose(pendingCursorTilePair, cursorTilePairApplied);
                UndoAndDispose(pendingCursorRegion, cursorRegionApplied);
                UndoAndDispose(pendingCursorMode, cursorModeApplied);
                UndoAndDispose(pendingCursor, cursorApplied);
                UndoAndDispose(pendingRegion, regionApplied);
                UndoAndDispose(pendingMode, modeApplied);
                UndoAndDispose(pendingGroupMoat, groupMoatApplied);
                UndoAndDispose(pendingFlood, floodApplied);
                UndoAndDispose(pendingBuilder, builderApplied);
                UndoAndDispose(pendingCombatFinishResume, combatFinishResumeApplied);
                UndoAndDispose(pendingPlanDetour, planApplied);
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            tribeMoveSubscription?.Dispose();
            tribeTargetSubscription?.Dispose();
            mapLoadSubscription?.Dispose();
            mapStartSubscription?.Dispose();
            mapUnloadSubscription?.Dispose();
            if (attackTickSubscribed)
            {
                GameTimeManagerAPI.Instance.OnTick -= ObserveTrackedAttackStates;
                attackTickSubscribed = false;
            }
            buildingCandidateConsumerDetour?.Dispose();
            buildingApproachBuilderDetour?.Dispose();
            attackApproachFloodBuilderDetour?.Dispose();
            regionPairReachabilityDetour?.Dispose();
            buildingCursorReachabilityDetour?.Dispose();
            cursorTilePairReachabilityDetour?.Dispose();
            cursorRegionPrecheckDetour?.Dispose();
            cursorTilePairFallbackSelectionDetour?.Dispose();
            cursorReachabilityDetour?.Dispose();
            regionReachabilityDetour?.Dispose();
            unitStandingOnCompletedMoatDetour?.Dispose();
            firstGroupUnitOnCompletedMoatDetour?.Dispose();
            tribeFloodFillMembershipDetour?.Dispose();
            pathBuilderDetour?.Dispose();
            combatFinishResumeDetour?.Dispose();
            centralMovementPlanDetour?.Dispose();
            activeMoveCommand = null;
            activePlan = null;
            pendingPlan = null;
            pendingAttackCursorPair = null;
            pendingCursorSelectionDiagnostic = null;
            activeAttackCommand = null;
            activeAttackApproachDiagnostic = null;
            activeBuildingApproachPerformance = null;
            activeBuildingConsumerPerformance = null;
            trackedAttackUnits.Clear();
            trackedMoatMoves.Clear();
            lastAttackCommandCandidates.Clear();
            loggedBuildingCursorReachabilityDecisions.Clear();
        }

        private void TryInstallBuildingCursorReachability(
            ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            NativeDetour pendingBuildingCursor = null;
            bool buildingCursorApplied = false;
            try
            {
                Shared.NativeResolution buildingCursorResolution = Resolve(
                    memory, BuildingCursorReachabilityPattern, BuildingCursorReachabilityRva,
                    "building cursor approach reachability helper");
                ValidateExactBytes(
                    memory, BuildingCursorReachabilityRva,
                    new byte[]
                    {
                        0x48, 0x89, 0x5C, 0x24, 0x08, 0x55, 0x56, 0x57,
                        0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
                        0x48, 0x83, 0xEC, 0x40, 0x4C, 0x8B, 0xE1, 0x85,
                        0xD2
                    },
                    "building cursor approach reachability detour span");
                ValidateCallTarget(
                    memory, BuildingCursorReachabilityCallRva, BuildingCursorReachabilityRva,
                    new byte[] { 0xE8, 0xC5, 0x90, 0x02, 0x00 },
                    "building cursor approach reachability call");

                rootedBuildingCursorReachability = AllowBuildingCursorThroughCompletedMoat;
                pendingBuildingCursor = CreateDetour(
                    libraryBase + unchecked((ulong)buildingCursorResolution.Rva),
                    rootedBuildingCursorReachability);
                originalBuildingCursorReachability =
                    pendingBuildingCursor.GenerateTrampoline<BuildingCursorReachabilityDelegate>();
                pendingBuildingCursor.Apply();
                buildingCursorApplied = true;
                buildingCursorReachabilityDetour = pendingBuildingCursor;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "MoveMoat building cursor reachability installed: " +
                    $"helper=0x{buildingCursorResolution.Rva:X}, " +
                    $"call=0x{BuildingCursorReachabilityCallRva:X}.");
            }
            catch (Exception ex)
            {
                UndoAndDispose(pendingBuildingCursor, buildingCursorApplied);
                rootedBuildingCursorReachability = null;
                originalBuildingCursorReachability = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "MoveMoat building cursor reachability was not installed; " +
                    $"building cursors remain Vanilla and the movement feature remains active: {ex}");
            }
        }

        private void TryInstallAttackApproachDiagnostics(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            NativeDetour pendingUnitFlood = null;
            NativeDetour pendingBuildingApproach = null;
            NativeDetour pendingBuildingConsumer = null;
            NativeDetour pendingRegionPair = null;
            bool unitFloodApplied = false;
            bool buildingApproachApplied = false;
            bool buildingConsumerApplied = false;
            bool regionPairApplied = false;
            try
            {
                Shared.NativeResolution unitFloodResolution = Resolve(
                    memory, AttackApproachFloodBuilderPattern, AttackApproachFloodBuilderRva,
                    "unit attack-approach flood builder");
                Shared.NativeResolution buildingApproachResolution = Resolve(
                    memory, BuildingApproachBuilderPattern, BuildingApproachBuilderRva,
                    "building attack-approach builder");
                Shared.NativeResolution buildingConsumerResolution = Resolve(
                    memory, BuildingCandidateConsumerPattern, BuildingCandidateConsumerRva,
                    "building attack candidate consumer");
                Shared.NativeResolution assassinSelectionResolution = Resolve(
                    memory, AllSelectedUnitsAssassinsPattern, AllSelectedUnitsAssassinsRva,
                    "all-selected-units-are-assassins helper");
                Shared.NativeResolution regionPairResolution = Resolve(
                    memory, RegionPairReachabilityPattern, RegionPairReachabilityRva,
                    "attack-approach region-pair reachability helper");

                ValidateAttackApproachEntries(memory);
                ValidateAttackApproachCalls(memory);

                allSelectedUnitsAssassins =
                    Marshal.GetDelegateForFunctionPointer<AllSelectedUnitsAssassinsDelegate>(
                        (IntPtr)(libraryBase + unchecked((ulong)assassinSelectionResolution.Rva)));
                rootedAttackApproachFloodBuilder = ObserveAttackApproachFloodBuilder;
                rootedBuildingApproachBuilder = ObserveBuildingApproachBuilder;
                rootedBuildingCandidateConsumer = ObserveBuildingCandidateConsumer;
                rootedRegionPairReachability = ObserveAttackApproachRegionPair;

                pendingUnitFlood = CreateDetour(
                    libraryBase + unchecked((ulong)unitFloodResolution.Rva),
                    rootedAttackApproachFloodBuilder);
                originalAttackApproachFloodBuilder =
                    pendingUnitFlood.GenerateTrampoline<AttackApproachFloodBuilderDelegate>();
                pendingBuildingApproach = CreateDetour(
                    libraryBase + unchecked((ulong)buildingApproachResolution.Rva),
                    rootedBuildingApproachBuilder);
                originalBuildingApproachBuilder =
                    pendingBuildingApproach.GenerateTrampoline<BuildingApproachBuilderDelegate>();
                pendingBuildingConsumer = CreateDetour(
                    libraryBase + unchecked((ulong)buildingConsumerResolution.Rva),
                    rootedBuildingCandidateConsumer);
                originalBuildingCandidateConsumer =
                    pendingBuildingConsumer.GenerateTrampoline<BuildingCandidateConsumerDelegate>();
                pendingRegionPair = CreateDetour(
                    libraryBase + unchecked((ulong)regionPairResolution.Rva),
                    rootedRegionPairReachability);
                originalRegionPairReachability =
                    pendingRegionPair.GenerateTrampoline<RegionPairReachabilityDelegate>();

                pendingUnitFlood.Apply();
                unitFloodApplied = true;
                pendingBuildingApproach.Apply();
                buildingApproachApplied = true;
                pendingBuildingConsumer.Apply();
                buildingConsumerApplied = true;
                pendingRegionPair.Apply();
                regionPairApplied = true;

                attackApproachFloodBuilderDetour = pendingUnitFlood;
                buildingApproachBuilderDetour = pendingBuildingApproach;
                buildingCandidateConsumerDetour = pendingBuildingConsumer;
                regionPairReachabilityDetour = pendingRegionPair;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "MoveMoat attack-approach hooks installed: " +
                    $"unitFlood=0x{unitFloodResolution.Rva:X}, " +
                    $"buildingApproach=0x{buildingApproachResolution.Rva:X}, " +
                    $"buildingConsumer=0x{buildingConsumerResolution.Rva:X}, " +
                    $"assassinSelection=0x{assassinSelectionResolution.Rva:X}, " +
                    $"regionPair=0x{regionPairResolution.Rva:X}.");
            }
            catch (Exception ex)
            {
                UndoAndDispose(pendingRegionPair, regionPairApplied);
                UndoAndDispose(pendingBuildingConsumer, buildingConsumerApplied);
                UndoAndDispose(pendingBuildingApproach, buildingApproachApplied);
                UndoAndDispose(pendingUnitFlood, unitFloodApplied);
                rootedAttackApproachFloodBuilder = null;
                rootedBuildingApproachBuilder = null;
                rootedBuildingCandidateConsumer = null;
                rootedRegionPairReachability = null;
                originalAttackApproachFloodBuilder = null;
                originalBuildingApproachBuilder = null;
                originalBuildingCandidateConsumer = null;
                originalRegionPairReachability = null;
                allSelectedUnitsAssassins = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "MoveMoat attack-approach hooks were not installed; building commands remain " +
                    $"Vanilla while the existing movement feature remains active: {ex}");
            }
        }

        private static void ValidateAttackApproachEntries(ReadOnlySpan<byte> memory)
        {
            ValidateExactBytes(
                memory, AttackApproachFloodBuilderRva,
                new byte[]
                {
                    0x44, 0x89, 0x4C, 0x24, 0x20, 0x53, 0x56, 0x41,
                    0x54, 0x41, 0x55, 0x41, 0x56, 0x48, 0x83, 0xEC,
                    0x60, 0x48, 0x8B, 0xD9, 0x4D, 0x63, 0xE9, 0x45,
                    0x33, 0xF6, 0x48, 0x8D, 0x0D, 0x9F, 0xAA, 0xBE,
                    0x07, 0x45, 0x8B, 0xE6, 0x44, 0x89, 0x74, 0x24,
                    0x3C, 0xE8, 0x92, 0xBB, 0x03, 0x00, 0x48, 0x63,
                    0xF0, 0x41, 0x81, 0xFD, 0x1F, 0x03, 0x00, 0x00
                },
                "unit attack-approach flood builder entry");
            ValidateExactBytes(
                memory, BuildingApproachBuilderRva,
                new byte[]
                {
                    0x48, 0x89, 0x4C, 0x24, 0x08, 0x53, 0x55, 0x56,
                    0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41,
                    0x57, 0x48, 0x83, 0xEC, 0x78, 0x48, 0x8D, 0x0D,
                    0x74, 0x2B, 0x3F, 0x06, 0x4C, 0x63, 0xD2, 0x49,
                    0x69, 0xD2, 0x88, 0x06, 0x00, 0x00, 0x4D, 0x63,
                    0xF0, 0x4C, 0x8D, 0x25, 0xB0, 0x5F, 0xF2, 0xFF,
                    0x4D, 0x69, 0xFE, 0x2C, 0x03, 0x00, 0x00, 0x33,
                    0xED, 0x41, 0x8B, 0xD9, 0x44, 0x8B, 0xED, 0x4C
                },
                "building attack-approach builder entry");
            ValidateExactBytes(
                memory, BuildingCandidateConsumerRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x74,
                    0x24, 0x10, 0x57, 0x48, 0x83, 0xEC, 0x40, 0x48,
                    0x63, 0xDA, 0x41, 0x8B, 0xF0, 0x8B, 0xD3, 0x48,
                    0x8B, 0xF9, 0xE8, 0x71, 0x47, 0xFF, 0xFF, 0x4C,
                    0x69, 0xCB, 0x88, 0x06, 0x00, 0x00, 0x48, 0x8D,
                    0x1D, 0xA3, 0xA5, 0xF8, 0x05, 0x49, 0x0F, 0xBF,
                    0x4C, 0x39, 0x5A, 0x48, 0x8D, 0x3D, 0x36, 0xCF,
                    0xED, 0xFF, 0x48, 0x69, 0xD1, 0x90, 0x04, 0x00
                },
                "building attack candidate consumer entry");
            ValidateExactBytes(
                memory, AllSelectedUnitsAssassinsRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C,
                    0x24, 0x10, 0x48, 0x89, 0x74, 0x24, 0x18, 0x48,
                    0x89, 0x7C, 0x24, 0x20, 0x41, 0x56, 0x48, 0x83,
                    0xEC, 0x20, 0x48, 0x63, 0xF2, 0x33, 0xDB, 0x4C,
                    0x69, 0xC6, 0x88, 0x06, 0x00, 0x00, 0x48, 0x8B,
                    0xE9, 0x41, 0x0F, 0xBF, 0x7C, 0x08, 0x5C, 0x85,
                    0xFF, 0x7E, 0x4D, 0x4C, 0x8D, 0x35, 0xA6, 0x0B,
                    0x6D, 0x06, 0x66, 0x0F, 0x1F, 0x44, 0x00, 0x00,
                    0x44, 0x8B, 0xC3, 0x8B, 0xD6, 0x48, 0x8B, 0xCD,
                    0xE8, 0x23, 0x27, 0x00, 0x00, 0x48, 0x98, 0xFF,
                    0xC3, 0x48, 0x69, 0xC8, 0x90, 0x04, 0x00, 0x00,
                    0x66, 0x42, 0x83, 0xBC, 0x31, 0xE4, 0x06, 0x00,
                    0x00, 0x02, 0x75, 0x18, 0x66, 0x42, 0x83, 0xBC,
                    0x31, 0xF8, 0x08, 0x00, 0x00, 0x00, 0x75, 0x0C,
                    0x66, 0x42, 0x83, 0xBC, 0x31, 0xE6, 0x06, 0x00,
                    0x00, 0x49
                },
                "all-selected-units-are-assassins helper entry");
            ValidateExactBytes(
                memory, RegionPairReachabilityRva,
                new byte[]
                {
                    0x40, 0x55, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56,
                    0x48, 0x8D, 0xAC, 0x24, 0x78, 0xF7, 0xFF, 0xFF,
                    0x48, 0x81, 0xEC, 0x88, 0x09, 0x00, 0x00, 0x48,
                    0x8B, 0x05, 0x7A, 0x5D, 0x25, 0x00, 0x48, 0x33,
                    0xC4, 0x48, 0x89, 0x85, 0x70, 0x08, 0x00, 0x00
                },
                "attack-approach region-pair helper entry");
        }

        private static void ValidateAttackApproachCalls(ReadOnlySpan<byte> memory)
        {
            int tribeManagerTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, AttackApproachFloodBuilderRva + 0x1D, AttackApproachFloodBuilderRva + 0x21);
            if (tribeManagerTarget != NativeTribeManagerRva)
            {
                throw new InvalidOperationException(
                    $"The unit attack-approach builder references tribe manager 0x{tribeManagerTarget:X} " +
                    $"instead of 0x{NativeTribeManagerRva:X}.");
            }

            ValidateCallTarget(memory, AttackApproachFloodCallRva, AttackApproachFloodBuilderRva,
                new byte[] { 0xE8, 0x14, 0xCE, 0xFB, 0xFF }, "primary unit attack-approach call");
            ValidateCallTarget(memory, AttackApproachFloodAlternativeCallRva, AttackApproachFloodBuilderRva,
                new byte[] { 0xE8, 0xF0, 0xC7, 0xFB, 0xFF }, "alternative unit attack-approach call");
            ValidateCallTarget(memory, BuildingApproachCallRva, BuildingApproachBuilderRva,
                new byte[] { 0xE8, 0x81, 0xA0, 0xFB, 0xFF }, "building attack-approach call");
            ValidateCallTarget(memory, BuildingCandidateConsumerCallRva, BuildingCandidateConsumerRva,
                new byte[] { 0xE8, 0xE4, 0x30, 0x00, 0x00 }, "building candidate consumer call");
            ValidateCallTarget(memory, BuildingCandidateConsumerAlternativeCallRva, BuildingCandidateConsumerRva,
                new byte[] { 0xE8, 0xAC, 0x29, 0x00, 0x00 }, "alternative building candidate consumer call");
            ValidateCallTarget(memory, BuildingCandidateConsumerForceCallRva, BuildingCandidateConsumerRva,
                new byte[] { 0xE8, 0xBE, 0x23, 0x00, 0x00 }, "force-building candidate consumer call");

            ValidateCallTarget(memory, AttackFloodAssassinSelectionCallRva, AllSelectedUnitsAssassinsRva,
                new byte[] { 0xE8, 0x92, 0xBB, 0x03, 0x00 }, "unit flood Assassin-selection call");
            ValidateCallTarget(memory, AttackFloodRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0xFE, 0x66, 0x00, 0x00 }, "unit flood region-pair call");
            ValidateCallTarget(memory, AttackFloodTilePairCallRva, CursorTilePairReachabilityRva,
                new byte[] { 0xE8, 0x68, 0x6D, 0x00, 0x00 }, "unit flood tile-pair call");

            ValidateCallTarget(memory, BuildingApproachAssassinSelectionCallRva, AllSelectedUnitsAssassinsRva,
                new byte[] { 0xE8, 0x63, 0xD7, 0x03, 0x00 }, "building approach Assassin-selection call");
            ValidateCallTarget(memory, BuildingApproachRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0x12, 0x84, 0x00, 0x00 }, "building approach region-pair call");
            ValidateCallTarget(memory, BuildingApproachTilePairCallRva, CursorTilePairReachabilityRva,
                new byte[] { 0xE8, 0x69, 0x8A, 0x00, 0x00 }, "building approach tile-pair call");
            ValidateCallTarget(memory, BuildingApproachAlternativeRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0x8F, 0x81, 0x00, 0x00 }, "alternative building region-pair call");
            ValidateCallTarget(memory, BuildingApproachAlternativeTilePairCallRva, CursorTilePairReachabilityRva,
                new byte[] { 0xE8, 0xEA, 0x87, 0x00, 0x00 }, "alternative building tile-pair call");

            ValidateCallTarget(memory, BuildingConsumerAssassinSelectionCallRva, AllSelectedUnitsAssassinsRva,
                new byte[] { 0xE8, 0x71, 0x47, 0xFF, 0xFF }, "building consumer Assassin-selection call");
            ValidateCallTarget(memory, BuildingConsumerFallbackBuilderCallRva, AlternativePathBuilderRva,
                new byte[] { 0xE8, 0x49, 0x85, 0xFB, 0xFF }, "building consumer fallback-builder call");
            ValidateCallTarget(memory, BuildingConsumerAssassinBuilderCallRva, AssassinPathBuilderRva,
                new byte[] { 0xE8, 0x16, 0x6B, 0xFB, 0xFF }, "building consumer Assassin-builder call");
            ValidateCallTarget(memory, BuildingConsumerGroundBuilderCallRva, GroundPathBuilderRva,
                new byte[] { 0xE8, 0x5F, 0x74, 0xFB, 0xFF }, "building consumer ground-builder call");
        }

        private void ObserveTribeMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (disposed)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                RemoveTrackedAttacksForTribe(args.TribeId, "move-command");
                RemoveTrackedMoatMovesForTribe(args.TribeId, "new-move-command");
                activeMoveCommand = new MoveCommandScope(
                    ++moveCommandSequence,
                    args.TribeId,
                    args.TileX,
                    args.TileY,
                    args.IsPatrolPath != 0,
                    args.IsNewOrder,
                    args.MoveType,
                    activeAttackCommand?.Sequence ?? 0,
                    activeAttackCommand?.Command ?? TribeAICommand.Unknown0);
                CaptureMoveCommandGroupSummary(activeMoveCommand);
                try
                {
                    LogCommandDiagnostic(
                        $"stage=move-command commandSeq={activeMoveCommand.Sequence} " +
                        $"tribe={args.TribeId} target=({args.TileX},{args.TileY}) " +
                        $"phase=pre patrol={args.IsPatrolPath} newOrder={args.IsNewOrder} " +
                        $"moveType={args.MoveType} activeUnits={activeMoveCommand.ActiveUnitsAtDispatch} " +
                        $"diggers={activeMoveCommand.DiggersAtDispatch} " +
                        $"onMoat={activeMoveCommand.UnitsOnMoatAtDispatch} " +
                        $"playerMask=0x{activeMoveCommand.PlayerMaskAtDispatch:X} " +
                        $"parentAttack={activeMoveCommand.ParentAttackCommandSequence}/" +
                        $"{activeMoveCommand.ParentAttackCommand}");
                }
                catch
                {
                    // Diagnostics must not escape into the synchronous command event.
                }
            }
            else if (args.Phase == EventHookPhase.Post)
            {
                MoveCommandScope command = activeMoveCommand;
                try
                {
                    QualifyPendingCommandDiagnostics(command);
                    string lastBuilderResult = command != null && command.BuilderCalls > 0
                        ? command.LastBuilderResult.ToString()
                        : "none";
                    string lastVanillaBuilderResult = command != null &&
                        command.VanillaBuilderCalls > 0
                            ? command.LastVanillaBuilderResult.ToString()
                            : "none";
                    LogCommandDiagnostic(
                        $"stage=move-command-result commandSeq={command?.Sequence ?? 0} " +
                        $"tribe={args.TribeId} " +
                        $"target=({args.TileX},{args.TileY}) patrol={args.IsPatrolPath} " +
                        $"newOrder={args.IsNewOrder} moveType={args.MoveType} return={args.ReturnValue} " +
                        $"activeUnits={command?.ActiveUnitsAtDispatch ?? 0} " +
                        $"diggers={command?.DiggersAtDispatch ?? 0} " +
                        $"onMoat={command?.UnitsOnMoatAtDispatch ?? 0} " +
                        $"playerMask=0x{(command?.PlayerMaskAtDispatch ?? 0):X} " +
                        $"parentAttack={command?.ParentAttackCommandSequence ?? 0}/" +
                        $"{command?.ParentAttackCommand ?? TribeAICommand.Unknown0} " +
                        $"plannerCalls={command?.CentralPlannerCalls ?? 0} " +
                        $"floodCalls={command?.FloodCalls ?? 0} " +
                        $"floodVanillaPositive={command?.FloodVanillaPositive ?? 0} " +
                        $"floodBypasses={command?.FloodFillBypasses ?? 0} " +
                        $"modeCalls={command?.ModeCalls ?? 0} regionCalls={command?.RegionCalls ?? 0} " +
                        $"builderCalls={command?.BuilderCalls ?? 0} " +
                        $"vanillaBuilderCalls={command?.VanillaBuilderCalls ?? 0} " +
                        $"fallbackBuilderCalls={command?.FallbackBuilderCalls ?? 0} " +
                        $"positiveBuilders={command?.PositiveBuilderCalls ?? 0} " +
                        $"weightedUnits={command?.WeightedUnitIds.Count ?? 0} " +
                        $"weightedDecisions={command?.WeightedDecisions ?? 0} " +
                        $"weightedPublished={command?.WeightedPublished ?? 0} " +
                        $"weightedSearchMs={(command?.WeightedSearchMilliseconds ?? 0):F3} " +
                        $"weightedMaxSearchMs={(command?.WeightedMaximumSearchMilliseconds ?? 0):F3} " +
                        $"lastVanillaBuilderResult={lastVanillaBuilderResult} " +
                        $"lastBuilderResult={lastBuilderResult}");
                    FlushCommandDiagnostics(command);
                }
                catch
                {
                    // Diagnostics must not escape into the synchronous command event.
                }
                activeMoveCommand = null;
                activePlan = null;
                pendingPlan = null;
            }
        }

        private void ObserveTribeTargetOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (disposed)
                return;

            try
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    RemoveTrackedAttacksForTribe(args.TribeId, "new-target-command");
                    RemoveTrackedMoatMovesForTribe(args.TribeId, "new-target-command");
                    if (IsAttackCommand(args.AICommand))
                    {
                        activeAttackCommand = null;
                        activeAttackCommand = new AttackCommandScope(
                            null,
                            ++attackCommandSequence,
                            mapEpoch,
                            args.TribeId,
                            args.AICommand,
                            args.TargetValue1,
                            args.TargetValue2);
                        CaptureAttackCommandCandidates(activeAttackCommand);
                        LogAttackCommandCandidates(activeAttackCommand, "pre");
                    }
                }

                LogCommandDiagnostic(
                    $"stage=target-command phase={args.Phase.ToString().ToLowerInvariant()} " +
                    $"tribe={args.TribeId} aiCommand={args.AICommand} " +
                    $"target1={args.TargetValue1} target2={args.TargetValue2} a6={args.a6} " +
                    $"return={args.ReturnValue}");

                if (args.Phase == EventHookPhase.Post && IsAttackCommand(args.AICommand))
                {
                    AttackCommandScope scope = activeAttackCommand;
                    if (scope != null && scope.Matches(args, mapEpoch))
                    {
                        LogAttackCommandCandidates(scope, "post");
                        if (args.ReturnValue > 0)
                        {
                            TrackUnitsUpdatedByAttackCommand(args, scope);
                            RemoveSynchronousAttackTrackers(scope, "command-dispatched");
                        }
                        else
                            RemoveSynchronousAttackTrackers(scope, "command-rejected");
                        LogCommandDiagnostic(
                            $"stage=attack-command-summary commandSeq={scope.Sequence} " +
                            $"tribe={scope.TribeId} command={scope.Command} " +
                            $"target={scope.TargetValue1}/{scope.TargetValue2} " +
                            $"return={args.ReturnValue} weightedUnits={scope.WeightedUnitIds.Count} " +
                            $"weightedDecisions={scope.WeightedDecisions} " +
                            $"weightedPublished={scope.WeightedPublished} " +
                            $"weightedSearchMs={scope.WeightedSearchMilliseconds:F3} " +
                            $"weightedMaxSearchMs={scope.WeightedMaximumSearchMilliseconds:F3}");
                    }
                }
            }
            catch
            {
                // Target-order observation must never affect attack command dispatch.
            }
            finally
            {
                if (args.Phase == EventHookPhase.Post && IsAttackCommand(args.AICommand))
                {
                    AttackCommandScope scope = activeAttackCommand;
                    if (scope != null)
                        activeAttackCommand = scope.Previous;
                    if (pendingPlan != null && pendingPlan.AttackMovementQualified)
                        pendingPlan = null;
                }
            }
        }

        private void TrackUnitsUpdatedByAttackCommand(
            TribeIssueOrderWithTargetEventArgs args,
            AttackCommandScope scope)
        {
            int trackedCount = 0;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int unitId = 1; unitId <= units.Length; unitId++)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != args.TribeId ||
                    !MatchesCompletedAttackTargetContext(unit, scope))
                {
                    continue;
                }

                GetOrCreateAttackTracker(scope, unitId);
                trackedCount++;
            }

            LogCommandDiagnostic(
                $"stage=attack-track-start tribe={args.TribeId} command={args.AICommand} " +
                $"target1={args.TargetValue1} target2={args.TargetValue2} units={trackedCount}");
        }

        private void CaptureAttackCommandCandidates(AttackCommandScope scope)
        {
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int unitId = 1; unitId <= units.Length; unitId++)
            {
                if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) &&
                    unit != null && unit->r_AliveState == AliveState.IsAlive &&
                    unit->r_TribeId == scope.TribeId)
                {
                    scope.CandidateUnitIds.Add(unitId);
                    scope.PreCandidateSignatures[unitId] = GetAttackCandidateSignature(unit);
                }
            }
        }

        private void LogAttackCommandCandidates(AttackCommandScope scope, string phase)
        {
            if (phase == "pre")
            {
                LogCommandDiagnostic(
                    $"stage=attack-command-candidate phase=pre tribe={scope.TribeId} " +
                    $"command={scope.Command} target={scope.TargetValue1}/{scope.TargetValue2} " +
                    $"tribeUnits={scope.CandidateUnitIds.Count}");
                return;
            }

            int logged = 0;
            foreach (int unitId in scope.CandidateUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null)
                    continue;

                string candidateState = GetAttackCandidateSignature(unit);
                if (scope.PreCandidateSignatures.TryGetValue(unitId, out string preState) &&
                    string.Equals(preState, candidateState, StringComparison.Ordinal) &&
                    !MatchesAttackTargetContext(
                        unit, scope.Command, scope.TargetValue1, scope.TargetValue2))
                {
                    continue;
                }
                string signature =
                    $"{phase}:{scope.MapEpoch}:{scope.TribeId}:{scope.Command}:" +
                    $"{scope.TargetValue1}:{scope.TargetValue2}:{candidateState}";
                if (lastAttackCommandCandidates.TryGetValue(unitId, out string previous) &&
                    string.Equals(previous, signature, StringComparison.Ordinal))
                {
                    continue;
                }

                lastAttackCommandCandidates[unitId] = signature;
                logged++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=attack-command-candidate phase={phase} unit={unitId} " +
                    $"type={unit->r_UnitChimp} global={unit->r_GlobalId} player={unit->r_ControllableForPlayerId} " +
                    $"tribe={unit->r_TribeId}/{scope.TribeId} aiState={unit->r_AIState} " +
                    $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand}/{scope.Command} " +
                    $"target={scope.TargetValue1}/{scope.TargetValue2} " +
                    $"contextUnit={unit->r_AI_ContextTargetUnitId}/{unit->r_AI_ContextTargetUnitGlobalId} " +
                    $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                    $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                    $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY}).");
            }

            if (logged == 0 && phase == "post")
            {
                LogCommandDiagnostic(
                    $"stage=attack-command-candidate phase=post tribe={scope.TribeId} " +
                    $"command={scope.Command} target={scope.TargetValue1}/{scope.TargetValue2} " +
                    $"changedUnits=0 candidates={scope.CandidateUnitIds.Count}");
                foreach (int unitId in scope.CandidateUnitIds)
                {
                    if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                        unit == null)
                    {
                        continue;
                    }

                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=attack-command-candidate phase=post-unchanged " +
                        $"commandSeq={scope.Sequence} unit={unitId} type={unit->r_UnitChimp} " +
                        $"alive={unit->r_AliveState} global={unit->r_GlobalId} " +
                        $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId}/{scope.TribeId} " +
                        $"aiState={unit->r_AIState} " +
                        $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand}/{scope.Command} " +
                        $"target={scope.TargetValue1}/{scope.TargetValue2} " +
                        $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                        $"targetTile=({unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}) " +
                        $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2}) " +
                        $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY}) " +
                        $"contextUnit={unit->r_AI_ContextTargetUnitId}/" +
                        $"{unit->r_AI_ContextTargetUnitGlobalId} " +
                        $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                        $"path={unit->r_PathPlanRelated1}/{unit->r_PathPlanStateBitFlags}/" +
                        $"{unit->r_MovingRelevant}/{unit->p_CurrentPathPlanPosition}/" +
                        $"{unit->p_PathPlanSize}.");
                }
            }
        }

        private static string GetAttackCandidateSignature(GameUnit* unit) =>
            $"{unit->r_AIState}:{unit->r_AI_LastIssuedTribeCommand}:" +
            $"{unit->r_AI_ContextTargetUnitId}:{unit->r_AI_ContextTargetUnitGlobalId}:" +
            $"{unit->r_AI_ContextTargetBuildingTileId}:" +
            $"{unit->r_AttackMoveToTargetTileX}:{unit->r_AttackMoveToTargetTileY}:" +
            $"{unit->r_TargetTilePositionX}:{unit->r_TargetTilePositionY}";

        private AttackUnitTracker GetOrCreateAttackTracker(AttackCommandScope scope, int unitId)
        {
            if (trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker) &&
                tracker.MapEpoch == scope.MapEpoch && tracker.TribeId == scope.TribeId &&
                tracker.Command == scope.Command && tracker.TargetValue1 == scope.TargetValue1 &&
                tracker.TargetValue2 == scope.TargetValue2)
            {
                tracker.ReplacePublishedBuildingApproaches(scope.PublishedBuildingApproaches);
                scope.SynchronousTrackerUnitIds.Add(unitId);
                return tracker;
            }

            tracker = new AttackUnitTracker(
                scope.MapEpoch,
                unitId,
                scope.TribeId,
                scope.Command,
                scope.TargetValue1,
                scope.TargetValue2);
            tracker.ReplacePublishedBuildingApproaches(scope.PublishedBuildingApproaches);
            trackedAttackUnits[unitId] = tracker;
            scope.SynchronousTrackerUnitIds.Add(unitId);
            return tracker;
        }

        private void RemoveSynchronousAttackTrackers(AttackCommandScope scope, string reason)
        {
            foreach (int unitId in scope.SynchronousTrackerUnitIds)
            {
                if (trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker))
                    EndTrackedAttack(unitId, tracker, reason);
            }
        }

        private void ObserveTrackedAttackStates(int tick)
        {
            if (disposed)
                return;

            if (trackedAttackUnits.Count != 0)
            {
                try
                {
                    List<int> unitIds = new List<int>(trackedAttackUnits.Keys);
                    foreach (int unitId in unitIds)
                    {
                        if (!trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker))
                            continue;
                        if (tracker.MapEpoch != mapEpoch)
                        {
                            EndTrackedAttack(unitId, tracker, "map-changed");
                            continue;
                        }
                        if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                            unit == null || unit->r_AliveState != AliveState.IsAlive)
                        {
                            EndTrackedAttack(unitId, tracker, "unit-dead-or-invalid");
                            continue;
                        }

                        TribeAICommand currentCommand =
                            (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
                        if (unit->r_TribeId != tracker.TribeId)
                        {
                            EndTrackedAttack(unitId, tracker, "command-ended-or-replaced");
                            continue;
                        }
                        if (!MatchesTrackedAttackTargetContext(unit, tracker, currentCommand))
                        {
                            EndTrackedAttack(unitId, tracker, "target-changed");
                            continue;
                        }

                        if (tracker.BuilderObserved && tracker.LastPlannerTargetX >= 0 &&
                            unit->r_CurrentTilePositionX == tracker.LastPlannerTargetX &&
                            unit->r_CurrentTilePositionY == tracker.LastPlannerTargetY)
                        {
                            EndTrackedAttack(unitId, tracker, "approach-reached");
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        LogFailure("attack-state-tick", ex);
                    }
                    catch
                    {
                        // Read-only attack diagnostics must never escape into the simulation tick.
                    }
                }
            }

            ObserveTrackedMoatMoveStates(tick);
        }

        private void MarkTrackedAttackPipeline(
            int unitId,
            AttackPipelineStage stage,
            int targetX,
            int targetY,
            bool vanillaModeDetected)
        {
            try
            {
                if (!trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker))
                    return;

                switch (stage)
                {
                    case AttackPipelineStage.Mode:
                        tracker.ModeObserved = true;
                        tracker.VanillaModeDetected |= vanillaModeDetected;
                        break;
                    case AttackPipelineStage.Planner:
                        tracker.PlannerObserved = true;
                        tracker.LastPlannerTargetX = targetX;
                        tracker.LastPlannerTargetY = targetY;
                        break;
                    case AttackPipelineStage.Builder:
                        tracker.BuilderObserved = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    LogFailure("attack-pipeline-marker", ex);
                }
                catch
                {
                    // A diagnostic marker must never escape across a native callback.
                }
            }
        }

        private void RemoveTrackedAttacksForTribe(int tribeId, string reason)
        {
            if (trackedAttackUnits.Count == 0)
                return;

            List<int> unitIds = new List<int>(trackedAttackUnits.Keys);
            foreach (int unitId in unitIds)
            {
                if (trackedAttackUnits.TryGetValue(unitId, out AttackUnitTracker tracker) &&
                    tracker.TribeId == tribeId)
                {
                    EndTrackedAttack(unitId, tracker, reason);
                }
            }
        }

        private void EndTrackedAttack(int unitId, AttackUnitTracker tracker, string reason)
        {
            trackedAttackUnits.Remove(unitId);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=attack-state-end unit={unitId} command={tracker.Command} " +
                $"reason={reason} mode={tracker.ModeObserved} planner={tracker.PlannerObserved} " +
                $"builder={tracker.BuilderObserved}.");
        }

        private void LogBuilderNativeState(
            string stage,
            IntPtr pathManager,
            PlanScope plan,
            int movementClass,
            int movementProfile,
            int? result)
        {
            try
            {
                byte* manager = (byte*)pathManager.ToPointer();
                string resultText = result.HasValue ? result.Value.ToString() : "pending";
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) ||
                    unit == null)
                {
                    LogPipelineDiagnostic(
                        $"stage=builder-native-{stage} unit={plan.UnitId} type=unavailable " +
                        $"path80={*(int*)(manager + PathManagerRouteVariantOffset)} " +
                        $"path84={*(int*)(manager + PathManagerMovementVariantOffset)} " +
                        $"path88={*(int*)(manager + PathManagerAssassinModeOffset)} " +
                        $"movementClass={movementClass} movementProfile={movementProfile} " +
                        $"result={resultText}");
                    return;
                }

                LogPipelineDiagnostic(
                    $"stage=builder-native-{stage} unit={plan.UnitId} type={unit->r_UnitChimp} " +
                    $"player={unit->r_ControllableForPlayerId} aiState={unit->r_AIState} " +
                    $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand} " +
                    $"path80={*(int*)(manager + PathManagerRouteVariantOffset)} " +
                    $"path84={*(int*)(manager + PathManagerMovementVariantOffset)} " +
                    $"path88={*(int*)(manager + PathManagerAssassinModeOffset)} " +
                    $"movementClass={movementClass} movementProfile={movementProfile} " +
                    $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                    $"target=({unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}) " +
                    $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2}) " +
                    $"path={unit->r_PathPlanRelated1}/{unit->r_PathPlanStateBitFlags}/" +
                    $"{unit->r_MovingRelevant}/{unit->p_CurrentPathPlanPosition}/" +
                    $"{unit->p_PathPlanSize} result={resultText}");
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("builder-native-state", ex);
            }
        }

        private void StartOrRefreshMoatMoveTracker(
            PlanScope plan, RouteProbeSummary summary, int builderResult)
        {
            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive)
                {
                    return;
                }

                if (trackedMoatMoves.TryGetValue(
                        plan.UnitId, out MoatMoveTracker existing) &&
                    existing.MapEpoch == mapEpoch &&
                    existing.TargetX == plan.TargetX &&
                    existing.TargetY == plan.TargetY)
                {
                    existing.TribeId = unit->r_TribeId;
                    return;
                }

                var tracker = new MoatMoveTracker(
                    mapEpoch,
                    plan.UnitId,
                    unit->r_TribeId,
                    unit->r_UnitChimp,
                    unit->r_ControllableForPlayerId,
                    plan.TargetX,
                    plan.TargetY,
                    builderResult,
                    unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY,
                    unit->p_CurrentPathPlanPosition,
                    IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)),
                    ReadUnitMoatPathConsumptionMode(unit),
                    CaptureCurrentGameTick());
                ResolveCommandDiagnosticContext(
                    plan.UnitId, unit, out TribeAICommand command, out string commandContext,
                    out int commandSequence);
                tracker.WeightedCommand = command;
                tracker.WeightedCommandContext = commandContext;
                tracker.WeightedCommandSequence = commandSequence;
                trackedMoatMoves[plan.UnitId] = tracker;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=move-track-start unit={plan.UnitId} type={unit->r_UnitChimp} " +
                    $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId} " +
                    $"target=({plan.TargetX},{plan.TargetY}) command={command} " +
                    $"commandContext={commandContext} canDig={CanDigMoat(unit)} " +
                    $"builderResult={builderResult} " +
                    $"{summary.ToLogFields()}.");
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("move-track-start", ex);
            }
        }

        private void StartOrRefreshWeightedShadowTracker(
            BuilderWeightedScope shadow,
            int builderResult,
            WeightedMoatRouteSummary nativeSummary,
            bool nativeValid,
            string decision)
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(shadow.UnitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive)
            {
                return;
            }

            if (!trackedMoatMoves.TryGetValue(shadow.UnitId, out MoatMoveTracker tracker) ||
                tracker.MapEpoch != mapEpoch || tracker.TribeId != shadow.TribeId ||
                tracker.TargetX != shadow.TargetX || tracker.TargetY != shadow.TargetY ||
                tracker.InitialX != shadow.StartX || tracker.InitialY != shadow.StartY)
            {
                tracker = new MoatMoveTracker(
                    mapEpoch, shadow.UnitId, shadow.TribeId, shadow.UnitType, shadow.PlayerId,
                    shadow.TargetX, shadow.TargetY,
                    builderResult, shadow.StartX, shadow.StartY,
                    unit->p_CurrentPathPlanPosition,
                    IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)),
                    ReadUnitMoatPathConsumptionMode(unit),
                    CaptureCurrentGameTick());
                trackedMoatMoves[shadow.UnitId] = tracker;
            }

            tracker.HasWeightedShadow = true;
            tracker.WeightedPlayerId = shadow.PlayerId;
            tracker.WeightedUnitType = shadow.UnitType;
            tracker.WeightedCommand = shadow.Command;
            tracker.WeightedCommandContext = shadow.CommandContext;
            tracker.WeightedCommandSequence = shadow.CommandSequence;
            tracker.AllowReservedTarget = shadow.AllowReservedTarget;
            tracker.PlanningCostProfile = shadow.CostProfile;
            tracker.NativeRouteSummary = nativeSummary;
            tracker.NativeRouteValid = nativeValid;
            tracker.NativeEstimatedTicks = nativeSummary.EstimatedTicks;
            tracker.ShadowEstimatedTicks = shadow.Candidate.EstimatedTicks;
            tracker.ShadowDecision = decision;
            tracker.BuilderResult = builderResult;
            tracker.WeightedPathPublished = string.Equals(
                decision, "weighted-path-published", StringComparison.Ordinal);
            tracker.PublishedLengthChecked = false;
            tracker.PublishedLengthVerified = false;
            tracker.ObservedPublishedPathSize = -1;
            tracker.PublishedRouteSummary = tracker.WeightedPathPublished
                ? shadow.Candidate
                : nativeSummary;
            tracker.RuntimeCadenceCaptured = false;
            tracker.RuntimeCadenceChanged = false;
            tracker.RuntimeCadenceRebased = false;
            tracker.RuntimeCostProfile = default;
            tracker.RuntimeShadowMatchesPublishedCostProfile = false;
            tracker.RuntimeNativeEstimatedTicks = long.MaxValue;
            tracker.RuntimeShadowEstimatedTicks = long.MaxValue;
            tracker.RuntimeShadowDecision = null;
            tracker.LastRuntimeCadenceRejection = null;
            tracker.Calibratable = !tracker.CombatInterrupted &&
                shadow.Calibratable && nativeValid && shadow.CandidateFound;
            tracker.ShadowMatchesPublishedCostProfile = shadow.CandidateFound &&
                (tracker.WeightedPathPublished ||
                 nativeValid && nativeSummary.RouteLength == shadow.Candidate.RouteLength &&
                 nativeSummary.GroundEdges == shadow.Candidate.GroundEdges &&
                 nativeSummary.MoatEdges == shadow.Candidate.MoatEdges);
            tracker.CalibrationReason = tracker.Calibratable
                ? "isolated-unretargeted"
                : tracker.CombatInterrupted
                    ? "combat-interrupted"
                    : "group-or-route-unvalidated";
        }

        private void ObserveTrackedMoatMoveStates(int tick)
        {
            if (trackedMoatMoves.Count == 0)
                return;

            try
            {
                List<int> unitIds = new List<int>(trackedMoatMoves.Keys);
                foreach (int unitId in unitIds)
                {
                    if (!trackedMoatMoves.TryGetValue(unitId, out MoatMoveTracker tracker))
                        continue;
                    if (tracker.MapEpoch != mapEpoch)
                    {
                        EndTrackedMoatMove(unitId, tracker, "map-changed");
                        continue;
                    }
                    if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                        unit == null || unit->r_AliveState != AliveState.IsAlive)
                    {
                        EndTrackedMoatMove(unitId, tracker, "unit-dead-or-invalid");
                        continue;
                    }
                    if (tracker.CombatInterrupted &&
                        (unit->r_TargetTilePositionX2 != tracker.TargetX ||
                         unit->r_TargetTilePositionY2 != tracker.TargetY))
                    {
                        EndTrackedMoatMove(unitId, tracker, "saved-target-changed");
                        continue;
                    }
                    if (unit->r_TribeId != tracker.TribeId)
                    {
                        bool savedTargetStillMatches =
                            unit->r_TargetTilePositionX2 == tracker.TargetX &&
                            unit->r_TargetTilePositionY2 == tracker.TargetY;
                        if (!savedTargetStillMatches)
                        {
                            EndTrackedMoatMove(
                                unitId, tracker, "tribe-changed-and-saved-target-changed");
                            continue;
                        }

                        tracker.TribeId = unit->r_TribeId;
                        tracker.Calibratable = false;
                        tracker.CalibrationReason = "combat-interrupted";
                        if (!tracker.CombatInterrupted)
                        {
                            tracker.CombatInterrupted = true;
                            LogMoveMilestone(
                                tick, unitId, unit, tracker,
                                (TribeAICommand)unit->r_AI_LastIssuedTribeCommand,
                                "combat-interrupted");
                        }
                    }

                    bool reachedRequestedTarget =
                        unit->r_CurrentTilePositionX == tracker.TargetX &&
                        unit->r_CurrentTilePositionY == tracker.TargetY;
                    bool pathConsumed = unit->p_PathPlanSize <= 0 ||
                        unit->p_CurrentPathPlanPosition >= unit->p_PathPlanSize;
                    bool settledOnCurrentTile =
                        unit->r_TargetTilePositionX == unit->r_CurrentTilePositionX &&
                        unit->r_TargetTilePositionY == unit->r_CurrentTilePositionY &&
                        unit->r_NextTilePositionX2 == unit->r_CurrentTilePositionX &&
                        unit->r_NextTilePositionY2 == unit->r_CurrentTilePositionY;
                    bool currentMoat = IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId));
                    int currentTileId = unchecked((int)unit->r_CurrentPositionTileId);
                    ushort currentConsumerMode = ReadUnitMoatPathConsumptionMode(unit);
                    tracker.LastConsumerMode = currentConsumerMode;
                    if (currentConsumerMode < tracker.MinimumConsumerMode)
                        tracker.MinimumConsumerMode = currentConsumerMode;
                    if (currentConsumerMode > tracker.MaximumConsumerMode)
                        tracker.MaximumConsumerMode = currentConsumerMode;
                    tracker.ConsumerModeObservedNonZero |= currentConsumerMode != 0;
                    if (currentMoat)
                        ObserveActualMoatOwnership(tracker, currentTileId);
                    TribeAICommand command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
                    bool tileChanged = unit->r_CurrentTilePositionX != tracker.LastX ||
                        unit->r_CurrentTilePositionY != tracker.LastY;
                    bool progressed = tileChanged ||
                        unit->p_CurrentPathPlanPosition != tracker.LastPathPosition;
                    int transitionTicks = -1;
                    bool firstObservation = tracker.FirstObservedTick < 0;
                    if (firstObservation)
                    {
                        tracker.FirstObservedTick = tick;
                        if (tracker.LastTileTransitionTick < 0)
                            tracker.LastTileTransitionTick = tick;
                    }
                    if (tracker.WeightedPathPublished && !tracker.PublishedLengthChecked)
                    {
                        tracker.PublishedLengthChecked = true;
                        tracker.ObservedPublishedPathSize = unchecked((int)unit->p_PathPlanSize);
                        tracker.PublishedLengthVerified =
                            tracker.ObservedPublishedPathSize == tracker.BuilderResult;
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"MoveMoat stage=weighted-path-consumer-contract " +
                            $"unit={unitId} commandSeq={tracker.WeightedCommandSequence} " +
                            $"expectedLength={tracker.BuilderResult} " +
                            $"observedLength={tracker.ObservedPublishedPathSize} " +
                            $"pathPosition={unit->p_CurrentPathPlanPosition} " +
                            $"valid={tracker.PublishedLengthVerified}.");
                        if (!tracker.PublishedLengthVerified)
                        {
                            tracker.Calibratable = false;
                            tracker.CalibrationReason = "published-length-not-consumed";
                        }
                    }
                    tracker.LastObservedTick = tick;
                    if (tileChanged)
                    {
                        int dx = Math.Abs(unit->r_CurrentTilePositionX - tracker.LastX);
                        int dy = Math.Abs(unit->r_CurrentTilePositionY - tracker.LastY);
                        if (dx > 1 || dy > 1)
                        {
                            tracker.Calibratable = false;
                            tracker.CalibrationReason = "non-adjacent-tile-change";
                        }
                        if (tracker.TileTransitionCount == 0)
                        {
                            tracker.FirstTileTransitionTick = tick;
                            tracker.FirstTransitionTimingUnavailable =
                                firstObservation && tracker.TrackingStartTick < 0;
                        }
                        tracker.TileTransitionCount++;
                        if ((!firstObservation || tracker.TrackingStartTick >= 0) &&
                            tracker.LastTileTransitionTick >= 0)
                        {
                            transitionTicks = tick - tracker.LastTileTransitionTick;
                            if (transitionTicks >= 0)
                            {
                                tracker.TimedTileTransitionCount++;
                                tracker.TileTransitionTicks += transitionTicks;
                                tracker.MaximumTileTransitionTicks = Math.Max(
                                    tracker.MaximumTileTransitionTicks, transitionTicks);
                            }
                        }

                        if (dx <= 1 && dy <= 1 && dx + dy > 0)
                        {
                            bool moatTransition = tracker.WasOnMoat || currentMoat;
                            if (moatTransition)
                            {
                                tracker.ActualMoatTransitions++;
                                if (transitionTicks >= 0)
                                {
                                    tracker.TimedMoatTransitions++;
                                    tracker.ActualMoatTransitionTicks += transitionTicks;
                                    tracker.MinimumMoatTransitionTicks = Math.Min(
                                        tracker.MinimumMoatTransitionTicks, transitionTicks);
                                    tracker.MaximumMoatTransitionTicks = Math.Max(
                                        tracker.MaximumMoatTransitionTicks, transitionTicks);
                                }
                            }
                            else
                            {
                                tracker.ActualGroundTransitions++;
                                if (transitionTicks >= 0)
                                {
                                    tracker.TimedGroundTransitions++;
                                    tracker.ActualGroundTransitionTicks += transitionTicks;
                                    tracker.MinimumGroundTransitionTicks = Math.Min(
                                        tracker.MinimumGroundTransitionTicks, transitionTicks);
                                    tracker.MaximumGroundTransitionTicks = Math.Max(
                                        tracker.MaximumGroundTransitionTicks, transitionTicks);
                                }
                            }
                            if (dx == 1 && dy == 1)
                            {
                                tracker.ActualDiagonalTransitions++;
                                if (transitionTicks >= 0)
                                {
                                    tracker.TimedDiagonalTransitions++;
                                    tracker.ActualDiagonalTransitionTicks += transitionTicks;
                                }
                            }
                            else
                            {
                                tracker.ActualCardinalTransitions++;
                                if (transitionTicks >= 0)
                                {
                                    tracker.TimedCardinalTransitions++;
                                    tracker.ActualCardinalTransitionTicks += transitionTicks;
                                }
                            }

                            int direction = EncodeDirectionDelta(
                                unit->r_CurrentTilePositionX - tracker.LastX,
                                unit->r_CurrentTilePositionY - tracker.LastY);
                            if (direction >= 0)
                            {
                                if (tracker.LastActualDirection >= 0 &&
                                    tracker.LastActualDirection != direction)
                                {
                                    tracker.ActualDirectionChanges++;
                                }
                                tracker.LastActualDirection = direction;
                                tracker.ActualRouteFingerprint = unchecked(
                                    (tracker.ActualRouteFingerprint ^ (byte)direction) *
                                    RouteFingerprintPrime);
                            }
                        }
                        tracker.LastTileTransitionTick = tick;
                    }
                    if (progressed && tracker.StallReported)
                    {
                        tracker.StallReported = false;
                        tracker.Calibratable = false;
                        tracker.CalibrationReason = "resumed-after-stall";
                        LogMoveMilestone(
                            tick, unitId, unit, tracker, command, "resumed-after-stall");
                    }
                    if (tracker.LastProgressTick < 0 || progressed)
                        tracker.LastProgressTick = tick;

                    if (progressed && tracker.HasWeightedShadow)
                        ObserveRuntimeCadenceShadow(tick, unit, tracker);

                    if (progressed && tracker.PostCombatRepathEntered &&
                        !tracker.MovementResumedAfterCombat)
                    {
                        tracker.MovementResumedAfterCombat = true;
                        LogMoveMilestone(
                            tick, unitId, unit, tracker, command, "movement-resumed");
                    }

                    if (!tracker.MovementStarted &&
                        (unit->r_CurrentTilePositionX != tracker.InitialX ||
                         unit->r_CurrentTilePositionY != tracker.InitialY))
                    {
                        tracker.MovementStarted = true;
                        LogMoveMilestone(
                            tick, unitId, unit, tracker, command, "movement-started");
                    }

                    if (currentMoat && !tracker.MoatEntered)
                    {
                        tracker.MoatEntered = true;
                        LogMoveMilestone(
                            tick, unitId, unit, tracker, command,
                            tracker.StartedOnMoat ? "started-on-moat" : "moat-entered");
                    }
                    if (tracker.MoatEntered && !tracker.MoatExited &&
                        tracker.WasOnMoat && !currentMoat)
                    {
                        tracker.MoatExited = true;
                        LogMoveMilestone(
                            tick, unitId, unit, tracker, command, "moat-exited");
                    }

                    if (reachedRequestedTarget && pathConsumed && settledOnCurrentTile)
                    {
                        EndTrackedMoatMove(unitId, tracker, "path-completed-at-target");
                        continue;
                    }

                    if (tracker.LastProgressTick >= 0 &&
                        tick - tracker.LastProgressTick >= DiagnosticStallTickThreshold &&
                        !tracker.StallReported)
                    {
                        tracker.StallReported = true;
                        tracker.Calibratable = false;
                        tracker.CalibrationReason = "stalled-or-congested";
                        LogMoveMilestone(
                            tick, unitId, unit, tracker, command, "stalled");
                    }

                    tracker.LastX = unit->r_CurrentTilePositionX;
                    tracker.LastY = unit->r_CurrentTilePositionY;
                    tracker.LastPathPosition = unit->p_CurrentPathPlanPosition;
                    tracker.WasOnMoat = currentMoat;
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("move-state-tick", ex);
            }
        }

        private void ObserveRuntimeCadenceShadow(
            int tick, GameUnit* unit, MoatMoveTracker tracker)
        {
            if (!TryCaptureWeightedMovementCostProfile(
                    unit, out WeightedMovementCostProfile runtimeProfile,
                    out string rejectionReason))
            {
                string rejection = rejectionReason ?? "runtime-cadence-unavailable";
                if (!string.Equals(
                        tracker.LastRuntimeCadenceRejection, rejection,
                        StringComparison.Ordinal))
                {
                    tracker.LastRuntimeCadenceRejection = rejection;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=weighted-shadow-runtime unit={tracker.UnitId} " +
                        $"tick={tick} decision=no-valid-shadow-route reason={rejection}.");
                }
                return;
            }

            if (tracker.RuntimeCadenceCaptured)
            {
                if (tracker.RuntimeCostProfile.HasSameNormalizedCadence(runtimeProfile))
                    return;

                // Some handlers initialize SpeedBonus only after path publication. If this
                // happens before the first tile transition, the new profile describes the
                // entire measurable route more accurately and can safely replace the first one.
                if (tracker.TileTransitionCount == 0 && !tracker.RuntimeCadenceChanged)
                {
                    WeightedMovementCostProfile previousProfile = tracker.RuntimeCostProfile;
                    tracker.RuntimeCostProfile = runtimeProfile;
                    tracker.RuntimeCadenceRebased = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=weighted-shadow-runtime-rebase unit={tracker.UnitId} " +
                        $"tick={tick} previous={FormatCostProfile(previousProfile)} " +
                        $"current={FormatCostProfile(runtimeProfile)}.");
                    CalculateAndLogRuntimeCadenceShadow(tick, tracker, runtimeProfile, "rebase");
                    return;
                }

                if (tracker.TileTransitionCount <= 1 && !tracker.RuntimeCadenceChanged &&
                    tracker.RuntimeCostProfile.SpeedBonus == 0 &&
                    runtimeProfile.SpeedBonus > 0 &&
                    tracker.RuntimeCostProfile.HasSameBaseCadenceExceptSpeedBonus(runtimeProfile))
                {
                    WeightedMovementCostProfile previousProfile = tracker.RuntimeCostProfile;
                    tracker.RuntimeCostProfile = runtimeProfile;
                    tracker.RuntimeCadenceRebased = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=weighted-shadow-runtime-rebase unit={tracker.UnitId} " +
                        $"tick={tick} kind=late-handler-speed-bonus " +
                        $"previous={FormatCostProfile(previousProfile)} " +
                        $"current={FormatCostProfile(runtimeProfile)}.");
                    CalculateAndLogRuntimeCadenceShadow(
                        tick, tracker, runtimeProfile, "late-handler-speed-bonus");
                    return;
                }

                if (!tracker.RuntimeCadenceChanged)
                {
                    tracker.RuntimeCadenceChanged = true;
                    tracker.Calibratable = false;
                    tracker.CalibrationReason = "runtime-cadence-changed-after-first-transition";
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=weighted-shadow-runtime-change unit={tracker.UnitId} " +
                        $"tick={tick} first={FormatCostProfile(tracker.RuntimeCostProfile)} " +
                        $"current={FormatCostProfile(runtimeProfile)}.");
                }
                return;
            }

            tracker.RuntimeCadenceCaptured = true;
            tracker.RuntimeCostProfile = runtimeProfile;
            tracker.LastRuntimeCadenceRejection = null;

            CalculateAndLogRuntimeCadenceShadow(tick, tracker, runtimeProfile, "initial");
        }

        private void CalculateAndLogRuntimeCadenceShadow(
            int tick,
            MoatMoveTracker tracker,
            WeightedMovementCostProfile runtimeProfile,
            string captureKind)
        {
            tracker.LastRuntimeCadenceRejection = null;

            bool found = false;
            WeightedMoatRouteSummary runtimeCandidate = default;
            if (!weightedShadowBusy)
            {
                weightedShadowBusy = true;
                try
                {
                    found = weightedMoatRoutePlanner.TryBuild(
                        tracker.WeightedPlayerId,
                        tracker.InitialX,
                        tracker.InitialY,
                        tracker.TargetX,
                        tracker.TargetY,
                        runtimeProfile,
                        tracker.AllowReservedTarget,
                        out runtimeCandidate);
                }
                finally
                {
                    weightedShadowBusy = false;
                }
            }

            long runtimeNativeTicks = tracker.NativeRouteValid
                ? runtimeProfile.EstimateRouteTicks(
                    tracker.NativeRouteSummary.GroundEdges,
                    tracker.NativeRouteSummary.MoatEdges)
                : long.MaxValue;
            string decision;
            string reason;
            if (!found)
            {
                decision = "no-valid-shadow-route";
                reason = runtimeCandidate.Reason;
            }
            else if (!tracker.NativeRouteValid || runtimeNativeTicks == long.MaxValue)
            {
                decision = "no-valid-shadow-route";
                reason = "native-route-unavailable-for-runtime-cadence";
            }
            else if (runtimeCandidate.MoatEdges > 0 &&
                runtimeCandidate.EstimatedTicks < runtimeNativeTicks)
            {
                decision = "shadow-friendly-moat";
                reason = "runtime-cadence-shadow-faster";
            }
            else
            {
                decision = tracker.NativeRouteSummary.MoatEdges > 0
                    ? "native-friendly-moat"
                    : "native-ground";
                reason = runtimeCandidate.MoatEdges == 0
                    ? "runtime-cadence-ground-winner"
                    : "runtime-cadence-native-not-slower";
            }

            tracker.RuntimeNativeEstimatedTicks = runtimeNativeTicks;
            tracker.RuntimeShadowEstimatedTicks = tracker.WeightedPathPublished
                ? runtimeProfile.EstimateRouteTicks(
                    tracker.PublishedRouteSummary.GroundEdges,
                    tracker.PublishedRouteSummary.MoatEdges)
                : found ? runtimeCandidate.EstimatedTicks : long.MaxValue;
            tracker.RuntimeShadowDecision = decision;
            tracker.RuntimeShadowMatchesPublishedCostProfile =
                (tracker.WeightedPathPublished && tracker.PublishedRouteSummary.Found) ||
                (found && tracker.NativeRouteValid &&
                 tracker.NativeRouteSummary.RouteLength == runtimeCandidate.RouteLength &&
                 tracker.NativeRouteSummary.GroundEdges == runtimeCandidate.GroundEdges &&
                 tracker.NativeRouteSummary.MoatEdges == runtimeCandidate.MoatEdges);

            long saving = found && runtimeNativeTicks != long.MaxValue
                ? runtimeNativeTicks - runtimeCandidate.EstimatedTicks
                : 0;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=weighted-shadow-runtime unit={tracker.UnitId} " +
                $"type={tracker.WeightedUnitType} player={tracker.WeightedPlayerId} " +
                $"command={tracker.WeightedCommand}({(uint)tracker.WeightedCommand}) tick={tick} " +
                $"start=({tracker.InitialX},{tracker.InitialY}) " +
                $"target=({tracker.TargetX},{tracker.TargetY}) " +
                $"capture={captureKind} " +
                $"planning={FormatCostProfile(tracker.PlanningCostProfile)} " +
                $"runtime={FormatCostProfile(runtimeProfile)} " +
                $"decision={decision} reason={reason} " +
                $"nativeLength={tracker.NativeRouteSummary.RouteLength} " +
                $"nativeGround={tracker.NativeRouteSummary.GroundEdges} " +
                $"nativeMoat={tracker.NativeRouteSummary.MoatEdges} " +
                $"nativeDiagonal={tracker.NativeRouteSummary.DiagonalEdges} " +
                $"nativeDirectionChanges={tracker.NativeRouteSummary.DirectionChanges} " +
                $"nativeFingerprint=0x{tracker.NativeRouteSummary.RouteFingerprint:X16} " +
                $"nativeTicks={(runtimeNativeTicks == long.MaxValue ? "n/a" : runtimeNativeTicks.ToString())} " +
                $"shadowFound={found} shadowLength={runtimeCandidate.RouteLength} " +
                $"shadowGround={runtimeCandidate.GroundEdges} " +
                $"shadowMoat={runtimeCandidate.MoatEdges} " +
                $"shadowDiagonal={runtimeCandidate.DiagonalEdges} " +
                $"shadowDirectionChanges={runtimeCandidate.DirectionChanges} " +
                $"shadowFingerprint=0x{runtimeCandidate.RouteFingerprint:X16} " +
                $"shadowTicks={(found ? runtimeCandidate.EstimatedTicks.ToString() : "n/a")} " +
                $"publishedWeighted={tracker.WeightedPathPublished} " +
                $"publishedTicks={(tracker.RuntimeShadowEstimatedTicks == long.MaxValue ? "n/a" : tracker.RuntimeShadowEstimatedTicks.ToString())} " +
                $"savingTicks={saving} searchMs={runtimeCandidate.SearchMilliseconds:F3} " +
                $"expanded={runtimeCandidate.ExpandedNodes}.");
        }

        private static string FormatCostProfile(WeightedMovementCostProfile profile) =>
            $"speed={profile.CurrentSpeed}/effective={profile.CurrentSpeed2}/" +
            $"bonus={profile.SpeedBonus}/additionalSubsteps={profile.AdditionalSubsteps}/" +
            $"extraDelay={profile.ExtraDelay}/phase={profile.MoatPhase}/" +
            $"terrainPenalty={profile.CurrentTerrainPenalty}/" +
            $"normalized={profile.NormalizedDelay}/progress={profile.CadenceProgress}";

        private static int EncodeDirectionDelta(int dx, int dy)
        {
            if (dx == 0 && dy == -1) return 0;
            if (dx == 1 && dy == -1) return 1;
            if (dx == 1 && dy == 0) return 2;
            if (dx == 1 && dy == 1) return 3;
            if (dx == 0 && dy == 1) return 4;
            if (dx == -1 && dy == 1) return 5;
            if (dx == -1 && dy == 0) return 6;
            if (dx == -1 && dy == -1) return 7;
            return -1;
        }

        private static int CaptureCurrentGameTick()
        {
            try
            {
                return GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
            }
            catch
            {
                return -1;
            }
        }

        private bool IsCompletedMoatTile(int tileId) =>
            IsValidTileId(tileId) && (tileFlags[tileId] & CompletedMoatTileFlag) != 0;

        private void ObserveActualMoatOwnership(MoatMoveTracker tracker, int tileId)
        {
            if (!IsCompletedMoatTile(tileId) || !tracker.ActualMoatTileIds.Add(tileId))
                return;

            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero || getMoatIdAtTile == null)
            {
                tracker.ActualInvalidMoatOwnerTiles++;
                return;
            }

            int moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (moatId <= 0 || moatId >= moatCount)
            {
                tracker.ActualInvalidMoatOwnerTiles++;
                return;
            }

            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            int ownerId = moatRecord[MoatOwnerOffset];
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(ownerId) ||
                !playerApi.IsPlayerIdValid(tracker.PlayerId))
            {
                tracker.ActualInvalidMoatOwnerTiles++;
                return;
            }

            if ((uint)ownerId < 32)
                tracker.ActualMoatOwnerMask |= 1u << ownerId;
            if (ownerId == tracker.PlayerId)
                tracker.ActualOwnMoatTiles++;
            else if (playerApi.IsPlayerAlliedTo(tracker.PlayerId, ownerId))
                tracker.ActualAlliedMoatTiles++;
            else
                tracker.ActualEnemyMoatTiles++;
        }

        private void LogMoveMilestone(
            int tick,
            int unitId,
            GameUnit* unit,
            MoatMoveTracker tracker,
            TribeAICommand command,
            string milestone)
        {
            string cadenceSnapshot = TryCaptureWeightedMovementCostProfile(
                unit, out WeightedMovementCostProfile profile, out string rejectionReason)
                    ? FormatCostProfile(profile)
                    : $"unavailable/{rejectionReason ?? "unknown"}";
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=move-milestone event={milestone} tick={tick} " +
                $"unit={unitId} type={unit->r_UnitChimp} " +
                $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId} " +
                $"runtimeCommand={command}({(uint)command}) " +
                $"commandSeq={tracker.WeightedCommandSequence} " +
                $"plannedCommand={tracker.WeightedCommand}({(uint)tracker.WeightedCommand}) " +
                $"commandContext={tracker.WeightedCommandContext ?? "unresolved"} " +
                $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                $"requestedTarget=({tracker.TargetX},{tracker.TargetY}) " +
                $"path={unit->p_CurrentPathPlanPosition}/{unit->p_PathPlanSize} " +
                $"moatConsumerMode={ReadUnitMoatPathConsumptionMode(unit)} " +
                $"cadence={cadenceSnapshot} " +
                $"builderResult={tracker.BuilderResult}.");
        }

        private void MarkPostCombatRepathEntered(
            int unitId,
            GameUnit* unit,
            PlanScope plan,
            bool requiredFriendlyMoat)
        {
            if (!trackedMoatMoves.TryGetValue(unitId, out MoatMoveTracker tracker) ||
                tracker.MapEpoch != mapEpoch ||
                tracker.TargetX != plan.TargetX || tracker.TargetY != plan.TargetY)
            {
                return;
            }

            tracker.TribeId = unit->r_TribeId;
            if (tracker.PostCombatRepathEntered)
                return;

            tracker.PostCombatRepathEntered = true;
            tracker.PostCombatRequiredFriendlyMoat = requiredFriendlyMoat;
            LogMoveMilestone(
                CaptureCurrentGameTick(), unitId, unit, tracker,
                (TribeAICommand)unit->r_AI_LastIssuedTribeCommand,
                requiredFriendlyMoat
                    ? "post-combat-repath-entered-required-moat"
                    : "post-combat-repath-entered-ground-or-faster-moat");
        }

        private static ushort ReadUnitMoatPathConsumptionMode(GameUnit* unit)
        {
            if (unit == null)
                return 0;

            // Native indexes from the unit-manager base and stores this ushort at +0x9C8.
            // The Script Extender pointer begins at manager + unitId*0x490 + 0x65C.
            return *(ushort*)((byte*)unit + UnitMoatPathConsumptionModeOffset);
        }

        private void RemoveTrackedMoatMovesForTribe(int tribeId, string reason)
        {
            if (trackedMoatMoves.Count == 0)
                return;

            List<int> unitIds = new List<int>(trackedMoatMoves.Keys);
            foreach (int unitId in unitIds)
            {
                if (trackedMoatMoves.TryGetValue(unitId, out MoatMoveTracker tracker) &&
                    tracker.TribeId == tribeId)
                {
                    EndTrackedMoatMove(unitId, tracker, reason);
                }
            }
        }

        private void EndTrackedMoatMove(int unitId, MoatMoveTracker tracker, string reason)
        {
            trackedMoatMoves.Remove(unitId);
            bool completedAtTarget = string.Equals(
                reason, "path-completed-at-target", StringComparison.Ordinal);
            if (!completedAtTarget && tracker.Calibratable)
            {
                tracker.Calibratable = false;
                tracker.CalibrationReason = $"incomplete-{reason}";
            }
            int measurementStartTick = tracker.TrackingStartTick >= 0
                ? tracker.TrackingStartTick
                : tracker.FirstObservedTick;
            int actualTicks = measurementStartTick >= 0 &&
                tracker.LastObservedTick >= measurementStartTick
                    ? tracker.LastObservedTick - measurementStartTick
                    : -1;
            int firstTransitionWaitTicks = measurementStartTick >= 0 &&
                !tracker.FirstTransitionTimingUnavailable &&
                tracker.FirstTileTransitionTick >= measurementStartTick
                    ? tracker.FirstTileTransitionTick - measurementStartTick
                    : -1;
            int finalSettleTicks = tracker.LastTileTransitionTick >= 0 &&
                tracker.LastObservedTick >= tracker.LastTileTransitionTick
                    ? tracker.LastObservedTick - tracker.LastTileTransitionTick
                    : -1;
            bool actualMatchesNativeFingerprint = tracker.NativeRouteValid &&
                tracker.TileTransitionCount == tracker.NativeRouteSummary.RouteLength &&
                tracker.ActualRouteFingerprint == tracker.NativeRouteSummary.RouteFingerprint;
            bool actualMatchesPublishedFingerprint =
                tracker.PublishedRouteSummary.Found &&
                tracker.TileTransitionCount == tracker.PublishedRouteSummary.RouteLength &&
                tracker.ActualRouteFingerprint == tracker.PublishedRouteSummary.RouteFingerprint;
            if (tracker.WeightedPathPublished && !actualMatchesPublishedFingerprint)
            {
                tracker.Calibratable = false;
                tracker.CalibrationReason =
                    tracker.PublishedRouteSummary.MoatEdges > 0 &&
                    tracker.ActualMoatTransitions == 0
                        ? "published-path-rejected-before-moat"
                        : "published-path-not-consumed";
            }
            bool ownerSafeActualRoute = tracker.ActualEnemyMoatTiles == 0 &&
                tracker.ActualInvalidMoatOwnerTiles == 0;
            bool weightedPublicationVerified = tracker.WeightedPathPublished &&
                completedAtTarget && actualMatchesPublishedFingerprint &&
                tracker.PublishedLengthVerified &&
                tracker.ConsumerModeObservedNonZero && ownerSafeActualRoute;
            long nativeCalibrationDelta = tracker.Calibratable &&
                !tracker.WeightedPathPublished && actualTicks >= 0 &&
                tracker.NativeEstimatedTicks > 0
                    ? actualTicks - tracker.NativeEstimatedTicks
                    : long.MinValue;
            long shadowCalibrationDelta = tracker.Calibratable &&
                tracker.ShadowMatchesPublishedCostProfile && actualTicks >= 0 &&
                tracker.ShadowEstimatedTicks > 0
                    ? actualTicks - tracker.ShadowEstimatedTicks
                    : long.MinValue;
            long runtimeNativeCalibrationDelta = tracker.Calibratable &&
                tracker.RuntimeCadenceCaptured && !tracker.RuntimeCadenceChanged &&
                actualTicks >= 0 && tracker.RuntimeNativeEstimatedTicks > 0 &&
                tracker.RuntimeNativeEstimatedTicks != long.MaxValue
                    ? actualTicks - tracker.RuntimeNativeEstimatedTicks
                    : long.MinValue;
            long runtimeShadowCalibrationDelta = tracker.Calibratable &&
                tracker.RuntimeCadenceCaptured && !tracker.RuntimeCadenceChanged &&
                tracker.RuntimeShadowMatchesPublishedCostProfile && actualTicks >= 0 &&
                tracker.RuntimeShadowEstimatedTicks > 0 &&
                tracker.RuntimeShadowEstimatedTicks != long.MaxValue
                    ? actualTicks - tracker.RuntimeShadowEstimatedTicks
                    : long.MinValue;
            string shadowCalibrationReason = !tracker.Calibratable
                ? tracker.CalibrationReason ?? "context-unvalidated"
                : tracker.ShadowMatchesPublishedCostProfile
                    ? "matching-published-cost-profile"
                    : "published-cost-profile-differs";
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=move-state-end unit={unitId} type={tracker.UnitType} " +
                $"player={tracker.PlayerId} tribe={tracker.TribeId} " +
                $"commandSeq={tracker.WeightedCommandSequence} " +
                $"plannedCommand={tracker.WeightedCommand}({(uint)tracker.WeightedCommand}) " +
                $"commandContext={tracker.WeightedCommandContext ?? "unresolved"} " +
                $"target=({tracker.TargetX},{tracker.TargetY}) " +
                $"reason={reason} weightedShadow={tracker.HasWeightedShadow} " +
                $"combatInterrupted={tracker.CombatInterrupted} " +
                $"postCombatRepath={tracker.PostCombatRepathEntered} " +
                $"postCombatRequiredFriendlyMoat={tracker.PostCombatRequiredFriendlyMoat} " +
                $"movementResumedAfterCombat={tracker.MovementResumedAfterCombat} " +
                $"decision={tracker.ShadowDecision ?? "none"} " +
                $"actualTicks={actualTicks} " +
                $"nativeEstimatedTicks={tracker.NativeEstimatedTicks} " +
                $"shadowEstimatedTicks={tracker.ShadowEstimatedTicks} " +
                $"nativeCalibrationDeltaTicks={(nativeCalibrationDelta == long.MinValue ? "n/a" : nativeCalibrationDelta.ToString())} " +
                $"shadowCalibrationDeltaTicks={(shadowCalibrationDelta == long.MinValue ? "n/a" : shadowCalibrationDelta.ToString())} " +
                $"runtimeCadenceCaptured={tracker.RuntimeCadenceCaptured} " +
                $"runtimeCadenceChanged={tracker.RuntimeCadenceChanged} " +
                $"runtimeCadenceRebased={tracker.RuntimeCadenceRebased} " +
                $"runtimeDecision={tracker.RuntimeShadowDecision ?? "none"} " +
                $"runtimeNativeEstimatedTicks={(tracker.RuntimeNativeEstimatedTicks == long.MaxValue ? "n/a" : tracker.RuntimeNativeEstimatedTicks.ToString())} " +
                $"runtimeShadowEstimatedTicks={(tracker.RuntimeShadowEstimatedTicks == long.MaxValue ? "n/a" : tracker.RuntimeShadowEstimatedTicks.ToString())} " +
                $"runtimeNativeCalibrationDeltaTicks={(runtimeNativeCalibrationDelta == long.MinValue ? "n/a" : runtimeNativeCalibrationDelta.ToString())} " +
                $"runtimeShadowCalibrationDeltaTicks={(runtimeShadowCalibrationDelta == long.MinValue ? "n/a" : runtimeShadowCalibrationDelta.ToString())} " +
                $"tileTransitions={tracker.TileTransitionCount} " +
                $"actualGround={tracker.ActualGroundTransitions}/timed={tracker.TimedGroundTransitions}/ticks={tracker.ActualGroundTransitionTicks}/" +
                $"min={(tracker.TimedGroundTransitions > 0 ? tracker.MinimumGroundTransitionTicks : -1)}/" +
                $"max={(tracker.TimedGroundTransitions > 0 ? tracker.MaximumGroundTransitionTicks : -1)} " +
                $"actualMoat={tracker.ActualMoatTransitions}/timed={tracker.TimedMoatTransitions}/ticks={tracker.ActualMoatTransitionTicks}/" +
                $"min={(tracker.TimedMoatTransitions > 0 ? tracker.MinimumMoatTransitionTicks : -1)}/" +
                $"max={(tracker.TimedMoatTransitions > 0 ? tracker.MaximumMoatTransitionTicks : -1)} " +
                $"actualMoatTiles={tracker.ActualMoatTileIds.Count} " +
                $"actualMoatOwners=own:{tracker.ActualOwnMoatTiles}/allied:{tracker.ActualAlliedMoatTiles}/" +
                $"enemy:{tracker.ActualEnemyMoatTiles}/invalid:{tracker.ActualInvalidMoatOwnerTiles}/" +
                $"mask:0x{tracker.ActualMoatOwnerMask:X} " +
                $"ownerSafetyViolation={tracker.ActualEnemyMoatTiles > 0} " +
                $"consumerMode=last:{tracker.LastConsumerMode}/min:{tracker.MinimumConsumerMode}/" +
                $"max:{tracker.MaximumConsumerMode}/nonZero:{tracker.ConsumerModeObservedNonZero} " +
                $"publishedLength=expected:{tracker.BuilderResult}/" +
                $"observed:{tracker.ObservedPublishedPathSize}/" +
                $"checked:{tracker.PublishedLengthChecked}/valid:{tracker.PublishedLengthVerified} " +
                $"weightedPublicationVerified={weightedPublicationVerified} " +
                $"actualCardinal={tracker.ActualCardinalTransitions}/timed={tracker.TimedCardinalTransitions}/ticks={tracker.ActualCardinalTransitionTicks} " +
                $"actualDiagonal={tracker.ActualDiagonalTransitions}/timed={tracker.TimedDiagonalTransitions}/ticks={tracker.ActualDiagonalTransitionTicks} " +
                $"actualDirectionChanges={tracker.ActualDirectionChanges} " +
                $"actualFingerprint=0x{tracker.ActualRouteFingerprint:X16} " +
                $"nativeFingerprint=0x{tracker.NativeRouteSummary.RouteFingerprint:X16} " +
                $"actualMatchesNativeFingerprint={actualMatchesNativeFingerprint} " +
                $"publishedFingerprint=0x{tracker.PublishedRouteSummary.RouteFingerprint:X16} " +
                $"actualMatchesPublishedFingerprint={actualMatchesPublishedFingerprint} " +
                $"firstTransitionWaitTicks={firstTransitionWaitTicks} " +
                $"firstTransitionTimingUnavailable={tracker.FirstTransitionTimingUnavailable} " +
                $"finalSettleTicks={finalSettleTicks} " +
                $"timedTransitions={tracker.TimedTileTransitionCount} " +
                $"averageTransitionTicks={(tracker.TimedTileTransitionCount > 0 ? tracker.TileTransitionTicks / tracker.TimedTileTransitionCount : -1)} " +
                $"maximumTransitionTicks={tracker.MaximumTileTransitionTicks} " +
                $"calibratable={tracker.Calibratable} " +
                $"shadowMatchesPublishedCostProfile={tracker.ShadowMatchesPublishedCostProfile} " +
                $"shadowCalibrationReason={shadowCalibrationReason} " +
                $"calibrationReason={tracker.CalibrationReason ?? "not-instrumented"}.");
        }

        private bool MatchesAttackTargetContext(
            GameUnit* unit,
            TribeAICommand command,
            int targetValue1,
            int targetValue2)
        {
            if (command == TribeAICommand.AttackUnit)
            {
                return unit->r_AI_ContextTargetUnitId == targetValue1 &&
                    unit->r_AI_ContextTargetUnitGlobalId == unchecked((uint)targetValue2);
            }

            if (!IsBuildingAttackCommand(command) ||
                unit->r_AI_ContextTargetBuildingTileId > (uint)int.MaxValue)
            {
                return false;
            }

            int footprintTileId = (int)unit->r_AI_ContextTargetBuildingTileId;
            return TryValidateHostileBuildingTarget(
                    targetValue1,
                    unchecked((uint)targetValue2),
                    unit->r_ControllableForPlayerId,
                    out GameBuilding* building) &&
                IsExactBuildingContextTile(targetValue1, building, footprintTileId);
        }

        private bool MatchesCompletedAttackTargetContext(
            GameUnit* unit, AttackCommandScope scope)
        {
            if (unit == null || scope == null)
                return false;
            if (scope.Command == TribeAICommand.AttackUnit)
            {
                return (TribeAICommand)unit->r_AI_LastIssuedTribeCommand == scope.Command &&
                    MatchesAttackTargetContext(
                        unit, scope.Command, scope.TargetValue1, scope.TargetValue2);
            }

            if (!IsBuildingAttackCommand(scope.Command) ||
                !MatchesAttackTargetContext(
                    unit, scope.Command, scope.TargetValue1, scope.TargetValue2))
            {
                return false;
            }

            return TryGetUnitAttackMoveTile(unit, out int approachTileId) &&
                HasPublishedBuildingApproachPair(
                    scope.PublishedBuildingApproaches,
                    approachTileId,
                    unchecked((int)unit->r_AI_ContextTargetBuildingTileId));
        }

        private bool MatchesTrackedAttackTargetContext(
            GameUnit* unit, AttackUnitTracker tracker, TribeAICommand currentCommand)
        {
            if (tracker.Command == TribeAICommand.AttackUnit)
            {
                return currentCommand == tracker.Command &&
                    MatchesAttackTargetContext(
                        unit, tracker.Command, tracker.TargetValue1, tracker.TargetValue2);
            }

            if (!IsBuildingAttackCommand(tracker.Command) ||
                !MatchesAttackTargetContext(
                    unit, tracker.Command, tracker.TargetValue1, tracker.TargetValue2) ||
                !TryGetUnitAttackMoveTile(unit, out int approachTileId))
            {
                return false;
            }

            return HasPublishedBuildingApproachPair(
                tracker.PublishedBuildingApproaches,
                approachTileId,
                unchecked((int)unit->r_AI_ContextTargetBuildingTileId));
        }

        private static bool IsAttackCommand(TribeAICommand command) =>
            command == TribeAICommand.AttackUnit ||
            command == TribeAICommand.AttackBuilding ||
            command == TribeAICommand.ForceAttackBuilding;

        private static bool IsBuildingAttackCommand(TribeAICommand command) =>
            command == TribeAICommand.AttackBuilding ||
            command == TribeAICommand.ForceAttackBuilding;

        // Mirrors the per-unit switch in Vanilla's DigMoatTileId (command 6) handler.
        // There is no generic capability field in that command path.
        private static bool CanDigMoat(GameUnit* unit) =>
            unit != null && CanDigMoat(unit->r_UnitChimp);

        private static bool CanDigMoat(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_SPEARMAN:
                case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN:
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                    return true;
                default:
                    return false;
            }
        }

        private bool TryGetSelectedVanillaDigger(
            int preferredUnitId,
            int expectedPlayerId,
            out int unitId,
            out GameUnit* unit)
        {
            unitId = 0;
            unit = null;
            if (preferredUnitId > 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(preferredUnitId, out GameUnit* preferred) &&
                preferred != null && preferred->r_AliveState == AliveState.IsAlive &&
                (expectedPlayerId < 0 || preferred->r_ControllableForPlayerId == expectedPlayerId) &&
                (preferred->r_UnitSelected != 0 || preferred->r_UnitSelected2 != 0) &&
                CanDigMoat(preferred))
            {
                unitId = preferredUnitId;
                unit = preferred;
                return true;
            }

            int[] selectedUnitIds = GamePlayerManagerAPI.Instance.GetSelectedChimps();
            for (int index = 0; index < selectedUnitIds.Length; index++)
            {
                int selectedUnitId = selectedUnitIds[index];
                if (selectedUnitId <= 0 ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(selectedUnitId, out GameUnit* selected) ||
                    selected == null || selected->r_AliveState != AliveState.IsAlive ||
                    (expectedPlayerId >= 0 && selected->r_ControllableForPlayerId != expectedPlayerId) ||
                    !CanDigMoat(selected))
                {
                    continue;
                }

                unitId = selectedUnitId;
                unit = selected;
                return true;
            }

            return false;
        }

        private void LogDiggerDecision(
            string source, int unitId, GameUnit* unit, int targetX, int targetY,
            bool accepted, bool friendlyMoatRequired = false)
        {
            if (unit == null)
                return;
            string key = $"{mapEpoch}:{source}:{unit->r_UnitChimp}:{unit->r_AI_LastIssuedTribeCommand}:" +
                $"{targetX}:{targetY}:{accepted}:{friendlyMoatRequired}";
            if (!loggedDiggerDecisions.Add(key))
                return;
            Shared.DebugLogHelper.LogInfo(log,
                $"MoveMoat stage=vanilla-digger source={source} unit={unitId} " +
                $"type={unit->r_UnitChimp} command=" +
                $"{(TribeAICommand)unit->r_AI_LastIssuedTribeCommand} " +
                $"target=({targetX},{targetY}) accepted={accepted} " +
                $"friendlyMoatRequired={friendlyMoatRequired}.");
        }

        private int RunCentralMovementPlanWithContext(
            IntPtr unitManager, int unitId, int targetX, int targetY)
        {
            if (disposed || unitManager == IntPtr.Zero || unitId <= 0)
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);

            MarkTrackedAttackPipeline(unitId, AttackPipelineStage.Planner, targetX, targetY, false);
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* plannerUnit) ||
                plannerUnit == null || !CanDigMoat(plannerUnit))
            {
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
            }

            PlanScope previous = activePlan;
            PlanScope plan = previous != null && previous.PostCombatRepath &&
                previous.UnitId == unitId && previous.TargetX == targetX &&
                previous.TargetY == targetY
                    ? previous
                    : new PlanScope(unitId, targetX, targetY);
            if (activeMoveCommand != null)
                activeMoveCommand.CentralPlannerCalls++;
            if (activeMoveCommand == null && !plan.FriendlyRouteQualified &&
                !plan.OwnerRouteProbeCompleted)
            {
                try
                {
                    if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        plan, out RouteProbeSummary summary))
                    {
                        LogRejectedPlannerRoute(plan, summary);
                        return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
                    }

                    plan.FriendlyRouteQualified = true;
                    try
                    {
                        LogPipelineDiagnostic(
                            $"stage=planner-owner-qualified unit={unitId} player={plan.PlayerId} " +
                            $"target=({targetX},{targetY}) {summary.ToLogFields()}");
                    }
                    catch
                    {
                        // Diagnostics must not reject an otherwise qualified planner scope.
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        LogFailure("planner-owner-qualification", ex);
                    }
                    catch
                    {
                        // Never let diagnostics escape across the native planner callback.
                    }
                    return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
                }
            }

            activePlan = plan;
            try
            {
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
            }
            finally
            {
                activePlan = previous;
                if (ReferenceEquals(pendingPlan, plan))
                    pendingPlan = null;
            }
        }

        private void ResumeMovementAfterCombatWithMoatContext(
            IntPtr unitManager, int unitId)
        {
            GameUnit* unit = null;
            try
            {
                if (disposed || unitManager == IntPtr.Zero || unitId <= 0 ||
                    unitManager != (IntPtr)nativeUnitManager ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    !CanDigMoat(unit) ||
                    *(short*)((byte*)unit + UnitCombatFinishGateOffset) != 0 ||
                    *(short*)((byte*)unit + UnitGroupInactiveStateOffset) != 0 ||
                    *(short*)((byte*)unit + UnitPostCombatMovementStateOffset) == 3)
                {
                    originalCombatFinishResume(unitManager, unitId);
                    return;
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("post-combat-context", ex);
                originalCombatFinishResume(unitManager, unitId);
                return;
            }

            int targetX = unit->r_TargetTilePositionX2;
            int targetY = unit->r_TargetTilePositionY2;
            if ((uint)targetX >= MapWidth || (uint)targetY >= MapWidth ||
                (targetX == unit->r_CurrentTilePositionX &&
                 targetY == unit->r_CurrentTilePositionY))
            {
                originalCombatFinishResume(unitManager, unitId);
                return;
            }

            var plan = new PlanScope(unitId, targetX, targetY)
            {
                PlayerId = unit->r_ControllableForPlayerId,
                PostCombatRepath = true
            };
            RouteProbeSummary requiredRouteSummary = default;
            bool requiredFriendlyMoat = false;
            try
            {
                requiredFriendlyMoat =
                    TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        plan, out requiredRouteSummary);
                plan.OwnerRouteProbeCompleted = true;
                plan.FriendlyRouteQualified = requiredFriendlyMoat;
            }
            catch (Exception ex)
            {
                // A failed owner probe must never suppress Vanilla's post-combat resume.
                TryLogDiagnosticFailure("post-combat-owner-probe", ex);
            }

            PlanScope previousActivePlan = activePlan;
            PlanScope previousPendingPlan = pendingPlan;
            activePlan = plan;
            pendingPlan = plan;
            try
            {
                MarkPostCombatRepathEntered(unitId, unit, plan, requiredFriendlyMoat);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=post-combat-repath-entered unit={unitId} " +
                    $"type={unit->r_UnitChimp} player={unit->r_ControllableForPlayerId} " +
                    $"tribe={unit->r_TribeId} aiState={unit->r_AIState} " +
                    $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                    $"savedTarget=({targetX},{targetY}) " +
                    $"requiredFriendlyMoat={requiredFriendlyMoat} " +
                    $"routeProbe={requiredRouteSummary.ToLogFields()}.");
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("post-combat-enter-diagnostic", ex);
            }

            try
            {
                // Vanilla restores the saved state and calls MoveHere itself. Keeping this
                // scope alive around that call supports both required and merely faster
                // friendly-moat routes without blocking an otherwise valid ground route.
                originalCombatFinishResume(unitManager, unitId);

                try
                {
                    // If the original tracker no longer existed, the synchronous builder may
                    // have created it during the repath. Attach the same continuation marker.
                    MarkPostCombatRepathEntered(
                        unitId, unit, plan, requiredFriendlyMoat);
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=post-combat-repath-result unit={unitId} " +
                        $"target=({targetX},{targetY}) modeObserved={plan.ModeObserved} " +
                        $"friendlyRouteQualified={plan.FriendlyRouteQualified} " +
                        $"path={unit->p_CurrentPathPlanPosition}/{unit->p_PathPlanSize} " +
                        $"currentTarget=({unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}).");
                }
                catch (Exception ex)
                {
                    TryLogDiagnosticFailure("post-combat-result-diagnostic", ex);
                }
            }
            finally
            {
                activePlan = previousActivePlan;
                pendingPlan = previousPendingPlan;
            }
        }

        private int EnableCompletedMoatModeForScopedMovement(IntPtr unitManager, int unitId)
        {
            int vanillaResult = originalUnitStandingOnCompletedMoat(unitManager, unitId);
            PlanScope plan = activePlan;
            bool plannerQualified = plan != null && plan.FriendlyRouteQualified;
            if (disposed || unitManager == IntPtr.Zero || unitId <= 0)
                return vanillaResult;

            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null)
                    return vanillaResult;
                if (!CanDigMoat(unit))
                {
                    return vanillaResult;
                }

                if (!plannerQualified && activeMoveCommand != null)
                {
                    PlanScope movePlan = plan;
                    if (movePlan == null || movePlan.UnitId != unitId ||
                        movePlan.TargetX != activeMoveCommand.TargetX ||
                        movePlan.TargetY != activeMoveCommand.TargetY)
                    {
                        movePlan = new PlanScope(
                            unitId,
                            activeMoveCommand.TargetX,
                            activeMoveCommand.TargetY);
                    }

                    if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        movePlan, out RouteProbeSummary moveSummary))
                    {
                        try
                        {
                            LogPipelineDiagnostic(
                                $"stage=mode-move-rejected unit={unitId} " +
                                $"target=({movePlan.TargetX},{movePlan.TargetY}) " +
                                $"vanilla={vanillaResult} reason=no-required-friendly-moat-route " +
                                moveSummary.ToLogFields());
                        }
                        catch
                        {
                            // A failed qualification must remain Vanilla even if logging fails.
                        }
                        return vanillaResult;
                    }

                    movePlan.FriendlyRouteQualified = true;
                    plan = movePlan;
                    plannerQualified = true;
                    pendingPlan = movePlan;
                    try
                    {
                        LogPipelineDiagnostic(
                            $"stage=mode-move-owner-qualified unit={unitId} player={movePlan.PlayerId} " +
                            $"target=({movePlan.TargetX},{movePlan.TargetY}) " +
                            moveSummary.ToLogFields());
                    }
                    catch
                    {
                        // Qualification remains valid even if diagnostics fail.
                    }
                }

                if (!plannerQualified)
                {
                    if (!TryQualifyAttackMovementPlan(
                        unitId, unit, vanillaResult, out plan, out RouteProbeSummary attackSummary,
                        out string rejectionReason))
                    {
                        if (activeAttackCommand != null || IsAttackCommand(
                            (TribeAICommand)unit->r_AI_LastIssuedTribeCommand))
                        {
                            try
                            {
                                LogAttackScopeDecision(
                                    "attack-scope-rejected", unitId, unit, vanillaResult,
                                    rejectionReason, attackSummary);
                            }
                            catch
                            {
                                // Rejection diagnostics must not affect Vanilla behavior.
                            }
                        }
                        LogUnscopedAttackMode(unitId, unit, vanillaResult);
                        return vanillaResult;
                    }

                    plannerQualified = true;
                    pendingPlan = plan;
                    try
                    {
                        LogAttackScopeDecision(
                            "attack-scope-qualified", unitId, unit, vanillaResult,
                            rejectionReason, attackSummary);
                    }
                    catch
                    {
                        // Qualification remains valid even if diagnostics fail.
                    }
                }

                MarkTrackedAttackPipeline(
                    unitId, AttackPipelineStage.Mode, -1, -1, vanillaResult != 0);

                if (plan == null)
                    return vanillaResult;
                plan.ModeObserved = true;
                plan.VanillaModeDetected = vanillaResult != 0;
                plan.PlayerId = unit->r_ControllableForPlayerId;
                if (activeMoveCommand != null)
                    activeMoveCommand.ModeCalls++;
                try
                {
                    LogModeContext(plan, unit, vanillaResult);
                }
                catch
                {
                    // Context logging must not change the native mode decision.
                }
                if (vanillaResult == 0)
                    LogMovementContext($"stage=mode unit={unitId} vanilla=0 effective=1");
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("mode", ex);
                return vanillaResult;
            }
        }

        private void ResolveCommandDiagnosticContext(
            int unitId,
            GameUnit* unit,
            out TribeAICommand command,
            out string commandContext,
            out int commandSequence)
        {
            if (activePlan != null && activePlan.PostCombatRepath &&
                activePlan.UnitId == unitId)
            {
                if (trackedMoatMoves.TryGetValue(unitId, out MoatMoveTracker tracker) &&
                    tracker.MapEpoch == mapEpoch &&
                    tracker.TargetX == activePlan.TargetX &&
                    tracker.TargetY == activePlan.TargetY)
                {
                    command = tracker.WeightedCommand;
                    commandSequence = tracker.WeightedCommandSequence;
                }
                else
                {
                    command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
                    commandSequence = 0;
                }
                commandContext = "post-combat-resume";
                return;
            }

            if (activeAttackCommand != null &&
                activeAttackCommand.TribeId == unit->r_TribeId &&
                activeAttackCommand.CandidateUnitIds.Contains(unitId))
            {
                command = activeAttackCommand.Command;
                commandContext = $"target-order-{activeAttackCommand.Command}";
                commandSequence = activeAttackCommand.Sequence;
                return;
            }

            if (activeMoveCommand != null && activeMoveCommand.TribeId == unit->r_TribeId)
            {
                command = TribeAICommand.MoveHerePosition;
                string orderKind = activeMoveCommand.IsPatrolPath ? "patrol-leg" : "move-order";
                string orderPhase = activeMoveCommand.IsNewOrder ? "new" : "continuation";
                commandContext = $"{orderKind}-{orderPhase}-{activeMoveCommand.MoveType}";
                commandSequence = activeMoveCommand.Sequence;
                return;
            }

            command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
            commandContext = $"unit-state-{command}";
            commandSequence = 0;
        }

        private void CaptureMoveCommandGroupSummary(MoveCommandScope command)
        {
            if (command == null ||
                !TryCaptureOrderedActiveGroupUnits(
                    nativeTribeManager, command.TribeId, out int[] unitIds))
            {
                return;
            }

            foreach (int unitId in unitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                command.ActiveUnitsAtDispatch++;
                if (CanDigMoat(unit))
                    command.DiggersAtDispatch++;
                if (IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)))
                    command.UnitsOnMoatAtDispatch++;
                int playerId = unit->r_ControllableForPlayerId;
                if ((uint)playerId < 32)
                    command.PlayerMaskAtDispatch |= 1u << playerId;
            }
        }

        private bool TryCaptureWeightedMovementCostProfile(
            GameUnit* unit,
            out WeightedMovementCostProfile profile,
            out string rejectionReason)
        {
            profile = default;
            rejectionReason = "invalid-unit";
            if (unit == null)
                return false;

            int tileId = GameTileManagerAPI.Instance.GetTileId(
                unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
            if (!IsValidTileId(tileId))
            {
                rejectionReason = "invalid-speed-snapshot-tile";
                return false;
            }

            int currentSpeed = unchecked((short)unit->r_CurrentSpeed);
            int currentSpeed2 = unchecked((short)unit->r_CurrentSpeed2);
            int speedBonus = unchecked((short)unit->r_SpeedBonus);
            int additionalSubsteps = *(short*)((byte*)unit + UnitAdditionalMovementSubstepsOffset);
            int extraDelay = unchecked((short)unit->r_PathPlanRelated1);
            int moatPhase = *((byte*)unit + UnitMoatSlowdownPhaseOffset);
            bool completedMoat = IsCompletedMoatTile(tileId);
            bool valid = WeightedMovementCostProfile.TryCreate(
                currentSpeed,
                currentSpeed2,
                speedBonus,
                additionalSubsteps,
                extraDelay,
                moatPhase,
                completedMoat,
                out profile,
                out rejectionReason);
            if (valid && !completedMoat && moatPhase == 0 &&
                currentSpeed2 - currentSpeed == 3)
            {
                // 0x19B260 keeps the +3 moat-exit delay for the update in which the
                // phase has already decayed to zero. A single snapshot cannot safely
                // distinguish that residual from another transient +3 adjustment.
                rejectionReason = "ambiguous-moat-exit-residual";
                return false;
            }
            if (valid && !completedMoat && moatPhase != 0 &&
                (tileFlags[tileId] & AlternativeTerrainDelayTileFlag) != 0)
            {
                // 0x19B260 uses a separate +2 branch here instead of the +3 moat
                // decay. The two contributions cannot be separated from this snapshot.
                rejectionReason = "ambiguous-terrain-and-moat-delay";
                return false;
            }
            return valid;
        }

        private bool IsPublishedWalkableBuildingApproach(int unitId, int targetTileId)
        {
            if (!IsValidTileId(targetTileId) || !IsWalkableBuildingApproachEndpoint(targetTileId))
                return false;
            AttackCommandScope command = activeAttackCommand;
            if (command == null || !IsBuildingAttackCommand(command.Command) ||
                !command.CandidateUnitIds.Contains(unitId))
                return false;
            return command.PublishedBuildingApproaches.ContainsKey(targetTileId);
        }

        private bool IsFriendlyCompletedMoatForWeightedShadow(int playerId, int tileId)
        {
            if (!IsCompletedMoatTile(tileId))
                return false;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(playerId))
                return false;

            int moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (moatId <= 0 || moatId >= moatCount)
                return false;
            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            int ownerId = moatRecord[MoatOwnerOffset];
            return playerApi.IsPlayerIdValid(ownerId) &&
                (ownerId == playerId || playerApi.IsPlayerAlliedTo(playerId, ownerId));
        }

        private bool IsIsolatedActiveGroupUnit(int unitId, int tribeId)
        {
            if (tribeId < 0 || tribeId >= MaximumTribeCount || getGroupUnitId == null)
                return false;
            byte* tribeRecord = (byte*)nativeTribeManager.ToPointer() +
                tribeId * TribeRecordSize;
            return *(short*)(tribeRecord + TribeUnitCountOffset) == 1 &&
                getGroupUnitId(nativeTribeManager, tribeId, 0) == unitId;
        }

        private BuilderWeightedScope TryCaptureBuilderWeightedScope(IntPtr pathManager)
        {
            try
            {
                if (disposed || weightedShadowBusy || pathManager == IntPtr.Zero ||
                    pathManager != nativePathManager || nativeUnitManager == null)
                    return null;

                byte* manager = (byte*)pathManager.ToPointer();
                byte* nativePath = *(byte**)(manager + PathManagerOutputBufferOffset);
                byte* firstUnitPath = nativeUnitManager + NativeUnitPathBufferOffset;
                if (nativePath == null || nativePath < firstUnitPath)
                    return null;

                long pathOffset = nativePath - firstUnitPath;
                if (pathOffset <= 0 || pathOffset % NativeUnitPathBufferStride != 0)
                    return null;
                long unitId64 = pathOffset / NativeUnitPathBufferStride;
                if (unitId64 <= 0 || unitId64 > MaximumUnitCount)
                    return null;
                int unitId = (int)unitId64;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive || !CanDigMoat(unit))
                    return null;

                int startX = *(int*)(manager + 0x08);
                int startY = *(int*)(manager + 0x0C);
                int targetX = *(int*)(manager + 0x10);
                int targetY = *(int*)(manager + 0x14);
                if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth ||
                    targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth ||
                    (startX == targetX && startY == targetY))
                    return null;

                // Exact 0x196280 start selection: unrelated shared-builder calls fail closed.
                bool vanillaUsesCurrentTile = unit->r_PathPlanStateBitFlags == 0 &&
                    unit->r_MovingRelevant == 8;
                int expectedStartX = vanillaUsesCurrentTile
                    ? unit->r_CurrentTilePositionX
                    : unit->r_NextTilePositionX2;
                int expectedStartY = vanillaUsesCurrentTile
                    ? unit->r_CurrentTilePositionY
                    : unit->r_NextTilePositionY2;
                if (startX != expectedStartX || startY != expectedStartY)
                    return null;

                int playerId = unit->r_ControllableForPlayerId;
                string profileRejection = null;
                WeightedMovementCostProfile costProfile = default;
                bool validPlayer = GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId);
                bool validProfile = validPlayer && TryCaptureWeightedMovementCostProfile(
                    unit, out costProfile, out profileRejection);
                if (!validProfile)
                {
                    uint rejectedCommand = unchecked((uint)unit->r_AI_LastIssuedTribeCommand);
                    if (rejectedCommand == (uint)TribeAICommand.DigMoatTileId ||
                        rejectedCommand == (uint)TribeAICommand.Unknown7)
                    {
                        LogWeightedPublicationDecision(
                            unitId,
                            $"MoveMoat stage=weighted-builder-capture-rejected " +
                            $"captureSource=unit-builder unit={unitId} " +
                            $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand} " +
                            $"reason={profileRejection ?? "invalid-player"}.");
                    }
                    return null;
                }

                int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
                bool allowReservedTarget = IsPublishedWalkableBuildingApproach(unitId, targetTileId);
                ResolveCommandDiagnosticContext(
                    unitId, unit, out TribeAICommand command, out string commandContext,
                    out int commandSequence);
                uint rawCommand = unchecked((uint)unit->r_AI_LastIssuedTribeCommand);
                string workKind = rawCommand == (uint)TribeAICommand.DigMoatTileId
                    ? "dig-moat-work"
                    : rawCommand == (uint)TribeAICommand.Unknown7
                        ? "fill-moat-work"
                        : "not-moat-work";
                string workPhase = rawCommand == (uint)TribeAICommand.DigMoatTileId ||
                    rawCommand == (uint)TribeAICommand.Unknown7
                    ? activeMoveCommand?.IsNewOrder == true
                        ? "initial-command"
                        : "automatic-follow-up"
                    : "not-applicable";
                bool calibratable = (activePlan != null && activePlan.UnitId == unitId &&
                    activePlan.PostCombatRepath) ||
                    IsIsolatedActiveGroupUnit(unitId, unit->r_TribeId);
                return new BuilderWeightedScope(
                    mapEpoch,
                    unitId,
                    unit->r_UnitChimp,
                    playerId,
                    unit->r_TribeId,
                    command,
                    commandContext,
                    commandSequence,
                    startX,
                    startY,
                    targetX,
                    targetY,
                    unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY,
                    unchecked((uint)unit->r_AIState),
                    rawCommand,
                    workKind,
                    workPhase,
                    costProfile,
                    allowReservedTarget,
                    calibratable);
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("weighted-builder-capture", ex);
                return null;
            }
        }

        private bool ObserveWeightedMoatShadowResult(
            IntPtr pathManager, int builderResult, BuilderWeightedScope shadow)
        {
            try
            {
                if (shadow.MapEpoch != mapEpoch || pathManager == IntPtr.Zero ||
                    pathManager != nativePathManager)
                {
                    return false;
                }

                bool snapshotAvailable = GameUnitManagerAPI.Instance.TryGetUnitById(
                    shadow.UnitId, out GameUnit* snapshotUnit) && snapshotUnit != null &&
                    snapshotUnit->r_AliveState == AliveState.IsAlive;
                bool identityMatches = snapshotAvailable && CanDigMoat(snapshotUnit) &&
                    snapshotUnit->r_UnitChimp == shadow.UnitType &&
                    snapshotUnit->r_ControllableForPlayerId == shadow.PlayerId &&
                    snapshotUnit->r_TribeId == shadow.TribeId;
                bool snapshotPositionMatches = identityMatches &&
                    snapshotUnit->r_CurrentTilePositionX == shadow.SnapshotCurrentX &&
                    snapshotUnit->r_CurrentTilePositionY == shadow.SnapshotCurrentY;
                string currentProfileRejection = null;
                WeightedMovementCostProfile currentCostProfile = default;
                bool currentProfileValid = snapshotPositionMatches &&
                    TryCaptureWeightedMovementCostProfile(
                        snapshotUnit, out currentCostProfile, out currentProfileRejection);
                if (!currentProfileValid || !currentCostProfile.Equals(shadow.CostProfile))
                {
                    LogWeightedShadowDecision(
                        shadow, builderResult, default, false, false,
                        "no-valid-shadow-route",
                        !snapshotAvailable ? "runtime-unit-unavailable" :
                        !identityMatches ? "runtime-unit-identity-changed" :
                        !snapshotPositionMatches ? "runtime-position-changed" :
                        currentProfileRejection ?? "runtime-speed-snapshot-changed");
                    return true;
                }

                byte* manager = (byte*)pathManager.ToPointer();
                int builderStartX = *(int*)(manager + 0x08);
                int builderStartY = *(int*)(manager + 0x0C);
                int builderTargetX = *(int*)(manager + 0x10);
                int builderTargetY = *(int*)(manager + 0x14);
                int nativeLength = *(int*)(manager + PathManagerOutputLengthOffset);
                byte* nativePath = *(byte**)(manager + PathManagerOutputBufferOffset);
                byte* expectedPath = nativeUnitManager + NativeUnitPathBufferOffset +
                    shadow.UnitId * NativeUnitPathBufferStride;
                bool publishedToUnit = nativePath == expectedPath;
                bool vanillaStillUsesCurrentTile =
                    snapshotUnit->r_PathPlanStateBitFlags == 0 &&
                    snapshotUnit->r_MovingRelevant == 8;
                int revalidatedStartX = vanillaStillUsesCurrentTile
                    ? snapshotUnit->r_CurrentTilePositionX
                    : snapshotUnit->r_NextTilePositionX2;
                int revalidatedStartY = vanillaStillUsesCurrentTile
                    ? snapshotUnit->r_CurrentTilePositionY
                    : snapshotUnit->r_NextTilePositionY2;
                if (!publishedToUnit || builderStartX != shadow.StartX ||
                    builderStartY != shadow.StartY || builderTargetX != shadow.TargetX ||
                    builderTargetY != shadow.TargetY || revalidatedStartX != shadow.StartX ||
                    revalidatedStartY != shadow.StartY)
                {
                    LogWeightedShadowDecision(
                        shadow, builderResult, default, false, publishedToUnit,
                        "no-valid-shadow-route", "builder-contract-changed-during-call");
                    return false;
                }

                WeightedMoatRouteSummary nativeSummary = default;
                bool nativeValid = false;
                if (!weightedShadowBusy && builderResult > 0 && nativeLength == builderResult &&
                    nativeLength <= WeightedMoatRoutePlanner.MaximumRouteEdges &&
                    nativePath != null)
                {
                    weightedShadowBusy = true;
                    try
                    {
                        nativeValid = weightedMoatRoutePlanner.TryDescribeEncodedPath(
                            shadow.PlayerId,
                            shadow.StartX,
                            shadow.StartY,
                            shadow.TargetX,
                            shadow.TargetY,
                            shadow.CostProfile,
                            nativePath,
                            nativeLength,
                            shadow.AllowReservedTarget,
                            out nativeSummary);
                    }
                    finally
                    {
                        weightedShadowBusy = false;
                    }
                }

                if (!nativeValid)
                {
                    if (shadow.WorkKind != "not-moat-work")
                    {
                        LogWeightedShadowDecision(
                            shadow, builderResult, nativeSummary, false, publishedToUnit,
                            "no-valid-shadow-route",
                            builderResult <= 0 ? "builder-did-not-produce-positive-path" :
                            "native-path-decode-failed");
                    }
                    return true;
                }

                int minimumEdges = Math.Max(
                    Math.Abs(shadow.TargetX - shadow.StartX),
                    Math.Abs(shadow.TargetY - shadow.StartY));
                shadow.OptimisticLowerBoundTicks = shadow.CostProfile.EstimateRouteTicks(
                    minimumEdges, 0);
                bool couldMeetMargin = shadow.OptimisticLowerBoundTicks != long.MaxValue &&
                    nativeSummary.EstimatedTicks >= shadow.OptimisticLowerBoundTicks &&
                    nativeSummary.EstimatedTicks - shadow.OptimisticLowerBoundTicks >=
                        WeightedPublicationSafetyMarginTicks;
                if (couldMeetMargin)
                {
                    weightedShadowBusy = true;
                    try
                    {
                        shadow.CandidateFound = weightedMoatRoutePlanner.TryBuildEncoded(
                            shadow.PlayerId,
                            shadow.StartX,
                            shadow.StartY,
                            shadow.TargetX,
                            shadow.TargetY,
                            shadow.CostProfile,
                            shadow.AllowReservedTarget,
                            out WeightedMoatRouteSummary candidate,
                            out WeightedMoatEncodedRoute encodedCandidate);
                        shadow.Candidate = candidate;
                        shadow.CandidateRoute = encodedCandidate;
                        shadow.AccumulatedSearchMilliseconds += candidate.SearchMilliseconds;
                        shadow.SearchPasses++;
                    }
                    finally
                    {
                        weightedShadowBusy = false;
                    }
                }
                else
                {
                    shadow.CandidateFound = false;
                    shadow.Candidate = WeightedMoatRouteSummary.Failed(
                        "optimistic-lower-bound-below-margin", 0);
                }

                string decision;
                string reason;
                if (!shadow.CandidateFound)
                {
                    decision = "no-valid-shadow-route";
                    reason = shadow.Candidate.Reason;
                }
                else if (!nativeValid)
                {
                    decision = builderResult <= 0 && shadow.Candidate.MoatEdges > 0
                        ? "shadow-friendly-moat"
                        : "no-valid-shadow-route";
                    reason = builderResult > 0
                        ? "native-path-decode-failed"
                        : "vanilla-no-path";
                }
                else if (shadow.Candidate.MoatEdges > 0 &&
                    shadow.Candidate.EstimatedTicks < nativeSummary.EstimatedTicks)
                {
                    decision = "shadow-friendly-moat";
                    reason = "weighted-shadow-faster";
                }
                else
                {
                    decision = nativeSummary.MoatEdges > 0
                        ? "native-friendly-moat"
                        : "native-ground";
                    reason = shadow.Candidate.MoatEdges == 0
                        ? "weighted-ground-winner"
                        : "native-not-slower";
                }

                int effectiveBuilderResult = builderResult;
                if (couldMeetMargin && builderResult > 0 && nativeValid && publishedToUnit &&
                    TryPublishConservativelyFasterWeightedRoute(
                        pathManager,
                        nativePath,
                        nativeLength,
                        shadow,
                        out WeightedMoatRouteSummary publishedSummary,
                        out long guaranteedSaving,
                        out string cadenceProfiles,
                        out string publicationDetails))
                {
                    effectiveBuilderResult = shadow.PublishedBuilderResult;
                    shadow.CandidateFound = true;
                    shadow.Candidate = publishedSummary;
                    decision = "weighted-path-published";
                    reason = "faster-by-conservative-margin";
                    int consumerModeBefore = *moatPathMode;
                    // 0x196280 persists this global into unit+0x9C8 immediately after
                    // 0xF4930 returns, then clears it. Without that marker 0x1855A0/
                    // 0xDCE60 reject the first moat edge and rebuild the ground detour.
                    *moatPathMode = 1;
                    LogWeightedPublicationDecision(
                        shadow.UnitId,
                        $"MoveMoat stage=weighted-path-published captureSource={shadow.CaptureSource} " +
                        $"work={shadow.WorkKind} workPhase={shadow.WorkPhase} " +
                        $"unit={shadow.UnitId} aiState={shadow.AiState} " +
                        $"type={shadow.UnitType} commandSeq={shadow.CommandSequence} " +
                        $"command={shadow.Command} " +
                        $"start=({shadow.StartX},{shadow.StartY}) " +
                        $"target=({shadow.TargetX},{shadow.TargetY}) " +
                        $"commandContext={shadow.CommandContext} handlerProfiles={cadenceProfiles} " +
                        $"length={publishedSummary.RouteLength} ground={publishedSummary.GroundEdges} " +
                        $"moat={publishedSummary.MoatEdges} diagonal={publishedSummary.DiagonalEdges} " +
                        $"fingerprint=0x{publishedSummary.RouteFingerprint:X16} " +
                        $"guaranteedSavingTicks={guaranteedSaving} " +
                        $"profileCosts={publicationDetails} " +
                        $"searchMsTotal={shadow.AccumulatedSearchMilliseconds:F3} " +
                        $"searchPasses={shadow.SearchPasses} " +
                        $"roundtrip=True pathBuffer=unit " +
                        $"consumerMode={consumerModeBefore}->1 " +
                        "persistentUnitMode=deferred-to-0x196280.");
                }

                bool moatRelevantDiagnostic = shadow.WorkKind != "not-moat-work" ||
                    nativeSummary.MoatEdges > 0 ||
                    (shadow.CandidateFound && shadow.Candidate.MoatEdges > 0);
                if (moatRelevantDiagnostic)
                {
                    LogWeightedShadowDecision(
                        shadow, effectiveBuilderResult, nativeSummary, nativeValid,
                        publishedToUnit, decision, reason);
                }
                if (moatRelevantDiagnostic && effectiveBuilderResult > 0 && publishedToUnit)
                {
                    StartOrRefreshWeightedShadowTracker(
                        shadow, effectiveBuilderResult, nativeValid ? nativeSummary : default,
                        nativeValid, decision);
                }
                return true;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("weighted-shadow-result", ex);
                return true;
            }
        }

        private bool TryPublishConservativelyFasterWeightedRoute(
            IntPtr pathManager,
            byte* nativePath,
            int nativeLength,
            BuilderWeightedScope shadow,
            out WeightedMoatRouteSummary publishedSummary,
            out long guaranteedSaving,
            out string cadenceProfiles,
            out string rejectionReason)
        {
            publishedSummary = default;
            guaranteedSaving = long.MinValue;
            cadenceProfiles = "none";
            rejectionReason = "publication-not-evaluated";
            // 0x196280 registers exactly one real unit buffer before each 0xF4930 call.
            // The builder-local scope therefore covers formation followers and internal
            // worker follow-up targets without depending on the command that led here.
            if (weightedShadowBusy || pathManager == IntPtr.Zero || nativePath == null ||
                nativeLength <= 0 || nativeLength > WeightedMoatRoutePlanner.MaximumRouteEdges)
            {
                rejectionReason = "invalid-publication-buffer";
                return false;
            }

            if (!nativeMovementCadenceResolver.TryGetPlausibleSpeedBonuses(
                    (int)shadow.UnitType,
                    shadow.CostProfile.SpeedBonus,
                    out int[] speedBonuses,
                    out ulong handlerRva,
                    out rejectionReason))
            {
                if (shadow.WorkKind != "not-moat-work")
                {
                    LogWeightedPublicationDecision(
                        shadow.UnitId,
                        $"MoveMoat stage=weighted-path-not-published unit={shadow.UnitId} " +
                        $"captureSource={shadow.CaptureSource} work={shadow.WorkKind} " +
                        $"workPhase={shadow.WorkPhase} type={shadow.UnitType} " +
                        $"start=({shadow.StartX},{shadow.StartY}) " +
                        $"target=({shadow.TargetX},{shadow.TargetY}) reason={rejectionReason}.");
                }
                return false;
            }

            cadenceProfiles = $"rva-0x{handlerRva:X}:bonus-[{string.Join(",", speedBonuses)}]";
            var profiles = new List<WeightedMovementCostProfile>(speedBonuses.Length);
            foreach (int speedBonus in speedBonuses)
            {
                if (!shadow.CostProfile.TryWithSpeedBonus(
                        speedBonus, out WeightedMovementCostProfile profile,
                        out rejectionReason))
                {
                    return false;
                }
                profiles.Add(profile);
            }

            var candidates = new List<WeightedPublicationCandidate>(profiles.Count);
            weightedShadowBusy = true;
            try
            {
                foreach (WeightedMovementCostProfile profile in profiles)
                {
                    WeightedMoatRouteSummary candidateSummary;
                    WeightedMoatEncodedRoute encodedRoute;
                    bool candidateFound;
                    if (profile.Equals(shadow.CostProfile) && shadow.CandidateRoute.IsValid)
                    {
                        candidateSummary = shadow.Candidate;
                        encodedRoute = shadow.CandidateRoute;
                        candidateFound = shadow.CandidateFound;
                    }
                    else
                    {
                        candidateFound = weightedMoatRoutePlanner.TryBuildEncoded(
                            shadow.PlayerId,
                            shadow.StartX,
                            shadow.StartY,
                            shadow.TargetX,
                            shadow.TargetY,
                            profile,
                            shadow.AllowReservedTarget,
                            out candidateSummary,
                            out encodedRoute);
                        shadow.AccumulatedSearchMilliseconds += candidateSummary.SearchMilliseconds;
                        shadow.SearchPasses++;
                    }
                    if (!candidateFound ||
                        !encodedRoute.IsValid || candidateSummary.MoatEdges <= 0)
                    {
                        continue;
                    }

                    bool duplicate = false;
                    foreach (WeightedPublicationCandidate existing in candidates)
                    {
                        if (existing.Summary.RouteLength == candidateSummary.RouteLength &&
                            existing.Summary.RouteFingerprint == candidateSummary.RouteFingerprint &&
                            RoutesEqual(existing.Route, encodedRoute))
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (!duplicate)
                        candidates.Add(new WeightedPublicationCandidate(encodedRoute, candidateSummary));
                }

                WeightedPublicationCandidate winner = null;
                foreach (WeightedPublicationCandidate candidate in candidates)
                {
                    long minimumSaving = long.MaxValue;
                    long maximumCandidateTicks = 0;
                    bool validForEveryProfile = true;
                    fixed (byte* candidatePath = candidate.Route.Bytes)
                    {
                        foreach (WeightedMovementCostProfile profile in profiles)
                        {
                            if (!weightedMoatRoutePlanner.TryDescribeEncodedPath(
                                    shadow.PlayerId,
                                    shadow.StartX,
                                    shadow.StartY,
                                    shadow.TargetX,
                                    shadow.TargetY,
                                    profile,
                                    nativePath,
                                    nativeLength,
                                    shadow.AllowReservedTarget,
                                    out WeightedMoatRouteSummary nativeProfileSummary) ||
                                !weightedMoatRoutePlanner.TryDescribeEncodedPath(
                                    shadow.PlayerId,
                                    shadow.StartX,
                                    shadow.StartY,
                                    shadow.TargetX,
                                    shadow.TargetY,
                                    profile,
                                    candidatePath,
                                    candidate.Route.DirectionCount,
                                    shadow.AllowReservedTarget,
                                    out WeightedMoatRouteSummary candidateProfileSummary))
                            {
                                validForEveryProfile = false;
                                break;
                            }

                            long saving = nativeProfileSummary.EstimatedTicks -
                                candidateProfileSummary.EstimatedTicks;
                            candidate.ProfileCosts.Add(
                                $"b{profile.SpeedBonus}:n{nativeProfileSummary.EstimatedTicks}:" +
                                $"c{candidateProfileSummary.EstimatedTicks}:s{saving}");
                            if (saving < WeightedPublicationSafetyMarginTicks)
                            {
                                validForEveryProfile = false;
                                break;
                            }
                            minimumSaving = Math.Min(minimumSaving, saving);
                            maximumCandidateTicks = Math.Max(
                                maximumCandidateTicks, candidateProfileSummary.EstimatedTicks);
                        }
                    }

                    if (!validForEveryProfile)
                        continue;
                    candidate.MinimumSaving = minimumSaving;
                    candidate.MaximumEstimatedTicks = maximumCandidateTicks;
                    if (winner == null || IsBetterPublicationCandidate(candidate, winner))
                        winner = candidate;
                }

                if (winner == null)
                {
                    rejectionReason = candidates.Count == 0
                        ? "no-friendly-moat-candidate"
                        : "not-faster-under-all-cadence-profiles";
                    if (shadow.WorkKind != "not-moat-work" || candidates.Count > 0)
                    {
                        LogWeightedPublicationDecision(
                            shadow.UnitId,
                            $"MoveMoat stage=weighted-path-not-published unit={shadow.UnitId} " +
                            $"captureSource={shadow.CaptureSource} work={shadow.WorkKind} " +
                            $"workPhase={shadow.WorkPhase} type={shadow.UnitType} " +
                            $"start=({shadow.StartX},{shadow.StartY}) " +
                            $"target=({shadow.TargetX},{shadow.TargetY}) " +
                            $"handlerProfiles={cadenceProfiles} " +
                            $"candidates={candidates.Count} reason={rejectionReason}.");
                    }
                    return false;
                }

                byte* manager = (byte*)pathManager.ToPointer();
                int newByteCount = winner.Route.Bytes.Length;
                int oldByteCount = (nativeLength + 1) >> 1;
                int affectedByteCount = Math.Max(newByteCount, oldByteCount);
                if (affectedByteCount > NativeUnitPathBufferStride)
                {
                    rejectionReason = "publication-buffer-overflow";
                    return false;
                }

                byte[] backup = new byte[affectedByteCount];
                Marshal.Copy((IntPtr)nativePath, backup, 0, affectedByteCount);
                int originalLength = *(int*)(manager + PathManagerOutputLengthOffset);
                try
                {
                    for (int index = 0; index < affectedByteCount; index++)
                        nativePath[index] = index < newByteCount ? winner.Route.Bytes[index] : (byte)0;
                    *(int*)(manager + PathManagerOutputLengthOffset) = winner.Route.DirectionCount;

                    if (!weightedMoatRoutePlanner.TryDescribeEncodedPath(
                            shadow.PlayerId,
                            shadow.StartX,
                            shadow.StartY,
                            shadow.TargetX,
                            shadow.TargetY,
                            profiles[0],
                            nativePath,
                            winner.Route.DirectionCount,
                            shadow.AllowReservedTarget,
                            out WeightedMoatRouteSummary roundtrip) ||
                        roundtrip.RouteFingerprint != winner.Summary.RouteFingerprint)
                    {
                        throw new InvalidOperationException(
                            "The published weighted path failed its final roundtrip validation.");
                    }
                    for (int index = 0; index < newByteCount; index++)
                    {
                        if (nativePath[index] != winner.Route.Bytes[index])
                        {
                            throw new InvalidOperationException(
                                "The published weighted path differs from its encoded source.");
                        }
                    }
                }
                catch
                {
                    Marshal.Copy(backup, 0, (IntPtr)nativePath, affectedByteCount);
                    *(int*)(manager + PathManagerOutputLengthOffset) = originalLength;
                    throw;
                }

                shadow.PublishedBuilderResult = winner.Route.DirectionCount;
                publishedSummary = winner.Summary;
                guaranteedSaving = winner.MinimumSaving;
                rejectionReason = string.Join("|", winner.ProfileCosts);
                return true;
            }
            finally
            {
                weightedShadowBusy = false;
            }
        }

        private static bool IsBetterPublicationCandidate(
            WeightedPublicationCandidate candidate,
            WeightedPublicationCandidate current)
        {
            if (candidate.MaximumEstimatedTicks != current.MaximumEstimatedTicks)
                return candidate.MaximumEstimatedTicks < current.MaximumEstimatedTicks;
            if (candidate.Summary.MoatEdges != current.Summary.MoatEdges)
                return candidate.Summary.MoatEdges < current.Summary.MoatEdges;
            if (candidate.Summary.RouteLength != current.Summary.RouteLength)
                return candidate.Summary.RouteLength < current.Summary.RouteLength;
            for (int index = 0; index < candidate.Route.DirectionCount; index++)
            {
                int candidateDirection =
                    (candidate.Route.Bytes[index >> 1] >> ((index & 1) * 4)) & 0x0F;
                int currentDirection =
                    (current.Route.Bytes[index >> 1] >> ((index & 1) * 4)) & 0x0F;
                if (candidateDirection != currentDirection)
                    return candidateDirection < currentDirection;
            }
            return false;
        }

        private static bool RoutesEqual(
            WeightedMoatEncodedRoute left,
            WeightedMoatEncodedRoute right)
        {
            if (left.DirectionCount != right.DirectionCount ||
                left.Bytes == null || right.Bytes == null ||
                left.Bytes.Length != right.Bytes.Length)
            {
                return false;
            }
            for (int index = 0; index < left.Bytes.Length; index++)
            {
                if (left.Bytes[index] != right.Bytes[index])
                    return false;
            }
            return true;
        }

        private void LogWeightedPublicationDecision(int unitId, string message)
        {
            if (lastWeightedPublicationDecisionByUnit.TryGetValue(
                    unitId, out string previous) &&
                string.Equals(previous, message, StringComparison.Ordinal))
            {
                return;
            }
            lastWeightedPublicationDecisionByUnit[unitId] = message;
            Shared.DebugLogHelper.LogInfo(log, message);
        }

        private void LogWeightedShadowDecision(
            BuilderWeightedScope shadow,
            int builderResult,
            WeightedMoatRouteSummary native,
            bool nativeValid,
            bool publishedToUnit,
            string decision,
            string reason)
        {
            long saving = nativeValid && shadow.CandidateFound
                ? native.EstimatedTicks - shadow.Candidate.EstimatedTicks
                : 0;
            string signature = $"{mapEpoch}:{shadow.UnitId}:{(uint)shadow.Command}:" +
                $"{shadow.CommandContext}:{shadow.CommandSequence}:" +
                $"{shadow.RawCommand}:{shadow.WorkKind}:{shadow.WorkPhase}:{shadow.AiState}:" +
                $"{shadow.StartX}:{shadow.StartY}:{shadow.TargetX}:{shadow.TargetY}:" +
                $"{shadow.CostProfile.CurrentSpeed}:{shadow.CostProfile.CurrentSpeed2}:" +
                $"{shadow.CostProfile.SpeedBonus}:{shadow.CostProfile.AdditionalSubsteps}:" +
                $"{shadow.CostProfile.ExtraDelay}:" +
                $"{shadow.CostProfile.MoatPhase}:{shadow.CostProfile.StartedOnCompletedMoat}:" +
                $"{builderResult}:{decision}:{reason}:" +
                $"{shadow.Candidate.RouteLength}:{shadow.Candidate.EstimatedTicks}:" +
                $"{shadow.AccumulatedSearchMilliseconds}:{shadow.SearchPasses}:" +
                $"{native.RouteLength}:{native.EstimatedTicks}:{publishedToUnit}:" +
                $"{shadow.OptimisticLowerBoundTicks}";
            if (lastWeightedShadowDecisionByUnit.TryGetValue(
                    shadow.UnitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }
            lastWeightedShadowDecisionByUnit[shadow.UnitId] = signature;
            RecordWeightedCommandDecision(shadow, decision);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=weighted-shadow captureSource={shadow.CaptureSource} " +
                $"work={shadow.WorkKind} workPhase={shadow.WorkPhase} " +
                $"unit={shadow.UnitId} type={shadow.UnitType} aiState={shadow.AiState} " +
                $"player={shadow.PlayerId} commandSeq={shadow.CommandSequence} " +
                $"command={shadow.Command}({(uint)shadow.Command}) " +
                $"runtimeCommandRaw={shadow.RawCommand} " +
                $"commandContext={shadow.CommandContext} " +
                $"start=({shadow.StartX},{shadow.StartY}) target=({shadow.TargetX},{shadow.TargetY}) " +
                $"currentSpeed={shadow.CostProfile.CurrentSpeed} " +
                $"currentSpeed2={shadow.CostProfile.CurrentSpeed2} " +
                $"speedBonus={shadow.CostProfile.SpeedBonus} " +
                $"additionalSubsteps={shadow.CostProfile.AdditionalSubsteps} " +
                $"extraDelay={shadow.CostProfile.ExtraDelay} " +
                $"moatPhase={shadow.CostProfile.MoatPhase} " +
                $"startOnMoat={shadow.CostProfile.StartedOnCompletedMoat} " +
                $"terrainPenalty={shadow.CostProfile.CurrentTerrainPenalty} " +
                $"normalizedDelay={shadow.CostProfile.NormalizedDelay} " +
                $"cadenceProgress={shadow.CostProfile.CadenceProgress} " +
                $"decision={decision} reason={reason} " +
                $"shadowFound={shadow.CandidateFound} " +
                $"shadowLength={shadow.Candidate.RouteLength} " +
                $"shadowGround={shadow.Candidate.GroundEdges} shadowMoat={shadow.Candidate.MoatEdges} " +
                $"shadowDiagonal={shadow.Candidate.DiagonalEdges} " +
                $"shadowDirectionChanges={shadow.Candidate.DirectionChanges} " +
                $"shadowFingerprint=0x{shadow.Candidate.RouteFingerprint:X16} " +
                $"shadowTicks={shadow.Candidate.EstimatedTicks} savingTicks={saving} " +
                $"nativeValid={nativeValid} nativeLength={native.RouteLength} " +
                $"pathBuffer={(publishedToUnit ? "unit" : "temporary")} " +
                $"nativeGround={native.GroundEdges} nativeMoat={native.MoatEdges} " +
                $"nativeDiagonal={native.DiagonalEdges} nativeTicks={native.EstimatedTicks} " +
                $"optimisticTicks={shadow.OptimisticLowerBoundTicks} " +
                $"nativeDirectionChanges={native.DirectionChanges} " +
                $"nativeFingerprint=0x{native.RouteFingerprint:X16} " +
                $"searchMs={shadow.Candidate.SearchMilliseconds:F3} " +
                $"searchMsTotal={shadow.AccumulatedSearchMilliseconds:F3} " +
                $"searchPasses={shadow.SearchPasses} " +
                $"expanded={shadow.Candidate.ExpandedNodes} abort={shadow.Candidate.Reason}.");
        }

        private void RecordWeightedCommandDecision(BuilderWeightedScope shadow, string decision)
        {
            double searchMilliseconds = shadow.AccumulatedSearchMilliseconds;
            bool published = string.Equals(
                decision, "weighted-path-published", StringComparison.Ordinal);
            if (activeAttackCommand != null &&
                activeAttackCommand.TribeId == shadow.TribeId &&
                activeAttackCommand.CandidateUnitIds.Contains(shadow.UnitId))
            {
                activeAttackCommand.WeightedUnitIds.Add(shadow.UnitId);
                activeAttackCommand.WeightedDecisions++;
                if (published)
                    activeAttackCommand.WeightedPublished++;
                activeAttackCommand.WeightedSearchMilliseconds += searchMilliseconds;
                activeAttackCommand.WeightedMaximumSearchMilliseconds = Math.Max(
                    activeAttackCommand.WeightedMaximumSearchMilliseconds, searchMilliseconds);
                return;
            }

            if (activeMoveCommand != null && activeMoveCommand.TribeId == shadow.TribeId)
            {
                activeMoveCommand.WeightedUnitIds.Add(shadow.UnitId);
                activeMoveCommand.WeightedDecisions++;
                if (published)
                    activeMoveCommand.WeightedPublished++;
                activeMoveCommand.WeightedSearchMilliseconds += searchMilliseconds;
                activeMoveCommand.WeightedMaximumSearchMilliseconds = Math.Max(
                    activeMoveCommand.WeightedMaximumSearchMilliseconds, searchMilliseconds);
            }
        }

        private bool TryQualifyAttackMovementPlan(
            int unitId,
            GameUnit* unit,
            int vanillaResult,
            out PlanScope plan,
            out RouteProbeSummary summary,
            out string rejectionReason)
        {
            plan = null;
            summary = default;
            rejectionReason = "no-active-attack-command";
            AttackCommandScope scope = activeAttackCommand;
            if (scope == null || scope.MapEpoch != mapEpoch || !IsAttackCommand(scope.Command))
                return false;
            if (!CanDigMoat(unit))
            {
                rejectionReason = "unit-cannot-dig-moat";
                return false;
            }
            if (!scope.CandidateUnitIds.Contains(unitId))
            {
                rejectionReason = "unit-not-command-candidate";
                return false;
            }
            try
            {
                LogSynchronousAttackCandidate(scope, unitId, unit);
            }
            catch
            {
                // Candidate diagnostics must not reject an otherwise valid attack scope.
            }
            if (unit->r_AliveState != AliveState.IsAlive || unit->r_TribeId != scope.TribeId)
            {
                rejectionReason = "unit-or-tribe-mismatch";
                return false;
            }
            if (!MatchesSynchronousAttackMovementContext(unit, scope, out string contextReason))
            {
                rejectionReason = contextReason;
                return false;
            }

            int targetX = unit->r_AttackMoveToTargetTileX;
            int targetY = unit->r_AttackMoveToTargetTileY;
            if (targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth)
            {
                rejectionReason = "invalid-attack-move-target";
                return false;
            }

            plan = new PlanScope(unitId, targetX, targetY)
            {
                PlayerId = unit->r_ControllableForPlayerId,
                AttackMovementQualified = true
            };
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            bool nativeUnitApproach = scope.Command == TribeAICommand.AttackUnit &&
                scope.PublishedUnitAttackApproaches.Contains(targetTileId);
            bool routeQualified = nativeUnitApproach
                ? TryFindRequiredFriendlyCompletedMoatRouteToEndpoint(
                    plan,
                    targetTileId,
                    false,
                    out summary,
                    out _)
                : TryFindRequiredFriendlyCompletedMoatRouteForPlan(plan, out summary);
            if (!routeQualified)
            {
                plan = null;
                rejectionReason = nativeUnitApproach
                    ? "native-unit-approach-owner-route-rejected"
                    : "no-required-friendly-moat-route";
                return false;
            }

            plan.FriendlyRouteQualified = true;
            rejectionReason = nativeUnitApproach
                ? "native-unit-approach-endpoint"
                : "friendly-moat-required";
            GetOrCreateAttackTracker(scope, unitId);
            return true;
        }

        private bool MatchesSynchronousAttackMovementContext(
            GameUnit* unit, AttackCommandScope scope, out string rejectionReason)
        {
            rejectionReason = "command-or-target-context-mismatch";
            if (scope.Command == TribeAICommand.AttackUnit)
            {
                return (TribeAICommand)unit->r_AI_LastIssuedTribeCommand == scope.Command &&
                    MatchesAttackTargetContext(
                        unit, scope.Command, scope.TargetValue1, scope.TargetValue2);
            }

            if (!IsBuildingAttackCommand(scope.Command))
                return false;
            if (!TryValidateHostileBuildingTarget(
                    scope.TargetValue1,
                    unchecked((uint)scope.TargetValue2),
                    unit->r_ControllableForPlayerId,
                    out GameBuilding* building))
            {
                rejectionReason = "building-target-context-mismatch";
                return false;
            }
            if (!TryGetUnitAttackMoveTile(unit, out int approachTileId) ||
                !TryGetPublishedBuildingFootprint(
                    scope.PublishedBuildingApproaches,
                    approachTileId,
                    out int footprintTileId) ||
                !IsValidBuildingApproachPair(
                    scope.TargetValue1, building, approachTileId, footprintTileId))
            {
                rejectionReason = "building-approach-context-mismatch";
                return false;
            }

            return true;
        }

        private void LogSynchronousAttackCandidate(
            AttackCommandScope scope, int unitId, GameUnit* unit)
        {
            string signature = $"sync:{scope.MapEpoch}:{scope.TribeId}:{scope.Command}:" +
                $"{scope.TargetValue1}:{scope.TargetValue2}:{GetAttackCandidateSignature(unit)}";
            if (lastAttackCommandCandidates.TryGetValue(unitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }

            lastAttackCommandCandidates[unitId] = signature;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=attack-command-candidate phase=sync unit={unitId} " +
                $"type={unit->r_UnitChimp} global={unit->r_GlobalId} player={unit->r_ControllableForPlayerId} " +
                $"tribe={unit->r_TribeId}/{scope.TribeId} aiState={unit->r_AIState} " +
                $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand}/{scope.Command} " +
                $"target={scope.TargetValue1}/{scope.TargetValue2} " +
                $"contextUnit={unit->r_AI_ContextTargetUnitId}/{unit->r_AI_ContextTargetUnitGlobalId} " +
                $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY}).");
        }

        private void LogAttackScopeDecision(
            string stage,
            int unitId,
            GameUnit* unit,
            int vanillaResult,
            string reason,
            RouteProbeSummary summary)
        {
            AttackCommandScope scope = activeAttackCommand;
            string signature =
                $"{stage}:{mapEpoch}:{scope?.TribeId}:{scope?.Command}:{scope?.TargetValue1}:" +
                $"{scope?.TargetValue2}:{unit->r_AIState}:{unit->r_AttackMoveToTargetTileX}:" +
                $"{unit->r_AttackMoveToTargetTileY}:{reason}:{summary.RouteFound}";
            if (scope != null && scope.LastDecisionByUnit.TryGetValue(unitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }
            if (scope != null)
                scope.LastDecisionByUnit[unitId] = signature;

            string buildingPair = string.Empty;
            if (scope != null && IsBuildingAttackCommand(scope.Command) &&
                TryGetUnitAttackMoveTile(unit, out int approachTileId) &&
                TryGetPublishedBuildingFootprint(
                    scope.PublishedBuildingApproaches,
                    approachTileId,
                    out int footprintTileId))
            {
                buildingPair = $" buildingPair={approachTileId}->{footprintTileId}";
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage={stage} unit={unitId} type={unit->r_UnitChimp} " +
                $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId}/{scope?.TribeId} " +
                $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand}/{scope?.Command} " +
                $"target={scope?.TargetValue1}/{scope?.TargetValue2} " +
                $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY}) " +
                $"vanillaMode={vanillaResult} reason={reason}{buildingPair} " +
                $"{summary.ToLogFields()}.");
        }

        private int AllowBuilderAfterFailedRegionSearch(
            IntPtr pathManager, int movementClass, int targetRegion, int startX, int startY)
        {
            int vanillaResult = originalRegionReachability(pathManager, movementClass, targetRegion, startX, startY);
            PlanScope plan = activePlan ?? pendingPlan;
            bool scoped = activeMoveCommand != null ||
                (plan != null && plan.FriendlyRouteQualified);
            if (disposed || !scoped)
                return vanillaResult;

            if (plan == null ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) ||
                unit == null || !CanDigMoat(unit))
            {
                return vanillaResult;
            }

            if (activeMoveCommand != null)
                activeMoveCommand.RegionCalls++;

            try
            {
                bool bypass = vanillaResult == 0 && plan.FriendlyRouteQualified &&
                    plan.ModeObserved && *moatPathMode == 1 &&
                    targetRegion > 0 && targetRegion <= MaximumRegionId;
                if (!bypass)
                    return vanillaResult;

                LogMovementContext(
                    $"stage=region movementClass={movementClass} start=({startX},{startY}) " +
                    $"targetRegion={targetRegion} vanilla=0 effective={targetRegion}");
                return targetRegion;
            }
            catch (Exception ex)
            {
                LogFailure("region", ex);
                return vanillaResult;
            }
        }

        private int AllowTribeFloodFillForMoveOrder(IntPtr tribeManager, int tribeId, int floodFillStamp)
        {
            int vanillaResult = originalTribeFloodFillMembership(tribeManager, tribeId, floodFillStamp);
            if (disposed)
                return vanillaResult;

            try
            {
                MoveCommandScope command = activeMoveCommand;
                bool managerValid = tribeManager != IntPtr.Zero;
                bool matchingTribe = command != null && tribeId == command.TribeId;
                bool stampValid = floodFillStamp > 0 && floodFillStamp <= MaximumFloodFillStamp;
                bool bypass = vanillaResult == 0 && managerValid && matchingTribe && stampValid;
                if (command != null)
                {
                    command.FloodCalls++;
                    if (vanillaResult != 0)
                        command.FloodVanillaPositive++;
                    try
                    {
                        LogPipelineDiagnostic(
                            $"stage=tribe-flood-observed commandTribe={command.TribeId} callTribe={tribeId} " +
                            $"matchingTribe={matchingTribe} stamp={floodFillStamp} stampValid={stampValid} " +
                            $"managerValid={managerValid} vanilla={vanillaResult} " +
                            $"effective={(bypass ? 1 : vanillaResult)} bypass={bypass}");
                    }
                    catch
                    {
                        // Diagnostics must not change the flood-fill decision.
                    }
                }
                if (!bypass)
                    return vanillaResult;

                command.FloodFillBypasses++;
                try
                {
                    LogMovementContext(
                        $"stage=tribe-flood-fill tribe={tribeId} stamp={floodFillStamp} vanilla=0 effective=1");
                }
                catch
                {
                    // Diagnostics must not undo an otherwise valid scoped bypass.
                }
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("tribe-flood-fill", ex);
                return vanillaResult;
            }
        }

        private int NormalizeMixedGroupMoatMode(IntPtr tribeManager, int tribeId)
        {
            int vanillaResult = originalFirstGroupUnitOnCompletedMoat(tribeManager, tribeId);
            if (disposed || vanillaResult <= 0)
                return vanillaResult;

            MoveCommandScope command = activeMoveCommand;
            if (command == null || command.TribeId != tribeId ||
                tribeManager == IntPtr.Zero || tribeManager != nativeTribeManager ||
                tribeId < 0 || tribeId >= MaximumTribeCount || getGroupUnitId == null)
            {
                return vanillaResult;
            }

            try
            {
                byte* tribeRecord = (byte*)tribeManager.ToPointer() +
                    (tribeId * TribeRecordSize);
                int leadUnitId = *(short*)(tribeRecord + TribeLeadUnitIdOffset);
                int unitCount = *(short*)(tribeRecord + TribeUnitCountOffset);
                if (unitCount <= 1 || unitCount > MaximumUnitCount)
                    return vanillaResult;

                // 0x11B520 only selects the shared moat builder when the first active
                // moat unit returned by 0x117BC0 is also the tribe's lead unit.
                if (vanillaResult != leadUnitId)
                    return vanillaResult;

                int activeUnitsOnMoat = 0;
                int activeUnitsOffMoat = 0;
                int diggerUnits = 0;
                int qualifyingDiggerUnitsOnMoat = 0;
                int qualifyingUnitId = 0;
                RouteProbeSummary qualifyingRoute = default;
                for (int ordinal = 0; ordinal < unitCount; ordinal++)
                {
                    int unitId = getGroupUnitId(tribeManager, tribeId, ordinal);
                    if (unitId <= 0 ||
                        !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                        unit == null || unit->r_AliveState != AliveState.IsAlive ||
                        *(ushort*)((byte*)unit + UnitGroupInactiveStateOffset) != 0)
                    {
                        continue;
                    }

                    int startX = unit->r_CurrentTilePositionX;
                    int startY = unit->r_CurrentTilePositionY;
                    if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                        continue;
                    int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                    if (!IsValidTileId(startTileId))
                        continue;

                    bool onCompletedMoat =
                        (tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
                    if (onCompletedMoat)
                        activeUnitsOnMoat++;
                    else
                        activeUnitsOffMoat++;

                    if (!CanDigMoat(unit))
                        continue;
                    diggerUnits++;
                    if (!onCompletedMoat || qualifyingUnitId != 0)
                        continue;

                    PlanScope probe = new PlanScope(unitId, command.TargetX, command.TargetY)
                    {
                        PlayerId = unit->r_ControllableForPlayerId
                    };
                    if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                            probe, out RouteProbeSummary route))
                    {
                        continue;
                    }

                    qualifyingDiggerUnitsOnMoat++;
                    qualifyingUnitId = unitId;
                    qualifyingRoute = route;
                }

                bool mixedPositions = activeUnitsOnMoat > 0 && activeUnitsOffMoat > 0;
                bool normalize = mixedPositions && qualifyingUnitId > 0;
                // The original positive result proves that this command is moat-relevant,
                // even when no owner-qualified route was found and the decision stays Vanilla.
                command.MoatRelevant = true;
                MarkCommandMoatRelevant(command, qualifyingRoute);
                string diagnostic =
                    $"stage=group-moat-mode tribe={tribeId} target=({command.TargetX},{command.TargetY}) " +
                    $"lead={leadUnitId} vanillaFirstMoat={vanillaResult} onMoat={activeUnitsOnMoat} " +
                    $"offMoat={activeUnitsOffMoat} diggers={diggerUnits} " +
                    $"qualifyingDiggersOnMoat={qualifyingDiggerUnitsOnMoat} " +
                    $"qualifyingUnit={qualifyingUnitId} " +
                    $"effective={(normalize ? 0 : vanillaResult)} " +
                    $"decision={(normalize ? "normalized=ground-per-unit" : "vanilla")}";
                if (!string.Equals(
                        command.LastGroupMoatModeDiagnostic, diagnostic, StringComparison.Ordinal))
                {
                    command.LastGroupMoatModeDiagnostic = diagnostic;
                    LogCommandDiagnostic(diagnostic);
                }

                // The shared moat flood would apply to every group member. Use the ordinary
                // group flood and let the later owner/capability-qualified per-unit retry decide.
                return normalize ? 0 : vanillaResult;
            }
            catch (Exception ex)
            {
                LogFailure("group-moat-mode", ex);
                return vanillaResult;
            }
        }

        private int BuildPathWithCompletedMoatRouteVariant(
            IntPtr pathManager, int movementClass, int movementProfile)
        {
            BuilderWeightedScope shadow = TryCaptureBuilderWeightedScope(pathManager);
            int result = BuildPathWithCompletedMoatRouteVariantCore(
                pathManager, movementClass, movementProfile);
            if (shadow != null)
                ObserveWeightedMoatShadowResult(pathManager, result, shadow);
            return shadow != null && shadow.PublishedBuilderResult >= 0
                ? shadow.PublishedBuilderResult
                : result;
        }

        private int BuildPathWithCompletedMoatRouteVariantCore(
            IntPtr pathManager, int movementClass, int movementProfile)
        {
            MoveCommandScope command = activeMoveCommand;
            PlanScope plan = activePlan ?? pendingPlan;
            bool plannerQualified = plan != null && plan.FriendlyRouteQualified;
            if (disposed || pathManager == IntPtr.Zero || plan == null ||
                (command == null && !plannerQualified))
            {
                return originalPathBuilder(pathManager, movementClass, movementProfile);
            }

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* builderUnit) ||
                builderUnit == null || !CanDigMoat(builderUnit))
            {
                return originalPathBuilder(pathManager, movementClass, movementProfile);
            }

            LogBuilderNativeState(
                "entry", pathManager, plan, movementClass, movementProfile, null);

            MarkTrackedAttackPipeline(
                plan.UnitId, AttackPipelineStage.Builder, plan.TargetX, plan.TargetY, false);

            if (command != null)
            {
                command.BuilderCalls++;
                command.BuilderReached = true;
            }

            int currentMoatMode = *moatPathMode;
            int floodFillBypasses = command?.FloodFillBypasses ?? 0;
            // Flood membership is not a reliable reachability verdict. The unchanged builder
            // gets the first decision; owner-aware moat fallback is considered only after 0.
            bool builderEligible = plan.FriendlyRouteQualified &&
                plan.ModeObserved && currentMoatMode == 1;
            if (!builderEligible)
            {
                int vanillaBuilderResult = originalPathBuilder(pathManager, movementClass, movementProfile);
                RecordVanillaBuilderResult(command, vanillaBuilderResult);
                RecordBuilderResult(command, vanillaBuilderResult);
                LogBuilderNativeState(
                    "after-vanilla-ineligible", pathManager, plan,
                    movementClass, movementProfile, vanillaBuilderResult);
                try
                {
                    LogPipelineDiagnostic(
                        $"stage=builder-gate unit={plan.UnitId} player={plan.PlayerId} " +
                        $"target=({plan.TargetX},{plan.TargetY}) eligible=False " +
                        $"modeObserved={plan.ModeObserved} " +
                        $"vanillaModeDetected={plan.VanillaModeDetected} " +
                        $"plannerQualified={plannerQualified} floodBypasses={floodFillBypasses} " +
                        $"moatMode={currentMoatMode} " +
                        $"movementClass={movementClass} movementProfile={movementProfile} " +
                        $"vanillaBuilderResult={vanillaBuilderResult}");
                }
                catch
                {
                    // Diagnostics must not change the native builder result.
                }
                return vanillaBuilderResult;
            }

            int* routeVariant = (int*)((byte*)pathManager.ToPointer() + PathManagerRouteVariantOffset);
            int originalRouteVariant = *routeVariant;
            int originalAssassinMode =
                *(int*)((byte*)pathManager.ToPointer() + PathManagerAssassinModeOffset);
            int vanillaMoatMode = plan.VanillaModeDetected ? 1 : 0;
            bool vanillaModeAdjusted = currentMoatMode != vanillaMoatMode;
            int vanillaResult;
            if (vanillaModeAdjusted)
                *moatPathMode = vanillaMoatMode;
            try
            {
                vanillaResult = originalPathBuilder(
                    pathManager, movementClass, movementProfile);
            }
            finally
            {
                if (vanillaModeAdjusted)
                    *moatPathMode = currentMoatMode;
            }

            RecordVanillaBuilderResult(command, vanillaResult);
            LogBuilderNativeState(
                "after-vanilla-first", pathManager, plan,
                movementClass, movementProfile, vanillaResult);
            bool supportedRouteVariant = originalRouteVariant == 0 || originalRouteVariant == 1;
            bool route80FallbackCandidate = vanillaResult == 0 && supportedRouteVariant;
            bool switchRouteVariantToGround = route80FallbackCandidate && originalRouteVariant == 1;
            string fallbackCandidate = route80FallbackCandidate
                ? (originalRouteVariant == 0
                    ? "route80-already-ground"
                    : (plan.VanillaModeDetected ? "standing-on-moat-route80" : "route80-switch"))
                : "none";
            try
            {
                LogPipelineDiagnostic(
                    $"stage=builder-vanilla-first unit={plan.UnitId} player={plan.PlayerId} " +
                    $"target=({plan.TargetX},{plan.TargetY}) route80={originalRouteVariant} " +
                    $"path88={originalAssassinMode} moatMode={vanillaMoatMode} " +
                    $"result={vanillaResult} fallbackCandidate={fallbackCandidate}");
            }
            catch
            {
                // Diagnostics must not change the Vanilla-first builder result.
            }
            if (!route80FallbackCandidate)
            {
                RecordBuilderResult(command, vanillaResult);
                return vanillaResult;
            }

            RouteProbeSummary routeSummary;
            try
            {
                bool friendlyRoute = TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                    plan, out routeSummary);
                MarkCommandMoatRelevant(command, routeSummary);
                if (!friendlyRoute)
                {
                    LogBuilderDecision(
                        $"stage=owner-gate unit={plan.UnitId} player={plan.PlayerId} " +
                        $"target=({plan.TargetX},{plan.TargetY}) effective=vanilla " +
                        routeSummary.ToLogFields());
                    RecordBuilderResult(command, vanillaResult);
                    return vanillaResult;
                }
            }
            catch (Exception ex)
            {
                LogFailure("owner-gate", ex);
                RecordBuilderResult(command, vanillaResult);
                return vanillaResult;
            }

            LogBuilderDecision(
                $"stage=owner-gate unit={plan.UnitId} player={plan.PlayerId} " +
                $"target=({plan.TargetX},{plan.TargetY}) effective=allow " +
                routeSummary.ToLogFields());

            if (switchRouteVariantToGround)
                *routeVariant = 0;
            LogBuilderNativeState(
                "before-fallback", pathManager, plan,
                movementClass, movementProfile, vanillaResult);
            int result;
            try
            {
                if (command != null)
                    command.FallbackBuilderCalls++;
                result = originalPathBuilder(pathManager, movementClass, movementProfile);
            }
            catch
            {
                if (switchRouteVariantToGround)
                    *routeVariant = originalRouteVariant;
                throw;
            }

            bool retained = result > 0;
            if (switchRouteVariantToGround && !retained)
                *routeVariant = originalRouteVariant;

            LogBuilderNativeState(
                "after-fallback", pathManager, plan,
                movementClass, movementProfile, result);

            try
            {
                if (route80FallbackCandidate)
                {
                    LogBuilderDecision(
                        $"stage=builder-route80 unit={plan.UnitId} movementClass={movementClass} " +
                        $"variant={fallbackCandidate} " +
                        $"movementProfile={movementProfile} original={originalRouteVariant} " +
                        $"effective={(retained ? 0 : originalRouteVariant)} " +
                        $"vanillaResult={vanillaResult} result={result} retained={retained}");
                }
            }
            catch
            {
                // Logging must never change a successfully produced native path.
            }

            RecordBuilderResult(command, result);

            if (retained)
                StartOrRefreshMoatMoveTracker(plan, routeSummary, result);

            return result;
        }

        private void ObserveAttackApproachFloodBuilder(
            IntPtr pathManager,
            int tribeId,
            int targetContext,
            uint targetX,
            uint targetY,
            int requestedResults,
            int sourceRegion,
            int movementClass)
        {
            AttackApproachDiagnosticScope previous = activeAttackApproachDiagnostic;
            AttackApproachDiagnosticScope scope = null;
            try
            {
                AttackCommandScope command = activeAttackCommand;
                if (!disposed && pathManager != IntPtr.Zero && command != null &&
                    command.MapEpoch == mapEpoch && command.TribeId == tribeId &&
                    command.Command == TribeAICommand.AttackUnit)
                {
                    ResolveAttackApproachRepresentative(
                        command, out int unitId, out int playerId, out eChimps unitType);
                    scope = new AttackApproachDiagnosticScope(
                        command,
                        AttackApproachKind.UnitFlood,
                        command.Sequence,
                        command.Command,
                        tribeId,
                        targetContext,
                        unchecked((int)targetX),
                        unchecked((int)targetY),
                        requestedResults,
                        sourceRegion,
                        movementClass,
                        unitId,
                        playerId,
                        unitType,
                        CaptureAttackApproachState(pathManager))
                    {
                        AllSelectedAssassins = allSelectedUnitsAssassins != null &&
                            allSelectedUnitsAssassins(nativeTribeManager, tribeId) != 0
                    };
                    activeAttackApproachDiagnostic = scope;
                }
            }
            catch (Exception ex)
            {
                activeAttackApproachDiagnostic = previous;
                TryLogDiagnosticFailure("attack-approach-unit-pre", ex);
                scope = null;
            }

            try
            {
                originalAttackApproachFloodBuilder(
                    pathManager,
                    tribeId,
                    targetContext,
                    targetX,
                    targetY,
                    requestedResults,
                    sourceRegion,
                    movementClass);
            }
            finally
            {
                if (scope != null)
                {
                    try
                    {
                        scope.After = CaptureAttackApproachState(pathManager);
                        PublishUnitAttackApproachTiles(scope.OwnerCommand, pathManager);
                        LogAttackApproachDiagnostic(scope);
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure("attack-approach-unit-post", ex);
                    }
                }
                activeAttackApproachDiagnostic = previous;
            }
        }

        private void ObserveBuildingApproachBuilder(
            IntPtr pathManager,
            int tribeId,
            int buildingId,
            int requestedResults,
            int sourceRegion,
            int movementClass)
        {
            AttackApproachDiagnosticScope previous = activeAttackApproachDiagnostic;
            BuildingApproachPerformanceScope previousPerformance =
                activeBuildingApproachPerformance;
            AttackApproachDiagnosticScope scope = null;
            BuildingApproachPerformanceScope performance = null;
            long started = 0;
            try
            {
                AttackCommandScope command = activeAttackCommand;
                if (!disposed && pathManager != IntPtr.Zero && command != null &&
                    command.MapEpoch == mapEpoch && command.TribeId == tribeId &&
                    IsBuildingAttackCommand(command.Command))
                {
                    ResolveAttackApproachRepresentative(
                        command, out int unitId, out int playerId, out eChimps unitType);
                    scope = new AttackApproachDiagnosticScope(
                        command,
                        AttackApproachKind.BuildingApproach,
                        command.Sequence,
                        command.Command,
                        tribeId,
                        buildingId,
                        -1,
                        -1,
                        requestedResults,
                        sourceRegion,
                        movementClass,
                        unitId,
                        playerId,
                        unitType,
                        CaptureAttackApproachState(pathManager, requirePairedResult: true))
                    {
                        AllSelectedAssassins = allSelectedUnitsAssassins != null &&
                            allSelectedUnitsAssassins(nativeTribeManager, tribeId) != 0
                    };
                    performance = new BuildingApproachPerformanceScope(
                        command.Sequence, buildingId);
                    activeAttackApproachDiagnostic = scope;
                    activeBuildingApproachPerformance = performance;
                }
            }
            catch (Exception ex)
            {
                activeAttackApproachDiagnostic = previous;
                activeBuildingApproachPerformance = previousPerformance;
                TryLogDiagnosticFailure("attack-approach-building-pre", ex);
                scope = null;
            }

            try
            {
                started = Stopwatch.GetTimestamp();
                originalBuildingApproachBuilder(
                    pathManager, tribeId, buildingId, requestedResults, sourceRegion, movementClass);
            }
            finally
            {
                if (performance != null && started != 0)
                    performance.TotalElapsedTicks = Stopwatch.GetTimestamp() - started;
                if (scope != null)
                {
                    try
                    {
                        scope.After = CaptureAttackApproachState(
                            pathManager, requirePairedResult: true);
                        LogAttackApproachDiagnostic(scope);
                        LogBuildingApproachPerformance(performance);
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure("attack-approach-building-post", ex);
                    }
                }
                activeBuildingApproachPerformance = previousPerformance;
                activeAttackApproachDiagnostic = previous;
            }
        }

        private void ObserveBuildingCandidateConsumer(
            IntPtr tribeManager, int tribeId, int builderVariant)
        {
            AttackApproachDiagnosticScope previous = activeAttackApproachDiagnostic;
            BuildingConsumerPerformanceScope previousPerformance =
                activeBuildingConsumerPerformance;
            AttackApproachDiagnosticScope scope = null;
            BuildingConsumerPerformanceScope performance = null;
            BuildingApproachCandidate[] vanillaCandidates = Array.Empty<BuildingApproachCandidate>();
            bool vanillaCompleted = false;
            long vanillaStarted = 0;
            try
            {
                AttackCommandScope command = activeAttackCommand;
                if (!disposed && tribeManager != IntPtr.Zero && command != null &&
                    command.MapEpoch == mapEpoch && command.TribeId == tribeId &&
                    IsBuildingAttackCommand(command.Command))
                {
                    ResolveAttackApproachRepresentative(
                        command, out int unitId, out int playerId, out eChimps unitType);
                    scope = new AttackApproachDiagnosticScope(
                        command,
                        AttackApproachKind.BuildingCandidateConsumer,
                        command.Sequence,
                        command.Command,
                        tribeId,
                        command.TargetValue1,
                        -1,
                        -1,
                        -1,
                        -1,
                        -1,
                        unitId,
                        playerId,
                        unitType,
                        CaptureAttackApproachState(
                            nativePathManager, requirePairedResult: true))
                    {
                        AllSelectedAssassins = allSelectedUnitsAssassins != null &&
                            allSelectedUnitsAssassins(tribeManager, tribeId) != 0,
                        ConsumerVariant = builderVariant
                    };
                    vanillaCandidates = CaptureBuildingApproachCandidates(nativePathManager);
                    performance = new BuildingConsumerPerformanceScope(
                        command.Sequence, command.TargetValue1, vanillaCandidates.Length);
                    activeAttackApproachDiagnostic = scope;
                }
            }
            catch (Exception ex)
            {
                activeAttackApproachDiagnostic = previous;
                TryLogDiagnosticFailure("attack-approach-building-consumer-pre", ex);
                scope = null;
            }

            try
            {
                vanillaStarted = Stopwatch.GetTimestamp();
                originalBuildingCandidateConsumer(tribeManager, tribeId, builderVariant);
                vanillaCompleted = true;
            }
            finally
            {
                if (performance != null && vanillaStarted != 0)
                {
                    performance.VanillaElapsedTicks =
                        Stopwatch.GetTimestamp() - vanillaStarted;
                }
                if (scope != null)
                {
                    try
                    {
                        AttackApproachState vanillaAfter =
                            CaptureAttackApproachState(nativePathManager, requirePairedResult: true);
                        BuildingConsumerFallbackResult fallback;
                        long fallbackStarted = Stopwatch.GetTimestamp();
                        activeBuildingConsumerPerformance = performance;
                        try
                        {
                            fallback = vanillaCompleted
                                ? TryApplyBuildingConsumerFallback(
                                    scope, tribeManager, vanillaCandidates, vanillaAfter)
                                : BuildingConsumerFallbackResult.NotAttempted("vanilla-threw");
                        }
                        finally
                        {
                            if (performance != null)
                            {
                                performance.FallbackElapsedTicks =
                                    Stopwatch.GetTimestamp() - fallbackStarted;
                            }
                            activeBuildingConsumerPerformance = previousPerformance;
                        }
                        PublishBuildingApproachPairs(scope.OwnerCommand, nativePathManager);
                        scope.After = CaptureAttackApproachState(
                            nativePathManager, requirePairedResult: true);
                        LogBuildingConsumerCandidates(
                            scope, vanillaCandidates, vanillaAfter, fallback);
                        LogBuildingConsumerPerformance(performance, fallback);
                        LogAttackApproachDiagnostic(scope);
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure("attack-approach-building-consumer-post", ex);
                    }
                }
                activeBuildingConsumerPerformance = previousPerformance;
                activeAttackApproachDiagnostic = previous;
            }
        }

        private BuildingConsumerFallbackResult TryApplyBuildingConsumerFallback(
            AttackApproachDiagnosticScope scope,
            IntPtr tribeManager,
            BuildingApproachCandidate[] vanillaCandidates,
            AttackApproachState vanillaAfter)
        {
            if (vanillaAfter.UsableResultCount > 0)
                return BuildingConsumerFallbackResult.NotAttempted("vanilla-usable");
            if (scope == null || scope.OwnerCommand == null ||
                scope.OwnerCommand.MapEpoch != mapEpoch ||
                scope.OwnerCommand.TribeId != scope.TribeId ||
                !IsBuildingAttackCommand(scope.OwnerCommand.Command) ||
                tribeManager == IntPtr.Zero || tribeManager != nativeTribeManager ||
                nativePathManager == IntPtr.Zero)
            {
                return BuildingConsumerFallbackResult.Rejected("invalid-command-scope");
            }

            AttackCommandScope command = scope.OwnerCommand;
            if (!TryCaptureOrderedActiveGroupUnits(
                    tribeManager, scope.TribeId, out int[] groupUnitIds))
            {
                return BuildingConsumerFallbackResult.Rejected("invalid-command-group");
            }

            List<int> diggerUnitIds = new List<int>();
            int playerId = -1;
            foreach (int unitId in groupUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != scope.TribeId || !CanDigMoat(unit))
                {
                    continue;
                }

                int candidatePlayerId = unit->r_ControllableForPlayerId;
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(candidatePlayerId))
                    continue;
                if (playerId < 0)
                    playerId = candidatePlayerId;
                if (candidatePlayerId == playerId)
                    diggerUnitIds.Add(unitId);
            }
            if (diggerUnitIds.Count == 0 || playerId < 0)
                return BuildingConsumerFallbackResult.Rejected("no-active-vanilla-digger");
            if (!TryValidateHostileBuildingTarget(
                    command.TargetValue1,
                    unchecked((uint)command.TargetValue2),
                    playerId,
                    out GameBuilding* building))
            {
                return BuildingConsumerFallbackResult.Rejected("invalid-hostile-building");
            }

            BuildingConsumerPerformanceScope performance = activeBuildingConsumerPerformance;
            if (performance != null)
                performance.DiggerUnits = diggerUnitIds.Count;

            List<BuildingApproachCandidate> candidates =
                new List<BuildingApproachCandidate>();
            HashSet<long> uniquePairs = new HashSet<long>();
            RouteProbeSummary observed = default;
            int walkableReservations = 0;
            int missingContexts = 0;
            int invalidContexts = 0;
            int reservedBlocked = 0;
            int ownerRouteRejected = 0;
            for (int index = 0; index < vanillaCandidates.Length; index++)
            {
                BuildingApproachCandidate candidate = vanillaCandidates[index];
                if (candidate.FootprintTileId <= 0)
                {
                    missingContexts++;
                    continue;
                }
                if (!IsValidBuildingApproachPair(
                        command.TargetValue1, building,
                        candidate.ApproachTileId, candidate.FootprintTileId))
                {
                    invalidContexts++;
                    if (IsValidTileId(candidate.ApproachTileId) &&
                        GameTileManagerAPI.Instance.GetTileBuildingId(
                            candidate.ApproachTileId) != 0 &&
                        nativeMovementMasks[candidate.ApproachTileId] == 0)
                    {
                        reservedBlocked++;
                    }
                    continue;
                }

                UnmanagedVector2<ushort> approachPosition =
                    GameTileManagerAPI.Instance.GetTileVectorFromId(candidate.ApproachTileId);
                candidate.ApproachX = approachPosition.X;
                candidate.ApproachY = approachPosition.Y;
                candidate.Score = int.MaxValue;
                candidate.TargetRegion = IsValidTileId(candidate.ApproachTileId)
                    ? pathRegionGrid[candidate.ApproachTileId]
                    : 0;
                long pairKey = ((long)candidate.ApproachTileId << 32) |
                    unchecked((uint)candidate.FootprintTileId);
                if (!uniquePairs.Add(pairKey))
                    continue;
                candidate.OriginalOrder = index;
                candidates.Add(candidate);
                if (candidates.Count >= VanillaAttackFloodResultCapacity - 1)
                    break;
            }

            int[] evaluationOrder = new int[candidates.Count];
            for (int index = 0; index < evaluationOrder.Length; index++)
                evaluationOrder[index] = index;
            Array.Sort(evaluationOrder, (leftIndex, rightIndex) =>
            {
                BuildingApproachCandidate left = candidates[leftIndex];
                BuildingApproachCandidate right = candidates[rightIndex];
                int regionComparison = left.TargetRegion.CompareTo(right.TargetRegion);
                return regionComparison != 0
                    ? regionComparison
                    : left.OriginalOrder.CompareTo(right.OriginalOrder);
            });

            if (performance != null)
                performance.ValidCandidates = candidates.Count;
            // Keep one unit active in EnsureReachabilityMap while all candidates of the
            // same region are evaluated. The previous candidate-first order evicted the
            // one-entry cache for every unit and caused candidates x units full BFS runs.
            foreach (int unitId in diggerUnitIds)
            {
                for (int orderIndex = 0; orderIndex < evaluationOrder.Length; orderIndex++)
                {
                    int candidateIndex = evaluationOrder[orderIndex];
                    BuildingApproachCandidate candidate = candidates[candidateIndex];
                    if (performance != null)
                        performance.RouteEvaluations++;
                    PlanScope route = new PlanScope(
                        unitId, candidate.ApproachX, candidate.ApproachY);
                    if (!TryFindRequiredFriendlyCompletedMoatRouteToEndpoint(
                            route, candidate.ApproachTileId,
                            true,
                            out RouteProbeSummary routeSummary,
                            out int routeDistance))
                    {
                        observed.MergeObservations(routeSummary);
                        continue;
                    }

                    observed.MergeObservations(routeSummary);
                    if (routeDistance < candidate.Score)
                    {
                        candidate.Score = routeDistance;
                        candidates[candidateIndex] = candidate;
                    }
                }
            }

            List<BuildingApproachCandidate> accepted = new List<BuildingApproachCandidate>();
            for (int index = 0; index < candidates.Count; index++)
            {
                BuildingApproachCandidate candidate = candidates[index];
                if (candidate.Score == int.MaxValue)
                {
                    ownerRouteRejected++;
                    continue;
                }
                if (GameTileManagerAPI.Instance.GetTileBuildingId(
                        candidate.ApproachTileId) != 0)
                {
                    walkableReservations++;
                }
                accepted.Add(candidate);
            }

            accepted.Sort((left, right) =>
            {
                int scoreComparison = left.Score.CompareTo(right.Score);
                return scoreComparison != 0
                    ? scoreComparison
                    : left.OriginalOrder.CompareTo(right.OriginalOrder);
            });

            if (accepted.Count == 0)
            {
                return BuildingConsumerFallbackResult.Rejected(
                    "no-owner-qualified-vanilla-candidate",
                    diggerUnitIds.Count,
                    0,
                    walkableReservations,
                    missingContexts,
                    invalidContexts,
                    reservedBlocked,
                    ownerRouteRejected,
                    observed);
            }

            BuildingApproachCandidate[] postVanillaBuffer =
                CaptureBuildingApproachBuffer(nativePathManager);
            try
            {
                WriteBuildingApproachCandidates(nativePathManager, accepted);
            }
            catch
            {
                RestoreBuildingApproachBuffer(nativePathManager, postVanillaBuffer);
                throw;
            }

            return BuildingConsumerFallbackResult.Applied(
                diggerUnitIds.Count,
                accepted.Count,
                walkableReservations,
                missingContexts,
                invalidContexts,
                reservedBlocked,
                ownerRouteRejected,
                observed);
        }

        private int ObserveAttackApproachRegionPair(
            IntPtr pathManager,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind)
        {
            int vanillaResult = originalRegionPairReachability(
                pathManager, movementClass, sourceRegion, targetRegion, routeKind);
            AttackApproachDiagnosticScope scope = activeAttackApproachDiagnostic;
            if (scope == null || disposed)
                return vanillaResult;

            try
            {
                scope.ObserveRegionPair(
                    movementClass, sourceRegion, targetRegion, routeKind, vanillaResult);

                if (vanillaResult == 0 && scope.Kind == AttackApproachKind.UnitFlood &&
                    scope.Command == TribeAICommand.AttackUnit)
                {
                    string decisionKey =
                        $"{movementClass}:{sourceRegion}:{targetRegion}:{routeKind}";
                    if (!scope.RegionFallbackDecisions.TryGetValue(
                            decisionKey, out AttackRegionFallbackDecision decision))
                    {
                        decision = EvaluateAttackUnitRegionFallback(
                            scope, movementClass, sourceRegion, targetRegion, routeKind);
                        scope.RegionFallbackDecisions[decisionKey] = decision;

                        string logSignature =
                            $"attack-unit-region:{scope.CommandSequence}:{decisionKey}:" +
                            $"{decision.Allowed}:{decision.Reason}:{decision.ApproachX}:" +
                            $"{decision.ApproachY}:{decision.Summary.ObservedOwnerMask}:" +
                            $"{decision.Summary.FriendlyMoatTiles}:" +
                            $"{decision.Summary.EnemyMoatTiles}";
                        if (scope.OwnerCommand.AttackApproachDiagnosticSignatures.Add(logSignature))
                        {
                            Shared.DebugLogHelper.LogInfo(
                                log,
                                $"MoveMoat stage=attack-unit-region-fallback " +
                                $"commandSeq={scope.CommandSequence} unit={scope.UnitId} " +
                                $"player={scope.PlayerId} targetContext={scope.TargetContext} " +
                                $"target=({scope.TargetX},{scope.TargetY}) " +
                                $"class={movementClass} regions={sourceRegion}->{targetRegion} " +
                                $"routeKind={routeKind} vanilla=0 " +
                                $"effective={(decision.Allowed ? 1 : 0)} " +
                                $"approach=({decision.ApproachX},{decision.ApproachY}) " +
                                $"reason={decision.Reason} {decision.Summary.ToLogFields()}.");
                        }
                    }

                    if (decision.Allowed)
                        return 1;
                }
                else if (vanillaResult == 0 &&
                    scope.Kind == AttackApproachKind.BuildingApproach &&
                    IsBuildingAttackCommand(scope.Command))
                {
                    string decisionKey =
                        $"building:{movementClass}:{sourceRegion}:{targetRegion}:{routeKind}";
                    if (!scope.RegionFallbackDecisions.TryGetValue(
                            decisionKey, out AttackRegionFallbackDecision decision))
                    {
                        BuildingApproachPerformanceScope performance =
                            activeBuildingApproachPerformance;
                        long fallbackStarted = Stopwatch.GetTimestamp();
                        try
                        {
                            decision = EvaluateAttackBuildingRegionFallback(
                                scope, movementClass, sourceRegion, targetRegion, routeKind);
                        }
                        finally
                        {
                            if (performance != null)
                            {
                                performance.RegionFallbackEvaluations++;
                                performance.RegionFallbackElapsedTicks +=
                                    Stopwatch.GetTimestamp() - fallbackStarted;
                            }
                        }
                        scope.RegionFallbackDecisions[decisionKey] = decision;

                        string logSignature =
                            $"attack-building-region:{scope.CommandSequence}:{decisionKey}:" +
                            $"{decision.Allowed}:{decision.Reason}:{decision.ApproachX}:" +
                            $"{decision.ApproachY}:{decision.Summary.ObservedOwnerMask}:" +
                            $"{decision.Summary.FriendlyMoatTiles}:" +
                            $"{decision.Summary.EnemyMoatTiles}";
                        if (scope.OwnerCommand.AttackApproachDiagnosticSignatures.Add(logSignature))
                        {
                            Shared.DebugLogHelper.LogInfo(
                                log,
                                $"MoveMoat stage=building-approach-region-fallback " +
                                $"commandSeq={scope.CommandSequence} building=" +
                                $"{scope.OwnerCommand.TargetValue1}/{scope.OwnerCommand.TargetValue2} " +
                                $"unit={scope.UnitId} player={scope.PlayerId} " +
                                $"class={movementClass} regions={sourceRegion}->{targetRegion} " +
                                $"routeKind={routeKind} vanilla=0 " +
                                $"effective={(decision.Allowed ? 1 : 0)} " +
                                $"approach=({decision.ApproachX},{decision.ApproachY}) " +
                                $"reason={decision.Reason} {decision.Summary.ToLogFields()}.");
                        }
                    }

                    if (decision.Allowed)
                        return 1;
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("attack-approach-region-pair", ex);
            }
            return vanillaResult;
        }

        private AttackRegionFallbackDecision EvaluateAttackUnitRegionFallback(
            AttackApproachDiagnosticScope scope,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind)
        {
            if (scope == null || scope.OwnerCommand == null ||
                scope.OwnerCommand.MapEpoch != mapEpoch)
                return AttackRegionFallbackDecision.Reject("invalid-scope");
            if (scope.UnitId <= 0 || scope.PlayerId < 0 ||
                movementClass != scope.MovementClass || sourceRegion != scope.SourceRegion ||
                sourceRegion <= 0 || sourceRegion > MaximumRegionId ||
                targetRegion < 0 || targetRegion > MaximumRegionId || routeKind != 0)
            {
                return AttackRegionFallbackDecision.Reject("movement-or-region-context-mismatch");
            }
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(scope.UnitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_TribeId != scope.TribeId ||
                unit->r_ControllableForPlayerId != scope.PlayerId || !CanDigMoat(unit))
            {
                return AttackRegionFallbackDecision.Reject("representative-unit-not-vanilla-digger");
            }
            if (scope.TargetContext != scope.OwnerCommand.TargetValue1 ||
                !TryGetHostileLivingUnitAtTile(
                    scope.PlayerId,
                    scope.TargetX,
                    scope.TargetY,
                    scope.OwnerCommand.TargetValue1,
                    scope.OwnerCommand.TargetValue2,
                    out _,
                    out _))
            {
                return AttackRegionFallbackDecision.Reject("hostile-target-context-mismatch");
            }

            int startTileId = GameTileManagerAPI.Instance.GetTileId(
                unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
            if (!IsValidTileId(startTileId) || pathRegionGrid[startTileId] != sourceRegion)
                return AttackRegionFallbackDecision.Reject("source-region-mismatch");

            AttackCursorPairScope routeScope = new AttackCursorPairScope(
                mapEpoch,
                scope.UnitId,
                scope.PlayerId,
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY,
                startTileId,
                scope.TargetX,
                scope.TargetY,
                GameTileManagerAPI.Instance.GetTileId(scope.TargetX, scope.TargetY),
                CursorPairFallbackKind.UnitApproach);
            bool routeFound = TryFindFriendlyCompletedMoatRouteToAttackApproach(
                routeScope, out int approachX, out int approachY, out RouteProbeSummary summary);
            if (!routeFound)
                return AttackRegionFallbackDecision.Reject("no-required-friendly-moat-route", summary);
            // Native UnitFlood legitimately uses targetRegion=0 as an approach-search
            // sentinel. Bind a positive target region when supplied, but never reject the
            // owner-qualified concrete approach tile merely because Vanilla passed zero.
            if (summary.StartRegion != sourceRegion ||
                (targetRegion > 0 && summary.TargetRegion != targetRegion))
            {
                return AttackRegionFallbackDecision.Reject(
                    "resolved-region-pair-mismatch", summary, approachX, approachY);
            }

            return AttackRegionFallbackDecision.Allow(summary, approachX, approachY);
        }

        private AttackRegionFallbackDecision EvaluateAttackBuildingRegionFallback(
            AttackApproachDiagnosticScope scope,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind)
        {
            if (scope == null || scope.OwnerCommand == null ||
                scope.OwnerCommand.MapEpoch != mapEpoch ||
                scope.OwnerCommand.TribeId != scope.TribeId ||
                !IsBuildingAttackCommand(scope.OwnerCommand.Command))
            {
                return AttackRegionFallbackDecision.Reject("invalid-building-scope");
            }
            if (movementClass != scope.MovementClass || sourceRegion != scope.SourceRegion ||
                sourceRegion < 0 || sourceRegion > MaximumRegionId ||
                targetRegion <= 0 || targetRegion > MaximumRegionId || routeKind != 0)
            {
                return AttackRegionFallbackDecision.Reject(
                    "building-movement-or-region-context-mismatch");
            }

            AttackCommandScope command = scope.OwnerCommand;
            int playerId = -1;
            List<int> diggerUnitIds = new List<int>();
            bool sourceOnFriendlyCompletedMoat = false;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero)
                return AttackRegionFallbackDecision.Reject("missing-tile-manager");
            if (!TryCaptureOrderedActiveGroupUnits(
                    nativeTribeManager, scope.TribeId, out int[] groupUnitIds))
            {
                return AttackRegionFallbackDecision.Reject("invalid-command-group");
            }
            foreach (int unitId in groupUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != scope.TribeId || !CanDigMoat(unit))
                {
                    continue;
                }

                int candidatePlayerId = unit->r_ControllableForPlayerId;
                int startX = unit->r_CurrentTilePositionX;
                int startY = unit->r_CurrentTilePositionY;
                if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                    continue;
                int startTileId = GameTileManagerAPI.Instance.GetTileId(
                    startX, startY);
                if (!playerApi.IsPlayerIdValid(candidatePlayerId) ||
                    !IsValidTileId(startTileId) || pathRegionGrid[startTileId] != sourceRegion)
                {
                    continue;
                }
                if (sourceRegion == 0)
                {
                    RouteProbeSummary sourceSummary =
                        new RouteProbeSummary(candidatePlayerId);
                    if (!IsCompletedMoatTile(startTileId) ||
                        !TryClassifyFriendlyMoat(
                            tileManager,
                            playerApi,
                            startTileId,
                            candidatePlayerId,
                            ref sourceSummary))
                    {
                        continue;
                    }
                    sourceOnFriendlyCompletedMoat = true;
                }
                if (playerId < 0)
                    playerId = candidatePlayerId;
                if (candidatePlayerId == playerId)
                    diggerUnitIds.Add(unitId);
            }
            if (diggerUnitIds.Count == 0 || playerId < 0)
            {
                return AttackRegionFallbackDecision.Reject(
                    sourceRegion == 0
                        ? "no-friendly-moat-source-digger"
                        : "no-source-region-digger");
            }

            if (!TryValidateHostileBuildingTarget(
                    command.TargetValue1,
                    unchecked((uint)command.TargetValue2),
                    playerId,
                    out GameBuilding* building) ||
                building == null || IsWallStairOrRampStructure(building->r_BuildingType))
            {
                return AttackRegionFallbackDecision.Reject("invalid-hostile-building");
            }

            RouteProbeSummary observed = new RouteProbeSummary(playerId);
            IReadOnlyList<int> approachTiles = GetBuildingApproachTilesForRegion(
                command.TargetValue1, building, targetRegion);
            // Keep one unit active while all endpoints of this region are evaluated so
            // EnsureReachabilityMap can reuse its single-entry map.
            foreach (int unitId in diggerUnitIds)
            {
                foreach (int tileId in approachTiles)
                {
                    if (activeBuildingApproachPerformance != null)
                        activeBuildingApproachPerformance.RouteEvaluations++;
                    UnmanagedVector2<ushort> position =
                        GameTileManagerAPI.Instance.GetTileVectorFromId(tileId);
                    PlanScope route = new PlanScope(unitId, position.X, position.Y);
                    if (!TryFindRequiredFriendlyCompletedMoatRouteToEndpoint(
                            route, tileId, true,
                            out RouteProbeSummary summary, out _))
                    {
                        observed.MergeObservations(summary);
                        continue;
                    }

                    observed.MergeObservations(summary);
                    return AttackRegionFallbackDecision.Allow(
                        summary,
                        position.X,
                        position.Y,
                        sourceOnFriendlyCompletedMoat
                            ? "required-friendly-moat-route-from-moat"
                            : "required-friendly-moat-route");
                }
            }

            return AttackRegionFallbackDecision.Reject(
                "no-owner-qualified-building-approach-in-region", observed);
        }

        private IReadOnlyList<int> GetBuildingApproachTilesForRegion(
            int buildingId, GameBuilding* building, int targetRegion)
        {
            BuildingApproachPerformanceScope performance =
                activeBuildingApproachPerformance;
            if (performance != null && performance.BuildingId == buildingId &&
                performance.ApproachTilesByRegion != null)
            {
                return performance.ApproachTilesByRegion.TryGetValue(
                    targetRegion, out List<int> cached)
                    ? cached
                    : Array.Empty<int>();
            }

            long started = Stopwatch.GetTimestamp();
            Dictionary<int, List<int>> byRegion = new Dictionary<int, List<int>>();
            Dictionary<int, HashSet<int>> seenByRegion = new Dictionary<int, HashSet<int>>();
            int footprintTiles = 0;
            int approachTiles = 0;

            // DA020 pairs an approach tile with one of four cardinal StructureGrid tiles.
            // Index the target building once instead of rescanning every native tile for
            // every E2610 region pair. Do not use the smaller building record bounds: some
            // valid, walkable reservations lie outside them.
            for (int footprintTileId = 0; footprintTileId < NativeTileCount; footprintTileId++)
            {
                if (!IsExactBuildingContextTile(buildingId, building, footprintTileId))
                    continue;
                footprintTiles++;

                UnmanagedVector2<ushort> footprint =
                    GameTileManagerAPI.Instance.GetTileVectorFromId(footprintTileId);
                int footprintX = footprint.X;
                int footprintY = footprint.Y;
                for (int index = 0; index < 4; index++)
                {
                    int approachX = footprintX + EndpointNeighbourX[index];
                    int approachY = footprintY + EndpointNeighbourY[index];
                    if (approachX < 0 || approachX >= MapWidth ||
                        approachY < 0 || approachY >= MapWidth)
                    {
                        continue;
                    }

                    int approachTileId = GameTileManagerAPI.Instance.GetTileId(
                        approachX, approachY);
                    if (!IsWalkableBuildingApproachEndpoint(approachTileId))
                        continue;
                    int region = pathRegionGrid[approachTileId];
                    if (region <= 0 || region > MaximumRegionId)
                        continue;

                    if (!seenByRegion.TryGetValue(region, out HashSet<int> seen))
                    {
                        seen = new HashSet<int>();
                        seenByRegion.Add(region, seen);
                        byRegion.Add(region, new List<int>());
                    }
                    if (!seen.Add(approachTileId))
                        continue;
                    byRegion[region].Add(approachTileId);
                    approachTiles++;
                }
            }

            if (performance != null && performance.BuildingId == buildingId)
            {
                performance.IndexElapsedTicks += Stopwatch.GetTimestamp() - started;
                performance.IndexScans++;
                performance.IndexedNativeTiles += NativeTileCount;
                performance.IndexedFootprintTiles = footprintTiles;
                performance.IndexedApproachTiles = approachTiles;
                performance.ApproachTilesByRegion = byRegion;
            }
            return byRegion.TryGetValue(targetRegion, out List<int> result)
                ? result
                : Array.Empty<int>();
        }

        private void ResolveAttackApproachRepresentative(
            AttackCommandScope command,
            out int unitId,
            out int playerId,
            out eChimps unitType)
        {
            unitId = -1;
            playerId = -1;
            unitType = default;
            foreach (int candidateId in command.CandidateUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(candidateId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != command.TribeId || !CanDigMoat(unit))
                {
                    continue;
                }

                unitId = candidateId;
                playerId = unit->r_ControllableForPlayerId;
                unitType = unit->r_UnitChimp;
                return;
            }
        }

        private bool TryCaptureOrderedActiveGroupUnits(
            IntPtr tribeManager, int tribeId, out int[] unitIds)
        {
            unitIds = Array.Empty<int>();
            if (tribeManager == IntPtr.Zero || tribeManager != nativeTribeManager ||
                tribeId < 0 || tribeId >= MaximumTribeCount || getGroupUnitId == null)
            {
                return false;
            }

            byte* tribeRecord = (byte*)tribeManager.ToPointer() + tribeId * TribeRecordSize;
            int unitCount = *(short*)(tribeRecord + TribeUnitCountOffset);
            if (unitCount <= 0 || unitCount > MaximumUnitCount)
                return false;

            List<int> active = new List<int>(unitCount);
            for (int ordinal = 0; ordinal < unitCount; ordinal++)
            {
                int unitId = getGroupUnitId(tribeManager, tribeId, ordinal);
                if (unitId <= 0 ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != tribeId ||
                    *(ushort*)((byte*)unit + UnitGroupInactiveStateOffset) != 0)
                {
                    continue;
                }
                active.Add(unitId);
            }

            unitIds = active.ToArray();
            return unitIds.Length > 0;
        }

        private static BuildingApproachCandidate[] CaptureBuildingApproachCandidates(
            IntPtr pathManager)
        {
            if (pathManager == IntPtr.Zero)
                return Array.Empty<BuildingApproachCandidate>();

            byte* manager = (byte*)pathManager.ToPointer();
            List<BuildingApproachCandidate> candidates = new List<BuildingApproachCandidate>();
            for (int index = 0; index < VanillaAttackFloodResultCapacity; index++)
            {
                byte* entry = manager + PathManagerFloodResultTileOffset +
                    index * PathManagerFloodResultStride;
                BuildingApproachCandidate candidate = new BuildingApproachCandidate(
                    *(int*)(entry + BuildingCandidateApproachTileOffset),
                    *(int*)(entry + BuildingCandidateFootprintTileOffset),
                    *(int*)(entry + BuildingCandidateScoreOffset));
                if (candidate.ApproachTileId == 0 && candidate.FootprintTileId == 0)
                    break;
                candidates.Add(candidate);
            }
            return candidates.ToArray();
        }

        private static BuildingApproachCandidate[] CaptureBuildingApproachBuffer(
            IntPtr pathManager)
        {
            BuildingApproachCandidate[] buffer =
                new BuildingApproachCandidate[VanillaAttackFloodResultCapacity];
            byte* manager = (byte*)pathManager.ToPointer();
            for (int index = 0; index < buffer.Length; index++)
            {
                byte* entry = manager + PathManagerFloodResultTileOffset +
                    index * PathManagerFloodResultStride;
                buffer[index] = new BuildingApproachCandidate(
                    *(int*)(entry + BuildingCandidateApproachTileOffset),
                    *(int*)(entry + BuildingCandidateFootprintTileOffset),
                    *(int*)(entry + BuildingCandidateScoreOffset));
            }
            return buffer;
        }

        private static void RestoreBuildingApproachBuffer(
            IntPtr pathManager, BuildingApproachCandidate[] buffer)
        {
            byte* manager = (byte*)pathManager.ToPointer();
            int count = Math.Min(buffer.Length, VanillaAttackFloodResultCapacity);
            for (int index = 0; index < count; index++)
                WriteBuildingApproachCandidate(manager, index, buffer[index]);
        }

        private static void WriteBuildingApproachCandidates(
            IntPtr pathManager, List<BuildingApproachCandidate> candidates)
        {
            byte* manager = (byte*)pathManager.ToPointer();
            int count = Math.Min(candidates.Count, VanillaAttackFloodResultCapacity - 1);
            for (int index = 0; index < count; index++)
                WriteBuildingApproachCandidate(manager, index, candidates[index]);
            WriteBuildingApproachCandidate(manager, count, default);
        }

        private static void WriteBuildingApproachCandidate(
            byte* manager, int index, BuildingApproachCandidate candidate)
        {
            byte* entry = manager + PathManagerFloodResultTileOffset +
                index * PathManagerFloodResultStride;
            *(int*)(entry + BuildingCandidateApproachTileOffset) = candidate.ApproachTileId;
            *(int*)(entry + BuildingCandidateFootprintTileOffset) = candidate.FootprintTileId;
            *(int*)(entry + BuildingCandidateScoreOffset) = candidate.Score;
        }

        private void PublishBuildingApproachPairs(
            AttackCommandScope command, IntPtr pathManager)
        {
            if (command == null || !IsBuildingAttackCommand(command.Command))
                return;

            command.PublishedBuildingApproaches.Clear();
            foreach (BuildingApproachCandidate candidate in
                CaptureBuildingApproachCandidates(pathManager))
            {
                if (candidate.ApproachTileId <= 0 || candidate.FootprintTileId <= 0)
                    break;
                if (!command.PublishedBuildingApproaches.TryGetValue(
                        candidate.ApproachTileId, out HashSet<int> footprintTiles))
                {
                    footprintTiles = new HashSet<int>();
                    command.PublishedBuildingApproaches[candidate.ApproachTileId] = footprintTiles;
                }
                footprintTiles.Add(candidate.FootprintTileId);
            }
        }

        private void PublishUnitAttackApproachTiles(
            AttackCommandScope command,
            IntPtr pathManager)
        {
            if (command == null || command.Command != TribeAICommand.AttackUnit ||
                command.MapEpoch != mapEpoch || pathManager == IntPtr.Zero)
            {
                return;
            }

            command.PublishedUnitAttackApproaches.Clear();
            byte* manager = (byte*)pathManager.ToPointer();
            for (int index = 0; index < VanillaAttackFloodResultCapacity; index++)
            {
                byte* entry = manager + PathManagerFloodResultTileOffset +
                    index * PathManagerFloodResultStride;
                int tileId = *(int*)entry;
                int usableForAttack = *(int*)(entry + 4);
                if (tileId == 0 && usableForAttack == 0)
                    break;
                if (IsValidTileId(tileId) && usableForAttack != 0)
                    command.PublishedUnitAttackApproaches.Add(tileId);
            }

            LogCommandDiagnostic(
                $"stage=attack-unit-approaches commandSeq={command.Sequence} " +
                $"target={command.TargetValue1}/{command.TargetValue2} " +
                $"published={command.PublishedUnitAttackApproaches.Count}");
        }

        private static bool TryGetPublishedBuildingFootprint(
            Dictionary<int, HashSet<int>> approaches,
            int approachTileId,
            out int footprintTileId)
        {
            footprintTileId = -1;
            if (approaches == null ||
                !approaches.TryGetValue(approachTileId, out HashSet<int> footprintTiles))
            {
                return false;
            }
            foreach (int candidate in footprintTiles)
            {
                footprintTileId = candidate;
                return true;
            }
            return false;
        }

        private static bool HasPublishedBuildingApproachPair(
            Dictionary<int, HashSet<int>> approaches,
            int approachTileId,
            int footprintTileId) =>
            approaches != null &&
            approaches.TryGetValue(approachTileId, out HashSet<int> footprintTiles) &&
            footprintTiles.Contains(footprintTileId);

        private void LogBuildingConsumerCandidates(
            AttackApproachDiagnosticScope scope,
            BuildingApproachCandidate[] before,
            AttackApproachState vanillaAfter,
            BuildingConsumerFallbackResult fallback)
        {
            int beforeUsable = 0;
            int beforeMalformed = 0;
            foreach (BuildingApproachCandidate candidate in before)
            {
                if (candidate.ApproachTileId > 0 && candidate.FootprintTileId > 0)
                    beforeUsable++;
                else
                    beforeMalformed++;
            }
            LogCommandDiagnostic(
                $"stage=building-consumer-candidates commandSeq={scope.CommandSequence} " +
                $"building={scope.OwnerCommand.TargetValue1}/{scope.OwnerCommand.TargetValue2} " +
                $"beforeRaw={before.Length} beforeUsable={beforeUsable} " +
                $"beforeMalformed={beforeMalformed} vanillaRaw={vanillaAfter.ResultCount} " +
                $"vanillaUsable={vanillaAfter.UsableResultCount} " +
                $"vanillaMalformed={vanillaAfter.MalformedResultCount} " +
                $"finalUsable={scope.After.UsableResultCount} " +
                $"finalFirst={scope.After.FirstResultTile}/" +
                $"{scope.After.FirstCompanionTile}/{scope.After.FirstScore}.");
            LogCommandDiagnostic(
                $"stage=building-consumer-fallback commandSeq={scope.CommandSequence} " +
                $"building={scope.OwnerCommand.TargetValue1}/{scope.OwnerCommand.TargetValue2} " +
                $"applied={fallback.WasApplied} reason={fallback.Reason} " +
                $"diggers={fallback.DiggerUnits} published={fallback.PublishedCandidates} " +
                $"walkableReservations={fallback.WalkableReservations} " +
                $"missingContext={fallback.MissingContexts} " +
                $"invalidContext={fallback.InvalidContexts} " +
                $"reservedBlocked={fallback.ReservedBlocked} " +
                $"ownerRouteRejected={fallback.OwnerRouteRejected} " +
                fallback.Summary.ToLogFields());
        }

        private void LogBuildingConsumerPerformance(
            BuildingConsumerPerformanceScope performance,
            BuildingConsumerFallbackResult fallback)
        {
            if (performance == null)
                return;

            LogCommandDiagnostic(
                $"stage=building-consumer-performance " +
                $"commandSeq={performance.CommandSequence} building={performance.BuildingId} " +
                $"vanillaMs={performance.VanillaMilliseconds:F3} " +
                $"fallbackMs={performance.FallbackMilliseconds:F3} " +
                $"rawCandidates={performance.RawCandidates} " +
                $"validCandidates={performance.ValidCandidates} " +
                $"diggers={performance.DiggerUnits} " +
                $"routeEvaluations={performance.RouteEvaluations} " +
                $"reachabilityMaps={performance.ReachabilityMapsBuilt} " +
                $"reachabilityCacheHits={performance.ReachabilityCacheHits} " +
                $"moatOwnerCache={performance.MoatOwnerCacheHits}/" +
                $"{performance.MoatOwnerCacheMisses} " +
                $"applied={fallback.WasApplied} reason={fallback.Reason}");
        }

        private void LogBuildingApproachPerformance(
            BuildingApproachPerformanceScope performance)
        {
            if (performance == null)
                return;

            double estimatedVanillaMilliseconds = Math.Max(
                0.0,
                performance.TotalMilliseconds - performance.RegionFallbackMilliseconds);
            LogCommandDiagnostic(
                $"stage=building-approach-performance " +
                $"commandSeq={performance.CommandSequence} building={performance.BuildingId} " +
                $"totalMs={performance.TotalMilliseconds:F3} " +
                $"fallbackMs={performance.RegionFallbackMilliseconds:F3} " +
                $"vanillaEstimatedMs={estimatedVanillaMilliseconds:F3} " +
                $"fallbackEvaluations={performance.RegionFallbackEvaluations} " +
                $"routeEvaluations={performance.RouteEvaluations} " +
                $"indexMs={performance.IndexMilliseconds:F3} " +
                $"indexScans={performance.IndexScans} " +
                $"nativeTiles={performance.IndexedNativeTiles} " +
                $"footprintTiles={performance.IndexedFootprintTiles} " +
                $"approachTiles={performance.IndexedApproachTiles} " +
                $"regions={performance.ApproachTilesByRegion?.Count ?? 0} " +
                $"reachabilityMaps={performance.ReachabilityMapsBuilt} " +
                $"reachabilityCacheHits={performance.ReachabilityCacheHits} " +
                $"moatOwnerCache={performance.MoatOwnerCacheHits}/" +
                $"{performance.MoatOwnerCacheMisses}");
        }

        private static AttackApproachState CaptureAttackApproachState(
            IntPtr pathManager, bool requirePairedResult = false)
        {
            if (pathManager == IntPtr.Zero)
                return default;

            byte* manager = (byte*)pathManager.ToPointer();
            int resultCount = 0;
            int usableResultCount = 0;
            int malformedResultCount = 0;
            int firstResultTile = 0;
            int firstCompanionTile = 0;
            int firstScore = 0;
            bool usablePrefix = true;
            for (int index = 0; index < VanillaAttackFloodResultCapacity; index++)
            {
                byte* result = manager + PathManagerFloodResultTileOffset +
                    index * PathManagerFloodResultStride;
                int tileId = *(int*)result;
                int classification = *(int*)(result + 4);
                int score = *(int*)(result + 8);
                if (tileId == 0 && classification == 0)
                    break;
                if (resultCount == 0)
                {
                    firstResultTile = tileId;
                    firstCompanionTile = classification;
                    firstScore = score;
                }
                resultCount++;
                bool usable = tileId > 0 && (!requirePairedResult ||
                    (classification > 0 && score != VanillaUnreachableCandidateScore));
                if (usablePrefix && usable)
                    usableResultCount++;
                else
                    usablePrefix = false;
                if (!usable)
                    malformedResultCount++;
            }

            return new AttackApproachState(
                *(int*)(manager + PathManagerFloodGenerationOffset),
                *(int*)(manager + PathManagerFloodDepthOffset),
                *(int*)(manager + PathManagerFloodQueueHeadOffset),
                *(int*)(manager + PathManagerFloodQueueTailOffset),
                resultCount,
                usableResultCount,
                malformedResultCount,
                firstResultTile,
                firstCompanionTile,
                firstScore);
        }

        private void LogAttackApproachDiagnostic(AttackApproachDiagnosticScope scope)
        {
            if (!scope.OwnerCommand.AttackApproachDiagnosticSignatures.Add(
                scope.GetSemanticSignature()))
            {
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=attack-approach kind={scope.Kind} commandSeq={scope.CommandSequence} " +
                $"command={scope.Command} tribe={scope.TribeId} unit={scope.UnitId} " +
                $"type={scope.UnitType} player={scope.PlayerId} targetContext={scope.TargetContext} " +
                $"target=({scope.TargetX},{scope.TargetY}) requestedResults={scope.RequestedResults} " +
                $"sourceRegion={scope.SourceRegion} movementClass={scope.MovementClass} " +
                $"consumerVariant={scope.ConsumerVariantText} " +
                $"allSelectedAssassins={scope.AllSelectedAssassinsText} " +
                $"before=[{scope.Before.ToLogFields()}] after=[{scope.After.ToLogFields()}] " +
                $"regionPairs={scope.FormatRegionPairs()} tilePairs={scope.FormatTilePairs()}.");
        }

        private void DiagnoseAttackApproachTilePair(
            AttackApproachDiagnosticScope scope,
            int targetTileId,
            int selectedUnitTileId,
            byte useCache,
            int vanillaResult)
        {
            bool ownerRoute = false;
            RouteProbeSummary summary = default;
            int startX = -1;
            int startY = -1;
            int targetX = -1;
            int targetY = -1;
            try
            {
                if (IsValidTileId(selectedUnitTileId) && IsValidTileId(targetTileId))
                {
                    UnmanagedVector2<ushort> start =
                        GameTileManagerAPI.Instance.GetTileVectorFromId(selectedUnitTileId);
                    UnmanagedVector2<ushort> target =
                        GameTileManagerAPI.Instance.GetTileVectorFromId(targetTileId);
                    startX = start.X;
                    startY = start.Y;
                    targetX = target.X;
                    targetY = target.Y;
                    if (scope.UnitId > 0 && scope.PlayerId >= 0)
                    {
                        // Walkability and moat ownership are symmetric here. Starting at the
                        // flood's stable target lets every neighbour in the same region reuse
                        // one reachability map instead of running a full-map BFS per E2CA0 call.
                        int cacheKey = unchecked(
                            (scope.CommandSequence * 397) ^ scope.UnitId ^ (targetTileId << 1));
                        ownerRoute = TryFindFriendlyCompletedMoatRoute(
                            cacheKey,
                            scope.PlayerId,
                            targetX,
                            targetY,
                            startX,
                            startY,
                            out summary);
                    }
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("attack-approach-tile-pair-probe", ex);
            }

            scope.ObserveTilePair(
                targetTileId,
                selectedUnitTileId,
                IsValidTileId(selectedUnitTileId) ? pathRegionGrid[selectedUnitTileId] : 0,
                IsValidTileId(targetTileId) ? pathRegionGrid[targetTileId] : 0,
                startX,
                startY,
                targetX,
                targetY,
                useCache,
                vanillaResult,
                ownerRoute,
                summary);
        }

        private int AllowBuildingCursorThroughCompletedMoat(
            IntPtr buildingManager,
            int buildingId,
            int unitId)
        {
            int vanillaResult = originalBuildingCursorReachability(
                buildingManager, buildingId, unitId);
            if (disposed)
                return vanillaResult;

            try
            {
                string reason = "vanilla-positive";
                int effectiveResult = vanillaResult;
                int playerId = -1;
                int targetX = -1;
                int targetY = -1;
                int targetTileId = -1;
                BuildingCursorTarget target = default;
                CursorGroupRouteSummary group = default;
                bool groupEvaluated = false;
                uint rawHoverBuildingTileId = 0;
                uint rawMouseTileId2 = 0;
                uint rawMouseTileId = 0;
                int rawMouseX = -1;
                int rawMouseY = -1;

                if (vanillaResult == 0)
                {
                    if (buildingId <= 0 || unitId <= 0 ||
                        !GameUnitManagerAPI.Instance.TryGetUnitById(
                            unitId, out GameUnit* unit) || unit == null ||
                        unit->r_AliveState != AliveState.IsAlive)
                    {
                        reason = "invalid-unit-or-building-id";
                    }
                    else
                    {
                        playerId = unit->r_ControllableForPlayerId;
                        GameCursorManager* cursorManager =
                            GamePlayerManagerAPI.Instance.GetCursorManager().Pointer;
                        uint rawBuildingId = cursorManager != null
                            ? cursorManager->r_HoverOverBuildingId : 0;
                        rawHoverBuildingTileId = cursorManager != null
                            ? cursorManager->r_HoverOverBuildingTileId : 0;
                        rawMouseTileId2 = cursorManager != null
                            ? cursorManager->r_MouseTileId2 : 0;
                        rawMouseTileId = cursorManager != null
                            ? cursorManager->r_MouseTileId : 0;
                        rawMouseX = cursorManager != null
                            ? unchecked((int)cursorManager->r_MouseTileX) : -1;
                        rawMouseY = cursorManager != null
                            ? unchecked((int)cursorManager->r_MouseTileY) : -1;
                        if (rawBuildingId != unchecked((uint)buildingId) ||
                            !TryResolveHostileLivingBuildingFromRawCursor(
                                playerId,
                                rawBuildingId,
                                rawHoverBuildingTileId,
                                rawMouseTileId2,
                                rawMouseTileId,
                                rawMouseX,
                                rawMouseY,
                                out targetX,
                                out targetY,
                                out targetTileId,
                                out target) ||
                            target.BuildingId != buildingId)
                        {
                            reason = "hover-building-not-exact-hostile-target";
                        }
                        else
                        {
                            int startX = unit->r_CurrentTilePositionX;
                            int startY = unit->r_CurrentTilePositionY;
                            int startTileId = startX >= 0 && startX < MapWidth &&
                                startY >= 0 && startY < MapWidth
                                ? GameTileManagerAPI.Instance.GetTileId(startX, startY)
                                : -1;
                            AttackCursorPairScope template = IsValidTileId(startTileId)
                                ? new AttackCursorPairScope(
                                    mapEpoch,
                                    unitId,
                                    playerId,
                                    startX,
                                    startY,
                                    startTileId,
                                    targetX,
                                    targetY,
                                    targetTileId,
                                    CursorPairFallbackKind.BuildingApproach,
                                    target.BuildingId,
                                    target.GlobalId,
                                    target.OwnerId,
                                    target.BuildingType,
                                    target.HoverTileId)
                                : null;
                            groupEvaluated = TryQualifySelectedGroupCursorRoute(
                                template, out _, out group);
                            if (groupEvaluated && group.AllowFallback &&
                                group.DiggerUnits > 0)
                            {
                                effectiveResult = 1;
                                reason = "required-friendly-moat-building-route";
                            }
                            else
                            {
                                reason = groupEvaluated
                                    ? "no-legal-moat-relevant-group-route"
                                    : "group-route-not-evaluable";
                            }
                        }
                    }
                }

                string key = $"{mapEpoch}:{buildingId}:{unitId}:{playerId}:" +
                    $"{target.GlobalId}:{rawHoverBuildingTileId}:{rawMouseTileId2}:" +
                    $"{rawMouseTileId}:{targetTileId}:{target.HoverTileSource}:" +
                    $"{group.SelectionSignature}:" +
                    $"{group.SelectedUnits}:{group.DiggerUnits}:" +
                    $"{group.LegallyReachableUnits}:{group.FriendlyMoatSeparatedUnits}:" +
                    $"{vanillaResult}:{effectiveResult}:{reason}";
                if (loggedBuildingCursorReachabilityDecisions.Add(key))
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=building-cursor-reachability " +
                        $"building={buildingId}/{target.GlobalId} type={target.BuildingType} " +
                        $"owner={target.OwnerId} unit={unitId} player={playerId} " +
                        $"hoverRaw=buildingTile:{rawHoverBuildingTileId}/" +
                        $"mouse2:{rawMouseTileId2}/mouse:{rawMouseTileId}/" +
                        $"xy:({rawMouseX},{rawMouseY}) " +
                        $"hoverResolved=({targetX},{targetY})/{targetTileId} " +
                        $"hoverTileSource={FormatBuildingHoverTileSource(target.HoverTileSource)} " +
                        $"groupEvaluated={groupEvaluated} selected={group.SelectedUnits} " +
                        $"diggers={group.DiggerUnits} legal={group.LegallyReachableUnits} " +
                        $"friendlyMoatSeparated={group.FriendlyMoatSeparatedUnits} " +
                        $"vanilla={vanillaResult} effective={effectiveResult} reason={reason}.");
                }

                return effectiveResult;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("building-cursor-reachability", ex);
                return vanillaResult;
            }
        }

        private int ObserveCursorTilePairFallbackSelection(IntPtr selectionState)
        {
            int vanillaResult = originalCursorTilePairFallbackSelection(selectionState);
            pendingAttackCursorPair = null;
            pendingCursorSelectionDiagnostic = null;
            if (disposed || selectionState == IntPtr.Zero)
                return vanillaResult;

            try
            {
                byte* state = (byte*)selectionState.ToPointer();
                int* slots = (int*)(state + 0x564);
                ulong occupiedSlots = 0;
                for (int index = 0; index < 35; index++)
                {
                    if (slots[index] == 0)
                        continue;

                    occupiedSlots |= 1UL << index;
                }
                bool hasVanillaDiggerSelection = selectionCanDigMoat(selectionState) != 0;

                int unitId = getRepresentativeSelectedUnit(selectionState, 1);
                int nextUnitId = *(int*)nativeUnitManager;
                int targetX = *cursorTargetX;
                int targetY = *cursorTargetY;
                int playerId = -1;
                int startX = -1;
                int startY = -1;
                int startTileId = -1;
                int targetTileId = -1;
                string rejectionReason;
                GameCursorManager* cursorManager =
                    GamePlayerManagerAPI.Instance.GetCursorManager().Pointer;
                uint rawHoverBuildingId = cursorManager != null
                    ? cursorManager->r_HoverOverBuildingId : 0;
                uint rawHoverUnitId = cursorManager != null
                    ? cursorManager->r_HoverOverUnitId : 0;
                uint rawHoverBuildingTileId = cursorManager != null
                    ? cursorManager->r_HoverOverBuildingTileId : 0;
                uint rawMouseTileId2 = cursorManager != null
                    ? cursorManager->r_MouseTileId2 : 0;
                uint rawHoveringOverWall = cursorManager != null
                    ? cursorManager->r_HoveringOverWall : 0;
                uint rawMouseTileId = cursorManager != null
                    ? cursorManager->r_MouseTileId : 0;
                int rawMouseX = cursorManager != null
                    ? unchecked((int)cursorManager->r_MouseTileX) : -1;
                int rawMouseY = cursorManager != null
                    ? unchecked((int)cursorManager->r_MouseTileY) : -1;

                BuildingCursorTarget buildingTarget = default;
                bool hostileBuildingTarget = false;
                bool wallTarget = false;
                bool validTarget = targetX >= 0 && targetX < MapWidth &&
                    targetY >= 0 && targetY < MapWidth;
                GameUnit* unit = null;
                bool validUnit = unitId > 0 && unitId < nextUnitId &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out unit) && unit != null;
                int representativePlayerId = validUnit ? unit->r_ControllableForPlayerId : -1;
                if (hasVanillaDiggerSelection && (!validUnit || !CanDigMoat(unit)) &&
                    TryGetSelectedVanillaDigger(
                        unitId, representativePlayerId, out int diggerUnitId, out GameUnit* diggerUnit))
                {
                    unitId = diggerUnitId;
                    unit = diggerUnit;
                    validUnit = true;
                }
                if (validUnit)
                {
                    playerId = unit->r_ControllableForPlayerId;
                    startX = unit->r_CurrentTilePositionX;
                    startY = unit->r_CurrentTilePositionY;
                    startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                }
                if (validTarget)
                    targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);

                // Sprite overhangs can leave the dispatcher's global target at (0,0), although
                // Vanilla still reports the exact building ID. Bind such hovers to a verified
                // StructureGrid tile; the building approach probe still enumerates the footprint.
                if (validUnit && (!validTarget || !IsValidTileId(targetTileId)) &&
                    TryResolveHostileLivingBuildingFromRawCursor(
                        playerId,
                        rawHoverBuildingId,
                        rawHoverBuildingTileId,
                        rawMouseTileId2,
                        rawMouseTileId,
                        rawMouseX,
                        rawMouseY,
                        out int recoveredTargetX,
                        out int recoveredTargetY,
                        out int recoveredTargetTileId,
                        out BuildingCursorTarget recoveredBuilding))
                {
                    targetX = recoveredTargetX;
                    targetY = recoveredTargetY;
                    targetTileId = recoveredTargetTileId;
                    buildingTarget = recoveredBuilding;
                    hostileBuildingTarget = true;
                    validTarget = true;
                }

                int cursorPairTargetTileId = targetTileId;
                bool validPair = validUnit && validTarget &&
                    IsValidTileId(startTileId) && IsValidTileId(targetTileId);
                CursorPairFallbackKind fallbackKind = CursorPairFallbackKind.DirectTile;
                bool hostileUnitTarget = false;
                int hostileUnitId = -1;
                uint hostileUnitGlobalId = 0;
                bool occupiedByLivingUnit = false;
                bool freeOrdinaryTarget = false;
                if (validPair)
                {
                    // Vanilla's sprite hit-test owns entity identity. The tile below a unit
                    // sprite may be adjacent to the unit and is only the cursor-call binding.
                    hostileUnitTarget = TryResolveHostileLivingUnitFromRawCursor(
                        playerId,
                        rawHoverUnitId,
                        out hostileUnitId,
                        out hostileUnitGlobalId,
                        out int resolvedUnitX,
                        out int resolvedUnitY,
                        out int resolvedUnitTileId);
                    if (hostileUnitTarget)
                    {
                        occupiedByLivingUnit = true;
                        targetX = resolvedUnitX;
                        targetY = resolvedUnitY;
                        targetTileId = resolvedUnitTileId;
                    }
                    else
                    {
                        hostileUnitTarget = TryGetHostileLivingUnitAtTile(
                            playerId,
                            targetX,
                            targetY,
                            -1,
                            -1,
                            out hostileUnitId,
                            out occupiedByLivingUnit);
                        if (hostileUnitTarget &&
                            GameUnitManagerAPI.Instance.TryGetUnitById(
                                hostileUnitId, out GameUnit* hostileUnit) && hostileUnit != null)
                        {
                            hostileUnitGlobalId = hostileUnit->r_GlobalId;
                        }
                    }
                    if (!hostileBuildingTarget)
                    {
                        hostileBuildingTarget = TryGetHostileLivingBuildingForCursor(
                            playerId, targetTileId, out buildingTarget, out wallTarget);
                    }
                    // Walls are not reliably represented as living GameBuilding records.
                    // The cursor's dedicated wall field is the authoritative raw signal.
                    wallTarget |= rawHoveringOverWall != 0;
                    int targetCell = (targetY * MapWidth) + targetX;
                    freeOrdinaryTarget = !occupiedByLivingUnit && !hostileBuildingTarget && !wallTarget &&
                        movementTargetAvailability[targetCell] != 0 &&
                        (tileFlags[targetTileId] & OrdinaryWalkableTileFlag) != 0 &&
                        (tileFlags[targetTileId] & CursorSpecialStructureTileFlagMask) == 0;
                    if (hostileBuildingTarget)
                        fallbackKind = CursorPairFallbackKind.BuildingApproach;
                    else if (hostileUnitTarget)
                        fallbackKind = CursorPairFallbackKind.UnitApproach;
                }

                AttackCursorPairScope candidateScope = validPair &&
                    (freeOrdinaryTarget || hostileUnitTarget || hostileBuildingTarget)
                    ? new AttackCursorPairScope(
                        mapEpoch, unitId, playerId, startX, startY, startTileId,
                        targetX, targetY, targetTileId, fallbackKind,
                        buildingTarget.BuildingId, buildingTarget.GlobalId,
                        buildingTarget.OwnerId, buildingTarget.BuildingType,
                        buildingTarget.HoverTileId)
                    : null;
                if (candidateScope != null && hostileUnitTarget)
                {
                    candidateScope.TargetUnitId = hostileUnitId;
                    candidateScope.TargetUnitGlobalId = hostileUnitGlobalId;
                    candidateScope.CursorPairTargetTileId = cursorPairTargetTileId;
                }
                CursorGroupRouteSummary groupRoute = default;
                bool groupRouteEvaluated = false;
                bool ownerRoute;
                bool dedicatedBuildingReachability =
                    fallbackKind == CursorPairFallbackKind.BuildingApproach;
                if (vanillaResult == 0 && candidateScope != null &&
                    hasVanillaDiggerSelection &&
                    !dedicatedBuildingReachability)
                {
                    groupRouteEvaluated = TryQualifySelectedGroupCursorRoute(
                        candidateScope, out AttackCursorPairScope groupScope, out groupRoute);
                    ownerRoute = groupRouteEvaluated && groupRoute.AllowFallback;
                    if (ownerRoute)
                        candidateScope = groupScope;
                    LogCursorGroupRoute(candidateScope, groupRouteEvaluated, groupRoute, ownerRoute);
                }
                else if (dedicatedBuildingReachability)
                {
                    // B70C0 owns normal-building approach enumeration. Arming E2CA0 here is
                    // reentrant: B70C0 calls this selection helper again and replaces the scope.
                    ownerRoute = false;
                }
                else
                {
                    ownerRoute = candidateScope != null &&
                        TryQualifyCursorScope(candidateScope, out _, out _, out _);
                }
                bool functionalArmed = candidateScope != null && ownerRoute &&
                    hasVanillaDiggerSelection;
                if (!validUnit)
                    rejectionReason = "invalid-representative-unit";
                else if (!validTarget)
                    rejectionReason = "invalid-cursor-target";
                else if (!validPair)
                    rejectionReason = "invalid-tile-pair";
                else if (!hasVanillaDiggerSelection)
                    rejectionReason = "selection-cannot-dig-moat";
                else if (wallTarget)
                    rejectionReason = "wall-or-stair-kept-vanilla";
                else if (dedicatedBuildingReachability)
                    rejectionReason = "building-routed-through-B70C0";
                else if (!freeOrdinaryTarget && !hostileUnitTarget && !hostileBuildingTarget &&
                    !wallTarget)
                    rejectionReason = "target-not-free-or-hostile-entity";
                else if (!ownerRoute)
                    rejectionReason = groupRouteEvaluated
                        ? "no-legal-moat-relevant-group-route"
                        : "no-required-friendly-moat-route";
                else if (vanillaResult != 0)
                    rejectionReason = "vanilla-selection-positive-route-context-only";
                else
                    rejectionReason = "none";

                CursorSelectionDiagnosticScope diagnostic = new CursorSelectionDiagnosticScope(
                    mapEpoch,
                    vanillaResult,
                    unitId,
                    playerId,
                    startX,
                    startY,
                    startTileId,
                    targetX,
                    targetY,
                    targetTileId,
                    occupiedSlots,
                    hasVanillaDiggerSelection,
                    functionalArmed,
                    fallbackKind,
                    rejectionReason,
                    buildingTarget.BuildingId,
                    buildingTarget.GlobalId,
                    buildingTarget.BuildingType,
                    buildingTarget.HoverTileSource,
                    rawHoverBuildingId,
                    rawHoverUnitId,
                    rawHoverBuildingTileId,
                    rawMouseTileId2,
                    rawHoveringOverWall,
                    rawMouseTileId);
                pendingCursorSelectionDiagnostic = diagnostic;
                LogCursorSelectionDiagnostic(diagnostic);

                // A positive selection-gate result is diagnostic only. The functional moat
                // fallback retains its proven Vanilla-zero requirement.
                if (functionalArmed)
                {
                    pendingAttackCursorPair = candidateScope;
                }

                // A positive Vanilla answer is authoritative. Only a Vanilla rejection may be
                // lifted, and only after the route has already passed the owner-aware moat probe.
                if (vanillaResult == 0 && functionalArmed)
                    return 1;
            }
            catch (Exception ex)
            {
                pendingAttackCursorPair = null;
                pendingCursorSelectionDiagnostic = null;
                LogFailure("cursor-selection", ex);
            }

            return vanillaResult;
        }

        private void LogCursorSelectionDiagnostic(CursorSelectionDiagnosticScope diagnostic)
        {
            string signature =
                $"{diagnostic.MapEpoch}:{diagnostic.VanillaSelectionResult}:{diagnostic.UnitId}:" +
                $"{diagnostic.PlayerId}:{diagnostic.StartTileId}:{diagnostic.TargetTileId}:" +
                $"{diagnostic.OccupiedSlots:X}:{diagnostic.HasVanillaDiggerSelection}:" +
                $"{diagnostic.FunctionalFallbackArmed}:{diagnostic.FallbackKind}:" +
                $"{diagnostic.BuildingId}:{diagnostic.BuildingGlobalId}:" +
                $"{diagnostic.BuildingHoverTileSource}:" +
                $"{diagnostic.RawHoverBuildingId}:{diagnostic.RawHoverUnitId}:" +
                $"{diagnostic.RawHoverBuildingTileId}:{diagnostic.RawMouseTileId2}:" +
                $"{diagnostic.RawHoveringOverWall}:{diagnostic.RawMouseTileId}:" +
                $"{diagnostic.RejectionReason}";
            if (string.Equals(lastCursorSelectionDiagnostic, signature, StringComparison.Ordinal))
                return;

            lastCursorSelectionDiagnostic = signature;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=cursor-selection-gate vanilla={diagnostic.VanillaSelectionResult} " +
                $"unit={diagnostic.UnitId} player={diagnostic.PlayerId} " +
                $"start=({diagnostic.StartX},{diagnostic.StartY})/{diagnostic.StartTileId} " +
                $"target=({diagnostic.TargetX},{diagnostic.TargetY})/{diagnostic.TargetTileId} " +
                $"slots=0x{diagnostic.OccupiedSlots:X9} " +
                $"vanillaDiggerSelection={diagnostic.HasVanillaDiggerSelection} " +
                $"fallbackArmed={diagnostic.FunctionalFallbackArmed} " +
                $"fallbackKind={diagnostic.FallbackKind} " +
                $"building={diagnostic.BuildingId}/{diagnostic.BuildingGlobalId}/" +
                $"{diagnostic.BuildingType} hoverTileSource=" +
                $"{FormatBuildingHoverTileSource(diagnostic.BuildingHoverTileSource)} " +
                $"rawCursor=building:{diagnostic.RawHoverBuildingId}/" +
                $"unit:{diagnostic.RawHoverUnitId}/buildingTile:" +
                $"{diagnostic.RawHoverBuildingTileId}/mouse2:{diagnostic.RawMouseTileId2}/" +
                $"wall:{diagnostic.RawHoveringOverWall}/mouse:{diagnostic.RawMouseTileId} " +
                $"reason={diagnostic.RejectionReason}.");
        }

        private void LogCursorTilePairDiagnostic(
            CursorSelectionDiagnosticScope diagnostic,
            AttackCursorPairScope functionalScope,
            int actualTargetTileId,
            int actualSelectedUnitTileId,
            byte useCache,
            int vanillaTilePairResult)
        {
            bool mapMatches = diagnostic.MapEpoch == mapEpoch;
            bool startMatches = functionalScope != null
                ? CursorStartMatchesBoundSelection(functionalScope, actualSelectedUnitTileId)
                : diagnostic.StartTileId == actualSelectedUnitTileId;
            bool pairMatches = startMatches &&
                (diagnostic.TargetTileId == actualTargetTileId ||
                 (functionalScope != null &&
                  CursorScopeMatchesTargetTile(functionalScope, actualTargetTileId)));
            bool cacheAccepted = useCache == 1;
            bool effectiveFallbackScope = diagnostic.FunctionalFallbackArmed && mapMatches &&
                pairMatches && cacheAccepted && vanillaTilePairResult == 0;
            string reason;
            if (!mapMatches)
                reason = "map-epoch-mismatch";
            else if (!diagnostic.FunctionalFallbackArmed)
                reason = diagnostic.RejectionReason;
            else if (!pairMatches)
                reason = "tile-pair-mismatch";
            else if (!cacheAccepted)
                reason = "cache-mode-not-supported";
            else if (vanillaTilePairResult != 0)
                reason = "vanilla-tile-pair-positive";
            else
                reason = "none";

            string signature =
                $"{diagnostic.MapEpoch}:{diagnostic.VanillaSelectionResult}:{diagnostic.UnitId}:" +
                $"{diagnostic.StartTileId}:{diagnostic.TargetTileId}:{actualSelectedUnitTileId}:" +
                $"{actualTargetTileId}:{useCache}:{vanillaTilePairResult}:{pairMatches}:" +
                $"{effectiveFallbackScope}:{reason}";
            if (string.Equals(lastCursorTilePairDiagnostic, signature, StringComparison.Ordinal))
                return;

            lastCursorTilePairDiagnostic = signature;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=cursor-tile-pair-observed selectionVanilla=" +
                $"{diagnostic.VanillaSelectionResult} tilePairVanilla={vanillaTilePairResult} " +
                $"unit={diagnostic.UnitId} expected={diagnostic.StartTileId}->{diagnostic.TargetTileId} " +
                $"actual={actualSelectedUnitTileId}->{actualTargetTileId} cache={useCache} " +
                $"pairMatches={pairMatches} fallbackScope={effectiveFallbackScope} reason={reason}.");
        }

        private int AllowAttackCursorTilePairThroughCompletedMoat(
            IntPtr pathManager, int targetTileId, int selectedUnitTileId, byte useCache)
        {
            int vanillaResult = originalCursorTilePairReachability(
                pathManager, targetTileId, selectedUnitTileId, useCache);
            AttackCursorPairScope scope = pendingAttackCursorPair;
            pendingAttackCursorPair = null;
            CursorSelectionDiagnosticScope cursorDiagnostic = pendingCursorSelectionDiagnostic;
            pendingCursorSelectionDiagnostic = null;
            AttackApproachDiagnosticScope attackApproachScope = activeAttackApproachDiagnostic;
            if (attackApproachScope != null)
            {
                try
                {
                    // Read-only: every scoped attack-approach helper receives Vanilla's result unchanged.
                    DiagnoseAttackApproachTilePair(
                        attackApproachScope,
                        targetTileId,
                        selectedUnitTileId,
                        useCache,
                        vanillaResult);
                }
                catch (Exception ex)
                {
                    TryLogDiagnosticFailure("attack-approach-tile-pair", ex);
                }
                return vanillaResult;
            }

            if (cursorDiagnostic != null)
            {
                try
                {
                    LogCursorTilePairDiagnostic(
                        cursorDiagnostic, scope, targetTileId, selectedUnitTileId,
                        useCache, vanillaResult);
                }
                catch (Exception ex)
                {
                    TryLogDiagnosticFailure("cursor-tile-pair-observer", ex);
                }
            }

            if (disposed || vanillaResult != 0)
                return vanillaResult;

            if (scope == null || scope.MapEpoch != mapEpoch || useCache != 1 ||
                scope.FallbackKind == CursorPairFallbackKind.BuildingApproach ||
                !CursorStartMatchesBoundSelection(scope, selectedUnitTileId) ||
                !CursorScopeMatchesTargetTile(scope, targetTileId))
            {
                return vanillaResult;
            }

            try
            {
                bool friendlyRoute;
                int approachX;
                int approachY;
                RouteProbeSummary summary;
                if (scope.GroupCursorAuthorized)
                {
                    approachX = scope.TargetX;
                    approachY = scope.TargetY;
                    summary = default;
                    summary.RouteFound = true;
                    friendlyRoute = true;
                }
                else
                {
                    friendlyRoute = TryQualifyCursorScope(
                        scope, out approachX, out approachY, out summary);
                }
                string decisionKey = $"{scope.MapEpoch}:{scope.UnitId}:{scope.PlayerId}:" +
                    $"{scope.StartTileId}:{scope.TargetTileId}:{friendlyRoute}:" +
                    $"{scope.FallbackKind}:{summary.ObservedOwnerMask}:" +
                    $"{summary.FriendlyMoatTiles}:{summary.EnemyMoatTiles}";
                if (!string.Equals(lastAttackCursorDecision, decisionKey, StringComparison.Ordinal))
                {
                    lastAttackCursorDecision = decisionKey;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=attack-cursor-pair unit={scope.UnitId} player={scope.PlayerId} " +
                        $"start=({scope.StartX},{scope.StartY})/{scope.StartTileId} " +
                        $"target=({scope.TargetX},{scope.TargetY})/{scope.TargetTileId} " +
                        $"kind={scope.FallbackKind} approach=({approachX},{approachY}) vanilla=0 " +
                        $"effective={(friendlyRoute ? 1 : 0)} cache={useCache} " +
                        $"actualTarget={targetTileId} {summary.ToLogFields()}.");
                }

                return friendlyRoute ? 1 : vanillaResult;
            }
            catch (Exception ex)
            {
                LogFailure("attack-cursor-pair", ex);
                return vanillaResult;
            }
        }

        private int AllowCursorRegionThroughCompletedMoat(IntPtr pathManager, int nativeUnitIndex)
        {
            int vanillaResult = originalCursorRegionPrecheck(pathManager, nativeUnitIndex);
            if (disposed)
                return vanillaResult;

            try
            {
                int targetX = *cursorTargetX;
                int targetY = *cursorTargetY;
                if (TryAllowScopedGroupCursorPrecheck(
                    targetX, targetY, vanillaResult, out int groupedResult))
                {
                    return groupedResult;
                }
                bool probeCompleted = TryProbeConservativeCursorRoute(
                    nativeUnitIndex, targetX, targetY,
                    out bool ownerSafeRoute, out bool requiredFriendlyMoatRoute,
                    out RouteProbeSummary summary);
                if (vanillaResult != 0)
                {
                    if (probeCompleted && ShouldBlockPositiveCursorResult(ownerSafeRoute, summary))
                    {
                        LogCursorOwnerBlockDecision(
                            ref lastCursorRegionBlockGeneration,
                            $"stage=cursor-region-owner-block unitIndex={nativeUnitIndex} " +
                            $"target=({targetX},{targetY}) vanilla={vanillaResult} effective=0 " +
                            summary.ToLogFields());
                        return 0;
                    }

                    return vanillaResult;
                }

                if (!probeCompleted || !requiredFriendlyMoatRoute)
                    return vanillaResult;

                LogPositiveCursorDecision(
                    ref lastCursorRegionPositiveGeneration,
                    $"stage=cursor-region unitIndex={nativeUnitIndex} target=({targetX},{targetY}) " +
                    $"vanilla=0 effective=1 {summary.ToLogFields()}");
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("cursor-region", ex);
                return vanillaResult;
            }
        }

        private bool TryAllowScopedGroupCursorPrecheck(
            int targetX,
            int targetY,
            int vanillaResult,
            out int effectiveResult)
        {
            effectiveResult = vanillaResult;
            AttackCursorPairScope scope = pendingAttackCursorPair;
            if (vanillaResult != 0 || scope == null || !scope.GroupCursorAuthorized ||
                scope.MapEpoch != mapEpoch ||
                !CursorScopeMatchesDispatcherTarget(scope, targetX, targetY) ||
                string.IsNullOrEmpty(scope.GroupSelectionSignature) ||
                !TryCaptureSelectedGroup(
                    scope.PlayerId, out _, out string currentSelectionSignature) ||
                !string.Equals(
                    scope.GroupSelectionSignature, currentSelectionSignature,
                    StringComparison.Ordinal))
            {
                return false;
            }

            effectiveResult = 1;
            return true;
        }

        private bool CursorScopeMatchesDispatcherTarget(
            AttackCursorPairScope scope, int targetX, int targetY)
        {
            if (scope == null || targetX < 0 || targetX >= MapWidth ||
                targetY < 0 || targetY >= MapWidth)
            {
                return false;
            }

            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            return scope.FallbackKind == CursorPairFallbackKind.UnitApproach
                ? (scope.CursorPairTargetTileId == targetTileId ||
                   scope.TargetTileId == targetTileId)
                : scope.TargetTileId == targetTileId;
        }

        private int AllowCursorReachabilityThroughCompletedMoat(
            IntPtr pathManager, int nativeUnitIndex, int targetX, int targetY)
        {
            int vanillaResult = originalCursorReachability(pathManager, nativeUnitIndex, targetX, targetY);
            if (disposed)
                return vanillaResult;

            try
            {
                if (TryAllowScopedGroupCursorPrecheck(
                    targetX, targetY, vanillaResult, out int groupedResult))
                {
                    return groupedResult;
                }
                bool probeCompleted = TryProbeConservativeCursorRoute(
                    nativeUnitIndex, targetX, targetY,
                    out bool ownerSafeRoute, out bool requiredFriendlyMoatRoute,
                    out RouteProbeSummary summary);
                if (vanillaResult != 0)
                {
                    if (probeCompleted && ShouldBlockPositiveCursorResult(ownerSafeRoute, summary))
                    {
                        LogCursorOwnerBlockDecision(
                            ref lastCursorDirectBlockGeneration,
                            $"stage=cursor-direct-owner-block unitIndex={nativeUnitIndex} " +
                            $"target=({targetX},{targetY}) vanilla={vanillaResult} effective=0 " +
                            summary.ToLogFields());
                        return 0;
                    }

                    return vanillaResult;
                }

                if (!probeCompleted || !requiredFriendlyMoatRoute)
                    return vanillaResult;

                LogPositiveCursorDecision(
                    ref lastCursorDirectPositiveGeneration,
                    $"stage=cursor-direct unitIndex={nativeUnitIndex} target=({targetX},{targetY}) " +
                    $"vanilla=0 effective=1 {summary.ToLogFields()}");
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("cursor-direct", ex);
                return vanillaResult;
            }
        }

        private static bool ShouldBlockPositiveCursorResult(
            bool ownerSafeRoute, RouteProbeSummary summary) =>
            !ownerSafeRoute &&
            (summary.FriendlyMoatTiles > 0 || summary.EnemyMoatTiles > 0);

        private bool TryProbeConservativeCursorRoute(
            int nativeUnitIndex,
            int targetX,
            int targetY,
            out bool ownerSafeRoute,
            out bool requiredFriendlyMoatRoute,
            out RouteProbeSummary summary)
        {
            ownerSafeRoute = false;
            requiredFriendlyMoatRoute = false;
            summary = default;
            if (targetX < 0 || targetX >= MapWidth ||
                targetY < 0 || targetY >= MapWidth ||
                movementTargetAvailability[(targetY * MapWidth) + targetX] == 0)
            {
                return false;
            }

            int nextUnitId = *(int*)nativeUnitManager;
            if (nativeUnitIndex <= 0 || nativeUnitIndex >= nextUnitId)
                return false;

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(nativeUnitIndex, out GameUnit* unit) ||
                unit == null)
            {
                return false;
            }

            int routeUnitId = nativeUnitIndex;
            GameUnit* routeUnit = unit;
            int playerId = unit->r_ControllableForPlayerId;
            if (!CanDigMoat(routeUnit) &&
                TryGetSelectedVanillaDigger(
                    nativeUnitIndex, playerId, out int selectedDiggerId, out GameUnit* selectedDigger))
            {
                routeUnitId = selectedDiggerId;
                routeUnit = selectedDigger;
            }

            int startX = routeUnit->r_CurrentTilePositionX;
            int startY = routeUnit->r_CurrentTilePositionY;
            if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                return false;

            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            if (!IsValidTileId(targetTileId) ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
            {
                return false;
            }

            if (pathRegionGrid[targetTileId] <= 0 ||
                pathRegionGrid[targetTileId] > MaximumRegionId)
            {
                return false;
            }

            TryFindFriendlyCompletedMoatRoute(
                routeUnitId,
                playerId,
                startX,
                startY,
                targetX,
                targetY,
                out summary);
            int targetCell = (targetY * MapWidth) + targetX;
            bool reachedWithoutMoat = gridGeneration > 0 && visitedWithoutMoat != null &&
                visitedWithoutMoat[targetCell] == gridGeneration;
            bool reachedWithMoat = gridGeneration > 0 && visitedWithMoat != null &&
                visitedWithMoat[targetCell] == gridGeneration;
            bool canDigMoat = CanDigMoat(routeUnit);
            ownerSafeRoute = reachedWithoutMoat || (canDigMoat && reachedWithMoat);
            requiredFriendlyMoatRoute = canDigMoat && reachedWithMoat && !reachedWithoutMoat &&
                summary.FriendlyMoatTiles > 0;
            if ((summary.FriendlyMoatTiles > 0 || summary.EnemyMoatTiles > 0) &&
                !reachedWithoutMoat)
            {
                LogDiggerDecision("cursor", routeUnitId, routeUnit,
                    targetX, targetY, canDigMoat);
            }
            summary.AttackProbeEvaluated = true;
            summary.ReachedWithMoat = reachedWithMoat;
            summary.ReachedWithoutMoat = reachedWithoutMoat;
            summary.RouteFound = ownerSafeRoute;
            return true;
        }

        private bool TryFindFriendlyCompletedMoatRouteForPlan(
            PlanScope plan, out RouteProbeSummary summary)
        {
            summary = default;
            if (plan == null || plan.TargetX < 0 || plan.TargetY < 0 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) ||
                unit == null || !CanDigMoat(unit))
            {
                return false;
            }

            int playerId = unit->r_ControllableForPlayerId;
            plan.PlayerId = playerId;
            return TryFindFriendlyCompletedMoatRoute(
                plan.UnitId,
                playerId,
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY,
                plan.TargetX,
                plan.TargetY,
                out summary);
        }

        private bool TryFindRequiredFriendlyCompletedMoatRouteForPlan(
            PlanScope plan, out RouteProbeSummary summary)
        {
            summary = default;
            if (plan == null || plan.TargetX < 0 || plan.TargetX >= MapWidth ||
                plan.TargetY < 0 || plan.TargetY >= MapWidth ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) ||
                unit == null)
            {
                return false;
            }

            bool reachedTargetWithMoat = TryFindFriendlyCompletedMoatRouteForPlan(plan, out summary);
            if (!reachedTargetWithMoat)
                return false;
            int targetCell = (plan.TargetY * MapWidth) + plan.TargetX;
            bool reachedWithMoat = gridGeneration > 0 &&
                visitedWithMoat != null && visitedWithMoat[targetCell] == gridGeneration;
            bool reachedWithoutMoat = gridGeneration > 0 &&
                visitedWithoutMoat != null && visitedWithoutMoat[targetCell] == gridGeneration;
            int groundStartTileId = GameTileManagerAPI.Instance.GetTileId(
                unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
            bool startOnCompletedMoat = IsValidTileId(groundStartTileId) &&
                (tileFlags[groundStartTileId] & CompletedMoatTileFlag) != 0;
            bool regionTopologyQualified =
                (summary.StartRegion > 0 && summary.StartRegion != summary.TargetRegion) ||
                (summary.StartRegion == 0 && startOnCompletedMoat);

            summary.AttackProbeEvaluated = true;
            summary.ReachedWithMoat = reachedWithMoat;
            summary.ReachedWithoutMoat = reachedWithoutMoat;
            summary.RegionTopologyQualified = regionTopologyQualified;
            summary.RouteFound = reachedWithMoat && !reachedWithoutMoat &&
                summary.FriendlyMoatTiles > 0;
            if (summary.RouteFound)
            {
                LogDiggerDecision("route", plan.UnitId, unit,
                    plan.TargetX, plan.TargetY, true, friendlyMoatRequired: true);
            }
            return summary.RouteFound;
        }

        private bool TryFindRequiredFriendlyCompletedMoatRouteToEndpoint(
            PlanScope plan,
            int endpointTileId,
            bool requireBuildingReservation,
            out RouteProbeSummary summary,
            out int distance)
        {
            distance = int.MaxValue;
            if (TryFindRequiredFriendlyCompletedMoatRouteForPlan(plan, out summary))
            {
                int targetCell = plan.TargetY * MapWidth + plan.TargetX;
                distance = distanceWithMoat[targetCell];
                return true;
            }
            if (plan == null || !IsValidTileId(endpointTileId) ||
                (requireBuildingReservation &&
                 GameTileManagerAPI.Instance.GetTileBuildingId(endpointTileId) == 0))
            {
                return false;
            }

            // DA590 expands edges from the source tile. A Vanilla-published endpoint can have
            // no outgoing mask of its own (notably an occupied UnitFlood attack position); only
            // an owner-qualified neighbour's outgoing edge is required for the final step.
            RouteProbeSummary observed = summary;
            bool found = false;
            int bestDistance = int.MaxValue;
            RouteProbeSummary bestSummary = default;
            for (int index = 0; index < EndpointNeighbourX.Length; index++)
            {
                int neighbourX = plan.TargetX + EndpointNeighbourX[index];
                int neighbourY = plan.TargetY + EndpointNeighbourY[index];
                if (neighbourX < 0 || neighbourX >= MapWidth ||
                    neighbourY < 0 || neighbourY >= MapWidth)
                {
                    continue;
                }

                int neighbourTileId = GameTileManagerAPI.Instance.GetTileId(neighbourX, neighbourY);
                if (!IsValidTileId(neighbourTileId) ||
                    (nativeMovementMasks[neighbourTileId] & EndpointSourceEdgeMasks[index]) == 0)
                {
                    continue;
                }

                PlanScope neighbourPlan = new PlanScope(plan.UnitId, neighbourX, neighbourY);
                if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        neighbourPlan, out RouteProbeSummary neighbourSummary))
                {
                    observed.MergeObservations(neighbourSummary);
                    continue;
                }

                neighbourSummary.TargetRegion = pathRegionGrid[endpointTileId];
                neighbourSummary.RouteFound = true;
                neighbourSummary.ReachedWithMoat = true;
                neighbourSummary.ReachedWithoutMoat = false;
                observed.MergeObservations(neighbourSummary);
                int neighbourCell = neighbourY * MapWidth + neighbourX;
                int candidateDistance = distanceWithMoat[neighbourCell] + 1;
                if (candidateDistance < bestDistance)
                {
                    found = true;
                    bestDistance = candidateDistance;
                    bestSummary = neighbourSummary;
                }
            }

            summary = observed;
            if (!found)
                return false;
            summary.MergeObservations(bestSummary);
            distance = bestDistance;
            return true;
        }

        private bool TryFindFriendlyCompletedMoatRoute(
            int cacheKey,
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY,
            out RouteProbeSummary summary)
        {
            summary = default;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(playerId) ||
                targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth ||
                startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth ||
                movementTargetAvailability[(targetY * MapWidth) + targetX] == 0)
            {
                return false;
            }

            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            if (!IsValidTileId(targetTileId))
                return false;

            int targetRegion = pathRegionGrid[targetTileId];
            if (targetRegion <= 0 || targetRegion > MaximumRegionId)
                return false;

            EnsureReachabilityMap(
                cacheKey, playerId, tileManager, playerApi, startX, startY, targetRegion);
            summary = cachedRouteSummary;
            summary.RouteFound = visitedWithMoat[(targetY * MapWidth) + targetX] == gridGeneration;
            return summary.RouteFound;
        }

        private bool TryFindRequiredFriendlyCompletedMoatRouteToDirectTile(
            AttackCursorPairScope scope,
            out RouteProbeSummary summary)
        {
            summary = default;
            if (scope == null || scope.FallbackKind != CursorPairFallbackKind.DirectTile ||
                !IsValidTileId(scope.TargetTileId))
            {
                return false;
            }

            int targetCell = (scope.TargetY * MapWidth) + scope.TargetX;
            if (movementTargetAvailability[targetCell] == 0 ||
                (tileFlags[scope.TargetTileId] & OrdinaryWalkableTileFlag) == 0 ||
                (tileFlags[scope.TargetTileId] & CursorSpecialStructureTileFlagMask) != 0)
            {
                return false;
            }

            bool found = TryFindFriendlyCompletedMoatRoute(
                scope.UnitId,
                scope.PlayerId,
                scope.StartX,
                scope.StartY,
                scope.TargetX,
                scope.TargetY,
                out summary);
            if (!found)
                return false;

            bool reachedWithMoat = visitedWithMoat[targetCell] == gridGeneration;
            bool reachedWithoutMoat = visitedWithoutMoat[targetCell] == gridGeneration;
            bool startOnCompletedMoat = IsValidTileId(scope.StartTileId) &&
                (tileFlags[scope.StartTileId] & CompletedMoatTileFlag) != 0;
            bool regionTopologyQualified =
                (summary.StartRegion > 0 && summary.StartRegion != summary.TargetRegion) ||
                (summary.StartRegion == 0 && startOnCompletedMoat);
            summary.AttackProbeEvaluated = true;
            summary.ReachedWithMoat = reachedWithMoat;
            summary.ReachedWithoutMoat = reachedWithoutMoat;
            summary.RegionTopologyQualified = regionTopologyQualified;
            summary.RouteFound = reachedWithMoat && !reachedWithoutMoat &&
                summary.FriendlyMoatTiles > 0;
            return summary.RouteFound;
        }

        private bool TryQualifySelectedGroupCursorRoute(
            AttackCursorPairScope template,
            out AttackCursorPairScope boundScope,
            out CursorGroupRouteSummary group)
        {
            boundScope = null;
            group = default;
            if (template == null ||
                (template.FallbackKind != CursorPairFallbackKind.DirectTile &&
                 template.FallbackKind != CursorPairFallbackKind.UnitApproach &&
                 template.FallbackKind != CursorPairFallbackKind.BuildingApproach) ||
                !TryCaptureSelectedGroup(
                    template.PlayerId, out int[] selectedUnitIds, out string selectionSignature))
            {
                return false;
            }

            group.SelectionSignature = selectionSignature;
            string targetSignature = template.FallbackKind == CursorPairFallbackKind.BuildingApproach
                ? $"building:{template.BuildingId}:{template.BuildingGlobalId}:{template.BuildingType}"
                : $"tile:{template.TargetTileId}:unit:{template.TargetUnitId}:" +
                  $"{template.TargetUnitGlobalId}";
            string groupCacheKey = $"{mapEpoch}:{template.PlayerId}:{template.FallbackKind}:" +
                $"{targetSignature}:{selectionSignature}";
            if (string.Equals(cursorGroupRouteCacheKey, groupCacheKey, StringComparison.Ordinal))
            {
                group = cachedCursorGroupRoute;
                if (group.AllowFallback && group.RepresentativeUnitId > 0 &&
                    IsValidTileId(group.RepresentativeStartTileId))
                {
                    boundScope = CreateCursorScopeForSnapshot(
                        template,
                        new SelectedCursorUnitSnapshot(
                            group.RepresentativeUnitId,
                            group.RepresentativeStartX,
                            group.RepresentativeStartY,
                            group.RepresentativeStartTileId,
                            group.RepresentativeCanDig));
                    boundScope.GroupCursorAuthorized = true;
                    boundScope.GroupSelectionSignature = group.SelectionSignature;
                }
                return group.SelectedUnits > 0;
            }

            Dictionary<int, List<SelectedCursorUnitSnapshot>> unitsByStartRegion =
                new Dictionary<int, List<SelectedCursorUnitSnapshot>>();
            for (int index = 0; index < selectedUnitIds.Length; index++)
            {
                int selectedUnitId = selectedUnitIds[index];
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        selectedUnitId, out GameUnit* selectedUnit) ||
                    selectedUnit == null || selectedUnit->r_AliveState != AliveState.IsAlive ||
                    selectedUnit->r_ControllableForPlayerId != template.PlayerId)
                {
                    continue;
                }

                int startX = selectedUnit->r_CurrentTilePositionX;
                int startY = selectedUnit->r_CurrentTilePositionY;
                if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                    continue;
                int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                if (!IsValidTileId(startTileId))
                    continue;

                bool canDig = CanDigMoat(selectedUnit);
                group.SelectedUnits++;
                if (canDig)
                    group.DiggerUnits++;

                int startRegion = pathRegionGrid[startTileId];
                int regionKey = startRegion > 0 ? startRegion : -startTileId - 1;
                if (!unitsByStartRegion.TryGetValue(
                    regionKey, out List<SelectedCursorUnitSnapshot> regionUnits))
                {
                    regionUnits = new List<SelectedCursorUnitSnapshot>();
                    unitsByStartRegion.Add(regionKey, regionUnits);
                }
                regionUnits.Add(new SelectedCursorUnitSnapshot(
                    selectedUnitId, startX, startY, startTileId, canDig));
            }

            foreach (KeyValuePair<int, List<SelectedCursorUnitSnapshot>> pair in unitsByStartRegion)
            {
                List<SelectedCursorUnitSnapshot> regionUnits = pair.Value;
                if (regionUnits.Count == 0)
                    continue;
                SelectedCursorUnitSnapshot probeUnit = regionUnits[0];
                AttackCursorPairScope unitScope = CreateCursorScopeForSnapshot(
                    template, probeUnit);
                bool normalReachable;
                bool friendlyMoatSeparated;
                RouteProbeSummary routeSummary;
                bool probed;
                if (template.FallbackKind == CursorPairFallbackKind.DirectTile)
                {
                    probed = TryProbeDirectCursorRoute(
                        unitScope, out normalReachable, out friendlyMoatSeparated,
                        out routeSummary);
                }
                else if (template.FallbackKind == CursorPairFallbackKind.UnitApproach)
                {
                    probed = TryProbeUnitApproachCursorRoute(
                        unitScope, out normalReachable, out friendlyMoatSeparated,
                        out routeSummary);
                }
                else
                {
                    probed = TryProbeBuildingApproachCursorRoute(
                        unitScope, out normalReachable, out friendlyMoatSeparated,
                        out _, out _, out routeSummary);
                }
                if (!probed)
                    continue;

                group.ObservedRoute.MergeObservations(routeSummary);
                if (friendlyMoatSeparated)
                    group.FriendlyMoatSeparatedUnits += regionUnits.Count;
                for (int unitIndex = 0; unitIndex < regionUnits.Count; unitIndex++)
                {
                    SelectedCursorUnitSnapshot member = regionUnits[unitIndex];
                    bool legallyReachable = normalReachable ||
                        (member.CanDig && friendlyMoatSeparated);
                    if (!legallyReachable)
                        continue;

                    group.LegallyReachableUnits++;
                    if (boundScope == null || (member.CanDig && !group.RepresentativeCanDig))
                    {
                        boundScope = CreateCursorScopeForSnapshot(template, member);
                        group.RepresentativeUnitId = member.UnitId;
                        group.RepresentativeStartX = member.StartX;
                        group.RepresentativeStartY = member.StartY;
                        group.RepresentativeStartTileId = member.StartTileId;
                        group.RepresentativeCanDig = member.CanDig;
                    }
                }
            }

            group.AllowFallback = boundScope != null && group.LegallyReachableUnits > 0 &&
                group.FriendlyMoatSeparatedUnits > 0;
            if (group.AllowFallback)
            {
                boundScope.GroupCursorAuthorized = true;
                boundScope.GroupSelectionSignature = group.SelectionSignature;
            }
            cursorGroupRouteCacheKey = groupCacheKey;
            cachedCursorGroupRoute = group;
            return group.SelectedUnits > 0;
        }

        private static AttackCursorPairScope CreateCursorScopeForSnapshot(
            AttackCursorPairScope template, SelectedCursorUnitSnapshot unit)
        {
            AttackCursorPairScope scope = new AttackCursorPairScope(
                template.MapEpoch,
                unit.UnitId,
                template.PlayerId,
                unit.StartX,
                unit.StartY,
                unit.StartTileId,
                template.TargetX,
                template.TargetY,
                template.TargetTileId,
                template.FallbackKind,
                template.BuildingId,
                template.BuildingGlobalId,
                template.BuildingOwnerId,
                template.BuildingType,
                template.HoverBuildingTileId);
            scope.TargetUnitId = template.TargetUnitId;
            scope.TargetUnitGlobalId = template.TargetUnitGlobalId;
            scope.CursorPairTargetTileId = template.CursorPairTargetTileId;
            return scope;
        }

        private bool TryCaptureSelectedGroup(
            int playerId, out int[] selectedUnitIds, out string signature)
        {
            selectedUnitIds = GamePlayerManagerAPI.Instance.GetSelectedChimps();
            if (selectedUnitIds == null || selectedUnitIds.Length == 0)
            {
                signature = string.Empty;
                return false;
            }
            Array.Sort(selectedUnitIds);
            StringBuilder builder = new StringBuilder(selectedUnitIds.Length * 24);
            int validCount = 0;
            for (int index = 0; index < selectedUnitIds.Length; index++)
            {
                int unitId = selectedUnitIds[index];
                if (unitId <= 0 ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_ControllableForPlayerId != playerId)
                {
                    continue;
                }

                int currentX = unit->r_CurrentTilePositionX;
                int currentY = unit->r_CurrentTilePositionY;
                if (currentX < 0 || currentX >= MapWidth ||
                    currentY < 0 || currentY >= MapWidth)
                {
                    continue;
                }

                int tileId = GameTileManagerAPI.Instance.GetTileId(currentX, currentY);
                if (!IsValidTileId(tileId))
                    continue;
                builder.Append(unitId).Append(':')
                    .Append((int)unit->r_UnitChimp).Append(':')
                    .Append(tileId).Append(':')
                    .Append(CanDigMoat(unit) ? '1' : '0').Append(';');
                validCount++;
            }

            signature = builder.ToString();
            return validCount > 0;
        }

        private bool TryProbeDirectCursorRoute(
            AttackCursorPairScope scope,
            out bool normalReachable,
            out bool friendlyMoatSeparated,
            out RouteProbeSummary summary)
        {
            normalReachable = false;
            friendlyMoatSeparated = false;
            summary = default;
            if (scope == null || !IsValidTileId(scope.StartTileId) ||
                !IsValidTileId(scope.TargetTileId) ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(scope.PlayerId) ||
                pathRegionGrid[scope.TargetTileId] <= 0 ||
                pathRegionGrid[scope.TargetTileId] > MaximumRegionId ||
                GameTileManagerAPI.Instance.GetTileManager() == IntPtr.Zero)
            {
                return false;
            }

            int targetCell = scope.TargetY * MapWidth + scope.TargetX;
            if (movementTargetAvailability[targetCell] == 0 ||
                (tileFlags[scope.TargetTileId] & OrdinaryWalkableTileFlag) == 0 ||
                (tileFlags[scope.TargetTileId] & CursorSpecialStructureTileFlagMask) != 0)
            {
                return false;
            }

            int startRegion = pathRegionGrid[scope.StartTileId];
            int targetRegion = pathRegionGrid[scope.TargetTileId];
            if (startRegion > 0 && startRegion == targetRegion)
            {
                summary = new RouteProbeSummary(scope.PlayerId)
                {
                    StartRegion = startRegion,
                    TargetRegion = targetRegion,
                    ReachedWithoutMoat = true,
                    RouteFound = true
                };
                normalReachable = true;
                return true;
            }

            TryFindFriendlyCompletedMoatRoute(
                scope.UnitId, scope.PlayerId, scope.StartX, scope.StartY,
                scope.TargetX, scope.TargetY, out summary);
            normalReachable = gridGeneration > 0 &&
                visitedWithoutMoat[targetCell] == gridGeneration;
            bool reachableWithMoat = gridGeneration > 0 &&
                visitedWithMoat[targetCell] == gridGeneration;
            friendlyMoatSeparated = reachableWithMoat && !normalReachable &&
                summary.FriendlyMoatTiles > 0;
            summary.ReachedWithoutMoat = normalReachable;
            summary.ReachedWithMoat = reachableWithMoat;
            summary.RouteFound = normalReachable || reachableWithMoat;
            return true;
        }

        private bool TryProbeUnitApproachCursorRoute(
            AttackCursorPairScope scope,
            out bool normalReachable,
            out bool friendlyMoatSeparated,
            out RouteProbeSummary summary)
        {
            normalReachable = false;
            friendlyMoatSeparated = false;
            summary = new RouteProbeSummary(scope != null ? scope.PlayerId : -1);
            if (scope == null || scope.FallbackKind != CursorPairFallbackKind.UnitApproach)
                return false;

            bool reachableWithMoat = false;
            bool candidateObserved = false;
            int startRegion = IsValidTileId(scope.StartTileId)
                ? pathRegionGrid[scope.StartTileId]
                : 0;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(scope.PlayerId))
                return false;

            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    if (xOffset == 0 && yOffset == 0)
                        continue;
                    int candidateX = scope.TargetX + xOffset;
                    int candidateY = scope.TargetY + yOffset;
                    if (candidateX < 0 || candidateX >= MapWidth ||
                        candidateY < 0 || candidateY >= MapWidth)
                    {
                        continue;
                    }

                    int candidateCell = candidateY * MapWidth + candidateX;
                    int candidateTileId = GameTileManagerAPI.Instance.GetTileId(candidateX, candidateY);
                    if (!IsValidTileId(candidateTileId) ||
                        movementTargetAvailability[candidateCell] == 0 ||
                        (tileFlags[candidateTileId] & OrdinaryWalkableTileFlag) == 0 ||
                        (tileFlags[candidateTileId] & CursorSpecialStructureTileFlagMask) != 0)
                    {
                        continue;
                    }

                    int candidateRegion = pathRegionGrid[candidateTileId];
                    if (candidateRegion <= 0 || candidateRegion > MaximumRegionId)
                        continue;

                    candidateObserved = true;
                    if (startRegion > 0 && startRegion == candidateRegion)
                    {
                        summary.StartRegion = startRegion;
                        summary.TargetRegion = candidateRegion;
                        summary.ReachedWithoutMoat = true;
                        summary.RouteFound = true;
                        normalReachable = true;
                        return true;
                    }

                    EnsureReachabilityMap(
                        scope.UnitId, scope.PlayerId, tileManager, playerApi,
                        scope.StartX, scope.StartY, candidateRegion);
                    RouteProbeSummary candidateSummary = cachedRouteSummary;
                    bool candidateWithoutMoat =
                        visitedWithoutMoat[candidateCell] == gridGeneration;
                    bool candidateWithMoat = visitedWithMoat[candidateCell] == gridGeneration;
                    candidateSummary.ReachedWithoutMoat = candidateWithoutMoat;
                    candidateSummary.ReachedWithMoat = candidateWithMoat;
                    candidateSummary.RouteFound = candidateWithoutMoat || candidateWithMoat;
                    summary.MergeObservations(candidateSummary);
                    normalReachable |= candidateWithoutMoat;
                    reachableWithMoat |= candidateWithMoat;
                }
            }

            friendlyMoatSeparated = reachableWithMoat && !normalReachable &&
                summary.FriendlyMoatTiles > 0;
            summary.ReachedWithoutMoat = normalReachable;
            summary.ReachedWithMoat = reachableWithMoat;
            summary.RouteFound = normalReachable || reachableWithMoat;
            return candidateObserved;
        }

        private void LogCursorGroupRoute(
            AttackCursorPairScope scope,
            bool evaluated,
            CursorGroupRouteSummary group,
            bool effective)
        {
            string key = $"{mapEpoch}:{scope?.PlayerId}:{scope?.TargetTileId}:" +
                $"{scope?.FallbackKind}:{group.SelectionSignature}:{evaluated}:" +
                $"{group.SelectedUnits}:{group.DiggerUnits}:{group.LegallyReachableUnits}:" +
                $"{group.FriendlyMoatSeparatedUnits}:{effective}";
            if (string.Equals(lastCursorGroupRouteDiagnostic, key, StringComparison.Ordinal))
                return;
            lastCursorGroupRouteDiagnostic = key;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=cursor-group-route player={scope?.PlayerId} " +
                $"target=({scope?.TargetX},{scope?.TargetY})/{scope?.TargetTileId} " +
                $"kind={scope?.FallbackKind} evaluated={evaluated} " +
                $"selected={group.SelectedUnits} diggers={group.DiggerUnits} " +
                $"legal={group.LegallyReachableUnits} " +
                $"friendlyMoatSeparated={group.FriendlyMoatSeparatedUnits} " +
                $"representative={group.RepresentativeUnitId} effective={effective}.");
        }

        private bool TryQualifyCursorScope(
            AttackCursorPairScope scope,
            out int approachX,
            out int approachY,
            out RouteProbeSummary summary)
        {
            approachX = -1;
            approachY = -1;
            summary = default;
            if (scope == null)
                return false;

            if (scope.FallbackKind == CursorPairFallbackKind.DirectTile)
            {
                approachX = scope.TargetX;
                approachY = scope.TargetY;
                return TryFindRequiredFriendlyCompletedMoatRouteToDirectTile(scope, out summary);
            }

            return TryFindFriendlyCompletedMoatRouteToAttackApproach(
                scope, out approachX, out approachY, out summary);
        }

        private bool CursorScopeMatchesTargetTile(AttackCursorPairScope scope, int targetTileId)
        {
            if (scope.FallbackKind == CursorPairFallbackKind.UnitApproach)
            {
                return (scope.CursorPairTargetTileId == targetTileId ||
                        scope.TargetTileId == targetTileId) &&
                    scope.TargetUnitId > 0 &&
                    TryGetHostileLivingUnitAtTile(
                        scope.PlayerId,
                        scope.TargetX,
                        scope.TargetY,
                        scope.TargetUnitId,
                        -1,
                        out _,
                        out _) &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(
                        scope.TargetUnitId, out GameUnit* targetUnit) &&
                    targetUnit != null && targetUnit->r_GlobalId == scope.TargetUnitGlobalId;
            }
            return scope.TargetTileId == targetTileId;
        }

        private bool CursorStartMatchesBoundSelection(
            AttackCursorPairScope scope, int actualSelectedUnitTileId)
        {
            if (scope.GroupCursorAuthorized)
            {
                return !string.IsNullOrEmpty(scope.GroupSelectionSignature) &&
                    TryCaptureSelectedGroup(
                        scope.PlayerId, out _, out string currentSelectionSignature) &&
                    string.Equals(
                        scope.GroupSelectionSignature,
                        currentSelectionSignature,
                        StringComparison.Ordinal);
            }

            if (scope.StartTileId == actualSelectedUnitTileId)
                return true;

            // E2CA0 can receive another representative member of a mixed selection.
            // The cursor is group-wide, while the later movement hooks filter every unit.
            return TryGetSelectedVanillaDigger(
                       scope.UnitId, scope.PlayerId, out int selectedDiggerId, out GameUnit* selectedDigger) &&
                   selectedDiggerId == scope.UnitId && selectedDigger != null &&
                   GameTileManagerAPI.Instance.GetTileId(
                       selectedDigger->r_CurrentTilePositionX,
                       selectedDigger->r_CurrentTilePositionY) == scope.StartTileId;
        }

        private bool TryResolveHostileLivingUnitFromRawCursor(
            int playerId,
            uint rawUnitId,
            out int targetUnitId,
            out uint targetUnitGlobalId,
            out int targetX,
            out int targetY,
            out int targetTileId)
        {
            targetUnitId = -1;
            targetUnitGlobalId = 0;
            targetX = -1;
            targetY = -1;
            targetTileId = -1;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId) || rawUnitId == 0 || rawUnitId > int.MaxValue)
                return false;

            int unitId = (int)rawUnitId;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* target) ||
                target == null || target->r_AliveState != AliveState.IsAlive ||
                target->r_GlobalId == 0)
            {
                return false;
            }

            int ownerId = target->r_ControllableForPlayerId;
            if (!playerApi.IsPlayerIdValid(ownerId) || ownerId == playerId ||
                playerApi.IsPlayerAlliedTo(playerId, ownerId))
            {
                return false;
            }

            int x = target->r_CurrentTilePositionX;
            int y = target->r_CurrentTilePositionY;
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapWidth)
                return false;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!IsValidTileId(tileId))
                return false;

            targetUnitId = unitId;
            targetUnitGlobalId = target->r_GlobalId;
            targetX = x;
            targetY = y;
            targetTileId = tileId;
            return true;
        }

        private bool TryResolveHostileLivingBuildingFromRawCursor(
            int playerId,
            uint rawBuildingId,
            uint rawHoverBuildingTileId,
            uint rawMouseTileId2,
            uint rawMouseTileId,
            int rawMouseX,
            int rawMouseY,
            out int targetX,
            out int targetY,
            out int targetTileId,
            out BuildingCursorTarget target)
        {
            targetX = -1;
            targetY = -1;
            targetTileId = -1;
            target = default;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId) || rawBuildingId == 0 ||
                rawBuildingId > int.MaxValue)
            {
                return false;
            }

            int buildingId = (int)rawBuildingId;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    buildingId, out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive ||
                building->r_GlobalId == 0 || IsWallStairOrRampStructure(building->r_BuildingType))
            {
                return false;
            }

            int ownerId = building->r_PlayerIdOwner;
            if (!playerApi.IsPlayerIdValid(ownerId) || ownerId == playerId ||
                playerApi.IsPlayerAlliedTo(playerId, ownerId))
            {
                return false;
            }

            BuildingHoverTileSource hoverTileSource;
            if (TryResolveRawBuildingFootprintTile(
                    buildingId, building, rawHoverBuildingTileId,
                    out targetX, out targetY, out targetTileId))
            {
                hoverTileSource = BuildingHoverTileSource.BuildingTile;
            }
            else if (TryResolveRawBuildingFootprintTile(
                         buildingId, building, rawMouseTileId2,
                         out targetX, out targetY, out targetTileId))
            {
                hoverTileSource = BuildingHoverTileSource.MouseTile2;
            }
            else if (TryResolveRawBuildingFootprintTile(
                         buildingId, building, rawMouseTileId,
                         out targetX, out targetY, out targetTileId))
            {
                hoverTileSource = BuildingHoverTileSource.MouseTile;
            }
            else if (TryResolveNearestBuildingFootprintTile(
                         buildingId, building, rawMouseTileId, rawMouseX, rawMouseY,
                         out targetX, out targetY, out targetTileId))
            {
                hoverTileSource = BuildingHoverTileSource.NearestFootprint;
            }
            else
            {
                return false;
            }

            target = new BuildingCursorTarget
            {
                BuildingId = buildingId,
                GlobalId = building->r_GlobalId,
                OwnerId = ownerId,
                BuildingType = building->r_BuildingType,
                HoverTileId = targetTileId,
                HoverTileSource = hoverTileSource
            };
            return true;
        }

        private bool TryResolveNearestBuildingFootprintTile(
            int buildingId,
            GameBuilding* building,
            uint rawMouseTileId,
            int rawMouseX,
            int rawMouseY,
            out int targetX,
            out int targetY,
            out int targetTileId)
        {
            targetX = -1;
            targetY = -1;
            targetTileId = -1;
            if (building == null)
                return false;

            int mouseX;
            int mouseY;
            if (rawMouseTileId <= int.MaxValue && IsValidTileId((int)rawMouseTileId))
            {
                // The cursor dispatcher can leave r_MouseTileX/Y at (0,0) over a sprite
                // overhang while r_MouseTileId and r_HoverOverBuildingId remain valid.
                UnmanagedVector2<ushort> mousePosition =
                    GameTileManagerAPI.Instance.GetTileVectorFromId((int)rawMouseTileId);
                mouseX = mousePosition.X;
                mouseY = mousePosition.Y;
                if (rawMouseX >= 0 && rawMouseX < MapWidth &&
                    rawMouseY >= 0 && rawMouseY < MapWidth &&
                    GameTileManagerAPI.Instance.GetTileId(rawMouseX, rawMouseY) ==
                        (int)rawMouseTileId)
                {
                    mouseX = rawMouseX;
                    mouseY = rawMouseY;
                }
            }
            else
            {
                return false;
            }

            int minX = Math.Max(0, Math.Min(
                (int)building->r_TilePositionXBegin, (int)building->r_TilePositionXEnd));
            int maxX = Math.Min(MapWidth - 1, Math.Max(
                (int)building->r_TilePositionXBegin, (int)building->r_TilePositionXEnd));
            int minY = Math.Max(0, Math.Min(
                (int)building->r_TilePositionYBegin, (int)building->r_TilePositionYEnd));
            int maxY = Math.Min(MapWidth - 1, Math.Max(
                (int)building->r_TilePositionYBegin, (int)building->r_TilePositionYEnd));
            long bestDistanceSquared = long.MaxValue;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int candidateTileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                    if (!IsValidTileId(candidateTileId) ||
                        GameTileManagerAPI.Instance.GetTileBuildingId(candidateTileId) != buildingId)
                    {
                        continue;
                    }

                    long deltaX = x - mouseX;
                    long deltaY = y - mouseY;
                    long distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared > bestDistanceSquared ||
                        (distanceSquared == bestDistanceSquared &&
                         targetTileId >= 0 && candidateTileId >= targetTileId))
                    {
                        continue;
                    }

                    bestDistanceSquared = distanceSquared;
                    targetX = x;
                    targetY = y;
                    targetTileId = candidateTileId;
                }
            }

            return targetTileId >= 0;
        }

        private static string FormatBuildingHoverTileSource(BuildingHoverTileSource source)
        {
            switch (source)
            {
                case BuildingHoverTileSource.BuildingTile:
                    return "buildingTile";
                case BuildingHoverTileSource.MouseTile2:
                    return "mouse2";
                case BuildingHoverTileSource.MouseTile:
                    return "mouse";
                case BuildingHoverTileSource.NearestFootprint:
                    return "nearest-footprint";
                default:
                    return "none";
            }
        }

        private bool TryResolveRawBuildingFootprintTile(
            int buildingId,
            GameBuilding* building,
            uint rawTileId,
            out int targetX,
            out int targetY,
            out int targetTileId)
        {
            targetX = -1;
            targetY = -1;
            targetTileId = -1;
            if (building == null || rawTileId > int.MaxValue || !IsValidTileId((int)rawTileId))
                return false;

            int candidateTileId = (int)rawTileId;
            if (GameTileManagerAPI.Instance.GetTileBuildingId(candidateTileId) != buildingId)
                return false;

            int minX = Math.Max(0, Math.Min(
                (int)building->r_TilePositionXBegin, (int)building->r_TilePositionXEnd));
            int maxX = Math.Min(MapWidth - 1, Math.Max(
                (int)building->r_TilePositionXBegin, (int)building->r_TilePositionXEnd));
            int minY = Math.Max(0, Math.Min(
                (int)building->r_TilePositionYBegin, (int)building->r_TilePositionYEnd));
            int maxY = Math.Min(MapWidth - 1, Math.Max(
                (int)building->r_TilePositionYBegin, (int)building->r_TilePositionYEnd));
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (GameTileManagerAPI.Instance.GetTileId(x, y) != candidateTileId)
                        continue;

                    targetX = x;
                    targetY = y;
                    targetTileId = candidateTileId;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetHostileLivingBuildingForCursor(
            int playerId,
            int targetTileId,
            out BuildingCursorTarget target,
            out bool wallLike)
        {
            target = default;
            wallLike = false;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId) || !IsValidTileId(targetTileId))
                return false;

            int hoveredBuildingId = playerApi.GetHoveredBuildingId();
            int hoverTileId = playerApi.GetHoveredBuildingTileId();
            int structureBuildingId = GameTileManagerAPI.Instance.GetTileBuildingId(targetTileId);
            int buildingId = hoveredBuildingId > 0 ? hoveredBuildingId : structureBuildingId;
            bool hoverTileBelongs = IsValidTileId(hoverTileId) && buildingId > 0 &&
                GameTileManagerAPI.Instance.GetTileBuildingId(hoverTileId) == buildingId;
            if (buildingId <= 0 ||
                (structureBuildingId != buildingId && !hoverTileBelongs) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive ||
                building->r_GlobalId == 0)
            {
                return false;
            }

            eStructs buildingType = building->r_BuildingType;
            wallLike = IsWallStairOrRampStructure(buildingType);
            target = new BuildingCursorTarget
            {
                BuildingId = buildingId,
                GlobalId = building->r_GlobalId,
                OwnerId = building->r_PlayerIdOwner,
                BuildingType = buildingType,
                HoverTileId = hoverTileId
            };
            if (wallLike)
                return false;

            int ownerId = building->r_PlayerIdOwner;
            return playerApi.IsPlayerIdValid(ownerId) && ownerId != playerId &&
                !playerApi.IsPlayerAlliedTo(playerId, ownerId);
        }

        private static bool IsWallStairOrRampStructure(eStructs buildingType) =>
            buildingType == eStructs.STRUCT_WOOD_WALL ||
            buildingType == eStructs.STRUCT_STONE_WALL ||
            buildingType == eStructs.STRUCT_CRENAL_WALL ||
            buildingType == eStructs.STRUCT_STAIRS ||
            buildingType == eStructs.STRUCT_WAS_WALL;

        private bool TryValidateHostileBuildingTarget(
            int buildingId,
            uint buildingGlobalId,
            int playerId,
            out GameBuilding* building)
        {
            building = null;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (buildingId <= 0 || buildingGlobalId == 0 ||
                !playerApi.IsPlayerIdValid(playerId) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    buildingId, out GameBuilding* candidate) ||
                candidate == null || candidate->r_AliveState != AliveState.IsAlive ||
                candidate->r_GlobalId != buildingGlobalId ||
                IsWallStairOrRampStructure(candidate->r_BuildingType))
            {
                return false;
            }

            int ownerId = candidate->r_PlayerIdOwner;
            if (!playerApi.IsPlayerIdValid(ownerId) || ownerId == playerId ||
                playerApi.IsPlayerAlliedTo(playerId, ownerId))
            {
                return false;
            }

            building = candidate;
            return true;
        }

        private static bool TryGetUnitAttackMoveTile(GameUnit* unit, out int tileId)
        {
            tileId = -1;
            if (unit == null || unit->r_AttackMoveToTargetTileX < 0 ||
                unit->r_AttackMoveToTargetTileX >= MapWidth ||
                unit->r_AttackMoveToTargetTileY < 0 ||
                unit->r_AttackMoveToTargetTileY >= MapWidth)
            {
                return false;
            }

            tileId = GameTileManagerAPI.Instance.GetTileId(
                unit->r_AttackMoveToTargetTileX, unit->r_AttackMoveToTargetTileY);
            return IsValidTileId(tileId);
        }

        private bool IsExactBuildingContextTile(
            int buildingId, GameBuilding* building, int footprintTileId)
        {
            if (building == null || !IsValidTileId(footprintTileId) ||
                GameTileManagerAPI.Instance.GetTileBuildingId(footprintTileId) != buildingId)
            {
                return false;
            }

            // DA020 uses the StructureGrid identity and this exact flag mask. Some buildings
            // reserve valid context tiles outside their smaller record bounding rectangle.
            return (tileFlags[footprintTileId] & BuildingContextBlockingTileFlagMask) == 0;
        }

        private bool IsValidBuildingApproachPair(
            int buildingId,
            GameBuilding* building,
            int approachTileId,
            int footprintTileId)
        {
            if (!IsExactBuildingContextTile(buildingId, building, footprintTileId) ||
                !IsValidTileId(approachTileId))
            {
                return false;
            }

            // StructureGrid also reserves surrounding tiles for some buildings. Those tiles
            // remain valid movement endpoints when Vanilla's native occupancy mask exposes at
            // least one traversable direction; the Assassin fix uses the same distinction.
            ushort approachBuildingId =
                GameTileManagerAPI.Instance.GetTileBuildingId(approachTileId);
            if (approachBuildingId != 0 && nativeMovementMasks[approachTileId] == 0)
                return false;

            UnmanagedVector2<ushort> approach =
                GameTileManagerAPI.Instance.GetTileVectorFromId(approachTileId);
            UnmanagedVector2<ushort> footprint =
                GameTileManagerAPI.Instance.GetTileVectorFromId(footprintTileId);
            int approachX = approach.X;
            int approachY = approach.Y;
            int footprintX = footprint.X;
            int footprintY = footprint.Y;
            if (approachX < 0 || approachX >= MapWidth || approachY < 0 ||
                approachY >= MapWidth ||
                GameTileManagerAPI.Instance.GetTileId(approachX, approachY) != approachTileId ||
                Math.Abs(approachX - footprintX) + Math.Abs(approachY - footprintY) != 1)
            {
                return false;
            }

            return IsWalkableBuildingApproachEndpoint(approachTileId);
        }

        private bool TryFindVanillaBuildingContextNeighbour(
            int buildingId, int approachTileId, out int footprintTileId)
        {
            footprintTileId = 0;
            if (!IsValidTileId(approachTileId))
                return false;

            UnmanagedVector2<ushort> approach =
                GameTileManagerAPI.Instance.GetTileVectorFromId(approachTileId);
            int approachX = approach.X;
            int approachY = approach.Y;
            if (approachX < 0 || approachX >= MapWidth || approachY < 0 ||
                approachY >= MapWidth ||
                GameTileManagerAPI.Instance.GetTileId(approachX, approachY) != approachTileId)
            {
                return false;
            }

            int[] cardinalX = { -1, 1, 0, 0 };
            int[] cardinalY = { 0, 0, -1, 1 };
            for (int index = 0; index < cardinalX.Length; index++)
            {
                int x = approachX + cardinalX[index];
                int y = approachY + cardinalY[index];
                if (x < 0 || x >= MapWidth || y < 0 || y >= MapWidth)
                    continue;
                int candidateFootprintTileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (!IsValidTileId(candidateFootprintTileId) ||
                    GameTileManagerAPI.Instance.GetTileBuildingId(candidateFootprintTileId) != buildingId ||
                    (tileFlags[candidateFootprintTileId] &
                        BuildingContextBlockingTileFlagMask) != 0)
                    continue;

                footprintTileId = candidateFootprintTileId;
                return true;
            }

            return false;
        }

        private bool IsWalkableBuildingApproachEndpoint(int tileId)
        {
            if (!IsValidTileId(tileId))
                return false;
            UnmanagedVector2<ushort> position =
                GameTileManagerAPI.Instance.GetTileVectorFromId(tileId);
            int x = position.X;
            int y = position.Y;
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapWidth ||
                GameTileManagerAPI.Instance.GetTileId(x, y) != tileId ||
                movementTargetAvailability[y * MapWidth + x] == 0)
            {
                return false;
            }

            ushort reservedByBuilding = GameTileManagerAPI.Instance.GetTileBuildingId(tileId);
            if (reservedByBuilding != 0)
                return nativeMovementMasks[tileId] != 0;
            return (tileFlags[tileId] & OrdinaryWalkableTileFlag) != 0 &&
                (tileFlags[tileId] & CursorSpecialStructureTileFlagMask) == 0;
        }

        private bool TryProbeBuildingApproachCursorRoute(
            AttackCursorPairScope scope,
            out bool normalReachable,
            out bool friendlyMoatSeparated,
            out int approachX,
            out int approachY,
            out RouteProbeSummary summary)
        {
            normalReachable = false;
            friendlyMoatSeparated = false;
            approachX = -1;
            approachY = -1;
            summary = default;
            if (scope == null || scope.BuildingId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    scope.BuildingId, out GameBuilding* building) || building == null ||
                building->r_AliveState != AliveState.IsAlive ||
                building->r_GlobalId != scope.BuildingGlobalId ||
                building->r_PlayerIdOwner != scope.BuildingOwnerId ||
                building->r_BuildingType != scope.BuildingType ||
                IsWallStairOrRampStructure(building->r_BuildingType))
            {
                return false;
            }

            int minX = Math.Min(building->r_TilePositionXBegin, building->r_TilePositionXEnd);
            int maxX = Math.Max(building->r_TilePositionXBegin, building->r_TilePositionXEnd);
            int minY = Math.Min(building->r_TilePositionYBegin, building->r_TilePositionYEnd);
            int maxY = Math.Max(building->r_TilePositionYBegin, building->r_TilePositionYEnd);
            minX = Math.Max(0, minX - 1);
            minY = Math.Max(0, minY - 1);
            maxX = Math.Min(MapWidth - 1, maxX + 1);
            maxY = Math.Min(MapWidth - 1, maxY + 1);

            RouteProbeSummary observed = new RouteProbeSummary(scope.PlayerId);
            bool reachableWithMoat = false;
            bool candidateObserved = false;
            int startRegion = IsValidTileId(scope.StartTileId)
                ? pathRegionGrid[scope.StartTileId]
                : 0;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(scope.PlayerId))
                return false;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                    if (!IsValidTileId(tileId) ||
                        GameTileManagerAPI.Instance.GetTileBuildingId(tileId) == scope.BuildingId)
                    {
                        continue;
                    }
                    bool adjacentToFootprint = false;
                    for (int dy = -1; dy <= 1 && !adjacentToFootprint; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if ((dx == 0 && dy == 0) || x + dx < 0 || x + dx >= MapWidth ||
                                y + dy < 0 || y + dy >= MapWidth)
                                continue;
                            int adjacentTile = GameTileManagerAPI.Instance.GetTileId(x + dx, y + dy);
                            if (IsValidTileId(adjacentTile) &&
                                GameTileManagerAPI.Instance.GetTileBuildingId(adjacentTile) == scope.BuildingId)
                            {
                                adjacentToFootprint = true;
                                break;
                            }
                        }
                    }
                    int cell = y * MapWidth + x;
                    if (!adjacentToFootprint || movementTargetAvailability[cell] == 0 ||
                        (tileFlags[tileId] & OrdinaryWalkableTileFlag) == 0 ||
                        (tileFlags[tileId] & CursorSpecialStructureTileFlagMask) != 0)
                        continue;
                    int region = pathRegionGrid[tileId];
                    if (region <= 0 || region > MaximumRegionId)
                        continue;

                    candidateObserved = true;
                    if (startRegion > 0 && startRegion == region)
                    {
                        normalReachable = true;
                        if (approachX < 0)
                        {
                            approachX = x;
                            approachY = y;
                        }
                        observed.MergeObservations(new RouteProbeSummary(scope.PlayerId)
                        {
                            StartRegion = startRegion,
                            TargetRegion = region,
                            ReachedWithoutMoat = true,
                            RouteFound = true
                        });
                        continue;
                    }

                    EnsureReachabilityMap(scope.UnitId, scope.PlayerId, tileManager, playerApi,
                        scope.StartX, scope.StartY, region);
                    RouteProbeSummary candidate = cachedRouteSummary;
                    bool withMoat = visitedWithMoat[cell] == gridGeneration;
                    bool withoutMoat = visitedWithoutMoat[cell] == gridGeneration;
                    candidate.AttackProbeEvaluated = true;
                    candidate.ReachedWithMoat = withMoat;
                    candidate.ReachedWithoutMoat = withoutMoat;
                    candidate.RegionTopologyQualified = true;
                    candidate.RouteFound = withoutMoat || withMoat;
                    observed.MergeObservations(candidate);
                    normalReachable |= withoutMoat;
                    reachableWithMoat |= withMoat;
                    if ((withoutMoat || withMoat) && approachX < 0)
                    {
                        approachX = x;
                        approachY = y;
                    }
                }
            }

            friendlyMoatSeparated = reachableWithMoat && !normalReachable &&
                observed.FriendlyMoatTiles > 0;
            observed.ReachedWithoutMoat = normalReachable;
            observed.ReachedWithMoat = reachableWithMoat;
            observed.RouteFound = normalReachable || reachableWithMoat;
            summary = observed;
            return candidateObserved;
        }

        private bool TryGetHostileLivingUnitAtTile(
            int playerId,
            int targetX,
            int targetY,
            int requiredUnitId,
            int requiredGlobalId,
            out int targetUnitId,
            out bool occupiedByLivingUnit)
        {
            targetUnitId = -1;
            occupiedByLivingUnit = false;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId) || targetX < 0 || targetX >= MapWidth ||
                targetY < 0 || targetY >= MapWidth)
            {
                return false;
            }

            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int unitId = 1; unitId <= units.Length; unitId++)
            {
                if (requiredUnitId > 0 && unitId != requiredUnitId)
                    continue;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* target) ||
                    target == null || target->r_AliveState != AliveState.IsAlive ||
                    target->r_CurrentTilePositionX != targetX ||
                    target->r_CurrentTilePositionY != targetY)
                {
                    continue;
                }
                if (requiredGlobalId >= 0 &&
                    target->r_GlobalId != unchecked((uint)requiredGlobalId))
                {
                    continue;
                }

                occupiedByLivingUnit = true;

                int targetPlayerId = target->r_ControllableForPlayerId;
                if (!playerApi.IsPlayerIdValid(targetPlayerId) || targetPlayerId == playerId ||
                    playerApi.IsPlayerAlliedTo(playerId, targetPlayerId))
                {
                    continue;
                }

                targetUnitId = unitId;
                return true;
            }

            return false;
        }

        private bool TryFindFriendlyCompletedMoatRouteToAttackApproach(
            AttackCursorPairScope scope,
            out int approachX,
            out int approachY,
            out RouteProbeSummary summary)
        {
            approachX = -1;
            approachY = -1;
            summary = default;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (scope == null || tileManager == IntPtr.Zero ||
                !playerApi.IsPlayerIdValid(scope.PlayerId))
            {
                return false;
            }

            RouteProbeSummary bestObserved = new RouteProbeSummary(scope.PlayerId);
            bool startOnCompletedMoat = IsValidTileId(scope.StartTileId) &&
                (tileFlags[scope.StartTileId] & CompletedMoatTileFlag) != 0;
            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    if (xOffset == 0 && yOffset == 0)
                        continue;

                    int candidateX = scope.TargetX + xOffset;
                    int candidateY = scope.TargetY + yOffset;
                    if (candidateX < 0 || candidateX >= MapWidth ||
                        candidateY < 0 || candidateY >= MapWidth)
                    {
                        continue;
                    }

                    int candidateCell = (candidateY * MapWidth) + candidateX;
                    if (movementTargetAvailability[candidateCell] == 0)
                        continue;

                    int candidateTileId = GameTileManagerAPI.Instance.GetTileId(candidateX, candidateY);
                    if (!IsValidTileId(candidateTileId) ||
                        (tileFlags[candidateTileId] & OrdinaryWalkableTileFlag) == 0 ||
                        (tileFlags[candidateTileId] & CursorSpecialStructureTileFlagMask) != 0)
                    {
                        continue;
                    }

                    int candidateRegion = pathRegionGrid[candidateTileId];
                    if (candidateRegion <= 0 || candidateRegion > MaximumRegionId)
                        continue;

                    EnsureReachabilityMap(
                        scope.UnitId,
                        scope.PlayerId,
                        tileManager,
                        playerApi,
                        scope.StartX,
                        scope.StartY,
                        candidateRegion);
                    RouteProbeSummary candidateSummary = cachedRouteSummary;
                    bool reachedWithMoat = visitedWithMoat[candidateCell] == gridGeneration;
                    bool reachedWithoutMoat = visitedWithoutMoat[candidateCell] == gridGeneration;
                    bool regionTopologyQualified =
                        (candidateSummary.StartRegion > 0 &&
                         candidateSummary.StartRegion != candidateSummary.TargetRegion) ||
                        (candidateSummary.StartRegion == 0 && startOnCompletedMoat);
                    candidateSummary.AttackProbeEvaluated = true;
                    candidateSummary.ReachedWithMoat = reachedWithMoat;
                    candidateSummary.ReachedWithoutMoat = reachedWithoutMoat;
                    candidateSummary.RegionTopologyQualified = regionTopologyQualified;
                    candidateSummary.RouteFound = reachedWithMoat && !reachedWithoutMoat &&
                        candidateSummary.FriendlyMoatTiles > 0;
                    bestObserved.MergeObservations(candidateSummary);
                    if (!candidateSummary.RouteFound)
                        continue;

                    approachX = candidateX;
                    approachY = candidateY;
                    summary = candidateSummary;
                    return true;
                }
            }

            summary = bestObserved;
            return false;
        }

        private void EnsureReachabilityMap(
            int cacheKey,
            int playerId,
            IntPtr tileManager,
            GamePlayerManagerAPI playerApi,
            int startX,
            int startY,
            int targetRegion)
        {
            if (visitedWithoutMoat != null && cacheMapEpoch == mapEpoch &&
                cacheUnitIndex == cacheKey && cachePlayerId == playerId &&
                cacheStartX == startX && cacheStartY == startY &&
                cacheTargetRegion == targetRegion)
            {
                if (activeBuildingConsumerPerformance != null)
                    activeBuildingConsumerPerformance.ReachabilityCacheHits++;
                if (activeBuildingApproachPerformance != null)
                    activeBuildingApproachPerformance.ReachabilityCacheHits++;
                return;
            }

            if (activeBuildingConsumerPerformance != null)
                activeBuildingConsumerPerformance.ReachabilityMapsBuilt++;
            if (activeBuildingApproachPerformance != null)
                activeBuildingApproachPerformance.ReachabilityMapsBuilt++;

            if (visitedWithoutMoat == null)
            {
                visitedWithoutMoat = new int[MapCellCount];
                visitedWithMoat = new int[MapCellCount];
                distanceWithoutMoat = new int[MapCellCount];
                distanceWithMoat = new int[MapCellCount];
                rejectedMoat = new int[MapCellCount];
                queue = new int[MapCellCount * 2];
            }

            if (gridGeneration == int.MaxValue)
            {
                Array.Clear(visitedWithoutMoat, 0, visitedWithoutMoat.Length);
                Array.Clear(visitedWithMoat, 0, visitedWithMoat.Length);
                Array.Clear(rejectedMoat, 0, rejectedMoat.Length);
                gridGeneration = 1;
            }
            else
            {
                gridGeneration++;
            }

            cacheMapEpoch = mapEpoch;
            cacheUnitIndex = cacheKey;
            cachePlayerId = playerId;
            cacheStartX = startX;
            cacheStartY = startY;
            cacheTargetRegion = targetRegion;
            cachedRouteSummary = new RouteProbeSummary(playerId);

            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            if (!IsValidTileId(startTileId))
                return;

            int startRegion = pathRegionGrid[startTileId];
            cachedRouteSummary.StartRegion = startRegion;
            cachedRouteSummary.TargetRegion = targetRegion;
            int startCell = (startY * MapWidth) + startX;
            bool startIsMoat = (tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
            bool startIsFriendlyMoat = startIsMoat && TryClassifyFriendlyMoat(
                tileManager, playerApi, startTileId, playerId, ref cachedRouteSummary);
            if (startIsFriendlyMoat)
            {
                visitedWithMoat[startCell] = gridGeneration;
                distanceWithMoat[startCell] = 0;
            }
            else
            {
                visitedWithoutMoat[startCell] = gridGeneration;
                distanceWithoutMoat[startCell] = 0;
            }

            int head = 0;
            int tail = 0;
            queue[tail++] = startCell | (startIsFriendlyMoat ? MoatStateBit : 0);
            while (head < tail)
            {
                int encoded = queue[head++];
                bool usedMoat = (encoded & MoatStateBit) != 0;
                int cell = encoded & (MoatStateBit - 1);
                int y = cell / MapWidth;
                int x = cell - (y * MapWidth);
                int currentDistance = usedMoat
                    ? distanceWithMoat[cell]
                    : distanceWithoutMoat[cell];

                VisitNeighbour(tileManager, playerApi, playerId, x - 1, y, usedMoat, currentDistance, startRegion, targetRegion, ref tail);
                VisitNeighbour(tileManager, playerApi, playerId, x + 1, y, usedMoat, currentDistance, startRegion, targetRegion, ref tail);
                VisitNeighbour(tileManager, playerApi, playerId, x, y - 1, usedMoat, currentDistance, startRegion, targetRegion, ref tail);
                VisitNeighbour(tileManager, playerApi, playerId, x, y + 1, usedMoat, currentDistance, startRegion, targetRegion, ref tail);
            }
        }

        private void VisitNeighbour(
            IntPtr tileManager,
            GamePlayerManagerAPI playerApi,
            int playerId,
            int x,
            int y,
            bool usedMoat,
            int currentDistance,
            int startRegion,
            int targetRegion,
            ref int queueTail)
        {
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapWidth)
                return;

            int cell = (y * MapWidth) + x;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!IsValidTileId(tileId))
                return;

            uint flags = tileFlags[tileId];
            bool isMoat = (flags & CompletedMoatTileFlag) != 0;
            if (isMoat && rejectedMoat[cell] == gridGeneration)
                return;
            if (isMoat && !TryClassifyFriendlyMoat(
                    tileManager, playerApi, tileId, playerId, ref cachedRouteSummary))
            {
                rejectedMoat[cell] = gridGeneration;
                return;
            }
            if (!isMoat && ((flags & OrdinaryWalkableTileFlag) == 0 || movementTargetAvailability[cell] == 0))
                return;

            if (!isMoat)
            {
                int region = pathRegionGrid[tileId];
                if ((!usedMoat && region != startRegion) ||
                    (usedMoat && region != startRegion && region != targetRegion))
                {
                    return;
                }
            }

            bool nextUsedMoat = usedMoat || isMoat;
            int[] visited = nextUsedMoat ? visitedWithMoat : visitedWithoutMoat;
            int[] distances = nextUsedMoat ? distanceWithMoat : distanceWithoutMoat;
            if (visited[cell] == gridGeneration)
                return;

            visited[cell] = gridGeneration;
            distances[cell] = currentDistance + 1;
            queue[queueTail++] = cell | (nextUsedMoat ? MoatStateBit : 0);
        }

        private bool TryClassifyFriendlyMoat(
            IntPtr tileManager,
            GamePlayerManagerAPI playerApi,
            int tileId,
            int playerId,
            ref RouteProbeSummary summary)
        {
            int moatOwnerId;
            BuildingConsumerPerformanceScope consumerPerformance =
                activeBuildingConsumerPerformance;
            BuildingApproachPerformanceScope approachPerformance =
                activeBuildingApproachPerformance;
            Dictionary<int, int> ownerCache = consumerPerformance != null
                ? consumerPerformance.MoatOwnerByTile
                : approachPerformance?.MoatOwnerByTile;
            if (ownerCache != null && ownerCache.TryGetValue(tileId, out int cachedOwnerId))
            {
                if (consumerPerformance != null)
                    consumerPerformance.MoatOwnerCacheHits++;
                else
                    approachPerformance.MoatOwnerCacheHits++;
                moatOwnerId = cachedOwnerId;
            }
            else
            {
                int moatId = getMoatIdAtTile(tileManager, tileId);
                int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
                if (moatId <= 0 || moatId >= moatCount)
                {
                    moatOwnerId = -1;
                }
                else
                {
                    byte* moatRecord = (byte*)tileManager.ToPointer() +
                        MoatRecordArrayOffset + moatId * MoatRecordSize;
                    moatOwnerId = moatRecord[MoatOwnerOffset];
                }
                if (ownerCache != null)
                {
                    if (consumerPerformance != null)
                        consumerPerformance.MoatOwnerCacheMisses++;
                    else
                        approachPerformance.MoatOwnerCacheMisses++;
                    ownerCache[tileId] = moatOwnerId;
                }
            }
            summary.ObserveOwner(moatOwnerId);
            if (!playerApi.IsPlayerIdValid(moatOwnerId))
            {
                summary.InvalidMoatTiles++;
                return false;
            }

            bool friendly = moatOwnerId == playerId ||
                playerApi.IsPlayerAlliedTo(playerId, moatOwnerId);
            if (friendly)
                summary.FriendlyMoatTiles++;
            else
                summary.EnemyMoatTiles++;
            return friendly;
        }

        private void LogUnscopedAttackMode(int unitId, GameUnit* unit, int vanillaResult)
        {
            TribeAICommand command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
            if (command != TribeAICommand.AttackUnit &&
                command != TribeAICommand.AttackBuilding &&
                command != TribeAICommand.ForceAttackBuilding)
            {
                return;
            }

            string signature = $"{mapEpoch}:{(uint)command}:{unit->r_AIState}:" +
                $"{unit->r_CurrentTilePositionX}:{unit->r_CurrentTilePositionY}:" +
                $"{unit->r_AttackMoveToTargetTileX}:{unit->r_AttackMoveToTargetTileY}:" +
                $"{unit->r_AI_ContextTargetUnitId}:{unit->r_AI_ContextTargetUnitGlobalId}:" +
                $"{unit->r_AI_ContextTargetBuildingTileId}:" +
                $"{unit->r_ContextTargetTileX}:{unit->r_ContextTargetTileY}:{vanillaResult}";
            if (lastUnscopedAttackModes.TryGetValue(unitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }

            lastUnscopedAttackModes[unitId] = signature;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=attack-mode-unscoped unit={unitId} " +
                $"player={unit->r_ControllableForPlayerId} command={command}({(uint)command}) " +
                $"aiState={unit->r_AIState} current=({unit->r_CurrentTilePositionX}," +
                $"{unit->r_CurrentTilePositionY}) attackMove=({unit->r_AttackMoveToTargetTileX}," +
                $"{unit->r_AttackMoveToTargetTileY}) contextUnit={unit->r_AI_ContextTargetUnitId}/" +
                $"{unit->r_AI_ContextTargetUnitGlobalId} " +
                $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                $"contextTile=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                $"vanillaStandingOnMoat={vanillaResult}.");
        }

        private void ResetMapState()
        {
            mapEpoch++;
            cacheMapEpoch = -1;
            cacheUnitIndex = -1;
            cacheStartX = -1;
            cacheStartY = -1;
            cacheTargetRegion = -1;
            cachePlayerId = -1;
            cachedRouteSummary = default;
            lastCursorRegionPositiveGeneration = -1;
            lastCursorDirectPositiveGeneration = -1;
            lastCursorRegionBlockGeneration = -1;
            lastCursorDirectBlockGeneration = -1;
            lastAttackCursorDecision = null;
            lastCursorSelectionDiagnostic = null;
            lastCursorTilePairDiagnostic = null;
            lastCursorGroupRouteDiagnostic = null;
            loggedBuildingCursorReachabilityDecisions.Clear();
            cursorGroupRouteCacheKey = null;
            cachedCursorGroupRoute = default;
            lastUnscopedAttackModes.Clear();
            lastAttackCommandCandidates.Clear();
            trackedAttackUnits.Clear();
            activeMoveCommand = null;
            activePlan = null;
            pendingPlan = null;
            pendingAttackCursorPair = null;
            pendingCursorSelectionDiagnostic = null;
            activeAttackCommand = null;
            activeAttackApproachDiagnostic = null;
            activeBuildingApproachPerformance = null;
            activeBuildingConsumerPerformance = null;
            trackedMoatMoves.Clear();
            loggedDiggerDecisions.Clear();
            lastWeightedShadowDecisionByUnit.Clear();
            lastWeightedPublicationDecisionByUnit.Clear();
        }

        private void LogCursorDecision(string message)
        {
            Shared.DebugLogHelper.LogInfo(log, $"MoveMoat {message}.");
        }

        private void LogPositiveCursorDecision(ref int lastLoggedGeneration, string message)
        {
            if (lastLoggedGeneration == gridGeneration)
                return;

            lastLoggedGeneration = gridGeneration;
            LogCursorDecision(message);
        }

        private void LogCursorOwnerBlockDecision(ref int lastLoggedGeneration, string message)
        {
            if (lastLoggedGeneration == gridGeneration)
                return;

            lastLoggedGeneration = gridGeneration;
            LogCursorDecision(message);
        }

        private void LogMovementContext(string message)
        {
            BufferOrLogCommandDiagnostic(message);
        }

        private void LogBuilderDecision(string message)
        {
            BufferOrLogCommandDiagnostic(message);
        }

        private void LogPipelineDiagnostic(string message)
        {
            BufferOrLogCommandDiagnostic(message);
        }

        private void LogCommandDiagnostic(string message)
        {
            BufferOrLogCommandDiagnostic(message);
        }

        private void BufferOrLogCommandDiagnostic(string message)
        {
            MoveCommandScope command = activeMoveCommand;
            if (command != null)
            {
                command.Diagnostics.Add(message);
                return;
            }

            Shared.DebugLogHelper.LogInfo(log, $"MoveMoat {message}.");
        }

        private static void MarkCommandMoatRelevant(
            MoveCommandScope command, RouteProbeSummary summary)
        {
            if (command != null &&
                (summary.FriendlyMoatTiles > 0 || summary.EnemyMoatTiles > 0))
            {
                command.MoatRelevant = true;
            }
        }

        private void QualifyPendingCommandDiagnostics(MoveCommandScope command)
        {
            if (command == null || command.BuilderReached || pendingPlan == null)
                return;

            // A Patrol leg can leave MoveHere after mode selection but before the builder.
            // Probe once in Post so precisely that early exit remains diagnosable.
            try
            {
                TryFindFriendlyCompletedMoatRouteForPlan(
                    pendingPlan, out RouteProbeSummary summary);
                MarkCommandMoatRelevant(command, summary);
            }
            catch (Exception ex)
            {
                LogFailure("command-diagnostic-route", ex);
            }
        }

        private void FlushCommandDiagnostics(MoveCommandScope command)
        {
            if (command == null ||
                !command.MoatRelevant)
                return;

            foreach (string message in command.Diagnostics)
                Shared.DebugLogHelper.LogInfo(log, $"MoveMoat {message}.");
        }

        private void LogModeContext(PlanScope plan, GameUnit* unit, int vanillaResult)
        {
            int startX = unit->r_CurrentTilePositionX;
            int startY = unit->r_CurrentTilePositionY;
            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            int startRegion = IsValidTileId(startTileId) ? pathRegionGrid[startTileId] : 0;
            uint startFlags = IsValidTileId(startTileId) ? tileFlags[startTileId] : 0;

            bool targetInBounds = plan.TargetX >= 0 && plan.TargetX < MapWidth &&
                plan.TargetY >= 0 && plan.TargetY < MapWidth;
            int targetCell = targetInBounds ? (plan.TargetY * MapWidth) + plan.TargetX : -1;
            int targetTileId = targetInBounds
                ? GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY)
                : -1;
            int targetRegion = IsValidTileId(targetTileId) ? pathRegionGrid[targetTileId] : 0;
            uint targetFlags = IsValidTileId(targetTileId) ? tileFlags[targetTileId] : 0;
            int targetAvailability = targetCell >= 0 ? movementTargetAvailability[targetCell] : -1;
            string source = ReferenceEquals(activePlan, plan) ? "central-planner" : "movehere-direct";

            LogCommandDiagnostic(
                $"stage=mode-context unit={plan.UnitId} player={plan.PlayerId} source={source} " +
                $"start=({startX},{startY}) target=({plan.TargetX},{plan.TargetY}) " +
                $"sameTile={startX == plan.TargetX && startY == plan.TargetY} " +
                $"startTile={startTileId} startRegion={startRegion} startFlags=0x{startFlags:X8} " +
                $"targetTile={targetTileId} targetRegion={targetRegion} " +
                $"targetFlags=0x{targetFlags:X8} targetAvailability={targetAvailability} " +
                $"vanillaMode={vanillaResult} effectiveMode=1");
        }

        private static void RecordBuilderResult(MoveCommandScope command, int result)
        {
            if (command == null)
                return;

            command.LastBuilderResult = result;
            if (result > 0)
                command.PositiveBuilderCalls++;
        }

        private static void RecordVanillaBuilderResult(
            MoveCommandScope command, int result)
        {
            if (command == null)
                return;

            command.VanillaBuilderCalls++;
            command.LastVanillaBuilderResult = result;
        }

        private void LogRejectedPlannerRoute(PlanScope plan, RouteProbeSummary summary)
        {
            if (plan == null)
                return;

            int targetAvailability = -1;
            int targetRegion = 0;
            bool targetInBounds = plan.TargetX >= 0 && plan.TargetX < MapWidth &&
                plan.TargetY >= 0 && plan.TargetY < MapWidth;
            if (targetInBounds)
            {
                int targetCell = (plan.TargetY * MapWidth) + plan.TargetX;
                targetAvailability = movementTargetAvailability[targetCell];
                int targetTileId = GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY);
                if (IsValidTileId(targetTileId))
                    targetRegion = pathRegionGrid[targetTileId];
            }

            bool moatRelevant = summary.FriendlyMoatTiles > 0 ||
                summary.EnemyMoatTiles > 0;
            if (!moatRelevant)
                return;

            string reason = !targetInBounds
                ? "target-out-of-bounds"
                : targetAvailability == 0
                    ? "target-unavailable-or-occupied"
                    : summary.EnemyMoatTiles > 0 && summary.FriendlyMoatTiles == 0
                        ? "enemy-moat-only"
                        : "no-qualified-friendly-moat-route";

            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=planner-owner-rejected unit={plan.UnitId} player={plan.PlayerId} " +
                $"target=({plan.TargetX},{plan.TargetY}) targetAvailability={targetAvailability} " +
                $"targetRegion={targetRegion} reason={reason} {summary.ToLogFields()}.");
        }

        private void LogFailure(string stage, Exception ex)
        {
            if (callbackFailureReported)
                return;
            callbackFailureReported = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"MoveMoat {stage} callback failed once; Vanilla behavior remains active: {ex}");
        }

        private void TryLogDiagnosticFailure(string stage, Exception ex)
        {
            try
            {
                if (!reportedDiagnosticFailureStages.Add(stage))
                    return;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MoveMoat read-only diagnostic stage={stage} failed; " +
                    $"Vanilla behavior remains unchanged: {ex}");
            }
            catch
            {
                // Never let diagnostic error reporting escape into a native callback.
            }
        }

        private Shared.NativeResolution Resolve(
            ReadOnlySpan<byte> memory, string pattern, int expectedRva, string label)
        {
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                expectedRva,
                referenceHashMatches: true,
                name: label,
                log: null);
            if (resolution.Rva != expectedRva)
            {
                throw new InvalidOperationException(
                    $"The native {label} resolved to 0x{resolution.Rva:X} instead of 0x{expectedRva:X}.");
            }
            return resolution;
        }

        private static void ValidateExactBytes(
            ReadOnlySpan<byte> memory, int rva, byte[] expected, string label)
        {
            if (rva < 0 || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    $"The validated instruction bytes for {label} did not match CrusaderDE.dll.");
            }
        }

        private static void ValidateGameUnitFieldOffset(string fieldName, int expectedOffset)
        {
            ValidateStructFieldOffset(typeof(GameUnit), fieldName, expectedOffset);
        }

        private static void ValidateStructFieldOffset(Type structType, string fieldName, int expectedOffset)
        {
            int actualOffset = Marshal.OffsetOf(structType, fieldName).ToInt32();
            if (actualOffset != expectedOffset)
            {
                throw new InvalidOperationException(
                    $"Unexpected {structType.Name}.{fieldName} offset 0x{actualOffset:X}; " +
                    $"expected 0x{expectedOffset:X}.");
            }
        }

        private static void ValidateCallTarget(
            ReadOnlySpan<byte> memory,
            int callRva,
            int expectedTargetRva,
            byte[] expectedBytes,
            string label)
        {
            ValidateExactBytes(memory, callRva, expectedBytes, label);
            if (expectedBytes.Length != 5 || expectedBytes[0] != 0xE8)
                throw new InvalidOperationException($"The validated {label} is not a near CALL.");

            int targetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, callRva + 1, callRva + 5);
            if (targetRva != expectedTargetRva)
            {
                throw new InvalidOperationException(
                    $"The validated {label} targets 0x{targetRva:X} instead of 0x{expectedTargetRva:X}.");
            }
        }

        private static bool IsValidTileId(int tileId) =>
            tileId >= 0 && tileId < NativeTileCount;

        private static NativeDetour CreateDetour<TDelegate>(ulong targetAddress, TDelegate callback)
            where TDelegate : Delegate =>
            new NativeDetour(
                (IntPtr)unchecked((long)targetAddress),
                Marshal.GetFunctionPointerForDelegate(callback),
                new NativeDetourConfig { ManualApply = true });

        private static void UndoAndDispose(NativeDetour detour, bool applied)
        {
            if (applied)
                detour?.Undo();
            detour?.Dispose();
        }

        private enum AttackPipelineStage
        {
            Mode,
            Planner,
            Builder
        }

        private enum AttackApproachKind
        {
            UnitFlood,
            BuildingApproach,
            BuildingCandidateConsumer
        }

        private enum BuildingHoverTileSource
        {
            None,
            BuildingTile,
            MouseTile2,
            MouseTile,
            NearestFootprint
        }

        private enum CursorPairFallbackKind
        {
            UnitApproach,
            BuildingApproach,
            DirectTile
        }

        private sealed class AttackCommandScope
        {
            public AttackCommandScope(
                AttackCommandScope previous,
                int sequence,
                int mapEpoch,
                int tribeId,
                TribeAICommand command,
                int targetValue1,
                int targetValue2)
            {
                Previous = previous;
                Sequence = sequence;
                MapEpoch = mapEpoch;
                TribeId = tribeId;
                Command = command;
                TargetValue1 = targetValue1;
                TargetValue2 = targetValue2;
            }

            public AttackCommandScope Previous { get; }
            public int Sequence { get; }
            public int MapEpoch { get; }
            public int TribeId { get; }
            public TribeAICommand Command { get; }
            public int TargetValue1 { get; }
            public int TargetValue2 { get; }
            public HashSet<int> CandidateUnitIds { get; } = new HashSet<int>();
            public Dictionary<int, string> PreCandidateSignatures { get; } =
                new Dictionary<int, string>();
            public HashSet<int> SynchronousTrackerUnitIds { get; } = new HashSet<int>();
            public Dictionary<int, string> LastDecisionByUnit { get; } =
                new Dictionary<int, string>();
            public HashSet<string> AttackApproachDiagnosticSignatures { get; } =
                new HashSet<string>(StringComparer.Ordinal);
            public Dictionary<int, HashSet<int>> PublishedBuildingApproaches { get; } =
                new Dictionary<int, HashSet<int>>();
            public HashSet<int> PublishedUnitAttackApproaches { get; } = new HashSet<int>();
            public int WeightedDecisions { get; set; }
            public int WeightedPublished { get; set; }
            public double WeightedSearchMilliseconds { get; set; }
            public double WeightedMaximumSearchMilliseconds { get; set; }
            public HashSet<int> WeightedUnitIds { get; } = new HashSet<int>();

            public bool Matches(TribeIssueOrderWithTargetEventArgs args, int currentMapEpoch) =>
                MapEpoch == currentMapEpoch && TribeId == args.TribeId &&
                Command == args.AICommand && TargetValue1 == args.TargetValue1 &&
                TargetValue2 == args.TargetValue2;
        }

        private sealed class AttackUnitTracker
        {
            public AttackUnitTracker(
                int mapEpoch,
                int unitId,
                int tribeId,
                TribeAICommand command,
                int targetValue1,
                int targetValue2)
            {
                MapEpoch = mapEpoch;
                UnitId = unitId;
                TribeId = tribeId;
                Command = command;
                TargetValue1 = targetValue1;
                TargetValue2 = targetValue2;
            }

            public int MapEpoch { get; }
            public int UnitId { get; }
            public int TribeId { get; }
            public TribeAICommand Command { get; }
            public int TargetValue1 { get; }
            public int TargetValue2 { get; }
            public bool ModeObserved { get; set; }
            public bool PlannerObserved { get; set; }
            public bool BuilderObserved { get; set; }
            public bool VanillaModeDetected { get; set; }
            public int LastPlannerTargetX { get; set; } = -1;
            public int LastPlannerTargetY { get; set; } = -1;
            public Dictionary<int, HashSet<int>> PublishedBuildingApproaches { get; } =
                new Dictionary<int, HashSet<int>>();

            public void ReplacePublishedBuildingApproaches(
                Dictionary<int, HashSet<int>> approaches)
            {
                PublishedBuildingApproaches.Clear();
                if (approaches == null)
                    return;
                foreach (KeyValuePair<int, HashSet<int>> pair in approaches)
                    PublishedBuildingApproaches[pair.Key] = new HashSet<int>(pair.Value);
            }
        }

        private sealed class MoatMoveTracker
        {
            public MoatMoveTracker(
                int mapEpoch,
                int unitId,
                int tribeId,
                eChimps unitType,
                int playerId,
                int targetX,
                int targetY,
                int builderResult,
                int initialX,
                int initialY,
                int initialPathPosition,
                bool startedOnMoat,
                ushort initialConsumerMode,
                int trackingStartTick)
            {
                MapEpoch = mapEpoch;
                UnitId = unitId;
                TribeId = tribeId;
                UnitType = unitType;
                PlayerId = playerId;
                TargetX = targetX;
                TargetY = targetY;
                BuilderResult = builderResult;
                InitialX = initialX;
                InitialY = initialY;
                LastX = initialX;
                LastY = initialY;
                LastPathPosition = initialPathPosition;
                StartedOnMoat = startedOnMoat;
                LastConsumerMode = initialConsumerMode;
                MinimumConsumerMode = initialConsumerMode;
                MaximumConsumerMode = initialConsumerMode;
                ConsumerModeObservedNonZero = initialConsumerMode != 0;
                TrackingStartTick = trackingStartTick;
                WasOnMoat = startedOnMoat;
                ActualRouteFingerprint = RouteFingerprintOffsetBasis;
                MinimumGroundTransitionTicks = int.MaxValue;
                MinimumMoatTransitionTicks = int.MaxValue;
                RuntimeNativeEstimatedTicks = long.MaxValue;
                RuntimeShadowEstimatedTicks = long.MaxValue;
                LastTileTransitionTick = trackingStartTick;
            }

            public int MapEpoch { get; }
            public int UnitId { get; }
            public int TribeId { get; set; }
            public eChimps UnitType { get; }
            public int PlayerId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int BuilderResult { get; set; }
            public int InitialX { get; }
            public int InitialY { get; }
            public bool StartedOnMoat { get; }
            public int TrackingStartTick { get; }
            public int LastX { get; set; }
            public int LastY { get; set; }
            public int LastPathPosition { get; set; }
            public int LastProgressTick { get; set; } = -1;
            public bool MovementStarted { get; set; }
            public bool MoatEntered { get; set; }
            public bool MoatExited { get; set; }
            public bool WasOnMoat { get; set; }
            public bool StallReported { get; set; }
            public bool CombatInterrupted { get; set; }
            public bool PostCombatRepathEntered { get; set; }
            public bool PostCombatRequiredFriendlyMoat { get; set; }
            public bool MovementResumedAfterCombat { get; set; }
            public bool HasWeightedShadow { get; set; }
            public int WeightedPlayerId { get; set; }
            public eChimps WeightedUnitType { get; set; }
            public TribeAICommand WeightedCommand { get; set; }
            public string WeightedCommandContext { get; set; }
            public int WeightedCommandSequence { get; set; }
            public bool AllowReservedTarget { get; set; }
            public WeightedMovementCostProfile PlanningCostProfile { get; set; }
            public WeightedMoatRouteSummary NativeRouteSummary { get; set; }
            public bool NativeRouteValid { get; set; }
            public bool Calibratable { get; set; }
            public bool ShadowMatchesPublishedCostProfile { get; set; }
            public bool RuntimeCadenceCaptured { get; set; }
            public bool RuntimeCadenceChanged { get; set; }
            public bool RuntimeCadenceRebased { get; set; }
            public WeightedMovementCostProfile RuntimeCostProfile { get; set; }
            public bool RuntimeShadowMatchesPublishedCostProfile { get; set; }
            public long RuntimeNativeEstimatedTicks { get; set; }
            public long RuntimeShadowEstimatedTicks { get; set; }
            public string RuntimeShadowDecision { get; set; }
            public string LastRuntimeCadenceRejection { get; set; }
            public int FirstObservedTick { get; set; } = -1;
            public int LastObservedTick { get; set; } = -1;
            public int LastTileTransitionTick { get; set; } = -1;
            public int FirstTileTransitionTick { get; set; } = -1;
            public bool FirstTransitionTimingUnavailable { get; set; }
            public int TileTransitionCount { get; set; }
            public int TimedTileTransitionCount { get; set; }
            public long TileTransitionTicks { get; set; }
            public int MaximumTileTransitionTicks { get; set; }
            public int ActualGroundTransitions { get; set; }
            public int TimedGroundTransitions { get; set; }
            public long ActualGroundTransitionTicks { get; set; }
            public int MinimumGroundTransitionTicks { get; set; }
            public int MaximumGroundTransitionTicks { get; set; }
            public int ActualMoatTransitions { get; set; }
            public int TimedMoatTransitions { get; set; }
            public long ActualMoatTransitionTicks { get; set; }
            public int MinimumMoatTransitionTicks { get; set; }
            public int MaximumMoatTransitionTicks { get; set; }
            public int ActualCardinalTransitions { get; set; }
            public int TimedCardinalTransitions { get; set; }
            public long ActualCardinalTransitionTicks { get; set; }
            public int ActualDiagonalTransitions { get; set; }
            public int TimedDiagonalTransitions { get; set; }
            public long ActualDiagonalTransitionTicks { get; set; }
            public int ActualDirectionChanges { get; set; }
            public int LastActualDirection { get; set; } = -1;
            public ulong ActualRouteFingerprint { get; set; }
            public long NativeEstimatedTicks { get; set; }
            public long ShadowEstimatedTicks { get; set; }
            public string ShadowDecision { get; set; }
            public bool WeightedPathPublished { get; set; }
            public bool PublishedLengthChecked { get; set; }
            public bool PublishedLengthVerified { get; set; }
            public int ObservedPublishedPathSize { get; set; } = -1;
            public WeightedMoatRouteSummary PublishedRouteSummary { get; set; }
            public string CalibrationReason { get; set; }
            public ushort LastConsumerMode { get; set; }
            public ushort MinimumConsumerMode { get; set; }
            public ushort MaximumConsumerMode { get; set; }
            public bool ConsumerModeObservedNonZero { get; set; }
            public uint ActualMoatOwnerMask { get; set; }
            public int ActualOwnMoatTiles { get; set; }
            public int ActualAlliedMoatTiles { get; set; }
            public int ActualEnemyMoatTiles { get; set; }
            public int ActualInvalidMoatOwnerTiles { get; set; }
            public HashSet<int> ActualMoatTileIds { get; } = new HashSet<int>();
        }

        private sealed class BuilderWeightedScope
        {
            public BuilderWeightedScope(
                int mapEpoch,
                int unitId,
                eChimps unitType,
                int playerId,
                int tribeId,
                TribeAICommand command,
                string commandContext,
                int commandSequence,
                int startX,
                int startY,
                int targetX,
                int targetY,
                int snapshotCurrentX,
                int snapshotCurrentY,
                uint aiState,
                uint rawCommand,
                string workKind,
                string workPhase,
                WeightedMovementCostProfile costProfile,
                bool allowReservedTarget,
                bool calibratable)
            {
                MapEpoch = mapEpoch;
                UnitId = unitId;
                UnitType = unitType;
                PlayerId = playerId;
                TribeId = tribeId;
                Command = command;
                CommandContext = commandContext;
                CommandSequence = commandSequence;
                StartX = startX;
                StartY = startY;
                TargetX = targetX;
                TargetY = targetY;
                SnapshotCurrentX = snapshotCurrentX;
                SnapshotCurrentY = snapshotCurrentY;
                AiState = aiState;
                RawCommand = rawCommand;
                WorkKind = workKind;
                WorkPhase = workPhase;
                CostProfile = costProfile;
                AllowReservedTarget = allowReservedTarget;
                Calibratable = calibratable;
                Candidate = WeightedMoatRouteSummary.Failed("not-evaluated", 0);
            }

            public int MapEpoch { get; }
            public int UnitId { get; }
            public eChimps UnitType { get; }
            public int PlayerId { get; }
            public int TribeId { get; }
            public TribeAICommand Command { get; }
            public string CommandContext { get; }
            public int CommandSequence { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int SnapshotCurrentX { get; }
            public int SnapshotCurrentY { get; }
            public uint AiState { get; }
            public uint RawCommand { get; }
            public string WorkKind { get; }
            public string WorkPhase { get; }
            public string CaptureSource => "unit-builder";
            public WeightedMovementCostProfile CostProfile { get; }
            public bool AllowReservedTarget { get; }
            public bool CandidateFound { get; set; }
            public WeightedMoatRouteSummary Candidate { get; set; }
            public WeightedMoatEncodedRoute CandidateRoute { get; set; }
            public bool Calibratable { get; }
            public double AccumulatedSearchMilliseconds { get; set; }
            public int SearchPasses { get; set; }
            public long OptimisticLowerBoundTicks { get; set; } = long.MaxValue;
            public int PublishedBuilderResult { get; set; } = -1;
        }

        private sealed class WeightedPublicationCandidate
        {
            public WeightedPublicationCandidate(
                WeightedMoatEncodedRoute route,
                WeightedMoatRouteSummary summary)
            {
                Route = route;
                Summary = summary;
            }

            public WeightedMoatEncodedRoute Route { get; }
            public WeightedMoatRouteSummary Summary { get; }
            public long MinimumSaving { get; set; }
            public long MaximumEstimatedTicks { get; set; }
            public List<string> ProfileCosts { get; } = new List<string>();
        }

        private readonly struct AttackRegionFallbackDecision
        {
            private AttackRegionFallbackDecision(
                bool allowed,
                string reason,
                RouteProbeSummary summary,
                int approachX,
                int approachY)
            {
                Allowed = allowed;
                Reason = reason;
                Summary = summary;
                ApproachX = approachX;
                ApproachY = approachY;
            }

            public bool Allowed { get; }
            public string Reason { get; }
            public RouteProbeSummary Summary { get; }
            public int ApproachX { get; }
            public int ApproachY { get; }

            public static AttackRegionFallbackDecision Allow(
                RouteProbeSummary summary,
                int approachX,
                int approachY,
                string reason = "required-friendly-moat-route") =>
                new AttackRegionFallbackDecision(
                    true, reason, summary, approachX, approachY);

            public static AttackRegionFallbackDecision Reject(
                string reason,
                RouteProbeSummary summary = default,
                int approachX = -1,
                int approachY = -1) =>
                new AttackRegionFallbackDecision(false, reason, summary, approachX, approachY);
        }

        private readonly struct AttackApproachState
        {
            public AttackApproachState(
                int generation,
                int depth,
                int queueHead,
                int queueTail,
                int resultCount,
                int usableResultCount,
                int malformedResultCount,
                int firstResultTile,
                int firstCompanionTile,
                int firstScore)
            {
                Generation = generation;
                Depth = depth;
                QueueHead = queueHead;
                QueueTail = queueTail;
                ResultCount = resultCount;
                UsableResultCount = usableResultCount;
                MalformedResultCount = malformedResultCount;
                FirstResultTile = firstResultTile;
                FirstCompanionTile = firstCompanionTile;
                FirstScore = firstScore;
            }

            public int Generation { get; }
            public int Depth { get; }
            public int QueueHead { get; }
            public int QueueTail { get; }
            public int ResultCount { get; }
            public int UsableResultCount { get; }
            public int MalformedResultCount { get; }
            public int FirstResultTile { get; }
            public int FirstCompanionTile { get; }
            public int FirstScore { get; }

            public string ToLogFields() =>
                $"generation={Generation} depth={Depth} queue={QueueHead}->{QueueTail} " +
                $"results={ResultCount} usable={UsableResultCount} malformed={MalformedResultCount} " +
                $"first={FirstResultTile}/{FirstCompanionTile}/{FirstScore}";
        }

        private struct BuildingApproachCandidate
        {
            public BuildingApproachCandidate(int approachTileId, int footprintTileId, int score)
            {
                ApproachTileId = approachTileId;
                FootprintTileId = footprintTileId;
                Score = score;
                ApproachX = -1;
                ApproachY = -1;
                TargetRegion = 0;
                OriginalOrder = -1;
            }

            public int ApproachTileId;
            public int FootprintTileId;
            public int Score;
            public int ApproachX;
            public int ApproachY;
            public int TargetRegion;
            public int OriginalOrder;
        }

        private sealed class BuildingApproachPerformanceScope
        {
            public BuildingApproachPerformanceScope(int commandSequence, int buildingId)
            {
                CommandSequence = commandSequence;
                BuildingId = buildingId;
            }

            public int CommandSequence { get; }
            public int BuildingId { get; }
            public int RegionFallbackEvaluations { get; set; }
            public int RouteEvaluations { get; set; }
            public int IndexScans { get; set; }
            public int IndexedNativeTiles { get; set; }
            public int IndexedFootprintTiles { get; set; }
            public int IndexedApproachTiles { get; set; }
            public int ReachabilityMapsBuilt { get; set; }
            public int ReachabilityCacheHits { get; set; }
            public int MoatOwnerCacheHits { get; set; }
            public int MoatOwnerCacheMisses { get; set; }
            public long TotalElapsedTicks { get; set; }
            public long RegionFallbackElapsedTicks { get; set; }
            public long IndexElapsedTicks { get; set; }
            public Dictionary<int, List<int>> ApproachTilesByRegion { get; set; }
            public Dictionary<int, int> MoatOwnerByTile { get; } =
                new Dictionary<int, int>();

            public double TotalMilliseconds =>
                TotalElapsedTicks * 1000.0 / Stopwatch.Frequency;
            public double RegionFallbackMilliseconds =>
                RegionFallbackElapsedTicks * 1000.0 / Stopwatch.Frequency;
            public double IndexMilliseconds =>
                IndexElapsedTicks * 1000.0 / Stopwatch.Frequency;
        }

        private sealed class BuildingConsumerPerformanceScope
        {
            public BuildingConsumerPerformanceScope(
                int commandSequence, int buildingId, int rawCandidates)
            {
                CommandSequence = commandSequence;
                BuildingId = buildingId;
                RawCandidates = rawCandidates;
            }

            public int CommandSequence { get; }
            public int BuildingId { get; }
            public int RawCandidates { get; }
            public int ValidCandidates { get; set; }
            public int DiggerUnits { get; set; }
            public int RouteEvaluations { get; set; }
            public int ReachabilityMapsBuilt { get; set; }
            public int ReachabilityCacheHits { get; set; }
            public int MoatOwnerCacheHits { get; set; }
            public int MoatOwnerCacheMisses { get; set; }
            public long VanillaElapsedTicks { get; set; }
            public long FallbackElapsedTicks { get; set; }
            public Dictionary<int, int> MoatOwnerByTile { get; } =
                new Dictionary<int, int>();

            public double VanillaMilliseconds =>
                VanillaElapsedTicks * 1000.0 / Stopwatch.Frequency;
            public double FallbackMilliseconds =>
                FallbackElapsedTicks * 1000.0 / Stopwatch.Frequency;
        }

        private readonly struct BuildingConsumerFallbackResult
        {
            private BuildingConsumerFallbackResult(
                bool wasApplied,
                string reason,
                int diggerUnits,
                int publishedCandidates,
                int walkableReservations,
                int missingContexts,
                int invalidContexts,
                int reservedBlocked,
                int ownerRouteRejected,
                RouteProbeSummary summary)
            {
                WasApplied = wasApplied;
                Reason = reason;
                DiggerUnits = diggerUnits;
                PublishedCandidates = publishedCandidates;
                WalkableReservations = walkableReservations;
                MissingContexts = missingContexts;
                InvalidContexts = invalidContexts;
                ReservedBlocked = reservedBlocked;
                OwnerRouteRejected = ownerRouteRejected;
                Summary = summary;
            }

            public bool WasApplied { get; }
            public string Reason { get; }
            public int DiggerUnits { get; }
            public int PublishedCandidates { get; }
            public int WalkableReservations { get; }
            public int MissingContexts { get; }
            public int InvalidContexts { get; }
            public int ReservedBlocked { get; }
            public int OwnerRouteRejected { get; }
            public RouteProbeSummary Summary { get; }

            public static BuildingConsumerFallbackResult NotAttempted(string reason) =>
                new BuildingConsumerFallbackResult(
                    false, reason, 0, 0, 0, 0, 0, 0, 0, default);

            public static BuildingConsumerFallbackResult Rejected(
                string reason,
                int diggerUnits = 0,
                int publishedCandidates = 0,
                int walkableReservations = 0,
                int missingContexts = 0,
                int invalidContexts = 0,
                int reservedBlocked = 0,
                int ownerRouteRejected = 0,
                RouteProbeSummary summary = default) =>
                new BuildingConsumerFallbackResult(
                    false,
                    reason,
                    diggerUnits,
                    publishedCandidates,
                    walkableReservations,
                    missingContexts,
                    invalidContexts,
                    reservedBlocked,
                    ownerRouteRejected,
                    summary);

            public static BuildingConsumerFallbackResult Applied(
                int diggerUnits,
                int publishedCandidates,
                int walkableReservations,
                int missingContexts,
                int invalidContexts,
                int reservedBlocked,
                int ownerRouteRejected,
                RouteProbeSummary summary) =>
                new BuildingConsumerFallbackResult(
                    true,
                    "owner-qualified-friendly-moat-candidates",
                    diggerUnits,
                    publishedCandidates,
                    walkableReservations,
                    missingContexts,
                    invalidContexts,
                    reservedBlocked,
                    ownerRouteRejected,
                    summary);
        }

        private sealed class AttackApproachDiagnosticScope
        {
            private readonly Dictionary<string, int> regionPairCounts =
                new Dictionary<string, int>();
            private readonly Dictionary<string, TilePairAggregate> tilePairGroups =
                new Dictionary<string, TilePairAggregate>();

            public AttackApproachDiagnosticScope(
                AttackCommandScope ownerCommand,
                AttackApproachKind kind,
                int commandSequence,
                TribeAICommand command,
                int tribeId,
                int targetContext,
                int targetX,
                int targetY,
                int requestedResults,
                int sourceRegion,
                int movementClass,
                int unitId,
                int playerId,
                eChimps unitType,
                AttackApproachState before)
            {
                OwnerCommand = ownerCommand;
                Kind = kind;
                CommandSequence = commandSequence;
                Command = command;
                TribeId = tribeId;
                TargetContext = targetContext;
                TargetX = targetX;
                TargetY = targetY;
                RequestedResults = requestedResults;
                SourceRegion = sourceRegion;
                MovementClass = movementClass;
                UnitId = unitId;
                PlayerId = playerId;
                UnitType = unitType;
                Before = before;
            }

            public AttackCommandScope OwnerCommand { get; }
            public AttackApproachKind Kind { get; }
            public int CommandSequence { get; }
            public TribeAICommand Command { get; }
            public int TribeId { get; }
            public int TargetContext { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int RequestedResults { get; }
            public int SourceRegion { get; }
            public int MovementClass { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public eChimps UnitType { get; }
            public AttackApproachState Before { get; }
            public AttackApproachState After { get; set; }
            public Dictionary<string, AttackRegionFallbackDecision> RegionFallbackDecisions { get; } =
                new Dictionary<string, AttackRegionFallbackDecision>(StringComparer.Ordinal);
            public bool? AllSelectedAssassins { get; set; }
            public int? ConsumerVariant { get; set; }
            public string AllSelectedAssassinsText =>
                AllSelectedAssassins.HasValue ? AllSelectedAssassins.Value.ToString() : "not-observed";
            public string ConsumerVariantText =>
                ConsumerVariant.HasValue ? ConsumerVariant.Value.ToString() : "not-applicable";

            public string GetSemanticSignature() =>
                $"{Kind}:{Command}:{TribeId}:{TargetContext}:{TargetX}:{TargetY}:" +
                $"{RequestedResults}:{SourceRegion}:{MovementClass}:{UnitId}:{PlayerId}:{UnitType}:" +
                $"{ConsumerVariantText}:{AllSelectedAssassinsText}:" +
                $"{After.ResultCount}:{After.UsableResultCount}:{After.MalformedResultCount}:" +
                $"{After.FirstResultTile}:{After.FirstCompanionTile}:{After.FirstScore}:" +
                $"{FormatRegionPairs()}:{FormatTilePairs()}";

            public void ObserveRegionPair(
                int movementClass,
                int sourceRegion,
                int targetRegion,
                int routeKind,
                int vanillaResult)
            {
                string key = $"class={movementClass},regions={sourceRegion}->{targetRegion}," +
                    $"routeKind={routeKind},vanilla={vanillaResult}";
                regionPairCounts.TryGetValue(key, out int count);
                regionPairCounts[key] = count + 1;
            }

            public void ObserveTilePair(
                int targetTileId,
                int selectedUnitTileId,
                int selectedRegion,
                int targetRegion,
                int startX,
                int startY,
                int targetX,
                int targetY,
                byte useCache,
                int vanillaResult,
                bool ownerRoute,
                RouteProbeSummary summary)
            {
                string key = $"regions={selectedRegion}->{targetRegion},cache={useCache}," +
                    $"vanilla={vanillaResult},ownerBfs={ownerRoute},friendly={summary.FriendlyMoatTiles}," +
                    $"enemy={summary.EnemyMoatTiles}";
                string pair = $"({startX},{startY})/{selectedUnitTileId}->" +
                    $"({targetX},{targetY})/{targetTileId}";
                if (!tilePairGroups.TryGetValue(key, out TilePairAggregate aggregate))
                {
                    aggregate = new TilePairAggregate(pair);
                    tilePairGroups[key] = aggregate;
                }
                aggregate.Observe(pair);
            }

            public string FormatRegionPairs()
            {
                if (regionPairCounts.Count == 0)
                    return "none";
                List<string> values = new List<string>(regionPairCounts.Count);
                foreach (KeyValuePair<string, int> pair in regionPairCounts)
                    values.Add($"{{{pair.Key},calls={pair.Value}}}");
                values.Sort(StringComparer.Ordinal);
                return string.Join(";", values);
            }

            public string FormatTilePairs()
            {
                if (tilePairGroups.Count == 0)
                    return "none";
                List<string> values = new List<string>(tilePairGroups.Count);
                foreach (KeyValuePair<string, TilePairAggregate> pair in tilePairGroups)
                {
                    values.Add(
                        $"{{{pair.Key},calls={pair.Value.Count},first={pair.Value.First}," +
                        $"last={pair.Value.Last}}}");
                }
                values.Sort(StringComparer.Ordinal);
                return string.Join(";", values);
            }
        }

        private sealed class TilePairAggregate
        {
            public TilePairAggregate(string first)
            {
                First = first;
                Last = first;
            }

            public int Count { get; private set; }
            public string First { get; }
            public string Last { get; private set; }

            public void Observe(string pair)
            {
                Count++;
                Last = pair;
            }
        }

        private sealed class MoveCommandScope
        {
            public MoveCommandScope(
                int sequence,
                int tribeId,
                int targetX,
                int targetY,
                bool isPatrolPath,
                bool isNewOrder,
                TribeMoveType moveType,
                int parentAttackCommandSequence,
                TribeAICommand parentAttackCommand)
            {
                Sequence = sequence;
                TribeId = tribeId;
                TargetX = targetX;
                TargetY = targetY;
                IsPatrolPath = isPatrolPath;
                IsNewOrder = isNewOrder;
                MoveType = moveType;
                ParentAttackCommandSequence = parentAttackCommandSequence;
                ParentAttackCommand = parentAttackCommand;
            }

            public int Sequence { get; }
            public int TribeId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public bool IsPatrolPath { get; }
            public bool IsNewOrder { get; }
            public TribeMoveType MoveType { get; }
            public int ParentAttackCommandSequence { get; }
            public TribeAICommand ParentAttackCommand { get; }
            public int ActiveUnitsAtDispatch { get; set; }
            public int DiggersAtDispatch { get; set; }
            public int UnitsOnMoatAtDispatch { get; set; }
            public uint PlayerMaskAtDispatch { get; set; }
            public int CentralPlannerCalls { get; set; }
            public int FloodCalls { get; set; }
            public int FloodVanillaPositive { get; set; }
            public int FloodFillBypasses { get; set; }
            public int ModeCalls { get; set; }
            public int RegionCalls { get; set; }
            public int BuilderCalls { get; set; }
            public int VanillaBuilderCalls { get; set; }
            public int FallbackBuilderCalls { get; set; }
            public int PositiveBuilderCalls { get; set; }
            public int LastBuilderResult { get; set; } = int.MinValue;
            public int LastVanillaBuilderResult { get; set; } = int.MinValue;
            public bool MoatRelevant { get; set; }
            public bool BuilderReached { get; set; }
            public int WeightedDecisions { get; set; }
            public int WeightedPublished { get; set; }
            public double WeightedSearchMilliseconds { get; set; }
            public double WeightedMaximumSearchMilliseconds { get; set; }
            public HashSet<int> WeightedUnitIds { get; } = new HashSet<int>();
            public string LastGroupMoatModeDiagnostic { get; set; }
            public List<string> Diagnostics { get; } = new List<string>();
        }

        private sealed class PlanScope
        {
            public PlanScope(int unitId, int targetX, int targetY)
            {
                UnitId = unitId;
                TargetX = targetX;
                TargetY = targetY;
            }

            public int UnitId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int PlayerId { get; set; } = -1;
            public bool ModeObserved { get; set; }
            public bool VanillaModeDetected { get; set; }
            public bool FriendlyRouteQualified { get; set; }
            public bool OwnerRouteProbeCompleted { get; set; }
            public bool AttackMovementQualified { get; set; }
            public bool PostCombatRepath { get; set; }
        }

        private sealed class AttackCursorPairScope
        {
            public AttackCursorPairScope(
                int mapEpoch,
                int unitId,
                int playerId,
                int startX,
                int startY,
                int startTileId,
                int targetX,
                int targetY,
                int targetTileId,
                CursorPairFallbackKind fallbackKind,
                int buildingId = 0,
                uint buildingGlobalId = 0,
                int buildingOwnerId = -1,
                eStructs buildingType = eStructs.STRUCT_NULL,
                int hoverBuildingTileId = -1)
            {
                MapEpoch = mapEpoch;
                UnitId = unitId;
                PlayerId = playerId;
                StartX = startX;
                StartY = startY;
                StartTileId = startTileId;
                TargetX = targetX;
                TargetY = targetY;
                TargetTileId = targetTileId;
                FallbackKind = fallbackKind;
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                BuildingOwnerId = buildingOwnerId;
                BuildingType = buildingType;
                HoverBuildingTileId = hoverBuildingTileId;
                CursorPairTargetTileId = targetTileId;
            }

            public int MapEpoch { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int StartTileId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int TargetTileId { get; }
            public CursorPairFallbackKind FallbackKind { get; }
            public int BuildingId { get; }
            public uint BuildingGlobalId { get; }
            public int BuildingOwnerId { get; }
            public eStructs BuildingType { get; }
            public int HoverBuildingTileId { get; }
            public int CursorPairTargetTileId { get; set; }
            public bool GroupCursorAuthorized { get; set; }
            public string GroupSelectionSignature { get; set; }
            public int TargetUnitId { get; set; } = -1;
            public uint TargetUnitGlobalId { get; set; }
        }

        private struct BuildingCursorTarget
        {
            public int BuildingId;
            public uint GlobalId;
            public int OwnerId;
            public eStructs BuildingType;
            public int HoverTileId;
            public BuildingHoverTileSource HoverTileSource;
        }

        private sealed class CursorSelectionDiagnosticScope
        {
            public CursorSelectionDiagnosticScope(
                int mapEpoch,
                int vanillaSelectionResult,
                int unitId,
                int playerId,
                int startX,
                int startY,
                int startTileId,
                int targetX,
                int targetY,
                int targetTileId,
                ulong occupiedSlots,
                bool hasVanillaDiggerSelection,
                bool functionalFallbackArmed,
                CursorPairFallbackKind fallbackKind,
                string rejectionReason,
                int buildingId = 0,
                uint buildingGlobalId = 0,
                eStructs buildingType = eStructs.STRUCT_NULL,
                BuildingHoverTileSource buildingHoverTileSource = BuildingHoverTileSource.None,
                uint rawHoverBuildingId = 0,
                uint rawHoverUnitId = 0,
                uint rawHoverBuildingTileId = 0,
                uint rawMouseTileId2 = 0,
                uint rawHoveringOverWall = 0,
                uint rawMouseTileId = 0)
            {
                MapEpoch = mapEpoch;
                VanillaSelectionResult = vanillaSelectionResult;
                UnitId = unitId;
                PlayerId = playerId;
                StartX = startX;
                StartY = startY;
                StartTileId = startTileId;
                TargetX = targetX;
                TargetY = targetY;
                TargetTileId = targetTileId;
                OccupiedSlots = occupiedSlots;
                HasVanillaDiggerSelection = hasVanillaDiggerSelection;
                FunctionalFallbackArmed = functionalFallbackArmed;
                FallbackKind = fallbackKind;
                RejectionReason = rejectionReason;
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                BuildingType = buildingType;
                BuildingHoverTileSource = buildingHoverTileSource;
                RawHoverBuildingId = rawHoverBuildingId;
                RawHoverUnitId = rawHoverUnitId;
                RawHoverBuildingTileId = rawHoverBuildingTileId;
                RawMouseTileId2 = rawMouseTileId2;
                RawHoveringOverWall = rawHoveringOverWall;
                RawMouseTileId = rawMouseTileId;
            }

            public int MapEpoch { get; }
            public int VanillaSelectionResult { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int StartTileId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int TargetTileId { get; }
            public ulong OccupiedSlots { get; }
            public bool HasVanillaDiggerSelection { get; }
            public bool FunctionalFallbackArmed { get; }
            public CursorPairFallbackKind FallbackKind { get; }
            public string RejectionReason { get; }
            public int BuildingId { get; }
            public uint BuildingGlobalId { get; }
            public eStructs BuildingType { get; }
            public BuildingHoverTileSource BuildingHoverTileSource { get; }
            public uint RawHoverBuildingId { get; }
            public uint RawHoverUnitId { get; }
            public uint RawHoverBuildingTileId { get; }
            public uint RawMouseTileId2 { get; }
            public uint RawHoveringOverWall { get; }
            public uint RawMouseTileId { get; }
        }

        private struct RouteProbeSummary
        {
            public RouteProbeSummary(int playerId)
            {
                PlayerId = playerId;
                FriendlyMoatTiles = 0;
                EnemyMoatTiles = 0;
                InvalidMoatTiles = 0;
                ObservedOwnerMask = 0;
                StartRegion = 0;
                TargetRegion = 0;
                RouteFound = false;
                AttackProbeEvaluated = false;
                ReachedWithMoat = false;
                ReachedWithoutMoat = false;
                RegionTopologyQualified = false;
            }

            public int PlayerId;
            public int FriendlyMoatTiles;
            public int EnemyMoatTiles;
            public int InvalidMoatTiles;
            public uint ObservedOwnerMask;
            public int StartRegion;
            public int TargetRegion;
            public bool RouteFound;
            public bool AttackProbeEvaluated;
            public bool ReachedWithMoat;
            public bool ReachedWithoutMoat;
            public bool RegionTopologyQualified;

            public void MergeObservations(RouteProbeSummary other)
            {
                PlayerId = other.PlayerId;
                FriendlyMoatTiles = Math.Max(FriendlyMoatTiles, other.FriendlyMoatTiles);
                EnemyMoatTiles = Math.Max(EnemyMoatTiles, other.EnemyMoatTiles);
                InvalidMoatTiles = Math.Max(InvalidMoatTiles, other.InvalidMoatTiles);
                ObservedOwnerMask |= other.ObservedOwnerMask;
                if (StartRegion == 0)
                    StartRegion = other.StartRegion;
                if (other.TargetRegion != 0)
                    TargetRegion = other.TargetRegion;
                RouteFound |= other.RouteFound;
                AttackProbeEvaluated |= other.AttackProbeEvaluated;
                ReachedWithMoat |= other.ReachedWithMoat;
                ReachedWithoutMoat |= other.ReachedWithoutMoat;
                RegionTopologyQualified |= other.RegionTopologyQualified;
            }

            public void ObserveOwner(int ownerId)
            {
                if (ownerId >= 0 && ownerId < 32)
                    ObservedOwnerMask |= 1u << ownerId;
            }

            public string ToLogFields()
            {
                string attackFields = AttackProbeEvaluated
                    ? $" attackWithMoat={ReachedWithMoat} attackWithoutMoat={ReachedWithoutMoat} " +
                      $"attackRegionTopology={RegionTopologyQualified}"
                    : string.Empty;
                return $"route={RouteFound} friendlyTiles={FriendlyMoatTiles} " +
                    $"enemyTiles={EnemyMoatTiles} invalidTiles={InvalidMoatTiles} " +
                    $"ownerMask=0x{ObservedOwnerMask:X} regions={StartRegion}->{TargetRegion}" +
                    attackFields;
            }
        }

        private struct CursorGroupRouteSummary
        {
            public string SelectionSignature;
            public int SelectedUnits;
            public int DiggerUnits;
            public int LegallyReachableUnits;
            public int FriendlyMoatSeparatedUnits;
            public int RepresentativeUnitId;
            public int RepresentativeStartX;
            public int RepresentativeStartY;
            public int RepresentativeStartTileId;
            public bool RepresentativeCanDig;
            public bool AllowFallback;
            public RouteProbeSummary ObservedRoute;
        }

        private readonly struct SelectedCursorUnitSnapshot
        {
            public SelectedCursorUnitSnapshot(
                int unitId, int startX, int startY, int startTileId, bool canDig)
            {
                UnitId = unitId;
                StartX = startX;
                StartY = startY;
                StartTileId = startTileId;
                CanDig = canDig;
            }

            public int UnitId { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int StartTileId { get; }
            public bool CanDig { get; }
        }

    }
}
