using System;
using System.Collections.Generic;

namespace MapParser.Core
{
    public sealed class MapTileGeometry
    {
        public const int FixedRowCount = 800;
        public const int FixedTileCount = 320800;

        private static readonly IReadOnlyList<int> KnownWorldSizes =
            Array.AsReadOnly(new[] { 160, 200, 300, 400 });

        public MapTileGeometry(int tileCount, int worldSize)
        {
            if (tileCount != FixedTileCount)
            {
                throw new MapUnsupportedGeometryException(
                    $"Unsupported tile count {tileCount}; the SCDE tile layers require exactly {FixedTileCount} tiles.");
            }
            if (!IsSupportedWorldSize(worldSize))
            {
                throw new MapUnsupportedGeometryException(
                    $"Unsupported world size {worldSize}; supported sizes are 160, 200, 300 and 400.");
            }

            TileCount = tileCount;
            WorldSize = worldSize;
            WorldBorder = (FixedRowCount - worldSize) / 2;
            WorldTileCount = checked(worldSize * (worldSize + 2) / 2);
        }

        public int TileCount { get; }
        public int RowCount => FixedRowCount;
        public int WorldSize { get; }
        public int WorldBorder { get; }
        public int WorldTileCount { get; }
        public static IReadOnlyList<int> SupportedWorldSizes => KnownWorldSizes;

        public static bool IsSupportedWorldSize(int worldSize) =>
            worldSize == 160 || worldSize == 200 || worldSize == 300 || worldSize == 400;

        public bool IsValidCoordinate(int x, int y)
        {
            if (y < 0 || y >= FixedRowCount)
                return false;

            return x >= GetFirstX(y, FixedRowCount) && x <= GetLastX(y, FixedRowCount);
        }

        public bool IsWithinWorldBounds(int x, int y)
        {
            // World-size bounds are an inset diamond; they do not change the fixed section geometry.
            int localX = x - WorldBorder;
            int localY = y - WorldBorder;
            if (localY < 0 || localY >= WorldSize)
                return false;

            return localX >= GetFirstX(localY, WorldSize) &&
                localX <= GetLastX(localY, WorldSize);
        }

        public bool TryGetTileId(int x, int y, out int tileId)
        {
            if (!IsValidCoordinate(x, y))
            {
                tileId = default;
                return false;
            }

            // The native lookup stores rowStart and adds x only after validating the row span.
            tileId = GetFirstTileId(y) - GetFirstX(y, FixedRowCount) + x;
            return true;
        }

        public int GetTileId(int x, int y)
        {
            if (!TryGetTileId(x, y, out int tileId))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    $"Coordinate ({x},{y}) is outside the fixed {FixedRowCount}-row SCDE tile geometry.");
            }
            return tileId;
        }

        public bool TryGetCoordinate(int tileId, out MapCoordinate coordinate)
        {
            if (tileId < 0 || tileId >= TileCount)
            {
                coordinate = default;
                return false;
            }

            int low = 0;
            int high = FixedRowCount - 1;
            while (low < high)
            {
                int middle = low + ((high - low + 1) / 2);
                if (GetFirstTileId(middle) <= tileId)
                    low = middle;
                else
                    high = middle - 1;
            }

            int y = low;
            int rowStart = GetFirstTileId(y) - GetFirstX(y, FixedRowCount);
            coordinate = new MapCoordinate(tileId - rowStart, y);
            return true;
        }

        private static int GetFirstX(int y, int rowCount)
        {
            int half = rowCount / 2;
            return y < half ? half - 1 - y : y - half;
        }

        private static int GetLastX(int y, int rowCount)
        {
            int half = rowCount / 2;
            return y < half ? half + y : rowCount + half - 1 - y;
        }

        private static int GetFirstTileId(int y)
        {
            if (y < FixedRowCount / 2)
                return checked(y * (y + 1));

            int remainingRows = FixedRowCount - y;
            return checked(FixedTileCount - (remainingRows * (remainingRows + 1)));
        }
    }
}
