using System;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    // A lost executable dependency is never re-admitted after byte restoration.
    // Logging's duplicate suppression is deliberately absent from this decision.
    internal sealed class RescueNativeIntegrity
    {
        private bool lost;

        internal bool Check(Func<bool> relocation, Func<bool> workerGuard)
        {
            if (lost) return false;
            try
            {
                if (relocation != null && relocation() && (workerGuard == null || workerGuard())) return true;
            }
            catch { /* An unreadable dependency is unavailable, not implicitly healthy. */ }
            lost = true;
            return false;
        }
    }
}
