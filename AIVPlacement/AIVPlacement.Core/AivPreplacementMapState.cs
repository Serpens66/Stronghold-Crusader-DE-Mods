using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AIVParser.Core;
using MapParser.Core;

namespace AIVPlacement.Core
{
    public sealed class AivPreplacementMapState : IAivPlacementTileSource
    {
        private const int RemovedStartBuildingFlags = 0x10000500;
        private const int IsWall = 0x00000100;
        private const int ImpassableEdge = 0x00000080;
        private const int RockOrganismIdBase = 4000;
        private const int BuildingRecordSize = 0x32C;
        private const int AliveStateOffset = 0xD0;
        private const int OwnerOffset = 0xD6;
        private const ushort AliveStateIsAlive = 2;

        private readonly IAivPlacementTileSource source;
        private readonly HashSet<ushort> startBuildingIds;
        private readonly HashSet<ushort> retainedStartBuildingIds;
        private readonly HashSet<ushort> serializedRetainedStartBuildingIds;
        private readonly HashSet<ushort> rebuiltStartBuildingIds;
        private readonly HashSet<ushort> removedStartBuildingIds;
        private readonly Dictionary<ushort, AivTileOccupancyKind> startKindsByBuildingId;
        private readonly Dictionary<int, RebuiltStartCell> rebuiltStartCellsByTileId;
        private readonly Dictionary<int, ushort> reconstructedRockIdsByTileId;
        private readonly IReadOnlyList<ushort> normalizedStartBuildingIds;
        private readonly IReadOnlyList<ushort> retainedStartBuildingIdList;

        public AivPreplacementMapState(
            IAivPlacementTileSource source,
            IEnumerable<ushort> startBuildingIds,
            IEnumerable<ushort> retainedStartBuildingIds,
            IEnumerable<MapRockRecord> rockRecords,
            IReadOnlyDictionary<ushort, AivTileOccupancyKind> startKindsByBuildingId = null)
            : this(
                source,
                startBuildingIds,
                retainedStartBuildingIds,
                rockRecords,
                startKindsByBuildingId,
                null)
        {
        }

        private AivPreplacementMapState(
            IAivPlacementTileSource source,
            IEnumerable<ushort> startBuildingIds,
            IEnumerable<ushort> retainedStartBuildingIds,
            IEnumerable<MapRockRecord> rockRecords,
            IReadOnlyDictionary<ushort, AivTileOccupancyKind> startKindsByBuildingId,
            IReadOnlyDictionary<ushort, StartRebuildTransform> rebuildTransformsByBuildingId)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            if (source.Geometry == null)
                throw new ArgumentException("The tile source has no geometry.", nameof(source));
            if (startBuildingIds == null)
                throw new ArgumentNullException(nameof(startBuildingIds));
            if (retainedStartBuildingIds == null)
                throw new ArgumentNullException(nameof(retainedStartBuildingIds));
            if (rockRecords == null)
                throw new ArgumentNullException(nameof(rockRecords));

            this.startBuildingIds = new HashSet<ushort>(startBuildingIds);
            this.retainedStartBuildingIds = new HashSet<ushort>(retainedStartBuildingIds);
            if (!this.retainedStartBuildingIds.IsSubsetOf(this.startBuildingIds))
            {
                throw new ArgumentException(
                    "Retained start buildings must belong to the complete start-building set.",
                    nameof(retainedStartBuildingIds));
            }

