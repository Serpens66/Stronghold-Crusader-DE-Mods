using System;
using System.IO;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.MoatTerrain
{
    // This is our state, never stored in spare/unknown fields belonging to Vanilla.
    internal static class TerrainState
    {
        internal const int Capacity = 320800;
        internal const int DataOffset = 64;
        internal const int BufferBytes = DataOffset + Capacity * 2;
        internal const ushort Planned = 0x100, Dug = 0x200, Bridge = 0x400, Preparing = 0x800;
        internal const ushort StageMask = 0xF00;
        private const int HeaderBytes = 16;
        internal const int RecordBytes = HeaderBytes + Capacity * 2;
        private const int Signature = 0x3152544D; // MTR1, little endian; record version is owned by APIShared.

        internal static bool IsPersistent(ushort value) => value == 0 ||
            (value & 0xFF00) == Planned || (value & 0xFF00) == Dug || (value & 0xFF00) == (Dug | Bridge);

        internal static ushort ReconcileRepairEntry(ushort value, TilePropertyFlag flags, bool liveBridge)
        {
            if ((value & Bridge) != 0)
            {
                if (liveBridge) return value;
                value = (value & Dug) != 0 ? (ushort)(value & ~Bridge) : (ushort)0;
            }
            if (((flags & TilePropertyFlag.IsMoat) != 0 && (value & Dug) != 0) ||
                ((flags & TilePropertyFlag.PlannedMoat) != 0 && (value & Planned) != 0)) return value;
            return 0;
        }

        internal static byte[] Encode(ushort[] tiles, int mapSize, bool repair = false)
        {
            if (tiles == null || tiles.Length != Capacity || mapSize <= 0 || mapSize > 800)
                throw new InvalidDataException("Invalid moat terrain layout.");
            bool any = false;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (!IsPersistent(tiles[i]) && !(repair && tiles[i] == Bridge))
                    throw new InvalidDataException("Invalid moat terrain record state.");
                any |= tiles[i] != 0;
            }
            if (!any && !repair) return null; // The caller still writes an explicit empty versioned record.
            byte[] bytes = new byte[RecordBytes];
            Put(bytes, 0, Signature); Put(bytes, 4, Capacity); Put(bytes, 8, mapSize); Put(bytes, 12, 2);
            for (int i = 0; i < Capacity; i++)
            {
                bytes[HeaderBytes + i * 2] = (byte)tiles[i];
                bytes[HeaderBytes + i * 2 + 1] = (byte)(tiles[i] >> 8);
            }
            return bytes;
        }

        internal static ushort[] Decode(ReadOnlySpan<byte> bytes, out int mapSize, bool repair = false)
        {
            mapSize = 0;
            if (bytes.Length != RecordBytes ||
                Get(bytes, 0) != Signature || Get(bytes, 4) != Capacity || Get(bytes, 12) != 2)
                throw new InvalidDataException("Unsupported moat terrain record layout.");
            mapSize = Get(bytes, 8);
            if (mapSize <= 0 || mapSize > 800) throw new InvalidDataException("Invalid saved map size.");
            ushort[] tiles = new ushort[Capacity];
            for (int i = 0; i < Capacity; i++)
            {
                ushort value = (ushort)(bytes[HeaderBytes + i * 2] | bytes[HeaderBytes + i * 2 + 1] << 8);
                if (!IsPersistent(value) && !(repair && value == Bridge))
                    throw new InvalidDataException("Invalid moat terrain record state.");
                tiles[i] = value;
            }
            return tiles;
        }

        private static int Get(ReadOnlySpan<byte> bytes, int offset) => bytes[offset] | bytes[offset + 1] << 8 |
            bytes[offset + 2] << 16 | bytes[offset + 3] << 24;
        private static void Put(byte[] bytes, int offset, int value)
        { for (int i = 0; i < 4; i++) bytes[offset + i] = (byte)(value >> (i * 8)); }
    }
}
