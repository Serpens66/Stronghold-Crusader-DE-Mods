using System;
using System.Collections.Generic;

namespace AreaConstructionTest
{
    internal static class QuarryLayout
    {
        internal const int Scale = 6;
        internal const int PileScale = 2;
        internal const int MapSize = 800;
        private const int WalkwayWidth = 1;

        internal static bool Fill(List<Tile> sites, Tile start, Tile end)
        {
            int dx = end.X - start.X;
            int dy = end.Y - start.Y;
            sites.Clear();
            if (Math.Abs(dx) < Scale && Math.Abs(dy) < Scale)
            {
                sites.Add(start);
                return false;
            }

            AddBlock(sites, start, dx < 0 ? -Scale : Scale, dy < 0 ? -Scale : Scale);
            return true;
        }

        internal static bool IsBlock(IReadOnlyList<Tile> sites)
        {
            if (sites == null || sites.Count == 0 || sites.Count > 4 || !Inside(sites[0]))
                return false;

            var expected = new List<Tile>(4);
            for (int dx = -Scale; dx <= Scale; dx += 2 * Scale)
            for (int dy = -Scale; dy <= Scale; dy += 2 * Scale)
            {
                expected.Clear();
                AddBlock(expected, sites[0], dx, dy);
                if (expected.Count != sites.Count)
                    continue;
                int i = 0;
                while (i < sites.Count && expected[i].Equals(sites[i]))
                    i++;
                if (i == sites.Count)
                    return true;
            }
            return false;
        }

        internal static bool AllowsPile(Tile pile, IReadOnlyList<Tile> block, IReadOnlyList<Tile> previousPiles)
        {
            if (!Inside(pile) || pile.X + PileScale > MapSize || pile.Y + PileScale > MapSize)
                return false;

            int guardedWidth = Scale + 2 * WalkwayWidth;
            foreach (Tile site in block)
            {
                var corner = new Tile(site.X - Scale / 2 - WalkwayWidth, site.Y - Scale / 2 - WalkwayWidth);
                if (Overlaps(pile, corner, guardedWidth))
                    return false;
            }
            foreach (Tile other in previousPiles)
                if (Overlaps(pile, other, PileScale))
                    return false;
            return true;
        }

        private static bool Overlaps(Tile pile, Tile corner, int width) =>
            pile.X < corner.X + width && pile.X + PileScale > corner.X &&
            pile.Y < corner.Y + width && pile.Y + PileScale > corner.Y;

        private static void AddBlock(List<Tile> sites, Tile start, int dx, int dy)
        {
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
            {
                var site = new Tile(start.X + x * dx, start.Y + y * dy);
                if (Inside(site))
                    sites.Add(site);
            }
        }

        private static bool Inside(Tile tile) => (uint)tile.X < MapSize && (uint)tile.Y < MapSize;
    }
}
