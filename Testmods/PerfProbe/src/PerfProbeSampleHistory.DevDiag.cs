using System;

namespace PerfProbe
{
    // Single sampler-thread owner. Raw addresses only; no allocation or symbol lookup in Add.
    internal sealed class PerfProbeSampleHistory
    {
        private readonly ulong[] frames;
        private readonly byte[] depths;
        private readonly long[] stamps;
        private readonly int frameLimit;
        private int next, count;

        internal PerfProbeSampleHistory(int capacity, int frameLimit)
        {
            if (capacity <= 0 || frameLimit <= 0 || frameLimit > byte.MaxValue) throw new ArgumentOutOfRangeException();
            frames = new ulong[checked(capacity * frameLimit)];
            depths = new byte[capacity]; stamps = new long[capacity]; this.frameLimit = frameLimit;
        }

        internal long Oldest => count == 0 ? 0 : stamps[(next - count + stamps.Length) % stamps.Length];

        internal void Add(long stamp, ulong[] source, int offset, int depth)
        {
            depth = Math.Min(depth, frameLimit);
            Array.Copy(source, offset, frames, next * frameLimit, depth);
            stamps[next] = stamp; depths[next] = (byte)depth;
            next = (next + 1) % stamps.Length;
            count = Math.Min(count + 1, stamps.Length);
        }

        internal int Visit(long started, long ended, Action<ulong[], int, int, long> visit)
        {
            int matched = 0;
            for (int i = 0; i < count; i++)
            {
                int index = (next - count + i + stamps.Length) % stamps.Length;
                if (stamps[index] < started || stamps[index] > ended) continue;
                visit(frames, index * frameLimit, depths[index], stamps[index]); matched++;
            }
            return matched;
        }

        // Only the sampler calls this; batches are owned by a published analysis job.
        internal void CopyTo(PerfProbeSampleBatch batch, long started, long ended)
        {
            batch.Reset(); batch.Oldest = Oldest;
            for (int i = 0; i < count; i++)
            {
                int index = (next - count + i + stamps.Length) % stamps.Length;
                if (stamps[index] <= started || stamps[index] > ended) continue;
                batch.Add(frames, index * frameLimit, depths[index], stamps[index]);
            }
        }
    }
}
