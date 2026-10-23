namespace BugfixesAndQoL.GateRouting
{
    // Render-thread state. Producers publish monotonic revisions after their error counters.
    // An unchanged revision skips the clock and snapshot work entirely.
    internal sealed class IntegrityErrorFlushGate
    {
        private long own, same, topology, next;

        internal bool HasChanges(long ownRevision, long sameRevision, long topologyRevision) =>
            own != ownRevision || same != sameRevision || topology != topologyRevision;

        internal bool TryReserve(long ownRevision, long sameRevision, long topologyRevision,
            long now, long interval, bool final)
        {
            if (!final && (!HasChanges(ownRevision, sameRevision, topologyRevision) || now < next)) return false;
            next = now + interval;
            // Acknowledge the revisions sampled BEFORE snapshots. New errors stay pending.
            own = ownRevision; same = sameRevision; topology = topologyRevision;
            return true;
        }

        internal void Reset(long ownRevision, long sameRevision, long topologyRevision)
        {
            own = ownRevision; same = sameRevision; topology = topologyRevision; next = 0;
        }
    }
}
