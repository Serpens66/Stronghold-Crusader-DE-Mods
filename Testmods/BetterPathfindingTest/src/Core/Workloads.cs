using System;
using System.Collections.Generic;

namespace BetterPathfindingTest.Core
{
    public static class Workloads
    {
        // Diagnostic input generation, not a substitute for measured game orders.
        // Reverse walks prove positive connectivity instead of choosing arbitrary
        // valid-but-isolated coordinates. The bounded sample does not prove negatives.
        public static List<RouteQuery> Create(GridSnapshot map, UnitSeed[] units, int count, string family, uint seed = 731902)
        {
            var result = new List<RouteQuery>(); if (count < 1) return result;
            int target = -1;
            foreach (UnitSeed unit in units) if (unit.Player == map.Player && map.IsValid(unit.Node) && Incoming(map, unit.Node)) { target = unit.Node; break; }
            if (target < 0) for (int n = 0; n < map.Count; n++) if (map.IsValid(n) && Incoming(map, n)) { target = n; break; }
            if (target < 0) return result;
            bool wetRequired = family == "moat-required";
            var template = new RouteQuery { Target = target };
            int states = wetRequired ? 2 : 1, capacity = Math.Min(map.Count * states, 8192);
            var seen = new bool[map.Count * states]; var queue = new int[capacity]; var depth = new int[capacity]; var starts = new List<int>();
            int read = 0, write = 1; queue[0] = target; seen[target] = true;
            while (read < write)
            {
                int state = queue[read], node = state % map.Count, distance = depth[read++];
                if (distance > 0 && (!wetRequired || state >= map.Count)) starts.Add(node);
                if (distance >= 1500) continue;
                for (int d = 0; d < 8 && write < capacity; d++)
                {
                    int pred = map.Neighbor(node, (d + 4) & 7);
                    if (!map.IsValid(pred) || !map.Edge(pred, d, template, out _, out bool wet, out _)) continue;
                    int next = pred + (wetRequired && (wet || state >= map.Count) ? map.Count : 0);
                    if (seen[next]) continue; seen[next] = true; queue[write] = next; depth[write++] = distance + 1;
                }
            }
            if (starts.Count == 0) return result;
            var targets = new List<int> { target };
            if (family == "formation" || family == "distributed-profiles")
            {
                var visited = new HashSet<int> { target }; var open = new Queue<int>(); open.Enqueue(target);
                int radiusLimit = family == "formation" ? Math.Max(2, (int)Math.Ceiling(Math.Sqrt(count) / 2)) : 100;
                while (open.Count != 0 && targets.Count < (family == "formation" ? count : 2048))
                {
                    int node = open.Dequeue();
                    for (int d = 0; d < 8; d++) if (map.Edge(node, d, template, out int next, out _, out _) && map.IsValid(next) && visited.Add(next))
                    {
                        int radius = Math.Max(Math.Abs(next % map.Width - target % map.Width), Math.Abs(next / map.Width - target / map.Width));
                        if (radius > radiusLimit) continue;
                        targets.Add(next); open.Enqueue(next);
                    }
                }
            }
            for (int i = 0; i < count; i++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                int start = starts[(int)(seed % (uint)starts.Count)];
                var q = new RouteQuery { Start = start, Target = targets[i % targets.Count], GroundCost = 1, MoatCost = 7, RequireMoat = wetRequired };
                if (family == "distributed-profiles") { q.GroundCost = 1 + i % 5; q.MoatCost = q.GroundCost + 6; }
                result.Add(q);
            }
            return result;
        }
        private static bool Incoming(GridSnapshot map, int node)
        { var q = new RouteQuery(); for (int d = 0; d < 8; d++) { int pred = map.Neighbor(node, (d + 4) & 7); if (map.IsValid(pred) && map.Edge(pred, d, q, out _, out _, out _)) return true; } return false; }
        public static GridSnapshot Synthetic(int size, bool moat)
        {
            var valid = new byte[size * size]; var words = new uint[valid.Length];
            for (int n = 0; n < valid.Length; n++) { valid[n] = 1; for (int d = 0; d < 8; d++) { int x = n % size + GridSnapshot.Dx[d], y = n / size + GridSnapshot.Dy[d]; if ((uint)x >= size || (uint)y >= size) continue; int next = y * size + x; words[n] |= 1U << d; if (moat && (n % size == size / 2 || next % size == size / 2)) words[n] |= 1U << (8 + d); } }
            return new GridSnapshot(size, size, valid, words, 1);
        }
        public static List<RouteQuery> SyntheticBatch(GridSnapshot map, int count, bool moat)
        {
            var queries = new List<RouteQuery>();
            for (int i = 0; i < count; i++) queries.Add(new RouteQuery { Start = (i / 100 % map.Height) * map.Width + i % Math.Min(100, map.Width / 4), Target = (map.Height / 2) * map.Width + map.Width - 2, RequireMoat = moat });
            return queries;
        }
    }
}
