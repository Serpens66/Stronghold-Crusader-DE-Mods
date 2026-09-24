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
        private readonly HashSet<int> uncertainNativeStartTileIds;
        private readonly Dictionary<int, ushort> reconstructedRockIdsByTileId;
        private readonly IReadOnlyList<ushort> normalizedStartBuildingIds;
        private readonly IReadOnlyList<ushort> retainedStartBuildingIdList;

        public bool HasCrossOwnerStartWallAdjacency { get; }
        public bool HasPotentialConnectedRecordCleanup =>
            PotentialConnectedRecordCleanupEvidence != null;
        public string PotentialConnectedRecordCleanupEvidence { get; }

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
            uncertainNativeStartTileIds = new HashSet<int>();
            rebuiltStartCellsByTileId = RebuildStartCells(
                rebuildTransformsByBuildingId ??
                new Dictionary<ushort, StartRebuildTransform>());
            HasCrossOwnerStartWallAdjacency = DetectCrossOwnerStartWallAdjacency();
            PotentialConnectedRecordCleanupEvidence =
                DetectPotentialConnectedRecordCleanup(
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
            if (rebuiltStartRotationsBySlot == null)
                throw new ArgumentNullException(nameof(rebuiltStartRotationsBySlot));
            var starts = new Dictionary<int, AivStartRebuildState>();
            foreach (KeyValuePair<int, AivRotation> pair in rebuiltStartRotationsBySlot)
                starts.Add(pair.Key, new AivStartRebuildState(
                    pair.Value, AivStartRebuildState.CanonicalMarker));
            return Create(document, retainedStartSlotIndexes, starts);
        }

        public static AivPreplacementMapState Create(
            MapDocument document,
            IEnumerable<int> retainedStartSlotIndexes,
            IReadOnlyDictionary<int, AivStartRebuildState> rebuiltStartsBySlot)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (retainedStartSlotIndexes == null)
                throw new ArgumentNullException(nameof(retainedStartSlotIndexes));
            if (rebuiltStartsBySlot == null)
                throw new ArgumentNullException(nameof(rebuiltStartsBySlot));

            var retainedSlots = new HashSet<int>(retainedStartSlotIndexes);
            foreach (int slotIndex in retainedSlots)
            {
                if (slotIndex < 0 || slotIndex >= MapKeepAnchors.SlotCount)
                    throw new ArgumentOutOfRangeException(nameof(retainedStartSlotIndexes));
            }
            foreach (int slotIndex in rebuiltStartsBySlot.Keys)
            {
                if (slotIndex < 0 || slotIndex >= MapKeepAnchors.SlotCount)
                    throw new ArgumentOutOfRangeException(nameof(rebuiltStartsBySlot));
                if (!retainedSlots.Contains(slotIndex))
                {
                    throw new ArgumentException(
                        "A rebuilt start must also be retained in the current session state.",
                        nameof(rebuiltStartsBySlot));
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
                if (rebuiltStartsBySlot.TryGetValue(
                        owner - 1,
                        out AivStartRebuildState start))
                {
                    MapKeepAnchorResult anchor = anchors.GetSlot(owner - 1);
                    if (anchor.Status != MapKeepAnchorStatus.Exact || !anchor.Coordinate.HasValue)
                    {
                        throw new InvalidOperationException(
                            $"Start slot {owner - 1} cannot be rebuilt without an exact Keep anchor.");
                    }
                    rebuildTransforms[(ushort)recordIndex] = new StartRebuildTransform(
                        anchor.Coordinate.Value,
                        start,
                        checked((byte)owner));
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
            // Live grids for the current native build retain 0-degree cells unchanged
            // and put the 180- and 270-degree groups at the 13-cell pivot. The
            // 90-degree offset agrees with its observed 7x7 Keep and campground footprints.
            // Remaining constructor uncertainty is guarded at candidate reads.
            return rotation switch
            {
                AivRotation.Degrees0 => coordinate,
                AivRotation.Degrees90 => new MapCoordinate(keep.X + y + 1, keep.Y + 12 - x),
                AivRotation.Degrees180 => new MapCoordinate(keep.X + 13 - x, keep.Y + 13 - y),
                AivRotation.Degrees270 => new MapCoordinate(keep.X + 13 - y, keep.Y + x),
                _ => throw new ArgumentOutOfRangeException(nameof(rotation))
            };
        }

        public static MapCoordinate TransformRebuiltStartCoordinate(
            MapCoordinate coordinate,
            MapCoordinate keep,
            AivStartRebuildState start)
        {
            MapCoordinate canonical = TransformRebuiltStartCoordinate(
                coordinate, keep, start.Rotation);
            return new MapCoordinate(
                canonical.X + start.MarkerDeltaX,
                canonical.Y + start.MarkerDeltaY);
        }

        public bool HasUnprovenNativeStartInteraction(AivProjectedCastle castle)
        {
            if (castle == null)
                throw new ArgumentNullException(nameof(castle));
            if (uncertainNativeStartTileIds.Count == 0)
                return false;

            foreach (AivProjectedElement element in castle.Elements)
            {
                foreach (AivProjectedTile tile in element.OccupiedTiles)
                {
                    MapCoordinate coordinate = tile.MapCoordinate;
                    if (Geometry.TryGetTileId(coordinate.X, coordinate.Y, out int tileId) &&
                        uncertainNativeStartTileIds.Contains(tileId))
                        return true;
                }
            }
            return false;
        }

        private void MarkUncertainNativeStartArea(MapCoordinate coordinate)
        {
            // 0x6D580 can clear a footprint and refresh neighboring path cells.
            // Until all 0x77E60 success and abort branches are reconstructed,
            // a candidate reading this area cannot receive a proven fit.
            for (int y = coordinate.Y - 4; y <= coordinate.Y + 4; y++)
            {
                for (int x = coordinate.X - 4; x <= coordinate.X + 4; x++)
                {
                    if (Geometry.TryGetTileId(x, y, out int tileId))
                        uncertainNativeStartTileIds.Add(tileId);
                }
            }
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
            if (HasAdjacentEffectiveStartBuilding(tileId, evidence.OwnerId))
                return false;
            return HasAdjacentBuilding(tileId, evidence.OwnerId, removedStartBuildingIds);
        }

        private bool DetectCrossOwnerStartWallAdjacency()
        {
            for (int tileId = 0; tileId < Geometry.TileCount; tileId++)
            {
                AivPlacementTileEvidence wall = source.GetTileEvidence(tileId);
                if (wall.OwnerId == 0 || (wall.TerrainFlags & IsWall) == 0 ||
                    !Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate))
                    continue;

                for (int y = coordinate.Y - 1; y <= coordinate.Y + 1; y++)
                {
                    for (int x = coordinate.X - 1; x <= coordinate.X + 1; x++)
                    {
                        if ((x == coordinate.X && y == coordinate.Y) ||
                            !Geometry.TryGetTileId(x, y, out int neighborTileId))
                            continue;
                        AivPlacementTileEvidence neighbor = source.GetTileEvidence(neighborTileId);
                        if (startBuildingIds.Contains(neighbor.BuildingId) &&
                            neighbor.OwnerId != 0 && neighbor.OwnerId != wall.OwnerId)
                            return true;
                    }
                }
            }
            return false;
        }

        private bool HasAdjacentEffectiveStartBuilding(int tileId, byte wallOwnerId)
        {
            if (!Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate))
                return false;

            for (int y = coordinate.Y - 1; y <= coordinate.Y + 1; y++)
            {
                for (int x = coordinate.X - 1; x <= coordinate.X + 1; x++)
                {
                    if ((x != coordinate.X || y != coordinate.Y) &&
                        Geometry.TryGetTileId(x, y, out int neighborTileId) &&
                        HasEffectiveStartBuilding(neighborTileId, wallOwnerId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasEffectiveStartBuilding(int tileId, byte wallOwnerId)
        {
            if (rebuiltStartCellsByTileId.TryGetValue(tileId, out RebuiltStartCell rebuilt))
                return rebuilt.BuildingId != 0 &&
                    (wallOwnerId == 0 || rebuilt.OwnerId == wallOwnerId);
            AivPlacementTileEvidence evidence = source.GetTileEvidence(tileId);
            return serializedRetainedStartBuildingIds.Contains(
                evidence.BuildingId) &&
                (wallOwnerId == 0 || evidence.OwnerId == wallOwnerId);
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

        private bool HasAdjacentBuilding(
            int tileId,
            byte wallOwnerId,
            HashSet<ushort> buildingIds)
        {
            if (!Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate))
                return false;

            for (int y = coordinate.Y - 1; y <= coordinate.Y + 1; y++)
            {
                for (int x = coordinate.X - 1; x <= coordinate.X + 1; x++)
                {
                    if ((x != coordinate.X || y != coordinate.Y) &&
                        Geometry.TryGetTileId(x, y, out int neighborTileId) &&
                        buildingIds.Contains(source.GetTileEvidence(neighborTileId).BuildingId) &&
                        (wallOwnerId == 0 ||
                         source.GetTileEvidence(neighborTileId).OwnerId == wallOwnerId))
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

            var markedKeeps = new HashSet<MapCoordinate>();
            foreach (StartRebuildTransform start in transforms.Values)
            {
                if (!markedKeeps.Add(start.Keep))
                    continue;
                // The AIV Keep marker at the native reference can spawn a
                // seven-cell structure beyond the serialized start group.
                MarkUncertainNativeStartKeepArea(start.Keep);
                MarkUncertainNativeStartKeepArea(new MapCoordinate(
                    start.Keep.X + start.Start.MarkerDeltaX,
                    start.Keep.Y + start.Start.MarkerDeltaY));
            }

            for (int tileId = 0; tileId < Geometry.TileCount; tileId++)
            {
                AivPlacementTileEvidence evidence = source.GetTileEvidence(tileId);
                bool isBuildingCell = transforms.TryGetValue(
                    evidence.BuildingId,
                    out StartRebuildTransform transform);
                if (!isBuildingCell &&
                    ((evidence.TerrainFlags & IsWall) == 0 ||
                     !TryGetAdjacentRebuildTransform(
                         tileId,
                         evidence.OwnerId,
                         transforms,
                         out transform)))
                {
                    continue;
                }

                Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate);
                MapCoordinate target = TransformRebuiltStartCoordinate(
                    coordinate,
                    transform.Keep,
                    transform.Start);
                MarkUncertainNativeStartArea(coordinate);
                MarkUncertainNativeStartArea(target);
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

        private void MarkUncertainNativeStartKeepArea(MapCoordinate keep)
        {
            for (int y = keep.Y - 24; y <= keep.Y + 24; y++)
            {
                for (int x = keep.X - 24; x <= keep.X + 24; x++)
                {
                    if (Geometry.TryGetTileId(x, y, out int nearbyTileId))
                        uncertainNativeStartTileIds.Add(nearbyTileId);
                }
            }
        }

        private string DetectPotentialConnectedRecordCleanup(
            IReadOnlyDictionary<ushort, StartRebuildTransform> transforms)
        {
            // The type-41 compound constructor only enters whole-record
            // collision cleanup after one of its Keep, camp or yard footprints
            // meets an existing building. Its static offsets fit within this
            // wider 24-tile region around the selected AIV Keep marker.
            var checkedKeeps = new HashSet<(MapCoordinate Keep, byte OwnerId)>();
            foreach (StartRebuildTransform start in transforms.Values)
            {
                MapCoordinate selectedKeep = new MapCoordinate(
                    start.Keep.X + start.Start.MarkerDeltaX,
                    start.Keep.Y + start.Start.MarkerDeltaY);
                if (!checkedKeeps.Add((selectedKeep, start.OwnerId)))
                    continue;
                for (int y = selectedKeep.Y - 24; y <= selectedKeep.Y + 24; y++)
                {
                    for (int x = selectedKeep.X - 24; x <= selectedKeep.X + 24; x++)
                    {
                        if (!Geometry.TryGetTileId(x, y, out int tileId))
                            continue;
                        AivPlacementTileEvidence existing = source.GetTileEvidence(tileId);
                        if (existing.BuildingId != 0 &&
                            !(transforms.TryGetValue(existing.BuildingId,
                                out StartRebuildTransform ownSource) &&
                              ownSource.Keep.Equals(start.Keep) &&
                              ownSource.Start.Equals(start.Start)))
                            return $"source tile ({x},{y}) buildingId={existing.BuildingId} owner={existing.OwnerId} near AI start owner={start.OwnerId}";
                        if (rebuiltStartCellsByTileId.TryGetValue(
                                tileId, out RebuiltStartCell rebuilt) &&
                            rebuilt.BuildingId != 0 &&
                            !(transforms.TryGetValue(rebuilt.BuildingId,
                                out StartRebuildTransform ownRebuild) &&
                              ownRebuild.Keep.Equals(start.Keep) &&
                              ownRebuild.Start.Equals(start.Start)))
                            return $"rebuilt tile ({x},{y}) buildingId={rebuilt.BuildingId} owner={rebuilt.OwnerId} near AI start owner={start.OwnerId}";
                    }
                }
            }
            return null;
        }

        private bool TryGetAdjacentRebuildTransform(
            int tileId,
            byte wallOwnerId,
            IReadOnlyDictionary<ushort, StartRebuildTransform> transforms,
            out StartRebuildTransform transform)
        {
            if (wallOwnerId == 0)
            {
                transform = default;
                return false;
            }

            Geometry.TryGetCoordinate(tileId, out MapCoordinate coordinate);
            bool found = false;
            StartRebuildTransform selected = default;
            for (int y = coordinate.Y - 1; y <= coordinate.Y + 1; y++)
            {
                for (int x = coordinate.X - 1; x <= coordinate.X + 1; x++)
                {
                    if ((x != coordinate.X || y != coordinate.Y) &&
                        Geometry.TryGetTileId(x, y, out int neighborTileId) &&
                        transforms.TryGetValue(
                            source.GetTileEvidence(neighborTileId).BuildingId,
                            out StartRebuildTransform adjacent) &&
                        adjacent.OwnerId == wallOwnerId)
                    {
                        if (found &&
                            (!selected.Keep.Equals(adjacent.Keep) ||
                             !selected.Start.Equals(adjacent.Start)))
                        {
                            throw new InvalidOperationException(
                                $"Wall tile {coordinate} belongs to multiple rebuilt starts.");
                        }
                        selected = adjacent;
                        found = true;
                    }
                }
            }

            transform = selected;
            return found;
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
            public StartRebuildTransform(
                MapCoordinate keep,
                AivStartRebuildState start,
                byte ownerId)
            {
                Keep = keep;
                Start = start;
                OwnerId = ownerId;
            }

            public MapCoordinate Keep { get; }
            public AivStartRebuildState Start { get; }
            public byte OwnerId { get; }
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
