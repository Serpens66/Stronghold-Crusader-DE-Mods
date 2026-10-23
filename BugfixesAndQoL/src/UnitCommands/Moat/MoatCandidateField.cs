using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.UnitCommands
{
    // A placement query needs distances, not one encoded route per unit/candidate.
    // Terminal edges may reach a reserved destination but never extend the frontier.
    internal sealed class MoatCandidateField
    {
        private readonly int width, height;
        // pending[tile] == generation marks a target whose distance is still unknown.
        private readonly int[] marks, distances, queue, pending;
        private readonly Dictionary<int, int> goals = new Dictionary<int, int>();
        private int generation;
        public int Expanded { get; private set; }
        public bool BudgetExceeded { get; private set; }
        public MoatCandidateField(int width, int height)
        {
            this.width = width; this.height = height;
            marks = new int[width * height]; distances = new int[marks.Length]; queue = new int[marks.Length];
            pending = new int[marks.Length];
        }
        public int[] Resolve(IList<int> starts, IList<int> targets, MoatSearchEdge edge,
            MoatSearchEdge terminal, int maximumExpanded = int.MaxValue,
            int maximumDistance = int.MaxValue)
        {
            if (++generation == int.MaxValue) { Array.Clear(marks, 0, marks.Length); Array.Clear(pending, 0, pending.Length); generation = 1; }
            Expanded = 0; BudgetExceeded = false; goals.Clear();
            foreach (int tile in targets) if ((uint)tile < marks.Length) { goals[tile] = -1; pending[tile] = generation; }
            int remaining = goals.Count, head = 0, tail = 0;
            foreach (int tile in starts)
            {
                if ((uint)tile >= marks.Length || marks[tile] == generation) continue;
                marks[tile] = generation; distances[tile] = 0; queue[tail++] = tile;
                if (pending[tile] == generation) { goals[tile] = 0; pending[tile] = 0; remaining--; }
            }
            while (head < tail && remaining != 0)
            {
                if (Expanded >= maximumExpanded)
                {
                    BudgetExceeded = true;
                    break;
                }
                int from = queue[head++], x = from % width, y = from / width;
                Expanded++;
                if (distances[from] >= maximumDistance) continue;
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + WeightedMoatRoutePlanner.DirectionX[d], ny = y + WeightedMoatRoutePlanner.DirectionY[d];
                    if ((uint)nx >= width || (uint)ny >= height) continue;
                    int to = ny * width + nx, distance = distances[from] + 1;
                    if (distance > maximumDistance) continue;
                    bool goal = pending[to] == generation;
                    // A visited non-goal tile cannot change the result, and edge evaluation is pure
                    // apart from diagnostics: skip it (otherwise ~8 calls per expanded node).
                    if (!goal && marks[to] == generation) continue;
                    bool normal = edge(from, to, d, out _, out _);
                    if (goal && (normal || terminal(from, to, d, out _, out _)))
                    { goals[to] = distance; pending[to] = 0; remaining--; }
                    if (normal && marks[to] != generation)
                    { marks[to] = generation; distances[to] = distance; queue[tail++] = to; }
                }
            }
            var result = new int[targets.Count];
            for (int i = 0; i < result.Length; i++) result[i] = goals.TryGetValue(targets[i], out int distance) ? distance : -1;
            return result;
        }
    }

    internal delegate bool MoatSearchEdge(int from, int to, int direction, out bool moat, out bool structure);

    internal readonly struct MoatSearchLimit
    {
        public MoatSearchLimit(long ground, long moat, long maximum)
        { Ground = ground; Moat = moat; Maximum = maximum; }
        public readonly long Ground, Moat, Maximum;
        public bool Allows(int ground, int moat, int remaining) =>
            Ground * (ground + (long)remaining) + Moat * moat <= Maximum;
    }

}
