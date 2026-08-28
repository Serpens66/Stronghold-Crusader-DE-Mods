using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CastlePlanner
{
    internal static class AivOptionDisplayNames
    {
        private const string WorkshopSourcePrefix = "Steam Workshop ";

        public static IReadOnlyDictionary<string, string> Build(
            IEnumerable<string> options)
        {
            var entries = (options ?? Enumerable.Empty<string>())
                .Select(Parse)
                .ToArray();
            var result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string, Entry> group in entries.GroupBy(
                entry => entry.BaseDisplayName,
                StringComparer.OrdinalIgnoreCase))
            {
                Entry[] collisions = group.ToArray();
                if (collisions.Length == 1)
                {
                    result[collisions[0].Option] = collisions[0].BaseDisplayName;
                    continue;
                }

                IReadOnlyDictionary<Entry, string> qualifiers =
                    BuildShortestUniqueQualifiers(collisions);
                foreach (Entry entry in collisions)
                {
                    result[entry.Option] =
                        entry.BaseDisplayName + " — " + qualifiers[entry];
                }
            }

            return result;
        }

        private static Entry Parse(string option)
        {
            string normalized = option ?? string.Empty;
            int sourceEnd = normalized.IndexOf(']');
            string source = sourceEnd > 1
                ? normalized.Substring(1, sourceEnd - 1)
                : string.Empty;
            string relativePath = sourceEnd >= 0 && sourceEnd + 1 < normalized.Length
                ? normalized.Substring(sourceEnd + 1).TrimStart()
                : normalized;
            string fileName = Path.GetFileNameWithoutExtension(relativePath) ??
                string.Empty;
            string displayName;
            if (source.StartsWith(
                    WorkshopSourcePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                string workshopId = source.Substring(WorkshopSourcePrefix.Length).Trim();
                displayName = $"[Steam] {fileName} ({workshopId})";
            }
            else
            {
                displayName = string.IsNullOrEmpty(source)
                    ? fileName
                    : $"[{source}] {fileName}";
            }

            string directory = Path.GetDirectoryName(relativePath) ?? string.Empty;
            string[] directoryParts = directory
                .Split(
                    new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries);
            return new Entry(normalized, displayName, directoryParts);
        }

        private static IReadOnlyDictionary<Entry, string>
            BuildShortestUniqueQualifiers(Entry[] entries)
        {
            var depths = entries.ToDictionary(
                entry => entry,
                entry => entry.DirectoryParts.Length == 0 ? 0 : 1);

            while (true)
            {
                IGrouping<string, Entry>[] duplicates = entries
                    .GroupBy(
                        entry => BuildQualifier(entry, depths[entry]),
                        StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1)
                    .ToArray();
                if (duplicates.Length == 0)
                    break;

                bool changed = false;
                foreach (IGrouping<string, Entry> duplicate in duplicates)
                {
                    foreach (Entry entry in duplicate)
                    {
                        if (depths[entry] < entry.DirectoryParts.Length)
                        {
                            depths[entry]++;
                            changed = true;
                        }
                    }
                }

                // Exact duplicate stable keys cannot normally reach this point.
                // Keep labels deterministic and unique if malformed input does.
                if (!changed)
                    break;
            }

            var result = new Dictionary<Entry, string>();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Entry entry in entries.OrderBy(
                value => value.Option,
                StringComparer.OrdinalIgnoreCase))
            {
                string qualifier = BuildQualifier(entry, depths[entry]);
                string unique = qualifier;
                int suffix = 2;
                while (!used.Add(unique))
                    unique = qualifier + " " + suffix++;
                result.Add(entry, unique);
            }

            return result;
        }

        private static string BuildQualifier(Entry entry, int depth)
        {
            if (depth <= 0 || entry.DirectoryParts.Length == 0)
                return ".";

            int start = Math.Max(0, entry.DirectoryParts.Length - depth);
            return string.Join(
                "/",
                entry.DirectoryParts.Skip(start));
        }

        private sealed class Entry
        {
            public Entry(
                string option,
                string baseDisplayName,
                string[] directoryParts)
            {
                Option = option;
                BaseDisplayName = baseDisplayName;
                DirectoryParts = directoryParts;
            }

            public string Option { get; }
            public string BaseDisplayName { get; }
            public string[] DirectoryParts { get; }
        }
    }
}
