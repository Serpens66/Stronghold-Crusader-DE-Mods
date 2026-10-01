using System;

namespace EnemyGatePathfindingTest
{
    // Bounded memoization, never an admission limit: a miss requires validation.
    internal sealed class CursorPreviewCache
    {
        internal readonly struct Key : IEquatable<Key>
        {
            internal readonly int Player, Unit, Global, StartX, StartY, TargetX,
                TargetY, TargetPcl, SourcePcl;
            internal readonly long Epoch, Generation;
            internal readonly ulong Fingerprint;
            internal Key(int player, int unit, int global, int startX, int startY,
                int targetX, int targetY, int targetPcl, int sourcePcl,
                long epoch, long generation, ulong fingerprint)
            {
                Player = player; Unit = unit; Global = global; StartX = startX;
                StartY = startY; TargetX = targetX; TargetY = targetY;
                TargetPcl = targetPcl; SourcePcl = sourcePcl; Epoch = epoch;
                Generation = generation; Fingerprint = fingerprint;
            }
            public bool Equals(Key other) => Player == other.Player &&
                Unit == other.Unit && Global == other.Global && StartX == other.StartX &&
                StartY == other.StartY && TargetX == other.TargetX && TargetY == other.TargetY &&
                TargetPcl == other.TargetPcl && SourcePcl == other.SourcePcl &&
                Epoch == other.Epoch && Generation == other.Generation &&
                Fingerprint == other.Fingerprint;
        }

        private struct Entry
        {
            internal Key Key;
            internal bool Valid, Allowed;
            internal long Created, Used;
        }
        private readonly Entry[] entries = new Entry[32];
        private readonly object sync = new object();
        private readonly long lifetime;
        private long sequence;
        internal CursorPreviewCache(long lifetime) { this.lifetime = lifetime; }
        internal bool TryGet(Key key, long now, out bool allowed)
        {
            lock (sync)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (!entries[i].Valid || !entries[i].Key.Equals(key)) continue;
                    long age = now - entries[i].Created;
                    if (age < 0 || age > lifetime) { entries[i].Valid = false; break; }
                    entries[i].Used = ++sequence;
                    allowed = entries[i].Allowed;
                    return true;
                }
            }
            allowed = true;
            return false;
        }
        internal void Put(Key key, long now, bool allowed)
        {
            lock (sync)
            {
                int victim = 0;
                for (int i = 0; i < entries.Length; i++)
                {
                    if (!entries[i].Valid || entries[i].Key.Equals(key)) { victim = i; break; }
                    if (entries[i].Used < entries[victim].Used) victim = i;
                }
                entries[victim] = new Entry { Valid = true, Key = key,
                    Created = now, Used = ++sequence, Allowed = allowed };
            }
        }
        internal void Clear()
        {
            lock (sync) { Array.Clear(entries, 0, entries.Length); sequence = 0; }
        }
    }
}
