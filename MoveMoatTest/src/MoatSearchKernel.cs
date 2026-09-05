using System;
using System.Collections.Generic;

namespace MoveMoatTest
{
    internal delegate bool MoatSearchEdge(int from, int to, int direction, out bool moat, out bool structure);

    internal readonly struct MoatSearchLimit
    {
        public MoatSearchLimit(long ground, long moat, long maximum)
        { Ground = ground; Moat = moat; Maximum = maximum; }
        public readonly long Ground, Moat, Maximum;
        public bool Allows(int ground, int moat, int remaining) =>
            Ground * (ground + (long)remaining) + Moat * moat <= Maximum;
    }

    // Pure directed-grid search. No unit identity, native buffer or game API belongs here.
    // The same kernel is compiled into the standalone tests and the runtime mod.
    internal sealed class MoatSearchKernel
    {
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        private readonly int width, height;
        private readonly MoatSearchEdge edge;
        private readonly int[] heads;
        private readonly List<int> touched = new List<int>();
        private readonly List<Label> labels = new List<Label>();
        private readonly List<int> heap = new List<int>();
        private ReverseField field;
        private readonly List<ReverseField> fields = new List<ReverseField>(8);
        private long fieldScale = 1;
        private long fieldGeneration;
        private int target;
        private long groundCost, moatCost;
        private bool useField;

        public MoatSearchKernel(int width, int height, MoatSearchEdge edge)
        {
            this.width = width; this.height = height; this.edge = edge;
            heads = new int[width * height];
            for (int i = 0; i < heads.Length; i++) heads[i] = -1;
        }

        public long Expanded { get; private set; }
        public long Searches { get; private set; }
        public long FieldHits { get; private set; }
        public long Refinements { get; private set; }
        public void Invalidate() { fieldGeneration++; useField = false; }
        internal int CachedFields => fields.Count;
        private static long Gcd(long a, long b) { while (b != 0) { long t = a % b; a = b; b = t; } return a; }

        public bool Search(int start, int destination, long ground, long moat,
            int maximumEdges, bool requireMoat, bool excludeStructures,
            MoatSearchLimit[] limits, bool shareField, out int[] path)
        {
            path = null;
            if (start < 0 || destination < 0 || start >= heads.Length || destination >= heads.Length)
                return false;
            target = destination; groundCost = ground; moatCost = moat;
            useField = false;
            // Do the constant-time bound before constructing or extending any field.
            if (!Fits(0, 0, start, requireMoat, limits)) return false;
            if (shareField)
            {
                fieldScale = Gcd(ground, moat);
                if (fieldScale <= 0) fieldScale = 1;
                long normalizedGround = ground / fieldScale, normalizedMoat = moat / fieldScale;
                int match = -1;
                for (int i=0;i<fields.Count;i++)
                    if (fields[i].Ground == normalizedGround && fields[i].Moat == normalizedMoat && fields[i].ExcludeStructures == excludeStructures)
                    { match=i;break; }
                if (match >= 0)
                {
                    field = fields[match]; fields.RemoveAt(match);
                    if (field.CacheGeneration != fieldGeneration)
                        field.Reset(destination,start,normalizedGround,normalizedMoat,excludeStructures);
                    else if (destination != field.Anchor)
                    {
                        // A settled optimal path's prefix is itself optimal. Reuse it
                        // directly if it contains the new endpoint; otherwise reset the
                        // same pages instead of expanding a remote heuristic anchor.
                        int[] prefix = field.Prefix(start,destination);
                        if (prefix != null && Accept(prefix,maximumEdges,requireMoat,excludeStructures,limits))
                        { fields.Add(field); FieldHits++; path=prefix; return true; }
                        field.Reset(destination,start,normalizedGround,normalizedMoat,excludeStructures);
                    }
                }
                else if (fields.Count == 8)
                { field = fields[0]; fields.RemoveAt(0); field.Reset(destination, start, normalizedGround, normalizedMoat, excludeStructures); }
                else field = new ReverseField(this, destination, start, normalizedGround, normalizedMoat, excludeStructures);
                field.CacheGeneration = fieldGeneration;
                fields.Add(field);
                long before = field.Expanded;
                long ceiling = long.MaxValue;
                if (limits != null)
                    foreach (MoatSearchLimit limit in limits)
                        if (limit.Ground == ground && limit.Moat == moat) ceiling = Math.Min(ceiling, limit.Maximum);
                field.Settle(destination, ceiling / fieldScale);
                // For another formation endpoint the anchor is only a heuristic.
                // Settling that unit's start as well duplicates its forward search.
                if (destination == field.Anchor) field.Settle(start, ceiling / fieldScale);
                Expanded += field.Expanded - before;
                if (field.Expanded != before) Searches++;
                useField = field.IsSettled(destination);
                if (useField) FieldHits++;
                // If target can reach the anchor but start cannot, start cannot reach target.
                // Only an EXHAUSTED field proves this; stopping at a cost bound does not.
                if (useField && field.Exhausted && !field.IsSettled(start)) return false;
                if (destination == field.Anchor && field.IsSettled(start))
                {
                    int[] shared = field.Path(start);
                    if (Accept(shared, maximumEdges, requireMoat, excludeStructures, limits))
                    { path = shared; return true; }
                }
            }

            // Most routes need only one scalar A*. A resource-constrained refinement is
            // necessary only when its optimum violates length, moat or profile conditions.
            if (!Run(start, maximumEdges, requireMoat, excludeStructures, limits, false, out int[] first))
                return false;
            if (Accept(first, maximumEdges, requireMoat, excludeStructures, limits))
            { path = first; return true; }
            Refinements++;
            return Run(start, maximumEdges, requireMoat, excludeStructures, limits, true, out path);
        }

