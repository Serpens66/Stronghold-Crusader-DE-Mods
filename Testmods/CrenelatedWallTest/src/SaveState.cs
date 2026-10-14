using System;
using System.Collections.Generic;
using System.Linq;

namespace CrenelatedWallTest
{
    internal static class SaveState
    {
        internal const int Schema = 1;
        internal static int[][] Capture(int mapSize, IEnumerable<WallCell> cells)
        {
            var rows = new List<int[]> { new[] { Schema, mapSize } };
            foreach (WallCell cell in cells.OrderBy(c => c.Id))
                rows.Add(new[] { cell.Id, cell.X, cell.Y, cell.Owner, cell.High ? 1 : 0 });
            return rows.ToArray();
        }
        internal static List<WallCell> Validate(int[][] rows, int mapSize, IWallWorld world)
        {
            if (rows == null || rows.Length == 0 || rows.Length > WallPolicy.MaximumCells + 1 ||
                rows[0] == null || rows[0].Length != 2 || rows[0][0] != Schema || rows[0][1] != mapSize)
                throw new InvalidOperationException("Save schema, size or map identity is invalid.");
            var cells = new List<WallCell>(); int previous = -1;
            for (int i = 1; i < rows.Length; i++)
            {
                int[] row = rows[i];
                if (row == null || row.Length != 5 || row[0] <= previous || row[0] >= WallPolicy.MaximumCells ||
                    row[1] < 0 || row[1] >= 800 || row[2] < 0 || row[2] >= 800 || row[3] < 1 || row[3] > 8 || row[4] < 0 || row[4] > 1)
                    throw new InvalidOperationException("Invalid or duplicate saved tile at record " + i);
                previous = row[0];
                if (!world.TryRead(row[1], row[2], out WallCell cell) || cell.Id != row[0])
                    throw new InvalidOperationException("Saved coordinate/id mapping changed.");
                // Removed, damaged or transferred cells must never be resurrected by metadata.
                if (!cell.IsWall || !cell.IsCrenel || cell.Owner != row[3] || cell.Damaged || cell.Structure) continue;
                cell = cell.Copy(); cell.High = row[4] != 0; cells.Add(cell);
            }
            return cells;
        }
    }
}
