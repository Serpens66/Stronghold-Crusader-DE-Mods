using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MapParser.Core
{
    public enum MapKeepAnchorStatus
    {
        Exact,
        NotEvaluable
    }

    public enum MapKeepAnchorFailureKind
    {
        None,
        SlotNotSelectable,
        InvalidSlotMetadata,
        SectionsUnavailable,
        BuildingSectionMissing,
        BuildingSectionUnavailable,
        InvalidBuildingSectionLength,
        UnsupportedGeometry,
        KeepRecordMissing,
        AmbiguousKeepRecords,
        InvalidKeepCoordinate,
        KeepOutsideWorldBounds
    }

    public sealed class MapKeepAnchorResult
    {
        internal MapKeepAnchorResult(
            int slotIndex,
            bool isSelectable,
            MapCoordinate radarCoordinate,
            MapKeepAnchorStatus status,
            MapKeepAnchorFailureKind failureKind,
            MapCoordinate? coordinate = null,
            int? tileId = null,
            int? buildingRecordIndex = null)
        {
            SlotIndex = slotIndex;
            IsSelectable = isSelectable;
            RadarCoordinate = radarCoordinate;
            Status = status;
            FailureKind = failureKind;
            Coordinate = coordinate;
            TileId = tileId;
            BuildingRecordIndex = buildingRecordIndex;
        }

        public int SlotIndex { get; }
        public bool IsSelectable { get; }
        public MapCoordinate RadarCoordinate { get; }
        public MapKeepAnchorStatus Status { get; }
        public MapKeepAnchorFailureKind FailureKind { get; }
        public MapCoordinate? Coordinate { get; }
        public int? TileId { get; }
        public int? BuildingRecordIndex { get; }
    }

    public sealed class MapKeepAnchors
    {
        public const int SlotCount = 8;

        private const int BuildingRecordSize = 0x32C;
        private const int BuildingRecordCount = 2000;
        private const int AliveStateOffset = 0xD0;
        private const int BuildingTypeOffset = 0xD2;
        private const int OwnerOffset = 0xD6;
        private const int TileXOffset = 0xEE;
        private const int TileYOffset = 0xF0;
        private const short AliveStateIsAlive = 2;
        private const ushort KeepMarkerBuildingType = 41;

        private readonly IReadOnlyList<MapKeepAnchorResult> slots;

        private MapKeepAnchors(MapKeepAnchorResult[] slots)
        {
            this.slots = new ReadOnlyCollection<MapKeepAnchorResult>(slots);
        }

        public IReadOnlyList<MapKeepAnchorResult> Slots => slots;

        public MapKeepAnchorResult GetSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            return slots[slotIndex];
        }

        public static MapKeepAnchors Create(MapDocument document)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));

            var results = new MapKeepAnchorResult[SlotCount];
            var evaluableSlots = new List<int>(SlotCount);
            for (int slotIndex = 0; slotIndex < SlotCount; slotIndex++)
            {
                MapCoordinate radar = slotIndex < document.Metadata.KeepLocations.Count
                    ? document.Metadata.KeepLocations[slotIndex]
                    : new MapCoordinate(-1, -1);
                if (radar.X == -1 && radar.Y == -1)
                {
                    results[slotIndex] = NotEvaluable(
                        slotIndex,
                        false,
                        radar,
                        MapKeepAnchorFailureKind.SlotNotSelectable);
                }
                else if (radar.X < 0 || radar.Y < 0)
                {
                    results[slotIndex] = NotEvaluable(
                        slotIndex,
                        false,
                        radar,
                        MapKeepAnchorFailureKind.InvalidSlotMetadata);
                }
                else
                {
                    evaluableSlots.Add(slotIndex);
                }
            }

            if (evaluableSlots.Count == 0)
                return new MapKeepAnchors(results);

            if (!document.SectionsAvailable)
            {
                SetFailure(results, evaluableSlots, document, MapKeepAnchorFailureKind.SectionsUnavailable);
                return new MapKeepAnchors(results);
            }
            if (!document.TryGetLogicalSection(MapSectionCatalog.BuildingObjects, out MapSectionInfo section))
            {
                SetFailure(results, evaluableSlots, document, MapKeepAnchorFailureKind.BuildingSectionMissing);
                return new MapKeepAnchors(results);
            }
            if (!section.IsContentAvailable)
            {
                SetFailure(results, evaluableSlots, document, MapKeepAnchorFailureKind.BuildingSectionUnavailable);
                return new MapKeepAnchors(results);
            }
            if (section.UncompressedSize != BuildingRecordSize * BuildingRecordCount)
            {
                SetFailure(results, evaluableSlots, document, MapKeepAnchorFailureKind.InvalidBuildingSectionLength);
                return new MapKeepAnchors(results);
            }

            MapTileGeometry geometry;
            try
            {
                geometry = new MapTileGeometry(MapTileGeometry.FixedTileCount, document.Metadata.WorldSize);
            }
            catch (MapUnsupportedGeometryException)
            {
                SetFailure(results, evaluableSlots, document, MapKeepAnchorFailureKind.UnsupportedGeometry);
                return new MapKeepAnchors(results);
            }

            byte[] records = section.GetOrReadContent();
            var recordsBySlot = new List<KeepRecord>[SlotCount];
            for (int index = 0; index < BuildingRecordCount; index++)
            {
                int offset = index * BuildingRecordSize;
                short aliveState = LittleEndian.ReadInt16(records, offset + AliveStateOffset);
                ushort buildingType = LittleEndian.ReadUInt16(records, offset + BuildingTypeOffset);
                if (aliveState != AliveStateIsAlive || buildingType != KeepMarkerBuildingType)
                {
                    continue;
                }

                ushort owner = LittleEndian.ReadUInt16(records, offset + OwnerOffset);
                if (owner < 1 || owner > SlotCount)
                    continue;

                int slotIndex = owner - 1;
                if (recordsBySlot[slotIndex] == null)
                    recordsBySlot[slotIndex] = new List<KeepRecord>();
                recordsBySlot[slotIndex].Add(new KeepRecord(
                    index,
                    LittleEndian.ReadUInt16(records, offset + TileXOffset),
                    LittleEndian.ReadUInt16(records, offset + TileYOffset)));
            }

            foreach (int slotIndex in evaluableSlots)
            {
                MapCoordinate radar = document.Metadata.KeepLocations[slotIndex];
                List<KeepRecord> matches = recordsBySlot[slotIndex];
                if (matches == null || matches.Count == 0)
                {
                    results[slotIndex] = NotEvaluable(
                        slotIndex,
                        true,
                        radar,
                        MapKeepAnchorFailureKind.KeepRecordMissing);
                    continue;
                }
                if (matches.Count != 1)
                {
                    results[slotIndex] = NotEvaluable(
                        slotIndex,
                        true,
                        radar,
                        MapKeepAnchorFailureKind.AmbiguousKeepRecords);
                    continue;
                }

                KeepRecord match = matches[0];
                if (!geometry.TryGetTileId(match.X, match.Y, out int tileId))
                {
                    results[slotIndex] = NotEvaluable(
                        slotIndex,
                        true,
                        radar,
                        MapKeepAnchorFailureKind.InvalidKeepCoordinate);
                    continue;
                }
                if (!geometry.IsWithinWorldBounds(match.X, match.Y))
                {
                    results[slotIndex] = NotEvaluable(
                        slotIndex,
                        true,
                        radar,
                        MapKeepAnchorFailureKind.KeepOutsideWorldBounds);
                    continue;
                }

                // The building record stores the same world-tile pair passed to Vanilla BuildStructure.
                results[slotIndex] = new MapKeepAnchorResult(
                    slotIndex,
                    true,
                    radar,
                    MapKeepAnchorStatus.Exact,
                    MapKeepAnchorFailureKind.None,
                    new MapCoordinate(match.X, match.Y),
                    tileId,
                    match.Index);
            }

            return new MapKeepAnchors(results);
        }

        private static void SetFailure(
            MapKeepAnchorResult[] results,
            IEnumerable<int> slotIndexes,
            MapDocument document,
            MapKeepAnchorFailureKind failureKind)
        {
            foreach (int slotIndex in slotIndexes)
            {
                results[slotIndex] = NotEvaluable(
                    slotIndex,
                    true,
                    document.Metadata.KeepLocations[slotIndex],
                    failureKind);
            }
        }

        private static MapKeepAnchorResult NotEvaluable(
            int slotIndex,
            bool isSelectable,
            MapCoordinate radarCoordinate,
            MapKeepAnchorFailureKind failureKind) =>
            new MapKeepAnchorResult(
                slotIndex,
                isSelectable,
                radarCoordinate,
                MapKeepAnchorStatus.NotEvaluable,
                failureKind);

        private readonly struct KeepRecord
        {
            public KeepRecord(int index, int x, int y)
            {
                Index = index;
                X = x;
                Y = y;
            }

            public int Index { get; }
            public int X { get; }
            public int Y { get; }
        }
    }
}
