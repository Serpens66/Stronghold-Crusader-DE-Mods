using System;
using System.Collections.Generic;
using System.IO;

namespace BugfixesAndQoL.Crenels
{
    // Save record: which tiles the feature manages, never their heights (heights follow from the map).
    internal static class CrenelSaveRecord
    {
        internal const int Schema = 1;

        // cells in ascending id order (the runtime's managed set is a SortedDictionary).
        internal static int[][] Capture(int mapSize, IEnumerable<CrenelCell> cells)
        {
            var rows = new List<int[]> { new[] { Schema, mapSize } };
            foreach (CrenelCell cell in cells)
                rows.Add(new[] { cell.Id, cell.X, cell.Y, cell.Owner });
            return rows.ToArray();
        }

        internal static List<CrenelCell> Validate(int[][] rows, int mapSize, ICrenelWorld world)
        {
            if (rows == null || rows.Length == 0 || rows.Length > CrenelPolicy.MaximumCells + 1 ||
                rows[0] == null || rows[0].Length != 2 || rows[0][0] != Schema || rows[0][1] != mapSize)
                throw new InvalidDataException("Crenel save schema, size or map identity is invalid.");
            var cells = new List<CrenelCell>(); int previous = -1;
            for (int i = 1; i < rows.Length; i++)
            {
                int[] row = rows[i];
                if (row == null || row.Length != 4 || row[0] <= previous || row[0] >= CrenelPolicy.MaximumCells ||
                    row[1] < 0 || row[1] >= 800 || row[2] < 0 || row[2] >= 800 || row[3] < 1 || row[3] > 8)
                    throw new InvalidDataException("Invalid or duplicate saved crenel at record " + i);
                previous = row[0];
                if (!world.TryRead(row[1], row[2], out CrenelCell cell) || cell.Id != row[0])
                    throw new InvalidDataException("Saved crenel coordinate/id mapping changed.");
                // Removed or transferred cells must never be resurrected by metadata.
                if (CrenelPolicy.Crenel(cell) && cell.Owner == row[3]) cells.Add(cell.Identity());
            }
            return cells;
        }
    }

    // Multiplayer resync record: one nibble per packed tile, zero means unmanaged, 1-8 the owner.
    // Coordinates and shape are read from the repaired map. An inactive feature sends only the header.
    internal static class CrenelRepairRecord
    {
        private const int HeaderBytes = sizeof(ushort);
        internal const int MaximumBytes = HeaderBytes + (CrenelPolicy.MaximumCells + 1) / 2;

        internal static byte[] Capture(int mapSize, IEnumerable<CrenelCell> managed, ICrenelWorld world)
        {
            if (mapSize <= 0 || mapSize > 800) throw new InvalidDataException("Invalid crenel map size.");
            var bytes = new byte[HeaderBytes];
            foreach (CrenelCell entry in managed)
            {
                if (!world.TryRead(entry.X, entry.Y, out CrenelCell live) || !CrenelPolicy.Crenel(live) ||
                    live.Id != entry.Id || live.Owner != entry.Owner) continue;
                if ((uint)entry.Id >= CrenelPolicy.MaximumCells || entry.Owner < 1 || entry.Owner > 8)
                    throw new InvalidDataException("Invalid crenel placement identity.");
                if (bytes.Length == HeaderBytes) bytes = new byte[MaximumBytes];
                bytes[HeaderBytes + entry.Id / 2] |= (byte)(entry.Owner << ((entry.Id & 1) * 4));
            }
            bytes[0] = (byte)mapSize;
            bytes[1] = (byte)(mapSize >> 8);
            return bytes;
        }

        internal static SortedDictionary<int, CrenelCell> Prepare(ReadOnlySpan<byte> bytes, int mapSize, Func<int, CrenelCell> read)
        {
            if ((bytes.Length != HeaderBytes && bytes.Length != MaximumBytes) || mapSize <= 0 || mapSize > 800 ||
                (bytes[0] | (bytes[1] << 8)) != mapSize)
                throw new InvalidDataException("Crenel repair map identity or record size differs.");
            var replacement = new SortedDictionary<int, CrenelCell>();
            if (bytes.Length == HeaderBytes) return replacement;
            for (int id = 0; id < CrenelPolicy.MaximumCells; id++)
            {
                int owner = (bytes[HeaderBytes + id / 2] >> ((id & 1) * 4)) & 15;
                if (owner == 0) continue;
                if (owner > 8) throw new InvalidDataException("Invalid crenel placement owner.");
                CrenelCell live = read(id);
                if (CrenelPolicy.Crenel(live) && live.Id == id && live.Owner == owner) replacement.Add(id, live.Identity());
            }
            return replacement;
        }
    }
}
