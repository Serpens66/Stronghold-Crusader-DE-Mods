using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BetterPathfindingTest.Core;

internal static class TraceTape
{
    private sealed class Tape { internal GridSnapshot Map; internal readonly List<RouteQuery> Queries = new List<RouteQuery>(); internal readonly List<int> Batches = new List<int>(); }
    private static readonly ConditionalWeakTable<AreaNavigator, Tape> Tapes = new ConditionalWeakTable<AreaNavigator, Tape>();
    internal static void Record(AreaNavigator nav, GridSnapshot map, IList<RouteQuery> queries)
    {
        Tape tape = Tapes.GetValue(nav, owner => new Tape());
        if (!ReferenceEquals(tape.Map, map)) { tape.Map = map; tape.Queries.Clear(); tape.Batches.Clear(); }
        tape.Batches.Add(queries.Count); foreach (RouteQuery q in queries) tape.Queries.Add(q.Copy());
    }
    internal static void Save(string path, AreaNavigator nav, GridSnapshot map, IList<RouteQuery> production, RouteResult actual, RouteResult old)
    {
        Tape tape = Tapes.GetValue(nav, owner => new Tape()); string nativePath = path + ".native.bpf.gz";
        SnapshotCodec.WriteQueries(path, map, production); SnapshotCodec.WriteQueries(nativePath, map, tape.Queries);
        File.WriteAllText(path + ".tape.json", JsonSerializer.Serialize(new { nativeQueries = Path.GetFileName(nativePath), nativeBatches = tape.Batches, nativeDirections = Convert.ToBase64String(actual.Directions), preciseDirections = Convert.ToBase64String(old.Directions), nativeStatus = actual.Status.ToString(), nativeCost = actual.Cost }, new JsonSerializerOptions { WriteIndented = true }));
    }
    internal static int Replay(string path)
    {
        using JsonDocument tape = JsonDocument.Parse(File.ReadAllText(path + ".tape.json")); JsonElement root = tape.RootElement;
        string name = root.GetProperty("nativeQueries").GetString(); if (Path.GetFileName(name) != name) throw new InvalidDataException("Tape input must be a neighboring file.");
        RouteQuery[] native = SnapshotCodec.ReadQueries(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), name), out GridSnapshot map);
        RouteResult actual = null; int offset = 0;
        using (var nav = new AreaNavigator(map, new WorkBudget()))
            foreach (JsonElement size in root.GetProperty("nativeBatches").EnumerateArray())
            {
                int n = size.GetInt32(); if (n < 1 || n > native.Length - offset) throw new InvalidDataException("Tape batch bounds.");
                var batch = new RouteQuery[n]; Array.Copy(native, offset, batch, 0, n); RouteResult[] results = nav.FindPaths(batch); actual = results[results.Length - 1]; offset += n;
            }
        if (offset != native.Length || actual == null) throw new InvalidDataException("Incomplete tape.");
        RouteQuery[] production = SnapshotCodec.ReadQueries(path, out GridSnapshot oracleMap); var oracle = new ProductionOracle(oracleMap); RouteResult old = null;
        foreach (RouteQuery q in production) old = oracle.Find(q);
        RouteResult expected = ReferenceSearch.Find(map, native[native.Length - 1], new WorkBudget());
        bool cost = actual.Status == expected.Status && (actual.Status != SearchStatus.Found || actual.Cost == expected.Cost);
        bool directions = Convert.ToBase64String(actual.Directions) == root.GetProperty("nativeDirections").GetString() && old != null && Convert.ToBase64String(old.Directions) == root.GetProperty("preciseDirections").GetString();
        string status = cost && directions ? "PASS: recorded native batch history and Precise history reproduce directions/cost" : "FAILED: recorded source/history differs";
        File.WriteAllText(path + ".replay.md", status + Environment.NewLine); Console.WriteLine(status); return cost && directions ? 0 : 1;
    }
}
