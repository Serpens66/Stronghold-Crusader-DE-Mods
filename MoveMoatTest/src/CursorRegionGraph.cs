using System;
using System.Collections.Generic;

namespace MoveMoatTest
{
    // Connectivity only: no costs, parents, encoded paths, or unit identities.
    // Edge reference counts allow a changed boundary tile to remove its contribution
    // without deleting another tile's connection between the same two regions.
    internal sealed class CursorRegionGraph
    {
        private readonly Dictionary<int, Dictionary<int, int>> reverse = new Dictionary<int, Dictionary<int, int>>();
        private readonly int[] reached, queue;
        private int generation, target = -1;
        public long Queries, CacheHits, ExpandedNodes;
        public CursorRegionGraph(int nodeCount)
        {
            reached = new int[nodeCount]; queue = new int[nodeCount];
        }
        public void ChangeEdge(int from, int to, int delta)
        {
            if (from < 0 || to < 0 || from == to) return;
            if (!reverse.TryGetValue(to, out var incoming))
            {
                if (delta < 0) throw new InvalidOperationException("Missing connectivity edge");
                reverse.Add(to, incoming = new Dictionary<int, int>());
            }
            incoming.TryGetValue(from, out int count);
            count += delta;
            if (count < 0) throw new InvalidOperationException("Negative connectivity edge count");
            if (count == 0) incoming.Remove(from); else incoming[from] = count;
            target = -1;
        }
        public bool CanReach(int source, int destination)
        {
            Queries++;
            if ((uint)source >= reached.Length || (uint)destination >= reached.Length) return false;
            if (source == destination) { CacheHits++; return true; }
            if (target == destination) CacheHits++;
            else
            {
                if (++generation == int.MaxValue) { Array.Clear(reached, 0, reached.Length); generation = 1; }
                target = destination;
                int head = 0, tail = 0;
                queue[tail++] = destination; reached[destination] = generation;
                while (head < tail)
                {
                    int node = queue[head++]; ExpandedNodes++;
                    if (!reverse.TryGetValue(node, out var incoming)) continue;
                    foreach (var edge in incoming)
                        if (reached[edge.Key] != generation)
                        { reached[edge.Key] = generation; queue[tail++] = edge.Key; }
                }
            }
            return reached[source] == generation;
        }
    }
}
