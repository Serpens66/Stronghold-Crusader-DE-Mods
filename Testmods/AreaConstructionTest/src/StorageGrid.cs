using System;
using System.Collections.Generic;

namespace AreaConstructionTest
{
    internal static class StorageGrid
    {
        internal static void Fill(List<Tile> result, Tile start, Tile end, int width, int height, int mapSize)
        {
            result.Clear();
            if (width <= 0 || height <= 0 || mapSize <= 0 ||
                (uint)start.X >= (uint)mapSize || (uint)start.Y >= (uint)mapSize)
                return;

            int dx = Math.Max(0, Math.Min(end.X, mapSize - 1)) - start.X;
            int dy = Math.Max(0, Math.Min(end.Y, mapSize - 1)) - start.Y;
            int columns = Math.Abs(dx) / width + 1;
            int rows = Math.Abs(dy) / height + 1;
            int stepX = dx < 0 ? -width : width;
            int stepY = dy < 0 ? -height : height;

            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                    result.Add(new Tile(start.X + column * stepX, start.Y + row * stepY));
        }

        // Eligibility can reserve budget. Visit each site once, and only once it has
        // an eligible cardinal neighbor. The sole exception is the Vanilla-valid anchor.
        internal static void CollectConnected(IReadOnlyList<Tile> candidates, int width, int height,
            Func<Tile, Tile?, bool> isEligible, List<Tile> accepted, HashSet<Tile> unvisited = null)
        {
            accepted.Clear();
            if (candidates.Count == 0 || width <= 0 || height <= 0 || !isEligible(candidates[0], null))
                return;

            unvisited = unvisited ?? new HashSet<Tile>();
            unvisited.Clear();
            for (int index = 1; index < candidates.Count; index++)
                unvisited.Add(candidates[index]);

            accepted.Add(candidates[0]);
            for (int index = 0; index < accepted.Count; index++)
            {
                Tile current = accepted[index];
                Visit(new Tile(current.X + width, current.Y), current);
                Visit(new Tile(current.X, current.Y + height), current);
                Visit(new Tile(current.X - width, current.Y), current);
                Visit(new Tile(current.X, current.Y - height), current);
            }
            unvisited.Clear();

            void Visit(Tile candidate, Tile neighbor)
            {
                if (unvisited.Remove(candidate) && isEligible(candidate, neighbor))
                    accepted.Add(candidate);
            }
        }
    }
}
