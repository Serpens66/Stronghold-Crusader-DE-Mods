using System;
using System.Collections.Generic;
using System.IO;
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
        internal const string NotReadyKey = "ExtendedData.ErrorParticipantsNotReady";
        internal const string SelectionNotReadyKey = "ExtendedData.ErrorParticipantsSelectionNotReady";

        internal static string LocalLoadFailure(bool missing, string packageId) => missing
            ? SerpLocalization.Get("ExtendedData.ErrorPackageMissingHelp").Replace("{Package}", packageId ?? string.Empty)
            : SerpLocalization.Get("ExtendedData.ErrorPackageValidationFailed");

        // Exact host status means ready. Otherwise the result is the locale key of the reason.
        internal static string Classify(string status, string expected)
        {
            status = status ?? string.Empty;
            if (string.Equals(status, expected, StringComparison.Ordinal)) return null;
            if (status == CoopPackageStatusCodes.Missing) return "ExtendedData.ErrorParticipantsMissing";
            if (status == CoopPackageStatusCodes.Mismatch) return "ExtendedData.ErrorParticipantsMismatch";
            if (status.StartsWith("OK|", StringComparison.Ordinal))
            {
                string[] remote = status.Split('|');
                string[] host = (expected ?? string.Empty).Split('|');
                // A different package ID (for example OK|VANILLA) means the joining player has not
                // applied the host's selection yet. Only the same package with other contents is a version conflict.
                if (remote.Length < 3 || host.Length < 3 || !string.Equals(remote[1], host[1], StringComparison.Ordinal))
                    return NotReadyKey;
                // The optional fourth field binds readiness to the mission and Lord selection.
                return string.Equals(remote[2], host[2], StringComparison.Ordinal)
                    ? SelectionNotReadyKey : "ExtendedData.ErrorParticipantsMismatch";
            }
            if (status.StartsWith(CoopPackageStatusCodes.InvalidPrefix, StringComparison.Ordinal))
                return "ExtendedData.ErrorParticipantsInvalid";
            if (status == CoopPackageStatusCodes.Disabled) return "ExtendedData.ErrorParticipantsDisabled";
            return NotReadyKey;
        }

        // Reasons a joining player resolves without action: not reported yet or still applying the host selection.
        internal static bool IsLoading(string key) => key == NotReadyKey || key == SelectionNotReadyKey;

        internal static string Participants(IEnumerable<KeyValuePair<string, string>> reports, string expected)
        {
            string text = Format(Group(reports, expected));
            return text.Length == 0 ? SerpLocalization.Get("ExtendedData.ErrorPackageNotReady") : text;
        }

        // Lobby status split: blocking reasons keep their specific text; loading players share one short wait hint.
        internal static void ParticipantStatus(IEnumerable<KeyValuePair<string, string>> reports, string expected,
            out string blocking, out string loading)
        {
            var groups = Group(reports, expected);
            blocking = Format(groups.Where(group => !IsLoading(group.Key)));
            string[] players = groups.Where(group => IsLoading(group.Key)).SelectMany(group => group.Value)
                .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            loading = players.Length == 0 ? string.Empty :
                SerpLocalization.Get("ExtendedData.LoadingParticipants").Replace("{Players}", string.Join(", ", players));
        }

        // Player-facing text for a failed Coop mission preparation; the exception itself belongs in the log.
        internal static string MissionPreparationFailure(Exception exception)
        {
            switch (exception)
            {
                case MissionAssetException asset when asset.Category == MissionAssetException.MapMissing:
                    return SerpLocalization.Get("ExtendedData.ErrorMissionMapMissing").Replace("{Map}", asset.AssetPath ?? string.Empty);
                case MissionAssetException asset when asset.Category == MissionAssetException.LordUnresolved:
                    return SerpLocalization.Get("ExtendedData.ErrorMissionLordMissing").Replace("{Lord}", asset.AssetPath ?? string.Empty);
                case MissionAssetException asset when asset.Category == MissionAssetException.AivMissing:
                    return SerpLocalization.Get("ExtendedData.ErrorMissionLordMissing").Replace("{Lord}", asset.AssetPath ?? string.Empty);
                case MissionRetryException _:
                    return SerpLocalization.Get("ExtendedData.ErrorMissionRetry");
                case MissionAssetException _:
                case InvalidDataException _:
                case FileNotFoundException _:
                case DirectoryNotFoundException _:
                    return SerpLocalization.Get("ExtendedData.ErrorPackageValidationFailed");
                default:
                    return SerpLocalization.Get("ExtendedData.ErrorCoopPreparationFailed");
            }
        }

        private static List<KeyValuePair<string, List<string>>> Group(IEnumerable<KeyValuePair<string, string>> reports, string expected)
        {
            var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var report in reports)
            {
                string key = Classify(report.Value, expected);
                if (key == null) continue;
                if (!groups.TryGetValue(key, out var names)) groups.Add(key, names = new List<string>());
                names.Add(report.Key);
            }
            return groups.OrderBy(group => group.Key, StringComparer.Ordinal).ToList();
        }

        private static string Format(IEnumerable<KeyValuePair<string, List<string>>> groups) =>
            string.Join("\r\n", groups.Select(group => SerpLocalization.Get(group.Key)
                .Replace("{Players}", string.Join(", ", group.Value.OrderBy(name => name, StringComparer.Ordinal)))));
    }
}
