using System;

namespace ExtremePowers.API
{
    internal static class ExtremePowerChoreSender
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
                return Reject("Packet hook is not registered.", out rejectionReason);
            if (packet == null || serialize == null || getChoreManagerAddress == null || sendViaChore == null)
                return Reject("Chore send prerequisites are incomplete.", out rejectionReason);

            try
            {
                // SendPacketToAllEx2 serializes this same object again. The caller must not mutate it.
                body = serialize(packet) ?? throw new InvalidOperationException("The packet serializer returned null.");
                if (getChoreManagerAddress() == 0)
                    return Reject("Chore manager is unavailable.", out rejectionReason);
                if (body.Length > MaximumPayloadBytes - sizeof(short))
                    return Reject($"Chore payload has {sizeof(short) + body.Length} bytes; limit is {MaximumPayloadBytes}.", out rejectionReason);

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
