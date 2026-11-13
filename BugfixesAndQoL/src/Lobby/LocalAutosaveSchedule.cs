using System;

namespace BugfixesAndQoL
{
    internal enum LocalAutosaveAttemptResult { Deferred, Saved, Failed }

    // Main-thread state only. Timestamps/frequency are supplied so tests exercise this
    // production clock without sleeping or depending on Unity's scaled game time.
    internal sealed class LocalAutosaveSchedule
    {
        private readonly long frequency;
        private long lastSample, retryAt;
        private bool lastActive, enabled, queued;
        internal long SessionId { get; }
        internal long Generation { get; private set; }
        internal int Minutes { get; private set; }
        internal long ActiveElapsed { get; private set; }

        internal LocalAutosaveSchedule(long sessionId, int minutes, long now, long frequency, bool enabled)
        {
            if (frequency <= 0 || frequency > long.MaxValue / (1440L * 60))
                throw new ArgumentOutOfRangeException(nameof(frequency));
            ValidateMinutes(minutes);
            SessionId = sessionId;
            Minutes = minutes;
            this.frequency = frequency;
            this.enabled = enabled;
            lastSample = now;
        }

        internal void Sample(long now, bool active)
        {
            active = active && enabled && Minutes > 0;
            if (now >= lastSample && active && lastActive)
            {
                long delta = now - lastSample;
                ActiveElapsed = delta > long.MaxValue - ActiveElapsed ? long.MaxValue : ActiveElapsed + delta;
            }
            lastSample = now;
            lastActive = active;
        }

        internal void SetMinutes(int minutes, long now)
        {
            ValidateMinutes(minutes);
            if (minutes == Minutes) return;
            Sample(now, false);
            bool reset = minutes == 0 || Minutes == 0;
            Minutes = minutes;
            InvalidatePending();
            if (reset) { ActiveElapsed = 0; retryAt = 0; }
        }

        internal void SetEnabled(bool value, long now)
        {
            if (value == enabled) return;
            enabled = value;
            ActiveElapsed = 0;
            retryAt = 0;
            Sample(now, false);
            InvalidatePending();
        }

        internal void Cancel(long now)
        {
            Sample(now, false);
            enabled = false;
            InvalidatePending();
        }

        internal bool IsDue(long now) => enabled && Minutes > 0 &&
            ActiveElapsed >= Minutes * 60L * frequency && now >= retryAt;

        internal bool TryQueue(long now, out long generation)
        {
            generation = Generation;
            if (queued || !IsDue(now)) return false;
            queued = true;
            return true;
        }

        internal bool IsCurrentAttempt(long generation, long now) =>
            queued && generation == Generation && IsDue(now);

        internal void Complete(long generation, LocalAutosaveAttemptResult result, long now)
        {
            if (!queued || generation != Generation) return;
            queued = false;
            // Saving (including a blocked or failed writer) never counts as active time.
            Sample(now, false);
            if (result == LocalAutosaveAttemptResult.Saved)
            {
                ActiveElapsed = 0;
                retryAt = 0;
            }
            else
            {
                long delay = result == LocalAutosaveAttemptResult.Failed ? Minutes * 60L * frequency : frequency;
                retryAt = now > long.MaxValue - delay ? long.MaxValue : now + delay;
            }
        }

        private void InvalidatePending() { Generation++; queued = false; }
        private static void ValidateMinutes(int minutes)
        {
            if (minutes < 0 || minutes > 1440) throw new ArgumentOutOfRangeException(nameof(minutes));
        }
    }
}
