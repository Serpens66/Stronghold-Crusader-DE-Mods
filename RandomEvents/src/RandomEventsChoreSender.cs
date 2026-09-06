using System;

namespace RandomEvents
{
    internal static class RandomEventsChoreSender
    {
        internal const int MaximumPayloadBytes = 1200;

        internal static bool TrySend<T>(
            T packet,
            short packetId,
            bool packetHookRegistered,
            Func<T, byte[]> serialize,
            Func<ulong> getChoreManagerAddress,
            Action<T, short> sendViaChore,
            out byte[] body,
            out string rejectionReason)
            where T : class
        {
            body = Array.Empty<byte>();
            rejectionReason = null;
            if (!packetHookRegistered)
                return Reject("packet hook is not registered", out rejectionReason);
            if (packet == null || serialize == null || getChoreManagerAddress == null || sendViaChore == null)
                return Reject("Chore send prerequisites are incomplete", out rejectionReason);

            try
            {
                // The public 2.2.0 API serializes this same object again before queuing the Chore.
                body = serialize(packet) ?? throw new InvalidOperationException("the packet serializer returned null");
                if (getChoreManagerAddress() == 0)
                    return Reject("the Chore manager is unavailable", out rejectionReason);
                if (body.Length > MaximumPayloadBytes - sizeof(short))
                    return Reject($"payload has {sizeof(short) + body.Length} bytes; limit is {MaximumPayloadBytes}", out rejectionReason);

                sendViaChore(packet, packetId);
                return true;
            }
            catch (Exception ex)
            {
                return Reject("Chore send failed: " + ex.Message, out rejectionReason);
            }
        }

        private static bool Reject(string reason, out string rejectionReason)
        {
            rejectionReason = reason;
            return false;
        }
    }
}