        private bool Run(int start, int maximumEdges, bool requireMoat, bool excludeStructures,
            MoatSearchLimit[] limits, bool refine, out int[] path)
        {
            path = null;
            Searches++;
            foreach (int n in touched) heads[n] = -1;
            touched.Clear(); labels.Clear(); heap.Clear();
            Add(new Label(start, -1, 0, 0, -1));
            while (heap.Count != 0)
            {
                int index = Pop(); Label current = labels[index];
                if (current.Dead) continue;
                if (limits != null)
                {
                    long lower = current.Ground * groundCost + current.Moat * moatCost + Heuristic(current.Node);
                    bool over = false;
                    foreach (MoatSearchLimit limit in limits)
                        if (limit.Ground == groundCost && limit.Moat == moatCost && lower > limit.Maximum) over = true;
                    if (over) return false;
                }
                Expanded++;
                if (current.Node == target && (!refine || !requireMoat || current.Moat > 0))
                { path = Trace(index); return true; }
                if (refine && current.Ground + current.Moat >= maximumEdges) continue;
                for (int d = 0; d < 8; d++)
                {
                    int next = Neighbour(current.Node, d);
                    if (next < 0 || !edge(current.Node, next, d, out bool wet, out bool structure) ||
                        (excludeStructures && structure)) continue;
                    int ng = current.Ground + (wet ? 0 : 1), nm = current.Moat + (wet ? 1 : 0);
                    if (refine && (!Fits(ng, nm, next, requireMoat, limits) ||
                        ng + nm + Distance(next, target) > maximumEdges)) continue;
                    bool dominated = false;
                    long cost = ng * groundCost + nm * moatCost;
                    for (int old = heads[next]; old >= 0; old = labels[old].Next)
                    {
                        Label previous = labels[old];
                        if (previous.Dead) continue;
                        bool sameState = !requireMoat || (previous.Moat > 0) == (nm > 0);
                        if ((!refine && previous.Ground * groundCost + previous.Moat * moatCost <= cost) ||
                            (refine && sameState && previous.Ground <= ng && previous.Moat <= nm))
                        { dominated = true; break; }
                    }
                    if (dominated) continue;
                    for (int old = heads[next]; old >= 0; old = labels[old].Next)
                    {
                        Label previous = labels[old];
                        bool sameState = !requireMoat || (previous.Moat > 0) == (nm > 0);
                        if (!refine || (sameState && ng <= previous.Ground && nm <= previous.Moat))
                        { previous.Dead = true; labels[old] = previous; }
                    }
                    Add(new Label(next, index, ng, nm, heads[next]));
                }
            }
            return false;
        }

        private bool Fits(int ground, int moat, int node, bool requireMoat, MoatSearchLimit[] limits)
        {
            if (limits == null) return true;
            int remaining = Distance(node, target);
            foreach (MoatSearchLimit limit in limits)
            {
                long lower = limit.Ground * (ground + (long)remaining) + limit.Moat * moat;
                if (requireMoat && moat == 0)
                    lower += remaining > 0 ? limit.Moat - limit.Ground : limit.Moat;
                if (lower > limit.Maximum) return false;
            }
            return true;
        }

        private bool Accept(int[] path, int maximumEdges, bool requireMoat,
            bool excludeStructures, MoatSearchLimit[] limits)
        {
            if (path == null || path.Length - 1 > maximumEdges) return false;
            int ground = 0, moat = 0;
            for (int i = 1; i < path.Length; i++)
            {
                int d = Direction(path[i - 1], path[i]);
                if (d < 0 || !edge(path[i - 1], path[i], d, out bool wet, out bool structure) ||
                    (excludeStructures && structure)) return false;
                if (wet) moat++; else ground++;
            }
            return (!requireMoat || moat > 0) && Fits(ground, moat, target, false, limits);
        }

