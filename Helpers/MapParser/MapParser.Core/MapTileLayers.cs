using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MapParser.Core
{
    public sealed class MapTileLayers
    {
        private static readonly int[] RequiredIds =
        {
            MapSectionCatalog.Logic,
            MapSectionCatalog.Logic2,
            MapSectionCatalog.Height,
            MapSectionCatalog.DefaultHeight,
            MapSectionCatalog.Organism,
            MapSectionCatalog.Building,
            MapSectionCatalog.Entity,
            MapSectionCatalog.WallOwner
        };

        private MapTileLayers(
            int[] terrainFlags,
            byte[] secondaryLogic,
            byte[] heights,
            byte[] defaultHeights,
            ushort[] organisms,
            ushort[] buildingOccupancy,
            ushort[] entityOccupancy,
            byte[] ownerOccupancy)
        {
            TileCount = terrainFlags.Length;
            TerrainFlags = Array.AsReadOnly(terrainFlags);
            SecondaryLogic = Array.AsReadOnly(secondaryLogic);
            Heights = Array.AsReadOnly(heights);
            DefaultHeights = Array.AsReadOnly(defaultHeights);
            Organisms = Array.AsReadOnly(organisms);
            BuildingOccupancy = Array.AsReadOnly(buildingOccupancy);
            EntityOccupancy = Array.AsReadOnly(entityOccupancy);
            OwnerOccupancy = Array.AsReadOnly(ownerOccupancy);
        }

        public int TileCount { get; }
        public IReadOnlyList<int> TerrainFlags { get; }
        public IReadOnlyList<byte> SecondaryLogic { get; }
        public IReadOnlyList<byte> Heights { get; }
        public IReadOnlyList<byte> DefaultHeights { get; }
        public IReadOnlyList<ushort> Organisms { get; }
        public IReadOnlyList<ushort> BuildingOccupancy { get; }
        public IReadOnlyList<ushort> EntityOccupancy { get; }
        public IReadOnlyList<byte> OwnerOccupancy { get; }

        internal static bool CanCreate(MapDocument document)
        {
            if (document == null || !document.SectionsAvailable)
                return false;

            foreach (int id in RequiredIds)
            {
                if (!document.TryGetLogicalSection(id, out _))
                    return false;
            }

            int logicBytes = document.GetLogicalSection(MapSectionCatalog.Logic).UncompressedSize;
            if (logicBytes <= 0 || logicBytes % 4 != 0)
                return false;
            int count = logicBytes / 4;
            return HasSize(document, MapSectionCatalog.Logic2, count) &&
                HasSize(document, MapSectionCatalog.Height, count) &&
                HasSize(document, MapSectionCatalog.DefaultHeight, count) &&
                HasSize(document, MapSectionCatalog.Organism, checked(count * 2)) &&
                HasSize(document, MapSectionCatalog.Building, checked(count * 2)) &&
                HasSize(document, MapSectionCatalog.Entity, checked(count * 2)) &&
                HasSize(document, MapSectionCatalog.WallOwner, count);
        }

        internal static MapTileLayers Create(MapDocument document)
        {
            if (!CanCreate(document))
                throw new MapCorruptDataException("The map does not contain a complete, size-consistent placement-layer set.");

            // Decoding happens here, not while parsing the directory, so lobby callers only pay for needed layers.
            int[] terrain = ReadInt32(document.GetLogicalSection(MapSectionCatalog.Logic).GetOrReadContent());
            int count = terrain.Length;
            return new MapTileLayers(
                terrain,
                ReadBytes(document, MapSectionCatalog.Logic2, count),
                ReadBytes(document, MapSectionCatalog.Height, count),
                ReadBytes(document, MapSectionCatalog.DefaultHeight, count),
                ReadUInt16(document, MapSectionCatalog.Organism, count),
                ReadUInt16(document, MapSectionCatalog.Building, count),
                ReadUInt16(document, MapSectionCatalog.Entity, count),
                ReadBytes(document, MapSectionCatalog.WallOwner, count));
        }

        private static bool HasSize(MapDocument document, int id, int size) =>
            document.GetLogicalSection(id).UncompressedSize == size;

        private static byte[] ReadBytes(MapDocument document, int id, int count)
        {
            byte[] data = document.GetLogicalSection(id).GetOrReadContent();
            if (data.Length != count)
                throw new MapCorruptDataException($"Logical section {id} has an inconsistent tile count.");
            return (byte[])data.Clone();
        }

        private static ushort[] ReadUInt16(MapDocument document, int id, int count)
        {
            byte[] data = document.GetLogicalSection(id).GetOrReadContent();
            if (data.Length != checked(count * 2))
                throw new MapCorruptDataException($"Logical section {id} has an inconsistent tile count.");
            var values = new ushort[count];
            for (int index = 0; index < count; index++)
                values[index] = LittleEndian.ReadUInt16(data, index * 2);
            return values;
        }

        private static int[] ReadInt32(byte[] data)
        {
            if (data.Length == 0 || data.Length % 4 != 0)
                throw new MapCorruptDataException("Terrain flags do not contain complete Int32 values.");
            var values = new int[data.Length / 4];
            for (int index = 0; index < values.Length; index++)
                values[index] = LittleEndian.ReadInt32(data, index * 4);
            return values;
        }
    }
}
