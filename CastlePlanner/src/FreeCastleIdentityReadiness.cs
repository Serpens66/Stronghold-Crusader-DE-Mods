namespace CastlePlanner
{
    internal enum FreeCastleIdentityStatus { Waiting, Ready, Spectator, Conflict, TimedOut }

    // The deadline starts at managed mission completion, never at an early native callback.
    internal sealed class FreeCastleIdentityReadiness
    {
        internal const double TimeoutSeconds = 30;
        private long sessionId;
        internal long SessionId => sessionId;
        private double startedAt;
        internal bool HasStarted { get; private set; }
        internal bool IsReady { get; private set; }

        internal void Begin(long session)
        {
            sessionId = session;
            HasStarted = false;
            IsReady = false;
            startedAt = 0;
        }

        internal void MarkStarted(long session, double now)
        {
            if (session != sessionId || HasStarted) return;
            HasStarted = true;
            startedAt = now;
        }

        internal FreeCastleIdentityStatus Evaluate(double now, bool resolved, bool conflict,
            bool spectator, bool rosterReady, bool currentPlayState = true)
        {
            if (!HasStarted) return FreeCastleIdentityStatus.Waiting;
            if (!currentPlayState)
                return now - startedAt >= TimeoutSeconds ? FreeCastleIdentityStatus.TimedOut : FreeCastleIdentityStatus.Waiting;
            if (conflict) return FreeCastleIdentityStatus.Conflict;
            if (spectator) return FreeCastleIdentityStatus.Spectator;
            if (now - startedAt >= TimeoutSeconds) return FreeCastleIdentityStatus.TimedOut;
            if (resolved && rosterReady)
            {
                IsReady = true;
                return FreeCastleIdentityStatus.Ready;
            }
            return FreeCastleIdentityStatus.Waiting;
        }
    }
}
