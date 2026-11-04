namespace Spectator
{
    // One pending refresh per panel. A close/replacement invalidates its entire generation.
    internal sealed class PanelRefreshState
    {
        internal bool Pending { get; private set; }
        internal long Generation { get; private set; }
        private long session;
        private object panel, state;
        internal void Begin(long sessionId, object view, object snapshot)
        { Generation++; session = sessionId; panel = view; state = snapshot; Pending = true; }
        internal bool IsCurrent(long sessionId, object view, bool open) =>
            Pending && open && sessionId == session && ReferenceEquals(panel, view);
        internal bool HasFreshState(object snapshot) => Pending && snapshot != null && !ReferenceEquals(state, snapshot);
        internal void Cancel() { Generation++; Pending = false; panel = state = null; }
    }
}