        private int[] Trace(int index)
        {
            Label end = labels[index]; int count = end.Ground + end.Moat + 1;
            var result = new int[count];
            for (int i = count - 1; i >= 0; i--)
            { result[i] = labels[index].Node; index = labels[index].Parent; }
            return result;
        }
        private void Add(Label label)
        {
            // The reverse field is frozen during this forward run. Cache priorities once;
            // heap comparisons must not repeatedly query its distance map.
            label.Cost = label.Ground * groundCost + label.Moat * moatCost;
            label.Priority = label.Cost + Heuristic(label.Node);
            if (heads[label.Node] < 0) touched.Add(label.Node);
            int index = labels.Count; labels.Add(label); heads[label.Node] = index;
            heap.Add(index); int at = heap.Count - 1;
            while (at > 0)
            {
                int parent = (at - 1) / 2;
                if (!Before(index, heap[parent])) break;
                heap[at] = heap[parent]; at = parent;
            }
            heap[at] = index;
        }
        private int Pop()
        {
            int result = heap[0], last = heap[heap.Count - 1]; heap.RemoveAt(heap.Count - 1);
            int at = 0;
            while (at * 2 + 1 < heap.Count)
            {
                int child = at * 2 + 1;
                if (child + 1 < heap.Count && Before(heap[child + 1], heap[child])) child++;
                if (!Before(heap[child], last)) break;
                heap[at] = heap[child]; at = child;
            }
            if (heap.Count > 0) heap[at] = last;
            return result;
        }
        private bool Before(int left, int right)
        {
            Label a = labels[left], b = labels[right];
            long ga = a.Cost, gb = b.Cost;
            long fa = a.Priority, fb = b.Priority;
            return fa != fb ? fa < fb : ga != gb ? ga > gb : left < right;
        }
        private long Heuristic(int node)
        {
            long lower = Distance(node, target) * groundCost;
            if (!useField) return lower;
            long difference = field.Lower(node) - field.Cost(target);
            long scaled = difference <= 0 ? 0 : difference > long.MaxValue / fieldScale ? long.MaxValue : difference * fieldScale;
            return Math.Max(lower, scaled);
        }
        private int Distance(int a, int b) => Math.Max(Math.Abs(a % width - b % width), Math.Abs(a / width - b / width));
        private int Neighbour(int node, int direction)
        {
            int x = node % width + Dx[direction], y = node / width + Dy[direction];
            return x < 0 || y < 0 || x >= width || y >= height ? -1 : y * width + x;
        }
        public int Direction(int from, int to)
        {
            int dx = to % width - from % width, dy = to / width - from / width;
            for (int d = 0; d < 8; d++) if (Dx[d] == dx && Dy[d] == dy) return d;
            return -1;
        }
        private struct Label
        {
            public Label(int node, int parent, int ground, int moat, int next)
            { Node = node; Parent = parent; Ground = ground; Moat = moat; Next = next; Dead = false; Cost = Priority = 0; }
            public int Node, Parent, Ground, Moat, Next;
            public long Cost, Priority;
            public bool Dead;
        }

