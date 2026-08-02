using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace MapParser.Core
{
    public static class MapFileReader
    {
        private const uint ScdeMagic = 0xfffffffeu;
        private const int MaximumFileSize = 512 * 1024 * 1024;
        private const int MaximumBlockSize = 128 * 1024 * 1024;
        private static readonly HashSet<uint> StandardDirectoryTags = new HashSet<uint> { 2036, 3036, 4036 };
        private static readonly HashSet<uint> SpecialDirectoryTags = new HashSet<uint> { 1076, 2100, 2108 };

        public static MapDocument Parse(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (!string.Equals(Path.GetExtension(path), ".map", StringComparison.OrdinalIgnoreCase))
                throw new MapUnsupportedFormatException("Only SCDE .map files are supported.");
            return ParseOwned(File.ReadAllBytes(path), Path.GetFullPath(path));
        }

        public static MapDocument Parse(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("The stream must be readable.", nameof(stream));

            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                if (memory.Length > MaximumFileSize)
                    throw new MapUnsupportedFormatException($"Map files larger than {MaximumFileSize} bytes are not supported.");
                return ParseOwned(memory.ToArray(), string.Empty);
            }
        }

        public static MapDocument Parse(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return ParseOwned((byte[])data.Clone(), string.Empty);
        }

        private static MapDocument ParseOwned(byte[] data, string sourceName)
        {
            if (data.Length > MaximumFileSize)
                throw new MapUnsupportedFormatException($"Map files larger than {MaximumFileSize} bytes are not supported.");
            var cursor = new Cursor(data);
            uint magic = cursor.ReadUInt32("SCDE magic");
            if (magic != ScdeMagic)
                throw new MapUnsupportedFormatException($"Unsupported map magic 0x{magic:X8}; expected SCDE magic 0x{ScdeMagic:X8}.");

            Block radar = cursor.ReadBlock("radar map");
            Block description = cursor.ReadBlock("description");
            Block u1 = cursor.ReadBlock("U1");
            Block u2 = cursor.ReadBlock("U2");
            Block u3 = cursor.ReadBlock("U3");
            Block u4 = cursor.ReadBlock("U4");

            if (u4.Size != 0)
            {
                uint restartSize = cursor.ReadUInt32("restart-info size");
                cursor.SkipChecked(restartSize, "restart-info payload");
                if (restartSize != 0)
                    cursor.ReadUInt32("restart-info terminator");
            }

            int directoryTagOffset = cursor.Position;
            uint directoryTag = cursor.ReadUInt32("directory tag");
            var preamble = new MapPreambleInfo(
                radar.Size, description.Size, u1.Size, u2.Size, u3.Size, u4.Size, directoryTagOffset);
            MapMetadata metadata = ReadMetadata(data, magic, radar, u2, u3, u4);

            if (SpecialDirectoryTags.Contains(directoryTag))
            {
                // These base/mission files share the preamble but not the normal section directory.
                return new MapDocument(
                    data,
                    sourceName,
                    MapFormatKind.CrusaderDefinitiveEditionSpecial,
                    preamble,
                    metadata,
                    null,
                    new List<MapSectionInfo>(),
                    directoryTagOffset,
                    data.Length - directoryTagOffset);
            }
            if (!StandardDirectoryTags.Contains(directoryTag))
                throw new MapUnsupportedFormatException($"Unsupported SCDE directory tag {directoryTag} at offset {directoryTagOffset}.");

            int capacity = checked(((int)directoryTag - 36) / 20);
            int bodySize = checked((int)directoryTag - 4);
            int bodyOffset = cursor.Position;
            cursor.Require(bodySize, "section directory");
            uint payloadSize = LittleEndian.ReadUInt32(data, bodyOffset);
            int sectionCount = ToInt32(LittleEndian.ReadUInt32(data, bodyOffset + 4), "section count");
            uint formatVersion = LittleEndian.ReadUInt32(data, bodyOffset + 8);
            if (sectionCount < 0 || sectionCount > capacity)
                throw new MapCorruptDataException($"Section count {sectionCount} exceeds directory capacity {capacity}.");

            int arrayOffset = checked(bodyOffset + 28);
            int payloadOffset = checked(bodyOffset + bodySize);
            RequireRange(data, payloadOffset, payloadSize, "section payload");
            int payloadEnd = checked(payloadOffset + (int)payloadSize);

            var sections = new List<MapSectionInfo>(sectionCount);
            var rawIds = new HashSet<int>();
            var logicalIds = new HashSet<int>();
            long totalUncompressedSize = 0;
            int expectedRelativeOffset = 0;
            for (int index = 0; index < sectionCount; index++)
            {
                int uncompressedSize = ReadDirectoryInt(data, arrayOffset, capacity, 0, index, "uncompressed size");
                int storedSize = ReadDirectoryInt(data, arrayOffset, capacity, 1, index, "stored size");
                int sectionId = ReadDirectoryInt(data, arrayOffset, capacity, 2, index, "section ID");
                int compressionFlag = ReadDirectoryInt(data, arrayOffset, capacity, 3, index, "compression flag");
                int relativeOffset = ReadDirectoryInt(data, arrayOffset, capacity, 4, index, "section offset");
                if (uncompressedSize < 0 || uncompressedSize > MaximumBlockSize || storedSize < 0 || storedSize > MaximumBlockSize)
                    throw new MapCorruptDataException($"Section {sectionId} has an unsupported size.");
                if (compressionFlag != 0 && compressionFlag != 1)
                    throw new MapCorruptDataException($"Section {sectionId} has unknown compression flag {compressionFlag}.");
                if (compressionFlag == 0 && storedSize != uncompressedSize)
                    throw new MapCorruptDataException($"Raw section {sectionId} stored and uncompressed sizes differ.");
                if (compressionFlag == 1 && storedSize < 12)
                    throw new MapCorruptDataException($"Compressed section {sectionId} is shorter than its header.");
                RequireRange(data, payloadOffset, payloadSize, relativeOffset, storedSize, $"section {sectionId}");
                if (relativeOffset != expectedRelativeOffset)
                {
                    throw new MapCorruptDataException(
                        $"Section {sectionId} starts at {relativeOffset}, expected contiguous offset {expectedRelativeOffset}.");
                }
                expectedRelativeOffset = checked(relativeOffset + storedSize);
                if (compressionFlag == 1)
                {
                    int absoluteOffset = checked(payloadOffset + relativeOffset);
                    uint declaredSize = LittleEndian.ReadUInt32(data, absoluteOffset);
                    uint declaredCompressedSize = LittleEndian.ReadUInt32(data, absoluteOffset + 4);
                    if (declaredSize != (uint)uncompressedSize || declaredCompressedSize != (uint)(storedSize - 12))
                    {
                        throw new MapCorruptDataException(
                            $"Section {sectionId} compressed header sizes disagree with its directory entry.");
                    }
                }

                int logicalId = MapSectionCatalog.GetLogicalSectionId(sectionId);
                if (!rawIds.Add(sectionId))
                    throw new MapCorruptDataException($"Duplicate section ID {sectionId}.");
                if (!logicalIds.Add(logicalId))
                    throw new MapCorruptDataException($"Duplicate logical section ID {logicalId}.");
                totalUncompressedSize += uncompressedSize;
                if (totalUncompressedSize > 1024L * 1024 * 1024)
                    throw new MapUnsupportedFormatException("Total declared uncompressed section size exceeds 1 GiB.");

                MapSectionStorageKind storageKind = compressionFlag == 0
                    ? MapSectionStorageKind.Raw
                    : IsKnownUnavailableSection1190(data, payloadOffset + relativeOffset, storedSize, sectionId, uncompressedSize)
                        ? MapSectionStorageKind.UnavailableZeroFilledDcl
                        : MapSectionStorageKind.PkwareDcl;
                sections.Add(new MapSectionInfo(
                    index,
                    sectionId,
                    logicalId,
                    storageKind,
                    uncompressedSize,
                    storedSize,
                    relativeOffset,
                    checked(payloadOffset + relativeOffset)));
            }
            if (expectedRelativeOffset != payloadSize)
                throw new MapCorruptDataException("Section ranges do not cover the declared payload exactly.");

            var directory = new MapDirectoryInfo(
                directoryTag, capacity, formatVersion, sectionCount, payloadSize, payloadOffset);
            return new MapDocument(
                data,
                sourceName,
                MapFormatKind.CrusaderDefinitiveEdition,
                preamble,
                metadata,
                directory,
                sections,
                payloadEnd,
                data.Length - payloadEnd);
        }

        private static MapMetadata ReadMetadata(byte[] data, uint magic, Block radar, Block u2, Block u3, Block u4)
        {
            int mapType = ReadOptionalInt32(data, u2, 0, 0);
            int maxPlayers = ReadOptionalInt32(data, u2, 24, 0);
            int missionType = ReadOptionalInt32(data, u3, 0, 0);
            int missionLockType = ReadOptionalInt32(data, u3, 8, 0);
            string fileName = string.Empty;
            if (u3.Size >= 16)
            {
                uint length = LittleEndian.ReadUInt32(data, u3.Offset + 12);
                if (length > 0 && length <= u3.Size - 16)
                    fileName = DecodeUtf8(data, u3.Offset + 16, checked((int)length));
            }

            bool isSkirmish = ReadOptionalInt32(data, u4, 4, 0) == 99;
            bool isBalanced = ReadOptionalInt32(data, u4, 12, 1) == 0;
            var keeps = new List<MapCoordinate>();
            if (u4.Size >= 80)
            {
                for (int index = 0; index < 8; index++)
                {
                    int offset = u4.Offset + 16 + index * 8;
                    keeps.Add(new MapCoordinate(
                        LittleEndian.ReadInt32(data, offset),
                        LittleEndian.ReadInt32(data, offset + 4)));
                }
            }
            int worldSize = ReadOptionalInt32(data, u4, 80, 0);
            return new MapMetadata(
                magic,
                radar.Size,
                mapType,
                maxPlayers,
                missionType,
                missionLockType,
                fileName,
                isSkirmish,
                isBalanced,
                new ReadOnlyCollection<MapCoordinate>(keeps),
                worldSize);
        }

        private static string DecodeUtf8(byte[] data, int offset, int length)
        {
            try
            {
                string value = new UTF8Encoding(false, true).GetString(data, offset, length);
                int terminator = value.IndexOf('\0');
                return terminator >= 0 ? value.Substring(0, terminator) : value;
            }
            catch (DecoderFallbackException ex)
            {
                throw new MapCorruptDataException("U3 standalone filename is not valid UTF-8.", ex);
            }
        }

        private static int ReadOptionalInt32(byte[] data, Block block, int relativeOffset, int fallback) =>
            relativeOffset >= 0 && relativeOffset <= block.Size - 4
                ? LittleEndian.ReadInt32(data, block.Offset + relativeOffset)
                : fallback;

        private static int ReadDirectoryInt(
            byte[] data, int arrayOffset, int capacity, int arrayIndex, int itemIndex, string field)
        {
            uint value = LittleEndian.ReadUInt32(data, checked(arrayOffset + ((arrayIndex * capacity + itemIndex) * 4)));
            return ToInt32(value, field);
        }

        private static bool IsKnownUnavailableSection1190(
            byte[] data, int offset, int storedSize, int sectionId, int uncompressedSize)
        {
            if (sectionId != 1190 || storedSize <= 12 ||
                LittleEndian.ReadUInt32(data, offset) != (uint)uncompressedSize ||
                LittleEndian.ReadUInt32(data, offset + 4) != (uint)(storedSize - 12))
                return false;

            // Several shipped maps contain this exact writer anomaly: the declared DCL payload is only zero padding.
            for (int index = offset + 12; index < offset + storedSize; index++)
            {
                if (data[index] != 0)
                    return false;
            }
            return true;
        }

        private static int ToInt32(uint value, string field)
        {
            if (value > int.MaxValue)
                throw new MapCorruptDataException($"Declared {field} {value} exceeds the supported range.");
            return (int)value;
        }

        private static void RequireRange(byte[] data, int offset, uint size, string subject)
        {
            if (size > int.MaxValue || offset < 0 || (long)offset + size > data.Length)
                throw new MapCorruptDataException($"Declared {subject} exceeds file bounds.");
        }

        private static void RequireRange(
            byte[] data, int payloadOffset, uint payloadSize, int relativeOffset, int size, string subject)
        {
            if (relativeOffset < 0 || size < 0 || (long)relativeOffset + size > payloadSize ||
                (long)payloadOffset + relativeOffset + size > data.Length)
                throw new MapCorruptDataException($"Declared {subject} exceeds payload bounds.");
        }

        private readonly struct Block
        {
            public Block(uint size, int offset)
            {
                Size = size;
                Offset = offset;
            }

            public uint Size { get; }
            public int Offset { get; }
        }

        private sealed class Cursor
        {
            private readonly byte[] data;

            public Cursor(byte[] data)
            {
                this.data = data;
            }

            public int Position { get; private set; }

            public uint ReadUInt32(string subject)
            {
                Require(4, subject);
                uint value = LittleEndian.ReadUInt32(data, Position);
                Position += 4;
                return value;
            }

            public Block ReadBlock(string subject)
            {
                uint size = ReadUInt32(subject + " size");
                if (size > MaximumBlockSize)
                    throw new MapUnsupportedFormatException($"{subject} block exceeds the supported size.");
                int offset = Position;
                SkipChecked(size, subject + " block");
                return new Block(size, offset);
            }

            public void SkipChecked(uint size, string subject)
            {
                RequireRange(data, Position, size, subject);
                Position = checked(Position + (int)size);
            }

            public void Require(int size, string subject)
            {
                if (size < 0 || Position > data.Length - size)
                    throw new MapCorruptDataException($"Map ended while reading {subject} at offset {Position}.");
            }
        }
    }
}
