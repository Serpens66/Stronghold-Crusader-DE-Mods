using System.Collections.Generic;

namespace EnemyGatePathfindingTest
{
    // No event history or cap: one row per exception type, exact repetition counts.
    // Native callbacks record only; the persistent deferred publisher performs logging.
    internal sealed class DeferredGateDiagnosticErrors
    {
        private sealed class Row { internal long Count; internal string First, Last; internal bool Reported; }
        private readonly object sync = new object();
        private readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        private int unreportedCauses;
        internal void Record(string cause, string detail)
        {
            lock (sync)
            {
                if (!rows.TryGetValue(cause, out Row row))
                {
                    rows.Add(cause, row = new Row { First = detail });
                    unreportedCauses++;
                }
                row.Count++;
                row.Last = detail;
            }
        }
        internal string[] DrainNewCauses()
        {
            lock (sync)
            {
                if (unreportedCauses == 0) return System.Array.Empty<string>();
                var output = new List<string>();
                foreach (var pair in rows)
                {
                    if (pair.Value.Reported) continue;
                    pair.Value.Reported = true;
                    output.Add(pair.Key + ",count=" + pair.Value.Count + ",first=" + pair.Value.First);
                }
                unreportedCauses = 0;
                return output.ToArray();
            }
        }
        internal string Summary
        {
            get
            {
                lock (sync)
                {
                    var output = new List<string>();
                    foreach (var pair in rows)
                        output.Add(pair.Key + ",count=" + pair.Value.Count + ",first=" + pair.Value.First + ",last=" + pair.Value.Last);
                    return string.Join(";", output);
                }
            }
        }
        internal void Reset() { lock (sync) { rows.Clear(); unreportedCauses = 0; } }
    }
}
