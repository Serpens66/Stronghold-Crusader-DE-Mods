using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace BetterPathfindingTest.Core
{
    public sealed class BenchmarkResult
    {
        public int Planned, Completed, Mismatches, Ties, Incomplete, ProductionDifferences, ProductionTies;
        public int Found, Unreachable, ReferenceCompared, ReferencePlanned, MoatPaths, NativeCompleted;
        public double PreparationMs, ColdMs, WarmMs, ReferenceMs, ProductionMs, OutputMs, MaximumSliceMs, P95CompletionMs, MaximumCompletionMs;
        public long PeakBytes, Regions, Vertices, Pruned, Fields, FieldHits, Expanded, Macros, Refinements, LocalFields, LocalHits;
        public long ManagedSolverAllocatedBytes = -1, NativeAllocatedBytes, NativeAllocationCalls, NativePeakBytes, ForwardSearches, AnalyticRoutes;
        public string Limitation = "";
        public readonly List<object> Cases = new List<object>();
        public Dictionary<string, object> Data() => new Dictionary<string, object> {
            ["planned"] = Planned, ["completed"] = Completed, ["found"] = Found, ["unreachable"] = Unreachable, ["moatPaths"] = MoatPaths,
            ["mismatches"] = Mismatches, ["equalCostDirectionDifferences"] = Ties, ["productionCostOrStatusDifferences"] = ProductionDifferences,
            ["productionEqualCostDirectionDifferences"] = ProductionTies, ["incomplete"] = Incomplete, ["limitation"] = Limitation,
            ["nativeColdCompleted"] = NativeCompleted, ["referenceCompared"] = ReferenceCompared, ["referencePlanned"] = ReferencePlanned, ["referenceScope"] = ReferencePlanned == Planned ? "exhaustive" : "deterministic sample; all returned paths edge/contract-validated",
            ["fieldPreparationMs"] = PreparationMs, ["coldSearchMs"] = ColdMs, ["warmSearchMs"] = WarmMs, ["referenceMs"] = ReferenceMs, ["productionMs"] = ProductionMs,
            ["privatePathEncodingAndValidationMs"] = OutputMs, ["navigationPeakBytes"] = PeakBytes, ["regions"] = Regions, ["vertices"] = Vertices, ["prunedInteriorCells"] = Pruned,
            ["localFieldsBuilt"] = LocalFields, ["localFieldHits"] = LocalHits, ["fieldsBuilt"] = Fields, ["fieldHits"] = FieldHits, ["expanded"] = Expanded, ["macroRelaxations"] = Macros, ["refinements"] = Refinements,
            ["maximumNativeSliceMs"] = MaximumSliceMs, ["p95BatchCompletionMs"] = P95CompletionMs, ["maximumBatchCompletionMs"] = MaximumCompletionMs,
            ["managedSolverThreadAllocatedBytes"] = ManagedSolverAllocatedBytes, ["nativeAllocatedBytes"] = NativeAllocatedBytes, ["nativeAllocationCalls"] = NativeAllocationCalls, ["nativePeakBytes"] = NativePeakBytes, ["forwardAreaSearches"] = ForwardSearches, ["analyticRoutes"] = AnalyticRoutes, ["reactionTargetMs"] = 100, ["privatePlanningAndValidationMs"] = PreparationMs + ColdMs + OutputMs, ["reactionTargetMet"] = PreparationMs + ColdMs + OutputMs <= 100 && Completed == Planned, ["cases"] = Cases
        };
        public string Markdown(string title, string runtime)
        {
            var t = new StringBuilder(); t.AppendLine("# " + title).AppendLine().AppendLine("Runtime: " + runtime).AppendLine();
            t.AppendLine("| Requests | Completed | Found | Unreachable | Errors | Incomplete | Reference checked |")
             .AppendLine("|---:|---:|---:|---:|---:|---:|---:|")
             .AppendLine("| " + Planned + " | " + Completed + " | " + Found + " | " + Unreachable + " | " + Mismatches + " | " + Incomplete + " | " + ReferenceCompared + "/" + ReferencePlanned + " |").AppendLine();
            t.AppendLine("- Native map/reduction build: " + F(PreparationMs) + " ms")
             .AppendLine("- Cold / warm native batches (includes private path output): " + F(ColdMs) + " / " + F(WarmMs) + " ms")
             .AppendLine("- Additional managed encoding/validation: " + F(OutputMs) + " ms")
             .AppendLine("- Independent reference: " + F(ReferenceMs) + " ms; scope: " + (ReferencePlanned == Planned ? "exhaustive" : "sampled"))
             .AppendLine("- Current Precise oracle (offline only): " + F(ProductionMs) + " ms; cost/status errors " + ProductionDifferences + "; direction differences " + ProductionTies)
             .AppendLine("- Regions / boundary vertices / pruned cells: " + Regions + " / " + Vertices + " / " + Pruned)
             .AppendLine("- Fields / hits / macro relaxations / resource refinements: " + Fields + " / " + FieldHits + " / " + Macros + " / " + Refinements)
             .AppendLine("- Tracked managed + native peak: " + PeakBytes + " bytes")
             .AppendLine("- Native completion p95 / maximum (before managed result availability): " + F(P95CompletionMs) + " / " + F(MaximumCompletionMs) + " ms; target ~100 ms")
             .AppendLine("- Maximum native worker slice: " + F(MaximumSliceMs) + " ms").AppendLine();
            string status = Mismatches != 0 || ProductionDifferences != 0 ? "FAILED" : Incomplete != 0 || Completed != Planned || ReferenceCompared != ReferencePlanned ? "INCOMPLETE" : Found == 0 ? "NEGATIVE CASES ONLY; no successful-route acceptance" : "CHECKED CASES PASSED; coverage and direction review required";
            t.AppendLine("Result: " + status + ".").AppendLine(); if (Limitation.Length != 0) t.AppendLine("Limitation: " + Limitation).AppendLine();
            t.AppendLine("This is a read-only architecture experiment, not a game-movement or 10,000-unit acceptance. Requests are not necessarily distinct captured units. Preparation, updates and all output costs belong in the total comparison. Reference samples do not certify every request's optimal cost. Equal-cost direction differences remain reviewable. No native commands, paths or settings were changed.");
            return t.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n");
        }
        private static string F(double n) => n.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
    }
    public static class Benchmark
    {
        private static readonly AllocationCounter Allocations = new AllocationCounter(() => { var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes); return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method); });
        private static bool Same(RouteResult a, RouteResult b) => a.Status == b.Status && (a.Status != SearchStatus.Found || a.Cost == b.Cost);
        private static bool SamePath(RouteResult a, RouteResult b) => Convert.ToBase64String(a.Directions) == Convert.ToBase64String(b.Directions);
        public static BenchmarkResult Run(GridSnapshot map, IList<RouteQuery> queries, WorkBudget budget,
            Func<RouteQuery, RouteResult> production = null, Action<string> progress = null)
        {
            var r = new BenchmarkResult { Planned = queries.Count, ReferencePlanned = map.Count <= 4096 ? queries.Count : Math.Min(3, queries.Count) };
            try
            {
                long allocatedBefore = Allocations.Read(); var clock = Stopwatch.StartNew();
                using (var nav = new AreaNavigator(map, budget))
                {
                    r.PreparationMs = clock.Elapsed.TotalMilliseconds;
                    clock.Restart(); RouteResult[] cold = nav.FindPaths(queries); r.ColdMs = clock.Elapsed.TotalMilliseconds;
                    clock.Restart(); RouteResult[] warm = nav.FindPaths(queries); r.WarmMs = clock.Elapsed.TotalMilliseconds;
                    foreach (RouteResult result in cold) if (result.Status != SearchStatus.Inconclusive) r.NativeCompleted++;
                    CaptureStatistics(r, nav);
                    long allocatedAfter = Allocations.Read(); if (allocatedBefore >= 0 && allocatedAfter >= 0) r.ManagedSolverAllocatedBytes = allocatedAfter - allocatedBefore;
                    var times = new List<double>();
                    for (int i = 0; i < queries.Count; i++)
                    {
                        budget.Check(); RouteQuery q = queries[i]; RouteResult a = cold[i], b = warm[i];
                        if (i % 64 == 0) progress?.Invoke("checked=" + i + "/" + queries.Count);
                        if (a.Status == SearchStatus.Inconclusive || b.Status == SearchStatus.Inconclusive) { r.Incomplete++; r.Limitation = a.Status == SearchStatus.Inconclusive ? a.Reason : b.Reason; continue; }
                        r.Completed++; if (a.Status == SearchStatus.Found) { r.Found++; if (a.Moat != 0) r.MoatPaths++; } else r.Unreachable++;
                        times.Add(a.CompletionMs);
                        bool match = Same(a, b); clock.Restart();
                        if (a.Status == SearchStatus.Found)
                        {
                            RouteResult described = map.Describe(q, a.Directions);
                            RouteResult warmDescription = map.Describe(q, b.Directions);
                            if (described.Status != SearchStatus.Found || described.Cost != a.Cost || described.Ground != a.Ground || described.Moat != a.Moat || warmDescription.Status != SearchStatus.Found || warmDescription.Cost != b.Cost) match = false;
                            var packed = new byte[(a.Directions.Length + 1) / 2]; for (int step = 0; step < a.Directions.Length; step++) packed[step >> 1] |= (byte)(a.Directions[step] << ((step & 1) * 4));
                        }
                        r.OutputMs += clock.Elapsed.TotalMilliseconds;
                        // Independent budgets: exhausted solver caches cannot starve the oracle.
                        RouteResult reference = null;
                        bool referenceCase = r.ReferencePlanned == queries.Count || i < r.ReferencePlanned;
                        if (referenceCase)
                        {
                            clock.Restart(); reference = ReferenceSearch.Find(map, q, new WorkBudget(budget.MaximumBytes, 10, budget.Cancellation)); r.ReferenceMs += clock.Elapsed.TotalMilliseconds;
                            if (reference.Status == SearchStatus.Inconclusive) { r.Incomplete++; r.Limitation = "independent-reference: " + reference.Reason; }
                            else { r.ReferenceCompared++; match &= Same(a, reference); if (a.Status == SearchStatus.Found && Same(a, reference) && !SamePath(a, reference)) r.Ties++; }
                        }
                        RouteResult old = null;
                        if (production != null) { clock.Restart(); old = production(q); r.ProductionMs += clock.Elapsed.TotalMilliseconds; if (!Same(a, old)) r.ProductionDifferences++; if (old.Status == SearchStatus.Found && Same(a, old) && !SamePath(a, old)) r.ProductionTies++; }
                        if (!match) r.Mismatches++;
                        var data = new Dictionary<string, object> { ["index"] = i, ["start"] = q.Start, ["target"] = q.Target, ["groundCost"] = q.GroundCost, ["moatCost"] = q.MoatCost,
                            ["requireMoat"] = q.RequireMoat, ["boundCount"] = q.Bounds.Length, ["status"] = a.Status.ToString(), ["cost"] = a.Cost, ["groundEdges"] = a.Ground, ["moatEdges"] = a.Moat,
                            ["completionMs"] = a.CompletionMs, ["referenceStatus"] = reference?.Status.ToString() ?? "NOT_SAMPLED", ["referenceCost"] = reference?.Cost ?? -1,
                            ["productionStatus"] = old?.Status.ToString() ?? "offline-only", ["productionCost"] = old?.Cost ?? -1 };
                        if (!match || r.Cases.Count < 16 && (old != null && !SamePath(a, old) || reference != null && !SamePath(a, reference))) { data["newDirectionsBase64"] = Convert.ToBase64String(a.Directions); if (reference != null) data["referenceDirectionsBase64"] = Convert.ToBase64String(reference.Directions); if (old != null) data["productionDirectionsBase64"] = Convert.ToBase64String(old.Directions); }
                        r.Cases.Add(data);
                    }
                    if (times.Count != 0) { times.Sort(); r.P95CompletionMs = times[(int)Math.Ceiling(times.Count * .95) - 1]; r.MaximumCompletionMs = times[times.Count - 1]; }
                    CaptureStatistics(r, nav);
                }
            }
            catch (SearchBudgetException ex) { r.Incomplete++; r.Limitation = ex.Message; }
            r.PeakBytes = budget.PeakBytes; return r;
        }
        private static void CaptureStatistics(BenchmarkResult r, AreaNavigator nav)
        {
            AreaNavigator.Statistics s = nav.Stats; r.Regions = s.Regions; r.Vertices = s.Vertices; r.Pruned = s.Pruned; r.Fields = s.Fields; r.FieldHits = s.Hits; r.Expanded = s.Expanded; r.Macros = s.Macros; r.Refinements = s.Refinements; r.MaximumSliceMs = nav.MaximumSliceMs; r.LocalFields = s.LocalFields; r.LocalHits = s.LocalHits; r.NativeAllocatedBytes = nav.NativeAllocatedBytes; r.NativeAllocationCalls = nav.NativeAllocationCalls; r.NativePeakBytes = s.Peak; r.ForwardSearches = nav.ForwardSearches; r.AnalyticRoutes = nav.AnalyticRoutes;
        }
        // Convenience adapter; workloads explicitly distinguish coherent formations from
        // distributed targets. No captured-unit-count claim follows from request count.
        public static List<RouteQuery> Workload(GridSnapshot map, UnitSeed[] units, int count, bool formation, uint seed = 731902)
            => Workloads.Create(map, units, count, formation ? "distributed-profiles" : "shared-goal", seed);
    }
}