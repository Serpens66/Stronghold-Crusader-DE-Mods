using BugfixesAndQoL.Diagnostics;
using System;

namespace AIBuildDiagnoseTest
{
    internal sealed class AiBuildObservationSink : IAiBuildDiagnosticSink
    {
        internal static readonly AiBuildObservationSink Instance = new AiBuildObservationSink();
        public bool HasObserver => AiBuildDiagnostic.HasObserver;
        public bool ShouldDeferWoodBuild(int playerId) => AiBuildDiagnostic.ShouldDeferWoodBuild(playerId);
        public void Publish(string stage, int playerId, long a, long b, long c, long d) =>
            AiBuildDiagnostic.Publish(stage, playerId, a, b, c, d);
        public void PublishEconomyGridEvidence(string stage, ulong state, int mode) =>
            AiBuildDiagnostic.PublishEconomyGridEvidence(stage, state, mode);
        public long BeginWoodAttempt(int playerId) => AiBuildDiagnostic.BeginWoodAttempt(playerId);
        public void EndWoodAttempt(long id) => AiBuildDiagnostic.EndWoodAttempt(id);
        public bool TryGetCurrentWoodAttempt(out long id, out int playerId) =>
            AiBuildDiagnostic.TryGetCurrentWoodAttempt(out id, out playerId);
        public Action BeginNearbyWoodObservation(ulong state, int playerId, int x, int y) =>
            AiBuildDiagnostic.BeginNearbyWoodObservation(state, playerId, x, y);
        public void EndNearbyWoodObservation(Action restore, ulong state, int playerId, int x, int y) =>
            AiBuildDiagnostic.EndNearbyWoodObservation(restore, state, playerId, x, y);
    }
}
