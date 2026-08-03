using System;
using System.Collections.Generic;
using AIVParser.Core;
using MapParser.Core;

namespace AIVPlacement.Core
{
    public sealed class AivCastleProjector
    {
        private const int NativeKeepReferenceRow = 56;
        private const int NativeKeepReferenceColumn = 43;

        public AivProjectedCastle Project(
            AivBlueprint blueprint,
            MapCoordinate mapKeepAnchor,
            AivRotation rotation)
        {
            if (blueprint == null)
                throw new ArgumentNullException(nameof(blueprint));
            if (!blueprint.KeepAnchor.HasValue)
            {
                throw new ArgumentException(
                    "The AIV blueprint has no exact keep anchor.",
                    nameof(blueprint));
            }

            ValidateRotation(rotation);

            AivGridPoint aivKeepAnchor = blueprint.KeepAnchor.Value;
            var buildSteps = new List<AivProjectedBuildStep>(blueprint.Frames.Count);
            var elements = new List<AivProjectedElement>();
            var occupiedTiles = new List<AivProjectedTile>();

            // Frame order is the original AIV build order and must remain untouched.
            foreach (AivBuildFrame frame in blueprint.Frames)
            {
                var stepElements = new List<AivProjectedElement>(frame.Positions.Count);
                for (int positionIndex = 0;
                     positionIndex < frame.Positions.Count;
                     positionIndex++)
                {
                    AivGridPoint sourceAnchor = frame.Positions[positionIndex];
                    int elementIndex = elements.Count;
                    var elementTiles = new List<AivProjectedTile>();
                    AivProjectedElementKind elementKind =
                        frame.Mapper.FootprintSize.HasValue
                            ? AivProjectedElementKind.Placement
                            : AivProjectedElementKind.AnchorOnly;

                    if (frame.Mapper.FootprintSize.HasValue)
                    {
                        AddFootprintTiles(
                            elementTiles,
                            elementIndex,
                            sourceAnchor,
                            frame.Mapper.FootprintSize.Value,
                            mapKeepAnchor,
                            rotation,
                            AivProjectedTileKind.CoreFootprint,
                            string.Empty,
                            null,
                            null);

                        IReadOnlyList<AivBlockedArea> blockedAreas =
                            AivBlockedAreaCatalog.Resolve(
                                frame.Mapper,
                                sourceAnchor,
                                rotation);
                        foreach (AivBlockedArea blockedArea in blockedAreas)
                        {
                            AddFootprintTiles(
                                elementTiles,
                                elementIndex,
                                blockedArea.Footprint.RawAnchor,
                                blockedArea.Footprint.Size,
                                mapKeepAnchor,
                                rotation,
                                AivProjectedTileKind.AssociatedBlockedArea,
                                blockedArea.Name,
                                blockedArea.Kind,
                                blockedArea.Source);
                        }
                    }

                    AivWorldTile projectedAnchor = ProjectNativeFitTile(
                        sourceAnchor,
                        mapKeepAnchor.X,
                        mapKeepAnchor.Y,
                        rotation);
                    var element = new AivProjectedElement(
                        elementIndex,
                        frame.BuildIndex,
                        positionIndex,
                        frame.RawItemType,
                        frame.Mapper,
                        frame.ShouldPause,
                        rotation,
                        sourceAnchor,
                        AivGridTransform.Rotate(sourceAnchor, rotation),
                        new MapCoordinate(projectedAnchor.X, projectedAnchor.Y),
                        elementKind,
                        elementTiles);
                    elements.Add(element);
                    stepElements.Add(element);
                    occupiedTiles.AddRange(elementTiles);
                }

                // Empty frames remain visible but cannot claim any map tiles.
                buildSteps.Add(new AivProjectedBuildStep(
                    frame.BuildIndex,
                    frame.RawItemType,
                    frame.Mapper,
                    frame.ShouldPause,
                    blueprint.PauseDelayAmount,
                    stepElements));
            }

            return new AivProjectedCastle(
                blueprint.SourceName,
                rotation,
                aivKeepAnchor,
                mapKeepAnchor,
                buildSteps,
                elements,
                occupiedTiles);
        }

        private static void AddFootprintTiles(
            ICollection<AivProjectedTile> target,
            int elementIndex,
            AivGridPoint rawAnchor,
            int size,
            MapCoordinate mapKeepAnchor,
            AivRotation rotation,
            AivProjectedTileKind kind,
            string associatedAreaName,
            AivBlockedAreaKind? associatedAreaKind,
            AivBlockedAreaSource? associatedAreaSource)
        {
            // Stored AIV footprints grow toward smaller rows and larger columns.
            int firstRow = rawAnchor.Row - size + 1;
            int lastColumn = rawAnchor.Column + size - 1;
            for (int row = firstRow; row <= rawAnchor.Row; row++)
            {
                for (int column = rawAnchor.Column; column <= lastColumn; column++)
                {
                    var sourcePoint = new AivGridPoint(row, column);
                    AivWorldTile projected = ProjectNativeFitTile(
                        sourcePoint,
                        mapKeepAnchor.X,
                        mapKeepAnchor.Y,
                        rotation);
                    target.Add(new AivProjectedTile(
                        elementIndex,
                        kind,
                        sourcePoint,
                        AivGridTransform.Rotate(sourcePoint, rotation),
                        new MapCoordinate(projected.X, projected.Y),
                        associatedAreaName,
                        associatedAreaKind,
                        associatedAreaSource));
                }
            }
        }

        private static AivWorldTile ProjectNativeFitTile(
            AivGridPoint point,
            int keepWorldX,
            int keepWorldY,
            AivRotation rotation)
        {
            AivGridPoint rotatedPoint = AivGridTransform.Rotate(point, rotation);

            // Native fit evaluation keeps a fixed grid origin even when a custom AIV stores
            // its Keep elsewhere; row 56/column 43 is the corresponding world-axis reference.
            return new AivWorldTile(
                keepWorldX + rotatedPoint.Column - NativeKeepReferenceColumn,
                keepWorldY - rotatedPoint.Row + NativeKeepReferenceRow);
        }

        private static void ValidateRotation(AivRotation rotation)
        {
            if (rotation != AivRotation.Degrees0 &&
                rotation != AivRotation.Degrees90 &&
                rotation != AivRotation.Degrees180 &&
                rotation != AivRotation.Degrees270)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rotation),
                    rotation,
                    "Unsupported AIV rotation.");
            }
        }
    }
}