        private sealed class ReverseField
        {
            public long CacheGeneration;
            private readonly MoatSearchKernel owner;
            private int first;
            // Sparse pages keep indexed reads cheap without allocating a full map's
            // distance/parent arrays for every traversal and speed profile.
            private const int PageShift = 10, PageSize = 1 << PageShift, PageMask = PageSize - 1;
            private readonly Page[] pages;
            private int generation;
            private readonly List<Entry> queue = new List<Entry>();
            private long frontier;
            public ReverseField(MoatSearchKernel owner, int anchor, int first, long ground, long moat, bool exclude)
            {
                this.owner = owner;
                pages = new Page[(owner.heads.Length + PageMask) >> PageShift];
                Reset(anchor, first, ground, moat, exclude);
            }
            public void Reset(int anchor, int first, long ground, long moat, bool exclude)
            {
                if (generation == int.MaxValue)
                {
                    foreach (Page page in pages) if (page != null) Array.Clear(page.Generation, 0, PageSize);
                    generation = 0;
                }
                generation++; queue.Clear(); frontier = 0; Expanded = 0;
                Anchor = anchor; this.first = first;
                Ground = ground; Moat = moat; ExcludeStructures = exclude;
                Set(anchor, 0, -1); Push(new Entry(anchor, 0, Priority(anchor, 0)));
            }
            public int Anchor { get; private set; }
            public long Ground { get; private set; }
            public long Moat { get; private set; }
            public bool ExcludeStructures { get; private set; }
            public long Expanded { get; private set; }
            public bool Exhausted => queue.Count == 0;
            public bool IsSettled(int node) => pages[node >> PageShift]?.Generation[node & PageMask] == -generation;
            public int[] Prefix(int start,int destination)
            {
                if (!IsSettled(start) || !IsSettled(destination)) return null;
                int node=start;
                while (node != destination && node != Anchor) node=pages[node >> PageShift].Parent[node & PageMask];
                if (node != destination) return null;
                var result=new List<int>();node=start;
                while(node != destination) {result.Add(node);node=pages[node >> PageShift].Parent[node & PageMask];}
                result.Add(destination);return result.ToArray();
            }
            public long Cost(int node) => pages[node >> PageShift].Distance[node & PageMask];
            private bool TryCost(int node, out long cost)
            {
                Page page = pages[node >> PageShift]; int slot = node & PageMask;
                if (page != null && (page.Generation[slot] == generation || page.Generation[slot] == -generation))
                { cost = page.Distance[slot]; return true; }
                cost = 0; return false;
            }
            private void Set(int node, long cost, int parent)
            {
                int index = node >> PageShift, slot = node & PageMask;
                Page page = pages[index] ?? (pages[index] = new Page());
                page.Generation[slot] = generation; page.Distance[slot] = cost; page.Parent[slot] = parent;
            }
            public long Lower(int node) => IsSettled(node) ? Cost(node) :
                Math.Max(owner.Distance(node, Anchor) * Ground, frontier - owner.Distance(node, first) * Ground);
            private long Priority(int node, long cost) => cost + owner.Distance(node, first) * Ground;
            public void Settle(int wanted, long ceiling)
            {
                while (queue.Count > 0 && !IsSettled(wanted))
                {
                    if (queue[0].Priority > ceiling) break;
                    Entry entry = Pop();
                    if (IsSettled(entry.Node) || Cost(entry.Node) != entry.Cost) continue;
                    pages[entry.Node >> PageShift].Generation[entry.Node & PageMask] = -generation; Expanded++;
                    for (int d = 0; d < 8; d++)
                    {
                        int predecessor = owner.Neighbour(entry.Node, d);
                        // Reverse traversal must test the ORIGINAL directed edge.
                        if (predecessor < 0 || !owner.edge(predecessor, entry.Node, (d + 4) & 7,
                            out bool wet, out bool structure) || (ExcludeStructures && structure)) continue;
                        long cost = entry.Cost + (wet ? Moat : Ground);
                        if (TryCost(predecessor, out long old) && old <= cost) continue;
                        Set(predecessor, cost, entry.Node);
                        Push(new Entry(predecessor, cost, Priority(predecessor, cost)));
                    }
                }
                while (queue.Count > 0 && (IsSettled(queue[0].Node) || Cost(queue[0].Node) != queue[0].Cost)) Pop();
                frontier = queue.Count == 0 ? 0 : queue[0].Priority;
            }
            public int[] Path(int start)
            {
                var nodes = new List<int>(); int node = start;
                while (node != Anchor) { nodes.Add(node); node = pages[node >> PageShift].Parent[node & PageMask]; }
                nodes.Add(Anchor); return nodes.ToArray();
            }
            private sealed class Page
            {
                public readonly long[] Distance = new long[PageSize];
                public readonly int[] Parent = new int[PageSize], Generation = new int[PageSize];
            }
            private static bool Before(Entry a, Entry b) => a.Priority != b.Priority ? a.Priority < b.Priority :
                a.Cost != b.Cost ? a.Cost > b.Cost : a.Node < b.Node;
            private void Push(Entry entry)
            {
                queue.Add(entry); int at = queue.Count - 1;
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (!Before(entry, queue[parent])) break;
                    queue[at] = queue[parent]; at = parent;
                }
                queue[at] = entry;
            }
            private Entry Pop()
            {
                Entry result = queue[0], last = queue[queue.Count - 1]; queue.RemoveAt(queue.Count - 1);
                int at = 0;
                while (at * 2 + 1 < queue.Count)
                {
                    int child = at * 2 + 1;
                    if (child + 1 < queue.Count && Before(queue[child + 1], queue[child])) child++;
                    if (!Before(queue[child], last)) break;
                    queue[at] = queue[child]; at = child;
                }
                if (queue.Count > 0) queue[at] = last;
                return result;
            }
            private readonly struct Entry
            {
                public Entry(int node, long cost, long priority) { Node = node; Cost = cost; Priority = priority; }
                public readonly int Node;
                public readonly long Cost, Priority;
            }
        }
    }
}
