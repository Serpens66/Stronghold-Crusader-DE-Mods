namespace EngineerSiegeFix
{
    internal enum HandoffDiagnosticOutcome
    {
        Pending,
        Passed,
        Failed,
        Inconclusive
    }

    internal static class EngineerHandoffDiagnosticPolicy
    {
        public const uint VerificationTimeoutTicks = 256;

        public static HandoffDiagnosticOutcome Evaluate(
            bool sessionContinues,
            bool deviceIdentityPresent,
            bool deviceReady,
            bool crewMatches,
            bool allOriginalEngineersGone,
            uint elapsedTicks)
        {
            if (!sessionContinues || !deviceIdentityPresent)
                return HandoffDiagnosticOutcome.Inconclusive;

            // A converted device with changed crew fields is already a conclusive
            // invariant violation; waiting longer cannot repair its identity.
            if (deviceReady && !crewMatches)
                return HandoffDiagnosticOutcome.Failed;

            if (deviceReady && crewMatches && allOriginalEngineersGone)
                return HandoffDiagnosticOutcome.Passed;

            return elapsedTicks >= VerificationTimeoutTicks
                ? HandoffDiagnosticOutcome.Failed
                : HandoffDiagnosticOutcome.Pending;
        }
    }
}
