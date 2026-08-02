using System;
using MapParser.Core;

namespace AIVPlacement.Core
{
    [Flags]
    public enum AivPlacementIssueKind
    {
        None = 0,
        OutsideMap = 1 << 0,
        InvalidMapTile = 1 << 1,
        HeightMismatch = 1 << 2,
        TerrainBlocked = 1 << 3,
        OrganismOccupied = 1 << 4,
        BuildingOccupied = 1 << 5,
        EntityOccupied = 1 << 6,
        OwnerConflict = 1 << 7,
        InternalOverlap = 1 << 8,
        BuildingRuleFailed = 1 << 9,
        UnresolvedNativeRule = 1 << 10
    }

    public readonly struct AivPlacementTileEvidence
    {
        public AivPlacementTileEvidence(MapPlacementTile tile)
            : this(
                tile.TerrainFlags,
                tile.SecondaryLogic,
                tile.Height,
                tile.DefaultHeight,
                tile.OrganismId,
                tile.BuildingId,
                tile.EntityId,
                tile.OwnerId)
        {
        }

        public AivPlacementTileEvidence(
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

        // Keep every raw snapshot value so later Oracle mismatches remain reproducible.
        public int TerrainFlags { get; }
        public byte SecondaryLogic { get; }
        public byte Height { get; }
        public byte DefaultHeight { get; }
        public ushort OrganismId { get; }
        public ushort BuildingId { get; }
        public ushort EntityId { get; }
        public byte OwnerId { get; }
    }

    public sealed class AivPlacementIssue
    {
        public AivPlacementIssue(
            AivPlacementIssueKind kind,
            int elementIndex,
            int buildIndex,
            int mapperValue,
            AivProjectedTileKind tileKind,
            MapCoordinate mapCoordinate,
            int? tileId,
            AivPlacementTileEvidence? tileEvidence,
            int? conflictingElementIndex = null)
        {
            if (kind == AivPlacementIssueKind.None)
                throw new ArgumentOutOfRangeException(nameof(kind), "An issue must have a reason code.");
            if (elementIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            if (buildIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(buildIndex));
            if (tileId.HasValue && tileId.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(tileId));
            if (conflictingElementIndex.HasValue && conflictingElementIndex.Value < 0)
                throw new ArgumentOutOfRangeException(nameof(conflictingElementIndex));

            Kind = kind;
            ElementIndex = elementIndex;
            BuildIndex = buildIndex;
            MapperValue = mapperValue;
            TileKind = tileKind;
            MapCoordinate = mapCoordinate;
            TileId = tileId;
            TileEvidence = tileEvidence;
            ConflictingElementIndex = conflictingElementIndex;
        }

        public AivPlacementIssueKind Kind { get; }
        public int ElementIndex { get; }
        public int BuildIndex { get; }
        public int MapperValue { get; }
        public AivProjectedTileKind TileKind { get; }
        public MapCoordinate MapCoordinate { get; }
        public int? TileId { get; }
        public AivPlacementTileEvidence? TileEvidence { get; }
        public int? ConflictingElementIndex { get; }
    }
}
