// TEMP_GATE_ROUTE_ACCEPTANCE: pure bounded-dimension counters, no event history.
using System;
using System.Collections.Generic;
namespace EnemyGatePathfindingTest
{
    internal sealed class TemporaryGateAcceptanceAggregate
    {
        internal sealed class Row
        {
            internal long Count, Changes;
            internal string First, Last, FirstTarget, LastTarget;
        }
        private readonly Dictionary<string, Row> window = new Dictionary<string, Row>();
        internal long Total { get; private set; }
        internal static string RouteVerdict(RouteTilePolicySnapshot entry, RouteTilePolicySnapshot current,
            int player, long edges, int result, string status, bool invalid, bool violations, bool stationary = false)
        {
            if (invalid || player <= 0 || player > 8) return "unclear:invalid-edge-or-player";
            if (!ReferenceEquals(entry, current)) return "unclear:snapshot-changed";
            if (entry == null || entry == RouteTilePolicySnapshot.Empty) return "unclear:no-policy-snapshot";
            if (status != "decoded") return "unclear:" + status;
            if (result < 0 || (result == 0 && !stationary) || edges != result) return "unclear:incomplete-edges";
            return violations ? "violated" : "checked";
        }
        internal void Record(string key, string target, string detail)
        {
            lock (window)
            {
                Total++;
                if (!window.TryGetValue(key, out Row row))
                    window.Add(key, row = new Row { First = detail, FirstTarget = target });
                if (row.Count != 0 && row.LastTarget != target) row.Changes++;
                row.Count++; row.Last = detail; row.LastTarget = target;
            }
        }
        internal string[] Drain()
        {
            lock (window)
            {
                var rows = new List<string>();
                foreach (var pair in window)
                    rows.Add(pair.Key + ",count=" + pair.Value.Count + ",targetChanges=" + pair.Value.Changes +
                        ",first=[" + pair.Value.First + "],last=[" + pair.Value.Last + "]");
                window.Clear(); rows.Sort(StringComparer.Ordinal); return rows.ToArray();
            }
        }
        internal void Reset() { lock (window) { window.Clear(); Total = 0; } }
    }
}
