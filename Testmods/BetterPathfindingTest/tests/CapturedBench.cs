using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using BetterPathfindingTest.Core;

internal static class CapturedBench
{
    internal static int Run(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("--captured <snapshot.raw.gz>");
        string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../results/captured-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        Directory.CreateDirectory(output); BackendEvidence.Save(output); CapturedMap raw = SnapshotCodec.ReadCaptured(args[1]);
        SnapshotCodec.WriteCaptured(Path.Combine(output, "input.raw.gz"), raw);
        var watch = Stopwatch.StartNew(); GridSnapshot graph = raw.Build(raw.Players[0]); double graphMs = watch.Elapsed.TotalMilliseconds;
        string inputHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(args[1]))); bool failed = false;
        foreach (string family in new[] { "shared-goal", "formation", "distributed-profiles", "moat-required" })
        {
            int count = family == "shared-goal" || family == "moat-required" ? 10000 : 128;
            var queries = Workloads.Create(graph, raw.Units, count, family); string name = family + "-" + count;
            SnapshotCodec.WriteQueries(Path.Combine(output, name + ".bpf.gz"), graph, queries);
            Console.WriteLine("BEGIN captured " + name + " inputs=" + queries.Count);
            var oracle = new ProductionOracle(graph);
            BenchmarkResult result = Benchmark.Run(graph, queries, new WorkBudget(maximumSeconds: 60), q => oracle.Find(q));
            if (queries.Count != count) { result.Incomplete++; result.Limitation = "Missing reachable inputs for the requested family."; }
            var data = result.Data(); data["capturedInputSha256"] = inputHash; data["capturedContext"] = raw.Context; data["graphBuildMs"] = graphMs; data["capturedUnits"] = raw.Units.Length;
            data["coverage"] = queries.Count == count ? "positive generated probes on captured graph; not recorded game orders" : "missing reachable inputs for this family";
            File.WriteAllText(Path.Combine(output, name + ".json"), System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", "\r\n"));
            File.WriteAllText(Path.Combine(output, name + ".md"), result.Markdown(name, ".NET " + Environment.Version + " + private C++; captured graph, not Unity execution"));
            Console.WriteLine("END captured " + name + " completed=" + result.Completed + " found=" + result.Found + " errors=" + result.Mismatches + "/" + result.ProductionDifferences + " incomplete=" + result.Incomplete + " coldMs=" + result.ColdMs.ToString("F2") + " preciseMs=" + result.ProductionMs.ToString("F2") + " pruned=" + result.Pruned);
            failed |= result.Incomplete != 0 || result.Mismatches != 0 || result.ProductionDifferences != 0;
        }
        Console.WriteLine("Evidence: " + output); return failed ? 2 : 0;
    }
}
