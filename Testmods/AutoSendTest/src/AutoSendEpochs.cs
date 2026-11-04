using System;

namespace AutoSendTest
{
    // Epochs distinguish command streams; authenticated sender identity, rather
    // than the public nonce alone, decides whether a Hello acknowledges us.
    internal sealed class AutoSendEpochs
    {
        private const int ValidMask = (1 << (AutoSendState.PlayerCount + 1)) - 2;
        private readonly long[] epochs = new long[AutoSendState.PlayerCount + 1];
        private int readyMask, readyRosterMask;
        private long readyFingerprint;

        internal bool Bind(int sender, long epoch, int expectedLocalPlayer, long localEpoch,
            out bool localAcknowledged)
        {
            localAcknowledged = false;
            if (sender < 1 || sender > AutoSendState.PlayerCount || epoch == 0) return false;
            if (epochs[sender] == 0) epochs[sender] = epoch;
            if (epochs[sender] != epoch) return false;
            localAcknowledged = sender == expectedLocalPlayer && epoch == localEpoch;
            return true;
        }

        internal long Get(int sender) => sender >= 1 && sender <= AutoSendState.PlayerCount ? epochs[sender] : 0;
        internal bool AllKnown(int mask)
        {
            if (!ValidRoster(mask)) return false;
            for (int sender = 1; sender <= AutoSendState.PlayerCount; sender++)
                if ((mask & (1 << sender)) != 0 && epochs[sender] == 0) return false;
            return true;
        }
        internal long Fingerprint(int mask)
        {
            if (!AllKnown(mask)) return 0;
            // Explicit little-endian FNV-1a input: roster UInt32, then eight
            // Int64 bit patterns. Nonparticipants are canonical zero slots, so
            // unrelated Hellos cannot change the acknowledged roster digest.
            ulong hash = 14695981039346656037UL;
            for (int index = 0; index < sizeof(int); index++) Mix(ref hash, (byte)((uint)mask >> (index * 8)));
            for (int sender = 1; sender <= AutoSendState.PlayerCount; sender++)
            {
                ulong epoch = (mask & (1 << sender)) != 0 ? unchecked((ulong)epochs[sender]) : 0;
                for (int index = 0; index < sizeof(long); index++) Mix(ref hash, (byte)(epoch >> (index * 8)));
            }
            long fingerprint = (long)(hash & long.MaxValue);
            return fingerprint == 0 ? 1 : fingerprint;
        }
        internal bool AcknowledgeReady(int sender, long epoch, int mask, long fingerprint)
        {
            if (sender < 1 || sender > AutoSendState.PlayerCount || epoch == 0 || epochs[sender] != epoch ||
                !AllKnown(mask) || (mask & (1 << sender)) == 0 || fingerprint <= 0 || Fingerprint(mask) != fingerprint)
                return false;
            // The runtime also checks mask against its captured human roster.
            // Do not combine acknowledgements belonging to different rosters.
            if (readyRosterMask != 0 && (readyRosterMask != mask || readyFingerprint != fingerprint)) return false;
            readyRosterMask = mask; readyFingerprint = fingerprint; readyMask |= 1 << sender;
            return true;
        }
        internal bool IsReady(int mask) => ValidRoster(mask) && mask == readyRosterMask &&
            (readyMask & mask) == mask && readyFingerprint == Fingerprint(mask);
        internal void Reset()
        { Array.Clear(epochs, 0, epochs.Length); readyMask = readyRosterMask = 0; readyFingerprint = 0; }
        private static bool ValidRoster(int mask) => mask != 0 && (mask & ~ValidMask) == 0;
        private static void Mix(ref ulong hash, byte value) => hash = unchecked((hash ^ value) * 1099511628211UL);
    }
}
