using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
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

        private bool applied;
        private bool mapActive;
        private bool disabledForMap;
        private long confirmedCount;
        private long verifiedCount;
        private long unverifiedCount;

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
                $"verificationTicks={OxIdleEpisodePolicy.VerificationTicks}, unitIdsAreOneBased=true.");
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
            LogInfo($"OX_IDLE_MAP_TRACKING_STARTED: reason={reason}.");
        }

        private void EndMap()
        {
            LogInfo(
                $"OX_IDLE_MAP_SUMMARY: confirmed={confirmedCount}, verified={verifiedCount}, " +
                $"unverified={unverifiedCount}, trackedEpisodes={episodes.Count}, disabled={disabledForMap}.");
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
                    observedUnitIds.Add(unitId);
                    OxObservation observation = Capture(unitId, unit);
                    if (!episodes.TryGetValue(unitId, out OxIdleEpisodePolicy episode))
                    {
                        if (!observation.HasIdleBugSignature)
                            continue;

                        episode = new OxIdleEpisodePolicy();
                        episodes.Add(unitId, episode);
                    }

                    OxEpisodeAction action = episode.Observe(observation, tick);
                    if (action == OxEpisodeAction.ConfirmAndRepair)
                        ConfirmAndRepair(tick, unit, observation);
                    else if (action == OxEpisodeAction.Verified)
                        RecordVerified(tick, observation);
                    else if (action == OxEpisodeAction.Unverified)
                        RecordUnverified(tick, observation);

                    if (!episode.IsActive)
                        episodes.Remove(unitId);
                }
            }

            staleUnitIds.Clear();
            foreach (KeyValuePair<int, OxIdleEpisodePolicy> pair in episodes)
            {
                if (!observedUnitIds.Contains(pair.Key))
                    staleUnitIds.Add(pair.Key);
            }
            foreach (int staleUnitId in staleUnitIds)
                episodes.Remove(staleUnitId);
        }

        private void ConfirmAndRepair(int tick, GameUnit* unit, in OxObservation observation)
        {
            confirmedCount++;
            LogInfo(
                $"OX_IDLE_BUG_CONFIRMED: tick={tick}, {Describe(unit, observation)}, " +
                $"consecutiveTicks={OxIdleEpisodePolicy.RequiredConsecutiveTicks}.");

            ushort markerBefore = unit->r_PathPlanRelated3;
            unit->r_PathPlanRelated3 = 0;
            LogInfo(
                $"OX_IDLE_FIX_APPLIED: tick={tick}, unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
                $"state={observation.State}, markerBefore={markerBefore}, markerAfter={unit->r_PathPlanRelated3}, " +
                $"expectedNextState={observation.ExpectedStateAfterRepair}, changedField=r_PathPlanRelated3.");
        }

        private void RecordVerified(int tick, in OxObservation observation)
        {
            verifiedCount++;
            LogInfo(
                $"OX_IDLE_FIX_VERIFIED: tick={tick}, unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
                $"actualState={observation.State}, position={observation.CurrentX}/{observation.CurrentY}, " +
                $"requested={observation.RequestedX}/{observation.RequestedY}.");
        }

        private void RecordUnverified(int tick, in OxObservation observation)
        {
            unverifiedCount++;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"OX_IDLE_FIX_UNVERIFIED: tick={tick}, unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
                $"actualState={observation.State}, pathFlags={observation.PathFlags}, " +
                $"marker={observation.AlternateTargetMarker}, position={observation.CurrentX}/{observation.CurrentY}, " +
                $"requested={observation.RequestedX}/{observation.RequestedY}.");
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
                unit->r_TargetTilePositionY2);

        private static string Describe(GameUnit* unit, in OxObservation observation) =>
            $"unitId={observation.UnitId}, globalId={observation.GlobalId}, " +
            $"playerIndex={unit->r_SpawnedForPlayerIndex}, state={observation.State}, " +
            $"position={observation.CurrentX}/{observation.CurrentY}, " +
            $"requested={observation.RequestedX}/{observation.RequestedY}, " +
            $"pathFlags={observation.PathFlags}, alternateTargetMarker={observation.AlternateTargetMarker}, " +
            $"linkedBuildingId={unit->r_LinkedProductionBuildingId}";

        private void ClearEpisodes()
        {
            foreach (OxIdleEpisodePolicy episode in episodes.Values)
                episode.Cancel();
            episodes.Clear();
            observedUnitIds.Clear();
            staleUnitIds.Clear();
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
    }
}
