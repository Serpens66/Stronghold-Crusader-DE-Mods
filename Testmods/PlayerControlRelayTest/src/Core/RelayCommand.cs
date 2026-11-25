using System;
using System.Buffers.Binary;
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

    // The owner is common to the command. Each six-byte row identifies exactly one
    // generation of one slot, including when a resync restores an older generation.
    internal static class RelayUnitCodec
    {
        internal const int MaximumSlot = 9999;
        private const int EntryBytes = sizeof(ushort) + sizeof(uint);
        internal const int MaximumUnitsPerCommand = 1000 / EntryBytes;
        internal const int MaximumEncodedBytes = MaximumUnitsPerCommand * EntryBytes;

        internal static byte[] Encode(IReadOnlyList<RelayUnit> units, int owner)
        {
            if (units == null || units.Count < 1 || units.Count > MaximumUnitsPerCommand)
                throw new ArgumentException("Relay unit identities exceed their size limit.");
            var bytes = new byte[units.Count * EntryBytes];
            int previous = 0;
            for (int index = 0; index < units.Count; index++)
            {
                RelayUnit unit = units[index];
                Validate(unit, owner, previous);
                int offset = index * EntryBytes;
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), (ushort)unit.Slot);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + sizeof(ushort)), unit.GlobalId);
                previous = unit.Slot;
            }
            return bytes;
        }

        internal static RelayUnit[] Decode(byte[] encoded, int owner)
        {
            if (encoded == null || encoded.Length < EntryBytes || encoded.Length > MaximumEncodedBytes ||
                encoded.Length % EntryBytes != 0)
                throw new ArgumentException("Encoded relay identities have an invalid size.");
            var units = new RelayUnit[encoded.Length / EntryBytes];
            int previous = 0;
            for (int index = 0; index < units.Length; index++)
            {
                int offset = index * EntryBytes;
                var unit = new RelayUnit(BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(offset)),
                    BinaryPrimitives.ReadUInt32LittleEndian(encoded.AsSpan(offset + sizeof(ushort))), owner);
                Validate(unit, owner, previous);
                units[index] = unit;
                previous = unit.Slot;
            }
            return units;
        }

        private static void Validate(RelayUnit unit, int owner, int previousSlot)
        {
            if (unit.Owner != owner || unit.GlobalId == 0 || unit.Slot <= previousSlot || unit.Slot > MaximumSlot)
                throw new ArgumentException("Relay units need an owner, a global ID and ascending native slots.");
        }

        internal static List<RelayUnit[]> Split(RelayUnit[] units)
        {
            var chunks = new List<RelayUnit[]>();
            for (int offset = 0; offset < units.Length; offset += MaximumUnitsPerCommand)
            {
                var chunk = new RelayUnit[Math.Min(MaximumUnitsPerCommand, units.Length - offset)];
                Array.Copy(units, offset, chunk, 0, chunk.Length);
                chunks.Add(chunk);
            }
            return chunks;
        }
    }

    // Immutable request snapshots are shared by transport, permission checks and execution.
    internal sealed class RelayCommand
    {
        private static readonly Encoding WireText = new UTF8Encoding(false, true);
        internal RelayCommand(string epoch, int owner, ulong requester, long requestId, RelayAction action,
            int targetX, int targetY, IReadOnlyList<RelayUnit> unitIdentities)
        {
            if (string.IsNullOrWhiteSpace(epoch) || epoch.Length > 128) throw new ArgumentException("Invalid relay epoch.", nameof(epoch));
            WireText.GetByteCount(epoch);
            if (owner < 1 || owner > 8 || requester == 0 || requestId <= 0) throw new ArgumentException("Invalid relay request identity.");
            if (action < RelayAction.Move || action > RelayAction.AttackHere) throw new ArgumentException("Unknown relay action.", nameof(action));
            EncodedUnits = RelayUnitCodec.Encode(unitIdentities, owner);
            var units = new RelayUnit[unitIdentities.Count];
            for (int i = 0; i < units.Length; i++) units[i] = unitIdentities[i];
            Units = Array.AsReadOnly(units);
            Epoch = epoch; Owner = owner; Requester = requester; RequestId = requestId;
            Action = action; TargetX = targetX; TargetY = targetY;
        }
        internal string Epoch { get; }
        internal int Owner { get; }
        internal ulong Requester { get; }
        internal long RequestId { get; }
        internal RelayAction Action { get; }
        internal int TargetX { get; }
        internal int TargetY { get; }
        internal ReadOnlyCollection<RelayUnit> Units { get; }
        internal byte[] EncodedUnits { get; }

        internal byte[] ComputeDigest()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, WireText))
            using (var sha = SHA256.Create())
            {
                writer.Write(4); // Canonical digest format, independent of MessagePack framing.
                writer.Write(Epoch); writer.Write(Owner); writer.Write(Requester); writer.Write(RequestId);
                writer.Write((int)Action); writer.Write(TargetX); writer.Write(TargetY);
                writer.Write(EncodedUnits.Length); writer.Write(EncodedUnits);
                writer.Flush(); return sha.ComputeHash(stream.ToArray());
            }
        }
    }
}
