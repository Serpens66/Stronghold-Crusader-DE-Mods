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
        private readonly Dictionary<int, InjectedFault> injectedFaults =
            new Dictionary<int, InjectedFault>();

        internal const int FaultInjectionIntervalSeconds = 30;
        internal const int FleetSnapshotIntervalSeconds = 10;
        internal const int FaultInjectionTerminalizationTimeoutTicks = 250;
        private static readonly long FaultInjectionIntervalStopwatchTicks =
            SecondsToStopwatchTicks(FaultInjectionIntervalSeconds);
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
        private long injectedCount;
        private long nextInjectionTimestamp;
        private long nextDeferredInjectionLogTimestamp;
        private long nextFleetSnapshotTimestamp;
        private int lastInjectedUnitId;

        public OxTetherIdleFixTestRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Apply()
        {
            if (applied)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => BeginMap($"new map campaignMapId={args.CampaignMapId}")));
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => BeginMap($"loaded save file={args.FileName ?? "<null>"}")));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => EndMap()));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;

            applied = true;
            LogInfo(
                $"OX_IDLE_DIAGNOSTIC_READY: correctionActive=true, scanEverySimulationTick=true, " +
                $"requiredConsecutiveTicks={OxIdleEpisodePolicy.RequiredConsecutiveTicks}, " +
                $"verificationTicks={OxIdleEpisodePolicy.VerificationTicks}, unitIdsAreOneBased=true, " +
                $"faultInjectionActive=true, faultInjectionIntervalSeconds={FaultInjectionIntervalSeconds}, " +
                $"fleetSnapshotIntervalSeconds={FleetSnapshotIntervalSeconds}, faultInjectionLimit=none.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            ClearEpisodes();
            mapActive = false;
            applied = false;
        }

        private void BeginMap(string reason)
        {
            ClearEpisodes();
            mapActive = true;
            disabledForMap = false;
            confirmedCount = 0;
            verifiedCount = 0;
            unverifiedCount = 0;
            candidateStartedCount = 0;
            candidateRecoveredCount = 0;
            injectedCount = 0;
            lastInjectedUnitId = 0;
            long now = Stopwatch.GetTimestamp();
            nextInjectionTimestamp = AddStopwatchTicks(now, FaultInjectionIntervalStopwatchTicks);
            nextDeferredInjectionLogTimestamp = nextInjectionTimestamp;
            nextFleetSnapshotTimestamp = now;
            LogInfo($"OX_IDLE_MAP_TRACKING_STARTED: reason={reason}.");
        }

        private void EndMap()
        {
            LogInfo(
                $"OX_IDLE_MAP_SUMMARY: confirmed={confirmedCount}, verified={verifiedCount}, " +
                $"unverified={unverifiedCount}, candidatesStarted={candidateStartedCount}, " +
                $"candidatesRecovered={candidateRecoveredCount}, faultsInjected={injectedCount}, " +
                $"trackedEpisodes={episodes.Count}, activeCandidateTraces={candidateTraces.Count}, " +
                $"activeInjectedFaults={injectedFaults.Count}, disabled={disabledForMap}.");
            mapActive = false;
            disabledForMap = false;
            ClearEpisodes();
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
                ClearEpisodes();
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
                TryInjectFault(units, tick, now);

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
                    observation = MaintainInjectedFault(tick, unit, observation);
                    RecordUnitTransition(tick, unit, observation);
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
                    $"activeInjectedFaults={injectedFaults.Count}.");
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
                injectedFaults.Remove(staleUnitId);
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
                injectedFaults.Remove(staleUnitId);
            }
        }

        private void TryInjectFault(SimpleNativeArray<GameUnit> units, int tick, long now)
        {
            int selectedUnitId = 0;
            GameUnit* selectedUnit = null;
            OxObservation selectedObservation = default;

            for (int pass = 0; pass < 2 && selectedUnit == null; pass++)
            {
                for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
                {
                    int unitId = spanIndex + 1;
                    if ((pass == 0 && unitId <= lastInjectedUnitId) ||
                        (pass == 1 && unitId > lastInjectedUnitId))
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
                    if (!IsFaultInjectionEligible(observation, episodeActive))
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
                        $"OX_IDLE_FAULT_INJECTION_DEFERRED: tick={tick}, " +
                        "reason=no eligible moving state-1/state-3 quarry ox with a mismatched requested target.");
                    nextDeferredInjectionLogTimestamp = AddStopwatchTicks(
                        now,
                        DeferredInjectionLogIntervalStopwatchTicks);
                }
                return;
            }

            ushort markerBefore = selectedUnit->r_PathPlanRelated3;
            ushort pathCursorBefore = selectedUnit->p_CurrentPathPlanPosition;
            uint pathSize = selectedUnit->p_PathPlanSize;
            ushort injectedMarker = markerBefore != 0 ? markerBefore : (ushort)1;

            // Advance the existing Vanilla path to its terminal cursor. On the next
            // movement update Vanilla itself writes pathFlags=0 and movingRelevant=8.
            // The nonzero marker then preserves the mismatched requested destination.
            selectedUnit->p_CurrentPathPlanPosition = checked((ushort)pathSize);
            selectedUnit->r_PathPlanRelated3 = injectedMarker;

            OxObservation injectedObservation = Capture(selectedUnitId, selectedUnit);
            injectedFaults[selectedUnitId] = new InjectedFault(
                injectedObservation.GlobalId,
                tick,
                injectedMarker,
                injectedObservation.State);
            injectedCount++;
            lastInjectedUnitId = selectedUnitId;
            nextInjectionTimestamp = AddStopwatchTicks(now, FaultInjectionIntervalStopwatchTicks);
            nextDeferredInjectionLogTimestamp = nextInjectionTimestamp;

            LogInfo(
                $"OX_IDLE_FAULT_INJECTION_APPLIED: tick={tick}, sequence={injectedCount}, " +
                $"unitId={selectedUnitId}, globalId={selectedObservation.GlobalId}, state={selectedObservation.State}, " +
                $"position={selectedObservation.CurrentX}/{selectedObservation.CurrentY}, " +
                $"requested={selectedObservation.RequestedX}/{selectedObservation.RequestedY}, " +
                $"pathFlagsBefore={selectedObservation.PathFlags}, pathFlagsAfter={injectedObservation.PathFlags}, " +
                $"movingRelevantBefore={selectedObservation.MovingRelevant}, " +
                $"movingRelevantAfter={injectedObservation.MovingRelevant}, " +
                $"pathCursorBefore={pathCursorBefore}, pathCursorAfter={injectedObservation.PathCursor}, " +
                $"pathSize={pathSize}, " +
                $"markerBefore={markerBefore}, markerAfter={injectedObservation.AlternateTargetMarker}, " +
                "changedFields=p_CurrentPathPlanPosition+r_PathPlanRelated3, " +
                "vanillaExpectedNextUpdate=pathFlags:2->0+movingRelevant:8, " +
                "preservedFields=AIState+position+requestedTarget+pathFlags+goods+buildingLink, " +
                $"nextInjectionAfterSeconds={FaultInjectionIntervalSeconds}.");
        }

        internal static bool IsFaultInjectionEligible(in OxObservation observation, bool episodeActive) =>
            !episodeActive &&
            (observation.State == 1 || observation.State == 3) &&
            observation.PathFlags == 2 &&
            observation.PathSize > 0 &&
            observation.PathSize <= ushort.MaxValue &&
            observation.PathCursor < observation.PathSize &&
            (observation.CurrentX != observation.RequestedX || observation.CurrentY != observation.RequestedY);

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

        private OxObservation MaintainInjectedFault(
            int tick,
            GameUnit* unit,
            in OxObservation rawObservation)
        {
            if (!injectedFaults.TryGetValue(rawObservation.UnitId, out InjectedFault fault) ||
                fault.GlobalId != rawObservation.GlobalId)
            {
                return rawObservation;
            }

            if (!fault.Terminalized)
            {
                if (rawObservation.HasIdleBugSignature)
                {
                    fault.Terminalized = true;
                    fault.TerminalTick = tick;
                    LogInfo(
                        $"OX_IDLE_FAULT_INJECTION_TERMINALIZED: tick={tick}, " +
                        $"injectionTick={fault.InjectionTick}, ticksUntilTerminal={tick - fault.InjectionTick}, " +
                        $"{Describe(unit, rawObservation)}.");
                    return rawObservation;
                }

                if (tick - fault.InjectionTick < FaultInjectionTerminalizationTimeoutTicks)
                    return rawObservation;

                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"OX_IDLE_FAULT_INJECTION_FAILED: tick={tick}, injectionTick={fault.InjectionTick}, " +
                    $"reason=Vanilla did not produce the terminal alternate-target signature within " +
                    $"{FaultInjectionTerminalizationTimeoutTicks} ticks, {Describe(unit, rawObservation)}.");
                injectedFaults.Remove(rawObservation.UnitId);
                return rawObservation;
            }

            if (fault.RepairApplied && rawObservation.State == fault.ExpectedState)
                return rawObservation;
            if (rawObservation.State != fault.OriginalState)
                return rawObservation;

            ushort markerToHold = fault.RepairApplied ? (ushort)0 : fault.Marker;
            bool replanObserved = rawObservation.PathFlags != 0 ||
                rawObservation.AlternateTargetMarker != markerToHold ||
                rawObservation.MovingRelevant != 8;

            // Keep only the synthetic episode isolated from Vanilla's generic route
            // retry. Before repair the marker stays nonzero; afterwards it stays zero,
            // so the marker remains the sole arrival-decision difference.
            unit->r_PathPlanStateBitFlags = 0;
            unit->r_MovingRelevant = 8;
            unit->r_PathPlanRelated3 = markerToHold;
            if (unit->p_PathPlanSize <= ushort.MaxValue)
                unit->p_CurrentPathPlanPosition = checked((ushort)unit->p_PathPlanSize);

            OxObservation heldObservation = Capture(rawObservation.UnitId, unit);
            if (replanObserved)
            {
                fault.SuppressedReplans++;
                LogInfo(
                    $"OX_IDLE_FAULT_INJECTION_REPLAN_SUPPRESSED: tick={tick}, " +
                    $"injectionTick={fault.InjectionTick}, repairApplied={fault.RepairApplied}, " +
                    $"suppressionCount={fault.SuppressedReplans}, " +
                    $"rawPathFlags={rawObservation.PathFlags}, heldPathFlags={heldObservation.PathFlags}, " +
                    $"rawMarker={rawObservation.AlternateTargetMarker}, heldMarker={heldObservation.AlternateTargetMarker}, " +
                    $"rawMovingRelevant={rawObservation.MovingRelevant}, " +
                    $"heldMovingRelevant={heldObservation.MovingRelevant}.");
            }
            return heldObservation;
        }

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
            injectedFaults.Remove(current.UnitId);
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

            if (previous.State != observation.State ||
                previous.PathFlags != observation.PathFlags ||
                previous.AlternateTargetMarker != observation.AlternateTargetMarker ||
                previous.RequestedX != observation.RequestedX ||
                previous.RequestedY != observation.RequestedY)
            {
                LogInfo(
                    $"OX_IDLE_UNIT_TRANSITION: tick={tick}, unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
                    $"state={previous.State}->{observation.State}, pathFlags={previous.PathFlags}->{observation.PathFlags}, " +
                    $"marker={previous.AlternateTargetMarker}->{observation.AlternateTargetMarker}, " +
                    $"position={previous.CurrentX}/{previous.CurrentY}->{observation.CurrentX}/{observation.CurrentY}, " +
                    $"requested={previous.RequestedX}/{previous.RequestedY}->{observation.RequestedX}/{observation.RequestedY}.");
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
            if (injectedFaults.TryGetValue(observation.UnitId, out InjectedFault fault) &&
                fault.GlobalId == observation.GlobalId)
            {
                fault.RepairApplied = true;
                fault.RepairTick = tick;
                fault.ExpectedState = observation.ExpectedStateAfterRepair;
                LogInfo(
                    $"OX_IDLE_FAULT_INJECTION_HOLD_REPAIR_PHASE: tick={tick}, " +
                    $"injectionTick={fault.InjectionTick}, terminalTick={fault.TerminalTick}, " +
                    $"expectedState={fault.ExpectedState}, suppressedReplans={fault.SuppressedReplans}, " +
                    "heldMarkerBefore=nonzero, heldMarkerAfter=0.");
            }
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
            if (source == "faultInjection")
            {
                InjectedFault fault = injectedFaults[observation.UnitId];
                LogInfo(
                    $"OX_IDLE_FAULT_INJECTION_HOLD_RELEASED: tick={tick}, outcome=verified, " +
                    $"injectionTick={fault.InjectionTick}, terminalTick={fault.TerminalTick}, " +
                    $"repairTick={fault.RepairTick}, suppressedReplans={fault.SuppressedReplans}.");
                injectedFaults.Remove(observation.UnitId);
            }
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
            if (source == "faultInjection")
            {
                InjectedFault fault = injectedFaults[observation.UnitId];
                LogInfo(
                    $"OX_IDLE_FAULT_INJECTION_HOLD_RELEASED: tick={tick}, outcome=unverified, " +
                    $"injectionTick={fault.InjectionTick}, terminalTick={fault.TerminalTick}, " +
                    $"repairTick={fault.RepairTick}, suppressedReplans={fault.SuppressedReplans}.");
                injectedFaults.Remove(observation.UnitId);
            }
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
                unit->r_MovingRelevant);

        private string GetEpisodeSource(in OxObservation observation) =>
            injectedFaults.TryGetValue(observation.UnitId, out InjectedFault fault) &&
            fault.GlobalId == observation.GlobalId
                ? "faultInjection"
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

        private void ClearEpisodes()
        {
            foreach (OxIdleEpisodePolicy episode in episodes.Values)
                episode.Cancel();
            episodes.Clear();
            candidateTraces.Clear();
            injectedFaults.Clear();
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

        private sealed class InjectedFault
        {
            internal InjectedFault(
                uint globalId,
                int injectionTick,
                ushort marker,
                ushort originalState)
            {
                GlobalId = globalId;
                InjectionTick = injectionTick;
                Marker = marker;
                OriginalState = originalState;
            }

            internal uint GlobalId { get; }
            internal int InjectionTick { get; }
            internal ushort Marker { get; }
            internal ushort OriginalState { get; }
            internal bool Terminalized { get; set; }
            internal int TerminalTick { get; set; }
            internal bool RepairApplied { get; set; }
            internal int RepairTick { get; set; }
            internal ushort ExpectedState { get; set; }
            internal int SuppressedReplans { get; set; }
        }
    }
}
