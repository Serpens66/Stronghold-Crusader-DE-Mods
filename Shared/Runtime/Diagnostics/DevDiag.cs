using System;
using System.Diagnostics;
using BepInEx.Logging;
#if SERP_DEVDIAG
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using System.Threading;
#endif

#if SERP_DEVDIAG
// Release gates reject every assembly that carries this marker (see AGENTS/build-release.md).
[assembly: System.Reflection.AssemblyMetadata("SerpDevDiagnostics", "true")]
#endif

namespace Shared
{
    // Development-only measurement points. Every method is [Conditional]: without the
    // SERP_DEVDIAG symbol the compiler removes each call including its argument
    // evaluation, so normal and release builds contain no DevDiag call and no timing.
    // Enable a build with build.bat /devdiag; release and pack drivers refuse the marker.
    // Names should be string literals: the slot lookup compares references first.
    internal static class DevDiag
    {
        [Conditional("SERP_DEVDIAG")]
        internal static void BeginBatch()
        {
#if SERP_DEVDIAG
            DevDiagRuntime.BeginBatch();
#endif
        }
        [Conditional("SERP_DEVDIAG")]
        internal static void EndBatch()
        {
#if SERP_DEVDIAG
            DevDiagRuntime.EndBatch();
#endif
        }
        // Writes aggregated "DEVDIAG perf:" lines to this logger every few seconds.
        [Conditional("SERP_DEVDIAG")]
        internal static void Attach(ManualLogSource log)
        {
#if SERP_DEVDIAG
            DevDiagRuntime.Attach(log);
#endif
        }

        [Conditional("SERP_DEVDIAG")]
        internal static void Count(string name)
        {
#if SERP_DEVDIAG
            DevDiagRuntime.Add(name, 1, 0);
#endif
        }

        [Conditional("SERP_DEVDIAG")]
        internal static void Count(string name, long amount)
        {
#if SERP_DEVDIAG
            DevDiagRuntime.Add(name, amount, 0);
#endif
        }

        // Pair with Stop on the same local: long t = 0; DevDiag.Start(ref t); ... DevDiag.Stop(ref t, "x");
        [Conditional("SERP_DEVDIAG")]
        internal static void Start(ref long started)
        {
#if SERP_DEVDIAG
            started = Stopwatch.GetTimestamp();
#endif
        }

        [Conditional("SERP_DEVDIAG")]
        internal static void Stop(ref long started, string name)
        {
#if SERP_DEVDIAG
            if (started == 0) return;
            DevDiagRuntime.Add(name, 1, Stopwatch.GetTimestamp() - started);
            started = 0;
#endif
        }

        // Like Stop, and additionally writes one immediate "DEVDIAG spike:" line with start/end
        // wall clock and thread when this single call took at least spikeMicros.
        [Conditional("SERP_DEVDIAG")]
        internal static void StopSpike(ref long started, string name, long spikeMicros)
        {
#if SERP_DEVDIAG
            if (started == 0) return;
            long elapsed = Stopwatch.GetTimestamp() - started;
            started = 0;
            DevDiagRuntime.Add(name, 1, elapsed);
            if (elapsed * 1000000 >= spikeMicros * Stopwatch.Frequency)
                DevDiagRuntime.Spike(name, elapsed, null);
#endif
        }

        // StopSpike with context; the detail factory runs only when the spike line is written.
        [Conditional("SERP_DEVDIAG")]
        internal static void StopSpike(ref long started, string name, long spikeMicros, Func<string> detail)
        {
#if SERP_DEVDIAG
            if (started == 0) return;
            long elapsed = Stopwatch.GetTimestamp() - started;
            started = 0;
            DevDiagRuntime.Add(name, 1, elapsed);
            if (elapsed * 1000000 < spikeMicros * Stopwatch.Frequency) return;
            string text;
            try { text = detail?.Invoke(); }
            catch (Exception ex) { text = "detailFailed=" + ex.GetType().Name; }
            DevDiagRuntime.Spike(name, elapsed, text);
#endif
        }

