using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Zhuqiaomon.Memory;

namespace OxTetherIdleFixTest
{
    internal sealed unsafe class OxTetherIdleFixTestRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly Dictionary<int, OxIdleEpisodePolicy> episodes =
            new Dictionary<int, OxIdleEpisodePolicy>();
        private readonly HashSet<int> observedUnitIds = new HashSet<int>();
        private readonly List<int> staleUnitIds = new List<int>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, OxObservation> lastObservations =
            new Dictionary<int, OxObservation>();
        private readonly Dictionary<int, CandidateTrace> candidateTraces =
            new Dictionary<int, CandidateTrace>();
        private readonly Dictionary<int, TargetBlockade> targetBlockades =
            new Dictionary<int, TargetBlockade>();
        private readonly Dictionary<int, BlockadeOrigin> blockadeOrigins =
            new Dictionary<int, BlockadeOrigin>();
        private readonly Dictionary<int, GeneralStallTrace> generalStalls =
            new Dictionary<int, GeneralStallTrace>();

        internal const int TargetBlockadeIntervalSeconds = 30;
        internal const int FleetSnapshotIntervalSeconds = 10;
        internal const int GeneralStallTicks = 50;
        internal const int GeneralStallRepeatTicks = 250;
        internal const int BlockerNoProgressTimeoutTicks = 250;
        internal const int BlockerApproachSearchRadius = 8;
        private static readonly long TargetBlockadeIntervalStopwatchTicks =
            SecondsToStopwatchTicks(TargetBlockadeIntervalSeconds);
        private static readonly long FleetSnapshotIntervalStopwatchTicks =
            SecondsToStopwatchTicks(FleetSnapshotIntervalSeconds);
        private static readonly long DeferredInjectionLogIntervalStopwatchTicks =
            SecondsToStopwatchTicks(5);

        private bool applied;
        private bool mapActive;
        private bool disabledForMap;
        private long confirmedCount;
        private long verifiedCount;
        private long unverifiedCount;
        private long candidateStartedCount;
        private long candidateRecoveredCount;
        private long targetBlockadeStartedCount;
        private long targetBlockadeReleasedCount;
        private long generalStallConfirmedCount;
        private long nextInjectionTimestamp;
        private long nextDeferredInjectionLogTimestamp;
        private long nextFleetSnapshotTimestamp;
        private int lastBlockedUnitId;

        public OxTetherIdleFixTestRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Apply()
        {
            if (applied)
                return;

            ValidateGameUnitLayout();

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => BeginMap($"new map campaignMapId={args.CampaignMapId}")));
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => BeginMap($"loaded save file={args.FileName ?? "<null>"}")));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => ReleaseAllTargetBlockades("mapUnloading")));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => EndMap()));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;

            applied = true;
            LogInfo(
                $"OX_IDLE_DIAGNOSTIC_READY: correctionActive=true, scanEverySimulationTick=true, " +
                $"requiredConsecutiveTicks={OxIdleEpisodePolicy.RequiredConsecutiveTicks}, " +
                $"verificationTicks={OxIdleEpisodePolicy.VerificationTicks}, unitIdsAreOneBased=true, " +
                $"targetBlockadeActive=true, targetBlockadeIntervalSeconds={TargetBlockadeIntervalSeconds}, " +
                $"blockerNoProgressTimeoutTicks={BlockerNoProgressTimeoutTicks}, " +
                $"generalStallTicks={GeneralStallTicks}, fleetSnapshotIntervalSeconds={FleetSnapshotIntervalSeconds}, " +
                $"physicalBlockerTeleportToApproach=true, blockerApproachSearchRadius={BlockerApproachSearchRadius}, " +
                "blockerUsesVanillaMoveToTarget=true, registeredOriginFallback=VanillaMoveToTileFromCurrentPosition, " +
                "directTileMutation=false, " +
                "directTargetOxMutation=false, replanSuppression=false, targetBlockadeLimit=none.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            ClearEpisodes(releaseBlockades: true);
            mapActive = false;
            applied = false;
        }

        private void BeginMap(string reason)
        {
            ClearEpisodes(releaseBlockades: false);
            mapActive = true;
            disabledForMap = false;
            confirmedCount = 0;
            verifiedCount = 0;
            unverifiedCount = 0;
            candidateStartedCount = 0;
            candidateRecoveredCount = 0;
            targetBlockadeStartedCount = 0;
            targetBlockadeReleasedCount = 0;
            generalStallConfirmedCount = 0;
            lastBlockedUnitId = 0;
            long now = Stopwatch.GetTimestamp();
            nextInjectionTimestamp = AddStopwatchTicks(now, TargetBlockadeIntervalStopwatchTicks);
            nextDeferredInjectionLogTimestamp = nextInjectionTimestamp;
            nextFleetSnapshotTimestamp = now;
            LogInfo($"OX_IDLE_MAP_TRACKING_STARTED: reason={reason}.");
        }

        private void EndMap()
        {
            LogInfo(
                $"OX_IDLE_MAP_SUMMARY: confirmed={confirmedCount}, verified={verifiedCount}, " +
                $"unverified={unverifiedCount}, candidatesStarted={candidateStartedCount}, " +
                $"candidatesRecovered={candidateRecoveredCount}, targetBlockadesStarted={targetBlockadeStartedCount}, " +
                $"targetBlockadesReleased={targetBlockadeReleasedCount}, generalStallsConfirmed={generalStallConfirmedCount}, " +
                $"trackedEpisodes={episodes.Count}, activeCandidateTraces={candidateTraces.Count}, " +
                $"trackedTargetBlockades={targetBlockades.Count}, disabled={disabledForMap}.");
            mapActive = false;
            disabledForMap = false;
            ClearEpisodes(releaseBlockades: false);
        }

        private void OnGameTick(int tick)
        {
            if (!mapActive || disabledForMap)
                return;

            try
            {
                ScanOxen(tick);
            }
            catch (Exception exception)
            {
                disabledForMap = true;
                try
                {
                    ReleaseAllTargetBlockades("diagnosticFailure");
                }
                catch (Exception cleanupException)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"OX_IDLE_TARGET_BLOCKADE_CLEANUP_FAILED: tick={tick}, exception={cleanupException}");
                }
                ClearEpisodes(releaseBlockades: false);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"OX_IDLE_DIAGNOSTIC_DISABLED: tick={tick}, reason=unit memory inspection failed, exception={exception}");
            }
        }

        private void ScanOxen(int tick)
        {
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            observedUnitIds.Clear();

            long now = Stopwatch.GetTimestamp();
            if (units._array != null && now >= nextInjectionTimestamp)
                TryStartTargetBlockade(units, tick, now);

            bool writeFleetSnapshot = now >= nextFleetSnapshotTimestamp;
            int oxCount = 0;
            Dictionary<ushort, int> stateCounts = writeFleetSnapshot
                ? new Dictionary<ushort, int>()
                : null;

            if (units._array != null)
            {
                for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
                {
                    GameUnit* unit = units.GetValuePointer(spanIndex);
                    if (unit == null ||
                        unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_UnitChimp != eChimps.CHIMP_TYPE_QUARRY_OX)
                    {
                        continue;
                    }

                    int unitId = spanIndex + 1;
                    oxCount++;
                    observedUnitIds.Add(unitId);
                    OxObservation observation = Capture(unitId, unit);
                    observation = MaintainTargetBlockade(tick, units, unit, observation);
                    RecordUnitTransition(tick, unit, observation);
                    UpdateGeneralStall(tick, unit, observation);
                    if (writeFleetSnapshot)
                    {
                        stateCounts.TryGetValue(observation.State, out int count);
                        stateCounts[observation.State] = count + 1;
                        LogInfo($"OX_IDLE_FLEET_UNIT: tick={tick}, {Describe(unit, observation)}.");
                    }

                    if (!episodes.TryGetValue(unitId, out OxIdleEpisodePolicy episode))
                    {
                        if (!observation.HasIdleBugSignature)
                        {
                            EndCandidateTraceIfPresent(tick, observation, "signatureCleared", unit);
                            continue;
                        }

                        episode = new OxIdleEpisodePolicy();
                        episodes.Add(unitId, episode);
                    }

                    UpdateCandidateTrace(tick, unit, observation);
                    OxEpisodeAction action = episode.Observe(observation, tick);
                    if (action == OxEpisodeAction.ConfirmAndRepair)
                    {
                        candidateTraces.Remove(unitId);
                        ConfirmAndRepair(tick, unit, observation);
                    }
                    else if (action == OxEpisodeAction.Verified)
                        RecordVerified(tick, observation);
                    else if (action == OxEpisodeAction.Unverified)
                        RecordUnverified(tick, observation);

                    if (!episode.IsActive)
                        episodes.Remove(unitId);
                }
            }

            if (writeFleetSnapshot)
            {
                LogInfo(
                    $"OX_IDLE_FLEET_SUMMARY: tick={tick}, oxCount={oxCount}, " +
                    $"states={FormatStateCounts(stateCounts)}, activeCandidates={candidateTraces.Count}, " +
                    $"trackedTargetBlockades={targetBlockades.Count}, activeGeneralStalls={generalStalls.Count}.");
                nextFleetSnapshotTimestamp = AddStopwatchTicks(now, FleetSnapshotIntervalStopwatchTicks);
            }

            staleUnitIds.Clear();
            foreach (KeyValuePair<int, OxIdleEpisodePolicy> pair in episodes)
            {
                if (!observedUnitIds.Contains(pair.Key))
                    staleUnitIds.Add(pair.Key);
            }
            foreach (int staleUnitId in staleUnitIds)
            {
                if (candidateTraces.TryGetValue(staleUnitId, out CandidateTrace trace))
                    EndCandidateTrace(tick, trace, "unitUnavailable", null);
                episodes.Remove(staleUnitId);
                candidateTraces.Remove(staleUnitId);
                ReleaseTargetBlockade(staleUnitId, tick, "unitUnavailable", null);
                generalStalls.Remove(staleUnitId);
                lastObservations.Remove(staleUnitId);
            }

            staleUnitIds.Clear();
            foreach (int knownUnitId in lastObservations.Keys)
            {
                if (!observedUnitIds.Contains(knownUnitId))
                    staleUnitIds.Add(knownUnitId);
            }
            foreach (int staleUnitId in staleUnitIds)
            {
                lastObservations.Remove(staleUnitId);
                ReleaseTargetBlockade(staleUnitId, tick, "unitUnavailable", null);
                generalStalls.Remove(staleUnitId);
            }
        }

        private void TryStartTargetBlockade(SimpleNativeArray<GameUnit> units, int tick, long now)
        {
            int selectedUnitId = 0;
            GameUnit* selectedUnit = null;
            OxObservation selectedObservation = default;

            for (int pass = 0; pass < 2 && selectedUnit == null; pass++)
            {
                for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
                {
                    int unitId = spanIndex + 1;
                    if ((pass == 0 && unitId <= lastBlockedUnitId) ||
                        (pass == 1 && unitId > lastBlockedUnitId))
                    {
                        continue;
                    }

                    GameUnit* unit = units.GetValuePointer(spanIndex);
                    if (unit == null || unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_UnitChimp != eChimps.CHIMP_TYPE_QUARRY_OX)
                    {
                        continue;
                    }

                    OxObservation observation = Capture(unitId, unit);
                    bool episodeActive = episodes.TryGetValue(unitId, out OxIdleEpisodePolicy episode) && episode.IsActive;
                    if (!OxTargetBlockadePolicy.IsEligible(observation, episodeActive) ||
                        IsUnitUsedByActiveBlockade(unitId))
                        continue;

                    selectedUnitId = unitId;
                    selectedUnit = unit;
                    selectedObservation = observation;
                    break;
                }
            }

            if (selectedUnit == null)
            {
                if (now >= nextDeferredInjectionLogTimestamp)
                {
                    LogInfo(
                        $"OX_IDLE_TARGET_BLOCKADE_DEFERRED: tick={tick}, " +
                        "reason=no eligible moving state-1/state-3 quarry ox with a mismatched requested target.");
                    nextDeferredInjectionLogTimestamp = AddStopwatchTicks(
                        now,
                        DeferredInjectionLogIntervalStopwatchTicks);
                }
                return;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int targetTileId = tileApi.GetTileId(selectedObservation.RequestedX, selectedObservation.RequestedY);
            if (!tileApi.IsValidTileId(targetTileId))
            {
                if (now >= nextDeferredInjectionLogTimestamp)
                {
                    LogInfo(
                        $"OX_IDLE_TARGET_BLOCKADE_DEFERRED: tick={tick}, unitId={selectedUnitId}, " +
                        "reason=requested target tile is invalid, " +
                        $"requested={selectedObservation.RequestedX}/{selectedObservation.RequestedY}, " +
                        $"targetTileId={targetTileId}.");
                    nextDeferredInjectionLogTimestamp = AddStopwatchTicks(
                        now,
                        DeferredInjectionLogIntervalStopwatchTicks);
                }
                return;
            }

            ushort existingTileUnitId = tileApi.GetTileUnitId(targetTileId);
            int blockerUnitId = existingTileUnitId;
            bool blockerCommanded = blockerUnitId == 0;
            bool blockerTeleported = false;
            ushort blockerApproachX = 0;
            ushort blockerApproachY = 0;
            int blockerApproachTileId = 0;
            if (blockerUnitId == 0)
            {
                blockerUnitId = FindBlockerUnitId(
                    units,
                    selectedUnitId,
                    selectedObservation,
                    out blockerTeleported);
                if (blockerUnitId != 0 && blockerTeleported &&
                    !TryFindFreeBlockerApproach(
                        tileApi,
                        selectedObservation,
                        out blockerApproachX,
                        out blockerApproachY,
                        out blockerApproachTileId))
                {
                    blockerUnitId = 0;
                }
            }

            if (blockerUnitId == 0 || blockerUnitId == selectedUnitId ||
                IsUnitUsedByActiveBlockade(blockerUnitId) ||
                !TryGetLivingUnitById(units, blockerUnitId, out GameUnit* blockerUnit) ||
                (blockerTeleported && blockerUnit->r_UnitChimp != eChimps.CHIMP_TYPE_QUARRY_OX))
            {
                if (now >= nextDeferredInjectionLogTimestamp)
                {
                    LogInfo(
                        $"OX_IDLE_TARGET_BLOCKADE_DEFERRED: tick={tick}, unitId={selectedUnitId}, " +
                        "reason=no valid independent blocker unit, " +
                        $"requested={selectedObservation.RequestedX}/{selectedObservation.RequestedY}, " +
                        $"targetTileId={targetTileId}, existingTileUnitId={existingTileUnitId}.");
                    nextDeferredInjectionLogTimestamp = AddStopwatchTicks(
                        now,
                        DeferredInjectionLogIntervalStopwatchTicks);
                }
                return;
            }

            BlockerSnapshot blockerBefore = BlockerSnapshot.Capture(blockerUnit);
            if (blockerTeleported)
            {
                GameUnitManagerAPI.Instance.SetCurrentLocalTilePosition(
                    blockerUnitId,
                    new UnmanagedVector2<ushort>(
                        blockerApproachX,
                        blockerApproachY));
                if (!TryGetLivingUnitById(units, blockerUnitId, out blockerUnit) ||
                    blockerUnit->r_GlobalId != blockerBefore.GlobalId ||
                    blockerUnit->r_CurrentTilePositionX != blockerApproachX ||
                    blockerUnit->r_CurrentTilePositionY != blockerApproachY)
                {
                    TryRestoreTeleportedBlocker(blockerUnitId, blockerBefore, "teleportVerificationFailed");
                    LogInfo(
                        $"OX_IDLE_TARGET_BLOCKADE_DEFERRED: tick={tick}, unitId={selectedUnitId}, " +
                        $"reason=physical blocker approach teleport could not be verified, blockerUnitId={blockerUnitId}, " +
                        $"approach={blockerApproachX}/{blockerApproachY}, approachTileId={blockerApproachTileId}.");
                    return;
                }

            }
            else
            {
                blockerApproachX = blockerUnit->r_CurrentTilePositionX;
                blockerApproachY = blockerUnit->r_CurrentTilePositionY;
                blockerApproachTileId = blockerCommanded
                    ? tileApi.GetTileId(blockerApproachX, blockerApproachY)
                    : targetTileId;
            }

            if (blockerCommanded)
            {
                // The target grid must be populated by Vanilla movement. Merely changing
                // r_CurrentTilePosition does not register a stationary unit as an occupant.
                GameUnitManagerAPI.Instance.MoveToTile(
                    blockerUnitId,
                    selectedObservation.RequestedX,
                    selectedObservation.RequestedY,
                    0);
                if (!TryGetLivingUnitById(units, blockerUnitId, out blockerUnit) ||
                    blockerUnit->r_GlobalId != blockerBefore.GlobalId ||
                    ((blockerUnit->r_CurrentTilePositionX != selectedObservation.RequestedX ||
                      blockerUnit->r_CurrentTilePositionY != selectedObservation.RequestedY) &&
                     (blockerUnit->r_TargetTilePositionX2 != selectedObservation.RequestedX ||
                      blockerUnit->r_TargetTilePositionY2 != selectedObservation.RequestedY ||
                      blockerUnit->r_PathPlanStateBitFlags == 0)))
                {
                    string blockerAfterCommandDescription = blockerUnit == null
                        ? "unavailable"
                        : BlockerSnapshot.Capture(blockerUnit).ToString();
                    if (blockerTeleported)
                    {
                        TryRestoreTeleportedBlocker(
                            blockerUnitId,
                            blockerBefore,
                            "vanillaMoveCommandRejected");
                    }
                    else
                    {
                        TryResumeRegisteredBlocker(
                            blockerUnitId,
                            blockerBefore,
                            "vanillaMoveCommandRejected",
                            out _);
                    }
                    LogInfo(
                        $"OX_IDLE_TARGET_BLOCKADE_DEFERRED: tick={tick}, unitId={selectedUnitId}, " +
                        $"reason=Vanilla MoveToTile did not create a route to the blocked target, " +
                        $"blockerUnitId={blockerUnitId}, approach={blockerApproachX}/{blockerApproachY}, " +
                        $"approachTileId={blockerApproachTileId}, blockerAfterCommand=({blockerAfterCommandDescription}).");
                    return;
                }
            }

            bool occupancyConfirmed = tileApi.GetTileUnitId(targetTileId) == blockerUnitId;
            int blockerTravelDistance = Math.Max(
                Math.Abs(blockerApproachX - selectedObservation.RequestedX),
                Math.Abs(blockerApproachY - selectedObservation.RequestedY));
            BlockerSnapshot blockerAfterCommand = BlockerSnapshot.Capture(blockerUnit);
            targetBlockades[selectedUnitId] = new TargetBlockade(
                selectedObservation.GlobalId,
                selectedObservation.State,
                selectedObservation.RequestedX,
                selectedObservation.RequestedY,
                targetTileId,
                blockerUnitId,
                blockerUnit->r_GlobalId,
                blockerCommanded,
                blockerTeleported,
                occupancyConfirmed,
                blockerApproachX,
                blockerApproachY,
                blockerApproachTileId,
                blockerBefore,
                blockerAfterCommand,
                tick);
            targetBlockadeStartedCount++;
            lastBlockedUnitId = selectedUnitId;
            nextInjectionTimestamp = AddStopwatchTicks(now, TargetBlockadeIntervalStopwatchTicks);
            nextDeferredInjectionLogTimestamp = nextInjectionTimestamp;

            LogInfo(
                $"OX_IDLE_TARGET_BLOCKADE_APPLIED: tick={tick}, sequence={targetBlockadeStartedCount}, " +
                $"targetUnitId={selectedUnitId}, targetGlobalId={selectedObservation.GlobalId}, " +
                $"blockerUnitId={blockerUnitId}, blockerGlobalId={blockerUnit->r_GlobalId}, " +
                $"blockerPosition={blockerUnit->r_CurrentTilePositionX}/{blockerUnit->r_CurrentTilePositionY}, " +
                $"blockedTarget={selectedObservation.RequestedX}/{selectedObservation.RequestedY}, " +
                $"targetTileId={targetTileId}, priorTileUnitId={existingTileUnitId}, " +
                $"tileUnitIdAfter={tileApi.GetTileUnitId(targetTileId)}, blockerCommanded={blockerCommanded}, " +
                $"blockerTeleported={blockerTeleported}, " +
                $"occupancyConfirmed={occupancyConfirmed}, " +
                $"blockerApproach={blockerApproachX}/{blockerApproachY}, " +
                $"blockerApproachTileId={blockerApproachTileId}, " +
                $"blockerTravelDistance={blockerTravelDistance}, " +
                $"blockerNoProgressTimeoutTicks={BlockerNoProgressTimeoutTicks}, " +
                $"targetSnapshot=({Describe(selectedUnit, selectedObservation)}), " +
                $"blockerBefore=({blockerBefore}), blockerAfterCommand=({blockerAfterCommand}), " +
                $"mechanism={(blockerTeleported ? "SetCurrentLocalTilePositionAdjacent+VanillaMoveToTile" : blockerCommanded ? "VanillaMoveToTileFromCurrentPosition" : "existingVanillaOccupancy")}, " +
                "directTileMutation=false, directTargetOxMutation=false, replanSuppression=false, " +
                $"releasePolicy=state-or-target-change/signature/general-stall, " +
                $"nextBlockadeAfterSeconds={TargetBlockadeIntervalSeconds}.");
        }

        private int FindBlockerUnitId(
            SimpleNativeArray<GameUnit> units,
            int targetUnitId,
            in OxObservation target,
            out bool canTeleportWithoutOrphanedOccupancy)
        {
            canTeleportWithoutOrphanedOccupancy = false;
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            for (int occupancyPass = 0; occupancyPass < 2; occupancyPass++)
            {
                for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
                {
                    int unitId = spanIndex + 1;
                    GameUnit* unit = units.GetValuePointer(spanIndex);
                    if (unitId == targetUnitId || IsUnitUsedByActiveBlockade(unitId) || unit == null ||
                        unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_UnitChimp != eChimps.CHIMP_TYPE_QUARRY_OX ||
                        (unit->r_CurrentTilePositionX == target.RequestedX &&
                         unit->r_CurrentTilePositionY == target.RequestedY))
                    {
                        continue;
                    }

                    if (episodes.TryGetValue(unitId, out OxIdleEpisodePolicy episode) && episode.IsActive)
                        continue;

                    OxObservation observation = Capture(unitId, unit);
                    if (!OxTargetBlockadePolicy.IsEligibleMovingBlocker(observation) ||
                        !OxTargetBlockadePolicy.HasIndependentTarget(
                            observation,
                            target.RequestedX,
                            target.RequestedY))
                        continue;

                    int currentTileId = tileApi.GetTileId(observation.CurrentX, observation.CurrentY);
                    if (!tileApi.IsValidTileId(currentTileId))
                        continue;
                    ushort originTileUnitId = tileApi.GetTileUnitId(currentTileId);
                    bool originIsUnregistered = originTileUnitId == 0;
                    bool originIsRegisteredToBlocker = originTileUnitId == unitId;
                    if ((occupancyPass == 0 && !originIsUnregistered) ||
                        (occupancyPass == 1 && !originIsRegisteredToBlocker))
                    {
                        continue;
                    }

                    canTeleportWithoutOrphanedOccupancy = originIsUnregistered;
                    return unitId;
                }
            }

            return 0;
        }

        private static bool TryFindFreeBlockerApproach(
            GameTileManagerAPI tileApi,
            in OxObservation target,
            out ushort approachX,
            out ushort approachY,
            out int approachTileId)
        {
            approachX = 0;
            approachY = 0;
            approachTileId = 0;

            for (int radius = 1; radius <= BlockerApproachSearchRadius; radius++)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    for (int offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != radius)
                            continue;

                        int x = target.RequestedX + offsetX;
                        int y = target.RequestedY + offsetY;
                        if (!tileApi.IsTileInsideMapBounds(x, y) ||
                            (x == target.CurrentX && y == target.CurrentY))
                        {
                            continue;
                        }

                        int tileId = tileApi.GetTileId(x, y);
                        if (!tileApi.IsValidTileId(tileId) ||
                            tileApi.GetTileBuildingId(tileId) != 0 ||
                            tileApi.GetTileUnitId(tileId) != 0 ||
                            !tileApi.IsTileWalkableAndUnoccupied(tileId))
                        {
                            continue;
                        }

                        approachX = checked((ushort)x);
                        approachY = checked((ushort)y);
                        approachTileId = tileId;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsUnitUsedByActiveBlockade(int unitId)
        {
            if (targetBlockades.ContainsKey(unitId))
                return true;
            foreach (TargetBlockade blockade in targetBlockades.Values)
            {
                if (blockade.BlockerUnitId == unitId)
                    return true;
            }
            return false;
        }

        private static bool TryGetLivingUnitById(
            SimpleNativeArray<GameUnit> units,
            int unitId,
            out GameUnit* unit)
        {
            unit = null;
            int spanIndex = unitId - 1;
            if (spanIndex < 0 || spanIndex >= units.Length)
                return false;
            unit = units.GetValuePointer(spanIndex);
            return unit != null && unit->r_AliveState == AliveState.IsAlive;
        }

        private void UpdateCandidateTrace(int tick, GameUnit* unit, in OxObservation observation)
        {
            if (!observation.HasIdleBugSignature)
            {
                EndCandidateTraceIfPresent(tick, observation, "signatureCleared", unit);
                return;
            }

            string source = GetEpisodeSource(observation);
            if (!candidateTraces.TryGetValue(observation.UnitId, out CandidateTrace trace))
            {
                StartCandidateTrace(tick, unit, observation, source);
                return;
            }

            if (tick != trace.LastTick + 1 || !trace.LastObservation.IsSameCandidateAs(observation))
            {
                string outcome = tick != trace.LastTick + 1 ? "tickGap" : "snapshotChanged";
                EndCandidateTrace(tick, trace, outcome, unit);
                StartCandidateTrace(tick, unit, observation, source);
                return;
            }

            trace.LastObservation = observation;
            trace.LastTick = tick;
            trace.ConsecutiveTicks++;
            candidateTraces[observation.UnitId] = trace;
        }

        private void StartCandidateTrace(int tick, GameUnit* unit, in OxObservation observation, string source)
        {
            CandidateTrace trace = new CandidateTrace(
                observation,
                tick,
                Stopwatch.GetTimestamp(),
                source,
                Describe(unit, observation));
            candidateTraces[observation.UnitId] = trace;
            candidateStartedCount++;
            LogInfo(
                $"OX_IDLE_CANDIDATE_STARTED: tick={tick}, source={source}, {trace.StartDescription}, " +
                $"requiredConsecutiveTicks={OxIdleEpisodePolicy.RequiredConsecutiveTicks}.");
        }

        private OxObservation MaintainTargetBlockade(
            int tick,
            SimpleNativeArray<GameUnit> units,
            GameUnit* unit,
            in OxObservation rawObservation)
        {
            if (!targetBlockades.TryGetValue(rawObservation.UnitId, out TargetBlockade blockade))
                return rawObservation;

            if (blockade.TargetGlobalId != rawObservation.GlobalId)
            {
                ReleaseTargetBlockade(rawObservation.UnitId, tick, "targetUnitIdReused", unit);
                return rawObservation;
            }

            string releaseReason = null;
            if (!TryGetMatchingBlocker(units, blockade, out GameUnit* blockerUnit))
                releaseReason = "blockerUnavailableOrReused";
            else if (rawObservation.HasIdleBugSignature)
                releaseReason = "vanillaProducedIdleSignature";
            else if (rawObservation.State != blockade.InitialState)
                releaseReason = "targetStateChanged";
            else if (rawObservation.RequestedX != blockade.TargetX ||
                     rawObservation.RequestedY != blockade.TargetY)
                releaseReason = "requestedTargetChanged";
            else if (blockade.OccupancyConfirmed &&
                     tick - blockade.OccupancyConfirmedTick >= GeneralStallTicks &&
                     generalStalls.TryGetValue(rawObservation.UnitId, out GeneralStallTrace stall) &&
                     stall.ConsecutiveTicks >= GeneralStallTicks)
                releaseReason = "generalTravelStallObserved";

            if (releaseReason != null)
            {
                ReleaseTargetBlockade(rawObservation.UnitId, tick, releaseReason, unit);
                return rawObservation;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            ushort tileUnitId = tileApi.GetTileUnitId(blockade.TargetTileId);
            if (!blockade.BlockerCommanded)
            {
                if (tileUnitId != blockade.BlockerUnitId)
                    ReleaseTargetBlockade(rawObservation.UnitId, tick, "existingBlockerLeftTarget", unit);
                return rawObservation;
            }

            if (!blockade.OccupancyConfirmed)
            {
                if (tileUnitId == blockade.BlockerUnitId)
                {
                    blockade.OccupancyConfirmed = true;
                    blockade.OccupancyConfirmedTick = tick;
                    LogInfo(
                        $"OX_IDLE_TARGET_BLOCKADE_OCCUPANCY_CONFIRMED: tick={tick}, " +
                        $"startTick={blockade.StartTick}, waitTicks={tick - blockade.StartTick}, " +
                        $"targetUnitId={rawObservation.UnitId}, blockerUnitId={blockade.BlockerUnitId}, " +
                        $"target={blockade.TargetX}/{blockade.TargetY}, tileUnitId={tileUnitId}, " +
                        "source=VanillaTileUnitIdGrid.");
                    return rawObservation;
                }

                if (tileUnitId != 0)
                {
                    ReleaseTargetBlockade(rawObservation.UnitId, tick, "realOccupantTookTarget", unit);
                    return rawObservation;
                }
                if ((blockerUnit->r_CurrentTilePositionX != blockade.TargetX ||
                     blockerUnit->r_CurrentTilePositionY != blockade.TargetY) &&
                    (blockerUnit->r_TargetTilePositionX2 != blockade.TargetX ||
                     blockerUnit->r_TargetTilePositionY2 != blockade.TargetY ||
                     blockerUnit->r_PathPlanStateBitFlags == 0))
                {
                    ReleaseTargetBlockade(rawObservation.UnitId, tick, "blockerRouteLostBeforeOccupancy", unit);
                    return rawObservation;
                }

                if (OxTargetBlockadePolicy.DidBlockerAdvance(
                        blockade.LastBlockerX,
                        blockade.LastBlockerY,
                        blockade.LastBlockerPathCursor,
                        blockerUnit->r_CurrentTilePositionX,
                        blockerUnit->r_CurrentTilePositionY,
                        blockerUnit->p_CurrentPathPlanPosition))
                {
                    blockade.LastBlockerX = blockerUnit->r_CurrentTilePositionX;
                    blockade.LastBlockerY = blockerUnit->r_CurrentTilePositionY;
                    blockade.LastBlockerPathCursor = blockerUnit->p_CurrentPathPlanPosition;
                    blockade.LastProgressTick = tick;
                }

                if (tick - blockade.LastProgressTick >= BlockerNoProgressTimeoutTicks)
                {
                    ReleaseTargetBlockade(rawObservation.UnitId, tick, "blockerNoProgressTimeout", unit);
                }
                return rawObservation;
            }

            if (tileUnitId != blockade.BlockerUnitId)
            {
                ReleaseTargetBlockade(
                    rawObservation.UnitId,
                    tick,
                    tileUnitId == 0 ? "VanillaBlockerLeftTarget" : "realOccupantTookTarget",
                    unit);
                return rawObservation;
            }
            return rawObservation;
        }

        private static bool TryGetMatchingBlocker(
            SimpleNativeArray<GameUnit> units,
            TargetBlockade blockade,
            out GameUnit* blockerUnit)
        {
            blockerUnit = null;
            int spanIndex = blockade.BlockerUnitId - 1;
            if (spanIndex < 0 || spanIndex >= units.Length)
                return false;

            blockerUnit = units.GetValuePointer(spanIndex);
            return blockerUnit != null &&
                blockerUnit->r_AliveState == AliveState.IsAlive &&
                (!blockade.BlockerCommanded ||
                 blockerUnit->r_UnitChimp == eChimps.CHIMP_TYPE_QUARRY_OX) &&
                blockerUnit->r_GlobalId == blockade.BlockerGlobalId;
        }

        private void ReleaseTargetBlockade(
            int targetUnitId,
            int tick,
            string reason,
            GameUnit* targetUnit)
        {
            if (!targetBlockades.TryGetValue(targetUnitId, out TargetBlockade blockade))
                return;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            ushort tileUnitIdBefore = tileApi.IsValidTileId(blockade.TargetTileId)
                ? tileApi.GetTileUnitId(blockade.TargetTileId)
                : (ushort)0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            bool blockerPhysicallyAtTarget = false;
            bool blockerRestored = false;
            string blockerRestoreDisposition = "notTeleported";
            if (TryGetMatchingBlocker(units, blockade, out GameUnit* blockerUnit))
            {
                blockerPhysicallyAtTarget =
                    blockerUnit->r_CurrentTilePositionX == blockade.TargetX &&
                    blockerUnit->r_CurrentTilePositionY == blockade.TargetY;
                if (blockade.BlockerCommanded)
                {
                    bool blockerWasVanillaRegistered =
                        blockade.OccupancyConfirmed ||
                        blockerPhysicallyAtTarget ||
                        tileUnitIdBefore == blockade.BlockerUnitId;
                    blockerRestored = blockerWasVanillaRegistered || !blockade.BlockerTeleported
                        ? TryResumeRegisteredBlocker(
                            blockade.BlockerUnitId,
                            blockade.BlockerBefore,
                            reason,
                            out blockerRestoreDisposition)
                        : TryRestoreTeleportedBlocker(
                            blockade.BlockerUnitId,
                            blockade.BlockerBefore,
                            reason,
                            out blockerRestoreDisposition);
                }
            }
            else if (blockade.BlockerTeleported)
                blockerRestoreDisposition = "identityUnavailableOrReused";

            if (reason == "vanillaProducedIdleSignature" ||
                reason == "generalTravelStallObserved")
            {
                blockadeOrigins[targetUnitId] = new BlockadeOrigin(
                    blockade.TargetGlobalId,
                    blockade.StartTick,
                    tick);
            }
            else
            {
                blockadeOrigins.Remove(targetUnitId);
            }

            targetBlockades.Remove(targetUnitId);
            targetBlockadeReleasedCount++;
            string targetSnapshot = targetUnit == null
                ? "target=unavailable"
                : Describe(targetUnit, Capture(targetUnitId, targetUnit));
            LogInfo(
                $"OX_IDLE_TARGET_BLOCKADE_RELEASED: tick={tick}, startTick={blockade.StartTick}, " +
                $"heldTicks={tick - blockade.StartTick}, reason={reason}, targetUnitId={targetUnitId}, " +
                $"targetGlobalId={blockade.TargetGlobalId}, blockerUnitId={blockade.BlockerUnitId}, " +
                $"blockerGlobalId={blockade.BlockerGlobalId}, blockedTarget={blockade.TargetX}/{blockade.TargetY}, " +
                $"targetTileId={blockade.TargetTileId}, tileUnitIdBefore={tileUnitIdBefore}, " +
                $"blockerPhysicallyAtTarget={blockerPhysicallyAtTarget}, blockerTeleported={blockade.BlockerTeleported}, " +
                $"blockerCommanded={blockade.BlockerCommanded}, " +
                $"lastProgressTick={blockade.LastProgressTick}, noProgressTicks={tick - blockade.LastProgressTick}, " +
                $"blockerNoProgressTimeoutTicks={BlockerNoProgressTimeoutTicks}, " +
                $"occupancyConfirmed={blockade.OccupancyConfirmed}, occupancyConfirmedTick={blockade.OccupancyConfirmedTick}, " +
                $"blockerRestored={blockerRestored}, blockerRestoreDisposition={blockerRestoreDisposition}, " +
                $"blockerApproach={blockade.BlockerApproachX}/{blockade.BlockerApproachY}, " +
                $"blockerApproachTileId={blockade.BlockerApproachTileId}, directTileMutation=false, directTargetOxMutation=false, " +
                $"targetSnapshot=({targetSnapshot}).");
        }

        private static bool TryRestoreTeleportedBlocker(
            int blockerUnitId,
            in BlockerSnapshot before,
            string reason) =>
            TryRestoreTeleportedBlocker(blockerUnitId, before, reason, out _);

        private static bool TryRestoreTeleportedBlocker(
            int blockerUnitId,
            in BlockerSnapshot before,
            string reason,
            out string disposition)
        {
            disposition = "identityUnavailableOrReused";
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(blockerUnitId, out GameUnit* blocker) ||
                blocker == null || blocker->r_AliveState != AliveState.IsAlive ||
                blocker->r_GlobalId != before.GlobalId)
            {
                return false;
            }

            ushort stateBeforeRestore = blocker->r_AIState;
            GameUnitManagerAPI.Instance.SetCurrentLocalTilePosition(
                blockerUnitId,
                new UnmanagedVector2<ushort>(before.CurrentX, before.CurrentY));
            if (OxTargetBlockadePolicy.ShouldReissueOriginalBlockerRoute(
                    before.State,
                    stateBeforeRestore,
                    before.CurrentX,
                    before.CurrentY,
                    before.RequestedX,
                    before.RequestedY))
            {
                GameUnitManagerAPI.Instance.MoveToTile(
                    blockerUnitId,
                    before.RequestedX,
                    before.RequestedY,
                    0);
                disposition = $"positionRestoredAndRouteReissued:{reason}";
            }
            else
            {
                disposition = stateBeforeRestore == before.State
                    ? $"stationaryPositionRestored:{reason}"
                    : $"positionRestoredWithoutRouteBecauseStateChanged:{before.State}->{stateBeforeRestore}:{reason}";
            }

            return blocker->r_CurrentTilePositionX == before.CurrentX &&
                blocker->r_CurrentTilePositionY == before.CurrentY;
        }

        private static bool TryResumeRegisteredBlocker(
            int blockerUnitId,
            in BlockerSnapshot before,
            string reason,
            out string disposition)
        {
            disposition = "identityUnavailableOrReused";
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(blockerUnitId, out GameUnit* blocker) ||
                blocker == null || blocker->r_AliveState != AliveState.IsAlive ||
                blocker->r_GlobalId != before.GlobalId)
            {
                return false;
            }

            if (blocker->r_AIState != before.State)
            {
                disposition = $"registeredBlockerStateChanged:{before.State}->{blocker->r_AIState}:{reason}";
                return false;
            }

            // Once Vanilla registered the blocker on the target, it must also move the
            // blocker away so the native occupancy grid is cleared consistently.
            GameUnitManagerAPI.Instance.MoveToTile(
                blockerUnitId,
                before.RequestedX,
                before.RequestedY,
                0);
            bool routeAccepted =
                (blocker->r_CurrentTilePositionX == before.RequestedX &&
                 blocker->r_CurrentTilePositionY == before.RequestedY) ||
                (blocker->r_TargetTilePositionX2 == before.RequestedX &&
                 blocker->r_TargetTilePositionY2 == before.RequestedY &&
                 blocker->r_PathPlanStateBitFlags != 0);
            disposition = routeAccepted
                ? $"registeredBlockerOriginalRouteReissued:{reason}"
                : $"registeredBlockerOriginalRouteRejected:{reason}";
            return routeAccepted;
        }

        private void UpdateGeneralStall(int tick, GameUnit* unit, in OxObservation observation)
        {
            if (observation.State != 1 && observation.State != 3)
            {
                EndGeneralStallIfPresent(tick, unit, observation, "leftTravelState");
                return;
            }

            if (!generalStalls.TryGetValue(observation.UnitId, out GeneralStallTrace trace) ||
                tick != trace.LastTick + 1 ||
                !trace.InitialObservation.IsSameGeneralStallAs(observation))
            {
                if (trace != null && trace.Confirmed)
                    LogGeneralStallRecovered(tick, trace, unit, observation, "snapshotChanged");
                generalStalls[observation.UnitId] = new GeneralStallTrace(
                    observation,
                    tick,
                    Describe(unit, observation));
                return;
            }

            trace.LastTick = tick;
            trace.ConsecutiveTicks++;
            if (!trace.Confirmed && trace.ConsecutiveTicks >= GeneralStallTicks)
            {
                trace.Confirmed = true;
                trace.LastReportTick = tick;
                generalStallConfirmedCount++;
                LogInfo(
                    $"OX_IDLE_GENERAL_STALL_CONFIRMED: tick={tick}, source={GetEpisodeSource(observation)}, " +
                    $"stationaryTicks={trace.ConsecutiveTicks}, startedWith=({trace.StartDescription}), " +
                    $"current=({Describe(unit, observation)}).");
            }
            else if (trace.Confirmed && tick - trace.LastReportTick >= GeneralStallRepeatTicks)
            {
                trace.LastReportTick = tick;
                LogInfo(
                    $"OX_IDLE_GENERAL_STALL_PERSISTS: tick={tick}, source={GetEpisodeSource(observation)}, " +
                    $"stationaryTicks={trace.ConsecutiveTicks}, current=({Describe(unit, observation)}).");
            }
        }

        private void EndGeneralStallIfPresent(
            int tick,
            GameUnit* unit,
            in OxObservation observation,
            string reason)
        {
            if (!generalStalls.TryGetValue(observation.UnitId, out GeneralStallTrace trace))
                return;
            if (trace.Confirmed)
                LogGeneralStallRecovered(tick, trace, unit, observation, reason);
            generalStalls.Remove(observation.UnitId);
        }

        private void LogGeneralStallRecovered(
            int tick,
            GeneralStallTrace trace,
            GameUnit* unit,
            in OxObservation observation,
            string reason) =>
            LogInfo(
                $"OX_IDLE_GENERAL_STALL_RECOVERED: tick={tick}, reason={reason}, " +
                $"stationaryTicks={trace.ConsecutiveTicks}, startedWith=({trace.StartDescription}), " +
                $"endedWith=({Describe(unit, observation)}).");

        private void EndCandidateTraceIfPresent(
            int tick,
            in OxObservation current,
            string outcome,
            GameUnit* currentUnit)
        {
            if (!candidateTraces.TryGetValue(current.UnitId, out CandidateTrace trace))
                return;

            EndCandidateTrace(tick, trace, outcome, currentUnit, current);
            candidateTraces.Remove(current.UnitId);
            blockadeOrigins.Remove(current.UnitId);
        }

        private void EndCandidateTrace(
            int tick,
            CandidateTrace trace,
            string outcome,
            GameUnit* currentUnit,
            OxObservation? current = null)
        {
            candidateRecoveredCount++;
            long elapsedMilliseconds = StopwatchTicksToMilliseconds(
                Stopwatch.GetTimestamp() - trace.StartTimestamp);
            string currentDescription = current.HasValue && currentUnit != null
                ? Describe(currentUnit, current.Value)
                : "current=unavailable";
            LogInfo(
                $"OX_IDLE_CANDIDATE_RECOVERED: tick={tick}, source={trace.Source}, outcome={outcome}, " +
                $"candidateTicks={trace.ConsecutiveTicks}, elapsedMs={elapsedMilliseconds}, " +
                $"startedWith=({trace.StartDescription}), endedWith=({currentDescription}).");
        }

        private void RecordUnitTransition(int tick, GameUnit* unit, in OxObservation observation)
        {
            if (!lastObservations.TryGetValue(observation.UnitId, out OxObservation previous) ||
                previous.GlobalId != observation.GlobalId)
            {
                LogInfo($"OX_IDLE_UNIT_DISCOVERED: tick={tick}, {Describe(unit, observation)}.");
                lastObservations[observation.UnitId] = observation;
                return;
            }

            if (observation.HasDiagnosticTransitionFrom(previous))
            {
                LogInfo(
                    $"OX_IDLE_UNIT_TRANSITION: tick={tick}, unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
                    $"state={previous.State}->{observation.State}, pathFlags={previous.PathFlags}->{observation.PathFlags}, " +
                    $"marker={previous.AlternateTargetMarker}->{observation.AlternateTargetMarker}, " +
                    $"position={previous.CurrentX}/{previous.CurrentY}->{observation.CurrentX}/{observation.CurrentY}, " +
                    $"primaryTarget={previous.PrimaryX}/{previous.PrimaryY}->{observation.PrimaryX}/{observation.PrimaryY}, " +
                    $"next={previous.NextX}/{previous.NextY}->{observation.NextX}/{observation.NextY}, " +
                    $"requested={previous.RequestedX}/{previous.RequestedY}->{observation.RequestedX}/{observation.RequestedY}, " +
                    $"pathCursor={previous.PathCursor}->{observation.PathCursor}, pathSize={previous.PathSize}->{observation.PathSize}, " +
                    $"movingRelevant={previous.MovingRelevant}->{observation.MovingRelevant}, " +
                    $"pathRelated1={previous.PathRelated1}->{observation.PathRelated1}, " +
                    $"animationTimer={previous.AnimationTimer}->{observation.AnimationTimer}, " +
                    $"carryGoods={previous.CarryGoods}->{observation.CarryGoods}, " +
                    $"workerTargetGlobalId={previous.WorkerTargetGlobalId}->{observation.WorkerTargetGlobalId}, " +
                    $"linkedBuildingId={previous.LinkedBuildingId}->{observation.LinkedBuildingId}.");
            }

            lastObservations[observation.UnitId] = observation;
        }

        private void ConfirmAndRepair(int tick, GameUnit* unit, in OxObservation observation)
        {
            confirmedCount++;
            LogInfo(
                $"OX_IDLE_BUG_CONFIRMED: tick={tick}, {Describe(unit, observation)}, " +
                $"source={GetEpisodeSource(observation)}, " +
                $"consecutiveTicks={OxIdleEpisodePolicy.RequiredConsecutiveTicks}.");

            ushort markerBefore = unit->r_PathPlanRelated3;
            unit->r_PathPlanRelated3 = 0;
            LogInfo(
                $"OX_IDLE_FIX_APPLIED: tick={tick}, unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
                $"state={observation.State}, markerBefore={markerBefore}, markerAfter={unit->r_PathPlanRelated3}, " +
                $"expectedNextState={observation.ExpectedStateAfterRepair}, " +
                $"source={GetEpisodeSource(observation)}, changedField=r_PathPlanRelated3.");
        }

        private void RecordVerified(int tick, in OxObservation observation)
        {
            verifiedCount++;
            string source = GetEpisodeSource(observation);
            LogInfo(
                $"OX_IDLE_FIX_VERIFIED: tick={tick}, unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
                $"actualState={observation.State}, position={observation.CurrentX}/{observation.CurrentY}, " +
                $"requested={observation.RequestedX}/{observation.RequestedY}, source={source}.");
            blockadeOrigins.Remove(observation.UnitId);
        }

        private void RecordUnverified(int tick, in OxObservation observation)
        {
            unverifiedCount++;
            string source = GetEpisodeSource(observation);
            Shared.DebugLogHelper.LogWarning(
                log,
                $"OX_IDLE_FIX_UNVERIFIED: tick={tick}, unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
                $"actualState={observation.State}, pathFlags={observation.PathFlags}, " +
                $"marker={observation.AlternateTargetMarker}, position={observation.CurrentX}/{observation.CurrentY}, " +
                $"requested={observation.RequestedX}/{observation.RequestedY}, source={source}.");
            blockadeOrigins.Remove(observation.UnitId);
        }

        private static OxObservation Capture(int unitId, GameUnit* unit) =>
            new OxObservation(
                unitId,
                unit->r_GlobalId,
                unit->r_AIState,
                unit->r_PathPlanStateBitFlags,
                unit->r_PathPlanRelated3,
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY,
                unit->r_TargetTilePositionX2,
                unit->r_TargetTilePositionY2,
                unit->p_CurrentPathPlanPosition,
                unit->p_PathPlanSize,
                unit->r_MovingRelevant,
                unit->r_PathPlanRelated1,
                unit->r_TargetTilePositionX,
                unit->r_TargetTilePositionY,
                unit->r_NextTilePositionX2,
                unit->r_NextTilePositionY2,
                unit->r_AnimationTimer,
                unit->r_CarryOverGoodsAmount,
                unit->r_WorkerTargetContextEntityGlobalId,
                unit->r_LinkedProductionBuildingId);

        private string GetEpisodeSource(in OxObservation observation) =>
            (targetBlockades.TryGetValue(observation.UnitId, out TargetBlockade blockade) &&
             blockade.TargetGlobalId == observation.GlobalId) ||
            (blockadeOrigins.TryGetValue(observation.UnitId, out BlockadeOrigin origin) &&
             origin.TargetGlobalId == observation.GlobalId)
                ? "targetBlockade"
                : "natural";

        private static string Describe(GameUnit* unit, in OxObservation observation) =>
            $"unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
            $"playerIndex={unit->r_SpawnedForPlayerIndex}, state={observation.State}, " +
            $"world={unit->r_CurrentWorldPositionX}/{unit->r_CurrentWorldPositionY}, " +
            $"position={observation.CurrentX}/{observation.CurrentY}, " +
            $"primaryTarget={unit->r_TargetTilePositionX}/{unit->r_TargetTilePositionY}, " +
            $"next={unit->r_NextTilePositionX2}/{unit->r_NextTilePositionY2}, " +
            $"requested={observation.RequestedX}/{observation.RequestedY}, " +
            $"pathFlags={observation.PathFlags}, pathRelated1={unit->r_PathPlanRelated1}, " +
            $"pathCursor={unit->p_CurrentPathPlanPosition}, pathSize={unit->p_PathPlanSize}, " +
            $"movingRelevant={unit->r_MovingRelevant}, " +
            $"alternateTargetMarker={observation.AlternateTargetMarker}, " +
            $"animationTimer={unit->r_AnimationTimer}, carryGoods={unit->r_CarryOverGoodsAmount}, " +
            $"workerTargetGlobalId={unit->r_WorkerTargetContextEntityGlobalId}, " +
            $"linkedBuildingId={unit->r_LinkedProductionBuildingId}";

        private static string FormatStateCounts(Dictionary<ushort, int> stateCounts)
        {
            if (stateCounts == null || stateCounts.Count == 0)
                return "none";

            List<ushort> states = new List<ushort>(stateCounts.Keys);
            states.Sort();
            StringBuilder builder = new StringBuilder();
            foreach (ushort state in states)
            {
                if (builder.Length > 0)
                    builder.Append(',');
                builder.Append(state).Append(':').Append(stateCounts[state]);
            }
            return builder.ToString();
        }

        private static long SecondsToStopwatchTicks(int seconds) =>
            checked((long)Math.Ceiling(seconds * (double)Stopwatch.Frequency));

        private static long AddStopwatchTicks(long timestamp, long delta) =>
            timestamp > long.MaxValue - delta ? long.MaxValue : timestamp + delta;

        private static long StopwatchTicksToMilliseconds(long ticks) =>
            ticks <= 0 ? 0 : (long)Math.Round(ticks * 1000d / Stopwatch.Frequency);

        private static void ValidateGameUnitLayout()
        {
            if (Marshal.SizeOf(typeof(GameUnit)) != 0x490 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_CurrentTilePositionX)).ToInt32() != 0xC0 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TargetTilePositionX2)).ToInt32() != 0xE8 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_PathPlanStateBitFlags)).ToInt32() != 0xF2 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_PathPlanRelated3)).ToInt32() != 0x290 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AIState)).ToInt32() != 0x2BC ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_LinkedProductionBuildingId)).ToInt32() != 0x334)
            {
                throw new InvalidOperationException(
                    "GameUnit layout differs from the audited Script Extender 1.42.0 contract.");
            }
        }

        private void ReleaseAllTargetBlockades(string reason)
        {
            staleUnitIds.Clear();
            foreach (KeyValuePair<int, TargetBlockade> pair in targetBlockades)
                staleUnitIds.Add(pair.Key);
            foreach (int targetUnitId in staleUnitIds)
            {
                int cleanupTick = targetBlockades.TryGetValue(targetUnitId, out TargetBlockade blockade)
                    ? blockade.StartTick
                    : 0;
                ReleaseTargetBlockade(targetUnitId, cleanupTick, reason, null);
            }
            staleUnitIds.Clear();
        }

        private void ClearEpisodes(bool releaseBlockades)
        {
            if (releaseBlockades)
                ReleaseAllTargetBlockades("runtimeDisposed");
            foreach (OxIdleEpisodePolicy episode in episodes.Values)
                episode.Cancel();
            episodes.Clear();
            candidateTraces.Clear();
            targetBlockades.Clear();
            blockadeOrigins.Clear();
            generalStalls.Clear();
            lastObservations.Clear();
            observedUnitIds.Clear();
            staleUnitIds.Clear();
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);

        private struct CandidateTrace
        {
            internal CandidateTrace(
                OxObservation observation,
                int tick,
                long startTimestamp,
                string source,
                string startDescription)
            {
                LastObservation = observation;
                LastTick = tick;
                ConsecutiveTicks = 1;
                StartTimestamp = startTimestamp;
                Source = source;
                StartDescription = startDescription;
            }

            internal OxObservation LastObservation;
            internal int LastTick;
            internal int ConsecutiveTicks;
            internal long StartTimestamp;
            internal string Source;
            internal string StartDescription;
        }

        private sealed class TargetBlockade
        {
            internal TargetBlockade(
                uint targetGlobalId,
                ushort initialState,
                ushort targetX,
                ushort targetY,
                int targetTileId,
                int blockerUnitId,
                uint blockerGlobalId,
                bool blockerCommanded,
                bool blockerTeleported,
                bool occupancyConfirmed,
                ushort blockerApproachX,
                ushort blockerApproachY,
                int blockerApproachTileId,
                BlockerSnapshot blockerBefore,
                BlockerSnapshot blockerAfterCommand,
                int startTick)
            {
                TargetGlobalId = targetGlobalId;
                InitialState = initialState;
                TargetX = targetX;
                TargetY = targetY;
                TargetTileId = targetTileId;
                BlockerUnitId = checked((ushort)blockerUnitId);
                BlockerGlobalId = blockerGlobalId;
                BlockerCommanded = blockerCommanded;
                BlockerTeleported = blockerTeleported;
                OccupancyConfirmed = occupancyConfirmed;
                OccupancyConfirmedTick = occupancyConfirmed ? startTick : 0;
                BlockerApproachX = blockerApproachX;
                BlockerApproachY = blockerApproachY;
                BlockerApproachTileId = blockerApproachTileId;
                BlockerBefore = blockerBefore;
                StartTick = startTick;
                LastProgressTick = startTick;
                LastBlockerX = blockerAfterCommand.CurrentX;
                LastBlockerY = blockerAfterCommand.CurrentY;
                LastBlockerPathCursor = blockerAfterCommand.PathCursor;
            }

            internal uint TargetGlobalId { get; }
            internal ushort InitialState { get; }
            internal ushort TargetX { get; }
            internal ushort TargetY { get; }
            internal int TargetTileId { get; }
            internal ushort BlockerUnitId { get; }
            internal uint BlockerGlobalId { get; }
            internal bool BlockerCommanded { get; }
            internal bool BlockerTeleported { get; }
            internal bool OccupancyConfirmed { get; set; }
            internal int OccupancyConfirmedTick { get; set; }
            internal ushort BlockerApproachX { get; }
            internal ushort BlockerApproachY { get; }
            internal int BlockerApproachTileId { get; }
            internal BlockerSnapshot BlockerBefore { get; }
            internal int StartTick { get; }
            internal int LastProgressTick { get; set; }
            internal ushort LastBlockerX { get; set; }
            internal ushort LastBlockerY { get; set; }
            internal ushort LastBlockerPathCursor { get; set; }
        }

        private readonly struct BlockerSnapshot
        {
            private BlockerSnapshot(
                uint globalId,
                eChimps unitType,
                ushort state,
                ushort currentX,
                ushort currentY,
                ushort requestedX,
                ushort requestedY,
                ushort pathFlags,
                ushort pathMarker,
                ushort pathCursor,
                uint pathSize)
            {
                GlobalId = globalId;
                UnitType = unitType;
                State = state;
                CurrentX = currentX;
                CurrentY = currentY;
                RequestedX = requestedX;
                RequestedY = requestedY;
                PathFlags = pathFlags;
                PathMarker = pathMarker;
                PathCursor = pathCursor;
                PathSize = pathSize;
            }

            internal uint GlobalId { get; }
            internal eChimps UnitType { get; }
            internal ushort State { get; }
            internal ushort CurrentX { get; }
            internal ushort CurrentY { get; }
            internal ushort RequestedX { get; }
            internal ushort RequestedY { get; }
            internal ushort PathFlags { get; }
            internal ushort PathMarker { get; }
            internal ushort PathCursor { get; }
            internal uint PathSize { get; }

            internal static BlockerSnapshot Capture(GameUnit* unit) =>
                new BlockerSnapshot(
                    unit->r_GlobalId,
                    unit->r_UnitChimp,
                    unit->r_AIState,
                    unit->r_CurrentTilePositionX,
                    unit->r_CurrentTilePositionY,
                    unit->r_TargetTilePositionX2,
                    unit->r_TargetTilePositionY2,
                    unit->r_PathPlanStateBitFlags,
                    unit->r_PathPlanRelated3,
                    unit->p_CurrentPathPlanPosition,
                    unit->p_PathPlanSize);

            public override string ToString() =>
                $"globalId={GlobalId}, unitType={UnitType}, state={State}, " +
                $"position={CurrentX}/{CurrentY}, requested={RequestedX}/{RequestedY}, " +
                $"pathFlags={PathFlags}, marker={PathMarker}, pathCursor={PathCursor}, pathSize={PathSize}";
        }

        private readonly struct BlockadeOrigin
        {
            internal BlockadeOrigin(uint targetGlobalId, int startTick, int releaseTick)
            {
                TargetGlobalId = targetGlobalId;
                StartTick = startTick;
                ReleaseTick = releaseTick;
            }

            internal uint TargetGlobalId { get; }
            internal int StartTick { get; }
            internal int ReleaseTick { get; }
        }

        private sealed class GeneralStallTrace
        {
            internal GeneralStallTrace(
                OxObservation initialObservation,
                int tick,
                string startDescription)
            {
                InitialObservation = initialObservation;
                LastTick = tick;
                ConsecutiveTicks = 1;
                StartDescription = startDescription;
            }

            internal OxObservation InitialObservation { get; }
            internal int LastTick { get; set; }
            internal int ConsecutiveTicks { get; set; }
            internal int LastReportTick { get; set; }
            internal bool Confirmed { get; set; }
            internal string StartDescription { get; }
        }
    }
}
