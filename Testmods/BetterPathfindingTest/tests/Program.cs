using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using BetterPathfindingTest.Core;

internal static partial class Program
{
    private static int assertions, comparisons, productionDifferences, ties;
    private static readonly string Evidence = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../results/offline-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
    private static void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
    internal static GridSnapshot Grid(int width, int height, Func<int, int, int, uint> edge = null)
    {
        var valid = new byte[width * height]; var words = new uint[valid.Length];
        for (int n = 0; n < valid.Length; n++) { valid[n] = 1; for (int d = 0; d < 8; d++) { int x = n % width + GridSnapshot.Dx[d], y = n / width + GridSnapshot.Dy[d]; if ((uint)x < width && (uint)y < height) words[n] |= edge == null ? 1U << d : edge(n, y * width + x, d); } }
        return new GridSnapshot(width, height, valid, words);
    }
    private static void Compare(HierarchicalNavigator nav, GridSnapshot map, RouteQuery query, ProductionOracle oracle = null)
    {
        RouteResult actual = nav.FindPath(query), expected = ReferenceSearch.Find(map, query, new WorkBudget(128L * 1024 * 1024, 15)); comparisons++;
        Check(expected.Status != SearchStatus.Inconclusive, "Reference budget: " + expected.Reason);
        Check(actual.Status == expected.Status, "Status mismatch " + actual.Status + "/" + expected.Status + " " + actual.Reason);
        if (actual.Status == SearchStatus.Found)
        {
            Check(actual.Cost == expected.Cost, "Optimal cost differs.");
            Check(map.Describe(query, actual.Directions).Status == SearchStatus.Found, "Invalid route.");
        }
        if (oracle != null)
        {
            RouteResult old = oracle.Find(query);
            if (old.Status != expected.Status || (old.Status == SearchStatus.Found && old.Cost != expected.Cost))
            {
                productionDifferences++; Directory.CreateDirectory(Evidence);
                SnapshotCodec.WriteQueries(Path.Combine(Evidence, "precise-difference-" + productionDifferences + ".bpf.gz"), map, oracle.History);
                if (productionDifferences <= 3) Console.WriteLine("PRECISE_REVIEW case=" + comparisons + " start=" + query.Start + " target=" + query.Target + " requireMoat=" + query.RequireMoat + " bounds=" + query.Bounds.Length + " original=" + old.Status + "/" + old.Cost + " exact=" + expected.Status + "/" + expected.Cost);
            }
            else if (old.Status == SearchStatus.Found && Convert.ToBase64String(old.Directions) != Convert.ToBase64String(actual.Directions))
            { ties++; Directory.CreateDirectory(Evidence); SnapshotCodec.WriteQueries(Path.Combine(Evidence, "precise-tie-" + ties + ".bpf.gz"), map, oracle.History); }
        }
    }
    private static void RandomDirected()
    {
        var rng = new Random(731902);
        for (int trial = 0; trial < 36; trial++)
        {
            GridSnapshot map = Grid(8, 7, (a, b, d) => rng.Next(100) < 32 ? 0U : (1U << d) | (rng.Next(100) < 23 ? 1U << (8 + d) : 0U) | (rng.Next(100) < 8 ? 1U << (16 + d) : 0U));
            var oracle = new ProductionOracle(map);
            using (var nav = new HierarchicalNavigator(map, new WorkBudget(128L * 1024 * 1024, 30)))
                for (int i = 0; i < 7; i++)
                {
                    var q = new RouteQuery { Start = rng.Next(map.Count), Target = rng.Next(map.Count), GroundCost = 1 + rng.Next(3), MoatCost = 4 + rng.Next(6), MaximumEdges = 20 + rng.Next(10), RequireMoat = i % 3 == 0, ExcludeStructures = i % 4 == 0 };
                    if (i % 2 == 0) q.Bounds = new[] { new CostBound(2, 1, 18 + rng.Next(20)), new CostBound(1, 5, 20 + rng.Next(30)) };
                    Compare(nav, map, q, oracle);
                }
        }
    }
    private static void HierarchyAndFields()
    {
        GridSnapshot map = Grid(133, 5, (a, b, d) => (1U << d) | ((b % 133 == 64 || b % 133 == 65) ? 1U << (8 + d) : 0));
        using (var nav = new HierarchicalNavigator(map, new WorkBudget(200L * 1024 * 1024, 45)))
        {
            var query = new RouteQuery { Start = 2 * 133 + 1, Target = 2 * 133 + 131, GroundCost = 2, MoatCost = 13, MaximumEdges = 200 };
            var oracle = new ProductionOracle(map); Compare(nav, map, query, oracle);
            Check(nav.RowsBuilt32 > 0 && nav.RowsBuilt128 > 0, "Both hierarchy levels must execute.");
            var batch = new List<RouteQuery>();
            for (int i = 0; i < 32; i++) { var q = query.Copy(); q.Start = (i % 5) * 133 + 1 + i % 24; batch.Add(q); }
            nav.PrepareBatch(batch); foreach (RouteQuery q in batch) Compare(nav, map, q, oracle);
            Check(nav.FieldsBuilt > 0 && nav.FieldHits >= 32, "Actual globally seeded local fields must be used.");
            uint[] words = map.CopyEdges(); int cut = 2 * 133 + 60; words[cut] = 0;
            GridSnapshot next = new GridSnapshot(133, 5, map.CopyValidity(), words); nav.Update(next);
            Check(nav.InvalidatedRows > 0, "Updated sectors must invalidate their rows.");
            Compare(nav, next, query);
        }
    }
    private static void Contracts()
    {
        var map = Grid(4, 2, (a, b, d) => b == 3 ? 0U : 1U << d);
        using (var nav = new HierarchicalNavigator(map, new WorkBudget()))
        {
            var q = new RouteQuery { Start = 0, Target = 3, MaximumEdges = 20 };
            Compare(nav, map, q); q.Terminals = new[] { new TerminalEdge(2, 3, true) }; q.RequireMoat = true; Compare(nav, map, q);
            q.MaximumEdges = 2; Compare(nav, map, q);
            q.MaximumEdges = 20; q.Bounds = new[] { new CostBound(1, 10, 4) }; Compare(nav, map, q);
        }
        var oneWay = Grid(132, 1, (a, b, d) => d == 2 ? 1U << d : 0);
        using (var nav = new HierarchicalNavigator(oneWay, new WorkBudget()))
        {
            Compare(nav, oneWay, new RouteQuery { Start = 0, Target = 131 });
            Compare(nav, oneWay, new RouteQuery { Start = 131, Target = 0 });
        }
        Check(PublicationRules.Improves(10, 0, 5, 0, new long[] { 8, 9 }, new long[] { 56, 57 }, new[] { 1, 1 }), "40 tick boundary.");
        Check(!PublicationRules.Improves(10, 0, 5, 0, new long[] { 8, 1 }, new long[] { 56, 57 }, new[] { 1, 10 }), "Other plausible profile must improve too.");
        Check(!PublicationRules.Improves(10, 0, 5, 0, new long[] { 7 }, new long[] { 55 }, new[] { 1 }), "39-or-less margin rejected.");
        bool limited = false; try { using (var nav = new HierarchicalNavigator(map, new WorkBudget(1000))) { } } catch (SearchBudgetException) { limited = true; }
        Check(limited, "Memory exhaustion is explicit, not unreachable.");
        var longMap = Grid(800, 4, (a, b, d) =>
        { int y = a / 800; bool go = (y % 2 == 0 ? d == 2 : d == 6) && b / 800 == y; bool turn = d == 4 && (a % 800 == (y % 2 == 0 ? 799 : 0)); return go || turn ? 1U << d : 0; });
        using (var nav = new HierarchicalNavigator(longMap, new WorkBudget(200L * 1024 * 1024, 30)))
        {
            var q = new RouteQuery { Start = 0, Target = 2400, MaximumEdges = 2000 };
            Compare(nav, longMap, q);
            Check(nav.CanReach(q, out SearchStatus status) && status == SearchStatus.Found, "Topological reachability is independent of packed length.");
        }
    }
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 0 && args[0] == "--replay") return Replay.Run(args);
            if (args.Length != 0 && args[0] == "--bench") return OfflineBench.Run();
            var watch = Stopwatch.StartNew(); Contracts(); RandomDirected(); HierarchyAndFields(); AdvancedContracts();
            Console.WriteLine("PASS assertions=" + assertions + " referenceComparisons=" + comparisons + " productionCostOrStatusDifferences=" + productionDifferences + " productionTiePathDifferences=" + ties + " elapsedMs=" + watch.ElapsedMilliseconds);
            if (productionDifferences != 0) Console.WriteLine("PRECISE_PARITY_REVIEW differences=" + productionDifferences + "; evidence=" + Evidence + "; no consumer acceptance claimed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
