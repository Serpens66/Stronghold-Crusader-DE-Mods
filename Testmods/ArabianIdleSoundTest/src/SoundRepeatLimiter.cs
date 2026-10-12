using System;
using System.Diagnostics;

namespace ArabianIdleSoundTest
{
    // One window for all matching requests, not one window per unit or source.
    // No native identity is available; measure real elapsed time, including pauses.
    internal sealed class SoundRepeatLimiter
    {
        private readonly object gate = new object();
        private readonly int milliseconds;
        private bool acquired;
        private long last;
        internal SoundRepeatLimiter(int milliseconds)
        {
            if (milliseconds < 0 || milliseconds > 60000) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            this.milliseconds = milliseconds;
        }
        internal bool TryAcquire(long now)
        {
            if (milliseconds == 0) return true;
            lock (gate) {
                if (acquired && now >= last && (now - last) * (1000d / Stopwatch.Frequency) < milliseconds) return false;
                acquired = true; last = now; return true;
            }
        }
    }
}
