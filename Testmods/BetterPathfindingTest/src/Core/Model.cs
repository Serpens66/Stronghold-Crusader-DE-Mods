using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace BetterPathfindingTest.Core
{
    public enum SearchStatus { Found, Unreachable, Inconclusive }
    public sealed class CostBound
    {
        public readonly long Ground, Moat, Maximum;
        public CostBound(long ground, long moat, long maximum) { if (ground <= 0 || moat <= 0 || maximum < 0) throw new ArgumentOutOfRangeException(); Ground = ground; Moat = moat; Maximum = maximum; }
        public bool Allows(int ground, int moat) => ground <= Maximum / Ground && moat <= (Maximum - ground * Ground) / Moat;
    }
    public sealed class TerminalEdge
    {
        public readonly int From, To; public readonly bool Moat, Structure;
        public TerminalEdge(int from, int to, bool moat, bool structure = false) { From = from; To = to; Moat = moat; Structure = structure; }
    }
    public sealed class RouteQuery
    {
        public int Start, Target;
        public long GroundCost = 1, MoatCost = 7;
        public int MaximumEdges = 2000;
        public bool RequireMoat, ExcludeStructures;
        public CostBound[] Bounds = new CostBound[0];
        public TerminalEdge[] Terminals = new TerminalEdge[0];
        public RouteQuery Copy() => new RouteQuery { Start = Start, Target = Target, GroundCost = GroundCost, MoatCost = MoatCost, MaximumEdges = MaximumEdges, RequireMoat = RequireMoat, ExcludeStructures = ExcludeStructures, Bounds = (CostBound[])Bounds.Clone(), Terminals = (TerminalEdge[])Terminals.Clone() };
        public bool Allows(int ground, int moat)
        {
            if ((long)ground + moat > MaximumEdges) return false;
            foreach (CostBound bound in Bounds) if (!bound.Allows(ground, moat)) return false;
            return true;
        }
        public void Validate(GridSnapshot map)
        {
            if (!map.IsValid(Start) || !map.IsValid(Target) || GroundCost <= 0 || MoatCost <= 0 || MaximumEdges < 0 || MaximumEdges > 2000000 || GroundCost > long.MaxValue / 4000001 || MoatCost > long.MaxValue / 4000001) throw new ArgumentException("Invalid route query.");
            foreach (TerminalEdge terminal in Terminals) if (terminal.To != Target || map.Direction(terminal.From, terminal.To) < 0) throw new ArgumentException("Terminal edges must be adjacent incoming edges of the exact target.");
        }
        // Stable, collision-free within this process; no hash alone authorizes cache reuse.
        internal string Key(bool scalar)
        {
            string key = GroundCost + ":" + MoatCost + ":" + ExcludeStructures;
            if (!scalar) { key += ":" + MaximumEdges; foreach (CostBound b in Bounds) key += ":" + b.Ground + "," + b.Moat + "," + b.Maximum; }
            foreach (TerminalEdge t in Terminals) key += ":t" + t.From + "," + t.To + "," + t.Moat + "," + t.Structure;
            return key;
        }
    }
    public sealed class RouteResult
    {
        public SearchStatus Status; public string Reason;
        public int Ground, Moat; public long Cost, Expanded;
        public byte[] Directions = new byte[0];
        public static RouteResult Failed(SearchStatus status, string reason) => new RouteResult { Status = status, Reason = reason };
    }
    public sealed class GridSnapshot
    {
        public static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 }, Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        public readonly int Width, Height, Player;
        private readonly byte[] valid;
        private readonly uint[] edges;
        public int Count => valid.Length;
        public long BufferBytes => valid.LongLength + edges.LongLength * 4 + 128;
        // Words: allowed bits 0..7, moat bits 8..15, structure bits 16..23.
        public GridSnapshot(int width, int height, byte[] validity, uint[] edgeWords, int player = 0)
        {
            if (width < 1 || height < 1 || (long)width * height > 640000 || validity.Length != width * height || edgeWords.Length != validity.Length) throw new ArgumentException("Grid dimensions.");
            Width = width; Height = height; Player = player; valid = (byte[])validity.Clone(); edges = (uint[])edgeWords.Clone();
            for (int n = 0; n < Count; n++) if (!IsValid(n) && edges[n] != 0) throw new ArgumentException("Invalid node has edges.");
        }
        public byte[] CopyValidity() => (byte[])valid.Clone();
        public uint[] CopyEdges() => (uint[])edges.Clone();
        public uint Word(int node) => edges[node];
        public bool IsValid(int node) => (uint)node < Count && valid[node] != 0;
        public int Neighbor(int node, int d)
        { int x = node % Width + Dx[d], y = node / Width + Dy[d]; return (uint)x < Width && (uint)y < Height ? y * Width + x : -1; }
        public int Direction(int from, int to)
        { if ((uint)from >= Count || (uint)to >= Count) return -1; for (int d = 0; d < 8; d++) if (Neighbor(from, d) == to) return d; return -1; }
        public bool Edge(int from, int d, RouteQuery query, out int to, out bool moat, out bool structure)
        {
            to = Neighbor(from, d); moat = structure = false;
            if (!IsValid(from) || !IsValid(to)) return false;
            uint word = edges[from]; moat = (word & (1U << (8 + d))) != 0; structure = (word & (1U << (16 + d))) != 0;
            bool allowed = (word & (1U << d)) != 0;
            foreach (TerminalEdge terminal in query.Terminals) if (terminal.From == from && terminal.To == to) { allowed = true; moat = terminal.Moat; structure = terminal.Structure; break; }
            return allowed && (!query.ExcludeStructures || !structure);
        }
        public RouteResult Describe(RouteQuery query, byte[] directions)
        {
            int node = query.Start, ground = 0, moatCount = 0;
            foreach (byte d in directions)
            {
                if (d >= 8 || !Edge(node, d, query, out int to, out bool wet, out _)) return RouteResult.Failed(SearchStatus.Inconclusive, "invalid-output-edge");
                if (wet) moatCount++; else ground++; node = to;
            }
            if (node != query.Target || !query.Allows(ground, moatCount) || (query.RequireMoat && moatCount == 0)) return RouteResult.Failed(SearchStatus.Inconclusive, "invalid-output-contract");
            return new RouteResult { Status = SearchStatus.Found, Ground = ground, Moat = moatCount, Cost = ground * query.GroundCost + moatCount * query.MoatCost, Directions = directions };
        }
        public ulong Fingerprint()
        { ulong h = 14695981039346656037UL; for (int i = 0; i < Count; i++) { h = unchecked((h ^ valid[i]) * 1099511628211UL); h = unchecked((h ^ edges[i]) * 1099511628211UL); } return h; }
    }
    public sealed class SearchBudgetException : Exception { public SearchBudgetException(string reason) : base(reason) { } }
    public sealed class WorkBudget
    {
        private readonly Stopwatch watch = Stopwatch.StartNew();
        private readonly CancellationToken cancellation;
        private readonly double seconds;
        public readonly long MaximumBytes;
        public long Bytes { get; private set; }
        public long PeakBytes { get; private set; }
        public long Expanded { get; private set; }
        public WorkBudget(long maximumBytes = 256L * 1024 * 1024, double maximumSeconds = 120, CancellationToken cancellation = default(CancellationToken))
        { MaximumBytes = maximumBytes; seconds = maximumSeconds; this.cancellation = cancellation; }
        public void Check() { cancellation.ThrowIfCancellationRequested(); if (watch.Elapsed.TotalSeconds >= seconds) throw new SearchBudgetException("time-budget"); }
        public void Step() { Expanded++; if ((Expanded & 255) == 0) Check(); }
        public void Reserve(long bytes) { Check(); if (bytes < 0 || bytes > MaximumBytes - Bytes) throw new SearchBudgetException("memory-budget"); Bytes += bytes; PeakBytes = Math.Max(PeakBytes, Bytes); }
        public void Release(long bytes) { Bytes -= bytes; if (Bytes < 0) throw new InvalidOperationException("Navigation accounting underflow."); }
    }
    internal sealed class MinHeap<T>
    {
        private readonly List<T> entries = new List<T>(); private readonly Comparison<T> compare;
        internal MinHeap(Comparison<T> compare) { this.compare = compare; }
        public int Count => entries.Count;
        public void Push(T value)
        { int i = entries.Count; entries.Add(value); while (i != 0) { int p = (i - 1) >> 1; if (compare(entries[p], value) <= 0) break; entries[i] = entries[p]; i = p; } entries[i] = value; }
        public T Pop()
        { T result = entries[0], last = entries[entries.Count - 1]; entries.RemoveAt(entries.Count - 1); if (entries.Count == 0) return result; int i = 0; while (i * 2 + 1 < entries.Count) { int child = i * 2 + 1; if (child + 1 < entries.Count && compare(entries[child + 1], entries[child]) < 0) child++; if (compare(last, entries[child]) <= 0) break; entries[i] = entries[child]; i = child; } entries[i] = last; return result; }
    }
    public static class PublicationRules
    {
        public static long Ticks(int ground, int moat, long groundCost, long moatCost, int progress)
        { if (ground < 0 || moat < 0 || groundCost <= 0 || moatCost <= 0 || progress <= 0 || ground > long.MaxValue / groundCost || moat > (long.MaxValue - ground * groundCost) / moatCost) return long.MaxValue; long cost = ground * groundCost + moat * moatCost; return cost / progress + (cost % progress == 0 ? 0 : 1); }
        public static bool Improves(int nativeGround, int nativeMoat, int candidateGround, int candidateMoat, long[] groundCosts, long[] moatCosts, int[] progress)
        { if (groundCosts.Length == 0 || groundCosts.Length != moatCosts.Length || progress.Length != groundCosts.Length) return false; for (int p = 0; p < progress.Length; p++) { long a = Ticks(nativeGround, nativeMoat, groundCosts[p], moatCosts[p], progress[p]), b = Ticks(candidateGround, candidateMoat, groundCosts[p], moatCosts[p], progress[p]); if (a == long.MaxValue || b == long.MaxValue || b > a || a - b < (p == 0 ? 40 : 1)) return false; } return true; }
    }
}
