namespace BugfixesAndQoL
{
    internal static class PeaceTimeCivilianProximityPolicy
    {
        internal const int MaximumCivilianRadius = 3;

        internal static bool CanCoexist(bool extraFeaturesPresent, bool sharedRegistrationPresent) =>
            !extraFeaturesPresent || sharedRegistrationPresent;

        internal static bool ShouldLimit(bool hostEnabled, bool featureEnabled, bool missionReady,
            bool compatible, bool peaceAvailable, bool peaceActive) =>
            hostEnabled && featureEnabled && missionReady && compatible && peaceAvailable && peaceActive;
    }
}
