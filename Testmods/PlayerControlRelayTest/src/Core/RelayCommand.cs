using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PlayerControlRelayTest
{
    internal enum RelayAction { Move = 1, Stop = 2 }
    internal enum RelayOutcome { Accepted, Rejected, Submitted, Executed, Uncertain }

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

    // Immutable request snapshots are shared by transport, permission checks and execution.
    // Unit order is canonical: a different enumeration of the same selection is no collision.
    internal sealed class RelayCommand
    {
        internal const int MaximumUnits = 64;
        private static readonly Encoding WireText = new UTF8Encoding(false, true);
        internal RelayCommand(string epoch, int owner, ulong requester, long requestId, RelayAction action,
            int targetX, int targetY, RelayUnit[] units)
        {
            if (string.IsNullOrWhiteSpace(epoch) || epoch.Length > 128) throw new ArgumentException("Invalid relay epoch.", nameof(epoch));
            WireText.GetByteCount(epoch);
            if (owner < 1 || owner > 8 || requester == 0 || requestId <= 0) throw new ArgumentException("Invalid relay request identity.");
            if (action != RelayAction.Move && action != RelayAction.Stop) throw new ArgumentException("Unknown relay action.", nameof(action));
            if (units == null || units.Length < 1 || units.Length > MaximumUnits) throw new ArgumentException("A relay selection needs 1..64 units.", nameof(units));
            var copy = (RelayUnit[])units.Clone();
            Array.Sort(copy, (left, right) => left.Slot.CompareTo(right.Slot));
            for (int i = 0; i < copy.Length; i++)
            {
                if (copy[i].Slot <= 0 || copy[i].GlobalId == 0 || copy[i].Owner != owner ||
                    (i > 0 && copy[i - 1].Slot == copy[i].Slot))
                    throw new ArgumentException("Relay units need distinct positive slots, identities and the requested owner.", nameof(units));
            }
            Epoch = epoch; Owner = owner; Requester = requester; RequestId = requestId;
            Action = action; TargetX = targetX; TargetY = targetY; Units = Array.AsReadOnly(copy);
        }
        internal string Epoch { get; }
        internal int Owner { get; }
        internal ulong Requester { get; }
        internal long RequestId { get; }
        internal RelayAction Action { get; }
        internal int TargetX { get; }
        internal int TargetY { get; }
        internal ReadOnlyCollection<RelayUnit> Units { get; }
        internal RelayUnit[] CopyUnits()
        { var result = new RelayUnit[Units.Count]; Units.CopyTo(result, 0); return result; }

        internal byte[] ComputeDigest()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, WireText))
            using (var sha = SHA256.Create())
            {
                writer.Write(1); // Canonical digest format, independent of MessagePack framing.
                writer.Write(Epoch); writer.Write(Owner); writer.Write(Requester); writer.Write(RequestId);
                writer.Write((int)Action); writer.Write(TargetX); writer.Write(TargetY); writer.Write(Units.Count);
                foreach (var unit in Units) { writer.Write(unit.Slot); writer.Write(unit.GlobalId); writer.Write(unit.Owner); }
                writer.Flush(); return sha.ComputeHash(stream.ToArray());
            }
        }
    }
}
