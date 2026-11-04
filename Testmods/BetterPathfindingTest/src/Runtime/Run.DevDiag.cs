#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BetterPathfindingTest.Api;
using BetterPathfindingTest.Core;

namespace BetterPathfindingTest
{
    internal sealed class ResidentReports
    {
        private readonly string directory;
        private readonly Action<string, string> write;
        private readonly Action<string> create;
        private int saved;
        private readonly long session;
        private double firstSaved;
        internal string DirectoryPath => directory;
        internal ResidentReports(string root, long session, Action<string, string> writer = null, Action<string> creator = null)
        {
            this.session = session; directory = Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-s" + session);
            write = writer ?? WriteFile; create = creator ?? (path => Directory.CreateDirectory(path));
        }
        private static void WriteFile(string path, string text) => File.WriteAllText(path, text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
        private void Json(string name, object data) { create(directory); write(Path.Combine(directory, name), Shared.DependencyFreeJson.Serialize(data)); }
        internal void Persist(CaptureEnvelope envelope, double capturedAt)
        {
            if (envelope.Map.Session != session || saved >= 2 || saved == 1 && capturedAt - firstSaved < 10) return;
            create(directory); int ordinal = saved + 1;
            SnapshotCodec.WriteCaptured(Path.Combine(directory, "snapshot-" + ordinal + ".raw.gz"), envelope.Map);
            Json("snapshot-" + ordinal + ".json", new Dictionary<string, object> {
                ["session"] = envelope.Map.Session, ["ordinal"] = ordinal, ["status"] = "CAPTURED; resident status reported separately",
                ["snapshotCopyMs"] = envelope.Map.CopyMilliseconds, ["snapshotBuffers"] = envelope.Map.BufferBytes,
                ["managedEvidence"] = envelope.Evidence.Data(), ["gateCoverage"] = envelope.Map.GateCoverage,
                ["localPlayer"] = envelope.LocalPlayer, ["context"] = envelope.Map.Context });
            if (saved == 0) firstSaved = capturedAt; saved++;
            Shared.DevDiag.Log(() => "phase=snapshot-persisted session=" + envelope.Map.Session + " ordinal=" + ordinal + " resident=true");
        }
        internal void Status(NavigationStatus status, long peak, Dictionary<string, object> metrics = null)
        {
            if (status.Context.Session != session) return;
            Json("resident.json", new Dictionary<string, object> {
                ["session"] = status.Context.Session, ["player"] = status.Context.PlayerId, ["generation"] = status.Context.Generation,
                ["functionalReady"] = status.IsReady, ["state"] = status.State.ToString(), ["phase"] = status.Phase, ["reason"] = status.Reason,
                ["snapshotAgeMs"] = status.SnapshotAgeMilliseconds, ["residentReservedBytes"] = status.MemoryBytes, ["residentPeakBytes"] = peak,
                ["limitBytes"] = status.MemoryLimitBytes, ["pendingBatches"] = status.PendingBatches, ["savedSnapshots"] = saved,
                ["performanceAccepted"] = false, ["specialRules"] = "Unsupported: moat, assassin, additional resource/terminal conditions",
                ["metrics"] = metrics,
                ["backend"] = "existing exact area/field kernel; CCH remains offline only", ["memoryScope"] = "Conservative source/client pools plus backend buffers and retained route leases; caller-owned released data and process heap excluded" });
            write(Path.Combine(directory, "resident.md"), "# BetterPathfindingTest resident prototype\r\n\r\nFunctional state: " + status.State + "; player " + status.Context.PlayerId + "; generation " + status.Context.Generation + ".\r\n\r\nPerformance target remains NOT ACCEPTED. Special rules are unsupported. Ready describes a completed captured generation, not native movement authority. See resident.json and api-probe.json.\r\n");
            if (status.IsReady) Json("resident-ready.json", new Dictionary<string, object> { ["session"] = session, ["generation"] = status.Context.Generation, ["player"] = status.Context.PlayerId, ["functionalReady"] = true, ["performanceAccepted"] = false, ["snapshotAgeMs"] = status.SnapshotAgeMilliseconds });
        }
        internal void Probe(NavigationBatchResult batch)
        {
            if (batch.Context.Session != session) return;
            int found = 0, unreachable = 0, incomplete = 0;
            foreach (NavigationRoute route in batch.Routes) { if (route.State == NavigationRouteState.Found) found++; else if (route.State == NavigationRouteState.Unreachable) unreachable++; else incomplete++; }
            Json("api-probe.json", new Dictionary<string, object> {
                ["session"] = batch.Context.Session, ["generation"] = batch.Context.Generation, ["ticket"] = batch.Ticket.Id,
                ["state"] = batch.State.ToString(), ["requestCount"] = batch.Routes.Count, ["found"] = found,
                ["unreachable"] = unreachable, ["incomplete"] = incomplete, ["queueMs"] = batch.QueueMilliseconds,
                ["calculationMs"] = batch.CalculationMilliseconds, ["scope"] = "Public API self-probe from copied coordinates; not movement, not distinct captured units", ["performanceAccepted"] = false });
        }
        internal void Requests(NavigationContext context, GridSnapshot graph, NavigationRequest[] requests)
        {
            if (context.Session != session) return;
            create(directory); var queries = new List<RouteQuery>(); var identities = new List<object>();
            foreach (var request in requests) { queries.Add(new RouteQuery { Start = request.StartY * graph.Width + request.StartX, Target = request.TargetY * graph.Width + request.TargetX, MaximumEdges = request.MaximumSteps, GroundCost = 1, MoatCost = 1 }); identities.Add(new Dictionary<string, object> { ["id"] = request.RequestId, ["startX"] = request.StartX, ["startY"] = request.StartY, ["targetX"] = request.TargetX, ["targetY"] = request.TargetY, ["maximumSteps"] = request.MaximumSteps }); }
            SnapshotCodec.WriteQueries(Path.Combine(directory, "api-probe.bpf.gz"), graph, queries);
            Json("api-requests.json", new Dictionary<string, object> { ["session"] = session, ["generation"] = context.Generation, ["player"] = context.PlayerId, ["requests"] = identities });
        }
        internal static void Unsupported(string output, Exception error)
        {
            try
            {
                Directory.CreateDirectory(output); string name = Path.Combine(output, "unsupported-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                WriteFile(name + ".json", Shared.DependencyFreeJson.Serialize(new Dictionary<string, object> { ["status"] = "UNSUPPORTED", ["error"] = error.ToString() }));
                WriteFile(name + ".md", "# BetterPathfindingTest\r\n\r\nUNSUPPORTED: " + error.Message + "\r\n");
            }
            catch (Exception ex) { Shared.DevDiag.Log(() => "phase=report-failed initialization=" + ex.Message); }
        }
    }
}
#endif
