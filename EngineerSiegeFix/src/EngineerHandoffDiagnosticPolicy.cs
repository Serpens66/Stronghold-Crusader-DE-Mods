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
        public const short LiveUnitState = 2;
        public const ushort EngineerType = 0x1E;
        public const ushort BoundCrewMainState = 5;

        public static HandoffDiagnosticOutcome Evaluate(
            bool sessionContinues,
            bool deviceIdentityPresent,
            bool deviceReady,
            bool crewMatches,
            bool allReferencedEngineersBound,
            uint elapsedTicks)
        {
            if (!sessionContinues || !deviceIdentityPresent)
                return HandoffDiagnosticOutcome.Inconclusive;

            // Once ready, a changed crew identity or a referenced engineer returning
            // to a free state is the observed duplication signature.
            if (deviceReady && (!crewMatches || !allReferencedEngineersBound))
                return HandoffDiagnosticOutcome.Failed;

            if (deviceReady && crewMatches && allReferencedEngineersBound &&
                elapsedTicks >= VerificationTimeoutTicks)
            {
                return HandoffDiagnosticOutcome.Passed;
            }

            if (elapsedTicks >= VerificationTimeoutTicks)
                return HandoffDiagnosticOutcome.Failed;

            return HandoffDiagnosticOutcome.Pending;
        }

        public static bool IsReferencedEngineerBound(
            int expectedUnitId,
            uint expectedGlobalId,
            byte expectedOwner,
            int actualUnitId,
            uint actualGlobalId,
            short actualAliveState,
            ushort actualType,
            byte actualOwner,
            uint actualPackedState) =>
            expectedUnitId > 0 && expectedGlobalId != 0 &&
            actualUnitId == expectedUnitId && actualGlobalId == expectedGlobalId &&
            actualAliveState == LiveUnitState && actualType == EngineerType &&
            actualOwner == expectedOwner && unchecked((ushort)actualPackedState) == BoundCrewMainState;

        public static bool AreCrewIdentitiesValidAndStable(
            int requiredCrew,
            ushort actualCrewCount,
            ushort[] expectedIds,
            uint[] expectedGlobals,
            ushort[] actualIds,
            uint[] actualGlobals)
        {
            if (requiredCrew < 1 || requiredCrew > 3 || actualCrewCount != requiredCrew ||
                expectedIds == null || expectedGlobals == null || actualIds == null || actualGlobals == null ||
                expectedIds.Length < requiredCrew || expectedGlobals.Length < requiredCrew ||
                actualIds.Length < requiredCrew || actualGlobals.Length < requiredCrew)
            {
                return false;
            }

            for (int index = 0; index < requiredCrew; index++)
            {
                if (expectedIds[index] == 0 || expectedGlobals[index] == 0 ||
                    actualIds[index] != expectedIds[index] || actualGlobals[index] != expectedGlobals[index])
                {
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (actualIds[previous] == actualIds[index] ||
                        actualGlobals[previous] == actualGlobals[index])
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
