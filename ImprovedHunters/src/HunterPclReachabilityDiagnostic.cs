using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ImprovedHunters
{
    /// <summary>
    /// Temporary, separately removable calibration of Vanilla's native PCL
    /// reachability query. It never changes target selection or issues orders.
    /// </summary>
    internal sealed unsafe class HunterPclReachabilityDiagnostic : IDisposable
    {
        private const int MaxProbeLogs = 160;
        private const int MaxCorrelationLogs = 160;
        private const int MaxActiveTargetInvalidationLogs = 80;
        private const int MaxFailureLogs = 20;
        private static readonly long StableContextProbeInterval = Stopwatch.Frequency * 2;
        private static readonly long MaximumCorrelationAge = Stopwatch.Frequency * 3;

        private readonly ManualLogSource log;
        private readonly Func<bool> canRun;
        private readonly object observationLock = new object();
        private readonly Dictionary<HunterPreyKey, ProbeState> probeStates =
            new Dictionary<HunterPreyKey, ProbeState>();
        private bool available;
        private bool disposed;
        private int probeLogs;
        private int correlationLogs;
        private int activeTargetInvalidationLogs;
        private int failureLogs;

        public HunterPclReachabilityDiagnostic(
            ManualLogSource log,
            bool referenceHashMatches,
            Func<bool> canRun)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.canRun = canRun ?? throw new ArgumentNullException(nameof(canRun));

            if (!referenceHashMatches)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Improved Hunters PCL reachability diagnostic unavailable: " +
                    "the installed native DLL differs from the audited build; observation remains inactive.");
                return;
            }

            available = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters PCL reachability diagnostic initialized: " +
                "query=GamePlayerManagerAPI.GetNextReachablePCLToDestinationForPlayer, " +
                "modeField=GameUnit+0x35C/N000001CA, stableProbeIntervalSeconds=2, " +
                "observationOnly=True, targetSelectionChanged=False, movementOrdersIssued=False.");
        }

        public bool IsAvailable => available && !disposed;

        public void RecordCandidate(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            long timestamp)
        {
            if (!IsAvailable || !canRun())
                return;

            try
            {
                if (!TryCaptureContext(
                        hunterUnitId,
                        preyUnitId,
                        preyGlobalId,
                        preyType,
                        out ProbeContext context,
                        out string failure))
                {
                    LogFailure(
                        $"Improved Hunters PCL candidate probe skipped: hunter={hunterUnitId}, " +
                        $"target={preyUnitId}/{preyGlobalId}/{preyType}, reason={failure}.");
                    return;
                }

                HunterPreyKey key = new HunterPreyKey(hunterUnitId, preyGlobalId);
                ProbeState previous;
                lock (observationLock)
                {
                    if (probeStates.TryGetValue(key, out previous) &&
                        previous.Observation.Context.HasSameNativeInputs(context) &&
                        timestamp < previous.NextProbeAt)
                    {
                        return;
                    }
                }

                ProbeObservation observation = InvokeProbe(context, timestamp);
                bool shouldLog;
                lock (observationLock)
                {
                    shouldLog = !probeStates.TryGetValue(key, out previous) ||
                        !previous.Observation.HasSameReportedOutcome(observation);
                    probeStates[key] = new ProbeState(
                        observation,
                        timestamp + StableContextProbeInterval);
                }

                if (shouldLog)
                    LogProbe(observation);
            }
            catch (Exception exception)
            {
                LogFailure(
                    $"Improved Hunters PCL candidate probe failed: hunter={hunterUnitId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{preyType}, error={exception}.");
            }
        }

        public void RecordMoveHereResult(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            int moveHereResult,
            long timestamp)
        {
            if (!IsAvailable || !canRun())
                return;

            try
            {
                HunterPreyKey key = new HunterPreyKey(hunterUnitId, preyGlobalId);
                ProbeState state;
                bool found;
                lock (observationLock)
                    found = probeStates.TryGetValue(key, out state);

                if (!TryCaptureContext(
                        hunterUnitId,
                        preyUnitId,
                        preyGlobalId,
                        preyType,
                        out ProbeContext currentContext,
                        out string failure))
                {
                    LogCorrelation(
                        "Improved Hunters PCL/MoveHere correlation unavailable: " +
                        $"hunter={hunterUnitId}, target={preyUnitId}/{preyGlobalId}/{preyType}, " +
                        $"moveHereResult={moveHereResult}, reason=current-context-{failure}.",
                        warning: true);
                    return;
                }

                if (!found)
                {
                    LogCorrelation(
                        "Improved Hunters PCL/MoveHere correlation unavailable: " +
                        $"hunter={hunterUnitId}, target={preyUnitId}/{preyGlobalId}/{preyType}, " +
                        $"moveHereResult={moveHereResult}, sourcePcl={currentContext.SourcePcl}, " +
                        $"targetPcl={currentContext.TargetPcl}, modeRaw={currentContext.RawMode}, " +
                        "reason=no-prior-candidate-probe.",
                        warning: true);
                    return;
                }

                ProbeObservation observation = state.Observation;
                long ageTicks = Math.Max(0, timestamp - observation.Timestamp);
                long ageMilliseconds = ageTicks * 1000 / Stopwatch.Frequency;
                bool inputsMatch = observation.Context.HasSameNativeInputs(currentContext);
                bool ageAllowed = ageTicks <= MaximumCorrelationAge;
                bool comparable = inputsMatch && ageAllowed;
                bool rawAgreement = comparable &&
                    ((observation.RawResult != 0) == (moveHereResult != 0));
                bool mode0Agreement = comparable &&
                    ((observation.Mode0Result != 0) == (moveHereResult != 0));
                bool mode2Agreement = comparable &&
                    ((observation.Mode2Result != 0) == (moveHereResult != 0));

                LogCorrelation(
                    "Improved Hunters PCL/MoveHere correlation: " +
                    $"hunter={hunterUnitId}/{currentContext.HunterGlobalId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{preyType}, " +
                    $"sourceTile={currentContext.SourceTileX},{currentContext.SourceTileY}, " +
                    $"targetTile={currentContext.TargetTileX},{currentContext.TargetTileY}, " +
                    $"player={currentContext.PlayerId}, sourcePcl={currentContext.SourcePcl}, " +
                    $"targetPcl={currentContext.TargetPcl}, modeRaw={currentContext.RawMode}, " +
                    $"resultRaw={observation.RawResult}, resultMode0={observation.Mode0Result}, " +
                    $"resultMode2={observation.Mode2Result}, moveHereResult={moveHereResult}, " +
                    $"probeAgeMs={ageMilliseconds}, inputsMatch={inputsMatch}, ageAllowed={ageAllowed}, " +
                    $"comparable={comparable}, rawAgreement={rawAgreement}, " +
                    $"mode0Agreement={mode0Agreement}, mode2Agreement={mode2Agreement}, " +
                    "observationOnly=True.",
                    warning: comparable && !rawAgreement);
            }
            catch (Exception exception)
            {
                LogFailure(
                    $"Improved Hunters PCL/MoveHere correlation failed: hunter={hunterUnitId}, " +
                    $"target={preyUnitId}/{preyGlobalId}/{preyType}, moveHereResult={moveHereResult}, " +
                    $"error={exception}.");
            }
        }

        public void ResetForMap()
        {
            lock (observationLock)
                probeStates.Clear();

            probeLogs = 0;
            correlationLogs = 0;
            activeTargetInvalidationLogs = 0;
            failureLogs = 0;
        }

        public void RecordActiveTargetInvalidation(string details, bool warning)
        {
            if (!IsAvailable || !canRun() ||
                activeTargetInvalidationLogs >= MaxActiveTargetInvalidationLogs)
            {
                return;
            }

            activeTargetInvalidationLogs++;
            string message =
                $"Improved Hunters active target PCL requery: {details} " +
                $"({activeTargetInvalidationLogs}/{MaxActiveTargetInvalidationLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, message);
            else
                Shared.DebugLogHelper.LogInfo(log, message);
        }

        private static bool TryCaptureContext(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            out ProbeContext context,
            out string failure)
        {
            context = default;
            failure = string.Empty;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (hunterUnitId <= 0 ||
                !unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                hunter->r_AliveState != AliveState.IsAlive ||
                hunter->r_CurrentHealth == 0 ||
                hunter->r_GlobalId == 0 ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
            {
                failure = "invalid-live-hunter";
                return false;
            }

            if (preyUnitId <= 0 ||
                !unitApi.TryGetUnitById(preyUnitId, out GameUnit* prey) ||
                prey == null ||
                prey->r_AliveState != AliveState.IsAlive ||
                prey->r_CurrentHealth == 0 ||
                prey->r_GlobalId != preyGlobalId ||
                prey->r_UnitChimp != preyType)
            {
                failure = "invalid-live-prey-identity";
                return false;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int sourceTileX = hunter->r_CurrentTilePositionX;
            int sourceTileY = hunter->r_CurrentTilePositionY;
            int targetTileX = prey->r_CurrentTilePositionX;
            int targetTileY = prey->r_CurrentTilePositionY;
            if (!tileApi.IsTileInsideMapBounds(sourceTileX, sourceTileY) ||
                !tileApi.IsTileInsideMapBounds(targetTileX, targetTileY))
            {
                failure = "tile-outside-map";
                return false;
            }

            int sourceTileId = tileApi.GetTileId(sourceTileX, sourceTileY);
            int targetTileId = tileApi.GetTileId(targetTileX, targetTileY);
            if (!tileApi.IsValidTileId(sourceTileId) || !tileApi.IsValidTileId(targetTileId))
            {
                failure = "invalid-tile-id";
                return false;
            }

            Span<ushort> pathConnections = tileApi.TileManager.PathConnectionGrid;
            if ((uint)sourceTileId >= (uint)pathConnections.Length ||
                (uint)targetTileId >= (uint)pathConnections.Length)
            {
                failure = "path-connection-grid-index-out-of-range";
                return false;
            }

            context = new ProbeContext(
                hunterUnitId,
                hunter->r_GlobalId,
                preyUnitId,
                preyGlobalId,
                preyType,
                hunter->r_ControllableForPlayerId,
                hunter->N000001CA,
                sourceTileX,
                sourceTileY,
                targetTileX,
                targetTileY,
                pathConnections[sourceTileId],
                pathConnections[targetTileId]);
            return true;
        }

        private static ProbeObservation InvokeProbe(ProbeContext context, long timestamp)
        {
            long startedAt = Stopwatch.GetTimestamp();
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            int mode0Result = playerApi.GetNextReachablePCLToDestinationForPlayer(
                context.PlayerId,
                context.TargetPcl,
                context.SourcePcl,
                0);
            int mode2Result = playerApi.GetNextReachablePCLToDestinationForPlayer(
                context.PlayerId,
                context.TargetPcl,
                context.SourcePcl,
                2);
            int rawResult;
            if (context.RawMode == 0)
                rawResult = mode0Result;
            else if (context.RawMode == 2)
                rawResult = mode2Result;
            else
            {
                rawResult = playerApi.GetNextReachablePCLToDestinationForPlayer(
                    context.PlayerId,
                    context.TargetPcl,
                    context.SourcePcl,
                    context.RawMode);
            }

            long elapsedTicks = Math.Max(0, Stopwatch.GetTimestamp() - startedAt);
            long elapsedMicroseconds = elapsedTicks * 1000000 / Stopwatch.Frequency;
            return new ProbeObservation(
                context,
                mode0Result,
                mode2Result,
                rawResult,
                timestamp,
                elapsedMicroseconds);
        }

        private void LogProbe(ProbeObservation observation)
        {
            if (probeLogs >= MaxProbeLogs)
                return;

            probeLogs++;
            ProbeContext context = observation.Context;
            bool modesAgree =
                (observation.Mode0Result != 0) == (observation.Mode2Result != 0) &&
                (observation.Mode0Result != 0) == (observation.RawResult != 0);
            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters PCL candidate probe: " +
                $"hunter={context.HunterUnitId}/{context.HunterGlobalId}, " +
                $"target={context.PreyUnitId}/{context.PreyGlobalId}/{context.PreyType}, " +
                $"sourceTile={context.SourceTileX},{context.SourceTileY}, " +
                $"targetTile={context.TargetTileX},{context.TargetTileY}, " +
                $"player={context.PlayerId}, sourcePcl={context.SourcePcl}, " +
                $"targetPcl={context.TargetPcl}, modeRaw={context.RawMode}, " +
                $"resultRaw={observation.RawResult}, resultMode0={observation.Mode0Result}, " +
                $"resultMode2={observation.Mode2Result}, modesAgree={modesAgree}, " +
                $"elapsedUs={observation.ElapsedMicroseconds}, observationOnly=True " +
                $"({probeLogs}/{MaxProbeLogs}).");
        }

        private void LogCorrelation(string message, bool warning)
        {
            if (correlationLogs >= MaxCorrelationLogs)
                return;

            correlationLogs++;
            string boundedMessage = $"{message} ({correlationLogs}/{MaxCorrelationLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, boundedMessage);
            else
                Shared.DebugLogHelper.LogInfo(log, boundedMessage);
        }

        private void LogFailure(string message)
        {
            if (failureLogs >= MaxFailureLogs)
                return;

            failureLogs++;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"{message} ({failureLogs}/{MaxFailureLogs}).");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            available = false;
            lock (observationLock)
                probeStates.Clear();
        }

        private readonly struct HunterPreyKey : IEquatable<HunterPreyKey>
        {
            private readonly int hunterUnitId;
            private readonly uint preyGlobalId;

            public HunterPreyKey(int hunterUnitId, uint preyGlobalId)
            {
                this.hunterUnitId = hunterUnitId;
                this.preyGlobalId = preyGlobalId;
            }

            public bool Equals(HunterPreyKey other)
            {
                return hunterUnitId == other.hunterUnitId &&
                    preyGlobalId == other.preyGlobalId;
            }

            public override bool Equals(object obj)
            {
                return obj is HunterPreyKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (hunterUnitId * 397) ^ preyGlobalId.GetHashCode();
                }
            }
        }

        private readonly struct ProbeContext
        {
            public readonly int HunterUnitId;
            public readonly uint HunterGlobalId;
            public readonly int PreyUnitId;
            public readonly uint PreyGlobalId;
            public readonly eChimps PreyType;
            public readonly int PlayerId;
            public readonly int RawMode;
            public readonly int SourceTileX;
            public readonly int SourceTileY;
            public readonly int TargetTileX;
            public readonly int TargetTileY;
            public readonly int SourcePcl;
            public readonly int TargetPcl;

            public ProbeContext(
                int hunterUnitId,
                uint hunterGlobalId,
                int preyUnitId,
                uint preyGlobalId,
                eChimps preyType,
                int playerId,
                int rawMode,
                int sourceTileX,
                int sourceTileY,
                int targetTileX,
                int targetTileY,
                int sourcePcl,
                int targetPcl)
            {
                HunterUnitId = hunterUnitId;
                HunterGlobalId = hunterGlobalId;
                PreyUnitId = preyUnitId;
                PreyGlobalId = preyGlobalId;
                PreyType = preyType;
                PlayerId = playerId;
                RawMode = rawMode;
                SourceTileX = sourceTileX;
                SourceTileY = sourceTileY;
                TargetTileX = targetTileX;
                TargetTileY = targetTileY;
                SourcePcl = sourcePcl;
                TargetPcl = targetPcl;
            }

            public bool HasSameNativeInputs(ProbeContext other)
            {
                return HunterUnitId == other.HunterUnitId &&
                    HunterGlobalId == other.HunterGlobalId &&
                    PreyUnitId == other.PreyUnitId &&
                    PreyGlobalId == other.PreyGlobalId &&
                    PreyType == other.PreyType &&
                    PlayerId == other.PlayerId &&
                    RawMode == other.RawMode &&
                    SourcePcl == other.SourcePcl &&
                    TargetPcl == other.TargetPcl;
            }
        }

        private readonly struct ProbeObservation
        {
            public readonly ProbeContext Context;
            public readonly int Mode0Result;
            public readonly int Mode2Result;
            public readonly int RawResult;
            public readonly long Timestamp;
            public readonly long ElapsedMicroseconds;

            public ProbeObservation(
                ProbeContext context,
                int mode0Result,
                int mode2Result,
                int rawResult,
                long timestamp,
                long elapsedMicroseconds)
            {
                Context = context;
                Mode0Result = mode0Result;
                Mode2Result = mode2Result;
                RawResult = rawResult;
                Timestamp = timestamp;
                ElapsedMicroseconds = elapsedMicroseconds;
            }

            public bool HasSameReportedOutcome(ProbeObservation other)
            {
                return Context.HasSameNativeInputs(other.Context) &&
                    Mode0Result == other.Mode0Result &&
                    Mode2Result == other.Mode2Result &&
                    RawResult == other.RawResult;
            }
        }

        private readonly struct ProbeState
        {
            public readonly ProbeObservation Observation;
            public readonly long NextProbeAt;

            public ProbeState(ProbeObservation observation, long nextProbeAt)
            {
                Observation = observation;
                NextProbeAt = nextProbeAt;
            }
        }
    }
}
