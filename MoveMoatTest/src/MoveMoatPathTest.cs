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
using System.Runtime.InteropServices;
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
        private delegate int CursorReachabilityDelegate(
            IntPtr pathManager, int nativeUnitIndex, int targetX, int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorTilePairFallbackSelectionDelegate(IntPtr selectionState);

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

        private const int CentralMovementPlanRva = 0x18E1E0;
        private const int TribeFloodFillMembershipRva = 0x124740;
        private const int UnitStandingOnCompletedMoatRva = 0x196840;
        private const int RegionReachabilityRva = 0xE7C40;
        private const int CursorReachabilityRva = 0xE9FF0;
        private const int CursorTilePairFallbackSelectionRva = 0x196870;
        private const int CursorTilePairReachabilityRva = 0xE2CA0;
        private const int GetRepresentativeSelectedUnitRva = 0x18D460;
        private const int CursorRegionPrecheckRva = 0xE9D90;
        private const int PathBuilderRva = 0xF4930;
        private const int AssassinPathBuilderRva = 0xD9C40;
        private const int GroundPathBuilderRva = 0xDA590;
        private const int AlternativePathBuilderRva = 0xDB650;
        private const int PathBuilderAssassinBranchRva = 0xF4B0C;
        private const int PathBuilderAssassinCallOffset = 0x1B;
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
        private const int CursorCurrentTileFlagGateRva = 0x8F388;
        private const int CursorCurrentTileFlagGateJumpRva = 0x8F393;
        private const int AttackUnitPairGateJumpRva = 0x8D72B;
        private const int AttackBuildingPairGateJumpRva = 0x8E2C6;
        private const int AttackAlternativePairGateJumpRva = 0x8E557;
        private const int TileFlagsRva = 0x48F71B0;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int CursorTargetXRva = 0x3A11E2C;
        private const int CursorTargetYRva = 0x3A11E30;
        private const int PathRegionGridRva = 0x50EC690;
        private const int MoatPathModeRva = 0x60AD6E4;
        private const int NativePathManagerRva = 0x60AD660;
        private const int NativeUnitManagerRva = 0x67E8400;
        private const int NativeTribeManagerRva = 0x7CC6720;

        private const int MoatRecordArrayOffset = 0x1F3EE30;
        private const int MoatRecordCountOffset = 0x2038E30;
        private const int MoatRecordSize = 0x10;
        private const int MoatOwnerOffset = 0x0C;

        private const int MaximumRegionId = short.MaxValue;
        private const int MaximumFloodFillStamp = 0x7D00;
        private const int MapWidth = 800;
        private const int MapCellCount = MapWidth * MapWidth;
        private const int MoatStateBit = 1 << 20;
        private const uint CompletedMoatTileFlag = 0x40000000;
        private const uint OrdinaryWalkableTileFlag = 0x00008000;
        private const uint CursorSpecialStructureTileFlagMask = 0x10000300;
        private const int PathManagerRouteVariantOffset = 0x80;
        private const int PathManagerMovementVariantOffset = 0x84;
        private const int PathManagerAssassinModeOffset = 0x88;
        private const int PathManagerFloodGenerationOffset = 0x04;
        private const int PathManagerFloodDepthOffset = 0x155F38;
        private const int PathManagerFloodQueueHeadOffset = 0x155F3C;
        private const int PathManagerFloodQueueTailOffset = 0x155F44;
        private const int PathManagerFloodResultTileOffset = 0x1B344;
        private const int PathManagerFloodResultStride = 0x0C;
        private const int VanillaAttackFloodResultCapacity = 500;

        private const string TribeFloodFillMembershipPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 " +
            "48 83 EC 20 48 63 F2 33 DB 4C 69 CE 88 06 00 00 45 8B F0 48 8B E9";

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

        private const string PathBuilderAssassinBranchPattern =
            "39 AB 88 00 00 00 74 1D 89 6C 24 30 48 8B CB C7 44 24 28 80 1A 06 00 " +
            "89 44 24 20 E8 14 51 FE FF";

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
        private readonly short* pathRegionGrid;
        private readonly IntPtr nativePathManager;
        private readonly IntPtr nativeTribeManager;

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
        private CursorReachabilityDelegate originalCursorReachability;
        private CursorReachabilityDelegate rootedCursorReachability;
        private CursorTilePairFallbackSelectionDelegate originalCursorTilePairFallbackSelection;
        private CursorTilePairFallbackSelectionDelegate rootedCursorTilePairFallbackSelection;
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

        private NativeDetour centralMovementPlanDetour;
        private NativeDetour pathBuilderDetour;
        private NativeDetour unitStandingOnCompletedMoatDetour;
        private NativeDetour regionReachabilityDetour;
        private NativeDetour tribeFloodFillMembershipDetour;
        private NativeDetour cursorReachabilityDetour;
        private NativeDetour cursorTilePairFallbackSelectionDetour;
        private NativeDetour cursorTilePairReachabilityDetour;
        private NativeDetour cursorRegionPrecheckDetour;
        private NativeDetour attackApproachFloodBuilderDetour;
        private NativeDetour buildingApproachBuilderDetour;
        private NativeDetour buildingCandidateConsumerDetour;
        private NativeDetour regionPairReachabilityDetour;
        private IDisposable tribeMoveSubscription;
        private IDisposable tribeTargetSubscription;
        private IDisposable mapLoadSubscription;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;
        private bool attackTickSubscribed;

        private int[] visitedWithoutMoat;
        private int[] visitedWithMoat;
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
        private bool cursorChecksArmed;
        private int lastCursorRegionPositiveGeneration = -1;
        private int lastCursorDirectPositiveGeneration = -1;
        private int lastCursorRegionBlockGeneration = -1;
        private int lastCursorDirectBlockGeneration = -1;
        private string lastAttackCursorDecision;
        private string lastCursorSelectionDiagnostic;
        private string lastCursorTilePairDiagnostic;
        private readonly Dictionary<int, string> lastUnscopedAttackModes = new Dictionary<int, string>();
        private readonly Dictionary<int, string> lastAttackCommandCandidates = new Dictionary<int, string>();
        private readonly Dictionary<int, AttackUnitTracker> trackedAttackUnits =
            new Dictionary<int, AttackUnitTracker>();
        private readonly Dictionary<int, MoatMoveTracker> trackedMoatMoves =
            new Dictionary<int, MoatMoveTracker>();
        private readonly Dictionary<int, WallClimbTracker> trackedWallClimbs =
            new Dictionary<int, WallClimbTracker>();
        private readonly HashSet<string> reportedDiagnosticFailureStages =
            new HashSet<string>(StringComparer.Ordinal);
        private int attackCommandSequence;
        private bool callbackFailureReported;
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
            Shared.NativeResolution planResolution = Resolve(
                memory, CentralMovementPlanPattern, CentralMovementPlanRva,
                "central ordinary-movement planner");
            Shared.NativeResolution modeResolution = Resolve(
                memory, UnitStandingOnCompletedMoatPattern, UnitStandingOnCompletedMoatRva,
                "unit-standing-on-completed-moat helper");
            Shared.NativeResolution regionResolution = Resolve(
                memory, RegionReachabilityPattern, RegionReachabilityRva,
                "moat-aware region reachability");
            Shared.NativeResolution builderResolution = Resolve(
                memory, PathBuilderPattern, PathBuilderRva,
                "central tile path builder");
            Shared.NativeResolution assassinBranchResolution = Resolve(
                memory, PathBuilderAssassinBranchPattern, PathBuilderAssassinBranchRva,
                "Assassin path-builder dispatcher branch");
            Shared.NativeResolution moatLookupResolution = Resolve(
                memory, GetMoatIdAtTilePattern, GetMoatIdAtTileRva,
                "moat ID lookup by tile");
            Shared.NativeResolution cursorResolution = Resolve(
                memory, CursorReachabilityFunctionPattern, CursorReachabilityRva,
                "ordinary-movement cursor reachability function");
            Shared.NativeResolution cursorModeResolution = Resolve(
                memory, CursorTilePairFallbackSelectionPattern, CursorTilePairFallbackSelectionRva,
                "cursor tile-pair fallback selection gate");
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
            ValidateExactBytes(
                memory,
                PathBuilderAssassinBranchRva,
                new byte[]
                {
                    0x39, 0xAB, 0x88, 0x00, 0x00, 0x00, 0x74, 0x1D,
                    0x89, 0x6C, 0x24, 0x30, 0x48, 0x8B, 0xCB, 0xC7,
                    0x44, 0x24, 0x28, 0x80, 0x1A, 0x06, 0x00, 0x89,
                    0x44, 0x24, 0x20, 0xE8, 0x14, 0x51, 0xFE, 0xFF
                },
                "Assassin path-builder dispatcher branch and call");
            int assassinBuilderTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                assassinBranchResolution.Rva + PathBuilderAssassinCallOffset + 1,
                assassinBranchResolution.Rva + PathBuilderAssassinCallOffset + 5);
            if (assassinBuilderTarget != AssassinPathBuilderRva)
            {
                throw new InvalidOperationException(
                    "The central builder no longer selects the audited Assassin path builder.");
            }

            moatPathMode = (int*)(libraryBase + MoatPathModeRva);
            cursorTargetX = (int*)(libraryBase + CursorTargetXRva);
            cursorTargetY = (int*)(libraryBase + CursorTargetYRva);
            nativeUnitManager = (byte*)(libraryBase + NativeUnitManagerRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);
            movementTargetAvailability = (byte*)(libraryBase + MovementTargetAvailabilityRva);
            pathRegionGrid = (short*)(libraryBase + PathRegionGridRva);
            nativePathManager = (IntPtr)(libraryBase + NativePathManagerRva);
            nativeTribeManager = (IntPtr)(libraryBase + NativeTribeManagerRva);
            getMoatIdAtTile = Marshal.GetDelegateForFunctionPointer<GetMoatIdAtTileDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)moatLookupResolution.Rva)));
            getRepresentativeSelectedUnit = Marshal.GetDelegateForFunctionPointer<GetRepresentativeSelectedUnitDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)representativeUnitResolution.Rva)));
            rootedCentralMovementPlan = RunCentralMovementPlanWithContext;
            rootedPathBuilder = BuildPathWithCompletedMoatRouteVariant;
            rootedTribeFloodFillMembership = AllowTribeFloodFillForMoveOrder;
            rootedUnitStandingOnCompletedMoat = EnableCompletedMoatModeForScopedMovement;
            rootedRegionReachability = AllowBuilderAfterFailedRegionSearch;
            rootedCursorReachability = AllowCursorReachabilityThroughCompletedMoat;
            rootedCursorTilePairFallbackSelection = ObserveCursorTilePairFallbackSelection;
            rootedCursorTilePairReachability = AllowAttackCursorTilePairThroughCompletedMoat;
            rootedCursorRegionPrecheck = AllowCursorRegionThroughCompletedMoat;

            NativeDetour pendingPlanDetour = null;
            NativeDetour pendingBuilder = null;
            NativeDetour pendingFlood = null;
            NativeDetour pendingMode = null;
            NativeDetour pendingRegion = null;
            NativeDetour pendingCursor = null;
            NativeDetour pendingCursorMode = null;
            NativeDetour pendingCursorTilePair = null;
            NativeDetour pendingCursorRegion = null;
            bool planApplied = false;
            bool builderApplied = false;
            bool floodApplied = false;
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
                pendingBuilder = CreateDetour(
                    libraryBase + unchecked((ulong)builderResolution.Rva),
                    rootedPathBuilder);
                originalPathBuilder = pendingBuilder.GenerateTrampoline<PathBuilderDelegate>();
                pendingFlood = CreateDetour(libraryBase + unchecked((ulong)floodResolution.Rva), rootedTribeFloodFillMembership);
                originalTribeFloodFillMembership = pendingFlood.GenerateTrampoline<TribeFloodFillMembershipDelegate>();
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
                pendingBuilder.Apply();
                builderApplied = true;
                pendingFlood.Apply();
                floodApplied = true;
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
                pathBuilderDetour = pendingBuilder;
                tribeFloodFillMembershipDetour = pendingFlood;
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
                    $"assassinBuilderBranch=0x{assassinBranchResolution.Rva:X}->0x{assassinBuilderTarget:X}, " +
                    $"tribeFloodFill=0x{floodResolution.Rva:X}, moatLookup=0x{moatLookupResolution.Rva:X}; " +
                    "friendlyAndAlliedCompletedMoats=true, enemyMoats=fail-closed-experimental.");

                // This separately validated hook group only observes the earlier unit/building
                // approach pipelines and cannot roll back the proven movement feature.
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
                UndoAndDispose(pendingFlood, floodApplied);
                UndoAndDispose(pendingBuilder, builderApplied);
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
            cursorTilePairReachabilityDetour?.Dispose();
            cursorRegionPrecheckDetour?.Dispose();
            cursorTilePairFallbackSelectionDetour?.Dispose();
            cursorReachabilityDetour?.Dispose();
            regionReachabilityDetour?.Dispose();
            unitStandingOnCompletedMoatDetour?.Dispose();
            tribeFloodFillMembershipDetour?.Dispose();
            pathBuilderDetour?.Dispose();
            centralMovementPlanDetour?.Dispose();
            activeMoveCommand = null;
            activePlan = null;
            pendingPlan = null;
            pendingAttackCursorPair = null;
            pendingCursorSelectionDiagnostic = null;
            activeAttackCommand = null;
            activeAttackApproachDiagnostic = null;
            trackedAttackUnits.Clear();
            trackedMoatMoves.Clear();
            trackedWallClimbs.Clear();
            lastAttackCommandCandidates.Clear();
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
                    "MoveMoat read-only attack-approach diagnostics installed: " +
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
                    "MoveMoat read-only attack-approach diagnostics were not installed; " +
                    $"the existing movement feature remains active: {ex}");
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
                    args.TribeId,
                    args.TileX,
                    args.TileY);
                try
                {
                    LogCommandDiagnostic(
                        $"stage=move-command tribe={args.TribeId} target=({args.TileX},{args.TileY}) " +
                        $"phase=pre patrol={args.IsPatrolPath} newOrder={args.IsNewOrder} " +
                        $"moveType={args.MoveType}");
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
                        $"stage=move-command-result tribe={args.TribeId} " +
                        $"target=({args.TileX},{args.TileY}) patrol={args.IsPatrolPath} " +
                        $"newOrder={args.IsNewOrder} moveType={args.MoveType} return={args.ReturnValue} " +
                        $"plannerCalls={command?.CentralPlannerCalls ?? 0} " +
                        $"floodCalls={command?.FloodCalls ?? 0} " +
                        $"floodVanillaPositive={command?.FloodVanillaPositive ?? 0} " +
                        $"floodBypasses={command?.FloodFillBypasses ?? 0} " +
                        $"modeCalls={command?.ModeCalls ?? 0} regionCalls={command?.RegionCalls ?? 0} " +
                        $"builderCalls={command?.BuilderCalls ?? 0} " +
                        $"vanillaBuilderCalls={command?.VanillaBuilderCalls ?? 0} " +
                        $"fallbackBuilderCalls={command?.FallbackBuilderCalls ?? 0} " +
                        $"positiveBuilders={command?.PositiveBuilderCalls ?? 0} " +
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
                            TrackUnitsUpdatedByAttackCommand(args, scope);
                        else
                            RemoveSynchronousAttackTrackers(scope, "command-rejected");
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
                    (TribeAICommand)unit->r_AI_LastIssuedTribeCommand != args.AICommand ||
                    !MatchesAttackTargetContext(
                        unit, args.AICommand, args.TargetValue1, args.TargetValue2))
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
                        if (unit->r_TribeId != tracker.TribeId || currentCommand != tracker.Command)
                        {
                            EndTrackedAttack(unitId, tracker, "command-ended-or-replaced");
                            continue;
                        }
                        if (!MatchesAttackTargetContext(
                            unit, tracker.Command, tracker.TargetValue1, tracker.TargetValue2))
                        {
                            EndTrackedAttack(unitId, tracker, "target-changed");
                            continue;
                        }

                        string signature =
                        $"{unit->r_AIState}:{unit->r_CurrentTilePositionX}:{unit->r_CurrentTilePositionY}:" +
                        $"{unit->r_TargetTilePositionX}:{unit->r_TargetTilePositionY}:" +
                        $"{unit->r_TargetTilePositionX2}:{unit->r_TargetTilePositionY2}:" +
                        $"{unit->r_NextTilePositionX2}:{unit->r_NextTilePositionY2}:" +
                        $"{unit->r_AttackMoveToTargetTileX}:{unit->r_AttackMoveToTargetTileY}:" +
                        $"{unit->r_CurrentPositionTileId}:{unit->r_TargetPositionTileId}:" +
                        $"{unit->r_NextPositionTileId2}:{unit->r_ContextCurrentPositionTileId}:" +
                        $"{unit->r_PathPlanRelated1}:{unit->r_PathPlanStateBitFlags}:" +
                        $"{unit->r_MovingRelevant}:{unit->p_CurrentPathPlanPosition}:" +
                        $"{unit->p_PathPlanSize}:{unit->r_CurrentSpeed}:{unit->r_CurrentSpeed2}:" +
                        $"{tracker.ModeObserved}:{tracker.PlannerObserved}:{tracker.BuilderObserved}:" +
                        $"{tracker.VanillaModeDetected}:{tracker.LastPlannerTargetX}:" +
                        $"{tracker.LastPlannerTargetY}";
                        if (string.Equals(tracker.LastSignature, signature, StringComparison.Ordinal))
                            continue;

                        tracker.LastSignature = signature;
                        Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=attack-state tick={tick} unit={unitId} " +
                        $"type={unit->r_UnitChimp} global={unit->r_GlobalId} " +
                        $"player={unit->r_ControllableForPlayerId} " +
                        $"tribe={unit->r_TribeId} aiState={unit->r_AIState} " +
                        $"command={currentCommand}({(uint)currentCommand}) " +
                        $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                        $"target=({unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}) " +
                        $"target2=({unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2}) " +
                        $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2}) " +
                        $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY}) " +
                        $"tiles={unit->r_CurrentPositionTileId}/{unit->r_TargetPositionTileId}/" +
                        $"{unit->r_NextPositionTileId2} contextCurrentTile={unit->r_ContextCurrentPositionTileId} " +
                        $"contextUnit={unit->r_AI_ContextTargetUnitId}/" +
                        $"{unit->r_AI_ContextTargetUnitGlobalId} " +
                        $"contextBuildingTile={unit->r_AI_ContextTargetBuildingTileId} " +
                        $"contextTile=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                        $"speed={unit->r_CurrentSpeed}/{unit->r_CurrentSpeed2} " +
                        $"path={unit->r_PathPlanRelated1}/{unit->r_PathPlanStateBitFlags}/" +
                        $"{unit->r_MovingRelevant}/{unit->p_CurrentPathPlanPosition}/" +
                        $"{unit->p_PathPlanSize} mode={tracker.ModeObserved} " +
                        $"planner={tracker.PlannerObserved} builder={tracker.BuilderObserved} " +
                        $"vanillaStandingOnMoat={tracker.VanillaModeDetected} " +
                            $"plannerTarget=({tracker.LastPlannerTargetX},{tracker.LastPlannerTargetY}).");
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
            ObserveTrackedWallClimbStates(tick);
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

                trackedMoatMoves[plan.UnitId] = new MoatMoveTracker(
                    mapEpoch,
                    plan.UnitId,
                    unit->r_TribeId,
                    plan.TargetX,
                    plan.TargetY,
                    builderResult);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=move-track-start unit={plan.UnitId} type={unit->r_UnitChimp} " +
                    $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId} " +
                    $"target=({plan.TargetX},{plan.TargetY}) builderResult={builderResult} " +
                    $"{summary.ToLogFields()}.");
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("move-track-start", ex);
            }
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
                    if (unit->r_TribeId != tracker.TribeId)
                    {
                        EndTrackedMoatMove(unitId, tracker, "tribe-changed");
                        continue;
                    }

                    bool reachedRequestedTarget =
                        unit->r_CurrentTilePositionX == tracker.TargetX &&
                        unit->r_CurrentTilePositionY == tracker.TargetY;
                    bool pathConsumed = unit->p_PathPlanSize > 0 &&
                        unit->p_CurrentPathPlanPosition >= unit->p_PathPlanSize;
                    bool settledOnCurrentTile =
                        unit->r_TargetTilePositionX == unit->r_CurrentTilePositionX &&
                        unit->r_TargetTilePositionY == unit->r_CurrentTilePositionY &&
                        unit->r_NextTilePositionX2 == unit->r_CurrentTilePositionX &&
                        unit->r_NextTilePositionY2 == unit->r_CurrentTilePositionY;
                    if (reachedRequestedTarget && pathConsumed && settledOnCurrentTile)
                    {
                        EndTrackedMoatMove(unitId, tracker, "path-completed-at-target");
                        continue;
                    }

                    bool currentMoat = IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId));
                    bool nextMoat = IsCompletedMoatTile(unchecked((int)unit->r_NextPositionTileId2));
                    bool targetMoat = IsCompletedMoatTile(unchecked((int)unit->r_TargetPositionTileId));
                    TribeAICommand command = (TribeAICommand)unit->r_AI_LastIssuedTribeCommand;
                    string signature =
                        $"{unit->r_AIState}:{(uint)command}:" +
                        $"{unit->r_CurrentTilePositionX}:{unit->r_CurrentTilePositionY}:" +
                        $"{unit->r_TargetTilePositionX}:{unit->r_TargetTilePositionY}:" +
                        $"{unit->r_TargetTilePositionX2}:{unit->r_TargetTilePositionY2}:" +
                        $"{unit->r_NextTilePositionX2}:{unit->r_NextTilePositionY2}:" +
                        $"{unit->r_CurrentPositionTileId}:{unit->r_TargetPositionTileId}:" +
                        $"{unit->r_NextPositionTileId2}:{unit->r_PathPlanRelated1}:" +
                        $"{unit->r_PathPlanStateBitFlags}:{unit->r_MovingRelevant}:" +
                        $"{unit->p_CurrentPathPlanPosition}:{unit->p_PathPlanSize}:" +
                        $"{unit->r_CurrentSpeed}:{unit->r_CurrentSpeed2}:" +
                        $"{currentMoat}:{nextMoat}:{targetMoat}";
                    if (string.Equals(tracker.LastSignature, signature, StringComparison.Ordinal))
                        continue;

                    tracker.LastSignature = signature;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=move-state tick={tick} unit={unitId} " +
                        $"type={unit->r_UnitChimp} player={unit->r_ControllableForPlayerId} " +
                        $"tribe={unit->r_TribeId} aiState={unit->r_AIState} command={command}({(uint)command}) " +
                        $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                        $"target=({unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}) " +
                        $"target2=({unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2}) " +
                        $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2}) " +
                        $"requestedTarget=({tracker.TargetX},{tracker.TargetY}) " +
                        $"tiles={unit->r_CurrentPositionTileId}/{unit->r_TargetPositionTileId}/" +
                        $"{unit->r_NextPositionTileId2} moat={currentMoat}/{targetMoat}/{nextMoat} " +
                        $"speed={unit->r_CurrentSpeed}/{unit->r_CurrentSpeed2} " +
                        $"path={unit->r_PathPlanRelated1}/{unit->r_PathPlanStateBitFlags}/" +
                        $"{unit->r_MovingRelevant}/{unit->p_CurrentPathPlanPosition}/" +
                        $"{unit->p_PathPlanSize} builderResult={tracker.BuilderResult}.");
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("move-state-tick", ex);
            }
        }

        private bool IsCompletedMoatTile(int tileId) =>
            IsValidTileId(tileId) && (tileFlags[tileId] & CompletedMoatTileFlag) != 0;

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
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=move-state-end unit={unitId} target=({tracker.TargetX},{tracker.TargetY}) " +
                $"reason={reason}.");
        }

        private static bool MatchesAttackTargetContext(
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

            return (command == TribeAICommand.AttackBuilding ||
                    command == TribeAICommand.ForceAttackBuilding) &&
                unit->r_AI_ContextTargetBuildingTileId == unchecked((uint)targetValue1);
        }

        private static bool IsAttackCommand(TribeAICommand command) =>
            command == TribeAICommand.AttackUnit ||
            command == TribeAICommand.AttackBuilding ||
            command == TribeAICommand.ForceAttackBuilding;

        private static bool IsBuildingAttackCommand(TribeAICommand command) =>
            command == TribeAICommand.AttackBuilding ||
            command == TribeAICommand.ForceAttackBuilding;

        private int RunCentralMovementPlanWithContext(
            IntPtr unitManager, int unitId, int targetX, int targetY)
        {
            if (disposed || unitManager == IntPtr.Zero || unitId <= 0)
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);

            MarkTrackedAttackPipeline(unitId, AttackPipelineStage.Planner, targetX, targetY, false);
            MarkTrackedWallPipeline(unitId, "planner", targetX, targetY, IntPtr.Zero, null);

            PlanScope previous = activePlan;
            PlanScope plan = new PlanScope(unitId, targetX, targetY);
            if (activeMoveCommand != null)
                activeMoveCommand.CentralPlannerCalls++;
            if (activeMoveCommand == null)
            {
                try
                {
                    if (!TryFindFriendlyCompletedMoatRouteForPlan(plan, out RouteProbeSummary summary))
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

        private int EnableCompletedMoatModeForScopedMovement(IntPtr unitManager, int unitId)
        {
            int vanillaResult = originalUnitStandingOnCompletedMoat(unitManager, unitId);
            MarkTrackedWallPipeline(unitId, "mode", -1, -1, IntPtr.Zero, vanillaResult);
            PlanScope plan = activePlan;
            bool plannerQualified = plan != null && plan.FriendlyRouteQualified;
            if (disposed || unitManager == IntPtr.Zero || unitId <= 0)
                return vanillaResult;

            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null)
                    return vanillaResult;

                if (activeMoveCommand == null && !plannerQualified)
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
                            "friendly-moat-required", attackSummary);
                    }
                    catch
                    {
                        // Qualification remains valid even if diagnostics fail.
                    }
                }

                MarkTrackedAttackPipeline(
                    unitId, AttackPipelineStage.Mode, -1, -1, vanillaResult != 0);

                if (plan == null)
                {
                    // Some ordinary MoveHere paths reach the mode helper without passing
                    // through the central planner detour. The surrounding Extender event
                    // still owns the exact command target for this synchronous call chain.
                    plan = new PlanScope(
                        unitId,
                        activeMoveCommand.TargetX,
                        activeMoveCommand.TargetY);
                    pendingPlan = plan;
                }
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
            if ((TribeAICommand)unit->r_AI_LastIssuedTribeCommand != scope.Command ||
                !MatchesAttackTargetContext(
                    unit, scope.Command, scope.TargetValue1, scope.TargetValue2))
            {
                rejectionReason = "command-or-target-context-mismatch";
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
            if (!TryFindFriendlyCompletedMoatRouteForAttackPlan(plan, out summary))
            {
                plan = null;
                rejectionReason = "no-required-friendly-moat-route";
                return false;
            }

            plan.FriendlyRouteQualified = true;
            GetOrCreateAttackTracker(scope, unitId);
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

            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage={stage} unit={unitId} type={unit->r_UnitChimp} " +
                $"player={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId}/{scope?.TribeId} " +
                $"command={(TribeAICommand)unit->r_AI_LastIssuedTribeCommand}/{scope?.Command} " +
                $"target={scope?.TargetValue1}/{scope?.TargetValue2} " +
                $"attackMove=({unit->r_AttackMoveToTargetTileX},{unit->r_AttackMoveToTargetTileY}) " +
                $"vanillaMode={vanillaResult} reason={reason} {summary.ToLogFields()}.");
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

            if (activeMoveCommand != null)
                activeMoveCommand.RegionCalls++;

            try
            {
                bool bypass = vanillaResult == 0 && *moatPathMode == 1 &&
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

        private int BuildPathWithCompletedMoatRouteVariant(
            IntPtr pathManager, int movementClass, int movementProfile)
        {
            MoveCommandScope command = activeMoveCommand;
            PlanScope plan = activePlan ?? pendingPlan;
            bool plannerQualified = plan != null && plan.FriendlyRouteQualified;
            if (disposed || pathManager == IntPtr.Zero || plan == null ||
                (command == null && !plannerQualified))
            {
                WallClimbTracker wallTracker = null;
                try
                {
                    if (pathManager != IntPtr.Zero)
                    {
                        wallTracker = TryGetWallTrackerForUnscopedBuilder();
                        if (wallTracker != null)
                        {
                            LogWallBuilderState("entry", wallTracker, pathManager,
                                movementClass, movementProfile, null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    TryLogDiagnosticFailure("wall-builder-entry", ex);
                    wallTracker = null;
                }
                int wallVanillaResult = originalPathBuilder(
                    pathManager, movementClass, movementProfile);
                if (wallTracker != null)
                {
                    try
                    {
                        LogWallBuilderState("return", wallTracker, pathManager,
                            movementClass, movementProfile, wallVanillaResult);
                        wallTracker.BuilderObserved = true;
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure("wall-builder-return", ex);
                    }
                }
                return wallVanillaResult;
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
            bool builderEligible = plan.ModeObserved && !plan.VanillaModeDetected &&
                currentMoatMode == 1;
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
            int* assassinMode =
                (int*)((byte*)pathManager.ToPointer() + PathManagerAssassinModeOffset);
            int originalAssassinMode = *assassinMode;
            bool isAssassin =
                GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* builderUnit) &&
                builderUnit != null &&
                builderUnit->r_AliveState == AliveState.IsAlive &&
                builderUnit->r_UnitChimp == eChimps.CHIMP_TYPE_ARAB_ASSASIN;
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
            bool route80FallbackCandidate = vanillaResult == 0 && originalRouteVariant == 1;
            bool assassinGroundFallbackCandidate = vanillaResult == 0 &&
                originalRouteVariant == 0 && originalAssassinMode != 0 && isAssassin;
            string fallbackCandidate = route80FallbackCandidate
                ? "route80"
                : assassinGroundFallbackCandidate ? "assassin-ground" : "none";
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
            if (!route80FallbackCandidate && !assassinGroundFallbackCandidate)
            {
                RecordBuilderResult(command, vanillaResult);
                return vanillaResult;
            }

            RouteProbeSummary routeSummary;
            try
            {
                bool friendlyRoute = TryFindFriendlyCompletedMoatRouteForPlan(
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

            if (route80FallbackCandidate)
                *routeVariant = 0;
            LogBuilderNativeState(
                "before-fallback", pathManager, plan,
                movementClass, movementProfile, vanillaResult);
            // The first unchanged run keeps Assassin wall weighting. A qualified pure-moat
            // retry needs the ordinary ground builder because D9C40 still rejects moat tiles.
            bool useAssassinGroundFallback = originalAssassinMode != 0 && isAssassin;

            int result;
            try
            {
                if (command != null)
                    command.FallbackBuilderCalls++;
                if (useAssassinGroundFallback)
                    *assassinMode = 0;
                result = originalPathBuilder(pathManager, movementClass, movementProfile);
            }
            catch
            {
                *routeVariant = originalRouteVariant;
                throw;
            }
            finally
            {
                if (useAssassinGroundFallback)
                    *assassinMode = originalAssassinMode;
            }

            bool retained = result > 0;
            if (route80FallbackCandidate && !retained)
                *routeVariant = originalRouteVariant;

            LogBuilderNativeState(
                "after-fallback", pathManager, plan,
                movementClass, movementProfile, result);

            try
            {
                if (useAssassinGroundFallback)
                {
                    LogBuilderDecision(
                        $"stage=builder-assassin-ground-fallback unit={plan.UnitId} " +
                        $"target=({plan.TargetX},{plan.TargetY}) " +
                        $"path80={originalRouteVariant}->{*routeVariant} " +
                        $"path88={originalAssassinMode}->0->" +
                        $"{*assassinMode} vanillaResult={vanillaResult} result={result} " +
                        $"retained={retained} {routeSummary.ToLogFields()}");
                }
                if (route80FallbackCandidate)
                {
                    LogBuilderDecision(
                        $"stage=builder-route80 unit={plan.UnitId} movementClass={movementClass} " +
                        $"movementProfile={movementProfile} original=1 " +
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
            AttackApproachDiagnosticScope scope = null;
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
                TryLogDiagnosticFailure("attack-approach-building-pre", ex);
                scope = null;
            }

            try
            {
                originalBuildingApproachBuilder(
                    pathManager, tribeId, buildingId, requestedResults, sourceRegion, movementClass);
            }
            finally
            {
                if (scope != null)
                {
                    try
                    {
                        scope.After = CaptureAttackApproachState(pathManager);
                        LogAttackApproachDiagnostic(scope);
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure("attack-approach-building-post", ex);
                    }
                }
                activeAttackApproachDiagnostic = previous;
            }
        }

        private void ObserveBuildingCandidateConsumer(
            IntPtr tribeManager, int tribeId, int builderVariant)
        {
            AttackApproachDiagnosticScope previous = activeAttackApproachDiagnostic;
            AttackApproachDiagnosticScope scope = null;
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
                        CaptureAttackApproachState(nativePathManager))
                    {
                        AllSelectedAssassins = allSelectedUnitsAssassins != null &&
                            allSelectedUnitsAssassins(tribeManager, tribeId) != 0,
                        ConsumerVariant = builderVariant
                    };
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
                originalBuildingCandidateConsumer(tribeManager, tribeId, builderVariant);
            }
            finally
            {
                if (scope != null)
                {
                    try
                    {
                        scope.After = CaptureAttackApproachState(nativePathManager);
                        LogAttackApproachDiagnostic(scope);
                    }
                    catch (Exception ex)
                    {
                        TryLogDiagnosticFailure("attack-approach-building-consumer-post", ex);
                    }
                }
                activeAttackApproachDiagnostic = previous;
            }
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
                routeKind != 0)
            {
                return AttackRegionFallbackDecision.Reject("movement-or-region-context-mismatch");
            }
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(scope.UnitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_TribeId != scope.TribeId ||
                unit->r_ControllableForPlayerId != scope.PlayerId)
            {
                return AttackRegionFallbackDecision.Reject("representative-unit-mismatch");
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
            if (summary.StartRegion != sourceRegion || summary.TargetRegion != targetRegion)
            {
                return AttackRegionFallbackDecision.Reject(
                    "resolved-region-pair-mismatch", summary, approachX, approachY);
            }

            return AttackRegionFallbackDecision.Allow(summary, approachX, approachY);
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
                    unit->r_TribeId != command.TribeId)
                {
                    continue;
                }

                unitId = candidateId;
                playerId = unit->r_ControllableForPlayerId;
                unitType = unit->r_UnitChimp;
                return;
            }
        }

        private static AttackApproachState CaptureAttackApproachState(IntPtr pathManager)
        {
            if (pathManager == IntPtr.Zero)
                return default;

            byte* manager = (byte*)pathManager.ToPointer();
            int resultCount = 0;
            int firstResultTile = 0;
            for (int index = 0; index < VanillaAttackFloodResultCapacity; index++)
            {
                byte* result = manager + PathManagerFloodResultTileOffset +
                    index * PathManagerFloodResultStride;
                int tileId = *(int*)result;
                int classification = *(int*)(result + 4);
                if (tileId == 0 && classification == 0)
                    break;
                if (resultCount == 0)
                    firstResultTile = tileId;
                resultCount++;
            }

            return new AttackApproachState(
                *(int*)(manager + PathManagerFloodGenerationOffset),
                *(int*)(manager + PathManagerFloodDepthOffset),
                *(int*)(manager + PathManagerFloodQueueHeadOffset),
                *(int*)(manager + PathManagerFloodQueueTailOffset),
                resultCount,
                firstResultTile);
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
                bool hasEligibleSlot = false;
                for (int index = 0; index < 35; index++)
                {
                    if (slots[index] == 0)
                        continue;

                    occupiedSlots |= 1UL << index;
                    if (index != 22)
                    {
                        cursorChecksArmed = true;
                        hasEligibleSlot = true;
                    }
                }

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

                bool validTarget = targetX >= 0 && targetX < MapWidth &&
                    targetY >= 0 && targetY < MapWidth;
                GameUnit* unit = null;
                bool validUnit = unitId > 0 && unitId < nextUnitId &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out unit) && unit != null;
                if (validUnit)
                {
                    playerId = unit->r_ControllableForPlayerId;
                    startX = unit->r_CurrentTilePositionX;
                    startY = unit->r_CurrentTilePositionY;
                    startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                }
                if (validTarget)
                    targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);

                bool validPair = validUnit && validTarget &&
                    IsValidTileId(startTileId) && IsValidTileId(targetTileId);
                CursorPairFallbackKind fallbackKind = CursorPairFallbackKind.DirectTile;
                bool hostileUnitTarget = false;
                bool occupiedByLivingUnit = false;
                bool freeOrdinaryTarget = false;
                BuildingCursorTarget buildingTarget = default;
                bool hostileBuildingTarget = false;
                bool wallTarget = false;
                if (validPair)
                {
                    hostileUnitTarget = TryGetHostileLivingUnitAtTile(
                        playerId,
                        targetX,
                        targetY,
                        -1,
                        -1,
                        out _,
                        out occupiedByLivingUnit);
                    hostileBuildingTarget = TryGetHostileLivingBuildingForCursor(
                        playerId, targetTileId, out buildingTarget, out wallTarget);
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
                bool ownerRoute = candidateScope != null &&
                    TryQualifyCursorScope(candidateScope, out _, out _, out _);
                bool functionalArmed = candidateScope != null && ownerRoute;
                if (!validUnit)
                    rejectionReason = "invalid-representative-unit";
                else if (!validTarget)
                    rejectionReason = "invalid-cursor-target";
                else if (!validPair)
                    rejectionReason = "invalid-tile-pair";
                else if (wallTarget)
                    rejectionReason = "wall-or-stair-kept-vanilla";
                else if (!freeOrdinaryTarget && !hostileUnitTarget && !hostileBuildingTarget)
                    rejectionReason = "target-not-free-or-hostile-entity";
                else if (!ownerRoute)
                    rejectionReason = "no-required-friendly-moat-route";
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
                    hasEligibleSlot,
                    functionalArmed,
                    fallbackKind,
                    rejectionReason,
                    buildingTarget.BuildingId,
                    buildingTarget.GlobalId,
                    buildingTarget.BuildingType);
                pendingCursorSelectionDiagnostic = diagnostic;
                LogCursorSelectionDiagnostic(diagnostic);

                // A positive selection-gate result is diagnostic only. The functional moat
                // fallback retains its proven Vanilla-zero requirement.
                if (functionalArmed)
                {
                    pendingAttackCursorPair = candidateScope;
                }

                if (wallTarget && validUnit &&
                    unit->r_UnitChimp == eChimps.CHIMP_TYPE_ARAB_ASSASIN)
                {
                    TrackWallClimbCandidate(unitId, playerId, startX, startY,
                        targetX, targetY, targetTileId, buildingTarget);
                }

                // A positive Vanilla answer is authoritative. Only a Vanilla rejection may be
                // lifted, and only after the route has already passed the owner-aware moat probe.
                if (vanillaResult == 0 && functionalArmed)
                    return 1;
            }
            catch (Exception ex)
            {
                cursorChecksArmed = false;
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
                $"{diagnostic.OccupiedSlots:X}:{diagnostic.HasEligibleSlot}:" +
                $"{diagnostic.FunctionalFallbackArmed}:{diagnostic.FallbackKind}:" +
                $"{diagnostic.BuildingId}:{diagnostic.BuildingGlobalId}:" +
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
                $"slots=0x{diagnostic.OccupiedSlots:X9} eligibleSlot={diagnostic.HasEligibleSlot} " +
                $"fallbackArmed={diagnostic.FunctionalFallbackArmed} " +
                $"fallbackKind={diagnostic.FallbackKind} " +
                $"building={diagnostic.BuildingId}/{diagnostic.BuildingGlobalId}/" +
                $"{diagnostic.BuildingType} " +
                $"reason={diagnostic.RejectionReason}.");
        }

        private void LogCursorTilePairDiagnostic(
            CursorSelectionDiagnosticScope diagnostic,
            int actualTargetTileId,
            int actualSelectedUnitTileId,
            byte useCache,
            int vanillaTilePairResult)
        {
            bool mapMatches = diagnostic.MapEpoch == mapEpoch;
            bool pairMatches = diagnostic.StartTileId == actualSelectedUnitTileId &&
                (diagnostic.TargetTileId == actualTargetTileId ||
                 (diagnostic.FallbackKind == CursorPairFallbackKind.BuildingApproach &&
                  diagnostic.BuildingId > 0 && IsValidTileId(actualTargetTileId) &&
                  GameTileManagerAPI.Instance.GetTileBuildingId(actualTargetTileId) ==
                      diagnostic.BuildingId));
            bool effectiveFallbackScope = diagnostic.FunctionalFallbackArmed && mapMatches &&
                pairMatches && useCache == 1 && vanillaTilePairResult == 0;
            string reason;
            if (!mapMatches)
                reason = "map-epoch-mismatch";
            else if (!diagnostic.FunctionalFallbackArmed)
                reason = diagnostic.RejectionReason;
            else if (!pairMatches)
                reason = "tile-pair-mismatch";
            else if (useCache != 1)
                reason = "cache-mode-not-one";
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
                        cursorDiagnostic, targetTileId, selectedUnitTileId, useCache, vanillaResult);
                }
                catch (Exception ex)
                {
                    TryLogDiagnosticFailure("cursor-tile-pair-observer", ex);
                }
            }

            if (disposed || vanillaResult != 0)
                return vanillaResult;

            if (scope == null || scope.MapEpoch != mapEpoch || useCache != 1 ||
                scope.StartTileId != selectedUnitTileId ||
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
                friendlyRoute = TryQualifyCursorScope(
                    scope, out approachX, out approachY, out summary);
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
                        $"effective={(friendlyRoute ? 1 : 0)} {summary.ToLogFields()}.");
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
                if (vanillaResult != 0 &&
                    !HasSeparatedPositiveRegions(nativeUnitIndex, targetX, targetY))
                {
                    return vanillaResult;
                }

                bool friendlyRoute = HasConservativeFriendlyCompletedMoatRoute(
                    nativeUnitIndex, targetX, targetY, out RouteProbeSummary summary);
                if (vanillaResult != 0)
                {
                    if (ShouldBlockPositiveCursorResult(friendlyRoute, summary))
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

                if (!friendlyRoute)
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

        private int AllowCursorReachabilityThroughCompletedMoat(
            IntPtr pathManager, int nativeUnitIndex, int targetX, int targetY)
        {
            int vanillaResult = originalCursorReachability(pathManager, nativeUnitIndex, targetX, targetY);
            if (disposed)
                return vanillaResult;

            try
            {
                if (vanillaResult != 0 &&
                    !HasSeparatedPositiveRegions(nativeUnitIndex, targetX, targetY))
                {
                    return vanillaResult;
                }

                bool friendlyRoute = HasConservativeFriendlyCompletedMoatRoute(
                    nativeUnitIndex, targetX, targetY, out RouteProbeSummary summary);
                if (vanillaResult != 0)
                {
                    if (ShouldBlockPositiveCursorResult(friendlyRoute, summary))
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

                if (!friendlyRoute)
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
            bool friendlyRoute, RouteProbeSummary summary) =>
            !friendlyRoute && summary.EnemyMoatTiles > 0 &&
            summary.StartRegion > 0 && summary.TargetRegion > 0 &&
            summary.StartRegion != summary.TargetRegion;

        private bool HasSeparatedPositiveRegions(int nativeUnitIndex, int targetX, int targetY)
        {
            if (targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth)
                return false;

            int nextUnitId = *(int*)nativeUnitManager;
            if (nativeUnitIndex <= 0 || nativeUnitIndex >= nextUnitId)
                return false;

            byte* nativeUnit = nativeUnitManager + (nativeUnitIndex * 0x490);
            int startX = *(ushort*)(nativeUnit + 0x71C);
            int startY = *(ushort*)(nativeUnit + 0x71E);
            if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                return false;

            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            if (!IsValidTileId(startTileId) || !IsValidTileId(targetTileId))
                return false;

            int startRegion = pathRegionGrid[startTileId];
            int targetRegion = pathRegionGrid[targetTileId];
            return startRegion > 0 && targetRegion > 0 && startRegion != targetRegion;
        }

        private bool HasConservativeFriendlyCompletedMoatRoute(
            int nativeUnitIndex, int targetX, int targetY, out RouteProbeSummary summary)
        {
            summary = default;
            if (!cursorChecksArmed || targetX < 0 || targetX >= MapWidth ||
                targetY < 0 || targetY >= MapWidth ||
                movementTargetAvailability[(targetY * MapWidth) + targetX] == 0)
            {
                return false;
            }

            int nextUnitId = *(int*)nativeUnitManager;
            if (nativeUnitIndex <= 0 || nativeUnitIndex >= nextUnitId)
                return false;

            byte* nativeUnit = nativeUnitManager + (nativeUnitIndex * 0x490);
            int startX = *(ushort*)(nativeUnit + 0x71C);
            int startY = *(ushort*)(nativeUnit + 0x71E);
            if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                return false;

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(nativeUnitIndex, out GameUnit* unit) ||
                unit == null)
            {
                return false;
            }

            return TryFindFriendlyCompletedMoatRoute(
                nativeUnitIndex,
                unit->r_ControllableForPlayerId,
                startX,
                startY,
                targetX,
                targetY,
                out summary);
        }

        private bool TryFindFriendlyCompletedMoatRouteForPlan(
            PlanScope plan, out RouteProbeSummary summary)
        {
            summary = default;
            if (plan == null || plan.TargetX < 0 || plan.TargetY < 0 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) ||
                unit == null)
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

        private bool TryFindFriendlyCompletedMoatRouteForAttackPlan(
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
            int startTileId = GameTileManagerAPI.Instance.GetTileId(
                unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
            bool startOnCompletedMoat = IsValidTileId(startTileId) &&
                (tileFlags[startTileId] & CompletedMoatTileFlag) != 0;
            bool regionTopologyQualified =
                (summary.StartRegion > 0 && summary.StartRegion != summary.TargetRegion) ||
                (summary.StartRegion == 0 && startOnCompletedMoat);

            summary.AttackProbeEvaluated = true;
            summary.ReachedWithMoat = reachedWithMoat;
            summary.ReachedWithoutMoat = reachedWithoutMoat;
            summary.RegionTopologyQualified = regionTopologyQualified;
            summary.RouteFound = reachedWithMoat && !reachedWithoutMoat &&
                regionTopologyQualified && summary.FriendlyMoatTiles > 0;
            return summary.RouteFound;
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
                regionTopologyQualified && summary.FriendlyMoatTiles > 0;
            return summary.RouteFound;
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

            if (scope.FallbackKind == CursorPairFallbackKind.BuildingApproach)
            {
                return TryFindFriendlyCompletedMoatRouteToBuildingApproach(
                    scope, out approachX, out approachY, out summary);
            }

            return TryFindFriendlyCompletedMoatRouteToAttackApproach(
                scope, out approachX, out approachY, out summary);
        }

        private bool CursorScopeMatchesTargetTile(AttackCursorPairScope scope, int targetTileId)
        {
            if (scope.FallbackKind != CursorPairFallbackKind.BuildingApproach)
                return scope.TargetTileId == targetTileId;
            return IsValidTileId(targetTileId) && scope.BuildingId > 0 &&
                GameTileManagerAPI.Instance.GetTileBuildingId(targetTileId) == scope.BuildingId;
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

        private bool TryFindFriendlyCompletedMoatRouteToBuildingApproach(
            AttackCursorPairScope scope,
            out int approachX,
            out int approachY,
            out RouteProbeSummary summary)
        {
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
            bool startOnCompletedMoat = IsValidTileId(scope.StartTileId) &&
                (tileFlags[scope.StartTileId] & CompletedMoatTileFlag) != 0;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero)
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

                    EnsureReachabilityMap(scope.UnitId, scope.PlayerId, tileManager, playerApi,
                        scope.StartX, scope.StartY, region);
                    RouteProbeSummary candidate = cachedRouteSummary;
                    bool withMoat = visitedWithMoat[cell] == gridGeneration;
                    bool withoutMoat = visitedWithoutMoat[cell] == gridGeneration;
                    bool topology = (candidate.StartRegion > 0 &&
                        candidate.StartRegion != candidate.TargetRegion) ||
                        (candidate.StartRegion == 0 && startOnCompletedMoat);
                    candidate.AttackProbeEvaluated = true;
                    candidate.ReachedWithMoat = withMoat;
                    candidate.ReachedWithoutMoat = withoutMoat;
                    candidate.RegionTopologyQualified = topology;
                    candidate.RouteFound = withMoat && !withoutMoat && topology &&
                        candidate.FriendlyMoatTiles > 0;
                    observed.MergeObservations(candidate);
                    if (!candidate.RouteFound)
                        continue;
                    approachX = x;
                    approachY = y;
                    summary = candidate;
                    return true;
                }
            }

            summary = observed;
            return false;
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
                        regionTopologyQualified && candidateSummary.FriendlyMoatTiles > 0;
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
                return;
            }

            if (visitedWithoutMoat == null)
            {
                visitedWithoutMoat = new int[MapCellCount];
                visitedWithMoat = new int[MapCellCount];
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
                visitedWithMoat[startCell] = gridGeneration;
            else
                visitedWithoutMoat[startCell] = gridGeneration;

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

                VisitNeighbour(tileManager, playerApi, playerId, x - 1, y, usedMoat, startRegion, targetRegion, ref tail);
                VisitNeighbour(tileManager, playerApi, playerId, x + 1, y, usedMoat, startRegion, targetRegion, ref tail);
                VisitNeighbour(tileManager, playerApi, playerId, x, y - 1, usedMoat, startRegion, targetRegion, ref tail);
                VisitNeighbour(tileManager, playerApi, playerId, x, y + 1, usedMoat, startRegion, targetRegion, ref tail);
            }
        }

        private void VisitNeighbour(
            IntPtr tileManager,
            GamePlayerManagerAPI playerApi,
            int playerId,
            int x,
            int y,
            bool usedMoat,
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
            if (visited[cell] == gridGeneration)
                return;

            visited[cell] = gridGeneration;
            queue[queueTail++] = cell | (nextUsedMoat ? MoatStateBit : 0);
        }

        private bool TryClassifyFriendlyMoat(
            IntPtr tileManager,
            GamePlayerManagerAPI playerApi,
            int tileId,
            int playerId,
            ref RouteProbeSummary summary)
        {
            int moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (moatId <= 0 || moatId >= moatCount)
            {
                summary.InvalidMoatTiles++;
                return false;
            }

            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            int moatOwnerId = moatRecord[MoatOwnerOffset];
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
            cursorChecksArmed = false;
            lastAttackCursorDecision = null;
            lastCursorSelectionDiagnostic = null;
            lastCursorTilePairDiagnostic = null;
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
            trackedMoatMoves.Clear();
            trackedWallClimbs.Clear();
        }

        private void TrackWallClimbCandidate(
            int unitId,
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int targetTileId,
            BuildingCursorTarget wall)
        {
            if (trackedWallClimbs.TryGetValue(unitId, out WallClimbTracker existing) &&
                existing.MapEpoch == mapEpoch && existing.TargetTileId == targetTileId &&
                existing.WallBuildingId == wall.BuildingId)
                return;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive)
                return;

            WallClimbTracker tracker = new WallClimbTracker
            {
                MapEpoch = mapEpoch,
                UnitId = unitId,
                PlayerId = playerId,
                StartX = startX,
                StartY = startY,
                TargetX = targetX,
                TargetY = targetY,
                TargetTileId = targetTileId,
                WallBuildingId = wall.BuildingId,
                WallGlobalId = wall.GlobalId,
                WallType = wall.BuildingType,
                InitialSignature = GetWallUnitSignature(unit)
            };
            trackedWallClimbs[unitId] = tracker;
            Shared.DebugLogHelper.LogInfo(log,
                $"MoveMoat stage=wall-track-start unit={unitId} type={unit->r_UnitChimp} " +
                $"player={playerId} start=({startX},{startY}) target=({targetX},{targetY})/" +
                $"{targetTileId} wall={wall.BuildingId}/{wall.GlobalId}/{wall.BuildingType}.");
        }

        private void ObserveTrackedWallClimbStates(int tick)
        {
            if (trackedWallClimbs.Count == 0)
                return;
            try
            {
                foreach (int unitId in new List<int>(trackedWallClimbs.Keys))
                {
                    if (!trackedWallClimbs.TryGetValue(unitId, out WallClimbTracker tracker))
                        continue;
                    if (tracker.MapEpoch != mapEpoch ||
                        !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                        unit == null || unit->r_AliveState != AliveState.IsAlive)
                    {
                        trackedWallClimbs.Remove(unitId);
                        continue;
                    }

                    string signature = GetWallUnitSignature(unit);
                    if (string.Equals(signature, tracker.LastSignature, StringComparison.Ordinal))
                        continue;
                    tracker.LastSignature = signature;
                    tracker.Activated |= !string.Equals(
                        signature, tracker.InitialSignature, StringComparison.Ordinal);
                    Shared.DebugLogHelper.LogInfo(log,
                        $"MoveMoat stage=wall-state tick={tick} unit={unitId} " +
                        $"aiState={unit->r_AIState} command=" +
                        $"{(TribeAICommand)unit->r_AI_LastIssuedTribeCommand} " +
                        $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                        $"target=({unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}) " +
                        $"target2=({unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2}) " +
                        $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2}) " +
                        $"context=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                        $"speed={unit->r_CurrentSpeed}/{unit->r_CurrentSpeed2} path=" +
                        $"{unit->r_PathPlanRelated1}/{unit->r_PathPlanStateBitFlags}/" +
                        $"{unit->r_MovingRelevant}/{unit->p_CurrentPathPlanPosition}/" +
                        $"{unit->p_PathPlanSize} mode={tracker.ModeObserved} " +
                        $"planner={tracker.PlannerObserved} builder={tracker.BuilderObserved}.");
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("wall-state-tick", ex);
            }
        }

        private static string GetWallUnitSignature(GameUnit* unit) =>
            $"{unit->r_AIState}:{unit->r_AI_LastIssuedTribeCommand}:" +
            $"{unit->r_CurrentTilePositionX}:{unit->r_CurrentTilePositionY}:" +
            $"{unit->r_TargetTilePositionX}:{unit->r_TargetTilePositionY}:" +
            $"{unit->r_TargetTilePositionX2}:{unit->r_TargetTilePositionY2}:" +
            $"{unit->r_NextTilePositionX2}:{unit->r_NextTilePositionY2}:" +
            $"{unit->r_ContextTargetTileX}:{unit->r_ContextTargetTileY}:" +
            $"{unit->r_PathPlanRelated1}:{unit->r_PathPlanStateBitFlags}:" +
            $"{unit->r_MovingRelevant}:{unit->p_CurrentPathPlanPosition}:" +
            $"{unit->p_PathPlanSize}:{unit->r_CurrentSpeed}:{unit->r_CurrentSpeed2}";

        private void MarkTrackedWallPipeline(
            int unitId, string stage, int targetX, int targetY,
            IntPtr pathManager, int? result)
        {
            try
            {
                if (!trackedWallClimbs.TryGetValue(unitId, out WallClimbTracker tracker))
                    return;
                if (stage == "planner")
                    tracker.PlannerObserved = true;
                else if (stage == "mode")
                    tracker.ModeObserved = true;
                Shared.DebugLogHelper.LogInfo(log,
                    $"MoveMoat stage=wall-{stage} unit={unitId} requested=({targetX},{targetY}) " +
                    $"wallTarget=({tracker.TargetX},{tracker.TargetY}) result=" +
                    $"{(result.HasValue ? result.Value.ToString() : "n/a")}.");
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("wall-pipeline-marker", ex);
            }
        }

        private WallClimbTracker TryGetWallTrackerForUnscopedBuilder()
        {
            WallClimbTracker match = null;
            foreach (WallClimbTracker tracker in trackedWallClimbs.Values)
            {
                if (!tracker.Activated || tracker.MapEpoch != mapEpoch ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(
                        tracker.UnitId, out GameUnit* unit) || unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                    continue;
                bool targetMatches =
                    (unit->r_TargetTilePositionX == tracker.TargetX &&
                     unit->r_TargetTilePositionY == tracker.TargetY) ||
                    (unit->r_TargetTilePositionX2 == tracker.TargetX &&
                     unit->r_TargetTilePositionY2 == tracker.TargetY) ||
                    (unit->r_ContextTargetTileX == tracker.TargetX &&
                     unit->r_ContextTargetTileY == tracker.TargetY);
                if (!targetMatches)
                    continue;
                if (match != null)
                    return null;
                match = tracker;
            }
            return match;
        }

        private void LogWallBuilderState(
            string stage, WallClimbTracker tracker, IntPtr pathManager,
            int movementClass, int movementProfile, int? result)
        {
            byte* manager = (byte*)pathManager.ToPointer();
            Shared.DebugLogHelper.LogInfo(log,
                $"MoveMoat stage=wall-builder-{stage} unit={tracker.UnitId} " +
                $"wallTarget=({tracker.TargetX},{tracker.TargetY})/{tracker.TargetTileId} " +
                $"path80={*(int*)(manager + PathManagerRouteVariantOffset)} " +
                $"path84={*(int*)(manager + PathManagerMovementVariantOffset)} " +
                $"path88={*(int*)(manager + PathManagerAssassinModeOffset)} " +
                $"movementClass={movementClass} movementProfile={movementProfile} " +
                $"result={(result.HasValue ? result.Value.ToString() : "n/a")}.");
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

        private static bool IsValidTileId(int tileId) => tileId >= 0 && tileId < 320800;

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
            public string LastSignature { get; set; }
            public bool ModeObserved { get; set; }
            public bool PlannerObserved { get; set; }
            public bool BuilderObserved { get; set; }
            public bool VanillaModeDetected { get; set; }
            public int LastPlannerTargetX { get; set; } = -1;
            public int LastPlannerTargetY { get; set; } = -1;
        }

        private sealed class MoatMoveTracker
        {
            public MoatMoveTracker(
                int mapEpoch,
                int unitId,
                int tribeId,
                int targetX,
                int targetY,
                int builderResult)
            {
                MapEpoch = mapEpoch;
                UnitId = unitId;
                TribeId = tribeId;
                TargetX = targetX;
                TargetY = targetY;
                BuilderResult = builderResult;
            }

            public int MapEpoch { get; }
            public int UnitId { get; }
            public int TribeId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int BuilderResult { get; }
            public string LastSignature { get; set; }
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
                RouteProbeSummary summary, int approachX, int approachY) =>
                new AttackRegionFallbackDecision(
                    true, "required-friendly-moat-route", summary, approachX, approachY);

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
                int firstResultTile)
            {
                Generation = generation;
                Depth = depth;
                QueueHead = queueHead;
                QueueTail = queueTail;
                ResultCount = resultCount;
                FirstResultTile = firstResultTile;
            }

            public int Generation { get; }
            public int Depth { get; }
            public int QueueHead { get; }
            public int QueueTail { get; }
            public int ResultCount { get; }
            public int FirstResultTile { get; }

            public string ToLogFields() =>
                $"generation={Generation} depth={Depth} queue={QueueHead}->{QueueTail} " +
                $"results={ResultCount} firstResultTile={FirstResultTile}";
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
                $"{After.ResultCount}:{After.FirstResultTile}:" +
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
                int tribeId,
                int targetX,
                int targetY)
            {
                TribeId = tribeId;
                TargetX = targetX;
                TargetY = targetY;
            }

            public int TribeId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
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
            public bool AttackMovementQualified { get; set; }
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
        }

        private struct BuildingCursorTarget
        {
            public int BuildingId;
            public uint GlobalId;
            public int OwnerId;
            public eStructs BuildingType;
            public int HoverTileId;
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
                bool hasEligibleSlot,
                bool functionalFallbackArmed,
                CursorPairFallbackKind fallbackKind,
                string rejectionReason,
                int buildingId = 0,
                uint buildingGlobalId = 0,
                eStructs buildingType = eStructs.STRUCT_NULL)
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
                HasEligibleSlot = hasEligibleSlot;
                FunctionalFallbackArmed = functionalFallbackArmed;
                FallbackKind = fallbackKind;
                RejectionReason = rejectionReason;
                BuildingId = buildingId;
                BuildingGlobalId = buildingGlobalId;
                BuildingType = buildingType;
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
            public bool HasEligibleSlot { get; }
            public bool FunctionalFallbackArmed { get; }
            public CursorPairFallbackKind FallbackKind { get; }
            public string RejectionReason { get; }
            public int BuildingId { get; }
            public uint BuildingGlobalId { get; }
            public eStructs BuildingType { get; }
        }

        private sealed class WallClimbTracker
        {
            public int MapEpoch;
            public int UnitId;
            public int PlayerId;
            public int StartX;
            public int StartY;
            public int TargetX;
            public int TargetY;
            public int TargetTileId;
            public int WallBuildingId;
            public uint WallGlobalId;
            public eStructs WallType;
            public string InitialSignature;
            public string LastSignature;
            public bool Activated;
            public bool ModeObserved;
            public bool PlannerObserved;
            public bool BuilderObserved;
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

    }
}
