using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace MoveMoatTest
{
    internal sealed unsafe class MoveMoatPathTest : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int DetectCompletedMoatModeDelegate(IntPtr unitManager, int unitId);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int RegionReachabilityDelegate(
            IntPtr pathManager,
            int movementClass,
            int targetRegion,
            int startX,
            int startY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PathBuilderDelegate(
            IntPtr pathManager,
            int movementClass,
            int movementProfile);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CentralMovementPlanDelegate(
            IntPtr unitManager,
            int unitId,
            int targetX,
            int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int TribeFloodFillMembershipDelegate(
            IntPtr tribeManager,
            int tribeId,
            int floodFillStamp);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int DirectionSeedBuilderDelegate(
            IntPtr pathManager,
            int startX,
            int startY,
            int targetX,
            int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorReachabilityDelegate(
            IntPtr pathManager,
            int nativeUnitIndex,
            int targetX,
            int targetY);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorSpecialModeDelegate(IntPtr selectionState);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CursorRegionPrecheckDelegate(IntPtr pathManager, int nativeUnitIndex);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int CommonPathRequestDelegate(
            IntPtr unitBase,
            int nativeUnitIndex,
            int targetX,
            int targetY,
            int pathOption);

        private const int CentralMovementPlanRva = 0x18E1E0;
        private const int TribeFloodFillMembershipRva = 0x124740;
        private const int TribeMovementPrecheckRva = 0x11B637;
        private const int TribeFormationTargetResultRva = 0x11B919;
        private const int TribeRegionCandidateRetryRva = 0x11B940;
        private const int TribeUnitScanStartRva = 0x11B9D6;
        private const int TribeEarlyReturnRva = 0x11BDF4;
        private const int TribeUnitIterationEndRva = 0x11C14F;
        private const int MovementStepMoatGateRva = 0xDCEF2;
        private const int CursorForbiddenResultRva = 0x8F3DA;
        private const int CursorReachabilityPatternRva = 0x8F3A8;
        private const int CursorReachabilityRva = 0xE9FF0;
        private const int CursorSpecialModeRva = 0x196870;
        private const int CursorRegionPrecheckRva = 0xE9D90;
        private const int CommonPathRequestRva = 0x196280;
        private const int DetectCompletedMoatModeRva = 0x196840;
        private const int RegionReachabilityRva = 0xE7C40;
        private const int PrimaryDirectionSeedBuilderRva = 0xF3060;
        private const int FallbackDirectionSeedBuilderRva = 0xF32B0;
        private const int PathBuilderRva = 0xF4930;
        private const int StandardTileExpanderRva = 0xF09A0;
        private const int MoatAwareTileExpanderRva = 0xF1A80;
        private const int MoatAwareCandidateResultRva = 0xF1C58;
        private const int MoatAwareAllianceComparisonRva = 0xF1C72;
        private const int MoveHereBuilderResultRva = 0x19667E;
        private const int TileFlagsRva = 0x48F71B0;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int CursorTargetXRva = 0x3A11E2C;
        private const int CursorTargetYRva = 0x3A11E30;
        private const int PathRegionGridRva = 0x50EC690;
        private const int DirectionTileOffsetTableRva = 0x405EDB0;
        private const int AllianceGroupTableRva = 0x37EDF3C;
        private const int MoatPathModeRva = 0x60AD6E4;
        private const int AssassinPathContextFlagRva = 0x60AD6E8;
        private const int CursorState548Rva = 0x60AD548;
        private const int CursorState54CRva = 0x60AD54C;
        private const int CursorState550Rva = 0x60AD550;
        private const int CursorState55CRva = 0x60AD55C;
        private const int CursorState560Rva = 0x60AD560;
        private const int NativeUnitManagerRva = 0x67E8400;
        private const int PathStartXRva = 0x60AD668;
        private const int PathStartYRva = 0x60AD66C;
        private const int PathTargetXRva = 0x60AD670;
        private const int PathTargetYRva = 0x60AD674;
        private const int MaximumRegionId = short.MaxValue;
        private const int MaximumFloodFillStamp = 0x7D00;
        private const int MaximumModeLogs = 24;
        private const int MaximumReachabilityLogs = 96;
        private const int MaximumCursorReachabilityLogs = 64;
        private const int MaximumDirectCursorLogs = 128;
        private const int MaximumCursorPollLogs = 128;
        private const int MaximumCursorPrecheckLogs = 128;
        private const int MaximumCursorForbiddenLogs = 128;
        private const int MaximumCommonPathLogs = 128;
        private const int MaximumBuilderLogs = 96;
        private const int MaximumPlanLogs = 96;
        private const int MaximumTrackingLogs = 256;
        private const int MaximumStepGateLogs = 128;
        private const int MaximumCommandLogs = 128;
        private const int MaximumTrackingTicks = 120;
        private const int MovementStepMoatGateHookLength = 18;
        private const int CursorForbiddenResultHookLength = 26;
        private const int MoveHereBuilderResultHookLength = 18;
        private const int TribeMovementPrecheckHookLength = 22;
        private const int TribeFormationTargetResultHookLength = 18;
        private const int TribeRegionCandidateRetryHookLength = 14;
        private const int TribeUnitScanStartHookLength = 20;
        private const int TribeEarlyReturnHookLength = 15;
        private const int TribeUnitIterationEndHookLength = 14;
        private const int StandardTileExpanderHookLength = 16;
        private const int MoatAwareTileExpanderHookLength = 15;
        private const int MoatAwareCandidateResultHookLength = 18;
        private const int MoatAwareAllianceComparisonHookLength = 14;
        private const int UnitPathBufferOffset = 0xB4FE78;
        private const int UnitPathBufferSize = 1000;
        private const int MaximumPackedPathEntries = UnitPathBufferSize * 2;
        private const int UnitMoatMovementMarkerOffset = 0x36C;
        private const uint CompletedMoatTileFlag = 0x40000000;
        private const uint CursorCurrentTileRequiredFlags = 0x10000100;

        private const string CentralMovementPlanPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC 38 04 00 00 " +
            "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 20 04 00 00 4C 63 FA " +
            "4C 8D 35 ?? ?? ?? ?? 49 69 DF 90 04 00 00 49 63 E8 48 03 D9 49 63 F1";

        private const string CursorReachabilityPattern =
            "44 8B 0D ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? " +
            "44 8B 05 ?? ?? ?? ?? 41 8B D6 E8 ?? ?? ?? ?? " +
            "85 C0 74 11 44 8B BC 24 C0 00 00 00";

        private const string CursorForbiddenResultPattern =
            "C7 05 ?? ?? ?? ?? F6 FF FF FF 41 BD 04 00 00 00 " +
            "C7 05 ?? ?? ?? ?? 41 00 00 00";

        private const string CursorReachabilityFunctionPattern =
            "44 89 4C 24 20 44 89 44 24 18 53 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 50 48 63 F2 45 33 ED 33 D2 49 63 E8 49 63 C1 48 8B D9";

        private const string CursorSpecialModePattern =
            "83 B9 BC 05 00 00 00 74 27 33 C0 48 81 C1 64 05 00 00 48 83 F8 16 " +
            "74 05 83 39 00 75 13 48 FF C0 48 83 C1 04 48 83 F8 23 7C E8 B8 01 00 00 00 C3";

        private const string CursorRegionPrecheckPattern =
            "40 53 55 57 41 54 41 56 48 83 EC 20 FF 41 04 48 8B D9 81 79 04 00 7D 00 00 " +
            "41 BC 01 00 00 00 48 63 FA 7E 1F 44 89 61 04";

        private const string CommonPathRequestPattern =
            "48 89 5C 24 20 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 F2 " +
            "45 33 D2 48 69 FE 90 04 00 00 4D 63 F0";

        private const string TribeFloodFillMembershipPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 " +
            "48 83 EC 20 48 63 F2 33 DB 4C 69 CE 88 06 00 00 45 8B F0 48 8B E9";

        private const string PrimaryDirectionSeedBuilderPattern =
            "48 89 5C 24 10 48 89 6C 24 18 56 57 41 55 41 56 41 57 48 83 EC 20 " +
            "4C 63 7C 24 70 4C 8B E9 48 63 DA 49 63 F9 49 63 E8";

        private const string FallbackDirectionSeedBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 30 4C 63 BC 24 80 00 00 00 4C 8B E9 48 63 DA 49 63 F9 49 63 E8";

        private const string StandardTileExpanderPattern =
            "48 89 5C 24 10 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 20 " +
            "FF 81 A0 00 00 00 45 8B E8 49 63 F1";

        private const string MoatAwareTileExpanderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 20 49 63 F8 48 8B D9 49 63 F1 4C 63 E2";

        private const string MoatAwareCandidateResultPattern =
            "85 C0 74 24 48 98 48 03 C0 49 0F BE 8C C1 3C EE F3 01";

        private const string MoatAwareAllianceComparisonPattern =
            "41 39 84 88 3C DF 7E 03 0F 84 82 05 00 00";

        private const string TribeMovementPrecheckPattern =
            "48 8D 04 8D 00 00 00 00 42 F6 84 18 B0 71 8F 04 30 " +
            "48 89 44 24 68";

        private const string TribeFormationTargetResultPattern =
            "44 8B BC 24 C8 00 00 00 48 8B CF 44 8B 05 39 1D F9 05";

        private const string TribeRegionCandidateRetryPattern =
            "44 8B 2D 51 7C 0E 06 48 8D 0D 12 1D F9 05";

        private const string TribeUnitScanStartPattern =
            "66 3B 48 5C 0F 8D AD 01 00 00 44 8B F9 " +
            "8B 94 24 C8 00 00 00";

        private const string TribeEarlyReturnPattern =
            "48 8B 9C 24 C0 00 00 00 48 81 C4 80 00 00 00 " +
            "41 5F 41 5E 41 5D 41 5C 5F 5E 5D C3";

        private const string TribeUnitIterationEndPattern =
            "33 ED 39 2D 85 15 F9 05 89 2D 83 15 F9 05";

        private const string DetectCompletedMoatModePattern =
            "48 63 C2 48 69 D0 90 04 00 00 48 63 84 0A 2C 07 00 00 " +
            "48 8D 0D ?? ?? ?? ?? 8B 04 81 C1 E8 1E 83 E0 01 C3";

        private const string RegionReachabilityPattern =
            "44 89 44 24 18 89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 38 45 33 D2 49 63 F9 4C 89 51 48 48 8B D9";

        private const string PathBuilderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 " +
            "48 63 41 0C 48 8B D9 41 8B F0 44 8B D2";

        private readonly ManualLogSource log;
        private readonly int* moatPathMode;
        private readonly int* assassinPathContextFlag;
        private readonly int* cursorTargetX;
        private readonly int* cursorTargetY;
        private readonly int* cursorState548;
        private readonly int* cursorState54C;
        private readonly int* cursorState550;
        private readonly int* cursorState55C;
        private readonly int* cursorState560;
        private readonly byte* nativeUnitManager;
        private readonly int* pathStartX;
        private readonly int* pathStartY;
        private readonly int* pathTargetX;
        private readonly int* pathTargetY;
        private readonly uint* tileFlags;
        private readonly byte* movementTargetAvailability;
        private readonly short* pathRegionGrid;
        private readonly int* directionTileOffsets;
        private readonly int* allianceGroupTable;
        private readonly object trackingLock = new object();
        private readonly Dictionary<int, TrackedPlan> trackedPlans = new Dictionary<int, TrackedPlan>();
        [ThreadStatic]
        private static PlanAttempt activePlanAttempt;
        [ThreadStatic]
        private static PlanAttempt pendingMoveHereAttempt;
        [ThreadStatic]
        private static CommandAttempt activeCommandAttempt;
        private CentralMovementPlanDelegate originalCentralMovementPlan;
        private CentralMovementPlanDelegate rootedCentralMovementPlan;
        private TribeFloodFillMembershipDelegate originalTribeFloodFillMembership;
        private TribeFloodFillMembershipDelegate rootedTribeFloodFillMembership;
        private DirectionSeedBuilderDelegate originalPrimaryDirectionSeedBuilder;
        private DirectionSeedBuilderDelegate originalFallbackDirectionSeedBuilder;
        private CursorReachabilityDelegate originalCursorReachability;
        private CursorReachabilityDelegate rootedCursorReachability;
        private CursorSpecialModeDelegate originalCursorSpecialMode;
        private CursorSpecialModeDelegate rootedCursorSpecialMode;
        private CursorRegionPrecheckDelegate originalCursorRegionPrecheck;
        private CursorRegionPrecheckDelegate rootedCursorRegionPrecheck;
        private CommonPathRequestDelegate originalCommonPathRequest;
        private CommonPathRequestDelegate rootedCommonPathRequest;
        private DetectCompletedMoatModeDelegate originalDetectCompletedMoatMode;
        private DetectCompletedMoatModeDelegate rootedDetectCompletedMoatMode;
        private RegionReachabilityDelegate originalRegionReachability;
        private RegionReachabilityDelegate rootedRegionReachability;
        private PathBuilderDelegate originalPathBuilder;
        private PathBuilderDelegate rootedPathBuilder;
        private NativeDetour detectCompletedMoatModeDetour;
        private NativeDetour regionReachabilityDetour;
        private NativeDetour pathBuilderDetour;
        private NativeDetour centralMovementPlanDetour;
        private NativeDetour tribeFloodFillMembershipDetour;
        private NativeDetour primaryDirectionSeedBuilderDetour;
        private NativeDetour fallbackDirectionSeedBuilderDetour;
        private NativeDetour cursorReachabilityDetour;
        private NativeDetour cursorSpecialModeDetour;
        private NativeDetour cursorRegionPrecheckDetour;
        private NativeDetour commonPathRequestDetour;
        private HookRef<X64InlineHook> movementStepMoatGateHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> cursorForbiddenResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moveHereBuilderResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeMovementPrecheckHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeFormationTargetResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeRegionCandidateRetryHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeUnitScanStartHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeEarlyReturnHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeUnitIterationEndHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> standardTileExpanderHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatAwareTileExpanderHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatAwareCandidateResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatAwareAllianceComparisonHook = new HookRef<X64InlineHook>();
        private IDisposable tribeMoveSubscription;
        private IDisposable unitMoveSubscription;
        private IDisposable mapLoadSubscription;
        private IDisposable mapStartSubscription;
        private IDisposable mapUnloadSubscription;
        private long nextPlanId;
        private long nextCommandId;
        private int mapEpoch;
        private int modeLogCount;
        private int reachabilityLogCount;
        private int cursorReachabilityLogCount;
        private int directCursorLogCount;
        private int cursorPollLogCount;
        private int cursorPrecheckLogCount;
        private int cursorForbiddenLogCount;
        private int commonPathLogCount;
        private int builderLogCount;
        private int planLogCount;
        private int trackingLogCount;
        private int stepGateLogCount;
        private int commandLogCount;
        private bool modeLogLimitReported;
        private bool reachabilityLogLimitReported;
        private bool cursorReachabilityLogLimitReported;
        private bool directCursorLogLimitReported;
        private bool cursorPollLogLimitReported;
        private bool cursorPrecheckLogLimitReported;
        private bool cursorForbiddenLogLimitReported;
        private bool commonPathLogLimitReported;
        private bool hasLastCursorReachabilityState;
        private int lastCursorMovementClass;
        private int lastCursorStartX;
        private int lastCursorStartY;
        private int lastCursorTargetX;
        private int lastCursorTargetY;
        private int lastCursorTargetRegion;
        private int lastCursorVanillaResult;
        private int lastCursorEffectiveResult;
        private int lastCursorPathMode;
        private bool hasLastDirectCursorState;
        private int lastDirectCursorNativeUnitIndex;
        private int lastDirectCursorTargetX;
        private int lastDirectCursorTargetY;
        private int lastDirectCursorResult;
        private int lastDirectCursorPathMode;
        private int lastDirectCursorAssassinContext;
        private long directCursorCallSerial;
        private bool hasLastCursorPollState;
        private int lastCursorPollFrame = -1;
        private int lastCursorPollTargetX;
        private int lastCursorPollTargetY;
        private int lastCursorPollState548;
        private int lastCursorPollState54C;
        private int lastCursorPollState550;
        private int lastCursorPollState55C;
        private int lastCursorPollState560;
        private int lastCursorPollPathMode;
        private int lastCursorPollAssassinContext;
        private bool cursorPollArmed;
        private bool hasLastCursorForbiddenState;
        private int lastCursorForbiddenNativeUnitIndex;
        private int lastCursorForbiddenTargetX;
        private int lastCursorForbiddenTargetY;
        private int lastCursorForbiddenAvailabilityGate;
        private int lastCursorForbiddenCurrentTileId;
        private string lastCursorForbiddenReason;
        private long cursorEvaluationSerial;
        private long cursorRegionEvaluationSerial;
        private long cursorDirectEvaluationSerial;
        private bool hasLastCursorSpecialModeState;
        private int lastCursorSpecialModeTargetX;
        private int lastCursorSpecialModeTargetY;
        private int lastCursorSpecialModeResult;
        private int lastCursorSpecialModeGate;
        private int lastCursorSpecialModeOccupiedSlots;
        private bool hasLastCursorRegionPrecheckState;
        private int lastCursorRegionPrecheckNativeUnitIndex;
        private int lastCursorRegionPrecheckTargetX;
        private int lastCursorRegionPrecheckTargetY;
        private int lastCursorRegionPrecheckResult;
        private bool hasLastCommonPathState;
        private int lastCommonPathNativeUnitIndex;
        private int lastCommonPathTargetX;
        private int lastCommonPathTargetY;
        private int lastCommonPathOption;
        private int lastCommonPathResult;
        private int lastCommonPathContextBefore;
        private int lastCommonPathContextAfter;
        private bool builderLogLimitReported;
        private bool planLogLimitReported;
        private bool trackingLogLimitReported;
        private bool stepGateLogLimitReported;
        private bool commandLogLimitReported;
        private bool tickSubscribed;
        private volatile bool mapRuntimeActive;
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
                    "The central moat-path test requires the validated CrusaderDE.dll layout.");
            }

            Shared.NativeResolution planResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CentralMovementPlanPattern,
                CentralMovementPlanRva,
                referenceHashMatches,
                "central ordinary-movement planner",
                log: null);
            Shared.NativeResolution cursorResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CursorReachabilityPattern,
                CursorReachabilityPatternRva,
                referenceHashMatches,
                "ordinary-movement cursor reachability caller",
                log: null);
            Shared.NativeResolution cursorForbiddenResultResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CursorForbiddenResultPattern,
                CursorForbiddenResultRva,
                referenceHashMatches,
                "ordinary-movement forbidden-cursor result block",
                log: null);
            Shared.NativeResolution cursorFunctionResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CursorReachabilityFunctionPattern,
                CursorReachabilityRva,
                referenceHashMatches,
                "ordinary-movement cursor reachability function",
                log: null);
            Shared.NativeResolution cursorSpecialModeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CursorSpecialModePattern,
                CursorSpecialModeRva,
                referenceHashMatches,
                "ordinary-movement cursor special-mode precheck",
                log: null);
            Shared.NativeResolution cursorRegionPrecheckResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CursorRegionPrecheckPattern,
                CursorRegionPrecheckRva,
                referenceHashMatches,
                "ordinary-movement cursor region precheck",
                log: null);
            Shared.NativeResolution commonPathRequestResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CommonPathRequestPattern,
                CommonPathRequestRva,
                referenceHashMatches,
                "shared common path request",
                log: null);
            Shared.NativeResolution modeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DetectCompletedMoatModePattern,
                DetectCompletedMoatModeRva,
                referenceHashMatches,
                "completed-moat path-mode detector",
                log: null);
            Shared.NativeResolution tribeFloodFillMembershipResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TribeFloodFillMembershipPattern,
                TribeFloodFillMembershipRva,
                referenceHashMatches,
                "Tribe flood-fill membership helper",
                log: null);
            Shared.NativeResolution tribePrecheckResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TribeMovementPrecheckPattern,
                TribeMovementPrecheckRva,
                referenceHashMatches,
                "Tribe MoveHere target and region precheck",
                log: null);
            Shared.NativeResolution tribeFormationResultResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TribeFormationTargetResultPattern,
                TribeFormationTargetResultRva,
                referenceHashMatches,
                "Tribe formation-target helper result",
                log: null);
            Shared.NativeResolution tribeRegionCandidateRetryResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TribeRegionCandidateRetryPattern,
                TribeRegionCandidateRetryRva,
                referenceHashMatches,
                "Tribe region-candidate retry",
                log: null);
            Shared.NativeResolution tribeUnitScanResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TribeUnitScanStartPattern,
                TribeUnitScanStartRva,
                referenceHashMatches,
                "Tribe unit-scan entry",
                log: null);
            Shared.NativeResolution tribeEarlyReturnResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TribeEarlyReturnPattern,
                TribeEarlyReturnRva,
                referenceHashMatches,
                "Tribe MoveHere central return",
                log: null);
            Shared.NativeResolution tribeUnitIterationEndResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TribeUnitIterationEndPattern,
                TribeUnitIterationEndRva,
                referenceHashMatches,
                "Tribe unit-iteration end",
                log: null);
            Shared.NativeResolution reachabilityResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                RegionReachabilityPattern,
                RegionReachabilityRva,
                referenceHashMatches,
                "moat-aware region reachability",
                log: null);
            Shared.NativeResolution primaryDirectionSeedResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                PrimaryDirectionSeedBuilderPattern,
                PrimaryDirectionSeedBuilderRva,
                referenceHashMatches,
                "primary direction-seed builder",
                log: null);
            Shared.NativeResolution fallbackDirectionSeedResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                FallbackDirectionSeedBuilderPattern,
                FallbackDirectionSeedBuilderRva,
                referenceHashMatches,
                "fallback direction-seed builder",
                log: null);
            Shared.NativeResolution builderResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                PathBuilderPattern,
                PathBuilderRva,
                referenceHashMatches,
                "central tile path builder",
                log: null);
            Shared.NativeResolution standardTileExpanderResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StandardTileExpanderPattern,
                StandardTileExpanderRva,
                referenceHashMatches,
                "standard tile expander",
                log: null);
            Shared.NativeResolution moatAwareTileExpanderResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                MoatAwareTileExpanderPattern,
                MoatAwareTileExpanderRva,
                referenceHashMatches,
                "moat-aware tile expander",
                log: null);
            Shared.NativeResolution moatAwareCandidateResultResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                MoatAwareCandidateResultPattern,
                MoatAwareCandidateResultRva,
                referenceHashMatches,
                "moat-aware completed-moat candidate result",
                log: null);
            Shared.NativeResolution moatAwareAllianceComparisonResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                MoatAwareAllianceComparisonPattern,
                MoatAwareAllianceComparisonRva,
                referenceHashMatches,
                "moat-aware alliance comparison",
                log: null);

            RequireValidatedRva(planResolution, CentralMovementPlanRva, "central ordinary-movement planner");
            RequireValidatedRva(
                cursorResolution,
                CursorReachabilityPatternRva,
                "ordinary-movement cursor reachability caller");
            RequireValidatedRva(
                cursorForbiddenResultResolution,
                CursorForbiddenResultRva,
                "ordinary-movement forbidden-cursor result block");
            RequireValidatedRva(
                cursorFunctionResolution,
                CursorReachabilityRva,
                "ordinary-movement cursor reachability function");
            RequireValidatedRva(
                cursorSpecialModeResolution,
                CursorSpecialModeRva,
                "ordinary-movement cursor special-mode precheck");
            RequireValidatedRva(
                cursorRegionPrecheckResolution,
                CursorRegionPrecheckRva,
                "ordinary-movement cursor region precheck");
            RequireValidatedRva(
                commonPathRequestResolution,
                CommonPathRequestRva,
                "shared common path request");
            RequireValidatedRva(
                tribeFloodFillMembershipResolution,
                TribeFloodFillMembershipRva,
                "Tribe flood-fill membership helper");
            RequireValidatedRva(
                tribePrecheckResolution,
                TribeMovementPrecheckRva,
                "Tribe MoveHere target and region precheck");
            RequireValidatedRva(
                tribeFormationResultResolution,
                TribeFormationTargetResultRva,
                "Tribe formation-target helper result");
            RequireValidatedRva(
                tribeRegionCandidateRetryResolution,
                TribeRegionCandidateRetryRva,
                "Tribe region-candidate retry");
            RequireValidatedRva(
                tribeUnitScanResolution,
                TribeUnitScanStartRva,
                "Tribe unit-scan entry");
            RequireValidatedRva(
                tribeEarlyReturnResolution,
                TribeEarlyReturnRva,
                "Tribe MoveHere central return");
            RequireValidatedRva(
                tribeUnitIterationEndResolution,
                TribeUnitIterationEndRva,
                "Tribe unit-iteration end");
            RequireValidatedRva(modeResolution, DetectCompletedMoatModeRva, "completed-moat path-mode detector");
            RequireValidatedRva(reachabilityResolution, RegionReachabilityRva, "moat-aware region reachability");
            RequireValidatedRva(
                primaryDirectionSeedResolution,
                PrimaryDirectionSeedBuilderRva,
                "primary direction-seed builder");
            RequireValidatedRva(
                fallbackDirectionSeedResolution,
                FallbackDirectionSeedBuilderRva,
                "fallback direction-seed builder");
            RequireValidatedRva(builderResolution, PathBuilderRva, "central tile path builder");
            RequireValidatedRva(
                standardTileExpanderResolution,
                StandardTileExpanderRva,
                "standard tile expander");
            RequireValidatedRva(
                moatAwareTileExpanderResolution,
                MoatAwareTileExpanderRva,
                "moat-aware tile expander");
            RequireValidatedRva(
                moatAwareCandidateResultResolution,
                MoatAwareCandidateResultRva,
                "moat-aware completed-moat candidate result");
            RequireValidatedRva(
                moatAwareAllianceComparisonResolution,
                MoatAwareAllianceComparisonRva,
                "moat-aware alliance comparison");
            ValidatePatternSpans(memory);
            ValidateInlineHookSpans(memory);

            int cursorTargetYRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 3,
                cursorResolution.Rva + 7,
                "cursor target Y");
            int cursorTargetXRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 17,
                cursorResolution.Rva + 21,
                "cursor target X");
            int cursorFunctionTargetRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                cursorResolution.Rva + 25,
                cursorResolution.Rva + 29);
            if (cursorTargetXRva != CursorTargetXRva || cursorTargetYRva != CursorTargetYRva)
            {
                throw new InvalidOperationException(
                    "The ordinary-movement cursor target globals did not match their validated RVAs.");
            }
            if (cursorFunctionTargetRva != cursorFunctionResolution.Rva)
            {
                throw new InvalidOperationException(
                    "The ordinary-movement cursor caller no longer targets the validated reachability function.");
            }

            moatPathMode = (int*)(libraryBase + MoatPathModeRva);
            assassinPathContextFlag = (int*)(libraryBase + AssassinPathContextFlagRva);
            cursorTargetX = (int*)(libraryBase + unchecked((ulong)cursorTargetXRva));
            cursorTargetY = (int*)(libraryBase + unchecked((ulong)cursorTargetYRva));
            cursorState548 = (int*)(libraryBase + CursorState548Rva);
            cursorState54C = (int*)(libraryBase + CursorState54CRva);
            cursorState550 = (int*)(libraryBase + CursorState550Rva);
            cursorState55C = (int*)(libraryBase + CursorState55CRva);
            cursorState560 = (int*)(libraryBase + CursorState560Rva);
            nativeUnitManager = (byte*)(libraryBase + NativeUnitManagerRva);
            pathStartX = (int*)(libraryBase + PathStartXRva);
            pathStartY = (int*)(libraryBase + PathStartYRva);
            pathTargetX = (int*)(libraryBase + PathTargetXRva);
            pathTargetY = (int*)(libraryBase + PathTargetYRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);
            movementTargetAvailability = (byte*)(libraryBase + MovementTargetAvailabilityRva);
            pathRegionGrid = (short*)(libraryBase + PathRegionGridRva);
            directionTileOffsets = (int*)(libraryBase + DirectionTileOffsetTableRva);
            allianceGroupTable = (int*)(libraryBase + AllianceGroupTableRva);

            rootedCentralMovementPlan = ObserveCentralMovementPlan;
            rootedTribeFloodFillMembership = AllowTribeFloodFillForMoveOrder;
            rootedDetectCompletedMoatMode = ForceCompletedMoatMode;
            rootedRegionReachability = AllowBuilderAfterFailedRegionSearch;
            rootedPathBuilder = ObservePathBuilder;
            rootedCursorReachability = ObserveCursorReachability;
            rootedCursorSpecialMode = ObserveCursorSpecialMode;
            rootedCursorRegionPrecheck = ObserveCursorRegionPrecheck;
            rootedCommonPathRequest = ObserveCommonPathRequest;

            NativeDetour pendingModeDetour = null;
            NativeDetour pendingReachabilityDetour = null;
            NativeDetour pendingBuilderDetour = null;
            NativeDetour pendingPlanDetour = null;
            NativeDetour pendingTribeFloodFillMembershipDetour = null;
            NativeDetour pendingCursorReachabilityDetour = null;
            NativeDetour pendingCursorSpecialModeDetour = null;
            NativeDetour pendingCursorRegionPrecheckDetour = null;
            NativeDetour pendingCommonPathRequestDetour = null;
            bool planApplied = false;
            bool tribeFloodFillMembershipApplied = false;
            bool modeApplied = false;
            bool reachabilityApplied = false;
            bool builderApplied = false;
            bool cursorReachabilityApplied = false;
            bool cursorSpecialModeApplied = false;
            bool cursorRegionPrecheckApplied = false;
            bool commonPathRequestApplied = false;
            try
            {
                pendingPlanDetour = CreateDetour(
                    libraryBase + unchecked((ulong)planResolution.Rva),
                    rootedCentralMovementPlan);
                originalCentralMovementPlan =
                    pendingPlanDetour.GenerateTrampoline<CentralMovementPlanDelegate>();

                pendingTribeFloodFillMembershipDetour = CreateDetour(
                    libraryBase + unchecked((ulong)tribeFloodFillMembershipResolution.Rva),
                    rootedTribeFloodFillMembership);
                originalTribeFloodFillMembership =
                    pendingTribeFloodFillMembershipDetour.GenerateTrampoline<TribeFloodFillMembershipDelegate>();

                pendingModeDetour = CreateDetour(
                    libraryBase + unchecked((ulong)modeResolution.Rva),
                    rootedDetectCompletedMoatMode);
                originalDetectCompletedMoatMode =
                    pendingModeDetour.GenerateTrampoline<DetectCompletedMoatModeDelegate>();

                pendingReachabilityDetour = CreateDetour(
                    libraryBase + unchecked((ulong)reachabilityResolution.Rva),
                    rootedRegionReachability);
                originalRegionReachability =
                    pendingReachabilityDetour.GenerateTrampoline<RegionReachabilityDelegate>();

                pendingBuilderDetour = CreateDetour(
                    libraryBase + unchecked((ulong)builderResolution.Rva),
                    rootedPathBuilder);
                originalPathBuilder = pendingBuilderDetour.GenerateTrampoline<PathBuilderDelegate>();

                pendingCursorReachabilityDetour = CreateDetour(
                    libraryBase + unchecked((ulong)cursorFunctionResolution.Rva),
                    rootedCursorReachability);
                originalCursorReachability =
                    pendingCursorReachabilityDetour.GenerateTrampoline<CursorReachabilityDelegate>();

                pendingCursorSpecialModeDetour = CreateDetour(
                    libraryBase + unchecked((ulong)cursorSpecialModeResolution.Rva),
                    rootedCursorSpecialMode);
                originalCursorSpecialMode =
                    pendingCursorSpecialModeDetour.GenerateTrampoline<CursorSpecialModeDelegate>();

                pendingCursorRegionPrecheckDetour = CreateDetour(
                    libraryBase + unchecked((ulong)cursorRegionPrecheckResolution.Rva),
                    rootedCursorRegionPrecheck);
                originalCursorRegionPrecheck =
                    pendingCursorRegionPrecheckDetour.GenerateTrampoline<CursorRegionPrecheckDelegate>();

                pendingCommonPathRequestDetour = CreateDetour(
                    libraryBase + unchecked((ulong)commonPathRequestResolution.Rva),
                    rootedCommonPathRequest);
                originalCommonPathRequest =
                    pendingCommonPathRequestDetour.GenerateTrampoline<CommonPathRequestDelegate>();

                pendingPlanDetour.Apply();
                planApplied = true;
                pendingTribeFloodFillMembershipDetour.Apply();
                tribeFloodFillMembershipApplied = true;
                pendingModeDetour.Apply();
                modeApplied = true;
                pendingReachabilityDetour.Apply();
                reachabilityApplied = true;
                pendingBuilderDetour.Apply();
                builderApplied = true;
                pendingCursorReachabilityDetour.Apply();
                cursorReachabilityApplied = true;
                pendingCursorSpecialModeDetour.Apply();
                cursorSpecialModeApplied = true;
                pendingCursorRegionPrecheckDetour.Apply();
                cursorRegionPrecheckApplied = true;
                pendingCommonPathRequestDetour.Apply();
                commonPathRequestApplied = true;

                detectCompletedMoatModeDetour = pendingModeDetour;
                regionReachabilityDetour = pendingReachabilityDetour;
                pathBuilderDetour = pendingBuilderDetour;
                centralMovementPlanDetour = pendingPlanDetour;
                tribeFloodFillMembershipDetour = pendingTribeFloodFillMembershipDetour;
                cursorReachabilityDetour = pendingCursorReachabilityDetour;
                cursorSpecialModeDetour = pendingCursorSpecialModeDetour;
                cursorRegionPrecheckDetour = pendingCursorRegionPrecheckDetour;
                commonPathRequestDetour = pendingCommonPathRequestDetour;

                HookTransaction transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref movementStepMoatGateHook,
                    libraryBase + MovementStepMoatGateRva,
                    EnableCompletedMoatMovementStep,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MovementStepMoatGateHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref cursorForbiddenResultHook,
                    libraryBase + CursorForbiddenResultRva,
                    ObserveCursorForbiddenResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: CursorForbiddenResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();
                if (!movementStepMoatGateHook.Success)
                    throw new InvalidOperationException("The completed-moat movement-step hook did not install.");
                if (!cursorForbiddenResultHook.Success)
                    throw new InvalidOperationException("The forbidden-cursor result hook did not install.");

                Application.onBeforeRender += ObserveCursorFrame;

                tribeMoveSubscription = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                    .Subscribe(ObserveTribeMoveOrder);
                unitMoveSubscription = UnitR3EventHooks.OnUnitMoveHere.Observable
                    .Subscribe(ObserveUnitMoveOrder);
                mapLoadSubscription = MapLoaderR3EventHooks.OnLoadMap.Observable
                    .Subscribe(args => ObserveMapLifecycle("load", args.Phase));
                mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
                    .Subscribe(args => ObserveMapLifecycle("start", args.Phase));
                mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Subscribe(args => ObserveMapLifecycle("unload", args.Phase));

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Move Moat Test installed: planRva=0x{planResolution.Rva:X}/method={planResolution.Method}, " +
                    $"modeRva=0x{modeResolution.Rva:X}/method={modeResolution.Method}, " +
                    $"tribeFloodFillMembershipRva=0x{tribeFloodFillMembershipResolution.Rva:X}/method=" +
                    $"{tribeFloodFillMembershipResolution.Method}, " +
                    $"reachabilityRva=0x{reachabilityResolution.Rva:X}/method={reachabilityResolution.Method}, " +
                    $"cursorFunctionRva=0x{cursorFunctionResolution.Rva:X}/method=" +
                    $"{cursorFunctionResolution.Method}, " +
                    $"cursorPrechecks=0x{cursorSpecialModeResolution.Rva:X}/" +
                    $"0x{cursorRegionPrecheckResolution.Rva:X}, " +
                    $"cursorForbiddenRva=0x{cursorForbiddenResultResolution.Rva:X}, " +
                    $"commonPathRva=0x{commonPathRequestResolution.Rva:X}/method=" +
                    $"{commonPathRequestResolution.Method}, " +
                    $"builderRva=0x{builderResolution.Rva:X}/method={builderResolution.Method}, " +
                    $"validatedDirectionSeeds=0x{primaryDirectionSeedResolution.Rva:X}/" +
                    $"0x{fallbackDirectionSeedResolution.Rva:X}, " +
                    $"tileExpanders=0x{standardTileExpanderResolution.Rva:X}/" +
                    $"0x{moatAwareTileExpanderResolution.Rva:X}, " +
                    $"tribePrecheckRva=0x{tribePrecheckResolution.Rva:X}/method={tribePrecheckResolution.Method}, " +
                    "legacyDiagnosticInlineHooks=disabled-for-runtime-safety, " +
                    "cursorForbiddenObserver=read-only, " +
                    $"stepGateRva=0x{MovementStepMoatGateRva:X}; " +
                    "allCompletedMoats=true, ownerFiltering=false, " +
                    "tribeFloodFillBypass=activeMoveHereOnly, realBuilderResultUnchanged=true.");
            }
            catch
            {
                if (tickSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick -= ObserveTrackedUnits;
                    tickSubscribed = false;
                }
                moveHereBuilderResultHook?.Value?.Dispose();
                Application.onBeforeRender -= ObserveCursorFrame;
                cursorForbiddenResultHook?.Value?.Dispose();
                movementStepMoatGateHook?.Value?.Dispose();
                tribeMovementPrecheckHook?.Value?.Dispose();
                tribeFormationTargetResultHook?.Value?.Dispose();
                tribeRegionCandidateRetryHook?.Value?.Dispose();
                tribeUnitScanStartHook?.Value?.Dispose();
                tribeUnitIterationEndHook?.Value?.Dispose();
                tribeEarlyReturnHook?.Value?.Dispose();
                standardTileExpanderHook?.Value?.Dispose();
                moatAwareTileExpanderHook?.Value?.Dispose();
                moatAwareCandidateResultHook?.Value?.Dispose();
                moatAwareAllianceComparisonHook?.Value?.Dispose();
                tribeMoveSubscription?.Dispose();
                unitMoveSubscription?.Dispose();
                mapLoadSubscription?.Dispose();
                mapStartSubscription?.Dispose();
                mapUnloadSubscription?.Dispose();
                tribeMoveSubscription = null;
                unitMoveSubscription = null;
                mapLoadSubscription = null;
                mapStartSubscription = null;
                mapUnloadSubscription = null;
                if (commonPathRequestApplied)
                    pendingCommonPathRequestDetour?.Undo();
                pendingCommonPathRequestDetour?.Dispose();
                if (cursorRegionPrecheckApplied)
                    pendingCursorRegionPrecheckDetour?.Undo();
                pendingCursorRegionPrecheckDetour?.Dispose();
                if (cursorSpecialModeApplied)
                    pendingCursorSpecialModeDetour?.Undo();
                pendingCursorSpecialModeDetour?.Dispose();
                if (cursorReachabilityApplied)
                    pendingCursorReachabilityDetour?.Undo();
                pendingCursorReachabilityDetour?.Dispose();
                if (builderApplied)
                    pendingBuilderDetour?.Undo();
                pendingBuilderDetour?.Dispose();
                if (reachabilityApplied)
                    pendingReachabilityDetour?.Undo();
                pendingReachabilityDetour?.Dispose();
                if (modeApplied)
                    pendingModeDetour?.Undo();
                pendingModeDetour?.Dispose();
                if (tribeFloodFillMembershipApplied)
                    pendingTribeFloodFillMembershipDetour?.Undo();
                pendingTribeFloodFillMembershipDetour?.Dispose();
                if (planApplied)
                    pendingPlanDetour?.Undo();
                pendingPlanDetour?.Dispose();
                originalCentralMovementPlan = null;
                originalTribeFloodFillMembership = null;
                originalPrimaryDirectionSeedBuilder = null;
                originalFallbackDirectionSeedBuilder = null;
                originalDetectCompletedMoatMode = null;
                originalRegionReachability = null;
                originalPathBuilder = null;
                originalCursorReachability = null;
                originalCursorSpecialMode = null;
                originalCursorRegionPrecheck = null;
                originalCommonPathRequest = null;
                rootedCentralMovementPlan = null;
                rootedTribeFloodFillMembership = null;
                rootedDetectCompletedMoatMode = null;
                rootedRegionReachability = null;
                rootedPathBuilder = null;
                rootedCursorReachability = null;
                rootedCursorSpecialMode = null;
                rootedCursorRegionPrecheck = null;
                rootedCommonPathRequest = null;
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            if (tickSubscribed)
            {
                GameTimeManagerAPI.Instance.OnTick -= ObserveTrackedUnits;
                tickSubscribed = false;
            }
            moveHereBuilderResultHook?.Value?.Dispose();
            Application.onBeforeRender -= ObserveCursorFrame;
            cursorForbiddenResultHook?.Value?.Dispose();
            movementStepMoatGateHook?.Value?.Dispose();
            tribeMovementPrecheckHook?.Value?.Dispose();
            tribeFormationTargetResultHook?.Value?.Dispose();
            tribeRegionCandidateRetryHook?.Value?.Dispose();
            tribeUnitScanStartHook?.Value?.Dispose();
            tribeUnitIterationEndHook?.Value?.Dispose();
            tribeEarlyReturnHook?.Value?.Dispose();
            standardTileExpanderHook?.Value?.Dispose();
            moatAwareTileExpanderHook?.Value?.Dispose();
            moatAwareCandidateResultHook?.Value?.Dispose();
            moatAwareAllianceComparisonHook?.Value?.Dispose();
            tribeMoveSubscription?.Dispose();
            unitMoveSubscription?.Dispose();
            mapLoadSubscription?.Dispose();
            mapStartSubscription?.Dispose();
            mapUnloadSubscription?.Dispose();
            tribeMoveSubscription = null;
            unitMoveSubscription = null;
            mapLoadSubscription = null;
            mapStartSubscription = null;
            mapUnloadSubscription = null;
            commonPathRequestDetour?.Dispose();
            cursorRegionPrecheckDetour?.Dispose();
            cursorSpecialModeDetour?.Dispose();
            cursorReachabilityDetour?.Dispose();
            pathBuilderDetour?.Dispose();
            fallbackDirectionSeedBuilderDetour?.Dispose();
            primaryDirectionSeedBuilderDetour?.Dispose();
            regionReachabilityDetour?.Dispose();
            detectCompletedMoatModeDetour?.Dispose();
            tribeFloodFillMembershipDetour?.Dispose();
            centralMovementPlanDetour?.Dispose();
            commonPathRequestDetour = null;
            cursorRegionPrecheckDetour = null;
            cursorSpecialModeDetour = null;
            cursorReachabilityDetour = null;
            pathBuilderDetour = null;
            fallbackDirectionSeedBuilderDetour = null;
            primaryDirectionSeedBuilderDetour = null;
            regionReachabilityDetour = null;
            detectCompletedMoatModeDetour = null;
            tribeFloodFillMembershipDetour = null;
            centralMovementPlanDetour = null;
            originalCentralMovementPlan = null;
            originalTribeFloodFillMembership = null;
            originalPrimaryDirectionSeedBuilder = null;
            originalFallbackDirectionSeedBuilder = null;
            originalPathBuilder = null;
            originalRegionReachability = null;
            originalDetectCompletedMoatMode = null;
            originalCursorReachability = null;
            originalCursorSpecialMode = null;
            originalCursorRegionPrecheck = null;
            originalCommonPathRequest = null;
            rootedCentralMovementPlan = null;
            rootedTribeFloodFillMembership = null;
            rootedPathBuilder = null;
            rootedRegionReachability = null;
            rootedDetectCompletedMoatMode = null;
            rootedCursorReachability = null;
            rootedCursorSpecialMode = null;
            rootedCursorRegionPrecheck = null;
            rootedCommonPathRequest = null;
            lock (trackingLock)
                trackedPlans.Clear();
        }

        private void ObserveMapLifecycle(string eventName, EventHookPhase phase)
        {
            if (disposed)
                return;

            // Unit IDs and their native path buffers are recycled between maps. Never let
            // diagnostic state from the previous map dereference a newly initialized unit.
            bool activate = eventName != "unload" && phase == EventHookPhase.Post;
            int clearedPlans;
            int epoch;
            lock (trackingLock)
            {
                mapRuntimeActive = false;
                clearedPlans = trackedPlans.Count;
                trackedPlans.Clear();
                epoch = ++mapEpoch;
                if (activate)
                    mapRuntimeActive = true;
            }

            activePlanAttempt = null;
            pendingMoveHereAttempt = null;
            activeCommandAttempt = null;
            hasLastCursorReachabilityState = false;
            hasLastDirectCursorState = false;
            hasLastCursorPollState = false;
            cursorPollArmed = false;
            hasLastCursorSpecialModeState = false;
            hasLastCursorRegionPrecheckState = false;
            hasLastCursorForbiddenState = false;
            hasLastCommonPathState = false;
            lastCursorPollFrame = -1;
            cursorPollLogCount = 0;
            cursorPollLogLimitReported = false;
            cursorPrecheckLogCount = 0;
            cursorPrecheckLogLimitReported = false;
            cursorForbiddenLogCount = 0;
            cursorForbiddenLogLimitReported = false;
            cursorEvaluationSerial = 0;
            cursorRegionEvaluationSerial = -1;
            cursorDirectEvaluationSerial = -1;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat map lifecycle: event={eventName}, phase={phase}, epoch={epoch}, " +
                $"runtimeActive={activate}, clearedTrackedPlans={clearedPlans}.");
        }

        private void ObserveTribeMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (disposed || !mapRuntimeActive)
                return;

            try
            {
                if (args.Phase == EventHookPhase.Pre)
                {
                    CommandAttempt attempt = new CommandAttempt(
                        ++nextCommandId,
                        args.TribeId,
                        args.TileX,
                        args.TileY);
                    activeCommandAttempt = attempt;
                    TileDiagnostic target = GetTileDiagnostic(args.TileX, args.TileY);
                    int availability = GetMovementAvailability(args.TileX, args.TileY);
                    attempt.TargetTileId = target.TileId;
                    attempt.TargetAvailability = availability;
                    LogCommand(
                        $"stage=tribe-order-pre command={attempt.Id} tribe={args.TribeId} " +
                        $"target=({args.TileX},{args.TileY}) targetTile=[{target}] " +
                        $"targetAvailability={availability} patrol={args.IsPatrolPath} " +
                        $"newOrder={args.IsNewOrder} moveType={args.MoveType} " +
                        $"skipOriginal={args.SkipOriginalFunction} pathMode={*moatPathMode}");
                    return;
                }

                if (args.Phase != EventHookPhase.Post)
                    return;

                CommandAttempt current = activeCommandAttempt;
                LogCommand(
                    $"stage=tribe-order-post command={current?.Id ?? 0} tribe={args.TribeId} " +
                    $"target=({args.TileX},{args.TileY}) result={args.ReturnValue} " +
                    $"skipOriginal={args.SkipOriginalFunction} " +
                    $"nativePrecheck={current?.NativePrecheckObserved ?? false} " +
                    $"regionObserved={current?.RegionObserved ?? false} " +
                    $"regionVanilla={current?.RegionVanillaResult ?? -1} " +
                    $"regionEffective={current?.RegionEffectiveResult ?? -1} " +
                    $"formationResult={current?.FormationTargetResult ?? int.MinValue} " +
                    $"floodFillMembershipCalls={current?.FloodFillMembershipCalls ?? 0} " +
                    $"floodFillMembershipBypasses={current?.FloodFillMembershipBypasses ?? 0} " +
                    $"lastFloodFillStamp={current?.LastFloodFillStamp ?? -1} " +
                    $"lastFloodFillVanilla={current?.LastFloodFillVanillaResult ?? -1} " +
                    $"lastFloodFillEffective={current?.LastFloodFillEffectiveResult ?? -1} " +
                    $"regionCandidateRetries={current?.RegionCandidateRetries ?? 0} " +
                    $"unitScan={current?.UnitScanObserved ?? false} " +
                    $"unitIterations={current?.UnitIterations ?? 0} " +
                    $"lastNativeStage={current?.LastNativeStage ?? "none"} " +
                    $"unitMoveObserved={current?.UnitMoveObserved ?? false}");
                activeCommandAttempt = null;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test tribe-order observer failed; event remains unchanged: {ex}");
            }
        }

        private void ObserveUnitMoveOrder(UnitMoveHereEventArgs args)
        {
            if (disposed || !mapRuntimeActive)
                return;

            try
            {
                CommandAttempt command = activeCommandAttempt;
                if (command != null)
                    command.UnitMoveObserved = true;

                string unitState = "unavailable";
                if (args.UnitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(
                        args.UnitId,
                        out GameUnit* unit) && unit != null)
                {
                    unitState =
                        $"player={unit->r_ControllableForPlayerId} unitType={(int)unit->r_UnitChimp} " +
                        $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                        $"marker={*(ushort*)((byte*)unit + UnitMoatMovementMarkerOffset)}";
                }

                LogCommand(
                    $"stage=unit-order-{args.Phase.ToString().ToLowerInvariant()} " +
                    $"command={command?.Id ?? 0} unit={args.UnitId} " +
                    $"target=({args.TileX},{args.TileY}) unknown={args.Unknown} " +
                    $"result={args.ReturnValue} skipOriginal={args.SkipOriginalFunction} " +
                    $"state=[{unitState}]");

                if (args.Phase == EventHookPhase.Post)
                    pendingMoveHereAttempt = null;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test unit-order observer failed; event remains unchanged: {ex}");
            }
        }

        private void ObserveTribeMovementPrecheck(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            int tribeId = unchecked((int)(uint)registers->R9);
            int unitId = unchecked((int)(uint)registers->R15);
            int targetX = unchecked((int)(uint)registers->RSI);
            int targetY = unchecked((int)(uint)registers->RBP);
            int targetTileId = unchecked((int)(uint)registers->RCX);
            int startRegion = unchecked((int)(uint)registers->R14);
            int targetRegion = unchecked((int)(uint)registers->R12);

            try
            {
                CommandAttempt command = activeCommandAttempt;
                if (command != null)
                {
                    command.NativePrecheckObserved = true;
                    command.RepresentativeUnitId = unitId;
                    command.StartRegion = startRegion;
                    command.TargetRegion = targetRegion;
                    command.LastNativeStage = "native-precheck";
                }

                uint flags = IsValidTileId(targetTileId) ? tileFlags[targetTileId] : 0;
                string unitState = "unavailable";
                if (unitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(
                        unitId,
                        out GameUnit* unit) && unit != null)
                {
                    unitState =
                        $"player={unit->r_ControllableForPlayerId} unitType={(int)unit->r_UnitChimp} " +
                        $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                        $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2})";
                }

                LogCommand(
                    $"stage=tribe-native-precheck command={command?.Id ?? 0} tribe={tribeId} " +
                    $"unit={unitId} target=({targetX},{targetY}) targetTile={targetTileId} " +
                    $"targetFlags=0x{flags:X8} blockingLowBits=0x{flags & 0x30:X2} " +
                    $"startRegion={startRegion} targetRegion={targetRegion} " +
                    $"targetAvailability={GetMovementAvailability(targetX, targetY)} " +
                    $"pathMode={*moatPathMode} state=[{unitState}]");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test native tribe-precheck observer failed; Vanilla continues unchanged: {ex}");
            }
        }

        private void ObserveTribeFormationTargetResult(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                CommandAttempt command = activeCommandAttempt;
                if (command == null)
                    return;

                int result = unchecked((int)(uint)context.Pointer->RAX);
                command.FormationTargetResult = result;
                command.LastNativeStage = "formation-target-result";
                LogCommand(
                    $"stage=tribe-formation-target-result command={command.Id} result={result} " +
                    $"returnedPositive={result > 0} unitMoveObserved={command.UnitMoveObserved}");
            }
            catch (Exception ex)
            {
                LogBreadcrumbFailure("formation-target result", ex);
            }
        }

        private void ObserveTribeRegionCandidateRetry(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                CommandAttempt command = activeCommandAttempt;
                if (command == null)
                    return;

                // Vanilla reaches this block when the preceding target-region candidate
                // was rejected and it retries with another formation candidate.
                command.RegionCandidateRetries++;
                command.LastNativeStage = "region-candidate-retry";
                LogCommand(
                    $"stage=tribe-region-candidate-retry command={command.Id} " +
                    $"retry={command.RegionCandidateRetries} " +
                    $"floodFillCalls={command.FloodFillMembershipCalls} " +
                    $"lastFloodFillVanilla={command.LastFloodFillVanillaResult} " +
                    $"lastFloodFillEffective={command.LastFloodFillEffectiveResult}");
            }
            catch (Exception ex)
            {
                LogBreadcrumbFailure("region-candidate retry", ex);
            }
        }

        private void ObserveTribeUnitScanStart(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                CommandAttempt command = activeCommandAttempt;
                if (command == null)
                    return;

                command.UnitScanObserved = true;
                command.LastNativeStage = "unit-scan-start";
                int scanIndex = unchecked((int)(uint)context.Pointer->RCX);
                LogCommand(
                    $"stage=tribe-unit-scan-start command={command.Id} " +
                    $"scanIndex={scanIndex} " +
                    $"representativeUnit={command.RepresentativeUnitId} " +
                    $"floodFillCalls={command.FloodFillMembershipCalls} " +
                    $"floodFillBypasses={command.FloodFillMembershipBypasses}");
            }
            catch (Exception ex)
            {
                LogBreadcrumbFailure("unit-scan entry", ex);
            }
        }

        private void ObserveTribeUnitIterationEnd(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                CommandAttempt command = activeCommandAttempt;
                if (command == null)
                    return;

                int unitId = unchecked((int)(uint)context.Pointer->RBP);
                command.UnitIterations++;
                command.LastNativeStage = "unit-iteration-end";
                string unitState = "unavailable";
                if (unitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(
                        unitId,
                        out GameUnit* unit) && unit != null)
                {
                    unitState =
                        $"player={unit->r_ControllableForPlayerId} unitType={(int)unit->r_UnitChimp} " +
                        $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                        $"aiState={unit->r_AIState} marker=" +
                        $"{*(ushort*)((byte*)unit + UnitMoatMovementMarkerOffset)}";
                }

                LogCommand(
                    $"stage=tribe-unit-iteration-end command={command.Id} iteration={command.UnitIterations} " +
                    $"unit={unitId} unitMoveObserved={command.UnitMoveObserved} state=[{unitState}]");
            }
            catch (Exception ex)
            {
                LogBreadcrumbFailure("unit-iteration end", ex);
            }
        }

        private void ObserveTribeEarlyReturn(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                CommandAttempt command = activeCommandAttempt;
                if (command == null)
                    return;

                string previousStage = command.LastNativeStage;
                int nativeResult = unchecked((int)(uint)context.Pointer->RAX);
                command.LastNativeStage = "central-return";
                LogCommand(
                    $"stage=tribe-central-return command={command.Id} previousStage={previousStage} " +
                    $"nativeResult={nativeResult} " +
                    $"formationResult={command.FormationTargetResult} " +
                    $"floodFillCalls={command.FloodFillMembershipCalls} " +
                    $"floodFillBypasses={command.FloodFillMembershipBypasses} " +
                    $"lastFloodFillStamp={command.LastFloodFillStamp} " +
                    $"lastFloodFillVanilla={command.LastFloodFillVanillaResult} " +
                    $"lastFloodFillEffective={command.LastFloodFillEffectiveResult} " +
                    $"regionCandidateRetries={command.RegionCandidateRetries} " +
                    $"unitScan={command.UnitScanObserved} unitIterations={command.UnitIterations} " +
                    $"unitMoveObserved={command.UnitMoveObserved} " +
                    $"outcome={(command.UnitMoveObserved ? "unit-dispatch-observed" : "no-unit-dispatch")}");
            }
            catch (Exception ex)
            {
                LogBreadcrumbFailure("central return", ex);
            }
        }

        private void LogBreadcrumbFailure(string stage, Exception ex)
        {
            Shared.DebugLogHelper.LogError(
                log,
                $"Move Moat Test Tribe dispatcher {stage} observer failed; Vanilla continues unchanged: {ex}");
        }

        private void LogCommand(string message)
        {
            if (commandLogCount < MaximumCommandLogs)
            {
                commandLogCount++;
                Shared.DebugLogHelper.LogInfo(log, $"MoveMoat {message}.");
                return;
            }

            if (commandLogLimitReported)
                return;
            commandLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat command diagnostics reached their {MaximumCommandLogs}-entry limit.");
        }

        private int ObserveCentralMovementPlan(
            IntPtr unitManager,
            int unitId,
            int targetX,
            int targetY)
        {
            if (disposed || !mapRuntimeActive || activeCommandAttempt == null)
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);

            PlanAttempt attempt = null;
            PlanAttempt previousAttempt = activePlanAttempt;
            try
            {
                if (!disposed && unitManager != IntPtr.Zero && unitId > 0 &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) && unit != null)
                {
                    attempt = new PlanAttempt(++nextPlanId, unitId, targetX, targetY);
                    activePlanAttempt = attempt;
                    LogPlanState("plan-enter", attempt, unit, result: null);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test plan-entry observer failed; Vanilla planning continues unchanged: {ex}");
            }

            int result;
            try
            {
                result = originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
            }
            finally
            {
                activePlanAttempt = previousAttempt;
            }

            if (attempt == null)
                return result;

            try
            {
                attempt.Result = result;
                if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) && unit != null)
                {
                    LogPlanState("plan-result", attempt, unit, result);
                    TrackPlan(attempt, unit);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test plan-result observer failed; result {result} remains unchanged: {ex}");
            }

            return result;
        }

        private int AllowTribeFloodFillForMoveOrder(
            IntPtr tribeManager,
            int tribeId,
            int floodFillStamp)
        {
            int vanillaResult = originalTribeFloodFillMembership(tribeManager, tribeId, floodFillStamp);
            if (disposed || !mapRuntimeActive)
                return vanillaResult;

            int effectiveResult = vanillaResult;

            try
            {
                CommandAttempt command = activeCommandAttempt;
                bool bypassApplied = !disposed &&
                    command != null &&
                    tribeManager != IntPtr.Zero &&
                    tribeId == command.TribeId &&
                    floodFillStamp > 0 &&
                    floodFillStamp <= MaximumFloodFillStamp &&
                    vanillaResult == 0;
                if (bypassApplied)
                    effectiveResult = 1;

                if (command != null)
                {
                    command.FloodFillMembershipCalls++;
                    if (bypassApplied)
                        command.FloodFillMembershipBypasses++;
                    command.LastFloodFillStamp = floodFillStamp;
                    command.LastFloodFillVanillaResult = vanillaResult;
                    command.LastFloodFillEffectiveResult = effectiveResult;
                    command.LastNativeStage = "flood-fill-membership";

                    LogCommand(
                        $"stage=tribe-flood-fill-membership command={command.Id} " +
                        $"tribeArgument={tribeId} floodFillStamp={floodFillStamp} " +
                        $"vanilla={vanillaResult} effective={effectiveResult} " +
                        $"bypass={bypassApplied} call={command.FloodFillMembershipCalls}");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test Tribe flood-fill membership callback failed; " +
                    $"Vanilla result {vanillaResult} remains active: {ex}");
                return vanillaResult;
            }

            return effectiveResult;
        }

        private int ForceCompletedMoatMode(IntPtr unitManager, int unitId)
        {
            int vanillaResult = originalDetectCompletedMoatMode(unitManager, unitId);
            if (disposed || !mapRuntimeActive || activeCommandAttempt == null ||
                unitManager == IntPtr.Zero || unitId <= 0)
                return vanillaResult;

            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null)
                {
                    return vanillaResult;
                }

                PlanAttempt attempt = activePlanAttempt;
                if (attempt == null)
                {
                    attempt = new PlanAttempt(++nextPlanId, unitId, -1, -1);
                    pendingMoveHereAttempt = attempt;
                }
                attempt.VanillaModeDetected = vanillaResult != 0;
                LogModeActivation(unitId, unit, vanillaResult, attempt);
                return 1;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test mode callback failed; Vanilla result {vanillaResult} remains active: {ex}");
                return vanillaResult;
            }
        }

        private int AllowBuilderAfterFailedRegionSearch(
            IntPtr pathManager,
            int movementClass,
            int targetRegion,
            int startX,
            int startY)
        {
            int vanillaResult = originalRegionReachability(
                pathManager,
                movementClass,
                targetRegion,
                startX,
                startY);
            if (disposed || !mapRuntimeActive || activeCommandAttempt == null)
                return vanillaResult;

            int effectiveResult = vanillaResult;

            try
            {
                bool bypassApplied = !disposed &&
                    vanillaResult == 0 &&
                    *moatPathMode == 1 &&
                    targetRegion > 0 &&
                    targetRegion <= MaximumRegionId;
                if (bypassApplied)
                    effectiveResult = targetRegion;

                CommandAttempt command = activeCommandAttempt;
                if (command != null)
                {
                    command.RegionObserved = true;
                    command.RegionVanillaResult = vanillaResult;
                    command.RegionEffectiveResult = effectiveResult;
                }

                LogReachability(
                    movementClass,
                    targetRegion,
                    startX,
                    startY,
                    vanillaResult,
                    effectiveResult,
                    bypassApplied);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test reachability callback failed; Vanilla result {vanillaResult} remains active: {ex}");
                return vanillaResult;
            }

            return effectiveResult;
        }

        private int ObservePrimaryDirectionSeedBuilder(
            IntPtr pathManager,
            int startX,
            int startY,
            int targetX,
            int targetY)
        {
            int result = originalPrimaryDirectionSeedBuilder(
                pathManager,
                startX,
                startY,
                targetX,
                targetY);
            RecordDirectionSeedResult(
                "primary",
                startX,
                startY,
                targetX,
                targetY,
                result);
            return result;
        }

        private int ObserveFallbackDirectionSeedBuilder(
            IntPtr pathManager,
            int startX,
            int startY,
            int targetX,
            int targetY)
        {
            int result = originalFallbackDirectionSeedBuilder(
                pathManager,
                startX,
                startY,
                targetX,
                targetY);
            RecordDirectionSeedResult(
                "fallback",
                startX,
                startY,
                targetX,
                targetY,
                result);
            return result;
        }

        private void RecordDirectionSeedResult(
            string variant,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int result)
        {
            try
            {
                PlanAttempt attempt = activePlanAttempt ?? pendingMoveHereAttempt;
                if (disposed || attempt == null)
                    return;

                if (variant == "primary")
                {
                    attempt.PrimaryDirectionSeedCalls++;
                    attempt.LastPrimaryDirectionSeedResult = result;
                }
                else
                {
                    attempt.FallbackDirectionSeedCalls++;
                    attempt.LastFallbackDirectionSeedResult = result;
                }

                if (builderLogCount < MaximumBuilderLogs)
                {
                    builderLogCount++;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=direction-seed-{variant} plan={attempt.Id} unit={attempt.UnitId} " +
                        $"start=({startX},{startY}) startAvailability={GetMovementAvailability(startX, startY)} " +
                        $"target=({targetX},{targetY}) targetAvailability=" +
                        $"{GetMovementAvailability(targetX, targetY)} result={result} " +
                        $"returnedPositive={result > 0} modeSource={GetModeSource(attempt)} " +
                        $"pathMode={*moatPathMode}.");
                }
                else if (!builderLogLimitReported)
                {
                    builderLogLimitReported = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"MoveMoat builder diagnostics reached their {MaximumBuilderLogs}-entry limit.");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test {variant} direction-seed observer failed; " +
                    $"real result {result} remains unchanged: {ex}");
            }
        }

        private void ObserveStandardTileExpander(NativePointer<X64SmartCPUContext> context)
        {
            PlanAttempt attempt = activePlanAttempt ?? pendingMoveHereAttempt;
            if (!disposed && attempt != null)
                attempt.StandardTileExpanderCalls++;
        }

        private void ObserveMoatAwareTileExpander(NativePointer<X64SmartCPUContext> context)
        {
            PlanAttempt attempt = activePlanAttempt ?? pendingMoveHereAttempt;
            if (!disposed && attempt != null)
                attempt.MoatAwareTileExpanderCalls++;
        }

        private void ObserveMoatAwareCandidateResult(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                PlanAttempt attempt = activePlanAttempt ?? pendingMoveHereAttempt;
                if (disposed || attempt == null)
                    return;

                X64SmartCPUContext* registers = context.Pointer;
                int tileId = unchecked((int)(uint)registers->RDI);
                int moatObjectId = unchecked((int)(uint)registers->RAX);
                attempt.CompletedMoatCandidates++;
                attempt.LastCompletedMoatCandidateTileId = tileId;
                attempt.LastCompletedMoatObjectId = moatObjectId;
                if (moatObjectId == 0)
                    attempt.CompletedMoatCandidatesWithoutObject++;
            }
            catch (Exception ex)
            {
                LogBreadcrumbFailure("moat-aware candidate counter", ex);
            }
        }

        private void ObserveMoatAwareAllianceComparison(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                PlanAttempt attempt = activePlanAttempt ?? pendingMoveHereAttempt;
                if (disposed || attempt == null)
                    return;

                X64SmartCPUContext* registers = context.Pointer;
                int movingPlayerIndex = unchecked((int)(uint)registers->R12);
                long moatOwnerIndex = unchecked((long)registers->RCX);
                int movingAllianceGroup = unchecked((int)(uint)registers->RAX);
                attempt.MoatAllianceComparisons++;
                attempt.LastMovingPlayerIndex = movingPlayerIndex;
                attempt.LastMoatOwnerIndex = moatOwnerIndex >= int.MinValue && moatOwnerIndex <= int.MaxValue
                    ? (int)moatOwnerIndex
                    : -1;
                attempt.LastMovingAllianceGroup = movingAllianceGroup;

                if (moatOwnerIndex < 0 || moatOwnerIndex > 8)
                {
                    attempt.InvalidMoatOwnerIndices++;
                    return;
                }

                int moatAllianceGroup = allianceGroupTable[moatOwnerIndex];
                attempt.LastMoatAllianceGroup = moatAllianceGroup;
                if (moatAllianceGroup == movingAllianceGroup)
                    attempt.AlliedMoatComparisons++;
                else
                    attempt.EnemyMoatComparisons++;
            }
            catch (Exception ex)
            {
                LogBreadcrumbFailure("moat-aware alliance counter", ex);
            }
        }

        private int ObservePathBuilder(
            IntPtr pathManager,
            int movementClass,
            int movementProfile)
        {
            if (disposed || !mapRuntimeActive || activeCommandAttempt == null)
                return originalPathBuilder(pathManager, movementClass, movementProfile);

            BuilderStateSnapshot inputState = default;
            bool inputStateCaptured = false;
            int* routeVariant = null;
            int originalRouteVariant = 0;
            bool routeVariantOverrideApplied = false;
            bool routeVariantOverrideRetained = false;
            try
            {
                inputState = CaptureBuilderState(pathManager);
                inputStateCaptured = true;

                PlanAttempt attempt = activePlanAttempt ?? pendingMoveHereAttempt;
                CommandAttempt command = activeCommandAttempt;
                if (!disposed &&
                    *moatPathMode == 1 &&
                    attempt != null &&
                    !attempt.VanillaModeDetected &&
                    command != null &&
                    command.FloodFillMembershipBypasses > 0)
                {
                    routeVariant = (int*)((byte*)pathManager + 0x80);
                    originalRouteVariant = *routeVariant;
                    if (originalRouteVariant == 1)
                    {
                        // Vanilla uses zero here when a unit starts on a completed moat.
                        // Limit the experiment to commands that reached us through the
                        // correlated flood-fill bypass; unrelated movement stays untouched.
                        *routeVariant = 0;
                        routeVariantOverrideApplied = true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (routeVariantOverrideApplied && routeVariant != null)
                {
                    *routeVariant = originalRouteVariant;
                    routeVariantOverrideApplied = false;
                }
                // Diagnostics must never prevent Vanilla's real builder from running.
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test could not prepare builder input; Vanilla builder will still run: {ex}");
            }

            int result;
            try
            {
                result = originalPathBuilder(pathManager, movementClass, movementProfile);
                routeVariantOverrideRetained = routeVariantOverrideApplied && result > 0;
                if (routeVariantOverrideApplied && !routeVariantOverrideRetained)
                    *routeVariant = originalRouteVariant;
            }
            catch
            {
                if (routeVariantOverrideApplied && routeVariant != null)
                    *routeVariant = originalRouteVariant;
                throw;
            }

            try
            {
                if (!disposed && *moatPathMode == 1)
                {
                    BuilderStateSnapshot outputState = CaptureBuilderState(pathManager);
                    LogBuilderResult(
                        movementClass,
                        movementProfile,
                        result,
                        inputStateCaptured,
                        inputState,
                        outputState,
                        routeVariantOverrideApplied,
                        routeVariantOverrideRetained);
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test builder observer failed; real builder result {result} remains unchanged: {ex}");
            }

            return result;
        }

        private static BuilderStateSnapshot CaptureBuilderState(IntPtr pathManager)
        {
            if (pathManager == IntPtr.Zero)
                throw new ArgumentNullException(nameof(pathManager));

            byte* state = (byte*)pathManager;
            return new BuilderStateSnapshot(
                *(int*)(state + 0x7C),
                *(int*)(state + 0x80),
                *(int*)(state + 0x84),
                *(int*)(state + 0x88),
                *(int*)(state + 0x94),
                *(int*)(state + 0xA8),
                *(int*)(state + 0xAC),
                *(int*)(state + 0x155F68));
        }

        private void RecordMoveHereBuilderResult(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            int unitId = unchecked((int)(uint)registers->RSI);
            PlanAttempt attempt = pendingMoveHereAttempt;
            if (unitId <= 0 || attempt == null || attempt.UnitId != unitId)
                return;

            try
            {
                attempt.TargetX = unchecked((ushort)registers->R14);
                attempt.TargetY = unchecked((ushort)registers->RBP);
                attempt.Result = unchecked((int)(uint)registers->RAX);
                bool usedPrimaryBuilder = unchecked((int)(uint)registers->R13) == 0;
                bool targetMatchesGlobals = attempt.TargetX == *pathTargetX &&
                    attempt.TargetY == *pathTargetY;

                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        unitId,
                        out GameUnit* unit) || unit == null)
                {
                    return;
                }

                if (planLogCount < MaximumPlanLogs)
                {
                    planLogCount++;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=movehere-builder-result plan={attempt.Id} unit={unitId} " +
                        $"target=({attempt.TargetX},{attempt.TargetY}) result={attempt.Result} " +
                        $"returnedPositive={attempt.Result > 0} " +
                        $"builder={(usedPrimaryBuilder ? "F4930" : "E32B0")} " +
                        $"modeSource={GetModeSource(attempt)} pathMode={*moatPathMode} " +
                        $"targetMatchesGlobals={targetMatchesGlobals} preCommit=[{FormatUnitState(unit, unitId)}].");
                }
                else
                {
                    ReportPlanLogLimitOnce();
                }

                TrackPlan(attempt, unit);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test MoveHere result observer failed; native result remains unchanged: {ex}");
            }
            finally
            {
                if (ReferenceEquals(pendingMoveHereAttempt, attempt))
                    pendingMoveHereAttempt = null;
            }
        }

        private void EnableCompletedMoatMovementStep(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            int unitId = unchecked((int)(uint)registers->R11);
            int currentTileId = unchecked((int)(uint)registers->RDI);
            int nextTileId = unchecked((int)(uint)registers->RDX);
            if (disposed || !mapRuntimeActive || unitId <= 0 ||
                !IsValidTileId(currentTileId) || !IsValidTileId(nextTileId))
                return;

            uint currentFlags = tileFlags[currentTileId];
            uint nextFlags = tileFlags[nextTileId];
            bool currentIsMoat = (currentFlags & CompletedMoatTileFlag) != 0;
            bool nextIsMoat = (nextFlags & CompletedMoatTileFlag) != 0;
            if (!currentIsMoat && !nextIsMoat)
                return;

            int* markerArgument = null;
            int originalMarker = 0;
            bool markerChanged = false;
            try
            {
                // This is Vanillas single runtime gate for entering or leaving completed moats.
                // Altering its existing argument keeps all ordinary collision tests in place.
                markerArgument = (int*)unchecked((long)(registers->RSP + 0x70));
                originalMarker = *markerArgument;
                bool bypassApplied = originalMarker == 0;
                if (bypassApplied)
                {
                    *markerArgument = 1;
                    markerChanged = true;
                }

                int unitMarker = -1;
                string unitDetails = "unavailable";
                if (GameUnitManagerAPI.Instance.TryGetUnitById(
                        unitId,
                        out GameUnit* unit) && unit != null)
                {
                    unitMarker = *(ushort*)((byte*)unit + UnitMoatMovementMarkerOffset);
                    unitDetails =
                        $"player={unit->r_ControllableForPlayerId} unitType={(int)unit->r_UnitChimp} " +
                        $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                        $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2})";
                }

                if (stepGateLogCount < MaximumStepGateLogs)
                {
                    stepGateLogCount++;
                    int pathRow = unchecked((int)(uint)registers->R9);
                    int direction = FindDirection(currentTileId, nextTileId, pathRow);
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=step-gate unit={unitId} {unitDetails} " +
                        $"currentTile={currentTileId} currentFlags=0x{currentFlags:X8} " +
                        $"currentRegion={pathRegionGrid[currentTileId]} currentMoat={currentIsMoat} " +
                        $"nextTile={nextTileId} nextFlags=0x{nextFlags:X8} " +
                        $"nextRegion={pathRegionGrid[nextTileId]} nextMoat={nextIsMoat} " +
                        $"pathRow={pathRow} direction={direction} " +
                        $"originalMarker={originalMarker} effectiveMarker={*markerArgument} " +
                        $"unitMarker={unitMarker} vanillaNatural={originalMarker != 0} " +
                        $"bypass={bypassApplied}.");
                }
                else if (!stepGateLogLimitReported)
                {
                    stepGateLogLimitReported = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"MoveMoat step-gate diagnostics reached their {MaximumStepGateLogs}-entry limit.");
                }
            }
            catch (Exception ex)
            {
                if (markerChanged && markerArgument != null)
                    *markerArgument = originalMarker;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test step-gate callback failed; Vanilla movement state remains active: {ex}");
            }
        }

        private void LogModeActivation(
            int unitId,
            GameUnit* unit,
            int vanillaResult,
            PlanAttempt attempt)
        {
            if (modeLogCount < MaximumModeLogs)
            {
                modeLogCount++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=mode plan={attempt.Id} unit={unitId} player={unit->r_ControllableForPlayerId} " +
                    $"unitType={(int)unit->r_UnitChimp} tile=({unit->r_CurrentTilePositionX}," +
                    $"{unit->r_CurrentTilePositionY}) target=({unit->r_TargetTilePositionX}," +
                    $"{unit->r_TargetTilePositionY}) vanilla={vanillaResult} effective=1 " +
                    $"modeSource={GetModeSource(attempt)} unitMarker=" +
                    $"{*(ushort*)((byte*)unit + UnitMoatMovementMarkerOffset)}.");
                return;
            }

            if (modeLogLimitReported)
                return;
            modeLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat mode diagnostics reached their {MaximumModeLogs}-entry limit.");
        }

        private void ObserveCursorForbiddenResult(NativePointer<X64SmartCPUContext> context)
        {
            if (disposed || !cursorPollArmed)
                return;

            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                int nativeUnitIndex = unchecked((int)(uint)registers->R14);
                int availabilityGate = unchecked((int)(uint)registers->R15);
                int shortcutResult = unchecked((int)(uint)registers->RBX);
                int targetX = *cursorTargetX;
                int targetY = *cursorTargetY;
                int currentTileId = -1;
                uint currentTileFlags = 0;
                int nextUnitId = *(int*)nativeUnitManager;
                if (nativeUnitIndex > 0 && nativeUnitIndex < nextUnitId)
                {
                    currentTileId = *(int*)(nativeUnitManager +
                        (nativeUnitIndex * 0x490) + 0x72C);
                    if (IsValidTileId(currentTileId))
                        currentTileFlags = tileFlags[currentTileId];
                }

                bool regionObserved = cursorRegionEvaluationSerial == cursorEvaluationSerial &&
                    hasLastCursorRegionPrecheckState &&
                    lastCursorRegionPrecheckNativeUnitIndex == nativeUnitIndex &&
                    lastCursorRegionPrecheckTargetX == targetX &&
                    lastCursorRegionPrecheckTargetY == targetY;
                bool directObserved = cursorDirectEvaluationSerial == cursorEvaluationSerial &&
                    hasLastDirectCursorState &&
                    lastDirectCursorNativeUnitIndex == nativeUnitIndex &&
                    lastDirectCursorTargetX == targetX &&
                    lastDirectCursorTargetY == targetY;

                string reason;
                if (shortcutResult != 0)
                    reason = "unexpected-nonzero-shortcut";
                else if (availabilityGate == 0)
                    reason = "availability-or-global-unit-gate";
                else if (!IsValidTileId(currentTileId))
                    reason = "invalid-selected-unit-current-tile";
                else if ((currentTileFlags & CursorCurrentTileRequiredFlags) == 0)
                    reason = "selected-unit-current-tile-flags";
                else if (!regionObserved)
                    reason = "before-region-precheck-despite-visible-gates";
                else if (lastCursorRegionPrecheckResult == 0)
                    reason = "region-precheck";
                else if (!directObserved)
                    reason = "before-direct-reachability";
                else if (lastDirectCursorResult == 0)
                    reason = "direct-reachability";
                else
                    reason = "post-reachability-or-other";

                bool changed = !hasLastCursorForbiddenState ||
                    nativeUnitIndex != lastCursorForbiddenNativeUnitIndex ||
                    targetX != lastCursorForbiddenTargetX ||
                    targetY != lastCursorForbiddenTargetY ||
                    availabilityGate != lastCursorForbiddenAvailabilityGate ||
                    currentTileId != lastCursorForbiddenCurrentTileId ||
                    !string.Equals(reason, lastCursorForbiddenReason, StringComparison.Ordinal);
                if (!changed)
                    return;

                hasLastCursorForbiddenState = true;
                lastCursorForbiddenNativeUnitIndex = nativeUnitIndex;
                lastCursorForbiddenTargetX = targetX;
                lastCursorForbiddenTargetY = targetY;
                lastCursorForbiddenAvailabilityGate = availabilityGate;
                lastCursorForbiddenCurrentTileId = currentTileId;
                lastCursorForbiddenReason = reason;

                if (cursorForbiddenLogCount >= MaximumCursorForbiddenLogs)
                {
                    if (!cursorForbiddenLogLimitReported)
                    {
                        cursorForbiddenLogLimitReported = true;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"MoveMoat forbidden-cursor diagnostics reached their {MaximumCursorForbiddenLogs}-entry limit.");
                    }
                    return;
                }

                cursorForbiddenLogCount++;
                string unitState = "unavailable";
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (nativeUnitIndex >= 0 && nativeUnitIndex < units.Length)
                {
                    GameUnit unit = units[nativeUnitIndex];
                    unitState =
                        $"player={unit.r_ControllableForPlayerId} type={(int)unit.r_UnitChimp} " +
                        $"current=({unit.r_CurrentTilePositionX},{unit.r_CurrentTilePositionY}) " +
                        $"aiState={unit.r_AIState} moving={unit.r_MovingRelevant}";
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=cursor-forbidden reason={reason} target=({targetX},{targetY}) " +
                    $"nativeUnitIndex={nativeUnitIndex} nextUnitId={nextUnitId} " +
                    $"shortcut={shortcutResult} availabilityGate={availabilityGate} " +
                    $"currentTile={currentTileId} currentFlags=0x{currentTileFlags:X8} " +
                    $"requiredFlags=0x{CursorCurrentTileRequiredFlags:X8} " +
                    $"evaluation={cursorEvaluationSerial} specialResult={lastCursorSpecialModeResult} " +
                    $"regionObserved={regionObserved} regionResult={lastCursorRegionPrecheckResult} " +
                    $"directObserved={directObserved} directResult={lastDirectCursorResult} " +
                    $"unitState=[{unitState}].");
            }
            catch (Exception ex)
            {
                if (cursorForbiddenLogLimitReported)
                    return;
                cursorForbiddenLogLimitReported = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MoveMoat forbidden-cursor diagnostics failed once; Vanilla cursor behavior remains active: {ex}");
            }
        }

        private int ObserveCursorSpecialMode(IntPtr selectionState)
        {
            CursorSpecialModeDelegate vanilla = originalCursorSpecialMode;
            if (vanilla == null)
                return 0;

            int vanillaResult = vanilla(selectionState);
            if (disposed)
                return vanillaResult;

            cursorEvaluationSerial++;

            try
            {
                int targetX = *cursorTargetX;
                int targetY = *cursorTargetY;
                int gate = int.MinValue;
                int occupiedSlots = 0;
                if (selectionState != IntPtr.Zero)
                {
                    byte* state = (byte*)selectionState.ToPointer();
                    gate = *(int*)(state + 0x5BC);
                    int* slots = (int*)(state + 0x564);
                    for (int index = 0; index < 35; index++)
                    {
                        if (index != 22 && slots[index] != 0)
                            occupiedSlots++;
                    }
                }

                if (occupiedSlots > 0 && !cursorPollArmed)
                {
                    // Ignore menu and pre-selection mouse movement so the bounded poll log
                    // remains available for the actual selected-unit hover test.
                    cursorPollArmed = true;
                    cursorPollLogCount = 0;
                    cursorPollLogLimitReported = false;
                    hasLastCursorPollState = false;
                }

                bool changed = !hasLastCursorSpecialModeState ||
                    targetX != lastCursorSpecialModeTargetX ||
                    targetY != lastCursorSpecialModeTargetY ||
                    vanillaResult != lastCursorSpecialModeResult ||
                    gate != lastCursorSpecialModeGate ||
                    occupiedSlots != lastCursorSpecialModeOccupiedSlots;
                if (!changed)
                    return vanillaResult;

                hasLastCursorSpecialModeState = true;
                lastCursorSpecialModeTargetX = targetX;
                lastCursorSpecialModeTargetY = targetY;
                lastCursorSpecialModeResult = vanillaResult;
                lastCursorSpecialModeGate = gate;
                lastCursorSpecialModeOccupiedSlots = occupiedSlots;
                if (TryReserveCursorPrecheckLog())
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=cursor-special-mode target=({targetX},{targetY}) " +
                        $"result={vanillaResult} gate5BC={gate} occupiedSlotsExcluding22={occupiedSlots} " +
                        $"selectionState=0x{selectionState.ToInt64():X}.");
                }
            }
            catch (Exception ex)
            {
                ReportCursorPrecheckFailure("special-mode", ex);
            }

            return vanillaResult;
        }

        private int ObserveCursorRegionPrecheck(IntPtr pathManager, int nativeUnitIndex)
        {
            CursorRegionPrecheckDelegate vanilla = originalCursorRegionPrecheck;
            if (vanilla == null)
                return 0;

            int vanillaResult = vanilla(pathManager, nativeUnitIndex);
            if (disposed)
                return vanillaResult;

            cursorRegionEvaluationSerial = cursorEvaluationSerial;

            try
            {
                int targetX = *cursorTargetX;
                int targetY = *cursorTargetY;
                bool changed = !hasLastCursorRegionPrecheckState ||
                    nativeUnitIndex != lastCursorRegionPrecheckNativeUnitIndex ||
                    targetX != lastCursorRegionPrecheckTargetX ||
                    targetY != lastCursorRegionPrecheckTargetY ||
                    vanillaResult != lastCursorRegionPrecheckResult;
                if (!changed)
                    return vanillaResult;

                hasLastCursorRegionPrecheckState = true;
                lastCursorRegionPrecheckNativeUnitIndex = nativeUnitIndex;
                lastCursorRegionPrecheckTargetX = targetX;
                lastCursorRegionPrecheckTargetY = targetY;
                lastCursorRegionPrecheckResult = vanillaResult;
                if (TryReserveCursorPrecheckLog())
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"MoveMoat stage=cursor-region-precheck nativeUnitIndex={nativeUnitIndex} " +
                        $"target=({targetX},{targetY}) result={vanillaResult} " +
                        $"pathMode={*moatPathMode} assassinContext={*assassinPathContextFlag} " +
                        $"pathManager=0x{pathManager.ToInt64():X}.");
                }
            }
            catch (Exception ex)
            {
                ReportCursorPrecheckFailure("region", ex);
            }

            return vanillaResult;
        }

        private int ObserveCommonPathRequest(
            IntPtr unitBase,
            int nativeUnitIndex,
            int targetX,
            int targetY,
            int pathOption)
        {
            CommonPathRequestDelegate vanilla = originalCommonPathRequest;
            if (vanilla == null)
                return 0;

            int contextBefore = *assassinPathContextFlag;
            int vanillaResult = vanilla(unitBase, nativeUnitIndex, targetX, targetY, pathOption);
            int contextAfter = *assassinPathContextFlag;
            if (disposed)
                return vanillaResult;

            try
            {
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                bool unitResolved = nativeUnitIndex >= 0 && nativeUnitIndex < units.Length;
                ActivateRuntimeFromValidatedNativeCall(
                    unitResolved,
                    targetX,
                    targetY,
                    "shared-common-path");

                bool changed = !hasLastCommonPathState ||
                    nativeUnitIndex != lastCommonPathNativeUnitIndex ||
                    targetX != lastCommonPathTargetX ||
                    targetY != lastCommonPathTargetY ||
                    pathOption != lastCommonPathOption ||
                    vanillaResult != lastCommonPathResult ||
                    contextBefore != lastCommonPathContextBefore ||
                    contextAfter != lastCommonPathContextAfter;
                if (!changed)
                    return vanillaResult;

                hasLastCommonPathState = true;
                lastCommonPathNativeUnitIndex = nativeUnitIndex;
                lastCommonPathTargetX = targetX;
                lastCommonPathTargetY = targetY;
                lastCommonPathOption = pathOption;
                lastCommonPathResult = vanillaResult;
                lastCommonPathContextBefore = contextBefore;
                lastCommonPathContextAfter = contextAfter;

                if (commonPathLogCount >= MaximumCommonPathLogs)
                {
                    if (!commonPathLogLimitReported)
                    {
                        commonPathLogLimitReported = true;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"MoveMoat common-path diagnostics reached their {MaximumCommonPathLogs}-entry limit.");
                    }
                    return vanillaResult;
                }

                commonPathLogCount++;
                string unitState = "unavailable";
                if (unitResolved)
                {
                    GameUnit unit = units[nativeUnitIndex];
                    unitState =
                        $"player={unit.r_ControllableForPlayerId} type={(int)unit.r_UnitChimp} " +
                        $"current=({unit.r_CurrentTilePositionX},{unit.r_CurrentTilePositionY}) " +
                        $"aiState={unit.r_AIState} moving={unit.r_MovingRelevant}";
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=common-path nativeUnitIndex={nativeUnitIndex} " +
                    $"target=({targetX},{targetY}) pathOption={pathOption} result={vanillaResult} " +
                    $"assassinContextBefore={contextBefore} assassinContextAfter={contextAfter} " +
                    $"unitBase=0x{unitBase.ToInt64():X} unitState=[{unitState}].");
            }
            catch (Exception ex)
            {
                if (!commonPathLogLimitReported)
                {
                    commonPathLogLimitReported = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"MoveMoat common-path diagnostics failed once; Vanilla result remains unchanged: {ex}");
                }
            }

            return vanillaResult;
        }

        private int ObserveCursorReachability(
            IntPtr pathManager,
            int nativeUnitIndex,
            int targetX,
            int targetY)
        {
            CursorReachabilityDelegate vanilla = originalCursorReachability;
            if (vanilla == null)
                return 0;

            int vanillaResult = vanilla(pathManager, nativeUnitIndex, targetX, targetY);
            if (disposed)
                return vanillaResult;

            cursorDirectEvaluationSerial = cursorEvaluationSerial;

            try
            {
                // The complete function detour leaves the caller's conditional branch untouched.
                // Seeing this call is itself a safe indication that native map/unit state is live.
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                bool unitResolved = nativeUnitIndex >= 0 && nativeUnitIndex < units.Length;
                ActivateRuntimeFromValidatedNativeCall(
                    unitResolved,
                    targetX,
                    targetY,
                    "cursor-reachability");

                int pathMode = *moatPathMode;
                int assassinContext = *assassinPathContextFlag;
                directCursorCallSerial++;

                bool changed = !hasLastDirectCursorState ||
                    nativeUnitIndex != lastDirectCursorNativeUnitIndex ||
                    targetX != lastDirectCursorTargetX ||
                    targetY != lastDirectCursorTargetY ||
                    vanillaResult != lastDirectCursorResult ||
                    pathMode != lastDirectCursorPathMode ||
                    assassinContext != lastDirectCursorAssassinContext;
                if (!changed)
                    return vanillaResult;

                hasLastDirectCursorState = true;
                lastDirectCursorNativeUnitIndex = nativeUnitIndex;
                lastDirectCursorTargetX = targetX;
                lastDirectCursorTargetY = targetY;
                lastDirectCursorResult = vanillaResult;
                lastDirectCursorPathMode = pathMode;
                lastDirectCursorAssassinContext = assassinContext;

                if (directCursorLogCount >= MaximumDirectCursorLogs)
                {
                    if (!directCursorLogLimitReported)
                    {
                        directCursorLogLimitReported = true;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"MoveMoat direct cursor diagnostics reached their {MaximumDirectCursorLogs}-entry limit.");
                    }
                    return vanillaResult;
                }

                directCursorLogCount++;
                string unitState = "unavailable";
                if (unitResolved)
                {
                    GameUnit unit = units[nativeUnitIndex];
                    unitState =
                        $"player={unit.r_ControllableForPlayerId} type={(int)unit.r_UnitChimp} " +
                        $"current=({unit.r_CurrentTilePositionX},{unit.r_CurrentTilePositionY}) " +
                        $"unitTarget=({unit.r_TargetTilePositionX},{unit.r_TargetTilePositionY}) " +
                        $"aiState={unit.r_AIState} moving={unit.r_MovingRelevant}";
                }

                TileDiagnostic target = GetTileDiagnostic(targetX, targetY);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=cursor-direct serial={directCursorCallSerial} " +
                    $"nativeUnitIndex={nativeUnitIndex} pathManager=0x{pathManager.ToInt64():X} " +
                    $"target=({targetX},{targetY}) targetTile=[{target}] " +
                    $"targetAvailability={GetMovementAvailability(targetX, targetY)} " +
                    $"vanilla={vanillaResult} pathMode={pathMode} assassinContext={assassinContext} " +
                    $"cursorGlobals=({*cursorTargetX},{*cursorTargetY}) unitState=[{unitState}].");
            }
            catch (Exception ex)
            {
                if (directCursorLogLimitReported)
                    return vanillaResult;
                directCursorLogLimitReported = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MoveMoat direct cursor diagnostics failed once; Vanilla cursor behavior remains active: {ex}");
            }

            return vanillaResult;
        }

        private void ObserveCursorFrame()
        {
            if (disposed || !cursorPollArmed || Time.frameCount == lastCursorPollFrame)
                return;
            lastCursorPollFrame = Time.frameCount;

            try
            {
                int targetX = *cursorTargetX;
                int targetY = *cursorTargetY;
                int state548 = *cursorState548;
                int state54C = *cursorState54C;
                int state550 = *cursorState550;
                int state55C = *cursorState55C;
                int state560 = *cursorState560;
                int pathMode = *moatPathMode;
                int assassinContext = *assassinPathContextFlag;
                long directSerial = directCursorCallSerial;
                bool changed = !hasLastCursorPollState ||
                    targetX != lastCursorPollTargetX ||
                    targetY != lastCursorPollTargetY ||
                    state548 != lastCursorPollState548 ||
                    state54C != lastCursorPollState54C ||
                    state550 != lastCursorPollState550 ||
                    state55C != lastCursorPollState55C ||
                    state560 != lastCursorPollState560 ||
                    pathMode != lastCursorPollPathMode ||
                    assassinContext != lastCursorPollAssassinContext;
                if (!changed)
                    return;

                hasLastCursorPollState = true;
                lastCursorPollTargetX = targetX;
                lastCursorPollTargetY = targetY;
                lastCursorPollState548 = state548;
                lastCursorPollState54C = state54C;
                lastCursorPollState550 = state550;
                lastCursorPollState55C = state55C;
                lastCursorPollState560 = state560;
                lastCursorPollPathMode = pathMode;
                lastCursorPollAssassinContext = assassinContext;

                if (cursorPollLogCount >= MaximumCursorPollLogs)
                {
                    if (!cursorPollLogLimitReported)
                    {
                        cursorPollLogLimitReported = true;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"MoveMoat cursor polling diagnostics reached their {MaximumCursorPollLogs}-entry limit.");
                    }
                    return;
                }

                cursorPollLogCount++;
                TileDiagnostic target = GetTileDiagnostic(targetX, targetY);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=cursor-poll frame={lastCursorPollFrame} " +
                    $"target=({targetX},{targetY}) targetTile=[{target}] " +
                    $"targetAvailability={GetMovementAvailability(targetX, targetY)} " +
                    $"cursorState=[548={state548},54C={state54C},550={state550}," +
                    $"55C={state55C},560={state560}] pathMode={pathMode} " +
                    $"assassinContext={assassinContext} directSerial={directSerial} " +
                    $"lastDirect=[nativeUnitIndex={lastDirectCursorNativeUnitIndex},target=({lastDirectCursorTargetX}," +
                    $"{lastDirectCursorTargetY}),result={lastDirectCursorResult}].");
            }
            catch (Exception ex)
            {
                if (cursorPollLogLimitReported)
                    return;
                cursorPollLogLimitReported = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"MoveMoat cursor polling diagnostics failed once; Vanilla cursor behavior remains active: {ex}");
            }
        }

        private void ActivateRuntimeFromValidatedNativeCall(
            bool unitResolved,
            int targetX,
            int targetY,
            string source)
        {
            if (mapRuntimeActive || !unitResolved ||
                targetX < 0 || targetX >= 800 || targetY < 0 || targetY >= 800)
                return;

            bool activated = false;
            lock (trackingLock)
            {
                if (!mapRuntimeActive)
                {
                    mapRuntimeActive = true;
                    mapEpoch++;
                    activated = true;
                }
            }

            if (activated)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat runtime activated from validated native stage={source} because map-load/start events were not observed.");
            }
        }

        private bool TryReserveCursorPrecheckLog()
        {
            if (cursorPrecheckLogCount < MaximumCursorPrecheckLogs)
            {
                cursorPrecheckLogCount++;
                return true;
            }

            if (!cursorPrecheckLogLimitReported)
            {
                cursorPrecheckLogLimitReported = true;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"MoveMoat cursor-precheck diagnostics reached their {MaximumCursorPrecheckLogs}-entry limit.");
            }
            return false;
        }

        private void ReportCursorPrecheckFailure(string stage, Exception ex)
        {
            if (cursorPrecheckLogLimitReported)
                return;
            cursorPrecheckLogLimitReported = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"MoveMoat cursor {stage} diagnostics failed once; Vanilla result remains unchanged: {ex}");
        }

        private void LogReachability(
            int movementClass,
            int targetRegion,
            int startX,
            int startY,
            int vanillaResult,
            int effectiveResult,
            bool bypassApplied)
        {
            PlanAttempt plan = activePlanAttempt ?? pendingMoveHereAttempt;
            CommandAttempt command = activeCommandAttempt;
            if (plan == null && command == null)
            {
                LogCursorReachabilityChange(
                    movementClass,
                    targetRegion,
                    startX,
                    startY,
                    vanillaResult,
                    effectiveResult,
                    bypassApplied);
                return;
            }

            if (reachabilityLogCount < MaximumReachabilityLogs)
            {
                reachabilityLogCount++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=region plan={plan?.Id ?? 0} command={command?.Id ?? 0} " +
                    $"movementClass={movementClass} start=({startX},{startY}) " +
                    $"targetRegion={targetRegion} vanilla={vanillaResult} effective={effectiveResult} " +
                    $"bypass={bypassApplied} pathMode={*moatPathMode}.");
                return;
            }

            if (reachabilityLogLimitReported)
                return;
            reachabilityLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat region diagnostics reached their {MaximumReachabilityLogs}-entry limit.");
        }

        private void LogCursorReachabilityChange(
            int movementClass,
            int targetRegion,
            int startX,
            int startY,
            int vanillaResult,
            int effectiveResult,
            bool bypassApplied)
        {
            int targetX = *pathTargetX;
            int targetY = *pathTargetY;
            int pathMode = *moatPathMode;
            bool changed = !hasLastCursorReachabilityState ||
                movementClass != lastCursorMovementClass ||
                startX != lastCursorStartX ||
                startY != lastCursorStartY ||
                targetX != lastCursorTargetX ||
                targetY != lastCursorTargetY ||
                targetRegion != lastCursorTargetRegion ||
                vanillaResult != lastCursorVanillaResult ||
                effectiveResult != lastCursorEffectiveResult ||
                pathMode != lastCursorPathMode;
            if (!changed)
                return;

            hasLastCursorReachabilityState = true;
            lastCursorMovementClass = movementClass;
            lastCursorStartX = startX;
            lastCursorStartY = startY;
            lastCursorTargetX = targetX;
            lastCursorTargetY = targetY;
            lastCursorTargetRegion = targetRegion;
            lastCursorVanillaResult = vanillaResult;
            lastCursorEffectiveResult = effectiveResult;
            lastCursorPathMode = pathMode;

            if (cursorReachabilityLogCount < MaximumCursorReachabilityLogs)
            {
                cursorReachabilityLogCount++;
                TileDiagnostic target = GetTileDiagnostic(targetX, targetY);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=cursor-region-candidate movementClass={movementClass} " +
                    $"start=({startX},{startY}) target=({targetX},{targetY}) " +
                    $"targetTile=[{target}] targetAvailability={GetMovementAvailability(targetX, targetY)} " +
                    $"targetRegionArgument={targetRegion} vanilla={vanillaResult} " +
                    $"effective={effectiveResult} bypass={bypassApplied} pathMode={pathMode}.");
                return;
            }

            if (cursorReachabilityLogLimitReported)
                return;
            cursorReachabilityLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat cursor reachability diagnostics reached their " +
                $"{MaximumCursorReachabilityLogs}-entry limit.");
        }

        private void LogBuilderResult(
            int movementClass,
            int movementProfile,
            int result,
            bool inputStateCaptured,
            BuilderStateSnapshot inputState,
            BuilderStateSnapshot outputState,
            bool routeVariantOverrideApplied,
            bool routeVariantOverrideRetained)
        {
            if (builderLogCount < MaximumBuilderLogs)
            {
                builderLogCount++;
                PlanAttempt attempt = activePlanAttempt ?? pendingMoveHereAttempt;
                if (attempt != null && attempt.TargetX < 0)
                {
                    attempt.TargetX = *pathTargetX;
                    attempt.TargetY = *pathTargetY;
                }
                bool targetMatchesPlan = attempt != null &&
                    attempt.TargetX == *pathTargetX && attempt.TargetY == *pathTargetY;
                string unitState = "unavailable";
                if (attempt != null &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(attempt.UnitId, out GameUnit* unit) && unit != null)
                {
                    unitState = FormatUnitState(unit, attempt.UnitId);
                }
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=builder-function plan={attempt?.Id ?? 0} unit={attempt?.UnitId ?? 0} " +
                    $"movementClass={movementClass} movementProfile={movementProfile} " +
                    $"start=({*pathStartX},{*pathStartY}) target=({*pathTargetX},{*pathTargetY}) " +
                    $"requestedTarget=({attempt?.TargetX ?? -1},{attempt?.TargetY ?? -1}) " +
                    $"result={result} returnedPositive={result > 0} targetMatchesPlan={targetMatchesPlan} " +
                    $"modeSource={GetModeSource(attempt)} pathMode={*moatPathMode} " +
                    $"builderState=[input={(inputStateCaptured ? inputState.ToString() : "unavailable")};" +
                    $"output={outputState};route80Override=" +
                    $"{(routeVariantOverrideApplied ? (routeVariantOverrideRetained ? "retained" : "restored") : "none")}] " +
                    $"directionSeeds=[primaryCalls={attempt?.PrimaryDirectionSeedCalls ?? 0}," +
                    $"primaryResult={attempt?.LastPrimaryDirectionSeedResult ?? -1}," +
                    $"fallbackCalls={attempt?.FallbackDirectionSeedCalls ?? 0}," +
                    $"fallbackResult={attempt?.LastFallbackDirectionSeedResult ?? -1}] " +
                    $"expanders=[standard={attempt?.StandardTileExpanderCalls ?? 0}," +
                    $"moatAware={attempt?.MoatAwareTileExpanderCalls ?? 0}] " +
                    $"moatCandidates=[completed={attempt?.CompletedMoatCandidates ?? 0}," +
                    $"withoutObject={attempt?.CompletedMoatCandidatesWithoutObject ?? 0}," +
                    $"allianceChecks={attempt?.MoatAllianceComparisons ?? 0}," +
                    $"allied={attempt?.AlliedMoatComparisons ?? 0}," +
                    $"enemy={attempt?.EnemyMoatComparisons ?? 0}," +
                    $"invalidOwner={attempt?.InvalidMoatOwnerIndices ?? 0}," +
                    $"lastTile={attempt?.LastCompletedMoatCandidateTileId ?? -1}," +
                    $"lastObject={attempt?.LastCompletedMoatObjectId ?? -1}," +
                    $"lastMovingPlayer={attempt?.LastMovingPlayerIndex ?? -1}," +
                    $"lastMoatOwner={attempt?.LastMoatOwnerIndex ?? -1}," +
                    $"lastMovingGroup={attempt?.LastMovingAllianceGroup ?? -1}," +
                    $"lastMoatGroup={attempt?.LastMoatAllianceGroup ?? -1}] " +
                    $"state=[{unitState}].");
                return;
            }

            if (builderLogLimitReported)
                return;
            builderLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat builder diagnostics reached their {MaximumBuilderLogs}-entry limit.");
        }

        private void LogPlanState(string stage, PlanAttempt attempt, GameUnit* unit, int? result)
        {
            if (planLogCount < MaximumPlanLogs)
            {
                planLogCount++;
                TileDiagnostic requestedTarget = GetTileDiagnostic(attempt.TargetX, attempt.TargetY);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage={stage} plan={attempt.Id} unit={attempt.UnitId} " +
                    $"player={unit->r_ControllableForPlayerId} unitType={(int)unit->r_UnitChimp} " +
                    $"requestedTarget=({attempt.TargetX},{attempt.TargetY}) " +
                    $"requestedTile=[{requestedTarget}] result={(result.HasValue ? result.Value.ToString() : "pending")} " +
                    $"returnedPositive={(result.HasValue ? (result.Value > 0).ToString() : "pending")} " +
                    $"modeSource={GetModeSource(attempt)} pathMode={*moatPathMode} " +
                    $"state=[{FormatUnitState(unit, attempt.UnitId)}].");
                return;
            }

            if (planLogLimitReported)
                return;
            planLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat plan diagnostics reached their {MaximumPlanLogs}-entry limit.");
        }

        private void TrackPlan(PlanAttempt attempt, GameUnit* unit)
        {
            lock (trackingLock)
            {
                if (!mapRuntimeActive)
                    return;

                TrackedPlan tracked = new TrackedPlan(
                    attempt.Id,
                    attempt.UnitId,
                    attempt.TargetX,
                    attempt.TargetY,
                    attempt.Result,
                    mapEpoch,
                    CreateTrackingSignature(unit));
                trackedPlans[attempt.UnitId] = tracked;
            }
        }

        private void ObserveTrackedUnits(int currentTick)
        {
            if (disposed || !mapRuntimeActive)
                return;

            List<TrackedPlan> plans;
            int currentEpoch;
            lock (trackingLock)
            {
                if (!mapRuntimeActive)
                    return;

                currentEpoch = mapEpoch;
                plans = new List<TrackedPlan>(trackedPlans.Values);
            }

            foreach (TrackedPlan tracked in plans)
            {
                bool remove = false;
                try
                {
                    if (!mapRuntimeActive || tracked.MapEpoch != currentEpoch ||
                        currentEpoch != mapEpoch)
                    {
                        lock (trackingLock)
                        {
                            if (trackedPlans.TryGetValue(tracked.UnitId, out TrackedPlan current) &&
                                ReferenceEquals(current, tracked))
                            {
                                trackedPlans.Remove(tracked.UnitId);
                            }
                        }
                        continue;
                    }

                    tracked.AgeTicks++;
                    if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                            tracked.UnitId,
                            out GameUnit* unit) || unit == null)
                    {
                        LogTrackedState(currentTick, tracked, null, "unit-missing");
                        remove = true;
                    }
                    else
                    {
                        string signature = CreateTrackingSignature(unit);
                        bool changed = !string.Equals(
                            signature,
                            tracked.LastSignature,
                            StringComparison.Ordinal);
                        if (changed)
                        {
                            tracked.UnchangedTicks = 0;
                            tracked.LastSignature = signature;
                        }
                        else
                        {
                            tracked.UnchangedTicks++;
                        }

                        bool heartbeat = tracked.AgeTicks == 1 || tracked.AgeTicks == 5 ||
                            tracked.AgeTicks == 15 || tracked.AgeTicks == 30 ||
                            tracked.AgeTicks == 60 || tracked.AgeTicks == MaximumTrackingTicks;
                        if (changed || heartbeat)
                        {
                            LogTrackedState(
                                currentTick,
                                tracked,
                                unit,
                                changed ? "changed" : "heartbeat");
                        }

                        remove = tracked.AgeTicks >= MaximumTrackingTicks;
                    }
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Move Moat Test tick observer failed for plan {tracked.Id}; tracking stops: {ex}");
                    remove = true;
                }

                if (remove)
                {
                    lock (trackingLock)
                    {
                        if (trackedPlans.TryGetValue(tracked.UnitId, out TrackedPlan current) &&
                            ReferenceEquals(current, tracked))
                        {
                            trackedPlans.Remove(tracked.UnitId);
                        }
                    }
                }
            }
        }

        private void LogTrackedState(
            int currentTick,
            TrackedPlan tracked,
            GameUnit* unit,
            string reason)
        {
            if (trackingLogCount < MaximumTrackingLogs)
            {
                trackingLogCount++;
                string state = unit == null ? "unavailable" : FormatUnitState(unit, tracked.UnitId);
                int distance = unit == null
                    ? -1
                    : Math.Abs(unit->r_CurrentTilePositionX - tracked.TargetX) +
                      Math.Abs(unit->r_CurrentTilePositionY - tracked.TargetY);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=unit-tick plan={tracked.Id} tick={currentTick} age={tracked.AgeTicks} " +
                    $"reason={reason} unchangedTicks={tracked.UnchangedTicks} unit={tracked.UnitId} " +
                    $"requestedTarget=({tracked.TargetX},{tracked.TargetY}) distance={distance} " +
                    $"planResult={tracked.Result} state=[{state}].");
                return;
            }

            if (trackingLogLimitReported)
                return;
            trackingLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat unit tracking reached its {MaximumTrackingLogs}-entry limit.");
        }

        private string FormatUnitState(GameUnit* unit, int unitId)
        {
            TileDiagnostic current = GetTileDiagnostic(
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY);
            TileDiagnostic next = GetTileDiagnostic(
                unit->r_NextTilePositionX2,
                unit->r_NextTilePositionY2);
            TileDiagnostic primary = GetTileDiagnostic(
                unit->r_TargetTilePositionX,
                unit->r_TargetTilePositionY);
            TileDiagnostic secondary = GetTileDiagnostic(
                unit->r_TargetTilePositionX2,
                unit->r_TargetTilePositionY2);
            ushort deferredShortening = *(ushort*)((byte*)unit + 0x28C);
            ushort moatMovementMarker = *(ushort*)((byte*)unit + UnitMoatMovementMarkerOffset);
            return
                $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY})[{current}] " +
                $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2})[{next}] " +
                $"primary=({unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY})[{primary}] " +
                $"secondary=({unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2})[{secondary}] " +
                $"previous=({unit->r_PreviousTilePositionX},{unit->r_PreviousTilePositionY}) " +
                $"pathState=0x{unit->r_PathPlanStateBitFlags:X4} moving={unit->r_MovingRelevant} " +
                $"pathPosition={unit->p_CurrentPathPlanPosition} pathSize={unit->p_PathPlanSize} " +
                $"path={FormatPathSummary(unitId, unit)} moatMovementMarker={moatMovementMarker} " +
                $"aiState={unit->r_AIState} " +
                $"speed={unit->r_CurrentSpeed}/{unit->r_CurrentSpeed2} " +
                $"deferredShortening={deferredShortening} linkage={unit->r_PathPlanRelated3}";
        }

        private static string CreateTrackingSignature(GameUnit* unit) =>
            $"{unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY};" +
            $"{unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2};" +
            $"{unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY};" +
            $"{unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2};" +
            $"{unit->r_PathPlanStateBitFlags};{unit->r_MovingRelevant};" +
            $"{unit->p_CurrentPathPlanPosition};{unit->p_PathPlanSize};" +
            $"{unit->r_AIState};{unit->r_CurrentSpeed};{unit->r_CurrentSpeed2};" +
            $"{unit->r_PathPlanRelated3};" +
            $"{*(ushort*)((byte*)unit + UnitMoatMovementMarkerOffset)}";

        private string FormatPathSummary(int unitId, GameUnit* unit)
        {
            int pathSize = unchecked((int)unit->p_PathPlanSize);
            int start = unit->p_CurrentPathPlanPosition;
            if (unitId <= 0 || pathSize <= 0 || start < 0 || start >= pathSize ||
                pathSize > MaximumPackedPathEntries)
            {
                return "[]";
            }

            byte* manager = (byte*)GameUnitManagerAPI.Instance.GetUnitManager().Pointer;
            // Vanilla stores two four-bit directions per byte in a manager-side buffer,
            // not inline in GameUnit. Decode it only for bounded diagnostics.
            byte* packedPath = manager + UnitPathBufferOffset + unitId * UnitPathBufferSize;
            int x = unit->r_CurrentTilePositionX;
            int y = unit->r_CurrentTilePositionY;
            int moatCount = 0;
            int firstMoatIndex = -1;
            bool invalid = false;
            const int maximumPreviewEntries = 12;
            StringBuilder preview = new StringBuilder();

            for (int index = start; index < pathSize; index++)
            {
                byte packed = packedPath[index >> 1];
                int direction = (index & 1) == 0 ? packed & 0xF : packed >> 4;
                if (direction > 7 || !TryResolveNextCoordinate(x, y, direction, out int nextX, out int nextY))
                {
                    invalid = true;
                    if (index - start < maximumPreviewEntries)
                    {
                        if (preview.Length > 0)
                            preview.Append(',');
                        preview.Append(index).Append(':').Append(direction).Append("->invalid");
                    }
                    break;
                }

                x = nextX;
                y = nextY;
                TileDiagnostic tile = GetTileDiagnostic(x, y);
                bool isMoat = (tile.Flags & CompletedMoatTileFlag) != 0;
                if (isMoat)
                {
                    moatCount++;
                    if (firstMoatIndex < 0)
                        firstMoatIndex = index;
                }

                if (index - start < maximumPreviewEntries)
                {
                    if (preview.Length > 0)
                        preview.Append(',');
                    preview.Append(index).Append(':').Append(direction).Append("->(")
                        .Append(x).Append(',').Append(y).Append(')');
                    if (isMoat)
                        preview.Append('M');
                }
            }

            return $"[{preview}] remaining={pathSize - start} end=({x},{y}) " +
                $"moatCount={moatCount} firstMoatIndex={firstMoatIndex} " +
                $"previewTruncated={pathSize - start > maximumPreviewEntries} invalid={invalid}";
        }

        private bool TryResolveNextCoordinate(
            int currentX,
            int currentY,
            int direction,
            out int nextX,
            out int nextY)
        {
            nextX = currentX;
            nextY = currentY;
            if (currentX < 0 || currentX >= 800 || currentY < 0 || currentY >= 800)
                return false;

            int currentTileId = GameTileManagerAPI.Instance.GetTileId(currentX, currentY);
            int nextTileId = currentTileId + directionTileOffsets[currentY * 8 + direction];
            for (int deltaY = -1; deltaY <= 1; deltaY++)
            {
                for (int deltaX = -1; deltaX <= 1; deltaX++)
                {
                    if (deltaX == 0 && deltaY == 0)
                        continue;
                    int candidateX = currentX + deltaX;
                    int candidateY = currentY + deltaY;
                    if (candidateX < 0 || candidateX >= 800 || candidateY < 0 || candidateY >= 800)
                        continue;
                    if (GameTileManagerAPI.Instance.GetTileId(candidateX, candidateY) == nextTileId)
                    {
                        nextX = candidateX;
                        nextY = candidateY;
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsValidTileId(int tileId) => tileId >= 0 && tileId < 320800;

        private int FindDirection(int currentTileId, int nextTileId, int pathRow)
        {
            if (pathRow < 0 || pathRow >= 800)
                return -1;
            for (int direction = 0; direction < 8; direction++)
            {
                if (currentTileId + directionTileOffsets[pathRow * 8 + direction] == nextTileId)
                    return direction;
            }
            return -1;
        }

        private static string GetModeSource(PlanAttempt attempt) =>
            attempt == null ? "unknown" :
            attempt.VanillaModeDetected ? "vanilla-natural" : "forced";

        private void ReportPlanLogLimitOnce()
        {
            if (planLogLimitReported)
                return;
            planLogLimitReported = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"MoveMoat plan diagnostics reached their {MaximumPlanLogs}-entry limit.");
        }

        private TileDiagnostic GetTileDiagnostic(int x, int y)
        {
            if (x < 0 || x >= 800 || y < 0 || y >= 800)
                return TileDiagnostic.Invalid;

            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (tileId < 0 || tileId >= 320800)
                return TileDiagnostic.Invalid;
            return new TileDiagnostic(tileId, tileFlags[tileId], pathRegionGrid[tileId]);
        }

        private int GetMovementAvailability(int x, int y)
        {
            if (x < 0 || x >= 800 || y < 0 || y >= 800)
                return -1;
            return movementTargetAvailability[(y * 800) + x];
        }

        private static NativeDetour CreateDetour<TDelegate>(ulong targetAddress, TDelegate callback)
            where TDelegate : Delegate =>
            new NativeDetour(
                (IntPtr)unchecked((long)targetAddress),
                Marshal.GetFunctionPointerForDelegate(callback),
                new NativeDetourConfig { ManualApply = true });

        private static int ResolveGlobalRva(
            ReadOnlySpan<byte> memory,
            int displacementRva,
            int nextInstructionRva,
            string label)
        {
            int resolvedRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                displacementRva,
                nextInstructionRva);
            if (resolvedRva < 0 || resolvedRva > memory.Length - sizeof(int))
                throw new InvalidOperationException($"The native {label} global is outside CrusaderDE.dll.");
            return resolvedRva;
        }

        private static void RequireValidatedRva(
            Shared.NativeResolution resolution,
            int expectedRva,
            string label)
        {
            if (resolution.Rva != expectedRva)
            {
                throw new InvalidOperationException(
                    $"The native {label} resolved to 0x{resolution.Rva:X} instead of validated RVA 0x{expectedRva:X}.");
            }
        }

        private static void ValidatePatternSpans(ReadOnlySpan<byte> memory)
        {
            ValidatePatternSpan(
                memory,
                CursorReachabilityPatternRva,
                CursorReachabilityPattern,
                "ordinary-movement cursor reachability caller");
            ValidatePatternSpan(
                memory,
                CursorForbiddenResultRva,
                CursorForbiddenResultPattern,
                "ordinary-movement forbidden-cursor result block");
            ValidatePatternSpan(
                memory,
                CursorReachabilityRva,
                CursorReachabilityFunctionPattern,
                "ordinary-movement cursor reachability function");
            ValidatePatternSpan(
                memory,
                CursorSpecialModeRva,
                CursorSpecialModePattern,
                "ordinary-movement cursor special-mode precheck");
            ValidatePatternSpan(
                memory,
                CursorRegionPrecheckRva,
                CursorRegionPrecheckPattern,
                "ordinary-movement cursor region precheck");
            ValidatePatternSpan(
                memory,
                CommonPathRequestRva,
                CommonPathRequestPattern,
                "shared common path request");
            ValidatePatternSpan(
                memory,
                CentralMovementPlanRva,
                CentralMovementPlanPattern,
                "central ordinary-movement planner");
            ValidatePatternSpan(
                memory,
                TribeFloodFillMembershipRva,
                TribeFloodFillMembershipPattern,
                "Tribe flood-fill membership helper");
            ValidatePatternSpan(
                memory,
                TribeMovementPrecheckRva,
                TribeMovementPrecheckPattern,
                "Tribe MoveHere target and region precheck");
            ValidatePatternSpan(
                memory,
                TribeFormationTargetResultRva,
                TribeFormationTargetResultPattern,
                "Tribe formation-target helper result");
            ValidatePatternSpan(
                memory,
                TribeRegionCandidateRetryRva,
                TribeRegionCandidateRetryPattern,
                "Tribe region-candidate retry");
            ValidatePatternSpan(
                memory,
                TribeUnitScanStartRva,
                TribeUnitScanStartPattern,
                "Tribe unit-scan entry");
            ValidatePatternSpan(
                memory,
                TribeEarlyReturnRva,
                TribeEarlyReturnPattern,
                "Tribe MoveHere central return");
            ValidatePatternSpan(
                memory,
                TribeUnitIterationEndRva,
                TribeUnitIterationEndPattern,
                "Tribe unit-iteration end");
            ValidatePatternSpan(
                memory,
                DetectCompletedMoatModeRva,
                DetectCompletedMoatModePattern,
                "completed-moat path-mode detector");
            ValidatePatternSpan(
                memory,
                RegionReachabilityRva,
                RegionReachabilityPattern,
                "moat-aware region reachability");
            ValidatePatternSpan(
                memory,
                PrimaryDirectionSeedBuilderRva,
                PrimaryDirectionSeedBuilderPattern,
                "primary direction-seed builder");
            ValidatePatternSpan(
                memory,
                FallbackDirectionSeedBuilderRva,
                FallbackDirectionSeedBuilderPattern,
                "fallback direction-seed builder");
            ValidatePatternSpan(
                memory,
                PathBuilderRva,
                PathBuilderPattern,
                "central tile path builder");
            ValidatePatternSpan(
                memory,
                StandardTileExpanderRva,
                StandardTileExpanderPattern,
                "standard tile expander");
            ValidatePatternSpan(
                memory,
                MoatAwareTileExpanderRva,
                MoatAwareTileExpanderPattern,
                "moat-aware tile expander");
            ValidatePatternSpan(
                memory,
                MoatAwareCandidateResultRva,
                MoatAwareCandidateResultPattern,
                "moat-aware completed-moat candidate result");
            ValidatePatternSpan(
                memory,
                MoatAwareAllianceComparisonRva,
                MoatAwareAllianceComparisonPattern,
                "moat-aware alliance comparison");
        }

        private static void ValidateInlineHookSpans(ReadOnlySpan<byte> memory)
        {
            ValidateExactBytes(
                memory,
                CursorForbiddenResultRva,
                new byte[]
                {
                    0xC7, 0x05, 0x7C, 0xE1, 0x01, 0x06, 0xF6, 0xFF, 0xFF, 0xFF,
                    0x41, 0xBD, 0x04, 0x00, 0x00, 0x00,
                    0xC7, 0x05, 0x54, 0xE1, 0x01, 0x06, 0x41, 0x00, 0x00, 0x00
                },
                "ordinary-movement forbidden-cursor result block");
            ValidateExactBytes(
                memory,
                TribeMovementPrecheckRva,
                new byte[]
                {
                    0x48, 0x8D, 0x04, 0x8D, 0x00, 0x00, 0x00, 0x00,
                    0x42, 0xF6, 0x84, 0x18, 0xB0, 0x71, 0x8F, 0x04, 0x30,
                    0x48, 0x89, 0x44, 0x24, 0x68
                },
                "Tribe MoveHere target and region precheck");
            ValidateExactBytes(
                memory,
                TribeFormationTargetResultRva,
                new byte[]
                {
                    0x44, 0x8B, 0xBC, 0x24, 0xC8, 0x00, 0x00, 0x00,
                    0x48, 0x8B, 0xCF, 0x44, 0x8B, 0x05, 0x39, 0x1D,
                    0xF9, 0x05
                },
                "Tribe formation-target helper result");
            ValidateExactBytes(
                memory,
                TribeRegionCandidateRetryRva,
                new byte[]
                {
                    0x44, 0x8B, 0x2D, 0x51, 0x7C, 0x0E, 0x06,
                    0x48, 0x8D, 0x0D, 0x12, 0x1D, 0xF9, 0x05
                },
                "Tribe region-candidate retry");
            ValidateExactBytes(
                memory,
                TribeUnitScanStartRva,
                new byte[]
                {
                    0x66, 0x3B, 0x48, 0x5C, 0x0F, 0x8D, 0xAD, 0x01,
                    0x00, 0x00, 0x44, 0x8B, 0xF9, 0x8B, 0x94, 0x24,
                    0xC8, 0x00, 0x00, 0x00
                },
                "Tribe unit-scan entry");
            ValidateExactBytes(
                memory,
                TribeEarlyReturnRva,
                new byte[]
                {
                    0x48, 0x8B, 0x9C, 0x24, 0xC0, 0x00, 0x00, 0x00,
                    0x48, 0x81, 0xC4, 0x80, 0x00, 0x00, 0x00
                },
                "Tribe MoveHere central return");
            ValidateExactBytes(
                memory,
                TribeUnitIterationEndRva,
                new byte[]
                {
                    0x33, 0xED, 0x39, 0x2D, 0x85, 0x15, 0xF9, 0x05,
                    0x89, 0x2D, 0x83, 0x15, 0xF9, 0x05
                },
                "Tribe unit-iteration end");
            ValidateExactBytes(
                memory,
                MovementStepMoatGateRva,
                new byte[]
                {
                    0x83, 0x7C, 0x24, 0x70, 0x00, 0x0F, 0x85, 0xFC, 0x00,
                    0x00, 0x00, 0x49, 0x69, 0xEB, 0x90, 0x04, 0x00, 0x00
                },
                "central completed-moat movement-step gate");
            ValidateExactBytes(
                memory,
                MoveHereBuilderResultRva,
                new byte[]
                {
                    0x45, 0x33, 0xC0, 0x44, 0x89, 0x05, 0x58, 0x70, 0xF1,
                    0x05, 0x85, 0xC0, 0x0F, 0x8E, 0xA4, 0x00, 0x00, 0x00
                },
                "MoveHere builder-result commit gate");
            ValidateExactBytes(
                memory,
                StandardTileExpanderRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x10, 0x55, 0x56, 0x57,
                    0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57
                },
                "standard tile-expander entry");
            ValidateExactBytes(
                memory,
                MoatAwareTileExpanderRva,
                new byte[]
                {
                    0x48, 0x89, 0x5C, 0x24, 0x08,
                    0x48, 0x89, 0x6C, 0x24, 0x10,
                    0x48, 0x89, 0x74, 0x24, 0x18
                },
                "moat-aware tile-expander entry");
            ValidateExactBytes(
                memory,
                MoatAwareCandidateResultRva,
                new byte[]
                {
                    0x85, 0xC0, 0x74, 0x24, 0x48, 0x98, 0x48, 0x03, 0xC0,
                    0x49, 0x0F, 0xBE, 0x8C, 0xC1, 0x3C, 0xEE, 0xF3, 0x01
                },
                "moat-aware completed-moat candidate result");
            ValidateExactBytes(
                memory,
                MoatAwareAllianceComparisonRva,
                new byte[]
                {
                    0x41, 0x39, 0x84, 0x88, 0x3C, 0xDF, 0x7E, 0x03,
                    0x0F, 0x84, 0x82, 0x05, 0x00, 0x00
                },
                "moat-aware alliance comparison");
        }

        private static void ValidateExactBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            byte[] expected,
            string label)
        {
            if (rva < 0 || rva > memory.Length - expected.Length ||
                !memory.Slice(rva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    $"The validated instruction span for {label} did not match CrusaderDE.dll.");
            }
        }

        private static void ValidatePatternSpan(
            ReadOnlySpan<byte> memory,
            int rva,
            string pattern,
            string label)
        {
            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, rva, pattern))
            {
                throw new InvalidOperationException(
                    $"The complete validated pattern span for {label} did not match CrusaderDE.dll.");
            }
        }

        private sealed class PlanAttempt
        {
            public PlanAttempt(long id, int unitId, int targetX, int targetY)
            {
                Id = id;
                UnitId = unitId;
                TargetX = targetX;
                TargetY = targetY;
                LastPrimaryDirectionSeedResult = -1;
                LastFallbackDirectionSeedResult = -1;
                LastCompletedMoatCandidateTileId = -1;
                LastCompletedMoatObjectId = -1;
                LastMovingPlayerIndex = -1;
                LastMoatOwnerIndex = -1;
                LastMovingAllianceGroup = -1;
                LastMoatAllianceGroup = -1;
            }

            public long Id { get; }
            public int UnitId { get; }
            public int TargetX { get; set; }
            public int TargetY { get; set; }
            public int Result { get; set; }
            public int PrimaryDirectionSeedCalls { get; set; }
            public int LastPrimaryDirectionSeedResult { get; set; }
            public int FallbackDirectionSeedCalls { get; set; }
            public int LastFallbackDirectionSeedResult { get; set; }
            public int StandardTileExpanderCalls { get; set; }
            public int MoatAwareTileExpanderCalls { get; set; }
            public int CompletedMoatCandidates { get; set; }
            public int CompletedMoatCandidatesWithoutObject { get; set; }
            public int MoatAllianceComparisons { get; set; }
            public int AlliedMoatComparisons { get; set; }
            public int EnemyMoatComparisons { get; set; }
            public int InvalidMoatOwnerIndices { get; set; }
            public int LastCompletedMoatCandidateTileId { get; set; }
            public int LastCompletedMoatObjectId { get; set; }
            public int LastMovingPlayerIndex { get; set; }
            public int LastMoatOwnerIndex { get; set; }
            public int LastMovingAllianceGroup { get; set; }
            public int LastMoatAllianceGroup { get; set; }
            public bool VanillaModeDetected { get; set; }
        }

        private readonly struct BuilderStateSnapshot
        {
            public BuilderStateSnapshot(
                int offset7C,
                int offset80,
                int offset84,
                int offset88,
                int offset94,
                int offsetA8,
                int offsetAC,
                int pathLength)
            {
                Offset7C = offset7C;
                Offset80 = offset80;
                Offset84 = offset84;
                Offset88 = offset88;
                Offset94 = offset94;
                OffsetA8 = offsetA8;
                OffsetAC = offsetAC;
                PathLength = pathLength;
            }

            public int Offset7C { get; }
            public int Offset80 { get; }
            public int Offset84 { get; }
            public int Offset88 { get; }
            public int Offset94 { get; }
            public int OffsetA8 { get; }
            public int OffsetAC { get; }
            public int PathLength { get; }

            public override string ToString() =>
                $"7C={Offset7C},80={Offset80},84={Offset84},88={Offset88}," +
                $"94={Offset94},A8={OffsetA8},AC={OffsetAC},length={PathLength}";
        }

        private sealed class CommandAttempt
        {
            public CommandAttempt(long id, int tribeId, int targetX, int targetY)
            {
                Id = id;
                TribeId = tribeId;
                TargetX = targetX;
                TargetY = targetY;
                RegionVanillaResult = -1;
                RegionEffectiveResult = -1;
                FormationTargetResult = int.MinValue;
                LastFloodFillStamp = -1;
                LastFloodFillVanillaResult = -1;
                LastFloodFillEffectiveResult = -1;
                LastNativeStage = "tribe-order-pre";
            }

            public long Id { get; }
            public int TribeId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int TargetTileId { get; set; }
            public int TargetAvailability { get; set; }
            public int RepresentativeUnitId { get; set; }
            public int StartRegion { get; set; }
            public int TargetRegion { get; set; }
            public int RegionVanillaResult { get; set; }
            public int RegionEffectiveResult { get; set; }
            public int FormationTargetResult { get; set; }
            public int FloodFillMembershipCalls { get; set; }
            public int FloodFillMembershipBypasses { get; set; }
            public int LastFloodFillStamp { get; set; }
            public int LastFloodFillVanillaResult { get; set; }
            public int LastFloodFillEffectiveResult { get; set; }
            public int RegionCandidateRetries { get; set; }
            public int UnitIterations { get; set; }
            public string LastNativeStage { get; set; }
            public bool NativePrecheckObserved { get; set; }
            public bool RegionObserved { get; set; }
            public bool UnitScanObserved { get; set; }
            public bool UnitMoveObserved { get; set; }
        }

        private sealed class TrackedPlan
        {
            public TrackedPlan(
                long id,
                int unitId,
                int targetX,
                int targetY,
                int result,
                int mapEpoch,
                string lastSignature)
            {
                Id = id;
                UnitId = unitId;
                TargetX = targetX;
                TargetY = targetY;
                Result = result;
                MapEpoch = mapEpoch;
                LastSignature = lastSignature;
            }

            public long Id { get; }
            public int UnitId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int Result { get; }
            public int MapEpoch { get; }
            public int AgeTicks { get; set; }
            public int UnchangedTicks { get; set; }
            public string LastSignature { get; set; }
        }

        private readonly struct TileDiagnostic
        {
            public static readonly TileDiagnostic Invalid = new TileDiagnostic(-1, 0, -1);

            public TileDiagnostic(int tileId, uint flags, int region)
            {
                TileId = tileId;
                Flags = flags;
                Region = region;
            }

            public int TileId { get; }
            public uint Flags { get; }
            public int Region { get; }

            public override string ToString() =>
                $"tile={TileId},flags=0x{Flags:X8},region={Region},completedMoat={(Flags & CompletedMoatTileFlag) != 0}";
        }
    }
}
