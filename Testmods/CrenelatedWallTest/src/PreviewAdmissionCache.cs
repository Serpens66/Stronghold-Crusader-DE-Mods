using System.Collections.Generic;

namespace CrenelatedWallTest
{
    // One last request, not a frame/tick cache. Every raw read (including empty
    // boundary cells and failed reads) and every consulted preference is rechecked.
    internal sealed class PreviewAdmissionCache : IWallWorld
    {
        private sealed class Read
        {
            internal int X, Y, PreferredOwner;
            internal bool Found, HasPreference, PreferredHigh;
            internal WallCell Cell;
        }
        private readonly Dictionary<long, Read> reads = new Dictionary<long, Read>();
        private IWallWorld world;
        private IReadOnlyDictionary<int, WallCell> preferred;
        private int owner, x, y, endX, endY, quota;
        private WallPlan result;
        internal void Clear() { result = null; reads.Clear(); world = null; preferred = null; }
        internal WallPlan Evaluate(IWallWorld source, int player, int bx, int by, int ex, int ey, int available,
            IReadOnlyDictionary<int, WallCell> assignments)
        {
            if (result != null && ReferenceEquals(world, source) && owner == player && x == bx && y == by &&
                endX == ex && endY == ey && quota == available && Matches(source, assignments)) return result;
            Clear(); world = source; preferred = assignments;
            owner = player; x = bx; y = by; endX = ex; endY = ey; quota = available;
            // Publish the cached result only after a complete successful calculation.
            result = WallAdmission.Evaluate(this, player, WallPolicy.Raster(this, bx, by, ex, ey, available), available, assignments);
            return result;
        }
        public bool TryRead(int rx, int ry, out WallCell cell)
        {
            bool found = world.TryRead(rx, ry, out cell);
            var read = new Read { X = rx, Y = ry, Found = found, Cell = found ? cell.Copy() : null };
            if (found && preferred != null && preferred.TryGetValue(cell.Id, out WallCell saved))
            { read.HasPreference = true; read.PreferredOwner = saved.Owner; read.PreferredHigh = saved.High; }
            reads[((long)rx << 32) | (uint)ry] = read;
            return found;
        }
        private bool Matches(IWallWorld source, IReadOnlyDictionary<int, WallCell> assignments)
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
                WallCell saved = null;
                bool has = assignments != null && assignments.TryGetValue(cell.Id, out saved);
                if (has != read.HasPreference || (has && (saved.Owner != read.PreferredOwner || saved.High != read.PreferredHigh))) return false;
            }
            return true;
        }
    }
}
