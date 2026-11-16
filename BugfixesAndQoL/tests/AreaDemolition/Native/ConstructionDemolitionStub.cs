namespace APIShared.Buildings
{
    // NativeDemolition wraps building and decoration mutations in APIShared's refund scope.
    // The real implementation lives in APIShared's native construction runtime, which these
    // game-free tests cannot link. No test executes a mutation, so any call is a test defect.
    internal static class ConstructionDemolition
    {
        public static void RunSelfDemolition(int actor, System.Action operation) =>
            throw new System.NotSupportedException("AreaDemolition native tests must not execute APIShared demolition mutations.");
    }
}