            serializedRetainedStartBuildingIds = new HashSet<ushort>(
                this.retainedStartBuildingIds);
            rebuiltStartBuildingIds = rebuildTransformsByBuildingId == null
                ? new HashSet<ushort>()
                : new HashSet<ushort>(rebuildTransformsByBuildingId.Keys);
            if (rebuildTransformsByBuildingId != null)
                serializedRetainedStartBuildingIds.ExceptWith(rebuildTransformsByBuildingId.Keys);
            removedStartBuildingIds = new HashSet<ushort>(this.startBuildingIds);
            removedStartBuildingIds.ExceptWith(serializedRetainedStartBuildingIds);
            this.startKindsByBuildingId = new Dictionary<ushort, AivTileOccupancyKind>();
            if (startKindsByBuildingId != null)
            {
                foreach (KeyValuePair<ushort, AivTileOccupancyKind> pair in startKindsByBuildingId)
                    this.startKindsByBuildingId.Add(pair.Key, pair.Value);
            }
            rebuiltStartCellsByTileId = RebuildStartCells(
                rebuildTransformsByBuildingId ??
                new Dictionary<ushort, StartRebuildTransform>());
            reconstructedRockIdsByTileId = ReconstructRockFootprints(rockRecords);
            var ordered = new List<ushort>(removedStartBuildingIds);
            ordered.Sort();
            normalizedStartBuildingIds = new ReadOnlyCollection<ushort>(ordered.ToArray());
            ordered = new List<ushort>(this.retainedStartBuildingIds);
            ordered.Sort();
            retainedStartBuildingIdList = new ReadOnlyCollection<ushort>(ordered.ToArray());
        }

        public MapTileGeometry Geometry => source.Geometry;
        public IReadOnlyList<ushort> NormalizedStartBuildingIds => normalizedStartBuildingIds;
        public IReadOnlyList<ushort> RetainedStartBuildingIds => retainedStartBuildingIdList;

        public static AivPreplacementMapState Create(MapDocument document)
        {
            return Create(document, Array.Empty<int>());
        }

        public static AivPreplacementMapState Create(
            MapDocument document,
            IEnumerable<int> retainedStartSlotIndexes)
        {
            return Create(
                document,
                retainedStartSlotIndexes,
                new Dictionary<int, AivRotation>());
        }

        public static AivPreplacementMapState Create(
            MapDocument document,
            IEnumerable<int> retainedStartSlotIndexes,
            IReadOnlyDictionary<int, AivRotation> rebuiltStartRotationsBySlot)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (retainedStartSlotIndexes == null)
                throw new ArgumentNullException(nameof(retainedStartSlotIndexes));
            if (rebuiltStartRotationsBySlot == null)
                throw new ArgumentNullException(nameof(rebuiltStartRotationsBySlot));

            var retainedSlots = new HashSet<int>(retainedStartSlotIndexes);
            foreach (int slotIndex in retainedSlots)
            {
                if (slotIndex < 0 || slotIndex >= MapKeepAnchors.SlotCount)
                    throw new ArgumentOutOfRangeException(nameof(retainedStartSlotIndexes));
            }
            foreach (int slotIndex in rebuiltStartRotationsBySlot.Keys)
            {
                if (slotIndex < 0 || slotIndex >= MapKeepAnchors.SlotCount)
                    throw new ArgumentOutOfRangeException(nameof(rebuiltStartRotationsBySlot));
                if (!retainedSlots.Contains(slotIndex))
                {
                    throw new ArgumentException(
                        "A rebuilt start must also be retained in the current session state.",
                        nameof(rebuiltStartRotationsBySlot));
                }
            }

            MapPlacementSnapshot snapshot = MapPlacementSnapshot.Create(document);
            MapSectionInfo recordsSection = document.GetLogicalSection(
                MapSectionCatalog.BuildingObjects);
            if (!MapSectionCatalog.TryGetBuildingObjectRecordCount(
                    recordsSection.SectionId,
                    out int buildingRecordCount) ||
                !recordsSection.IsContentAvailable ||
                recordsSection.UncompressedSize != BuildingRecordSize * buildingRecordCount)
            {
                throw new InvalidOperationException(
                    $"Section {recordsSection.SectionId} cannot provide the serialized player start buildings.");
            }

