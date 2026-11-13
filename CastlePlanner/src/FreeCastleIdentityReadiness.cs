namespace CastlePlanner
{
    internal enum FreeCastleIdentityStatus { Waiting, Ready, Spectator, Conflict }

    // Managed completion can precede the first published PlayState by an arbitrary loading wait.
    internal sealed class FreeCastleIdentityReadiness
    {
        internal const double DelayNoticeSeconds = 30;
        private long sessionId;
        internal long SessionId => sessionId;
        private double startedAt;
        private bool delayReported;
        private APIShared.IMissionPlayStateReadinessCapability playStateReadiness;
        internal bool HasStarted { get; private set; }
        internal bool IsReady { get; private set; }
        internal bool IsCurrentPlayStateReady => sessionId > 0 &&
            playStateReadiness?.Current?.SessionId == sessionId;

        internal bool TryBindPlayStateReadiness(APIShared.IMissionPlayStateReadinessCapability capability,
            System.Action<APIShared.MissionPlayStateReadyNotification> onReady,
            out APIShared.NativeCapabilityDiagnostic diagnostic)
        {
            playStateReadiness = capability ?? throw new System.ArgumentNullException(nameof(capability));
            if (capability.TryRegisterObserver("FreeCastlePreview", onReady, out diagnostic)) return true;
            playStateReadiness = null;
            return false;
        }

        internal void Begin(long session)
        {
            sessionId = session;
            HasStarted = false;
            IsReady = false;
            startedAt = 0;
            delayReported = false;
        }

        internal void MarkStarted(long session, double now)
        {
            if (session != sessionId || HasStarted) return;
            HasStarted = true;
            startedAt = now;
        }

        internal bool ShouldReportDelay(double now)
        {
            if (!HasStarted || IsReady || delayReported || now - startedAt < DelayNoticeSeconds) return false;
            delayReported = true;
            return true;
        }

        internal FreeCastleIdentityStatus Evaluate(bool resolved, bool conflict,
            bool spectator, bool rosterReady)
        {
            if (!HasStarted || !IsCurrentPlayStateReady) return FreeCastleIdentityStatus.Waiting;
            if (conflict) return FreeCastleIdentityStatus.Conflict;
            if (spectator) return FreeCastleIdentityStatus.Spectator;
            if (resolved && rosterReady)
            {
                IsReady = true;
                return FreeCastleIdentityStatus.Ready;
            }
            return FreeCastleIdentityStatus.Waiting;
        }
    }
}
