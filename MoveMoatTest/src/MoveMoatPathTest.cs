using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
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
    internal sealed unsafe partial class MoveMoatPathTest : IDisposable
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

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void CursorMoveStagerDelegate(
            IntPtr unitManager,
            int tribeId,
            int targetX,
            int targetY,
            int targetContext,
            int actionFlags);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool NativeSpecialStructurePredicateDelegate(
            IntPtr structureContext, int tileId);

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
        private const int AlternativePathBuilderRva = 0xDB650;
        private const int GetMoatIdAtTileRva = 0x69560;
        private const int AttackApproachFloodBuilderRva = 0xDBC60;
        private const int BuildingApproachBuilderRva = 0xDA020;
        private const int BuildingCandidateConsumerRva = 0x123090;
        private const int RegionPairReachabilityRva = 0xE2610;
        private const int AttackApproachFloodCallRva = 0x11EE47;
        private const int AttackApproachFloodAlternativeCallRva = 0x11F46B;
        private const int BuildingApproachCallRva = 0x11FF9A;
        private const int BuildingCandidateConsumerCallRva = 0x11FFA7;
        private const int BuildingCandidateConsumerAlternativeCallRva = 0x1206DF;
        private const int BuildingCandidateConsumerForceCallRva = 0x120CCD;
        private const int AttackFloodRegionPairCallRva = 0xDBF0D;
        private const int AttackFloodTilePairCallRva = 0xDBF33;
        private const int BuildingApproachRegionPairCallRva = 0xDA1F9;
        private const int BuildingApproachTilePairCallRva = 0xDA232;
        private const int BuildingApproachAlternativeRegionPairCallRva = 0xDA47C;
        private const int BuildingApproachAlternativeTilePairCallRva = 0xDA4B1;
        private const int BuildingConsumerFallbackBuilderCallRva = 0x123102;
        private const int BuildingConsumerGroundBuilderCallRva = 0x12312C;
        private const int BuildingCursorReachabilityRva = 0xB70C0;
        private const int BuildingCursorReachabilityCallRva = 0x8DFF6;
        private const int CombatFinishResumeRva = 0x1853F0;
        private const int CombatFinishPostCombatCallRva = 0x18540D;
        private const int PostCombatRepathRva = 0x1976C0;
        private const int PostCombatMoveHereCallRva = 0x19772B;
        private const int CursorMoveStagerRva = 0x195E30;
        private const int NativeSpecialStructurePredicateRva = 0x107160;
        private const int NativeSpecialStructureContextRva = 0x32DE440;
        private const int NativeBuildingTypeBiasRva = 0x64CCCDE;
        private const int CursorMoveStagerRegionPairCallRva = 0x195F46;
        private static readonly int[] CursorMoveStagerCallRvas =
            { 0x8F7BA, 0x8FD3C, 0x8FDC6, 0x8FE54 };
        private const int DirectFillApproachRva = 0xE7F60;
        private const int DirectFillApproachRegionPairCallRva = 0xE81EB;
        private const int DirectFillApproachCommandCallRva = 0x120E9D;
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

        private static readonly int[] MoatBuilderSpecialStructureCallRvas =
            { 0xDB29A, 0xDB2EC, 0xDB37A };
        private static readonly int[] MoatReconstructionSpecialStructureCallRvas =
            { 0xE1856, 0xE18DF, 0xE195B };

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
        private const int RouteStateShift = 20;
        private const int RouteCellMask = (1 << RouteStateShift) - 1;
        private const int GroundRouteState = 0;
        private const int FriendlyMoatRouteState = 1;
        private const int EnemyMoatRouteState = 2;
        private const uint CompletedMoatTileFlag = 0x40000000;
        private const uint AlternativeTerrainDelayTileFlag = 0x00200000;
        private const uint OrdinaryWalkableTileFlag = 0x00008000;
        private const uint CursorSpecialStructureTileFlagMask = 0x10000300;
        private const uint BuildingContextBlockingTileFlagMask = 0x0F000000;
        private const int PathManagerRouteVariantOffset = 0x80;
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
        private const int TribeMovementModeOffset = 0x582;
        private const int TribeMovementWaypointBaseOffset = 0x5B4;
        private const int TribeMovementWaypointIndexOffset = 0x5DC;
        private const int TribeMovementWaypointCountOffset = 0x5DE;
        private const int MaximumNativeMovementWaypoints = 10;
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

        private const string CursorMoveStagerPattern =
            "48 89 5C 24 10 48 89 6C 24 18 48 89 74 24 20 48 89 4C 24 08 " +
            "57 41 54 41 55 41 56 41 57 48 83 EC 30 8B 84 24 80 00 00 00 " +
            "48 8B F1 8B AC 24 88 00 00 00";

        private const string DirectFillApproachPattern =
            "44 89 44 24 18 89 54 24 10 53 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 50 45 33 E4 49 63 F9 4C 89";

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

        private const string NativeSpecialStructurePredicatePattern =
            "48 63 C2 48 8D 15 ?? ?? ?? ?? 48 0F BF 14 42 66 85 D2 74 1B " +
            "48 69 D2 9C 00 00 00 0F B7 44 0A 6A 66 83 F8 05 7C 09 " +
            "66 83 F8 0F 74 03 B0 01 C3 32 C0 C3";

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
        private readonly IntPtr nativeSpecialStructureContext;
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
        private NativeSpecialStructurePredicateDelegate nativeSpecialStructurePredicate;
        private AttackApproachFloodBuilderDelegate originalAttackApproachFloodBuilder;
        private AttackApproachFloodBuilderDelegate rootedAttackApproachFloodBuilder;
        private BuildingApproachBuilderDelegate originalBuildingApproachBuilder;
        private BuildingApproachBuilderDelegate rootedBuildingApproachBuilder;
        private BuildingCandidateConsumerDelegate originalBuildingCandidateConsumer;
        private BuildingCandidateConsumerDelegate rootedBuildingCandidateConsumer;
        private RegionPairReachabilityDelegate originalRegionPairReachability;
        private RegionPairReachabilityDelegate rootedRegionPairReachability;
        private BuildingCursorReachabilityDelegate originalBuildingCursorReachability;
        private BuildingCursorReachabilityDelegate rootedBuildingCursorReachability;
        private CombatFinishResumeDelegate originalCombatFinishResume;
        private CombatFinishResumeDelegate rootedCombatFinishResume;
        private CursorMoveStagerDelegate originalCursorMoveStager;
        private CursorMoveStagerDelegate rootedCursorMoveStager;

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
        private NativeDetour cursorMoveStagerDetour;
        private IDisposable tribeMoveSubscription;
        private IDisposable unitMoveSubscription;
        private UnitMoveFrame unitMoveFrame;
        private IDisposable tribeTargetSubscription;
        private IDisposable mapLoadSubscription;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;
        private bool attackTickSubscribed;

        private int[] visitedWithoutMoat;
        private int[] visitedWithMoat;
        private int[] visitedWithEnemyMoat;
        private int[] distanceWithoutMoat;
        private int[] distanceWithMoat;
        private int[] distanceWithEnemyMoat;
        private int[] queue;
        private int[] observedRouteRegions;
        private int[] reachedGroundRegions;
        private int[] reachedFriendlyMoatRegions;
        private int[] reachedEnemyMoatRegions;
        private int gridGeneration;
        private int mapEpoch;
        private int cacheMapEpoch = -1;
        private bool cacheIncludesEnemyRoutes;
        private int cachedReachabilityExpandedNodes;
        private int fallbackContractRejections;
        private int cacheStartX = -1;
        private int cacheStartY = -1;
        private int cachePlayerId = -1;
        private int cachedTraversedRegionCount;
        private int cachedReachabilityMapHits;
        private RouteProbeSummary cachedRouteSummary;
        private readonly HashSet<ulong> loggedBuildingCursorReachabilityDecisions =
            new HashSet<ulong>();
        private readonly Dictionary<int, NativeWaypointQueueTracker> trackedNativeWaypointQueues =
            new Dictionary<int, NativeWaypointQueueTracker>();
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
        private readonly Dictionary<int, string> lastWeightedPublicationDecisionByUnit =
            new Dictionary<int, string>();
        private int moveCommandSequence;
        private int attackCommandSequence;
        private bool callbackFailureReported;
        private bool weightedShadowBusy;
        private bool targetedRouteProbeBusy;
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
            Shared.NativeResolution cursorMoveStagerResolution = Resolve(
                memory, CursorMoveStagerPattern, CursorMoveStagerRva,
                "direct cursor move-command stager");
            Shared.NativeResolution nativeSpecialStructureResolution = Resolve(
                memory, NativeSpecialStructurePredicatePattern,
                NativeSpecialStructurePredicateRva,
                "DAFD0/E1640 special-structure predicate");
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
                CursorMoveStagerRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x6C,
                    0x24, 0x18, 0x48, 0x89, 0x74, 0x24, 0x20, 0x48,
                    0x89, 0x4C, 0x24, 0x08, 0x57, 0x41, 0x54, 0x41,
                    0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC,
                    0x30
                },
                "direct cursor move-command stager entry");
            ValidateCallTarget(
                memory, CursorMoveStagerCallRvas[0], CursorMoveStagerRva,
                new byte[] { 0xE8, 0x71, 0x66, 0x10, 0x00 },
                "primary direct cursor move-command call");
            ValidateCallTarget(
                memory, CursorMoveStagerCallRvas[1], CursorMoveStagerRva,
                new byte[] { 0xE8, 0xEF, 0x60, 0x10, 0x00 },
                "secondary direct cursor move-command call");
            ValidateCallTarget(
                memory, CursorMoveStagerCallRvas[2], CursorMoveStagerRva,
                new byte[] { 0xE8, 0x65, 0x60, 0x10, 0x00 },
                "map-editor direct cursor move-command call");
            ValidateCallTarget(
                memory, CursorMoveStagerCallRvas[3], CursorMoveStagerRva,
                new byte[] { 0xE8, 0xD7, 0x5F, 0x10, 0x00 },
                "alternate direct cursor move-command call");
            ValidateCallTarget(
                memory, CursorMoveStagerRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0xC5, 0xC6, 0xF4, 0xFF },
                "direct cursor move-command region-pair call");
            ValidateCallTarget(
                memory, MoatBuilderSpecialStructureCallRvas[0],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0xC1, 0xBE, 0x02, 0x00 },
                "DAFD0 first special-structure predicate call");
            ValidateCallTarget(
                memory, MoatBuilderSpecialStructureCallRvas[1],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0x6F, 0xBE, 0x02, 0x00 },
                "DAFD0 second special-structure predicate call");
            ValidateCallTarget(
                memory, MoatBuilderSpecialStructureCallRvas[2],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0xE1, 0xBD, 0x02, 0x00 },
                "DAFD0 third special-structure predicate call");
            ValidateCallTarget(
                memory, MoatReconstructionSpecialStructureCallRvas[0],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0x05, 0x59, 0x02, 0x00 },
                "E1640 first special-structure predicate call");
            ValidateCallTarget(
                memory, MoatReconstructionSpecialStructureCallRvas[1],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0x7C, 0x58, 0x02, 0x00 },
                "E1640 second special-structure predicate call");
            ValidateCallTarget(
                memory, MoatReconstructionSpecialStructureCallRvas[2],
                NativeSpecialStructurePredicateRva,
                new byte[] { 0xE8, 0x00, 0x58, 0x02, 0x00 },
                "E1640 third special-structure predicate call");
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
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_AttackMoveToTargetTileX), 0x2D8);
            ValidateGameUnitFieldOffset(nameof(GameUnit.r_AttackMoveToTargetTileY), 0x2DA);
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
            nativeSpecialStructureContext =
                (IntPtr)(libraryBase + NativeSpecialStructureContextRva);
            getMoatIdAtTile = Marshal.GetDelegateForFunctionPointer<GetMoatIdAtTileDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)moatLookupResolution.Rva)));
            getRepresentativeSelectedUnit = Marshal.GetDelegateForFunctionPointer<GetRepresentativeSelectedUnitDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)representativeUnitResolution.Rva)));
            selectionCanDigMoat = Marshal.GetDelegateForFunctionPointer<SelectionCanDigMoatDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)selectionCanDigResolution.Rva)));
            getGroupUnitId = Marshal.GetDelegateForFunctionPointer<GetGroupUnitIdDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)groupUnitResolution.Rva)));
            nativeSpecialStructurePredicate =
                Marshal.GetDelegateForFunctionPointer<NativeSpecialStructurePredicateDelegate>(
                    (IntPtr)(libraryBase +
                        unchecked((ulong)nativeSpecialStructureResolution.Rva)));
            weightedMoatRoutePlanner = new WeightedMoatRoutePlanner(
                nativeRowLookup,
                tileFlags,
                nativeBuildingLayer,
                nativeHeightLayer,
                nativeMovementMasks,
                nativeDirectionMasks,
                (byte*)(libraryBase + NativeBuildingTypeBiasRva),
                ResolveCompletedMoatRelationship,
                ResolveNativeSpecialStructure);
            nativeMovementCadenceResolver = new NativeMovementCadenceResolver(
                memory,
                libraryBase,
                unchecked((ulong)nativeUnitManager),
                log);
            rootedCentralMovementPlan = RunCentralMovementPlanWithContext;
            rootedPathBuilder = BuildPathWithCompletedMoatRouteVariant;
            rootedPathReconstruction = BuildReconstructedUnitPath;
            Resolve(memory, "40 53 48 83 EC 30 44 8B 49 10 33 C0 44 8B 41 0C 48 8B D9 8B 51 08 89 44 24 28 89 81 68 5F 15 00 8B 41 14 89 44 24 20 E8 64 E3 FF FF 8B 83 68 5F 15 00 48 83 C4 30 5B C3", 0xE32B0, "unit field reconstruction");
            ValidateExactBytes(memory, 0xE32B0, new byte[] { 0x40, 0x53, 0x48, 0x83, 0xEC, 0x30, 0x44, 0x8B, 0x49, 0x10, 0x33, 0xC0, 0x44, 0x8B, 0x41, 0x0C, 0x48, 0x8B, 0xD9, 0x8B, 0x51, 0x08, 0x89, 0x44, 0x24, 0x28, 0x89, 0x81, 0x68, 0x5F, 0x15, 0x00, 0x8B, 0x41, 0x14, 0x89, 0x44, 0x24, 0x20, 0xE8, 0x64, 0xE3, 0xFF, 0xFF, 0x8B, 0x83, 0x68, 0x5F, 0x15, 0x00, 0x48, 0x83, 0xC4, 0x30, 0x5B, 0xC3 }, "complete E32B0 function");
            rootedTribeFloodFillMembership = AllowTribeFloodFillForMoveOrder;
            rootedFirstGroupUnitOnCompletedMoat = SelectOwnerSafeGroupMoatMode;
            rootedUnitStandingOnCompletedMoat = EnableCompletedMoatModeForScopedMovement;
            rootedRegionReachability = AllowBuilderAfterFailedRegionSearch;
            rootedCursorReachability = AllowCursorReachabilityThroughCompletedMoat;
            rootedCursorTilePairFallbackSelection = ObserveCursorTilePairFallbackSelection;
            rootedCursorTilePairReachability = AllowAttackCursorTilePairThroughCompletedMoat;
            rootedCursorRegionPrecheck = AllowCursorRegionThroughCompletedMoat;
            rootedCombatFinishResume = ResumeMovementAfterCombatWithMoatContext;
            rootedCursorMoveStager = StageDirectCursorMoveWithOwnerRoute;

            NativeDetour pendingPlanDetour = null;
            NativeDetour pendingCombatFinishResume = null;
            NativeDetour pendingCursorMoveStager = null;
            NativeDetour pendingBuilder = null;
            NativeDetour pendingReconstruction = null;
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
            bool cursorMoveStagerApplied = false;
            bool builderApplied = false;
            bool reconstructionApplied = false;
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
                pendingCursorMoveStager = CreateDetour(
                    libraryBase + unchecked((ulong)cursorMoveStagerResolution.Rva),
                    rootedCursorMoveStager);
                originalCursorMoveStager =
                    pendingCursorMoveStager.GenerateTrampoline<CursorMoveStagerDelegate>();
                pendingBuilder = CreateDetour(
                    libraryBase + unchecked((ulong)builderResolution.Rva),
                    rootedPathBuilder);
                originalPathBuilder = pendingBuilder.GenerateTrampoline<PathBuilderDelegate>();
                pendingReconstruction = CreateDetour(libraryBase + 0xE32B0, rootedPathReconstruction);
                originalPathReconstruction = pendingReconstruction.GenerateTrampoline<PathReconstructionDelegate>();
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
                pendingCursorMoveStager.Apply();
                cursorMoveStagerApplied = true;
                pendingBuilder.Apply();
                builderApplied = true;
                pendingReconstruction.Apply();
                reconstructionApplied = true;
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
                cursorMoveStagerDetour = pendingCursorMoveStager;
                pathBuilderDetour = pendingBuilder;
                pathReconstructionDetour = pendingReconstruction;
                tribeFloodFillMembershipDetour = pendingFlood;
                firstGroupUnitOnCompletedMoatDetour = pendingGroupMoat;
                unitStandingOnCompletedMoatDetour = pendingMode;
                regionReachabilityDetour = pendingRegion;
                cursorReachabilityDetour = pendingCursor;
                cursorTilePairFallbackSelectionDetour = pendingCursorMode;
                cursorTilePairReachabilityDetour = pendingCursorTilePair;
                cursorRegionPrecheckDetour = pendingCursorRegion;

                tribeMoveSubscription = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable.Subscribe(ObserveTribeMoveOrder);
                unitMoveSubscription = UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(ObserveUnitMoveOrder);
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
                    $"directCursorMove=0x{cursorMoveStagerResolution.Rva:X}, " +
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
                TryInstallMoatWorkTargetSelection(memory, libraryBase);
                InstallConnectivityAndRecovery(memory, libraryBase);
                UnityEngine.Application.onBeforeRender += ObserveCursorPerformance;
            }
            catch
            {
                tribeMoveSubscription?.Dispose();
                unitMoveSubscription?.Dispose();
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
                UndoAndDispose(pendingReconstruction, reconstructionApplied);
                UndoAndDispose(pendingBuilder, builderApplied);
                UndoAndDispose(pendingCursorMoveStager, cursorMoveStagerApplied);
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
            unitMoveSubscription?.Dispose();
            tribeTargetSubscription?.Dispose();
            mapLoadSubscription?.Dispose();
            mapStartSubscription?.Dispose();
            mapUnloadSubscription?.Dispose();
            if (attackTickSubscribed)
            {
                GameTimeManagerAPI.Instance.OnTick -= ObserveTrackedAttackStates;
                attackTickSubscribed = false;
            }
            DisposeMoatWorkTargetSelection();
            buildingCandidateConsumerDetour?.Dispose();
            buildingApproachBuilderDetour?.Dispose();
            attackApproachFloodBuilderDetour?.Dispose();
            regionPairReachabilityDetour?.Dispose();
            buildingCursorReachabilityDetour?.Dispose();
            cursorTilePairReachabilityDetour?.Dispose();
            UnityEngine.Application.onBeforeRender -= ObserveCursorPerformance;
            DisposeConnectivityHooks();
            cursorRegionPrecheckDetour?.Dispose();
            cursorTilePairFallbackSelectionDetour?.Dispose();
            cursorReachabilityDetour?.Dispose();
            regionReachabilityDetour?.Dispose();
            unitStandingOnCompletedMoatDetour?.Dispose();
            firstGroupUnitOnCompletedMoatDetour?.Dispose();
            tribeFloodFillMembershipDetour?.Dispose();
            pathBuilderDetour?.Dispose();
            pathReconstructionDetour?.Dispose();
            cursorMoveStagerDetour?.Dispose();
            combatFinishResumeDetour?.Dispose();
            centralMovementPlanDetour?.Dispose();
            ClearUnitMoveFrames();
            activeMoveCommand = null;
            activePlan = null;
            pendingPlan = null;
            pendingAttackCursorPair = null;
            activeAttackCommand = null;
            activeAttackApproachDiagnostic = null;
            activeBuildingApproachPerformance = null;
            activeBuildingConsumerPerformance = null;
            ResetDirectMoatCommandScopes();
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
                Shared.NativeResolution regionPairResolution = Resolve(
                    memory, RegionPairReachabilityPattern, RegionPairReachabilityRva,
                    "attack-approach region-pair reachability helper");
                Shared.NativeResolution directFillApproachResolution = Resolve(
                    memory, DirectFillApproachPattern, DirectFillApproachRva,
                    "direct FillMoat approach search");

                ValidateAttackApproachEntries(memory);
                ValidateAttackApproachCalls(memory);

                rootedAttackApproachFloodBuilder = ObserveAttackApproachFloodBuilder;
                rootedBuildingApproachBuilder = ObserveBuildingApproachBuilder;
                rootedBuildingCandidateConsumer = ObserveBuildingCandidateConsumer;
                rootedRegionPairReachability = ObserveScopedRegionPairReachability;

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
                    $"regionPair=0x{regionPairResolution.Rva:X}, " +
                    $"directFillApproach=0x{directFillApproachResolution.Rva:X}->" +
                    $"0x{DirectFillApproachRegionPairCallRva:X}.");
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
                Shared.DebugLogHelper.LogError(
                    log,
                    "MoveMoat shared E2610/attack-approach hooks were not installed; " +
                    "building commands and direct FillMoat staging remain Vanilla while the " +
                    $"existing movement feature remains active: {ex}");
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

            ValidateCallTarget(memory, AttackFloodRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0xFE, 0x66, 0x00, 0x00 }, "unit flood region-pair call");
            ValidateCallTarget(memory, AttackFloodTilePairCallRva, CursorTilePairReachabilityRva,
                new byte[] { 0xE8, 0x68, 0x6D, 0x00, 0x00 }, "unit flood tile-pair call");

            ValidateCallTarget(memory, BuildingApproachRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0x12, 0x84, 0x00, 0x00 }, "building approach region-pair call");
            ValidateCallTarget(memory, BuildingApproachTilePairCallRva, CursorTilePairReachabilityRva,
                new byte[] { 0xE8, 0x69, 0x8A, 0x00, 0x00 }, "building approach tile-pair call");
            ValidateCallTarget(memory, BuildingApproachAlternativeRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0x8F, 0x81, 0x00, 0x00 }, "alternative building region-pair call");
            ValidateCallTarget(memory, BuildingApproachAlternativeTilePairCallRva, CursorTilePairReachabilityRva,
                new byte[] { 0xE8, 0xEA, 0x87, 0x00, 0x00 }, "alternative building tile-pair call");

            ValidateCallTarget(memory, BuildingConsumerFallbackBuilderCallRva, AlternativePathBuilderRva,
                new byte[] { 0xE8, 0x49, 0x85, 0xFB, 0xFF }, "building consumer fallback-builder call");
            ValidateCallTarget(memory, BuildingConsumerGroundBuilderCallRva, GroundPathBuilderRva,
                new byte[] { 0xE8, 0x5F, 0x74, 0xFB, 0xFF }, "building consumer ground-builder call");
            ValidateExactBytes(
                memory, DirectFillApproachRva,
                new byte[]
                {
                    0x44, 0x89, 0x44, 0x24, 0x18, 0x89, 0x54, 0x24,
                    0x10, 0x53, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55,
                    0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x50
                },
                "direct FillMoat approach-search entry");
            ValidateCallTarget(
                memory, DirectFillApproachCommandCallRva, DirectFillApproachRva,
                new byte[] { 0xE8, 0xBE, 0x70, 0xFC, 0xFF },
                "FillMoat command approach-search call");
            ValidateCallTarget(
                memory, DirectFillApproachRegionPairCallRva, RegionPairReachabilityRva,
                new byte[] { 0xE8, 0x20, 0xA4, 0xFF, 0xFF },
                "FillMoat approach-search region-pair call");
        }

        private void ObserveTribeMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (disposed)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                ClearUnitMoveFrames();
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
                ObserveNativeWaypointQueueAtCommand(
                    activeMoveCommand, "pre", args.TileX, args.TileY);
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
                ClearUnitMoveFrames();
                MoveCommandScope command = activeMoveCommand;
                try
                {
                    ObserveNativeWaypointQueueAtCommand(
                        command, "post", args.TileX, args.TileY);
                    QualifyPendingCommandDiagnostics(command);
                    string lastBuilderResult = command != null && command.BuilderCalls > 0
                        ? command.LastBuilderResult.ToString()
                        : "none";
                    string lastVanillaBuilderResult = command != null &&
                        command.VanillaBuilderCalls > 0
                            ? command.LastVanillaBuilderResult.ToString()
                            : "none";
                    if (command != null)
                    {
                        command.ElapsedMilliseconds =
                            (Stopwatch.GetTimestamp() - command.StartTimestamp) * 1000.0 /
                            Stopwatch.Frequency;
                    }
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
                        $"nativeModeEntries={nativeModeEntries} preBuilderFailures={preBuilderFailures} preBuilderRecovered={preBuilderRecovered} " +
                        $"preBuilderRejected={FormatRecoveryRejections()} " +
                        $"cursorTopologyBuilds={cursorTopologyBuilds} cursorTopologyUpdates={cursorTopologyUpdates} " +
                        $"unitMoveCalls={command?.UnitMoveCalls ?? 0} " +
                        $"unitMoveCompleted={command?.UnitMoveCompleted ?? 0} " +
                        $"unitMovePositive={command?.UnitMovePositive ?? 0} " +
                        $"unitMoveWithoutBuilder={command?.UnitMoveWithoutBuilder ?? 0} " +
                        $"unitMoveAlreadyArrived={command?.UnitMoveAlreadyArrived ?? 0} " +
                        $"searchRunsTotal={weightedMoatRoutePlanner.SearchRuns} searchNodesTotal={weightedMoatRoutePlanner.SearchNodes} " +
                        $"sharedFieldHitsTotal={weightedMoatRoutePlanner.SharedFieldHits} nativeRegionQueriesTotal={nativeGroundQueries} " +
                        $"nativeRegionCacheHitsTotal={nativeGroundCacheHits} " +
                        $"unitMoveAbandoned={command?.UnitMoveAbandoned ?? 0} " +
                        $"builderIntermediateTargets={command?.BuilderIntermediateTargets ?? 0} " +
                        $"floodCalls={command?.FloodCalls ?? 0} " +
                        $"floodVanillaPositive={command?.FloodVanillaPositive ?? 0} " +
                        $"floodBypasses={command?.FloodFillBypasses ?? 0} " +
                        $"modeCalls={command?.ModeCalls ?? 0} regionCalls={command?.RegionCalls ?? 0} " +
                        $"builderCalls={command?.BuilderCalls ?? 0} " +
                        $"vanillaBuilderCalls={command?.VanillaBuilderCalls ?? 0} " +
                        $"fallbackBuilderCalls={command?.FallbackBuilderCalls ?? 0} " +
                        $"contractRejections={command?.FallbackContractRejections ?? 0} " +
                        $"contractReasons={FormatContractRejectionReasons(command)} " +
                        $"fallbackRollbacks={command?.FallbackRollbacks ?? 0} " +
                        $"positiveBuilders={command?.PositiveBuilderCalls ?? 0} " +
                        $"weightedUnits={command?.WeightedUnitIds.Count ?? 0} " +
                        $"weightedDecisions={command?.WeightedDecisions ?? 0} " +
                        $"weightedPublished={command?.WeightedPublished ?? 0} " +
                        $"weightedSearchMs={(command?.WeightedSearchMilliseconds ?? 0):F3} " +
                        $"weightedMaxSearchMs={(command?.WeightedMaximumSearchMilliseconds ?? 0):F3} " +
                        $"targetedSearches={command?.TargetedRouteSearches ?? 0} " +
                        $"targetedSearchPasses={command?.TargetedRouteSearchPasses ?? 0} " +
                        $"targetedCacheHits={command?.TargetedRouteCacheHits ?? 0} " +
                        $"targetedExpanded={command?.TargetedRouteExpandedNodes ?? 0} " +
                        $"targetedSearchMs={(command?.TargetedRouteSearchMilliseconds ?? 0):F3} " +
                        $"targetedMaxSearchMs={(command?.TargetedRouteMaximumSearchMilliseconds ?? 0):F3} " +
                        $"elapsedMs={(command?.ElapsedMilliseconds ?? 0):F3} " +
                        $"lastVanillaBuilderResult={lastVanillaBuilderResult} " +
                        $"lastBuilderResult={lastBuilderResult}");
                    LogQueuedMoveHereOutcome(command, args.ReturnValue);
                    FlushCommandDiagnostics(command);
                }
                catch
                {
                    // Diagnostics must not escape into the synchronous command event.
                }
                activeMoveCommand = null;
                activePlan = null;
                pendingPlan = null;
                ClearUnitMoveFrames();
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
                    BeginDirectFillCommand(args);
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
                if (args.Phase == EventHookPhase.Post)
                    EndDirectFillCommand(args);
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

            ClearUnitMoveFrames();

            // The Script Extender raises no Post event when another subscriber handles a
            // command through SkipOriginalFunction (QueueTest deliberately does this). These
            // scopes are synchronous by contract and must never leak into the next game tick.
            if (activeMoveCommand != null)
            {
                try
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "MoveMoat " +
                        $"stage=move-command-incomplete commandSeq={activeMoveCommand.Sequence} " +
                        $"tribe={activeMoveCommand.TribeId} " +
                        $"target=({activeMoveCommand.TargetX},{activeMoveCommand.TargetY}) " +
                        "reason=no-post-event-before-next-tick.");
                }
                catch
                {
                    // Cleanup remains mandatory even if diagnostics fail.
                }
                activeMoveCommand = null;
                activePlan = null;
                pendingPlan = null;
            }
            if (activeAttackCommand != null)
            {
                try
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        "MoveMoat " +
                        $"stage=target-command-incomplete commandSeq={activeAttackCommand.Sequence} " +
                        $"tribe={activeAttackCommand.TribeId} command={activeAttackCommand.Command} " +
                        $"target={activeAttackCommand.TargetValue1}/" +
                        $"{activeAttackCommand.TargetValue2} " +
                        "reason=no-post-event-before-next-tick.");
                }
                catch
                {
                    // Cleanup remains mandatory even if diagnostics fail.
                }
                activeAttackCommand = null;
                if (pendingPlan != null && pendingPlan.AttackMovementQualified)
                    pendingPlan = null;
            }
            ClearIncompleteDirectFillScopeAtTick();

            // A work-target handoff is strictly synchronous (0x6AF60 -> 0x196280). If the
            // expected planner/builder was never entered, do not let it affect a later tick.
            if (pendingPlan != null && pendingPlan.MoatWorkMovement)
                pendingPlan = null;

            ObserveNativeWaypointQueues(tick);

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

        private void ObserveNativeWaypointQueueAtCommand(
            MoveCommandScope command, string phase, int requestedX, int requestedY)
        {
            try
            {
                if (command == null || !TryReadNativeWaypointQueue(
                        command.TribeId, out NativeWaypointQueueSnapshot snapshot))
                {
                    return;
                }

                if (string.Equals(phase, "pre", StringComparison.Ordinal))
                {
                    command.QueuePreSnapshot = snapshot;
                    command.HasQueuePreSnapshot = true;
                }
                else if (string.Equals(phase, "post", StringComparison.Ordinal))
                {
                    command.QueuePostSnapshot = snapshot;
                    command.HasQueuePostSnapshot = true;
                }

                bool alreadyTracked = trackedNativeWaypointQueues.TryGetValue(
                    command.TribeId, out NativeWaypointQueueTracker tracker);
                if (snapshot.Count == 0 && !alreadyTracked)
                    return;
                if (!alreadyTracked)
                {
                    tracker = new NativeWaypointQueueTracker(command.TribeId);
                    trackedNativeWaypointQueues.Add(command.TribeId, tracker);
                }
                tracker.LastCommandSequence = command.Sequence;
                tracker.LastRequestedX = requestedX;
                tracker.LastRequestedY = requestedY;
                LogNativeWaypointQueueChange(tracker, snapshot, $"command-{phase}", -1);
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("native-waypoint-queue-command", ex);
            }
        }

        private void ObserveNativeWaypointQueues(int tick)
        {
            if (trackedNativeWaypointQueues.Count == 0)
                return;

            try
            {
                var tribeIds = new List<int>(trackedNativeWaypointQueues.Keys);
                foreach (int tribeId in tribeIds)
                {
                    if (!trackedNativeWaypointQueues.TryGetValue(
                            tribeId, out NativeWaypointQueueTracker tracker) ||
                        !TryReadNativeWaypointQueue(tribeId, out NativeWaypointQueueSnapshot snapshot))
                    {
                        trackedNativeWaypointQueues.Remove(tribeId);
                        continue;
                    }

                    bool changed = LogNativeWaypointQueueChange(
                        tracker, snapshot, "simulation-tick", tick);
                    if (snapshot.Count == 0)
                    {
                        tracker.EmptyTicks = changed ? 0 : tracker.EmptyTicks + 1;
                        if (tracker.EmptyTicks >= 2)
                            trackedNativeWaypointQueues.Remove(tribeId);
                    }
                    else
                    {
                        tracker.EmptyTicks = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("native-waypoint-queue-tick", ex);
            }
        }

        private bool TryReadNativeWaypointQueue(
            int tribeId, out NativeWaypointQueueSnapshot snapshot)
        {
            snapshot = default;
            if (nativeTribeManager == IntPtr.Zero || tribeId < 0 || tribeId >= MaximumTribeCount)
                return false;

            byte* tribe = (byte*)nativeTribeManager.ToPointer() + tribeId * TribeRecordSize;
            int index = *(ushort*)(tribe + TribeMovementWaypointIndexOffset);
            int count = *(ushort*)(tribe + TribeMovementWaypointCountOffset);
            int mode = *(short*)(tribe + TribeMovementModeOffset);
            if (index < 0 || index > MaximumNativeMovementWaypoints ||
                count < 0 || count > MaximumNativeMovementWaypoints)
            {
                return false;
            }

            int currentX = -1;
            int currentY = -1;
            if (count > 0 && index < count && index < MaximumNativeMovementWaypoints)
            {
                currentX = *(ushort*)(tribe + TribeMovementWaypointBaseOffset + index * 4);
                currentY = *(ushort*)(tribe + TribeMovementWaypointBaseOffset + index * 4 + 2);
            }
            int lastX = -1;
            int lastY = -1;
            if (count > 0)
            {
                int last = count - 1;
                lastX = *(ushort*)(tribe + TribeMovementWaypointBaseOffset + last * 4);
                lastY = *(ushort*)(tribe + TribeMovementWaypointBaseOffset + last * 4 + 2);
            }
            snapshot = new NativeWaypointQueueSnapshot(
                index, count, mode, currentX, currentY, lastX, lastY);
            return true;
        }

        private bool LogNativeWaypointQueueChange(
            NativeWaypointQueueTracker tracker,
            NativeWaypointQueueSnapshot snapshot,
            string source,
            int tick)
        {
            string signature = snapshot.ToString();
            if (string.Equals(tracker.LastSignature, signature, StringComparison.Ordinal))
                return false;
            tracker.LastSignature = signature;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=native-waypoint-queue source={source} tick={tick} " +
                $"commandSeq={tracker.LastCommandSequence} tribe={tracker.TribeId} " +
                $"requested=({tracker.LastRequestedX},{tracker.LastRequestedY}) {signature}.");
            return true;
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
                    if (plan.MoatWorkTargetTileId > 0)
                        existing.WorkTargetMoatTileId = plan.MoatWorkTargetTileId;
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
                tracker.WorkTargetMoatTileId = plan.MoatWorkTargetTileId;
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

            trackedMoatMoves.TryGetValue(shadow.UnitId, out MoatMoveTracker tracker);
            bool sameTrackedRoute = tracker != null &&
                tracker.MapEpoch == mapEpoch && tracker.TribeId == shadow.TribeId &&
                tracker.TargetX == shadow.TargetX && tracker.TargetY == shadow.TargetY &&
                tracker.InitialX == shadow.StartX && tracker.InitialY == shadow.StartY;
            bool weightedPathPublished = string.Equals(
                decision, "weighted-path-published", StringComparison.Ordinal);
            // Runtime tracking is retained only for paths changed by this mod. A native moat
            // path that needed no fallback is Vanilla behavior and must not create per-tick work.
            if (!weightedPathPublished && !sameTrackedRoute)
                return;
            int workTargetMoatTileId = sameTrackedRoute
                ? tracker.WorkTargetMoatTileId
                : 0;
            if (!sameTrackedRoute)
            {
                tracker = new MoatMoveTracker(
                    mapEpoch, shadow.UnitId, shadow.TribeId, shadow.UnitType, shadow.PlayerId,
                    shadow.TargetX, shadow.TargetY,
                    builderResult, shadow.StartX, shadow.StartY,
                    unit->p_CurrentPathPlanPosition,
                    IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)),
                    ReadUnitMoatPathConsumptionMode(unit),
                    CaptureCurrentGameTick());
                // The builder can refresh the movement tracker after 0x6AF60 selected a
                // work moat. Preserve that identity so owner observations remain classifiable.
                tracker.WorkTargetMoatTileId = workTargetMoatTileId;
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
            tracker.WeightedPathPublished = weightedPathPublished;
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
                        ObserveActualMoatOwnership(tracker, currentTileId, unit, tick);
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
                    if (tracker.LastProgressTick < 0 || progressed)
                        tracker.LastProgressTick = tick;

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
                    }

                    if (currentMoat && !tracker.MoatEntered)
                    {
                        tracker.MoatEntered = true;
                    }
                    if (tracker.MoatEntered && !tracker.MoatExited &&
                        tracker.WasOnMoat && !currentMoat)
                    {
                        tracker.MoatExited = true;
                    }

                    if (reachedRequestedTarget && pathConsumed && settledOnCurrentTile)
                    {
                        EndTrackedMoatMove(unitId, tracker, "path-completed-at-target");
                        continue;
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

        private void ObserveActualMoatOwnership(
            MoatMoveTracker tracker, int tileId, GameUnit* unit, int tick)
        {
            if (!IsCompletedMoatTile(tileId) || !tracker.ActualMoatTileIds.Add(tileId))
                return;

            if (!TryReadCompletedMoatOwner(tileId, out int moatId, out int ownerId))
            {
                tracker.ActualInvalidMoatOwnerTiles++;
                tracker.ActualMoatOwnerAtFirstObservation[tileId] = -1;
                return;
            }

            tracker.ActualMoatOwnerAtFirstObservation[tileId] = ownerId;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(tracker.PlayerId))
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
            {
                tracker.ActualEnemyMoatTiles++;
                bool matchesWorkTarget = tileId == tracker.WorkTargetMoatTileId;
                if (matchesWorkTarget)
                    tracker.ActualEnemyMoatTilesMatchingWorkTarget++;
                else
                    tracker.ActualEnemyMoatTilesOutsideWorkTarget++;
                var position = GameTileManagerAPI.Instance.GetTileVectorFromId(tileId);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=owner-safety-observation tick={tick} " +
                    $"unit={tracker.UnitId} player={tracker.PlayerId} " +
                    $"command={tracker.WeightedCommand} " +
                    $"commandContext={tracker.WeightedCommandContext ?? "unresolved"} " +
                    $"tile={tileId}/({position.X},{position.Y}) moat={moatId} owner={ownerId} " +
                    $"workTargetTile={tracker.WorkTargetMoatTileId} " +
                    $"matchesWorkTarget={matchesWorkTarget} " +
                    $"requestedTarget=({tracker.TargetX},{tracker.TargetY}) " +
                    $"path={unit->p_CurrentPathPlanPosition}/{unit->p_PathPlanSize}.");
            }
        }

        private bool TryReadCompletedMoatOwner(int tileId, out int moatId, out int ownerId)
        {
            moatId = 0;
            ownerId = -1;
            if (!IsCompletedMoatTile(tileId) || getMoatIdAtTile == null)
                return false;

            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero)
                return false;
            moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (!IsValidMoatRecordId(moatId, moatCount))
                return false;

            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            ownerId = moatRecord[MoatOwnerOffset];
            return GamePlayerManagerAPI.Instance.IsPlayerIdValid(ownerId);
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
            bool ownerSafeActualRoute = tracker.ActualEnemyMoatTilesOutsideWorkTarget == 0 &&
                tracker.ActualInvalidMoatOwnerTiles == 0;
            int ownerChangedTiles = 0;
            int noLongerCompletedMoatTiles = 0;
            foreach (KeyValuePair<int, int> observed in tracker.ActualMoatOwnerAtFirstObservation)
            {
                if (!IsCompletedMoatTile(observed.Key))
                {
                    noLongerCompletedMoatTiles++;
                    continue;
                }
                if (!TryReadCompletedMoatOwner(observed.Key, out _, out int finalOwner) ||
                    finalOwner != observed.Value)
                {
                    ownerChangedTiles++;
                }
            }
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
                $"enemyMoatClassification=workTarget:{tracker.ActualEnemyMoatTilesMatchingWorkTarget}/" +
                $"traversed:{tracker.ActualEnemyMoatTilesOutsideWorkTarget} " +
                $"workTargetMoatTile={tracker.WorkTargetMoatTileId} " +
                $"ownerRecheck=changed:{ownerChangedTiles}/noLongerCompleted:{noLongerCompletedMoatTiles} " +
                $"ownerSafetyViolation={tracker.ActualEnemyMoatTilesOutsideWorkTarget > 0 || tracker.ActualInvalidMoatOwnerTiles > 0} " +
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

            if (!CaptureCursorSelection(expectedPlayerId, out int[] selectedUnitIds, out _)) return false;
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
            PlanScope previousPending = pendingPlan;
            PlanScope inherited = previous ?? pendingPlan;
            PlanScope plan = inherited != null &&
                (inherited.PostCombatRepath || inherited.MoatWorkMovement) &&
                inherited.UnitId == unitId && inherited.TargetX == targetX &&
                inherited.TargetY == targetY
                    ? inherited
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
                        if (ShouldLogUnitPipeline)
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
                pendingPlan = previous != null ? previousPending : null;
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

        // The extender owns 0x196280. Its synchronous event also covers the direct
        // group calls which never enter our temporary-path probe at 0x18E1E0.
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

            command.ActiveUnitIdsAtDispatch = unitIds;

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

        private CompletedMoatRelationship ResolveCompletedMoatRelationship(
            int playerId, int tileId)
        {
            if (!IsCompletedMoatTile(tileId))
                return CompletedMoatRelationship.Invalid;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(playerId))
                return CompletedMoatRelationship.Invalid;

            int moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (!IsValidMoatRecordId(moatId, moatCount))
                return CompletedMoatRelationship.Invalid;
            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            if (*(int*)moatRecord != tileId) return CompletedMoatRelationship.Invalid;
            int ownerId = moatRecord[MoatOwnerOffset];
            if (!playerApi.IsPlayerIdValid(ownerId))
                return CompletedMoatRelationship.Invalid;
            return ownerId == playerId || playerApi.IsPlayerAlliedTo(playerId, ownerId)
                ? CompletedMoatRelationship.Friendly
                : CompletedMoatRelationship.Enemy;
        }

        private bool IsFriendlyCompletedMoatForWeightedShadow(int playerId, int tileId) =>
            ResolveCompletedMoatRelationship(playerId, tileId) ==
                CompletedMoatRelationship.Friendly;

        private bool IsIsolatedActiveGroupUnit(int unitId, int tribeId)
        {
            if (tribeId < 0 || tribeId >= MaximumTribeCount || getGroupUnitId == null)
                return false;
            byte* tribeRecord = (byte*)nativeTribeManager.ToPointer() +
                tribeId * TribeRecordSize;
            return *(short*)(tribeRecord + TribeUnitCountOffset) == 1 &&
                getGroupUnitId(nativeTribeManager, tribeId, 0) == unitId;
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
            BuilderWeightedScope shadow, string decision)
        {
            RecordFillRouteDecision(shadow, decision);
            RecordWeightedCommandDecision(shadow, decision);
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
            // Baseline semantics: argument 2 is the player filter here, despite this legacy
            // delegate parameter name used by the older movement diagnostics.
            if (TryAllowDigWorkRegionSearch(
                    pathManager, movementClass, targetRegion, startX, startY,
                    vanillaResult, out int workResult))
            {
                return workResult;
            }
            if (TryAllowUnitMoveRegion(pathManager, movementClass, targetRegion, startX, startY, vanillaResult, out int unitResult))
                return unitResult;
            if (TryAllowEarlyMoveHereGroupRegion(
                    pathManager, movementClass, targetRegion, startX, startY,
                    vanillaResult, out int moveHereResult))
            {
                return moveHereResult;
            }
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

                if (ShouldLogUnitPipeline)
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

        private bool TryAllowEarlyMoveHereGroupRegion(
            IntPtr pathManager,
            int playerId,
            int targetRegion,
            int startX,
            int startY,
            int vanillaResult,
            out int effectiveResult)
        {
            effectiveResult = vanillaResult;
            MoveCommandScope command = activeMoveCommand;
            if (vanillaResult != 0 || command == null || disposed ||
                activePlan != null || pendingPlan != null ||
                pathManager != nativePathManager ||
                startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth ||
                targetRegion <= 0 || targetRegion > MaximumRegionId)
            {
                return false;
            }

            try
            {
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                    return false;
                int[] unitIds = command.ActiveUnitIdsAtDispatch;
                if (unitIds.Length == 0 ||
                    !GroupContainsCurrentPosition(unitIds, playerId, startX, startY))
                {
                    return false;
                }

                command.RegionCalls++;
                EarlyGroupRegionDecision decision = EvaluateEarlyMoveHereGroupRegion(
                    command, unitIds, playerId, targetRegion, null, "E7C40");
                try
                {
                    LogEarlyMoveHereGroupRegionDecision(
                        command, decision, playerId, null, targetRegion, "E7C40");
                }
                catch
                {
                    // Diagnostics must not undo an otherwise valid scoped decision.
                }
                if (!decision.Allowed)
                    return false;

                command.EarlyRegionBypasses++;
                effectiveResult = targetRegion;
                return true;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("movehere-group-region-search", ex);
                effectiveResult = vanillaResult;
                return false;
            }
        }

        private EarlyGroupRegionDecision EvaluateEarlyMoveHereGroupRegion(
            MoveCommandScope command,
            int[] unitIds,
            int playerId,
            int targetRegion,
            int? sourceRegion,
            string helper)
        {
            command.EarlyRegionCalls++;
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(
                command.TargetX, command.TargetY);
            if (!IsValidTileId(targetTileId) || pathRegionGrid[targetTileId] != targetRegion)
            {
                return new EarlyGroupRegionDecision(
                    false, 0, "command-target-region-mismatch", new RouteProbeSummary(playerId));
            }

            string key = $"{helper}:{playerId}:{sourceRegion?.ToString() ?? "none"}:" +
                $"{targetRegion}:{command.TargetX}:{command.TargetY}";
            if (command.EarlyRegionDecisions.TryGetValue(
                    key, out EarlyGroupRegionDecision cached))
            {
                return cached;
            }

            RouteProbeSummary observed = new RouteProbeSummary(playerId);
            int diggers = 0;
            foreach (int unitId in unitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != command.TribeId ||
                    unit->r_ControllableForPlayerId != playerId || !CanDigMoat(unit))
                {
                    continue;
                }

                int startTileId = GameTileManagerAPI.Instance.GetTileId(
                    unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
                if (!IsValidTileId(startTileId))
                    continue;
                int unitSourceRegion = pathRegionGrid[startTileId];
                if (sourceRegion.HasValue && unitSourceRegion != sourceRegion.Value)
                    continue;

                diggers++;
                var plan = new PlanScope(unitId, command.TargetX, command.TargetY)
                {
                    PlayerId = playerId
                };
                if (TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        plan, out RouteProbeSummary summary))
                {
                    observed.MergeObservations(summary);
                    var allowed = new EarlyGroupRegionDecision(
                        true, unitId, "required-friendly-moat-route", summary);
                    command.EarlyRegionDecisions[key] = allowed;
                    MarkCommandMoatRelevant(command, summary);
                    return allowed;
                }
                observed.MergeObservations(summary);
            }

            var rejected = new EarlyGroupRegionDecision(
                false,
                0,
                diggers == 0 ? "no-matching-digger" : "no-required-friendly-moat-route",
                observed);
            command.EarlyRegionDecisions[key] = rejected;
            return rejected;
        }

        private bool GroupContainsCurrentPosition(
            int[] unitIds, int playerId, int startX, int startY)
        {
            foreach (int unitId in unitIds)
            {
                if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) &&
                    unit != null && unit->r_AliveState == AliveState.IsAlive &&
                    unit->r_ControllableForPlayerId == playerId &&
                    unit->r_CurrentTilePositionX == startX &&
                    unit->r_CurrentTilePositionY == startY)
                {
                    return true;
                }
            }
            return false;
        }

        private void LogEarlyMoveHereGroupRegionDecision(
            MoveCommandScope command,
            EarlyGroupRegionDecision decision,
            int playerId,
            int? sourceRegion,
            int targetRegion,
            string helper)
        {
            string signature = $"early-group-region:{helper}:{playerId}:" +
                $"{sourceRegion?.ToString() ?? "none"}:{targetRegion}:" +
                $"{decision.Allowed}:{decision.UnitId}:{decision.Reason}";
            if (!command.EarlyRegionLogSignatures.Add(signature))
                return;
            LogCommandDiagnostic(
                $"stage=movehere-group-region commandSeq={command.Sequence} " +
                $"tribe={command.TribeId} player={playerId} helper={helper} " +
                $"regions={sourceRegion?.ToString() ?? "coordinates"}->{targetRegion} " +
                $"target=({command.TargetX},{command.TargetY}) vanilla=0 " +
                $"effective={(decision.Allowed ? targetRegion : 0)} " +
                $"qualifyingUnit={decision.UnitId} reason={decision.Reason} " +
                decision.Summary.ToLogFields());
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
                bool bypass = vanillaResult == 0 && managerValid && matchingTribe && stampValid &&
                    command.DiggersAtDispatch > 0 &&
                    TryQualifyMoveCommandFloodBypass(command);
                if (command != null)
                {
                    command.FloodCalls++;
                    if (vanillaResult != 0)
                        command.FloodVanillaPositive++;
                }
                if (!bypass)
                    return vanillaResult;

                command.FloodFillBypasses++;
                try
                {
                    if (ShouldLogUnitPipeline)
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

        private int SelectOwnerSafeGroupMoatMode(IntPtr tribeManager, int tribeId)
        {
            int vanillaResult = originalFirstGroupUnitOnCompletedMoat(tribeManager, tribeId);
            if (disposed)
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
                if (leadUnitId <= 0 || unitCount <= 0 || unitCount > MaximumUnitCount)
                    return vanillaResult;

                // A ground leader and a later moat member still need qualification.
                // Returning here used to strand the whole tribe on an isolated PCL.
                command.MoatRelevant |= vanillaResult > 0;

                int activeUnitsOnMoat = 0;
                int activeUnitsOffMoat = 0;
                int diggerUnits = 0;
                int qualifyingDiggerUnitsOnMoat = 0;
                int qualifyingUnitId = 0;
                bool leadUnitObserved = false;
                RouteProbeSummary qualifyingRoute = default;
                var probedDiggerStarts = new HashSet<int>();
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
                    if (unitId == leadUnitId)
                        leadUnitObserved = true;

                    bool onCompletedMoat =
                        (tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
                    if (onCompletedMoat)
                        activeUnitsOnMoat++;
                    else
                        activeUnitsOffMoat++;

                    if (!CanDigMoat(unit))
                        continue;
                    diggerUnits++;
                    // Qualify a required crossing before choosing a native group branch;
                    // both island starters and actual moat starters can supply evidence.
                    if (qualifyingUnitId != 0)
                        continue;
                    int startRegion = pathRegionGrid[startTileId];
                    int startKey = startRegion > 0 ? startRegion : -startTileId - 1;
                    if (!probedDiggerStarts.Add(startKey))
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

                    if (onCompletedMoat)
                        qualifyingDiggerUnitsOnMoat++;
                    qualifyingUnitId = unitId;
                    qualifyingRoute = route;
                }

                bool mixedPositions = activeUnitsOnMoat > 0 && activeUnitsOffMoat > 0;
                int targetTileId = command.TargetX >= 0 && command.TargetX < MapWidth &&
                    command.TargetY >= 0 && command.TargetY < MapWidth
                        ? GameTileManagerAPI.Instance.GetTileId(
                            command.TargetX, command.TargetY)
                        : 0;
                int targetRegion = IsValidTileId(targetTileId)
                    ? pathRegionGrid[targetTileId]
                    : 0;
                bool targetIsFriendlyCompletedMoat = IsValidTileId(targetTileId) &&
                    IsCompletedMoatTile(targetTileId) && qualifyingUnitId > 0 &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(
                        qualifyingUnitId, out GameUnit* qualifyingUnit) &&
                    qualifyingUnit != null &&
                    ResolveCompletedMoatRelationship(
                        qualifyingUnit->r_ControllableForPlayerId, targetTileId) ==
                        CompletedMoatRelationship.Friendly;
                bool forceSharedMoatMode = vanillaResult != leadUnitId && leadUnitObserved &&
                    ((targetRegion > 0 && targetRegion <= MaximumRegionId) ||
                     targetIsFriendlyCompletedMoat) &&
                    qualifyingUnitId > 0;
                bool normalize = vanillaResult > 0 && mixedPositions &&
                    qualifyingUnitId > 0;
                if (vanillaResult > 0 || forceSharedMoatMode)
                {
                    command.MoatRelevant = true;
                    MarkCommandMoatRelevant(command, qualifyingRoute);
                }
                int effectiveResult = forceSharedMoatMode
                    ? leadUnitId
                    : normalize ? 0 : vanillaResult;
                string decision = forceSharedMoatMode
                    ? "forced=shared-friendly-moat"
                    : normalize ? "normalized=ground-per-unit" : "vanilla";
                string diagnostic =
                    $"stage=group-moat-mode tribe={tribeId} target=({command.TargetX},{command.TargetY}) " +
                    $"targetRegion={targetRegion} " +
                    $"lead={leadUnitId} vanillaFirstMoat={vanillaResult} onMoat={activeUnitsOnMoat} " +
                    $"offMoat={activeUnitsOffMoat} diggers={diggerUnits} " +
                    $"qualifyingDiggersOnMoat={qualifyingDiggerUnitsOnMoat} " +
                    $"qualifyingUnit={qualifyingUnitId} " +
                    $"effective={effectiveResult} decision={decision}";
                if (!string.Equals(
                        command.LastGroupMoatModeDiagnostic, diagnostic, StringComparison.Ordinal))
                {
                    command.LastGroupMoatModeDiagnostic = diagnostic;
                    LogCommandDiagnostic(diagnostic);
                }

                // A ground leader with a later moat member needs the shared moat branch.
                // A moat leader in a mixed group uses Vanilla's per-unit common-target
                // branch. Never synthesize E2610: DF720 consumes its real portal state.
                // The later per-unit mode and builder hooks retain the capability/owner filter.
                return effectiveResult;
            }
            catch (Exception ex)
            {
                LogFailure("group-moat-mode", ex);
                return vanillaResult;
            }
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
            bool nativeFloodCompleted = false;
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
                        CaptureAttackApproachState(pathManager));
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
                // DBC60 builds its depth-limited queue before extracting 50..500
                // results. Request the full native pool only near forbidden moat;
                // this does not repeat its flood for individual units.
                bool expand = IsBoundUnitAttackFlood(scope, targetX, targetY);
                if (expand)
                {
                    expand = false;
                    for (int dy = -2; dy <= 2 && !expand; dy++)
                    for (int dx = -2; dx <= 2 && !expand; dx++)
                    {
                        int x = (int)targetX + dx, y = (int)targetY + dy;
                        if ((uint)x < MapWidth && (uint)y < MapWidth)
                            expand = IsForbiddenFormationMoat(scope.PlayerId, x, y);
                    }
                }
                originalAttackApproachFloodBuilder(
                    pathManager,
                    tribeId,
                    targetContext,
                    targetX,
                    targetY,
                    expand ? Math.Max(requestedResults, VanillaAttackFloodResultCapacity / 2) : requestedResults,
                    sourceRegion,
                    movementClass);
                nativeFloodCompleted = true;
            }
            finally
            {
                if (scope != null)
                {
                    try
                    {
                        scope.After = CaptureAttackApproachState(pathManager);
                        if (nativeFloodCompleted && scope.After.Generation != scope.Before.Generation &&
                            IsBoundUnitAttackFlood(scope, targetX, targetY))
                            FilterAttackOutput(pathManager, scope.PlayerId, scope.CommandSequence);
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

        private bool IsBoundUnitAttackFlood(AttackApproachDiagnosticScope scope, uint x, uint y)
        {
            if (scope == null || scope.OwnerCommand == null || scope.OwnerCommand.MapEpoch != mapEpoch ||
                scope.Command != TribeAICommand.AttackUnit || scope.TargetContext != scope.OwnerCommand.TargetValue1 ||
                (uint)scope.TargetX != x || (uint)scope.TargetY != y ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(scope.UnitId, out GameUnit* source) ||
                source == null || !CanDigMoat(source) || source->r_TribeId != scope.TribeId ||
                source->r_ControllableForPlayerId != scope.PlayerId) return false;
            return TryGetHostileLivingUnitAtTile(scope.PlayerId, (int)x, (int)y,
                scope.OwnerCommand.TargetValue1, scope.OwnerCommand.TargetValue2, out _, out _);
        }

        private void FilterAttackOutput(IntPtr manager, int player, int sequence)
        {
            if (manager != nativePathManager || manager == IntPtr.Zero ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(player)) return;
            int removed = WeightedMoatRoutePlanner.FilterNativeAttackCandidates(
                (int*)((byte*)manager + PathManagerFloodResultTileOffset), VanillaAttackFloodResultCapacity,
                tile => !IsValidTileId(tile) || (IsCompletedMoatTile(tile) &&
                    ResolveCompletedMoatRelationship(player, tile) != CompletedMoatRelationship.Friendly));
            if (removed > 0)
                LogCommandDiagnostic($"stage=attack-slot-filter commandSeq={sequence} removed={removed} source=native-pool");
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
                        CaptureAttackApproachState(pathManager, requirePairedResult: true));
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

            FilterAttackOutput(nativePathManager, playerId, command.Sequence);
            AttackApproachState filtered = CaptureAttackApproachState(nativePathManager, requirePairedResult: true);
            if (filtered.UsableResultCount > 0 && filtered.UsableResultCount == vanillaAfter.UsableResultCount)
                return BuildingConsumerFallbackResult.NotAttempted("vanilla-usable");
            BuildingApproachCandidate[] retainedPairs = CaptureBuildingApproachCandidates(nativePathManager);

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
                if (!IsValidTileId(candidate.ApproachTileId) ||
                    (IsCompletedMoatTile(candidate.ApproachTileId) &&
                     ResolveCompletedMoatRelationship(playerId, candidate.ApproachTileId) != CompletedMoatRelationship.Friendly))
                { ownerRouteRejected++; continue; }
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

            // Native-valid results keep their order and scores. Append only independently
            // qualified missing pairs; never displace them with required-moat-only results.
            var mergedPairs = new List<BuildingApproachCandidate>();
            var mergedKeys = new HashSet<long>();
            foreach (BuildingApproachCandidate candidate in retainedPairs)
                if (candidate.ApproachTileId > 0 && candidate.FootprintTileId > 0 &&
                    IsValidBuildingApproachPair(command.TargetValue1, building, candidate.ApproachTileId, candidate.FootprintTileId) &&
                    mergedKeys.Add(((long)candidate.ApproachTileId << 32) | (uint)candidate.FootprintTileId))
                    mergedPairs.Add(candidate);
            foreach (BuildingApproachCandidate candidate in accepted)
                if (mergedKeys.Add(((long)candidate.ApproachTileId << 32) | (uint)candidate.FootprintTileId))
                    mergedPairs.Add(candidate);
            accepted = mergedPairs;

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

        private int ObserveScopedRegionPairReachability(
            IntPtr pathManager,
            int movementClass,
            int sourceRegion,
            int targetRegion,
            int routeKind)
        {
            int vanillaResult = originalRegionPairReachability(
                pathManager, movementClass, sourceRegion, targetRegion, routeKind);
            // E2610 uses argument 2 as player ID. Keep the established delegate ABI/name, but
            // pass the value to the work selector according to the confirmed native semantics.
            if (TryAllowDigWorkRegionPair(
                    pathManager, movementClass, sourceRegion, targetRegion, routeKind,
                    vanillaResult))
            {
                return 1;
            }
            if (TryAllowDirectCursorMoveRegionPair(
                    pathManager, movementClass, sourceRegion, targetRegion, vanillaResult) ||
                TryAllowDirectFillRegionPair(
                    pathManager, movementClass, sourceRegion, targetRegion, vanillaResult))
            {
                return 1;
            }
            MoveCommandScope moveCommand = activeMoveCommand;
            if (moveCommand != null && pathManager == nativePathManager)
            {
                try
                {
                    moveCommand.RegionCalls++;
                    string signature = $"E2610-observed:{movementClass}:{sourceRegion}:" +
                        $"{targetRegion}:{routeKind}:{vanillaResult}";
                    if (moveCommand.EarlyRegionLogSignatures.Add(signature))
                    {
                        LogCommandDiagnostic(
                            $"stage=movehere-region-pair-observed " +
                            $"commandSeq={moveCommand.Sequence} tribe={moveCommand.TribeId} " +
                            $"player={movementClass} regions={sourceRegion}->{targetRegion} " +
                            $"routeKind={routeKind} vanilla={vanillaResult} " +
                            $"effective={vanillaResult} " +
                            "decision=vanilla-graph-state-preserved");
                    }
                }
                catch
                {
                    // Observation must never alter the native reachability result.
                }
            }
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
                TryLogDiagnosticFailure("scoped-region-pair", ex);
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
            int vanillaResult = CallBuildingCursorWithRegions(buildingManager, buildingId, unitId);
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

                // Diagnostic deduplication uses numeric fields; format only a new,
                // bounded detail entry, not every building hover frame.
                ulong key;
                unchecked
                {
                    key = (uint)buildingId;
                    key = key * 1099511628211UL ^ target.GlobalId;
                    key = key * 1099511628211UL ^ (uint)unitId;
                    key = key * 1099511628211UL ^ (uint)targetTileId;
                    key = key * 1099511628211UL ^ (uint)cursorSelectionRevision;
                    key = key * 1099511628211UL ^ (uint)vanillaResult;
                    key = key * 1099511628211UL ^ (uint)effectiveResult;
                }
                if (loggedBuildingCursorReachabilityDecisions.Count < 64 && loggedBuildingCursorReachabilityDecisions.Add(key))
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
            if (activeBuildingCursorConnectivity != null) return 1;
            int vanillaResult = originalCursorTilePairFallbackSelection(selectionState);
            pendingAttackCursorPair = null;
            if (disposed || selectionState == IntPtr.Zero)
                return vanillaResult;

            try
            {
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
                        int tileUnitId = GameTileManagerAPI.Instance.GetTileUnitId(targetTileId);
                        hostileUnitTarget = tileUnitId > 0 && TryGetHostileLivingUnitAtTile(
                            playerId,
                            targetX,
                            targetY,
                            tileUnitId,
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
                    if (hostileBuildingTarget)
                        fallbackKind = CursorPairFallbackKind.BuildingApproach;
                    else if (hostileUnitTarget)
                        fallbackKind = CursorPairFallbackKind.UnitApproach;
                }

                AttackCursorPairScope candidateScope = validPair &&
                    (!occupiedByLivingUnit || hostileUnitTarget || hostileBuildingTarget ||
                     (tileFlags[targetTileId] & CursorSpecialStructureTileFlagMask) != 0)
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
                    if (groupRouteEvaluated) RecordCursorDecision(ownerRoute ? "region-connected" : "no-region-connection", candidateScope);
                    if (ownerRoute)
                        candidateScope = groupScope;
                }
                else if (dedicatedBuildingReachability)
                {
                    // B70C0 owns normal-building approach enumeration. Arming E2CA0 here is
                    // reentrant: B70C0 calls this selection helper again and replaces the scope.
                    ownerRoute = false;
                }
                else
                {
                    ownerRoute = false;
                }
                bool functionalArmed = candidateScope != null && ownerRoute &&
                    hasVanillaDiggerSelection;
                if (candidateScope != null && hasVanillaDiggerSelection && !dedicatedBuildingReachability)
                    pendingAttackCursorPair = candidateScope;

                // A positive Vanilla answer is authoritative. Only a Vanilla rejection may be
                // lifted, and only after the route has already passed the owner-aware moat probe.
                if (vanillaResult == 0 && functionalArmed)
                    return 1;
            }
            catch (Exception ex)
            {
                pendingAttackCursorPair = null;
                LogFailure("cursor-selection", ex);
            }

            return vanillaResult;
        }

        private int AllowAttackCursorTilePairThroughCompletedMoat(
            IntPtr pathManager, int targetTileId, int selectedUnitTileId, byte useCache)
        {
            AttackCursorPairScope scope = pendingAttackCursorPair;
            pendingAttackCursorPair = null;
            // E2CA0 may call D9C40 with 400000 nodes. A bound cursor must be answered
            // BEFORE calling it, while genuine attack/work consumers retain Vanilla.
            if (!disposed && activeAttackApproachDiagnostic == null && pathManager == nativePathManager)
            {
                try
                {
                    if (TryAnswerBuildingCursorPair(targetTileId, selectedUnitTileId, useCache, out int buildingResult))
                        return buildingResult;
                    if (scope != null && scope.MapEpoch == mapEpoch && useCache == 1 &&
                        scope.FallbackKind != CursorPairFallbackKind.BuildingApproach &&
                        CursorStartMatchesBoundSelection(scope, selectedUnitTileId) && CursorScopeMatchesTargetTile(scope, targetTileId) &&
                        TryQualifySelectedGroupCursorRoute(scope, out _, out CursorGroupRouteSummary group))
                    {
                        RecordCursorDecision(group.AllowFallback ? "region-connected" : "no-region-connection", scope);
                        return group.AllowFallback ? 1 : 0;
                    }
                }
                catch (Exception ex) { LogFailure("cursor-region-pair", ex); return 0; }
            }
            try { RecordCursorDecision("native-consumer-or-unbound", scope); } catch { /* Diagnostics must not interrupt native consumers. */ }
            int vanillaResult = originalCursorTilePairReachability(pathManager, targetTileId, selectedUnitTileId, useCache);
            if (activeAttackApproachDiagnostic != null)
                DiagnoseAttackApproachTilePair(activeAttackApproachDiagnostic, targetTileId, selectedUnitTileId, useCache, vanillaResult);
            return vanillaResult;
        }

        private int AllowCursorRegionThroughCompletedMoat(IntPtr pathManager, int nativeUnitIndex)
        {
            // E9D90 probes structure exits; a boolean override violates its contract.
            return originalCursorRegionPrecheck(pathManager, nativeUnitIndex);
        }





        private int AllowCursorReachabilityThroughCompletedMoat(
            IntPtr pathManager, int nativeUnitIndex, int targetX, int targetY)
        {
            // E9FF0 also writes exit coordinates. Leave both result and outputs native.
            return originalCursorReachability(pathManager, nativeUnitIndex, targetX, targetY);
        }





        private bool TryFindRequiredFriendlyCompletedMoatRouteForPlan(
            PlanScope plan, out RouteProbeSummary summary)
        {
            summary = default;
            return TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                plan, exactTarget: plan != null && plan.ExactRouteEndpoints, allowReservedTarget: false, out summary);
        }

        private bool TryFindRequiredFriendlyCompletedMoatRouteForPlan(
            PlanScope plan,
            bool exactTarget,
            bool allowReservedTarget,
            out RouteProbeSummary summary,
            bool evaluateMissing = true)
        {
            summary = default;
            if (plan == null || plan.TargetX < 0 || plan.TargetX >= MapWidth ||
                plan.TargetY < 0 || plan.TargetY >= MapWidth ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) ||
                unit == null)
            {
                return false;
            }

            if (!CanDigMoat(unit))
                return false;

            int playerId = unit->r_ControllableForPlayerId;
            if (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != playerId)) return false;
            plan.UnitGlobalId = unit->r_GlobalId;
            plan.IdentityBound = true;
            GetNativeMovementStart(unit, out int nativeStartX, out int nativeStartY);
            int startX = plan.RouteStartX >= 0 ? plan.RouteStartX : nativeStartX;
            int startY = plan.RouteStartY >= 0 ? plan.RouteStartY : nativeStartY;
            if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                return false;
            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY);
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                !IsValidTileId(startTileId) || !IsValidTileId(targetTileId) ||
                targetedRouteProbeBusy || weightedShadowBusy)
            {
                return false;
            }
            plan.PlayerId = playerId;
            PrepareMovementSearch(plan, playerId);

            if (plan.MoatWorkMovement && evaluateMissing && !plan.ExactRouteEndpoints &&
                (plan.RouteStartX < 0 || (plan.MoatWorkSearch != null &&
                 plan.MoatWorkSearch.StartX == startX && plan.MoatWorkSearch.StartY == startY)))
            {
                if (TryGetMoatWorkRoute(plan.MoatWorkSearch, plan.TargetX, plan.TargetY, out summary)) return true;
                // The native consumer can choose a terminal contact endpoint beyond the work tile.
                // Qualify that exact endpoint below instead of losing the work contract.
            }

            int startRegion = pathRegionGrid[startTileId];
            int targetRegion = pathRegionGrid[targetTileId];
            // A concrete failed search is not a proof about every tile in two native PCLs.
            string cacheKey = $"{mapEpoch}:{CaptureCurrentGameTick()}:{playerId}:{startTileId}:{targetTileId}:{allowReservedTarget}:work:{plan.MoatWorkTargetTileId}";
            MoveCommandScope command = activeMoveCommand;
            if (command != null && command.TargetedRouteDecisions.TryGetValue(
                    cacheKey, out TargetedRouteDecision cached))
            {
                command.TargetedRouteCacheHits++;
                summary = cached.Summary;
                return cached.RequiredFriendlyMoat;
            }

            if (!evaluateMissing)
                return false;

            Stopwatch stopwatch = Stopwatch.StartNew();
            WeightedMoatRouteSummary ground = default;
            WeightedMoatRouteSummary friendly = default;
            long nodesBefore = weightedMoatRoutePlanner.SearchNodes;
            long runsBefore = weightedMoatRoutePlanner.SearchRuns;
            bool groundReachable;
            bool friendlyReachable = false;
            targetedRouteProbeBusy = true;
            try
            {
                groundReachable = weightedMoatRoutePlanner.TryProbeReachability(
                    playerId, startX, startY, plan.TargetX, plan.TargetY,
                    allowReservedTarget, MoatTraversalPolicy.GroundOnly, out ground);
                if (!groundReachable)
                {
                    friendlyReachable = weightedMoatRoutePlanner.TryProbeReachability(
                        playerId, startX, startY, plan.TargetX, plan.TargetY,
                        allowReservedTarget, MoatTraversalPolicy.FriendlyOnly,
                        out friendly);
                    if (!friendlyReachable && plan.MoatWorkMovement &&
                        TryBuildTerminalFillRoute(plan, unit, startX, startY, out friendly, out WeightedMoatEncodedRoute terminal))
                    {
                        plan.QualifiedTerminalRoute = terminal;
                        plan.QualifiedTerminalSummary = friendly;
                        friendlyReachable = true;
                    }
                }
            }
            finally
            {
                targetedRouteProbeBusy = false;
            }

            bool requiredFriendly = !groundReachable && friendlyReachable &&
                friendly.MoatEdges > 0;
            summary = new RouteProbeSummary(playerId)
            {
                StartRegion = startRegion,
                TargetRegion = targetRegion,
                RouteFound = requiredFriendly,
                AttackProbeEvaluated = true,
                ReachedWithoutMoat = groundReachable,
                ReachedWithMoat = friendlyReachable,
                FriendlyMoatTiles = friendly.MoatEdges,
                StructuralEdgesObserved = Math.Max(
                    ground.StructuralEdges, friendly.StructuralEdges),
                RouteDistance = friendlyReachable
                    ? (plan.QualifiedTerminalRoute.IsValid ? plan.QualifiedTerminalRoute.DirectionCount : friendly.RouteLength)
                    : groundReachable ? ground.RouteLength : int.MaxValue,
                TargetedExpandedNodes = (int)Math.Min(int.MaxValue, weightedMoatRoutePlanner.SearchNodes - nodesBefore),
                TargetedSearchMilliseconds = stopwatch.Elapsed.TotalMilliseconds
            };
            if (command != null)
            {
                command.TargetedRouteSearches++;
                command.TargetedRouteSearchPasses += (int)(weightedMoatRoutePlanner.SearchRuns - runsBefore);
                command.TargetedRouteExpandedNodes += summary.TargetedExpandedNodes;
                command.TargetedRouteSearchMilliseconds += summary.TargetedSearchMilliseconds;
                command.TargetedRouteMaximumSearchMilliseconds = Math.Max(
                    command.TargetedRouteMaximumSearchMilliseconds,
                    summary.TargetedSearchMilliseconds);
                command.TargetedRouteDecisions[cacheKey] =
                    new TargetedRouteDecision(requiredFriendly, summary);
            }
            summary.AttackProbeEvaluated = true;
            if (summary.RouteFound)
            {
                LogDiggerDecision("route", plan.UnitId, unit,
                    plan.TargetX, plan.TargetY, true, friendlyMoatRequired: true);
            }
            return summary.RouteFound;
        }

        private bool TryGetCachedRequiredFriendlyRouteForPlan(
            PlanScope plan, out RouteProbeSummary summary) =>
            TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                plan, exactTarget: false, allowReservedTarget: false,
                out summary, evaluateMissing: false);

        private bool TryQualifyMoveCommandFloodBypass(MoveCommandScope command)
        {
            if (command == null || command.DiggersAtDispatch == 0)
                return false;
            if (command.FloodOwnerRouteEvaluated)
                return command.FloodOwnerRouteAllowed;

            command.FloodOwnerRouteEvaluated = true;
            DirectCursorMoveScope direct = activeDirectCursorMove;
            if (direct != null && direct.MapEpoch == mapEpoch &&
                direct.TribeId == command.TribeId && direct.TargetX == command.TargetX &&
                direct.TargetY == command.TargetY)
            {
                command.FloodOwnerRouteAllowed = true;
                command.MoatRelevant = true;
                return true;
            }

            var probedSources = new HashSet<string>(StringComparer.Ordinal);
            foreach (int unitId in command.ActiveUnitIdsAtDispatch)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != command.TribeId || !CanDigMoat(unit))
                {
                    continue;
                }
                int startTileId = GameTileManagerAPI.Instance.GetTileId(
                    unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
                if (!IsValidTileId(startTileId))
                    continue;
                int startRegion = pathRegionGrid[startTileId];
                string sourceKey = startRegion > 0 && !IsCompletedMoatTile(startTileId) &&
                    (tileFlags[startTileId] & CursorSpecialStructureTileFlagMask) == 0
                        ? $"r:{unit->r_ControllableForPlayerId}:{startRegion}"
                        : $"t:{unit->r_ControllableForPlayerId}:{startTileId}";
                if (!probedSources.Add(sourceKey))
                    continue;

                var plan = new PlanScope(unitId, command.TargetX, command.TargetY);
                if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        plan, out RouteProbeSummary route))
                {
                    continue;
                }
                command.FloodOwnerRouteAllowed = true;
                command.MoatRelevant = true;
                MarkCommandMoatRelevant(command, route);
                return true;
            }
            return false;
        }

        private bool TryFindRequiredFriendlyCompletedMoatRouteToEndpoint(
            PlanScope plan,
            int endpointTileId,
            bool requireBuildingReservation,
            out RouteProbeSummary summary,
            out int distance)
        {
            distance = int.MaxValue;
            if (TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                    plan, exactTarget: true,
                    allowReservedTarget: requireBuildingReservation, out summary))
            {
                distance = summary.RouteDistance;
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
                        neighbourPlan, exactTarget: true,
                        allowReservedTarget: false,
                        out RouteProbeSummary neighbourSummary))
                {
                    observed.MergeObservations(neighbourSummary);
                    continue;
                }

                neighbourSummary.TargetRegion = pathRegionGrid[endpointTileId];
                neighbourSummary.RouteFound = true;
                neighbourSummary.ReachedWithMoat = true;
                neighbourSummary.ReachedWithoutMoat = false;
                observed.MergeObservations(neighbourSummary);
                int candidateDistance = neighbourSummary.RouteDistance == int.MaxValue
                    ? int.MaxValue
                    : neighbourSummary.RouteDistance + 1;
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

        private bool TryQualifySelectedGroupCursorRoute(
            AttackCursorPairScope template,
            out AttackCursorPairScope boundScope,
            out CursorGroupRouteSummary group)
        {
            boundScope = null; group = default;
            if (template == null || !TryCaptureSelectedGroup(template.PlayerId, out int[] ids, out string token)) return false;
            group.SelectionSignature = token;
            EnsureCursorTopology(template.PlayerId, false);
            cursorSources.Clear(); cursorSourceCounts.Clear();
            foreach (int id in ids)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* unit) || unit == null ||
                    unit->r_AliveState != AliveState.IsAlive || unit->r_ControllableForPlayerId != template.PlayerId) continue;
                group.SelectedUnits++;
                bool canDig = CanDigMoat(unit);
                if (canDig) group.DiggerUnits++;
                int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                int tile = GameTileManagerAPI.Instance.GetTileId(x, y), node = CursorNode(template.PlayerId, tile);
                if (node < 0) continue;
                int key = node * 2 + (canDig ? 1 : 0);
                if (cursorSources.ContainsKey(key)) { cursorSourceCounts[key]++; continue; }
                cursorSources.Add(key, new SelectedCursorUnitSnapshot(id, x, y, tile, canDig));
                cursorSourceCounts[key] = 1;
            }
            AttackCursorPairScope probe = null;
            foreach (var entry in cursorSources)
            {
                var member = entry.Value;
                if (probe == null) probe = CreateCursorScopeForSnapshot(template, member);
                else probe.SetSource(member);
                if (!TryQualifyCursorScope(probe, out _, out _, out RouteProbeSummary summary)) continue;
                if (!member.CanDig && !summary.ReachedWithoutMoat) continue;
                group.ObservedRoute.MergeObservations(summary);
                group.LegallyReachableUnits += cursorSourceCounts[entry.Key];
                if (summary.ReachedWithMoat && !summary.ReachedWithoutMoat) group.FriendlyMoatSeparatedUnits += cursorSourceCounts[entry.Key];
                if (boundScope != null) continue;
                boundScope = CreateCursorScopeForSnapshot(template, member);
                group.RepresentativeUnitId = member.UnitId; group.RepresentativeStartX = member.StartX;
                group.RepresentativeStartY = member.StartY; group.RepresentativeStartTileId = member.StartTileId;
                group.RepresentativeCanDig = member.CanDig;
            }
            group.AllowFallback = boundScope != null;
            if (boundScope != null) { boundScope.GroupCursorAuthorized = true; boundScope.GroupSelectionSignature = token; }
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
        { return CaptureCursorSelection(playerId, out selectedUnitIds, out signature); }

        private bool TryProbeDirectCursorRoute(
            AttackCursorPairScope scope,
            out bool normalReachable,
            out bool friendlyMoatSeparated,
            out RouteProbeSummary summary)
        {
            normalReachable = false; friendlyMoatSeparated = false; summary = default;
            if (scope == null || (uint)scope.TargetX >= MapWidth || (uint)scope.TargetY >= MapWidth ||
                !IsValidTileId(scope.TargetTileId) || movementTargetAvailability[scope.TargetY * MapWidth + scope.TargetX] == 0 ||
                (tileFlags[scope.TargetTileId] & MovementBlockedLowTileFlagMask) != 0)
            { RecordCursorDecision("invalid-move-target", scope); return false; }
            if (!ProbeCursorConnectivity(scope.PlayerId, scope.StartTileId, scope.TargetTileId, out summary)) return false;
            normalReachable = summary.ReachedWithoutMoat;
            friendlyMoatSeparated = summary.ReachedWithMoat && !normalReachable;
            return true;
        }

        private bool TryProbeUnitApproachCursorRoute(
            AttackCursorPairScope scope,
            out bool normalReachable,
            out bool friendlyMoatSeparated,
            out RouteProbeSummary summary)
        {
            normalReachable = false; friendlyMoatSeparated = false; summary = default;
            if (scope == null || !CursorScopeMatchesTargetTile(scope, scope.TargetTileId))
            { RecordCursorDecision("invalid-attack-target", scope); return false; }
            // 8C5F0 passes the actual target unit tile to E2CA0. Reachability is not
            // a melee approach search; weapon and command eligibility stay native.
            if (!ProbeCursorConnectivity(scope.PlayerId, scope.StartTileId, scope.TargetTileId, out summary)) return false;
            normalReachable = summary.ReachedWithoutMoat;
            friendlyMoatSeparated = summary.ReachedWithMoat;
            return true;
        }

        private bool TryQualifyCursorScope(
            AttackCursorPairScope scope,
            out int approachX,
            out int approachY,
            out RouteProbeSummary summary)
        {
            approachX = -1; approachY = -1; summary = default;
            if (scope == null) return false;
            bool normal, friendly;
            if (scope.FallbackKind == CursorPairFallbackKind.DirectTile)
            {
                approachX = scope.TargetX; approachY = scope.TargetY;
                return TryProbeDirectCursorRoute(scope, out normal, out friendly, out summary) && (normal || friendly);
            }
            if (scope.FallbackKind == CursorPairFallbackKind.UnitApproach)
                return TryProbeUnitApproachCursorRoute(scope, out normal, out friendly, out summary) && (normal || friendly);
            return TryProbeBuildingApproachCursorRoute(scope, out normal, out friendly, out approachX, out approachY, out summary) && (normal || friendly);
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
                    targetUnit != null && targetUnit->r_GlobalId == scope.TargetUnitGlobalId &&
                    targetUnit->r_CurrentTilePositionX == scope.TargetX && targetUnit->r_CurrentTilePositionY == scope.TargetY;
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
                    candidateObserved = true;
                    if (!ProbeCursorConnectivity(scope.PlayerId, scope.StartTileId, tileId,
                        out RouteProbeSummary candidate)) continue;
                    bool withoutMoat = candidate.ReachedWithoutMoat;
                    bool withMoat = candidate.ReachedWithMoat;
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
            for (int unitId = requiredUnitId > 0 ? requiredUnitId : 1; unitId <= (requiredUnitId > 0 ? requiredUnitId : units.Length); unitId++)
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

                    EnsureReachabilityMap(
                        scope.PlayerId,
                        scope.StartX,
                        scope.StartY, deferTraversal: true, owner: scope);
                    RouteProbeSummary candidateSummary =
                        GetCachedRouteSummaryForTarget(candidateX, candidateY);
                    bool reachedWithMoat = visitedWithMoat[candidateCell] == gridGeneration;
                    bool reachedWithoutMoat = visitedWithoutMoat[candidateCell] == gridGeneration;
                    candidateSummary.AttackProbeEvaluated = true;
                    candidateSummary.ReachedWithMoat = reachedWithMoat;
                    candidateSummary.ReachedWithoutMoat = reachedWithoutMoat;
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
            int playerId,
            int startX,
            int startY,
            bool includeEnemyRoutes = false, bool deferTraversal = false, object owner = null)
        {
            EnsureReachabilityStorage();
            object scopeOwner = owner ?? (object)activeMoatWorkSelection ?? activeMoveCommand ??
                (object)activeBuildingConsumerPerformance ?? activeBuildingApproachPerformance;
            int currentTick = CaptureCurrentGameTick();
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                startX < 0 || startX >= MapWidth ||
                startY < 0 || startY >= MapWidth)
            {
                cacheMapEpoch = -1;
                cachedRouteSummary = new RouteProbeSummary(playerId);
                return;
            }

            if (scopeOwner != null && ReferenceEquals(scopeOwner, reachabilityOwner) && reachabilityTick == currentTick &&
                visitedWithoutMoat != null && cacheMapEpoch == mapEpoch &&
                cachePlayerId == playerId && cacheStartX == startX &&
                cacheStartY == startY && cacheIncludesEnemyRoutes == includeEnemyRoutes)
            {
                if (!deferTraversal) AdvanceReachabilityMap();
                cachedReachabilityMapHits++;
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

            if (gridGeneration == int.MaxValue)
            {
                Array.Clear(visitedWithoutMoat, 0, visitedWithoutMoat.Length);
                Array.Clear(visitedWithMoat, 0, visitedWithMoat.Length);
                Array.Clear(visitedWithEnemyMoat, 0, visitedWithEnemyMoat.Length);
                Array.Clear(observedRouteRegions, 0, observedRouteRegions.Length);
                Array.Clear(reachedGroundRegions, 0, reachedGroundRegions.Length);
                Array.Clear(reachedFriendlyMoatRegions, 0, reachedFriendlyMoatRegions.Length);
                Array.Clear(reachedEnemyMoatRegions, 0, reachedEnemyMoatRegions.Length);
                gridGeneration = 1;
            }
            else
            {
                gridGeneration++;
            }

            // Publish the cache only after a complete traversal. An exception or an
            // invalid source must not make a partially built graph reusable.
            cacheMapEpoch = -1;
            reachabilityOwner = scopeOwner;
            reachabilityTick = currentTick;
            cacheIncludesEnemyRoutes = includeEnemyRoutes;
            cachedReachabilityExpandedNodes = 0;
            cachePlayerId = playerId;
            cacheStartX = startX;
            cacheStartY = startY;
            cachedReachabilityMapHits = 0;
            cachedTraversedRegionCount = 0;
            cachedRouteSummary = new RouteProbeSummary(playerId);

            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            if (!IsValidTileId(startTileId))
                return;

            int startRegion = pathRegionGrid[startTileId];
            cachedRouteSummary.StartRegion = startRegion;
            int startCell = (startY * MapWidth) + startX;
            bool startIsMoat = (tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
            CompletedMoatRelationship startRelationship = startIsMoat
                ? ResolveCompletedMoatRelationship(playerId, startTileId)
                : CompletedMoatRelationship.Friendly;
            int startState;
            if (!startIsMoat)
            {
                startState = GroundRouteState;
                visitedWithoutMoat[startCell] = gridGeneration;
                distanceWithoutMoat[startCell] = 0;
            }
            else if (startRelationship == CompletedMoatRelationship.Friendly)
            {
                startState = FriendlyMoatRouteState;
                visitedWithMoat[startCell] = gridGeneration;
                distanceWithMoat[startCell] = 0;
                cachedRouteSummary.FriendlyMoatTiles = 1;
            }
            else if (startRelationship == CompletedMoatRelationship.Enemy && includeEnemyRoutes)
            {
                startState = EnemyMoatRouteState;
                visitedWithEnemyMoat[startCell] = gridGeneration;
                distanceWithEnemyMoat[startCell] = 0;
                cachedRouteSummary.EnemyMoatTiles = 1;
            }
            else
            {
                cachedRouteSummary.InvalidMoatTiles = 1;
                return;
            }

            ObserveTraversedRegion(startRegion, startState);
            reachabilityQueueHead = 0;
            reachabilityQueueTail = 1;
            queue[0] = startCell | (startState << RouteStateShift);
            cacheMapEpoch = mapEpoch;
            if (!deferTraversal) AdvanceReachabilityMap();
        }

        private int reachabilityQueueHead, reachabilityQueueTail;
        private object reachabilityOwner;
        private int reachabilityTick;
        private void AdvanceReachabilityMap(int targetCell = -1)
        {
            if (cacheMapEpoch != mapEpoch || reachabilityQueueHead >= reachabilityQueueTail) return;
            weightedMoatRoutePlanner.BeginReachabilityProbe();
            try
            {
                // A discovered ground route is already a conclusive negative answer to
                // "requires moat". Otherwise exhaust the frontier before returning no route.
                while (reachabilityQueueHead < reachabilityQueueTail &&
                    (targetCell < 0 || visitedWithoutMoat[targetCell] != gridGeneration))
                {
                    int encoded = queue[reachabilityQueueHead++];
                    int state = encoded >> RouteStateShift, cell = encoded & RouteCellMask;
                    int y = cell / MapWidth, x = cell % MapWidth;
                    int distance = GetRouteDistance(state, cell);
                    for (int d = 0; d < WeightedMoatRoutePlanner.DirectionX.Length; d++)
                        VisitNeighbour(cachePlayerId, x, y, x + WeightedMoatRoutePlanner.DirectionX[d],
                            y + WeightedMoatRoutePlanner.DirectionY[d], d, state, distance, ref reachabilityQueueTail);
                }
                cachedReachabilityExpandedNodes = reachabilityQueueHead;
                cachedRouteSummary.TraversedRegionCount = cachedTraversedRegionCount;
            }
            catch { cacheMapEpoch = -1; throw; }
            finally { weightedMoatRoutePlanner.EndReachabilityProbe(); }
        }
        private void EnsureReachabilityStorage()
        {
            if (visitedWithoutMoat != null)
                return;

            visitedWithoutMoat = new int[MapCellCount];
            visitedWithMoat = new int[MapCellCount];
            visitedWithEnemyMoat = new int[MapCellCount];
            distanceWithoutMoat = new int[MapCellCount];
            distanceWithMoat = new int[MapCellCount];
            distanceWithEnemyMoat = new int[MapCellCount];
            queue = new int[MapCellCount * 3];
            observedRouteRegions = new int[MaximumRegionId + 1];
            reachedGroundRegions = new int[MaximumRegionId + 1];
            reachedFriendlyMoatRegions = new int[MaximumRegionId + 1];
            reachedEnemyMoatRegions = new int[MaximumRegionId + 1];
        }

        private void VisitNeighbour(
            int playerId,
            int currentX,
            int currentY,
            int nextX,
            int nextY,
            int direction,
            int currentState,
            int currentDistance,
            ref int queueTail)
        {
            if (nextX < 0 || nextX >= MapWidth || nextY < 0 || nextY >= MapWidth)
                return;

            int nextCell = (nextY * MapWidth) + nextX;
            int currentTileId = GameTileManagerAPI.Instance.GetTileId(currentX, currentY);
            int nextTileId = GameTileManagerAPI.Instance.GetTileId(nextX, nextY);
            if (!IsValidTileId(currentTileId) || !IsValidTileId(nextTileId))
                return;

            if (!weightedMoatRoutePlanner.TryGetTraversalEdge(
                    playerId,
                    currentX,
                    currentY,
                    currentTileId,
                    nextX,
                    nextY,
                    nextTileId,
                    direction,
                    false,
                    false,
                    cacheIncludesEnemyRoutes
                        ? MoatTraversalPolicy.AllowEnemyForDiagnostic
                        : MoatTraversalPolicy.FriendlyOnly,
                    out MoatTraversalEdgeKind edgeKind,
                    out bool structuralEdge))
            {
                return;
            }

            if (structuralEdge)
                cachedRouteSummary.StructuralEdgesObserved++;

            int nextState = currentState == EnemyMoatRouteState ||
                edgeKind == MoatTraversalEdgeKind.EnemyMoat
                    ? EnemyMoatRouteState
                    : currentState == FriendlyMoatRouteState ||
                      edgeKind == MoatTraversalEdgeKind.FriendlyMoat
                        ? FriendlyMoatRouteState
                        : GroundRouteState;
            int[] visited = GetRouteVisitedMap(nextState);
            if (visited[nextCell] == gridGeneration || queueTail >= queue.Length)
                return;

            visited[nextCell] = gridGeneration;
            GetRouteDistanceMap(nextState)[nextCell] = currentDistance + 1;
            if ((tileFlags[nextTileId] & CompletedMoatTileFlag) != 0)
            {
                if (nextState == FriendlyMoatRouteState)
                    cachedRouteSummary.FriendlyMoatTiles++;
                else if (nextState == EnemyMoatRouteState)
                    cachedRouteSummary.EnemyMoatTiles++;
            }
            ObserveTraversedRegion(pathRegionGrid[nextTileId], nextState);
            queue[queueTail++] = nextCell | (nextState << RouteStateShift);
        }

        private int[] GetRouteVisitedMap(int state) =>
            state == GroundRouteState
                ? visitedWithoutMoat
                : state == FriendlyMoatRouteState
                    ? visitedWithMoat
                    : visitedWithEnemyMoat;

        private int[] GetRouteDistanceMap(int state) =>
            state == GroundRouteState
                ? distanceWithoutMoat
                : state == FriendlyMoatRouteState
                    ? distanceWithMoat
                    : distanceWithEnemyMoat;

        private int GetRouteDistance(int state, int cell) =>
            GetRouteDistanceMap(state)[cell];

        private void ObserveTraversedRegion(int region, int state)
        {
            if (region <= 0 || region > MaximumRegionId)
                return;

            if (observedRouteRegions[region] != gridGeneration)
            {
                observedRouteRegions[region] = gridGeneration;
                cachedTraversedRegionCount++;
            }
            int[] reachedRegions = state == GroundRouteState
                ? reachedGroundRegions
                : state == FriendlyMoatRouteState
                    ? reachedFriendlyMoatRegions
                    : reachedEnemyMoatRegions;
            reachedRegions[region] = gridGeneration;
        }

        private RouteProbeSummary GetCachedRouteSummaryForTarget(int targetX, int targetY)
        {
            if ((uint)targetX < MapWidth && (uint)targetY < MapWidth) AdvanceReachabilityMap(targetY * MapWidth + targetX);
            RouteProbeSummary summary = cachedRouteSummary;
            if (targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth)
                return summary;

            int targetCell = targetY * MapWidth + targetX;
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            summary.TargetRegion = IsValidTileId(targetTileId)
                ? pathRegionGrid[targetTileId]
                : 0;
            summary.ReachedWithoutMoat =
                visitedWithoutMoat[targetCell] == gridGeneration;
            summary.ReachedWithMoat = visitedWithMoat[targetCell] == gridGeneration;
            summary.EnemyOnlyReachable =
                visitedWithEnemyMoat[targetCell] == gridGeneration &&
                !summary.ReachedWithoutMoat && !summary.ReachedWithMoat;
            summary.TraversedRegionCount = cachedTraversedRegionCount;
            summary.ReachabilityCacheHits = cachedReachabilityMapHits;
            return summary;
        }

        private RouteProbeSummary GetCachedRouteSummaryForRegion(int targetRegion)
        {
            AdvanceReachabilityMap();
            RouteProbeSummary summary = cachedRouteSummary;
            summary.TargetRegion = targetRegion;
            if (targetRegion <= 0 || targetRegion > MaximumRegionId)
                return summary;

            summary.ReachedWithoutMoat =
                reachedGroundRegions[targetRegion] == gridGeneration;
            summary.ReachedWithMoat =
                reachedFriendlyMoatRegions[targetRegion] == gridGeneration;
            summary.EnemyOnlyReachable =
                reachedEnemyMoatRegions[targetRegion] == gridGeneration &&
                !summary.ReachedWithoutMoat && !summary.ReachedWithMoat;
            summary.TraversedRegionCount = cachedTraversedRegionCount;
            summary.ReachabilityCacheHits = cachedReachabilityMapHits;
            return summary;
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
                if (!IsValidMoatRecordId(moatId, moatCount))
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
            ClearUnitMoveFrames();
            mapEpoch++;
            cursorTopologies.Clear(); noBuilderDetails = 0; preBuilderRejections.Clear();
            cursorDecisionCounts.Clear(); cursorDecisionDetails.Clear();
            fillRouteDecisions.Clear(); fillRouteLogTick = -1; fillRouteLogCount = 0; formationOwner = null;
            ResetMoatWorkTargetSelection();
            ResetDirectMoatCommandScopes();
            cacheMapEpoch = -1;
            cacheStartX = -1;
            cacheStartY = -1;
            cachePlayerId = -1;
            cachedTraversedRegionCount = 0;
            cachedReachabilityMapHits = 0;
            cachedRouteSummary = default;
            loggedBuildingCursorReachabilityDecisions.Clear();
            lastUnscopedAttackModes.Clear();
            lastAttackCommandCandidates.Clear();
            trackedAttackUnits.Clear();
            activeMoveCommand = null;
            activePlan = null;
            pendingPlan = null;
            pendingAttackCursorPair = null;
            activeAttackCommand = null;
            activeAttackApproachDiagnostic = null;
            activeBuildingApproachPerformance = null;
            activeBuildingConsumerPerformance = null;
            trackedMoatMoves.Clear();
            trackedNativeWaypointQueues.Clear();
            loggedDiggerDecisions.Clear();
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

        // Commands and work selections have aggregate counters; avoid formatting a
        // full per-unit trace in those hot paths. Standalone diagnostics remain available.
        private bool ShouldLogUnitPipeline => activeMoveCommand == null &&
            activeMoatWorkSelection == null &&
            !((activePlan ?? pendingPlan)?.MoatWorkMovement ?? false);

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
            if (command == null || command.BuilderReached || pendingPlan == null ||
                command.DiggersAtDispatch == 0)
                return;

            // A Patrol leg can leave MoveHere after mode selection but before the builder.
            // Probe once in Post so precisely that early exit remains diagnosable.
            try
            {
                if (TryGetCachedRequiredFriendlyRouteForPlan(
                        pendingPlan, out RouteProbeSummary summary))
                {
                    MarkCommandMoatRelevant(command, summary);
                }
            }
            catch (Exception ex)
            {
                LogFailure("command-diagnostic-route", ex);
            }
        }

        private void FlushCommandDiagnostics(MoveCommandScope command)
        {
            bool queueRelevant = command != null &&
                ((command.HasQueuePreSnapshot && command.QueuePreSnapshot.Count > 0) ||
                 (command.HasQueuePostSnapshot && command.QueuePostSnapshot.Count > 0));
            bool slow = command != null && command.ElapsedMilliseconds >= 50.0;
            bool earlyFailure = command != null && command.DiggersAtDispatch > 0 &&
                command.UnitMoveCalls == 0;
            if (command == null || (!command.MoatRelevant && !queueRelevant && !slow && !earlyFailure))
                return;

            if (slow && !command.MoatRelevant && !queueRelevant && !earlyFailure)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=move-command-performance commandSeq={command.Sequence} " +
                    $"tribe={command.TribeId} units={command.ActiveUnitsAtDispatch} " +
                    $"diggers={command.DiggersAtDispatch} elapsedMs={command.ElapsedMilliseconds:F3} " +
                    $"targetedSearches={command.TargetedRouteSearches} " +
                    $"targetedCacheHits={command.TargetedRouteCacheHits} " +
                    $"targetedExpanded={command.TargetedRouteExpandedNodes} " +
                    $"targetedSearchMs={command.TargetedRouteSearchMilliseconds:F3} " +
                    $"targetedMaxSearchMs={command.TargetedRouteMaximumSearchMilliseconds:F3} " +
                    $"weightedSearchMs={command.WeightedSearchMilliseconds:F3} " +
                    $"weightedMaxSearchMs={command.WeightedMaximumSearchMilliseconds:F3} " +
                    "moatIntervention=False.");
                return;
            }

            foreach (string message in command.Diagnostics)
                Shared.DebugLogHelper.LogInfo(log, $"MoveMoat {message}.");
        }

        private void LogQueuedMoveHereOutcome(MoveCommandScope command, long returnValue)
        {
            if (command == null || command.IsNewOrder)
                return;

            string before = command.HasQueuePreSnapshot
                ? command.QueuePreSnapshot.ToString()
                : "unavailable";
            string after = command.HasQueuePostSnapshot
                ? command.QueuePostSnapshot.ToString()
                : "unavailable";
            bool consumerAlreadyAdvanced = command.HasQueuePreSnapshot &&
                command.QueuePreSnapshot.Count > 0 &&
                command.QueuePreSnapshot.Index > 0 &&
                command.QueuePreSnapshot.Index < command.QueuePreSnapshot.Count;
            string stage = returnValue != 0
                ? "queue-waypoint-accepted"
                : consumerAlreadyAdvanced
                    ? "queue-waypoint-skipped"
                    : "movehere-continuation-rejected";
            string skipProbe = returnValue == 0
                ? DescribeRejectedQueueWaypoint(command)
                : "not-required";
            LogCommandDiagnostic(
                $"stage={stage} commandSeq={command.Sequence} tribe={command.TribeId} " +
                $"target=({command.TargetX},{command.TargetY}) return={returnValue} " +
                $"consumerAlreadyAdvanced={consumerAlreadyAdvanced} " +
                $"queueBefore=[{before}] queueAfter=[{after}] " +
                $"regionCalls={command.RegionCalls} floodCalls={command.FloodCalls} " +
                $"modeCalls={command.ModeCalls} builderCalls={command.BuilderCalls} " +
                $"earlyRegionCalls={command.EarlyRegionCalls} " +
                $"earlyRegionBypasses={command.EarlyRegionBypasses} " +
                $"skipProbe=[{skipProbe}]");
        }

        private string DescribeRejectedQueueWaypoint(MoveCommandScope command)
        {
            int alive = 0;
            int diggers = 0;
            int requiredFriendly = 0;
            RouteProbeSummary observed = default;
            foreach (int unitId in command.ActiveUnitIdsAtDispatch)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != command.TribeId)
                {
                    continue;
                }

                alive++;
                if (!CanDigMoat(unit))
                    continue;
                diggers++;
                var plan = new PlanScope(unitId, command.TargetX, command.TargetY)
                {
                    PlayerId = unit->r_ControllableForPlayerId
                };
                if (TryGetCachedRequiredFriendlyRouteForPlan(
                        plan, out RouteProbeSummary summary))
                {
                    requiredFriendly++;
                    observed.MergeObservations(summary);
                }
            }

            return $"alive={alive} diggers={diggers} " +
                $"cachedRequiredFriendly={requiredFriendly} {observed.ToLogFields()}";
        }

        private void LogModeContext(PlanScope plan, GameUnit* unit, int vanillaResult)
        {
            if (!ShouldLogUnitPipeline)
                return;
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

        private bool ResolveNativeSpecialStructure(int tileId) =>
            IsValidTileId(tileId) && nativeSpecialStructurePredicate != null &&
            nativeSpecialStructurePredicate(nativeSpecialStructureContext, tileId);

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
            public int ActualEnemyMoatTilesMatchingWorkTarget { get; set; }
            public int ActualEnemyMoatTilesOutsideWorkTarget { get; set; }
            public int WorkTargetMoatTileId { get; set; }
            public HashSet<int> ActualMoatTileIds { get; } = new HashSet<int>();
            public Dictionary<int, int> ActualMoatOwnerAtFirstObservation { get; } =
                new Dictionary<int, int>();
        }

        private sealed class BuilderWeightedScope
        {
            public PlanScope FillPlan { get; set; }
            public uint UnitGlobalId { get; set; }
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
            public int? ConsumerVariant { get; set; }
            public string ConsumerVariantText =>
                ConsumerVariant.HasValue ? ConsumerVariant.Value.ToString() : "not-applicable";

            public string GetSemanticSignature() =>
                $"{Kind}:{Command}:{TribeId}:{TargetContext}:{TargetX}:{TargetY}:" +
                $"{RequestedResults}:{SourceRegion}:{MovementClass}:{UnitId}:{PlayerId}:{UnitType}:" +
                $"{ConsumerVariantText}:" +
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
                StartTimestamp = Stopwatch.GetTimestamp();
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
            public long StartTimestamp { get; }
            public double ElapsedMilliseconds { get; set; }
            public int ActiveUnitsAtDispatch { get; set; }
            public int[] ActiveUnitIdsAtDispatch { get; set; } = Array.Empty<int>();
            public int DiggersAtDispatch { get; set; }
            public int UnitsOnMoatAtDispatch { get; set; }
            public uint PlayerMaskAtDispatch { get; set; }
            public int CentralPlannerCalls { get; set; }
            public int UnitMoveCalls { get; set; }
            public int UnitMoveCompleted { get; set; }
            public int UnitMovePositive { get; set; }
            public int UnitMoveWithoutBuilder { get; set; }
            public int UnitMoveAlreadyArrived { get; set; }
            public int UnitMoveAbandoned { get; set; }
            public int BuilderIntermediateTargets { get; set; }
            public Dictionary<string, int> ContractRejectionReasons { get; } = new Dictionary<string, int>();
            public int FloodCalls { get; set; }
            public int FloodVanillaPositive { get; set; }
            public int FloodFillBypasses { get; set; }
            public bool FloodOwnerRouteEvaluated { get; set; }
            public bool FloodOwnerRouteAllowed { get; set; }
            public int ModeCalls { get; set; }
            public int RegionCalls { get; set; }
            public int BuilderCalls { get; set; }
            public int VanillaBuilderCalls { get; set; }
            public int FallbackBuilderCalls { get; set; }
            public int FallbackContractRejections { get; set; }
            public int FallbackRollbacks { get; set; }
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
            public int TargetedRouteSearches { get; set; }
            public int TargetedRouteSearchPasses { get; set; }
            public int TargetedRouteCacheHits { get; set; }
            public int TargetedRouteExpandedNodes { get; set; }
            public double TargetedRouteSearchMilliseconds { get; set; }
            public double TargetedRouteMaximumSearchMilliseconds { get; set; }
            public Dictionary<string, TargetedRouteDecision> TargetedRouteDecisions { get; } =
                new Dictionary<string, TargetedRouteDecision>(StringComparer.Ordinal);
            public string LastGroupMoatModeDiagnostic { get; set; }
            public int EarlyRegionCalls { get; set; }
            public int EarlyRegionBypasses { get; set; }
            public Dictionary<string, EarlyGroupRegionDecision> EarlyRegionDecisions { get; } =
                new Dictionary<string, EarlyGroupRegionDecision>();
            public HashSet<string> EarlyRegionLogSignatures { get; } =
                new HashSet<string>(StringComparer.Ordinal);
            public bool HasQueuePreSnapshot { get; set; }
            public NativeWaypointQueueSnapshot QueuePreSnapshot { get; set; }
            public bool HasQueuePostSnapshot { get; set; }
            public NativeWaypointQueueSnapshot QueuePostSnapshot { get; set; }
            public List<string> Diagnostics { get; } = new List<string>();
        }

        private readonly struct EarlyGroupRegionDecision
        {
            public EarlyGroupRegionDecision(
                bool allowed, int unitId, string reason, RouteProbeSummary summary)
            {
                Allowed = allowed;
                UnitId = unitId;
                Reason = reason;
                Summary = summary;
            }

            public bool Allowed { get; }
            public int UnitId { get; }
            public string Reason { get; }
            public RouteProbeSummary Summary { get; }
        }

        private readonly struct TargetedRouteDecision
        {
            public TargetedRouteDecision(
                bool requiredFriendlyMoat, RouteProbeSummary summary)
            {
                RequiredFriendlyMoat = requiredFriendlyMoat;
                Summary = summary;
            }

            public bool RequiredFriendlyMoat { get; }
            public RouteProbeSummary Summary { get; }
        }

        private sealed class UnitMoveFrame
        {
            public UnitMoveFrame(UnitMoveHereEventArgs args, UnitMoveFrame parent,
                int mapEpoch, int tick, MoveCommandScope command)
            {
                Args = args;
                Parent = parent;
                MapEpoch = mapEpoch;
                Tick = tick;
                Command = command;
            }
            public UnitMoveHereEventArgs Args { get; }
            public UnitMoveFrame Parent { get; }
            public int MapEpoch { get; }
            public int Tick { get; }
            public MoveCommandScope Command { get; }
            public PlanScope Plan { get; set; }
            public PlanScope InheritedPlan { get; set; }
            public bool BuilderReached { get; set; }
            public bool NativeModeReached, RegionReached, RecoveryAttempted;
            public string RecoveryRejection;
            public short PrePortalRegion, FailedDestinationRegion, FailedPortalRegion;
            public bool RecoveryApplied;
            public PlacementUnit Placement;
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
            public int RouteStartX { get; set; } = -1;
            public int RouteStartY { get; set; } = -1;
            public bool ExactRouteEndpoints { get; set; }
            public bool NativeGroundPrecheck { get; set; }
            public bool PublishedUsesMoat { get; set; }
            public WeightedMoatEncodedRoute QualifiedTerminalRoute { get; set; }
            public WeightedMoatRouteSummary QualifiedTerminalSummary { get; set; }
            public int PlayerId { get; set; } = -1;
            public uint UnitGlobalId { get; set; }
            public bool IdentityBound { get; set; }
            public bool ModeObserved { get; set; }
            public bool VanillaModeDetected { get; set; }
            public bool FriendlyRouteQualified { get; set; }
            public bool OwnerRouteProbeCompleted { get; set; }
            public bool AttackMovementQualified { get; set; }
            public bool PostCombatRepath { get; set; }
            public bool MoatWorkMovement { get; set; }
            public MoatWorkSelectionScope MoatWorkSearch { get; set; }
            public int MoatWorkTargetTileId { get; set; }
        }

        private sealed class NativeWaypointQueueTracker
        {
            public NativeWaypointQueueTracker(int tribeId)
            {
                TribeId = tribeId;
            }

            public int TribeId { get; }
            public long LastCommandSequence { get; set; }
            public int LastRequestedX { get; set; }
            public int LastRequestedY { get; set; }
            public int EmptyTicks { get; set; }
            public string LastSignature { get; set; }
        }

        private readonly struct NativeWaypointQueueSnapshot
        {
            public NativeWaypointQueueSnapshot(
                int index, int count, int mode,
                int currentX, int currentY, int lastX, int lastY)
            {
                Index = index;
                Count = count;
                Mode = mode;
                CurrentX = currentX;
                CurrentY = currentY;
                LastX = lastX;
                LastY = lastY;
            }

            public int Index { get; }
            public int Count { get; }
            public int Mode { get; }
            public int CurrentX { get; }
            public int CurrentY { get; }
            public int LastX { get; }
            public int LastY { get; }

            public override string ToString() =>
                $"index={Index} count={Count} mode={Mode} " +
                $"current=({CurrentX},{CurrentY}) last=({LastX},{LastY})";
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

            public void SetSource(SelectedCursorUnitSnapshot unit)
            {
                UnitId = unit.UnitId; StartX = unit.StartX; StartY = unit.StartY; StartTileId = unit.StartTileId;
            }

            public int MapEpoch { get; }
            public int UnitId { get; private set; }
            public int PlayerId { get; }
            public int StartX { get; private set; }
            public int StartY { get; private set; }
            public int StartTileId { get; private set; }
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
                EnemyOnlyReachable = false;
                TraversedRegionCount = 0;
                ReachabilityCacheHits = 0;
                StructuralEdgesObserved = 0;
                RouteDistance = int.MaxValue;
                TargetedExpandedNodes = 0;
                TargetedSearchMilliseconds = 0;
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
            public bool EnemyOnlyReachable;
            public int TraversedRegionCount;
            public int ReachabilityCacheHits;
            public int StructuralEdgesObserved;
            public int RouteDistance;
            public int TargetedExpandedNodes;
            public double TargetedSearchMilliseconds;

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
                EnemyOnlyReachable |= other.EnemyOnlyReachable;
                TraversedRegionCount = Math.Max(
                    TraversedRegionCount, other.TraversedRegionCount);
                ReachabilityCacheHits = Math.Max(
                    ReachabilityCacheHits, other.ReachabilityCacheHits);
                StructuralEdgesObserved = Math.Max(
                    StructuralEdgesObserved, other.StructuralEdgesObserved);
                if (other.RouteDistance != int.MaxValue &&
                    (RouteDistance == int.MaxValue || other.RouteDistance < RouteDistance))
                {
                    RouteDistance = other.RouteDistance;
                }
                TargetedExpandedNodes += other.TargetedExpandedNodes;
                TargetedSearchMilliseconds += other.TargetedSearchMilliseconds;
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
                      $"attackTileGraphEvaluated=True"
                    : string.Empty;
                return $"route={RouteFound} friendlyTiles={FriendlyMoatTiles} " +
                    $"enemyTiles={EnemyMoatTiles} invalidTiles={InvalidMoatTiles} " +
                    $"ownerMask=0x{ObservedOwnerMask:X} regions={StartRegion}->{TargetRegion} " +
                    $"groundReachable={ReachedWithoutMoat} " +
                    $"friendlyReachable={ReachedWithMoat} " +
                    $"enemyOnlyReachable={EnemyOnlyReachable} " +
                    $"traversedRegions={TraversedRegionCount} " +
                    $"reachabilityCacheHits={ReachabilityCacheHits}" +
                    $" structuralEdges={StructuralEdgesObserved} " +
                    $"targetedExpanded={TargetedExpandedNodes} " +
                    $"targetedSearchMs={TargetedSearchMilliseconds:F3}" +
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
