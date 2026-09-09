using System;
using System.Collections.Generic;
using System.Linq;

namespace PreplacedTest
{
    internal static class AicSlotIndexResolver
    {
        public static bool TryResolve(int oneBasedSlot, int arrayLength, out int zeroBasedIndex)
        {
            zeroBasedIndex = oneBasedSlot - 1;
            return oneBasedSlot > 0 && zeroBasedIndex < arrayLength;
        }
    }

    internal static class PlacementValidatorResult
    {
        // Vanilla RVA 0x7B060 returns zero only for an allowed tile.
        public static string Classify(int result) => result == 0 ? "allowed" :
            result == 1 ? "rejected" : result == 2 ? "occupied-building" : "unknown-" + result;

        public static bool IsRejected(int result) => result != 0;
    }

    internal static class CrushedTimerTransition
    {
        public static bool IsActivation(int before, int after) => before == 0 && after == 1;
    }

    internal static class DamageObservationModel
    {
        public static bool IsLethalInput(int currentHealth, int damage) => currentHealth > 0 && damage >= currentHealth;
    }

    internal static class AivAreaClassifier
    {
        public static bool Intersects(int originX, int originY, int size, int beginX, int beginY, int endX, int endY) =>
            endX >= originX && beginX < originX + size && endY >= originY && beginY < originY + size;
    }

    internal sealed class EarlyOwnerEventBuffer
    {
        private readonly Dictionary<int, List<string>> events = new Dictionary<int, List<string>>();
        private readonly int minimumOwnerId;
        private readonly int maximumOwnerId;

        public EarlyOwnerEventBuffer(int minimumOwnerId, int maximumOwnerId)
        {
            if (minimumOwnerId > maximumOwnerId) throw new ArgumentOutOfRangeException(nameof(minimumOwnerId));
            this.minimumOwnerId = minimumOwnerId;
            this.maximumOwnerId = maximumOwnerId;
        }

        public void Add(int ownerId, string value)
        {
            if (ownerId < minimumOwnerId || ownerId > maximumOwnerId) throw new ArgumentOutOfRangeException(nameof(ownerId));
            if (!events.TryGetValue(ownerId, out List<string> ownerEvents))
            {
                ownerEvents = new List<string>();
                events.Add(ownerId, ownerEvents);
            }
            ownerEvents.Add(value);
        }

        public string[] Drain(int ownerId)
        {
            if (!events.TryGetValue(ownerId, out List<string> ownerEvents)) return Array.Empty<string>();
            events.Remove(ownerId);
            return ownerEvents.ToArray();
        }

        public int CountFor(int ownerId) => events.TryGetValue(ownerId, out List<string> ownerEvents) ? ownerEvents.Count : 0;

        public void Clear() => events.Clear();
    }

    internal sealed class DiagnosticCounterSet
    {
        private readonly SortedDictionary<string, long> interval = new SortedDictionary<string, long>(StringComparer.Ordinal);
        private readonly SortedDictionary<string, long> total = new SortedDictionary<string, long>(StringComparer.Ordinal);
        private bool accepting = true;

        public void Add(string key, long amount = 1)
        {
            if (!accepting)
                return;
            AddTo(interval, key, amount);
            AddTo(total, key, amount);
        }

        public KeyValuePair<string, long>[] DrainInterval()
        {
            KeyValuePair<string, long>[] result = interval.ToArray();
            interval.Clear();
            return result;
        }

        public KeyValuePair<string, long>[] SnapshotTotal() => total.ToArray();

        public long TotalFor(string key) => total.TryGetValue(key, out long value) ? value : 0;

        public void Clear()
        {
            interval.Clear();
            total.Clear();
            accepting = true;
        }

        public void Stop() => accepting = false;

        private static void AddTo(IDictionary<string, long> target, string key, long amount)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("A diagnostic key is required.", nameof(key));
            target[key] = checked((target.TryGetValue(key, out long value) ? value : 0) + amount);
        }
    }

    internal readonly struct SchedulerGateState
    {
        public SchedulerGateState(int activeAivSlot, int crushedCounter, int crushedDelay,
            int gold, int buildCounter, int buildRate, int pauseCounter, int currentStepGoal,
            int highestPreparedFrame)
        {
            ActiveAivSlot = activeAivSlot;
            CrushedCounter = crushedCounter;
            CrushedDelay = crushedDelay;
            Gold = gold;
            BuildCounter = buildCounter;
            BuildRate = buildRate;
            PauseCounter = pauseCounter;
            CurrentStepGoal = currentStepGoal;
            HighestPreparedFrame = highestPreparedFrame;
        }

        public int ActiveAivSlot { get; }
        public int CrushedCounter { get; }
        public int CrushedDelay { get; }
        public int Gold { get; }
        public int BuildCounter { get; }
        public int BuildRate { get; }
        public int PauseCounter { get; }
        public int CurrentStepGoal { get; }
        public int HighestPreparedFrame { get; }
    }

    internal static class SchedulerGateClassifier
    {
        // These thresholds are literal machine-contract values in Scheduler RVA 0x539B0.
        internal const int NormalGoldThresholdExclusive = 5001;
        internal const int FastGoldThresholdExclusive = 16001;

        public static string ClassifyBeforeCall(SchedulerGateState state)
        {
            if (state.ActiveAivSlot <= 0)
                return "inactive-aiv-slot";
            if (state.CrushedCounter != 0 && state.CrushedDelay <= 0)
                return "crushed-building-delay-unresolved-threshold";
            if (state.CrushedCounter != 0 && state.CrushedCounter + 1 < state.CrushedDelay)
                return "crushed-building-delay";
            if (state.Gold < NormalGoldThresholdExclusive && state.BuildCounter + 1 < state.BuildRate)
                return "build-rate";
            if (state.PauseCounter != 0)
                return "aiv-pause-countdown";
            if (state.HighestPreparedFrame <= 0)
                return "no-prepared-frames";
            if (state.CurrentStepGoal <= 0)
                return "step-goal-not-released";
            return "scheduler-work-eligible";
        }
    }

    internal sealed class FirstBuildingWindow
    {
        private readonly TimeSpan followUp;

        public FirstBuildingWindow(TimeSpan followUp) => this.followUp = followUp;

        public DateTime? ConfirmedAtUtc { get; private set; }

        public bool TryConfirm(DateTime nowUtc, bool executeSucceeded, bool matchingBuildingSpawn,
            bool frameStatusAdvanced)
        {
            // A frame transition alone can also be a wall, moat, or pure AIV command.
            // Only the low-level building-spawn event is a safe building confirmation.
            if (ConfirmedAtUtc.HasValue || !executeSucceeded || !matchingBuildingSpawn)
                return false;
            ConfirmedAtUtc = nowUtc;
            return true;
        }

        public bool FollowUpComplete(DateTime nowUtc) =>
            ConfirmedAtUtc.HasValue && nowUtc - ConfirmedAtUtc.Value >= followUp;
    }

    internal static class DiagnosticChunker
    {
        public static string[] Split(string value, int maximumLength)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (maximumLength <= 0) throw new ArgumentOutOfRangeException(nameof(maximumLength));
            if (value.Length == 0) return new[] { string.Empty };
            string[] result = new string[(value.Length + maximumLength - 1) / maximumLength];
            for (int index = 0; index < result.Length; index++)
            {
                int start = index * maximumLength;
                result[index] = value.Substring(start, Math.Min(maximumLength, value.Length - start));
            }
            return result;
        }
    }
}
