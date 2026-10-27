#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using BetterPathfindingTest.Core;

namespace BetterPathfindingTest
{
    internal sealed class SessionWork
    {
        internal readonly long Id;
        internal readonly string Context;
        internal readonly CancellationTokenSource Cancel = new CancellationTokenSource();
        private readonly string directory;
        private CapturedMap first;
        private ManagedEvidence firstEvidence;
        private readonly Action<string, string> writeText;
        private readonly Action<string> createDirectory;
        internal bool HasPreviousCapture => first != null;
        internal Exception LastFailure { get; private set; }
        private const long MaximumBytes = 256L * 1024 * 1024;
        internal Action Job(CaptureEnvelope envelope, int ordinal) => () => Process(envelope, ordinal);
        internal SessionWork(long id, string context, string output, Action<string, string> writeText = null, Action<string> createDirectory = null)
        {
            Id = id; Context = context; this.writeText = writeText ?? WriteFile; this.createDirectory = createDirectory ?? (path => Directory.CreateDirectory(path));
            directory = Path.Combine(output, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-s" + id);
        }
        private static void WriteFile(string path, string text) => File.WriteAllText(path, text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
        private void WriteJson(string path, object data) => writeText(path, Shared.DependencyFreeJson.Serialize(data));
        internal void Release() { first = null; firstEvidence = null; }
        internal Action FailureJob(Exception error, int ordinal) => () =>
        {
            Exception failure = RunGuard.Execute(() => {
                createDirectory(directory); WriteJson(Path.Combine(directory, "snapshot-" + ordinal + ".json"), new Dictionary<string, object> { ["status"] = "INCOMPLETE", ["session"] = Id, ["ordinal"] = ordinal, ["phase"] = "capture", ["error"] = error.ToString() });
                writeText(Path.Combine(directory, "snapshot-" + ordinal + ".md"), "# BetterPathfindingTest\r\n\r\nINCOMPLETE: " + error.Message + "\r\n");
            }, Release);
            if (failure != null) Shared.DevDiag.Log(() => "phase=report-failed session=" + Id + " ordinal=" + ordinal + " error=" + failure);
        };
        internal static void Unsupported(string output, Exception error)
        {
            Exception failure = RunGuard.Execute(() => {
                Directory.CreateDirectory(output); string path = Path.Combine(output, "unsupported-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                WriteFile(path + ".json", Shared.DependencyFreeJson.Serialize(new Dictionary<string, object> { ["status"] = "UNSUPPORTED", ["phase"] = error is CaptureContractException ? "capture-contract" : "initialization", ["error"] = error.ToString(), ["runtime"] = "Unity/Mono + private x64 backend" }));
                WriteFile(path + ".md", "# BetterPathfindingTest\r\n\r\nUNSUPPORTED: " + error.Message + "\r\n");
            }, () => { });
            if (failure != null) Shared.DevDiag.Log(() => "phase=report-failed initialization error=" + failure);
        }
        internal void Process(CaptureEnvelope envelope, int ordinal)
        {
            if (Cancel.IsCancellationRequested) return;
            bool succeeded = false;
            Exception failure = RunGuard.Execute(() => { PersistAndCompare(envelope, ordinal); succeeded = true; }, () => { if (!succeeded || ordinal == 2 || Cancel.IsCancellationRequested) Release(); });
            LastFailure = failure;
            if (failure != null) Shared.DevDiag.Log(() => "phase=report-failed session=" + Id + " ordinal=" + ordinal + " status=INCOMPLETE error=" + failure);
        }
        private void PersistAndCompare(CaptureEnvelope envelope, int ordinal)
        {
            var budget = new WorkBudget(MaximumBytes, 120, Cancel.Token); var captureWatch = Stopwatch.StartNew();
            budget.Reserve(envelope.Map.BufferBytes + (first?.BufferBytes ?? 0) + 16L * 1024 * 1024);
            createDirectory(directory); envelope.MaterializeGates(budget); envelope.Map.Validate();
            string prefix = "snapshot-" + ordinal;
            SnapshotCodec.WriteCaptured(Path.Combine(directory, prefix + ".raw.gz"), envelope.Map);
            WriteJson(Path.Combine(directory, prefix + ".json"), new Dictionary<string, object> {
                ["status"] = "CAPTURED; comparison pending", ["session"] = Id, ["ordinal"] = ordinal, ["context"] = Context,
                ["snapshotCopyMs"] = envelope.Map.CopyMilliseconds, ["snapshotBuffers"] = envelope.Map.BufferBytes, ["managedEvidence"] = envelope.Evidence.Data(),
                ["missingPhases"] = ordinal == 1 ? "awaiting second snapshot; updates; all workload comparisons" : "updates; all workload comparisons" });
            Shared.DevDiag.Log(() => "phase=snapshot-persisted session=" + Id + " ordinal=" + ordinal + " elapsedMs=" + captureWatch.ElapsedMilliseconds + " comparison=pending");
            if (ordinal == 1) { first = envelope.Map; firstEvidence = envelope.Evidence; return; }
            // No large group work runs before BOTH raw captures are on disk.
            if (first == null) throw new InvalidOperationException("Paired comparison lacks first snapshot.");
            int player = first.Players[0]; budget.Check();
            GridSnapshot before = first.Build(player, budget: budget), after = envelope.Map.Build(player, budget: budget);
            var updateClock = Stopwatch.StartNew();
            Shared.DevDiag.Log(() => "phase=real-updates session=" + Id + " ordinal=2 cases=8");
            var updateQueries = Workloads.Create(after, envelope.Map.Units, 8, "shared-goal");
            SnapshotCodec.WriteQueries(Path.Combine(directory, "snapshot-2-update.bpf.gz"), after, updateQueries);
            var update = UpdateComparison.Compare(before, after, updateQueries, budget);
            var controlled = UpdateComparison.Controlled(budget);
            update["wallMs"] = updateClock.Elapsed.TotalMilliseconds;
            update["coverage"] = updateQueries.Count == 0 ? "NO_POSITIVE_INPUTS" : (long)update["changedCoordinates"] == 0 ? "unchanged pair; no real-change acceptance" : "changed snapshot pair";
            WriteJson(Path.Combine(directory, "updates.json"), new Dictionary<string, object> { ["session"] = Id, ["real"] = update, ["controlled"] = controlled });
            Shared.DevDiag.Log(() => "phase=updates-finished session=" + Id + " ordinal=2 cases=" + update["compared"] + " differences=" + update["differences"]);
            CompareSnapshot(first, firstEvidence, before, 1, update, controlled);
            CompareSnapshot(envelope.Map, envelope.Evidence, after, 2, update, controlled);
        }
        private void CompareSnapshot(CapturedMap raw, ManagedEvidence evidence, GridSnapshot graph, int ordinal, object update, object controlled)
        {
            var total = Stopwatch.StartNew(); string prefix = "snapshot-" + ordinal;
            var summary = new Dictionary<string, object> {
                ["session"] = Id, ["ordinal"] = ordinal, ["context"] = Context, ["runtime"] = "Unity/Mono + private C++ area backend",
                ["snapshotCopyMs"] = raw.CopyMilliseconds, ["nonblockingLockRetryMs"] = raw.LockWaitMilliseconds, ["snapshotBuffers"] = raw.BufferBytes,
                ["managedEvidence"] = evidence.Data(), ["gateCoverage"] = raw.GateCoverage, ["nativeHash"] = raw.NativeHash, ["testedPlayer"] = graph.Player,
                ["capturedPlayers"] = raw.Players, ["realUpdates"] = update, ["controlledUpdates"] = controlled,
                ["missingCoverage"] = "Only first player tested; game movement/10,000 captured units not integrated. Terminal/cadence/profile-limit contracts are offline. Precise oracle is offline only. Large-map independent references are sampled."
            };
            var details = new List<object>(); summary["workloads"] = details;
            int compared = 0, planned = 0, found = 0, errors = 0, incomplete = 0, skipped = 0;
            var budget = new WorkBudget(MaximumBytes, 120, Cancel.Token);
            var pulse = new ProgressPulse(() => total.Elapsed.TotalSeconds, message => Shared.DevDiag.Log(() => "session=" + Id + " ordinal=" + ordinal + " " + message + " cases=" + compared + "/" + planned));
            budget.Checkpoint = pulse.Check;
            try
            {
                budget.Reserve(raw.BufferBytes + (first?.BufferBytes ?? 0) + graph.BufferBytes + 16L * 1024 * 1024);
                foreach (string family in new[] { "shared-goal", "formation", "distributed-profiles", "moat-required" })
                {
                    bool failedPreparation = false;
                    foreach (int count in graph.Count <= 4096 ? new[] { 1, 32, 128, 512 } : new[] { 1, 32, 128, 512, 10000 })
                    {
                        planned += count; if (failedPreparation) { skipped += count; incomplete++; continue; }
                        budget.Check(); string name = family + "-" + count; pulse.Phase = name;
                        var inputClock = Stopwatch.StartNew(); List<RouteQuery> queries = Workloads.Create(graph, raw.Units, count, family);
                        double inputMs = inputClock.Elapsed.TotalMilliseconds;
                        SnapshotCodec.WriteQueries(Path.Combine(directory, prefix + "-" + name + ".bpf.gz"), graph, queries);
                        Shared.DevDiag.Log(() => "phase=" + name + " session=" + Id + " ordinal=" + ordinal + " cases=" + queries.Count);
                        BenchmarkResult result = Benchmark.Run(graph, queries, budget);
                        if (queries.Count != count) { result.Incomplete++; result.Limitation = "No known positive inputs for this family; missing coverage."; }
                        compared += result.Completed; found += result.Found; errors += result.Mismatches; incomplete += result.Incomplete;
                        var data = result.Data(); data["inputGenerationMs"] = inputMs; data["requestCountIsUnitCount"] = false;
                        details.Add(new Dictionary<string, object> { ["family"] = name, ["result"] = data });
                        writeText(Path.Combine(directory, prefix + "-" + name + ".md"), result.Markdown(name, "Unity/Mono + native x64"));
                        WriteJson(Path.Combine(directory, prefix + ".progress.json"), new Dictionary<string, object> { ["session"] = Id, ["ordinal"] = ordinal, ["phase"] = name, ["compared"] = compared, ["found"] = found, ["elapsedMs"] = total.Elapsed.TotalMilliseconds });
                        failedPreparation = queries.Count == 0 || result.Incomplete != 0 && result.Completed == 0;
                    }
                }
            }
            catch (SearchBudgetException ex) { incomplete++; summary["budgetAbort"] = ex.Message; }
            catch (Exception ex) { incomplete++; summary["error"] = ex.ToString(); }
            finally
            {
                if (Cancel.IsCancellationRequested) { incomplete++; summary["cancelledByLifecycle"] = true; }
                string status = errors != 0 ? "FAILED" : incomplete != 0 || compared != planned ? "INCOMPLETE" : found == 0 ? "NEGATIVE CASES ONLY" : "CHECKED CASES PASSED; coverage/directions/game movement outstanding";
                summary["status"] = status; summary["compared"] = compared; summary["planned"] = planned; summary["found"] = found; summary["errors"] = errors; summary["incomplete"] = incomplete;
                summary["skippedRequests"] = skipped; summary["elapsedMs"] = total.Elapsed.TotalMilliseconds; summary["lastPhase"] = pulse.Phase; summary["navigationPeakBytes"] = budget.PeakBytes; summary["managedHeapAtEnd"] = GC.GetTotalMemory(false);
                WriteJson(Path.Combine(directory, prefix + ".json"), summary);
                writeText(Path.Combine(directory, prefix + ".md"), "# BetterPathfindingTest area-navigation prototype\r\n\r\nResult: " + status + "\r\n\r\nCompleted " + compared + "/" + planned + "; found " + found + "; errors " + errors + "; incomplete " + incomplete + ".\r\n\r\nSee adjacent JSON/workload reports. Positive-query and sampled-reference coverage are explicit. This does not certify game movement or 10,000 actual game units. No orders, native hooks, settings or path buffers were changed.\r\n");
                Shared.DevDiag.Log(() => "phase=finished session=" + Id + " ordinal=" + ordinal + " status=" + status + " elapsedMs=" + total.ElapsedMilliseconds + " cases=" + compared + "/" + planned + " found=" + found + " report=" + directory);
            }
        }
    }
}
#endif