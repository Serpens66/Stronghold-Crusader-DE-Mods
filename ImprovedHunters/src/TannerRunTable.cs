using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ImprovedHunters
{
    /// <summary>
    /// Per-Tannery simulation state, indexed by building ID and read directly by the native stubs.
    /// It lives here and not in the building record so the workshop's produced-good fields stay
    /// free for Vanilla and for mods that add other goods to the Tannery. An entry only counts
    /// for the building with its global ID, so a new building on a reused slot starts clean.
    /// Written only from the Chore, the native stubs and archive loads; identical on all peers.
    /// </summary>
    internal sealed unsafe class TannerRunTable
    {
        // Far above the game's building limit; a larger ID simply stays on the workshop's own good.
        public const int Capacity = 0x2000;
        public const int EntrySize = 8;
        public const int GlobalIdOffset = 0;
        public const int MeatRunsOffset = 4;
        public const int MeatTripOffset = 5;

        private readonly byte* entries;

        public TannerRunTable()
        {
            // Never freed: published native stubs read it until the process ends.
            entries = (byte*)Marshal.AllocHGlobal(Capacity * EntrySize);
            Clear();
        }

        public ulong Address => (ulong)entries;

        public static bool IsValidId(int buildingId) => buildingId >= 0 && buildingId < Capacity;

        public void Clear() => new Span<byte>(entries, Capacity * EntrySize).Clear();

        public int GetMeatRuns(int buildingId, uint globalId)
        {
            if (!IsValidId(buildingId))
                return 0;

            byte* entry = entries + buildingId * EntrySize;
            return *(uint*)(entry + GlobalIdOffset) == globalId ? entry[MeatRunsOffset] : 0;
        }

        /// <summary>A Tanner on his way keeps the good he carries; only later runs follow the new value.</summary>
        public void SetMeatRuns(int buildingId, uint globalId, int meatRuns)
        {
            if (!IsValidId(buildingId) || !TannerMeatPolicy.IsValidMeatRuns(meatRuns))
                return;

            byte* entry = entries + buildingId * EntrySize;
            if (*(uint*)(entry + GlobalIdOffset) != globalId)
            {
                entry[MeatTripOffset] = 0;
                *(uint*)(entry + GlobalIdOffset) = globalId;
            }

            entry[MeatRunsOffset] = (byte)meatRuns;
        }

        /// <summary>Ascending by ID, so all peers write the same bytes for the same state.</summary>
        public byte[] Encode()
        {
            var used = new List<int>();
            for (int id = 0; id < Capacity; id++)
            {
                byte* entry = entries + id * EntrySize;
                if (entry[MeatRunsOffset] != 0 || entry[MeatTripOffset] != 0)
                    used.Add(id);
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(used.Count);
                foreach (int id in used)
                {
                    byte* entry = entries + id * EntrySize;
                    writer.Write((ushort)id);
                    writer.Write(*(uint*)(entry + GlobalIdOffset));
                    writer.Write(entry[MeatRunsOffset]);
                    writer.Write(entry[MeatTripOffset]);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>Replaces the table. Damaged data leaves it empty and returns false.</summary>
        public bool TryRestore(byte[] data)
        {
            Clear();
            if (data == null || data.Length < sizeof(int))
                return false;

            const int EncodedEntrySize = sizeof(ushort) + sizeof(uint) + 2;
            int count = BitConverter.ToInt32(data, 0);
            if (count < 0 || count > Capacity || data.Length != sizeof(int) + count * EncodedEntrySize)
                return false;

            for (int index = 0; index < count; index++)
            {
                int offset = sizeof(int) + index * EncodedEntrySize;
                int id = BitConverter.ToUInt16(data, offset);
                int meatRuns = data[offset + 6];
                int meatTrip = data[offset + 7];
                if (!IsValidId(id) || !TannerMeatPolicy.IsValidMeatRuns(meatRuns) || meatTrip > 1)
                {
                    Clear();
                    return false;
                }

                byte* entry = entries + id * EntrySize;
                *(uint*)(entry + GlobalIdOffset) = BitConverter.ToUInt32(data, offset + 2);
                entry[MeatRunsOffset] = (byte)meatRuns;
                entry[MeatTripOffset] = (byte)meatTrip;
            }

            return true;
        }
    }
}
