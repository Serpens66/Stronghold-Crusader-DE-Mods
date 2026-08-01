namespace SpawnCastle
{
    internal static class BlueprintSortingPolicy
    {
        private const int VanillaWorldBaseOrder = -20000;
        private const int VanillaDepthRowStride = 49;
        private const int VanillaIconLocalOffset = 4;

        // Flat footprints cannot overlap in depth, so keep them completely
        // above the world while leaving cursor and UI overlays at 32000 free.
        public const int FlattenedIconSortingOrder = 31990;

        public static int GetNaturalIconSortingOrder(
            int depthRow,
            int localSortingOffset = 0)
        {
            return VanillaWorldBaseOrder + depthRow * VanillaDepthRowStride +
                VanillaIconLocalOffset + localSortingOffset;
        }
    }
}
