using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MapParser.Core
{
    public sealed class MapRockRecord
    {
        public MapRockRecord(
            int recordIndex,
            int gfx,
            uint tileId,
            int uid,
            short marker,
            short unknownGmid,
            short type,
            ushort x,
            ushort y,
            short size,
            short orientation)
        {
            if (recordIndex < 0 || recordIndex >= MapRockRecords.RecordCount)
                throw new ArgumentOutOfRangeException(nameof(recordIndex));

            RecordIndex = recordIndex;
            Gfx = gfx;
            TileId = tileId;
            Uid = uid;
            Marker = marker;
            UnknownGmid = unknownGmid;
            Type = type;
            X = x;
            Y = y;
            Size = size;
            Orientation = orientation;
        }

        public int RecordIndex { get; }
        public int Gfx { get; }
        public uint TileId { get; }
        public int Uid { get; }
        public short Marker { get; }
        public short UnknownGmid { get; }
        public short Type { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public short Size { get; }
        public short Orientation { get; }
        public bool IsActive => Marker != 0 && TileId != 0 && Size > 0;
    }

    public sealed class MapRockRecords
    {
        public const int RecordCount = 4000;
        public const int RecordSize = 32;

        private MapRockRecords(IReadOnlyList<MapRockRecord> records)
        {
            Records = records ?? throw new ArgumentNullException(nameof(records));
        }

        public IReadOnlyList<MapRockRecord> Records { get; }

        internal static MapRockRecords Create(MapDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            MapSectionInfo section = document.GetLogicalSection(MapSectionCatalog.Rocks);
            int expectedSize = RecordCount * RecordSize;
            if (!section.IsContentAvailable || section.UncompressedSize != expectedSize)
            {
                throw new InvalidOperationException(
                    $"Section {section.SectionId} cannot provide {RecordCount} rock records.");
            }

            byte[] data = section.ReadContent();
            var records = new MapRockRecord[RecordCount];
            for (int recordIndex = 0; recordIndex < records.Length; recordIndex++)
            {
                int offset = recordIndex * RecordSize;
                records[recordIndex] = new MapRockRecord(
                    recordIndex,
                    LittleEndian.ReadInt32(data, offset),
                    LittleEndian.ReadUInt32(data, offset + 4),
                    LittleEndian.ReadInt32(data, offset + 8),
                    LittleEndian.ReadInt16(data, offset + 12),
                    LittleEndian.ReadInt16(data, offset + 14),
                    LittleEndian.ReadInt16(data, offset + 16),
                    LittleEndian.ReadUInt16(data, offset + 18),
                    LittleEndian.ReadUInt16(data, offset + 20),
                    LittleEndian.ReadInt16(data, offset + 22),
                    LittleEndian.ReadInt16(data, offset + 24));
            }

            return new MapRockRecords(
                new ReadOnlyCollection<MapRockRecord>(records));
        }
    }
}
