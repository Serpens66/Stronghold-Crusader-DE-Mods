using System;

namespace MapParser.Core
{
    public enum MapPlacementSnapshotFailureKind
    {
        SectionsUnavailable,
        MissingLayer,
        UnavailableLayer,
        InconsistentLayerLength,
        UnsupportedGeometry
    }

    public readonly struct MapPlacementTile
    {
        internal MapPlacementTile(
            int terrainFlags,
            byte secondaryLogic,
            byte height,
            byte defaultHeight,
            ushort organismId,
            ushort buildingId,
            ushort entityId,
            byte ownerId)
        {
            TerrainFlags = terrainFlags;
            SecondaryLogic = secondaryLogic;
            Height = height;
            DefaultHeight = defaultHeight;
            OrganismId = organismId;
            BuildingId = buildingId;
            EntityId = entityId;
            OwnerId = ownerId;
        }

        public int TerrainFlags { get; }
        public byte SecondaryLogic { get; }
        public byte Height { get; }
        public byte DefaultHeight { get; }
        public ushort OrganismId { get; }
        public ushort BuildingId { get; }
        public ushort EntityId { get; }
        public byte OwnerId { get; }
    }

    public sealed class MapPlacementSnapshot
    {
        private static readonly LayerDefinition[] RequiredLayers =
        {
            new LayerDefinition(MapSectionCatalog.Logic, 4),
            new LayerDefinition(MapSectionCatalog.Logic2, 1),
            new LayerDefinition(MapSectionCatalog.Height, 1),
            new LayerDefinition(MapSectionCatalog.DefaultHeight, 1),
            new LayerDefinition(MapSectionCatalog.Organism, 2),
            new LayerDefinition(MapSectionCatalog.Building, 2),
            new LayerDefinition(MapSectionCatalog.Entity, 2),
            new LayerDefinition(MapSectionCatalog.WallOwner, 1)
        };

        private readonly MapTileLayers layers;

        private MapPlacementSnapshot(MapTileGeometry geometry, MapTileLayers layers)
        {
            Geometry = geometry;
            this.layers = layers;
        }

        public MapTileGeometry Geometry { get; }
        public int TileCount => Geometry.TileCount;

        public static MapPlacementSnapshot Create(MapDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            int tileCount = ValidateLayers(document);
            MapTileGeometry geometry;
            try
            {
                geometry = new MapTileGeometry(tileCount, document.Metadata.WorldSize);
            }
            catch (MapUnsupportedGeometryException ex)
            {
                throw new MapPlacementSnapshotException(
                    MapPlacementSnapshotFailureKind.UnsupportedGeometry,
                    "The map placement snapshot is not evaluable because its tile geometry is unsupported.",
                    ex);
            }

            // Layer decoding remains deferred until a caller explicitly asks for a snapshot.
            return new MapPlacementSnapshot(geometry, document.ReadPlacementLayers());
        }

        public MapPlacementTile GetTile(int tileId)
        {
            if (tileId < 0 || tileId >= TileCount)
                throw new ArgumentOutOfRangeException(nameof(tileId));

            return new MapPlacementTile(
                layers.TerrainFlags[tileId],
                layers.SecondaryLogic[tileId],
                layers.Heights[tileId],
                layers.DefaultHeights[tileId],
                layers.Organisms[tileId],
                layers.BuildingOccupancy[tileId],
                layers.EntityOccupancy[tileId],
                layers.OwnerOccupancy[tileId]);
        }

        public MapPlacementTile GetTile(int x, int y) => GetTile(Geometry.GetTileId(x, y));

        public bool TryGetTile(int x, int y, out MapPlacementTile tile)
        {
            if (!Geometry.TryGetTileId(x, y, out int tileId))
            {
                tile = default;
                return false;
            }

            tile = GetTile(tileId);
            return true;
        }

        internal static bool CanCreate(MapDocument document)
        {
            try
            {
                int tileCount = ValidateLayers(document);
                return tileCount == MapTileGeometry.FixedTileCount &&
                    MapTileGeometry.IsSupportedWorldSize(document.Metadata.WorldSize);
            }
            catch (MapPlacementSnapshotException)
            {
                return false;
            }
        }

        private static int ValidateLayers(MapDocument document)
        {
            if (document == null || !document.SectionsAvailable)
            {
                throw new MapPlacementSnapshotException(
                    MapPlacementSnapshotFailureKind.SectionsUnavailable,
                    "The map placement snapshot is not evaluable because map sections are unavailable.");
            }

            foreach (LayerDefinition definition in RequiredLayers)
            {
                if (!document.TryGetLogicalSection(definition.LogicalSectionId, out MapSectionInfo section))
                {
                    throw new MapPlacementSnapshotException(
                        MapPlacementSnapshotFailureKind.MissingLayer,
                        $"The map placement snapshot is not evaluable because logical section " +
                        $"{definition.LogicalSectionId} ({MapSectionCatalog.GetName(definition.LogicalSectionId)}) is missing.",
                        definition.LogicalSectionId);
                }
                if (!section.IsContentAvailable)
                {
                    throw new MapPlacementSnapshotException(
                        MapPlacementSnapshotFailureKind.UnavailableLayer,
                        $"The map placement snapshot is not evaluable because logical section " +
                        $"{definition.LogicalSectionId} ({MapSectionCatalog.GetName(definition.LogicalSectionId)}) is unavailable.",
                        definition.LogicalSectionId);
                }
            }

            MapSectionInfo terrain = document.GetLogicalSection(MapSectionCatalog.Logic);
            if (terrain.UncompressedSize <= 0 || terrain.UncompressedSize % 4 != 0)
            {
                throw LayerLengthException(
                    MapSectionCatalog.Logic,
                    terrain.UncompressedSize,
                    "a positive multiple of 4 bytes");
            }

            int tileCount = terrain.UncompressedSize / 4;
            foreach (LayerDefinition definition in RequiredLayers)
            {
                MapSectionInfo section = document.GetLogicalSection(definition.LogicalSectionId);
                int expectedLength = checked(tileCount * definition.BytesPerTile);
                if (section.UncompressedSize != expectedLength)
                {
                    throw LayerLengthException(
                        definition.LogicalSectionId,
                        section.UncompressedSize,
                        $"{expectedLength} bytes for {tileCount} tiles");
                }
            }

            return tileCount;
        }

        private static MapPlacementSnapshotException LayerLengthException(
            int logicalSectionId,
            int actualLength,
            string expected)
        {
            return new MapPlacementSnapshotException(
                MapPlacementSnapshotFailureKind.InconsistentLayerLength,
                $"The map placement snapshot is not evaluable because logical section " +
                $"{logicalSectionId} ({MapSectionCatalog.GetName(logicalSectionId)}) has {actualLength} bytes; " +
                $"expected {expected}.",
                logicalSectionId);
        }

        private readonly struct LayerDefinition
        {
            public LayerDefinition(int logicalSectionId, int bytesPerTile)
            {
                LogicalSectionId = logicalSectionId;
                BytesPerTile = bytesPerTile;
            }

            public int LogicalSectionId { get; }
            public int BytesPerTile { get; }
        }
    }
}
