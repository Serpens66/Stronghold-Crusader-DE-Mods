using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace BuildingLimit
{
    internal sealed class TowerSiegeUnitCache : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly HashSet<int> trackedUnitIds = new HashSet<int>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly ManualLogSource log;
        private bool subscribed;

        public TowerSiegeUnitCache(ManualLogSource log)
        {
            this.log = log;
        }

        public void SubscribeHooks()
        {
            if (subscribed)
                return;

            try
            {
                subscriptions.Add(Shared.GameplaySessionLifecycle.SubscribeStarted(log, _ => ResyncAll()));
                subscriptions.Add(Shared.MissionEvents.Ended.Subscribe(_ => Clear()));
                subscriptions.Add(UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(OnUnitCreate));
                subscriptions.Add(UnitR3EventHooks.OnUnitDelete.Observable.Subscribe(OnUnitDelete));
                subscriptions.Add(UnitR3EventHooks.OnUnitTransition.Observable.Subscribe(OnUnitTransition));
                subscribed = true;
            }
            catch
            {
                Dispose(); // Only a partially initialized, unpublished cache may be rolled back.
                throw;
            }
        }

        public void ResyncAll()
        {
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            int trackedCount;
            lock (syncRoot)
            {
                trackedUnitIds.Clear();
                for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
                {
                    ref GameUnit unit = ref units[spanIndex];
                    if (IsActive(unit.r_AliveState) && IsTowerSiegeUnit(unit.r_UnitChimp))
                        trackedUnitIds.Add(spanIndex + 1);
                }
                trackedCount = trackedUnitIds.Count;
            }

            Shared.DebugLogHelper.LogDebug(log, "TowerSiegeUnitCache resynced:", trackedCount);
        }

        public unsafe int GetAliveCount(int playerId, eChimps unitType)
        {
            int count = 0;
            lock (syncRoot)
            {
                foreach (int unitId in trackedUnitIds)
                {
                    if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                        !IsActive(unit->r_AliveState))
                        continue;

                    if (unit->r_UnitChimp == unitType && unit->r_ControllableForPlayerId == playerId)
                        count++;
                }
            }

            return count;
        }

        public unsafe bool TryReadActiveTower(int unitId, out int playerId, out eChimps unitType)
        {
            playerId = 0;
            unitType = eChimps.CHIMP_TYPE_NULL;
            if (unitId <= 0 ||
                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                !IsActive(unit->r_AliveState) || !IsTowerSiegeUnit(unit->r_UnitChimp))
                return false;

            playerId = unit->r_ControllableForPlayerId;
            unitType = unit->r_UnitChimp;
            return true;
        }

        public void Clear()
        {
            lock (syncRoot)
                trackedUnitIds.Clear();
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
            {
                try { subscription.Dispose(); }
                catch (Exception ex) { Shared.DebugLogHelper.LogDebug(log, "TowerSiegeUnitCache initialization rollback failed:", ex); }
            }
            subscriptions.Clear();
            subscribed = false;
            Clear();
        }

        private void OnUnitCreate(UnitCreateEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post || args.ReturnValue <= 0 ||
                args.ReturnValue > int.MaxValue ||
                (!IsTowerSiegeUnit(args.UnitType) && !TryReadTowerType((int)args.ReturnValue)))
                return;

            lock (syncRoot)
                trackedUnitIds.Add((int)args.ReturnValue);
        }

        private void OnUnitDelete(UnitDeleteEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post || args.UnitId == 0 || args.UnitId > int.MaxValue ||
                TryReadActiveTower((int)args.UnitId, out _, out _))
                return;

            lock (syncRoot)
                trackedUnitIds.Remove((int)args.UnitId);
        }

        private void OnUnitTransition(UnitTransitionEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre || args.UnitId <= 0)
                return;

            if (IsTowerSiegeUnit(args.NextUnitType))
                lock (syncRoot)
                    trackedUnitIds.Add(args.UnitId);
        }

        private unsafe bool TryReadTowerType(int unitId) =>
            APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) &&
            IsTowerSiegeUnit(unit->r_UnitChimp);

        private static bool IsTowerSiegeUnit(eChimps type) =>
            type == eChimps.CHIMP_TYPE_MANGONEL || type == eChimps.CHIMP_TYPE_BALLISTA;

        private static bool IsActive(AliveState state) =>
            state == AliveState.IsAlive || state == AliveState.NeedsInit;
    }
}