        // One-off development message with ms timestamp; the factory runs only in DEVDIAG builds.
        [Conditional("SERP_DEVDIAG")]
        internal static void Log(Func<string> message)
        {
#if SERP_DEVDIAG
            DevDiagRuntime.Log(message);
#endif
        }
    }

#if SERP_DEVDIAG
    internal static class DevDiagRuntime
    {
        private const int Capacity = 128;
        private static readonly long FlushInterval = Stopwatch.Frequency * 5;
        private static readonly object Sync = new object();
        private static readonly string[] Names = new string[Capacity];
        private static readonly long[] Calls = new long[Capacity];
        private static readonly long[] Ticks = new long[Capacity];
        private static readonly long[] MaxTicks = new long[Capacity];
        private static int used;
        private static long nextFlush;
        private static long windowStart;
        private static ManualLogSource logger;
        private static long rejectedAdds;
        [ThreadStatic] private static Dictionary<string, int> localSlots;
        [ThreadStatic] private static long[] batchCalls, batchTicks, batchMax;
        [ThreadStatic] private static int batchDepth, pendingCount;
        [ThreadStatic] private static PendingSpike[] pending;
        private struct PendingSpike { internal string Name, Detail; internal long Elapsed; internal DateTime End; internal int Thread; }
        internal static void BeginBatch()
        {
            if (batchCalls == null)
            {
                batchCalls = new long[Capacity]; batchTicks = new long[Capacity]; batchMax = new long[Capacity];
                pending = new PendingSpike[SpikeLinesPerWindow];
            }
            batchDepth++;
        }
        internal static void EndBatch()
        {
            if (batchDepth == 0 || --batchDepth != 0) return;
            for (int i = 0; i < Capacity; i++)
            {
                if (batchCalls[i] != 0 || batchTicks[i] != 0) Merge(i, batchCalls[i], batchTicks[i], batchMax[i]);
                batchCalls[i] = batchTicks[i] = batchMax[i] = 0;
            }
            for (int i = 0; i < pendingCount; i++)
            {
                var item = pending[i]; pending[i] = default;
                WriteSpike(item.Name, item.Elapsed, item.Detail, item.End, item.Thread);
            }
            pendingCount = 0; MaybeFlush();
        }

        internal static void Attach(ManualLogSource log)
        {
            if (log == null) return;
            if (Interlocked.CompareExchange(ref logger, log, null) == null)
            {
                long now = Stopwatch.GetTimestamp();
                Volatile.Write(ref windowStart, now);
                Volatile.Write(ref nextFlush, now + FlushInterval);
                Write("DEVDIAG active: development measurement build, never a release.");
            }
        }

        internal static void Add(string name, long amount, long elapsed)
        {
            if (name == null) return;
            if (localSlots == null) localSlots = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!localSlots.TryGetValue(name, out int slot))
            { slot = Slot(name); if (slot >= 0 && localSlots.Count < Capacity) localSlots[name] = slot; }
            if (slot < 0) { Interlocked.Increment(ref rejectedAdds); if (batchDepth == 0) MaybeFlush(); return; }
            if (batchDepth != 0)
            {
                batchCalls[slot] += amount; batchTicks[slot] += elapsed;
                if (elapsed > batchMax[slot]) batchMax[slot] = elapsed;
                return;
            }
            Merge(slot, amount, elapsed, elapsed); MaybeFlush();
        }
        private static void Merge(int slot, long amount, long elapsed, long max)
        {
            Interlocked.Add(ref Calls[slot], amount);
            if (elapsed > 0)
            {
                Interlocked.Add(ref Ticks[slot], elapsed);
                long observed;
                while (max > (observed = Volatile.Read(ref MaxTicks[slot])) &&
                    Interlocked.CompareExchange(ref MaxTicks[slot], max, observed) != observed) { }
            }
        }
        private static void MaybeFlush()
        {
            long now = Stopwatch.GetTimestamp();
            long due = Volatile.Read(ref nextFlush);
            if (due != 0 && now >= due &&
                Interlocked.CompareExchange(ref nextFlush, now + FlushInterval, due) == due)
                Flush(now);
        }

