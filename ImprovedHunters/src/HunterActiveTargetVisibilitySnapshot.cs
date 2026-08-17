using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    internal enum HunterActiveVisibilityState
    {
        Pending = 0,
        Blocked = 1,
        Visible = 2
    }

    internal readonly struct HunterActiveVisibilityObservation
    {
        public readonly HunterActiveVisibilityState State;
        public readonly string Status;
        public readonly string Classification;
        public readonly long SnapshotAgeMilliseconds;
        public readonly long PendingAgeMilliseconds;
        public readonly long PathGeneration;
        public readonly int WrapperResult;
        public readonly int HunterToPreyResult;
        public readonly int PreyToHunterResult;

        public HunterActiveVisibilityObservation(
            HunterActiveVisibilityState state,
            string status,
            string classification,
            long snapshotAgeMilliseconds,
            long pendingAgeMilliseconds,
            long pathGeneration,
            int wrapperResult,
            int hunterToPreyResult,
            int preyToHunterResult)
        {
            State = state;
            Status = status;
            Classification = classification;
            SnapshotAgeMilliseconds = snapshotAgeMilliseconds;
            PendingAgeMilliseconds = pendingAgeMilliseconds;
            PathGeneration = pathGeneration;
            WrapperResult = wrapperResult;
            HunterToPreyResult = hunterToPreyResult;
            PreyToHunterResult = preyToHunterResult;
        }
    }

    /// <summary>
    /// Refreshes native visibility for active Hunter targets outside inline
    /// hooks. Hook callbacks consume only short-lived immutable observations.
    /// </summary>
    internal sealed unsafe class HunterActiveTargetVisibilitySnapshot : IDisposable
    {
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
        private const int MaxDiagnosticLogs = 240;
        private const int MaxProbeSampleLogs = 240;

        private static readonly long ProbeInterval = Stopwatch.Frequency;
        private static readonly long SnapshotLifetime = Stopwatch.Frequency * 2;
        private static readonly long PendingLifetime = Stopwatch.Frequency * 2;
        private static readonly long TrackerRetentionInterval = Stopwatch.Frequency * 2;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly HunterNativeVisibilityProbe visibilityProbe;
        private readonly Func<bool> canRun;
        private readonly object stateLock = new object();
        private readonly Dictionary<int, Tracker> trackers = new Dictionary<int, Tracker>();
        private readonly Dictionary<int, long> acceptedPathGenerations =
            new Dictionary<int, long>();
        private long mapGeneration = 1;
        private long nextPathGeneration;
        private int diagnosticLogs;
        private int probeSampleLogs;
        private bool firstProbeConfirmed;
        private bool disposed;

        public HunterActiveTargetVisibilitySnapshot(
            ManualLogSource log,
            ImprovedHuntersViewModel settings,
            HunterNativeVisibilityProbe visibilityProbe,
            Func<bool> canRun)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.visibilityProbe = visibilityProbe ?? throw new ArgumentNullException(nameof(visibilityProbe));
            this.canRun = canRun ?? throw new ArgumentNullException(nameof(canRun));

            if (!visibilityProbe.IsAvailable)
                throw new InvalidOperationException("The validated native Hunter visibility probe is unavailable.");

            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters active-target visibility snapshot initialized: " +
                "probeSeconds=1, snapshotSeconds=2, pendingSeconds=2, trackerRetentionSeconds=2, " +
                "identityBinding=hunter/prey/global/player/map/accepted-MoveHere-generation/path, " +
                "explicitDirectionalDiagnosticsForWrapperZero=True, " +
                "nativeCallInsideInlineHook=False, behaviorMutation=False.");
        }

        public bool IsAvailable => !disposed && visibilityProbe.IsAvailable;

        public void RecordAcceptedVanillaPath(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType)
        {
            if (!IsAvailable || hunterUnitId <= 0 || preyUnitId <= 0 || preyGlobalId == 0)
                return;

            long previousGeneration;
            long generation;
            bool trackerPresent;
            lock (stateLock)
            {
                acceptedPathGenerations.TryGetValue(hunterUnitId, out previousGeneration);
                generation = ++nextPathGeneration;
                acceptedPathGenerations[hunterUnitId] = generation;
                trackerPresent = trackers.ContainsKey(hunterUnitId);
            }

            // The validated state-0 MoveHere result is the path boundary. Raw
            // +0xF4 and +0xF6 values oscillate while that same path advances.
            LogDiagnostic(
                "Improved Hunters accepted Vanilla path generation advanced: " +
                $"hunter={hunterUnitId}, target={preyUnitId}/{preyGlobalId}/{preyType}, " +
                $"pathGeneration={previousGeneration}->{generation}, " +
                $"previousTrackerPresent={trackerPresent}, behaviorMutation=False.");
        }

        public void ProcessNativeScan(SimpleNativeArray<GameUnit> units, long timestamp)
        {
            if (!IsAvailable || !canRun() || units._array == null || units.Length == 0)
                return;

            List<ProbeRequest> requests = new List<ProbeRequest>();
            HashSet<int> activeHunterIds = new HashSet<int>();
            Dictionary<int, string> captureFailures = new Dictionary<int, string>();
            HashSet<int> trackedHunterIds;
            lock (stateLock)
                trackedHunterIds = new HashSet<int>(trackers.Keys);

            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* hunter = units.GetValuePointer(index);
                int hunterUnitId = index + 1;
                if (!TryCaptureInputs(
                        units,
                        hunterUnitId,
                        hunter,
                        out VisibilityInputs inputs,
                        out string captureFailure))
                {
                    if (trackedHunterIds.Contains(hunterUnitId))
                        captureFailures[hunterUnitId] = captureFailure;
                    continue;
                }

                activeHunterIds.Add(hunterUnitId);
                lock (stateLock)
                {
                    Tracker tracker = GetOrReplaceTracker(inputs, timestamp, out _);
                    if (timestamp < tracker.NextProbeAt)
                        continue;

                    // Reserve the interval before leaving the lock. A reentrant
                    // scan cannot issue a second native call for this identity.
                    tracker.NextProbeAt = timestamp + ProbeInterval;
                    requests.Add(new ProbeRequest(inputs, tracker.PathGeneration));
                }
            }

            List<string> retentionLogs = null;
            lock (stateLock)
            {
                List<int> staleHunters = null;
                foreach (KeyValuePair<int, Tracker> entry in trackers)
                {
                    int hunterUnitId = entry.Key;
                    Tracker tracker = entry.Value;
                    if (activeHunterIds.Contains(hunterUnitId))
                        continue;

                    string captureFailure = captureFailures.TryGetValue(hunterUnitId, out string failure)
                        ? failure
                        : "not-observed-in-scan";
                    if (tracker.MissingSince == 0)
                        tracker.MissingSince = timestamp;
                    tracker.LastCaptureFailure = captureFailure;

                    // Movement callbacks can expose a short native transition in
                    // which the same state-1 path is not capturable. A recent
                    // successful scan or inline-hook validation keeps its bounded
                    // snapshot alive; a real inactive context still expires.
                    if (timestamp - tracker.LastValidatedAt < TrackerRetentionInterval)
                    {
                        if (!tracker.RetentionMissLogged)
                        {
                            tracker.RetentionMissLogged = true;
                            if (retentionLogs == null)
                                retentionLogs = new List<string>();
                            retentionLogs.Add(
                                "Improved Hunters active-target visibility tracker retained after " +
                                $"transient scan miss: hunter={hunterUnitId}/{tracker.Inputs.HunterGlobalId}, " +
                                $"target={tracker.Inputs.PreyUnitId}/{tracker.Inputs.PreyGlobalId}/" +
                                $"{tracker.Inputs.PreyType}, pathGeneration={tracker.PathGeneration}, " +
                                $"reason={captureFailure}, retentionMs=" +
                                $"{ToMilliseconds(TrackerRetentionInterval)}.");
                        }

                        continue;
                    }

                    if (staleHunters == null)
                        staleHunters = new List<int>();
                    staleHunters.Add(hunterUnitId);
                    if (retentionLogs == null)
                        retentionLogs = new List<string>();
                    retentionLogs.Add(
                        "Improved Hunters active-target visibility tracker expired after scan absence: " +
                        $"hunter={hunterUnitId}/{tracker.Inputs.HunterGlobalId}, " +
                        $"target={tracker.Inputs.PreyUnitId}/{tracker.Inputs.PreyGlobalId}/" +
                        $"{tracker.Inputs.PreyType}, pathGeneration={tracker.PathGeneration}, " +
                        $"absenceMs={ToMilliseconds(timestamp - tracker.MissingSince)}, " +
                        $"lastValidatedAgeMs={ToMilliseconds(timestamp - tracker.LastValidatedAt)}, " +
                        $"reason={tracker.LastCaptureFailure}.");
                }

                if (staleHunters != null)
                {
                    foreach (int hunterUnitId in staleHunters)
                        trackers.Remove(hunterUnitId);
                }

            }

            if (retentionLogs != null)
            {
                foreach (string message in retentionLogs)
                    LogDiagnostic(message);
            }

            foreach (ProbeRequest request in requests)
                Probe(request, timestamp);
        }

        public bool TryGetObservation(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            long timestamp,
            out HunterActiveVisibilityObservation observation)
        {
            observation = default;
            if (!IsAvailable || !canRun())
                return false;

            if (!TryCaptureInputs(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    preyType,
                    out VisibilityInputs inputs,
                    out string failure))
            {
                observation = Unavailable($"input-{failure}");
                return false;
            }

            lock (stateLock)
            {
                Tracker tracker = GetOrReplaceTracker(
                    inputs,
                    timestamp,
                    out string replacementStatus);
                if (tracker.HasSnapshot && timestamp < tracker.UsableUntil)
                {
                    long ageMilliseconds = ToMilliseconds(timestamp - tracker.ObservedAt);
                    observation = new HunterActiveVisibilityObservation(
                        tracker.State,
                        "hit",
                        tracker.Classification,
                        ageMilliseconds,
                        ToMilliseconds(timestamp - tracker.FirstObservedAt),
                        tracker.PathGeneration,
                        tracker.WrapperResult,
                        tracker.HunterToPreyResult,
                        tracker.PreyToHunterResult);
                    return true;
                }

                long pendingAge = Math.Max(0, timestamp - tracker.FirstObservedAt);
                if (!tracker.HasSnapshot && pendingAge < PendingLifetime)
                {
                    observation = new HunterActiveVisibilityObservation(
                        HunterActiveVisibilityState.Pending,
                        replacementStatus ?? tracker.LastFailureStatus,
                        "visibility-pending",
                        -1,
                        ToMilliseconds(pendingAge),
                        tracker.PathGeneration,
                        -1,
                        -1,
                        -1);
                    return true;
                }

                observation = Unavailable(
                    tracker.HasSnapshot ? "snapshot-expired" : "visibility-pending-expired",
                    tracker.HasSnapshot ? ToMilliseconds(timestamp - tracker.ObservedAt) : -1,
                    ToMilliseconds(pendingAge));
                return false;
            }
        }

        public void ResetForMap()
        {
            lock (stateLock)
            {
                trackers.Clear();
                acceptedPathGenerations.Clear();
                nextPathGeneration = 0;
                mapGeneration++;
            }

            diagnosticLogs = 0;
            probeSampleLogs = 0;
            firstProbeConfirmed = false;
        }

        private void Probe(ProbeRequest request, long timestamp)
        {
            try
            {
                VisibilityInputs before = request.Inputs;
                if (!TryCaptureInputs(
                        before.HunterUnitId,
                        before.PreyUnitId,
                        before.PreyGlobalId,
                        before.PreyType,
                        out VisibilityInputs current,
                        out string inputFailure) ||
                    !current.Equals(before))
                {
                    LogDiagnostic(
                        "Improved Hunters active-target visibility probe skipped: " +
                        $"hunter={before.HunterUnitId}/{before.HunterGlobalId}, " +
                        $"target={before.PreyUnitId}/{before.PreyGlobalId}/{before.PreyType}, " +
                        $"reason=input-changed-{inputFailure}.",
                        warning: true);
                    return;
                }

                if (!IsCurrentProbeRequest(request))
                    return;

                bool invoked = visibilityProbe.TryEvaluateNearVisibility(
                    before.HunterUnitId,
                    before.HunterGlobalId,
                    before.PreyUnitId,
                    before.PreyGlobalId,
                    before.PreyType,
                    out int wrapperResult,
                    out int hunterToPreyResult,
                    out int preyToHunterResult,
                    out HunterNearVisibilityGeometry geometry);
                string resultFailure = invoked ? string.Empty : "native-probe-unavailable";
                if (!invoked || !TryClassify(
                        wrapperResult,
                        hunterToPreyResult,
                        preyToHunterResult,
                        out HunterActiveVisibilityState state,
                        out string classification,
                        out resultFailure))
                {
                    lock (stateLock)
                    {
                        if (trackers.TryGetValue(before.HunterUnitId, out Tracker failed) &&
                            failed.Inputs.Equals(before) &&
                            failed.PathGeneration == request.PathGeneration &&
                            GetPathGeneration(before.HunterUnitId) == request.PathGeneration)
                        {
                            failed.LastFailureStatus = invoked
                                ? $"probe-invalid-{resultFailure}"
                                : "probe-unavailable";
                        }
                    }

                    LogDiagnostic(
                        "Improved Hunters active-target visibility probe failed: " +
                        $"hunter={before.HunterUnitId}/{before.HunterGlobalId}, " +
                        $"target={before.PreyUnitId}/{before.PreyGlobalId}/{before.PreyType}, " +
                        $"invoked={invoked}, reason={resultFailure}, wrapperResult={wrapperResult}, " +
                        $"coreHunterToPreyResult={hunterToPreyResult}, " +
                        $"corePreyToHunterResult={preyToHunterResult}.",
                        warning: true);
                    return;
                }

                LogProbeSample(
                    "Improved Hunters active-target visibility probe sample: " +
                    $"hunter={before.HunterUnitId}/{before.HunterGlobalId}, " +
                    $"target={before.PreyUnitId}/{before.PreyGlobalId}/{before.PreyType}, " +
                    $"player={before.PlayerId}, mapGeneration={before.MapGeneration}, " +
                    $"path={before.PathState}/{before.PathFieldF4}/" +
                    $"{before.PathProgress}/{before.PathLength}, reservation={before.Reservation}, " +
                    $"pathGeneration={request.PathGeneration}, " +
                    $"hunterTile={geometry.HunterTileX},{geometry.HunterTileY}, " +
                    $"preyTile={geometry.PreyTileX},{geometry.PreyTileY}, " +
                    $"tileManhattanDistance={geometry.TileManhattanDistance}, " +
                    $"hunterWorld={geometry.HunterWorldX},{geometry.HunterWorldY},{geometry.HunterHeight}, " +
                    $"preyWorld={geometry.PreyWorldX},{geometry.PreyWorldY},{geometry.PreyHeight}, " +
                    $"worldChebyshevDistance={geometry.WorldChebyshevDistance}, " +
                    $"wrapperResult={wrapperResult}, " +
                    $"coreHunterToPreyResult={hunterToPreyResult}, " +
                    $"corePreyToHunterResult={preyToHunterResult}, " +
                    $"classification={classification}, behaviorMutation=False.",
                    warning: wrapperResult == 0 &&
                        (hunterToPreyResult > 0 || preyToHunterResult > 0));

                if (!TryCaptureInputs(
                        before.HunterUnitId,
                        before.PreyUnitId,
                        before.PreyGlobalId,
                        before.PreyType,
                        out VisibilityInputs after,
                        out _) ||
                    !after.Equals(before))
                {
                    return;
                }

                bool changed;
                lock (stateLock)
                {
                    if (!trackers.TryGetValue(before.HunterUnitId, out Tracker tracker) ||
                        !tracker.Inputs.Equals(before) ||
                        tracker.PathGeneration != request.PathGeneration ||
                        GetPathGeneration(before.HunterUnitId) != request.PathGeneration)
                    {
                        return;
                    }

                    changed = !tracker.HasSnapshot ||
                        tracker.State != state ||
                        tracker.WrapperResult != wrapperResult ||
                        tracker.HunterToPreyResult != hunterToPreyResult ||
                        tracker.PreyToHunterResult != preyToHunterResult;
                    tracker.HasSnapshot = true;
                    tracker.ObservedAt = timestamp;
                    tracker.UsableUntil = timestamp + SnapshotLifetime;
                    tracker.State = state;
                    tracker.Classification = classification;
                    tracker.WrapperResult = wrapperResult;
                    tracker.HunterToPreyResult = hunterToPreyResult;
                    tracker.PreyToHunterResult = preyToHunterResult;
                    tracker.LastFailureStatus = "pending-first-probe";
                }

                if (!firstProbeConfirmed)
                {
                    firstProbeConfirmed = true;
                    LogDiagnostic(
                        "Improved Hunters active-target visibility probe callback confirmed: " +
                        $"hunter={before.HunterUnitId}/{before.HunterGlobalId}, " +
                        $"target={before.PreyUnitId}/{before.PreyGlobalId}/{before.PreyType}, " +
                        $"pathGeneration={request.PathGeneration}.");
                }

                if (changed)
                {
                    LogDiagnostic(
                        "Improved Hunters active-target visibility snapshot refreshed: " +
                        $"hunter={before.HunterUnitId}/{before.HunterGlobalId}, " +
                        $"target={before.PreyUnitId}/{before.PreyGlobalId}/{before.PreyType}, " +
                        $"player={before.PlayerId}, mapGeneration={before.MapGeneration}, " +
                        $"path={before.PathState}/{before.PathFieldF4}/" +
                        $"{before.PathProgress}/{before.PathLength}, reservation={before.Reservation}, " +
                        $"pathGeneration={request.PathGeneration}, " +
                        $"wrapperResult={wrapperResult}, " +
                        $"coreHunterToPreyResult={hunterToPreyResult}, " +
                        $"corePreyToHunterResult={preyToHunterResult}, " +
                        $"classification={classification}, nextProbeMs=1000, readableMs=2000.",
                        warning: state == HunterActiveVisibilityState.Blocked &&
                            (wrapperResult > 0 ||
                             hunterToPreyResult > 0 ||
                             preyToHunterResult > 0));
                }
            }
            catch (Exception exception)
            {
                LogDiagnostic(
                    "Improved Hunters active-target visibility scan failed; " +
                    $"the pending window remains bounded: {exception}",
                    warning: true);
            }
        }

        private Tracker GetOrReplaceTracker(
            VisibilityInputs inputs,
            long timestamp,
            out string replacementStatus)
        {
            long pathGeneration = GetPathGeneration(inputs.HunterUnitId);
            if (!trackers.TryGetValue(inputs.HunterUnitId, out Tracker tracker))
            {
                tracker = new Tracker(inputs, timestamp, pathGeneration);
                trackers[inputs.HunterUnitId] = tracker;
                replacementStatus = "new-tracker-pending";
                return tracker;
            }

            if (!tracker.Inputs.Equals(inputs))
            {
                tracker = new Tracker(inputs, timestamp, pathGeneration);
                trackers[inputs.HunterUnitId] = tracker;
                replacementStatus = "new-identity-pending";
                return tracker;
            }

            if (tracker.PathGeneration != pathGeneration)
            {
                tracker = new Tracker(inputs, timestamp, pathGeneration);
                trackers[inputs.HunterUnitId] = tracker;
                replacementStatus = "new-path-generation-pending";
                return tracker;
            }

            tracker.LastValidatedAt = timestamp;
            tracker.MissingSince = 0;
            replacementStatus = null;
            return tracker;
        }

        private bool IsCurrentProbeRequest(ProbeRequest request)
        {
            lock (stateLock)
            {
                return trackers.TryGetValue(request.Inputs.HunterUnitId, out Tracker tracker) &&
                    tracker.Inputs.Equals(request.Inputs) &&
                    tracker.PathGeneration == request.PathGeneration &&
                    GetPathGeneration(request.Inputs.HunterUnitId) == request.PathGeneration;
            }
        }

        private long GetPathGeneration(int hunterUnitId) =>
            acceptedPathGenerations.TryGetValue(hunterUnitId, out long generation)
                ? generation
                : 0;

        private bool TryCaptureInputs(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            out VisibilityInputs inputs,
            out string failure)
        {
            inputs = default;
            failure = string.Empty;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) || hunter == null)
            {
                failure = "invalid-hunter-slot";
                return false;
            }

            SimpleNativeArray<GameUnit> units = unitApi.GetUnitArray();
            if (units._array == null ||
                preyUnitId <= 0 ||
                preyUnitId > units.Length ||
                hunterUnitId <= 0 ||
                hunterUnitId > units.Length)
            {
                failure = "unit-array-or-slot-invalid";
                return false;
            }

            if (!TryCaptureInputs(
                    units,
                    hunterUnitId,
                    hunter,
                    out inputs,
                    out failure))
            {
                return false;
            }

            if (inputs.PreyUnitId != preyUnitId ||
                inputs.PreyGlobalId != preyGlobalId ||
                inputs.PreyType != preyType)
            {
                failure = "active-identity-or-state-mismatch";
                return false;
            }

            return true;
        }

        private bool TryCaptureInputs(
            SimpleNativeArray<GameUnit> units,
            int hunterUnitId,
            GameUnit* hunter,
            out VisibilityInputs inputs,
            out string failure)
        {
            inputs = default;
            failure = string.Empty;
            if (hunter == null || hunterUnitId <= 0 || hunterUnitId > units.Length)
            {
                failure = "invalid-hunter-slot";
                return false;
            }

            if (hunter->r_AliveState != AliveState.IsAlive)
            {
                failure = "hunter-not-alive";
                return false;
            }

            if (hunter->r_CurrentHealth == 0)
            {
                failure = "hunter-zero-health";
                return false;
            }

            if (hunter->r_GlobalId == 0)
            {
                failure = "hunter-zero-global-id";
                return false;
            }

            if (hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
            {
                failure = "unit-not-hunter";
                return false;
            }

            byte* hunterBytes = (byte*)hunter;
            ushort aiState = *(ushort*)(hunterBytes + HunterAiStateOffset);
            ushort pathState = *(ushort*)(hunterBytes + HunterPathStateOffset);
            ushort pathFieldF4 = *(ushort*)(hunterBytes + HunterPathFieldF4Offset);
            ushort pathProgress = *(ushort*)(hunterBytes + HunterPathProgressOffset);
            uint pathLength = *(uint*)(hunterBytes + HunterPathLengthOffset);
            int preyUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint preyGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            if (aiState != HunterStateFollowingTarget)
            {
                failure = $"hunter-state-{aiState}";
                return false;
            }

            if (pathState != ActivePathState)
            {
                failure = $"path-state-{pathState}";
                return false;
            }

            if (pathLength <= 1 || pathLength > MaximumPathSteps)
            {
                failure = $"path-length-{pathLength}";
                return false;
            }

            if (pathProgress >= pathLength)
            {
                failure = $"path-complete-{pathProgress}-{pathLength}";
                return false;
            }

            if (preyUnitId <= 0 || preyUnitId > units.Length || preyGlobalId == 0)
            {
                failure = $"target-identity-{preyUnitId}-{preyGlobalId}";
                return false;
            }

            GameUnit* prey = units.GetValuePointer(preyUnitId - 1);
            if (prey == null)
            {
                failure = "invalid-prey-slot";
                return false;
            }

            if (prey->r_AliveState != AliveState.IsAlive)
            {
                failure = $"prey-not-alive-{(short)prey->r_AliveState}";
                return false;
            }

            if (prey->r_CurrentHealth == 0)
            {
                failure = "prey-zero-health";
                return false;
            }

            if (prey->r_GlobalId != preyGlobalId)
            {
                failure = $"prey-global-id-{prey->r_GlobalId}-{preyGlobalId}";
                return false;
            }

            if (!settings.IsKnownAnimal(prey->r_UnitChimp))
            {
                failure = $"unknown-prey-type-{prey->r_UnitChimp}";
                return false;
            }

            if (!settings.IsHuntingEnabled(prey->r_UnitChimp))
            {
                failure = $"disabled-prey-type-{prey->r_UnitChimp}";
                return false;
            }

            ushort reservation = *(ushort*)((byte*)prey + PreyReservationOffset);
            if (reservation != OwnHunterReservation)
            {
                failure = $"prey-reservation-{reservation}";
                return false;
            }

            inputs = new VisibilityInputs(
                hunterUnitId,
                hunter->r_GlobalId,
                preyUnitId,
                preyGlobalId,
                prey->r_UnitChimp,
                hunter->r_ControllableForPlayerId,
                mapGeneration,
                aiState,
                pathState,
                pathFieldF4,
                pathProgress,
                pathLength,
                reservation);
            return true;
        }

        private static bool TryClassify(
            int wrapperResult,
            int hunterToPreyResult,
            int preyToHunterResult,
            out HunterActiveVisibilityState state,
            out string classification,
            out string failure)
        {
            state = HunterActiveVisibilityState.Pending;
            classification = string.Empty;
            failure = string.Empty;
            if (wrapperResult < 0 || wrapperResult > 432)
            {
                failure = $"wrapper-result-{wrapperResult}";
                return false;
            }

            if (wrapperResult == 0)
            {
                state = HunterActiveVisibilityState.Blocked;
                classification = "blocked-wrapper-both-directions";
                return true;
            }

            if (hunterToPreyResult < 0 || hunterToPreyResult > 432 ||
                preyToHunterResult < 0 || preyToHunterResult > 432)
            {
                failure = $"directional-results-{hunterToPreyResult}-{preyToHunterResult}";
                return false;
            }

            if (hunterToPreyResult > 0 && preyToHunterResult > 0)
            {
                state = HunterActiveVisibilityState.Visible;
                classification = "visible-attack-handoff";
            }
            else
            {
                state = HunterActiveVisibilityState.Blocked;
                classification = "blocked-directional-disagreement";
            }

            return true;
        }

        private static HunterActiveVisibilityObservation Unavailable(
            string status,
            long snapshotAgeMilliseconds = -1,
            long pendingAgeMilliseconds = -1) =>
            new HunterActiveVisibilityObservation(
                HunterActiveVisibilityState.Pending,
                status,
                "unavailable",
                snapshotAgeMilliseconds,
                pendingAgeMilliseconds,
                -1,
                -1,
                -1,
                -1);

        private static long ToMilliseconds(long ticks) =>
            Math.Max(0, ticks) * 1000 / Stopwatch.Frequency;

        private void LogDiagnostic(string message, bool warning = false)
        {
            if (diagnosticLogs >= MaxDiagnosticLogs)
                return;

            diagnosticLogs++;
            string bounded = $"{message} ({diagnosticLogs}/{MaxDiagnosticLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, bounded);
            else
                Shared.DebugLogHelper.LogInfo(log, bounded);
        }

        private void LogProbeSample(string message, bool warning)
        {
            if (probeSampleLogs >= MaxProbeSampleLogs)
                return;

            probeSampleLogs++;
            string bounded = $"{message} ({probeSampleLogs}/{MaxProbeSampleLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, bounded);
            else
                Shared.DebugLogHelper.LogInfo(log, bounded);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            lock (stateLock)
            {
                trackers.Clear();
                acceptedPathGenerations.Clear();
            }
        }

        private sealed class Tracker
        {
            public readonly VisibilityInputs Inputs;
            public readonly long FirstObservedAt;
            public readonly long PathGeneration;
            public long NextProbeAt;
            public long LastValidatedAt;
            public long MissingSince;
            public string LastCaptureFailure;
            public bool RetentionMissLogged;
            public bool HasSnapshot;
            public long ObservedAt;
            public long UsableUntil;
            public HunterActiveVisibilityState State;
            public string Classification;
            public int WrapperResult;
            public int HunterToPreyResult;
            public int PreyToHunterResult;
            public string LastFailureStatus;

            public Tracker(VisibilityInputs inputs, long timestamp, long pathGeneration)
            {
                Inputs = inputs;
                FirstObservedAt = timestamp;
                PathGeneration = pathGeneration;
                NextProbeAt = timestamp;
                LastValidatedAt = timestamp;
                LastCaptureFailure = string.Empty;
                Classification = "visibility-pending";
                WrapperResult = -1;
                HunterToPreyResult = -1;
                PreyToHunterResult = -1;
                LastFailureStatus = "pending-first-probe";
            }
        }

        private readonly struct ProbeRequest
        {
            public readonly VisibilityInputs Inputs;
            public readonly long PathGeneration;

            public ProbeRequest(VisibilityInputs inputs, long pathGeneration)
            {
                Inputs = inputs;
                PathGeneration = pathGeneration;
            }
        }

        private readonly struct VisibilityInputs : IEquatable<VisibilityInputs>
        {
            public readonly int HunterUnitId;
            public readonly uint HunterGlobalId;
            public readonly int PreyUnitId;
            public readonly uint PreyGlobalId;
            public readonly eChimps PreyType;
            public readonly int PlayerId;
            public readonly long MapGeneration;
            public readonly ushort AiState;
            public readonly ushort PathState;
            public readonly ushort PathFieldF4;
            public readonly ushort PathProgress;
            public readonly uint PathLength;
            public readonly ushort Reservation;

            public VisibilityInputs(
                int hunterUnitId,
                uint hunterGlobalId,
                int preyUnitId,
                uint preyGlobalId,
                eChimps preyType,
                int playerId,
                long mapGeneration,
                ushort aiState,
                ushort pathState,
                ushort pathFieldF4,
                ushort pathProgress,
                uint pathLength,
                ushort reservation)
            {
                HunterUnitId = hunterUnitId;
                HunterGlobalId = hunterGlobalId;
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
                PreyType = preyType;
                PlayerId = playerId;
                MapGeneration = mapGeneration;
                AiState = aiState;
                PathState = pathState;
                PathFieldF4 = pathFieldF4;
                PathProgress = pathProgress;
                PathLength = pathLength;
                Reservation = reservation;
            }

            // +0xF4 is a live locomotion substep, not a path identity. Progress
            // is likewise tracked separately so only a backwards jump resets.
            public bool Equals(VisibilityInputs other) =>
                HunterUnitId == other.HunterUnitId &&
                HunterGlobalId == other.HunterGlobalId &&
                PreyUnitId == other.PreyUnitId &&
                PreyGlobalId == other.PreyGlobalId &&
                PreyType == other.PreyType &&
                PlayerId == other.PlayerId &&
                MapGeneration == other.MapGeneration &&
                AiState == other.AiState &&
                PathState == other.PathState &&
                PathLength == other.PathLength &&
                Reservation == other.Reservation;

            public override bool Equals(object obj) => obj is VisibilityInputs other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = HunterUnitId;
                    hash = (hash * 397) ^ (int)HunterGlobalId;
                    hash = (hash * 397) ^ PreyUnitId;
                    hash = (hash * 397) ^ (int)PreyGlobalId;
                    hash = (hash * 397) ^ (int)PreyType;
                    hash = (hash * 397) ^ PlayerId;
                    hash = (hash * 397) ^ MapGeneration.GetHashCode();
                    hash = (hash * 397) ^ AiState;
                    hash = (hash * 397) ^ PathState;
                    hash = (hash * 397) ^ (int)PathLength;
                    return (hash * 397) ^ Reservation;
                }
            }
        }
    }
}
