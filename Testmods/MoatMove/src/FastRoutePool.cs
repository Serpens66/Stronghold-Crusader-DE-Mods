using System;
using System.Collections.Generic;

namespace MoatMove
{
    internal readonly struct FastFieldKey : IEquatable<FastFieldKey>
    {
        internal FastFieldKey(int player, int target, bool ground, bool reserved = false)
        { Player = player; Target = target; Ground = ground; Reserved = reserved; }
        internal int Player { get; }
        internal int Target { get; }
        internal bool Ground { get; }
        internal bool Reserved { get; }
        public bool Equals(FastFieldKey other) => Player == other.Player && Target == other.Target &&
            Ground == other.Ground && Reserved == other.Reserved;
        public override bool Equals(object value) => value is FastFieldKey key && Equals(key);
        public override int GetHashCode() => unchecked(((Player * 397 ^ Target) * 397 ^ (Ground ? 1 : 0)) * 397 ^ (Reserved ? 1 : 0));
    }

    internal sealed class FastFieldLease : IDisposable
    {
        private readonly FastRoutePool owner;
        internal FastRoutePool.Entry Entry;
        internal FastFieldLease(FastRoutePool owner, FastRoutePool.Entry entry)
        { this.owner = owner; Entry = entry; entry.Pins++; }
        internal FastRouteField Field => Entry?.Field;
        public void Dispose()
        { if (Entry != null) { owner.Release(Entry); Entry = null; } }
    }

    // Eviction is permitted only for unowned results. Pending command frontiers stay
    // pinned. Selection uses a deterministic access sequence, never wall-clock LRU.
    internal sealed class FastRoutePool
    {
        internal sealed class Entry
        {
            internal FastFieldKey Key;
            internal FastRouteField Field;
            internal MoatSearchEdge Resolve;
            internal int Pins;
            internal long Use;
        }
        private readonly int width, height, capacity;
        private readonly Func<FastFieldKey, MoatSearchEdge> edges;
        private readonly List<Entry> entries = new List<Entry>();
        private long use;
        internal FastRoutePool(int width, int height, long budget, Func<FastFieldKey, MoatSearchEdge> edges)
        {
            if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(width));
            this.width = width; this.height = height; this.edges = edges ?? throw new ArgumentNullException(nameof(edges));
            capacity = (int)Math.Min(int.MaxValue, budget / ((long)width * height * 9));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(budget));
        }
        internal long BufferBytes => (long)entries.Count * width * height * 9;
        internal long Builds { get; private set; }
        internal long Hits { get; private set; }
        internal long Invalidations { get; private set; }

        internal FastFieldLease Acquire(FastFieldKey key)
        {
            Entry oldest = null;
            foreach (Entry entry in entries)
            {
                if (entry.Key.Equals(key))
                { Hits++; entry.Use = ++use; return new FastFieldLease(this, entry); }
                if (entry.Pins == 0 && (oldest == null || entry.Use < oldest.Use)) oldest = entry;
            }
            if (entries.Count < capacity)
            {
                oldest = new Entry();
                // The delegate closes over its pooled entry, not a previous request key.
                Entry captured = oldest;
                oldest.Field = new FastRouteField(width, height,
                    (int a, int b, int direction, out bool wet, out bool structure) =>
                        captured.Resolve(a, b, direction, out wet, out structure));
                entries.Add(oldest);
            }
            if (oldest == null) return null;
            oldest.Key = key; oldest.Resolve = edges(key); oldest.Use = ++use; oldest.Field.Reset(key.Target); Builds++;
            return new FastFieldLease(this, oldest);
        }

        internal void Release(Entry entry)
        { if (entry.Pins <= 0) throw new InvalidOperationException("Fast lease released twice."); entry.Pins--; }

        internal void Invalidate(int player, IReadOnlyCollection<int> changed)
        {
            foreach (Entry entry in entries)
            {
                if (entry.Key.Player != player) continue;
                bool affected = false;
                foreach (int node in changed)
                {
                    // A reverse expansion reads incoming edges from neighboring sources,
                    // including rejected sources. Include those blocked frontier reads.
                    int x = node % width, y = node / width;
                    for (int dy = -1; dy <= 1 && !affected; dy++)
                        for (int dx = -1; dx <= 1 && !affected; dx++)
                            if ((uint)(x + dx) < width && (uint)(y + dy) < height)
                                affected = entry.Field.HasDiscovered((y + dy) * width + x + dx);
                    if (affected) break;
                }
                if (affected) { entry.Field.Reset(entry.Key.Target); Invalidations++; }
            }
        }

        internal void Clear()
        {
            foreach (Entry entry in entries)
            {
                if (entry.Pins != 0) throw new InvalidOperationException("Owned Fast field cleared.");
                entry.Field.Cancel();
            }
            entries.Clear(); use = 0;
        }
    }
}
