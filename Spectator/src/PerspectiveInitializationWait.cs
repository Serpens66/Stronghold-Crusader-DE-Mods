using System;

namespace Spectator
{
    // A delayed mission snapshot is still useful. Elapsed time only emits support information;
    // completion, session replacement and explicit failure own the end of this wait.
    internal sealed class PerspectiveInitializationWait
    {
        internal const double WarningSeconds = 15;
        private double startedAt;

        internal long SessionId { get; private set; }
        internal bool Pending { get; set; }
        internal bool WarningIssued { get; private set; }

        internal void Begin(long session, double now)
        {
            SessionId = session;
            startedAt = now;
            Pending = true;
            WarningIssued = false;
        }

        internal void Reset()
        {
            SessionId = 0;
            startedAt = 0;
            Pending = false;
            WarningIssued = false;
        }

        internal bool CanAdvance(long currentSession, long readySession, object snapshot, object previous) =>
            Pending && SessionId > 0 && SessionId == currentSession && SessionId == readySession &&
            snapshot != null && (previous == null || !ReferenceEquals(snapshot, previous));

        internal bool TryWarn(double now)
        {
            if (!Pending || WarningIssued || now - startedAt < WarningSeconds) return false;
            WarningIssued = true;
            return true;
        }

        internal TimeSpan WarningDelay(double now)
        {
            if (!Pending || WarningIssued) return TimeSpan.Zero;
            double elapsed = Math.Max(0, now - startedAt);
            return TimeSpan.FromSeconds(Math.Max(0, WarningSeconds - elapsed));
        }
    }
}
