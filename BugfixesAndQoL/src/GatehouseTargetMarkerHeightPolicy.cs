namespace BugfixesAndQoL
{
    // Native FBCB9319: wall markers add 20 at 0x43779; units add 40 at 0x184FD0.
    // Elevated roofs use the shared 0xC07C0 helper and must not receive this correction.
    internal static class GatehouseTargetMarkerHeightPolicy
    {
        internal const string NativeSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const uint WallFlag = 0x100;
        internal const uint ElevatedFlag = 0x10000000;
        internal const int MissingHeight = 20;

        // Mode-8 output keeps the native image unchanged. The +1 belongs only to cache hashing.
        // Record index 8 is tile_x; index 12 (_layerDelay) is independent sorting data.
        // The short click animation uses 106-countdown (16..1) with offset 10.
        internal static bool IsMoveTarget(int file, int image, int horizontalOffset, bool hiMode)
        {
            return file == 107 && !hiMode &&
                ((image >= 82 && image <= 89 && horizontalOffset == 12) ||
                 (image >= 90 && image <= 105 && horizontalOffset == 10));
        }

        internal static bool UsesStructureHeight(bool detailed, bool flattened, bool flattenAllowed)
        { return detailed && !(flattened && flattenAllowed); }

        internal static bool TryAdjust(bool enabled, int file, int image, int horizontalOffset, bool hiMode,
            bool detailed, bool flattened, bool flattenAllowed, uint flags, int buildingType,
            ref int tileY)
        {
            if (!enabled || !IsMoveTarget(file, image, horizontalOffset, hiMode) ||
                !UsesStructureHeight(detailed, flattened, flattenAllowed) ||
                (flags & WallFlag) == 0 || (flags & ElevatedFlag) != 0 ||
                (buildingType != 45 && buildingType != 46) || tileY > int.MaxValue - MissingHeight)
                return false;
            // addUpdatePixie receives the negated native vertical offset: positive moves upward.
            tileY += MissingHeight;
            return true;
        }
    }
}
