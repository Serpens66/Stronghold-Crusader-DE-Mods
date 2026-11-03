using System;

namespace Spectator
{
    // Consumer state, independent of the shared scheduler: geometry can run every frame while
    // status is sampled. A failed reconcile retains all idempotent UI work, never command payloads.
    internal sealed class PresentationRefreshState
    {
        internal bool ContentPending = true, RosterPending = true, StatePending = true;
        internal bool StatusPending = true, LayoutPending = true;
        private long nextStatus, retryAt;
        private bool perspectiveKnown, perspectiveActive;
        private int viewedPlayer;

        internal bool TakeStatusCheck(long now, long frequency)
        {
            if (!StatePending && now < nextStatus) return false;
            StatePending = false;
            nextStatus = now + Interval(frequency);
            return true;
        }

        internal bool ObservePerspective(bool active, int viewed)
        {
            if (perspectiveKnown && perspectiveActive == active && viewedPlayer == viewed) return false;
            perspectiveKnown = true;
            perspectiveActive = active;
            viewedPlayer = viewed;
            ContentPending = StatePending = StatusPending = LayoutPending = true;
            return true;
        }

        internal bool Ready(long now) => now >= retryAt;

        internal void Failed(long now, long frequency)
        {
            ContentPending = RosterPending = StatePending = StatusPending = LayoutPending = true;
            retryAt = now + Interval(frequency);
        }

        internal TimeSpan RetryDelay(long now, long frequency) =>
            now >= retryAt ? TimeSpan.Zero : TimeSpan.FromSeconds((retryAt - now) / (double)frequency);

        internal void Reset()
        {
            ContentPending = RosterPending = StatePending = StatusPending = LayoutPending = true;
            nextStatus = retryAt = 0;
            perspectiveKnown = perspectiveActive = false;
            viewedPlayer = 0;
        }

        private static long Interval(long frequency) => Math.Max(1, frequency / 10);
    }
}
