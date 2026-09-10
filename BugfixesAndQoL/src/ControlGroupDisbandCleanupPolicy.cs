namespace BugfixesAndQoL
{
    internal static class ControlGroupDisbandCleanupPolicy
    {
        internal static bool ShouldClean(bool clientFeaturesEnabled, bool cleanupEnabled) =>
            clientFeaturesEnabled && cleanupEnabled;
    }
}
