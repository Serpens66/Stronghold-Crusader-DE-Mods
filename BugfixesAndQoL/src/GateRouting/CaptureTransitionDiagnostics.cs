using System.Threading;

namespace BugfixesAndQoL.GateRouting
{
    internal sealed partial class DeferredCaptureRefreshRequest
    {
        // One atomic word contains both session identity and pending work. A delayed
        // callback from a previous map cannot recreate work after Reset/Begin.
        private long state = -1;
        private static long Session(int epoch) => (long)(uint)epoch << 1;
        internal void Begin(int epoch) => Interlocked.Exchange(ref state, Session(epoch));
        internal bool Request(int epoch)
        {
            long expected = Session(epoch);
            long previous = Interlocked.CompareExchange(ref state, expected | 1L, expected);
            return previous == expected || previous == (expected | 1L);
        }
        internal bool Consume(int epoch)
        {
            long expected = Session(epoch);
            return Interlocked.CompareExchange(ref state, expected, expected | 1L) == (expected | 1L);
        }
        internal void Reset() => Interlocked.Exchange(ref state, -1);
    }

    // Functional validation is independent of diagnostic history and cannot allocate.
    internal static class CaptureSnapshotMismatch
    {
        internal static bool RequiresRefresh(NativeGateAccessSnapshot source, long epoch, long publishedGeneration,
            int buildingId, uint globalId, int owner, int nativeCapturer)
        {
            if (source == null || source.DiagnosticEpoch != epoch || source.DiagnosticGeneration <= 0 ||
                source.DiagnosticGeneration > publishedGeneration || !source.MatchesGateIdentity(buildingId, globalId) ||
                buildingId < 0 || buildingId >= source.RecordsByBuildingId.Length || owner <= 0 || owner > 8 ||
                nativeCapturer < 0 || nativeCapturer > 8) return false;
            NativeGateAccessRecord old = source.RecordsByBuildingId[buildingId];
            return old.Valid && old.OwnerPlayerId == owner && old.CapturedByPlayerId != nativeCapturer;
        }
    }
}
