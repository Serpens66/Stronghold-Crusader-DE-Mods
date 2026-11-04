namespace Spectator
{
    // Buffer IDs are allocated before the native run, then delivered in order on Unity's thread.
    internal sealed class ReportRefreshState
    {
        internal bool Pending { get; private set; }
        internal long Generation { get; private set; }
        private long session;
        private object panel, accepted;
        private int player, page, firstFrame;
        internal void Begin(long sessionId, object view, int actor, int submode, int boundary)
        {
            Generation++; Pending = true; session = sessionId; panel = view;
            player = actor; page = submode; firstFrame = boundary; accepted = null;
        }
        internal bool IsCurrent(long sessionId, object view, int actor, int mode, int submode) =>
            Pending && session == sessionId && ReferenceEquals(panel, view) && player == actor && mode == 16 && page == submode;
        internal bool Observe(object snapshot, int frame)
        {
            // The counter is Int32; a six-buffer backlog stays far below half its range.
            if (!Pending || snapshot == null || unchecked(frame - firstFrame) < 0) return false;
            accepted = snapshot; return true;
        }
        internal bool HasFreshState(object snapshot) => Pending && accepted != null && ReferenceEquals(accepted, snapshot);
        internal void Cancel() { Generation++; Pending = false; panel = accepted = null; }
    }
}
