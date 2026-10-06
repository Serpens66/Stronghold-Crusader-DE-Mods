using System.Threading;

namespace Shared
{
    // Event callbacks only request work. The existing deferred publisher performs
    // the native reads after simulation callbacks have returned.
    internal sealed class DeferredSnapshotRefreshRequest
    {
        private int pending;
        internal void Request(bool mapActive, bool post)
        {
            if (mapActive && post) Interlocked.Exchange(ref pending, 1);
        }
        internal bool Consume() => Interlocked.Exchange(ref pending, 0) != 0;
        internal void Reset() => Interlocked.Exchange(ref pending, 0);
    }
}
