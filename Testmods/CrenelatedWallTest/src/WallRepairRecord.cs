using System;
using System.Collections.Generic;
using System.IO;

namespace CrenelatedWallTest
{
    // One nibble per packed cell: zero means unmanaged, 1-8 store the placement owner.
    // Coordinates and shape are read from the repaired map. Empty active maps need only a header.
    internal static class WallRepairRecord
    {
        private const int HeaderBytes = sizeof(ushort);
        internal const int MaximumBytes = HeaderBytes + (WallPolicy.MaximumCells + 1) / 2;

        internal static byte[] Capture(int mapSize, IEnumerable<WallCell> managed, IWallWorld world)
        {
            if (mapSize <= 0 || mapSize > 800) throw new InvalidDataException("Invalid crenel map size.");
            var bytes = new byte[HeaderBytes];
            foreach (WallCell entry in managed)
            {
                if (!world.TryRead(entry.X, entry.Y, out WallCell live) || !WallPolicy.Crenel(live) ||
                    live.Id != entry.Id || live.Owner != entry.Owner) continue;
                if ((uint)entry.Id >= WallPolicy.MaximumCells || entry.Owner < 1 || entry.Owner > 8)
                    throw new InvalidDataException("Invalid crenel placement identity.");
                if (bytes.Length == HeaderBytes) bytes = new byte[MaximumBytes];
                int index = HeaderBytes + entry.Id / 2;
                int shift = (entry.Id & 1) * 4;
                bytes[index] |= (byte)(entry.Owner << shift);
            }
            bytes[0] = (byte)mapSize;
            bytes[1] = (byte)(mapSize >> 8);
            return bytes;
        }

        internal static SortedDictionary<int, WallCell> Prepare(ReadOnlySpan<byte> bytes, int mapSize, Func<int, WallCell> read)
        {
            if ((bytes.Length != HeaderBytes && bytes.Length != MaximumBytes) || mapSize <= 0 || mapSize > 800 ||
                (bytes[0] | (bytes[1] << 8)) != mapSize)
                throw new InvalidDataException("Crenel repair map identity or record size differs.");
            var replacement = new SortedDictionary<int, WallCell>();
            if (bytes.Length == HeaderBytes) return replacement;
            for (int id = 0; id < WallPolicy.MaximumCells; id++)
            {
                int owner = (bytes[HeaderBytes + id / 2] >> ((id & 1) * 4)) & 15;
                if (owner == 0) continue;
                if (owner > 8) throw new InvalidDataException("Invalid crenel placement owner.");
                WallCell live = read(id);
                if (!WallPolicy.Crenel(live) || live.Id != id || live.Owner != owner) continue;
                replacement.Add(id, new WallCell { Id = id, X = live.X, Y = live.Y, Owner = owner });
            }
            return replacement;
        }
    }
}
