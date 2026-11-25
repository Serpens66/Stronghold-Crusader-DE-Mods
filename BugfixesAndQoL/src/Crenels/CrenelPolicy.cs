using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.Crenels
{
    internal sealed class CrenelCell
    {
        internal int Id, X, Y, Owner;
        internal bool IsWall, IsCrenel, High, Damaged, Structure;
        internal CrenelCell Copy() => (CrenelCell)MemberwiseClone();
        // Identity of a managed tile; heights are never remembered, they follow from the map.
        internal CrenelCell Identity() => new CrenelCell { Id = Id, X = X, Y = Y, Owner = Owner };
    }

    internal interface ICrenelWorld
    {
        bool TryRead(int x, int y, out CrenelCell cell);
    }

    // All decisions depend only on the synchronized map, owner and command coordinates.
    // No Unity, native addresses, frame time, local-player state or remembered heights.
    internal static class CrenelPolicy
    {
        internal const int MaximumCells = 320800; // Audited native packed tile capacity.
        // Cardinal N, E, S, W first, then the diagonals.
        internal static readonly int[] DX = { 0, 1, 0, -1, 1, 1, -1, -1 };
        internal static readonly int[] DY = { -1, 0, 1, 0, -1, 1, 1, -1 };

        // Tiles of one wall command in Vanilla's raster order (6F230).
        internal static List<CrenelCell> Raster(ICrenelWorld world, int x, int y, int endX, int endY, int maximum)
        {
            var result = new List<CrenelCell>();
            if (maximum <= 0) return result;
            long span = Math.Max(Math.Abs((long)endX - x), Math.Abs((long)endY - y));
            if (span > 800) throw new InvalidOperationException("Raster exceeds the native map axis.");
            // Vanilla advances each nonzero coordinate difference by one, then processes
            // the endpoint again. The second endpoint visit cannot create another tile.
            for (int i = 0; i <= span && i < maximum; i++)
            {
                if (!world.TryRead(x, y, out CrenelCell cell)) throw new InvalidOperationException("Invalid raster coordinate " + x + "," + y);
                result.Add(cell.Copy());
                if (x == endX && y == endY) break;
                x += Math.Sign((long)endX - x); y += Math.Sign((long)endY - y);
            }
            return result;
        }

        // Map checkerboard: Vanilla placement 6F230/6CB20 sets high exactly for odd x+y.
        internal static bool Parity(int x, int y) => ((x ^ y) & 1) != 0;

        internal static bool Crenel(CrenelCell cell) => cell != null && cell.IsWall && cell.IsCrenel;

        // Pieces with a diagonal crenel neighbour but no axis neighbour (diagonal rows, screen-horizontal).
        // Vanilla's neighbour table has no straight diagonal (axis neighbours only for directions 0/2/4/6).
        private static bool IsDiagonalPiece(Func<int, int, CrenelCell> read, CrenelCell cell)
        {
            if (cell.Structure) return false;
            for (int n = 0; n < 4; n++) if (Crenel(read(cell.X + DX[n], cell.Y + DY[n]))) return false;
            for (int n = 4; n < 8; n++) if (Crenel(read(cell.X + DX[n], cell.Y + DY[n]))) return true;
            return false;
        }

        // Target height, or null when Vanilla's own height stays untouched (damaged, building). Every
        // intact crenel follows the map checkerboard, so units cross any shape diagonally from low to low.
        // Diagonal pieces alternate along x instead: map parity would make a whole diagonal equally high
        // (x+y changes by 0 or 2 per step), and an all-high diagonal cannot be crossed.
        internal static bool? TargetHigh(Func<int, int, CrenelCell> read, CrenelCell cell)
        {
            if (!Crenel(cell) || cell.Damaged || cell.Structure) return null;
            if (IsDiagonalPiece(read, cell)) return (cell.X & 1) != 0;
            return Parity(cell.X, cell.Y);
        }

        // Cached reads of one consistent map state (one normalization pass).
        internal static Func<int, int, CrenelCell> Reader(ICrenelWorld world)
        {
            var cache = new Dictionary<long, CrenelCell>();
            return (x, y) =>
            {
                long key = ((long)x << 32) | (uint)y;
                if (cache.TryGetValue(key, out CrenelCell cached)) return cached;
                if (!world.TryRead(x, y, out CrenelCell cell)) cell = null;
                cache[key] = cell; return cell;
            };
        }
    }
}
