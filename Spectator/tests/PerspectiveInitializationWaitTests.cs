using System;

namespace Spectator
{
    // Runs the production readiness wait. The fixture controls publication and session identity;
    // it does not simulate Vanilla's engine or prove native report rendering.
    internal static class PerspectiveInitializationWaitTests
    {
        internal static void Run(Action<bool, string> check)
        {
            var wait = new PerspectiveInitializationWait();
            var previous = new object();
            var fresh = new object();
            check(!wait.CanAdvance(1, 1, fresh, null) && !wait.TryWarn(90) &&
                wait.WarningDelay(90) == TimeSpan.Zero, "an inactive wait cannot publish readiness or schedule diagnostics");

            foreach (double delay in new[] { 16d, 35d, 90d })
            {
                wait.Begin(1, 100);
                check(wait.TryWarn(100 + delay) && wait.Pending &&
                    wait.CanAdvance(1, 1, fresh, previous),
                    "a delayed first mission state remains eligible after " + delay + " seconds");
            }

            wait.Begin(2, 200);
            check(wait.SessionId == 2 && wait.Pending && !wait.WarningIssued,
                "beginning a new mission creates a fresh wait and warning budget");
            check(!wait.CanAdvance(1, 2, fresh, previous) && !wait.CanAdvance(2, 1, fresh, previous),
                "both current and ready mission identities must match the pending wait");
            check(!wait.CanAdvance(2, 2, null, previous) && !wait.CanAdvance(2, 2, previous, previous),
                "null and pre-load snapshots cannot satisfy mission readiness");
            check(wait.CanAdvance(2, 2, fresh, null),
                "the first published snapshot can satisfy a wait without a pre-load state");
            check(wait.WarningDelay(200) == TimeSpan.FromSeconds(15) &&
                wait.WarningDelay(207.5) == TimeSpan.FromSeconds(7.5) &&
                !wait.TryWarn(214.999), "the support notification waits for its own elapsed-time boundary");
            check(wait.WarningDelay(215) == TimeSpan.Zero && wait.TryWarn(215) &&
                !wait.TryWarn(216) && !wait.TryWarn(290) && wait.WarningDelay(290) == TimeSpan.Zero,
                "the elapsed-time notification fires once and does not cause a recurring timer");

            // Paused saves may deliver no replacement for either recovery stage. A warning must
            // preserve the wait while the caller retains that stage's own previous snapshot.
            check(wait.Pending && !wait.CanAdvance(2, 2, previous, previous),
                "waiting through pause does not accept a stale state or abandon pending recovery");
            check(wait.CanAdvance(2, 2, fresh, previous), "a later publication resumes recovery after pause");
            var verifiedView = new object();
            check(!wait.CanAdvance(2, 2, fresh, fresh) && wait.CanAdvance(2, 2, verifiedView, fresh),
                "the subsequent save recovery stage waits for its own replacement snapshot");

            wait.Pending = false;
            check(!wait.CanAdvance(2, 2, verifiedView, fresh) && !wait.TryWarn(400) &&
                wait.WarningDelay(400) == TimeSpan.Zero,
                "completed initialization ignores later state publications and timer wakes");
            wait.Begin(3, 500);
            check(wait.WarningDelay(499) == TimeSpan.FromSeconds(15),
                "an earlier clock reading cannot schedule beyond the warning interval");
            wait.Reset();
            check(wait.SessionId == 0 && !wait.Pending && !wait.WarningIssued &&
                !wait.CanAdvance(3, 3, verifiedView, fresh) && !wait.TryWarn(600) &&
                wait.WarningDelay(600) == TimeSpan.Zero,
                "mission end clears the wait and makes queued old publications inert");
            wait.Begin(4, 700);
            check(!wait.CanAdvance(3, 3, fresh, previous) && wait.CanAdvance(4, 4, fresh, previous),
                "a replacement mission only accepts its own readiness publication");
        }
    }
}
