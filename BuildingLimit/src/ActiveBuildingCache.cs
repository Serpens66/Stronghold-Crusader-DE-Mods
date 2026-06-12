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

namespace BuildingLimit
{
    internal sealed class ActiveBuildingCache : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly SortedSet<int> activeBuildingIds = new SortedSet<int>();
        private readonly Dictionary<int, BuildingSnapshot> snapshotsById = new Dictionary<int, BuildingSnapshot>();
        private readonly Dictionary<BuildingCountKey, int> countsByOwnerAndType = new Dictionary<BuildingCountKey, int>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly ManualLogSource log;
        private bool subscribed;

        public event Action<ActiveBuildingChangedEventArgs> OnActiveBuildingChanged;
        public event Action<ActiveBuildingTypeCountChangedEventArgs> OnActiveBuildingTypeCountChanged;

        public ActiveBuildingCache(ManualLogSource log = null)
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
            LogDebug("ActiveBuildingCache hooks subscribed.");
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            subscribed = false;
            Clear();
        }

        public void GetActiveBuildingIds(List<int> results)
        {
            results.Clear();
            lock (syncRoot)
            {
                results.AddRange(activeBuildingIds);
            }
        }

        public int GetActiveBuildingCount(int playerId, eStructs buildingType)
        {
            lock (syncRoot)
            {
                return countsByOwnerAndType.TryGetValue(new BuildingCountKey(playerId, buildingType), out int count)
                    ? count
                    : 0;
            }
        }

        public void ResyncAll(bool raiseEvents)
        {
            Dictionary<int, BuildingSnapshot> seenSnapshots = new Dictionary<int, BuildingSnapshot>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            int scannedBuildings = buildings.Length;
            int aliveBuildings = 0;
            for (int i = 0; i < buildings.Length; i++)
            {
                BuildingSnapshot snapshot = BuildingSnapshot.From(buildings[i]);
                if (!IsActiveBuildingState(snapshot.AliveState))
                    continue;

                aliveBuildings++;
                seenSnapshots[i + 1] = snapshot;
            }

            List<ActiveBuildingChangedEventArgs> events = null;
            lock (syncRoot)
            {
                foreach (KeyValuePair<int, BuildingSnapshot> pair in seenSnapshots)
                {
                    int buildingId = pair.Key;
                    BuildingSnapshot snapshot = pair.Value;
                    bool wasActive = activeBuildingIds.Contains(buildingId);
                    bool hadSnapshot = snapshotsById.TryGetValue(buildingId, out BuildingSnapshot oldSnapshot);

                    activeBuildingIds.Add(buildingId);
                    snapshotsById[buildingId] = snapshot;

                    if (!raiseEvents)
                        continue;

                    if (!wasActive || !hadSnapshot)
                        AddBuildingEvent(ref events, buildingId, hadSnapshot ? oldSnapshot : default(BuildingSnapshot), snapshot, ActiveBuildingChangeReason.ResyncAdded);
                    else if (TryGetChangeReason(oldSnapshot, snapshot, out ActiveBuildingChangeReason reason))
                        AddBuildingEvent(ref events, buildingId, oldSnapshot, snapshot, reason);
                }

                List<int> removedIds = null;
                foreach (int buildingId in activeBuildingIds)
                {
                    if (seenSnapshots.ContainsKey(buildingId))
                        continue;

                    if (removedIds == null)
                        removedIds = new List<int>();

                    removedIds.Add(buildingId);
                }

                if (removedIds != null)
                {
                    foreach (int buildingId in removedIds)
                    {
                        snapshotsById.TryGetValue(buildingId, out BuildingSnapshot oldSnapshot);
                        activeBuildingIds.Remove(buildingId);
                        snapshotsById.Remove(buildingId);
                        if (raiseEvents)
                            AddBuildingEvent(ref events, buildingId, oldSnapshot, default(BuildingSnapshot), ActiveBuildingChangeReason.ResyncRemoved);
                    }
                }
            }

            RebuildCounts();
            int humanActiveIds = GetHumanActiveBuildingIdCount();
            int humanCountKeys = GetHumanCountKeyCount();
            if (humanActiveIds > 0 || humanCountKeys > 0)
            {
                LogDebug(
                    "ActiveBuildingCache ResyncAll:",
                    "raiseEvents", raiseEvents,
                    "scanned", scannedBuildings,
                    "alive", aliveBuildings,
                    "humanActiveIds", humanActiveIds,
                    "humanCountKeys", humanCountKeys);
            }
            LogAllCounts("ActiveBuildingCache ResyncAll count");
            RaiseEvents(events);
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post || args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                return;

            NotifyNativeSnapshotChanged((int)args.ReturnValue, ActiveBuildingChangeReason.Created);
        }

        private void OnBuildingDelete(BuildingDeleteEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre || args.BuildingId <= 0)
                return;

            RemoveBuilding(args.BuildingId, ActiveBuildingChangeReason.Deleted);
        }

        private void NotifyNativeSnapshotChanged(int buildingId, ActiveBuildingChangeReason fallbackReason)
        {
            if (buildingId <= 0)
                return;

            if (TryReadSnapshot(buildingId, out BuildingSnapshot snapshot))
            {
                if (ShouldLogPlayer(snapshot.OwnerId))
                {
                    LogDebug(
                        "ActiveBuildingCache NotifyNativeSnapshotChanged read snapshot:",
                        "buildingId", buildingId,
                        "owner", snapshot.OwnerId,
                        "type", snapshot.BuildingType,
                        "aliveState", snapshot.AliveState,
                        "reason", fallbackReason);
                }
                UpdateSnapshot(buildingId, snapshot, fallbackReason);
            }
            else
            {
                RemoveBuilding(buildingId, fallbackReason);
            }
        }

        private unsafe bool TryReadSnapshot(int buildingId, out BuildingSnapshot snapshot)
        {
            snapshot = default(BuildingSnapshot);
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
                return false;

            snapshot = BuildingSnapshot.From(*building);
            return true;
        }

        private void UpdateSnapshot(int buildingId, BuildingSnapshot snapshot, ActiveBuildingChangeReason fallbackReason)
        {
            ActiveBuildingChangedEventArgs eventArgs = null;
            bool changed = false;
            lock (syncRoot)
            {
                bool wasActive = activeBuildingIds.Contains(buildingId);
                bool hadSnapshot = snapshotsById.TryGetValue(buildingId, out BuildingSnapshot oldSnapshot);
                bool isActive = IsActiveBuildingState(snapshot.AliveState);

                if (!isActive)
                {
                    bool removed = activeBuildingIds.Remove(buildingId);
                    snapshotsById.Remove(buildingId);
                    if (removed || hadSnapshot)
                    {
                        eventArgs = CreateEvent(buildingId, hadSnapshot ? oldSnapshot : default(BuildingSnapshot), snapshot, fallbackReason);
                        changed = true;
                    }
                }
                else
                {
                    activeBuildingIds.Add(buildingId);
                    snapshotsById[buildingId] = snapshot;

                    if (!wasActive || !hadSnapshot)
                    {
                        eventArgs = CreateEvent(buildingId, hadSnapshot ? oldSnapshot : default(BuildingSnapshot), snapshot, fallbackReason);
                        changed = true;
                    }
                    else if (TryGetChangeReason(oldSnapshot, snapshot, out ActiveBuildingChangeReason reason))
                    {
                        eventArgs = CreateEvent(buildingId, oldSnapshot, snapshot, reason);
                        changed = true;
                    }
                }
            }

            if (ShouldLogPlayer(snapshot.OwnerId) ||
                (eventArgs != null && ShouldLogPlayer(eventArgs.OldSnapshot.OwnerId)))
            {
                LogDebug(
                    "ActiveBuildingCache UpdateSnapshot:",
                    "buildingId", buildingId,
                    "owner", snapshot.OwnerId,
                    "type", snapshot.BuildingType,
                    "aliveState", snapshot.AliveState,
                    "reason", fallbackReason,
                    "changed", changed);
            }

            if (eventArgs != null)
                ApplyAndRaiseEvent(eventArgs);
        }

        private void RemoveBuilding(int buildingId, ActiveBuildingChangeReason reason)
        {
            ActiveBuildingChangedEventArgs eventArgs = null;
            bool removedOrKnown = false;
            BuildingSnapshot removedSnapshot = default(BuildingSnapshot);
            lock (syncRoot)
            {
                bool wasActive = activeBuildingIds.Remove(buildingId);
                bool hadSnapshot = snapshotsById.TryGetValue(buildingId, out BuildingSnapshot oldSnapshot);
                removedSnapshot = oldSnapshot;
                snapshotsById.Remove(buildingId);

                if (wasActive || hadSnapshot)
                {
                    eventArgs = CreateEvent(buildingId, oldSnapshot, default(BuildingSnapshot), reason);
                    removedOrKnown = true;
                }
            }

            if (ShouldLogPlayer(removedSnapshot.OwnerId))
            {
                LogDebug(
                    "ActiveBuildingCache RemoveBuilding:",
                    "buildingId", buildingId,
                    "owner", removedSnapshot.OwnerId,
                    "type", removedSnapshot.BuildingType,
                    "aliveState", removedSnapshot.AliveState,
                    "reason", reason,
                    "removedOrKnown", removedOrKnown);
            }

            if (eventArgs != null)
                ApplyAndRaiseEvent(eventArgs);
        }

        private void ApplyAndRaiseEvent(ActiveBuildingChangedEventArgs eventArgs)
        {
            ActiveBuildingTypeCountChangedEventArgs countEvent = ApplyCountDelta(eventArgs);
            OnActiveBuildingChanged?.Invoke(eventArgs);
            if (countEvent != null)
                OnActiveBuildingTypeCountChanged?.Invoke(countEvent);
        }

        private void RaiseEvents(List<ActiveBuildingChangedEventArgs> events)
        {
            if (events == null || events.Count == 0)
                return;

            events.Sort((left, right) => left.BuildingId.CompareTo(right.BuildingId));
            foreach (ActiveBuildingChangedEventArgs eventArgs in events)
                OnActiveBuildingChanged?.Invoke(eventArgs);

            // Resync rebuilds the whole count table atomically. The limit mods do not consume
            // batch count events, so avoid replaying deltas into the freshly rebuilt cache.
        }

        private ActiveBuildingTypeCountChangedEventArgs ApplyCountDelta(ActiveBuildingChangedEventArgs eventArgs)
        {
            Dictionary<BuildingCountKey, int> oldCounts = new Dictionary<BuildingCountKey, int>();
            Dictionary<BuildingCountKey, int> newCounts = new Dictionary<BuildingCountKey, int>();
            lock (syncRoot)
            {
                TrackCountDelta(eventArgs.OldSnapshot, -1, oldCounts, newCounts);
                TrackCountDelta(eventArgs.NewSnapshot, 1, oldCounts, newCounts);
            }

            foreach (KeyValuePair<BuildingCountKey, int> pair in newCounts)
            {
                int oldCount = oldCounts[pair.Key];
                int newCount = pair.Value;
                if (oldCount != newCount)
                {
                    if (ShouldLogPlayer(pair.Key.PlayerId))
                    {
                        LogDebug(
                            "ActiveBuildingCache count changed:",
                            "player", pair.Key.PlayerId,
                            "type", pair.Key.BuildingType,
                            "old", oldCount,
                            "new", newCount,
                            "delta", newCount - oldCount,
                            "buildingId", eventArgs.BuildingId,
                            "reason", eventArgs.Reason);
                    }
                    return new ActiveBuildingTypeCountChangedEventArgs(pair.Key.PlayerId, pair.Key.BuildingType, oldCount, newCount, eventArgs.BuildingId, eventArgs.Reason);
                }
            }

            return null;
        }

        private void TrackCountDelta(BuildingSnapshot snapshot, int delta, Dictionary<BuildingCountKey, int> oldCounts, Dictionary<BuildingCountKey, int> newCounts)
        {
            if (!IsActiveBuildingState(snapshot.AliveState))
                return;

            BuildingCountKey key = new BuildingCountKey(snapshot.OwnerId, snapshot.BuildingType);
            countsByOwnerAndType.TryGetValue(key, out int oldCount);
            if (!oldCounts.ContainsKey(key))
                oldCounts[key] = oldCount;

            int newCount = oldCount + delta;
            if (newCount <= 0)
            {
                countsByOwnerAndType.Remove(key);
                newCount = 0;
            }
            else
            {
                countsByOwnerAndType[key] = newCount;
            }

            newCounts[key] = newCount;
        }

        private void RebuildCounts()
        {
            lock (syncRoot)
            {
                countsByOwnerAndType.Clear();
                foreach (BuildingSnapshot snapshot in snapshotsById.Values)
                {
                    if (!IsActiveBuildingState(snapshot.AliveState))
                        continue;

                    BuildingCountKey key = new BuildingCountKey(snapshot.OwnerId, snapshot.BuildingType);
                    countsByOwnerAndType.TryGetValue(key, out int count);
                    countsByOwnerAndType[key] = count + 1;
                }
            }

            int humanCountKeys = GetHumanCountKeyCount();
            if (humanCountKeys > 0)
                LogDebug("ActiveBuildingCache RebuildCounts:", "humanCountKeys", humanCountKeys);
        }

        private int GetHumanActiveBuildingIdCount()
        {
            lock (syncRoot)
            {
                int count = 0;
                foreach (BuildingSnapshot snapshot in snapshotsById.Values)
                {
                    if (IsActiveBuildingState(snapshot.AliveState) && ShouldLogPlayer(snapshot.OwnerId))
                        count++;
                }

                return count;
            }
        }

        private int GetHumanCountKeyCount()
        {
            lock (syncRoot)
            {
                int count = 0;
                foreach (BuildingCountKey key in countsByOwnerAndType.Keys)
                {
                    if (ShouldLogPlayer(key.PlayerId))
                        count++;
                }

                return count;
            }
        }

        private void LogAllCounts(string prefix)
        {
            List<KeyValuePair<BuildingCountKey, int>> counts;
            lock (syncRoot)
            {
                counts = new List<KeyValuePair<BuildingCountKey, int>>(countsByOwnerAndType);
            }

            counts.Sort((left, right) =>
            {
                int playerCompare = left.Key.PlayerId.CompareTo(right.Key.PlayerId);
                if (playerCompare != 0)
                    return playerCompare;

                return left.Key.BuildingType.CompareTo(right.Key.BuildingType);
            });

            foreach (KeyValuePair<BuildingCountKey, int> pair in counts)
            {
                if (!ShouldLogPlayer(pair.Key.PlayerId))
                    continue;

                LogDebug(
                    prefix + ":",
                    "player", pair.Key.PlayerId,
                    "type", pair.Key.BuildingType,
                    "count", pair.Value);
            }
        }

        private static void AddBuildingEvent(ref List<ActiveBuildingChangedEventArgs> events, int buildingId, BuildingSnapshot oldSnapshot, BuildingSnapshot newSnapshot, ActiveBuildingChangeReason reason)
        {
            if (events == null)
                events = new List<ActiveBuildingChangedEventArgs>();

            events.Add(CreateEvent(buildingId, oldSnapshot, newSnapshot, reason));
        }

        private static ActiveBuildingChangedEventArgs CreateEvent(int buildingId, BuildingSnapshot oldSnapshot, BuildingSnapshot newSnapshot, ActiveBuildingChangeReason reason)
        {
            return new ActiveBuildingChangedEventArgs(buildingId, oldSnapshot, newSnapshot, reason);
        }

        private static bool TryGetChangeReason(BuildingSnapshot oldSnapshot, BuildingSnapshot newSnapshot, out ActiveBuildingChangeReason reason)
        {
            if (oldSnapshot.AliveState != newSnapshot.AliveState)
            {
                reason = ActiveBuildingChangeReason.AliveStateChanged;
                return true;
            }

            if (oldSnapshot.BuildingType != newSnapshot.BuildingType)
            {
                reason = ActiveBuildingChangeReason.TypeChanged;
                return true;
            }

            if (oldSnapshot.OwnerId != newSnapshot.OwnerId)
            {
                reason = ActiveBuildingChangeReason.OwnerChanged;
                return true;
            }

            reason = default(ActiveBuildingChangeReason);
            return false;
        }

        private static bool IsActiveBuildingState(AliveState aliveState)
        {
            return aliveState == AliveState.IsAlive || aliveState == AliveState.NeedsInit;
        }

        private static bool ShouldLogPlayer(int playerId)
        {
            try
            {
                return GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            }
            catch
            {
                return false;
            }
        }

        private void Clear()
        {
            lock (syncRoot)
            {
                activeBuildingIds.Clear();
                snapshotsById.Clear();
                countsByOwnerAndType.Clear();
            }

            LogDebug("ActiveBuildingCache cleared.");
        }

        private void LogDebug(params object[] parts)
        {
            Shared.DebugLogHelper.LogDebug(log, parts);
        }

        internal enum ActiveBuildingChangeReason
        {
            Created,
            Deleted,
            AliveStateChanged,
            TypeChanged,
            OwnerChanged,
            ResyncAdded,
            ResyncRemoved
        }

        internal sealed class ActiveBuildingChangedEventArgs
        {
            public readonly int BuildingId;
            public readonly BuildingSnapshot OldSnapshot;
            public readonly BuildingSnapshot NewSnapshot;
            public readonly ActiveBuildingChangeReason Reason;

            public ActiveBuildingChangedEventArgs(int buildingId, BuildingSnapshot oldSnapshot, BuildingSnapshot newSnapshot, ActiveBuildingChangeReason reason)
            {
                BuildingId = buildingId;
                OldSnapshot = oldSnapshot;
                NewSnapshot = newSnapshot;
                Reason = reason;
            }
        }

        internal sealed class ActiveBuildingTypeCountChangedEventArgs
        {
            public readonly int PlayerId;
            public readonly eStructs BuildingType;
            public readonly int OldCount;
            public readonly int NewCount;
            public readonly int Delta;
            public readonly int BuildingId;
            public readonly ActiveBuildingChangeReason Reason;

            public ActiveBuildingTypeCountChangedEventArgs(int playerId, eStructs buildingType, int oldCount, int newCount, int buildingId, ActiveBuildingChangeReason reason)
            {
                PlayerId = playerId;
                BuildingType = buildingType;
                OldCount = oldCount;
                NewCount = newCount;
                Delta = newCount - oldCount;
                BuildingId = buildingId;
                Reason = reason;
            }
        }

        internal readonly struct BuildingSnapshot
        {
            public readonly AliveState AliveState;
            public readonly eStructs BuildingType;
            public readonly int OwnerId;

            public BuildingSnapshot(AliveState aliveState, eStructs buildingType, int ownerId)
            {
                AliveState = aliveState;
                BuildingType = buildingType;
                OwnerId = ownerId;
            }

            public static BuildingSnapshot From(GameBuilding building)
            {
                return new BuildingSnapshot(building.r_AliveState, building.r_BuildingType, building.r_PlayerIdOwner);
            }
        }

        private struct BuildingCountKey
        {
            public readonly int PlayerId;
            public readonly eStructs BuildingType;

            public BuildingCountKey(int playerId, eStructs buildingType)
            {
                PlayerId = playerId;
                BuildingType = buildingType;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is BuildingCountKey other))
                    return false;

                return PlayerId == other.PlayerId && BuildingType == other.BuildingType;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (PlayerId * 397) ^ (int)BuildingType;
                }
            }
        }
    }
}
