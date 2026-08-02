using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AIVParser.Core;
using MapParser.Core;

namespace AIVPlacement.Core
{
    public enum AivProjectedElementKind
    {
        Placement,
        AnchorOnly
    }

    public enum AivProjectedTileKind
    {
        CoreFootprint,
        AssociatedBlockedArea,
        ElementAnchor
    }

    public sealed class AivProjectedTile
    {
        internal AivProjectedTile(
            int elementIndex,
            AivProjectedTileKind kind,
            AivGridPoint sourceAivCoordinate,
            AivGridPoint rotatedAivCoordinate,
            MapCoordinate mapCoordinate,
            string associatedAreaName,
            AivBlockedAreaKind? associatedAreaKind,
            AivBlockedAreaSource? associatedAreaSource)
        {
            ElementIndex = elementIndex;
            Kind = kind;
            SourceAivCoordinate = sourceAivCoordinate;
            RotatedAivCoordinate = rotatedAivCoordinate;
            MapCoordinate = mapCoordinate;
            AssociatedAreaName = associatedAreaName ?? string.Empty;
            AssociatedAreaKind = associatedAreaKind;
            AssociatedAreaSource = associatedAreaSource;
        }

        // This stable index makes every tile traceable to its source element.
        public int ElementIndex { get; }
        public AivProjectedTileKind Kind { get; }
        public AivGridPoint SourceAivCoordinate { get; }
        public AivGridPoint RotatedAivCoordinate { get; }
        public MapCoordinate MapCoordinate { get; }
        public string AssociatedAreaName { get; }
        public AivBlockedAreaKind? AssociatedAreaKind { get; }
        public AivBlockedAreaSource? AssociatedAreaSource { get; }
    }

    public sealed class AivProjectedElement
    {
        internal AivProjectedElement(
            int originalIndex,
            int buildIndex,
            int positionIndex,
            int rawItemType,
            AivMapperInfo mapper,
            bool shouldPause,
            AivRotation rotation,
            AivGridPoint aivCoordinate,
            AivGridPoint rotatedAivCoordinate,
            MapCoordinate mapCoordinate,
            AivProjectedElementKind kind,
            IReadOnlyList<AivProjectedTile> occupiedTiles)
        {
            OriginalIndex = originalIndex;
            BuildIndex = buildIndex;
            PositionIndex = positionIndex;
            RawItemType = rawItemType;
            Mapper = mapper;
            ShouldPause = shouldPause;
            Rotation = rotation;
            AivCoordinate = aivCoordinate;
            RotatedAivCoordinate = rotatedAivCoordinate;
            MapCoordinate = mapCoordinate;
            Kind = kind;
            OccupiedTiles = ProjectionCollections.Copy(occupiedTiles);
        }

        public int OriginalIndex { get; }
        public int BuildIndex { get; }
        public int PositionIndex { get; }
        public int RawItemType { get; }
        public AivMapperInfo Mapper { get; }
        public bool ShouldPause { get; }
        public AivRotation Rotation { get; }
        public AivGridPoint AivCoordinate { get; }
        public AivGridPoint RotatedAivCoordinate { get; }
        public MapCoordinate MapCoordinate { get; }
        public AivProjectedElementKind Kind { get; }
        public IReadOnlyList<AivProjectedTile> OccupiedTiles { get; }
    }

    public sealed class AivProjectedBuildStep
    {
        internal AivProjectedBuildStep(
            int buildIndex,
            int rawItemType,
            AivMapperInfo mapper,
            bool shouldPause,
            int pauseDelayAmount,
            IReadOnlyList<AivProjectedElement> elements)
        {
            BuildIndex = buildIndex;
            RawItemType = rawItemType;
            Mapper = mapper;
            ShouldPause = shouldPause;
            PauseDelayAmount = pauseDelayAmount;
            Elements = ProjectionCollections.Copy(elements);
        }

        public int BuildIndex { get; }
        public int RawItemType { get; }
        public AivMapperInfo Mapper { get; }
        public bool ShouldPause { get; }
        public int PauseDelayAmount { get; }
        public IReadOnlyList<AivProjectedElement> Elements { get; }
        public bool HasPlacements => Elements.Count != 0;
    }

    public sealed class AivProjectedCastle
    {
        internal AivProjectedCastle(
            string sourceName,
            AivRotation rotation,
            AivGridPoint aivKeepAnchor,
            MapCoordinate mapKeepAnchor,
            IReadOnlyList<AivProjectedBuildStep> buildSteps,
            IReadOnlyList<AivProjectedElement> elements,
            IReadOnlyList<AivProjectedTile> occupiedTiles)
        {
            SourceName = sourceName ?? string.Empty;
            Rotation = rotation;
            AivKeepAnchor = aivKeepAnchor;
            MapKeepAnchor = mapKeepAnchor;
            BuildSteps = ProjectionCollections.Copy(buildSteps);
            Elements = ProjectionCollections.Copy(elements);
            OccupiedTiles = ProjectionCollections.Copy(occupiedTiles);
        }

        public string SourceName { get; }
        public AivRotation Rotation { get; }
        public AivGridPoint AivKeepAnchor { get; }
        public MapCoordinate MapKeepAnchor { get; }
        public IReadOnlyList<AivProjectedBuildStep> BuildSteps { get; }
        public IReadOnlyList<AivProjectedElement> Elements { get; }
        public IReadOnlyList<AivProjectedTile> OccupiedTiles { get; }
    }

    internal static class ProjectionCollections
    {
        public static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            var values = new T[source.Count];
            for (int index = 0; index < source.Count; index++)
                values[index] = source[index];
            return new ReadOnlyCollection<T>(values);
        }
    }
}
