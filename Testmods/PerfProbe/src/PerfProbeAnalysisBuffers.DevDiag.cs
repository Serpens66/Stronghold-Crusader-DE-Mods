using System;
using System.Threading;

namespace PerfProbe
{
    internal sealed class PerfProbeSampleBatch
    {
        internal readonly ulong[] Frames;
        internal readonly byte[] Depth;
        internal readonly long[] Stamps;
        internal int Count, Dropped, OsThreadId;
        internal string Name;
        internal long Oldest;
        internal readonly int FrameLimit;
        internal PerfProbeSampleBatch(int capacity, int frameLimit)
        { FrameLimit = frameLimit; Frames = new ulong[checked(capacity * frameLimit)]; Depth = new byte[capacity]; Stamps = new long[capacity]; }
        internal void Reset() { Count = Dropped = 0; Oldest = 0; }
        internal void Add(ulong[] frames, int offset, int depth, long stamp)
        {
            if (Count == Depth.Length) { Dropped++; return; }
            Array.Copy(frames, offset, Frames, Count * FrameLimit, depth);
            Depth[Count] = (byte)depth; Stamps[Count++] = stamp;
        }
    }

    // Reusable single-producer/single-consumer ownership. The sampler never waits for
    // symbol resolution and the consumer never reads a live ring or producer's scratch.
    internal sealed class PerfProbeAnalysisJob
    {
        private int state; // 0 free, 1 producer, 2 ready, 3 consumer
        internal readonly bool Spike;
        internal readonly PerfProbeSampleBatch Main, Sim;
        internal long Sequence, Start, End, Id, SamplerCpu, SuspendedTicks, MaxSuspendedTicks, AccountingStart, AccountingEnd;
        internal string Name;
        internal int WindowDrops, RequestDrops;
        internal long CadenceWaits, CadenceLate, CadenceMaxLate, CadenceMissed;
        internal string CadenceMode;
        internal PerfProbeAnalysisJob(bool spike, int capacity, int frames)
        { Spike = spike; Main = new PerfProbeSampleBatch(capacity, frames) { Name = "main" }; Sim = new PerfProbeSampleBatch(capacity, frames) { Name = "sim" }; }
        internal bool TryClaim() => Interlocked.CompareExchange(ref state, 1, 0) == 0;
        internal void Publish() => Volatile.Write(ref state, 2);
        internal bool TryRead() => Interlocked.CompareExchange(ref state, 3, 2) == 2;
        internal bool Ready => Volatile.Read(ref state) == 2;
        internal void Release() => Volatile.Write(ref state, 0);
    }
}
