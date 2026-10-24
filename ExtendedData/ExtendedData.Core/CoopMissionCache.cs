using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtendedData.Core
{
    internal sealed class CoopMissionDependencies
    {
        private readonly int catalogueRevision;
        private readonly object installedMapHeader;
        private readonly KeyValuePair<string, string>[] files;

        internal CoopMissionDependencies(int catalogueRevision, IEnumerable<string> paths, object installedMapHeader)
        {
            this.catalogueRevision = catalogueRevision;
            this.installedMapHeader = installedMapHeader;
            files = paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new KeyValuePair<string, string>(path, CoopTrailPackageSession.FileState(path))).ToArray();
        }

        internal bool IsCurrent(int revision, object currentMapHeader) =>
            revision == catalogueRevision && ReferenceEquals(installedMapHeader, currentMapHeader) &&
            files.All(file => string.Equals(file.Value, CoopTrailPackageSession.FileState(file.Key), StringComparison.Ordinal));
    }

    internal sealed class CoopMissionCache<T> where T : class
    {
        private readonly Dictionary<int, T> entries = new Dictionary<int, T>();
        internal void Clear() => entries.Clear();

        internal T GetOrResolve(LoadedMission mission, Func<LoadedMission, T> resolve,
            Func<T, bool> isCurrent, out bool cacheHit)
        {
            int key = MissionCatalog.ToKey(mission.TrailNumber, mission.MissionNumber);
            if (entries.TryGetValue(key, out T value) && isCurrent(value))
            {
                cacheHit = true;
                return value;
            }
            cacheHit = false;
            entries.Remove(key);
            value = resolve(mission) ?? throw new InvalidOperationException("Mission resolution returned no data.");
            entries[key] = value;
            return value;
        }
    }

    // Own only entries inserted by this runtime; do not overwrite a later foreign replacement.
    internal sealed class CoopMissionTableOverrides<T> where T : class
    {
        private sealed class Entry { internal T[] Table; internal int Index; internal T Original, Replacement; }
        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();

        internal void Install(int key, T[] table, int index, T replacement)
        {
            if (table == null || index < 0 || index >= table.Length)
                throw new InvalidOperationException("Coop mission table is unavailable.");
            if (!entries.TryGetValue(key, out Entry entry))
                entries[key] = entry = new Entry { Table = table, Index = index, Original = table[index] };
            else if (!ReferenceEquals(entry.Table, table))
                throw new InvalidOperationException("Coop mission table changed before restoration.");
            else if (!ReferenceEquals(table[index], entry.Replacement))
                entry.Original = table[index];
            table[index] = entry.Replacement = replacement;
        }

        internal void Restore()
        {
            foreach (Entry entry in entries.Values)
                if (ReferenceEquals(entry.Table[entry.Index], entry.Replacement)) entry.Table[entry.Index] = entry.Original;
            entries.Clear();
        }
    }
}
