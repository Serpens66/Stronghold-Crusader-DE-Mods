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
        private const ushort HunterStateFollowingTarget = 1;
        private const ushort ActivePathState = 2;
        private const int VanillaContinuationDistance = 29;
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

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly HunterNativeVisibilityProbe visibilityProbe;
        private readonly Func<bool> canRun;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, ContinuationAttempt> activeAttempts =
            new Dictionary<int, ContinuationAttempt>();
        private readonly Dictionary<int, SuspendedAttempt> suspendedAttempts =
            new Dictionary<int, SuspendedAttempt>();
        private HookTransaction transaction;
        private HookRef<X64InlineHook> distanceCompareHook = new HookRef<X64InlineHook>();
        private bool featureAvailable;
        private bool hookConfirmed;
        private bool invalidContextLogged;
        private int diagnosticLogs;
        private bool disposed;

        public HunterVanillaPathContinuationDiagnostic(
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
                    "Improved Hunters Vanilla-path continuation diagnostic unavailable: " +
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
                    "registerOverride=RDI-distance-only, nativeVisibilityRequired=True.");
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
            }

            hookConfirmed = false;
            invalidContextLogged = false;
            diagnosticLogs = 0;
        }

        private void TryContinueExistingVanillaPath(NativePointer<X64SmartCPUContext> context)
        {
            int nativeDistance = unchecked((int)(uint)context.Pointer->RDI);
            if (!IsAvailable || !canRun() || nativeDistance < 0)
                return;

            int hunterUnitId = unchecked((int)(uint)context.Pointer->RBX);
            if (nativeDistance > 28)
            {
                // Distances 29 and 30 reach this compare through Vanilla's
                // normal stage ladder and need no recovery state.
                ClearHunterAttemptState(hunterUnitId);
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
                    out ushort pathLength))
            {
                ClearHunterAttemptState(hunterUnitId);
                LogInvalidContextOnce(hunterUnitId, nativeDistance);
                return;
            }

            if (!hookConfirmed)
            {
                hookConfirmed = true;
                LogDiagnostic(
                    "Improved Hunters Vanilla-path continuation hook confirmed: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, target={preyUnitId}/{preyGlobalId}, " +
                    $"nativeDistance={nativeDistance}, path={pathState}/{pathProgress}/{pathLength}.");
            }

            AttemptIdentity identity = new AttemptIdentity(
                hunterUnitId,
                hunter->r_GlobalId,
                preyUnitId,
                preyGlobalId);
            long timestamp = Stopwatch.GetTimestamp();
            if (IsRetryCoolingDown(identity, timestamp))
                return;

            if (pathState != ActivePathState || pathLength <= 1 || pathProgress >= pathLength)
            {
                StopAttempt(
                    identity,
                    $"path-unavailable-or-complete-{pathState}-{pathProgress}-{pathLength}",
                    nativeDistance,
                    pathFieldF4);
                return;
            }

            if (!visibilityProbe.TryEvaluateDirectVisibility(
                    hunterUnitId,
                    hunter->r_GlobalId,
                    preyUnitId,
                    preyGlobalId,
                    prey->r_UnitChimp,
                    out int visibilityResult))
            {
                StopAttempt(identity, "visibility-probe-unavailable", nativeDistance, pathFieldF4);
                return;
            }
            if (visibilityResult > 0)
            {
                StopAttempt(
                    identity,
                    $"visibility-clear-{visibilityResult}",
                    nativeDistance,
                    pathFieldF4);
                return;
            }

            ContinuationAttempt attempt;
            bool shouldLogContinuation;
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
                }
                else if (timestamp - attempt.LastObservedAt > AttemptContinuityGap)
                {
                    // A gap means Vanilla spent time outside this near blocked
                    // branch. Start a fresh bounded interval for the same pair.
                    attempt = new ContinuationAttempt(identity, timestamp, pathProgress, pathLength);
                }

                bool pathChanged =
                    pathProgress != attempt.LastProgress ||
                    pathLength != attempt.LastPathLength;
                if (pathChanged)
                    attempt = attempt.WithPathProgress(pathProgress, pathLength, timestamp);

                bool maxDurationReached = timestamp - attempt.StartedAt > MaxAttemptDuration;
                bool noProgressReached = timestamp - attempt.LastProgressAt > MaxNoProgressDuration;
                if (maxDurationReached || noProgressReached)
                {
                    activeAttempts.Remove(hunterUnitId);
                    suspendedAttempts[hunterUnitId] = new SuspendedAttempt(
                        identity,
                        timestamp + RetryCooldownDuration);
                    LogDiagnostic(
                        "Improved Hunters Vanilla-path continuation stopped: " +
                        $"hunter={hunterUnitId}/{hunter->r_GlobalId}, target={preyUnitId}/{preyGlobalId}, " +
                        $"reason={(maxDurationReached ? "max-duration" : "no-progress")}, " +
                        $"nativeDistance={nativeDistance}, " +
                        $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}, " +
                        $"continuations={attempt.Continuations}, " +
                        $"retryCooldownSeconds={RetryCooldownDuration / Stopwatch.Frequency}, " +
                        "currentCallbackMutation=False.",
                        warning: true);
                    return;
                }

                shouldLogContinuation = attempt.Continuations == 0 || pathChanged;
                attempt = attempt.WithContinuation(timestamp);
                activeAttempts[hunterUnitId] = attempt;
            }

            // This register is restored by HunterUpdate's epilogue. Changing it
            // only selects Vanilla's existing distance-29 stage for this update.
            context.Pointer->RDI = VanillaContinuationDistance;
            if (shouldLogContinuation)
            {
                LogDiagnostic(
                    "Improved Hunters continued existing Vanilla Hunter path: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, target={preyUnitId}/{preyGlobalId}/{prey->r_UnitChimp}, " +
                    $"nativeVisibility={visibilityResult}, nativeDistance={nativeDistance}->{VanillaContinuationDistance}, " +
                    $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}, " +
                    $"continuations={attempt.Continuations}, ownMovement=False, ownAiState=False, ownOrderWrite=False, " +
                    "registerOverride=RDI-distance-only.");
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
            out ushort pathLength)
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

            pathState = *(ushort*)(hunterBytes + HunterPathStateOffset);
            pathFieldF4 = *(ushort*)(hunterBytes + HunterPathFieldF4Offset);
            pathProgress = *(ushort*)(hunterBytes + HunterPathProgressOffset);
            pathLength = *(ushort*)(hunterBytes + HunterPathLengthOffset);
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

        private void LogInvalidContextOnce(int hunterUnitId, int nativeDistance)
        {
            if (invalidContextLogged)
                return;

            invalidContextLogged = true;
            LogDiagnostic(
                "Improved Hunters Vanilla-path continuation skipped invalid context: " +
                $"hunter={hunterUnitId}, nativeDistance={nativeDistance}, currentCallbackMutation=False.",
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
            public readonly long StartedAt;
            public readonly long LastProgressAt;
            public readonly long LastObservedAt;
            public readonly ushort LastProgress;
            public readonly ushort LastPathLength;
            public readonly int Continuations;

            public ContinuationAttempt(
                AttemptIdentity identity,
                long startedAt,
                ushort lastProgress,
                ushort lastPathLength,
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
                ushort pathLength,
                long timestamp) =>
                new ContinuationAttempt(
                    Identity,
                    StartedAt,
                    progress,
                    pathLength,
                    timestamp,
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
