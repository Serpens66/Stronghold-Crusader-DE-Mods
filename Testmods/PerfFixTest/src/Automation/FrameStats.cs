using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PerfFixTest.Automation
{
    // Frame-interval statistics of one hold phase, measured by the automation itself so a
    // comparison does not depend on PerfProbe (and can run without it).
    internal sealed class FrameStats
    {
        private readonly List<double> intervals = new List<double>(8192);

        internal void Add(double milliseconds) => intervals.Add(milliseconds);

        internal void Clear() => intervals.Clear();

        internal string Summary()
        {
            if (intervals.Count == 0) return "frames=0";
            double[] sorted = intervals.OrderBy(v => v).ToArray();
            double total = sorted.Sum();
            CultureInfo c = CultureInfo.InvariantCulture;
            return string.Format(c, "frames={0} fps={1:0.0} meanMs={2:0.000} p50Ms={3:0.000} p95Ms={4:0.000} p99Ms={5:0.000} maxMs={6:0.0}",
                sorted.Length, sorted.Length * 1000.0 / total, total / sorted.Length, Percentile(sorted, 0.50),
                Percentile(sorted, 0.95), Percentile(sorted, 0.99), sorted[sorted.Length - 1]);
        }

        private static double Percentile(double[] sorted, double fraction) =>
            sorted[Math.Min(sorted.Length - 1, (int)Math.Floor(fraction * (sorted.Length - 1)))];
    }
}