            byte[] records = recordsSection.ReadContent();
            var buildingIds = new List<ushort>();
            var retainedBuildingIds = new List<ushort>();
            var startKinds = new Dictionary<ushort, AivTileOccupancyKind>();
            var rebuildTransforms = new Dictionary<ushort, StartRebuildTransform>();
            MapKeepAnchors anchors = MapKeepAnchors.Create(document);
            for (int recordIndex = 1; recordIndex < buildingRecordCount; recordIndex++)
            {
                int offset = recordIndex * BuildingRecordSize;
                ushort aliveState = ReadUInt16(records, offset + AliveStateOffset);
                ushort buildingType = ReadUInt16(records, offset + 0xD2);
                ushort owner = ReadUInt16(records, offset + OwnerOffset);
                if (aliveState != AliveStateIsAlive || owner < 1 || owner > 8)
                    continue;

                // Section 1012 stores the nonzero object-record index directly in both formats.
                buildingIds.Add((ushort)recordIndex);
                startKinds[(ushort)recordIndex] = ClassifyStartBuilding(buildingType);
                if (retainedSlots.Contains(owner - 1))
                    retainedBuildingIds.Add((ushort)recordIndex);
                if (rebuiltStartRotationsBySlot.TryGetValue(
                        owner - 1,
                        out AivRotation rotation))
                {
                    MapKeepAnchorResult anchor = anchors.GetSlot(owner - 1);
                    if (anchor.Status != MapKeepAnchorStatus.Exact || !anchor.Coordinate.HasValue)
                    {
                        throw new InvalidOperationException(
                            $"Start slot {owner - 1} cannot be rebuilt without an exact Keep anchor.");
                    }
                    rebuildTransforms[(ushort)recordIndex] = new StartRebuildTransform(
                        anchor.Coordinate.Value,
                        rotation);
                }
            }

