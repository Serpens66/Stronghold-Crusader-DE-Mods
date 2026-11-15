namespace AIHunterPreyTest
{
    internal static class PreyPresencePolicy
    {
        internal const int ScanInterval = 4800;
        internal static bool Due(bool hasScan, int previous, int current) => !hasScan || unchecked((uint)(current - previous)) >= ScanInterval;
        // Reservations and hunter-local rankings are deliberately excluded: they do not exhaust prey.
        internal static int OwnerMask(bool active, bool live, bool corpseCollectible, int owner, bool ownPlayerAllowed)
        {
            if (!active || (!live && !corpseCollectible)) return 0;
            if (owner == 0) return 0x1FE;
            return ownPlayerAllowed && owner >= 1 && owner <= 8 ? 1 << owner : 0;
        }
        internal static bool ShouldBlock(bool enabled, bool controlAiv, bool autoCall, bool ai, bool known, bool prey) =>
            enabled && (controlAiv || autoCall) && ai && known && !prey;
    }
}
