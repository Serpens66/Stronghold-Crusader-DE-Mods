using System.Collections.Generic;

namespace OutpostTest
{
    internal sealed class OutpostSchedule
    {
        internal const int Interval = 200, WaveSize = 5;
        internal sealed class Entry
        {
            internal uint Global;
            internal int Owner, Type, NextTick, SeenTick;
            internal bool Adopted;
        }
        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        internal Entry Observe(int id, uint global, int owner, int type, int tick)
        {
            if (!entries.TryGetValue(id, out var e) || e.Global != global || e.Owner != owner || e.Type != type)
                entries[id] = e = new Entry { Global = global, Owner = owner, Type = type, NextTick = unchecked(tick + Interval) };
            e.SeenTick = tick;
            return e;
        }
        internal static bool TakeWave(Entry e, int tick)
        {
            if (unchecked(tick - e.NextTick) < 0) return false;
            e.NextTick = unchecked(tick + Interval); // never accumulate missed waves
            return true;
        }
        internal void Prune(int tick)
        {
            var dead = new List<int>();
            foreach (var pair in entries) if (pair.Value.SeenTick != tick) dead.Add(pair.Key);
            foreach (int id in dead) entries.Remove(id);
        }
        internal void Clear() => entries.Clear();
        internal static bool IsOutpost(int type) => type == 2 || type == 106 || type == 107;
        internal static bool HasCapacity(int mode, int count, int extraThisTick, int limit) =>
            mode == 0 || mode == 99 || (long)count + extraThisTick < limit;
    }
}
