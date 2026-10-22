using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace PerfProbe
{
    internal static partial class PerfProbeThreadSampler
    {
        private struct SpikeRequest
        {
            internal long Id, Start, End;
            internal string Name;
        }

        private static readonly ConcurrentQueue<SpikeRequest> spikeRequests = new ConcurrentQueue<SpikeRequest>();
        private static int pendingSpikes, suppressedSpikes, acceptedInWindow;
        private static long spikeBudgetWindow;

        internal static void RequestSpike(long id, string name, long started, long ended)
        {
            if (!enabled || worker == null) return;
            // Director/GUI spans duplicate a frame or EngineInterface spike; keep their event
            // lines, but request stack slices only for frames, simulation and engine calls.
            if (name == "main:Director.Update(total)" || name == "main:FatControler.NoesisGUIUpdateChecksInGame") return;
            if (Interlocked.Increment(ref pendingSpikes) > 16)
            {
                Interlocked.Decrement(ref pendingSpikes); Interlocked.Increment(ref suppressedSpikes); return;
            }
            spikeRequests.Enqueue(new SpikeRequest { Id = id, Name = name, Start = started, End = ended });
        }

        private static void DrainSpikeRequests()
        {
            long now = Stopwatch.GetTimestamp();
            if (now - spikeBudgetWindow >= Stopwatch.Frequency * 5)
            {
                spikeBudgetWindow = now; acceptedInWindow = 0;
                int suppressed = Interlocked.Exchange(ref suppressedSpikes, 0);
                if (suppressed > 0) Shared.DevDiag.Log(() => "spikeSample suppressed=" + suppressed);
            }
            // Bound symbol resolution/logging overhead. Sampling history is collected without
            // additional suspensions and retained across the ordinary two-second publications.
            if (!spikeRequests.TryDequeue(out SpikeRequest request)) return;
            Interlocked.Decrement(ref pendingSpikes);
            if (acceptedInWindow++ >= 12) { Interlocked.Increment(ref suppressedSpikes); return; }
            long started = Stopwatch.GetTimestamp();
            if (sampleMain) PublishSpike(main, request);
            if (sampleSim) PublishSpike(sim, request);
            ReportSamplerWork("spike", started);
        }

        private static void PublishSpike(Lane lane, SpikeRequest request)
        {
            var self = new Dictionary<string, int>();
            var inner = new Dictionary<string, int>();
            var callers = new Dictionary<string, int>();
            long first = 0, last = 0, previous = request.Start, maximumGap = 0;
            int samples = lane.History.Visit(request.Start, request.End, (addresses, offset, depth, stamp) =>
            {
                if (first == 0) first = stamp;
                maximumGap = Math.Max(maximumGap, stamp - previous); previous = stamp;
                last = stamp;
                string innerLabel = null, caller = null;
                for (int i = 0; i < depth; i++)
                {
                    string label = Label(addresses[offset + i], out string assembly);
                    if (i == 0) Bump(self, label);
                    if (innerLabel == null && IsMod(assembly)) innerLabel = label;
                    // A managed caller survives even when the leaf is a Monitor/OS wait.
                    if (caller == null && assembly == "Assembly-CSharp") caller = label;
                }
                Bump(inner, innerLabel ?? "(no mod frame)");
                Bump(callers, caller ?? "(caller unavailable)");
            });
            string Top(Dictionary<string, int> values) => string.Join(" | ", values.OrderByDescending(p => p.Value).Take(8)
                .Select(p => p.Key + "=" + p.Value.ToString(C) + "(" + (p.Value * 100.0 / Math.Max(1, samples)).ToString("F1", C) + "%)"));
            string head = string.Format(C,
                "spikeSample event={0} name={1} thread={2} durationMs={3:F1} samples={4} retainedStartClipped={5} " +
                "sampleSpanMs={6:F1} maxSampleGapMs={7:F1} startStamp={8} endStamp={9}", request.Id, request.Name, lane.Name,
                (request.End - request.Start) * 1000.0 / Stopwatch.Frequency, samples,
                lane.History.Oldest == 0 || request.Start < lane.History.Oldest,
                first == 0 ? 0 : (last - first) * 1000.0 / Stopwatch.Frequency,
                Math.Max(maximumGap, request.End - previous) * 1000.0 / Stopwatch.Frequency, request.Start, request.End);
            // Empty/clipped/sparse samples are explicit, never evidence of zero work. A sim
            // sample is taken only inside run; native-only work may have no managed caller.
            string selfText = Top(self), innerText = Top(inner), callerText = Top(callers);
            Shared.DevDiag.Log(() => head + " self: " + selfText);
            Shared.DevDiag.Log(() => head + " inner: " + innerText);
            Shared.DevDiag.Log(() => head + " caller: " + callerText);
        }

        private static void ReportSamplerWork(string kind, long started)
        {
            double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            // Always expose the profiler's own publication cost, including gaps in sampling.
            Shared.DevDiag.Log(() => "samplerAnalysis kind=" + kind + " wallMs=" + ms.ToString("F2", C));
        }
    }
}
