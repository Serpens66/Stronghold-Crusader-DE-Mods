using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BetterPathfindingTest.Core
{
    public static class UpdateComparison
    {
        public static Dictionary<string, object> Compare(GridSnapshot before, GridSnapshot after, IList<RouteQuery> queries, WorkBudget budget)
        {
            var data = new Dictionary<string, object>(); int compared = 0, differences = 0; bool incomplete = false;
            using (var updated = new AreaNavigator(before, budget))
            {
                foreach (RouteQuery query in queries) if (before.IsValid(query.Start) && before.IsValid(query.Target)) { updated.FindPath(query); break; }
                var watch = Stopwatch.StartNew(); updated.Update(after); data["updateMs"] = watch.Elapsed.TotalMilliseconds;
                data["invalidatedFields"] = updated.InvalidatedFields; data["changedCoordinates"] = updated.ChangedNodes;
                using (var fresh = new AreaNavigator(after, budget))
                    foreach (RouteQuery query in queries)
                    {
                        watch.Restart(); RouteResult a = updated.FindPath(query); double incrementalMs = watch.Elapsed.TotalMilliseconds;
                        watch.Restart(); RouteResult b = fresh.FindPath(query); double rebuildQueryMs = watch.Elapsed.TotalMilliseconds;
                        RouteResult c = ReferenceSearch.Find(after, query, new WorkBudget(budget.MaximumBytes, 10));
                        if (a.Status == SearchStatus.Inconclusive || b.Status == SearchStatus.Inconclusive || c.Status == SearchStatus.Inconclusive) { incomplete = true; break; }
                        compared++; bool same = a.Status == b.Status && a.Status == c.Status && (a.Status != SearchStatus.Found || a.Cost == b.Cost && a.Cost == c.Cost);
                        if (!same) differences++;
                        data["lastUpdatedQueryMs"] = incrementalMs; data["lastRebuildQueryMs"] = rebuildQueryMs;
                    }
            }
            data["compared"] = compared; data["differences"] = differences; data["incomplete"] = incomplete || compared != queries.Count;
            return data;
        }
        public static Dictionary<string, object> Controlled(WorkBudget budget)
        {
            const int width = 132; var valid = new byte[width * 3]; var words = new uint[valid.Length];
            for (int i = 0; i < valid.Length; i++) valid[i] = 1;
            int start = width, target = 2 * width - 1, opening = 2 * width + 64;
            words[start] = (1U << 0) | (1U << 8) | (1U << 4);
            for (int x = 0; x < width - 1; x++) { words[x] = (1U << 2) | (1U << 10); if (x != 64) words[2 * width + x] = 1U << 2; }
            words[width - 1] = 1U << 4; words[3 * width - 1] = 1U << 0;
            var before = new GridSnapshot(width, 3, valid, words); var q = new RouteQuery { Start = start, Target = target };
            words[opening] = 1U << 2; var shortcut = new GridSnapshot(width, 3, valid, words);
            words[opening] = 0; words[start] = 1U << 4; var closed = new GridSnapshot(width, 3, valid, words);
            var data = new Dictionary<string, object>();
            using (var nav = new AreaNavigator(before, budget))
            {
                var batch = new List<RouteQuery>(); for (int i = 0; i < 32; i++) batch.Add(q.Copy()); nav.FindPaths(batch);
                RouteResult old = nav.FindPath(q); bool outside = true; int node = start;
                foreach (byte d in old.Directions) { if (node == opening) outside = false; node = before.Neighbor(node, d); }
                nav.Update(shortcut); RouteResult next = nav.FindPath(q), full = ReferenceSearch.Find(shortcut, q, budget);
                nav.Update(closed); RouteResult blocked = nav.FindPath(q);
                bool complete = old.Status != SearchStatus.Inconclusive && next.Status != SearchStatus.Inconclusive && full.Status != SearchStatus.Inconclusive && blocked.Status != SearchStatus.Inconclusive;
                data["incomplete"] = !complete; data["oldCost"] = old.Cost; data["shortcutCost"] = next.Cost; data["openingOutsideOldRoute"] = outside;
                data["passed"] = complete && outside && old.Status == SearchStatus.Found && next.Status == SearchStatus.Found && next.Cost < old.Cost && next.Cost == full.Cost && blocked.Status == SearchStatus.Unreachable;
                data["scope"] = "controlled directed private copies; not native terrain mutations";
            }
            return data;
        }
    }
}
