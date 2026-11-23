namespace BugfixesAndQoL.AreaConstruction
{
    internal struct StorageBuildCadence
    {
        private int? lastTick;

        internal bool TryBegin(int? currentTick)
        {
            // The editor and an unavailable clock retain the existing frame cadence.
            if (!currentTick.HasValue)
                return true;

            int tick = currentTick.Value;
            // Compare within the forward half of the native Int32 counter, including wraparound.
            if (lastTick.HasValue && unchecked(tick - lastTick.Value) <= 0)
                return false;

            lastTick = tick;
            return true;
        }
    }
}
