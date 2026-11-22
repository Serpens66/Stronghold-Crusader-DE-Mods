#if SERP_DEVDIAG
namespace AIMarketOverbuyDiagnostic
{
    // Caller serializes this state with the capture gate. Requests are last-wins:
    // a session transition cannot reset a stack containing an in-flight original.
    internal struct TradeSessionState
    {
        private int depth, pending;
        private long session;
        private bool enabled;
        internal int Depth => depth;
        internal void Enter() { depth++; }
        internal void Leave() { depth--; }
        internal void RequestStart(long id, bool capture)
        { pending = 1; session = id; enabled = capture; }
        internal void RequestEnd() { pending = 1; enabled = false; }
        internal bool TakePending(out long id, out bool capture)
        {
            id = session; capture = enabled;
            if (depth != 0 || pending == 0) return false;
            pending = 0;
            return true;
        }
    }
}
#endif
