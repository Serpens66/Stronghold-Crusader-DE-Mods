using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PlayerControlRelayTest
{
    internal enum RelayAction { Move = 1, Stop = 2, Stance = 3, Patrol = 4, Disband = 5, AttackHere = 6 }
    internal enum RelayOutcome { Accepted, Rejected, Submitted, Executed, Uncertain }

    // Local snapshot for selection and markers.
    internal readonly struct RelayUnit : IEquatable<RelayUnit>
    {
        internal RelayUnit(int slot, uint globalId, int owner)
        { Slot = slot; GlobalId = globalId; Owner = owner; }
        internal int Slot { get; }
        internal uint GlobalId { get; }
        internal int Owner { get; }
        public bool Equals(RelayUnit other) => Slot == other.Slot && GlobalId == other.GlobalId && Owner == other.Owner;
        public override bool Equals(object value) => value is RelayUnit other && Equals(other);
        public override int GetHashCode() => unchecked((Slot * 397 ^ (int)GlobalId) * 397 ^ Owner);
    }

    // Unit slots as ascending LEB128 deltas: one native Chore carries hundreds of units,
    // which a list of separate identities could not. Caller splits larger selections.
    internal static class RelaySlotCodec
    {
        internal const int MaximumSlot = 9999;
        internal const int MaximumEncodedBytes = 1000;

        internal static byte[] Encode(IReadOnlyList<int> slots)
        {
            var bytes = new List<byte>(slots.Count * 2);
            int previous = 0;
            foreach (int slot in slots)
            {
                if (slot <= previous || slot > MaximumSlot) throw new ArgumentException("Unit slots must ascend within the native range.");
                for (int delta = slot - previous; ; delta >>= 7)
                {
                    if (delta < 0x80) { bytes.Add((byte)delta); break; }
                    bytes.Add((byte)(delta & 0x7F | 0x80));
                }
                previous = slot;
            }
            return bytes.ToArray();
        }

        internal static int[] Decode(byte[] encoded)
        {
            if (encoded == null || encoded.Length < 1 || encoded.Length > MaximumEncodedBytes)
                throw new ArgumentException("Encoded unit slots are outside their size limit.");
            var slots = new List<int>(encoded.Length);
            int previous = 0, delta = 0, shift = 0;
            foreach (byte value in encoded)
            {
                if (shift > 14) throw new ArgumentException("Encoded unit slot is too long.");
                delta |= (value & 0x7F) << shift;
                if ((value & 0x80) != 0) { shift += 7; continue; }
                if (delta == 0 || previous + delta > MaximumSlot) throw new ArgumentException("Encoded unit slots must ascend within the native range.");
                previous += delta; slots.Add(previous);
                delta = 0; shift = 0;
            }
            if (shift != 0) throw new ArgumentException("Encoded unit slots are truncated.");
            return slots.ToArray();
        }

        // Keeps every chunk within one Chore. Input must already ascend.
        internal static List<int[]> Split(IReadOnlyList<int> slots)
        {
            var chunks = new List<int[]>();
            var current = new List<int>();
            int previous = 0, size = 0;
            foreach (int slot in slots)
            {
                int delta = slot - previous;
                int cost = delta < 0x80 ? 1 : 2; // MaximumSlot needs at most two bytes.
                if (size + cost > MaximumEncodedBytes)
                {
                    chunks.Add(current.ToArray()); current.Clear();
                    size = 0; delta = slot; cost = delta < 0x80 ? 1 : 2;
                }
                current.Add(slot); size += cost; previous = slot;
            }
            if (current.Count > 0) chunks.Add(current.ToArray());
            return chunks;
        }
    }

    // Immutable request snapshots are shared by transport, permission checks and execution.
    // The native global ID counter only rises, so a slot reused after the capture carries a
    // higher ID than MaxGlobalId and is not mistaken for a selected unit.
    internal sealed class RelayCommand
    {
        private static readonly Encoding WireText = new UTF8Encoding(false, true);
        private readonly int[] slots;
        internal RelayCommand(string epoch, int owner, ulong requester, long requestId, RelayAction action,
            int targetX, int targetY, uint maxGlobalId, IReadOnlyList<int> unitSlots)
        {
            if (string.IsNullOrWhiteSpace(epoch) || epoch.Length > 128) throw new ArgumentException("Invalid relay epoch.", nameof(epoch));
            WireText.GetByteCount(epoch);
            if (owner < 1 || owner > 8 || requester == 0 || requestId <= 0 || maxGlobalId == 0) throw new ArgumentException("Invalid relay request identity.");
            if (action < RelayAction.Move || action > RelayAction.AttackHere) throw new ArgumentException("Unknown relay action.", nameof(action));
            if (unitSlots == null || unitSlots.Count < 1) throw new ArgumentException("A relay command needs at least one unit.", nameof(unitSlots));
            EncodedSlots = RelaySlotCodec.Encode(unitSlots);
            if (EncodedSlots.Length > RelaySlotCodec.MaximumEncodedBytes) throw new ArgumentException("Too many units for one relay command.", nameof(unitSlots));
            slots = new int[unitSlots.Count];
            for (int i = 0; i < slots.Length; i++) slots[i] = unitSlots[i];
            Slots = Array.AsReadOnly(slots);
            Epoch = epoch; Owner = owner; Requester = requester; RequestId = requestId;
            Action = action; TargetX = targetX; TargetY = targetY; MaxGlobalId = maxGlobalId;
        }
        internal string Epoch { get; }
        internal int Owner { get; }
        internal ulong Requester { get; }
        internal long RequestId { get; }
        internal RelayAction Action { get; }
        internal int TargetX { get; }
        internal int TargetY { get; }
        internal uint MaxGlobalId { get; }
        internal ReadOnlyCollection<int> Slots { get; }
        internal byte[] EncodedSlots { get; }

        internal byte[] ComputeDigest()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, WireText))
            using (var sha = SHA256.Create())
            {
                writer.Write(3); // Canonical digest format, independent of MessagePack framing.
                writer.Write(Epoch); writer.Write(Owner); writer.Write(Requester); writer.Write(RequestId);
                writer.Write((int)Action); writer.Write(TargetX); writer.Write(TargetY); writer.Write(MaxGlobalId);
                writer.Write(EncodedSlots.Length); writer.Write(EncodedSlots);
                writer.Flush(); return sha.ComputeHash(stream.ToArray());
            }
        }
    }
}
