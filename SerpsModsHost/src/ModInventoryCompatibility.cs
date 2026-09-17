using Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SerpsModsHost
{
    internal sealed class ModInventoryEntry
    {
        internal ModInventoryEntry(string guid, string name, string version, bool clientside)
        {
            Guid = guid ?? string.Empty;
            Name = name ?? string.Empty;
            Version = version ?? string.Empty;
            Clientside = clientside;
        }

        internal string Guid { get; }
        internal string Name { get; }
        internal string Version { get; }
        internal bool Clientside { get; }
        internal string Display => $"{Guid}@{Version}";
    }

    internal sealed class ModInventoryDifference
    {
        internal List<string> HostOnly { get; } = new List<string>();
        internal List<string> ClientOnly { get; } = new List<string>();
        internal List<string> VersionMismatches { get; } = new List<string>();
        internal int Count => HostOnly.Count + ClientOnly.Count + VersionMismatches.Count;
    }

    internal static class ModInventoryCompatibility
    {
        internal const string ScriptExtenderLobbyModListToken = "_SE_MODS_";
        private const int MaximumEncodedBytes = 8192;
        private const int MaximumGuidLength = 200;
        private const int MaximumNameLength = 160;
        private const int MaximumVersionLength = 80;

        internal static bool TryDecodeScriptExtenderMetadata(
            string json,
            out List<ModInventoryEntry> entries)
        {
            entries = new List<ModInventoryEntry>();
            if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) >= MaximumEncodedBytes)
                return false;

            try
            {
                if (!(DependencyFreeJson.Parse(json) is List<object> parsed))
                    return false;

                var seenGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (object item in parsed)
                {
                    if (!(item is Dictionary<string, object> fields) ||
                        !TryReadRequiredString(fields, "g", MaximumGuidLength, out string guid) ||
                        !TryReadRequiredString(fields, "n", MaximumNameLength, out string name) ||
                        !TryReadRequiredString(fields, "v", MaximumVersionLength, out string version) ||
                        !fields.TryGetValue("c", out object clientsideValue) ||
                        !(clientsideValue is bool clientside) ||
                        !seenGuids.Add(guid))
                    {
                        entries.Clear();
                        return false;
                    }

                    if (fields.TryGetValue("w", out object workshopValue) && !(workshopValue is string))
                    {
                        entries.Clear();
                        return false;
                    }

                    entries.Add(new ModInventoryEntry(guid, name, version, clientside));
                }

                entries.Sort((left, right) =>
                    StringComparer.OrdinalIgnoreCase.Compare(left.Guid, right.Guid));
                return true;
            }
            catch (InvalidDataException)
            {
                entries.Clear();
                return false;
            }
            catch (EncoderFallbackException)
            {
                entries.Clear();
                return false;
            }
        }

        internal static List<ModInventoryEntry> BuildCanonicalLocalInventory(
            IEnumerable<ModInventoryEntry> assetEntries,
            IEnumerable<ModInventoryEntry> pluginEntries)
        {
            var byGuid = new Dictionary<string, ModInventoryEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ModInventoryEntry asset in assetEntries ?? Enumerable.Empty<ModInventoryEntry>())
            {
                ModInventoryEntry cleaned = CleanLocalEntry(asset);
                if (cleaned != null)
                    byGuid[cleaned.Guid] = cleaned;
            }

            foreach (ModInventoryEntry plugin in pluginEntries ?? Enumerable.Empty<ModInventoryEntry>())
            {
                ModInventoryEntry cleaned = CleanLocalEntry(plugin);
                if (cleaned != null && !byGuid.ContainsKey(cleaned.Guid))
                    byGuid.Add(cleaned.Guid, cleaned);
            }

            return byGuid.Values
                .OrderBy(entry => entry.Guid, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        internal static ModInventoryDifference Compare(
            IEnumerable<ModInventoryEntry> hostEntries,
            IEnumerable<ModInventoryEntry> clientEntries)
        {
            Dictionary<string, ModInventoryEntry> host = ToGuidDictionary(hostEntries);
            Dictionary<string, ModInventoryEntry> client = ToGuidDictionary(clientEntries);
            var result = new ModInventoryDifference();

            foreach (ModInventoryEntry local in client.Values.OrderBy(
                entry => entry.Guid,
                StringComparer.OrdinalIgnoreCase))
            {
                if (!host.TryGetValue(local.Guid, out ModInventoryEntry remote))
                {
                    if (!local.Clientside)
                        result.ClientOnly.Add(local.Display);
                    continue;
                }

                if (!string.Equals(local.Version, remote.Version, StringComparison.Ordinal) &&
                    (!local.Clientside || !remote.Clientside))
                {
                    result.VersionMismatches.Add(
                        $"{local.Guid}: client {local.Version}, host {remote.Version}");
                }
            }

            foreach (ModInventoryEntry remote in host.Values.OrderBy(
                entry => entry.Guid,
                StringComparer.OrdinalIgnoreCase))
            {
                if (!client.ContainsKey(remote.Guid) && !remote.Clientside)
                    result.HostOnly.Add(remote.Display);
            }

            return result;
        }

        private static bool TryReadRequiredString(
            IReadOnlyDictionary<string, object> fields,
            string key,
            int maximumLength,
            out string value)
        {
            value = null;
            if (!fields.TryGetValue(key, out object raw) || !(raw is string text))
                return false;
            value = text.Trim();
            return value.Length > 0 && value.Length <= maximumLength &&
                value.IndexOf('\r') < 0 && value.IndexOf('\n') < 0;
        }

        private static ModInventoryEntry CleanLocalEntry(ModInventoryEntry entry)
        {
            if (entry == null)
                return null;
            string guid = CleanText(entry.Guid, MaximumGuidLength);
            if (string.IsNullOrWhiteSpace(guid))
                return null;
            string name = CleanText(entry.Name, MaximumNameLength);
            string version = CleanText(entry.Version, MaximumVersionLength);
            return new ModInventoryEntry(
                guid,
                string.IsNullOrWhiteSpace(name) ? guid : name,
                string.IsNullOrWhiteSpace(version) ? "0.0.0" : version,
                entry.Clientside);
        }

        private static string CleanText(string value, int maximumLength)
        {
            string cleaned = (value ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            return cleaned.Length <= maximumLength
                ? cleaned
                : cleaned.Substring(0, maximumLength);
        }

        private static Dictionary<string, ModInventoryEntry> ToGuidDictionary(
            IEnumerable<ModInventoryEntry> entries)
        {
            var result = new Dictionary<string, ModInventoryEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ModInventoryEntry entry in entries ?? Enumerable.Empty<ModInventoryEntry>())
            {
                if (entry != null && !string.IsNullOrWhiteSpace(entry.Guid))
                    result[entry.Guid] = entry;
            }
            return result;
        }
    }
}
