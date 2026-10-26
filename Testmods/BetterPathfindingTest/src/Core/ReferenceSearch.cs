using System;
using System.Collections.Generic;

namespace BetterPathfindingTest.Core
{
    // Full-grid multi-label Dijkstra; independent frontier, state store and reconstruction.
    // It never calls the hierarchy or consumes any of its rows/fields.
    public static class ReferenceSearch
    {
        private sealed class State
        {
            internal int Node, Ground, Moat, Parent, Direction; internal long Cost; internal bool Removed;
        }
        public static RouteResult Find(GridSnapshot map, RouteQuery query, WorkBudget budget)
        {
            query.Validate(map); long owned = 0, before = budget.Expanded;
            try
            {
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
