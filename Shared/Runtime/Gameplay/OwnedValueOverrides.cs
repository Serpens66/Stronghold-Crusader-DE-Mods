using System;
using System.Collections.Generic;

namespace Shared
{
    // Local ownership only: equal-valued foreign writes cannot be attributed without a shared broker.
    internal sealed class OwnedValueOverrides<TKey, TValue>
    {
        private readonly Func<TKey, TValue> read;
        private readonly Action<TKey, TValue> write;
        private readonly Dictionary<TKey, Entry> entries = new Dictionary<TKey, Entry>();
        private readonly EqualityComparer<TValue> equality = EqualityComparer<TValue>.Default;

        private struct Entry
        {
            internal TValue Before, Written;
        }

        internal OwnedValueOverrides(Func<TKey, TValue> read, Action<TKey, TValue> write)
        {
            this.read = read;
            this.write = write;
        }

        // A caller supplying canReplace permits only its own previous value or an accepted base.
        internal bool Apply(TKey key, TValue desired, Predicate<TValue> canReplace = null)
        {
            TValue current = read(key);
            bool owned = entries.TryGetValue(key, out Entry entry) && equality.Equals(current, entry.Written);
            if (!owned)
            {
                entries.Remove(key);
                if (canReplace != null && !canReplace(current))
                    return false;
                entry.Before = current;
            }
            if (equality.Equals(current, desired))
                return true; // Never claim a cell we did not change.

            entry.Written = desired;
            // Retain recovery information even if a setter mutates and subsequently throws.
            entries[key] = entry;
            write(key, desired);
            return true;
        }

        internal void Restore(TKey key)
        {
            if (!entries.TryGetValue(key, out Entry entry))
                return;
            if (equality.Equals(read(key), entry.Written))
                write(key, entry.Before);
            entries.Remove(key);
        }

        internal void RestoreAll(Action<Exception> report, bool endSession = false)
        {
            foreach (TKey key in new List<TKey>(entries.Keys))
            {
                try { Restore(key); }
                catch (Exception ex)
                {
                    try { report(ex); } catch { } // Diagnostics must not prevent independent restoration.
                }
            }
            // Failed cleanup must not carry an old session's ownership into another map.
            if (endSession)
                entries.Clear();
        }
    }
}
