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

        // Every raster cell Vanilla could still turn into a crenel. The actual-placement
        // quota is deliberately ignored: admission checks the whole affordable-or-not line,
        // so it can only be stricter than execution, never weaker.
        internal static List<WallCell> Predict(List<WallCell> raster, int owner)
        {
            var added = new List<WallCell>();
            var seen = new HashSet<int>();
            foreach (WallCell old in raster)
            {
                // Native 6F230 skips intact existing walls for MAPPER_CRENAL.
                if ((old.IsWall && !old.Damaged) || !seen.Add(old.Id)) continue;
                var cell = old.Copy(); cell.Owner = owner; cell.IsWall = cell.IsCrenel = true;
                cell.High = Parity(cell.X, cell.Y); cell.Damaged = false; cell.Structure = false;
                added.Add(cell);
            }
            return added;
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

        // Diagonal rows (screen-horizontal walls) consist of isolated pieces, which Vanilla makes
        // high. Measured in game: a dragged diagonal row is walkable only by stale path edges, a
        // piece-by-piece one not at all. Managed diagonal pieces alternate along the diagonal.
        internal static bool DiagonalHigh(int x) => (x & 1) != 0;

        internal static bool IsDiagonalPiece(Func<int, int, WallCell> read, WallCell cell) =>
            Crenel(cell) && !cell.Structure && CardinalMask(read, cell.X, cell.Y) == 0 && DiagonalMask(read, cell.X, cell.Y) != 0;

        // Straight middles (two opposite neighbours) and endpoints (one neighbour). Native
        // renders all other shapes as special pieces and permanently ORs high into them.
        internal static bool IsLine(int mask) => mask == 0b0101 || mask == 0b1010 || mask == 1 || mask == 2 || mask == 4 || mask == 8;

        // Special piece: corner, junction or isolated crenel. Vanilla makes it high and every
        // high crenel is unwalkable (0xD86F0 builds no edge to or from it).
        internal static bool IsSpecial(Func<int, int, WallCell> read, WallCell cell) =>
            Crenel(cell) && !cell.Structure && !IsLine(CardinalMask(read, cell.X, cell.Y));

        // Desired logical height, or null when Vanilla's own height must stay untouched.
        internal static bool? TargetHigh(Func<int, int, WallCell> read, WallCell cell)
        {
            if (!Crenel(cell) || cell.Damaged || cell.Structure) return null;
            int mask = CardinalMask(read, cell.X, cell.Y);
            if (IsLine(mask)) { LineRun run = Run(read, cell, mask); return run.High(run.IndexOf(cell.Id)); }
            if (mask == 0 && DiagonalMask(read, cell.X, cell.Y) != 0) return DiagonalHigh(cell.X);
            return null;
        }

        // A run is the maximal chain of straight pieces (middles and endpoints) on one axis. Each end
        // is free (no crenel) or a special piece (corner, junction, crossing), which Vanilla always
        // keeps high. Pieces next to a special are low and the run alternates from there; with
        // specials at both ends the lower-coordinate end anchors it, so an even run ends with a high
        // piece next to the other special (user decision 2026-11-25). Runs without a special keep
        // the map parity. Everything follows from the current map, never from the build order.
        internal sealed class LineRun
        {
            internal readonly List<WallCell> Members = new List<WallCell>(); // from the lower-coordinate end
            internal bool LowSpecial, HighSpecial;
            internal int IndexOf(int id) { for (int i = 0; i < Members.Count; i++) if (Members[i].Id == id) return i; return -1; }
            internal bool High(int index)
            {
                if (LowSpecial) return (index + 1) % 2 == 0;
                if (HighSpecial) return (Members.Count - index) % 2 == 0;
                return Parity(Members[index].X, Members[index].Y);
            }
        }

        // Walks the run through a straight piece; at most one native map axis in each direction.
        internal static LineRun Run(Func<int, int, WallCell> read, WallCell cell, int mask)
        {
            bool vertical = (mask & 0b0101) != 0; // N or S neighbour: the run follows y
            int dx = vertical ? 0 : 1, dy = vertical ? 1 : 0;
            var run = new LineRun();
            var low = new List<WallCell>();
            run.LowSpecial = Walk(read, cell, -dx, -dy, vertical, low);
            for (int i = low.Count - 1; i >= 0; i--) run.Members.Add(low[i]);
            run.Members.Add(cell);
            run.HighSpecial = Walk(read, cell, dx, dy, vertical, run.Members);
            return run;
        }

        // Adds the straight pieces in one direction; true when the run ends at a special piece.
        private static bool Walk(Func<int, int, WallCell> read, WallCell from, int dx, int dy, bool vertical, List<WallCell> into)
        {
            for (int step = 1; step <= 800; step++)
            {
                WallCell next = read(from.X + dx * step, from.Y + dy * step);
                if (!Crenel(next)) return false;
                int mask = CardinalMask(read, next.X, next.Y);
                bool sameAxis = vertical ? (mask & 0b1010) == 0 : (mask & 0b0101) == 0;
                if (next.Structure || !IsLine(mask) || !sameAxis) return true;
                into.Add(next);
            }
            return false;
        }

        internal static Func<int, int, WallCell> Reader(IWallWorld world, IEnumerable<WallCell> overlay = null)
        {
            var replaced = new Dictionary<long, WallCell>();
            if (overlay != null) foreach (WallCell cell in overlay) replaced[Key(cell.X, cell.Y)] = cell;
            var cache = new Dictionary<long, WallCell>();
            return (x, y) =>
            {
                long key = Key(x, y);
                if (replaced.TryGetValue(key, out WallCell value)) return value;
                if (cache.TryGetValue(key, out WallCell cached)) return cached;
                if (!world.TryRead(x, y, out WallCell cell)) cell = null;
                cache[key] = cell; return cell;
            };
        }

        // Returns null when the predicted construction keeps every wall enterable, otherwise
        // the first violated rule. Only shapes containing a changed cell are considered, so
        // pre-existing map or save walls never block an unrelated command.
        internal static string Violation(IWallWorld world, IList<WallCell> added, Func<int, bool> managed, bool heightsManaged = true)
        {
            if (added.Count == 0) return null;
            Func<int, int, WallCell> read = Reader(world, added);
            var isNew = new HashSet<int>();
            foreach (WallCell cell in added) isNew.Add(cell.Id);
            // Double rows: any filled 2x2 block makes the whole band special and high.
            foreach (WallCell seed in added)
                for (int ox = -1; ox <= 0; ox++) for (int oy = -1; oy <= 0; oy++)
                {
                    int x = seed.X + ox, y = seed.Y + oy;
                    if (Crenel(read(x, y)) && Crenel(read(x + 1, y)) && Crenel(read(x, y + 1)) && Crenel(read(x + 1, y + 1)))
                        return "double-row 2x2 at " + x + "," + y;
                }
            // Diagonal rows are allowed: managed diagonal pieces alternate high/low (TargetHigh).
            // Chains of three cardinally connected special pieces (dense zigzag, stairs, clustered
            // junctions) leave several adjacent high tiles a siege tower can dock to but not
            // unload onto. A cell's shape changes only for new cells and their cardinal
            // neighbours, so any new chain contains one of those that is newly special.
            // Pre-existing chains (e.g. map-designer walls) never block an extension.
            Func<int, int, WallCell> before = Reader(world);
            var affected = new Dictionary<int, WallCell>();
            foreach (WallCell cell in added)
            {
                affected[cell.Id] = cell;
                for (int n = 0; n < 4; n++) { WallCell next = read(cell.X + DX[n], cell.Y + DY[n]); if (Crenel(next)) affected[next.Id] = next; }
            }
            foreach (WallCell cell in affected.Values)
            {
                if (!IsSpecial(read, cell) || (!isNew.Contains(cell.Id) && IsSpecial(before, cell))) continue;
                var specials = new List<WallCell>();
                for (int n = 0; n < 4; n++) { WallCell next = read(cell.X + DX[n], cell.Y + DY[n]); if (IsSpecial(read, next)) specials.Add(next); }
                // As middle: two special neighbours. As end: a special neighbour that has another one.
                if (specials.Count >= 2) return "three special crenels through " + cell.X + "," + cell.Y;
                foreach (WallCell middle in specials)
                    for (int n = 0; n < 4; n++)
                    {
                        WallCell far = read(middle.X + DX[n], middle.Y + DY[n]);
                        if (far != null && far.Id != cell.Id && IsSpecial(read, far)) return "three special crenels through " + middle.X + "," + middle.Y;
                    }
            }
            // Runs of four or more connected high crenels (e.g. stairs with two-tile steps)
            // leave a siege tower front without a low tile. Vanilla checkerboard corners and
            // junctions produce at most three. A new run must contain a cell that becomes high
            // now; runs of tiles that are already high (e.g. designer walls) are not new.
            // With managed heights a new or changed corner shifts the phase of whole straight
            // runs (LineRun), so their members can become high far from the command as well.
            if (heightsManaged)
                foreach (WallCell seed in new List<WallCell>(affected.Values))
                    for (int n = -1; n < 4; n++)
                    {
                        WallCell cell = n < 0 ? seed : read(seed.X + DX[n], seed.Y + DY[n]);
                        if (!Crenel(cell)) continue;
                        int mask = CardinalMask(read, cell.X, cell.Y);
                        if (!IsLine(mask)) continue;
                        foreach (WallCell member in Run(read, cell, mask).Members) affected[member.Id] = member;
                    }
            foreach (WallCell cell in affected.Values)
            {
                if (!PredictedHigh(read, cell, isNew, managed, heightsManaged) || (!isNew.Contains(cell.Id) && cell.High)) continue;
                var arms = new List<List<WallCell>>();
                CollectArms(read, cell, new List<WallCell>(), arms, isNew, managed, heightsManaged);
                foreach (List<WallCell> a in arms)
                {
                    if (a.Count >= 3) return "four high crenels through " + cell.X + "," + cell.Y;
                    foreach (List<WallCell> b in arms)
                        if (a.Count + b.Count >= 3 && Disjoint(a, b)) return "four high crenels through " + cell.X + "," + cell.Y;
                }
            }
            return null;
        }

        // Height after this command and the mod's own normalization: specials are forced high
        // by the renderer, new and managed lines follow parity, foreign/map walls keep theirs.
        // Without height adjustment (heightsManaged=false) the Vanilla renderer decides: every
        // non-middle piece (endpoint, diagonal, isolated, corner, junction) is ORed high and
        // stays high; only straight middles keep placement parity or their current height.
        internal static bool PredictedHigh(Func<int, int, WallCell> read, WallCell cell, ISet<int> isNew, Func<int, bool> managed, bool heightsManaged = true)
        {
            if (!Crenel(cell) || cell.Structure) return false;
            int mask = CardinalMask(read, cell.X, cell.Y);
            if (!heightsManaged)
            {
                if (mask != 0b0101 && mask != 0b1010) return true;
                return isNew.Contains(cell.Id) && !cell.Damaged ? Parity(cell.X, cell.Y) : cell.High;
            }
            bool ours = isNew.Contains(cell.Id) || (managed != null && managed(cell.Id));
            if (!IsLine(mask))
                return !(ours && !cell.Damaged && mask == 0 && DiagonalMask(read, cell.X, cell.Y) != 0 && !DiagonalHigh(cell.X));
            if (cell.Damaged) return cell.High;
            if (!(isNew.Contains(cell.Id) || (managed != null && managed(cell.Id)))) return cell.High;
            LineRun run = Run(read, cell, mask);
            return run.High(run.IndexOf(cell.Id));
        }

        // All simple paths of up to three further high cells starting next to origin.
        private static void CollectArms(Func<int, int, WallCell> read, WallCell origin, List<WallCell> path,
            List<List<WallCell>> arms, ISet<int> isNew, Func<int, bool> managed, bool heightsManaged)
        {
            if (path.Count == 3) return;
            WallCell from = path.Count == 0 ? origin : path[path.Count - 1];
            for (int n = 0; n < 4; n++)
            {
                WallCell next = read(from.X + DX[n], from.Y + DY[n]);
                if (next == null || next.Id == origin.Id || path.Exists(c => c.Id == next.Id) ||
                    !PredictedHigh(read, next, isNew, managed, heightsManaged)) continue;
                var extended = new List<WallCell>(path) { next };
                arms.Add(extended);
                CollectArms(read, origin, extended, arms, isNew, managed, heightsManaged);
            }
        }

        private static bool Disjoint(List<WallCell> a, List<WallCell> b)
        {
            foreach (WallCell x in a) foreach (WallCell y in b) if (x.Id == y.Id) return false;
            return true;
        }

        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
