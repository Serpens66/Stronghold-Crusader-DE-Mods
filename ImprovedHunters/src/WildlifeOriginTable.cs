using System;
using System.Collections.Generic;
using System.IO;

namespace ImprovedHunters
{
    /// <summary>One deer or goat herd and where it started. An extinct herd keeps its entry for its return.</summary>
    internal struct WildlifeOrigin
    {
        public const int NotExtinct = -1;

        public bool IsDeer;
        public bool Respawns;
        public short X;
        public short Y;
        public int Herd;
        public int HerdSerial;
        public int ExtinctSinceTick;
    }

    /// <summary>
    /// The start places of deer and goat herds, for one mission. Nothing in the game keeps them once a
    /// herd has moved or died, so they go into the save. Filled and changed on the simulation thread from
    /// simulation data, so all peers hold the same table; the save thread only reads it under SyncRoot.
    /// </summary>
    internal sealed class WildlifeOriginTable
    {
        // The game has this many herd records, so a mission rarely comes near it.
        public const int HerdCapacity = 0x1194;
        private const byte RecordVersion = 1;
        private const int HeaderBytes = 5;
        private const int EntryBytes = 18;
        internal const int MaximumEncodedBytes = HeaderBytes + EntryBytes * HerdCapacity;
        private const int GridSize = 800;
        private const byte IsDeerFlag = 1;
        private const byte RespawnsFlag = 2;

        private sealed class Content
        {
            internal readonly List<WildlifeOrigin> Origins = new List<WildlifeOrigin>();
            internal readonly int[] OriginByHerd = new int[HerdCapacity];
            internal bool IsCaptured;
        }

        private volatile Content content = new Content();
        private List<WildlifeOrigin> origins => content.Origins;
        private int[] originByHerd => content.OriginByHerd;
        public WildlifeOriginTable() => Clear();

        public object SyncRoot { get; } = new object();

        /// <summary>False until this mission's herds were read or restored from its save.</summary>
        public bool IsCaptured => content.IsCaptured;
        internal bool ResyncReady { get; set; }
        internal volatile bool Repairing;

        public int Count => origins.Count;

        public WildlifeOrigin this[int index]
        {
            get => origins[index];
            set
            {
                origins[index] = value;
                // An extinct entry may name a record that a newer herd uses by now.
                if (value.ExtinctSinceTick == WildlifeOrigin.NotExtinct)
                    originByHerd[value.Herd] = index;
            }
        }

        public void MarkCaptured() => content.IsCaptured = true;

        // The stable table and its lock are shared by the respawn and route features.
        internal void PublishRepair(WildlifeOriginTable prepared) => content = prepared.content;

        public void Clear()
        {
            origins.Clear();
            for (int herd = 0; herd < originByHerd.Length; herd++)
                originByHerd[herd] = -1;
            content.IsCaptured = false;
        }

        /// <summary>A serial that no longer matches means the game gave the record to a new herd.</summary>
        public bool TryFind(int herd, int herdSerial, out WildlifeOrigin origin)
        {
            origin = default;
            if (herd <= 0 || herd >= HerdCapacity)
                return false;

            int index = originByHerd[herd];
            if (index >= 0 && origins[index].Herd == herd && origins[index].HerdSerial == herdSerial)
            {
                origin = origins[index];
                return true;
            }

            // After a load the shortcut can name another entry that used this record earlier.
            for (index = 0; index < origins.Count; index++)
            {
                if (origins[index].Herd == herd && origins[index].HerdSerial == herdSerial)
                {
                    originByHerd[herd] = index;
                    origin = origins[index];
                    return true;
                }
            }

            return false;
        }

        /// <summary>A herd the map started with, also when its first route already gave it an entry.</summary>
        public bool TryMarkRespawning(int herd, int herdSerial)
        {
            if (!TryFind(herd, herdSerial, out WildlifeOrigin origin))
                return false;

            origin.Respawns = true;
            origins[originByHerd[herd]] = origin;
            return true;
        }

        public bool TryAdd(WildlifeOrigin origin)
        {
            if (!IsValid(origin) || origins.Count >= HerdCapacity)
                return false;

            originByHerd[origin.Herd] = origins.Count;
            origins.Add(origin);
            return true;
        }

        public byte[] Encode() => Encode(content.Origins);

        internal byte[] EncodeCaptured()
        {
            Content snapshot = content;
            return snapshot.IsCaptured ? Encode(snapshot.Origins) : null;
        }

        private static byte[] Encode(List<WildlifeOrigin> entries)
        {
            using (var stream = new MemoryStream(HeaderBytes + entries.Count * EntryBytes))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(RecordVersion);
                writer.Write(entries.Count);
                foreach (WildlifeOrigin origin in entries)
                {
                    writer.Write((byte)((origin.IsDeer ? IsDeerFlag : 0) | (origin.Respawns ? RespawnsFlag : 0)));
                    writer.Write((byte)0);
                    writer.Write(origin.X);
                    writer.Write(origin.Y);
                    writer.Write(origin.Herd);
                    writer.Write(origin.HerdSerial);
                    writer.Write(origin.ExtinctSinceTick);
                }

                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>Leaves the table cleared and returns false for data that is not a whole valid record.</summary>
        public bool TryRestore(byte[] data, bool strict = false)
        {
            Clear();
            if (data == null || data.Length < HeaderBytes || data[0] != RecordVersion)
                return false;

            int count = BitConverter.ToInt32(data, 1);
            if (count < 0 || count > HerdCapacity || data.Length != HeaderBytes + count * EntryBytes)
                return false;

            using (var reader = new BinaryReader(new MemoryStream(data, HeaderBytes, data.Length - HeaderBytes)))
            {
                for (int index = 0; index < count; index++)
                {
                    byte flags = reader.ReadByte();
                    byte reserved = reader.ReadByte();
                    var origin = new WildlifeOrigin
                    {
                        IsDeer = (flags & IsDeerFlag) != 0,
                        Respawns = (flags & RespawnsFlag) != 0,
                        X = reader.ReadInt16(),
                        Y = reader.ReadInt16(),
                        Herd = reader.ReadInt32(),
                        HerdSerial = reader.ReadInt32(),
                        ExtinctSinceTick = reader.ReadInt32()
                    };
                    if ((strict && (reserved != 0 || (flags & ~(IsDeerFlag | RespawnsFlag)) != 0 ||
                        origin.HerdSerial == 0)) || !TryAdd(origin))
                    {
                        Clear();
                        return false;
                    }
                }
            }

            MarkCaptured();
            return true;
        }

        private static bool IsValid(WildlifeOrigin origin) =>
            origin.Herd > 0 && origin.Herd < HerdCapacity &&
            origin.X >= 0 && origin.X < GridSize &&
            origin.Y >= 0 && origin.Y < GridSize;
    }
}
