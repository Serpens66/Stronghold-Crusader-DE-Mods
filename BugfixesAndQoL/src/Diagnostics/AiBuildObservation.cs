using BepInEx.Logging;
using System;
using System.Threading;

namespace BugfixesAndQoL.Diagnostics
{
    // Optional, process-rooted subscriber to this mod's observation points.
    // No hook installation, memory snapshots or test-save policy belongs in this bridge.
    internal static class AiBuildObservation
    {
        private static readonly object Sync = new object();
        private static IAiBuildDiagnosticSink sink;
        private static ManualLogSource log;

        internal static bool TryRegister(IAiBuildDiagnosticSink candidate, ManualLogSource logger, out string error)
        {
            error = null;
            if (candidate == null) { error = "A diagnostic sink is required."; return false; }
            lock (Sync)
            {
                if (sink != null)
                {
                    if (ReferenceEquals(sink, candidate)) return true;
                    error = "A different AI build diagnostic sink is already registered.";
                    return false;
                }
                log = logger;
                Volatile.Write(ref sink, candidate);
                return true;
            }
        }
        internal static bool HasObserver => Volatile.Read(ref sink)?.HasObserver == true;
        internal static bool ShouldDeferWoodBuild(int playerId) =>
            Volatile.Read(ref sink)?.ShouldDeferWoodBuild(playerId) == true;
        internal static void Publish(string stage, int playerId, long a = 0, long b = 0, long c = 0, long d = 0) =>
            Volatile.Read(ref sink)?.Publish(stage, playerId, a, b, c, d);
        internal static void PublishEconomyGridEvidence(string stage, ulong state, int mode) =>
            Volatile.Read(ref sink)?.PublishEconomyGridEvidence(stage, state, mode);
        internal static long BeginWoodAttempt(int playerId) => Volatile.Read(ref sink)?.BeginWoodAttempt(playerId) ?? 0;
        internal static void EndWoodAttempt(long id) => Volatile.Read(ref sink)?.EndWoodAttempt(id);
        internal static bool TryGetCurrentWoodAttempt(out long id, out int playerId)
        {
            var current = Volatile.Read(ref sink);
            if (current != null) return current.TryGetCurrentWoodAttempt(out id, out playerId);
            id = 0; playerId = 0; return false;
        }
        internal static Action BeginNearbyWoodObservation(ulong state, int playerId, int x, int y) =>
            Volatile.Read(ref sink)?.BeginNearbyWoodObservation(state, playerId, x, y);
        internal static void EndNearbyWoodObservation(Action restore, ulong state, int playerId, int x, int y)
        {
            var current = Volatile.Read(ref sink);
            if (current != null) { current.EndNearbyWoodObservation(restore, state, playerId, x, y); return; }
            try { restore?.Invoke(); }
            catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "AI nearby wood diagnostic restore failed: " + ex); }
        }
    }
}
