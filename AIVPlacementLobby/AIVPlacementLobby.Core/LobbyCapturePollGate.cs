using System;

namespace AIVPlacementLobby.Core
{
    public sealed class LobbyCapturePollGate
    {
        private readonly long intervalTicks;
        private long nextPollTimestamp;

        public LobbyCapturePollGate(long intervalTicks)
        {
            if (intervalTicks < 1)
                throw new ArgumentOutOfRangeException(nameof(intervalTicks));
            this.intervalTicks = intervalTicks;
        }

        public bool ShouldCapture(long timestamp, bool force)
        {
            if (!force && timestamp < nextPollTimestamp)
                return false;

            nextPollTimestamp = timestamp + intervalTicks;
            return true;
        }

        // Known UI mutations should not have to wait for the safety poll.
        public void Invalidate() => nextPollTimestamp = 0;
    }
}
