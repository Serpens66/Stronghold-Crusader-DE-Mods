using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private readonly HashSet<ushort> removedStartBuildingIds;
        private readonly Dictionary<int, ushort> reconstructedRockIdsByTileId;
        private readonly IReadOnlyList<ushort> normalizedStartBuildingIds;
        private readonly IReadOnlyList<ushort> retainedStartBuildingIdList;

        public AivPreplacementMapState(
            IAivPlacementTileSource source,
            IEnumerable<ushort> startBuildingIds,
            IEnumerable<ushort> retainedStartBuildingIds,
            IEnumerable<MapRockRecord> rockRecords)
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

            removedStartBuildingIds = new HashSet<ushort>(this.startBuildingIds);
            removedStartBuildingIds.ExceptWith(this.retainedStartBuildingIds);
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
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (retainedStartSlotIndexes == null)
                throw new ArgumentNullException(nameof(retainedStartSlotIndexes));

            var retainedSlots = new HashSet<int>(retainedStartSlotIndexes);
            foreach (int slotIndex in retainedSlots)
            {
                if (slotIndex < 0 || slotIndex >= MapKeepAnchors.SlotCount)
                    throw new ArgumentOutOfRangeException(nameof(retainedStartSlotIndexes));
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
            for (int recordIndex = 1; recordIndex < buildingRecordCount; recordIndex++)
            {
                int offset = recordIndex * BuildingRecordSize;
                ushort aliveState = ReadUInt16(records, offset + AliveStateOffset);
                ushort owner = ReadUInt16(records, offset + OwnerOffset);
                if (aliveState != AliveStateIsAlive || owner < 1 || owner > 8)
                    continue;

                // Section 1012 stores the nonzero object-record index directly in both formats.
                buildingIds.Add((ushort)recordIndex);
                if (retainedSlots.Contains(owner - 1))
                    retainedBuildingIds.Add((ushort)recordIndex);
            }

            return new AivPreplacementMapState(
                new SnapshotTileSource(snapshot),
                buildingIds,
                retainedBuildingIds,
                document.ReadRockRecords().Records);
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
                    evidence.PlannedOccupancies);
            }

            if (retainedStartBuildingIds.Contains(evidence.BuildingId))
                return evidence;

            if (!removedStartBuildingIds.Contains(evidence.BuildingId))
            {
                if (!IsAdjacentRemovedStartWall(tileId, evidence))
                    return evidence;

                return new AivPlacementTileEvidence(
                    evidence.TerrainFlags & ~IsWall,
                    evidence.SecondaryLogic,
                    evidence.Height,
                    evidence.DefaultHeight,
                    evidence.OrganismId,
                    evidence.BuildingId,
                    evidence.EntityId,
                    0,
                    evidence.PlannedOccupancies);
            }

            // Native places starts in player order, so only current and later starts are absent.
            return new AivPlacementTileEvidence(
                evidence.TerrainFlags & ~RemovedStartBuildingFlags,
                evidence.SecondaryLogic,
                evidence.Height,
                evidence.DefaultHeight,
                evidence.OrganismId,
                0,
                evidence.EntityId,
                0,
                evidence.PlannedOccupancies);
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
                        !startBuildingIds.Contains(
                            source.GetTileEvidence(neighborTileId).BuildingId))
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
            if (HasAdjacentBuilding(tileId, retainedStartBuildingIds))
                return false;
            return HasAdjacentBuilding(tileId, removedStartBuildingIds);
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

        private static ushort ReadUInt16(byte[] data, int offset) =>
            (ushort)(data[offset] | (data[offset + 1] << 8));

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
