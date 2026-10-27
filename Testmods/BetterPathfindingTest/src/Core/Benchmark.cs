using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BetterPathfindingTest.Core
{
    public sealed class BenchmarkResult
    {
        public int Planned, Completed, Mismatches, Ties, Incomplete, ProductionDifferences, ProductionTies;
        public double PreparationMs, ColdMs, WarmMs, ReferenceMs, ProductionMs, OutputMs;
        public long PeakBytes, Rows32, Rows128, RowHits, Fields, FieldHits, Expanded;
        public string Limitation = "";
        public readonly List<object> Cases = new List<object>();
        public Dictionary<string, object> Data() => new Dictionary<string, object> {
            ["planned"] = Planned, ["completed"] = Completed, ["mismatches"] = Mismatches, ["equalCostDirectionDifferences"] = Ties,
            ["productionCostOrStatusDifferences"] = ProductionDifferences, ["productionEqualCostDirectionDifferences"] = ProductionTies, ["incomplete"] = Incomplete, ["limitation"] = Limitation,
            ["fieldPreparationMs"] = PreparationMs, ["coldSearchMs"] = ColdMs, ["warmSearchMs"] = WarmMs, ["referenceMs"] = ReferenceMs, ["productionMs"] = ProductionMs, ["privatePathEncodingAndValidationMs"] = OutputMs,
            ["navigationPeakBytes"] = PeakBytes, ["rows32"] = Rows32, ["rows128"] = Rows128, ["rowHits"] = RowHits, ["fieldsBuilt"] = Fields, ["fieldHits"] = FieldHits, ["expanded"] = Expanded, ["cases"] = Cases
        };
        public string Markdown(string title, string runtime)
        {
            var text = new StringBuilder(); text.AppendLine("# " + title).AppendLine().AppendLine("Runtime: " + runtime).AppendLine();
            text.AppendLine("| Planned | Compared | Cost/status errors | Equal-cost direction differences | Incomplete |")
                .AppendLine("|---:|---:|---:|---:|---:|").AppendLine("| " + Planned + " | " + Completed + " | " + Mismatches + " | " + Ties + " | " + Incomplete + " |").AppendLine();
            text.AppendLine("- Field preparation: " + PreparationMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms")
                .AppendLine("- Cold / warm searches: " + ColdMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " / " + WarmMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms")
                .AppendLine("- Independent full-grid reference: " + ReferenceMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms")
                .AppendLine("- Private path encoding/validation: " + OutputMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms (no native buffer write)")
                .AppendLine("- Current Precise reference (offline only): " + ProductionMs.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " ms; cost/status differences: " + ProductionDifferences + "; equal-cost direction differences: " + ProductionTies)
                .AppendLine("- Tracked navigation peak: " + PeakBytes + " bytes; 32/128 rows: " + Rows32 + "/" + Rows128 + "; field hits: " + FieldHits).AppendLine();
            text.AppendLine("Result: " + (Mismatches != 0 ? "FAILED" : Incomplete != 0 || Completed != Planned ? "INCOMPLETE" : "COST/REACHABILITY COMPARISON PASSED") + ".").AppendLine();
            if (Limitation.Length != 0) text.AppendLine("Limitation: " + Limitation).AppendLine();
            text.AppendLine("The first-search time follows field preparation; it is not an isolated cold-start total. Add preparation and graph/snapshot costs. Equal-cost path differences require separate review. A passed search comparison does not certify native movement, cadence qualification or multiplayer integration. No game orders or path buffers were modified.");
            text.AppendLine().AppendLine("The 10x target concerns comparable search workloads. Preparation, snapshot capture and updates must be included when judging total benefit; a low warm time alone is insufficient.");
            return text.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        }
    }
    public static class Benchmark
    {
        // Mono may not expose allocation totals. Never label retained-heap deltas as allocations.
        private static readonly AllocationCounter Allocations = new AllocationCounter(CreateAllocationCounter);
        private static Func<long> CreateAllocationCounter()
        { var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes); return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method); }
        private static long Fingerprint(byte[] bytes) { ulong hash = 14695981039346656037UL; foreach (byte b in bytes) hash = unchecked((hash ^ b) * 1099511628211UL); return unchecked((long)hash); }
        private static bool Same(RouteResult a, RouteResult b) => a.Status == b.Status && (a.Status != SearchStatus.Found || a.Cost == b.Cost);
        public static BenchmarkResult Run(GridSnapshot map, IList<RouteQuery> queries, WorkBudget budget,
            Func<RouteQuery, RouteResult> production = null, Action<string> progress = null)
        {
            var report = new BenchmarkResult { Planned = queries.Count };
            try
            {
                using (var navigator = new HierarchicalNavigator(map, budget))
                {
                    try
                    {
                        var watch = Stopwatch.StartNew();
                        navigator.PrepareBatch(queries); report.PreparationMs = watch.Elapsed.TotalMilliseconds;
                        for (int i = 0; i < queries.Count; i++)
                        {
                            budget.Check(); RouteQuery query = queries[i];
                            if (i % 16 == 0) progress?.Invoke("queries=" + i + "/" + queries.Count + " expanded=" + budget.Expanded + " buffers=" + budget.Bytes);
                            long memoryBefore = GC.GetTotalMemory(false), allocationBefore = Allocations.Read();
                            watch.Restart(); RouteResult cold = navigator.FindPath(query); double coldMs = watch.Elapsed.TotalMilliseconds; report.ColdMs += coldMs;
                            watch.Restart(); RouteResult warm = navigator.FindPath(query); double warmMs = watch.Elapsed.TotalMilliseconds; report.WarmMs += warmMs;
                            watch.Restart(); RouteResult reference = ReferenceSearch.Find(map, query, budget); double referenceMs = watch.Elapsed.TotalMilliseconds; report.ReferenceMs += referenceMs;
                            if (cold.Status == SearchStatus.Inconclusive || warm.Status == SearchStatus.Inconclusive || reference.Status == SearchStatus.Inconclusive)
                            { report.Incomplete++; report.Limitation = cold.Status == SearchStatus.Inconclusive ? cold.Reason : warm.Status == SearchStatus.Inconclusive ? warm.Reason : reference.Reason; report.Cases.Add(new Dictionary<string, object> { ["index"] = i, ["status"] = "Inconclusive", ["reason"] = report.Limitation }); break; }
                            bool matches = Same(cold, reference) && Same(warm, reference); report.Completed++;
                            if (!matches) report.Mismatches++;
                            bool tie = cold.Status == SearchStatus.Found && Same(cold, reference) && Fingerprint(cold.Directions) != Fingerprint(reference.Directions);
                            if (tie) report.Ties++;
                            RouteResult old = null;
                            if (production != null)
                            { watch.Restart(); old = production(query); report.ProductionMs += watch.Elapsed.TotalMilliseconds; if (!Same(old, reference)) report.ProductionDifferences++; }
                            bool productionTie = old != null && old.Status == SearchStatus.Found && Same(old, cold) && Fingerprint(old.Directions) != Fingerprint(cold.Directions);
                            if (productionTie) report.ProductionTies++;
                            int encodedBytes = 0;
                            if (cold.Status == SearchStatus.Found)
                            {
                                watch.Restart(); byte[] encoded = new byte[(cold.Directions.Length + 1) / 2];
                                for (int step = 0; step < cold.Directions.Length; step++) encoded[step >> 1] |= (byte)(cold.Directions[step] << ((step & 1) * 4));
                                encodedBytes = encoded.Length;
                                if (map.Describe(query, cold.Directions).Status != SearchStatus.Found) report.Mismatches++;
                                report.OutputMs += watch.Elapsed.TotalMilliseconds;
                            }
                            long allocationAfter = Allocations.Read();
                            var record = new Dictionary<string, object> {
                                ["index"] = i, ["start"] = query.Start, ["target"] = query.Target, ["groundCost"] = query.GroundCost, ["moatCost"] = query.MoatCost,
                                ["requireMoat"] = query.RequireMoat, ["excludeStructures"] = query.ExcludeStructures, ["boundCount"] = query.Bounds.Length,
                                ["status"] = cold.Status.ToString(), ["cost"] = cold.Cost, ["groundEdges"] = cold.Ground, ["moatEdges"] = cold.Moat, ["privateEncodedBytes"] = encodedBytes,
                                ["referenceStatus"] = reference.Status.ToString(), ["referenceCost"] = reference.Cost, ["coldMs"] = coldMs, ["warmMs"] = warmMs, ["referenceMs"] = referenceMs,
                                ["managedHeapDelta"] = GC.GetTotalMemory(false) - memoryBefore, ["threadAllocatedBytes"] = allocationBefore < 0 || allocationAfter < 0 ? -1 : allocationAfter - allocationBefore, ["pathFingerprint"] = Fingerprint(cold.Directions).ToString("X16"), ["equalCostDirectionDifference"] = tie, ["productionEqualCostDirectionDifference"] = productionTie,
                                ["productionStatus"] = old?.Status.ToString() ?? "offline-only", ["productionCost"] = old?.Cost ?? -1
                            };
                            if (!matches || tie || productionTie || (old != null && !Same(old, reference))) { record["newDirectionsBase64"] = Convert.ToBase64String(cold.Directions); record["referenceDirectionsBase64"] = Convert.ToBase64String(reference.Directions); if (old != null) record["productionDirectionsBase64"] = Convert.ToBase64String(old.Directions); }
                            report.Cases.Add(record);
                        }
                    }
                    finally { report.Rows32 = navigator.RowsBuilt32; report.Rows128 = navigator.RowsBuilt128; report.RowHits = navigator.RowHits; report.Fields = navigator.FieldsBuilt; report.FieldHits = navigator.FieldHits; }
                }
            }
            catch (SearchBudgetException ex) { report.Incomplete++; report.Limitation = ex.Message; }
            report.PeakBytes = budget.PeakBytes; report.Expanded = budget.Expanded;
            return report;
        }
        public static List<RouteQuery> Workload(GridSnapshot map, UnitSeed[] units, int count, bool formation, uint seed = 731902)
        {
            var nodes = new List<int>(); for (int n = 0; n < map.Count; n++) if (map.IsValid(n)) nodes.Add(n);
            var queries = new List<RouteQuery>(); if (nodes.Count == 0) return queries;
            var starts = new List<UnitSeed>(); foreach (UnitSeed unit in units) if (unit.Player == map.Player && map.IsValid(unit.Node)) starts.Add(unit);
            int sharedTarget = nodes[nodes.Count * 3 / 4];
            for (int i = 0; i < count; i++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                int start = starts.Count == 0 ? nodes[(int)(seed % (uint)nodes.Count)] : starts[i % starts.Count].Node;
                long ground = 1, moat = 7; int progress;
                if (starts.Count != 0 && starts[i % starts.Count].TryProfile(out long g, out long m, out progress)) { ground = g; moat = m; }
                // Shared groups use an identical profile; a separate family varies it.
                if (!formation) { ground = 1; moat = 7; }
                var q = new RouteQuery { Start = start, Target = formation ? nodes[(nodes.Count * 3 / 4 + i) % nodes.Count] : sharedTarget, GroundCost = ground, MoatCost = moat };
                if (formation && i % 4 == 0) { q.RequireMoat = true; q.ExcludeStructures = true; q.Bounds = new[] { new CostBound(ground, moat, (long)(map.Width + map.Height) * moat * 2), new CostBound(moat, ground, (long)(map.Width + map.Height) * moat * 2) }; }
                queries.Add(q);
            }
            return queries;
        }
    }
}
