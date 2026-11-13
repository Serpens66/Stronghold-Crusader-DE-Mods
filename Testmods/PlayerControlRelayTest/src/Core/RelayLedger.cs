using System;
using System.Collections.Generic;

namespace PlayerControlRelayTest
{
    // Caller serializes access. No entry eviction: a full ledger blocks new work until
    // a genuinely new wire epoch, so loss of an acknowledgement cannot permit a replay.
    internal sealed class RelayLedger
    {
        internal const int MaximumEntries = 4096;
        private readonly int capacity;
        private readonly Dictionary<Key, Entry> entries = new Dictionary<Key, Entry>();
        private string epoch;
        private readonly struct Key : IEquatable<Key>
        {
            private readonly ulong requester;
            private readonly int owner;
            private readonly long requestId;
            internal Key(RelayCommand command) { requester = command.Requester; owner = command.Owner; requestId = command.RequestId; }
            public bool Equals(Key other) => requester == other.requester && owner == other.owner && requestId == other.requestId;
            public override bool Equals(object other) => other is Key key && Equals(key);
            public override int GetHashCode() => unchecked((requester.GetHashCode() * 397 ^ owner) * 397 ^ requestId.GetHashCode());
        }
        private sealed class Entry
        {
            internal readonly byte[] Digest;
            internal RelayOutcome Outcome;
            internal string Reason;
            internal Entry(byte[] digest) { Digest = digest; Outcome = RelayOutcome.Accepted; Reason = "Request reserved."; }
        }
        internal RelayLedger(int capacity = MaximumEntries)
        {
            if (capacity < 1 || capacity > MaximumEntries) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
        }
        internal int Count => entries.Count;
        internal bool Clear(string newEpoch)
        {
            if (string.IsNullOrWhiteSpace(newEpoch)) throw new ArgumentException("A wire epoch is required.", nameof(newEpoch));
            if (StringComparer.Ordinal.Equals(epoch, newEpoch)) return false;
            epoch = newEpoch; entries.Clear(); return true;
        }
        internal bool Reserve(RelayCommand command, out bool duplicate, out string reason)
        {
            duplicate = false; reason = null;
            if (command == null || epoch == null || !StringComparer.Ordinal.Equals(epoch, command.Epoch))
            { reason = "Relay epoch is not current."; return false; }
            var key = new Key(command);
            byte[] digest = command.ComputeDigest();
            if (entries.TryGetValue(key, out var existing))
            {
                if (!Equal(digest, existing.Digest)) { reason = "Request identity was reused with different content."; return false; }
                duplicate = true; reason = existing.Reason; return true;
            }
            if (entries.Count >= capacity) { reason = "Relay replay ledger is full for this epoch."; return false; }
            entries.Add(key, new Entry(digest)); return true;
        }
        internal bool Record(RelayCommand command, RelayOutcome outcome, string reason)
        {
            if (!TryEntry(command, out var entry) || !Enum.IsDefined(typeof(RelayOutcome), outcome) ||
                !CanAdvance(entry.Outcome, outcome)) return false;
            entry.Outcome = outcome; entry.Reason = reason ?? string.Empty; return true;
        }
        internal bool TryGet(RelayCommand command, out RelayOutcome outcome, out string reason)
        {
            outcome = default; reason = null;
            if (!TryEntry(command, out var entry)) return false;
            outcome = entry.Outcome; reason = entry.Reason; return true;
        }
        private bool TryEntry(RelayCommand command, out Entry entry)
        {
            entry = null;
            return command != null && StringComparer.Ordinal.Equals(epoch, command.Epoch) &&
                entries.TryGetValue(new Key(command), out entry) && Equal(command.ComputeDigest(), entry.Digest);
        }
        private static bool CanAdvance(RelayOutcome previous, RelayOutcome next) => previous == next ||
            previous == RelayOutcome.Accepted && next != RelayOutcome.Accepted ||
            previous == RelayOutcome.Submitted && (next == RelayOutcome.Executed || next == RelayOutcome.Uncertain || next == RelayOutcome.Rejected) ||
            previous == RelayOutcome.Uncertain && next == RelayOutcome.Executed;
        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            int difference = 0;
            for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }
}
