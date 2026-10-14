using System;
using System.Collections.Generic;
using System.Linq;

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

    internal sealed class WallPlan
    {
        internal readonly SortedDictionary<int, bool> Heights = new SortedDictionary<int, bool>();
        internal readonly Dictionary<int, WallCell> Cells = new Dictionary<int, WallCell>();
        internal string Rejection;
        internal int Anchors, EqualPairs;
        internal bool Accepted => Rejection == null;
    }

    // All choices depend only on the synchronized map, owner and command coordinates.
    // No Unity, native addresses, frame time or local-player state enters this algorithm.
    internal static class WallPolicy
    {
        internal const int MaximumCells = 320800; // Audited native packed tile capacity.
        private static readonly int[] DX = { 0, 1, 0, -1, 1, 1, -1, -1 };
        private static readonly int[] DY = { -1, 0, 1, 0, -1, 1, 1, -1 };

        internal static List<WallCell> Raster(IWallWorld world, int x, int y, int endX, int endY, int maximum)
        {
            var result = new List<WallCell>();
            if (maximum <= 0) return result;
            int length = Math.Max(Math.Abs((long)endX - x), Math.Abs((long)endY - y)) > 800
                ? -1 : (int)Math.Max(Math.Abs((long)endX - x), Math.Abs((long)endY - y));
            if (length < 0) throw new InvalidOperationException("Raster exceeds the native map axis.");
            // Vanilla advances each nonzero coordinate difference by one, then processes
            // the endpoint again. The second endpoint visit cannot create another tile.
            for (int i = 0; i <= length && i < maximum; i++)
            {
                if (!world.TryRead(x, y, out WallCell cell)) throw new InvalidOperationException("Invalid raster coordinate " + x + "," + y);
                result.Add(cell.Copy());
                if (x == endX && y == endY) break;
                x += Math.Sign((long)endX - x); y += Math.Sign((long)endY - y);
            }
            return result;
        }

        internal static List<WallCell> Predict(List<WallCell> raster, int owner, int nativeAvailability = int.MaxValue)
        {
            var added = new List<WallCell>();
            // 6F230 stops when available < alreadyPlaced, not <=. Use the exact
            // Vanilla getter result, including campaign/cheat/remainder branches.
            long limit = Math.Max(0L, (long)nativeAvailability + 1);
            foreach (WallCell old in raster)
            {
                if (added.Count >= limit) break;
                // Native 6F230 skips intact existing walls for MAPPER_CRENAL.
                if (old.IsWall && !old.Damaged) continue;
                var cell = old.Copy(); cell.Owner = owner; cell.IsWall = cell.IsCrenel = true;
                cell.High = ((cell.X ^ cell.Y) & 1) != 0; cell.Damaged = false;
                added.Add(cell);
            }
            return added;
        }

        internal static WallPlan Plan(IWallWorld world, int owner, IList<WallCell> seeds, bool preventDoubleRows)
        {
            var plan = new WallPlan();
            var overlay = new Dictionary<long, WallCell>();
            foreach (WallCell seed in seeds) overlay[Key(seed.X, seed.Y)] = seed;
            var cache = new Dictionary<long, WallCell>();
            Func<int, int, WallCell> read = (x, y) => {
                long key = Key(x, y);
                if (overlay.TryGetValue(key, out WallCell replacement)) return replacement;
                if (cache.TryGetValue(key, out WallCell cached)) return cached;
                if (!world.TryRead(x, y, out WallCell cell)) cell = null;
                cache[key] = cell; return cell;
            };
            if (preventDoubleRows)
            {
                foreach (WallCell seed in seeds)
                    for (int ox = -1; ox <= 0; ox++) for (int oy = -1; oy <= 0; oy++)
                    {
                        WallCell a = read(seed.X + ox, seed.Y + oy), b = read(seed.X + ox + 1, seed.Y + oy);
                        WallCell c = read(seed.X + ox, seed.Y + oy + 1), d = read(seed.X + ox + 1, seed.Y + oy + 1);
                        if (Crenel(a) && Crenel(b) && Crenel(c) && Crenel(d))
                        { plan.Rejection = "double-row 2x2 at " + (seed.X + ox) + "," + (seed.Y + oy); return plan; }
                    }
            }
            var queue = new Queue<WallCell>();
            foreach (WallCell seed in seeds) if (Crenel(seed) && seed.Owner == owner) queue.Enqueue(seed);
            while (queue.Count != 0)
            {
                WallCell cell = queue.Dequeue();
                if (plan.Cells.ContainsKey(cell.Id)) continue;
                if (plan.Cells.Count >= MaximumCells) { plan.Rejection = "component capacity exceeded"; return plan; }
                plan.Cells.Add(cell.Id, cell);
                for (int n = 0; n < 8; n++)
                {
                    WallCell next = read(cell.X + DX[n], cell.Y + DY[n]);
                    if (Crenel(next) && next.Owner == owner && !plan.Cells.ContainsKey(next.Id)) queue.Enqueue(next);
                }
            }
            var all = new Dictionary<int, WallCell>(plan.Cells);
            // Two boundary rings are needed to detect triples crossing ownership borders.
            var edge = plan.Cells.Values.ToArray();
            for (int depth = 0; depth < 2; depth++)
            {
                var nextEdge = new List<WallCell>();
                foreach (WallCell cell in edge) for (int n = 0; n < 8; n++)
                {
                    WallCell next = read(cell.X + DX[n], cell.Y + DY[n]);
                    if (Crenel(next) && !all.ContainsKey(next.Id)) { all.Add(next.Id, next); nextEdge.Add(next); }
                }
                edge = nextEdge.ToArray();
            }
            var variable = new HashSet<int>();
            foreach (WallCell cell in plan.Cells.Values)
            {
                if (cell.Damaged || cell.Structure) continue;
                bool n = Crenel(read(cell.X, cell.Y - 1)), e = Crenel(read(cell.X + 1, cell.Y));
                bool s = Crenel(read(cell.X, cell.Y + 1)), w = Crenel(read(cell.X - 1, cell.Y));
                if ((n && s && !e && !w) || (e && w && !n && !s)) variable.Add(cell.Id);
                else { plan.Heights[cell.Id] = true; plan.Anchors++; }
            }
            var visited = new HashSet<int>();
            foreach (WallCell start in plan.Cells.Values.OrderBy(c => c.Id))
            {
                if (!variable.Contains(start.Id) || visited.Contains(start.Id)) continue;
                WallCell[] neighbors = Cardinal(start, read).Where(c => variable.Contains(c.Id)).ToArray();
                if (neighbors.Length == 2) continue; // Start at a segment endpoint.
                var chain = new List<WallCell>(); WallCell current = start, previous = null;
                while (current != null && visited.Add(current.Id))
                {
                    chain.Add(current);
                    WallCell next = Cardinal(current, read).FirstOrDefault(c => variable.Contains(c.Id) && (previous == null || c.Id != previous.Id));
                    previous = current; current = next;
                }
                WallCell left = Cardinal(chain[0], read).FirstOrDefault(c => !variable.Contains(c.Id));
                WallCell right = Cardinal(chain[chain.Count - 1], read).FirstOrDefault(c => !variable.Contains(c.Id) && (chain.Count != 1 || left == null || c.Id != left.Id));
                if (!Solve(chain, left, right, plan)) { plan.Rejection = "incompatible segment anchors at " + start.X + "," + start.Y; return plan; }
            }
            if (visited.Count != variable.Count) { plan.Rejection = "unresolved straight component"; return plan; }
            bool High(WallCell cell) => plan.Heights.TryGetValue(cell.Id, out bool high) ? high : cell.High;
            foreach (WallCell center in all.Values.OrderBy(c => c.Id))
            {
                if (center.Damaged) continue;
                WallCell[] same = Cardinal(center, read).Where(c => all.ContainsKey(c.Id) && !c.Damaged && High(c) == High(center)).ToArray();
                for (int a = 0; a < same.Length; a++) for (int b = a + 1; b < same.Length; b++)
                    if (plan.Cells.ContainsKey(center.Id) || plan.Cells.ContainsKey(same[a].Id) || plan.Cells.ContainsKey(same[b].Id))
                    { plan.Rejection = "three equal crenels through " + center.X + "," + center.Y; return plan; }
                // Native diagonal placements are isolated cardinal special pieces.
                for (int d = 4; d < 6; d++)
                {
                    WallCell a = read(center.X - DX[d], center.Y - DY[d]), b = read(center.X + DX[d], center.Y + DY[d]);
                    if (Crenel(a) && Crenel(b) && !a.Damaged && !b.Damaged && all.ContainsKey(a.Id) && all.ContainsKey(b.Id) &&
                        High(a) == High(center) && High(b) == High(center) &&
                        (plan.Cells.ContainsKey(center.Id) || plan.Cells.ContainsKey(a.Id) || plan.Cells.ContainsKey(b.Id)))
                    { plan.Rejection = "three equal diagonal crenels through " + center.X + "," + center.Y; return plan; }
                }
                if (plan.Cells.ContainsKey(center.Id)) foreach (WallCell next in Cardinal(center, read))
                    if (center.Id < next.Id && plan.Cells.ContainsKey(next.Id) && High(center) == High(next)) plan.EqualPairs++;
            }
            return plan;
        }

        private sealed class Score
        {
            internal int Pairs, Changes, Previous;
            internal Score(int pairs, int changes, int previous) { Pairs = pairs; Changes = changes; Previous = previous; }
        }
        private static bool Solve(List<WallCell> chain, WallCell left, WallCell right, WallPlan plan)
        {
            bool BoundaryHigh(WallCell c) => c != null && (plan.Heights.TryGetValue(c.Id, out bool h) ? h : c.High);
            bool leftOwn = left != null && plan.Heights.ContainsKey(left.Id), rightOwn = right != null && plan.Heights.ContainsKey(right.Id);
            bool lh = BoundaryHigh(left), rh = BoundaryHigh(right);
            var levels = new List<Score[]>();
            for (int i = 0; i < chain.Count; i++)
            {
                var level = new Score[4];
                for (int value = 0; value < 2; value++)
                {
                    if ((i == 0 && leftOwn && value == 1) || (i == chain.Count - 1 && rightOwn && value == 1)) continue;
                    int change = chain[i].High == (value == 1) ? 0 : 1;
                    if (i == 0)
                    {
                        int run = left != null && lh == (value == 1) ? 2 : 1;
                        level[value * 2 + run - 1] = new Score(run == 2 ? 1 : 0, change, -1);
                    }
                    else for (int prior = 0; prior < 4; prior++)
                    {
                        Score old = levels[i - 1][prior]; if (old == null) continue;
                        bool equal = prior / 2 == value; int run = equal ? prior % 2 + 2 : 1;
                        if (run > 2) continue;
                        int slot = value * 2 + run - 1, pairs = old.Pairs + (equal ? 1 : 0), changes = old.Changes + change;
                        Score best = level[slot];
                        if (best == null || pairs < best.Pairs || (pairs == best.Pairs && changes < best.Changes))
                            level[slot] = new Score(pairs, changes, prior);
                    }
                }
                levels.Add(level);
            }
            int chosen = -1, bestPairs = int.MaxValue, bestChanges = int.MaxValue;
            Score[] last = levels[levels.Count - 1];
            for (int state = 0; state < 4; state++)
            {
                Score score = last[state]; if (score == null) continue;
                bool equal = right != null && rh == (state / 2 == 1);
                if (equal && state % 2 == 1) continue;
                int pairs = score.Pairs + (equal ? 1 : 0);
                if (pairs < bestPairs || (pairs == bestPairs && score.Changes < bestChanges))
                { chosen = state; bestPairs = pairs; bestChanges = score.Changes; }
            }
            if (chosen < 0) return false;
            for (int i = chain.Count - 1; i >= 0; i--) { plan.Heights[chain[i].Id] = chosen / 2 == 1; chosen = levels[i][chosen].Previous; }
            return true;
        }

        private static IEnumerable<WallCell> Cardinal(WallCell cell, Func<int, int, WallCell> read)
        {
            for (int n = 0; n < 4; n++) { WallCell next = read(cell.X + DX[n], cell.Y + DY[n]); if (Crenel(next)) yield return next; }
        }
        private static bool Crenel(WallCell cell) => cell != null && cell.IsWall && cell.IsCrenel;
        private static long Key(int x, int y) => ((long)x << 32) | (uint)y;
    }
}
