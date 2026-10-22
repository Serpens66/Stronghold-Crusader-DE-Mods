using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace BugfixesAndQoL.VanillaPerformance
{
    internal struct MeritReadState
    {
        internal bool Served;
        internal long Started, CacheAge;
        internal int Gc;
        internal string Decision;
        public static implicit operator bool(MeritReadState state) => state.Served;
    }

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
            // Literals avoid a new counter-name allocation for each UI lookup.
            switch (reason)
            {
                case "disabled": Shared.DevDiag.Count("ui.merit.disabled"); break;
                case "cache-missing": Shared.DevDiag.Count("ui.merit.missing"); break;
                case "cache-expired": Shared.DevDiag.Count("ui.merit.expired"); break;
                case "lock-free-at-probe": Shared.DevDiag.Count("ui.merit.freeAtProbe"); break;
                case "cache-served-lock-busy": Shared.DevDiag.Count("ui.merit.servedBusy"); break;
            }
        }

        static partial void DiagEnd(ref MeritReadState state)
        {
            if (state.Started == 0) return;
            long ended = Stopwatch.GetTimestamp(), elapsed = ended - state.Started;
            Shared.DevDiagRuntime.Add("ui.merit.cache-wrapper", 1, elapsed);
            if (elapsed < Stopwatch.Frequency / 10) return;
            // No extra lock probe or cache policy change for diagnostics. A free-at-probe
            // decision can race with simulation acquisition; PerfProbe measures the later Enter.
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
