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
        private const int BuildingRecordSize = 0x32C;
        private const int AliveStateOffset = 0xD0;
        private const int OwnerOffset = 0xD6;
        private const ushort AliveStateIsAlive = 2;

        private readonly IAivPlacementTileSource source;
        private readonly HashSet<ushort> startBuildingIds;
        private readonly IReadOnlyList<ushort> normalizedStartBuildingIds;

        public AivPreplacementMapState(
            IAivPlacementTileSource source,
            IEnumerable<ushort> startBuildingIds)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            if (source.Geometry == null)
                throw new ArgumentException("The tile source has no geometry.", nameof(source));
            if (startBuildingIds == null)
                throw new ArgumentNullException(nameof(startBuildingIds));

            this.startBuildingIds = new HashSet<ushort>(startBuildingIds);
            var ordered = new List<ushort>(this.startBuildingIds);
            ordered.Sort();
            normalizedStartBuildingIds = new ReadOnlyCollection<ushort>(ordered.ToArray());
        }

        public MapTileGeometry Geometry => source.Geometry;
        public IReadOnlyList<ushort> NormalizedStartBuildingIds => normalizedStartBuildingIds;

        public static AivPreplacementMapState Create(MapDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

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
            for (int recordIndex = 1; recordIndex < buildingRecordCount; recordIndex++)
            {
                int offset = recordIndex * BuildingRecordSize;
                ushort aliveState = ReadUInt16(records, offset + AliveStateOffset);
                ushort owner = ReadUInt16(records, offset + OwnerOffset);
                if (aliveState != AliveStateIsAlive || owner < 1 || owner > 8)
                    continue;

                // Section 1012 stores the nonzero object-record index directly in both formats.
                buildingIds.Add((ushort)recordIndex);
            }

            return new AivPreplacementMapState(
                new SnapshotTileSource(snapshot),
                buildingIds);
        }

        public AivPlacementTileEvidence GetTileEvidence(int tileId)
        {
            AivPlacementTileEvidence evidence = source.GetTileEvidence(tileId);
            if (!startBuildingIds.Contains(evidence.BuildingId))
            {
                if (!IsAdjacentStartWall(tileId, evidence))
                    return evidence;

                return new AivPlacementTileEvidence(
                    evidence.TerrainFlags & ~IsWall,
                    evidence.SecondaryLogic,
                    evidence.Height,
                    evidence.DefaultHeight,
                    evidence.OrganismId,
                    evidence.BuildingId,
                    evidence.EntityId,
                    0);
            }

            // Native AIV selection sees serialized player starts after their occupancy is cleared.
            return new AivPlacementTileEvidence(
                evidence.TerrainFlags & ~RemovedStartBuildingFlags,
                evidence.SecondaryLogic,
                evidence.Height,
                evidence.DefaultHeight,
                evidence.OrganismId,
                0,
                evidence.EntityId,
                0);
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

        private bool IsAdjacentStartWall(int tileId, AivPlacementTileEvidence evidence)
        {
            if ((evidence.TerrainFlags & IsWall) == 0 ||
                !Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate))
            {
                return false;
            }

            AivStartBuildingAdjacency adjacency = GetStartBuildingAdjacency(tileId);
            return adjacency.OrthogonalNeighborCount != 0 ||
                adjacency.DiagonalNeighborCount != 0;
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
