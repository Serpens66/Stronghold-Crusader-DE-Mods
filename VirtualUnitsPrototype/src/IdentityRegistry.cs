using System.Collections.Generic;

namespace VirtualUnitsPrototype
{
    internal sealed class IdentityRegistry<T>
    {
        private readonly Dictionary<SlotKey, Entry> entries = new Dictionary<SlotKey, Entry>();
        public int Count => entries.Count;
        public IEnumerable<T> Values { get { foreach (Entry entry in entries.Values) yield return entry.Value; } }

        public void Set(byte kind, int gameId, uint globalId, T value) => entries[new SlotKey(kind, gameId)] = new Entry(globalId, value);
        public bool TryGet(byte kind, int gameId, uint globalId, out T value)
        {
            if (entries.TryGetValue(new SlotKey(kind, gameId), out Entry entry) && entry.GlobalId == globalId)
            { value = entry.Value; return true; }
            value = default(T); return false;
        }
        public bool TryGetSlot(byte kind, int gameId, out uint globalId, out T value)
        {
            if (entries.TryGetValue(new SlotKey(kind, gameId), out Entry entry))
            { globalId = entry.GlobalId; value = entry.Value; return true; }
            globalId = 0; value = default(T); return false;
        }
        public bool Remove(byte kind, int gameId) => entries.Remove(new SlotKey(kind, gameId));
        public void Clear() => entries.Clear();

        private readonly struct SlotKey
        {
            public SlotKey(byte kind, int gameId) { Kind = kind; GameId = gameId; }
            private byte Kind { get; }
            private int GameId { get; }
            public override bool Equals(object obj) => obj is SlotKey other && other.Kind == Kind && other.GameId == GameId;
            public override int GetHashCode() => (Kind * 397) ^ GameId;
        }
        private readonly struct Entry
        {
            public Entry(uint globalId, T value) { GlobalId = globalId; Value = value; }
            public uint GlobalId { get; }
            public T Value { get; }
        }
    }
}