        // At most SpikeLinesPerWindow immediate lines per flush window; the rest is counted
        // and reported as spikes.suppressed so a long freeze cannot flood the log.
        private const int SpikeLinesPerWindow = 60;
        private static int spikeLines;

        internal static void Spike(string name, long elapsed, string detail)
        {
            if (Interlocked.Increment(ref spikeLines) > SpikeLinesPerWindow)
            {
                Add("spikes.suppressed", 1, 0);
                return;
            }
            if (batchDepth != 0)
            {
                if (pendingCount < pending.Length) pending[pendingCount++] = new PendingSpike
                { Name = name, Elapsed = elapsed, Detail = detail, End = DateTime.Now, Thread = Thread.CurrentThread.ManagedThreadId };
                else Add("spikes.suppressed", 1, 0);
                return;
            }
            WriteSpike(name, elapsed, detail, DateTime.Now, Thread.CurrentThread.ManagedThreadId);
        }
        private static void WriteSpike(string name, long elapsed, string detail, DateTime end, int thread)
        {
            DateTime start = end.AddTicks(-(long)(elapsed * (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency));
            CultureInfo c = CultureInfo.InvariantCulture;
            Write("DEVDIAG spike: " + name + " ms=" + (elapsed * 1000.0 / Stopwatch.Frequency).ToString("F1", c) +
                " start=" + start.ToString("HH:mm:ss.fff", c) + " end=" + end.ToString("HH:mm:ss.fff", c) +
                " thread=" + thread.ToString(c) +
                (string.IsNullOrEmpty(detail) ? "" : " " + detail));
        }

        internal static void Log(Func<string> message)
        {
            if (message == null) return;
            try { Write("DEVDIAG: " + message()); }
            catch (Exception ex) { Write("DEVDIAG message failed: " + ex.GetType().Name); }
        }

        private static int Slot(string name)
        {
            if (name == null) return -1;
            int count = Volatile.Read(ref used);
            for (int index = 0; index < count; index++)
                if (ReferenceEquals(Names[index], name)) return index;
            lock (Sync)
            {
                for (int index = 0; index < used; index++)
                    if (string.Equals(Names[index], name, StringComparison.Ordinal)) return index;
                if (used >= Capacity) return -1;
                Names[used] = name;
                Volatile.Write(ref used, used + 1);
                return used - 1;
            }
        }

        private static void Flush(long now)
        {
            long start = Interlocked.Exchange(ref windowStart, now);
            Interlocked.Exchange(ref spikeLines, 0);
            double seconds = Math.Max(1e-9, (now - start) / (double)Stopwatch.Frequency);
            int count = Volatile.Read(ref used);
            var line = new StringBuilder(64 + count * 64);
            line.Append("DEVDIAG perf: window=").Append(seconds.ToString("F1", CultureInfo.InvariantCulture)).Append('s');
            line.Append(" rejectedCounterAdds=").Append(Interlocked.Exchange(ref rejectedAdds, 0));
            for (int index = 0; index < count; index++)
            {
                long calls = Interlocked.Exchange(ref Calls[index], 0);
                long ticks = Interlocked.Exchange(ref Ticks[index], 0);
                long max = Interlocked.Exchange(ref MaxTicks[index], 0);
                if (calls == 0 && ticks == 0) continue;
                line.Append(" | ").Append(Names[index])
                    .Append(" calls/s=").Append((calls / seconds).ToString("F0", CultureInfo.InvariantCulture));
                if (ticks > 0)
                    line.Append(" sumMs=").Append((ticks * 1000.0 / Stopwatch.Frequency).ToString("F2", CultureInfo.InvariantCulture))
                        .Append(" msPerS=").Append((ticks * 1000.0 / Stopwatch.Frequency / seconds).ToString("F2", CultureInfo.InvariantCulture))
                        .Append(" maxUs=").Append((max * 1000000.0 / Stopwatch.Frequency).ToString("F0", CultureInfo.InvariantCulture));
            }
            Write(line.ToString());
        }

        private static void Write(string text)
        {
            ManualLogSource log = Volatile.Read(ref logger);
            if (log == null) return;
            try { log.LogMessage("[" + DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "] " + text); }
            catch { }
        }
    }
#endif
}
