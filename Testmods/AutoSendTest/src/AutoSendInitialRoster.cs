namespace AutoSendTest
{
    // The caller captures loss state at NativeLoaded, before simulation advances.
    // Later per-peer callback timing must not change the mission's rendezvous set.
    internal sealed class AutoSendInitialRoster
    {
        private const int ValidPlayerBits = (1 << (AutoSendState.PlayerCount + 1)) - 2;
        private long attempt;
        private int defeated;
        private bool hasAttempt, captured;

        internal void Reset(long currentAttempt)
        { attempt = currentAttempt; defeated = 0; hasAttempt = true; captured = false; }

        internal bool Capture(long currentAttempt, int defeatedMask)
        {
            if (!hasAttempt || currentAttempt != attempt || (defeatedMask & ~ValidPlayerBits) != 0) return false;
            if (captured) return defeated == defeatedMask;
            defeated = defeatedMask; captured = true; return true;
        }

        internal bool TryGet(long currentAttempt, ulong[] humanIds, out int mask)
        {
            mask = 0;
            if (!hasAttempt || !captured || currentAttempt != attempt || humanIds == null ||
                humanIds.Length != AutoSendState.PlayerCount + 1 || humanIds[0] != 0) return false;
            for (int player = 1; player <= AutoSendState.PlayerCount; player++)
                if (humanIds[player] != 0 && (defeated & (1 << player)) == 0) mask |= 1 << player;
            return true;
        }
    }
}
