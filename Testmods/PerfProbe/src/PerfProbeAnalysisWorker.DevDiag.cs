using System;
using System.Diagnostics;
using System.Threading;

namespace PerfProbe
{
    internal static partial class PerfProbeThreadSampler
    {
        private static long samplerCpu = -1;
        private static long captureStarted;
        private static PerfProbeAnalysisJob ClaimJob(bool spike)
        {
            foreach (var job in analysisJobs) if (job.Spike == spike && job.TryClaim()) return job;
            return null;
        }
        private static void FillJob(PerfProbeAnalysisJob job, long start, long end)
        {
            job.Start = start; job.End = end; job.Sequence = ++jobSequence;
            if (sampleMain) main.History.CopyTo(job.Main, start, end); else job.Main.Reset();
            if (sampleSim) sim.History.CopyTo(job.Sim, start, end); else job.Sim.Reset();
            job.Main.OsThreadId = main.ThreadId; job.Sim.OsThreadId = sim.ThreadId;
        }
        private static void QueueWindow(long start, long end)
        {
            var job = ClaimJob(false);
            if (job == null) { windowDrops++; return; }
            FillJob(job, start, end);
            long cpu = PerfProbeLockTiming.ThreadCpuTime();
            job.SamplerCpu = samplerCpu < 0 || cpu < 0 ? -1 : cpu - samplerCpu; samplerCpu = cpu;
            job.AccountingStart = captureStarted; job.AccountingEnd = end; captureStarted = end;
            job.SuspendedTicks = suspendedTicks; job.MaxSuspendedTicks = maxSuspendedTicks;
            suspendedTicks = maxSuspendedTicks = 0;
            job.WindowDrops = windowDrops; windowDrops = 0;
            job.RequestDrops = Interlocked.Exchange(ref suppressedSpikes, 0);
            job.CadenceMode = cadence.Mode; job.CadenceWaits = cadence.Waits;
            job.CadenceLate = cadence.LateTicks; job.CadenceMaxLate = cadence.MaxLateTicks;
            job.CadenceMissed = cadence.Missed; cadence.ResetStats();
            job.Publish(); analysisReady.Set();
        }
        private static void AnalysisLoop()
        {
            while (true)
            {
                analysisReady.WaitOne(1000);
                while (true)
                {
                    PerfProbeAnalysisJob next = null;
                    // Spike jobs have priority; regular snapshots are limited to two slots.
                    foreach (var candidate in analysisJobs)
                        if (candidate.Ready && (next == null || (candidate.Spike && !next.Spike) ||
                            (candidate.Spike == next.Spike && candidate.Sequence < next.Sequence))) next = candidate;
                    if (next == null || !next.TryRead()) break;
                    long started = Stopwatch.GetTimestamp(), cpu = PerfProbeLockTiming.ThreadCpuTime();
                    int gc = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
                    try
                    {
                        if (started - imagesRefreshed >= Stopwatch.Frequency)
                        {
                            ResolveGameImage(); RefreshImages(); imagesRefreshed = started;
                        }
                        if (next.Spike)
                        {
                            if (sampleMain) PublishSpike(next.Main, next);
                            if (sampleSim) PublishSpike(next.Sim, next);
                            string owners = PerfProbeLockOwners.Describe(next.Start, next.End);
                            Shared.DevDiag.Log(() => "lockHistory event=" + next.Id + " startStamp=" + next.Start + " endStamp=" + next.End + " " + owners);
                        }
                        else
                        {
                            double seconds = (next.End - next.Start) / (double)Stopwatch.Frequency;
                            if (sampleMain) Publish(next.Main, seconds, next.Start, next.End);
                            if (sampleSim) Publish(next.Sim, seconds, next.Start, next.End);
                            Shared.DevDiag.Log(() => "samplerCapture startStamp=" + next.AccountingStart + " endStamp=" + next.AccountingEnd +
                                " sampleStartStamp=" + next.Start + " sampleEndStamp=" + next.End +
                                " cpuMs=" + (next.SamplerCpu < 0 ? "unavailable" : (next.SamplerCpu / 10000.0).ToString("F3", C)) +
                                " suspendedMs=" + (next.SuspendedTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", C) +
                                " maxSuspendedMs=" + (next.MaxSuspendedTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", C) +
                                " windowDrops=" + next.WindowDrops + " spikeRequestsSuppressed=" + next.RequestDrops +
                                " cadenceMode=" + next.CadenceMode + " cadenceWaits=" + next.CadenceWaits +
                                " missedIntervals=" + next.CadenceMissed +
                                " meanWakeLateUs=" + (next.CadenceLate * 1000000.0 / Stopwatch.Frequency / Math.Max(1, next.CadenceWaits)).ToString("F1", C) +
                                " maxWakeLateUs=" + (next.CadenceMaxLate * 1000000.0 / Stopwatch.Frequency).ToString("F1", C));
                        }
                        long end = Stopwatch.GetTimestamp(), cpuEnd = PerfProbeLockTiming.ThreadCpuTime();
                        Shared.DevDiag.Log(() => "samplerAnalysis kind=" + (next.Spike ? "spike" : "window") +
                            " startStamp=" + started + " endStamp=" + end + " wallMs=" + ((end - started) * 1000.0 / Stopwatch.Frequency).ToString("F2", C) +
                            " cpuMs=" + (cpu < 0 || cpuEnd < 0 ? "unavailable" : ((cpuEnd - cpu) / 10000.0).ToString("F3", C)) +
                            " gcDuring=" + (GC.CollectionCount(0) - gc) + " gc1During=" + (GC.CollectionCount(1) - gc1) +
                            " gc2During=" + (GC.CollectionCount(2) - gc2) + " gcCountersAreNotPauseDurations=true");
                    }
                    catch (Exception exception)
                    {
                        Shared.DevDiag.Log(() => "samplerAnalysis failed kind=" + (next.Spike ? "spike" : "window") + " error=" + exception.GetType().Name);
                    }
                    finally { next.Release(); }
                }
            }
        }
    }
}
