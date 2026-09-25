using System;

namespace MapParser.Core
{
    internal static class ClassicMapGeometry
    {
        internal const int RowCount = 400;
        internal const int TileCount = 80400;
        internal const int CoordinateOffset = 200;

        internal static bool IsValidCoordinate(int x, int y)
        {
            if (y < 0 || y >= RowCount)
                return false;
            int firstX = y < RowCount / 2 ? RowCount / 2 - 1 - y : y - RowCount / 2;
            int lastX = y < RowCount / 2 ? RowCount / 2 + y : RowCount + RowCount / 2 - 1 - y;
            return x >= firstX && x <= lastX;
        }

        internal static int ToNativeTileId(int classicTileId)
        {
            if (classicTileId < 0 || classicTileId >= TileCount)
                throw new MapCorruptDataException($"Classic tile ID {classicTileId} is outside 0..{TileCount - 1}.");
            int firstId = 0;
            for (int y = 0; y < RowCount; y++)
            {
                int width = y < RowCount / 2 ? 2 * (y + 1) : 2 * (RowCount - y);
                if (classicTileId < firstId + width)
                {
                    int firstX = y < RowCount / 2 ? RowCount / 2 - 1 - y : y - RowCount / 2;
                    var geometry = new MapTileGeometry(MapTileGeometry.FixedTileCount, 400);
                    return geometry.GetTileId(firstX + classicTileId - firstId + CoordinateOffset,
                        y + CoordinateOffset);
                }
                firstId += width;
            }
            throw new MapCorruptDataException("Classic tile ID could not be mapped.");
        }

        internal static T[] Expand<T>(T[] source)
        {
            if (source == null || source.Length != TileCount)
                throw new MapCorruptDataException("Classic tile layer does not contain 80,400 entries.");
            var result = new T[MapTileGeometry.FixedTileCount];
            var geometry = new MapTileGeometry(MapTileGeometry.FixedTileCount, 400);
            int sourceOffset = 0;
            for (int y = 0; y < RowCount; y++)
            {
                int width = y < RowCount / 2 ? 2 * (y + 1) : 2 * (RowCount - y);
                int firstX = y < RowCount / 2 ? RowCount / 2 - 1 - y : y - RowCount / 2;
                int destinationOffset = geometry.GetTileId(firstX + CoordinateOffset,
                    y + CoordinateOffset);
                Array.Copy(source, sourceOffset, result, destinationOffset, width);
                sourceOffset += width;
            }
            return result;
        }
    }
}
