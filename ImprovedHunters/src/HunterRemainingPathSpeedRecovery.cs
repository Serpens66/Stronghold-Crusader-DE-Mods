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
    /// Selects only Vanilla's existing Hunter distance stage when a valid path
    /// is substantially longer than the direct Manhattan distance.
    /// </summary>
    internal sealed unsafe class HunterRemainingPathSpeedRecovery : IDisposable
    {
        private const string ReferenceDllSha256 =
            "33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469";
        private const int DistanceStageSequenceRva = 0x13005C;
        private const int DistanceCompareOffset = 0x7;
        private const int DistanceNearBranchTargetOffset = 0x27;
        private const int GameUnitManagerRva = 0x67E7400;
        private const int PathBufferManagerOffset = 0xB4FE78;
        private const int EffectivePathBufferRva = 0x7337278;
        private const int PathBytesPerUnit = 0x3E8;
        private const uint MaximumPathSteps = PathBytesPerUnit * 2;
        private const int HunterAiStateOffset = 0x2BC;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int HunterPathStateOffset = 0xF2;
        private const int HunterPathFieldF4Offset = 0xF4;
        private const int HunterPathProgressOffset = 0xF6;
        private const int HunterPathLengthOffset = 0xF8;
        private const int HunterPathAdvanceControlOffset = 0x3F0;
        private const ushort HunterStateFollowingTarget = 1;
        private const ushort ActivePathState = 2;
        private const int MaximumSelectedDistance = 41;
        private const int MaximumVisibilityResult = 432;
        private const int MaxDiagnosticLogs = 600;

        private const string DistanceStageSequencePattern =
            "48 69 CB 90 04 00 00 83 FF 28 7E 1B " +
            "BE 01 00 00 00 42 89 B4 29 60 06 00 00 " +
            "66 42 89 B4 29 A4 09 00 00";

        private static readonly long MaxAttemptDuration = Stopwatch.Frequency * 60;
        private static readonly long MaxNoProgressDuration = Stopwatch.Frequency * 3;
        private static readonly long RetryCooldownDuration = Stopwatch.Frequency * 5;
        private static readonly long AttemptContinuityGap = Stopwatch.Frequency;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly HunterNativeVisibilityProbe visibilityProbe;
        private readonly Func<bool> canRun;
        private readonly ulong pathBufferAddress;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, SpeedAttempt> activeAttempts =
            new Dictionary<int, SpeedAttempt>();
        private readonly Dictionary<int, SuspendedAttempt> suspendedAttempts =
            new Dictionary<int, SuspendedAttempt>();
        private readonly Dictionary<int, ObservationState> observations =
            new Dictionary<int, ObservationState>();
        private readonly HashSet<string> loggedSkipReasons = new HashSet<string>();
        private HookTransaction transaction;
        private HookRef<X64InlineHook> distanceCompareHook = new HookRef<X64InlineHook>();
        private bool featureAvailable;
        private bool hookConfirmed;
        private int diagnosticLogs;
        private bool disposed;

        public HunterRemainingPathSpeedRecovery(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            HunterNativeVisibilityProbe visibilityProbe,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches,
            Func<bool> canRun)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.visibilityProbe = visibilityProbe ?? throw new ArgumentNullException(nameof(visibilityProbe));
            this.canRun = canRun ?? throw new ArgumentNullException(nameof(canRun));

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Improved Hunters remaining-path speed recovery unavailable: " +
                    $"DLL hash differs from audited SHA-256 {ReferenceDllSha256}; behavior remains unchanged.");
                return;
            }
            if (!visibilityProbe.IsAvailable)
                throw new InvalidOperationException("The validated native Hunter visibility probe is unavailable.");
            if (memory.Length == 0 || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            int sequenceRva = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DistanceStageSequencePattern,
                DistanceStageSequenceRva,
                referenceHashMatches,
                "Hunter state-1 initial distance stage",
                log).Rva;
            int compareRva = checked(sequenceRva + DistanceCompareOffset);
            ValidateDistanceBranch(memory, sequenceRva, compareRva);

            NativePointer<GameUnitManager> unitManager =
                GameUnitManagerAPI.Instance.GetUnitManager();
            if (unitManager.IsNull)
                throw new InvalidOperationException("The native GameUnitManager is unavailable.");

            ulong unitManagerAddress = (ulong)unitManager.Pointer;
            ulong expectedUnitManagerAddress = checked(libraryBase + (ulong)GameUnitManagerRva);
            if (unitManagerAddress != expectedUnitManagerAddress)
            {
                throw new InvalidOperationException(
                    "The native GameUnitManager address does not match the audited exact-hash RVA: " +
                    $"actual=0x{unitManagerAddress:X}, expected=0x{expectedUnitManagerAddress:X}.");
            }

            // Vanilla applies 0xB4FE78 to the manager base, not to the DLL base.
            pathBufferAddress = checked(unitManagerAddress + (ulong)PathBufferManagerOffset);
            ulong expectedPathBufferAddress = checked(libraryBase + (ulong)EffectivePathBufferRva);
            if (pathBufferAddress != expectedPathBufferAddress)
                throw new InvalidOperationException("The audited Hunter path-buffer address is inconsistent.");

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
                    TrySelectRemainingPathStage,
                    regs: X64SmartCPUContextRegs.Volatile |
                        X64SmartCPUContextRegs.RBX |
                        X64SmartCPUContextRegs.RDI,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();
                if (!distanceCompareHook.Success)
                    throw new InvalidOperationException("The Hunter initial distance-stage hook was not installed.");

                featureAvailable = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters remaining-path speed recovery initialized: " +
                    $"sequenceRva=0x{sequenceRva:X}, compareRva=0x{compareRva:X}, " +
                    $"nearBranchTargetRva=0x{sequenceRva + DistanceNearBranchTargetOffset:X}, " +
                    $"gameUnitManagerRva=0x{GameUnitManagerRva:X}, " +
                    $"pathBufferManagerOffset=0x{PathBufferManagerOffset:X}, " +
                    $"effectivePathBufferRva=0x{EffectivePathBufferRva:X}, " +
                    $"pathBytesPerUnit={PathBytesPerUnit}, " +
                    $"maximumPathSteps={MaximumPathSteps}, pathLengthType=UInt32, " +
                    "metric=cardinal-plus-two-times-diagonal, inFlightCorrection=none-conservative, " +
                    "nativeVisibilityRequired=True, ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "directSpeedWrite=False, directAnimationWrite=False, registerOverride=RDI-distance-only.");
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
            visibilityProbe.IsAvailable;

        public void ResetForMap()
        {
            lock (stateLock)
            {
                activeAttempts.Clear();
                suspendedAttempts.Clear();
                observations.Clear();
                loggedSkipReasons.Clear();
            }

            hookConfirmed = false;
            diagnosticLogs = 0;
        }

        private void TrySelectRemainingPathStage(NativePointer<X64SmartCPUContext> context)
        {
            int nativeDistance = unchecked((int)(uint)context.Pointer->RDI);
            if (!IsAvailable || !canRun() || nativeDistance < 0)
                return;

            int hunterUnitId = unchecked((int)(uint)context.Pointer->RBX);
            if (nativeDistance > 40)
            {
                ClearHunterState(hunterUnitId);
                return;
            }

            if (!TryCapturePath(
                    hunterUnitId,
                    out GameUnit* hunter,
                    out GameUnit* prey,
                    out PathSnapshot snapshot,
                    out string failureReason))
            {
                StopAttempt(hunterUnitId, "invalid-path-context", nativeDistance, logRelease: false);
                LogSkipOnce(failureReason, hunterUnitId, nativeDistance);
                return;
            }

            if (!hookConfirmed)
            {
                hookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters remaining-path speed hook confirmed: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                    $"target={snapshot.PreyUnitId}/{snapshot.PreyGlobalId}, " +
                    $"nativeDistance={nativeDistance}, path={snapshot.PathState}/" +
                    $"{snapshot.PathProgress}/{snapshot.PathLength}.");
            }

            int nativeSpeed = GetVanillaSpeed(nativeDistance);
            int routeSpeed = GetVanillaSpeed(snapshot.RemainingManhattanCost);
            if (routeSpeed >= nativeSpeed)
            {
                StopAttempt(hunterUnitId, "same-or-slower-stage", nativeDistance, logRelease: false);
                RecordObservation(
                    hunterUnitId,
                    hunter,
                    snapshot,
                    nativeDistance,
                    nativeSpeed,
                    routeSpeed,
                    visibilityResult: -1,
                    selectedDistance: nativeDistance,
                    behaviorMutation: false);
                return;
            }

            if (!visibilityProbe.TryEvaluateDirectVisibility(
                    hunterUnitId,
                    hunter->r_GlobalId,
                    snapshot.PreyUnitId,
                    snapshot.PreyGlobalId,
                    prey->r_UnitChimp,
                    out int visibilityResult) ||
                visibilityResult < 0 ||
                visibilityResult > MaximumVisibilityResult)
            {
                StopAttempt(hunterUnitId, "visibility-probe-unavailable", nativeDistance, logRelease: false);
                LogSkipOnce("visibility-probe-unavailable-or-invalid", hunterUnitId, nativeDistance);
                return;
            }

            if (visibilityResult > 0)
            {
                StopAttempt(hunterUnitId, "visibility-clear", nativeDistance, logRelease: true);
                RecordObservation(
                    hunterUnitId,
                    hunter,
                    snapshot,
                    nativeDistance,
                    nativeSpeed,
                    routeSpeed,
                    visibilityResult,
                    nativeDistance,
                    behaviorMutation: false);
                return;
            }

            if (!canRun() || !SnapshotStillCurrent(hunter, prey, snapshot))
            {
                StopAttempt(hunterUnitId, "context-changed-before-selection", nativeDistance, logRelease: false);
                LogSkipOnce("context-changed-before-selection", hunterUnitId, nativeDistance);
                return;
            }

            AttemptIdentity identity = new AttemptIdentity(
                hunterUnitId,
                hunter->r_GlobalId,
                snapshot.PreyUnitId,
                snapshot.PreyGlobalId);
            long timestamp = Stopwatch.GetTimestamp();
            if (!TryContinueAttempt(identity, snapshot, timestamp, nativeDistance))
                return;

            int selectedDistance = Math.Max(
                nativeDistance,
                Math.Min(snapshot.RemainingManhattanCost, MaximumSelectedDistance));

            // The relocated compare and every speed/animation write remain Vanilla-owned.
            context.Pointer->RDI = unchecked((ulong)(uint)selectedDistance);
            RecordObservation(
                hunterUnitId,
                hunter,
                snapshot,
                nativeDistance,
                nativeSpeed,
                routeSpeed,
                visibilityResult,
                selectedDistance,
                behaviorMutation: true);
        }

        private bool TryCapturePath(
            int hunterUnitId,
            out GameUnit* hunter,
            out GameUnit* prey,
            out PathSnapshot snapshot,
            out string failureReason)
        {
            hunter = null;
            prey = null;
            snapshot = default;
            failureReason = "invalid-hunter";

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
            {
                failureReason = "hunter-not-state-1";
                return false;
            }

            int preyUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint preyGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
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
                failureReason = "invalid-target-identity";
                return false;
            }

            ushort pathState = *(ushort*)(hunterBytes + HunterPathStateOffset);
            ushort pathFieldF4 = *(ushort*)(hunterBytes + HunterPathFieldF4Offset);
            ushort pathProgress = *(ushort*)(hunterBytes + HunterPathProgressOffset);
            uint pathLength = *(uint*)(hunterBytes + HunterPathLengthOffset);
            ushort pathAdvanceControl = *(ushort*)(hunterBytes + HunterPathAdvanceControlOffset);
            if (pathState != ActivePathState ||
                pathLength <= 1 ||
                pathLength > MaximumPathSteps ||
                pathProgress >= pathLength)
            {
                failureReason = "path-unavailable-complete-or-out-of-range";
                return false;
            }

            if (!TryDecodeRemainingPath(
                    hunterUnitId,
                    pathProgress,
                    pathLength,
                    out int cardinalSteps,
                    out int diagonalSteps,
                    out int remainingManhattanCost))
            {
                failureReason = "invalid-packed-path-direction";
                return false;
            }

            if (*(ushort*)(hunterBytes + HunterPathStateOffset) != pathState ||
                *(ushort*)(hunterBytes + HunterPathProgressOffset) != pathProgress ||
                *(uint*)(hunterBytes + HunterPathLengthOffset) != pathLength)
            {
                failureReason = "path-changed-during-read";
                return false;
            }

            uint remainingSteps = pathLength - pathProgress;
            if ((uint)(cardinalSteps + diagonalSteps) != remainingSteps)
            {
                failureReason = "path-count-invariant-failed";
                return false;
            }

            snapshot = new PathSnapshot(
                preyUnitId,
                preyGlobalId,
                pathState,
                pathFieldF4,
                pathProgress,
                pathLength,
                pathAdvanceControl,
                cardinalSteps,
                diagonalSteps,
                remainingManhattanCost);
            failureReason = null;
            return true;
        }

        private bool TryDecodeRemainingPath(
            int hunterUnitId,
            ushort pathProgress,
            uint pathLength,
            out int cardinalSteps,
            out int diagonalSteps,
            out int remainingManhattanCost)
        {
            cardinalSteps = 0;
            diagonalSteps = 0;
            remainingManhattanCost = 0;

            ulong unitPathAddress = checked(
                pathBufferAddress + (ulong)hunterUnitId * PathBytesPerUnit);
            byte* unitPath = (byte*)unitPathAddress;
            for (uint stepIndex = pathProgress; stepIndex < pathLength; stepIndex++)
            {
                byte packedDirections = unitPath[(int)(stepIndex >> 1)];
                int direction = (stepIndex & 1) == 0
                    ? packedDirections & 0xF
                    : packedDirections >> 4;
                if ((uint)direction > 7)
                    return false;

                if ((direction & 1) == 0)
                {
                    cardinalSteps++;
                    remainingManhattanCost++;
                }
                else
                {
                    diagonalSteps++;
                    remainingManhattanCost += 2;
                }
            }

            return true;
        }

        private static bool SnapshotStillCurrent(
            GameUnit* hunter,
            GameUnit* prey,
            PathSnapshot snapshot)
        {
            if (hunter == null ||
                prey == null ||
                hunter->r_AliveState != AliveState.IsAlive ||
                prey->r_AliveState != AliveState.IsAlive ||
                prey->r_GlobalId != snapshot.PreyGlobalId)
            {
                return false;
            }

            byte* hunterBytes = (byte*)hunter;
            return *(ushort*)(hunterBytes + HunterAiStateOffset) == HunterStateFollowingTarget &&
                *(ushort*)(hunterBytes + HunterTargetUnitIdOffset) == snapshot.PreyUnitId &&
                *(uint*)(hunterBytes + HunterTargetGlobalIdOffset) == snapshot.PreyGlobalId &&
                *(ushort*)(hunterBytes + HunterPathStateOffset) == snapshot.PathState &&
                *(ushort*)(hunterBytes + HunterPathProgressOffset) == snapshot.PathProgress &&
                *(uint*)(hunterBytes + HunterPathLengthOffset) == snapshot.PathLength;
        }

        private bool TryContinueAttempt(
            AttemptIdentity identity,
            PathSnapshot snapshot,
            long timestamp,
            int nativeDistance)
        {
            lock (stateLock)
            {
                if (activeAttempts.TryGetValue(identity.HunterUnitId, out SpeedAttempt current) &&
                    !current.Identity.Equals(identity))
                {
                    activeAttempts.Remove(identity.HunterUnitId);
                }

                if (suspendedAttempts.TryGetValue(identity.HunterUnitId, out SuspendedAttempt suspended))
                {
                    if (suspended.Identity.Equals(identity) && timestamp < suspended.RetryAt)
                        return false;
                    suspendedAttempts.Remove(identity.HunterUnitId);
                }

                if (!activeAttempts.TryGetValue(identity.HunterUnitId, out SpeedAttempt attempt) ||
                    timestamp - attempt.LastObservedAt > AttemptContinuityGap)
                {
                    attempt = new SpeedAttempt(
                        identity,
                        timestamp,
                        snapshot.PathProgress,
                        snapshot.PathLength);
                }
                else if (snapshot.PathProgress != attempt.LastProgress ||
                    snapshot.PathLength != attempt.LastPathLength)
                {
                    attempt = attempt.WithProgress(
                        snapshot.PathProgress,
                        snapshot.PathLength,
                        timestamp);
                }

                bool maxDurationReached = timestamp - attempt.StartedAt > MaxAttemptDuration;
                bool noProgressReached = timestamp - attempt.LastProgressAt > MaxNoProgressDuration;
                if (maxDurationReached || noProgressReached)
                {
                    activeAttempts.Remove(identity.HunterUnitId);
                    suspendedAttempts[identity.HunterUnitId] = new SuspendedAttempt(
                        identity,
                        timestamp + RetryCooldownDuration);
                    LogDiagnostic(
                        "Improved Hunters remaining-path speed recovery stopped: " +
                        $"hunter={identity.HunterUnitId}/{identity.HunterGlobalId}, " +
                        $"target={identity.PreyUnitId}/{identity.PreyGlobalId}, " +
                        $"reason={(maxDurationReached ? "max-duration" : "no-progress")}, " +
                        $"nativeDistance={nativeDistance}, path={snapshot.PathState}/" +
                        $"{snapshot.PathProgress}/{snapshot.PathLength}, selections={attempt.Selections}, " +
                        $"retryCooldownSeconds={RetryCooldownDuration / Stopwatch.Frequency}, " +
                        "currentCallbackMutation=False.",
                        warning: true);
                    return false;
                }

                activeAttempts[identity.HunterUnitId] = attempt.WithSelection(timestamp);
                return true;
            }
        }

        private void StopAttempt(
            int hunterUnitId,
            string reason,
            int nativeDistance,
            bool logRelease)
        {
            if (hunterUnitId <= 0)
                return;

            SpeedAttempt attempt = default;
            bool removed;
            lock (stateLock)
            {
                removed = activeAttempts.TryGetValue(hunterUnitId, out attempt);
                activeAttempts.Remove(hunterUnitId);
                suspendedAttempts.Remove(hunterUnitId);
            }

            if (removed && logRelease)
            {
                LogDiagnostic(
                    "Improved Hunters released remaining-path speed recovery: " +
                    $"hunter={attempt.Identity.HunterUnitId}/{attempt.Identity.HunterGlobalId}, " +
                    $"target={attempt.Identity.PreyUnitId}/{attempt.Identity.PreyGlobalId}, " +
                    $"reason={reason}, nativeDistance={nativeDistance}, " +
                    $"selections={attempt.Selections}, currentCallbackMutation=False.");
            }
        }

        private void ClearHunterState(int hunterUnitId)
        {
            if (hunterUnitId <= 0)
                return;

            lock (stateLock)
            {
                activeAttempts.Remove(hunterUnitId);
                suspendedAttempts.Remove(hunterUnitId);
                observations.Remove(hunterUnitId);
            }
        }

        private void RecordObservation(
            int hunterUnitId,
            GameUnit* hunter,
            PathSnapshot snapshot,
            int nativeDistance,
            int nativeSpeed,
            int routeSpeed,
            int visibilityResult,
            int selectedDistance,
            bool behaviorMutation)
        {
            ObservationState observation = new ObservationState(
                hunter->r_GlobalId,
                snapshot.PreyUnitId,
                snapshot.PreyGlobalId,
                snapshot.PathProgress,
                snapshot.PathLength,
                nativeSpeed,
                routeSpeed,
                visibilityResult,
                behaviorMutation);
            bool shouldLog;
            lock (stateLock)
            {
                shouldLog = !observations.TryGetValue(hunterUnitId, out ObservationState previous) ||
                    !previous.HasSameIdentity(observation) ||
                    previous.NativeSpeed != observation.NativeSpeed ||
                    previous.RouteSpeed != observation.RouteSpeed ||
                    previous.VisibilityResult != observation.VisibilityResult ||
                    previous.BehaviorMutation != observation.BehaviorMutation ||
                    (snapshot.RemainingManhattanCost > nativeDistance &&
                        (previous.PathProgress != observation.PathProgress ||
                            previous.PathLength != observation.PathLength));
                observations[hunterUnitId] = observation;
            }

            if (!shouldLog)
                return;

            uint remainingSteps = snapshot.PathLength - snapshot.PathProgress;
            LogDiagnostic(
                "Improved Hunters remaining-path speed observation: " +
                $"hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                $"target={snapshot.PreyUnitId}/{snapshot.PreyGlobalId}, " +
                $"nativeDistance={nativeDistance}, decodedRemainingCost={snapshot.RemainingManhattanCost}, " +
                $"selectedDistance={selectedDistance}, nativeSpeed={nativeSpeed}, routeSpeed={routeSpeed}, " +
                $"currentSpeedBefore={hunter->r_CurrentSpeed}, visibility={visibilityResult}, " +
                $"path={snapshot.PathState}/{snapshot.PathFieldF4}/" +
                $"{snapshot.PathProgress}/{snapshot.PathLength}, remainingEntries={remainingSteps}, " +
                $"cardinal={snapshot.CardinalSteps}, diagonal={snapshot.DiagonalSteps}, " +
                $"countInvariant={remainingSteps == (uint)(snapshot.CardinalSteps + snapshot.DiagonalSteps)}, " +
                $"advanceControl=0x{snapshot.PathAdvanceControl:X}, " +
                "inFlightCorrection=none-conservative, " +
                $"behaviorMutation={behaviorMutation}, registerOverrideOnly={behaviorMutation}.");
        }

        private void LogSkipOnce(string reason, int hunterUnitId, int nativeDistance)
        {
            if (string.IsNullOrEmpty(reason))
                reason = "unknown";

            lock (stateLock)
            {
                if (!loggedSkipReasons.Add(reason))
                    return;
            }

            LogDiagnostic(
                "Improved Hunters remaining-path speed recovery skipped: " +
                $"reason={reason}, hunter={hunterUnitId}, nativeDistance={nativeDistance}, " +
                "currentCallbackMutation=False.",
                warning: true);
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

        private static int GetVanillaSpeed(int distance)
        {
            if (distance > 40)
                return 1;
            if (distance > 36)
                return 2;
            if (distance > 34)
                return 3;
            if (distance > 32)
                return 4;
            if (distance > 30)
                return 6;
            if (distance > 28)
                return 8;
            return 10;
        }

        private static void ValidateDistanceBranch(
            ReadOnlySpan<byte> memory,
            int sequenceRva,
            int compareRva)
        {
            if ((uint)(compareRva + 5) > (uint)memory.Length ||
                memory[compareRva] != 0x83 ||
                memory[compareRva + 1] != 0xFF ||
                memory[compareRva + 2] != 0x28 ||
                memory[compareRva + 3] != 0x7E)
            {
                throw new InvalidOperationException("The Hunter distance-40 compare bytes changed.");
            }

            int targetRva = compareRva + 5 + unchecked((sbyte)memory[compareRva + 4]);
            int expectedTargetRva = sequenceRva + DistanceNearBranchTargetOffset;
            if (targetRva != expectedTargetRva)
            {
                throw new InvalidOperationException(
                    $"Hunter distance-40 branch target changed: 0x{targetRva:X}, expected 0x{expectedTargetRva:X}.");
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
                observations.Clear();
                loggedSkipReasons.Clear();
            }
        }

        private readonly struct PathSnapshot
        {
            public readonly int PreyUnitId;
            public readonly uint PreyGlobalId;
            public readonly ushort PathState;
            public readonly ushort PathFieldF4;
            public readonly ushort PathProgress;
            public readonly uint PathLength;
            public readonly ushort PathAdvanceControl;
            public readonly int CardinalSteps;
            public readonly int DiagonalSteps;
            public readonly int RemainingManhattanCost;

            public PathSnapshot(
                int preyUnitId,
                uint preyGlobalId,
                ushort pathState,
                ushort pathFieldF4,
                ushort pathProgress,
                uint pathLength,
                ushort pathAdvanceControl,
                int cardinalSteps,
                int diagonalSteps,
                int remainingManhattanCost)
            {
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
                PathState = pathState;
                PathFieldF4 = pathFieldF4;
                PathProgress = pathProgress;
                PathLength = pathLength;
                PathAdvanceControl = pathAdvanceControl;
                CardinalSteps = cardinalSteps;
                DiagonalSteps = diagonalSteps;
                RemainingManhattanCost = remainingManhattanCost;
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

        private readonly struct SpeedAttempt
        {
            public readonly AttemptIdentity Identity;
            public readonly long StartedAt;
            public readonly long LastProgressAt;
            public readonly long LastObservedAt;
            public readonly ushort LastProgress;
            public readonly uint LastPathLength;
            public readonly int Selections;

            public SpeedAttempt(
                AttemptIdentity identity,
                long startedAt,
                ushort lastProgress,
                uint lastPathLength,
                long lastProgressAt = 0,
                long lastObservedAt = 0,
                int selections = 0)
            {
                Identity = identity;
                StartedAt = startedAt;
                LastProgressAt = lastProgressAt == 0 ? startedAt : lastProgressAt;
                LastObservedAt = lastObservedAt == 0 ? startedAt : lastObservedAt;
                LastProgress = lastProgress;
                LastPathLength = lastPathLength;
                Selections = selections;
            }

            public SpeedAttempt WithProgress(
                ushort progress,
                uint pathLength,
                long timestamp) =>
                new SpeedAttempt(
                    Identity,
                    StartedAt,
                    progress,
                    pathLength,
                    timestamp,
                    timestamp,
                    Selections);

            public SpeedAttempt WithSelection(long timestamp) =>
                new SpeedAttempt(
                    Identity,
                    StartedAt,
                    LastProgress,
                    LastPathLength,
                    LastProgressAt,
                    timestamp,
                    Selections + 1);
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

        private readonly struct ObservationState
        {
            public readonly uint HunterGlobalId;
            public readonly int PreyUnitId;
            public readonly uint PreyGlobalId;
            public readonly ushort PathProgress;
            public readonly uint PathLength;
            public readonly int NativeSpeed;
            public readonly int RouteSpeed;
            public readonly int VisibilityResult;
            public readonly bool BehaviorMutation;

            public ObservationState(
                uint hunterGlobalId,
                int preyUnitId,
                uint preyGlobalId,
                ushort pathProgress,
                uint pathLength,
                int nativeSpeed,
                int routeSpeed,
                int visibilityResult,
                bool behaviorMutation)
            {
                HunterGlobalId = hunterGlobalId;
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
                PathProgress = pathProgress;
                PathLength = pathLength;
                NativeSpeed = nativeSpeed;
                RouteSpeed = routeSpeed;
                VisibilityResult = visibilityResult;
                BehaviorMutation = behaviorMutation;
            }

            public bool HasSameIdentity(ObservationState other) =>
                HunterGlobalId == other.HunterGlobalId &&
                PreyUnitId == other.PreyUnitId &&
                PreyGlobalId == other.PreyGlobalId;
        }
    }
}
