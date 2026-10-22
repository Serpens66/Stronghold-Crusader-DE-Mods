using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    // Pure coordinates only: no native pointer, building identity or live topology.
    // Readers retain immutable lists across nested queries and bounded cache clears.
    internal static class GatehouseFootprintGeometryCache
    {
        private const int Capacity = 256;
        [ThreadStatic] private static Dictionary<Key, IReadOnlyList<APIShared.GatehouseFootprintCandidate>> entries;

        internal static IReadOnlyList<APIShared.GatehouseFootprintCandidate> Get(int x, int y, int size)
        {
            var key = new Key(x, y, size);
            var cache = entries ?? (entries = new Dictionary<Key, IReadOnlyList<APIShared.GatehouseFootprintCandidate>>(Capacity));
            if (cache.TryGetValue(key, out var result))
            {
#if SERP_DEVDIAG
                GatehouseDiagnosticRuntime.Current.FootprintCacheHits++;
#endif
                return result;
            }
#if SERP_DEVDIAG
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            GatehouseDiagnosticRuntime.Current.FootprintCalls++;
            try
            {
#endif
            result = APIShared.GatehouseDrawbridgeCoupling.BuildOrderedFootprintCandidates(x, y, size).AsReadOnly();
#if SERP_DEVDIAG
            }
            finally { GatehouseDiagnosticRuntime.Current.FootprintTicks += System.Diagnostics.Stopwatch.GetTimestamp() - started; }
#endif
            if (cache.Count == Capacity) cache.Clear();
            cache.Add(key, result);
            return result;
        }

        private readonly struct Key : IEquatable<Key>
        {
            private readonly int x, y, size;
            internal Key(int x, int y, int size) { this.x = x; this.y = y; this.size = size; }
            public bool Equals(Key other) => x == other.x && y == other.y && size == other.size;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() { unchecked { return ((x * 397) ^ y) * 397 ^ size; } }
        }
    }
}
