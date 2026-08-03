using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AIVParser.Core;
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
        UnresolvedNativeRule = 1 << 10,
        PriorAivPrebuiltOccupied = 1 << 11
    }

    public enum AivTileOccupancyKind
    {
        MapPreplacedBuilding,
        PlayerStartKeep,
        PlayerStartStockpile,
        PlayerStartBuilding,
        PlannedAivElement,
        ScheduledAivPrebuild,
        PrebuiltAivBuilding,
        PrebuiltAivTile,
        RuntimeBuildingUnknown
    }

    public readonly struct AivTileOccupancy
    {
        public AivTileOccupancy(
            AivTileOccupancyKind kind,
            string sessionId,
            int playerId,
            ushort buildingId,
            ushort buildingType,
            int mapperValue,
            AivItemCategory category,
            int elementIndex,
            int buildIndex,
            bool blocksPlacement)
        {
            if (playerId < 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));
            if (elementIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            if (buildIndex < -1)
                throw new ArgumentOutOfRangeException(nameof(buildIndex));

            Kind = kind;
            SessionId = sessionId ?? string.Empty;
            PlayerId = playerId;
            BuildingId = buildingId;
            BuildingType = buildingType;
            MapperValue = mapperValue;
            Category = category;
            ElementIndex = elementIndex;
            BuildIndex = buildIndex;
            BlocksPlacement = blocksPlacement;
        }

        public AivTileOccupancyKind Kind { get; }
        public string SessionId { get; }
        public int PlayerId { get; }
        public ushort BuildingId { get; }
        public ushort BuildingType { get; }
        public int MapperValue { get; }
        public AivItemCategory Category { get; }
        public int ElementIndex { get; }
        public int BuildIndex { get; }
        public bool BlocksPlacement { get; }
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
                tile.OwnerId,
                tile.BuildingId == 0
                    ? null
                    : new[]
                    {
                        new AivTileOccupancy(
                            AivTileOccupancyKind.MapPreplacedBuilding,
                            string.Empty,
                            tile.OwnerId,
                            tile.BuildingId,
                            0,
                            -1,
                            AivItemCategory.Unknown,
                            -1,
                            -1,
                            true)
                    })
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
            byte ownerId,
            IReadOnlyList<AivTileOccupancy> occupancies = null)
        {
            TerrainFlags = terrainFlags;
            SecondaryLogic = secondaryLogic;
            Height = height;
            DefaultHeight = defaultHeight;
            OrganismId = organismId;
            BuildingId = buildingId;
            EntityId = entityId;
            OwnerId = ownerId;
            if (occupancies == null && buildingId != 0)
            {
                occupancies = new[]
                {
                    new AivTileOccupancy(
                        AivTileOccupancyKind.MapPreplacedBuilding,
                        string.Empty,
                        ownerId,
                        buildingId,
                        0,
                        -1,
                        AivItemCategory.Unknown,
                        -1,
                        -1,
                        true)
                };
            }
            Occupancies = occupancies == null || occupancies.Count == 0
                ? Array.Empty<AivTileOccupancy>()
                : new ReadOnlyCollection<AivTileOccupancy>(
                    new List<AivTileOccupancy>(occupancies));
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
        // BuildingId remains raw evidence; provenance explains what produced occupancy.
        public IReadOnlyList<AivTileOccupancy> Occupancies { get; }
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
