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
        private Dictionary<int, List<int>> forward;
        private long forwardRevision = -1;
        public long Queries, CacheHits, ExpandedNodes;
        public long PlacementQueries, PlacementCacheHits, PlacementExpandedNodes;
        public long Revision { get; private set; }
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
            Revision++;
        }
        public bool CanReach(int source, int destination, bool placement = false)
        {
            if (placement) PlacementQueries++; else Queries++;
            if ((uint)source >= reached.Length || (uint)destination >= reached.Length) return false;
            if (source == destination) { if (placement) PlacementCacheHits++; else CacheHits++; return true; }
            if (target == destination) { if (placement) PlacementCacheHits++; else CacheHits++; }
            else
            {
                if (++generation == int.MaxValue) { Array.Clear(reached, 0, reached.Length); generation = 1; }
                target = destination;
                int head = 0, tail = 0;
                queue[tail++] = destination; reached[destination] = generation;
                while (head < tail)
                {
                    int node = queue[head++]; if (placement) PlacementExpandedNodes++; else ExpandedNodes++;
                    if (!reverse.TryGetValue(node, out var incoming)) continue;
                    foreach (var edge in incoming)
                        if (reached[edge.Key] != generation)
                        { reached[edge.Key] = generation; queue[tail++] = edge.Key; }
                }
            }
            return reached[source] == generation;
        }

        public ForwardSearch StartForwardSearch(int source)
        {
            if (forwardRevision != Revision)
            {
                forward = new Dictionary<int, List<int>>();
                foreach (var destination in reverse)
                    foreach (var incoming in destination.Value)
                    {
                        if (!forward.TryGetValue(incoming.Key, out var neighbours))
                            forward.Add(incoming.Key, neighbours = new List<int>());
                        neighbours.Add(destination.Key);
                    }
                forwardRevision = Revision;
            }
            return new ForwardSearch(this, source, reached.Length);
        }

        // Placement only. The cursor retains its allocation-free reverse query.
        // No path or predecessor data: this proves directed connectivity from an anchor.
        internal sealed class ForwardSearch
        {
            private readonly CursorRegionGraph graph;
            private readonly long revision;
            private readonly int capacity;
            private readonly HashSet<int> visited = new HashSet<int>();
            private readonly Queue<int> frontier = new Queue<int>();
            public long ExpandedNodes { get; private set; }
            internal ForwardSearch(CursorRegionGraph graph, int source, int capacity)
            {
                this.graph = graph; this.capacity = capacity; revision = graph.Revision;
                if ((uint)source < capacity) { visited.Add(source); frontier.Enqueue(source); }
            }
            public bool CanReach(int destination)
            {
                if (revision != graph.Revision || (uint)destination >= capacity) return false;
                if (visited.Contains(destination)) return true;
                while (frontier.Count != 0)
                {
                    int node = frontier.Dequeue(); ExpandedNodes++;
                    if (graph.forward.TryGetValue(node, out var neighbours))
                        foreach (int next in neighbours) if (visited.Add(next)) frontier.Enqueue(next);
                    if (visited.Contains(destination)) return true;
                }
                return false;
            }
        }
    }
}
