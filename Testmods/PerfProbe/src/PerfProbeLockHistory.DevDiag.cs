using System;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;

namespace PerfProbe
{
    // One writer per lane; sequence validation makes concurrent readers fail explicitly
    // rather than reporting a torn or overwritten interval as a real owner.
    internal sealed class PerfProbeLockHistory
    {
        internal struct Span { internal long Started, Acquired, Ended; internal int Kind, Thread; internal bool NativeEntered; }
        private struct Slot { internal long Sequence; internal Span Value; }
        private readonly Slot[] slots;
        private long published;
        internal PerfProbeLockHistory(int capacity) { slots = new Slot[Math.Max(1, capacity)]; }
        internal void Add(Span value)
        {
            long sequence = published + 1;
            int index = (int)((sequence - 1) % slots.Length);
            Volatile.Write(ref slots[index].Sequence, -sequence);
            Thread.MemoryBarrier(); // Publish invalidation before overwriting any span field.
            slots[index].Value = value;
            Volatile.Write(ref slots[index].Sequence, sequence);
            Volatile.Write(ref published, sequence);
        }
        internal int Visit(long start, long end, Action<Span> visit, out bool clipped, out int lost)
        {
            long last = Volatile.Read(ref published), first = Math.Max(1, last - slots.Length + 1);
            clipped = last == 0; lost = 0; int count = 0;
            for (long sequence = first; sequence <= last; sequence++)
            {
                int index = (int)((sequence - 1) % slots.Length);
                long before = Volatile.Read(ref slots[index].Sequence);
                Span value = slots[index].Value;
                Thread.MemoryBarrier();
                if (before != sequence || Volatile.Read(ref slots[index].Sequence) != sequence) { lost++; continue; }
                if (sequence == first && first > 1 && value.Started > start) clipped = true;
                if (value.Acquired > end || value.Ended < start) continue;
                visit(value); count++;
            }
            return count;
        }
    }

    internal static class PerfProbeLockOwners
    {
        private static readonly PerfProbeLockHistory[] lanes = new PerfProbeLockHistory[8];
        [ThreadStatic] private static PerfProbeLockHistory own;
        [ThreadStatic] private static bool unavailableLane;
        private static int nextLane, unavailable;
        internal static void Record(long started, long acquired, long ended, int kind, bool native)
        {
            try
            {
            if (unavailableLane) return;
            if (own == null)
            {
                int index = Interlocked.Increment(ref nextLane) - 1;
                if (index >= lanes.Length) { unavailableLane = true; Interlocked.Increment(ref unavailable); return; }
                own = new PerfProbeLockHistory(32768);
                Volatile.Write(ref lanes[index], own);
            }
            own.Add(new PerfProbeLockHistory.Span { Started = started, Acquired = acquired, Ended = ended,
                Kind = kind, NativeEntered = native, Thread = Thread.CurrentThread.ManagedThreadId });
            }
            catch (Exception) { unavailableLane = true; Interlocked.Increment(ref unavailable); }
        }
        internal static string Describe(long start, long end)
        {
            long held = 0, longest = 0; int scopes = 0, native = 0, lost = 0; bool clipped = false;
            string owner = "unknown";
            var top = new PerfProbeLockHistory.Span[8];
            long minimum = Math.Max(start, end - Stopwatch.Frequency * 8);
            for (int i = 0; i < lanes.Length; i++)
            {
                var lane = Volatile.Read(ref lanes[i]); if (lane == null) continue;
                scopes += lane.Visit(minimum, end, value =>
                {
                    long overlap = Math.Max(0, Math.Min(end, value.Ended) - Math.Max(minimum, value.Acquired));
                    held += overlap; if (value.NativeEntered) native++;
                    for (int j = 0; j < top.Length; j++)
                        if (value.Ended - value.Acquired > top[j].Ended - top[j].Acquired)
                        { for (int k = top.Length - 1; k > j; k--) top[k] = top[k - 1]; top[j] = value; break; }
                    if (overlap > longest) { longest = overlap; owner = value.Kind == 1 ? "run" : value.Kind == 2 ? "GetMeritData" : value.Kind == 3 ? "SetAchData" : "unknown"; owner += "@" + value.Thread; }
                }, out bool laneClipped, out int laneLost);
                clipped |= laneClipped; lost += laneLost;
            }
            var rows = new List<string>(8);
            foreach (var value in top) if (value.Ended != 0)
                rows.Add(string.Format(CultureInfo.InvariantCulture,
                    "kind={0}:managedThread={1}:acquired={2}:released={3}:heldMs={4:F3}:nativeEntered={5}",
                    value.Kind == 1 ? "run" : value.Kind == 2 ? "GetMeritData" : value.Kind == 3 ? "SetAchData" : "unknown",
                    value.Thread, value.Acquired, value.Ended, (value.Ended - value.Acquired) * 1000.0 / Stopwatch.Frequency, value.NativeEntered));
            return string.Format(CultureInfo.InvariantCulture,
                "knownLockScopes={0} knownNativeScopes={1} knownHeldOverlapMs={2:F3} longestKnownOwner={3} " +
                "longestKnownOverlapMs={4:F3} unattributedMs={5:F3} lockHistoryClipped={6} lockHistoryLost={7} ownerCoverage=instrumented-completed-scopes-only",
                scopes, native, held * 1000.0 / Stopwatch.Frequency, owner, longest * 1000.0 / Stopwatch.Frequency,
                Math.Max(0, end - start - held) * 1000.0 / Stopwatch.Frequency,
                clipped || minimum > start, lost + Volatile.Read(ref unavailable)) +
                " largestKnownScopes=" + string.Join("|", rows) + " unlistedKnownScopes=" + Math.Max(0, scopes - rows.Count) +
                " unattributedOwner=unknown nativeEnteredIsNotNativeDuration=true";
        }
    }
}
