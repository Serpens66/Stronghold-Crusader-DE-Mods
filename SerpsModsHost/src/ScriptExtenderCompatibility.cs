using System;

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
}
