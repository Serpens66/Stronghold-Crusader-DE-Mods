using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace UnitLimit
{
    internal sealed class ActiveSiegeTentCache : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly Dictionary<int, SiegeTentSnapshot> snapshotsById = new Dictionary<int, SiegeTentSnapshot>();
        private readonly Dictionary<SiegeTentCountKey, int> countsByOwnerAndUnit = new Dictionary<SiegeTentCountKey, int>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly ManualLogSource log;
        private bool subscribed;

        public event Action<ActiveSiegeTentChangedEventArgs> OnActiveSiegeTentChanged;

        public ActiveSiegeTentCache(ManualLogSource log = null)
        {
            this.log = log;
        }

        public void SubscribeHooks()
        {
            if (subscribed)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ResyncAll(true)));
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ResyncAll(true)));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => Clear()));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                .Subscribe(OnBuildingSpawn));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable
                .Subscribe(OnBuildingDelete));

            subscribed = true;
            LogDebug("ActiveSiegeTentCache hooks subscribed.");
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            subscribed = false;
            Clear();
        }

        public int GetActiveSiegeTentCount(int playerId, eChimps unitType)
        {
            lock (syncRoot)
            {
                return countsByOwnerAndUnit.TryGetValue(new SiegeTentCountKey(playerId, unitType), out int count)
                    ? count
                    : 0;
            }
        }

        public void ResyncAll(bool raiseEvents)
        {
            Dictionary<int, SiegeTentSnapshot> seenSnapshots = new Dictionary<int, SiegeTentSnapshot>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int i = 0; i < buildings.Length; i++)
            {
                if (TryCreateSnapshot(buildings[i], out SiegeTentSnapshot snapshot))
                    seenSnapshots[i + 1] = snapshot;
            }

            List<ActiveSiegeTentChangedEventArgs> events = null;
            lock (syncRoot)
            {
                Dictionary<SiegeTentCountKey, int> oldCounts = new Dictionary<SiegeTentCountKey, int>(countsByOwnerAndUnit);
                snapshotsById.Clear();
                countsByOwnerAndUnit.Clear();

                foreach (KeyValuePair<int, SiegeTentSnapshot> pair in seenSnapshots)
                {
                    snapshotsById[pair.Key] = pair.Value;
                    AddCount(pair.Value);
                }

                if (raiseEvents)
                    CreateCountChangeEvents(oldCounts, countsByOwnerAndUnit, ref events, ActiveSiegeTentChangeReason.Resync);
            }

            LogDebug("ActiveSiegeTentCache ResyncAll:", "countKeys", countsByOwnerAndUnit.Count);
            RaiseEvents(events);
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post || args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                return;

            NotifyNativeSnapshotChanged((int)args.ReturnValue, ActiveSiegeTentChangeReason.Created);
        }

        private void OnBuildingDelete(BuildingDeleteEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre || args.BuildingId <= 0)
                return;

            RemoveBuilding(args.BuildingId, ActiveSiegeTentChangeReason.Deleted);
        }

        private void NotifyNativeSnapshotChanged(int buildingId, ActiveSiegeTentChangeReason reason)
        {
            if (buildingId <= 0)
                return;

            if (TryReadSnapshot(buildingId, out SiegeTentSnapshot snapshot))
                UpdateSnapshot(buildingId, snapshot, reason);
            else
                RemoveBuilding(buildingId, reason);
        }

        private unsafe bool TryReadSnapshot(int buildingId, out SiegeTentSnapshot snapshot)
        {
            snapshot = default(SiegeTentSnapshot);
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                return false;

            return TryCreateSnapshot(*building, out snapshot);
        }

        private void UpdateSnapshot(int buildingId, SiegeTentSnapshot snapshot, ActiveSiegeTentChangeReason reason)
        {
            ActiveSiegeTentChangedEventArgs eventArgs = null;
            lock (syncRoot)
            {
                snapshotsById.TryGetValue(buildingId, out SiegeTentSnapshot oldSnapshot);
                snapshotsById[buildingId] = snapshot;

                bool countChanged = ApplyCountDelta(oldSnapshot, -1) || false;
                countChanged = ApplyCountDelta(snapshot, 1) || countChanged;
                if (countChanged)
                    eventArgs = CreateEvent(buildingId, oldSnapshot, snapshot, reason);
            }

            if (eventArgs != null)
            {
                LogDebug("ActiveSiegeTentCache changed:", "buildingId", buildingId, "player", eventArgs.PlayerId, "unit", eventArgs.UnitType, "old", eventArgs.OldCount, "new", eventArgs.NewCount, "reason", reason);
                OnActiveSiegeTentChanged?.Invoke(eventArgs);
            }
        }

        private void RemoveBuilding(int buildingId, ActiveSiegeTentChangeReason reason)
        {
            ActiveSiegeTentChangedEventArgs eventArgs = null;
            lock (syncRoot)
            {
                if (!snapshotsById.TryGetValue(buildingId, out SiegeTentSnapshot oldSnapshot))
                    return;

                snapshotsById.Remove(buildingId);
                if (ApplyCountDelta(oldSnapshot, -1))
                    eventArgs = CreateEvent(buildingId, oldSnapshot, default(SiegeTentSnapshot), reason);
            }

            if (eventArgs != null)
            {
                LogDebug("ActiveSiegeTentCache removed:", "buildingId", buildingId, "player", eventArgs.PlayerId, "unit", eventArgs.UnitType, "old", eventArgs.OldCount, "new", eventArgs.NewCount, "reason", reason);
                OnActiveSiegeTentChanged?.Invoke(eventArgs);
            }
        }

        private void AddCount(SiegeTentSnapshot snapshot)
        {
            SiegeTentCountKey key = new SiegeTentCountKey(snapshot.OwnerId, snapshot.UnitType);
            countsByOwnerAndUnit.TryGetValue(key, out int count);
            countsByOwnerAndUnit[key] = count + 1;
        }

        private bool ApplyCountDelta(SiegeTentSnapshot snapshot, int delta)
        {
            if (snapshot.UnitType == eChimps.CHIMP_TYPE_NULL)
                return false;

            SiegeTentCountKey key = new SiegeTentCountKey(snapshot.OwnerId, snapshot.UnitType);
            countsByOwnerAndUnit.TryGetValue(key, out int oldCount);
            int newCount = oldCount + delta;
            if (newCount <= 0)
            {
                countsByOwnerAndUnit.Remove(key);
                newCount = 0;
            }
            else
            {
                countsByOwnerAndUnit[key] = newCount;
            }

            return oldCount != newCount;
        }

        private ActiveSiegeTentChangedEventArgs CreateEvent(int buildingId, SiegeTentSnapshot oldSnapshot, SiegeTentSnapshot newSnapshot, ActiveSiegeTentChangeReason reason)
        {
            SiegeTentSnapshot eventSnapshot = newSnapshot.UnitType != eChimps.CHIMP_TYPE_NULL ? newSnapshot : oldSnapshot;
            SiegeTentCountKey key = new SiegeTentCountKey(eventSnapshot.OwnerId, eventSnapshot.UnitType);
            countsByOwnerAndUnit.TryGetValue(key, out int newCount);
            int oldCount = newCount;
            if (oldSnapshot.UnitType != eChimps.CHIMP_TYPE_NULL &&
                oldSnapshot.OwnerId == key.PlayerId &&
                oldSnapshot.UnitType == key.UnitType)
            {
                oldCount++;
            }
            if (newSnapshot.UnitType != eChimps.CHIMP_TYPE_NULL &&
                newSnapshot.OwnerId == key.PlayerId &&
                newSnapshot.UnitType == key.UnitType)
            {
                oldCount--;
            }

            return new ActiveSiegeTentChangedEventArgs(buildingId, key.PlayerId, key.UnitType, oldCount, newCount, reason);
        }

        private static void CreateCountChangeEvents(
            Dictionary<SiegeTentCountKey, int> oldCounts,
            Dictionary<SiegeTentCountKey, int> newCounts,
            ref List<ActiveSiegeTentChangedEventArgs> events,
            ActiveSiegeTentChangeReason reason)
        {
            foreach (KeyValuePair<SiegeTentCountKey, int> pair in oldCounts)
            {
                newCounts.TryGetValue(pair.Key, out int newCount);
                if (pair.Value != newCount)
                    AddCountEvent(ref events, pair.Key, pair.Value, newCount, reason);
            }

            foreach (KeyValuePair<SiegeTentCountKey, int> pair in newCounts)
            {
                if (oldCounts.ContainsKey(pair.Key))
                    continue;

                AddCountEvent(ref events, pair.Key, 0, pair.Value, reason);
            }
        }

        private static void AddCountEvent(ref List<ActiveSiegeTentChangedEventArgs> events, SiegeTentCountKey key, int oldCount, int newCount, ActiveSiegeTentChangeReason reason)
        {
            if (events == null)
                events = new List<ActiveSiegeTentChangedEventArgs>();

            events.Add(new ActiveSiegeTentChangedEventArgs(0, key.PlayerId, key.UnitType, oldCount, newCount, reason));
        }

        private void RaiseEvents(List<ActiveSiegeTentChangedEventArgs> events)
        {
            if (events == null || events.Count == 0)
                return;

            foreach (ActiveSiegeTentChangedEventArgs eventArgs in events)
                OnActiveSiegeTentChanged?.Invoke(eventArgs);
        }

        private static bool TryCreateSnapshot(GameBuilding building, out SiegeTentSnapshot snapshot)
        {
            snapshot = default(SiegeTentSnapshot);
            if (!IsActiveBuildingState(building.r_AliveState) ||
                !TryGetSiegeUnitType(building.r_BuildingType, out eChimps unitType))
            {
                return false;
            }

            snapshot = new SiegeTentSnapshot(building.r_PlayerIdOwner, unitType);
            return true;
        }

        private static bool TryGetSiegeUnitType(eStructs structure, out eChimps unitType)
        {
            switch (structure)
            {
                case eStructs.STRUCT_SIEGE_TENT_CATAPULT:
                    unitType = eChimps.CHIMP_TYPE_CATAPULT;
                    return true;
                case eStructs.STRUCT_SIEGE_TENT_TREBUCHET:
                    unitType = eChimps.CHIMP_TYPE_TREBUCHET;
                    return true;
                case eStructs.STRUCT_SIEGE_TENT_BATTERING_RAM:
                    unitType = eChimps.CHIMP_TYPE_BATTERING_RAM;
                    return true;
                case eStructs.STRUCT_SIEGE_TENT_SIEGE_TOWER:
                    unitType = eChimps.CHIMP_TYPE_SIEGE_TOWER;
                    return true;
                case eStructs.STRUCT_SIEGE_TENT_PORTABLE_SHIELD:
                    unitType = eChimps.CHIMP_TYPE_PORTABLE_SHIELD;
                    return true;
                default:
                    unitType = eChimps.CHIMP_TYPE_NULL;
                    return false;
            }
        }

        private static bool IsActiveBuildingState(AliveState aliveState)
        {
            return aliveState == AliveState.IsAlive || aliveState == AliveState.NeedsInit;
        }

        private void Clear()
        {
            lock (syncRoot)
            {
                snapshotsById.Clear();
                countsByOwnerAndUnit.Clear();
            }

            LogDebug("ActiveSiegeTentCache cleared.");
        }

        private void LogDebug(params object[] parts)
        {
            if (log == null)
                return;

            log.LogDebug(string.Join(" ", parts));
        }

        internal enum ActiveSiegeTentChangeReason
        {
            Created,
            Deleted,
            Resync
        }

        internal sealed class ActiveSiegeTentChangedEventArgs
        {
            public readonly int BuildingId;
            public readonly int PlayerId;
            public readonly eChimps UnitType;
            public readonly int OldCount;
            public readonly int NewCount;
            public readonly int Delta;
            public readonly ActiveSiegeTentChangeReason Reason;

            public ActiveSiegeTentChangedEventArgs(int buildingId, int playerId, eChimps unitType, int oldCount, int newCount, ActiveSiegeTentChangeReason reason)
            {
                BuildingId = buildingId;
                PlayerId = playerId;
                UnitType = unitType;
                OldCount = oldCount;
                NewCount = newCount;
                Delta = newCount - oldCount;
                Reason = reason;
            }
        }

        private readonly struct SiegeTentSnapshot
        {
            public readonly int OwnerId;
            public readonly eChimps UnitType;

            public SiegeTentSnapshot(int ownerId, eChimps unitType)
            {
                OwnerId = ownerId;
                UnitType = unitType;
            }
        }

        private struct SiegeTentCountKey
        {
            public readonly int PlayerId;
            public readonly eChimps UnitType;

            public SiegeTentCountKey(int playerId, eChimps unitType)
            {
                PlayerId = playerId;
                UnitType = unitType;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is SiegeTentCountKey other))
                    return false;

                return PlayerId == other.PlayerId && UnitType == other.UnitType;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (PlayerId * 397) ^ (int)UnitType;
                }
            }
        }
    }
}
