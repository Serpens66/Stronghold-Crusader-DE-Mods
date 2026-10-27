using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using BetterPathfindingTest.Api;
using BetterPathfindingTest.Core;
using BetterPathfindingTest.Service;

internal static partial class Program
{
    // Real worker + public API replay; this is explicitly not Unity/Mono evidence.
    private static int ResidentBench(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("--resident-bench <first.raw.gz> <second.raw.gz>");
        string output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../results/resident-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")));
        Directory.CreateDirectory(output); BackendEvidence.Save(output);
        var rows = new List<object>(); var watch = Stopwatch.StartNew(); Exception failure = null;
        using var queue = new BlockingCollection<Action>();
        var thread = new Thread(() => { foreach (var job in queue.GetConsumingEnumerable()) try { job(); } catch (Exception ex) { failure = ex; } }) { IsBackground = true, Priority = ThreadPriority.BelowNormal };
        thread.Start(); var service = new ResidentNavigation(queue.Add, 1024L * 1024 * 1024, log: Console.WriteLine); NavigationApi.Bind(service);
        double Now() => watch.Elapsed.TotalSeconds;
        void Save(string name, object data) => File.WriteAllText(Path.Combine(output, name), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n").Replace("\n", "\r\n"));
        NavigationStatus Ready(int player)
        {
            var limit = Stopwatch.StartNew();
            while (limit.Elapsed.TotalSeconds < 300) { var status = service.GetStatus(player); if (status.IsReady) return status; if (status.State == NavigationState.Faulted || status.State == NavigationState.Unsupported) throw new InvalidOperationException(status.State + ": " + status.Reason); if (failure != null) throw failure; Thread.Sleep(1); }
            throw new TimeoutException("Resident replay preparation timeout.");
        }
        NavigationBatchResult Wait(NavigationTicket ticket)
        {
            var limit = Stopwatch.StartNew(); while (limit.Elapsed.TotalSeconds < 125) { var result = NavigationApi.Poll(ticket); if (result.State != NavigationBatchState.Pending) return result; Thread.Sleep(1); }
            throw new TimeoutException("Resident replay batch timeout.");
        }
        GridSnapshot Ground(CapturedMap raw, int player, WorkBudget budget = null)
        {
            GridSnapshot map = raw.Build(player, budget: budget); var words = map.CopyEdges();
            for (int i = 0; i < words.Length; i++) words[i] = (words[i] & 255U & ~(words[i] >> 8)) | (words[i] & 0xff0000U);
            return new GridSnapshot(map.Width, map.Height, map.CopyValidity(), words);
        }
        OwnedNavigationSnapshot Input(CapturedMap raw, int player) => new OwnedNavigationSnapshot { Session = 1, Width = raw.Width, Height = raw.Height, LocalPlayer = player, CapturedAt = Now(), Bytes = raw.BufferBytes + raw.Flags.LongLength * 9 + 4L * 1024 * 1024, BuildGround = (id, budget) => Ground(raw, id, budget) };
        try
        {
            var raw = SnapshotCodec.ReadCaptured(args[1]); int player = raw.Players[0];
            SnapshotCodec.WriteCaptured(Path.Combine(output, "input-1.raw.gz"), raw);
            service.Begin(1, player); var initial = Stopwatch.StartNew(); if (!service.Offer(Input(raw, player))) throw new InvalidOperationException("Resident source budget rejected captured map.");
            var status = Ready(player); double initializationMs = initial.Elapsed.TotalMilliseconds;
            var graph = Ground(raw, player); var seeds = raw.Units.Where(u => u.Player == player).Select(u => new UnitSeed { Player = 0, Node = u.Node }).ToArray();
            foreach (string family in new[] { "shared-goal", "formation", "distributed-profiles" })
            foreach (int count in new[] { 128, 512, 10000 })
            {
                var queries = Workloads.Create(graph, seeds, count, family); foreach (var query in queries) { query.GroundCost = 1; query.MoatCost = 1; }
                if (queries.Count != count) throw new InvalidOperationException("Missing positive workload coverage: " + family);
                string name = family + "-" + count; SnapshotCodec.WriteQueries(Path.Combine(output, name + ".bpf.gz"), graph, queries);
                var requests = queries.Select((q, i) => new NavigationRequest(i, q.Start % graph.Width, q.Start / graph.Width, q.Target % graph.Width, q.Target / graph.Width)).ToArray();
                Console.WriteLine("BEGIN resident " + name);
                if (NavigationApi.SubmitBatch(status.Context, requests, out var ticket) != NavigationSubmission.Queued) throw new InvalidOperationException("Resident batch rejected.");
                var result = Wait(ticket); if (result.State != NavigationBatchState.Completed) throw new InvalidOperationException("Resident batch " + result.State + ": " + result.Reason);
                int found = 0, ties = 0; var oracle = new ProductionOracle(graph); var precise = new Stopwatch(); var expectedPaths = new RouteResult[count];
                for (int i = 0; i < count; i++)
                {
                    precise.Start(); var expected = oracle.Find(queries[i]); precise.Stop(); expectedPaths[i] = expected; var actual = result.Routes[i];
                    if (actual.RequestId != i || actual.State != (expected.Status == SearchStatus.Found ? NavigationRouteState.Found : NavigationRouteState.Unreachable) || actual.State == NavigationRouteState.Found && actual.Cost != expected.Cost) throw new InvalidOperationException("Resident/current Precise mismatch: " + name + " case=" + i);
                    if (actual.State == NavigationRouteState.Found) found++;
                    if (i < 3) { var independent = ReferenceSearch.Find(graph, queries[i], new WorkBudget(256L * 1024 * 1024, 15)); if (independent.Status != expected.Status || independent.Status == SearchStatus.Found && independent.Cost != expected.Cost) throw new InvalidOperationException("Independent reference mismatch."); }
                }
                double preciseMs = precise.Elapsed.TotalMilliseconds;
                var chunks = result.Routes.Where(r => r.State == NavigationRouteState.Found).Select(r => new NavigationSliceRequest(r.Handle, 0, 2000)).ToArray();
                var handleIds = result.Routes.Where(r => r.State == NavigationRouteState.Found).ToDictionary(r => r.Handle.Id, r => (int)r.RequestId);
                NavigationApi.SubmitSlices(chunks, out var sliceTicket); var slices = Wait(sliceTicket);
                if (slices.State != NavigationBatchState.Completed) throw new InvalidOperationException("Public full route output failed.");
                foreach (var slice in slices.Slices)
                {
                    int i = handleIds[slice.Route.Id];
                    byte[] directions = slice.Directions.ToArray(); var path = graph.Describe(queries[i], directions);
                    if (path.Status != SearchStatus.Found || path.Cost != result.Routes[i].Cost) throw new InvalidOperationException("Illegal public reconstructed path.");
                    var expected = expectedPaths[i]; if (!directions.SequenceEqual(expected.Directions)) ties++;
                }
                NavigationApi.Release(sliceTicket); NavigationApi.Release(ticket);
                NavigationApi.SubmitBatch(status.Context, requests, out var warmTicket); var warm = Wait(warmTicket); NavigationApi.Release(warmTicket);
                if (warm.State != NavigationBatchState.Completed) throw new InvalidOperationException("Warm batch incomplete.");
                var row = new { family = family == "distributed-profiles" ? "distributed-individual-goals" : family, count, found, queueMs = result.QueueMilliseconds, calculationMs = result.CalculationMilliseconds, warmMs = warm.CalculationMilliseconds, fullOutputAdditionalMs = slices.CalculationMilliseconds, preciseMs, equalCostDirectionDifferences = ties };
                rows.Add(row); Save("progress.json", new { phase = name, rows, metrics = service.Metrics() }); Console.WriteLine("END resident " + name + " calculationMs=" + result.CalculationMilliseconds.ToString("F3") + " warmMs=" + warm.CalculationMilliseconds.ToString("F3"));
            }
            var second = SnapshotCodec.ReadCaptured(args[2]); SnapshotCodec.WriteCaptured(Path.Combine(output, "input-2.raw.gz"), second);
            var nextGraph = Ground(second, player); int changed = 0; for (int n = 0; n < graph.Count; n++) if (graph.Word(n) != nextGraph.Word(n) || graph.IsValid(n) != nextGraph.IsValid(n)) changed++;
            var update = Stopwatch.StartNew(); service.Offer(Input(second, player)); status = Ready(player); double updateMs = update.Elapsed.TotalMilliseconds;
            var probes = Workloads.Create(nextGraph, seeds, 128, "formation");
            var updateRequests = probes.Select((q, i) => new NavigationRequest(i, q.Start % graph.Width, q.Start / graph.Width, q.Target % graph.Width, q.Target / graph.Width)).ToArray();
            NavigationApi.SubmitBatch(status.Context, updateRequests, out var updateTicket); var updated = Wait(updateTicket);
            using (var rebuilt = new AreaNavigator(nextGraph, new WorkBudget(512L * 1024 * 1024, 30)))
                for (int i = 0; i < probes.Count; i++) { probes[i].GroundCost = probes[i].MoatCost = 1; var expected = rebuilt.FindPath(probes[i]); var actual = updated.Routes[i]; if (actual.State != (expected.Status == SearchStatus.Found ? NavigationRouteState.Found : NavigationRouteState.Unreachable) || actual.State == NavigationRouteState.Found && actual.Cost != expected.Cost) throw new InvalidOperationException("Resident update/rebuild mismatch."); }
            NavigationApi.Release(updateTicket);
            Save("result.json", new { functionalContracts = "PASS", performanceAccepted = false, runtime = ".NET " + Environment.Version + " replay, not Unity/Mono", initializationMs, changedCoordinates = changed, updateMs, updateComparisons = probes.Count, rows, metrics = service.Metrics(), inputSha256 = args.Skip(1).Select(p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))).ToArray() });
            Console.WriteLine("PASS resident replay; performance remains open; evidence=" + output); return 0;
        }
        catch (Exception ex) { Save("failure.json", new { error = ex.ToString(), rows, metrics = service.Metrics() }); Console.Error.WriteLine(ex); return 1; }
        finally { service.End(1); using var drained = new ManualResetEvent(false); queue.Add(() => drained.Set()); drained.WaitOne(10000); queue.CompleteAdding(); thread.Join(10000); NavigationApi.Bind(null); }
    }
}
