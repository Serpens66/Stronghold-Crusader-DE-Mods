using BepInEx.Logging;
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
            "33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469";
        private const int DistanceStageSequenceRva = 0x1300D2;
        private const int DistanceTwentyEightCompareOffset = 0x18;
        private const int DistanceTwentyEightNearBranchTargetOffset = 0x30;
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

        private const string DistanceStageSequencePattern =
            "83 FF 1E 7E 13 B8 06 00 00 00 " +
            "66 42 89 84 29 A4 09 00 00 E9 ? ? ? ? " +
            "83 FF 1C 7E 13 B8 08 00 00 00 " +
            "66 42 89 84 29 A4 09 00 00 E9 ? ? ? ? " +
            "BE 0A 00 00 00";

        private static readonly long MaxAttemptDuration = Stopwatch.Frequency * 60;
        private static readonly long MaxNoProgressDuration = Stopwatch.Frequency * 3;
        private static readonly long RetryCooldownDuration = Stopwatch.Frequency * 5;
        private static readonly long AttemptContinuityGap = Stopwatch.Frequency;
        private static readonly long PreparedContinuationLifetime = Stopwatch.Frequency;
        private static long nextGeneration;

        [ThreadStatic] private static PreparedContinuation preparedContinuation;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly HunterNativeVisibilityProbe visibilityProbe;
        private readonly HunterPclReachability pclReachability;
        private readonly Func<bool> canRun;
        private readonly long generation;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, ContinuationAttempt> activeAttempts =
            new Dictionary<int, ContinuationAttempt>();
        private readonly Dictionary<int, SuspendedAttempt> suspendedAttempts =
            new Dictionary<int, SuspendedAttempt>();
        private readonly Dictionary<int, string> lastPreparationRejections =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> lastVisibilityDecisions =
            new Dictionary<int, string>();
        private HookTransaction transaction;
        private HookRef<X64InlineHook> distanceCompareHook = new HookRef<X64InlineHook>();
        private bool featureAvailable;
        private bool hookConfirmed;
        private int diagnosticLogs;
        private bool disposed;

        public HunterVanillaPathContinuationDiagnostic(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            HunterNativeVisibilityProbe visibilityProbe,
            HunterPclReachability pclReachability,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<bool> canRun)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.visibilityProbe = visibilityProbe ?? throw new ArgumentNullException(nameof(visibilityProbe));
            this.pclReachability = pclReachability ?? throw new ArgumentNullException(nameof(pclReachability));
            this.canRun = canRun ?? throw new ArgumentNullException(nameof(canRun));
            generation = System.Threading.Interlocked.Increment(ref nextGeneration);

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Improved Hunters Vanilla-path continuation diagnostic unavailable: " +
                    $"DLL hash differs from audited SHA-256 {ReferenceDllSha256}; behavior remains unchanged.");
                return;
            }
            if (!visibilityProbe.IsAvailable)
                throw new InvalidOperationException("The validated native Hunter visibility probe is unavailable.");
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
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();
                if (!distanceCompareHook.Success)
                    throw new InvalidOperationException("The Hunter distance-28 continuation hook was not installed.");

                featureAvailable = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters Vanilla-path continuation diagnostic initialized: " +
                    $"sequenceRva=0x{sequenceRva:X}, compareRva=0x{compareRva:X}, " +
                    $"nearBranchTargetRva=0x{sequenceRva + DistanceTwentyEightNearBranchTargetOffset:X}, " +
                    $"forcedDistance={VanillaContinuationDistance}, globalIdentityLimit=None, " +
                    "maxActiveAttempts=one-per-hunter, " +
                    $"maxSeconds={MaxAttemptDuration / Stopwatch.Frequency}, " +
                    $"maxNoProgressSeconds={MaxNoProgressDuration / Stopwatch.Frequency}, " +
                    $"boundedRetryCooldownSeconds={RetryCooldownDuration / Stopwatch.Frequency}, " +
                    "ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "speedWrite=False, animationWrite=False, " +
                    "registerOverride=RDI-distance-only, preparedTicketRequired=True, " +
                    "nearVisibility=wrapper-plus-bidirectional-core, visibleAttackHandoff=True, " +
                    "directionalDisagreementContinuesPath=True, explicitCachedPclReachableRequired=True, " +
                    "nativePclQueryInsideHook=False.");
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
            visibilityProbe.IsAvailable &&
            pclReachability.IsAvailable;

        public void ResetForMap()
        {
            lock (stateLock)
            {
                activeAttempts.Clear();
                suspendedAttempts.Clear();
                lastPreparationRejections.Clear();
                lastVisibilityDecisions.Clear();
            }

            ClearPreparedContinuation();

            hookConfirmed = false;
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
            ClearPreparedContinuation();
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

            if (!visibilityProbe.TryEvaluateNearVisibility(
                    hunterUnitId,
                    hunter->r_GlobalId,
                    preyUnitId,
                    preyGlobalId,
                    prey->r_UnitChimp,
                    out int visibilityResult,
                    out int hunterToPreyResult,
                    out int preyToHunterResult))
            {
                StopAttempt(identity, "visibility-probe-unavailable", nativeWorldDistance, pathFieldF4);
                LogPreparationRejection(
                    hunterUnitId,
                    "visibility-probe-unavailable",
                    $"target={preyUnitId}/{preyGlobalId}",
                    warning: true);
                return HunterStateOneNearRefreshAction.None;
            }

            if (visibilityResult < 0 || visibilityResult > 432)
            {
                string reason = $"visibility-wrapper-result-invalid-{visibilityResult}";
                StopAttempt(identity, reason, nativeWorldDistance, pathFieldF4);
                LogPreparationRejection(
                    hunterUnitId,
                    reason,
                    $"target={preyUnitId}/{preyGlobalId}, nativeWorldDistance={nativeWorldDistance}");
                return HunterStateOneNearRefreshAction.None;
            }

            bool wrapperPass = visibilityResult > 0;
            bool hunterToPreyPass = false;
            bool preyToHunterPass = false;
            string visibilityClassification;
            if (wrapperPass)
            {
                // Vanilla's wrapper returns after its first positive internal
                // direction. Explicit endpoint labels expose which directional
                // sample passed without simulating a live projectile.
                if (hunterToPreyResult < 0 || hunterToPreyResult > 432 ||
                    preyToHunterResult < 0 || preyToHunterResult > 432)
                {
                    string reason =
                        $"directional-visibility-result-invalid-{hunterToPreyResult}-{preyToHunterResult}";
                    StopAttempt(identity, reason, nativeWorldDistance, pathFieldF4);
                    LogPreparationRejection(
                        hunterUnitId,
                        reason,
                        $"target={preyUnitId}/{preyGlobalId}, wrapperResult={visibilityResult}",
                        warning: true);
                    return HunterStateOneNearRefreshAction.None;
                }

                hunterToPreyPass = hunterToPreyResult > 0;
                preyToHunterPass = preyToHunterResult > 0;
                visibilityClassification = hunterToPreyPass && preyToHunterPass
                    ? "visible-attack-handoff"
                    : "blocked-directional-disagreement";
            }
            else
            {
                // A zero wrapper result already means that both internal core
                // calls failed, because Vanilla only tries reverse after forward.
                visibilityClassification = "blocked-wrapper-both-directions";
            }

            bool visibilityDecisionChanged = LogVisibilityDecision(
                identity,
                nativeWorldDistance,
                visibilityResult,
                hunterToPreyResult,
                preyToHunterResult,
                wrapperPass,
                hunterToPreyPass,
                preyToHunterPass,
                visibilityClassification);
            if (wrapperPass && hunterToPreyPass && preyToHunterPass)
            {
                StopAttempt(
                    identity,
                    $"bidirectional-visibility-clear-{visibilityResult}-" +
                    $"{hunterToPreyResult}-{preyToHunterResult}",
                    nativeWorldDistance,
                    pathFieldF4);
                lock (stateLock)
                    lastPreparationRejections.Remove(hunterUnitId);
                shouldLog = visibilityDecisionChanged;
                return HunterStateOneNearRefreshAction.HandoffToVanillaAttack;
            }

            ContinuationAttempt attempt;
            bool newAttempt = false;
            bool pathChanged;
            bool maxDurationReached;
            bool noProgressReached;
            lock (stateLock)
            {
                if (activeAttempts.TryGetValue(hunterUnitId, out ContinuationAttempt current) &&
                    !current.Identity.Equals(identity))
                {
                    activeAttempts.Remove(hunterUnitId);
                }

                if (!activeAttempts.TryGetValue(hunterUnitId, out attempt) ||
                    timestamp - attempt.LastObservedAt > AttemptContinuityGap)
                {
                    attempt = new ContinuationAttempt(identity, timestamp, pathProgress, pathLength);
                    newAttempt = true;
                }

                pathChanged =
                    pathProgress != attempt.LastProgress ||
                    pathLength != attempt.LastPathLength;
                attempt = pathChanged
                    ? attempt.WithPathProgress(pathProgress, pathLength, timestamp)
                    : attempt.WithObservation(timestamp);

                maxDurationReached = timestamp - attempt.StartedAt > MaxAttemptDuration;
                noProgressReached = timestamp - attempt.LastProgressAt > MaxNoProgressDuration;
                if (maxDurationReached || noProgressReached)
                {
                    activeAttempts.Remove(hunterUnitId);
                    suspendedAttempts[hunterUnitId] = new SuspendedAttempt(
                        identity,
                        timestamp + RetryCooldownDuration);
                }
                else
                {
                    shouldLog = newAttempt || pathChanged || visibilityDecisionChanged;
                    activeAttempts[hunterUnitId] = attempt;
                    lastPreparationRejections.Remove(hunterUnitId);
                }
            }

            if (maxDurationReached || noProgressReached)
            {
                LogDiagnostic(
                    "Improved Hunters Vanilla-path continuation stopped before near refresh: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, target={preyUnitId}/{preyGlobalId}, " +
                    $"reason={(maxDurationReached ? "max-duration" : "no-progress")}, " +
                    $"nativeWorldDistance={nativeWorldDistance}, " +
                    $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}, " +
                    $"continuations={attempt.Continuations}, " +
                    $"retryCooldownSeconds={RetryCooldownDuration / Stopwatch.Frequency}, " +
                    "nearRefreshBypass=False, currentCallbackMutation=False.",
                    warning: true);
                return HunterStateOneNearRefreshAction.None;
            }

            preparedContinuation = new PreparedContinuation(
                generation,
                identity,
                timestamp + PreparedContinuationLifetime,
                nativeWorldDistance,
                shouldLog);
            if (shouldLog)
            {
                LogDiagnostic(
                    "Improved Hunters prepared existing Vanilla Hunter path continuation: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{prey->r_UnitChimp}, " +
                    $"nativeWorldDistance={nativeWorldDistance}, wrapperResult={visibilityResult}, " +
                    $"coreHunterToPreyResult={hunterToPreyResult}, " +
                    $"corePreyToHunterResult={preyToHunterResult}, " +
                    $"visibilityClassification={visibilityClassification}, " +
                    $"pclReachable={pclReachable}, pclSource=active-target-snapshot, " +
                    $"pclSnapshotStatus={pclSnapshotStatus}, " +
                    $"pclSnapshotAgeMs={pclSnapshotAgeMilliseconds}, " +
                    $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}, " +
                    "ticket=prepared, ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "speedWrite=False, animationWrite=False.");
            }

            return HunterStateOneNearRefreshAction.ContinueExistingPath;
        }

        private void TryContinueExistingVanillaPath(NativePointer<X64SmartCPUContext> context)
        {
            int nativeDistance = unchecked((int)(uint)context.Pointer->RDI);
            int hunterUnitId = unchecked((int)(uint)context.Pointer->RBX);
            PreparedContinuation ticket = preparedContinuation;
            ClearPreparedContinuation();
            if (!IsAvailable ||
                !canRun() ||
                nativeDistance < 0 ||
                !ticket.IsValid ||
                ticket.Generation != generation)
            {
                return;
            }

            long timestamp = Stopwatch.GetTimestamp();
            if (ticket.Identity.HunterUnitId != hunterUnitId || timestamp > ticket.ExpiresAt)
            {
                LogPreparationRejection(
                    hunterUnitId,
                    ticket.Identity.HunterUnitId != hunterUnitId
                        ? "continuation-ticket-hunter-mismatch"
                        : "continuation-ticket-expired",
                    $"ticketHunter={ticket.Identity.HunterUnitId}, nativeDistance={nativeDistance}",
                    warning: true);
                return;
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
                    "continuation-ticket-context-invalid",
                    $"nativeDistance={nativeDistance}",
                    warning: true);
                return;
            }

            AttemptIdentity identity = new AttemptIdentity(
                hunterUnitId,
                hunter->r_GlobalId,
                preyUnitId,
                preyGlobalId);
            if (!identity.Equals(ticket.Identity) ||
                pathState != ActivePathState ||
                pathLength <= 1 ||
                pathLength > MaximumPathSteps ||
                pathProgress >= pathLength)
            {
                ClearHunterAttemptState(hunterUnitId);
                LogPreparationRejection(
                    hunterUnitId,
                    "continuation-ticket-revalidation-failed",
                    $"ticketTarget={ticket.Identity.PreyUnitId}/{ticket.Identity.PreyGlobalId}, " +
                    $"actualTarget={preyUnitId}/{preyGlobalId}, path={pathState}/{pathFieldF4}/" +
                    $"{pathProgress}/{pathLength}",
                    warning: true);
                return;
            }

            ContinuationAttempt attempt;
            lock (stateLock)
            {
                if (!activeAttempts.TryGetValue(hunterUnitId, out attempt) ||
                    !attempt.Identity.Equals(identity))
                {
                    return;
                }

                attempt = attempt.WithContinuation(timestamp);
                activeAttempts[hunterUnitId] = attempt;
            }

            bool distanceOverrideApplied = nativeDistance <= 28;
            if (distanceOverrideApplied)
            {
                // RDI is restored by HunterUpdate's epilogue. The value selects
                // Vanilla's existing distance-29 movement branch for this update.
                context.Pointer->RDI = VanillaContinuationDistance;
            }

            if (!hookConfirmed)
            {
                hookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters Vanilla-path continuation hook confirmed: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, target={preyUnitId}/{preyGlobalId}, " +
                    $"nativeDistance={nativeDistance}, path={pathState}/{pathProgress}/{pathLength}.");
            }

            if (ticket.ShouldLog)
            {
                LogDiagnostic(
                    "Improved Hunters consumed Vanilla Hunter path continuation ticket: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{prey->r_UnitChimp}, " +
                    $"nativeWorldDistance={ticket.NativeWorldDistance}, " +
                    $"nativeDistance={nativeDistance}->" +
                    $"{(distanceOverrideApplied ? VanillaContinuationDistance : nativeDistance)}, " +
                    $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}, " +
                    $"continuations={attempt.Continuations}, ticket=consumed, " +
                    "ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "speedWrite=False, animationWrite=False, " +
                    $"registerOverride={(distanceOverrideApplied ? "RDI-distance-only" : "none")}, " +
                    "transitionPhase=vanilla-path-continuation, " +
                    $"{HunterMovementSnapshot.TryFormat(hunter)}.");
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
            int wrapperResult,
            int hunterToPreyResult,
            int preyToHunterResult,
            bool wrapperPass,
            bool hunterToPreyPass,
            bool preyToHunterPass,
            string classification)
        {
            string signature =
                $"{identity.PreyUnitId}/{identity.PreyGlobalId}/{classification}";
            bool shouldLog;
            lock (stateLock)
            {
                shouldLog = !lastVisibilityDecisions.TryGetValue(
                        identity.HunterUnitId,
                        out string previous) ||
                    !string.Equals(previous, signature, StringComparison.Ordinal);
                if (shouldLog)
                    lastVisibilityDecisions[identity.HunterUnitId] = signature;
            }

            if (!shouldLog)
                return false;

            bool directionalCoreRan = hunterToPreyResult >= 0 && preyToHunterResult >= 0;
            string wrapperMatchingDirection;
            if (!directionalCoreRan)
            {
                wrapperMatchingDirection = "not-tested";
            }
            else if (wrapperResult == hunterToPreyResult && wrapperResult == preyToHunterResult)
            {
                wrapperMatchingDirection = "both";
            }
            else if (wrapperResult == hunterToPreyResult)
            {
                wrapperMatchingDirection = "hunter-to-prey";
            }
            else if (wrapperResult == preyToHunterResult)
            {
                wrapperMatchingDirection = "prey-to-hunter";
            }
            else
            {
                wrapperMatchingDirection = "neither";
            }

            LogDiagnostic(
                "Improved Hunters classified Hunter near-target visibility: " +
                $"hunter={identity.HunterUnitId}/{identity.HunterGlobalId}, " +
                $"target={identity.PreyUnitId}/{identity.PreyGlobalId}, " +
                $"nativeWorldDistance={nativeWorldDistance}, wrapperResult={wrapperResult}, " +
                $"coreHunterToPreyResult=" +
                $"{(directionalCoreRan ? hunterToPreyResult.ToString() : "not-run")}, " +
                $"corePreyToHunterResult=" +
                $"{(directionalCoreRan ? preyToHunterResult.ToString() : "not-run")}, " +
                $"wrapperPass={wrapperPass}, coreHunterToPreyPass={hunterToPreyPass}, " +
                $"corePreyToHunterPass={preyToHunterPass}, " +
                $"wrapperMatchingDirection={wrapperMatchingDirection}, " +
                $"classification={classification}, " +
                "physicalArrowCollisionPreflight=False, behaviorMutation=False.",
                warning: wrapperPass && !(hunterToPreyPass && preyToHunterPass));
            return true;
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

        private static void ClearPreparedContinuation()
        {
            preparedContinuation = default;
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
                lastVisibilityDecisions.Clear();
            }
            ClearPreparedContinuation();
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
            public readonly long StartedAt;
            public readonly long LastProgressAt;
            public readonly long LastObservedAt;
            public readonly ushort LastProgress;
            public readonly uint LastPathLength;
            public readonly int Continuations;

            public ContinuationAttempt(
                AttemptIdentity identity,
                long startedAt,
                ushort lastProgress,
                uint lastPathLength,
                long lastProgressAt = 0,
                long lastObservedAt = 0,
                int continuations = 0)
            {
                Identity = identity;
                StartedAt = startedAt;
                LastProgressAt = lastProgressAt == 0 ? startedAt : lastProgressAt;
                LastObservedAt = lastObservedAt == 0 ? startedAt : lastObservedAt;
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
                    StartedAt,
                    progress,
                    pathLength,
                    timestamp,
                    timestamp,
                    Continuations);

            public ContinuationAttempt WithObservation(long timestamp) =>
                new ContinuationAttempt(
                    Identity,
                    StartedAt,
                    LastProgress,
                    LastPathLength,
                    LastProgressAt,
                    timestamp,
                    Continuations);

            public ContinuationAttempt WithContinuation(long timestamp) =>
                new ContinuationAttempt(
                    Identity,
                    StartedAt,
                    LastProgress,
                    LastPathLength,
                    LastProgressAt,
                    timestamp,
                    Continuations + 1);
        }

        private readonly struct PreparedContinuation
        {
            public readonly long Generation;
            public readonly AttemptIdentity Identity;
            public readonly long ExpiresAt;
            public readonly int NativeWorldDistance;
            public readonly bool ShouldLog;

            public PreparedContinuation(
                long generation,
                AttemptIdentity identity,
                long expiresAt,
                int nativeWorldDistance,
                bool shouldLog)
            {
                Generation = generation;
                Identity = identity;
                ExpiresAt = expiresAt;
                NativeWorldDistance = nativeWorldDistance;
                ShouldLog = shouldLog;
            }

            public bool IsValid =>
                Generation > 0 &&
                Identity.HunterUnitId > 0 &&
                Identity.HunterGlobalId != 0 &&
                Identity.PreyUnitId > 0 &&
                Identity.PreyGlobalId != 0;
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
