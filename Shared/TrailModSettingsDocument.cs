using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Shared
{
    public static class TrailModSettingsRegistry
    {
        public static readonly string[] TargetModIds =
        {
            "BuildingCosts_Serp",
            "BuildingLimit_Serp",
            "ExtraFeatures_Serp",
            "RandomEvents_Serp",
            "StartConditions_Serp",
            "UnitCosts_Serp",
            "UnitLimit_Serp",
        };

        public static string ElectLeader(IEnumerable<string> loadedIds) =>
            (loadedIds ?? Enumerable.Empty<string>())
                .Where(id => TargetModIds.Contains(id, StringComparer.Ordinal))
                .OrderBy(id => id, StringComparer.Ordinal)
                .FirstOrDefault();
    }

    public sealed class TrailSettingsDocument
    {
        public int SchemaVersion { get; set; } = 1;
        public Dictionary<string, TrailModEntry> Mods { get; set; } = new Dictionary<string, TrailModEntry>(StringComparer.Ordinal);

        public static TrailSettingsDocument CreateDisabled()
        {
            var document = new TrailSettingsDocument();
            foreach (string id in TrailModSettingsRegistry.TargetModIds)
                document.Mods[id] = new TrailModEntry();
            return document;
        }
    }

    public sealed class TrailModEntry
    {
        public bool Enabled { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>(StringComparer.Ordinal);
    }

    public static class TrailSettingsJson
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = 16 * 1024 * 1024,
            RecursionLimit = 128,
        };

        public static TrailSettingsDocument Read(string path) => ParseObject(File.ReadAllText(path, Encoding.UTF8));

        public static TrailSettingsDocument ParseObject(string json)
        {
            object rootObject = Serializer.DeserializeObject(json ?? string.Empty);
            if (!(rootObject is Dictionary<string, object> root))
                throw new InvalidDataException("Trail mod-settings JSON root must be an object.");
            if (!root.TryGetValue("schemaVersion", out object schema) || Convert.ToInt32(schema, CultureInfo.InvariantCulture) != 1)
                throw new InvalidDataException("Unsupported Trail mod-settings schemaVersion.");
            if (!root.TryGetValue("mods", out object modsObject) || !(modsObject is Dictionary<string, object> mods))
                throw new InvalidDataException("Trail mod-settings JSON requires a mods object.");

            TrailSettingsDocument document = TrailSettingsDocument.CreateDisabled();
            foreach (KeyValuePair<string, object> mod in mods)
            {
                if (!(mod.Value is Dictionary<string, object> rawEntry))
                    throw new InvalidDataException($"Mod entry [{mod.Key}] must be an object.");
                var entry = new TrailModEntry();
                if (rawEntry.TryGetValue("enabled", out object enabled))
                    entry.Enabled = Convert.ToBoolean(enabled, CultureInfo.InvariantCulture);
                if (rawEntry.TryGetValue("settings", out object settingsObject))
                {
                    if (!(settingsObject is Dictionary<string, object> settings))
                        throw new InvalidDataException($"Mod entry [{mod.Key}].settings must be an object.");
                    foreach (KeyValuePair<string, object> setting in settings)
                    {
                        if (string.IsNullOrWhiteSpace(setting.Key) || !IsNativeValue(setting.Value))
                            throw new InvalidDataException($"Mod entry [{mod.Key}] contains an unsupported value for [{setting.Key}].");
                    }
                    entry.Settings = settings;
                }
                if (!entry.Enabled)
                    entry.Settings.Clear();
                document.Mods[mod.Key] = entry;
            }
            return document;
        }

        public static string Serialize(TrailSettingsDocument document)
        {
            var mods = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (string id in TrailModSettingsRegistry.TargetModIds)
            {
                TrailModEntry entry = document?.Mods != null && document.Mods.TryGetValue(id, out TrailModEntry found) ? found : new TrailModEntry();
                mods[id] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["enabled"] = entry.Enabled,
                    ["settings"] = entry.Enabled ? (object)entry.Settings : new Dictionary<string, object>(),
                };
            }
            return PrettyPrint(Serializer.Serialize(new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["schemaVersion"] = 1,
                ["mods"] = mods,
            }));
        }

        public static void WriteAtomic(string path, TrailSettingsDocument document)
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, Serialize(document), new UTF8Encoding(false));
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null);
                else File.Move(temporary, fullPath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private static bool IsNativeValue(object value) =>
            value is bool || value is string || value is int || value is long || value is decimal || value is double;

        private static string PrettyPrint(string json)
        {
            var output = new StringBuilder(json.Length + 256);
            bool quoted = false;
            bool escaped = false;
            int indent = 0;
            foreach (char character in json)
            {
                if (quoted)
                {
                    output.Append(character);
                    if (escaped) escaped = false;
                    else if (character == '\\') escaped = true;
                    else if (character == '"') quoted = false;
                    continue;
                }
                switch (character)
                {
                    case '"': quoted = true; output.Append(character); break;
                    case '{':
                    case '[': output.Append(character).Append("\r\n"); indent++; AppendIndent(output, indent); break;
                    case '}':
                    case ']': output.Append("\r\n"); indent--; AppendIndent(output, indent); output.Append(character); break;
                    case ',': output.Append(character).Append("\r\n"); AppendIndent(output, indent); break;
                    case ':': output.Append(": "); break;
                    default: if (!char.IsWhiteSpace(character)) output.Append(character); break;
                }
            }
            return output.Append("\r\n").ToString();
        }

        private static void AppendIndent(StringBuilder output, int indent) => output.Append(' ', Math.Max(0, indent) * 2);
    }
}
