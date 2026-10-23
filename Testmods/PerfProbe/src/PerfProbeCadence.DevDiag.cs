using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace PerfProbe
{
    // Private timer: never changes the process/system timer resolution. A missed
    // deadline is counted, rather than followed by a burst of catch-up samples.
    internal sealed class PerfProbeCadence : IDisposable
    {
        private IntPtr timer;
        private readonly long interval;
        private long next;
        internal long LateTicks, MaxLateTicks, Missed, Waits;
        internal string Mode => timer == IntPtr.Zero ? "sleep-degraded" : "high-resolution-timer";
        internal PerfProbeCadence(long intervalTicks, bool forceFallback = false)
        {
            interval = Math.Max(1, intervalTicks); next = Stopwatch.GetTimestamp();
            if (!forceFallback)
                try { timer = CreateWaitableTimerExW(IntPtr.Zero, null, 2, 0x100002); }
                catch (EntryPointNotFoundException) { }
        }
        internal void Wait()
        {
            next += interval;
            long now = Stopwatch.GetTimestamp();
            if (now >= next)
            {
                long skipped = (now - next) / interval + 1;
                Missed += skipped; next += skipped * interval;
            }
            while ((now = Stopwatch.GetTimestamp()) < next)
            {
                long left = next - now;
                if (timer != IntPtr.Zero)
                {
                    long due = -Math.Max(1, (long)Math.Ceiling(left * 10000000.0 / Stopwatch.Frequency));
                    if (SetWaitableTimerEx(timer, ref due, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0) &&
                        WaitForSingleObject(timer, 1000) == 0) continue;
                    CloseHandle(timer); timer = IntPtr.Zero;
                }
                Thread.Sleep(Math.Max(1, (int)(left * 1000 / Stopwatch.Frequency)));
            }
            long late = Math.Max(0, now - next);
            LateTicks += late; if (late > MaxLateTicks) MaxLateTicks = late; Waits++;
        }
        internal void ResetStats() { LateTicks = MaxLateTicks = Missed = Waits = 0; }
        public void Dispose() { if (timer != IntPtr.Zero) { CloseHandle(timer); timer = IntPtr.Zero; } }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string name, uint flags, uint access);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetWaitableTimerEx(IntPtr timer, ref long due, int period, IntPtr callback, IntPtr argument, IntPtr reason, uint tolerance);
        [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(IntPtr handle, uint timeout);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    }
}
