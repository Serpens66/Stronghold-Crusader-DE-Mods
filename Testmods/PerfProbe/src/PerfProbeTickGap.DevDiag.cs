using System;
using System.Threading;

namespace PerfProbe
{
    // Game-time gap record and its pure classification. No Unity/Extender types, so the in-memory
    // helper tests execute exactly this code.
    internal struct PerfProbeTickGap
    {
        internal int Source; // 0 = Extender OnTick, 1 = native elapsed-map-tick counter
        internal long Previous, ActiveStart, End, Expected, HaltStamp;
        internal int TickBefore, TickAfter, HaltKind;
        internal long RunCalls, NativeCalls, SkipCalls, NoBuffer, BusyTicks, Frames, MapTicks;
        // Vanilla pacing (-1 = timer probe unavailable).
        internal long TimerCalls, TimerMaxInterval, FramesToProcessMax, FramesToProcessEnd;

        // Tick numbers restart with every mission, so a smaller number ends the interval without a gap.
        // Pauses and loading are observed on the main thread (lastHalt = stamp of the last frame that
        // saw them); only the part after that frame counts, so a stall right after unpausing or
        // loading is still reported. The expected tick interval (game speed) is not a stall.
        internal static bool Classify(long previous, long now, long lastHalt, long expected, long threshold,
            int tickBefore, int tickAfter, out long activeStart)
        {
            activeStart = previous;
            if (threshold <= 0 || previous == 0 || now <= previous || tickAfter < tickBefore) return false;
            if (lastHalt > previous) activeStart = Math.Min(lastHalt, now);
            return now - activeStart >= threshold + Math.Max(0, expected);
        }

        // Excluded = long only because of a pause/load in between (counted, no spike line).
        internal static bool ExcludedByHalt(long previous, long now, long lastHalt, long expected, long threshold) =>
            threshold > 0 && previous != 0 && lastHalt > previous && now - previous >= threshold + Math.Max(0, expected);
    }

    // Sim thread produces, main thread drains once per frame. Gaps are rare, so the lock is only
    // taken on a gap or when the count shows pending entries; overflow is counted, never waits.
    internal sealed class PerfProbeGapQueue
    {
        private readonly PerfProbeTickGap[] items;
        private readonly object gate = new object();
        private int head, count, dropped;

        internal PerfProbeGapQueue(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            items = new PerfProbeTickGap[capacity];
        }

        internal int Dropped => Volatile.Read(ref dropped);

        internal bool TryEnqueue(PerfProbeTickGap gap)
        {
            lock (gate)
            {
                if (count == items.Length) { dropped++; return false; }
                items[(head + count) % items.Length] = gap;
                Volatile.Write(ref count, count + 1);
                return true;
            }
        }

        internal bool TryDequeue(out PerfProbeTickGap gap)
        {
            gap = default(PerfProbeTickGap);
            if (Volatile.Read(ref count) == 0) return false;
            lock (gate)
            {
                if (count == 0) return false;
                gap = items[head];
                head = (head + 1) % items.Length;
                count--;
                return true;
            }
        }
    }
}
