using System;
using System.Diagnostics;
using System.IO;
using BetterPathfindingTest.Core;

internal static class ScaleBench
{
    internal static int Run()
    {
        string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../results/areas-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output); bool failed = false;
        foreach (bool wet in new[] { false, true })
        {
            var watch = Stopwatch.StartNew(); GridSnapshot map = Workloads.Synthetic(800, wet); double mapMs = watch.Elapsed.TotalMilliseconds;
            foreach (int count in new[] { 1, 128, 512, 10000 })
            {
                string name = (wet ? "moat" : "ground") + "-800-" + count;
                var queries = Workloads.SyntheticBatch(map, count, wet);
                SnapshotCodec.WriteQueries(Path.Combine(output, name + ".bpf.gz"), map, queries);
                Console.WriteLine("BEGIN " + name);
                watch.Restart(); var oracle = new ProductionOracle(map); double oraclePreparationMs = watch.Elapsed.TotalMilliseconds;
                BenchmarkResult result = Benchmark.Run(map, queries, new WorkBudget(), q => oracle.Find(q));
                var data = result.Data(); data["mapBuildMs"] = mapMs; data["preciseAdapterPreparationMs"] = oraclePreparationMs;
                data["runtime"] = ".NET " + Environment.Version + " + private x64 C++";
                data["scope"] = "synthetic requests, not game units; Precise timing includes unchanged oracle adapter conversion/validation";
                File.WriteAllText(Path.Combine(output, name + ".json"), System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", "\r\n"));
                File.WriteAllText(Path.Combine(output, name + ".md"), result.Markdown(name, (string)data["runtime"]));
                Console.WriteLine("END " + name + " complete=" + result.Completed + " found=" + result.Found + " errors=" + result.Mismatches + "/" + result.ProductionDifferences + " incomplete=" + result.Incomplete + " buildMs=" + result.PreparationMs.ToString("F2") + " coldMs=" + result.ColdMs.ToString("F2") + " validationMs=" + result.OutputMs.ToString("F2") + " preciseMs=" + result.ProductionMs.ToString("F2") + " pruned=" + result.Pruned + " peak=" + result.PeakBytes);
                failed |= result.Mismatches != 0 || result.ProductionDifferences != 0 || result.Incomplete != 0 || result.Completed != count;
                if (result.Incomplete != 0 && result.Completed == 0) break;
            }
        }
        Console.WriteLine("Evidence: " + output); return failed ? 2 : 0;
    }
}
