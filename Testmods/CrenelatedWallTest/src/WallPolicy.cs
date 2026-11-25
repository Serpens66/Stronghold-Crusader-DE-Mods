using System;
using System.Collections.Generic;

namespace CrenelatedWallTest
{
    internal sealed class WallCell
    {
        internal int Id, X, Y, Owner;
        internal bool IsWall, IsCrenel, High, Damaged, Structure;
        internal WallCell Copy() => (WallCell)MemberwiseClone();
    }

    internal interface IWallWorld
    {
        bool TryRead(int x, int y, out WallCell cell);
    }

    // All decisions depend only on the synchronized map, owner and command coordinates.
    // No Unity, native addresses, frame time, local-player state or remembered heights.
    internal static class WallPolicy
    {
        internal const int MaximumCells = 320800; // Audited native packed tile capacity.
        // Cardinal N, E, S, W first, then the diagonals.
        private static readonly int[] DX = { 0, 1, 0, -1, 1, 1, -1, -1 };
        private static readonly int[] DY = { -1, 0, 1, 0, -1, 1, 1, -1 };

        internal static List<WallCell> Raster(IWallWorld world, int x, int y, int endX, int endY, int maximum)
        {
            var result = new List<WallCell>();
            if (maximum <= 0) return result;
            long span = Math.Max(Math.Abs((long)endX - x), Math.Abs((long)endY - y));
            if (span > 800) throw new InvalidOperationException("Raster exceeds the native map axis.");
            // Vanilla advances each nonzero coordinate difference by one, then processes
            // the endpoint again. The second endpoint visit cannot create another tile.
            for (int i = 0; i <= span && i < maximum; i++)
            {
                if (!world.TryRead(x, y, out WallCell cell)) throw new InvalidOperationException("Invalid raster coordinate " + x + "," + y);
                result.Add(cell.Copy());
                if (x == endX && y == endY) break;
                x += Math.Sign((long)endX - x); y += Math.Sign((long)endY - y);
            }
            return result;
        }

        // Vanilla placement 6F230/6CB20 sets high exactly for odd x+y.
        internal static bool Parity(int x, int y) => ((x ^ y) & 1) != 0;

        internal static bool Crenel(WallCell cell) => cell != null && cell.IsWall && cell.IsCrenel;

        // Bits 0..3 = N, E, S, W, matching the cardinal mask read by native 5C2D0 (owner-independent).
        internal static int CardinalMask(Func<int, int, WallCell> read, int x, int y)
        {
            int mask = 0;
            for (int n = 0; n < 4; n++) if (Crenel(read(x + DX[n], y + DY[n]))) mask |= 1 << n;
            return mask;
        }

        // Bits 0..3 = crenels at (+1,-1), (+1,+1), (-1,+1), (-1,-1). Vanilla has no straight
        // diagonal: the neighbour table (TileManager+row*32) uses axis neighbours for 0/2/4/6.
        internal static int DiagonalMask(Func<int, int, WallCell> read, int x, int y)
        {
            int mask = 0;
            for (int n = 4; n < 8; n++) if (Crenel(read(x + DX[n], y + DY[n]))) mask |= 1 << (n - 4);
            return mask;
        }

        // Diagonal rows (screen-horizontal walls) consist of pieces without axis neighbours. Map
        // parity would give a whole diagonal the same height (x+y changes by 0 or 2 per step), and an
        // all-high diagonal cannot be crossed; so these pieces alternate along x instead.
        internal static bool DiagonalHigh(int x) => (x & 1) != 0;

        internal static bool IsDiagonalPiece(Func<int, int, WallCell> read, WallCell cell) =>
            Crenel(cell) && !cell.Structure && CardinalMask(read, cell.X, cell.Y) == 0 && DiagonalMask(read, cell.X, cell.Y) != 0;

        // Desired logical height, or null when Vanilla's own height must stay untouched. Every intact
        // crenel follows the map checkerboard, so units cross any shape diagonally from low to low
        // (in-game proof 2026-11-25); pieces without axis neighbours alternate along x.
        internal static bool? TargetHigh(Func<int, int, WallCell> read, WallCell cell)
        {
            if (!Crenel(cell) || cell.Damaged || cell.Structure) return null;
            if (IsDiagonalPiece(read, cell)) return DiagonalHigh(cell.X);
            return Parity(cell.X, cell.Y);
        }

        // Cached reads of one consistent map state (one normalization pass).
        internal static Func<int, int, WallCell> Reader(IWallWorld world)
        {
            var cache = new Dictionary<long, WallCell>();
            return (x, y) =>
            {
                long key = Key(x, y);
                if (cache.TryGetValue(key, out WallCell cached)) return cached;
                if (!world.TryRead(x, y, out WallCell cell)) cell = null;
                cache[key] = cell; return cell;
            };
        }

        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
