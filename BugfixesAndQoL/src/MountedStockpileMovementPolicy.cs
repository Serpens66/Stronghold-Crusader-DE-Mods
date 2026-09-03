namespace BugfixesAndQoL
{
    internal static class MountedStockpileMovementPolicy
    {
        internal const uint GoodsyardRelated = 0x2;
        internal const uint IsWallOrElevated = 0x10000100;

        public static bool ShouldUseNormalMovementClassification(
            long vanillaClassificationResult,
            bool coordinateValid,
            bool targetAvailable,
            uint targetFlags,
            int selectedCount,
            int mountedCount,
            bool allSelectedUnitsResolved) =>
            vanillaClassificationResult > 0 &&
            coordinateValid &&
            targetAvailable &&
            (targetFlags & GoodsyardRelated) != 0 &&
            selectedCount > 0 &&
            allSelectedUnitsResolved &&
            mountedCount == selectedCount;

        public static bool ShouldBypassMountedEndpointWallGate(
            bool vanillaWallGateRejected,
            bool coordinateValid,
            bool targetAvailable,
            uint targetFlags,
            bool currentUnitResolvedAndMounted) =>
            vanillaWallGateRejected &&
            coordinateValid &&
            targetAvailable &&
            (targetFlags & GoodsyardRelated) != 0 &&
            currentUnitResolvedAndMounted;
    }
}