            return new AivPreplacementMapState(
                new SnapshotTileSource(snapshot),
                buildingIds,
                retainedBuildingIds,
                document.ReadRockRecords().Records,
                startKinds,
                rebuildTransforms);
        }

        public static MapCoordinate TransformRebuiltStartCoordinate(
            MapCoordinate coordinate,
            MapCoordinate keep,
            AivRotation rotation)
        {
            int x = coordinate.X - keep.X;
            int y = coordinate.Y - keep.Y;
            // Native rebuilds the 13x13 start reference after choosing the AIV rotation.
            return rotation switch
            {
                AivRotation.Degrees0 => new MapCoordinate(keep.X + x + 1, keep.Y + y + 1),
                AivRotation.Degrees90 => new MapCoordinate(keep.X + y + 1, keep.Y + 12 - x),
                AivRotation.Degrees180 => new MapCoordinate(keep.X + 12 - x, keep.Y + 12 - y),
                AivRotation.Degrees270 => new MapCoordinate(keep.X + 12 - y, keep.Y + x + 1),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation))
            };
        }

        public AivPlacementTileEvidence GetTileEvidence(int tileId)
        {
            AivPlacementTileEvidence evidence = source.GetTileEvidence(tileId);
            if (reconstructedRockIdsByTileId.TryGetValue(tileId, out ushort rockId))
            {
                // The native map loader replays square rock footprints. This repairs
                // serialized cells that still contain an overwritten tree or stale flag.
                evidence = new AivPlacementTileEvidence(
                    evidence.TerrainFlags | ImpassableEdge,
                    evidence.SecondaryLogic,
                    evidence.Height,
                    evidence.DefaultHeight,
                    rockId,
                    evidence.BuildingId,
                    evidence.EntityId,
                    evidence.OwnerId,
                    evidence.Occupancies);
            }

            if (serializedRetainedStartBuildingIds.Contains(evidence.BuildingId))
            {
                var occupancies = new List<AivTileOccupancy>();
                foreach (AivTileOccupancy occupancy in evidence.Occupancies)
                {
                    if (occupancy.Kind != AivTileOccupancyKind.MapPreplacedBuilding)
                        occupancies.Add(occupancy);
                }
                AivTileOccupancyKind kind = startKindsByBuildingId.TryGetValue(
                    evidence.BuildingId,
                    out AivTileOccupancyKind knownKind)
                    ? knownKind
                    : AivTileOccupancyKind.PlayerStartBuilding;
                occupancies.Add(new AivTileOccupancy(
                    kind,
                    string.Empty,
                    evidence.OwnerId,
                    evidence.BuildingId,
                    0,
                    -1,
                    AivItemCategory.Unknown,
                    -1,
                    -1,
                    true));
                evidence = new AivPlacementTileEvidence(
                    evidence.TerrainFlags,
                    evidence.SecondaryLogic,
                    evidence.Height,
                    evidence.DefaultHeight,
                    evidence.OrganismId,
                    evidence.BuildingId,
                    evidence.EntityId,
                    evidence.OwnerId,
                    occupancies);
            }
            else if (!removedStartBuildingIds.Contains(evidence.BuildingId))
            {
                if (IsAdjacentRemovedStartWall(tileId, evidence))
                {
                    evidence = new AivPlacementTileEvidence(
                        evidence.TerrainFlags & ~IsWall,
                        evidence.SecondaryLogic,
                        evidence.Height,
                        evidence.DefaultHeight,
                        evidence.OrganismId,
                        evidence.BuildingId,
                        evidence.EntityId,
                        0,
                        evidence.Occupancies);
                }
            }
            else
            {
                // Native places starts in player order, so current and later starts are absent.
                evidence = new AivPlacementTileEvidence(
                    evidence.TerrainFlags & ~RemovedStartBuildingFlags,
                    evidence.SecondaryLogic,
                    evidence.Height,
                    evidence.DefaultHeight,
                    evidence.OrganismId,
                    0,
                    evidence.EntityId,
                    0,
                    evidence.Occupancies.Where(item =>
                        item.Kind != AivTileOccupancyKind.MapPreplacedBuilding).ToArray());
            }

            return rebuiltStartCellsByTileId.TryGetValue(
                    tileId,
                    out RebuiltStartCell rebuilt)
                ? rebuilt.Apply(evidence)
                : evidence;
        }

        public AivPlacementTileEvidence GetOriginalTileEvidence(int tileId)
        {
            // Oracle diagnostics need to distinguish a native rule mismatch from
            // state deliberately removed by the pre-placement normalization.
            return source.GetTileEvidence(tileId);
        }

        public AivStartBuildingAdjacency GetStartBuildingAdjacency(int tileId)
        {
            if (!Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate))
                throw new ArgumentOutOfRangeException(nameof(tileId));

            int orthogonal = 0;
            int diagonal = 0;
            for (int y = coordinate.Y - 1; y <= coordinate.Y + 1; y++)
            {
                for (int x = coordinate.X - 1; x <= coordinate.X + 1; x++)
                {
                    if ((x == coordinate.X && y == coordinate.Y) ||
                        !Geometry.TryGetTileId(x, y, out int neighborTileId) ||
                        !HasStartBuildingForAdjacency(neighborTileId))
                    {
                        continue;
                    }

                    if (x == coordinate.X || y == coordinate.Y)
                        orthogonal++;
                    else
                        diagonal++;
                }
            }

            return new AivStartBuildingAdjacency(orthogonal, diagonal);
        }

        private bool IsAdjacentRemovedStartWall(int tileId, AivPlacementTileEvidence evidence)
        {
            if ((evidence.TerrainFlags & IsWall) == 0 ||
                !Geometry.TryGetCoordinate(tileId, out _))
            {
                return false;
            }

            // A retained start owns the shared wall state; only walls belonging solely
            // to starts that have not yet been created are normalized away.
            if (HasAdjacentEffectiveStartBuilding(tileId))
                return false;
            return HasAdjacentBuilding(tileId, removedStartBuildingIds);
        }

        private bool HasAdjacentEffectiveStartBuilding(int tileId)
        {
            if (!Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate))
                return false;

            for (int y = coordinate.Y - 1; y <= coordinate.Y + 1; y++)
            {
                for (int x = coordinate.X - 1; x <= coordinate.X + 1; x++)
                {
                    if ((x != coordinate.X || y != coordinate.Y) &&
                        Geometry.TryGetTileId(x, y, out int neighborTileId) &&
                        HasEffectiveStartBuilding(neighborTileId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasEffectiveStartBuilding(int tileId)
        {
            if (rebuiltStartCellsByTileId.ContainsKey(tileId))
                return true;
            return serializedRetainedStartBuildingIds.Contains(
                source.GetTileEvidence(tileId).BuildingId);
        }

        private bool HasStartBuildingForAdjacency(int tileId)
        {
            if (rebuiltStartCellsByTileId.ContainsKey(tileId))
                return true;

            ushort buildingId = source.GetTileEvidence(tileId).BuildingId;
            // Pending starts still matter to this native-rule diagnostic; rebuilt origins do not.
            return startBuildingIds.Contains(buildingId) &&
                !rebuiltStartBuildingIds.Contains(buildingId);
        }

        private bool HasAdjacentBuilding(int tileId, HashSet<ushort> buildingIds)
        {
            if (!Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate))
                return false;

            for (int y = coordinate.Y - 1; y <= coordinate.Y + 1; y++)
            {
                for (int x = coordinate.X - 1; x <= coordinate.X + 1; x++)
                {
                    if ((x != coordinate.X || y != coordinate.Y) &&
                        Geometry.TryGetTileId(x, y, out int neighborTileId) &&
                        buildingIds.Contains(source.GetTileEvidence(neighborTileId).BuildingId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private Dictionary<int, ushort> ReconstructRockFootprints(
            IEnumerable<MapRockRecord> rockRecords)
        {
            var result = new Dictionary<int, ushort>();
            foreach (MapRockRecord record in rockRecords)
            {
                if (record == null || !record.IsActive)
                    continue;

                ushort rockId = checked((ushort)(RockOrganismIdBase + record.RecordIndex));
                for (int y = record.Y; y < record.Y + record.Size; y++)
                {
                    for (int x = record.X; x < record.X + record.Size; x++)
                    {
                        if (Geometry.TryGetTileId(x, y, out int tileId))
                            result[tileId] = rockId;
                    }
                }
            }

            return result;
        }

        private Dictionary<int, RebuiltStartCell> RebuildStartCells(
            IReadOnlyDictionary<ushort, StartRebuildTransform> transforms)
        {
            var result = new Dictionary<int, RebuiltStartCell>();
            if (transforms.Count == 0)
                return result;

            for (int tileId = 0; tileId < Geometry.TileCount; tileId++)
            {
                AivPlacementTileEvidence evidence = source.GetTileEvidence(tileId);
                bool isBuildingCell = transforms.TryGetValue(
                    evidence.BuildingId,
                    out StartRebuildTransform transform);
                if (!isBuildingCell &&
                    ((evidence.TerrainFlags & IsWall) == 0 ||
                     !TryGetAdjacentRebuildTransform(tileId, transforms, out transform)))
                {
                    continue;
                }

                Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate);
                MapCoordinate target = TransformRebuiltStartCoordinate(
                    coordinate,
                    transform.Keep,
                    transform.Rotation);
                if (!Geometry.TryGetTileId(target.X, target.Y, out int targetTileId))
                    continue;
                if (result.ContainsKey(targetTileId))
                {
                    throw new InvalidOperationException(
                        $"Rebuilt player starts overlap at map tile {target}.");
                }

                AivTileOccupancyKind kind = isBuildingCell &&
                    startKindsByBuildingId.TryGetValue(
                        evidence.BuildingId,
                        out AivTileOccupancyKind knownKind)
                        ? knownKind
                        : AivTileOccupancyKind.PlayerStartBuilding;
                result.Add(targetTileId, new RebuiltStartCell(
                    evidence.TerrainFlags & RemovedStartBuildingFlags,
                    isBuildingCell ? evidence.BuildingId : (ushort)0,
                    evidence.OwnerId,
                    kind));
            }

            return result;
        }

        private bool TryGetAdjacentRebuildTransform(
            int tileId,
            IReadOnlyDictionary<ushort, StartRebuildTransform> transforms,
            out StartRebuildTransform transform)
        {
            Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate);
            for (int y = coordinate.Y - 1; y <= coordinate.Y + 1; y++)
            {
                for (int x = coordinate.X - 1; x <= coordinate.X + 1; x++)
                {
                    if ((x != coordinate.X || y != coordinate.Y) &&
                        Geometry.TryGetTileId(x, y, out int neighborTileId) &&
                        transforms.TryGetValue(
                            source.GetTileEvidence(neighborTileId).BuildingId,
                            out transform))
                    {
                        return true;
                    }
                }
            }

            transform = default;
            return false;
        }

        private static ushort ReadUInt16(byte[] data, int offset) =>
            (ushort)(data[offset] | (data[offset + 1] << 8));

        private static AivTileOccupancyKind ClassifyStartBuilding(ushort buildingType)
        {
            if (buildingType >= 40 && buildingType <= 44)
                return AivTileOccupancyKind.PlayerStartKeep;
            if (buildingType == 10)
                return AivTileOccupancyKind.PlayerStartStockpile;
            return AivTileOccupancyKind.PlayerStartBuilding;
        }

        private readonly struct StartRebuildTransform
        {
            public StartRebuildTransform(MapCoordinate keep, AivRotation rotation)
            {
                Keep = keep;
                Rotation = rotation;
            }

            public MapCoordinate Keep { get; }
            public AivRotation Rotation { get; }
        }

        private readonly struct RebuiltStartCell
        {
            public RebuiltStartCell(
                int terrainFlags,
                ushort buildingId,
                byte ownerId,
                AivTileOccupancyKind kind)
            {
                TerrainFlags = terrainFlags;
                BuildingId = buildingId;
                OwnerId = ownerId;
                Kind = kind;
            }

            public int TerrainFlags { get; }
            public ushort BuildingId { get; }
            public byte OwnerId { get; }
            public AivTileOccupancyKind Kind { get; }

            public AivPlacementTileEvidence Apply(AivPlacementTileEvidence evidence)
            {
                var occupancies = evidence.Occupancies.ToList();
                if (BuildingId != 0)
                {
                    occupancies.RemoveAll(item =>
                        item.Kind == AivTileOccupancyKind.MapPreplacedBuilding);
                    occupancies.Add(new AivTileOccupancy(
                        Kind,
                        string.Empty,
                        OwnerId,
                        BuildingId,
                        0,
                        -1,
                        AivItemCategory.Unknown,
                        -1,
                        -1,
                        true));
                }
                return new AivPlacementTileEvidence(
                    evidence.TerrainFlags | TerrainFlags,
                    evidence.SecondaryLogic,
                    evidence.Height,
                    evidence.DefaultHeight,
                    evidence.OrganismId,
                    BuildingId == 0 ? evidence.BuildingId : BuildingId,
                    evidence.EntityId,
                    OwnerId,
                    occupancies);
            }
        }

        private sealed class SnapshotTileSource : IAivPlacementTileSource
        {
            private readonly MapPlacementSnapshot snapshot;

            public SnapshotTileSource(MapPlacementSnapshot snapshot)
            {
                this.snapshot = snapshot;
            }

            public MapTileGeometry Geometry => snapshot.Geometry;

            public AivPlacementTileEvidence GetTileEvidence(int tileId) =>
                new AivPlacementTileEvidence(snapshot.GetTile(tileId));
        }
    }


    public readonly struct AivStartBuildingAdjacency
    {
        public AivStartBuildingAdjacency(
            int orthogonalNeighborCount,
            int diagonalNeighborCount)
        {
            OrthogonalNeighborCount = orthogonalNeighborCount;
            DiagonalNeighborCount = diagonalNeighborCount;
        }

        public int OrthogonalNeighborCount { get; }
        public int DiagonalNeighborCount { get; }
    }
}
