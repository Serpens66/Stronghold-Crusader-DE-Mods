using System;
using System.Numerics;

namespace ProjectileTrailsTest
{
    internal sealed class TrailBuffer
    {
        internal const int WideProjectileLimit = 6000;
        internal const int Lifetime = 10;
        // Ten live sweeps and one sweep the producer can write during a snapshot.
        internal const int CaptureCapacity = 1 << 17;

        internal struct Sample
        {
            internal int Appearance;
            internal long SortKey;
            internal int ObjectLayer, SourceSlot;
            internal Vector3 Position;
            internal uint Tick;
            internal int Visibility;
#if SERP_DEVDIAG
            internal int TileX, TileY, SubX, SubY, File, Frame, Height;
#endif
        }

        private Sample[] samples = Array.Empty<Sample>();
        private int start;
        internal int Count { get; private set; }

        internal ref Sample Get(int index) => ref samples[(start + index) & (samples.Length - 1)];

        internal void Append(Sample sample)
        {
            if (Count == samples.Length)
            {
                var grown = new Sample[samples.Length == 0 ? 256 : checked(samples.Length * 2)];
                for (int i = 0; i < Count; i++) grown[i] = Get(i);
                samples = grown;
                start = 0;
            }
            samples[(start + Count) & (samples.Length - 1)] = sample;
            Count++;
        }

        internal bool Expire(uint tick)
        {
            int previous = Count;
            while (Count > 0)
            {
                uint age = unchecked(tick - samples[start].Tick);
                if (age < Lifetime || age > int.MaxValue) break;
                start = (start + 1) & (samples.Length - 1);
                Count--;
            }
            return Count != previous;
        }

        internal void Clear()
        {
            start = 0;
            Count = 0;
        }

        internal static long Key(int layer, int order) => ((long)layer << 32) | (uint)order;

        internal static float Opacity(uint age)
        {
            if (age >= Lifetime) return 0;
            uint stage = Math.Min(age / 3, 2u);
            uint transparency = stage * 32 / 3;
            return (32 - transparency) / 32f;
        }
    }
}
