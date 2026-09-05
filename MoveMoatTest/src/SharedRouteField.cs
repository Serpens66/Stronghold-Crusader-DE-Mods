using System;
using System.Collections.Generic;

namespace MoveMoatTest
{
    // Immutable geometry. Reverse edges are evaluated in their original direction.
    internal sealed class SharedRouteField
    {
        internal const int Radius = 12;
        private readonly int[] route;
        private readonly Field entry, exit;
        internal long Expanded => entry.Expanded + exit.Expanded;
        internal SharedRouteField(int width, int[] route, Func<int, int, long> edge)
        {
            this.route = (int[])route.Clone();
            var prefix = new long[route.Length];
            for (int i = 1; i < route.Length; i++)
            {
                long cost = edge(route[i - 1], route[i]);
                if (cost <= 0) throw new ArgumentException("Invalid main route");
                prefix[i] = checked(prefix[i - 1] + cost);
            }
            entry = new Field(width, route[0], route, prefix, true, edge);
            exit = new Field(width, route[route.Length - 1], route, prefix, false, edge);
        }
        internal bool TryConnect(int start, int target, out int[] nodes)
        {
            nodes = null;
            if (!entry.TryTrace(start, out List<int> first, out int begin) ||
                !exit.TryTrace(target, out List<int> last, out int end) || begin > end) return false;
            int length = first.Count + end - begin + last.Count - 1;
            if (length < 2 || length > 2001) return false;
            var result = new int[length];
            int at = 0;
            foreach (int n in first) result[at++] = n;
            for (int i = begin + 1; i <= end; i++) result[at++] = route[i];
            for (int i = last.Count - 2; i >= 0; i--) result[at++] = last[i];
            var seen = new HashSet<int>();
            foreach (int n in result) if (!seen.Add(n)) return false;
            nodes = result;
            return true;
        }
        private sealed class Field
        {
            private const int Side = Radius * 2 + 1;
            private readonly int width, x0, y0;
            private readonly long[] distances = new long[Side * Side];
            private readonly int[] parent = new int[Side * Side], anchor = new int[Side * Side];
            internal long Expanded;
            private readonly List<Item> heap = new List<Item>();
            private struct Item { internal int Node; internal long Cost; }
            internal Field(int width, int center, int[] route, long[] prefix, bool reverse, Func<int, int, long> edge)
            {
                this.width = width; x0 = center % width - Radius; y0 = center / width - Radius;
                for (int i = 0; i < distances.Length; i++) { distances[i] = long.MaxValue; parent[i] = -1; anchor[i] = -1; }
                for (int i = 0; i < route.Length; i++)
                {
                    int n = Local(route[i]);
                    if (n < 0) continue;
                    long cost = reverse ? prefix[prefix.Length - 1] - prefix[i] : prefix[i];
                    if (cost >= distances[n]) continue;
                    distances[n] = cost; anchor[n] = i; Push(n, cost);
                }
                while (heap.Count != 0)
                {
                    Item current = Pop();
                    if (current.Cost != distances[current.Node]) continue;
                    Expanded++;
                    int world = World(current.Node), x = world % width, y = world / width;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || (uint)(x + dx) >= width || (uint)(y + dy) >= width) continue;
                        int to = world + dx + dy * width, next = Local(to);
                        if (next < 0) continue;
                        long step = reverse ? edge(to, world) : edge(world, to);
                        if (step <= 0 || current.Cost > long.MaxValue - step) continue;
                        long cost = current.Cost + step;
                        if (cost >= distances[next]) continue;
                        distances[next] = cost; parent[next] = current.Node; anchor[next] = anchor[current.Node]; Push(next, cost);
                    }
                }
            }
            private int Local(int world)
            {
                if (world < 0 || world >= width * width) return -1;
                int x = world % width - x0, y = world / width - y0;
                return (uint)x < Side && (uint)y < Side ? x + y * Side : -1;
            }
            private int World(int local) => x0 + local % Side + (y0 + local / Side) * width;
            internal bool TryTrace(int world, out List<int> path, out int index)
            {
                path = null; index = -1;
                int n = Local(world);
                if (n < 0 || distances[n] == long.MaxValue) return false;
                index = anchor[n]; path = new List<int>();
                for (int remaining = Side * Side; n >= 0 && remaining > 0; remaining--)
                { path.Add(World(n)); n = parent[n]; }
                return n < 0;
            }
            private static bool Before(Item a, Item b) => a.Cost < b.Cost || (a.Cost == b.Cost && a.Node < b.Node);
            private void Push(int node, long cost)
            {
                var item = new Item { Node = node, Cost = cost }; int i = heap.Count; heap.Add(item);
                while (i > 0) { int p = (i - 1) / 2; if (!Before(item, heap[p])) break; heap[i] = heap[p]; i = p; }
                heap[i] = item;
            }
            private Item Pop()
            {
                Item result = heap[0], last = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
                if (heap.Count == 0) return result;
                int i = 0;
                while (i * 2 + 1 < heap.Count)
                { int c = i * 2 + 1; if (c + 1 < heap.Count && Before(heap[c + 1], heap[c])) c++;
                  if (!Before(heap[c], last)) break; heap[i] = heap[c]; i = c; }
                heap[i] = last; return result;
            }
        }
    }
}
