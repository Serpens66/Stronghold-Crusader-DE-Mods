using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

namespace CustomCustomTrail.Core
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
            if (!root.TryGetValue("schemaVersion", out object schema) || !(schema is int schemaVersion) || schemaVersion != 1)
                throw new InvalidDataException("Unsupported Trail mod-settings schemaVersion.");
            if (!root.TryGetValue("mods", out object modsObject) || !(modsObject is Dictionary<string, object> mods))
                throw new InvalidDataException("Trail mod-settings JSON requires a mods object.");

            ModSettingsDefinition document = ModSettingsDefinition.CreateDisabled();
            foreach (KeyValuePair<string, object> mod in mods)
            {
                if (!(mod.Value is Dictionary<string, object> rawEntry))
                    throw new InvalidDataException($"Mod entry [{mod.Key}] must be an object.");
                var entry = new ModSettingsEntry();
                if (rawEntry.TryGetValue("enabled", out object enabled))
                {
                    if (!(enabled is bool enabledValue))
                        throw new InvalidDataException($"Mod entry [{mod.Key}].enabled must be a boolean.");
                    entry.Enabled = enabledValue;
                }
                if (rawEntry.TryGetValue("settings", out object settingsObject))
                {
                    if (!(settingsObject is Dictionary<string, object> settings))
                        throw new InvalidDataException($"Mod entry [{mod.Key}].settings must be an object.");
                    foreach (KeyValuePair<string, object> setting in settings)
                    {
                    if (string.IsNullOrWhiteSpace(setting.Key) || !IsSupportedValue(setting.Value))
                            throw new InvalidDataException($"Mod entry [{mod.Key}] contains an unsupported value for [{setting.Key}].");
                    }
                    entry.Settings = settings;
                }
                if (!entry.Enabled)
                    entry.Settings.Clear();
                document.Mods[mod.Key] = entry;
            }
            return NormalizeAndValidate(document, "Trail mod-settings");
        }

        public static ModSettingsDefinition NormalizeAndValidate(ModSettingsDefinition settings, string path)
        {
            settings = settings ?? ModSettingsDefinition.CreateDisabled();
            if (settings.SchemaVersion != 1)
                throw new InvalidDataException((path ?? "modSettings") + ".schemaVersion must be 1.");
            settings.Mods = settings.Mods ?? new Dictionary<string, ModSettingsEntry>(StringComparer.Ordinal);
            var normalized = new Dictionary<string, ModSettingsEntry>(StringComparer.Ordinal);
            foreach (string id in ModSettingsDefinition.TargetModIds)
            {
                if (!settings.Mods.TryGetValue(id, out ModSettingsEntry entry) || entry == null)
                    entry = new ModSettingsEntry();
                entry.Settings = entry.Settings ?? new Dictionary<string, object>(StringComparer.Ordinal);
                if (!entry.Enabled)
                    entry.Settings.Clear();
                foreach (KeyValuePair<string, object> value in entry.Settings)
                {
                    if (string.IsNullOrWhiteSpace(value.Key) || !IsSupportedValue(value.Value))
                        throw new InvalidDataException((path ?? "modSettings") + "." + id + ".settings contains an unsupported value for " + value.Key + ".");
                }
                entry.Settings = entry.Settings
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);
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
                !document.Mods.TryGetValue(modId, out ModSettingsEntry entry) || entry?.Settings == null)
            {
                return Array.Empty<string>();
            }

            var currentNames = new HashSet<string>(currentSettingNames ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            string[] removed = entry.Settings.Keys
                .Where(name => !currentNames.Contains(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            foreach (string name in removed)
                entry.Settings.Remove(name);
            return removed;
        }

        public static string Serialize(ModSettingsDefinition document)
        {
            var mods = new OrderedDictionary(StringComparer.Ordinal);
            foreach (string id in ModSettingsDefinition.TargetModIds)
            {
                ModSettingsEntry entry = document?.Mods != null &&
                    document.Mods.TryGetValue(id, out ModSettingsEntry found)
                        ? found
                        : new ModSettingsEntry();
                var settings = new OrderedDictionary(StringComparer.Ordinal);
                if (entry.Enabled && entry.Settings != null)
                {
                    foreach (KeyValuePair<string, object> setting in
                        entry.Settings.OrderBy(item => item.Key, StringComparer.Ordinal))
                    {
                        if (string.IsNullOrWhiteSpace(setting.Key) || !IsSupportedValue(setting.Value))
                            throw new InvalidDataException($"Mod entry [{id}] contains an unsupported value for [{setting.Key}].");
                        settings.Add(setting.Key, setting.Value);
                    }
                }

                mods.Add(id, new OrderedDictionary(StringComparer.Ordinal)
                {
                    { "enabled", entry.Enabled },
                    { "settings", settings }
                });
            }

            return Shared.DependencyFreeJson.Serialize(new OrderedDictionary(StringComparer.Ordinal)
            {
                { "schemaVersion", 1 },
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
