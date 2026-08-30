using System;
using System.Collections.Generic;
using System.Linq;

namespace SerpsModsHost
{
    internal enum ScriptExtenderCompatibilityStatus
    {
        Compatible,
        BelowMinimum,
        AboveMaximum,
        InvalidInstalledVersion,
        InvalidMinimumVersion,
        InvalidMaximumVersion,
        InvalidRange
    }

    internal sealed class ScriptExtenderCompatibilityResult
    {
        public ScriptExtenderCompatibilityStatus Status { get; set; }
        public string InstalledVersion { get; set; }
        public string MinimumVersion { get; set; }
        public string MaximumVersion { get; set; }
        public bool IsCompatible => Status == ScriptExtenderCompatibilityStatus.Compatible;
        public bool HasMaximum => !string.IsNullOrWhiteSpace(MaximumVersion);
    }

    internal static class ScriptExtenderCompatibility
    {
        public static ScriptExtenderCompatibilityResult Evaluate(
            string installedVersion,
            string minimumVersion,
            string maximumVersion)
        {
            var result = new ScriptExtenderCompatibilityResult
            {
                InstalledVersion = NormalizeDisplay(installedVersion),
                MinimumVersion = NormalizeDisplay(minimumVersion),
                MaximumVersion = NormalizeDisplay(maximumVersion)
            };

            if (!TryParseComparableVersion(installedVersion, out Version installed))
            {
                result.Status = ScriptExtenderCompatibilityStatus.InvalidInstalledVersion;
                return result;
            }
            if (!TryParseComparableVersion(minimumVersion, out Version minimum))
            {
                result.Status = ScriptExtenderCompatibilityStatus.InvalidMinimumVersion;
                return result;
            }

            Version maximum = null;
            if (!string.IsNullOrWhiteSpace(maximumVersion) &&
                !TryParseComparableVersion(maximumVersion, out maximum))
            {
                result.Status = ScriptExtenderCompatibilityStatus.InvalidMaximumVersion;
                return result;
            }
            if (maximum != null && minimum > maximum)
            {
                result.Status = ScriptExtenderCompatibilityStatus.InvalidRange;
                return result;
            }
            if (installed < minimum)
            {
                result.Status = ScriptExtenderCompatibilityStatus.BelowMinimum;
                return result;
            }
            if (maximum != null && installed > maximum)
            {
                result.Status = ScriptExtenderCompatibilityStatus.AboveMaximum;
                return result;
            }

            result.Status = ScriptExtenderCompatibilityStatus.Compatible;
            return result;
        }

        private static bool TryParseComparableVersion(string raw, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string text = raw.Trim();
            if (text.Length > 0 && (text[0] == 'v' || text[0] == 'V'))
                text = text.Substring(1);

            // Published Script Extender versions are numeric. Normalize omitted
            // components so 1.43, 1.43.0 and 1.43.0.0 compare identically.
            string[] parts = text.Split('.');
            if (parts.Length < 2 || parts.Length > 4)
                return false;
            var values = new int[4];
            for (int index = 0; index < parts.Length; index++)
            {
                if (!int.TryParse(parts[index], out values[index]) || values[index] < 0)
                    return false;
            }

            version = new Version(values[0], values[1], values[2], values[3]);
            return true;
        }

        private static string NormalizeDisplay(string value) => value?.Trim() ?? string.Empty;
    }

    internal sealed class ScriptExtenderVersionEvidence
    {
        public ScriptExtenderVersionEvidence(string source, string version)
        {
            Source = source ?? string.Empty;
            Version = version ?? string.Empty;
        }

        public string Source { get; }
        public string Version { get; }
    }

    internal sealed class ScriptExtenderVersionResolution
    {
        public string Version { get; set; }
        public string Diagnostic { get; set; }
        public bool ContainsOnlyPlaceholders { get; set; }
        public bool IsResolved => !string.IsNullOrWhiteSpace(Version);
    }

    internal static class ScriptExtenderVersionResolver
    {
        public static ScriptExtenderVersionResolution Resolve(
            IEnumerable<ScriptExtenderVersionEvidence> evidence)
        {
            var accepted = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            var ignored = new List<string>();
            int placeholderCount = 0;

            foreach (ScriptExtenderVersionEvidence item in evidence ?? Enumerable.Empty<ScriptExtenderVersionEvidence>())
            {
                if (item == null || !TryNormalize(item.Version, out string normalized))
                {
                    ignored.Add($"{item?.Source ?? "unknown"}='{item?.Version ?? string.Empty}' (invalid)");
                    continue;
                }
                if (string.Equals(normalized, "1.0.0", StringComparison.Ordinal))
                {
                    // The Script Extender source and local builds use 1.0.0 as a
                    // release-process placeholder, so it is not installation evidence.
                    ignored.Add($"{item.Source}='{item.Version}' (placeholder)");
                    placeholderCount++;
                    continue;
                }

                if (!accepted.TryGetValue(normalized, out List<string> sources))
                {
                    sources = new List<string>();
                    accepted.Add(normalized, sources);
                }
                sources.Add($"{item.Source}='{item.Version}'");
            }

            string ignoredText = ignored.Count == 0 ? "none" : string.Join(", ", ignored);
            if (accepted.Count == 1)
            {
                KeyValuePair<string, List<string>> winner = accepted.Single();
                return new ScriptExtenderVersionResolution
                {
                    Version = winner.Key,
                    Diagnostic = $"selected {winner.Key} from {string.Join(", ", winner.Value)}; ignored: {ignoredText}"
                };
            }
            if (accepted.Count == 0)
            {
                return new ScriptExtenderVersionResolution
                {
                    ContainsOnlyPlaceholders = placeholderCount > 0,
                    Diagnostic = "no non-placeholder Script Extender version was found; ignored: " + ignoredText
                };
            }

            string conflicts = string.Join(
                "; ",
                accepted.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + " from " + string.Join(", ", pair.Value)));
            return new ScriptExtenderVersionResolution
            {
                Diagnostic = "conflicting Script Extender versions were found: " + conflicts + "; ignored: " + ignoredText
            };
        }

        private static bool TryNormalize(string raw, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string text = raw.Trim();
            if (text.Length > 0 && (text[0] == 'v' || text[0] == 'V'))
                text = text.Substring(1);
            int metadataIndex = text.IndexOfAny(new[] { '+', '-' });
            if (metadataIndex >= 0)
                text = text.Substring(0, metadataIndex);

            string[] parts = text.Split('.');
            if (parts.Length < 2 || parts.Length > 4)
                return false;
            var values = new int[4];
            for (int index = 0; index < parts.Length; index++)
            {
                if (!int.TryParse(parts[index], out values[index]) || values[index] < 0)
                    return false;
            }

            normalized = values[3] == 0
                ? $"{values[0]}.{values[1]}.{values[2]}"
                : $"{values[0]}.{values[1]}.{values[2]}.{values[3]}";
            return true;
        }
    }
}
