using System;
using System.Collections.Generic;

namespace MoatMove
{
    internal delegate bool FastLiveTraversal(int from, int to, int direction, bool ground,
        out bool wet, out bool structure);
    // True when the GroundOnly answer of an edge equals its FriendlyOnly answer (no completed
    // moat on either endpoint or, for diagonals, on either corner tile).
    internal delegate bool FastGroundEqualsFriendly(int from, int to, int direction);
    // A word another cache already holds for the same predicate (first read of a node only).
    internal delegate bool FastWordSeed(int node, out uint word);
    // True when a node's complete word is the same for every player (see FastSharedWords).
    internal delegate bool FastPlayerNeutral(int node);

    // Words shared by the per-player caches of one map: a player-neutral node is evaluated once
    // and reused by the other players until a mark of any sharing cache invalidates it. Same
    // validity as each cache's own words (last value read, refreshed after every mark).
    internal sealed class FastSharedWords
    {
        private readonly uint[] words; private readonly byte[] valid;
        internal FastSharedWords(int count) { words = new uint[count]; valid = new byte[count]; }
        internal long Reuses { get; private set; }
        internal bool TryGet(int node, out uint word)
        {
            if (valid[node] != 0) { word = words[node]; Reuses++; return true; }
            word = 0; return false;
        }
        internal void Set(int node, uint word) { words[node] = word; valid[node] = 1; }
        internal void Invalidate(int node) { if ((uint)node < valid.Length) valid[node] = 0; }
    }

    // Only logical walkability is retained. Native search stamps, unit positions and
    // transient search buffers are never dependencies of this cache.
    internal sealed class FastTraversalCache
    {
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        private readonly int width, height;
        private readonly FastLiveTraversal live;
        private readonly FastGroundEqualsFriendly sameGround;
        private readonly FastSharedWords shared;
        private readonly FastPlayerNeutral neutral;
        private readonly uint[] words;
        private readonly byte[] known;
        private readonly List<int> knownNodes = new List<int>();
        // Dirty nodes: a flag per node and an insertion list, re-read in marking order (deterministic;
        // no consumer depends on node order; the sort was a visible part of a 113 ms tile-burst tick, pf-iter-8).
        // Bursts of tile reports mark the same nodes many times; a
        // sorted set paid a tree insert and an allocation per mark (2.2 s stall, 29.10.2026).
        private readonly byte[] dirtyFlags;
        private readonly List<int> dirty = new List<int>();
        private bool allDirty;
        internal FastTraversalCache(int width, int height, FastLiveTraversal live, FastGroundEqualsFriendly sameGround = null,
            FastSharedWords shared = null, FastPlayerNeutral neutral = null)
        {
            this.width = width; this.height = height; this.live = live; this.sameGround = sameGround;
            this.shared = neutral != null ? shared : null; this.neutral = neutral;
            words = new uint[checked(width * height)]; known = new byte[words.Length]; dirtyFlags = new byte[words.Length];
        }
        internal long Revision { get; private set; }
        internal long EdgeEvaluations { get; private set; }
        internal long Seeded { get; private set; }
        // Optional source for a node's first word; refreshes of marked nodes always read live.
        internal FastWordSeed Seed { get; set; }

        // The current word of a node this cache holds and has not marked for a re-read.
        internal bool TryGetClean(int node, out uint word)
        {
            word = 0;
            if ((uint)node >= words.Length || known[node] == 0 || allDirty || dirtyFlags[node] != 0) return false;
            word = words[node]; return true;
        }

        internal bool Edge(int from, int to, int direction, bool ground, out bool wet, out bool structure)
        {
            wet = structure = false;
            if ((uint)from >= words.Length || (uint)to >= words.Length || (uint)direction >= 8) return false;
            if (known[from] == 0)
            {
                FastWordSeed seed = Seed;
                if (seed != null && seed(from, out uint seeded)) { words[from] = seeded; Seeded++; } else words[from] = Read(from);
                known[from] = 1; knownNodes.Add(from);
            }
            uint word = words[from];
            wet = (word & (1U << (16 + direction))) != 0;
            structure = (word & (1U << (24 + direction))) != 0;
            return (word & (1U << ((ground ? 8 : 0) + direction))) != 0;
        }

        // Complete cached word of a node (anchor-mode mask export); read once like Edge().
        internal uint Word(int node)
        {
            if ((uint)node >= words.Length) return 0;
            if (known[node] == 0) { words[node] = ReadShared(node); known[node] = 1; knownNodes.Add(node); }
            return words[node];
        }

        // A player-neutral node takes another player's current word; otherwise (and for the first
        // reader) the live evaluation, which then serves the other players.
        private uint ReadShared(int node)
        {
            if (shared == null || !neutral(node)) return Read(node);
            if (shared.TryGet(node, out uint word)) return word;
            word = Read(node); shared.Set(node, word); return word;
        }

        // Takes a word evaluated live for another player (player-neutral node, same sweep step).
        internal uint Adopt(int node, uint word)
        {
            if ((uint)node >= words.Length) return 0;
            words[node] = word;
            if (known[node] == 0) { known[node] = 1; knownNodes.Add(node); }
            return word;
        }

        // Fresh live word of one node (anchor-mode chunked resweep after access changes).
        internal uint Reread(int node)
        {
            if ((uint)node >= words.Length) return 0;
            // A resweep always evaluates live (it looks for unreported changes) and shares the result.
            words[node] = Read(node);
            if (shared != null && neutral(node)) shared.Set(node, words[node]);
            if (known[node] == 0) { known[node] = 1; knownNodes.Add(node); }
            return words[node];
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
                // Away from moats an edge allowed for the friendly policy is allowed on the ground
                // too; a rejected one is evaluated again (ground edges skip moat mode's height rule).
                bool same = friendly && sameGround != null && sameGround(from, to, direction);
                bool ground = same || live(from, to, direction, true, out _, out _);
                EdgeEvaluations += same ? 1 : 2;
                if (friendly) word |= 1U << direction;
                if (ground) word |= 1U << (8 + direction);
                if (wet) word |= 1U << (16 + direction);
                if (structure) word |= 1U << (24 + direction);
            }
            return word;
        }

        internal void Mark(int node)
        {
            if ((uint)node >= words.Length || known[node] == 0) return;
            shared?.Invalidate(node);
            if (dirtyFlags[node] == 0) { dirtyFlags[node] = 1; dirty.Add(node); }
        }
        internal void MarkAll() { allDirty = true; }

        internal int DirtyCount => dirty.Count;

        // maximum > 0 re-reads at most that many marked nodes (earliest marked first); the others stay marked
        // for the next call. A full mark (MarkAll) is always processed completely.
        internal IReadOnlyCollection<int> Refresh(int maximum = 0)
        {
            if (!allDirty && dirty.Count == 0) return Array.Empty<int>();
            var changed = new List<int>();
            List<int> candidates = allDirty ? knownNodes : dirty;
            int count = allDirty || maximum <= 0 ? candidates.Count : Math.Min(maximum, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                int node = candidates[i];
                uint updated = allDirty ? Read(node) : ReadShared(node);
                if (updated != words[node]) { words[node] = updated; changed.Add(node); }
            }
            if (allDirty || count == dirty.Count)
            {
                foreach (int node in dirty) dirtyFlags[node] = 0;
                dirty.Clear(); allDirty = false;
            }
            else
            {
                for (int i = 0; i < count; i++) dirtyFlags[dirty[i]] = 0;
                dirty.RemoveRange(0, count);
            }
            if (changed.Count != 0) Revision++;
            return changed;
        }
    }
}
