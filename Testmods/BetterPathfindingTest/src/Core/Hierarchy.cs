using System;
using System.Collections.Generic;

namespace BetterPathfindingTest.Core
{
    // Every portal is an original grid vertex. No portal centers or corridor restriction.
    public sealed class HierarchicalNavigator : IDisposable
    {
        internal sealed class Arc
        {
            internal int To, Ground, Moat; internal byte[] Path;
        }
        private sealed class Row
        {
            internal int Level, Source, Region; internal long Bytes; internal Arc[] Arcs;
        }
        private sealed class Label
        {
            internal int Node, Ground, Moat, Parent, Next;
            internal long Cost; internal bool Dead; internal byte[] Segment;
        }
        private sealed class Search : IDisposable
        {
            internal readonly List<Label> Labels = new List<Label>();
            internal readonly Dictionary<int, int> Heads = new Dictionary<int, int>();
            internal readonly MinHeap<int> Heap;
            private readonly RouteQuery query; private readonly bool scalar; private readonly WorkBudget budget;
            private long bytes;
            internal Search(RouteQuery query, bool scalar, WorkBudget budget)
            {
                this.query = query; this.scalar = scalar; this.budget = budget;
                Heap = new MinHeap<int>((a, b) => Labels[a].Cost != Labels[b].Cost ? Labels[a].Cost.CompareTo(Labels[b].Cost) : a.CompareTo(b));
            }
            internal bool Add(int node, int ground, int moat, int parent, byte[] path)
            {
                if (!scalar && !query.Allows(ground, moat)) return false;
                long cost = ground * query.GroundCost + moat * query.MoatCost;
                int head; if (!Heads.TryGetValue(node, out head)) head = -1;
                for (int i = head; i >= 0; i = Labels[i].Next)
                {
                    Label old = Labels[i]; if (old.Dead) continue;
                    bool state = scalar || (old.Moat != 0) == (moat != 0);
                    if (state && (scalar ? old.Cost <= cost : old.Ground <= ground && old.Moat <= moat)) return false;
                }
                budget.Reserve(192); bytes += 192; // includes list capacity, heap and dictionary overhead.
                for (int i = head; i >= 0; i = Labels[i].Next)
                {
                    Label old = Labels[i]; bool state = scalar || (old.Moat != 0) == (moat != 0);
                    if (state && (scalar ? cost <= old.Cost : ground <= old.Ground && moat <= old.Moat)) old.Dead = true;
                }
                int index = Labels.Count;
                Labels.Add(new Label { Node = node, Ground = ground, Moat = moat, Parent = parent, Next = head, Segment = path, Cost = cost });
                Heads[node] = index; Heap.Push(index); return true;
            }
            internal byte[] Trace(int index)
            {
                Label label = Labels[index]; int length = checked(label.Ground + label.Moat);
                var path = new byte[length]; int write = length;
                while (index >= 0)
                { label = Labels[index]; if (label.Segment != null) { write -= label.Segment.Length; Array.Copy(label.Segment, 0, path, write, label.Segment.Length); } index = label.Parent; }
                if (write != 0) throw new InvalidOperationException("Hierarchy witness length.");
                return path;
            }
            public void Dispose() { budget.Release(bytes); }
        }
        private static readonly byte[][] Single = { new byte[] { 0 }, new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }, new byte[] { 4 }, new byte[] { 5 }, new byte[] { 6 }, new byte[] { 7 } };
        private readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        private readonly Dictionary<string, LocalField> fields = new Dictionary<string, LocalField>();
        private GridSnapshot map;
        public GridSnapshot Map => map;
        public readonly WorkBudget Budget;
        private long fixedBytes;
        public long RowsBuilt32, RowsBuilt128, RowHits, FieldsBuilt, FieldHits, InvalidatedRows, GlobalSearches, ChangedNodes;
        public HierarchicalNavigator(GridSnapshot map, WorkBudget budget)
        {
            this.map = map; Budget = budget;
            // Reserve output/transient headroom as well as the owned immutable graph.
            fixedBytes = map.BufferBytes + 4L * 1024 * 1024; budget.Reserve(fixedBytes);
        }
        internal int Region(int node, int size) => (node / map.Width / size) * ((map.Width + size - 1) / size) + node % map.Width / size;
        private bool Boundary(int node, int size)
        {
            int r = Region(node, size);
            for (int d = 0; d < 8; d++) { int next = map.Neighbor(node, d); if (map.IsValid(next) && Region(next, size) != r) return true; }
            return false;
        }
        private Row GetRow(int level, int source, RouteQuery query, bool scalar)
        {
            Budget.Check(); int size = level == 0 ? 32 : 128, region = Region(source, size);
            int localTarget = Region(query.Target, size) == region ? query.Target : -1;
            string key = level + ":" + source + ":" + localTarget + ":" + query.Key(scalar);
            if (rows.TryGetValue(key, out Row cached)) { RowHits++; return cached; }
            long rowBytes = 128 + key.Length * 2L;
            Budget.Reserve(rowBytes); bool published = false;
            try
            {
                var arcs = new List<Arc>();
                using (var search = new Search(query, scalar, Budget))
                {
                    search.Add(source, 0, 0, -1, null);
                    while (search.Heap.Count != 0)
                    {
                        int index = search.Heap.Pop(); Label current = search.Labels[index]; if (current.Dead) continue;
                        Budget.Step();
                        if (level == 0) ExpandGrid(search, index, query, size, region, false);
                        else
                        {
                            Row child = GetRow(level - 1, current.Node, query, scalar);
                            foreach (Arc arc in child.Arcs) search.Add(arc.To, current.Ground + arc.Ground, current.Moat + arc.Moat, index, arc.Path);
                            ExpandGrid(search, index, query, size, region, true);
                        }
                    }
                    for (int index = 0; index < search.Labels.Count; index++)
                    {
                        Label label = search.Labels[index];
                        if (label.Dead || label.Parent < 0 || (!Boundary(label.Node, size) && label.Node != localTarget)) continue;
                        long bytes = (long)label.Ground + label.Moat + 128;
                        Budget.Reserve(bytes); rowBytes += bytes;
                        arcs.Add(new Arc { To = label.Node, Ground = label.Ground, Moat = label.Moat, Path = search.Trace(index) });
                    }
                }
                Row row = new Row { Level = level, Source = source, Region = region, Bytes = rowBytes, Arcs = arcs.ToArray() };
                rows.Add(key, row); published = true;
                if (level == 0) RowsBuilt32++; else RowsBuilt128++;
                return row;
            }
            finally { if (!published) Budget.Release(rowBytes); }
        }
        private void ExpandGrid(Search search, int index, RouteQuery query, int size, int region, bool crossOnly)
        {
            Label current = search.Labels[index];
            for (int d = 0; d < 8; d++)
            {
                if (!map.Edge(current.Node, d, query, out int to, out bool wet, out _)) continue;
                if (size != 0 && Region(to, size) != region) continue;
                int childSize = size == 128 ? 32 : 128;
                if (crossOnly && Region(current.Node, childSize) == Region(to, childSize)) continue;
                search.Add(to, current.Ground + (wet ? 0 : 1), current.Moat + (wet ? 1 : 0), index, Single[d]);
            }
        }
        private RouteResult SearchGlobal(RouteQuery query, bool scalar)
        {
            GlobalSearches++;
            using (var search = new Search(query, scalar, Budget))
            {
                search.Add(query.Start, 0, 0, -1, null);
                while (search.Heap.Count != 0)
                {
                    int index = search.Heap.Pop(); Label current = search.Labels[index]; if (current.Dead) continue;
                    Budget.Step();
                    if (current.Node == query.Target && (scalar || !query.RequireMoat || current.Moat > 0))
                    {
                        byte[] path = search.Trace(index);
                        RouteQuery validation = query;
                        if (scalar) { validation = query.Copy(); validation.MaximumEdges = 2000000; validation.RequireMoat = false; validation.Bounds = new CostBound[0]; }
                        return map.Describe(validation, path);
                    }
                    Row row = GetRow(1, current.Node, query, scalar);
                    foreach (Arc arc in row.Arcs) search.Add(arc.To, current.Ground + arc.Ground, current.Moat + arc.Moat, index, arc.Path);
                    // Only actual edges crossing the 128 boundary are absent from the row.
                    for (int d = 0; d < 8; d++)
                    {
                        if (map.Edge(current.Node, d, query, out int to, out bool wet, out _) && Region(current.Node, 128) != Region(to, 128))
                            search.Add(to, current.Ground + (wet ? 0 : 1), current.Moat + (wet ? 1 : 0), index, Single[d]);
                    }
                }
                return RouteResult.Failed(SearchStatus.Unreachable, "no-feasible-route");
            }
        }
        public RouteResult FindPath(RouteQuery query)
        {
            long before = Budget.Expanded;
            try
            {
                query.Validate(map); Budget.Check();
                string fieldKey = Region(query.Start, 32) + ":" + query.Target + ":" + query.Key(true);
                RouteResult scalar;
                if (fields.TryGetValue(fieldKey, out LocalField field)) { FieldHits++; scalar = field.Read(query, map); }
                else scalar = SearchGlobal(query, true);
                if (scalar.Status == SearchStatus.Unreachable) return scalar;
                if (scalar.Status != SearchStatus.Found) return scalar;
                RouteResult accepted = map.Describe(query, scalar.Directions);
                if (accepted.Status == SearchStatus.Found) { accepted.Expanded = Budget.Expanded - before; return accepted; }
                RouteResult constrained = SearchGlobal(query, false); constrained.Expanded = Budget.Expanded - before; return constrained;
            }
            catch (SearchBudgetException ex) { return RouteResult.Failed(SearchStatus.Inconclusive, ex.Message); }
        }
        public bool CanReach(RouteQuery query, out SearchStatus status)
        { RouteQuery probe = query.Copy(); probe.MaximumEdges = 2000000; probe.RequireMoat = false; probe.Bounds = new CostBound[0]; RouteResult result = FindPath(probe); status = result.Status; return status == SearchStatus.Found; }
        public RouteResult[] EvaluateCandidates(RouteQuery template, int[] candidates)
        {
            var results = new RouteResult[candidates.Length];
            for (int i = 0; i < candidates.Length; i++)
            {
                var query = template.Copy(); query.Target = candidates[i]; var terminals = new List<TerminalEdge>();
                foreach (TerminalEdge terminal in query.Terminals) if (terminal.To == query.Target) terminals.Add(terminal);
                query.Terminals = terminals.ToArray(); results[i] = FindPath(query);
            }
            return results;
        }

