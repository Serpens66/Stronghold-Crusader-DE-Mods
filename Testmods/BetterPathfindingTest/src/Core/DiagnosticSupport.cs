using System;

namespace BetterPathfindingTest.Core
{
    internal sealed class AllocationCounter
    {
        private Func<long> read;
        internal AllocationCounter(Func<Func<long>> factory)
        { try { read = factory(); } catch (Exception) { read = null; } }
        internal long Read()
        {
            if (read == null) return -1;
            try { return read(); } catch (Exception) { read = null; return -1; }
        }
    }
    internal static class RunGuard
    {
        internal static Exception Execute(Action work, Action cleanup)
        { try { work(); return null; } catch (Exception ex) { return ex; } finally { cleanup(); } }
    }
    internal static class RunPhases
    {
        internal static void Execute(bool hasPrevious, Action controlled, Action update, Action workloads)
        { controlled(); if (hasPrevious) update(); workloads(); }
    }
    internal sealed class ProgressPulse
    {
        private readonly Func<double> seconds;
        private readonly Action<string> output;
        private double next;
        internal string Phase = "begin";
        internal ProgressPulse(Func<double> seconds, Action<string> output) { this.seconds = seconds; this.output = output; next = seconds() + 5; }
        internal void Check() { double now = seconds(); if (now < next) return; next = now + 5; output("phase=" + Phase + " elapsedMs=" + (now * 1000).ToString("F0", System.Globalization.CultureInfo.InvariantCulture)); }
    }
}
