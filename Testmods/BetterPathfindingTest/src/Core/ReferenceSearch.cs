using System;
using System.Collections.Generic;

namespace BetterPathfindingTest.Core
{
    // Full-grid multi-label Dijkstra; independent frontier, state store and reconstruction.
    // It never calls the hierarchy or consumes any of its rows/fields.
    public static class ReferenceSearch
    {
        // Independent forward Dijkstra on the ORIGINAL grid, with a wet-visit bit.
        // A feasible scalar optimum is also optimal for all additional bounds. If
        // it violates them, the independent Pareto search below remains necessary.
        private static RouteResult Scalar(GridSnapshot map, RouteQuery query, WorkBudget budget)
        {
            int n = checked(map.Count * (query.RequireMoat ? 2 : 1));
            long arrays = n * 13L, queued = 0; budget.Reserve(arrays);
            try
            {
                var distance = new long[n]; var parent = new int[n]; var direction = new byte[n];
                for (int i = 0; i < n; i++) { distance[i] = long.MaxValue; parent[i] = -1; }
                var frontier = new SortedSet<Tuple<long, int>>();
                distance[query.Start] = 0; budget.Reserve(96); queued += 96; frontier.Add(Tuple.Create(0L, query.Start));
                while (frontier.Count != 0)
                {
                    var item = frontier.Min; frontier.Remove(item); budget.Release(96); queued -= 96;
                    int state = item.Item2; if (distance[state] != item.Item1) continue;
                    budget.Step(); int node = state % map.Count; bool seen = state >= map.Count;
                    if (node == query.Target && (!query.RequireMoat || seen))
                    {
                        var path = new List<byte>(); for (int at = state; parent[at] >= 0; at = parent[at]) path.Add(direction[at]); path.Reverse();
                        RouteResult result = map.Describe(query, path.ToArray());
                        return result.Status == SearchStatus.Found ? result : null;
                    }
                    for (int d = 0; d < 8; d++) if (map.Edge(node, d, query, out int to, out bool wet, out _))
                    {
                        int next = to + (query.RequireMoat && (seen || wet) ? map.Count : 0);
                        long increment = wet ? query.MoatCost : query.GroundCost;
                        if (item.Item1 > long.MaxValue - increment) continue; long candidate = item.Item1 + increment;
                        if (candidate >= distance[next]) continue;
                        distance[next] = candidate; parent[next] = state; direction[next] = (byte)d;
                        budget.Reserve(96); queued += 96; frontier.Add(Tuple.Create(candidate, next));
                    }
                }
                return RouteResult.Failed(SearchStatus.Unreachable, "reference-grid-exhausted");
            }
            finally { budget.Release(arrays + queued); }
        }
        private sealed class State
        {
            internal int Node, Ground, Moat, Parent, Direction; internal long Cost; internal bool Removed;
        }
        public static RouteResult Find(GridSnapshot map, RouteQuery query, WorkBudget budget)
        {
            query.Validate(map); long owned = 0, before = budget.Expanded;
            try
            {
                RouteResult scalar = Scalar(map, query, budget);
                if (scalar != null) { scalar.Expanded = budget.Expanded - before; return scalar; }
                var states = new List<State>();
                var byNode = new Dictionary<int, List<int>>();
                var frontier = new SortedSet<Tuple<long, int>>();
                Action<int, int, int, int, int> insert = (node, ground, moat, parent, direction) =>
                {
                    if (!query.Allows(ground, moat)) return;
                    List<int> previous; if (!byNode.TryGetValue(node, out previous)) byNode[node] = previous = new List<int>();
                    foreach (int index in previous)
                    { State old = states[index]; if (!old.Removed && (old.Moat > 0) == (moat > 0) && old.Ground <= ground && old.Moat <= moat) return; }
                    budget.Reserve(256); owned += 256;
                    foreach (int index in previous)
                    { State old = states[index]; if ((old.Moat > 0) == (moat > 0) && ground <= old.Ground && moat <= old.Moat) old.Removed = true; }
                    int id = states.Count; long cost = ground * query.GroundCost + moat * query.MoatCost;
                    states.Add(new State { Node = node, Ground = ground, Moat = moat, Parent = parent, Direction = direction, Cost = cost });
                    previous.Add(id); frontier.Add(Tuple.Create(cost, id));
                };
                insert(query.Start, 0, 0, -1, -1);
                while (frontier.Count != 0)
                {
                    Tuple<long, int> best = frontier.Min; frontier.Remove(best);
                    int id = best.Item2; State current = states[id]; if (current.Removed) continue;
                    budget.Step();
                    if (current.Node == query.Target && (!query.RequireMoat || current.Moat > 0))
                    {
                        var directions = new List<byte>(); int cursor = id;
                        while (states[cursor].Parent >= 0) { directions.Add((byte)states[cursor].Direction); cursor = states[cursor].Parent; }
                        directions.Reverse(); RouteResult result = map.Describe(query, directions.ToArray()); result.Expanded = budget.Expanded - before; return result;
                    }
                    for (int d = 0; d < 8; d++) if (map.Edge(current.Node, d, query, out int next, out bool wet, out _))
                        insert(next, current.Ground + (wet ? 0 : 1), current.Moat + (wet ? 1 : 0), id, d);
                }
                return RouteResult.Failed(SearchStatus.Unreachable, "reference-no-feasible-route");
            }
            catch (SearchBudgetException ex) { return RouteResult.Failed(SearchStatus.Inconclusive, ex.Message); }
            finally { budget.Release(owned); }
        }
    }
}
