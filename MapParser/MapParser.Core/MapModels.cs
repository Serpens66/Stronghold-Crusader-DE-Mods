using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MapParser.Core
{
    public enum MapFormatKind
    {
        CrusaderDefinitiveEdition,
        CrusaderDefinitiveEditionSpecial
    }

    public enum MapSectionStorageKind
    {
        Raw,
        PkwareDcl,
        UnavailableZeroFilledDcl
    }

    public readonly struct MapCoordinate : IEquatable<MapCoordinate>
    {
        public MapCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(MapCoordinate other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is MapCoordinate other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X},{Y})";
    }

    public sealed class MapMetadata
    {
        internal MapMetadata(
            uint magic,
            uint radarBlockSize,
            int mapType,
            int maxPlayers,
            int scenarioMissionType,
            int missionLockType,
            string standaloneFileName,
            bool isSkirmishMap,
            bool isBalancedMap,
            IReadOnlyList<MapCoordinate> keepLocations,
            int worldSize)
        {
            Magic = magic;
            RadarBlockSize = radarBlockSize;
            MapType = mapType;
            MaxPlayers = maxPlayers;
            ScenarioMissionType = scenarioMissionType;
            MissionLockType = missionLockType;
            StandaloneFileName = standaloneFileName ?? string.Empty;
            IsSkirmishMap = isSkirmishMap;
            IsBalancedMap = isBalancedMap;
            KeepLocations = keepLocations ?? Array.Empty<MapCoordinate>();
            WorldSize = worldSize;
        }

        public uint Magic { get; }
        public uint RadarBlockSize { get; }
        public int MapType { get; }
        public int MaxPlayers { get; }
        public int ScenarioMissionType { get; }
        public int MissionLockType { get; }
        public string StandaloneFileName { get; }
        public bool IsSkirmishMap { get; }
        public bool IsBalancedMap { get; }
        public IReadOnlyList<MapCoordinate> KeepLocations { get; }
        public int WorldSize { get; }
    }

    public sealed class MapPreambleInfo
    {
        internal MapPreambleInfo(
            uint radarBlockSize,
            uint descriptionBlockSize,
            uint u1BlockSize,
            uint u2BlockSize,
            uint u3BlockSize,
            uint u4BlockSize,
            int directoryTagOffset)
        {
            RadarBlockSize = radarBlockSize;
            DescriptionBlockSize = descriptionBlockSize;
            U1BlockSize = u1BlockSize;
            U2BlockSize = u2BlockSize;
            U3BlockSize = u3BlockSize;
            U4BlockSize = u4BlockSize;
            DirectoryTagOffset = directoryTagOffset;
        }

        public uint RadarBlockSize { get; }
        public uint DescriptionBlockSize { get; }
        public uint U1BlockSize { get; }
        public uint U2BlockSize { get; }
        public uint U3BlockSize { get; }
        public uint U4BlockSize { get; }
        public int DirectoryTagOffset { get; }
    }

    public sealed class MapDirectoryInfo
    {
        internal MapDirectoryInfo(
            uint directoryTag,
            int capacity,
            uint formatVersion,
            int sectionCount,
            uint payloadSize,
            int payloadOffset)
        {
            DirectoryTag = directoryTag;
            Capacity = capacity;
            FormatVersion = formatVersion;
            SectionCount = sectionCount;
            PayloadSize = payloadSize;
            PayloadOffset = payloadOffset;
        }

        public uint DirectoryTag { get; }
        public int Capacity { get; }
        public uint FormatVersion { get; }
        public int SectionCount { get; }
        public uint PayloadSize { get; }
        public int PayloadOffset { get; }
    }

    public sealed class MapSectionInfo
    {
        private MapDocument owner;
        private readonly object sync = new object();
        private byte[] cachedContent;

        internal MapSectionInfo(
            int index,
            int sectionId,
            int logicalSectionId,
            MapSectionStorageKind storageKind,
            int uncompressedSize,
            int storedSize,
            int payloadRelativeOffset,
            int absoluteOffset)
        {
            Index = index;
            SectionId = sectionId;
            LogicalSectionId = logicalSectionId;
            StorageKind = storageKind;
            UncompressedSize = uncompressedSize;
            StoredSize = storedSize;
            PayloadRelativeOffset = payloadRelativeOffset;
            AbsoluteOffset = absoluteOffset;
        }

        public int Index { get; }
        public int SectionId { get; }
        public int LogicalSectionId { get; }
        public MapSectionStorageKind StorageKind { get; }
        public int UncompressedSize { get; }
        public int StoredSize { get; }
        public int PayloadRelativeOffset { get; }
        public int AbsoluteOffset { get; }
        public bool IsContentAvailable => StorageKind != MapSectionStorageKind.UnavailableZeroFilledDcl;

        public byte[] ReadContent()
        {
            byte[] content = GetOrReadContent();
            return (byte[])content.Clone();
        }

        internal byte[] GetOrReadContent()
        {
            if (cachedContent != null)
                return cachedContent;

            lock (sync)
            {
                if (cachedContent == null)
                    cachedContent = owner.DecodeSection(this);
                return cachedContent;
            }
        }

        internal void Attach(MapDocument document)
        {
            if (owner != null)
                throw new InvalidOperationException("Map section is already attached.");
            owner = document ?? throw new ArgumentNullException(nameof(document));
        }
    }

    public sealed class MapDocument
    {
        private readonly byte[] fileBytes;
        private readonly Dictionary<int, MapSectionInfo> sectionsById;
        private readonly Dictionary<int, MapSectionInfo> sectionsByLogicalId;

        internal MapDocument(
            byte[] fileBytes,
            string sourceName,
            MapFormatKind formatKind,
            MapPreambleInfo preamble,
            MapMetadata metadata,
            MapDirectoryInfo directory,
            IList<MapSectionInfo> sections,
            int opaqueTailOffset,
            int opaqueTailLength)
        {
            this.fileBytes = fileBytes;
            SourceName = sourceName ?? string.Empty;
            FormatKind = formatKind;
            Preamble = preamble;
            Metadata = metadata;
            Directory = directory;
            Sections = new ReadOnlyCollection<MapSectionInfo>(sections);
            OpaqueTailOffset = opaqueTailOffset;
            OpaqueTailLength = opaqueTailLength;
            sectionsById = new Dictionary<int, MapSectionInfo>();
            sectionsByLogicalId = new Dictionary<int, MapSectionInfo>();
            foreach (MapSectionInfo section in sections)
            {
                section.Attach(this);
                sectionsById.Add(section.SectionId, section);
                sectionsByLogicalId.Add(section.LogicalSectionId, section);
            }
        }

        public string SourceName { get; }
        public MapFormatKind FormatKind { get; }
        public MapPreambleInfo Preamble { get; }
        public MapMetadata Metadata { get; }
        public MapDirectoryInfo Directory { get; }
        public IReadOnlyList<MapSectionInfo> Sections { get; }
        public int OpaqueTailOffset { get; }
        public int OpaqueTailLength { get; }
        public bool SectionsAvailable => Directory != null;
        public bool HasPlacementLayers => SectionsAvailable && MapTileLayers.CanCreate(this);
        public bool HasPlacementSnapshot => MapPlacementSnapshot.CanCreate(this);

        public bool TryGetSection(int sectionId, out MapSectionInfo section) =>
            sectionsById.TryGetValue(sectionId, out section);

        public bool TryGetLogicalSection(int logicalSectionId, out MapSectionInfo section) =>
            sectionsByLogicalId.TryGetValue(logicalSectionId, out section);

        public MapSectionInfo GetSection(int sectionId)
        {
            if (!TryGetSection(sectionId, out MapSectionInfo section))
                throw new MapSectionNotFoundException(sectionId, false);
            return section;
        }

        public MapSectionInfo GetLogicalSection(int logicalSectionId)
        {
            if (!TryGetLogicalSection(logicalSectionId, out MapSectionInfo section))
                throw new MapSectionNotFoundException(logicalSectionId, true);
            return section;
        }

        public MapTileLayers ReadPlacementLayers() => MapTileLayers.Create(this);

        public MapPlacementSnapshot ReadPlacementSnapshot() => MapPlacementSnapshot.Create(this);

        public MapKeepAnchors ReadKeepAnchors() => MapKeepAnchors.Create(this);

        public MapRockRecords ReadRockRecords() => MapRockRecords.Create(this);

        public byte[] ReadOpaqueTail()
        {
            if (OpaqueTailLength <= 0)
                return Array.Empty<byte>();
            var result = new byte[OpaqueTailLength];
            Buffer.BlockCopy(fileBytes, OpaqueTailOffset, result, 0, result.Length);
            return result;
        }

        internal byte[] DecodeSection(MapSectionInfo section)
        {
            if (section.StorageKind == MapSectionStorageKind.Raw)
            {
                var result = new byte[section.UncompressedSize];
                Buffer.BlockCopy(fileBytes, section.AbsoluteOffset, result, 0, result.Length);
                return result;
            }

            if (section.StorageKind == MapSectionStorageKind.UnavailableZeroFilledDcl)
            {
                throw new MapUnsupportedFormatException(
                    $"Section {section.SectionId} is a recognized zero-filled DCL placeholder; " +
                    "its declared content is not present in the map file.");
            }

            return PkwareDclDecoder.DecodeSection(
                fileBytes,
                section.AbsoluteOffset,
                section.StoredSize,
                section.SectionId,
                section.UncompressedSize);
        }
    }
}
