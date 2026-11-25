using System;
using System.Globalization;

namespace ExtendedData
{
    internal static class CoopMissionSettingsReadiness
    {
        // Inputs identify an already resolved mission; this does not decide whether
        // its package or save is eligible to launch.
        internal static string BindMission(string packageReadyStatus, int trailNumber, int missionNumber) =>
            packageReadyStatus + "|settings:" + trailNumber.ToString(CultureInfo.InvariantCulture) +
            "/" + missionNumber.ToString(CultureInfo.InvariantCulture);

        internal static string Resolve(string proposed, string expected, string pending,
            string invalidPrefix, string blockReason, bool prepared)
        {
            // Preserve existing package/Lord failures rather than replacing their reason.
            if (!string.Equals(proposed, expected, StringComparison.Ordinal)) return proposed;
            if (!string.IsNullOrEmpty(blockReason)) return invalidPrefix + blockReason;
            return prepared ? expected : pending;
        }
    }
}
