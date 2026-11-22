using System;
using System.Collections.Generic;
using System.Numerics;

namespace ProjectileTrailsTest
{
    internal sealed class TrailBuffer
    {
        // Vanilla publishes ordinary projectile slots in this range. Shadow render IDs
        // use a separate namespace starting at 10000.
        internal const int SlotLimit = 6000;
        internal const float MaximumMovement = 2f;
        internal const float TrailLength = 0.45f;

        internal struct Trail
        {
            internal object Identity;
            internal int Kind;
            internal long SortKey;
            internal Vector3 Head;
            internal Vector3 Tail;
            internal bool HasSegment;
        }

        private readonly Trail[] trails = new Trail[SlotLimit];
        private readonly int[] indices = new int[SlotLimit];
        private readonly List<int> slots = new List<int>(SlotLimit - 1);
        private int segmentCount;

        internal int Count => slots.Count;
        internal bool HasSegments => segmentCount != 0;
        internal Trail Get(int index) => trails[slots[index]];
#if SERP_DEVDIAG
        internal int GetSlot(int index) => slots[index];
#endif
        internal static bool IsBodySlot(int id) => id > 0 && id < SlotLimit;

        internal bool Observe(int id, object identity, int kind, long sortKey, Vector3 position)
        {
            ref Trail current = ref trails[id];
            if (current.Identity == null)
            {
                indices[id] = slots.Count;
                slots.Add(id);
            }
            Vector3 delta = position - current.Head;
            float distanceSquared = delta.LengthSquared();
            bool hasSegment = ReferenceEquals(current.Identity, identity) && current.Kind == kind &&
                delta.X * delta.X + delta.Y * delta.Y > 0.000001f &&
                distanceSquared <= MaximumMovement * MaximumMovement;
            Vector3 tail = hasSegment && distanceSquared > TrailLength * TrailLength
                ? position - delta * (TrailLength / (float)Math.Sqrt(distanceSquared))
                : current.Head;
            bool dirty = current.HasSegment || hasSegment;
            if (current.HasSegment != hasSegment) segmentCount += hasSegment ? 1 : -1;
            current = new Trail { Identity = identity, Kind = kind, SortKey = sortKey,
                Head = position, Tail = tail, HasSegment = hasSegment };
            return dirty;
        }

        internal bool Remove(int id)
        {
            if (!IsBodySlot(id) || trails[id].Identity == null) return false;
            int index = indices[id];
            int last = slots[slots.Count - 1];
            slots[index] = last;
            indices[last] = index;
            slots.RemoveAt(slots.Count - 1);
            bool dirty = trails[id].HasSegment;
            if (dirty) segmentCount--;
            trails[id] = default;
            return dirty;
        }

        internal void Clear()
        {
            foreach (int id in slots) trails[id] = default;
            slots.Clear();
            segmentCount = 0;
        }
    }

    internal sealed class BatchBudget
    {
        internal const int SegmentLimit = 1024;
        internal const int GroupLimit = 64;
        private readonly Dictionary<long, int> groups = new Dictionary<long, int>(GroupLimit);

        internal int Count { get; private set; }
        internal int GroupCount => groups.Count;
        internal static long Key(int layer, int order) => ((long)layer << 32) | (uint)order;

        internal bool TryAdd(long key, out int group)
        {
            group = -1;
            if (Count >= SegmentLimit) return false;
            if (!groups.TryGetValue(key, out group))
            {
                if (GroupCount >= GroupLimit) return false;
                group = GroupCount;
                groups.Add(key, group);
            }
            Count++;
            return true;
        }

        internal void Clear()
        {
            groups.Clear();
            Count = 0;
        }
    }
}
