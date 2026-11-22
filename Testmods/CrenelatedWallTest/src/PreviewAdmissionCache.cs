using System;
using System.Collections.Generic;

namespace CrenelatedWallTest
{
    // One last request, not a frame/tick cache. Every raw read (including empty
    // boundary cells and failed reads) and every consulted managed flag is rechecked.
    internal sealed class PreviewAdmissionCache : IWallWorld
    {
        private sealed class Read
        {
            internal int X, Y;
            internal bool Found, Managed;
            internal WallCell Cell;
        }
        private readonly Dictionary<long, Read> reads = new Dictionary<long, Read>();
        private IWallWorld world;
        private Func<int, bool> managed;
        private int owner, x, y, endX, endY, maximum;
        private WallVerdict result;
        internal void Clear() { result = null; reads.Clear(); world = null; managed = null; }
        internal WallVerdict Evaluate(IWallWorld source, int player, int bx, int by, int ex, int ey, int max, Func<int, bool> isManaged)
        {
            if (result != null && ReferenceEquals(world, source) && owner == player && x == bx && y == by &&
                endX == ex && endY == ey && maximum == max && Matches(source, isManaged)) return result;
            Clear(); world = source; managed = isManaged;
            owner = player; x = bx; y = by; endX = ex; endY = ey; maximum = max;
            // Publish the cached result only after a complete successful calculation.
            result = WallAdmission.Evaluate(this, player, WallPolicy.Raster(this, bx, by, ex, ey, max), isManaged);
            return result;
        }
        public bool TryRead(int rx, int ry, out WallCell cell)
        {
            bool found = world.TryRead(rx, ry, out cell);
            reads[((long)rx << 32) | (uint)ry] = new Read { X = rx, Y = ry, Found = found, Cell = found ? cell.Copy() : null,
                Managed = found && managed != null && managed(cell.Id) };
            return found;
        }
        private bool Matches(IWallWorld source, Func<int, bool> isManaged)
        {
            foreach (Read read in reads.Values)
            {
                bool found = source.TryRead(read.X, read.Y, out WallCell cell);
                if (found != read.Found) return false;
                if (!found) continue;
                WallCell old = read.Cell;
                if (cell.Id != old.Id || cell.X != old.X || cell.Y != old.Y || cell.Owner != old.Owner ||
                    cell.IsWall != old.IsWall || cell.IsCrenel != old.IsCrenel || cell.High != old.High ||
                    cell.Damaged != old.Damaged || cell.Structure != old.Structure) return false;
                if ((isManaged != null && isManaged(cell.Id)) != read.Managed) return false;
            }
            return true;
        }
    }
}
