using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    // Config presence is captured before Bind consumes orphaned entries. An existing
    // main-mod value always wins, including when BepInEx repairs an invalid value.
    internal static class FormationSelectionMigration
    {
        internal static Dictionary<string, string> ReadFormationSection(string text)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool formation = false;
            foreach (string raw in (text ?? string.Empty).Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal))
                { formation = string.Equals(line, "[Formation]", StringComparison.OrdinalIgnoreCase); continue; }
                int separator = line.IndexOf('=');
                if (formation && separator > 0)
                    values[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
            }
            return values;
        }

        internal static T ResolveEnum<T>(Dictionary<string, string> main, Dictionary<string, string> legacy, string key, T fallback)
            where T : struct
        {
            if (main.ContainsKey(key) || !legacy.TryGetValue(key, out string raw) ||
                !Enum.TryParse(raw, true, out T value) || !Enum.IsDefined(typeof(T), value)) return fallback;
            return value;
        }

        internal static int ResolveDensity(Dictionary<string, string> main, Dictionary<string, string> legacy)
        {
            return !main.ContainsKey("Density") && legacy.TryGetValue("Density", out string raw) &&
                int.TryParse(raw, out int value) && value >= 1 && value <= 4 ? value : 2;
        }

        internal static bool ResolveRoleMarkers(Dictionary<string, string> main, Dictionary<string, string> legacy)
        {
            return main.ContainsKey("ShowRoleMarkers") || !legacy.TryGetValue("ShowRoleMarkers", out string raw) ||
                !bool.TryParse(raw, out bool value) || value;
        }
    }
}
