using System;
using System.Collections.Generic;
using GroupPathProbe.Core;

// Diagnostic group orders, not recorded game commands. Every start is proven to reach
// its group anchor and every slot is strongly connected with that anchor, so all
// queries are positive. Starts are a compact cluster; slots a compact formation.
internal sealed class GroupLoad
{
    internal string Name; internal GridSnapshot Map;
    internal readonly List<RouteQuery> Queries = new List<RouteQuery>();
    internal readonly List<int> GroupOf = new List<int>();
    internal readonly List<int> Anchors = new List<int>();
    internal int Groups => Anchors.Count;
}

internal static class GroupWorkloads
{
    internal static bool[] Reach(GridSnapshot map, int source, bool reverse)
    {
        var seen = new bool[map.Count]; var queue = new int[map.Count]; int read = 0, write = 0; var q = new RouteQuery();
        seen[source] = true; queue[write++] = source;
        while (read < write)
        {
            int node = queue[read++];
            for (int d = 0; d < 8; d++)
            {
                int next = map.Neighbor(node, reverse ? (d + 4) & 7 : d);
                if (!map.IsValid(next) || seen[next] || !map.Edge(reverse ? next : node, d, q, out _, out _, out _)) continue;
                seen[next] = true; queue[write++] = next;
            }
        }
        return seen;
    }
    // Nearest nodes around center in plain grid order, restricted to the allowed set.
    private static List<int> Cluster(GridSnapshot map, int center, int count, Func<int, bool> allowed)
    {
        var result = new List<int>(); var seen = new HashSet<int> { center }; var open = new Queue<int>(); open.Enqueue(center);
        while (open.Count != 0 && result.Count < count)
        {
            int node = open.Dequeue(); if (allowed(node)) result.Add(node);
            for (int d = 0; d < 8; d++) { int next = map.Neighbor(node, d); if (next >= 0 && map.IsValid(next) && seen.Add(next) && seen.Count < 200000) open.Enqueue(next); }
        }
        return result;
    }
    private static int Chebyshev(GridSnapshot map, int a, int b) => Math.Max(Math.Abs(a % map.Width - b % map.Width), Math.Abs(a / map.Width - b / map.Width));
    // Adds one group; returns false when the requested group cannot be formed.
    internal static bool AddGroup(GroupLoad load, int center, int anchor, int size, long ground, long moat)
    {
        GridSnapshot map = load.Map;
        bool[] toAnchor = Reach(map, anchor, true), fromAnchor = Reach(map, anchor, false);
        if (!toAnchor[center]) return false;
        List<int> starts = Cluster(map, center, size, n => toAnchor[n]);
        List<int> slots = Cluster(map, anchor, size, n => toAnchor[n] && fromAnchor[n]);
        if (starts.Count < size || slots.Count < size) return false;
        int group = load.Anchors.Count; load.Anchors.Add(anchor);
        for (int i = 0; i < size; i++) { load.Queries.Add(new RouteQuery { Start = starts[i], Target = slots[i], GroundCost = ground, MoatCost = moat }); load.GroupOf.Add(group); }
        return true;
    }
    // Far anchor (>= 200 Chebyshev) reachable from the center, chosen deterministically.
    private static int FarAnchor(GridSnapshot map, int center, Random random)
    {
        bool[] forward = Reach(map, center, false); var candidates = new List<int>(); int best = center, bestDistance = -1;
        for (int n = 0; n < map.Count; n++) if (forward[n])
        {
            int distance = Chebyshev(map, n, center); if (distance >= 200) candidates.Add(n);
            if (distance > bestDistance) { bestDistance = distance; best = n; }
        }
        return candidates.Count != 0 ? candidates[random.Next(candidates.Count)] : best;
    }
    private static List<int> Centers(GridSnapshot map, UnitSeed[] units, Random random, int count)
    {
        var result = new List<int>();
        foreach (UnitSeed unit in units) if (unit.Player == map.Player && map.IsValid(unit.Node) && (map.Word(unit.Node) & 255) != 0 && !result.Contains(unit.Node)) result.Add(unit.Node);
        for (int i = result.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int t = result[i]; result[i] = result[j]; result[j] = t; }
        for (int guard = 0; result.Count < count && guard < 100000; guard++) { int n = random.Next(map.Count); if (map.IsValid(n) && (map.Word(n) & 255) != 0) result.Add(n); }
        return result;
    }
    internal static List<GroupLoad> Captured(GridSnapshot map, UnitSeed[] units)
    {
        var loads = new List<GroupLoad>(); var random = new Random(529117);
        List<int> centers = Centers(map, units, random, 400); int cursor = 0;
        foreach (int size in new[] { 16, 64, 256, 1024 })
        {
            var load = new GroupLoad { Name = "group-" + size, Map = map };
            while (load.Groups == 0 && cursor < centers.Count) { int c = centers[cursor++]; AddGroup(load, c, FarAnchor(map, c, random), size, 1, 7); }
            loads.Add(load);
        }
        var mix = new GroupLoad { Name = "group-mix-10000", Map = map };
        while (mix.Queries.Count < 10000 && cursor < centers.Count) { int c = centers[cursor++]; bool odd = (mix.Groups & 1) != 0; AddGroup(mix, c, FarAnchor(map, c, random), 250, odd ? 3 : 1, odd ? 9 : 7); }
        loads.Add(mix);
        // Worker trips: few shared goals, many scattered starts (one group per goal).
        var trips = new GroupLoad { Name = "worker-trips-10000", Map = map };
        for (int goal = 0; goal < 8 && cursor < centers.Count; cursor++)
        {
            int target = centers[cursor]; bool[] toTarget = Reach(map, target, true); var pool = new List<int>();
            for (int n = 0; n < map.Count; n++) if (toTarget[n] && n != target && Chebyshev(map, n, target) >= 40) pool.Add(n);
            if (pool.Count < 1250) continue;
            int group = trips.Anchors.Count; trips.Anchors.Add(target); goal++;
            for (int i = 0; i < 1250; i++) { trips.Queries.Add(new RouteQuery { Start = pool[random.Next(pool.Count)], Target = target, GroundCost = 1, MoatCost = 7 }); trips.GroupOf.Add(group); }
        }
        loads.Add(trips);
        return loads;
    }
    // Open 800x800 field split by a moat strip; the strip becomes a wall near the bottom
    // with one ground gap. Northern groups profit from crossing the moat, southern ones
    // from the gap, so moat decisions differ inside some groups.
    internal static GridSnapshot MoatMap()
    {
        const int size = 800; var valid = new byte[size * size]; var words = new uint[valid.Length];
        bool Strip(int x) => x >= 396 && x <= 404;
        bool Wall(int x, int y) => Strip(x) && y >= 700 && (y < 780 || y > 790);
        for (int n = 0; n < valid.Length; n++) valid[n] = Wall(n % size, n / size) ? (byte)0 : (byte)1;
        for (int n = 0; n < valid.Length; n++)
        {
            if (valid[n] == 0) continue; int x = n % size, y = n / size;
            for (int d = 0; d < 8; d++)
            {
                int nx = x + GridSnapshot.Dx[d], ny = y + GridSnapshot.Dy[d];
                if ((uint)nx >= size || (uint)ny >= size || valid[ny * size + nx] == 0) continue;
                bool wet = (Strip(x) && y < 700) || (Strip(nx) && ny < 700);
                words[n] |= (1U << d) | (wet ? 1U << (8 + d) : 0U);
            }
        }
        return new GridSnapshot(size, size, valid, words, 1);
    }
    internal static GroupLoad MoatMix(GridSnapshot map)
    {
        var load = new GroupLoad { Name = "moat-group-mix-10000", Map = map }; var random = new Random(77013);
        while (load.Queries.Count < 10000)
        {
            int center = (50 + random.Next(700)) * map.Width + 50 + random.Next(250);
            int anchor = (50 + random.Next(700)) * map.Width + 500 + random.Next(250);
            AddGroup(load, center, anchor, 250, 1, 7);
        }
        return load;
    }
}
