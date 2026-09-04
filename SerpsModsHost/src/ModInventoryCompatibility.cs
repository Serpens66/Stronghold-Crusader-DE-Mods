using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SerpsModsHost
{
    internal sealed class ModInventoryEntry
    {
        internal ModInventoryEntry(string kind, string guid, string version)
        {
            Kind = kind ?? string.Empty;
            Guid = guid ?? string.Empty;
            Version = version ?? string.Empty;
        }

        internal string Kind { get; }
        internal string Guid { get; }
        internal string Version { get; }
        internal string Display => $"{Guid}@{Version} [{Kind}]";
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
        private const string Schema = "v1";
        private const int MaximumEncodedBytes = 8192;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static string Encode(IEnumerable<ModInventoryEntry> entries)
        {
            string[] lines = (entries ?? Enumerable.Empty<ModInventoryEntry>())
                .OrderBy(entry => entry.Kind, StringComparer.Ordinal)
                .ThenBy(entry => entry.Guid, StringComparer.Ordinal)
                .ThenBy(entry => entry.Version, StringComparer.Ordinal)
                .Select(entry => string.Join("|",
                    EncodePart(entry.Kind),
                    EncodePart(entry.Guid),
                    EncodePart(entry.Version)))
                .ToArray();
            return lines.Length == 0 ? Schema : Schema + "\n" + string.Join("\n", lines);
        }

        internal static bool TryDecode(string encoded, out List<ModInventoryEntry> entries)
        {
            entries = new List<ModInventoryEntry>();
            if (string.IsNullOrEmpty(encoded) || Encoding.UTF8.GetByteCount(encoded) >= MaximumEncodedBytes)
                return false;

            string[] lines = encoded.Split(new[] { '\n' }, StringSplitOptions.None);
            if (lines.Length == 0 || !string.Equals(lines[0], Schema, StringComparison.Ordinal))
                return false;

            try
            {
                for (int index = 1; index < lines.Length; index++)
                {
                    string[] parts = lines[index].Split('|');
                    if (parts.Length != 3)
                        return false;
                    string kind = DecodePart(parts[0]);
                    string guid = DecodePart(parts[1]);
                    string version = DecodePart(parts[2]);
                    if ((kind != "plugin" && kind != "asset") ||
                        string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(version))
                    {
                        return false;
                    }
                    entries.Add(new ModInventoryEntry(kind, guid, version));
                }
                return true;
            }
            catch (FormatException)
            {
                entries.Clear();
                return false;
            }
            catch (DecoderFallbackException)
            {
                entries.Clear();
                return false;
            }
        }

        internal static ModInventoryDifference Compare(
            IEnumerable<ModInventoryEntry> hostEntries,
            IEnumerable<ModInventoryEntry> clientEntries)
        {
            Dictionary<string, List<ModInventoryEntry>> host = Group(hostEntries);
            Dictionary<string, List<ModInventoryEntry>> client = Group(clientEntries);
            var result = new ModInventoryDifference();
            foreach (string key in host.Keys.Concat(client.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
            {
                host.TryGetValue(key, out List<ModInventoryEntry> hostGroup);
                client.TryGetValue(key, out List<ModInventoryEntry> clientGroup);
                hostGroup = hostGroup ?? new List<ModInventoryEntry>();
                clientGroup = clientGroup ?? new List<ModInventoryEntry>();
                if (Versions(hostGroup).SequenceEqual(Versions(clientGroup), StringComparer.Ordinal))
                    continue;

                if (clientGroup.Count == 0)
                    result.HostOnly.Add(DescribeGroup(hostGroup));
                else if (hostGroup.Count == 0)
                    result.ClientOnly.Add(DescribeGroup(clientGroup));
                else
                    result.VersionMismatches.Add(
                        $"{hostGroup[0].Guid} [{hostGroup[0].Kind}]: client {string.Join(",", Versions(clientGroup))}, host {string.Join(",", Versions(hostGroup))}");
            }
            return result;
        }

        private static Dictionary<string, List<ModInventoryEntry>> Group(IEnumerable<ModInventoryEntry> entries) =>
            (entries ?? Enumerable.Empty<ModInventoryEntry>())
                .GroupBy(entry => entry.Kind + "\0" + entry.Guid, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(entry => entry.Version, StringComparer.Ordinal).ToList(),
                    StringComparer.Ordinal);

        private static IEnumerable<string> Versions(IEnumerable<ModInventoryEntry> entries) =>
            entries.Select(entry => entry.Version);

        private static string DescribeGroup(IReadOnlyList<ModInventoryEntry> entries)
        {
            ModInventoryEntry first = entries[0];
            string versions = string.Join(",", Versions(entries));
            return $"{first.Guid}@{versions} [{first.Kind}]";
        }

        private static string EncodePart(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

        private static string DecodePart(string value) =>
            StrictUtf8.GetString(Convert.FromBase64String(value));
    }
}
