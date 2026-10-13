using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.GateRouting
{
    internal readonly struct GateFootprintIdentity
    {
        internal readonly uint Global, Grid, X, Y;
        internal readonly int Building, Type, Alive, Owner, Capturer;
        internal GateFootprintIdentity(int building, uint global, int type, int alive, int owner,
            int capturer, uint grid, uint x, uint y)
        { Building = building; Global = global; Type = type; Alive = alive; Owner = owner;
            Capturer = capturer; Grid = grid; X = x; Y = y; }
        internal bool Matches(GateFootprintIdentity other) => Building > 0 && Global != 0 &&
            Building == other.Building && Global == other.Global && Type == other.Type &&
            Alive == other.Alive && Owner == other.Owner && Capturer == other.Capturer &&
            Grid == other.Grid && X == other.X && Y == other.Y;
    }

    // Deferred, read-only reconstruction. The caller reads at most 36 inline seeds.
    // No native helper that writes the construction scratch state is invoked.
    internal static class ConnectedGateFootprint
    {
        internal readonly struct Cell
        {
            internal readonly int Id, X, Y;
            internal Cell(int id, int x, int y) { Id = id; X = x; Y = y; }
        }

        internal static bool TryCollect(int buildingId, IList<Cell> seeds,
            Func<int, int, Cell?> readOwnedCell, out Cell[] footprint)
        {
            footprint = Array.Empty<Cell>();
            if (buildingId <= 0 || seeds == null || seeds.Count == 0 || seeds.Count > 36)
                return false;
            var found = new Dictionary<int, Cell>();
            var pending = new Queue<Cell>();
            Cell first = seeds[0];
            Cell? verified = readOwnedCell(first.X, first.Y);
            if (!verified.HasValue || verified.Value.Id != first.Id) return false;
            found.Add(first.Id, first); pending.Enqueue(first);
            int minX = first.X, maxX = first.X, minY = first.Y, maxY = first.Y;
            while (pending.Count != 0)
            {
                Cell cell = pending.Dequeue();
                for (int direction = 0; direction < 4; direction++)
                {
                    int x = cell.X + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
                    int y = cell.Y + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
                    Cell? next = readOwnedCell(x, y);
                    if (!next.HasValue) continue;
                    Cell n = next.Value;
                    if (n.X != x || n.Y != y) return false;
                    if (found.TryGetValue(n.Id, out Cell existing))
                    {
                        if (existing.X != x || existing.Y != y) return false;
                        continue;
                    }
                    if (found.Count == 49) return false;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    if (maxX - minX >= 7 || maxY - minY >= 7) return false;
                    found.Add(n.Id, n); pending.Enqueue(n);
                }
            }
            if (found.Count != 49 || maxX - minX != 6 || maxY - minY != 6) return false;
            foreach (Cell seed in seeds)
                if (!found.TryGetValue(seed.Id, out Cell cell) || cell.X != seed.X || cell.Y != seed.Y)
                    return false;
            // Stable order is shared by the signature and the published footprint.
            var result = new List<Cell>(found.Values);
            result.Sort((a, b) => a.Id.CompareTo(b.Id));
            // Re-read every cell before publication; topology changes remain fail-open.
            foreach (Cell cell in result)
            {
                Cell? current = readOwnedCell(cell.X, cell.Y);
                if (!current.HasValue || current.Value.Id != cell.Id) return false;
            }
            footprint = result.ToArray();
            return true;
        }
    }
}
