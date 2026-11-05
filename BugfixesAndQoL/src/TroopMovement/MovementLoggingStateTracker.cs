namespace BugfixesAndQoL
{
    internal sealed class MovementLoggingStateTracker
    {
        private bool hasState;
        private bool sameSpeedActive;
        private bool rallyActive;
        private bool nativeFastpathsActive;

        internal bool TryUpdate(
            bool newSameSpeedActive,
            bool newRallyActive,
            bool newNativeFastpathsActive)
        {
            if (hasState &&
                sameSpeedActive == newSameSpeedActive &&
                rallyActive == newRallyActive &&
                nativeFastpathsActive == newNativeFastpathsActive)
            {
                return false;
            }

            hasState = true;
            sameSpeedActive = newSameSpeedActive;
            rallyActive = newRallyActive;
            nativeFastpathsActive = newNativeFastpathsActive;
            return true;
        }
    }
}
