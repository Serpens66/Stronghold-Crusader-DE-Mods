using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using GroupPathProbe.Core;

internal static class GroupGate
{
    private static int comparisons;
    internal static GridSnapshot Grid(int width, int height, Func<int, int, int, uint> edge = null, Func<int, bool> validNode = null)
    {
        var valid = new byte[width * height]; var words = new uint[valid.Length];
        for (int n = 0; n < valid.Length; n++) valid[n] = validNode == null || validNode(n) ? (byte)1 : (byte)0;
        for (int n = 0; n < valid.Length; n++)
        {
            if (valid[n] == 0) continue;
            for (int d = 0; d < 8; d++) { int x = n % width + GridSnapshot.Dx[d], y = n / width + GridSnapshot.Dy[d]; if ((uint)x < width && (uint)y < height && valid[y * width + x] != 0) words[n] |= edge == null ? 1U << d : edge(n, y * width + x, d); }
        }
        return new GridSnapshot(width, height, valid, words);
    }
    private static void Check(GroupLoad load, RouteResult[] exact, RouteResult[] anchor)
    {
        for (int i = 0; i < load.Queries.Count; i++)
        {
            RouteQuery q = load.Queries[i];
            RouteResult reference = ReferenceSearch.Find(load.Map, q, new WorkBudget(256L * 1024 * 1024, 30)); comparisons++;
            if (reference.Status == SearchStatus.Inconclusive) throw new InvalidOperationException("Reference inconclusive; case=" + i);
            RouteResult a = exact[i], b = anchor[i];
            if (a.Status == SearchStatus.Inconclusive) continue; // over 2,000 steps: never a claimed route
            if (a.Status != reference.Status || a.Status == SearchStatus.Found && a.Cost != reference.Cost) throw new InvalidOperationException("Exact lanes/reference mismatch; case=" + i + " " + a.Status + "/" + reference.Status + " " + a.Cost + "/" + reference.Cost);
            if (b.Status != reference.Status) throw new InvalidOperationException("Anchor/reference status mismatch; case=" + i);
            foreach (RouteResult r in new[] { a, b }) if (r.Status == SearchStatus.Found)
            {
                RouteResult described = load.Map.Describe(q, r.Directions);
                if (described.Status != SearchStatus.Found || described.Cost != r.Cost) throw new InvalidOperationException("Illegal path or cost; case=" + i);
            }
            if (b.Status == SearchStatus.Found && b.Cost < reference.Cost) throw new InvalidOperationException("Anchor below optimum; case=" + i);
        }
    }
    // Variant B must not depend on the worker count: compare 1 and 4 threads byte-wise.
    private static RouteResult[] AnchorChecked(GroupProbeInterop probe, GroupLoad load, int margin = 2, int mode = GroupProbeInterop.Anchor)
    {
        var single = probe.Run(mode, load.Queries, load.GroupOf.ToArray(), load.Anchors.ToArray(), margin, 1);
        var parallel = probe.Run(mode, load.Queries, load.GroupOf.ToArray(), load.Anchors.ToArray(), margin, 4);
        Same(single, parallel, load.Name);
        return single;
    }
    private static RouteResult[] PerUnitChecked(GroupProbeInterop probe, GroupLoad load) => AnchorChecked(probe, load, 2, GroupProbeInterop.PerUnit);
    private static void Same(RouteResult[] a, RouteResult[] b, string name)
    {
        for (int i = 0; i < a.Length; i++)
            if (a[i].Status != b[i].Status || a[i].Cost != b[i].Cost || !a[i].Directions.SequenceEqual(b[i].Directions)) throw new InvalidOperationException(name + ": anchor result depends on thread count; case=" + i);
    }
    internal static void Contracts()
    {
        var random = new Random(610447);
        var profiles = new[] { (1L, 7L), (2L, 3L), (5L, 5L) };
        for (int sample = 0; sample < 24; sample++)
        {
            var validity = new bool[23 * 19]; for (int i = 0; i < validity.Length; i++) validity[i] = sample == 0 || random.Next(100) < 85;
            GridSnapshot map = Grid(23, 19, (a, b, d) => random.Next(100) < 67 ? (1U << d) | (random.Next(100) < 25 ? 1U << (8 + d) : 0U) : 0U, n => validity[n]);
            var nodes = Enumerable.Range(0, map.Count).Where(map.IsValid).ToArray();
            var load = new GroupLoad { Name = "contract", Map = map };
            for (int group = 0; group < 3; group++)
            {
                var (ground, moat) = profiles[(sample + group) % profiles.Length];
                int size = 1 + random.Next(group == 2 ? 90 : 20); load.Anchors.Add(-1);
                for (int i = 0; i < size; i++) { load.Queries.Add(new RouteQuery { Start = nodes[random.Next(nodes.Length)], Target = nodes[random.Next(nodes.Length)], GroundCost = ground, MoatCost = moat }); load.GroupOf.Add(group); }
                load.Anchors[group] = load.Queries[load.Queries.Count - 1].Target;
            }
            // Group 3 shares anchor and profile with group 0, so both use one field.
            load.Anchors.Add(load.Anchors[0]);
            for (int i = 0; i < 12; i++) { load.Queries.Add(new RouteQuery { Start = nodes[random.Next(nodes.Length)], Target = nodes[random.Next(nodes.Length)], GroundCost = load.Queries[0].GroundCost, MoatCost = load.Queries[0].MoatCost }); load.GroupOf.Add(3); }
            using var probe = new GroupProbeInterop(map, 256L * 1024 * 1024);
            var exact = probe.Run(GroupProbeInterop.Exact, load.Queries, load.GroupOf.ToArray(), load.Anchors.ToArray());
            Check(load, exact, AnchorChecked(probe, load));
            Check(load, exact, PerUnitChecked(probe, load));
        }
        // Two slots that are neighbors across a wall but ~400 steps apart: lane spread
        // exceeds the 8-bit offset, so the block must split and stay exact.
        {
            GridSnapshot map = Grid(200, 3, null, n => n / 200 != 1 || n % 200 == 199);
            var load = new GroupLoad { Name = "split", Map = map }; load.Anchors.Add(0);
            load.Queries.Add(new RouteQuery { Start = 5, Target = 2 * 200, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0);
            load.Queries.Add(new RouteQuery { Start = 2 * 200 + 5, Target = 0, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0);
            using var probe = new GroupProbeInterop(map, 256L * 1024 * 1024);
            var exact = probe.Run(GroupProbeInterop.Exact, load.Queries, load.GroupOf.ToArray(), load.Anchors.ToArray());
            if (probe.Stats[3] == 0) throw new InvalidOperationException("Large lane spread did not split the block.");
            Check(load, exact, AnchorChecked(probe, load));
            Check(load, exact, PerUnitChecked(probe, load));
        }
        // Directed snake longer than 2,000 steps: reported over the limit, never as a route.
        {
            GridSnapshot map = Grid(800, 4, (a, b, d) => ((a / 800) % 2 == 0 ? d == 2 : d == 6) && a / 800 == b / 800 || d == 4 && a % 800 == ((a / 800) % 2 == 0 ? 799 : 0) ? 1U << d : 0);
            var load = new GroupLoad { Name = "snake", Map = map }; load.Anchors.Add(2400);
            load.Queries.Add(new RouteQuery { Start = 0, Target = 2400, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0);
            using var probe = new GroupProbeInterop(map, 256L * 1024 * 1024);
            foreach (int mode in new[] { GroupProbeInterop.Exact, GroupProbeInterop.Anchor, GroupProbeInterop.PerUnit })
                if (probe.Run(mode, load.Queries, load.GroupOf.ToArray(), load.Anchors.ToArray())[0].Status != SearchStatus.Inconclusive) throw new InvalidOperationException("Over-limit route was not reported as inconclusive.");
        }
        // Moat decision inside one group: exact lanes must equal the reference per unit.
        {
            GridSnapshot map = Grid(60, 40, (a, b, d) => (1U << d) | ((a % 60 == 30 || b % 60 == 30) && a / 60 < 30 ? 1U << (8 + d) : 0U), n => n % 60 != 30 || n / 60 < 30 || n / 60 == 38);
            var load = new GroupLoad { Name = "moat", Map = map }; load.Anchors.Add(20 * 60 + 50);
            for (int y = 2; y < 40; y += 3) { load.Queries.Add(new RouteQuery { Start = y * 60 + 5, Target = Math.Min(39, y + 1) * 60 + 50, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0); }
            using var probe = new GroupProbeInterop(map, 256L * 1024 * 1024);
            var exact = probe.Run(GroupProbeInterop.Exact, load.Queries, load.GroupOf.ToArray(), load.Anchors.ToArray());
            Check(load, exact, AnchorChecked(probe, load));
            Check(load, exact, PerUnitChecked(probe, load));
            if (!exact.Any(r => r.Moat > 0) || !exact.Any(r => r.Status == SearchStatus.Found && r.Moat == 0)) throw new InvalidOperationException("Moat fixture lacks mixed decisions.");
        }
        // Slot behind a wall whose only opening lies outside both ending boxes: B must stay
        // legal without the full solver, either through a far-slot sub-plan (current rule:
        // graph distance from the anchor far above the spatial one) or the via-anchor route.
        {
            GridSnapshot map = Grid(60, 60, null, n => n / 60 != 33 || n % 60 == 59);
            var load = new GroupLoad { Name = "via-anchor", Map = map }; load.Anchors.Add(30 * 60 + 30);
            for (int x = 20; x < 40; x += 4) { load.Queries.Add(new RouteQuery { Start = 5 * 60 + x, Target = 36 * 60 + 30, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0); }
            using var probe = new GroupProbeInterop(map, 256L * 1024 * 1024);
            var exact = probe.Run(GroupProbeInterop.Exact, load.Queries, load.GroupOf.ToArray(), load.Anchors.ToArray());
            var anchor = AnchorChecked(probe, load);
            long[] stats = probe.Stats;
            if (stats[GroupProbeInterop.Stat("fallbacks")] + stats[GroupProbeInterop.Stat("subPlans")] == 0 || stats[GroupProbeInterop.Stat("exactFallbacks")] != 0) throw new InvalidOperationException("Walled slot was not served without the full solver.");
            Check(load, exact, anchor);
            Check(load, exact, PerUnitChecked(probe, load));
        }
        // Two slots spatially next to the anchor but behind a wall (opening far south); the
        // units come from the wall's far side. They must get a sub-plan with its own anchor
        // instead of walking around the wall to the original anchor.
        {
            GridSnapshot map = Grid(60, 60, null, n => n % 60 != 34 || n / 60 == 59);
            var load = new GroupLoad { Name = "far-slots", Map = map }; load.Anchors.Add(30 * 60 + 30);
            foreach (int slot in new[] { 30 * 60 + 30, 30 * 60 + 31, 30 * 60 + 36, 30 * 60 + 37 }) { load.Queries.Add(new RouteQuery { Start = 10 * 60 + 50 + load.Queries.Count, Target = slot, GroundCost = 1, MoatCost = 7 }); load.GroupOf.Add(0); }
            using var probe = new GroupProbeInterop(map, 256L * 1024 * 1024);
            var exact = probe.Run(GroupProbeInterop.Exact, load.Queries, load.GroupOf.ToArray(), load.Anchors.ToArray());
            var anchor = AnchorChecked(probe, load);
            long[] stats = probe.Stats;
            if (stats[GroupProbeInterop.Stat("subPlans")] != 1 || stats[GroupProbeInterop.Stat("farUnits")] != 2) throw new InvalidOperationException("Far slots did not form one sub-plan.");
            Check(load, exact, anchor);
            for (int i = 2; i < 4; i++) if (anchor[i].Cost > exact[i].Cost + 2) throw new InvalidOperationException("Far-slot unit still detours; case=" + i + " " + anchor[i].Cost + "/" + exact[i].Cost);
            // Per-unit variant: the first far unit creates one sub-anchor, the second reuses it.
            var perUnit = PerUnitChecked(probe, load); stats = probe.Stats;
            if (stats[GroupProbeInterop.Stat("subPlans")] != 1) throw new InvalidOperationException("Per-unit far slots did not share one sub-anchor.");
            Check(load, exact, perUnit);
            for (int i = 2; i < 4; i++) if (perUnit[i].Cost > exact[i].Cost + 2) throw new InvalidOperationException("Per-unit far-slot unit still detours; case=" + i + " " + perUnit[i].Cost + "/" + exact[i].Cost);
        }
        Console.WriteLine("PASS group contracts independentComparisons=" + comparisons);
    }
    private static double Percentile(List<long> values, double p) { if (values.Count == 0) return 0; values.Sort(); return values[Math.Min(values.Count - 1, (int)Math.Ceiling(p * values.Count) - 1)]; }
    private static Dictionary<string, long> Named(long[] stats) { var named = new Dictionary<string, long>(); for (int i = 0; i < stats.Length; i++) named[GroupProbeInterop.StatNames[i]] = stats[i]; return named; }
    private static object Quality(GroupLoad load, RouteResult[] precise, RouteResult[] b)
    {
        var extra = new List<long>(); var relative = new List<long>(); int worse = 0, decisionMismatch = 0;
        for (int i = 0; i < precise.Length; i++)
        {
            RouteResult p = precise[i];
            if (b[i].Status != SearchStatus.Found) throw new InvalidOperationException(load.Name + ": anchor did not find positive case " + i + " status=" + b[i].Status);
            RouteResult described = load.Map.Describe(load.Queries[i], b[i].Directions);
            if (described.Status != SearchStatus.Found || described.Cost != b[i].Cost) throw new InvalidOperationException(load.Name + ": illegal anchor path/cost case=" + i);
            if (b[i].Cost < p.Cost) throw new InvalidOperationException(load.Name + ": anchor below optimum case=" + i);
            extra.Add(b[i].Cost - p.Cost); relative.Add(p.Cost == 0 ? 0 : (b[i].Cost - p.Cost) * 10000 / p.Cost);
            if (b[i].Cost > p.Cost) worse++;
            if ((b[i].Moat > 0) != (p.Moat > 0)) decisionMismatch++;
        }
        // The eight largest detours with graph distances around the anchor, for diagnosis.
        var outliers = new List<object>(); GridSnapshot map = load.Map;
        foreach (int i in Enumerable.Range(0, precise.Length).OrderByDescending(i => b[i].Cost - precise[i].Cost).ThenBy(i => i).Take(8))
        {
            RouteQuery q = load.Queries[i]; int anchor = load.Anchors[load.GroupOf[i]];
            long Graph(int from, int to) { var r = ReferenceSearch.Find(map, new RouteQuery { Start = from, Target = to, GroundCost = q.GroundCost, MoatCost = q.MoatCost }, new WorkBudget(512L * 1024 * 1024, 60)); comparisons++; return r.Status == SearchStatus.Found ? r.Cost : -1; }
            int Cheb(int a, int c) => Math.Max(Math.Abs(a % map.Width - c % map.Width), Math.Abs(a / map.Width - c / map.Width));
            outliers.Add(new { unit = i, group = load.GroupOf[i], profile = q.GroundCost + "/" + q.MoatCost, start = new[] { q.Start % map.Width, q.Start / map.Width }, slot = new[] { q.Target % map.Width, q.Target / map.Width }, anchor = new[] { anchor % map.Width, anchor / map.Width }, optimum = precise[i].Cost, anchorRoute = b[i].Cost, startToAnchor = Graph(q.Start, anchor), anchorToSlot = Graph(anchor, q.Target), slotToAnchor = Graph(q.Target, anchor), chebyshevAnchorSlot = Cheb(anchor, q.Target), chebyshevStartSlot = Cheb(q.Start, q.Target) });
        }
        return new { worseThanOptimum = worse, extraCostMean = extra.Average(), extraCostP95 = Percentile(extra, 0.95), extraCostMax = extra.Max(), extraPercentP95 = Percentile(relative, 0.95) / 100.0, extraPercentMax = relative.Max() / 100.0, moatDecisionMismatches = decisionMismatch, outliers };
    }
    // B configurations: group endings (mode 1) with margin 2 at 1/4/8 worker threads and
    // margin 8 at 4 threads; per-unit endings (mode 2) with margin 2 at 1/4/8 threads and
    // margin 8 at 4 threads.
    private static readonly (int Mode, int Margin, int Threads)[] AnchorConfigurations =
        { (1, 2, 1), (1, 2, 4), (1, 2, 8), (1, 8, 4), (2, 2, 1), (2, 2, 4), (2, 2, 8), (2, 8, 4) };
    private static object Measure(GroupLoad load, GroupProbeInterop probe, bool withExact)
    {
        int[] groupOf = load.GroupOf.ToArray(), anchors = load.Anchors.ToArray(); int count = load.Queries.Count;
        var rows = new Dictionary<string, object>(); RouteResult[] exact = null;
        var anchorResults = new Dictionary<string, RouteResult[]>();
        if (withExact)
        {
            exact = probe.Run(GroupProbeInterop.Exact, load.Queries, groupOf, anchors); double coldMs = probe.LastMilliseconds; long[] stats = probe.Stats;
            rows["exactLanes"] = new { coldMs, perUnitMicroseconds = coldMs * 1000 / count, stats = Named(stats) };
            Console.WriteLine("  A exact lanes            cold=" + coldMs.ToString("F1") + " ms");
        }
        foreach (var (mode, margin, threads) in AnchorConfigurations)
        {
            string key = (mode == GroupProbeInterop.PerUnit ? "unit" : "group") + "-margin" + margin;
            var cold = probe.Run(mode, load.Queries, groupOf, anchors, margin, threads); double coldMs = probe.LastMilliseconds; long[] stats = probe.Stats;
            probe.Run(mode, load.Queries, groupOf, anchors, margin, threads); double warmMs = probe.LastMilliseconds;
            // Same results for every worker count, compared against the first configuration.
            if (!anchorResults.TryGetValue(key, out RouteResult[] first)) anchorResults[key] = cold;
            else Same(first, cold, load.Name + " " + key + " threads=" + threads);
            rows[key + "-t" + threads] = new { coldMs, warmMs, perUnitMicroseconds = coldMs * 1000 / count, stats = Named(stats) };
            Console.WriteLine("  B " + key + " threads=" + threads + "  cold=" + coldMs.ToString("F1") + " ms warm=" + warmMs.ToString("F1") + " ms fields=" + stats[GroupProbeInterop.Stat("fieldTasks")] + " anchorMs=" + (stats[GroupProbeInterop.Stat("anchorNs")] / 1e6).ToString("F1") + " localMs=" + (stats[GroupProbeInterop.Stat("localNs")] / 1e6).ToString("F1") + " fallbackMs=" + (stats[GroupProbeInterop.Stat("fallbackNs")] / 1e6).ToString("F1") + " fallbacks=" + stats[GroupProbeInterop.Stat("fallbacks")] + "/" + stats[GroupProbeInterop.Stat("exactFallbacks")] + " viaChosen=" + stats[GroupProbeInterop.Stat("viaChosen")] + " far=" + stats[GroupProbeInterop.Stat("farUnits")] + "/" + stats[GroupProbeInterop.Stat("subPlans")] + " peakMiB=" + (stats[GroupProbeInterop.Stat("peakBytes")] / 1048576));
        }
        var oracle = new PreciseOracle(load.Map); var precise = new RouteResult[count]; var watch = Stopwatch.StartNew(); double next = 5;
        for (int i = 0; i < count; i++)
        {
            precise[i] = oracle.Find(load.Queries[i]);
            if (watch.Elapsed.TotalSeconds >= next) { Console.WriteLine("  PROGRESS Precise " + (i + 1) + "/" + count + " elapsedMs=" + watch.ElapsedMilliseconds); next = watch.Elapsed.TotalSeconds + 5; }
        }
        double preciseMs = watch.Elapsed.TotalMilliseconds;
        Console.WriteLine("  Precise                  total=" + preciseMs.ToString("F1") + " ms");
        int steps = 0, longest = 0;
        for (int i = 0; i < count; i++)
        {
            RouteResult p = precise[i];
            if (p.Status != SearchStatus.Found) throw new InvalidOperationException(load.Name + ": positive workload case not found by Precise; case=" + i + " " + p.Reason);
            if (exact != null)
            {
                RouteResult described = load.Map.Describe(load.Queries[i], exact[i].Directions);
                if (exact[i].Status != SearchStatus.Found || described.Status != SearchStatus.Found || described.Cost != exact[i].Cost || exact[i].Cost != p.Cost) throw new InvalidOperationException(load.Name + ": exact lanes differ from Precise case=" + i);
            }
            steps += p.Ground + p.Moat; longest = Math.Max(longest, p.Ground + p.Moat);
        }
        foreach (int i in new[] { 0, count / 2, count - 1 })
        {
            RouteResult reference = ReferenceSearch.Find(load.Map, load.Queries[i], new WorkBudget(512L * 1024 * 1024, 60)); comparisons++;
            if (reference.Status != SearchStatus.Found || reference.Cost != precise[i].Cost) throw new InvalidOperationException(load.Name + ": independent reference mismatch case=" + i);
        }
        var quality = new Dictionary<string, object>(); foreach (var entry in anchorResults) quality[entry.Key] = Quality(load, precise, entry.Value);
        return new
        {
            kind = "workload", name = load.Name, units = count, groups = load.Groups, distinctAnchors = load.Anchors.Distinct().Count(), distinctSlots = load.Queries.Select(q => q.Target).Distinct().Count(),
            averageSteps = (double)steps / count, longestSteps = longest, results = rows,
            precise = new { totalMs = preciseMs, perUnitMicroseconds = preciseMs * 1000 / count },
            anchorQuality = quality,
            scope = "Precise for every unit plus three independent references; anchor results legal, >= optimum and identical across worker counts" + (exact != null ? "; exact lanes equal Precise for every unit" : "")
        };
    }
    internal static int Run(string capture, bool withExact, string[] only = null)
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        string output = Path.Combine(root, "results", "group-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(output);
        File.Copy(Path.Combine(root, ".local/native/build.json"), Path.Combine(output, "native-build.json"));
        using (JsonDocument proof = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "native-build.json"))))
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, ".local/native/GroupProbe.dll")))), proof.RootElement.GetProperty("dllSha256").GetString(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Group probe binary/proof mismatch");
        Save(Path.Combine(output, "host.json"), new { timestampUtc = DateTime.UtcNow, runtime = Environment.Version.ToString(), testAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(GroupGate).Assembly.Location))), inputSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(capture))), oracleSources = OracleHashes(root) });
        var rows = new List<object>(); int exit = 0;
        try
        {
            Contracts(); rows.Add(new { kind = "contracts", passed = true, independentComparisons = comparisons }); Save(Path.Combine(output, "progress.json"), rows);
            CapturedMap raw = SnapshotCodec.ReadCaptured(capture);
            GridSnapshot captured = raw.Build(raw.Players[0]);
            var loads = GroupWorkloads.Captured(captured, raw.Units); GridSnapshot moatMap = GroupWorkloads.MoatMap(); loads.Add(GroupWorkloads.MoatMix(moatMap));
            // --only keeps the generated inputs identical and merely skips other loads.
            if (only != null) loads = loads.Where(l => only.Contains(l.Name)).ToList();
            GroupProbeInterop probe = null; GridSnapshot probeMap = null;
            try
            {
                foreach (GroupLoad load in loads)
                {
                    if (load.Queries.Count == 0) { rows.Add(new { kind = "incomplete", name = load.Name, reason = "no positive group could be formed" }); exit = 2; continue; }
                    if (!ReferenceEquals(probeMap, load.Map)) { probe?.Dispose(); probe = new GroupProbeInterop(load.Map); probeMap = load.Map; }
                    Console.WriteLine("BEGIN " + load.Name + " units=" + load.Queries.Count + " groups=" + load.Groups);
                    SnapshotCodec.WriteQueries(Path.Combine(output, load.Name + ".bpf.gz"), load.Map, load.Queries);
                    rows.Add(Measure(load, probe, withExact)); Save(Path.Combine(output, "progress.json"), rows);
                }
            }
            finally { probe?.Dispose(); }
        }
        catch (Exception ex) { rows.Add(new { kind = "incomplete", error = ex.ToString() }); Console.WriteLine(ex); exit = 1; }
        Save(Path.Combine(output, "results.json"), rows);
        Console.WriteLine("GROUP PROBE exit=" + exit + "; evidence=" + output); return exit;
    }
    private static Dictionary<string, string> OracleHashes(string root)
    {
        var result = new Dictionary<string, string>();
        foreach (string relative in new[] { "BugfixesAndQoL/src/UnitCommands/Movement/WeightedGridSearchKernel.cs", "BugfixesAndQoL/src/UnitCommands/Movement/WeightedMoatRoutePlanner.cs", "BugfixesAndQoL/src/UnitCommands/Moat/MoatCandidateField.cs", "BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandContracts.cs" })
            result[relative] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, "../..", relative))));
        return result;
    }
    private static void Save(string path, object data) => File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", "\r\n"));
}
