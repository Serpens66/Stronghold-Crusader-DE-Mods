namespace ExtraFeatures
{
    public sealed class GameSpeedRepeatScheduler
    {
        public const int RepeatIntervalMilliseconds = 250;

        private bool armed;
        private bool cancelledUntilRelease;
        private bool saturated;
        private long nextRepeatTimestamp;

        public bool Update(bool held, bool initiallyPressed, bool blocked, bool atBoundary,
            long nowTimestamp, long timestampFrequency)
        {
            if (!held)
            {
                Reset();
                return false;
            }
            if (blocked || timestampFrequency <= 0)
            {
                armed = false;
                cancelledUntilRelease = true;
                return false;
            }
            if (cancelledUntilRelease)
                return false;
            if (initiallyPressed)
            {
                armed = true;
                saturated = atBoundary;
                nextRepeatTimestamp = AddInterval(nowTimestamp, timestampFrequency);
                return false;
            }
            if (!armed || saturated)
                return false;
            if (atBoundary)
            {
                saturated = true;
                return false;
            }
            if (nowTimestamp < nextRepeatTimestamp)
                return false;

            // Schedule from the actual repeat so a stalled frame never causes a catch-up burst.
            nextRepeatTimestamp = AddInterval(nowTimestamp, timestampFrequency);
            return true;
        }

        public void Reset()
        {
            armed = false;
            cancelledUntilRelease = false;
            saturated = false;
            nextRepeatTimestamp = 0;
        }

        private static long AddInterval(long timestamp, long frequency)
        {
            long interval = frequency * RepeatIntervalMilliseconds / 1000;
            if (interval <= 0)
                interval = 1;
            return timestamp > long.MaxValue - interval ? long.MaxValue : timestamp + interval;
        }
    }
}
