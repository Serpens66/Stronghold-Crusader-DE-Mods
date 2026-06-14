using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace UnitLimit
{
    internal sealed class ActiveUnitCache : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly Dictionary<int, UnitSnapshot> snapshotsById = new Dictionary<int, UnitSnapshot>();
        private readonly Dictionary<UnitCountKey, int> countsByOwnerAndType = new Dictionary<UnitCountKey, int>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly ManualLogSource log;
        private bool subscribed;

        public event Action<ActiveUnitChangedEventArgs> OnActiveUnitChanged;

        public ActiveUnitCache(ManualLogSource log = null)
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
            subscriptions.Add(UnitR3EventHooks.OnUnitCreate.Observable
                .Subscribe(OnUnitCreate));
            subscriptions.Add(UnitR3EventHooks.OnUnitDelete.Observable
                .Subscribe(OnUnitDelete));
            subscriptions.Add(UnitR3EventHooks.OnUnitTransition.Observable
                .Subscribe(OnUnitTransition));

            subscribed = true;
            LogDebug("ActiveUnitCache hooks subscribed.");
        }

        public void Dispose()
        {
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            subscribed = false;
            Clear();
        }

        public int GetActiveUnitCount(int playerId, eChimps unitType)
        {
            lock (syncRoot)
            {
                return countsByOwnerAndType.TryGetValue(new UnitCountKey(playerId, unitType), out int count)
                    ? count
                    : 0;
            }
        }

        public void ResyncAll(bool raiseEvents)
        {
            Dictionary<int, UnitSnapshot> seenSnapshots = new Dictionary<int, UnitSnapshot>();
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            int scannedUnits = units.Length;
            int aliveUnits = 0;
            for (int i = 0; i < units.Length; i++)
            {
                UnitSnapshot snapshot = UnitSnapshot.From(units[i]);
                if (!IsActiveUnitState(snapshot.AliveState))
                    continue;

                aliveUnits++;
                seenSnapshots[i + 1] = snapshot;
            }

            List<ActiveUnitChangedEventArgs> events = null;
            lock (syncRoot)
            {
                HashSet<int> previousUnitIds = new HashSet<int>(snapshotsById.Keys);
                foreach (KeyValuePair<int, UnitSnapshot> pair in seenSnapshots)
                {
                    int unitId = pair.Key;
                    UnitSnapshot snapshot = pair.Value;
                    bool hadSnapshot = snapshotsById.TryGetValue(unitId, out UnitSnapshot oldSnapshot);

                    snapshotsById[unitId] = snapshot;
                    previousUnitIds.Remove(unitId);

                    if (!raiseEvents)
                        continue;

                    if (!hadSnapshot)
                        AddUnitEvent(ref events, unitId, hadSnapshot ? oldSnapshot : default(UnitSnapshot), snapshot, ActiveUnitChangeReason.ResyncAdded);
                    else if (TryGetChangeReason(oldSnapshot, snapshot, out ActiveUnitChangeReason reason))
                        AddUnitEvent(ref events, unitId, oldSnapshot, snapshot, reason);
                }

                foreach (int unitId in previousUnitIds)
                {
                    snapshotsById.TryGetValue(unitId, out UnitSnapshot oldSnapshot);
                    snapshotsById.Remove(unitId);
                    if (raiseEvents)
                        AddUnitEvent(ref events, unitId, oldSnapshot, default(UnitSnapshot), ActiveUnitChangeReason.ResyncRemoved);
                }
            }

            RebuildCounts();
            int humanActiveIds = GetHumanActiveUnitIdCount();
            int humanCountKeys = GetHumanCountKeyCount();
            if (humanActiveIds > 0 || humanCountKeys > 0)
            {
                LogDebug(
                    "ActiveUnitCache ResyncAll:",
                    "raiseEvents", raiseEvents,
                    "scanned", scannedUnits,
                    "alive", aliveUnits,
                    "humanActiveIds", humanActiveIds,
                    "humanCountKeys", humanCountKeys);
            }
            LogAllCounts("ActiveUnitCache ResyncAll count");
            RaiseEvents(events);
        }

        private void OnUnitCreate(UnitCreateEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post || args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                return;

            LogDebug($"OnUnitCreate: unitId={args.ReturnValue}, type={args.UnitType}, owner={args.PlayerOwnerId}, phase={args.Phase}");

            NotifyNativeSnapshotChanged((int)args.ReturnValue, ActiveUnitChangeReason.Created);
        }

        private void OnUnitDelete(UnitDeleteEventArgs args)
        {
            LogDebug("OnUnitDelete", "unitId", args.UnitId, "phase", args.Phase);
            if (args.Phase != EventHookPhase.Pre || args.UnitId > int.MaxValue)
                return;

            RemoveUnit((int)args.UnitId, ActiveUnitChangeReason.Deleted);
        }

        private void OnUnitTransition(UnitTransitionEventArgs args)
        {
            LogDebug(
                "ActiveUnitCache OnUnitTransition fired: " +
                "phase=" + args.Phase +
                ", unitId=" + args.UnitId +
                ", playerOwnerId=" + args.PlayerOwnerId +
                ", nextUnitType=" + args.NextUnitType +
                ", source=" + args.Source);

            if (args.Phase != EventHookPhase.Pre || args.UnitId <= 0)
                return;

            if (!TryReadSnapshot(args.UnitId, out UnitSnapshot snapshot))
                return;

            UnitSnapshot transitionedSnapshot = new UnitSnapshot(
                snapshot.AliveState,
                args.NextUnitType,
                snapshot.TransformIntoUnitOfType,
                args.PlayerOwnerId);

            ActiveUnitChangeReason reason = args.Source == UnitTransitionSource.Disband
                ? ActiveUnitChangeReason.Disbanded
                : ActiveUnitChangeReason.TypeChanged;
            UpdateSnapshot(args.UnitId, transitionedSnapshot, reason, true);
        }

        internal void NotifyNativeSnapshotChanged(int unitId, ActiveUnitChangeReason fallbackReason, bool preferFallbackReason = false)
        {
            if (unitId <= 0)
                return;

            if (TryReadSnapshot(unitId, out UnitSnapshot snapshot))
            {
                if (ShouldLogCacheChange(fallbackReason, snapshot.OwnerId))
                {
                    LogDebug(
                        "ActiveUnitCache NotifyNativeSnapshotChanged read snapshot:",
                        "unitId", unitId,
                        "owner", snapshot.OwnerId,
                        "type", snapshot.UnitType,
                        "aliveState", snapshot.AliveState,
                        "reason", fallbackReason,
                        "preferFallbackReason", preferFallbackReason);
                }
                UpdateSnapshot(unitId, snapshot, fallbackReason, preferFallbackReason);
            }
            else
            {
                if (ShouldLogCacheChange(fallbackReason, 0))
                {
                    LogDebug(
                        "ActiveUnitCache NotifyNativeSnapshotChanged missing snapshot:",
                        "unitId", unitId,
                        "reason", fallbackReason,
                        "preferFallbackReason", preferFallbackReason);
                }
                RemoveUnit(unitId, fallbackReason);
            }
        }

        private unsafe bool TryReadSnapshot(int unitId, out UnitSnapshot snapshot)
        {
            snapshot = default(UnitSnapshot);
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                return false;

            snapshot = UnitSnapshot.From(*unit);
            return true;
        }

        private void UpdateSnapshot(int unitId, UnitSnapshot snapshot, ActiveUnitChangeReason fallbackReason, bool preferFallbackReason)
        {
            ActiveUnitChangedEventArgs eventArgs = null;
            bool changed = false;
            lock (syncRoot)
            {
                bool hadSnapshot = snapshotsById.TryGetValue(unitId, out UnitSnapshot oldSnapshot);
                bool isActive = IsActiveUnitState(snapshot.AliveState);

                if (!isActive)
                {
                    snapshotsById.Remove(unitId);
                    if (hadSnapshot)
                    {
                        eventArgs = CreateEvent(unitId, hadSnapshot ? oldSnapshot : default(UnitSnapshot), snapshot, fallbackReason);
                        ApplyCountDelta(eventArgs);
                        changed = true;
                    }
                }
                else
                {
                    snapshotsById[unitId] = snapshot;

                    if (!hadSnapshot)
                    {
                        eventArgs = CreateEvent(unitId, hadSnapshot ? oldSnapshot : default(UnitSnapshot), snapshot, fallbackReason);
                        ApplyCountDelta(eventArgs);
                        changed = true;
                    }
                    else if (TryGetChangeReason(oldSnapshot, snapshot, out ActiveUnitChangeReason reason))
                    {
                        eventArgs = CreateEvent(unitId, oldSnapshot, snapshot, preferFallbackReason ? fallbackReason : reason);
                        ApplyCountDelta(eventArgs);
                        changed = true;
                    }
                }
            }

            if (ShouldLogCacheChange(fallbackReason, snapshot.OwnerId) ||
                (eventArgs != null && ShouldLogCacheChange(eventArgs.Reason, eventArgs.OldSnapshot.OwnerId)))
            {
                LogDebug(
                    "ActiveUnitCache UpdateSnapshot:",
                    "unitId", unitId,
                    "oldOwner", eventArgs == null ? 0 : eventArgs.OldSnapshot.OwnerId,
                    "oldType", eventArgs == null ? default(eChimps) : eventArgs.OldSnapshot.UnitType,
                    "oldAliveState", eventArgs == null ? default(AliveState) : eventArgs.OldSnapshot.AliveState,
                    "owner", snapshot.OwnerId,
                    "type", snapshot.UnitType,
                    "aliveState", snapshot.AliveState,
                    "reason", fallbackReason,
                    "preferFallbackReason", preferFallbackReason,
                    "changed", changed);
            }

            if (eventArgs != null)
                ApplyAndRaiseEvent(eventArgs);
        }

        private void RemoveUnit(int unitId, ActiveUnitChangeReason reason)
        {
            ActiveUnitChangedEventArgs eventArgs = null;
            bool removedOrKnown = false;
            UnitSnapshot removedSnapshot = default(UnitSnapshot);
            lock (syncRoot)
            {
                bool hadSnapshot = snapshotsById.TryGetValue(unitId, out UnitSnapshot oldSnapshot);
                removedSnapshot = oldSnapshot;
                snapshotsById.Remove(unitId);

                if (hadSnapshot)
                {
                    eventArgs = CreateEvent(unitId, oldSnapshot, default(UnitSnapshot), reason);
                    ApplyCountDelta(eventArgs);
                    removedOrKnown = true;
                }
            }

            if (ShouldLogCacheChange(reason, removedSnapshot.OwnerId))
            {
                LogDebug(
                    "ActiveUnitCache RemoveUnit:",
                    "unitId", unitId,
                    "owner", removedSnapshot.OwnerId,
                    "type", removedSnapshot.UnitType,
                    "aliveState", removedSnapshot.AliveState,
                    "reason", reason,
                    "removedOrKnown", removedOrKnown);
            }

            if (eventArgs != null)
                ApplyAndRaiseEvent(eventArgs);
        }

        private void ApplyAndRaiseEvent(ActiveUnitChangedEventArgs eventArgs)
        {
            OnActiveUnitChanged?.Invoke(eventArgs);
        }

        private void RaiseEvents(List<ActiveUnitChangedEventArgs> events)
        {
            if (events == null || events.Count == 0)
                return;

            events.Sort((left, right) => left.UnitId.CompareTo(right.UnitId));
            foreach (ActiveUnitChangedEventArgs eventArgs in events)
                OnActiveUnitChanged?.Invoke(eventArgs);

            // Resync rebuilds the whole count table atomically. The limit mods do not consume
            // batch count events, so avoid replaying deltas into the freshly rebuilt cache.
        }

        private void ApplyCountDelta(ActiveUnitChangedEventArgs eventArgs)
        {
            bool oldCounted = TryGetActiveCountKey(eventArgs.OldSnapshot, out UnitCountKey oldKey);
            bool newCounted = TryGetActiveCountKey(eventArgs.NewSnapshot, out UnitCountKey newKey);
            if (oldCounted && newCounted && oldKey.Equals(newKey))
                return;

            if (oldCounted)
                ApplyCountDelta(oldKey, -1, eventArgs.UnitId, eventArgs.Reason);

            if (newCounted)
                ApplyCountDelta(newKey, 1, eventArgs.UnitId, eventArgs.Reason);
        }

        private bool ApplyCountDelta(UnitCountKey key, int delta, int unitId, ActiveUnitChangeReason reason)
        {
            countsByOwnerAndType.TryGetValue(key, out int oldCount);
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

            if (oldCount == newCount)
                return false;

            if (ShouldLogCacheChange(reason, key.PlayerId))
            {
                LogDebug(
                    "ActiveUnitCache count changed:",
                    "player", key.PlayerId,
                    "type", key.UnitType,
                    "old", oldCount,
                    "new", newCount,
                    "delta", newCount - oldCount,
                    "unitId", unitId,
                    "reason", reason);
            }

            return true;
        }

        private static bool TryGetActiveCountKey(UnitSnapshot snapshot, out UnitCountKey key)
        {
            if (IsActiveUnitState(snapshot.AliveState))
            {
                key = new UnitCountKey(snapshot.OwnerId, snapshot.UnitType);
                return true;
            }

            key = default(UnitCountKey);
            return false;
        }

        private void RebuildCounts()
        {
            lock (syncRoot)
            {
                countsByOwnerAndType.Clear();
                foreach (UnitSnapshot snapshot in snapshotsById.Values)
                {
                    if (!IsActiveUnitState(snapshot.AliveState))
                        continue;

                    UnitCountKey key = new UnitCountKey(snapshot.OwnerId, snapshot.UnitType);
                    countsByOwnerAndType.TryGetValue(key, out int count);
                    countsByOwnerAndType[key] = count + 1;
                }
            }

            int humanCountKeys = GetHumanCountKeyCount();
            if (humanCountKeys > 0)
                LogDebug("ActiveUnitCache RebuildCounts:", "humanCountKeys", humanCountKeys);
        }

        private int GetHumanActiveUnitIdCount()
        {
            lock (syncRoot)
            {
                int count = 0;
                foreach (UnitSnapshot snapshot in snapshotsById.Values)
                {
                    if (IsActiveUnitState(snapshot.AliveState) && ShouldLogPlayer(snapshot.OwnerId))
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
                foreach (UnitCountKey key in countsByOwnerAndType.Keys)
                {
                    if (ShouldLogPlayer(key.PlayerId))
                        count++;
                }

                return count;
            }
        }

        private void LogAllCounts(string prefix)
        {
            List<KeyValuePair<UnitCountKey, int>> counts;
            lock (syncRoot)
            {
                counts = new List<KeyValuePair<UnitCountKey, int>>(countsByOwnerAndType);
            }

            counts.Sort((left, right) =>
            {
                int playerCompare = left.Key.PlayerId.CompareTo(right.Key.PlayerId);
                if (playerCompare != 0)
                    return playerCompare;

                return left.Key.UnitType.CompareTo(right.Key.UnitType);
            });

            foreach (KeyValuePair<UnitCountKey, int> pair in counts)
            {
                if (!ShouldLogPlayer(pair.Key.PlayerId))
                    continue;

                LogDebug(
                    prefix + ":",
                    "player", pair.Key.PlayerId,
                    "type", pair.Key.UnitType,
                    "count", pair.Value);
            }
        }

        private static void AddUnitEvent(ref List<ActiveUnitChangedEventArgs> events, int unitId, UnitSnapshot oldSnapshot, UnitSnapshot newSnapshot, ActiveUnitChangeReason reason)
        {
            if (events == null)
                events = new List<ActiveUnitChangedEventArgs>();

            events.Add(CreateEvent(unitId, oldSnapshot, newSnapshot, reason));
        }

        private static ActiveUnitChangedEventArgs CreateEvent(int unitId, UnitSnapshot oldSnapshot, UnitSnapshot newSnapshot, ActiveUnitChangeReason reason)
        {
            return new ActiveUnitChangedEventArgs(unitId, oldSnapshot, newSnapshot, reason);
        }

        private static bool TryGetChangeReason(UnitSnapshot oldSnapshot, UnitSnapshot newSnapshot, out ActiveUnitChangeReason reason)
        {
            if (oldSnapshot.AliveState != newSnapshot.AliveState)
            {
                reason = ActiveUnitChangeReason.AliveStateChanged;
                return true;
            }

            if (oldSnapshot.UnitType != newSnapshot.UnitType)
            {
                reason = ActiveUnitChangeReason.TypeChanged;
                return true;
            }

            if (oldSnapshot.OwnerId != newSnapshot.OwnerId)
            {
                reason = ActiveUnitChangeReason.OwnerChanged;
                return true;
            }

            reason = default(ActiveUnitChangeReason);
            return false;
        }

        private static bool ShouldLogCacheChange(ActiveUnitChangeReason reason, int playerId)
        {
            return reason == ActiveUnitChangeReason.Disbanded || ShouldLogPlayer(playerId);
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

        private static bool IsActiveUnitState(AliveState aliveState)
        {
            return aliveState == AliveState.IsAlive ||
                aliveState == AliveState.NeedsInit;
        }

        private void Clear()
        {
            lock (syncRoot)
            {
                snapshotsById.Clear();
                countsByOwnerAndType.Clear();
            }

            LogDebug("ActiveUnitCache cleared.");
        }

        private void LogDebug(params object[] parts)
        {
            Shared.DebugLogHelper.LogDebug(log, parts);
        }

        internal enum ActiveUnitChangeReason
        {
            Created,
            Deleted,
            AliveStateChanged,
            TypeChanged,
            OwnerChanged,
            Disbanded,
            ResyncAdded,
            ResyncRemoved
        }

        internal sealed class ActiveUnitChangedEventArgs
        {
            public readonly int UnitId;
            public readonly UnitSnapshot OldSnapshot;
            public readonly UnitSnapshot NewSnapshot;
            public readonly ActiveUnitChangeReason Reason;

            public ActiveUnitChangedEventArgs(int unitId, UnitSnapshot oldSnapshot, UnitSnapshot newSnapshot, ActiveUnitChangeReason reason)
            {
                UnitId = unitId;
                OldSnapshot = oldSnapshot;
                NewSnapshot = newSnapshot;
                Reason = reason;
            }
        }

        internal readonly struct UnitSnapshot
        {
            public readonly AliveState AliveState;
            public readonly eChimps UnitType;
            public readonly eChimps TransformIntoUnitOfType;
            public readonly int OwnerId;

            public UnitSnapshot(AliveState aliveState, eChimps unitType, eChimps transformIntoUnitOfType, int ownerId)
            {
                AliveState = aliveState;
                UnitType = unitType;
                TransformIntoUnitOfType = transformIntoUnitOfType;
                OwnerId = ownerId;
            }

            public static UnitSnapshot From(GameUnit unit)
            {
                return new UnitSnapshot(
                    unit.r_AliveState,
                    unit.r_UnitChimp,
                    unit.r_TransformIntoUnitOfType,
                    unit.r_ControllableForPlayerId);
            }
        }

        private struct UnitCountKey
        {
            public readonly int PlayerId;
            public readonly eChimps UnitType;

            public UnitCountKey(int playerId, eChimps unitType)
            {
                PlayerId = playerId;
                UnitType = unitType;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is UnitCountKey other))
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
