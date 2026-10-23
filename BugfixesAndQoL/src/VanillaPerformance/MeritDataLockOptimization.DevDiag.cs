using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace BugfixesAndQoL.VanillaPerformance
{
    internal static partial class MeritDataLockOptimization
    {
        static partial void DiagBegin(ref MeritReadState state)
        {
            state.Started = Stopwatch.GetTimestamp();
            state.Gc = GC.CollectionCount(0);
            state.CacheAge = -1;
        }

        static partial void DiagDecision(ref MeritReadState state, string reason, long cacheAge)
        {
            state.Decision = reason; state.CacheAge = cacheAge;
        }

        static partial void DiagEnd(ref MeritReadState state)
        {
            if (state.Started == 0) return;
            long ended = Stopwatch.GetTimestamp(), elapsed = ended - state.Started;
            // Literals avoid a new counter-name allocation for each UI lookup.
            // Count after our extra Monitor ownership has been released.
            switch (state.Decision)
            {
                case "disabled": Shared.DevDiag.Count("ui.merit.disabled"); break;
                case "cache-missing": Shared.DevDiag.Count("ui.merit.missing"); break;
                case "stale-served-refresh-requested": Shared.DevDiag.Count("ui.merit.staleServed"); break;
                case "simulation-refresh": Shared.DevDiag.Count("ui.merit.refreshAttempt"); break;
                case "lock-held-through-original": Shared.DevDiag.Count("ui.merit.lockHeld"); break;
                case "cache-served-lock-busy": Shared.DevDiag.Count("ui.merit.servedBusy"); break;
            }
            Shared.DevDiagRuntime.Add("ui.merit.cache-wrapper", 1, elapsed);
            if (elapsed < Stopwatch.Frequency / 10) return;
            // GC counts describe collections, never a measured GC pause duration.
            string detail = string.Format(CultureInfo.InvariantCulture,
                    "decision={0} servedCache={1} cacheAgeAtStartMs={2:F1} maxAgeMs={3:F1} enabledAtEnd={4} failedAtEnd={5} gcDuring={6} startStamp={7} endStamp={8}",
                state.Decision ?? "unknown", state.Served,
                state.CacheAge < 0 ? -1 : state.CacheAge * 1000.0 / Stopwatch.Frequency,
                MaxAgeTicks * 1000.0 / Stopwatch.Frequency, Volatile.Read(ref enabled) != 0,
                Volatile.Read(ref failed) != 0, GC.CollectionCount(0) - state.Gc, state.Started, ended);
            Shared.DevDiagRuntime.Spike("ui.merit.cache-wrapper", elapsed, detail);
        }
    }
}
