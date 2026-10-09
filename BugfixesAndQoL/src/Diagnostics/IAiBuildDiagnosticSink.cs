using System;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("AIBuildDiagnoseTest")]
[assembly: InternalsVisibleTo("AICoarsePathComponentFixTest")]

namespace BugfixesAndQoL.Diagnostics
{
    // Observation seam for this mod's existing hooks, not a general APIShared service.
    // Calls are synchronous on the native caller thread. A published sink lives until exit.
    internal interface IAiBuildDiagnosticSink
    {
        bool HasObserver { get; }
        bool ShouldDeferWoodBuild(int playerId);
        void Publish(string stage, int playerId, long a, long b, long c, long d);
        void PublishEconomyGridEvidence(string stage, ulong state, int mode);
        long BeginWoodAttempt(int playerId);
        void EndWoodAttempt(long id);
        bool TryGetCurrentWoodAttempt(out long id, out int playerId);
        Action BeginNearbyWoodObservation(ulong state, int playerId, int x, int y);
        void EndNearbyWoodObservation(Action restore, ulong state, int playerId, int x, int y);
    }
}
