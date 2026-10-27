using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using BetterPathfindingTest.Core;

internal static class CchGate
{
    private static int comparisons;
    private static GridSnapshot Ground(GridSnapshot map)
    {
        uint[] words = map.CopyEdges();
        for (int i = 0; i < words.Length; i++) words[i] = (words[i] & 0xffU & ~(words[i] >> 8)) | (words[i] & 0xff0000U);
        return new GridSnapshot(map.Width, map.Height, map.CopyValidity(), words, map.Player);
    }
    private static void Verify(CchProbeInterop nav, IList<RouteQuery> queries, RouteResult[] result, bool referenceAll)
    {
        for (int i = 0; i < queries.Count; i++)
        {
            var a = result[i]; var q = queries[i];
            if (a.Status == SearchStatus.Found)
            {
                RouteResult path = nav.Map.Describe(q, a.Directions);
                if (path.Status != SearchStatus.Found || path.Cost != a.Cost) throw new InvalidOperationException("CCH emitted illegal path/cost; case=" + i);
            }
            if (referenceAll || i < 32)
            {
                var b = ReferenceSearch.Find(nav.Map, q, new WorkBudget(256L * 1024 * 1024, 10)); comparisons++;
                if (b.Status == SearchStatus.Inconclusive || a.Status != b.Status || a.Status == SearchStatus.Found && a.Cost != b.Cost) throw new InvalidOperationException("CCH/reference mismatch; case=" + i + " " + a.Status + "/" + b.Status + " " + a.Cost + "/" + b.Cost);
            }
        }
    }
    private static void Contracts()
    {
        var random = new Random(347180);
        for (int sample = 0; sample < 12; sample++)
        {
            GridSnapshot map = Program.Grid(6, 5, (a, b, d) => sample == 0 || random.Next(100) < 64 ? 1U << d : 0);
            using var nav = new CchProbeInterop(map, 64L * 1024 * 1024, 10);
            var queries = new List<RouteQuery>();
            for (int start = 0; start < map.Count; start++) for (int target = 0; target < map.Count; target++) queries.Add(new RouteQuery { Start = start, Target = target, MaximumEdges = sample % 2 == 0 ? 2000 : 8 });
            Verify(nav, queries, nav.Find(queries), true);
            if (sample != 0 && nav.Stats[14] == 0) throw new InvalidOperationException("Repeated sources did not share exact upward labels");
        }
        foreach (var map in new[] { Program.Grid(1, 17), Program.Grid(17, 1), Program.Grid(40, 9, (a,b,d) => d == 2 ? 1U << d : 0), Program.Grid(48, 39, (a,b,d) => a%48 == 23 ? 0 : 1U<<d) })
        {
            using var nav = new CchProbeInterop(map, 64L * 1024 * 1024, 15);
            var queries = new List<RouteQuery>(); for (int i = 0; i < 128; i++) queries.Add(new RouteQuery { Start = random.Next(map.Count), Target = random.Next(map.Count) });
            Verify(nav, queries, nav.Find(queries), true);
        }
        // Larger directed graphs exercise separators, not only the <=32 leaf order.
        for (int sample = 0; sample < 8; sample++)
        {
            var validity = new byte[23 * 19]; for (int i = 0; i < validity.Length; i++) validity[i] = random.Next(100) < 85 ? (byte)1 : (byte)0;
            var original = Program.Grid(23, 19, (a,b,d) => validity[a] != 0 && random.Next(100) < 67 ? 1U << d : 0);
            var map = new GridSnapshot(23, 19, validity, original.CopyEdges()); var validNodes = Enumerable.Range(0, map.Count).Where(map.IsValid).ToArray();
            using var nav = new CchProbeInterop(map, 64L * 1024 * 1024, 10);
            var queries = new List<RouteQuery>(); for (int i = 0; i < 128; i++) queries.Add(new RouteQuery { Start = validNodes[random.Next(validNodes.Length)], Target = validNodes[random.Next(validNodes.Length)], MaximumEdges = i % 3 == 0 ? 16 : 2000 });
            Verify(nav, queries, nav.Find(queries), true);
        }
        var blocked = Program.Grid(41, 7, (a,b,d) => a % 41 == 20 || b % 41 == 20 ? 0 : 1U << d);
        using (var nav = new CchProbeInterop(blocked, 64L * 1024 * 1024, 10))
        {
            var queries = new[] { new RouteQuery { Start = 3 * 41, Target = 4 * 41 - 1 } };
            Verify(nav, queries, nav.Find(queries), true);
            var opened = Program.Grid(41, 7); nav.Replace(opened); Verify(nav, queries, nav.Find(queries), true);
            using var fresh = new CchProbeInterop(opened, 64L * 1024 * 1024, 10);
            if (nav.Find(queries)[0].Cost != fresh.Find(queries)[0].Cost) throw new InvalidOperationException("CCH recustomization/rebuild differs");
            nav.Replace(blocked); Verify(nav, queries, nav.Find(queries), true);
            var compact = fresh.Find(queries, false)[0]; var full = fresh.Find(queries)[0];
            if (compact.Cost != full.Cost || !compact.Directions.SequenceEqual(full.Directions.Take(8))) throw new InvalidOperationException("CCH compact prefix");
        }
        bool Permit(int node) => node % 41 != 20 || node / 41 == 0;
        var detour = Program.Grid(41, 81, (a,b,d) => Permit(a) && Permit(b) ? 1U << d : 0);
        int opening = 40 * 41 + 20;
        var shortcut = Program.Grid(41, 81, (a,b,d) => (Permit(a) || a == opening) && (Permit(b) || b == opening) ? 1U << d : 0);
        using (var nav = new CchProbeInterop(detour, 128L * 1024 * 1024, 10))
        {
            var queries = new[] { new RouteQuery { Start = 40 * 41 + 1, Target = 40 * 41 + 39 } };
            var old = nav.Find(queries); Verify(nav, queries, old, true);
            int node = queries[0].Start; foreach (byte d in old[0].Directions) { node = detour.Neighbor(node, d); if (node == opening) throw new InvalidOperationException("Shortcut fixture lies on old route"); }
            long arcCount = nav.Stats[2]; nav.Replace(shortcut); var changed = nav.Find(queries); Verify(nav, queries, changed, true);
            if (changed[0].Cost >= old[0].Cost || nav.Stats[2] != arcCount) throw new InvalidOperationException("Off-route opening failed or rebuilt topology");
            using var fresh = new CchProbeInterop(shortcut, 128L * 1024 * 1024, 10);
            if (changed[0].Cost != fresh.Find(queries)[0].Cost) throw new InvalidOperationException("CCH shortcut/rebuild mismatch");
            nav.Replace(detour); Verify(nav, queries, nav.Find(queries), true);
            var group = new List<RouteQuery>(); for (int i = 0; i < 64; i++) group.Add(new RouteQuery { Start = 40 * 41 + 1 + i % 10, Target = 40 * 41 + 39 });
            Verify(nav, group, nav.Find(group), true);
            if (nav.Stats[12] == 0 || nav.Stats[13] == 0) throw new InvalidOperationException("Repeated targets did not share a goal field");
        }
        var snake = Program.Grid(800, 4, (a,b,d) => ((a/800)%2 == 0 ? d == 2 : d == 6) && a/800 == b/800 || d == 4 && a%800 == ((a/800)%2 == 0 ? 799 : 0) ? 1U<<d : 0);
        using (var nav = new CchProbeInterop(snake, 128L * 1024 * 1024, 15))
        {
            var queries = new[] { new RouteQuery { Start = 0, Target = 2400, MaximumEdges = 2000 }, new RouteQuery { Start = 0, Target = 2400, MaximumEdges = 4000 } };
            Verify(nav, queries, nav.Find(queries), true);
        }
        bool limited = false; try { using var nav = new CchProbeInterop(Program.Grid(10, 10), 1000, 10); } catch (SearchBudgetException) { limited = true; }
        if (!limited) throw new InvalidOperationException("CCH memory exhaustion did not remain incomplete.");
        Console.WriteLine("PASS CCH independent comparisons=" + comparisons);
    }
    internal static int Run(string[] args)
    {
        string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../results/cch-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
        Directory.CreateDirectory(output); File.Copy(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.local/cch/build.json")), Path.Combine(output, "native-build.json"));
        string library = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.local/cch/CchProbe.dll"));
        using (JsonDocument proof = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "native-build.json"))))
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(library))), proof.RootElement.GetProperty("dllSha256").GetString(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("CCH binary/proof mismatch");
        Save(Path.Combine(output, "host.json"), new { timestampUtc = DateTime.UtcNow, runtime = Environment.Version.ToString(), is64Bit = Environment.Is64BitProcess, testAssemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(CchGate).Assembly.Location))) });
        var rows = new List<object>(); bool fits = true, speed = true;
        try
        {
            Contracts();
            Save(Path.Combine(output, "contracts.json"), new { passed = true, independentComparisons = comparisons });
            if (args.Length > 1 && args[1] == "--contracts") return 0;
            var maps = new List<KeyValuePair<string, GridSnapshot>>(); var seeds = new UnitSeed[0];
            if (args.Length < 3 || args[2] != "--capture-only") maps.Add(new KeyValuePair<string, GridSnapshot>("open800", Workloads.Synthetic(800, false)));
            if (args.Length > 1)
            {
                CapturedMap raw = SnapshotCodec.ReadCaptured(args[1]); seeds = raw.Units;
                maps.Add(new KeyValuePair<string, GridSnapshot>("captured800", Ground(raw.Build(raw.Players[0]))));
                File.Copy(args[1], Path.Combine(output, "input.raw.gz"));
            }
            foreach (var item in maps)
            {
                Console.WriteLine("BEGIN CCH initialization " + item.Key);
                using var nav = new CchProbeInterop(item.Value, 1024L * 1024 * 1024, 300, s => Console.WriteLine("PROGRESS phase=" + s[0] + " cursor=" + s[1] + " arcs=" + s[2] + " MiB=" + s[3]/1048576 + " triangles=" + s[5]));
                var preparation = new { kind = "preparation", map = item.Key, buildMs = nav.BuildMilliseconds, stats = nav.Stats };
                rows.Add(preparation); Write(output, rows); Console.WriteLine(JsonSerializer.Serialize(preparation));
                foreach (string family in new[] { "shared-goal", "formation", "distributed-goals", "global-ground" }) foreach (int count in new[] { 128, 512, 10000 })
                {
                    var queries = family == "global-ground" ? Global(item.Value, count) : Workloads.Create(item.Value, item.Key == "captured800" ? seeds : new UnitSeed[0], count, family == "distributed-goals" ? "distributed-profiles" : family);
                    foreach (var q in queries) { q.GroundCost = 1; q.MoatCost = 7; }
                    SnapshotCodec.WriteQueries(Path.Combine(output, item.Key + "-" + family + "-" + count + ".bpf.gz"), item.Value, queries);
                    var compact = nav.Find(queries, false); double compactMs = nav.LastBatchMilliseconds;
                    var warm = nav.Find(queries, false); double warmMs = nav.LastBatchMilliseconds;
                    var cold = nav.Find(queries); double fullMs = nav.LastBatchMilliseconds; Verify(nav, queries, cold, false);
                    for (int i = 0; i < queries.Count; i++) if (compact[i].Status != cold[i].Status || compact[i].Cost != cold[i].Cost || warm[i].Status != cold[i].Status || warm[i].Cost != cold[i].Cost || !compact[i].Directions.SequenceEqual(cold[i].Directions.Take(8))) throw new InvalidOperationException("CCH compact/full parity");
                    var oracle = new ProductionOracle(item.Value); var watch = Stopwatch.StartNew(); double nextProgress = 5;
                    for (int i = 0; i < queries.Count; i++) { var previous = oracle.Find(queries[i]); if (previous.Status != cold[i].Status || previous.Status == SearchStatus.Found && previous.Cost != cold[i].Cost) throw new InvalidOperationException("CCH/Precise parity"); if (watch.Elapsed.TotalSeconds >= nextProgress) { Console.WriteLine("PROGRESS Precise checked=" + (i+1) + "/" + queries.Count + " elapsedMs=" + watch.ElapsedMilliseconds); nextProgress = watch.Elapsed.TotalSeconds + 5; } }
                    double preciseMs = watch.Elapsed.TotalMilliseconds;
                    var row = new { kind = "batch", map = item.Key, family, count, found = cold.Count(x => x.Status == SearchStatus.Found), compactMs, warmMs, fullMs, preciseMs, minimumSteps = cold.Where(x=>x.Status==SearchStatus.Found).Select(x=>x.Ground).DefaultIfEmpty().Min(), maximumSteps = cold.Where(x=>x.Status==SearchStatus.Found).Select(x=>x.Ground).DefaultIfEmpty().Max(), stats = nav.Stats, maximumSliceMs = nav.MaximumSliceMilliseconds, scope = "unit-cost ground; generator profile variation normalized; native query/output plus managed batch adaptation" };
                    rows.Add(row); Write(output, rows); Console.WriteLine(JsonSerializer.Serialize(row));speed &= compactMs <= 100;
                }
            }
        }
        catch (Exception ex) { fits = false; rows.Add(new { kind = "incomplete", error = ex.ToString() }); Write(output, rows); Console.WriteLine(ex); }
        var decision = new { functionalComparisonsPassed = fits, nativeInitializationWithinLimit = fits, completeManagedMemoryAcceptance = false, reactionTargetMet = speed && fits, acceptedForRuntime = speed && fits, comparisons, scope = "offline CCH candidate with shared goal fields, cached target search and reusable source labels; native counted allocation limit plus input graph; no complete resident-service memory accounting, no runtime/API installation", rows };
        Save(Path.Combine(output, "decision.json"), decision);
        Console.WriteLine("CCH GATE native-limit=" + fits + " target=" + (speed && fits) + "; evidence=" + output); return fits && speed ? 0 : 2;
    }
    private static void Write(string path, List<object> rows) => Save(Path.Combine(path, "progress.json"), rows);
    private static void Save(string path, object data) => File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", "\r\n"));
    private static List<RouteQuery> Global(GridSnapshot map, int count)
    {
        int anchor = -1; List<int> source = null, targets = null;
        for (int node = 0, attempts = 0; node < map.Count && attempts < 8; node++)
        {
            if (!map.IsValid(node) || (map.Word(node) & 255) == 0) continue;
            attempts++; var a = Reach(map, node, true); var b = Reach(map, node, false);
            if (source == null || Math.Min(a.Count, b.Count) > Math.Min(source.Count, targets.Count)) { anchor = node; source = a; targets = b; }
            node += Math.Max(1, map.Count / 8);
        }
        if (anchor < 0 || source.Count == 0 || targets.Count == 0) throw new InvalidOperationException("No global ground input component");
        var random = new Random(894213); var queries = new List<RouteQuery>();
        for (int i = 0; i < count; i++)
        {
            int start = source[random.Next(source.Count)], target = targets[random.Next(targets.Count)];
            for (int attempt = 0; attempt < 24 && Math.Max(Math.Abs(start % map.Width-target % map.Width),Math.Abs(start/map.Width-target/map.Width)) < map.Width/2; attempt++) { start = source[random.Next(source.Count)]; target = targets[random.Next(targets.Count)]; }
            queries.Add(new RouteQuery { Start = start, Target = target });
        }
        return queries;
    }
    private static List<int> Reach(GridSnapshot map, int source, bool reverse)
    {
        var seen = new bool[map.Count]; var nodes = new List<int> { source }; seen[source] = true; var q = new RouteQuery();
        for (int i = 0; i < nodes.Count; i++) for (int d = 0; d < 8; d++)
        {
            int next = map.Neighbor(nodes[i], reverse ? (d+4)&7 : d);
            if (!map.IsValid(next) || seen[next] || !map.Edge(reverse ? next : nodes[i], d, q, out _, out _, out _)) continue;
            seen[next] = true; nodes.Add(next);
        }
        return nodes;
    }
}
