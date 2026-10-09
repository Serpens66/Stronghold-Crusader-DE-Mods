using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
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

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        // Detailed command traces are opt-in diagnostics, not production logging.
        internal static readonly bool DetailedDiagnosticsEnabled = false;

        internal const int CentralMovementPlanRva = 0x18E1E0;
        internal const int TribeFloodFillMembershipRva = 0x124740;
        internal const int FirstGroupUnitOnCompletedMoatRva = 0x117BC0;
        internal const int GetGroupUnitIdRva = 0x119F90;
        internal const int GetTribeMovementModeRva = 0x117C70;
        internal const int OrdinaryMovementGroupModeCallRva = 0x11B736;
        internal const int GroupMoatModeCallRva = 0x11B666;
        internal const int UnitStandingOnCompletedMoatRva = 0x196840;
        internal const int RegionReachabilityRva = 0xE7C40;
        internal const int CursorReachabilityRva = 0xE9FF0;
        internal const int SelectionCanDigMoatRva = 0x191C00;
        internal const int SelectionCanDigMoatCallRva = 0x8D3CE;
        internal const int CursorTilePairReachabilityRva = 0xE2CA0;
        internal const int GetRepresentativeSelectedUnitRva = 0x18D460;
        internal const int CursorRegionPrecheckRva = 0xE9D90;
        internal const int PathBuilderRva = 0xF4930;
        internal const int GroundPathBuilderRva = 0xDA590;
        internal const int AlternativePathBuilderRva = 0xDB650;
        internal const int GetMoatIdAtTileRva = 0x69560;
        internal const int AttackApproachFloodBuilderRva = 0xDBC60;
        internal const int BuildingApproachBuilderRva = 0xDA020;
        internal const int BuildingCandidateConsumerRva = 0x123090;
        internal const int RegionPairReachabilityRva = 0xE2610;
        internal const int AttackApproachFloodCallRva = 0x11EE47;
        internal const int AttackApproachFloodAlternativeCallRva = 0x11F46B;
        internal const int BuildingApproachCallRva = 0x11FF9A;
        internal const int BuildingCandidateConsumerCallRva = 0x11FFA7;
        internal const int BuildingCandidateConsumerAlternativeCallRva = 0x1206DF;
        internal const int BuildingCandidateConsumerForceCallRva = 0x120CCD;
        internal const int AttackFloodRegionPairCallRva = 0xDBF0D;
        internal const int AttackFloodTilePairCallRva = 0xDBF33;
        internal const int BuildingApproachRegionPairCallRva = 0xDA1F9;
        internal const int BuildingApproachTilePairCallRva = 0xDA232;
        internal const int BuildingApproachAlternativeRegionPairCallRva = 0xDA47C;
        internal const int BuildingApproachAlternativeTilePairCallRva = 0xDA4B1;
        internal const int OrdinaryMovementLadderPrecheckCallRva = 0x11B768;
        internal const int OrdinaryMovementLadderReachabilityCallRva = 0x11B785;
        internal const int BuildingConsumerFallbackBuilderCallRva = 0x123102;
        internal const int BuildingConsumerGroundBuilderCallRva = 0x12312C;
        internal const int BuildingCursorReachabilityRva = 0xB70C0;
        internal const int BuildingCursorReachabilityCallRva = 0x8DFF6;
        internal const int CombatFinishResumeRva = 0x1853F0;
        internal const int CombatFinishPostCombatCallRva = 0x18540D;
        internal const int PostCombatRepathRva = 0x1976C0;
        internal const int PostCombatMoveHereCallRva = 0x19772B;
        internal const int CursorMoveStagerRva = 0x195E30;
        internal const int NativeSpecialStructurePredicateRva = 0x107160;
        internal const int NativeSpecialStructureContextRva = 0x32DE440;
        internal const int NativeBuildingTypeBiasRva = 0x64CCCDE;
        internal const int CursorMoveStagerRegionPairCallRva = 0x195F46;
        internal static readonly int[] CursorMoveStagerCallRvas =
            { 0x8F7BA, 0x8FD3C, 0x8FDC6, 0x8FE54 };
        internal const int DirectFillApproachRva = 0xE7F60;
        internal const int DirectFillApproachRegionPairCallRva = 0xE81EB;
        internal const int DirectFillApproachCommandCallRva = 0x120E9D;
        internal const int MovementTerrainPhaseRva = 0x19B506;
        internal const int MovementCadenceRva = 0x184203;
        internal const int MovementSubstepRva = 0x1855A0;
        internal const int MovementAdditionalSubstepsRva = 0x1857AA;
        internal const int MoatPathConsumptionReadRva = 0x185934;
        internal const int MoatPathConsumptionPersistRva = 0x19670C;
        internal const int CursorCurrentTileFlagGateRva = 0x8F388;
        internal const int CursorCurrentTileFlagGateJumpRva = 0x8F393;
        internal const int AttackUnitPairGateJumpRva = 0x8D72B;
        internal const int AttackBuildingPairGateJumpRva = 0x8E2C6;
        internal const int AttackAlternativePairGateJumpRva = 0x8E557;
        internal const int TileFlagsRva = 0x48F71B0;
        internal const int MovementTargetAvailabilityRva = 0x3A11EA4;
        internal const int NativeMovementMaskRva = 0x51890D0;
        internal const int RowLookupRva = 0x402FF2C;
        internal const int NativeBuildingLayerRva = 0x4B6AA50;
        internal const int NativeHeightLayerRva = 0x4DDD350;
        internal const int NativeDirectionMaskRva = 0x312620;
        internal const int CursorTargetXRva = 0x3A11E2C;
        internal const int CursorTargetYRva = 0x3A11E30;
        internal const int PathRegionGridRva = 0x50EC690;
        internal const int MoatPathModeRva = 0x60AD6E4;
        internal const int NativePathManagerRva = 0x60AD660;
        internal const int NativeUnitManagerRva = 0x67E8400;
        internal const int NativeTribeManagerRva = 0x7CC6720;

        internal static readonly int[] MoatBuilderSpecialStructureCallRvas =
            { 0xDB29A, 0xDB2EC, 0xDB37A };
        internal static readonly int[] MoatReconstructionSpecialStructureCallRvas =
            { 0xE1856, 0xE18DF, 0xE195B };

        internal const int TribeRecordSize = 0x688;
        internal const int TribeLeadUnitIdOffset = 0x5A;
        internal const int TribeUnitCountOffset = 0x5C;
        internal const int UnitGroupInactiveStateOffset = 0x29C;
        internal const int UnitMoatSlowdownPhaseOffset = 0x6C;
        internal const int UnitPostCombatMovementStateOffset = 0x88;
        internal const int UnitCombatFinishGateOffset = 0x33A;
        // 0x1855A0 reads this currently unnamed short in addition to r_SpeedBonus.
        internal const int UnitAdditionalMovementSubstepsOffset = 0x3CE;
        internal const int MaximumTribeCount = 4500;
        internal const int MaximumUnitCount = 10000;
        internal static readonly int[] EndpointNeighbourX = { -1, 1, 0, 0, -1, 1, -1, 1 };
        internal static readonly int[] EndpointNeighbourY = { 0, 0, -1, 1, -1, -1, 1, 1 };
        internal static readonly byte[] EndpointSourceEdgeMasks =
            { 0x04, 0x40, 0x10, 0x01, 0x08, 0x20, 0x02, 0x80 };

        internal const int MoatRecordArrayOffset = 0x1F3EE30;
        internal const int MoatRecordCountOffset = 0x2038E30;
        internal const int MoatRecordSize = 0x10;
        internal const int MoatOwnerOffset = 0x0C;

        // Script Extender 2.4.0 exposes the native-sized PCL grid as UInt16. Large or
        // fragmented maps can legitimately use region IDs above Int16.MaxValue.
        internal const int MaximumRegionId = ushort.MaxValue;
        internal const int MaximumFloodFillStamp = 0x7D00;
        internal const int MapWidth = 800;
        internal const int MapCellCount = MapWidth * MapWidth;
        // Tile-indexed native arrays contain 0x4E520 entries. MapCellCount is only
        // the rectangular coordinate-cell count used by the managed BFS buffers.
        internal const int NativeTileCount = 0x4E520;
        internal const int RouteStateShift = 20;
        internal const int RouteCellMask = (1 << RouteStateShift) - 1;
        internal const int GroundRouteState = 0;
        internal const int FriendlyMoatRouteState = 1;
        internal const int EnemyMoatRouteState = 2;
        internal const uint CompletedMoatTileFlag = 0x40000000;
        internal const uint AlternativeTerrainDelayTileFlag = 0x00200000;
        internal const uint OrdinaryWalkableTileFlag = 0x00008000;
        internal const uint CursorSpecialStructureTileFlagMask = 0x10000300;
        internal const uint BuildingContextBlockingTileFlagMask = 0x0F000000;
        internal const int PathManagerRouteVariantOffset = 0x80;
        internal const int PathManagerFloodGenerationOffset = 0x04;
        internal const int PathManagerFloodDepthOffset = 0x155F38;
        internal const int PathManagerFloodQueueHeadOffset = 0x155F3C;
        internal const int PathManagerFloodQueueTailOffset = 0x155F44;
        internal const int PathManagerFloodResultTileOffset = 0x1B344;
        internal const int PathManagerFloodResultStride = 0x0C;
        internal const int PathManagerOutputBufferOffset = 0x155F60;
        internal const int PathManagerOutputLengthOffset = 0x155F68;
        internal const int NativeUnitPathBufferOffset = 0xB4FE78;
        internal const int NativeUnitPathBufferStride = 1000;
        internal const int NativeUnitStride = 0x490;
        internal const int NativeUnitSlotDataOffset = 0x65C;
        internal const int NativeMoatPathConsumptionModeOffset = 0x9C8;
        internal const int UnitMoatPathConsumptionModeOffset =
            NativeMoatPathConsumptionModeOffset - NativeUnitSlotDataOffset;
        internal const int VanillaAttackFloodResultCapacity = 500;
        internal const int TribeMovementModeOffset = 0x582;
        internal const int TribeMovementWaypointBaseOffset = 0x5B4;
        internal const int TribeMovementWaypointIndexOffset = 0x5DC;
        internal const int TribeMovementWaypointCountOffset = 0x5DE;
        internal const int MaximumNativeMovementWaypoints = 10;
        internal const int WeightedPublicationSafetyMarginTicks = 40;
        internal const int BuildingCandidateApproachTileOffset = 0x00;
        internal const int BuildingCandidateFootprintTileOffset = 0x04;
        internal const int BuildingCandidateScoreOffset = 0x08;
        internal const int VanillaUnreachableCandidateScore = 10000000;
        internal const ulong RouteFingerprintOffsetBasis = 14695981039346656037UL;
        internal const ulong RouteFingerprintPrime = 1099511628211UL;

        internal const string TribeFloodFillMembershipPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 " +
            "48 83 EC 20 48 63 F2 33 DB 4C 69 CE 88 06 00 00 45 8B F0 48 8B E9";

        internal const string FirstGroupUnitOnCompletedMoatPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 " +
            "41 56 48 83 EC 20 48 63 F2 33 DB 4C 69 C6 88 06 00 00 48 8B E9 " +
            "41 0F BF 7C 08 5C 85 FF 7E 58 4C 8D 35 ?? ?? ?? ??";

        internal const string GetGroupUnitIdPattern =
            "48 89 5C 24 08 48 63 C2 45 33 C9 48 69 D0 88 06 00 00 4C 8B D9 " +
            "66 83 7C 0A 40 02 75 5A 0F BF 44 0A 5C 44 3B C0 7D 50 45 85 C0 " +
            "78 4B 48 83 C1 60";

        internal const string GetTribeMovementModePattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 " +
            "41 56 48 83 EC 20 48 63 F2 33 DB 4C 69 C6 88 06 00 00 48 8B E9 " +
            "41 0F BF 7C 08 5C 85 FF 7E 4D 4C 8D 35 ?? ?? ?? ??";

        internal const string CentralMovementPlanPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC 38 04 00 00 " +
            "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 20 04 00 00 4C 63 FA " +
            "4C 8D 35 ?? ?? ?? ?? 49 69 DF 90 04 00 00 49 63 E8 48 03 D9 49 63 F1";

        internal const string UnitStandingOnCompletedMoatPattern =
            "48 63 C2 48 69 D0 90 04 00 00 48 63 84 0A 2C 07 00 00 " +
            "48 8D 0D ?? ?? ?? ?? 8B 04 81 C1 E8 1E 83 E0 01 C3";

        internal const string RegionReachabilityPattern =
            "44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 38 45 33 D2 49 63 F9 4C 89 51 48 48 8B D9";

        internal const string PathBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 " +
            "48 63 41 0C 48 8B D9 41 8B F0 44 8B D2";

        internal const string GetMoatIdAtTilePattern =
            "48 63 C2 0F B7 84 41 ?? ?? ?? ?? C3 CC CC CC";

        internal const string AttackApproachFloodBuilderPattern =
            "44 89 4C 24 20 53 56 41 54 41 55 41 56 48 83 EC 60 " +
            "48 8B D9 4D 63 E9 45 33 F6 48 8D 0D ?? ?? ?? ?? 45 8B E6 " +
            "44 89 74 24 3C E8 ?? ?? ?? ?? 48 63 F0 41 81 FD 1F 03 00 00";

        internal const string BuildingApproachBuilderPattern =
            "48 89 4C 24 08 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 " +
            "48 8D 0D ?? ?? ?? ?? 4C 63 D2 49 69 D2 88 06 00 00 4D 63 F0 " +
            "4C 8D 25 ?? ?? ?? ?? 4D 69 FE 2C 03 00 00 33 ED 41 8B D9 44 8B ED";

        internal const string BuildingCandidateConsumerPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 40 48 63 DA 41 8B F0 " +
            "8B D3 48 8B F9 E8 ?? ?? ?? ?? 4C 69 CB 88 06 00 00 " +
            "48 8D 1D ?? ?? ?? ?? 49 0F BF 4C 39 5A";

        internal const string RegionPairReachabilityPattern =
            "40 55 41 54 41 55 41 56 48 8D AC 24 78 F7 FF FF " +
            "48 81 EC 88 09 00 00 48 8B 05 ?? ?? ?? ?? 48 33 C4 " +
            "48 89 85 70 08 00 00 4C 63 F2 45 8B E9 4C 8B E1";

        internal const string CursorReachabilityFunctionPattern =
            "44 89 4C 24 20 44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 50 48 63 F2 45 33 ED 33 D2 49 63 E8 49 63 C1 48 8B D9";

        internal const string SelectionCanDigMoatPattern =
            "83 B9 80 05 00 00 00 75 54 83 B9 B4 05 00 00 00 75 4B " +
            "83 B9 68 05 00 00 00 75 42 83 B9 64 05 00 00 00 75 39 " +
            "83 B9 6C 05 00 00 00 75 30 83 B9 E8 05 00 00 00 75 27 " +
            "83 B9 EC 05 00 00 00 75 1E 83 B9 E0 05 00 00 00 75 15 " +
            "83 B9 D8 05 00 00 00 75 0C 83 B9 74 05 00 00 00 75 03 " +
            "33 C0 C3 B8 01 00 00 00 C3";

        internal const string SelectionCanDigMoatCallPattern =
            "44 39 25 ?? ?? ?? ?? 74 3C 48 8B CE E8 ?? ?? ?? ?? 85 C0 74 30 B8 01 00 00 00";

        internal const string BuildingCursorReachabilityPattern =
            "48 89 5C 24 08 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 40 " +
            "4C 8B E1 85 D2 0F 84 ?? ?? ?? ?? 81 FA A0 0F 00 00 0F 8D ?? ?? ?? ?? " +
            "48 63 C2 48 69 D0 2C 03 00 00";

        internal const string CombatFinishResumePattern =
            "40 53 48 83 EC 20 48 63 C2 48 69 D8 90 04 00 00 48 03 D9 " +
            "66 83 BB 96 09 00 00 00 75 14 E8 ?? ?? ?? ?? 33 C0 " +
            "66 89 83 96 09 00 00 89 83 98 09 00 00";

        internal const string PostCombatRepathPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 30 48 63 FA 48 8B F1 " +
            "48 69 DF 90 04 00 00 48 03 D9 66 83 BB F8 08 00 00 00";

        internal const string CursorMoveStagerPattern =
            "48 89 5C 24 10 48 89 6C 24 18 48 89 74 24 20 48 89 4C 24 08 " +
            "57 41 54 41 55 41 56 41 57 48 83 EC 30 8B 84 24 80 00 00 00 " +
            "48 8B F1 8B AC 24 88 00 00 00";

        internal const string DirectFillApproachPattern =
            "44 89 44 24 18 89 54 24 10 53 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 50 45 33 E4 49 63 F9 4C 89";

        internal const string CursorRegionPrecheckPattern =
            "40 53 55 57 41 54 41 56 48 83 EC 20 FF 41 04 48 8B D9 81 79 04 00 7D 00 00 " +
            "41 BC 01 00 00 00 48 63 FA 7E 1F 44 89 61 04";

        internal const string CursorCurrentTileFlagGatePattern =
            "F7 84 97 00 84 89 00 00 01 00 10 74 45 41 8B D6";

        internal const string CursorTilePairReachabilityPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 " +
            "48 83 EC 40 4C 8D 3D ?? ?? ?? ?? 4C 63 DA 45 0F B6 F1 48 8B D9 " +
            "4D 63 C8 4F 8D 04 1B 43 0F BF B4 38 90 C6 0E 05";

        internal const string GetRepresentativeSelectedUnitPattern =
            "48 89 5C 24 18 55 48 8D 6C 24 80 48 81 EC 80 01 00 00 " +
            "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 70 45 33 DB 48 8D 04 24 " +
            "4C 8B C9 0F 57 C0 45 33 C0 41 8D 4B 02";

        internal const string AttackUnitPairGatePattern =
            "85 C0 75 48 49 8B CC E8 ?? ?? ?? ?? 85 C0 74 23 46 8B 84 26 2C 07 00 00 " +
            "48 8D 0D ?? ?? ?? ?? 42 8B 94 23 2C 07 00 00 41 B1 01";

        internal const string AttackBuildingPairGatePattern =
            "48 8B CE E8 ?? ?? ?? ?? 48 8D 15 ?? ?? ?? ?? 85 C0 74 63 48 63 C7 " +
            "48 69 C8 2C 03 00 00 0F BF 84 19 2E 01 00 00 83 C0 D3";

        internal const string AttackAlternativePairGatePattern =
            "85 C0 75 48 49 8B CC E8 ?? ?? ?? ?? 85 C0 74 23 45 8B 84 2C 2C 07 00 00 " +
            "48 8D 0D ?? ?? ?? ?? 43 8B 94 26 2C 07 00 00 41 B1 01";

        internal const string NativeSpecialStructurePredicatePattern =
            "48 63 C2 48 8D 15 ?? ?? ?? ?? 48 0F BF 14 42 66 85 D2 74 1B " +
            "48 69 D2 9C 00 00 00 0F B7 44 0A 6A 66 83 F8 05 7C 09 " +
            "66 83 F8 0F 74 03 B0 01 C3 32 C0 C3";

        internal static readonly byte[] CursorGateJumpOriginal = { 0x74, 0x45 };
        internal static readonly byte[] AttackUnitPairGateOriginal = { 0x74, 0x23 };
        internal static readonly byte[] AttackBuildingPairGateOriginal = { 0x74, 0x63 };
        internal static readonly byte[] AttackAlternativePairGateOriginal = { 0x74, 0x23 };

        internal readonly ManualLogSource log;
        internal readonly IUnitCommandSettings settings;
        internal readonly int* moatPathMode;
        internal readonly int* cursorTargetX;
        internal readonly int* cursorTargetY;
        internal readonly byte* nativeUnitManager;
        internal readonly uint* tileFlags;
        internal readonly byte* movementTargetAvailability;
        internal readonly byte* nativeMovementMasks;
        internal readonly int* nativeRowLookup;
        internal readonly ushort* nativeBuildingLayer;
        internal readonly byte* nativeHeightLayer;
        internal readonly byte* nativeDirectionMasks;
        internal readonly ushort* pathRegionGrid;
        internal readonly IntPtr nativePathManager;
        internal readonly IntPtr nativeTribeManager;
        internal readonly IntPtr nativeSpecialStructureContext;
        internal readonly WeightedMoatRoutePlanner weightedMoatRoutePlanner;
        internal readonly NativeMovementCadenceResolver nativeMovementCadenceResolver;

        [ThreadStatic]
        internal static MoveCommandScope activeMoveCommand;
        [ThreadStatic]
        internal static PlanScope activePlan;
        [ThreadStatic]
        internal static PlanScope pendingPlan;
        [ThreadStatic]
        internal static AttackCursorPairScope pendingAttackCursorPair;
        [ThreadStatic]
        internal static AttackCommandScope activeAttackCommand;
        [ThreadStatic] internal static bool weightedPhaseTimingActive;
        [ThreadStatic]
        internal static AttackApproachDiagnosticScope activeAttackApproachDiagnostic;
        [ThreadStatic] internal static int attackQualificationTimingDepth;
        [ThreadStatic] internal static int requiredPublicationTimingDepth;
        [ThreadStatic]
        internal static BuildingApproachPerformanceScope activeBuildingApproachPerformance;
        [ThreadStatic]
        internal static BuildingConsumerPerformanceScope activeBuildingConsumerPerformance;
        [ThreadStatic]
        internal static LadderAttackProbeScope activeLadderAttackProbe;

        internal MovementOptionsSnapshot CurrentOptions =>
            activeMoveCommand?.Options ?? activeAttackCommand?.Options ??
            GetCurrentUnitMoveFrame()?.Options ?? MovementOptionsSnapshot.Capture(settings);
        internal bool ExtensionsEnabled => CurrentOptions.Enabled;
        internal bool RequiredOnlyMode => CurrentOptions.RequiredOnly;
        internal CentralMovementPlanDelegate originalCentralMovementPlan;
        internal CentralMovementPlanDelegate rootedCentralMovementPlan;
        internal PathBuilderDelegate originalPathBuilder;
        internal PathBuilderDelegate rootedPathBuilder;
        internal UnitStandingOnCompletedMoatDelegate originalUnitStandingOnCompletedMoat;
        internal UnitStandingOnCompletedMoatDelegate rootedUnitStandingOnCompletedMoat;
        internal RegionReachabilityDelegate originalRegionReachability;
        internal RegionReachabilityDelegate rootedRegionReachability;
        internal TribeFloodFillMembershipDelegate originalTribeFloodFillMembership;
        internal TribeFloodFillMembershipDelegate rootedTribeFloodFillMembership;
        internal FirstGroupUnitOnCompletedMoatDelegate originalFirstGroupUnitOnCompletedMoat;
        internal FirstGroupUnitOnCompletedMoatDelegate rootedFirstGroupUnitOnCompletedMoat;
        internal GetGroupUnitIdDelegate getGroupUnitId;
        internal GetTribeMovementModeDelegate getTribeMovementMode;
        internal CursorReachabilityDelegate originalCursorReachability;
        internal CursorReachabilityDelegate rootedCursorReachability;
        internal SelectionCanDigMoatDelegate selectionCanDigMoat;
        internal CursorTilePairReachabilityDelegate originalCursorTilePairReachability;
        internal CursorTilePairReachabilityDelegate rootedCursorTilePairReachability;
        internal GetRepresentativeSelectedUnitDelegate getRepresentativeSelectedUnit;
        internal CursorRegionPrecheckDelegate originalCursorRegionPrecheck;
        internal CursorRegionPrecheckDelegate rootedCursorRegionPrecheck;
        internal GetMoatIdAtTileDelegate getMoatIdAtTile;
        internal NativeSpecialStructurePredicateDelegate nativeSpecialStructurePredicate;
        internal AttackApproachFloodBuilderDelegate originalAttackApproachFloodBuilder;
        internal AttackApproachFloodBuilderDelegate rootedAttackApproachFloodBuilder;
        internal BuildingApproachBuilderDelegate originalBuildingApproachBuilder;
        internal BuildingApproachBuilderDelegate rootedBuildingApproachBuilder;
        internal BuildingCandidateConsumerDelegate originalBuildingCandidateConsumer;
        internal BuildingCandidateConsumerDelegate rootedBuildingCandidateConsumer;
        internal RegionPairReachabilityDelegate originalRegionPairReachability;
        internal RegionPairReachabilityDelegate rootedRegionPairReachability;
        internal BuildingCursorReachabilityDelegate originalBuildingCursorReachability;
        internal BuildingCursorReachabilityDelegate rootedBuildingCursorReachability;
        internal CombatFinishResumeDelegate originalCombatFinishResume;
        internal CombatFinishResumeDelegate rootedCombatFinishResume;
        internal CursorMoveStagerDelegate originalCursorMoveStager;
        internal CursorMoveStagerDelegate rootedCursorMoveStager;

        internal RedBirdDetour<CentralMovementPlanDelegate> centralMovementPlanDetour;
        internal RedBirdDetour<PathBuilderDelegate> pathBuilderDetour;
        internal RedBirdDetour<UnitStandingOnCompletedMoatDelegate> unitStandingOnCompletedMoatDetour;
        internal RedBirdDetour<RegionReachabilityDelegate> regionReachabilityDetour;
        internal RedBirdDetour<TribeFloodFillMembershipDelegate> tribeFloodFillMembershipDetour;
        internal RedBirdDetour<FirstGroupUnitOnCompletedMoatDelegate> firstGroupUnitOnCompletedMoatDetour;
        internal RedBirdDetour<CursorReachabilityDelegate> cursorReachabilityDetour;
        internal RedBirdDetour<CursorTilePairReachabilityDelegate> cursorTilePairReachabilityDetour;
        internal RedBirdDetour<CursorRegionPrecheckDelegate> cursorRegionPrecheckDetour;
        internal RedBirdDetour<AttackApproachFloodBuilderDelegate> attackApproachFloodBuilderDetour;
        internal RedBirdDetour<BuildingApproachBuilderDelegate> buildingApproachBuilderDetour;
        internal RedBirdDetour<BuildingCandidateConsumerDelegate> buildingCandidateConsumerDetour;
        internal RedBirdDetour<RegionPairReachabilityDelegate> regionPairReachabilityDetour;
        internal RedBirdDetour<BuildingCursorReachabilityDelegate> buildingCursorReachabilityDetour;
        internal RedBirdDetour<CombatFinishResumeDelegate> combatFinishResumeDetour;
        internal RedBirdDetour<CursorMoveStagerDelegate> cursorMoveStagerDetour;
        internal readonly ScanRegion nativeRegion;
        internal HookTransaction mainHookTransaction;
        internal HookTransaction buildingCursorHookTransaction;
        internal HookTransaction attackApproachHookTransaction;
        internal IDisposable tribeMoveSubscription;
        internal IDisposable unitMoveSubscription;
        internal UnitMoveFrame unitMoveFrame;
        internal IDisposable tribeTargetSubscription;
        internal IDisposable mapLoadSubscription;
        internal IDisposable mapStartSubscription;
        internal IDisposable mapUnloadSubscription;
        internal bool attackTickSubscribed;

        internal int[] visitedWithoutMoat;
        internal int[] visitedWithMoat;
        internal int[] visitedWithEnemyMoat;
        internal int[] distanceWithoutMoat;
        internal int[] distanceWithMoat;
        internal int[] distanceWithEnemyMoat;
        internal int[] queue;
        internal int[] observedRouteRegions;
        internal int[] reachedGroundRegions;
        internal int[] reachedFriendlyMoatRegions;
        internal int[] reachedEnemyMoatRegions;
        internal int gridGeneration;
        internal int mapEpoch;
        internal int cacheMapEpoch = -1;
        internal bool cacheIncludesEnemyRoutes;
        internal int cachedReachabilityExpandedNodes;
        internal int fallbackContractRejections;
        internal int cacheStartX = -1;
        internal int cacheStartY = -1;
        internal int cachePlayerId = -1;
        internal int cachedTraversedRegionCount;
        internal int cachedReachabilityMapHits;
        internal RouteProbeSummary cachedRouteSummary;
        internal readonly HashSet<ulong> loggedBuildingCursorReachabilityDecisions =
            new HashSet<ulong>();
        internal readonly Dictionary<int, NativeWaypointQueueTracker> trackedNativeWaypointQueues =
            new Dictionary<int, NativeWaypointQueueTracker>();
        internal readonly Dictionary<int, string> lastUnscopedAttackModes = new Dictionary<int, string>();
        internal readonly Dictionary<int, string> lastAttackCommandCandidates = new Dictionary<int, string>();
        internal readonly Dictionary<int, AttackUnitTracker> trackedAttackUnits =
            new Dictionary<int, AttackUnitTracker>();
        internal readonly Dictionary<int, MoatMoveTracker> trackedMoatMoves =
            new Dictionary<int, MoatMoveTracker>();
        internal readonly HashSet<int> requiredBackgroundTrackedUnitIds = new HashSet<int>();
        internal readonly Dictionary<string, int> requiredBackgroundDiagnostics =
            new Dictionary<string, int>(StringComparer.Ordinal);
        internal readonly Dictionary<string, int> requiredBackgroundSuppressedDiagnostics =
            new Dictionary<string, int>(StringComparer.Ordinal);
        internal int requiredBackgroundDiagnosticTick = int.MinValue;
        internal readonly HashSet<string> reportedDiagnosticFailureStages =
            new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<string> loggedDiggerDecisions =
            new HashSet<string>(StringComparer.Ordinal);
        internal readonly HashSet<int> loggedFormationExecuteMoveTypes = new HashSet<int>();
        internal readonly Dictionary<int, string> lastWeightedPublicationDecisionByUnit =
            new Dictionary<int, string>();
        internal int moveCommandSequence;
        internal int attackCommandSequence;
        internal bool callbackFailureReported;
        internal bool weightedShadowBusy;
        internal bool targetedRouteProbeBusy;
        internal bool disposed;

        // Scratch data only: command snapshots and native rollback buffers retain their ownership.
        internal readonly Stack<BuildingFallbackWorkBuffers> buildingFallbackWorkBuffers =
            new Stack<BuildingFallbackWorkBuffers>();
        internal readonly Stack<MoatCandidateField> buildingCandidateFields = new Stack<MoatCandidateField>();

        internal int reachabilityQueueHead, reachabilityQueueTail;
        internal object reachabilityOwner;
        internal int reachabilityTick;

    }
}
