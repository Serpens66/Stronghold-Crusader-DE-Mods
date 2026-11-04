using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using GroupPathProbe.Core;

// Moat decision study for the planned MoatMove anchor mode: how well and at what cost can
// "use the moat when it is cheaper" (cost minimization) be decided with the anchor core,
// compared with Fast ("moat only without a ground route") and an approximation of Precise
// (moat only when it saves at least 40 cost units; the real rule uses ticks and profiles).
// Every unit has a ground route here, so the decision actually matters.
internal static class CostMinGate
{
    private const int Margin = 8, Friendly = 1, Ground = 2, PreciseThreshold = 40;
    // Ground-only snapshot: every edge with a moat bit is removed.
    internal static GridSnapshot GroundOnly(GridSnapshot map)
    {
        uint[] words = map.CopyEdges();
        for (int i = 0; i < words.Length; i++) words[i] = (words[i] & 0xFFU & ~(words[i] >> 8)) | (words[i] & 0xFF0000U);
        return new GridSnapshot(map.Width, map.Height, map.CopyValidity(), words, map.Player);
    }
    private static double Percentile(List<long> values, double p) { if (values.Count == 0) return 0; values.Sort(); return values[Math.Min(values.Count - 1, (int)Math.Ceiling(p * values.Count) - 1)]; }
    private sealed class Decision
    {
        internal string Name; internal int Moat, Mismatch; internal readonly List<long> Loss = new List<long>(); internal long Total;
        internal object Report(long optimumTotal) => new { name = Name, moatUnits = Moat, decisionMismatches = Mismatch, lossMean = Loss.Average(), lossP95 = Percentile(new List<long>(Loss), 0.95), lossMax = Loss.Max(), totalCostOverOptimumPercent = (Total - optimumTotal) * 100.0 / optimumTotal };
    }
    private static RouteResult[] Solve(AnchorCoreInterop core, GroupLoad load, int player, int threads, out double stepMs, out double queryMs)
    {
        var tasks = new int[load.Groups];
        for (int g = 0; g < load.Groups; g++)
        {
            RouteQuery q = load.Queries[load.GroupOf.IndexOf(g)];
            int[] starts = Enumerable.Range(0, load.Queries.Count).Where(i => load.GroupOf[i] == g).Select(i => load.Queries[i].Start).ToArray();
            tasks[g] = core.CreateTask(player, load.Anchors[g], q.GroundCost, q.MoatCost, starts);
        }
        var watch = Stopwatch.StartNew(); core.Step(int.MaxValue, threads); stepMs = watch.Elapsed.TotalMilliseconds;
        var results = new RouteResult[load.Queries.Count]; watch.Restart();
        for (int i = 0; i < results.Length; i++) results[i] = core.Query(tasks[load.GroupOf[i]], load.Queries[i].Start, load.Queries[i].Target, Margin);
        queryMs = watch.Elapsed.TotalMilliseconds;
        foreach (int t in tasks) core.Release(t);
        return results;
    }
    private static object Study(GroupLoad load, int threads)
    {
        GridSnapshot ground = GroundOnly(load.Map);
        using var core = new AnchorCoreInterop(load.Map.Width, load.Map.Height, 768L * 1024 * 1024);
        core.WriteWholeMask(Friendly, load.Map, 16384); core.WriteWholeMask(Ground, ground, 16384);
        RouteResult[] bf = Solve(core, load, Friendly, threads, out double friendlyStepMs, out double friendlyQueryMs);
        RouteResult[] bg = Solve(core, load, Ground, threads, out double groundStepMs, out double groundQueryMs);
        Console.WriteLine("  anchor friendly: step=" + friendlyStepMs.ToString("F1") + " ms queries=" + friendlyQueryMs.ToString("F1") + " ms; ground: step=" + groundStepMs.ToString("F1") + " ms queries=" + groundQueryMs.ToString("F1") + " ms");
        // Exact references: weighted optimum with moat (F*) and ground-only optimum (G*).
        var friendlyOracle = new PreciseOracle(load.Map); var groundOracle = new PreciseOracle(ground);
        var exactF = new long[load.Queries.Count]; var exactG = new long[load.Queries.Count];
        var watch = Stopwatch.StartNew(); double next = 5;
        for (int i = 0; i < exactF.Length; i++)
        {
            RouteQuery q = load.Queries[i];
            RouteResult f = friendlyOracle.Find(q), g = groundOracle.Find(q);
            if (f.Status != SearchStatus.Found) throw new InvalidOperationException(load.Name + ": friendly optimum missing case=" + i);
            exactF[i] = f.Cost; exactG[i] = g.Status == SearchStatus.Found ? g.Cost : long.MaxValue;
            foreach (var (r, map) in new[] { (bf[i], load.Map), (bg[i], ground) })
                if (r.Status == SearchStatus.Found)
                {
                    RouteResult described = map.Describe(q, r.Directions);
                    if (described.Status != SearchStatus.Found || described.Cost != r.Cost) throw new InvalidOperationException(load.Name + ": illegal anchor path case=" + i);
                }
            if (watch.Elapsed.TotalSeconds >= next) { Console.WriteLine("  PROGRESS references " + (i + 1) + "/" + exactF.Length); next = watch.Elapsed.TotalSeconds + 5; }
        }
        long optimumTotal = 0; for (int i = 0; i < exactF.Length; i++) optimumTotal += Math.Min(exactF[i], exactG[i]);
        var decisions = new[] { "exact-cost-min", "fast", "precise-approx", "anchor-single-field", "anchor-two-fields" }.Select(n => new Decision { Name = n }).ToArray();
        for (int i = 0; i < exactF.Length; i++)
        {
            long f = exactF[i], g = exactG[i], best = Math.Min(f, g);
            bool exactMoat = f < g;
            long bfCost = bf[i].Status == SearchStatus.Found ? bf[i].Cost : long.MaxValue, bgCost = bg[i].Status == SearchStatus.Found ? bg[i].Cost : long.MaxValue;
            bool bfMoat = bf[i].Status == SearchStatus.Found && bf[i].Moat > 0;
            // Moat choices are published by MoatMove; ground choices stay with Vanilla,
            // which is counted with the ground optimum G* (an optimistic assumption).
            var choices = new (bool Moat, long Cost)[]
            {
                (exactMoat, exactMoat ? f : g),
                (g == long.MaxValue, g == long.MaxValue ? f : g),
                (g == long.MaxValue || g - f >= PreciseThreshold, g == long.MaxValue || g - f >= PreciseThreshold ? f : g),
                (bfMoat, bfMoat ? bfCost : g),
                (bfMoat && bfCost < bgCost, bfMoat && bfCost < bgCost ? bfCost : g),
            };
            for (int k = 0; k < decisions.Length; k++)
            {
                var (moat, cost) = choices[k]; Decision d = decisions[k];
                if (moat) d.Moat++; if (moat != exactMoat && f != g) d.Mismatch++;
                d.Loss.Add(cost - best); d.Total += cost;
            }
        }
        Console.WriteLine("  references total=" + watch.ElapsedMilliseconds + " ms");
        foreach (Decision d in decisions) Console.WriteLine("  " + d.Name.PadRight(20) + " moat=" + d.Moat + " mismatches=" + d.Mismatch + " lossMean=" + d.Loss.Average().ToString("F2") + " lossMax=" + d.Loss.Max() + " total+" + ((d.Total - optimumTotal) * 100.0 / optimumTotal).ToString("F2") + "%");
        return new
        {
            kind = "cost-min", name = load.Name, units = load.Queries.Count, groups = load.Groups, threads,
            timing = new { friendlyStepMs, friendlyQueryMs, groundStepMs, groundQueryMs },
            unitsWhereMoatIsCheaper = Enumerable.Range(0, exactF.Length).Count(i => exactF[i] < exactG[i]),
            decisions = decisions.Select(d => d.Report(optimumTotal)).ToArray(),
            scope = "ground decisions counted with the exact ground optimum G* (Vanilla's own route is not optimal in general); precise-approx uses a 40 cost-unit threshold instead of ticks/profiles"
        };
    }
    internal static int Run()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string output = Path.Combine(root, "results", "costmin-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(output);
        File.Copy(Path.GetFullPath(Path.Combine(root, "../MoatMove/.local/anchor/build.json")), Path.Combine(output, "native-build.json"));
        AnchorCoreInterop.Load();
        int threads = AnchorCoreInterop.mma_recommended_threads(); var rows = new List<object>(); int exit = 0;
        try
        {
            foreach (var (name, bridges) in new[] { ("moat-wall-5000", false), ("moat-bridges-5000", true) })
            {
                GroupLoad load = GroupWorkloads.MoatMix(GroupWorkloads.MoatMap(bridges), name, 5000, 52331);
                Console.WriteLine("BEGIN " + name + " units=" + load.Queries.Count + " groups=" + load.Groups + " threads=" + threads);
                rows.Add(Study(load, threads));
                File.WriteAllText(Path.Combine(output, "progress.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch (Exception ex) { rows.Add(new { kind = "incomplete", error = ex.ToString() }); Console.WriteLine(ex); exit = 1; }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", "\r\n"));
        Console.WriteLine("COST-MIN exit=" + exit + "; evidence=" + output); return exit;
    }
}
