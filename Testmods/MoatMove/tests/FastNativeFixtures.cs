using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace MoatMove
{
    public static class FastNativeFixtures
    {
        public static void Initialize()
        {
            const string dll = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
            byte[] source = File.ReadAllBytes(dll);
            if (Convert.ToHexString(SHA256.HashData(source)) != "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2") throw new Exception("Native hash mismatch.");
            using (var pe = new PEReader(new MemoryStream(source)))
                FastNativeKernel.Initialize(pe.GetSectionData(FastNativeKernel.SourceRva).GetContent(0, FastNativeKernel.SourceLength).ToArray(),
                    unchecked((ulong)Process.GetCurrentProcess().MainModule.BaseAddress.ToInt64()));
        }
        public static void Run(string root)
        {
            Initialize();
            for (int seed = 1; seed <= 12; seed++) RandomGraph(seed);
            LongPath();
            CompareBackends();
            string folder = Path.Combine(root, "_inspect/MoatMove");
            File.WriteAllBytes(Path.Combine(folder, "fast-native-kernel.bin"), FastNativeKernel.Get(800, 800).CodeBytes);
            Console.WriteLine("PASS FastNative: actual relocated DA590 execution, independent directed BFS, slices, multi-roots, 1/100/680 starts, long paths, guard edges, interleaved fields and packed reconstruction.");
            Console.WriteLine($"FastNative measurement: preparationNodes={FastNativeMask.PreparedNodes} preparationMs={FastNativeMask.PreparationTicks * 1000.0 / Stopwatch.Frequency:F3} nativeCalls={FastNativeRouteField.NativeCalls} nativeMs={FastNativeRouteField.NativeTicks * 1000.0 / Stopwatch.Frequency:F3}");
        }

        private static void RandomGraph(int seed)
        {
            const int width = 32, height = 29; int count = width * height;
            var random = new Random(seed); var masks = new byte[count]; random.NextBytes(masks);
            MoatSearchEdge edge = (int a, int b, int d, out bool wet, out bool structure) =>
            { wet = structure = false; return (masks[a] & (1 << d)) != 0; };
            int[] roots = seed % 2 == 0 ? new[] { 0, count - 1, count / 2 } : new[] { count / 2 };
            int[] expected = Reference(width, height, masks, roots);
            var field = new FastNativeRouteField(width, height, edge);
            var other = new FastNativeRouteField(width, height, edge);
            field.ResetRoots(roots); other.Reset(0);
            int slice = seed % 3 == 0 ? 1 : seed % 3 == 1 ? 17 : int.MaxValue;
            int[] starts = new int[count]; for (int i = 0; i < count; i++) starts[i] = i;
            foreach (int start in starts)
            {
                int attempts = 0;
                while (field.Status(start, int.MaxValue) == FastRouteStatus.Pending)
                {
                    long before = field.Work; int expanded = field.Expanded;
                    field.Advance(start, slice, int.MaxValue);
                    if (field.Work - before > slice || field.Expanded - expanded > slice) throw new Exception("Slice exceeded.");
                    other.Advance(count - 1, 7, int.MaxValue);
                    if (++attempts > count * 3) throw new Exception("Field did not resume.");
                }
                if (field.Distance(start) != expected[start]) throw new Exception($"Distance seed={seed} start={start}: {field.Distance(start)} != {expected[start]}");
                field.VerifyGuards();
                if (expected[start] < 0) continue;
                byte[] packed = new byte[1000];
                if (field.WritePacked(start, packed, out int length) != FastRouteStatus.Found || length != expected[start]) throw new Exception("Packed length.");
                int node = start;
                for (int i = 0; i < length; i++)
                {
                    int d = (packed[i / 2] >> ((i & 1) * 4)) & 15;
                    if (d > 7 || (masks[node] & (1 << d)) == 0) throw new Exception("Invalid packed edge.");
                    node += FastNativeRouteField.Dy[d] * width + FastNativeRouteField.Dx[d];
                }
                if (Array.IndexOf(roots, node) < 0) throw new Exception("Wrong root.");
            }
            int built = field.Expanded;
            foreach (int group in new[] { 1, 100, 680 })
                for (int i = 0; i < group; i++) field.Advance(i, 1024);
            if (field.Expanded != built) throw new Exception("Group repeated a completed search.");
            field.Cancel(); if (field.Status(0) != FastRouteStatus.Cancelled) throw new Exception("Cancellation.");
            field.Reset(0); if (field.Distance(0) != 0 || field.Expanded != 0) throw new Exception("Reset.");
        }

        private static int[] Reference(int width, int height, byte[] masks, int[] roots)
        {
            int[] distance = new int[masks.Length]; Array.Fill(distance, -1); var queue = new Queue<int>();
            foreach (int root in roots) { if (distance[root] == 0) continue; distance[root] = 0; queue.Enqueue(root); }
            while (queue.Count > 0)
            {
                int target = queue.Dequeue();
                for (int direction = 0; direction < 8; direction++)
                {
                    int x = target % width - FastNativeRouteField.Dx[direction], y = target / width - FastNativeRouteField.Dy[direction];
                    if (x < 0 || y < 0 || x >= width || y >= height) continue;
                    int source = y * width + x;
                    if (distance[source] >= 0 || (masks[source] & (1 << direction)) == 0) continue;
                    distance[source] = distance[target] + 1; queue.Enqueue(source);
                }
            }
            return distance;
        }

        private static void LongPath()
        {
            const int width = 800, height = 800; var masks = new byte[width * height];
            // Directed snake exceeds both the packed limit and the old ushort distance.
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int node = y * width + x;
                if ((y & 1) == 0) { if (x + 1 < width) masks[node] = 4; else if (y + 1 < height) masks[node] = 16; }
                else { if (x > 0) masks[node] = 64; else if (y + 1 < height) masks[node] = 16; }
            }
            MoatSearchEdge edge = (int a, int b, int d, out bool wet, out bool structure) =>
            { wet = structure = false; return (masks[a] & (1 << d)) != 0; };
            var field = new FastNativeRouteField(width, height, edge); field.Reset((height - 1) * width);
            while (field.Advance(0, 1024, int.MaxValue) == FastRouteStatus.Pending) { }
            if (field.Distance(0) != width * height - 1 || field.Status(0) != FastRouteStatus.TooLong) throw new Exception("Long ground route lost.");
            Console.WriteLine($"Native field bytes={field.BufferBytes}; long distance={field.Distance(0)}");
            field.VerifyGuards();
        }

        private static void CompareBackends()
        {
            const int width = 160, height = 160;
            MoatSearchEdge edge = (int a, int b, int d, out bool wet, out bool structure) =>
            { wet = structure = false; return true; };
            foreach (int size in new[] { 1, 100, 680 })
            {
                foreach (bool native in new[] { false, true })
                {
                    IFastRouteField field = native ? (IFastRouteField)new FastNativeRouteField(width, height, edge) : new FastRouteField(width, height, edge);
                    field.Reset(width * height - 1);
                    long began = Stopwatch.GetTimestamp();
                    for (int unit = 0; unit < size; unit++)
                    {
                        int start = unit;
                        if (field.Advance(start, int.MaxValue) != FastRouteStatus.Found) throw new Exception("Comparison route failed.");
                        field.WritePacked(start, new byte[1000], out _);
                    }
                    Console.WriteLine($"FAST BACKEND native={native} units={size} totalMs={(Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency:F3} expanded={field.Expanded} work={field.Work} fieldBytes={field.BufferBytes}");
                }
            }
        }
    }
}
