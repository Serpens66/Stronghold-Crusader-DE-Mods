using System.Threading;
using System.Runtime.InteropServices;

namespace PerfProbe
{
    // Run tracking is shared with timing probes. The former independent walker is
    // replaced by the same lock-free raw capture and analysis pipeline as regular sampling.
    internal static class PerfProbeSimSampler
    {
        private static int simThreadId;
        private static long runStart;
        internal static int SimThreadId => Volatile.Read(ref simThreadId);
        internal static long RunStart => Volatile.Read(ref runStart);
        internal static void Install()
        {
            if (!PerfProbeThreadSampler.IsEnabled) PerfProbeThreadSampler.InstallLongRunSampler();
        }
        internal static void OnRunBegin(long stamp)
        {
            if (simThreadId == 0) simThreadId = (int)GetCurrentThreadId();
            Volatile.Write(ref runStart, stamp);
        }
        internal static void OnRunEnd() => Volatile.Write(ref runStart, 0);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    }
}
