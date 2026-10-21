using System;
using System.Threading;

namespace Spectator
{
    // Process-permanent, mod-private owner seam; no dependency on a diagnostic consumer.
    internal static class AvailabilityObservation
    {
        private static Func<int, int, int, Action<int?, bool>> availability;
        private static long failures;
        private static int ready;
        internal static bool Ready => Volatile.Read(ref ready) != 0;
        internal static long CallbackFailures => Interlocked.Read(ref failures);
        internal static void ReportReady() => Volatile.Write(ref ready, 1);
        internal static bool Register(Func<int, int, int, Action<int?, bool>> callback) =>
            callback != null && Interlocked.CompareExchange(ref availability, callback, null) == null;
        internal static Action<int?, bool> Begin(int player, int mapper, int feedback)
        {
            try
            {
                var finish = Volatile.Read(ref availability)?.Invoke(player, mapper, feedback);
                if (finish == null) return null;
                return (result, vanilla) => { try { finish(result, vanilla); } catch { Interlocked.Increment(ref failures); } };
            }
            catch { Interlocked.Increment(ref failures); return null; }
        }
    }
}
