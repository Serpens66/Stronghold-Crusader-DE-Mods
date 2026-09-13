using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MoatMove
{
    internal sealed class FastNativeMask
    {
        private readonly int width, height;
        private readonly MoatSearchEdge edge;
        private int prepared;
        internal readonly byte[] Bytes;
        internal bool Ready => prepared == Bytes.Length;
        internal static long PreparedNodes, PreparationTicks;
        internal FastNativeMask(int width, int height, MoatSearchEdge edge)
        { this.width = width; this.height = height; this.edge = edge; Bytes = new byte[checked(width * height)]; }
        internal int Prepare(int budget)
        {
            long began = Stopwatch.GetTimestamp(); int before = prepared;
            try
            {
                while (prepared < Bytes.Length && prepared - before < budget)
                {
                    int x = prepared % width, y = prepared / width; byte mask = 0;
                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + FastNativeRouteField.Dx[d], ny = y + FastNativeRouteField.Dy[d];
                        if ((uint)nx < width && (uint)ny < height &&
                            edge(ny * width + nx, prepared, (d + 4) & 7, out _, out _)) mask |= (byte)(1 << d);
                    }
                    Bytes[prepared++] = mask;
                }
                return prepared - before;
            }
            finally { PreparedNodes += prepared - before; PreparationTicks += Stopwatch.GetTimestamp() - began; }
        }
    }

    internal sealed unsafe class FastNativeRouteField : IFastRouteField
    {
        internal static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        internal static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        private readonly FastNativeKernel kernel;
        private readonly FastNativeKernel.Layout layout;
        private readonly byte[] slab;
        private readonly Func<FastNativeMask> maskSource;
        private FastNativeMask mask;
        private int firstRoot = -1;
        private bool cancelled, maskCopied;
        private long work;
        internal static long NativeTicks, NativeCalls;

        internal FastNativeRouteField(int width, int height, MoatSearchEdge edge, Func<FastNativeMask> masks = null)
        {
            kernel = FastNativeKernel.Get(width, height); layout = kernel.Memory;
            slab = new byte[layout.Bytes];
            maskSource = masks ?? (() => new FastNativeMask(width, height, edge));
            fixed (byte* p = slab)
            {
                *(int*)(p + 4) = 1;
                for (int y = 0; y < height; y++)
                {
                    *(int*)(p + layout.Rows + y * 12) = y * width;
                    *(int*)(p + layout.Steps + y * 32) = -width;
                    *(int*)(p + layout.Steps + y * 32 + 16) = width;
                }
                for (int i = 0; i < layout.Count; i++) p[layout.Availability + i] = 1;
            }
        }
        public int Expanded { get { fixed (byte* p = slab) return *(int*)(p + 12); } }
        public long Work => work;
        public long BufferBytes => slab.LongLength;
        internal long PreparationMaskBytes => mask?.Bytes.LongLength ?? 0;
        internal void VerifyGuards()
        {
            fixed (byte* p = slab)
            {
                int* distance = (int*)(p + layout.Distance); ushort* stamp = (ushort*)(p + layout.Stamp);
                for (int i = 1; i <= layout.Guard; i++)
                    if (distance[-i] != 0 || stamp[-i] != 0 || distance[layout.Count + i - 1] != 0 || stamp[layout.Count + i - 1] != 0)
                        throw new InvalidOperationException("FastNative guard memory was written.");
                for (int y = 0; y < layout.Height; y++)
                    if (*(int*)(p + layout.Rows + y * 12) != y * layout.Width ||
                        *(int*)(p + layout.Steps + y * 32) != -layout.Width ||
                        *(int*)(p + layout.Steps + y * 32 + 16) != layout.Width)
                        throw new InvalidOperationException("FastNative read-only index table was written.");
                if (*(int*)(p + 8) < 0 || *(int*)(p + 8) > layout.Count || *(int*)(p + 12) > *(int*)(p + 8))
                    throw new InvalidOperationException("FastNative queue bounds changed.");
            }
        }
        public bool Exhausted { get { fixed (byte* p = slab) return firstRoot >= 0 && !cancelled && *(int*)(p + 8) == *(int*)(p + 12); } }
        public bool HasDiscovered(int node) => Distance(node) >= 0;
        public int Distance(int node)
        {
            if ((uint)node >= layout.Count) return -1;
            fixed (byte* p = slab) return ((int*)(p + layout.Distance))[node] - 1;
        }
        public void Reset(int target) { ResetRoots(new[] { target }); }
        public void ResetRoots(IEnumerable<int> roots)
        {
            if (roots == null) throw new ArgumentNullException(nameof(roots));
            fixed (byte* p = slab)
            {
                int* distances = (int*)(p + layout.Distance), queue = (int*)(p + layout.Queue);
                ushort* stamps = (ushort*)(p + layout.Stamp);
                int tail = *(int*)(p + 8);
                for (int i = 0; i < tail; i++) { distances[queue[i]] = 0; stamps[queue[i]] = 0; }
                *(int*)(p + 8) = *(int*)(p + 12) = *(int*)(p + 16) = 0;
                firstRoot = -1; cancelled = false; maskCopied = false;
                mask = maskSource();
                foreach (int root in roots)
                {
                    if ((uint)root >= layout.Count) throw new ArgumentOutOfRangeException(nameof(roots));
                    if (distances[root] != 0) continue;
                    if (firstRoot < 0) firstRoot = root;
                    int slot = (*(int*)(p + 8))++;
                    queue[slot] = root; ((short*)(p + layout.QueueY))[slot] = (short)(root / layout.Width);
                    distances[root] = 1; stamps[root] = 1;
                }
            }
        }
        public void Cancel() { cancelled = true; }
        public FastRouteStatus Status(int start, int maximumEdges = 2000)
        {
            if ((uint)start >= layout.Count || maximumEdges < 0 || firstRoot < 0) return FastRouteStatus.InvalidQuery;
            if (cancelled) return FastRouteStatus.Cancelled;
            int distance = Distance(start);
            if (distance >= 0) return distance > maximumEdges ? FastRouteStatus.TooLong : FastRouteStatus.Found;
            return Exhausted ? FastRouteStatus.NoRoute : FastRouteStatus.Pending;
        }
        public FastRouteStatus Advance(int start, int maximumExpanded, int maximumEdges = 2000)
        {
            if (maximumExpanded < 0) throw new ArgumentOutOfRangeException(nameof(maximumExpanded));
            FastRouteStatus status = Status(start, maximumEdges);
            if (status != FastRouteStatus.Pending || maximumExpanded == 0) return status;
            int prepared = mask.Prepare(maximumExpanded); work += prepared; maximumExpanded -= prepared;
            if (!mask.Ready || maximumExpanded == 0) return FastRouteStatus.Pending;
            if (!maskCopied) { Buffer.BlockCopy(mask.Bytes, 0, slab, layout.Mask, layout.Count); maskCopied = true; }
            long began = Stopwatch.GetTimestamp(); int before = Expanded;
            try
            {
                fixed (byte* p = slab)
                {
                    int limit = (int)Math.Min(layout.Count, (long)before + maximumExpanded);
                    kernel.Run((IntPtr)p, firstRoot % layout.Width, firstRoot / layout.Width, start % layout.Width, start / layout.Width, limit, 1);
                    *(int*)(p + 16) = *(int*)(p + 12);
                }
            }
            finally { work += Expanded - before; NativeCalls++; NativeTicks += Stopwatch.GetTimestamp() - began; }
            return Status(start, maximumEdges);
        }
        private int Next(int node, out int direction)
        {
            int distance = Distance(node), x = node % layout.Width, y = node / layout.Width;
            for (int d = 0; d < 8; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if ((uint)nx >= layout.Width || (uint)ny >= layout.Height) continue;
                int next = ny * layout.Width + nx;
                if ((slab[layout.Mask + next] & (1 << ((d + 4) & 7))) != 0 && Distance(next) == distance - 1)
                { direction = d; return next; }
            }
            throw new InvalidOperationException("FastNative predecessor chain invalid.");
        }
        public FastRouteStatus WritePacked(int start, byte[] buffer, out int count, int maximumEdges = 2000)
        {
            count = 0; FastRouteStatus status = Status(start, maximumEdges);
            if (status != FastRouteStatus.Found) return status;
            int length = Distance(start);
            if (buffer == null || buffer.Length < (length + 1) / 2) return FastRouteStatus.InvalidQuery;
            Array.Clear(buffer, 0, (length + 1) / 2);
            int node = start;
            for (int i = 0; i < length; i++)
            { node = Next(node, out int d); buffer[i >> 1] |= (byte)(d << ((i & 1) * 4)); }
            if (Distance(node) != 0) throw new InvalidOperationException("FastNative endpoint invalid.");
            count = length; return status;
        }
        public FastRouteStatus GetPath(int start, out int[] path, int maximumEdges = 2000)
        {
            path = null; FastRouteStatus status = Status(start, maximumEdges);
            if (status != FastRouteStatus.Found) return status;
            int length = Distance(start); path = new int[length + 1]; int node = start;
            for (int i = 0; i <= length; i++) { path[i] = node; if (i < length) node = Next(node, out _); }
            return status;
        }
    }
}
