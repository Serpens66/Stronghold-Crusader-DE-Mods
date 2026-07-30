using System;
using System.Collections.Generic;

namespace SpawnCastle
{
    internal readonly struct WorldTile : IEquatable<WorldTile>
    {
        public WorldTile(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(WorldTile other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldTile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }
    }

    internal readonly struct WallSegment
    {
        public WallSegment(WorldTile start, WorldTile end, int tileCount)
        {
            Start = start;
            End = end;
            TileCount = tileCount;
        }

        public WorldTile Start { get; }
        public WorldTile End { get; }
        public int TileCount { get; }
    }

    internal static class AivWorldPlacement
    {
        public static WorldTile ToWorld(
            AivGridPoint point,
            AivGridPoint keepAnchor,
            int keepTileX,
            int keepTileY)
        {
            AivGridDelta delta = AivGridTransform.GetAnchorDelta(
                point,
                keepAnchor,
                AivRotation.Degrees0);

            // AIV editor rows grow upwards, while world tile Y grows downwards.
            return new WorldTile(
                keepTileX + delta.Column,
                keepTileY - delta.Row);
        }

        public static IReadOnlyList<WallSegment> CreateWallSegments(
            IEnumerable<WorldTile> sourceTiles,
            out IReadOnlyList<WorldTile> isolatedTiles,
            out int duplicateTileCount)
        {
            var tiles = new HashSet<WorldTile>();
            duplicateTileCount = 0;
            foreach (WorldTile tile in sourceTiles)
            {
                if (!tiles.Add(tile))
                    duplicateTileCount++;
            }

            var ordered = new List<WorldTile>(tiles);
            var covered = new HashSet<WorldTile>();
            var segments = new List<WallSegment>();

            ordered.Sort(CompareHorizontal);
            foreach (WorldTile tile in ordered)
            {
                if (tiles.Contains(new WorldTile(tile.X - 1, tile.Y)))
                    continue;

                int endX = tile.X;
                while (tiles.Contains(new WorldTile(endX + 1, tile.Y)))
                    endX++;

                if (endX == tile.X)
                    continue;

                segments.Add(
                    new WallSegment(
                        tile,
                        new WorldTile(endX, tile.Y),
                        endX - tile.X + 1));
                for (int x = tile.X; x <= endX; x++)
                    covered.Add(new WorldTile(x, tile.Y));
            }

            ordered.Sort(CompareVertical);
            foreach (WorldTile tile in ordered)
            {
                if (tiles.Contains(new WorldTile(tile.X, tile.Y - 1)))
                    continue;

                int endY = tile.Y;
                while (tiles.Contains(new WorldTile(tile.X, endY + 1)))
                    endY++;

                if (endY == tile.Y)
                    continue;

                segments.Add(
                    new WallSegment(
                        tile,
                        new WorldTile(tile.X, endY),
                        endY - tile.Y + 1));
                for (int y = tile.Y; y <= endY; y++)
                    covered.Add(new WorldTile(tile.X, y));
            }

            var isolated = new List<WorldTile>();
            foreach (WorldTile tile in ordered)
            {
                if (!covered.Contains(tile))
                    isolated.Add(tile);
            }

            isolatedTiles = isolated;
            return segments;
        }

        private static int CompareHorizontal(WorldTile left, WorldTile right)
        {
            int result = left.Y.CompareTo(right.Y);
            return result != 0 ? result : left.X.CompareTo(right.X);
        }

        private static int CompareVertical(WorldTile left, WorldTile right)
        {
            int result = left.X.CompareTo(right.X);
            return result != 0 ? result : left.Y.CompareTo(right.Y);
        }
    }
}
