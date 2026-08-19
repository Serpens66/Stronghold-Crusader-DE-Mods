using System;

namespace CastlePlanner
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

        // Captures store depth relative to their own footprint. Remap it to
        // the current rotated footprint before calculating Unity sort order.
        public static int RemapDepthRow(
            int captureMinimumRow,
            int captureMaximumRow,
            int currentMinimumRow,
            int currentMaximumRow,
            int capturedRowOffset)
        {
            int capturedSpan = Math.Max(
                0,
                captureMaximumRow - captureMinimumRow);
            int currentSpan = Math.Max(
                0,
                currentMaximumRow - currentMinimumRow);
            int remappedOffset = capturedSpan == 0
                ? 0
                : (int)Math.Round(
                    capturedRowOffset * currentSpan / (double)capturedSpan,
                    MidpointRounding.AwayFromZero);
            return currentMinimumRow + remappedOffset;
        }

        public static int GetMiddleDepthRow(int minimumRow, int maximumRow)
        {
            if (maximumRow < minimumRow)
                throw new ArgumentOutOfRangeException(nameof(maximumRow));

            return minimumRow + (maximumRow - minimumRow) / 2;
        }
    }
}
