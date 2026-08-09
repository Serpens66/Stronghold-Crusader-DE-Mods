using System;
using System.Collections.Generic;
using System.Globalization;
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
            object rootObject = new JsonParser(json ?? string.Empty).Parse();
            if (!(rootObject is Dictionary<string, object> root))
                throw new InvalidDataException("Trail mod-settings JSON root must be an object.");
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
                        if (string.IsNullOrWhiteSpace(setting.Key) || !IsNativeValue(setting.Value))
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
                    if (string.IsNullOrWhiteSpace(value.Key) || !IsNativeValue(value.Value))
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

        public static string Serialize(ModSettingsDefinition document)
        {
            var output = new StringBuilder(4096);
            output.Append("{\r\n  \"schemaVersion\": 1,\r\n  \"mods\": {\r\n");
            for (int modIndex = 0; modIndex < ModSettingsDefinition.TargetModIds.Length; modIndex++)
            {
                string id = ModSettingsDefinition.TargetModIds[modIndex];
                ModSettingsEntry entry = document?.Mods != null && document.Mods.TryGetValue(id, out ModSettingsEntry found) ? found : new ModSettingsEntry();
                output.Append("    ");
                AppendString(output, id);
                output.Append(": {\r\n      \"enabled\": ").Append(entry.Enabled ? "true" : "false");
                output.Append(",\r\n      \"settings\": {");
                if (entry.Enabled && entry.Settings != null && entry.Settings.Count > 0)
                {
                    KeyValuePair<string, object>[] settings = entry.Settings.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray();
                    output.Append("\r\n");
                    for (int settingIndex = 0; settingIndex < settings.Length; settingIndex++)
                    {
                        output.Append("        ");
                        AppendString(output, settings[settingIndex].Key);
                        output.Append(": ");
                        AppendValue(output, settings[settingIndex].Value);
                        if (settingIndex + 1 < settings.Length) output.Append(',');
                        output.Append("\r\n");
                    }
                    output.Append("      ");
                }
                output.Append("}\r\n    }");
                if (modIndex + 1 < ModSettingsDefinition.TargetModIds.Length) output.Append(',');
                output.Append("\r\n");
            }
            return output.Append("  }\r\n}\r\n").ToString();
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

        private static bool IsNativeValue(object value) =>
            value is bool || value is string || value is int || value is long || value is decimal || value is double;

        private static void AppendValue(StringBuilder output, object value)
        {
            if (value is string text) AppendString(output, text);
            else if (value is bool boolean) output.Append(boolean ? "true" : "false");
            else if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is decimal)
                output.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
            else if (value is double number)
            {
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new InvalidDataException("Trail mod-settings JSON cannot contain a non-finite number.");
                output.Append(number.ToString("R", CultureInfo.InvariantCulture));
            }
            else throw new InvalidDataException($"Unsupported Trail mod-settings JSON value [{value?.GetType().FullName ?? "null"}].");
        }

        private static void AppendString(StringBuilder output, string value)
        {
            output.Append('"');
            foreach (char character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': output.Append("\\\""); break;
                    case '\\': output.Append("\\\\"); break;
                    case '\b': output.Append("\\b"); break;
                    case '\f': output.Append("\\f"); break;
                    case '\n': output.Append("\\n"); break;
                    case '\r': output.Append("\\r"); break;
                    case '\t': output.Append("\\t"); break;
                    default:
                        if (character < 0x20) output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        else output.Append(character);
                        break;
                }
            }
            output.Append('"');
        }

        // Kept internal so the seven embedded copies need no JSON assembly that Unity may not load.
        private sealed class JsonParser
        {
            private readonly string json;
            private int position;

            public JsonParser(string json) => this.json = json;

            public object Parse()
            {
                object value = ParseValue();
                SkipWhitespace();
                if (position != json.Length) Fail("Unexpected trailing content");
                return value;
            }

            private object ParseValue()
            {
                SkipWhitespace();
                if (position >= json.Length) Fail("Unexpected end of JSON");
                switch (json[position])
                {
                    case '{': return ParseDictionary();
                    case '[': return ParseArray();
                    case '"': return ParseString();
                    case 't': ReadLiteral("true"); return true;
                    case 'f': ReadLiteral("false"); return false;
                    case 'n': ReadLiteral("null"); return null;
                    default: return ParseNumber();
                }
            }

            private Dictionary<string, object> ParseDictionary()
            {
                position++;
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (Consume('}')) return result;
                while (true)
                {
                    SkipWhitespace();
                    if (position >= json.Length || json[position] != '"') Fail("Expected an object property name");
                    string key = ParseString();
                    SkipWhitespace();
                    if (!Consume(':')) Fail("Expected ':' after an object property name");
                    if (result.ContainsKey(key)) Fail($"Duplicate object property [{key}]");
                    result[key] = ParseValue();
                    SkipWhitespace();
                    if (Consume('}')) return result;
                    if (!Consume(',')) Fail("Expected ',' or '}' in object");
                }
            }

            private List<object> ParseArray()
            {
                position++;
                var result = new List<object>();
                SkipWhitespace();
                if (Consume(']')) return result;
                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (Consume(']')) return result;
                    if (!Consume(',')) Fail("Expected ',' or ']' in array");
                }
            }

            private string ParseString()
            {
                position++;
                var result = new StringBuilder();
                while (position < json.Length)
                {
                    char character = json[position++];
                    if (character == '"') return result.ToString();
                    if (character < 0x20) Fail("Unescaped control character in string");
                    if (character != '\\')
                    {
                        result.Append(character);
                        continue;
                    }
                    if (position >= json.Length) Fail("Incomplete string escape");
                    switch (json[position++])
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u': result.Append(ParseUnicodeEscape()); break;
                        default: Fail("Invalid string escape"); break;
                    }
                }
                Fail("Unterminated string");
                return null;
            }

            private char ParseUnicodeEscape()
            {
                if (position + 4 > json.Length) Fail("Incomplete Unicode escape");
                string digits = json.Substring(position, 4);
                if (!ushort.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort value))
                    Fail("Invalid Unicode escape");
                position += 4;
                return (char)value;
            }

            private object ParseNumber()
            {
                int start = position;
                if (Consume('-') && position >= json.Length) Fail("Incomplete number");
                if (Consume('0'))
                {
                    if (position < json.Length && char.IsDigit(json[position])) Fail("Leading zero in number");
                }
                else
                {
                    if (position >= json.Length || json[position] < '1' || json[position] > '9') Fail("Invalid JSON value");
                    while (position < json.Length && char.IsDigit(json[position])) position++;
                }
                bool fractional = false;
                if (Consume('.'))
                {
                    fractional = true;
                    if (position >= json.Length || !char.IsDigit(json[position])) Fail("Invalid number fraction");
                    while (position < json.Length && char.IsDigit(json[position])) position++;
                }
                if (position < json.Length && (json[position] == 'e' || json[position] == 'E'))
                {
                    fractional = true;
                    position++;
                    if (position < json.Length && (json[position] == '+' || json[position] == '-')) position++;
                    if (position >= json.Length || !char.IsDigit(json[position])) Fail("Invalid number exponent");
                    while (position < json.Length && char.IsDigit(json[position])) position++;
                }
                string token = json.Substring(start, position - start);
                if (fractional)
                {
                    if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double real) || double.IsNaN(real) || double.IsInfinity(real))
                        Fail("Invalid floating-point number");
                    return real;
                }
                if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
                    Fail("Integer is outside the supported range");
                return integer >= int.MinValue && integer <= int.MaxValue ? (object)(int)integer : integer;
            }

            private void ReadLiteral(string literal)
            {
                if (position + literal.Length > json.Length || string.CompareOrdinal(json, position, literal, 0, literal.Length) != 0)
                    Fail($"Invalid JSON literal [{literal}]");
                position += literal.Length;
            }

            private bool Consume(char character)
            {
                if (position >= json.Length || json[position] != character) return false;
                position++;
                return true;
            }

            private void SkipWhitespace()
            {
                while (position < json.Length && (json[position] == ' ' || json[position] == '\t' || json[position] == '\r' || json[position] == '\n')) position++;
            }

            private void Fail(string message) => throw new InvalidDataException($"{message} at JSON position {position}.");
        }
    }
}
