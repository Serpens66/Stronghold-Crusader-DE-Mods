using System;
using System.Diagnostics;
using System.Threading;

namespace PerfProbe
{
    public static class CadenceTests
    {
        public static string Run()
        {
            long step = Stopwatch.Frequency / 1000;
            using (var cadence = new PerfProbeCadence(step))
            {
                cadence.Wait(); cadence.ResetStats();
                long cpu = PerfProbeLockTiming.ThreadCpuTime(), start = Stopwatch.GetTimestamp();
                for (int i = 0; i < 400; i++) cadence.Wait();
                double elapsed = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                double used = (PerfProbeLockTiming.ThreadCpuTime() - cpu) / 10000.0;
                if (used > elapsed * .2) throw new Exception("Cadence uses more than 20% of one CPU: " + used + "/" + elapsed);
                if (cadence.Waits != 400 || cadence.LateTicks < 0 || cadence.MaxLateTicks < 0)
                    throw new Exception("Cadence coverage counters");
                Thread.Sleep(12); cadence.Wait();
                if (cadence.Missed < 1) throw new Exception("Overrun did not report missed intervals");
                cadence.ResetStats();
                if (cadence.Waits != 0 || cadence.Missed != 0) throw new Exception("Cadence window reset");
                string result = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "PASS cadence: mode={0}, wallMs={1:F1}, cpuMs={2:F3}; missed deadlines, reset and sleeping fallback.", cadence.Mode, elapsed, used);
                cadence.Dispose(); cadence.Wait();
                if (cadence.Mode != "sleep-degraded") throw new Exception("Timer failure fallback");
                using (var fallback = new PerfProbeCadence(step, true))
                { fallback.Wait(); if (fallback.Mode != "sleep-degraded") throw new Exception("Unavailable timer mode"); }
                return result;
            }
        }
    }
}
