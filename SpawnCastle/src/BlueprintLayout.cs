using AIVParser.Core;
using System;
using System.Collections.Generic;

namespace SpawnCastle
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
            int maximumWorldY)
        {
            MapperValue = mapperValue;
            MinimumWorldX = minimumWorldX;
            MaximumWorldX = maximumWorldX;
            MinimumWorldY = minimumWorldY;
            MaximumWorldY = maximumWorldY;
        }

        public int MapperValue { get; }
        public int MinimumWorldX { get; }
        public int MaximumWorldX { get; }
        public int MinimumWorldY { get; }
        public int MaximumWorldY { get; }
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
            var tiles = new Dictionary<BlueprintWorldTile, BlueprintTilePlacement>();
            var icons = new List<BlueprintIconPlacement>();
            var unknownMappers = new HashSet<int>();

            foreach (AivJsonFrame frame in document.frames)
            {
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

                    int minimumWorldX = int.MaxValue;
                    int maximumWorldX = int.MinValue;
                    int minimumWorldY = int.MaxValue;
                    int maximumWorldY = int.MinValue;
                    for (int row = footprint.Minimum.Row;
                         row <= footprint.Maximum.Row;
                         row++)
                    {
                        for (int column = footprint.Minimum.Column;
                             column <= footprint.Maximum.Column;
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

                            minimumWorldX = Math.Min(minimumWorldX, world.X);
                            maximumWorldX = Math.Max(maximumWorldX, world.X);
                            minimumWorldY = Math.Min(minimumWorldY, world.Y);
                            maximumWorldY = Math.Max(maximumWorldY, world.Y);
                        }
                    }

                    if (mapper.Category == AivItemCategory.Building)
                    {
                        icons.Add(new BlueprintIconPlacement(
                            frame.itemType,
                            minimumWorldX,
                            maximumWorldX,
                            minimumWorldY,
                            maximumWorldY));
                    }
                }
            }

            return new BlueprintLayout(
                new List<BlueprintTilePlacement>(tiles.Values),
                icons,
                unknownMappers.Count);
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
    }
}
