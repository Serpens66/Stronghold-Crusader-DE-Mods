using System;
using System.Collections.Generic;

namespace AreaConstructionTest
{
    internal readonly struct Tile : IEquatable<Tile>
    {
        internal Tile(int x, int y) { X = x; Y = y; }

        internal int X { get; }
        internal int Y { get; }

        public bool Equals(Tile other) => X == other.X && Y == other.Y;
        public override bool Equals(object other) => other is Tile tile && Equals(tile);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
    }

    internal static class AreaGrid
    {
        // Tile coordinates denote the same footprint reference for all candidates.
        // Enumeration is nearest block first, independent of the final drag extent.
        internal static void Fill(List<Tile> result, Tile start, Tile end, int width, int height, int mapSize)
        {
            result.Clear();
            if (width <= 0 || height <= 0 || mapSize <= 0 || !Inside(start, mapSize))
                return;

            int dx = Math.Max(0, Math.Min(end.X, mapSize - 1)) - start.X;
            int dy = Math.Max(0, Math.Min(end.Y, mapSize - 1)) - start.Y;
            if (Math.Abs(dx) < width && Math.Abs(dy) < height)
            {
                result.Add(start);
                return;
            }

            int strideX = 2 * width + 1;
            int strideY = 2 * height + 1;
            int blocksX = Math.Abs(dx) / strideX;
            int blocksY = Math.Abs(dy) / strideY;
            int directionX = dx < 0 ? -1 : 1;
            int directionY = dy < 0 ? -1 : 1;

            for (int ring = 0; ring <= Math.Max(blocksX, blocksY); ring++)
            {
                if (ring <= blocksY)
                {
                    for (int blockX = 0; blockX <= Math.Min(ring, blocksX); blockX++)
                        AddBlock(blockX, ring);
                }

                if (ring <= blocksX)
                {
                    for (int blockY = 0; blockY < Math.Min(ring, blocksY + 1); blockY++)
                        AddBlock(ring, blockY);
                }
            }

            void AddBlock(int blockX, int blockY)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        var tile = new Tile(
                            start.X + directionX * (blockX * strideX + x * width),
                            start.Y + directionY * (blockY * strideY + y * height));
                        if (Inside(tile, mapSize))
                            result.Add(tile);
                    }
                }
            }
        }

        private static bool Inside(Tile tile, int mapSize) =>
            (uint)tile.X < (uint)mapSize && (uint)tile.Y < (uint)mapSize;
    }
}
