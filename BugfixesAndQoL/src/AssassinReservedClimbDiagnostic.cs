// TEMPORARY DIAGNOSTIC: remove this file and every call site carrying
// ASSASSIN_RESERVED_CLIMB_DIAGNOSTIC to remove the investigation completely.
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BugfixesAndQoL
{
    internal sealed class AssassinReservedClimbDiagnostic
    {
        public const string Marker = "ASSASSIN_RESERVED_CLIMB_DIAGNOSTIC";

        private const int MaximumBuildingCandidateLogsPerMap = 160;
        private const int MaximumGenericCandidateLogsPerMap = 40;
        private const int MaximumSelectedEdgeLogsPerMap = 80;
        private const int MaximumRequestSummaryLogsPerMap = 80;
        private const int MaximumRuntimeStateLogsPerMap = 320;
        private const int RuntimeObservationTicksPerRequest = 300;
        private const uint IsWallFlag = 1u << 8;

        private readonly ManualLogSource log;
        private int requestSequence;
        private int currentRequestId;
        private int buildingCandidateLogs;
        private int genericCandidateLogs;
        private int selectedEdgeLogs;
        private int requestSummaryLogs;
        private int requestBuildingCandidates;
        private int requestGenericCandidates;
        private int requestFallbackAccepted;
        private int requestRelaxedStartAccepted;
        private int requestSelectedInterestingEdges;
        private int runtimeStateLogs;
        private int runtimeTicksRemaining;
        private int lastRuntimeTick = int.MinValue;
        private int observedPlayerId;
        private int observedStartX;
        private int observedStartY;
        private int observedTargetX;
        private int observedTargetY;
        private readonly Dictionary<int, string> lastUnitSnapshots = new Dictionary<int, string>();

        public AssassinReservedClimbDiagnostic(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void BeginMap()
        {
            requestSequence = 0;
            currentRequestId = 0;
            buildingCandidateLogs = 0;
            genericCandidateLogs = 0;
            selectedEdgeLogs = 0;
            requestSummaryLogs = 0;
            runtimeStateLogs = 0;
            runtimeTicksRemaining = 0;
            lastRuntimeTick = int.MinValue;
            lastUnitSnapshots.Clear();
            ResetRequestCounters();
            Write("map-begin: counters reset.");
        }

        public void EndMap()
        {
            Write(
                $"map-end: requests={requestSequence}, buildingCandidateLogs={buildingCandidateLogs}, " +
                $"genericCandidateLogs={genericCandidateLogs}, selectedEdgeLogs={selectedEdgeLogs}, " +
                $"requestSummaryLogs={requestSummaryLogs}, runtimeStateLogs={runtimeStateLogs}.");
            currentRequestId = 0;
        }

        public int BeginRequest(
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int maximumNodes,
            int speedDelay,
            bool climbingAllowed,
            int vanillaResult)
        {
            currentRequestId = ++requestSequence;
            observedPlayerId = playerId;
            observedStartX = startX;
            observedStartY = startY;
            observedTargetX = targetX;
            observedTargetY = targetY;
            runtimeTicksRemaining = RuntimeObservationTicksPerRequest;
            lastRuntimeTick = int.MinValue;
            lastUnitSnapshots.Clear();
            ResetRequestCounters();
            if (requestSummaryLogs < MaximumRequestSummaryLogsPerMap)
            {
                Write(
                    $"request-begin: request={currentRequestId}, playerId={playerId}, " +
                    $"start={startX},{startY}, target={targetX},{targetY}, maximumNodes={maximumNodes}, " +
                    $"speedDelay={speedDelay}, climbingAllowed={climbingAllowed}, vanillaResult={vanillaResult}.");
                requestSummaryLogs++;
            }
            return currentRequestId;
        }

        public void ObservePublishedRoute(
            int requestId,
            int generation,
            int touchedNodes,
            int expandedNodes,
            int routeLength,
            short startStamp,
            short startDistance,
            short targetStamp,
            short targetDistance)
        {
            if (requestId != currentRequestId)
                return;
            Write(
                $"route-published: request={requestId}, generation={generation}, touched={touchedNodes}, " +
                $"expanded={expandedNodes}, routeLength={routeLength}, startStamp={startStamp}, " +
                $"startDistance={startDistance}, targetStamp={targetStamp}, targetDistance={targetDistance}.");
        }

        public bool BeginRuntimeTick(int tick)
        {
            if (runtimeTicksRemaining <= 0 || tick == lastRuntimeTick)
                return false;
            lastRuntimeTick = tick;
            runtimeTicksRemaining--;
            return true;
        }

        public bool ShouldObserveUnit(int playerId, int x, int y)
        {
            if (playerId != observedPlayerId)
                return false;
            int startDistance = Math.Abs(x - observedStartX) + Math.Abs(y - observedStartY);
            int targetDistance = Math.Abs(x - observedTargetX) + Math.Abs(y - observedTargetY);
            return startDistance <= 64 || targetDistance <= 64;
        }

        public void ObserveRuntimeUnit(
            int tick,
            int unitId,
            uint globalId,
            int currentX,
            int currentY,
            int targetX,
            int targetY,
            int nextX,
            int nextY,
            uint currentTileId,
            int mappedTileId,
            ushort aiState,
            ushort movingRelevant,
            ushort pathStateFlags,
            ushort pathPosition,
            uint pathSize,
            uint animationTimer,
            byte climbVisualActive,
            ushort climbProgress,
            ushort facing,
            ushort buildingId,
            byte movementMask,
            uint tileFlags,
            short visitStamp,
            short nativeDistance)
        {
            string snapshot =
                $"pos={currentX},{currentY};target={targetX},{targetY};next={nextX},{nextY};" +
                $"tile={currentTileId}/{mappedTileId};ai={aiState};moving={movingRelevant};" +
                $"path={pathStateFlags}/{pathPosition}/{pathSize};animation={animationTimer};" +
                $"climb={climbVisualActive}/{climbProgress}/{facing};building={buildingId};" +
                $"mask={movementMask};flags={tileFlags};stamp={visitStamp};distance={nativeDistance}";
            bool heartbeat = runtimeTicksRemaining % 50 == 0;
            if (!heartbeat && lastUnitSnapshots.TryGetValue(unitId, out string previous) && previous == snapshot)
                return;
            lastUnitSnapshots[unitId] = snapshot;
            if (runtimeStateLogs >= MaximumRuntimeStateLogsPerMap)
                return;
            runtimeStateLogs++;
            Write(
                $"unit-state: request={currentRequestId}, tick={tick}, unitId={unitId}, globalId={globalId}, " +
                $"position={currentX},{currentY}, target={targetX},{targetY}, next={nextX},{nextY}, " +
                $"tile={currentTileId}/mapped{mappedTileId}, aiState={aiState}, moving={movingRelevant}, " +
                $"pathFlags=0x{pathStateFlags:X4}, pathPosition={pathPosition}, pathSize={pathSize}, " +
                $"animationTimer={animationTimer}, climbVisual={climbVisualActive}, climbProgress={climbProgress}, " +
                $"facing={facing}, building={buildingId}, movementMask=0x{movementMask:X2}, " +
                $"tileFlags=0x{tileFlags:X8}, visitStamp={visitStamp}, nativeDistance={nativeDistance}.");
        }

        public void ObserveCandidate(
            int requestId,
            int currentX,
            int currentY,
            int currentTile,
            int targetX,
            int targetY,
            int targetTile,
            int direction,
            ushort currentBuilding,
            ushort targetBuilding,
            byte currentMovementMask,
            byte targetMovementMask,
            uint currentFlags,
            uint targetFlags,
            bool targetAccepted,
            bool startAccepted,
            bool targetBuildingAccepted,
            bool hasWall,
            bool fallbackAccepted,
            bool climbingAllowed)
        {
            if (requestId != currentRequestId || !hasWall)
                return;

            bool buildingAssociated = currentBuilding != 0 || targetBuilding != 0;
            bool relaxedStart = currentBuilding != 0 && startAccepted;
            if (buildingAssociated)
                requestBuildingCandidates++;
            else
                requestGenericCandidates++;
            if (fallbackAccepted)
                requestFallbackAccepted++;
            if (relaxedStart)
                requestRelaxedStartAccepted++;

            if (buildingAssociated)
            {
                if (buildingCandidateLogs >= MaximumBuildingCandidateLogsPerMap)
                    return;
                buildingCandidateLogs++;
            }
            else
            {
                if (genericCandidateLogs >= MaximumGenericCandidateLogsPerMap)
                    return;
                genericCandidateLogs++;
            }

            Write(
                $"candidate: request={requestId}, current={currentX},{currentY}/tile{currentTile}, " +
                $"target={targetX},{targetY}/tile{targetTile}, direction={direction}, " +
                $"building={currentBuilding}->{targetBuilding}, movementMask=0x{currentMovementMask:X2}->0x{targetMovementMask:X2}, " +
                $"flags=0x{currentFlags:X8}->0x{targetFlags:X8}, targetAccepted={targetAccepted}, " +
                $"startAccepted={startAccepted}, targetBuildingAccepted={targetBuildingAccepted}, hasWall={hasWall}, " +
                $"fallbackAccepted={fallbackAccepted}, climbingAllowed={climbingAllowed}, " +
                $"finalAccepted={fallbackAccepted && climbingAllowed}, relaxedStart={relaxedStart}.");
        }

        public void ObserveSelectedEdge(
            int currentX,
            int currentY,
            int currentTile,
            int targetX,
            int targetY,
            int targetTile,
            bool ordinaryEdge,
            ushort currentBuilding,
            ushort targetBuilding,
            byte currentMovementMask,
            byte targetMovementMask,
            uint currentFlags,
            uint targetFlags)
        {
            bool hasWall = ((currentFlags | targetFlags) & IsWallFlag) != 0;
            bool buildingAssociated = currentBuilding != 0 || targetBuilding != 0;
            if (!hasWall && !buildingAssociated)
                return;

            requestSelectedInterestingEdges++;
            if (selectedEdgeLogs >= MaximumSelectedEdgeLogsPerMap)
                return;
            selectedEdgeLogs++;
            Write(
                $"selected-edge: request={currentRequestId}, current={currentX},{currentY}/tile{currentTile}, " +
                $"target={targetX},{targetY}/tile{targetTile}, ordinary={ordinaryEdge}, " +
                $"building={currentBuilding}->{targetBuilding}, movementMask=0x{currentMovementMask:X2}->0x{targetMovementMask:X2}, " +
                $"flags=0x{currentFlags:X8}->0x{targetFlags:X8}.");
        }

        public void CompleteRequest(int requestId, bool weightedResult)
        {
            if (requestId != currentRequestId)
                return;
            if ((requestBuildingCandidates != 0 || requestGenericCandidates != 0 ||
                 requestSelectedInterestingEdges != 0) &&
                requestSummaryLogs < MaximumRequestSummaryLogsPerMap)
            {
                Write(
                    $"request-end: request={requestId}, weightedResult={weightedResult}, " +
                    $"buildingCandidates={requestBuildingCandidates}, genericWallCandidates={requestGenericCandidates}, " +
                    $"fallbackAccepted={requestFallbackAccepted}, relaxedStartAccepted={requestRelaxedStartAccepted}, " +
                    $"selectedInterestingEdges={requestSelectedInterestingEdges}.");
                requestSummaryLogs++;
            }
        }

        public void ReportFailure(int requestId, Exception exception)
        {
            log.LogError(
                $"[{TimestampNow()}] [{Marker}] request-failure: request={requestId}, exception={exception}");
        }

        public void ReportRuntimeFailure(int tick, Exception exception)
        {
            runtimeTicksRemaining = 0;
            log.LogError(
                $"[{TimestampNow()}] [{Marker}] runtime-failure: request={currentRequestId}, tick={tick}, exception={exception}");
        }

        private void ResetRequestCounters()
        {
            requestBuildingCandidates = 0;
            requestGenericCandidates = 0;
            requestFallbackAccepted = 0;
            requestRelaxedStartAccepted = 0;
            requestSelectedInterestingEdges = 0;
        }

        private void Write(string message)
        {
            log.LogDebug($"[{TimestampNow()}] [{Marker}] {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }
    }
}
