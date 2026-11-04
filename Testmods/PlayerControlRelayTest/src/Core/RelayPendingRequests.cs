using System;
using System.Collections.Generic;

namespace PlayerControlRelayTest
{
    // Unity-thread request bookkeeping. A terminal reply/report may arrive after a
    // newer input for the same owner; only its exact captured request may be cleared.
    internal sealed class RelayPendingRequests
    {
        private readonly Dictionary<int, RelayCommand> byOwner = new Dictionary<int, RelayCommand>();
        internal int Count => byOwner.Count;
        internal bool TryAdd(RelayCommand command)
        {
            if (command == null || command.Owner < 1 || command.Owner > 8 || byOwner.ContainsKey(command.Owner) || byOwner.Count >= 8)
                return false;
            byOwner.Add(command.Owner, command); return true;
        }
        internal bool TryGet(int owner, out RelayCommand command) => byOwner.TryGetValue(owner, out command);
        internal bool ContainsOwner(int owner) => byOwner.ContainsKey(owner);
        internal bool RemoveIfMatches(RelayCommand command)
        {
            if (command == null || !byOwner.TryGetValue(command.Owner, out var current) ||
                !StringComparer.Ordinal.Equals(current.Epoch, command.Epoch) || current.Requester != command.Requester ||
                current.RequestId != command.RequestId) return false;
            byte[] expected = current.ComputeDigest(), received = command.ComputeDigest();
            int difference = expected.Length ^ received.Length;
            if (difference != 0) return false;
            for (int i = 0; i < expected.Length; i++) difference |= expected[i] ^ received[i];
            return difference == 0 && byOwner.Remove(command.Owner);
        }
        internal void Clear() => byOwner.Clear();
    }
}
