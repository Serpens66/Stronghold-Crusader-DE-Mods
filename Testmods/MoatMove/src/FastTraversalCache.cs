using System;
using System.Collections.Generic;

namespace MoatMove
{
    internal delegate bool FastLiveTraversal(int from, int to, int direction, bool ground,
        out bool wet, out bool structure);

    // Only logical walkability is retained. Native search stamps, unit positions and
    // transient search buffers are never dependencies of this cache.
    internal sealed class FastTraversalCache
    {
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        private readonly int width, height;
        private readonly FastLiveTraversal live;
        private readonly uint[] words;
        private readonly byte[] known;
        private readonly List<int> knownNodes = new List<int>();
        private readonly SortedSet<int> dirty = new SortedSet<int>();
        private bool allDirty;
        internal FastTraversalCache(int width, int height, FastLiveTraversal live)
        {
            this.width = width; this.height = height; this.live = live;
            words = new uint[checked(width * height)]; known = new byte[words.Length];
        }
        internal long Revision { get; private set; }
        internal long EdgeEvaluations { get; private set; }

        internal bool Edge(int from, int to, int direction, bool ground, out bool wet, out bool structure)
        {
            wet = structure = false;
            if ((uint)from >= words.Length || (uint)to >= words.Length || (uint)direction >= 8) return false;
            if (known[from] == 0)
            { words[from] = Read(from); known[from] = 1; knownNodes.Add(from); }
            uint word = words[from];
            wet = (word & (1U << (16 + direction))) != 0;
            structure = (word & (1U << (24 + direction))) != 0;
            return (word & (1U << ((ground ? 8 : 0) + direction))) != 0;
        }

        private uint Read(int from)
        {
            uint word = 0; int x = from % width, y = from / width;
            for (int direction = 0; direction < 8; direction++)
            {
                int nx = x + Dx[direction], ny = y + Dy[direction];
                if ((uint)nx >= width || (uint)ny >= height) continue;
                int to = ny * width + nx;
                bool friendly = live(from, to, direction, false, out bool wet, out bool structure);
                bool ground = live(from, to, direction, true, out _, out _);
                EdgeEvaluations += 2;
                if (friendly) word |= 1U << direction;
                if (ground) word |= 1U << (8 + direction);
                if (wet) word |= 1U << (16 + direction);
                if (structure) word |= 1U << (24 + direction);
            }
            return word;
        }

        internal void Mark(int node)
        { if ((uint)node < words.Length && known[node] != 0) dirty.Add(node); }
        internal void MarkAll() { allDirty = true; }

        internal IReadOnlyCollection<int> Refresh()
        {
            if (!allDirty && dirty.Count == 0) return Array.Empty<int>();
            var changed = new List<int>();
            IEnumerable<int> candidates = allDirty ? (IEnumerable<int>)knownNodes : dirty;
            foreach (int node in candidates)
            {
                uint updated = Read(node);
                if (updated != words[node]) { words[node] = updated; changed.Add(node); }
            }
            dirty.Clear(); allDirty = false;
            if (changed.Count != 0) Revision++;
            return changed;
        }
    }
}