        // Multi-source reverse field seeded with exact GLOBAL continuation values, not zeros.
        private sealed class LocalField
        {
            internal int X, Y, Width, Height; internal long Bytes;
            internal long[] Distance; internal byte[] Next; internal Dictionary<int, byte[]> Continuations;
            internal int Slot(int node, GridSnapshot map) => (node / map.Width - Y) * Width + node % map.Width - X;
            internal RouteResult Read(RouteQuery query, GridSnapshot map)
            {
                int node = query.Start, slot = Slot(node, map);
                if (Distance[slot] == long.MaxValue) return RouteResult.Failed(SearchStatus.Unreachable, "field-unreachable");
                var path = new List<byte>(); int guard = Width * Height + 1;
                while (Next[Slot(node, map)] != 255)
                { if (--guard == 0) throw new InvalidOperationException("Local field cycle."); byte d = Next[Slot(node, map)]; path.Add(d); node = map.Neighbor(node, d); }
                if (!Continuations.TryGetValue(node, out byte[] continuation)) throw new InvalidOperationException("Missing continuation.");
                path.AddRange(continuation);
                var relaxed = query.Copy(); relaxed.MaximumEdges = 2000000; relaxed.RequireMoat = false; relaxed.Bounds = new CostBound[0];
                return map.Describe(relaxed, path.ToArray());
            }
        }
        public void PrepareBatch(IList<RouteQuery> queries)
        {
            var groups = new Dictionary<string, List<RouteQuery>>();
            foreach (RouteQuery q in queries)
            { string key = Region(q.Start, 32) + ":" + q.Target + ":" + q.Key(true); if (!groups.TryGetValue(key, out List<RouteQuery> list)) groups.Add(key, list = new List<RouteQuery>()); list.Add(q); }
            foreach (var group in groups) if (group.Value.Count >= 32 && !fields.ContainsKey(group.Key)) BuildField(group.Key, group.Value[0]);
        }
        private void BuildField(string key, RouteQuery query)
        {
            int x = query.Start % map.Width / 32 * 32, y = query.Start / map.Width / 32 * 32;
            int w = Math.Min(32, map.Width - x), h = Math.Min(32, map.Height - y);
            long bytes = w * h * 9L + key.Length * 2L + 256;
            Budget.Reserve(bytes); bool published = false;
            try
            {
                var field = new LocalField { X = x, Y = y, Width = w, Height = h, Distance = new long[w * h], Next = new byte[w * h], Continuations = new Dictionary<int, byte[]>() };
                var queue = new MinHeap<Tuple<long, int>>((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2));
                for (int i = 0; i < w * h; i++) { field.Distance[i] = long.MaxValue; field.Next[i] = 255; }
                for (int cy = y; cy < y + h; cy++) for (int cx = x; cx < x + w; cx++)
                {
                    int node = cy * map.Width + cx;
                    if (!map.IsValid(node) || (!Boundary(node, 32) && node != query.Target)) continue;
                    var seed = query.Copy(); seed.Start = node;
                    RouteResult continuation = SearchGlobal(seed, true);
                    if (continuation.Status != SearchStatus.Found) continue;
                    Budget.Reserve(continuation.Directions.Length + 96L); bytes += continuation.Directions.Length + 96L;
                    field.Continuations.Add(node, continuation.Directions);
                    field.Distance[field.Slot(node, map)] = continuation.Cost;
                    queue.Push(Tuple.Create(continuation.Cost, node));
                }
                while (queue.Count != 0)
                {
                    var item = queue.Pop(); int node = item.Item2, slot = field.Slot(node, map);
                    if (field.Distance[slot] != item.Item1) continue;
                    Budget.Step();
                    for (int d = 0; d < 8; d++)
                    {
                        int predecessor = map.Neighbor(node, (d + 4) & 7);
                        if (!map.IsValid(predecessor) || Region(predecessor, 32) != Region(query.Start, 32) || !map.Edge(predecessor, d, query, out _, out bool wet, out _)) continue;
                        int other = field.Slot(predecessor, map); long cost = item.Item1 + (wet ? query.MoatCost : query.GroundCost);
                        if (cost >= field.Distance[other]) continue;
                        field.Distance[other] = cost; field.Next[other] = (byte)d;
                        queue.Push(Tuple.Create(cost, predecessor));
                    }
                }
                field.Bytes = bytes; fields.Add(key, field); FieldsBuilt++; published = true;
            }
            finally { if (!published) Budget.Release(bytes); }
        }
        public void Update(GridSnapshot next)
        {
            if (next.Width != map.Width || next.Height != map.Height || next.Player != map.Player) throw new ArgumentException("Incompatible update.");
            var leaves = new HashSet<int>(); var parents = new HashSet<int>();
            ChangedNodes = 0;
            for (int node = 0; node < map.Count; node++) if (map.Word(node) != next.Word(node) || map.IsValid(node) != next.IsValid(node))
            {
                ChangedNodes++;
                for (int d = -1; d < 8; d++) { int affected = d < 0 ? node : map.Neighbor(node, d); if (affected < 0) continue; leaves.Add(Region(affected, 32)); parents.Add(Region(affected, 128)); }
            }
            if (leaves.Count == 0) { map = next; return; }
            var remove = new List<string>();
            foreach (var pair in rows) if ((pair.Value.Level == 0 ? leaves : parents).Contains(pair.Value.Region)) remove.Add(pair.Key);
            foreach (string key in remove) { Budget.Release(rows[key].Bytes); rows.Remove(key); InvalidatedRows++; }
            // A shortcut anywhere changes global continuation values, even outside old paths.
            foreach (LocalField field in fields.Values) Budget.Release(field.Bytes);
            fields.Clear(); map = next;
        }
        public void Dispose()
        { foreach (Row row in rows.Values) Budget.Release(row.Bytes); foreach (LocalField field in fields.Values) Budget.Release(field.Bytes); rows.Clear(); fields.Clear(); if (fixedBytes != 0) { Budget.Release(fixedBytes); fixedBytes = 0; } }
    }
}
