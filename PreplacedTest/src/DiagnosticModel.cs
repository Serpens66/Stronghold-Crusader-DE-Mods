using System;
using System.Collections.Generic;
using System.Linq;

namespace PreplacedTest
{
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
