#nullable disable

using AIVParser.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CastlePlanner
{
    internal readonly struct BlueprintWorldTile : IEquatable<BlueprintWorldTile>
    {
        public BlueprintWorldTile(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool Equals(BlueprintWorldTile other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is BlueprintWorldTile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }
    }

    internal sealed class BlueprintTilePlacement
    {
        public BlueprintTilePlacement(
            BlueprintWorldTile tile,
            AivItemCategory category,
            AivVisualGroup visualGroup)
        {
            Tile = tile;
            Category = category;
            VisualGroup = visualGroup;
        }

        public BlueprintWorldTile Tile { get; }
        public AivItemCategory Category { get; }
        public AivVisualGroup VisualGroup { get; }
    }

    internal sealed class BlueprintIconPlacement
    {
        public BlueprintIconPlacement(
            int mapperValue,
            int minimumWorldX,
            int maximumWorldX,
            int minimumWorldY,
            int maximumWorldY,
            int markerMinimumWorldX,
            int markerMaximumWorldX,
            int markerMinimumWorldY,
            int markerMaximumWorldY,
            BlueprintWorldTile? adjacentGateCenter = null,
            BlueprintWorldTile? stairLowEnd = null,
            BlueprintWorldTile? stairHighEnd = null)
        {
            MapperValue = mapperValue;
            MinimumWorldX = minimumWorldX;
            MaximumWorldX = maximumWorldX;
            MinimumWorldY = minimumWorldY;
            MaximumWorldY = maximumWorldY;
            MarkerMinimumWorldX = markerMinimumWorldX;
            MarkerMaximumWorldX = markerMaximumWorldX;
            MarkerMinimumWorldY = markerMinimumWorldY;
            MarkerMaximumWorldY = markerMaximumWorldY;
            AdjacentGateCenter = adjacentGateCenter;
            StairLowEnd = stairLowEnd;
            StairHighEnd = stairHighEnd;
        }

        public int MapperValue { get; }
        public int MinimumWorldX { get; }
        public int MaximumWorldX { get; }
        public int MinimumWorldY { get; }
        public int MaximumWorldY { get; }
        public int MarkerMinimumWorldX { get; }
        public int MarkerMaximumWorldX { get; }
        public int MarkerMinimumWorldY { get; }
        public int MarkerMaximumWorldY { get; }
        public BlueprintWorldTile? AdjacentGateCenter { get; }
        public BlueprintWorldTile? StairLowEnd { get; }
        public BlueprintWorldTile? StairHighEnd { get; }
        public int Size => Math.Max(
            MaximumWorldX - MinimumWorldX + 1,
            MaximumWorldY - MinimumWorldY + 1);
    }

    internal sealed class BlueprintLayout
    {
        public BlueprintLayout(
            IReadOnlyList<BlueprintTilePlacement> tiles,
            IReadOnlyList<BlueprintIconPlacement> icons,
            int unknownMapperCount)
        {
            Tiles = tiles;
            Icons = icons;
            UnknownMapperCount = unknownMapperCount;
        }

        public IReadOnlyList<BlueprintTilePlacement> Tiles { get; }
        public IReadOnlyList<BlueprintIconPlacement> Icons { get; }
        public int UnknownMapperCount { get; }
    }

    internal static class BlueprintLayoutBuilder
    {
        private const int DrawbridgeMapperValue = 105;

        public static BlueprintLayout Build(
            AivJsonDocument document,
            int keepWorldX,
            int keepWorldY)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (document.frames == null)
                throw new FormatException("The AIVJSON does not contain a frames array.");

            var keepPositions = new List<int>();
            foreach (AivJsonFrame frame in document.frames)
            {
                if (frame != null && AivMapperCatalog.IsKeep(frame.itemType))
                {
                    if (frame.tilePositionOfsets != null)
                        keepPositions.AddRange(frame.tilePositionOfsets);
                }
            }

            if (keepPositions.Count != 1)
            {
                throw new FormatException(
                    $"The AIVJSON must contain exactly one Keep placement; found {keepPositions.Count}.");
            }

            var keepAnchor = CreateGridPoint(keepPositions[0], "Keep");
            IReadOnlyList<BlueprintGatePlacement> gatePlacements =
                CollectGatePlacements(document.frames);
            IReadOnlyDictionary<int, BlueprintStairEndpoints>
                stairEndpointsByOffset = CollectStairEndpoints(document.frames);
            var tiles = new Dictionary<BlueprintWorldTile, BlueprintTilePlacement>();
            var icons = new List<BlueprintIconPlacement>();
            var unknownMappers = new HashSet<int>();

            for (int frameIndex = 0;
                 frameIndex < document.frames.Count;
                 frameIndex++)
            {
                AivJsonFrame frame = document.frames[frameIndex];
                if (frame == null || AivMapperCatalog.IsKeep(frame.itemType))
                    continue;
                if (frame.tilePositionOfsets == null ||
                    frame.tilePositionOfsets.Count == 0)
                {
                    continue;
                }

                AivMapperInfo mapper = AivMapperCatalog.Resolve(frame.itemType);
                if (!mapper.IsKnown)
                    unknownMappers.Add(frame.itemType);

                int footprintSize = mapper.FootprintSize ?? 1;
                foreach (int encodedOffset in frame.tilePositionOfsets)
                {
                    AivGridPoint anchor =
                        CreateGridPoint(encodedOffset, mapper.Name);
                    AivFootprint footprint = AivGridTransform.GetFootprint(
                        anchor,
                        footprintSize,
                        AivRotation.Degrees0);

                    int markerMinimumRow = footprint.Minimum.Row;
                    int markerMaximumRow = footprint.Maximum.Row;
                    int markerMinimumColumn = footprint.Minimum.Column;
                    int markerMaximumColumn = footprint.Maximum.Column;
                    if (BlueprintBuildingIconCatalog
                        .TryGetReservedFootprintDimensions(
                            mapper.Name,
                            footprintSize,
                            out int reservedRows,
                            out int reservedColumns))
                    {
                        // Vanilla grows each special yard away from the same
                        // AIV anchor. Only marker bounds use this extra space;
                        // icon bounds below remain on the visible core.
                        markerMinimumRow = Math.Max(
                            0,
                            anchor.Row - reservedRows + 1);
                        markerMaximumColumn = Math.Min(
                            AivGridPoint.GridSize - 1,
                            anchor.Column + reservedColumns - 1);
                    }

                    int minimumWorldX = int.MaxValue;
                    int maximumWorldX = int.MinValue;
                    int minimumWorldY = int.MaxValue;
                    int maximumWorldY = int.MinValue;
                    int markerMinimumWorldX = int.MaxValue;
                    int markerMaximumWorldX = int.MinValue;
                    int markerMinimumWorldY = int.MaxValue;
                    int markerMaximumWorldY = int.MinValue;
                    for (int row = markerMinimumRow;
                         row <= markerMaximumRow;
                         row++)
                    {
                        for (int column = markerMinimumColumn;
                             column <= markerMaximumColumn;
                             column++)
                        {
                            AivWorldTile world = AivWorldTransform.Project(
                                new AivGridPoint(row, column),
                                keepAnchor,
                                keepWorldX,
                                keepWorldY,
                                AivRotation.Degrees0);
                            var tile = new BlueprintWorldTile(world.X, world.Y);
                            tiles[tile] = new BlueprintTilePlacement(
                                tile,
                                mapper.Category,
                                mapper.VisualGroup);
                            markerMinimumWorldX = Math.Min(markerMinimumWorldX, world.X);
                            markerMaximumWorldX = Math.Max(markerMaximumWorldX, world.X);
                            markerMinimumWorldY = Math.Min(markerMinimumWorldY, world.Y);
                            markerMaximumWorldY = Math.Max(markerMaximumWorldY, world.Y);

                            if (row >= footprint.Minimum.Row &&
                                row <= footprint.Maximum.Row &&
                                column >= footprint.Minimum.Column &&
                                column <= footprint.Maximum.Column)
                            {
                                minimumWorldX = Math.Min(minimumWorldX, world.X);
                                maximumWorldX = Math.Max(maximumWorldX, world.X);
                                minimumWorldY = Math.Min(minimumWorldY, world.Y);
                                maximumWorldY = Math.Max(maximumWorldY, world.Y);
                            }
                        }
                    }

                    // Path frames enumerate individual cells, so giving every
                    // mapped placement an icon also covers walls and defenses.
                    if (BlueprintBuildingIconCatalog.Resolve(mapper.Name) != null)
                    {
                        BlueprintWorldTile? adjacentGateCenter = null;
                        BlueprintWorldTile? stairLowEnd = null;
                        BlueprintWorldTile? stairHighEnd = null;
                        if (frame.itemType == DrawbridgeMapperValue &&
                            TryFindAdjacentGate(
                                footprint,
                                frameIndex,
                                gatePlacements,
                                out BlueprintGatePlacement adjacentGate))
                        {
                            AivGridPoint gateCenter = GetCenter(
                                adjacentGate.Footprint);
                            AivWorldTile gateWorld = AivWorldTransform.Project(
                                gateCenter,
                                keepAnchor,
                                keepWorldX,
                                keepWorldY,
                                AivRotation.Degrees0);
                            adjacentGateCenter = new BlueprintWorldTile(
                                gateWorld.X,
                                gateWorld.Y);
                        }

                        if (stairEndpointsByOffset.TryGetValue(
                                encodedOffset,
                                out BlueprintStairEndpoints stairEndpoints))
                        {
                            AivWorldTile lowWorld = AivWorldTransform.Project(
                                stairEndpoints.Low,
                                keepAnchor,
                                keepWorldX,
                                keepWorldY,
                                AivRotation.Degrees0);
                            AivWorldTile highWorld = AivWorldTransform.Project(
                                stairEndpoints.High,
                                keepAnchor,
                                keepWorldX,
                                keepWorldY,
                                AivRotation.Degrees0);
                            stairLowEnd = new BlueprintWorldTile(
                                lowWorld.X,
                                lowWorld.Y);
                            stairHighEnd = new BlueprintWorldTile(
                                highWorld.X,
                                highWorld.Y);
                        }

                        icons.Add(new BlueprintIconPlacement(
                            frame.itemType,
                            minimumWorldX,
                            maximumWorldX,
                            minimumWorldY,
                            maximumWorldY,
                            markerMinimumWorldX,
                            markerMaximumWorldX,
                            markerMinimumWorldY,
                            markerMaximumWorldY,
                            adjacentGateCenter,
                            stairLowEnd,
                            stairHighEnd));
                    }
                }
            }

            return new BlueprintLayout(
                new List<BlueprintTilePlacement>(tiles.Values),
                icons,
                unknownMappers.Count);
        }

        private static IReadOnlyDictionary<int, BlueprintStairEndpoints>
            CollectStairEndpoints(IReadOnlyList<AivJsonFrame> frames)
        {
            var segments = new List<BlueprintStairSegment>();
            foreach (AivJsonFrame frame in frames)
            {
                if (frame == null ||
                    frame.itemType < 181 ||
                    frame.itemType > 186 ||
                    frame.tilePositionOfsets == null)
                {
                    continue;
                }

                foreach (int offset in frame.tilePositionOfsets)
                {
                    segments.Add(new BlueprintStairSegment(
                        offset,
                        frame.itemType,
                        CreateGridPoint(offset, "Stair")));
                }
            }

            var result = new Dictionary<int, BlueprintStairEndpoints>();
            var unused = new List<BlueprintStairSegment>(segments);
            while (unused.Count > 0)
            {
                BlueprintStairSegment first = unused
                    .OrderBy(value => value.MapperValue)
                    .First();
                unused.Remove(first);
                var chain = new List<BlueprintStairSegment> { first };
                BlueprintStairSegment current = first;
                for (int mapper = first.MapperValue + 1;
                    mapper <= 186;
                    mapper++)
                {
                    BlueprintStairSegment next = unused
                        .Where(value => value.MapperValue == mapper &&
                            AreAdjacent(current.Point, value.Point))
                        .OrderBy(value => value.Offset)
                        .FirstOrDefault();
                    if (next == null)
                        break;
                    unused.Remove(next);
                    chain.Add(next);
                    current = next;
                }

                var endpoints = new BlueprintStairEndpoints(
                    chain.First().Point,
                    chain.Last().Point);
                foreach (BlueprintStairSegment segment in chain)
                    result[segment.Offset] = endpoints;
            }

            return result;
        }

        private static bool AreAdjacent(AivGridPoint first, AivGridPoint second)
        {
            return Math.Max(
                Math.Abs(first.Row - second.Row),
                Math.Abs(first.Column - second.Column)) <= 1;
        }

        private static IReadOnlyList<BlueprintGatePlacement>
            CollectGatePlacements(IReadOnlyList<AivJsonFrame> frames)
        {
            var placements = new List<BlueprintGatePlacement>();
            for (int frameIndex = 0;
                 frameIndex < frames.Count;
                 frameIndex++)
            {
                AivJsonFrame frame = frames[frameIndex];
                if (frame == null ||
                    !IsDirectionalStoneGate(frame.itemType) ||
                    frame.tilePositionOfsets == null)
                {
                    continue;
                }

                AivMapperInfo mapper = AivMapperCatalog.Resolve(frame.itemType);
                int footprintSize = mapper.FootprintSize ?? 1;
                foreach (int encodedOffset in frame.tilePositionOfsets)
                {
                    AivGridPoint anchor =
                        CreateGridPoint(encodedOffset, mapper.Name);
                    placements.Add(
                        new BlueprintGatePlacement(
                            frame.itemType,
                            frameIndex,
                            AivGridTransform.GetFootprint(
                                anchor,
                                footprintSize,
                                AivRotation.Degrees0)));
                }
            }

            return placements;
        }

        private static bool TryFindAdjacentGate(
            AivFootprint drawbridge,
            int drawbridgeFrameIndex,
            IReadOnlyList<BlueprintGatePlacement> gates,
            out BlueprintGatePlacement adjacentGate)
        {
            adjacentGate = null;
            int bestFrameDistance = int.MaxValue;
            int bestMatchCount = 0;
            foreach (BlueprintGatePlacement gate in gates)
            {
                bool touchesAcrossRows =
                    drawbridge.Maximum.Row + 1 == gate.Footprint.Minimum.Row ||
                    gate.Footprint.Maximum.Row + 1 == drawbridge.Minimum.Row;
                bool touchesAcrossColumns =
                    drawbridge.Maximum.Column + 1 ==
                        gate.Footprint.Minimum.Column ||
                    gate.Footprint.Maximum.Column + 1 ==
                        drawbridge.Minimum.Column;
                bool gateUsesRowAxis =
                    gate.MapperValue == 144 || gate.MapperValue == 146;
                AivGridPoint drawbridgeCenter = GetCenter(drawbridge);
                AivGridPoint gateCenter = GetCenter(gate.Footprint);
                bool centeredOnSharedEdge = touchesAcrossRows
                    ? drawbridgeCenter.Column == gateCenter.Column
                    : drawbridgeCenter.Row == gateCenter.Row;
                int overlap = touchesAcrossRows
                    ? GetInclusiveOverlap(
                        drawbridge.Minimum.Column,
                        drawbridge.Maximum.Column,
                        gate.Footprint.Minimum.Column,
                        gate.Footprint.Maximum.Column)
                    : GetInclusiveOverlap(
                        drawbridge.Minimum.Row,
                        drawbridge.Maximum.Row,
                        gate.Footprint.Minimum.Row,
                        gate.Footprint.Maximum.Row);

                // A drawbridge occupies five tiles and must touch the centered
                // five-tile edge of the matching A/B gatehouse orientation.
                if (overlap != drawbridge.Size ||
                    !centeredOnSharedEdge ||
                    (touchesAcrossRows && !gateUsesRowAxis) ||
                    (touchesAcrossColumns && gateUsesRowAxis) ||
                    (!touchesAcrossRows && !touchesAcrossColumns))
                {
                    continue;
                }

                int frameDistance = Math.Abs(
                    gate.FrameIndex - drawbridgeFrameIndex);
                if (frameDistance < bestFrameDistance)
                {
                    adjacentGate = gate;
                    bestFrameDistance = frameDistance;
                    bestMatchCount = 1;
                }
                else if (frameDistance == bestFrameDistance)
                {
                    bestMatchCount++;
                }
            }

            // AIV build order places a bridge directly beside its intended
            // gate, resolving rare layouts with gates on both bridge edges.
            if (bestMatchCount == 1)
                return true;

            adjacentGate = null;
            return false;
        }

        private static int GetInclusiveOverlap(
            int firstMinimum,
            int firstMaximum,
            int secondMinimum,
            int secondMaximum)
        {
            return Math.Max(
                0,
                Math.Min(firstMaximum, secondMaximum) -
                Math.Max(firstMinimum, secondMinimum) + 1);
        }

        private static AivGridPoint GetCenter(AivFootprint footprint)
        {
            return new AivGridPoint(
                (footprint.Minimum.Row + footprint.Maximum.Row) / 2,
                (footprint.Minimum.Column + footprint.Maximum.Column) / 2);
        }

        private static bool IsDirectionalStoneGate(int mapperValue)
        {
            return mapperValue >= 144 && mapperValue <= 147;
        }

        private static AivGridPoint CreateGridPoint(
            int encodedOffset,
            string itemName)
        {
            if (encodedOffset < 0 ||
                encodedOffset >= AivGridPoint.GridSize * AivGridPoint.GridSize)
            {
                throw new FormatException(
                    $"{itemName} contains an off-grid AIV offset: {encodedOffset}.");
            }

            return new AivGridPoint(encodedOffset);
        }

        private sealed class BlueprintGatePlacement
        {
            public BlueprintGatePlacement(
                int mapperValue,
                int frameIndex,
                AivFootprint footprint)
            {
                MapperValue = mapperValue;
                FrameIndex = frameIndex;
                Footprint = footprint;
            }

            public int MapperValue { get; }

            public int FrameIndex { get; }

            public AivFootprint Footprint { get; }
        }

        private sealed class BlueprintStairSegment
        {
            public BlueprintStairSegment(
                int offset,
                int mapperValue,
                AivGridPoint point)
            {
                Offset = offset;
                MapperValue = mapperValue;
                Point = point;
            }

            public int Offset { get; }

            public int MapperValue { get; }

            public AivGridPoint Point { get; }
        }

        private readonly struct BlueprintStairEndpoints
        {
            public BlueprintStairEndpoints(AivGridPoint low, AivGridPoint high)
            {
                Low = low;
                High = high;
            }

            public AivGridPoint Low { get; }

            public AivGridPoint High { get; }
        }
    }
}
