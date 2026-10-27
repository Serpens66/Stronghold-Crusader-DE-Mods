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
        private readonly Action<string, string> writeText;
        private readonly Action<string> createDirectory;
        internal bool HasPreviousCapture => first != null;
        private const long MaximumBytes = 256L * 1024 * 1024;
        internal Action Job(CaptureEnvelope envelope, int ordinal) => () => Process(envelope, ordinal);
        internal Action FailureJob(Exception error, int ordinal) => () =>
        {
            Exception failure = RunGuard.Execute(() => {
                createDirectory(directory);
                WriteJson(Path.Combine(directory, "snapshot-" + ordinal + ".json"), new Dictionary<string, object> { ["status"] = "INCOMPLETE", ["session"] = Id, ["ordinal"] = ordinal, ["phase"] = "capture", ["error"] = error.ToString() });
                writeText(Path.Combine(directory, "snapshot-" + ordinal + ".md"), "# BetterPathfindingTest\r\n\r\nINCOMPLETE: capture failed: " + error.Message + "\r\n");
            }, Release);
            if (failure != null) Shared.DevDiag.Log(() => "phase=report-failed session=" + Id + " ordinal=" + ordinal + " error=" + failure);
        };
        internal void Release() { first = null; }
        internal SessionWork(long id, string context, string output, Action<string, string> writeText = null, Action<string> createDirectory = null)
        {
            Id = id; Context = context;
            this.writeText = writeText ?? WriteFile;
            this.createDirectory = createDirectory ?? (path => Directory.CreateDirectory(path));
            directory = Path.Combine(output, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-s" + id);
        }
        private static void Json(string path, object data) => File.WriteAllText(path, Shared.DependencyFreeJson.Serialize(data), new UTF8Encoding(false));
        private static void WriteFile(string path, string text) => File.WriteAllText(path, text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
        private void WriteJson(string path, object data) => writeText(path, Shared.DependencyFreeJson.Serialize(data));
        internal static void Unsupported(string output, Exception error)
        {
            Exception failure = RunGuard.Execute(() => {
                Directory.CreateDirectory(output); string path = Path.Combine(output, "unsupported-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                Json(path + ".json", new Dictionary<string, object> { ["status"] = "UNSUPPORTED", ["phase"] = error is CaptureContractException ? "capture-contract" : "initialization", ["error"] = error.ToString(), ["runtime"] = "Unity/Mono" });
                File.WriteAllText(path + ".md", "# BetterPathfindingTest\r\n\r\nUNSUPPORTED: " + error.Message + "\r\n\r\nNo snapshot/search comparison was accepted.\r\n");
            }, () => { });
            if (failure != null) Shared.DevDiag.Log(() => "phase=report-failed initialization error=" + failure);
        }
        internal void Process(CaptureEnvelope envelope, int ordinal)
        {
            if (Cancel.IsCancellationRequested) return;
            Exception failure = RunGuard.Execute(() => ProcessCore(envelope, ordinal), () => { if (ordinal == 2 || Cancel.IsCancellationRequested) Release(); });
            if (failure != null) Shared.DevDiag.Log(() => "phase=report-failed session=" + Id + " ordinal=" + ordinal + " status=INCOMPLETE error=" + failure);
        }
        private void ProcessCore(CaptureEnvelope envelope, int ordinal)
        {
            var total = Stopwatch.StartNew(); var raw = envelope.Map;
            string prefix = "snapshot-" + ordinal;
            var summary = new Dictionary<string, object> { ["session"] = Id, ["context"] = Context, ["runtime"] = "Unity/Mono (not .NET offline)", ["nativeHash"] = raw.NativeHash, ["managedHash"] = raw.ManagedHash, ["gateCoverage"] = raw.GateCoverage, ["snapshotCopyMs"] = raw.CopyMilliseconds, ["nonblockingLockRetryMs"] = raw.LockWaitMilliseconds, ["snapshotBuffers"] = raw.BufferBytes, ["limitBytes"] = MaximumBytes, ["limitSeconds"] = 120 };
            var details = new List<object>(); summary["workloads"] = details;
            summary["ordinal"] = ordinal; summary["managedEvidence"] = envelope.Evidence.Data();
            summary["controlledUpdates"] = "NOT_RUN"; summary["realUpdates"] = ordinal == 1 ? "Awaiting second snapshot" : "NOT_RUN";
            var markdown = new StringBuilder("# BetterPathfindingTest automatic comparison\r\n\r\n");
            markdown.AppendLine("Session " + Id + "; snapshot " + ordinal + "; " + Context).AppendLine();
            var budget = new WorkBudget(MaximumBytes, 120, Cancel.Token);
            int errors = 0, incomplete = 0, planned = 2 * (1 + 32 + 128 + 512), completed = 0;
            var pulse = new ProgressPulse(() => total.Elapsed.TotalSeconds, message => Shared.DevDiag.Log(() => "session=" + Id + " ordinal=" + ordinal + " " + message + " cases=" + completed + "/" + planned));
            budget.Checkpoint = pulse.Check;
            try
            {
                pulse.Phase = "snapshot-write"; createDirectory(directory);
                // Accounts for both retained raw captures and graph-construction/copy/output headroom.
                budget.Reserve(raw.BufferBytes + (first?.BufferBytes ?? 0) + 16L * 1024 * 1024);
                CapturedMap previousRaw = first;
                pulse.Phase = "policy-materialization"; envelope.MaterializeGates(budget); raw.Validate();
                if (ordinal == 1) first = raw;
                pulse.Phase = "snapshot-write"; SnapshotCodec.WriteCaptured(Path.Combine(directory, prefix + ".raw.gz"), raw); budget.Check();
                pulse.Phase = "graph-build";
                var graphWatch = Stopwatch.StartNew(); GridSnapshot graph = raw.Build(previousRaw == null ? raw.Players[0] : previousRaw.Players[0], budget: budget);
                summary["graphBuildMs"] = graphWatch.Elapsed.TotalMilliseconds; summary["graphFingerprint"] = graph.Fingerprint().ToString("X16");
                summary["testedPlayer"] = graph.Player; summary["capturedPlayers"] = raw.Players;
                summary["missingCoverage"] = "Only first captured player's access context is exercised per run. Native reservation/terminal queries, alternate cadence profiles and 40-tick qualification use offline synthetic contracts. No assassin/native movement integration. Precise production oracle is offline only.";
                if (ordinal == 2 && previousRaw == null) { incomplete++; summary["realUpdates"] = "NOT_RUN: no usable first capture"; }
                RunPhases.Execute(previousRaw != null, () => {
                    pulse.Phase = "controlled-updates";
                    var controlled = UpdateComparison.Controlled(budget); summary["controlledUpdates"] = controlled;
                    if ((bool)controlled["incomplete"]) incomplete++; else if (!(bool)controlled["passed"]) errors++;
                }, () => {
                    pulse.Phase = "real-updates";
                    graphWatch.Restart(); GridSnapshot previous = previousRaw.Build(graph.Player, budget: budget); summary["previousGraphRebuildMs"] = graphWatch.Elapsed.TotalMilliseconds;
                    var updateQueries = Benchmark.Workload(graph, raw.Units, 8, false);
                    SnapshotCodec.WriteQueries(Path.Combine(directory, prefix + "-update.bpf.gz"), graph, updateQueries);
                    var update = UpdateComparison.Compare(previous, graph, updateQueries, budget); summary["realUpdates"] = update;
                    errors += (int)update["differences"]; if ((bool)update["incomplete"]) incomplete++;
                }, () => {
                  foreach (bool formation in new[] { false, true }) foreach (int count in new[] { 1, 32, 128, 512 })
                {
                    string family = (formation ? "formation-profiles-" : "shared-goal-") + count;
                    pulse.Phase = family;
                    List<RouteQuery> queries = Benchmark.Workload(graph, raw.Units, count, formation);
                    SnapshotCodec.WriteQueries(Path.Combine(directory, prefix + "-" + family + ".bpf.gz"), graph, queries);
                    Shared.DevDiag.Log(() => "phase=" + family + " session=" + Id + " ordinal=" + ordinal + " elapsedMs=" + total.ElapsedMilliseconds + " cases=" + queries.Count);
                    BenchmarkResult result = Benchmark.Run(graph, queries, budget);
                    if (queries.Count != count) { result.Incomplete++; result.Limitation = "No captured valid coordinates for planned cohort."; }
                    completed += result.Completed; errors += result.Mismatches; incomplete += result.Incomplete;
                    details.Add(new Dictionary<string, object> { ["family"] = family, ["result"] = result.Data() });
                    writeText(Path.Combine(directory, prefix + "-" + family + ".md"), result.Markdown(family, "Unity/Mono"));
                }
                });
            }
            catch (SearchBudgetException ex) { incomplete++; summary["budgetAbort"] = ex.Message; }
            catch (Exception ex) { incomplete++; summary["error"] = ex.ToString(); }
            finally
            {
                if (Cancel.IsCancellationRequested) { incomplete++; summary["cancelledByLifecycle"] = true; }
                string status = errors != 0 ? "FAILED" : incomplete != 0 || completed != planned ? "INCOMPLETE" : "SEARCH COMPARISONS PASSED; path-fidelity review outstanding";
                summary["lastPhase"] = pulse.Phase; summary["skippedRequests"] = planned - completed;
                summary["status"] = status; summary["compared"] = completed; summary["planned"] = planned; summary["errors"] = errors; summary["incomplete"] = incomplete; summary["elapsedMs"] = total.Elapsed.TotalMilliseconds; summary["navigationPeakBytes"] = budget.PeakBytes; summary["managedHeapAtEnd"] = GC.GetTotalMemory(false);
                markdown.AppendLine("Result: " + status).AppendLine().AppendLine("Compared " + completed + "/" + planned + "; errors " + errors + "; incomplete " + incomplete + ". Elapsed " + total.ElapsedMilliseconds + " ms.").AppendLine();
                markdown.AppendLine("Snapshot copy: " + raw.CopyMilliseconds.ToString("F3") + " ms; lock retries: " + raw.LockWaitMilliseconds.ToString("F3") + " ms. Tracked peak: " + budget.PeakBytes + " bytes.").AppendLine();
                markdown.AppendLine("See adjacent JSON for phases, hash identities, update evidence and missing coverage; workload Markdown files include search timing and direction differences. Production Precise comparisons require offline replay of each .bpf.gz file. Shared profiles 1/7 are synthetic; formation profiles use captured cadence only when unambiguous. Unreachable and long-route coverage is guaranteed by offline contracts, not claimed for every real map.").AppendLine();
                markdown.AppendLine("No orders, traversal provider, native hooks, path buffers or game settings were changed. Cost parity alone does not accept direction parity or native movement. No consumer integration is approved. Managed heap deltas are retained heap observations, not allocation totals; .NET offline allocation measurements are separate.");
                WriteJson(Path.Combine(directory, prefix + ".json"), summary); writeText(Path.Combine(directory, prefix + ".md"), markdown.ToString());
                Shared.DevDiag.Log(() => "phase=finished session=" + Id + " ordinal=" + ordinal + " status=" + status + " elapsedMs=" + total.ElapsedMilliseconds + " cases=" + completed + "/" + planned + " report=" + directory);
            }
        }
    }
}
#endif
