using System;
using System.IO;

namespace AutoSendTest
{
    // A restored rules record is usable only together with its complete identity
    // record. Keep candidates private until both parts have passed validation.
    internal static class AutoSendSavedPlayers
    {
        private const int Slots = AutoSendState.PlayerCount + 1;
        private const int IdentityBytes = Slots * (sizeof(uint) + sizeof(ulong));

        internal static bool TryRead(byte[] rules, byte[] players, int[] allowed, int[] limits,
            out AutoSendState state, out uint[] lords, out ulong[] humans, out string reason)
        {
            state = null; lords = null; humans = null;
            if (!AutoSendSaveCodec.TryDecode(rules, allowed, limits, out AutoSendState candidate, out reason)) return false;
            if (players == null || players.Length != IdentityBytes)
            { reason = "Saved player identities are missing or have an invalid size."; return false; }
            var candidateLords = new uint[Slots]; var candidateHumans = new ulong[Slots];
            using (var stream = new MemoryStream(players, false))
            using (var reader = new BinaryReader(stream))
            {
                // BinaryReader/Writer define the little-endian byte contract.
                // Global IDs are opaque unsigned values, including zero.
                for (int player = 0; player < Slots; player++)
                { candidateLords[player] = reader.ReadUInt32(); candidateHumans[player] = reader.ReadUInt64(); }
            }
            if (candidateLords[0] != 0 || candidateHumans[0] != 0)
            { reason = "Unused saved player slot zero must be empty."; return false; }
            state = candidate; lords = candidateLords; humans = candidateHumans;
            reason = string.Empty; return true;
        }

        internal static byte[] EncodePlayers(uint[] lords, ulong[] humans)
        {
            if (lords == null || humans == null || lords.Length != Slots || humans.Length != Slots ||
                lords[0] != 0 || humans[0] != 0)
                throw new ArgumentException("Nine player slots with an empty slot zero are required.");
            using (var stream = new MemoryStream(IdentityBytes))
            using (var writer = new BinaryWriter(stream))
            {
                for (int player = 0; player < Slots; player++)
                { writer.Write(lords[player]); writer.Write(humans[player]); }
                writer.Flush(); return stream.ToArray();
            }
        }
    }
}
