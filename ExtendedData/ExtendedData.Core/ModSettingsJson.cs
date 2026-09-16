using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

namespace ExtendedData.Core
{
    public static class ModSettingsJson
    {
        public static ModSettingsDefinition Read(string path) => ParseObject(File.ReadAllText(path, Encoding.UTF8));

        public static ModSettingsDefinition ParseObject(string json)
        {
            object rootObject = Shared.DependencyFreeJson.Parse(json);
            if (!(rootObject is Dictionary<string, object> root))
                throw new InvalidDataException("Trail mod-settings JSON root must be an object.");
            return ParseObject(root);
        }

        internal static ModSettingsDefinition ParseObject(Dictionary<string, object> root)
        {
            if (!root.TryGetValue("schemaVersion", out object schema) || !(schema is int schemaVersion) || schemaVersion != 3)
                throw new InvalidDataException("Unsupported Trail mod-settings schemaVersion.");
            if (!root.TryGetValue("mods", out object modsObject) || !(modsObject is Dictionary<string, object> mods))
                throw new InvalidDataException("Trail mod-settings JSON requires a mods object.");

            ModSettingsDefinition document = ModSettingsDefinition.CreateModDefaults();
            foreach (KeyValuePair<string, object> mod in mods)
            {
                if (string.IsNullOrWhiteSpace(mod.Key))
                    throw new InvalidDataException("Trail mod-settings contains an empty mod id.");
                if (!(mod.Value is Dictionary<string, object> rawEntry))
                    throw new InvalidDataException($"Mod entry [{mod.Key}] must be an object.");
                var entry = new ModSettingsEntry();
                if (!rawEntry.TryGetValue("playerSettings", out object playerSettingsObject) ||
                    !(playerSettingsObject is List<object> rawPlayerSettings))
                {
                    throw new InvalidDataException($"Mod entry [{mod.Key}] requires a playerSettings array.");
                }
                entry.PlayerSettings = rawPlayerSettings
                    .Select(value => value as string ?? throw new InvalidDataException(
                        $"Mod entry [{mod.Key}].playerSettings must contain only strings."))
                    .ToArray();
                if (!rawEntry.TryGetValue("overrides", out object settingsObject))
                    throw new InvalidDataException($"Mod entry [{mod.Key}] requires an overrides object.");
                if (!(settingsObject is Dictionary<string, object> settings))
                    throw new InvalidDataException($"Mod entry [{mod.Key}].overrides must be an object.");
                foreach (KeyValuePair<string, object> setting in settings)
                {
                    if (string.IsNullOrWhiteSpace(setting.Key) || !IsSupportedValue(setting.Value))
                        throw new InvalidDataException($"Mod entry [{mod.Key}] contains an unsupported value for [{setting.Key}].");
                }
                entry.Overrides = settings;
                if (entry.PlayerSettings.Length != 0 || entry.Overrides.Count != 0)
                    document.Mods[mod.Key] = entry;
            }
            return NormalizeAndValidate(document, "Trail mod-settings");
        }

