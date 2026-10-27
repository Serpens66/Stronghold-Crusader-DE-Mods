using System;
using System.Collections.Generic;
using System.IO;
using BetterPathfindingTest.Core;

internal static class OfflineBench
{
    internal static int Run()
    {
        string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../results/bench-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"))); Directory.CreateDirectory(output);
        foreach (int size in new[] { 32, 128, 800 })
        {
            var build = System.Diagnostics.Stopwatch.StartNew(); GridSnapshot map = Program.Grid(size, size);
            double buildMs = build.Elapsed.TotalMilliseconds;
            foreach (int count in new[] { 1, 32, 128, 512 })
            {
                var queries = new List<RouteQuery>();
                for (int i = 0; i < count; i++) queries.Add(new RouteQuery { Start = (i % 24) * size + (i % 24), Target = size * size - 1 });
                string name = "open-" + size + "-shared-" + count;
                SnapshotCodec.WriteQueries(Path.Combine(output, name + ".bpf.gz"), map, queries);
                Console.WriteLine("BEGIN " + name);
                var oracle = new ProductionOracle(map);
                BenchmarkResult report = Benchmark.Run(map, queries, new WorkBudget(), q => oracle.Find(q));
                var data = report.Data(); data["graphBuildMs"] = buildMs; data["runtime"] = ".NET " + Environment.Version; data["scope"] = "synthetic open grid; not Unity/Mono or native gameplay";
                File.WriteAllText(Path.Combine(output, name + ".json"), System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", "\r\n"));
                File.WriteAllText(Path.Combine(output, name + ".md"), report.Markdown(name, ".NET " + Environment.Version));
                Console.WriteLine("END " + name + " compared=" + report.Completed + "/" + count + " errors=" + report.Mismatches + " incomplete=" + report.Incomplete + " prepMs=" + report.PreparationMs.ToString("F2") + " searchMs=" + report.ColdMs.ToString("F2") + " preciseMs=" + report.ProductionMs.ToString("F2") + " peak=" + report.PeakBytes);
                // Repeating a failed cold build for larger cohorts adds no evidence.
                if (report.Incomplete != 0 && report.Completed == 0) break;
            }
        }
        Console.WriteLine("Benchmark evidence: " + output); return 0;
    }
}
