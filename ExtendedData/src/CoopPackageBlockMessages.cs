using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtendedData
{
    // Keep the existing wire values; technical details belong in the log, not in lobby chat.
    internal static class CoopPackageStatusCodes
    {
        internal const string ErrorPrefix = "ERROR|";
        internal const string Missing = "ERROR|MISSING";
        internal const string Mismatch = "ERROR|MISMATCH";
        internal const string InvalidPrefix = "ERROR|INVALID|";
        internal const string Disabled = "ERROR|DISABLED";
        internal const string Waiting = "WAITING";
    }

    internal static class CoopPackageBlockMessages
    {
        internal static string LocalLoadFailure(bool missing, string packageId) => missing
            ? SerpLocalization.Get("ExtendedData.ErrorPackageMissingHelp").Replace("{Package}", packageId ?? string.Empty)
            : SerpLocalization.Get("ExtendedData.ErrorPackageValidationFailed");

        internal static string Participants(IEnumerable<KeyValuePair<string, string>> reports, string expected)
        {
            var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var report in reports)
            {
                string status = report.Value ?? string.Empty;
                if (string.Equals(status, expected, StringComparison.Ordinal)) continue;
                string key = status == CoopPackageStatusCodes.Missing ? "ExtendedData.ErrorParticipantsMissing" :
                    status == CoopPackageStatusCodes.Mismatch ? "ExtendedData.ErrorParticipantsMismatch" :
                    status.StartsWith("OK|", StringComparison.Ordinal)
                        ? SamePackageVersion(status, expected) ? "ExtendedData.ErrorParticipantsSelectionNotReady" :
                            "ExtendedData.ErrorParticipantsMismatch" :
                    status.StartsWith(CoopPackageStatusCodes.InvalidPrefix, StringComparison.Ordinal)
                        ? "ExtendedData.ErrorParticipantsInvalid" :
                    status == CoopPackageStatusCodes.Disabled ? "ExtendedData.ErrorParticipantsDisabled" :
                        "ExtendedData.ErrorParticipantsNotReady";
                if (!groups.TryGetValue(key, out var names)) groups.Add(key, names = new List<string>());
                names.Add(report.Key);
            }
            return groups.Count == 0 ? SerpLocalization.Get("ExtendedData.ErrorPackageNotReady") :
                string.Join("\r\n", groups.OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => SerpLocalization.Get(group.Key)
                        .Replace("{Players}", string.Join(", ", group.Value.OrderBy(name => name, StringComparer.Ordinal)))));
        }

        private static bool SamePackageVersion(string report, string expected)
        {
            // The optional fourth field binds readiness to the mission and Lord selection.
            // A mismatch there is not evidence of different package files.
            string[] remote = report.Split('|');
            string[] host = expected.Split('|');
            return remote.Length >= 3 && host.Length >= 3 &&
                string.Equals(remote[1], host[1], StringComparison.Ordinal) &&
                string.Equals(remote[2], host[2], StringComparison.Ordinal);
        }
    }
}