        public static ModSettingsDefinition NormalizeAndValidate(ModSettingsDefinition settings, string path)
        {
            settings = settings ?? ModSettingsDefinition.CreateModDefaults();
            if (settings.SchemaVersion != 3)
                throw new InvalidDataException((path ?? "modSettings") + ".schemaVersion must be 3.");
            settings.Mods = settings.Mods ?? new Dictionary<string, ModSettingsEntry>(StringComparer.Ordinal);
            var normalized = new Dictionary<string, ModSettingsEntry>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, ModSettingsEntry> mod in settings.Mods
                .OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                string id = mod.Key;
                if (string.IsNullOrWhiteSpace(id))
                    throw new InvalidDataException((path ?? "modSettings") + ".mods contains an empty mod id.");
                ModSettingsEntry entry = mod.Value ?? new ModSettingsEntry();
                if ((entry.PlayerSettings ?? Array.Empty<string>())
                    .Any(name => string.IsNullOrWhiteSpace(name) ||
                        !string.Equals(name, name.Trim(), StringComparison.Ordinal)))
                {
                    throw new InvalidDataException((path ?? "modSettings") + "." + id + ".playerSettings contains an empty or padded property name.");
                }
                entry.PlayerSettings = (entry.PlayerSettings ?? Array.Empty<string>())
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray();
                entry.Overrides = entry.Overrides ?? new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> value in entry.Overrides)
                {
                    if (string.IsNullOrWhiteSpace(value.Key) || !IsSupportedValue(value.Value))
                        throw new InvalidDataException((path ?? "modSettings") + "." + id + ".overrides contains an unsupported value for " + value.Key + ".");
                }
                entry.Overrides = entry.Overrides
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
                string conflict = entry.PlayerSettings.FirstOrDefault(entry.Overrides.ContainsKey);
                if (conflict != null)
                    throw new InvalidDataException((path ?? "modSettings") + "." + id + " selects " + conflict + " as both player and fixed.");
                if (entry.PlayerSettings.Length != 0 || entry.Overrides.Count != 0)
                    normalized[id] = entry;
            }
            settings.Mods = normalized;
            return settings;
        }

        public static string[] RemoveUnknownSettings(
            ModSettingsDefinition document,
            string modId,
            IEnumerable<string> currentSettingNames)
        {
            if (document?.Mods == null || string.IsNullOrEmpty(modId) ||
                !document.Mods.TryGetValue(modId, out ModSettingsEntry entry) || entry == null)
            {
                return Array.Empty<string>();
            }

            entry.PlayerSettings = entry.PlayerSettings ?? Array.Empty<string>();
            entry.Overrides = entry.Overrides ?? new Dictionary<string, object>(StringComparer.Ordinal);
            var currentNames = new HashSet<string>(currentSettingNames ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            string[] removed = entry.PlayerSettings
                .Concat(entry.Overrides.Keys)
                .Where(name => !currentNames.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            entry.PlayerSettings = entry.PlayerSettings
                .Where(currentNames.Contains)
                .ToArray();
            foreach (string name in removed)
                entry.Overrides.Remove(name);
            if (entry.PlayerSettings.Length == 0 && entry.Overrides.Count == 0)
                document.Mods.Remove(modId);
            return removed;
        }

        public static string Serialize(ModSettingsDefinition document)
        {
            document = NormalizeAndValidate(document, "Trail mod-settings");
            var mods = new OrderedDictionary(StringComparer.Ordinal);
            foreach (string id in document.Mods.Keys
                .OrderBy(value => value, StringComparer.Ordinal))
            {
                ModSettingsEntry entry = document.Mods[id];
                var playerSettings = new List<object>();
                foreach (string setting in entry.PlayerSettings)
                    playerSettings.Add(setting);
                var settings = new OrderedDictionary(StringComparer.Ordinal);
                if (entry.Overrides != null)
                {
                    foreach (KeyValuePair<string, object> setting in
                        entry.Overrides.OrderBy(item => item.Key, StringComparer.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(setting.Key) || !IsSupportedValue(setting.Value))
                            throw new InvalidDataException($"Mod entry [{id}] contains an unsupported value for [{setting.Key}].");
                        settings.Add(setting.Key, setting.Value);
                    }
                }

                if (playerSettings.Count != 0 || settings.Count != 0)
                {
                    mods.Add(id, new OrderedDictionary(StringComparer.Ordinal)
                    {
                        { "playerSettings", playerSettings },
                        { "overrides", settings }
                    });
                }
            }

            return Shared.DependencyFreeJson.Serialize(new OrderedDictionary(StringComparer.Ordinal)
            {
                { "schemaVersion", 3 },
                { "mods", mods }
            });
        }

        public static void WriteAtomic(string path, ModSettingsDefinition document)
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

        private static bool IsNativeScalar(object value) =>
            value is bool || value is string ||
            value is byte || value is sbyte || value is short || value is ushort ||
            value is int || value is uint || value is long || value is ulong ||
            value is decimal || value is double;

        public static bool IsSupportedValue(object value)
        {
            if (IsNativeScalar(value))
                return true;
            if (!(value is IEnumerable sequence) || value is IDictionary)
                return false;
            foreach (object item in sequence)
            {
                if (!IsNativeScalar(item))
                    return false;
            }
            return true;
        }
    }
}
