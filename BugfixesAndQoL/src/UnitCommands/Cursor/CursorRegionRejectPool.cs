using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.UnitCommands
{
    internal readonly struct CursorRegionRejectKey : IEquatable<CursorRegionRejectKey>
    {
        internal readonly int Player, Source, Target, Mode;
        internal CursorRegionRejectKey(int player, int source, int target, int mode)
        { Player = player; Source = source; Target = target; Mode = mode; }
        public bool Equals(CursorRegionRejectKey other) => Player == other.Player && Source == other.Source && Target == other.Target && Mode == other.Mode;
        public override bool Equals(object value) => value is CursorRegionRejectKey other && Equals(other);
        public override int GetHashCode() => unchecked(((Player * 397 ^ Source) * 397 ^ Target) * 397 ^ Mode);
    }

    internal sealed class CursorRegionRejectPool
    {
        internal const int Capacity = 8, KeysPerBatch = 128;
        internal const int GroundProofsPerBatch = 4, GroundMinimumSelection = 16;
        internal sealed class Batch
        {
            internal object Owner;
            internal int Epoch, Tick;
            internal long Revision;
            internal ulong Diplomacy;
            internal bool Disabled;
            internal int SelectionCount, GroundCount, GroundAttempts;
            internal readonly CursorGroundRejectProof[] Ground = CreateGroundProofs();
            internal readonly Dictionary<CursorRegionRejectKey, byte> Rejections = new Dictionary<CursorRegionRejectKey, byte>(KeysPerBatch);
            private static CursorGroundRejectProof[] CreateGroundProofs()
            {
                var result = new CursorGroundRejectProof[GroundProofsPerBatch];
                for (int i = 0; i < result.Length; i++) result[i] = new CursorGroundRejectProof();
                return result;
            }
            internal void ClearGround()
            {
                GroundCount = GroundAttempts = SelectionCount = 0;
                for (int i = 0; i < Ground.Length; i++) Ground[i].Reset();
            }
        }
        private readonly object sync = new object();
        private readonly Batch[] available = new Batch[Capacity];
        internal CursorRegionRejectPool() { for (int i = 0; i < available.Length; i++) available[i] = new Batch(); }
        internal Batch Rent(object owner, int epoch, int tick, long revision, ulong diplomacy)
        {
            lock (sync)
            {
                for (int i = 0; i < available.Length; i++)
                {
                    Batch value = available[i]; if (value == null) continue;
                    available[i] = null; value.Rejections.Clear(); value.Disabled = false;
                    value.ClearGround();
                    value.Owner = owner; value.Epoch = epoch; value.Tick = tick; value.Revision = revision; value.Diplomacy = diplomacy;
                    return value;
                }
            }
            return null;
        }
        internal void Return(Batch value)
        {
            if (value == null) return;
            lock (sync)
            {
                value.Rejections.Clear(); value.Owner = null;
                value.ClearGround();
                for (int i = 0; i < available.Length; i++) if (available[i] == null) { available[i] = value; return; }
            }
        }
    }
}
