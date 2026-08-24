using BepInEx.Logging;
using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    /// <summary>
    /// Temporary, separately removable singleplayer calibration. When Vanilla
    /// already owns a live Hunter path but its near-range visibility is blocked,
    /// this lets the original distance-29 branch continue that same path.
    /// </summary>
    internal sealed unsafe class HunterVanillaPathContinuationDiagnostic : IDisposable
    {
        private const string ReferenceDllSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int DistanceStageSequenceRva = 0x130122;
        private const int DistanceTwentyEightCompareOffset = 0x18;
        private const int DistanceTwentyEightNearBranchTargetOffset = 0x30;
        private const int HunterUpdateStartRva = 0x12FC70;
        private const int HunterUpdateEndRva = 0x131422;
        private const int AttackGateHookRva = 0x130160;
        private const int AttackGateHookLength = 0x14;
        private const int AttackGateFirstBranchTargetRva = 0x13017A;
        private const int AttackGateSecondBranchRva = 0x130174;
        private const int AttackGateExitTargetRva = 0x131402;
        private const int HunterAiStateOffset = 0x2BC;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int HunterPathStateOffset = 0xF2;
        private const int HunterPathFieldF4Offset = 0xF4;
        private const int HunterPathProgressOffset = 0xF6;
        private const int HunterPathLengthOffset = 0xF8;
        private const int PreyReservationOffset = 0x448;
        private const ushort HunterStateFollowingTarget = 1;
        private const ushort ActivePathState = 2;
        private const ushort OwnHunterReservation = 2;
        private const uint MaximumPathSteps = 2000;
        private const int VanillaContinuationDistance = 29;
        private const int MaximumPreparedWorldDistance = 28;
        private const int MaxDiagnosticLogs = 600;
        private const ulong ZeroFlagMask = 1UL << 6;

        private const string DistanceStageSequencePattern =
            "83 FF 1E 7E 13 B8 06 00 00 00 " +
            "66 42 89 84 29 A4 09 00 00 E9 ? ? ? ? " +
            "83 FF 1C 7E 13 B8 08 00 00 00 " +
            "66 42 89 84 29 A4 09 00 00 E9 ? ? ? ? " +
            "BE 0A 00 00 00";

        private static readonly long MaxNoProgressDuration = Stopwatch.Frequency * 3;
        private static readonly long RetryCooldownDuration = Stopwatch.Frequency * 5;
        private static readonly long FreshAttackGateSnapshotLifetime = Stopwatch.Frequency / 2;
        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly HunterActiveTargetVisibilitySnapshot activeVisibility;
        private readonly HunterPclReachability pclReachability;
        private readonly Func<bool> canRun;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, ContinuationAttempt> activeAttempts =
            new Dictionary<int, ContinuationAttempt>();
        private readonly Dictionary<int, SuspendedAttempt> suspendedAttempts =
            new Dictionary<int, SuspendedAttempt>();
        private readonly Dictionary<int, string> lastPreparationRejections =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> lastWorldVisibilityDecisions =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> lastTileVisibilityDecisions =
            new Dictionary<int, string>();
        private readonly Dictionary<int, WorldRefreshObservation> lastWorldRefreshes =
            new Dictionary<int, WorldRefreshObservation>();
        private HookTransaction transaction;
        private HookRef<X64InlineHook> distanceCompareHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> attackGateHook = new HookRef<X64InlineHook>();
        private bool featureAvailable;
        private bool hookConfirmed;
        private bool attackGateHookConfirmed;
        private int diagnosticLogs;
        private bool disposed;

        public HunterVanillaPathContinuationDiagnostic(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            HunterActiveTargetVisibilitySnapshot activeVisibility,
            HunterPclReachability pclReachability,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<bool> canRun)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.activeVisibility = activeVisibility ?? throw new ArgumentNullException(nameof(activeVisibility));
            this.pclReachability = pclReachability ?? throw new ArgumentNullException(nameof(pclReachability));
            this.canRun = canRun ?? throw new ArgumentNullException(nameof(canRun));

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters Vanilla-path continuation diagnostic unavailable: " +
                    $"DLL hash differs from audited SHA-256 {ReferenceDllSha256}; behavior remains unchanged.");
                return;
            }
            if (!activeVisibility.IsAvailable)
                throw new InvalidOperationException("The active Hunter visibility snapshot is unavailable.");
            if (!pclReachability.IsAvailable)
                throw new InvalidOperationException("The validated native Hunter PCL reachability query is unavailable.");
            if (memory.Length == 0 || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int sequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DistanceStageSequencePattern,
                DistanceStageSequenceRva,
                referenceHashMatches,
                "Hunter state-1 distance-stage continuation",
                log).Rva;
            int compareRva = checked(sequenceRva + DistanceTwentyEightCompareOffset);
            ValidateDistanceBranch(memory, sequenceRva, compareRva);
            int distanceHookLength = ValidateDistanceContinuationHookSpan(
                memory,
                libraryBase,
                compareRva);
            ValidateAttackGateHookSpan(memory, libraryBase);
            if (compareRva + distanceHookLength > AttackGateHookRva)
            {
                throw new InvalidOperationException(
                    "The Hunter distance and attack-gate hook spans overlap.");
            }

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref distanceCompareHook,
                    libraryBase + unchecked((ulong)compareRva),
                    TryContinueExistingVanillaPath,
                    regs: X64SmartCPUContextRegs.Volatile |
                        X64SmartCPUContextRegs.RBX |
                        X64SmartCPUContextRegs.RDI,
                    hookSize: distanceHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref attackGateHook,
                    libraryBase + unchecked((ulong)AttackGateHookRva),
                    TryHandoffFreshVisibleAttack,
                    regs: X64SmartCPUContextRegs.Volatile |
                        X64SmartCPUContextRegs.RBX |
                        X64SmartCPUContextRegs.RDI |
                        X64SmartCPUContextRegs.RBP |
                        X64SmartCPUContextRegs.R13 |
                        X64SmartCPUContextRegs.R14 |
                        X64SmartCPUContextRegs.Flags,
                    hookSize: AttackGateHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.BeforeCallback);
                transaction.Commit();
                if (!distanceCompareHook.Success || !attackGateHook.Success)
                {
                    throw new InvalidOperationException(
                        "One or more Hunter continuation/attack-gate hooks were not installed.");
                }

                featureAvailable = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters Vanilla-path continuation diagnostic initialized: " +
                    $"sequenceRva=0x{sequenceRva:X}, compareRva=0x{compareRva:X}, " +
                    $"distanceHookSpan=[0x{compareRva:X},0x{compareRva + distanceHookLength:X}), " +
                    $"attackGateHookSpan=[0x{AttackGateHookRva:X}," +
                    $"0x{AttackGateHookRva + AttackGateHookLength:X}), " +
                    $"nearBranchTargetRva=0x{sequenceRva + DistanceTwentyEightNearBranchTargetOffset:X}, " +
                    $"forcedDistance={VanillaContinuationDistance}, globalIdentityLimit=None, " +
                    "maxActiveAttempts=one-per-hunter, " +
                    "totalDurationLimit=None, " +
                    $"maxNoProgressSeconds={MaxNoProgressDuration / Stopwatch.Frequency}, " +
                    $"boundedRetryCooldownSeconds={RetryCooldownDuration / Stopwatch.Frequency}, " +
                    "ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "speedWrite=False, animationWrite=False, " +
                    "registerOverride=RDI-distance-only, preparedTicketRequired=False, " +
                    "tileAttackDecision=active-visibility-snapshot, " +
                    "nearVisibility=wrapper-plus-bidirectional-core, visibleAttackHandoff=True, " +
                    "directionalDisagreementContinuesPath=True, explicitCachedPclReachableRequired=True, " +
                    "staleBlockedContinuation=True, visiblePositionMatchRequired=True, " +
                    "attackGateSnapshotMaxAgeMs=500, attackGateOverride=RFLAGS.ZF-clear-only, " +
                    "nativeVisibilityOrPclQueryInsideHook=False.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public bool IsAvailable =>
            featureAvailable &&
            !disposed &&
            distanceCompareHook.Success &&
            attackGateHook.Success &&
            activeVisibility.IsAvailable &&
            pclReachability.IsAvailable;

        public void ResetForMap()
        {
            lock (stateLock)
            {
                activeAttempts.Clear();
                suspendedAttempts.Clear();
                lastPreparationRejections.Clear();
                lastWorldVisibilityDecisions.Clear();
                lastTileVisibilityDecisions.Clear();
                lastWorldRefreshes.Clear();
            }

            hookConfirmed = false;
            attackGateHookConfirmed = false;
            diagnosticLogs = 0;
        }

        public HunterStateOneNearRefreshAction TryPrepareStateOneNearRefresh(
            int hunterUnitId,
            int expectedPreyUnitId,
            uint expectedPreyGlobalId,
            int nativeWorldDistance,
            out bool shouldLog)
        {
            shouldLog = false;
            if (!IsAvailable ||
                !canRun() ||
                nativeWorldDistance < 0 ||
                nativeWorldDistance > MaximumPreparedWorldDistance)
            {
                return HunterStateOneNearRefreshAction.None;
            }

            if (!TryGetContext(
                    hunterUnitId,
                    out GameUnit* hunter,
                    out GameUnit* prey,
                    out int preyUnitId,
                    out uint preyGlobalId,
                    out ushort pathState,
                    out ushort pathFieldF4,
                    out ushort pathProgress,
                    out uint pathLength))
            {
                ClearHunterAttemptState(hunterUnitId);
                LogPreparationRejection(
                    hunterUnitId,
                    "invalid-state1-context",
                    $"nativeWorldDistance={nativeWorldDistance}",
                    warning: true);
                return HunterStateOneNearRefreshAction.None;
            }

            AttemptIdentity identity = new AttemptIdentity(
                hunterUnitId,
                hunter->r_GlobalId,
                preyUnitId,
                preyGlobalId);
            if (preyUnitId != expectedPreyUnitId || preyGlobalId != expectedPreyGlobalId)
            {
                ClearHunterAttemptState(hunterUnitId);
                LogPreparationRejection(
                    hunterUnitId,
                    "target-identity-mismatch",
                    $"expected={expectedPreyUnitId}/{expectedPreyGlobalId}, " +
                    $"actual={preyUnitId}/{preyGlobalId}",
                    warning: true);
                return HunterStateOneNearRefreshAction.None;
            }

            long timestamp = Stopwatch.GetTimestamp();
            lock (stateLock)
            {
                lastWorldRefreshes[hunterUnitId] = new WorldRefreshObservation(
                    identity,
                    nativeWorldDistance,
                    timestamp);
            }

            if (IsRetryCoolingDown(identity, timestamp))
            {
                LogPreparationRejection(
                    hunterUnitId,
                    "retry-cooldown",
                    $"target={preyUnitId}/{preyGlobalId}, nativeWorldDistance={nativeWorldDistance}");
                return HunterStateOneNearRefreshAction.None;
            }

            if (pathState != ActivePathState ||
                pathLength <= 1 ||
                pathLength > MaximumPathSteps ||
                pathProgress >= pathLength)
            {
                string reason = $"path-unavailable-or-complete-{pathState}-{pathProgress}-{pathLength}";
                StopAttempt(identity, reason, nativeWorldDistance, pathFieldF4);
                LogPreparationRejection(
                    hunterUnitId,
                    reason,
                    $"target={preyUnitId}/{preyGlobalId}, path={pathState}/{pathFieldF4}/" +
                    $"{pathProgress}/{pathLength}");
                return HunterStateOneNearRefreshAction.None;
            }

            if (!pclReachability.TryGetActiveTargetReachability(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    prey->r_UnitChimp,
                    timestamp,
                    out bool pclReachable,
                    out long pclSnapshotAgeMilliseconds,
                    out string pclSnapshotStatus))
            {
                string reason = $"pcl-active-snapshot-{pclSnapshotStatus}";
                StopAttempt(identity, reason, nativeWorldDistance, pathFieldF4);
                LogPreparationRejection(
                    hunterUnitId,
                    reason,
                    $"target={preyUnitId}/{preyGlobalId}, path={pathState}/{pathProgress}/{pathLength}, " +
                    $"snapshotAgeMs={pclSnapshotAgeMilliseconds}",
                    warning: false);
                return HunterStateOneNearRefreshAction.None;
            }
            if (!pclReachable)
            {
                StopAttempt(identity, "pcl-unreachable", nativeWorldDistance, pathFieldF4);
                LogPreparationRejection(
                    hunterUnitId,
                    "pcl-unreachable",
                    $"target={preyUnitId}/{preyGlobalId}, path={pathState}/{pathProgress}/{pathLength}");
                return HunterStateOneNearRefreshAction.None;
            }

            if (!activeVisibility.TryGetObservation(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    prey->r_UnitChimp,
                    timestamp,
                    out HunterActiveVisibilityObservation visibility))
            {
                StopAttempt(
                    identity,
                    $"visibility-snapshot-{visibility.Status}",
                    nativeWorldDistance,
                    pathFieldF4);
                LogPreparationRejection(
                    hunterUnitId,
                    $"visibility-snapshot-{visibility.Status}",
                    $"target={preyUnitId}/{preyGlobalId}, " +
                    $"snapshotAgeMs={visibility.SnapshotAgeMilliseconds}, " +
                    $"pendingAgeMs={visibility.PendingAgeMilliseconds}",
                    warning: true);
                return HunterStateOneNearRefreshAction.None;
            }

            bool visibilityDecisionChanged = LogVisibilityDecision(
                identity,
                nativeWorldDistance,
                visibility,
                decisionPoint: "world-refresh");
            shouldLog = visibilityDecisionChanged;
            if (visibility.State == HunterActiveVisibilityState.Visible)
            {
                StopAttempt(
                    identity,
                    "active-visibility-visible",
                    nativeWorldDistance,
                    pathFieldF4);
                lock (stateLock)
                    lastPreparationRejections.Remove(hunterUnitId);
                return HunterStateOneNearRefreshAction.HandoffToVanillaAttack;
            }

            if (shouldLog)
            {
                LogDiagnostic(
                    "Improved Hunters authorized Hunter world-refresh bypass from active visibility: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{prey->r_UnitChimp}, " +
                    $"worldRefreshDistance={nativeWorldDistance}, " +
                    $"visibilitySnapshotStatus={visibility.Status}, " +
                    $"visibilitySnapshotAgeMs={visibility.SnapshotAgeMilliseconds}, " +
                    $"visibilityPendingAgeMs={visibility.PendingAgeMilliseconds}, " +
                    $"visibilityPathGeneration={visibility.PathGeneration}, " +
                    $"wrapperResult={visibility.WrapperResult}, " +
                    $"coreHunterToPreyResult={visibility.HunterToPreyResult}, " +
                    $"corePreyToHunterResult={visibility.PreyToHunterResult}, " +
                    $"visibilityClassification={visibility.Classification}, " +
                    $"visibilityPositionsMatch={visibility.PositionsMatch}, " +
                    $"pclReachable={pclReachable}, pclSource=active-target-snapshot, " +
                    $"pclSnapshotStatus={pclSnapshotStatus}, " +
                    $"pclSnapshotAgeMs={pclSnapshotAgeMilliseconds}, " +
                    $"reservation={OwnHunterReservation}, " +
                    $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}, " +
                    $"action={(visibility.State == HunterActiveVisibilityState.Pending ? "visibility-pending" : "continue-vanilla-path")}, " +
                    "ticket=not-required, ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "speedWrite=False, animationWrite=False.");
            }

            return visibility.State == HunterActiveVisibilityState.Pending
                ? HunterStateOneNearRefreshAction.ContinueExistingPathPendingVisibility
                : HunterStateOneNearRefreshAction.ContinueExistingPath;
        }

        private void TryContinueExistingVanillaPath(NativePointer<X64SmartCPUContext> context)
        {
            int tileAttackDistance = unchecked((int)(uint)context.Pointer->RDI);
            int hunterUnitId = unchecked((int)(uint)context.Pointer->RBX);
            if (!IsAvailable ||
                !canRun() ||
                tileAttackDistance < 0 ||
                tileAttackDistance > MaximumPreparedWorldDistance)
            {
                return;
            }

            long timestamp = Stopwatch.GetTimestamp();
            if (!TryGetContext(
                    hunterUnitId,
                    out GameUnit* hunter,
                    out GameUnit* prey,
                    out int preyUnitId,
                    out uint preyGlobalId,
                    out ushort pathState,
                    out ushort pathFieldF4,
                    out ushort pathProgress,
                    out uint pathLength))
            {
                ClearHunterAttemptState(hunterUnitId);
                LogPreparationRejection(
                    hunterUnitId,
                    "tile-attack-context-invalid",
                    $"tileAttackDistance={tileAttackDistance}",
                    warning: true);
                return;
            }

            AttemptIdentity identity = new AttemptIdentity(
                hunterUnitId,
                hunter->r_GlobalId,
                preyUnitId,
                preyGlobalId);
            if (pathState != ActivePathState ||
                pathLength <= 1 ||
                pathLength > MaximumPathSteps ||
                pathProgress >= pathLength)
            {
                ClearHunterAttemptState(hunterUnitId);
                LogPreparationRejection(
                    hunterUnitId,
                    "tile-attack-path-revalidation-failed",
                    $"target={preyUnitId}/{preyGlobalId}, path={pathState}/{pathFieldF4}/" +
                    $"{pathProgress}/{pathLength}",
                    warning: true);
                return;
            }

            if (!pclReachability.TryGetActiveTargetReachability(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    prey->r_UnitChimp,
                    timestamp,
                    out bool pclReachable,
                    out long pclSnapshotAgeMilliseconds,
                    out string pclSnapshotStatus) ||
                !pclReachable)
            {
                string pclReason = pclReachable
                    ? $"pcl-active-snapshot-{pclSnapshotStatus}"
                    : "pcl-unreachable";
                StopAttempt(identity, pclReason, tileAttackDistance, pathFieldF4);
                LogTileDecision(
                    identity,
                    tileAttackDistance,
                    pathState,
                    pathFieldF4,
                    pathProgress,
                    pathLength,
                    OwnHunterReservation,
                    new HunterActiveVisibilityObservation(
                        HunterActiveVisibilityState.Pending,
                        "not-read-pcl-rejected",
                        "unavailable",
                        -1,
                        -1,
                        -1,
                        -1,
                        -1,
                        -1,
                        positionsMatch: false),
                    pclReachable,
                    pclSnapshotStatus,
                    pclSnapshotAgeMilliseconds,
                    $"reject-{pclReason}",
                    force: true);
                return;
            }

            if (!activeVisibility.TryGetObservation(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    prey->r_UnitChimp,
                    timestamp,
                    out HunterActiveVisibilityObservation visibility))
            {
                StopAttempt(
                    identity,
                    $"visibility-snapshot-{visibility.Status}",
                    tileAttackDistance,
                    pathFieldF4);
                LogTileDecision(
                    identity,
                    tileAttackDistance,
                    pathState,
                    pathFieldF4,
                    pathProgress,
                    pathLength,
                    OwnHunterReservation,
                    visibility,
                    pclReachable,
                    pclSnapshotStatus,
                    pclSnapshotAgeMilliseconds,
                    $"reject-visibility-{visibility.Status}",
                    force: true);
                return;
            }

            if (visibility.State == HunterActiveVisibilityState.Visible)
            {
                StopAttempt(identity, "visible-attack-handoff", tileAttackDistance, pathFieldF4);
                string visibleAction = pathFieldF4 == 0
                    ? "release-distance-override-vanilla-attack-gate-ready"
                    : "release-distance-override-vanilla-attack-gate-deferred";
                LogTileDecision(
                    identity,
                    tileAttackDistance,
                    pathState,
                    pathFieldF4,
                    pathProgress,
                    pathLength,
                    OwnHunterReservation,
                    visibility,
                    pclReachable,
                    pclSnapshotStatus,
                    pclSnapshotAgeMilliseconds,
                    visibleAction,
                    force: false);
                return;
            }

            if (IsRetryCoolingDown(identity, timestamp))
            {
                LogTileDecision(
                    identity,
                    tileAttackDistance,
                    pathState,
                    pathFieldF4,
                    pathProgress,
                    pathLength,
                    OwnHunterReservation,
                    visibility,
                    pclReachable,
                    pclSnapshotStatus,
                    pclSnapshotAgeMilliseconds,
                    "reject-retry-cooldown",
                    force: false);
                return;
            }

            ContinuationAttempt attempt;
            bool newAttempt = false;
            bool pathChanged;
            bool noProgressReached;
            lock (stateLock)
            {
                if (activeAttempts.TryGetValue(hunterUnitId, out ContinuationAttempt current) &&
                    !current.Identity.Equals(identity))
                {
                    activeAttempts.Remove(hunterUnitId);
                }

                if (!activeAttempts.TryGetValue(hunterUnitId, out attempt))
                {
                    attempt = new ContinuationAttempt(identity, timestamp, pathProgress, pathLength);
                    newAttempt = true;
                }

                pathChanged = pathProgress != attempt.LastProgress ||
                    pathLength != attempt.LastPathLength;
                if (pathChanged)
                    attempt = attempt.WithPathProgress(pathProgress, pathLength, timestamp);
                noProgressReached = timestamp - attempt.LastProgressAt > MaxNoProgressDuration;
                if (noProgressReached)
                {
                    activeAttempts.Remove(hunterUnitId);
                    suspendedAttempts[hunterUnitId] = new SuspendedAttempt(
                        identity,
                        timestamp + RetryCooldownDuration);
                }
                else
                {
                    attempt = attempt.WithContinuation();
                    activeAttempts[hunterUnitId] = attempt;
                    lastPreparationRejections.Remove(hunterUnitId);
                }
            }

            if (noProgressReached)
            {
                LogTileDecision(
                    identity,
                    tileAttackDistance,
                    pathState,
                    pathFieldF4,
                    pathProgress,
                    pathLength,
                    OwnHunterReservation,
                    visibility,
                    pclReachable,
                    pclSnapshotStatus,
                    pclSnapshotAgeMilliseconds,
                    "reject-no-progress",
                    force: true);
                return;
            }

            // RDI is restored by HunterUpdate's epilogue. This selects only
            // Vanilla's existing distance-29 locomotion branch for this update.
            context.Pointer->RDI = VanillaContinuationDistance;

            if (!hookConfirmed)
            {
                hookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters Vanilla-path continuation hook confirmed: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, target={preyUnitId}/{preyGlobalId}, " +
                    $"tileAttackDistance={tileAttackDistance}, " +
                    $"path={pathState}/{pathProgress}/{pathLength}.");
            }

            LogTileDecision(
                identity,
                tileAttackDistance,
                pathState,
                pathFieldF4,
                pathProgress,
                pathLength,
                OwnHunterReservation,
                visibility,
                pclReachable,
                pclSnapshotStatus,
                pclSnapshotAgeMilliseconds,
                visibility.State == HunterActiveVisibilityState.Pending
                    ? "visibility-pending"
                    : "continue-vanilla-path",
                force: newAttempt || pathChanged);
        }

        private void TryHandoffFreshVisibleAttack(NativePointer<X64SmartCPUContext> context)
        {
            // The relocated first CMP/JE and path-state CMP already ran. A set
            // ZF therefore means Vanilla would take its path-state-2 exit at
            // 0x130124. Clearing only that flag falls through to the untouched
            // attack setup at 0x13012A; every failed guard leaves ZF unchanged.
            if (!IsAvailable || !canRun() || (context.Pointer->Rflags & ZeroFlagMask) == 0)
                return;

            int tileAttackDistance = unchecked((int)(uint)context.Pointer->RDI);
            int hunterUnitId = unchecked((int)(uint)context.Pointer->RBX);
            if (tileAttackDistance < 0 || tileAttackDistance > MaximumPreparedWorldDistance)
                return;

            long timestamp = Stopwatch.GetTimestamp();
            if (!TryGetContext(
                    hunterUnitId,
                    out GameUnit* hunter,
                    out GameUnit* prey,
                    out int preyUnitId,
                    out uint preyGlobalId,
                    out ushort pathState,
                    out ushort pathFieldF4,
                    out ushort pathProgress,
                    out uint pathLength) ||
                pathState != ActivePathState ||
                pathLength <= 1 ||
                pathLength > MaximumPathSteps ||
                pathProgress >= pathLength)
            {
                return;
            }

            if (!pclReachability.TryGetActiveTargetReachability(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    prey->r_UnitChimp,
                    timestamp,
                    out bool pclReachable,
                    out long pclSnapshotAgeMilliseconds,
                    out string pclSnapshotStatus) ||
                !pclReachable)
            {
                return;
            }

            if (!activeVisibility.TryGetObservation(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    prey->r_UnitChimp,
                    timestamp,
                    out HunterActiveVisibilityObservation visibility) ||
                visibility.State != HunterActiveVisibilityState.Visible ||
                !visibility.PositionsMatch ||
                visibility.SnapshotAgeMilliseconds < 0 ||
                visibility.SnapshotAgeMilliseconds >
                    FreshAttackGateSnapshotLifetime * 1000 / Stopwatch.Frequency)
            {
                return;
            }

            context.Pointer->Rflags &= ~ZeroFlagMask;
            if (!attackGateHookConfirmed)
            {
                attackGateHookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters fresh visible attack-gate handoff confirmed: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{prey->r_UnitChimp}, " +
                    $"tileAttackDistance={tileAttackDistance}, snapshotAgeMs=" +
                    $"{visibility.SnapshotAgeMilliseconds}, positionsMatch=True, " +
                    $"pclSnapshotStatus={pclSnapshotStatus}, " +
                    $"pclSnapshotAgeMs={pclSnapshotAgeMilliseconds}, " +
                    $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}, " +
                    "registerOverride=RFLAGS.ZF-clear-only, targetRva=0x13012A, " +
                    "ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "pathFieldWrite=False.");
            }
        }

        private bool TryGetContext(
            int hunterUnitId,
            out GameUnit* hunter,
            out GameUnit* prey,
            out int preyUnitId,
            out uint preyGlobalId,
            out ushort pathState,
            out ushort pathFieldF4,
            out ushort pathProgress,
            out uint pathLength)
        {
            hunter = null;
            prey = null;
            preyUnitId = 0;
            preyGlobalId = 0;
            pathState = 0;
            pathFieldF4 = 0;
            pathProgress = 0;
            pathLength = 0;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (hunterUnitId <= 0 ||
                !unitApi.TryGetUnitById(hunterUnitId, out hunter) ||
                hunter == null ||
                hunter->r_AliveState != AliveState.IsAlive ||
                hunter->r_CurrentHealth == 0 ||
                hunter->r_GlobalId == 0 ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
            {
                return false;
            }

            byte* hunterBytes = (byte*)hunter;
            if (*(ushort*)(hunterBytes + HunterAiStateOffset) != HunterStateFollowingTarget)
                return false;

            preyUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            preyGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            if (preyUnitId <= 0 ||
                preyGlobalId == 0 ||
                !unitApi.TryGetUnitById(preyUnitId, out prey) ||
                prey == null ||
                prey->r_AliveState != AliveState.IsAlive ||
                prey->r_CurrentHealth == 0 ||
                prey->r_GlobalId != preyGlobalId ||
                !settings.IsKnownAnimal(prey->r_UnitChimp) ||
                !settings.IsHuntingEnabled(prey->r_UnitChimp))
            {
                return false;
            }

            byte* preyBytes = (byte*)prey;
            if (*(ushort*)(preyBytes + PreyReservationOffset) != OwnHunterReservation)
                return false;

            pathState = *(ushort*)(hunterBytes + HunterPathStateOffset);
            pathFieldF4 = *(ushort*)(hunterBytes + HunterPathFieldF4Offset);
            pathProgress = *(ushort*)(hunterBytes + HunterPathProgressOffset);
            pathLength = *(uint*)(hunterBytes + HunterPathLengthOffset);
            return true;
        }

        private void StopAttempt(
            AttemptIdentity identity,
            string reason,
            int nativeDistance,
            ushort pathFieldF4)
        {
            ContinuationAttempt attempt = default;
            bool hadActiveAttempt = false;
            lock (stateLock)
            {
                if (activeAttempts.TryGetValue(identity.HunterUnitId, out attempt) &&
                    attempt.Identity.Equals(identity))
                {
                    activeAttempts.Remove(identity.HunterUnitId);
                    hadActiveAttempt = true;
                }

                if (suspendedAttempts.TryGetValue(identity.HunterUnitId, out SuspendedAttempt suspended) &&
                    suspended.Identity.Equals(identity))
                {
                    suspendedAttempts.Remove(identity.HunterUnitId);
                }
            }

            if (!hadActiveAttempt)
                return;

            LogDiagnostic(
                "Improved Hunters released Vanilla Hunter path continuation: " +
                $"hunter={identity.HunterUnitId}/{identity.HunterGlobalId}, " +
                $"target={identity.PreyUnitId}/{identity.PreyGlobalId}, reason={reason}, " +
                $"nativeDistance={nativeDistance}, pathFieldF4={pathFieldF4}, " +
                $"continuations={attempt.Continuations}, currentCallbackMutation=False.");
        }

        private bool IsRetryCoolingDown(AttemptIdentity identity, long timestamp)
        {
            lock (stateLock)
            {
                if (activeAttempts.TryGetValue(identity.HunterUnitId, out ContinuationAttempt active) &&
                    !active.Identity.Equals(identity))
                {
                    activeAttempts.Remove(identity.HunterUnitId);
                }

                if (!suspendedAttempts.TryGetValue(identity.HunterUnitId, out SuspendedAttempt suspended))
                    return false;

                if (!suspended.Identity.Equals(identity) || timestamp >= suspended.RetryAt)
                {
                    suspendedAttempts.Remove(identity.HunterUnitId);
                    return false;
                }

                return true;
            }
        }

        private void ClearHunterAttemptState(int hunterUnitId)
        {
            if (hunterUnitId <= 0)
                return;

            lock (stateLock)
            {
                activeAttempts.Remove(hunterUnitId);
                suspendedAttempts.Remove(hunterUnitId);
            }
        }

        private bool LogVisibilityDecision(
            AttemptIdentity identity,
            int nativeWorldDistance,
            HunterActiveVisibilityObservation visibility,
            string decisionPoint)
        {
            string signature =
                $"{identity.PreyUnitId}/{identity.PreyGlobalId}/{decisionPoint}/" +
                $"{visibility.Status}/{visibility.Classification}/{visibility.PathGeneration}";
            bool shouldLog;
            lock (stateLock)
            {
                shouldLog = !lastWorldVisibilityDecisions.TryGetValue(
                        identity.HunterUnitId,
                        out string previous) ||
                    !string.Equals(previous, signature, StringComparison.Ordinal);
                if (shouldLog)
                    lastWorldVisibilityDecisions[identity.HunterUnitId] = signature;
            }

            if (!shouldLog)
                return false;

            LogDiagnostic(
                "Improved Hunters consumed active Hunter visibility decision: " +
                $"hunter={identity.HunterUnitId}/{identity.HunterGlobalId}, " +
                $"target={identity.PreyUnitId}/{identity.PreyGlobalId}, " +
                $"decisionPoint={decisionPoint}, worldRefreshDistance={nativeWorldDistance}, " +
                $"visibilitySnapshotStatus={visibility.Status}, " +
                $"visibilitySnapshotAgeMs={visibility.SnapshotAgeMilliseconds}, " +
                $"visibilityPendingAgeMs={visibility.PendingAgeMilliseconds}, " +
                $"visibilityPathGeneration={visibility.PathGeneration}, " +
                $"wrapperResult={visibility.WrapperResult}, " +
                $"coreHunterToPreyResult={visibility.HunterToPreyResult}, " +
                $"corePreyToHunterResult={visibility.PreyToHunterResult}, " +
                $"classification={visibility.Classification}, " +
                $"positionsMatch={visibility.PositionsMatch}, " +
                "physicalArrowCollisionPreflight=False, behaviorMutation=False.",
                warning: visibility.Classification == "blocked-directional-disagreement");
            return true;
        }

        private void LogTileDecision(
            AttemptIdentity identity,
            int tileAttackDistance,
            ushort pathState,
            ushort pathFieldF4,
            ushort pathProgress,
            uint pathLength,
            ushort reservation,
            HunterActiveVisibilityObservation visibility,
            bool pclReachable,
            string pclSnapshotStatus,
            long pclSnapshotAgeMilliseconds,
            string action,
            bool force)
        {
            int worldRefreshDistance = -1;
            long worldRefreshAgeMilliseconds = -1;
            string signature =
                $"{identity.PreyUnitId}/{identity.PreyGlobalId}/{action}/" +
                $"{visibility.Status}/{visibility.Classification}/{visibility.PathGeneration}/" +
                $"{pathProgress}/{pathLength}";
            lock (stateLock)
            {
                if (lastWorldRefreshes.TryGetValue(
                        identity.HunterUnitId,
                        out WorldRefreshObservation worldRefresh) &&
                    worldRefresh.Identity.Equals(identity))
                {
                    worldRefreshDistance = worldRefresh.Distance;
                    worldRefreshAgeMilliseconds = Math.Max(
                            0,
                            Stopwatch.GetTimestamp() - worldRefresh.ObservedAt) *
                        1000 / Stopwatch.Frequency;
                }

                if (!force &&
                    lastTileVisibilityDecisions.TryGetValue(identity.HunterUnitId, out string previous) &&
                    string.Equals(previous, signature, StringComparison.Ordinal))
                {
                    return;
                }

                lastTileVisibilityDecisions[identity.HunterUnitId] = signature;
            }

            LogDiagnostic(
                "Improved Hunters Hunter tile-attack decision: " +
                $"hunter={identity.HunterUnitId}/{identity.HunterGlobalId}, " +
                $"target={identity.PreyUnitId}/{identity.PreyGlobalId}, " +
                $"tileAttackDistance={tileAttackDistance}, " +
                $"worldRefreshDistance={worldRefreshDistance}, " +
                $"worldRefreshAgeMs={worldRefreshAgeMilliseconds}, " +
                $"visibilitySnapshotStatus={visibility.Status}, " +
                $"visibilitySnapshotAgeMs={visibility.SnapshotAgeMilliseconds}, " +
                $"visibilityPendingAgeMs={visibility.PendingAgeMilliseconds}, " +
                $"visibilityPathGeneration={visibility.PathGeneration}, " +
                $"wrapperResult={visibility.WrapperResult}, " +
                $"coreHunterToPreyResult={visibility.HunterToPreyResult}, " +
                $"corePreyToHunterResult={visibility.PreyToHunterResult}, " +
                $"visibilityClassification={visibility.Classification}, " +
                $"visibilityPositionsMatch={visibility.PositionsMatch}, " +
                $"pclReachable={pclReachable}, pclSnapshotStatus={pclSnapshotStatus}, " +
                $"pclSnapshotAgeMs={pclSnapshotAgeMilliseconds}, reservation={reservation}, " +
                $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}, action={action}, " +
                $"registerOverride={(action == "continue-vanilla-path" || action == "visibility-pending" ? "RDI-distance-only" : "none")}, " +
                "ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                "speedWrite=False, animationWrite=False.",
                warning: action.StartsWith("reject-", StringComparison.Ordinal));
        }

        private void LogPreparationRejection(
            int hunterUnitId,
            string reason,
            string details,
            bool warning = false)
        {
            bool shouldLog;
            lock (stateLock)
            {
                shouldLog = !lastPreparationRejections.TryGetValue(hunterUnitId, out string previous) ||
                    !string.Equals(previous, reason, StringComparison.Ordinal);
                if (shouldLog)
                    lastPreparationRejections[hunterUnitId] = reason;
            }

            if (!shouldLog)
                return;

            LogDiagnostic(
                "Improved Hunters did not prepare Vanilla Hunter path continuation: " +
                $"hunter={hunterUnitId}, reason={reason}, {details}, " +
                "nearRefreshBypass=False, currentCallbackMutation=False.",
                warning);
        }

        private void LogDiagnostic(string message, bool warning = false)
        {
            if (diagnosticLogs >= MaxDiagnosticLogs)
                return;

            diagnosticLogs++;
            string boundedMessage = $"{message} ({diagnosticLogs}/{MaxDiagnosticLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, boundedMessage);
            else
                Shared.DebugLogHelper.LogInfo(log, boundedMessage);
        }

        private static int ValidateDistanceContinuationHookSpan(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            int hookRva)
        {
            const int decodeLookahead = 48;
            if (hookRva < 0 || hookRva > memory.Length - decodeLookahead)
                throw new InvalidOperationException("Hunter distance hook lies outside the module image.");

            ulong hookAddress = libraryBase + unchecked((ulong)hookRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(hookRva, decodeLookahead).ToArray()),
                hookAddress);
            Instruction compare = decoder.Decode();
            Instruction nearBranch = decoder.Decode();
            Instruction stageLoad = decoder.Decode();
            Instruction stageWrite = decoder.Decode();
            int hookLength = checked((int)(decoder.IP - hookAddress));
            if (compare.IsInvalid ||
                nearBranch.IsInvalid ||
                stageLoad.IsInvalid ||
                stageWrite.IsInvalid ||
                compare.Mnemonic != Mnemonic.Cmp ||
                compare.Length != 3 ||
                nearBranch.Mnemonic != Mnemonic.Jle ||
                nearBranch.Length != 2 ||
                stageLoad.Mnemonic != Mnemonic.Mov ||
                stageLoad.Length != 5 ||
                stageWrite.Mnemonic != Mnemonic.Mov ||
                stageWrite.Length != 9 ||
                hookLength != 0x13)
            {
                throw new InvalidOperationException(
                    "Hunter distance hook does not decode as the audited 3+2+5+9-byte span.");
            }

            ulong hookEndAddress = hookAddress + unchecked((ulong)hookLength);
            ulong expectedNearTarget = libraryBase + 0x130152UL;
            if (nearBranch.FlowControl != FlowControl.ConditionalBranch ||
                nearBranch.NearBranchTarget != expectedNearTarget ||
                nearBranch.NearBranchTarget < hookEndAddress)
            {
                throw new InvalidOperationException(
                    $"Hunter distance-hook branch changed: target=0x{nearBranch.NearBranchTarget:X}, " +
                    $"span=[0x{hookAddress:X},0x{hookEndAddress:X}).");
            }

            ValidateNoExternalDirectBranchTargetsInsideHook(
                memory,
                libraryBase,
                hookAddress,
                hookEndAddress,
                "distance-28");
            return hookLength;
        }

        private static void ValidateAttackGateHookSpan(
            ReadOnlySpan<byte> memory,
            ulong libraryBase)
        {
            const int decodeLookahead = 48;
            if (AttackGateHookRva < 0 || AttackGateHookRva > memory.Length - decodeLookahead)
                throw new InvalidOperationException("Hunter attack-gate hook lies outside the module image.");

            ulong hookAddress = libraryBase + unchecked((ulong)AttackGateHookRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(
                    memory.Slice(AttackGateHookRva, decodeLookahead).ToArray()),
                hookAddress);
            Instruction locomotionCompare = decoder.Decode();
            Instruction readyBranch = decoder.Decode();
            Instruction pathStateCompare = decoder.Decode();
            int hookLength = checked((int)(decoder.IP - hookAddress));
            if (locomotionCompare.IsInvalid ||
                readyBranch.IsInvalid ||
                pathStateCompare.IsInvalid ||
                locomotionCompare.Mnemonic != Mnemonic.Cmp ||
                locomotionCompare.Length != 9 ||
                readyBranch.Mnemonic != Mnemonic.Je ||
                readyBranch.Length != 2 ||
                pathStateCompare.Mnemonic != Mnemonic.Cmp ||
                pathStateCompare.Length != 9 ||
                hookLength != AttackGateHookLength)
            {
                throw new InvalidOperationException(
                    "Hunter attack-gate hook does not decode as the audited 9+2+9-byte span.");
            }

            ulong hookEndAddress = hookAddress + AttackGateHookLength;
            ulong expectedReadyTarget =
                libraryBase + unchecked((ulong)AttackGateFirstBranchTargetRva);
            if (readyBranch.FlowControl != FlowControl.ConditionalBranch ||
                readyBranch.NearBranchTarget != expectedReadyTarget ||
                readyBranch.NearBranchTarget < hookEndAddress)
            {
                throw new InvalidOperationException(
                    $"Hunter attack-gate ready branch changed: target=0x{readyBranch.NearBranchTarget:X}, " +
                    $"span=[0x{hookAddress:X},0x{hookEndAddress:X}).");
            }

            Instruction exitBranch = decoder.Decode();
            ulong expectedExitTarget =
                libraryBase + unchecked((ulong)AttackGateExitTargetRva);
            if (exitBranch.IsInvalid ||
                exitBranch.IP != libraryBase + unchecked((ulong)AttackGateSecondBranchRva) ||
                exitBranch.Mnemonic != Mnemonic.Je ||
                exitBranch.FlowControl != FlowControl.ConditionalBranch ||
                exitBranch.NearBranchTarget != expectedExitTarget)
            {
                throw new InvalidOperationException(
                    $"Hunter attack-gate exit branch changed: address=0x{exitBranch.IP:X}, " +
                    $"target=0x{exitBranch.NearBranchTarget:X}.");
            }

            ValidateNoExternalDirectBranchTargetsInsideHook(
                memory,
                libraryBase,
                hookAddress,
                hookEndAddress,
                "attack-gate");
        }

        private static void ValidateNoExternalDirectBranchTargetsInsideHook(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            ulong hookAddress,
            ulong hookEndAddress,
            string hookName)
        {
            int functionLength = HunterUpdateEndRva - HunterUpdateStartRva;
            if (functionLength <= 0 ||
                HunterUpdateStartRva > memory.Length - functionLength)
            {
                throw new InvalidOperationException(
                    "HunterUpdate branch-audit range lies outside the module image.");
            }

            ulong functionAddress = libraryBase + unchecked((ulong)HunterUpdateStartRva);
            ulong functionEndAddress = libraryBase + unchecked((ulong)HunterUpdateEndRva);
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(
                    memory.Slice(HunterUpdateStartRva, functionLength).ToArray()),
                functionAddress);
            while (decoder.IP < functionEndAddress)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || decoder.LastError != DecoderError.None)
                {
                    throw new InvalidOperationException(
                        $"HunterUpdate branch audit failed to decode RVA " +
                        $"0x{instruction.IP - libraryBase:X}.");
                }

                // An indirect jump could hide a jump-table entry into either
                // overwritten span. This audited function has none; fail closed
                // if a future binary introduces one anywhere in HunterUpdate.
                if (instruction.FlowControl == FlowControl.IndirectBranch)
                {
                    throw new InvalidOperationException(
                        $"HunterUpdate contains an unauditable indirect branch at RVA " +
                        $"0x{instruction.IP - libraryBase:X}.");
                }

                bool hasDirectTarget =
                    instruction.Op0Kind == OpKind.NearBranch16 ||
                    instruction.Op0Kind == OpKind.NearBranch32 ||
                    instruction.Op0Kind == OpKind.NearBranch64;
                bool auditedFlow =
                    instruction.FlowControl == FlowControl.ConditionalBranch ||
                    instruction.FlowControl == FlowControl.UnconditionalBranch ||
                    instruction.FlowControl == FlowControl.Call;
                if (!hasDirectTarget || !auditedFlow)
                    continue;

                ulong target = instruction.NearBranchTarget;
                bool sourceOutside = instruction.IP < hookAddress || instruction.IP >= hookEndAddress;
                if (sourceOutside && target > hookAddress && target < hookEndAddress)
                {
                    throw new InvalidOperationException(
                        $"Unsafe inbound branch into Hunter {hookName} hook: " +
                        $"sourceRva=0x{instruction.IP - libraryBase:X}, " +
                        $"targetRva=0x{target - libraryBase:X}, " +
                        $"span=[0x{hookAddress - libraryBase:X}," +
                        $"0x{hookEndAddress - libraryBase:X}).");
                }
            }

            if (decoder.IP != functionEndAddress)
            {
                throw new InvalidOperationException(
                    $"HunterUpdate branch audit ended at unexpected RVA " +
                    $"0x{decoder.IP - libraryBase:X}.");
            }
        }

        private static void ValidateDistanceBranch(
            ReadOnlySpan<byte> memory,
            int sequenceRva,
            int compareRva)
        {
            if ((uint)(compareRva + 5) > (uint)memory.Length ||
                memory[compareRva] != 0x83 ||
                memory[compareRva + 1] != 0xFF ||
                memory[compareRva + 2] != 0x1C ||
                memory[compareRva + 3] != 0x7E)
            {
                throw new InvalidOperationException("The Hunter distance-28 compare bytes changed.");
            }

            int targetRva = compareRva + 5 + unchecked((sbyte)memory[compareRva + 4]);
            int expectedTargetRva = sequenceRva + DistanceTwentyEightNearBranchTargetOffset;
            if (targetRva != expectedTargetRva)
            {
                throw new InvalidOperationException(
                    $"Hunter distance-28 branch target changed: 0x{targetRva:X}, expected 0x{expectedTargetRva:X}.");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            featureAvailable = false;
            transaction?.Unload();
            transaction?.Dispose();
            transaction = null;
            lock (stateLock)
            {
                activeAttempts.Clear();
                suspendedAttempts.Clear();
                lastPreparationRejections.Clear();
                lastWorldVisibilityDecisions.Clear();
                lastTileVisibilityDecisions.Clear();
                lastWorldRefreshes.Clear();
            }
        }

        private readonly struct AttemptIdentity : IEquatable<AttemptIdentity>
        {
            public readonly int HunterUnitId;
            public readonly uint HunterGlobalId;
            public readonly int PreyUnitId;
            public readonly uint PreyGlobalId;

            public AttemptIdentity(
                int hunterUnitId,
                uint hunterGlobalId,
                int preyUnitId,
                uint preyGlobalId)
            {
                HunterUnitId = hunterUnitId;
                HunterGlobalId = hunterGlobalId;
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
            }

            public bool Equals(AttemptIdentity other) =>
                HunterUnitId == other.HunterUnitId &&
                HunterGlobalId == other.HunterGlobalId &&
                PreyUnitId == other.PreyUnitId &&
                PreyGlobalId == other.PreyGlobalId;

            public override bool Equals(object obj) => obj is AttemptIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = HunterUnitId;
                    hash = (hash * 397) ^ (int)HunterGlobalId;
                    hash = (hash * 397) ^ PreyUnitId;
                    hash = (hash * 397) ^ (int)PreyGlobalId;
                    return hash;
                }
            }
        }

        private readonly struct ContinuationAttempt
        {
            public readonly AttemptIdentity Identity;
            public readonly long LastProgressAt;
            public readonly ushort LastProgress;
            public readonly uint LastPathLength;
            public readonly int Continuations;

            public ContinuationAttempt(
                AttemptIdentity identity,
                long startedAt,
                ushort lastProgress,
                uint lastPathLength,
                long lastProgressAt = 0,
                int continuations = 0)
            {
                Identity = identity;
                LastProgressAt = lastProgressAt == 0 ? startedAt : lastProgressAt;
                LastProgress = lastProgress;
                LastPathLength = lastPathLength;
                Continuations = continuations;
            }

            public ContinuationAttempt WithPathProgress(
                ushort progress,
                uint pathLength,
                long timestamp) =>
                new ContinuationAttempt(
                    Identity,
                    LastProgressAt,
                    progress,
                    pathLength,
                    timestamp,
                    Continuations);

            public ContinuationAttempt WithContinuation() =>
                new ContinuationAttempt(
                    Identity,
                    LastProgressAt,
                    LastProgress,
                    LastPathLength,
                    LastProgressAt,
                    Continuations + 1);
        }

        private readonly struct WorldRefreshObservation
        {
            public readonly AttemptIdentity Identity;
            public readonly int Distance;
            public readonly long ObservedAt;

            public WorldRefreshObservation(
                AttemptIdentity identity,
                int distance,
                long observedAt)
            {
                Identity = identity;
                Distance = distance;
                ObservedAt = observedAt;
            }
        }

        private readonly struct SuspendedAttempt
        {
            public readonly AttemptIdentity Identity;
            public readonly long RetryAt;

            public SuspendedAttempt(AttemptIdentity identity, long retryAt)
            {
                Identity = identity;
                RetryAt = retryAt;
            }
        }
    }
}
