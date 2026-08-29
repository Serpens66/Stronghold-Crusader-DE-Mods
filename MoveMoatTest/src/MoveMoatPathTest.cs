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
        private delegate int TribeRegionMembershipDelegate(
            IntPtr tribeManager,
            int tribeId,
            int targetRegion);

        private const int CentralMovementPlanRva = 0x18E1E0;
        private const int TribeRegionMembershipRva = 0x124740;
        private const int TribeMovementPrecheckRva = 0x11B637;
        private const int TribeFormationTargetResultRva = 0x11B919;
        private const int TribeRegionCandidateRetryRva = 0x11B940;
        private const int TribeUnitScanStartRva = 0x11B9D6;
        private const int TribeEarlyReturnRva = 0x11BDF4;
        private const int TribeUnitIterationEndRva = 0x11C14F;
        private const int MovementStepMoatGateRva = 0xDCEF2;
        private const int DetectCompletedMoatModeRva = 0x196840;
        private const int RegionReachabilityRva = 0xE7C40;
        private const int PathBuilderRva = 0xF4930;
        private const int MoveHereBuilderResultRva = 0x19667E;
        private const int TileFlagsRva = 0x48F71B0;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int PathRegionGridRva = 0x50EC690;
        private const int DirectionTileOffsetTableRva = 0x405EDB0;
        private const int MoatPathModeRva = 0x60AD6E4;
        private const int PathStartXRva = 0x60AD668;
        private const int PathStartYRva = 0x60AD66C;
        private const int PathTargetXRva = 0x60AD670;
        private const int PathTargetYRva = 0x60AD674;
        private const int MaximumRegionId = short.MaxValue;
        private const int MaximumModeLogs = 24;
        private const int MaximumReachabilityLogs = 96;
        private const int MaximumBuilderLogs = 96;
        private const int MaximumPlanLogs = 96;
        private const int MaximumTrackingLogs = 256;
        private const int MaximumStepGateLogs = 128;
        private const int MaximumCommandLogs = 128;
        private const int MaximumTrackingTicks = 120;
        private const int MovementStepMoatGateHookLength = 18;
        private const int MoveHereBuilderResultHookLength = 18;
        private const int TribeMovementPrecheckHookLength = 22;
        private const int TribeFormationTargetResultHookLength = 18;
        private const int TribeRegionCandidateRetryHookLength = 14;
        private const int TribeUnitScanStartHookLength = 20;
        private const int TribeEarlyReturnHookLength = 15;
        private const int TribeUnitIterationEndHookLength = 14;
        private const int UnitPathBufferOffset = 0xB4FE78;
        private const int UnitPathBufferSize = 1000;
        private const int MaximumPackedPathEntries = UnitPathBufferSize * 2;
        private const int UnitMoatMovementMarkerOffset = 0x36C;
        private const uint CompletedMoatTileFlag = 0x40000000;

        private const string CentralMovementPlanPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 81 EC 38 04 00 00 " +
            "48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 20 04 00 00 4C 63 FA " +
            "4C 8D 35 ?? ?? ?? ?? 49 69 DF 90 04 00 00 49 63 E8 48 03 D9 49 63 F1";

        private const string TribeRegionMembershipPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 " +
            "48 83 EC 20 48 63 F2 33 DB 4C 69 CE 88 06 00 00 45 8B F0 48 8B E9";

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
        private readonly int* pathStartX;
        private readonly int* pathStartY;
        private readonly int* pathTargetX;
        private readonly int* pathTargetY;
        private readonly uint* tileFlags;
        private readonly byte* movementTargetAvailability;
        private readonly short* pathRegionGrid;
        private readonly int* directionTileOffsets;
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
        private TribeRegionMembershipDelegate originalTribeRegionMembership;
        private TribeRegionMembershipDelegate rootedTribeRegionMembership;
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
        private NativeDetour tribeRegionMembershipDetour;
        private HookRef<X64InlineHook> movementStepMoatGateHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moveHereBuilderResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeMovementPrecheckHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeFormationTargetResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeRegionCandidateRetryHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeUnitScanStartHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeEarlyReturnHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> tribeUnitIterationEndHook = new HookRef<X64InlineHook>();
        private IDisposable tribeMoveSubscription;
        private IDisposable unitMoveSubscription;
        private long nextPlanId;
        private long nextCommandId;
        private int modeLogCount;
        private int reachabilityLogCount;
        private int builderLogCount;
        private int planLogCount;
        private int trackingLogCount;
        private int stepGateLogCount;
        private int commandLogCount;
        private bool modeLogLimitReported;
        private bool reachabilityLogLimitReported;
        private bool builderLogLimitReported;
        private bool planLogLimitReported;
        private bool trackingLogLimitReported;
        private bool stepGateLogLimitReported;
        private bool commandLogLimitReported;
        private bool tickSubscribed;
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
            Shared.NativeResolution modeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DetectCompletedMoatModePattern,
                DetectCompletedMoatModeRva,
                referenceHashMatches,
                "completed-moat path-mode detector",
                log: null);
            Shared.NativeResolution tribeRegionMembershipResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TribeRegionMembershipPattern,
                TribeRegionMembershipRva,
                referenceHashMatches,
                "Tribe target-region membership helper",
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
            Shared.NativeResolution builderResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                PathBuilderPattern,
                PathBuilderRva,
                referenceHashMatches,
                "central tile path builder",
                log: null);

            RequireValidatedRva(planResolution, CentralMovementPlanRva, "central ordinary-movement planner");
            RequireValidatedRva(
                tribeRegionMembershipResolution,
                TribeRegionMembershipRva,
                "Tribe target-region membership helper");
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
            RequireValidatedRva(builderResolution, PathBuilderRva, "central tile path builder");
            ValidatePatternSpans(memory);
            ValidateInlineHookSpans(memory);

            moatPathMode = (int*)(libraryBase + MoatPathModeRva);
            pathStartX = (int*)(libraryBase + PathStartXRva);
            pathStartY = (int*)(libraryBase + PathStartYRva);
            pathTargetX = (int*)(libraryBase + PathTargetXRva);
            pathTargetY = (int*)(libraryBase + PathTargetYRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);
            movementTargetAvailability = (byte*)(libraryBase + MovementTargetAvailabilityRva);
            pathRegionGrid = (short*)(libraryBase + PathRegionGridRva);
            directionTileOffsets = (int*)(libraryBase + DirectionTileOffsetTableRva);

            rootedCentralMovementPlan = ObserveCentralMovementPlan;
            rootedTribeRegionMembership = AllowTribeTargetRegionForMoveOrder;
            rootedDetectCompletedMoatMode = ForceCompletedMoatMode;
            rootedRegionReachability = AllowBuilderAfterFailedRegionSearch;
            rootedPathBuilder = ObservePathBuilder;

            NativeDetour pendingModeDetour = null;
            NativeDetour pendingReachabilityDetour = null;
            NativeDetour pendingBuilderDetour = null;
            NativeDetour pendingPlanDetour = null;
            NativeDetour pendingTribeRegionMembershipDetour = null;
            bool planApplied = false;
            bool tribeRegionMembershipApplied = false;
            bool modeApplied = false;
            bool reachabilityApplied = false;
            bool builderApplied = false;
            try
            {
                pendingPlanDetour = CreateDetour(
                    libraryBase + unchecked((ulong)planResolution.Rva),
                    rootedCentralMovementPlan);
                originalCentralMovementPlan =
                    pendingPlanDetour.GenerateTrampoline<CentralMovementPlanDelegate>();

                pendingTribeRegionMembershipDetour = CreateDetour(
                    libraryBase + unchecked((ulong)tribeRegionMembershipResolution.Rva),
                    rootedTribeRegionMembership);
                originalTribeRegionMembership =
                    pendingTribeRegionMembershipDetour.GenerateTrampoline<TribeRegionMembershipDelegate>();

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

                pendingPlanDetour.Apply();
                planApplied = true;
                pendingTribeRegionMembershipDetour.Apply();
                tribeRegionMembershipApplied = true;
                pendingModeDetour.Apply();
                modeApplied = true;
                pendingReachabilityDetour.Apply();
                reachabilityApplied = true;
                pendingBuilderDetour.Apply();
                builderApplied = true;

                detectCompletedMoatModeDetour = pendingModeDetour;
                regionReachabilityDetour = pendingReachabilityDetour;
                pathBuilderDetour = pendingBuilderDetour;
                centralMovementPlanDetour = pendingPlanDetour;
                tribeRegionMembershipDetour = pendingTribeRegionMembershipDetour;

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
                    ref moveHereBuilderResultHook,
                    libraryBase + MoveHereBuilderResultRva,
                    RecordMoveHereBuilderResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoveHereBuilderResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref tribeMovementPrecheckHook,
                    libraryBase + unchecked((ulong)tribePrecheckResolution.Rva),
                    ObserveTribeMovementPrecheck,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: TribeMovementPrecheckHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref tribeFormationTargetResultHook,
                    libraryBase + unchecked((ulong)tribeFormationResultResolution.Rva),
                    ObserveTribeFormationTargetResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: TribeFormationTargetResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref tribeRegionCandidateRetryHook,
                    libraryBase + unchecked((ulong)tribeRegionCandidateRetryResolution.Rva),
                    ObserveTribeRegionCandidateRetry,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: TribeRegionCandidateRetryHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref tribeUnitScanStartHook,
                    libraryBase + unchecked((ulong)tribeUnitScanResolution.Rva),
                    ObserveTribeUnitScanStart,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: TribeUnitScanStartHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref tribeUnitIterationEndHook,
                    libraryBase + unchecked((ulong)tribeUnitIterationEndResolution.Rva),
                    ObserveTribeUnitIterationEnd,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: TribeUnitIterationEndHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref tribeEarlyReturnHook,
                    libraryBase + unchecked((ulong)tribeEarlyReturnResolution.Rva),
                    ObserveTribeEarlyReturn,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: TribeEarlyReturnHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();
                if (!movementStepMoatGateHook.Success || !moveHereBuilderResultHook.Success ||
                    !tribeMovementPrecheckHook.Success || !tribeFormationTargetResultHook.Success ||
                    !tribeRegionCandidateRetryHook.Success || !tribeUnitScanStartHook.Success ||
                    !tribeUnitIterationEndHook.Success || !tribeEarlyReturnHook.Success)
                    throw new InvalidOperationException("A central MoveHere diagnostic hook did not install.");

                tribeMoveSubscription = TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                    .Subscribe(ObserveTribeMoveOrder);
                unitMoveSubscription = UnitR3EventHooks.OnUnitMoveHere.Observable
                    .Subscribe(ObserveUnitMoveOrder);

                GameTimeManagerAPI.Instance.OnTick += ObserveTrackedUnits;
                tickSubscribed = true;

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Move Moat Test installed: planRva=0x{planResolution.Rva:X}/method={planResolution.Method}, " +
                    $"modeRva=0x{modeResolution.Rva:X}/method={modeResolution.Method}, " +
                    $"tribeRegionMembershipRva=0x{tribeRegionMembershipResolution.Rva:X}/method=" +
                    $"{tribeRegionMembershipResolution.Method}, " +
                    $"reachabilityRva=0x{reachabilityResolution.Rva:X}/method={reachabilityResolution.Method}, " +
                    $"builderRva=0x{builderResolution.Rva:X}/method={builderResolution.Method}, " +
                    $"tribePrecheckRva=0x{tribePrecheckResolution.Rva:X}/method={tribePrecheckResolution.Method}, " +
                    "tribeDispatcherBreadcrumbs=6, " +
                    $"moveHereResultRva=0x{MoveHereBuilderResultRva:X}, " +
                    $"stepGateRva=0x{MovementStepMoatGateRva:X}; " +
                    "allCompletedMoats=true, ownerFiltering=false, " +
                    "tribeRegionBypass=activeMoveHereOnly, realBuilderResultUnchanged=true.");
            }
            catch
            {
                if (tickSubscribed)
                {
                    GameTimeManagerAPI.Instance.OnTick -= ObserveTrackedUnits;
                    tickSubscribed = false;
                }
                moveHereBuilderResultHook?.Value?.Dispose();
                movementStepMoatGateHook?.Value?.Dispose();
                tribeMovementPrecheckHook?.Value?.Dispose();
                tribeFormationTargetResultHook?.Value?.Dispose();
                tribeRegionCandidateRetryHook?.Value?.Dispose();
                tribeUnitScanStartHook?.Value?.Dispose();
                tribeUnitIterationEndHook?.Value?.Dispose();
                tribeEarlyReturnHook?.Value?.Dispose();
                tribeMoveSubscription?.Dispose();
                unitMoveSubscription?.Dispose();
                tribeMoveSubscription = null;
                unitMoveSubscription = null;
                if (builderApplied)
                    pendingBuilderDetour?.Undo();
                pendingBuilderDetour?.Dispose();
                if (reachabilityApplied)
                    pendingReachabilityDetour?.Undo();
                pendingReachabilityDetour?.Dispose();
                if (modeApplied)
                    pendingModeDetour?.Undo();
                pendingModeDetour?.Dispose();
                if (tribeRegionMembershipApplied)
                    pendingTribeRegionMembershipDetour?.Undo();
                pendingTribeRegionMembershipDetour?.Dispose();
                if (planApplied)
                    pendingPlanDetour?.Undo();
                pendingPlanDetour?.Dispose();
                originalCentralMovementPlan = null;
                originalTribeRegionMembership = null;
                originalDetectCompletedMoatMode = null;
                originalRegionReachability = null;
                originalPathBuilder = null;
                rootedCentralMovementPlan = null;
                rootedTribeRegionMembership = null;
                rootedDetectCompletedMoatMode = null;
                rootedRegionReachability = null;
                rootedPathBuilder = null;
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
            movementStepMoatGateHook?.Value?.Dispose();
            tribeMovementPrecheckHook?.Value?.Dispose();
            tribeFormationTargetResultHook?.Value?.Dispose();
            tribeRegionCandidateRetryHook?.Value?.Dispose();
            tribeUnitScanStartHook?.Value?.Dispose();
            tribeUnitIterationEndHook?.Value?.Dispose();
            tribeEarlyReturnHook?.Value?.Dispose();
            tribeMoveSubscription?.Dispose();
            unitMoveSubscription?.Dispose();
            tribeMoveSubscription = null;
            unitMoveSubscription = null;
            pathBuilderDetour?.Dispose();
            regionReachabilityDetour?.Dispose();
            detectCompletedMoatModeDetour?.Dispose();
            tribeRegionMembershipDetour?.Dispose();
            centralMovementPlanDetour?.Dispose();
            pathBuilderDetour = null;
            regionReachabilityDetour = null;
            detectCompletedMoatModeDetour = null;
            tribeRegionMembershipDetour = null;
            centralMovementPlanDetour = null;
            originalCentralMovementPlan = null;
            originalTribeRegionMembership = null;
            originalPathBuilder = null;
            originalRegionReachability = null;
            originalDetectCompletedMoatMode = null;
            rootedCentralMovementPlan = null;
            rootedTribeRegionMembership = null;
            rootedPathBuilder = null;
            rootedRegionReachability = null;
            rootedDetectCompletedMoatMode = null;
            lock (trackingLock)
                trackedPlans.Clear();
        }

        private void ObserveTribeMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (disposed)
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
                    int availability = IsValidTileId(target.TileId)
                        ? movementTargetAvailability[target.TileId]
                        : -1;
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
                    $"regionMembershipCalls={current?.RegionMembershipCalls ?? 0} " +
                    $"regionMembershipBypasses={current?.RegionMembershipBypasses ?? 0} " +
                    $"lastMembershipRegion={current?.LastRegionMembershipTarget ?? -1} " +
                    $"lastMembershipVanilla={current?.LastRegionMembershipVanillaResult ?? -1} " +
                    $"lastMembershipEffective={current?.LastRegionMembershipEffectiveResult ?? -1} " +
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
            if (disposed)
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
                    $"targetAvailability={(IsValidTileId(targetTileId) ? movementTargetAvailability[targetTileId] : -1)} " +
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
                    $"membershipCalls={command.RegionMembershipCalls} " +
                    $"lastMembershipVanilla={command.LastRegionMembershipVanillaResult} " +
                    $"lastMembershipEffective={command.LastRegionMembershipEffectiveResult}");
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
                    $"membershipCalls={command.RegionMembershipCalls} " +
                    $"membershipBypasses={command.RegionMembershipBypasses}");
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
                    $"membershipCalls={command.RegionMembershipCalls} " +
                    $"membershipBypasses={command.RegionMembershipBypasses} " +
                    $"lastMembershipRegion={command.LastRegionMembershipTarget} " +
                    $"lastMembershipVanilla={command.LastRegionMembershipVanillaResult} " +
                    $"lastMembershipEffective={command.LastRegionMembershipEffectiveResult} " +
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

        private int AllowTribeTargetRegionForMoveOrder(
            IntPtr tribeManager,
            int tribeId,
            int targetRegion)
        {
            int vanillaResult = originalTribeRegionMembership(tribeManager, tribeId, targetRegion);
            int effectiveResult = vanillaResult;

            try
            {
                CommandAttempt command = activeCommandAttempt;
                bool bypassApplied = !disposed &&
                    command != null &&
                    tribeManager != IntPtr.Zero &&
                    tribeId == command.TribeId &&
                    targetRegion > 0 &&
                    targetRegion <= MaximumRegionId &&
                    vanillaResult == 0;
                if (bypassApplied)
                    effectiveResult = 1;

                if (command != null)
                {
                    command.RegionMembershipCalls++;
                    if (bypassApplied)
                        command.RegionMembershipBypasses++;
                    command.LastRegionMembershipTarget = targetRegion;
                    command.LastRegionMembershipVanillaResult = vanillaResult;
                    command.LastRegionMembershipEffectiveResult = effectiveResult;
                    command.LastNativeStage = "region-membership";

                    LogCommand(
                        $"stage=tribe-region-membership command={command.Id} " +
                        $"tribeArgument={tribeId} targetRegion={targetRegion} " +
                        $"vanilla={vanillaResult} effective={effectiveResult} " +
                        $"bypass={bypassApplied} call={command.RegionMembershipCalls}");
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test Tribe region-membership callback failed; " +
                    $"Vanilla result {vanillaResult} remains active: {ex}");
                return vanillaResult;
            }

            return effectiveResult;
        }

        private int ForceCompletedMoatMode(IntPtr unitManager, int unitId)
        {
            int vanillaResult = originalDetectCompletedMoatMode(unitManager, unitId);
            if (disposed || unitManager == IntPtr.Zero || unitId <= 0)
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

        private int ObservePathBuilder(
            IntPtr pathManager,
            int movementClass,
            int movementProfile)
        {
            int result = originalPathBuilder(pathManager, movementClass, movementProfile);
            try
            {
                if (!disposed && *moatPathMode == 1)
                    LogBuilderResult(movementClass, movementProfile, result);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Move Moat Test builder observer failed; real builder result {result} remains unchanged: {ex}");
            }

            return result;
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
            if (disposed || unitId <= 0 || !IsValidTileId(currentTileId) || !IsValidTileId(nextTileId))
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
            if (plan == null && command == null && *moatPathMode == 0)
                return;

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

        private void LogBuilderResult(int movementClass, int movementProfile, int result)
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
                    $"modeSource={GetModeSource(attempt)} pathMode={*moatPathMode} state=[{unitState}].");
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
            TrackedPlan tracked = new TrackedPlan(
                attempt.Id,
                attempt.UnitId,
                attempt.TargetX,
                attempt.TargetY,
                attempt.Result,
                CreateTrackingSignature(unit));
            lock (trackingLock)
                trackedPlans[attempt.UnitId] = tracked;
        }

        private void ObserveTrackedUnits(int currentTick)
        {
            if (disposed)
                return;

            List<TrackedPlan> plans;
            lock (trackingLock)
                plans = new List<TrackedPlan>(trackedPlans.Values);

            foreach (TrackedPlan tracked in plans)
            {
                bool remove = false;
                try
                {
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

        private static NativeDetour CreateDetour<TDelegate>(ulong targetAddress, TDelegate callback)
            where TDelegate : Delegate =>
            new NativeDetour(
                (IntPtr)unchecked((long)targetAddress),
                Marshal.GetFunctionPointerForDelegate(callback),
                new NativeDetourConfig { ManualApply = true });

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
                CentralMovementPlanRva,
                CentralMovementPlanPattern,
                "central ordinary-movement planner");
            ValidatePatternSpan(
                memory,
                TribeRegionMembershipRva,
                TribeRegionMembershipPattern,
                "Tribe target-region membership helper");
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
                PathBuilderRva,
                PathBuilderPattern,
                "central tile path builder");
        }

        private static void ValidateInlineHookSpans(ReadOnlySpan<byte> memory)
        {
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
            }

            public long Id { get; }
            public int UnitId { get; }
            public int TargetX { get; set; }
            public int TargetY { get; set; }
            public int Result { get; set; }
            public bool VanillaModeDetected { get; set; }
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
                LastRegionMembershipTarget = -1;
                LastRegionMembershipVanillaResult = -1;
                LastRegionMembershipEffectiveResult = -1;
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
            public int RegionMembershipCalls { get; set; }
            public int RegionMembershipBypasses { get; set; }
            public int LastRegionMembershipTarget { get; set; }
            public int LastRegionMembershipVanillaResult { get; set; }
            public int LastRegionMembershipEffectiveResult { get; set; }
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
                string lastSignature)
            {
                Id = id;
                UnitId = unitId;
                TargetX = targetX;
                TargetY = targetY;
                Result = result;
                LastSignature = lastSignature;
            }

            public long Id { get; }
            public int UnitId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int Result { get; }
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
