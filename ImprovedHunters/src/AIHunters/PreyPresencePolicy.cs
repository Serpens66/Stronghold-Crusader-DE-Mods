namespace ImprovedHunters
{
    internal static class PreyPresencePolicy
    {
        internal const int ScanInterval = 4800;
        internal const int InitialDelay = 200; // Five simulation seconds at speed 40.
        // Reservations and hunter-local rankings are deliberately excluded: they do not exhaust prey.
        internal static int OwnerMask(bool active, bool live, bool corpseCollectible, int owner, bool ownPlayerAllowed)
        {
            if (!active || (!live && !corpseCollectible)) return 0;
            if (owner == 0) return 0x1FE;
            return ownPlayerAllowed && owner >= 1 && owner <= 8 ? 1 << owner : 0;
        }
    }

    // Default/reset starts a fresh mission-relative delay, even when a save restores a large map tick.
    internal struct HunterCheckSchedule
    {
        private bool anchored, checkedOnce;
        private int anchor;
        internal bool TryBeginCheck(int tick)
        {
            if (!anchored) { anchored = true; anchor = tick; return false; }
            uint interval = (uint)(checkedOnce ? PreyPresencePolicy.ScanInterval : PreyPresencePolicy.InitialDelay);
            if (unchecked((uint)(tick - anchor)) < interval) return false;
            anchor = tick; checkedOnce = true;
            return true;
        }
    }
}
