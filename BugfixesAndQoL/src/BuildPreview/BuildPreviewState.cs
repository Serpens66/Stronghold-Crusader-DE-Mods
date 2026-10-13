using System.Threading;
using SHCDESE.Interop;

namespace BugfixesAndQoL.BuildPreview
{
    // One immutable publication holds activation, mission and counts. Compare/exchange
    // makes retirement atomic: an in-flight producer cannot publish into a new generation.
    internal sealed class BuildPreviewState
    {
        internal readonly bool Enabled;
        internal readonly long Generation, Session;
        internal readonly GroundSnapshot Snapshot;
        internal BuildPreviewState(bool enabled, long generation, long session, GroundSnapshot snapshot)
        { Enabled = enabled; Generation = generation; Session = session; Snapshot = snapshot; }
    }

    internal sealed class BuildPreviewStateStore
    {
        private BuildPreviewState state = new BuildPreviewState(false, 0, 0, null);
        internal BuildPreviewState Read() => Volatile.Read(ref state);
        internal static bool Effective(bool client, bool setting) => client && setting;
        internal void SetEnabled(bool enabled)
        {
            while (true)
            {
                var old = Read();
                if (old.Enabled == enabled) return;
                var next = new BuildPreviewState(enabled, old.Generation + 1, old.Session, null);
                if (ReferenceEquals(Interlocked.CompareExchange(ref state, next, old), old)) return;
            }
        }
        internal void SetSession(long session)
        {
            while (true)
            {
                var old = Read();
                var next = new BuildPreviewState(old.Enabled, old.Generation + 1, session, null);
                if (ReferenceEquals(Interlocked.CompareExchange(ref state, next, old), old)) return;
            }
        }
        internal bool IsCurrent(BuildPreviewState capture)
        {
            var current = Read();
            return capture.Enabled && current.Enabled && current.Session != 0 &&
                current.Generation == capture.Generation && current.Session == capture.Session;
        }
        internal void Clear(BuildPreviewState capture)
        {
            while (true)
            {
                var old = Read();
                if (old.Generation != capture.Generation || old.Session != capture.Session || old.Snapshot == null) return;
                var next = new BuildPreviewState(old.Enabled, old.Generation, old.Session, null);
                if (ReferenceEquals(Interlocked.CompareExchange(ref state, next, old), old)) return;
            }
        }
        internal void Publish(BuildPreviewState capture, eMappers mapper, int x, int y, GroundCounts counts)
        {
            while (true)
            {
                var old = Read();
                if (!old.Enabled || old.Session == 0 || old.Generation != capture.Generation || old.Session != capture.Session) return;
                var value = old.Snapshot;
                if (value != null && value.Mapper == mapper && value.X == x && value.Y == y &&
                    value.Counts.Coverage == counts.Coverage && value.Counts.Green == counts.Green && value.Counts.Resource == counts.Resource) return;
                value = new GroundSnapshot(old.Session, mapper, x, y, counts, old.Generation);
                var next = new BuildPreviewState(old.Enabled, old.Generation, old.Session, value);
                if (ReferenceEquals(Interlocked.CompareExchange(ref state, next, old), old)) return;
            }
        }
    }
}
