using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    // One synchronous placement batch. This enumerates places, never publishes paths.
    // A candidate may be discovered against a directed edge; source->candidate is
    // checked separately, so discovery cannot grant travel in the wrong direction.
    internal sealed class MoatPlacementSearch
    {
        private readonly int width, height;
        private readonly Func<int, int, bool> connected;
        private readonly List<int> cells = new List<int>();
        private readonly HashSet<int> discovered = new HashSet<int>();
        private readonly Dictionary<int, int> reservations = new Dictionary<int, int>();
        private readonly Dictionary<long, bool> decisions = new Dictionary<long, bool>();
        private int expanded;
        public long ExpandedNodes { get; private set; }
        public long ReachabilityChecks { get; private set; }
        public long CacheHits { get; private set; }

        public MoatPlacementSearch(int width, int height, int anchor, Func<int, int, bool> connected)
        {
            if (width <= 0 || height <= 0 || (uint)anchor >= (long)width * height)
                throw new ArgumentOutOfRangeException(nameof(anchor));
            this.width = width; this.height = height; this.connected = connected;
            cells.Add(anchor); discovered.Add(anchor);
        }

        public bool TryReserve(int unit, int source, Func<int, bool> available,
            Func<int, bool> reachable, out int cell)
        {
            for (int index = 0; ; index++)
            {
                while (index >= cells.Count && Expand()) { }
                if (index >= cells.Count) break;
                int candidate = cells[index];
                if (reservations.ContainsKey(candidate) || !available(candidate)) continue;
                long key = ((long)(uint)source << 32) | (uint)candidate;
                if (!decisions.TryGetValue(key, out bool valid))
                { ReachabilityChecks++; decisions[key] = valid = reachable(candidate); }
                else CacheHits++;
                if (!valid) continue;
                reservations.Add(candidate, unit); cell = candidate; return true;
            }
            cell = -1; return false;
        }

        public void Release(int unit, int cell)
        {
            if (reservations.TryGetValue(cell, out int owner) && owner == unit)
                reservations.Remove(cell);
        }

        private bool Expand()
        {
            if (expanded == cells.Count) return false;
            int cell = cells[expanded++], x = cell % width, y = cell / width;
            ExpandedNodes++;
            // Fixed clockwise order, shared by host/client. Breadth first means
            // minimum placement steps from the click, including obstacles.
            for (int d = 0; d < 8; d++)
            {
                int nx = x + WeightedMoatRoutePlanner.DirectionX[d];
                int ny = y + WeightedMoatRoutePlanner.DirectionY[d];
                if ((uint)nx >= width || (uint)ny >= height) continue;
                int next = ny * width + nx;
                if (!discovered.Contains(next) && (connected(cell, next) || connected(next, cell)))
                { discovered.Add(next); cells.Add(next); }
            }
            return true;
        }
    }
}
