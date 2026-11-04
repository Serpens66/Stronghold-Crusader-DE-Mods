using System;
using System.Diagnostics;
using System.IO;
using BetterPathfindingTest.Core;

internal static class Replay
{
    internal static int Run(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("--replay <queries.bpf.gz>");
        if (File.Exists(args[1] + ".tape.json")) return TraceTape.Replay(args[1]);
        RouteQuery[] queries = SnapshotCodec.ReadQueries(args[1], out GridSnapshot map);
        var budget = new WorkBudget(); var oracle = new ProductionOracle(map);
        BenchmarkResult result = Benchmark.Run(map, queries, budget, q => oracle.Find(q));
        string report = result.Markdown("Offline replay: " + Path.GetFileName(args[1]), ".NET " + Environment.Version);
        File.WriteAllText(args[1] + ".replay.md", report);
        File.WriteAllText(args[1] + ".replay.json", System.Text.Json.JsonSerializer.Serialize(result.Data(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", "\r\n"));
        Console.WriteLine(report);
        return result.Mismatches != 0 || result.ProductionDifferences != 0 ? 1 : result.Incomplete != 0 || result.Completed != result.Planned ? 2 : 0;
    }
}
