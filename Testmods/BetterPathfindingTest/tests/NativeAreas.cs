using System;
using System.Collections.Generic;
using System.Threading;
using BetterPathfindingTest.Core;

internal static partial class Program
{
    private static void NativeAreaContracts()
    {
        // Exactness of the Chebyshev adaptation is NOT implied by the RSR paper.
        // Exhaust all endpoints in small rectangles, including wet cycles at start=goal.
        foreach (int width in new[] { 3, 4, 7 }) foreach (int height in new[] { 3, 5 }) foreach (bool wet in new[] { false, true })
        {
            GridSnapshot map = Grid(width, height, (a, b, d) => (1U << d) | (wet ? 1U << (8 + d) : 0));
            using (var nav = new AreaNavigator(map, new WorkBudget()))
            {
                for (int start = 0; start < map.Count; start++) for (int target = 0; target < map.Count; target++)
                {
                    Compare(nav, map, new RouteQuery { Start = start, Target = target, GroundCost = 5, MoatCost = 2, RequireMoat = wet });
                }
                Check(nav.Stats.Pruned > 0, "Uniform rectangle interiors must really disappear from the searched graph.");
            }
        }
        // Heterogeneous seams, all four directions, cheap-moat profiles and external
        // detours cheaper than a same-rectangle direct walk.
        var rng = new Random(912703);
        for (int trial = 0; trial < 12; trial++)
        {
            int width = 17, height = 13;
            var map = Grid(width, height, (a, b, d) => {
                int ax = a % width, bx = b % width;
                if ((ax == 8 || bx == 8) && a / width != 6 && b / width != 6) return 0U;
                bool wet = a / width < 4 || b / width < 4;
                return (1U << d) | (wet ? 1U << (8 + d) : 0) | (a / width == 10 && b / width == 10 ? 1U << (16 + d) : 0);
            });
            using (var nav = new AreaNavigator(map, new WorkBudget()))
            {
                var oracle = new ProductionOracle(map);
                for (int i = 0; i < 40; i++)
                {
                    var q = new RouteQuery { Start = rng.Next(map.Count), Target = rng.Next(map.Count), GroundCost = trial % 2 == 0 ? 1 : 7, MoatCost = trial % 2 == 0 ? 7 : 1, RequireMoat = i % 3 == 0, ExcludeStructures = i % 4 == 0, MaximumEdges = i % 5 == 0 ? 12 : 100 };
                    if (i % 7 == 0) q.Bounds = new[] { new CostBound(2, 3, 70), new CostBound(5, 1, 75) };
                    Compare(nav, map, q, trial % 2 == 0 ? oracle : null);
                }
            }
        }
        var shared = Workloads.Synthetic(48, true); var batch = Workloads.SyntheticBatch(shared, 128, true);
        var budget = new WorkBudget();
        using (var nav = new AreaNavigator(shared, budget))
        {
            RouteResult[] firstBatch = nav.FindPaths(batch), secondBatch = nav.FindPaths(batch);
            Check(nav.Stats.Fields == 1 && nav.Stats.Hits >= 255, "A whole group must share ONE actual reverse field.");
            Check(nav.Stats.LocalFields > 0 && nav.Stats.LocalHits > 0, "Shared groups must use actual local flow fields with global continuation costs.");
            for (int i = 0; i < batch.Count; i++)
            {
                Check(firstBatch[i].Status == SearchStatus.Found && firstBatch[i].Moat > 0, "Positive moat batch coverage missing.");
                Check(Convert.ToBase64String(firstBatch[i].Directions) == Convert.ToBase64String(secondBatch[i].Directions), "Repeated batches must be deterministic.");
            }
            var unchanged = new GridSnapshot(shared.Width, shared.Height, shared.CopyValidity(), shared.CopyEdges(), shared.Player);
            nav.Update(unchanged); nav.FindPath(batch[0]); Check(nav.ChangedNodes == 0 && nav.Stats.Fields == 1, "Unchanged generations must retain fields.");
        }
        Check(budget.Bytes == 0, "Native, result and managed buffers must all be released.");
        var cancelled = new CancellationTokenSource(); var cancelling = new WorkBudget(cancellation: cancelled.Token);
        using (var nav = new AreaNavigator(shared, cancelling))
        { cancelled.Cancel(); bool observed = false; try { nav.FindPaths(batch); } catch (OperationCanceledException) { observed = true; } Check(observed, "A cancelled batch must not continue native stepping."); }
        Check(cancelling.Bytes == 0, "Cancellation must release owned native buffers.");
        var smallBudget = new WorkBudget(6L * 1024 * 1024);
        using (var nav = new AreaNavigator(shared, smallBudget))
        {
            var pressure = Workloads.SyntheticBatch(shared, 10000, true); RouteResult[] limited = nav.FindPaths(pressure); bool incomplete = false;
            foreach (RouteResult r in limited) { Check(r.Status != SearchStatus.Unreachable, "Memory pressure must never become unreachable."); incomplete |= r.Status == SearchStatus.Inconclusive; }
            Check(incomplete, "Memory-pressure fixture did not exhaust its bound.");
            RouteResult recovered = nav.FindPath(batch[0]); Check(recovered.Status == SearchStatus.Found && recovered.Moat > 0, "An interrupted field must be discarded, then rebuilt correctly.");
        }
        Check(smallBudget.Bytes == 0, "Failed batches must release all counted buffers.");
        var allWet = Grid(17, 13, (a, b, d) => (1U << d) | (1U << (8 + d)));
        foreach (GridSnapshot localMap in new[] { allWet, shared })
        using (var nav = new AreaNavigator(localMap, new WorkBudget()))
        {
            var localBatch = new List<RouteQuery>();
            for (int i = 0; i < 128; i++) localBatch.Add(new RouteQuery { Start = localMap.Width + 1 + i % (localMap.Width - 2), Target = localMap.Width * 3 + 3, RequireMoat = true });
            RouteResult[] actual = nav.FindPaths(localBatch);
            for (int i = 0; i < actual.Length; i++)
            { RouteResult reference = ReferenceSearch.Find(localMap, localBatch[i], new WorkBudget()); Check(actual[i].Status == reference.Status && actual[i].Cost == reference.Cost && localMap.Describe(localBatch[i], actual[i].Directions).Status == SearchStatus.Found, "Local field wet-state/return-to-goal contract."); }
        }
        var structured = Grid(5, 5, (a, b, d) => (1U << d) | (1U << (16 + d)));
        using (var nav = new AreaNavigator(structured, new WorkBudget())) Compare(nav, structured, new RouteQuery { Start = 12, Target = 12, ExcludeStructures = true });
        foreach (string family in new[] { "shared-goal", "formation", "distributed-profiles", "moat-required" })
        {
            var queries = Workloads.Create(shared, new UnitSeed[0], 32, family);
            Check(queries.Count == 32, "Diagnostic workload must include positive inputs: " + family);
            using (var nav = new AreaNavigator(shared, new WorkBudget())) foreach (RouteQuery q in queries) Compare(nav, shared, q);
        }
    }
}
