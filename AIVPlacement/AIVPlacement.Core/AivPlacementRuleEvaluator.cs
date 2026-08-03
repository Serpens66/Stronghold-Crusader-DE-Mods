using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MapParser.Core;

namespace AIVPlacement.Core
{
    public sealed class AivPlacementRuleEvaluator
    {
        private const int NativeCoordinateMaximum = 799;
        private const int NativeMaximumHeight = 200;

        private const int Sea = 0x00000001;
        private const int IsFarm = 0x00000004;
        private const int PitchTrap = 0x00000008;
        private const int MapEdges = 0x00000030;
        private const int ImpassableEdge = 0x00000080;
        private const int IsWall = 0x00000100;
        private const int IsBuildingOrElevated = 0x10000400;
        private const int River = 0x00100000;
        private const int Ford = 0x00200000;
        private const int FarmTypeFlags = 0x0F000000;
        private const int IsSwamp = 0x20000000;
        private const int IsMoat = 0x40000000;

        public AivElementPlacementResult EvaluateElement(
            MapPlacementSnapshot map,
            AivProjectedElement element)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            return EvaluateElement(new SnapshotTileSource(map), element);
        }

        public AivElementPlacementResult EvaluateElement(
            IAivPlacementTileSource map,
            AivProjectedElement element)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (map.Geometry == null)
                throw new ArgumentException("The tile source has no geometry.", nameof(map));
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            return new AivElementPlacementResult(
                element,
                EvaluateMapRules(map, element));
        }

        public IReadOnlyList<AivElementPlacementResult> EvaluateElements(
            MapPlacementSnapshot map,
            AivProjectedCastle castle)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            return EvaluateElements(new SnapshotTileSource(map), castle);
        }

        public IReadOnlyList<AivElementPlacementResult> EvaluateElements(
            IAivPlacementTileSource map,
            AivProjectedCastle castle)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (map.Geometry == null)
                throw new ArgumentException("The tile source has no geometry.", nameof(map));
            if (castle == null)
                throw new ArgumentNullException(nameof(castle));

            var results = new List<AivElementPlacementResult>(castle.Elements.Count);
            var lastClaimantByCoordinate = new Dictionary<MapCoordinate, int>();
            foreach (AivProjectedElement element in castle.Elements)
            {
                foreach (AivProjectedTile tile in element.OccupiedTiles)
                    lastClaimantByCoordinate[tile.MapCoordinate] = element.OriginalIndex;
            }

            var firstClaimants = new Dictionary<MapCoordinate, int>();
            foreach (AivProjectedElement element in castle.Elements)
            {
                var effectiveCoordinates = new HashSet<MapCoordinate>(
                    lastClaimantByCoordinate
                        .Where(pair => pair.Value == element.OriginalIndex)
                        .Select(pair => pair.Key));
                List<AivPlacementIssue> issues = EvaluateMapRules(
                    map,
                    element,
                    effectiveCoordinates);
                AddInternalOverlapIssues(map, element, firstClaimants, issues);
                results.Add(new AivElementPlacementResult(element, issues));
            }

            return new ReadOnlyCollection<AivElementPlacementResult>(results.ToArray());
        }

        private static List<AivPlacementIssue> EvaluateMapRules(
            IAivPlacementTileSource map,
            AivProjectedElement element,
            ISet<MapCoordinate> effectiveCoordinates = null)
        {
            var issues = new List<AivPlacementIssue>();
            if (element.Kind == AivProjectedElementKind.AnchorOnly)
            {
                // An unknown footprint cannot safely contribute to a complete castle result.
                issues.Add(new AivPlacementIssue(
                    AivPlacementIssueKind.UnresolvedNativeRule,
                    element.OriginalIndex,
                    element.BuildIndex,
                    element.Mapper.Value,
                    AivProjectedTileKind.ElementAnchor,
                    element.MapCoordinate,
                    null,
                    null));
                return issues;
            }

            foreach (AivProjectedTile tile in element.OccupiedTiles)
            {
                MapCoordinate coordinate = tile.MapCoordinate;
                // Native candidate loading is last-writer-wins before the fit scan.
                if (effectiveCoordinates != null && !effectiveCoordinates.Contains(coordinate))
                    continue;
                if (coordinate.X < 0 || coordinate.X > NativeCoordinateMaximum ||
                    coordinate.Y < 0 || coordinate.Y > NativeCoordinateMaximum)
                {
                    issues.Add(CreateIssue(
                        AivPlacementIssueKind.OutsideMap,
                        element,
                        tile,
                        null,
                        null));
                    continue;
                }

                if (!map.Geometry.TryGetTileId(coordinate.X, coordinate.Y, out int tileId))
                {
                    issues.Add(CreateIssue(
                        AivPlacementIssueKind.InvalidMapTile,
                        element,
                        tile,
                        null,
                        null));
                    continue;
                }

                AivPlacementTileEvidence evidence = map.GetTileEvidence(tileId);
                AivPlacementIssueKind reasons = EvaluateDirectRules(
                    element.Mapper.Value,
                    evidence);
                if (reasons != AivPlacementIssueKind.None)
                {
                    issues.Add(CreateIssue(
                        reasons,
                        element,
                        tile,
                        tileId,
                        evidence));
                }
            }

            return issues;
        }

        private static AivPlacementIssueKind EvaluateDirectRules(
            int mapperValue,
            AivPlacementTileEvidence evidence)
        {
            AivPlacementIssueKind reasons = AivPlacementIssueKind.None;
            int flags = evidence.TerrainFlags;

            if (evidence.Height > NativeMaximumHeight)
                reasons |= AivPlacementIssueKind.HeightMismatch;
            if (evidence.BuildingId != 0)
                reasons |= AivPlacementIssueKind.BuildingOccupied;
            IReadOnlyList<AivTileOccupancy> occupancies =
                evidence.Occupancies ?? Array.Empty<AivTileOccupancy>();
            foreach (AivTileOccupancy occupancy in occupancies)
            {
                if (!occupancy.BlocksPlacement)
                    continue;

                if (occupancy.Kind == AivTileOccupancyKind.PrebuiltAivBuilding ||
                    occupancy.Kind == AivTileOccupancyKind.PrebuiltAivTile)
                {
                    reasons |= AivPlacementIssueKind.PriorAivPrebuiltOccupied;
                }
                else if (evidence.BuildingId == 0)
                {
                    reasons |= AivPlacementIssueKind.BuildingOccupied;
                }
            }

            // TestSpecificCandidate passes player zero, so every existing wall fails ownership.
            if ((flags & IsWall) != 0)
                reasons |= AivPlacementIssueKind.OwnerConflict;

            if (IsTerrainBlocked(mapperValue, flags))
                reasons |= AivPlacementIssueKind.TerrainBlocked;

            // Skirmish initialization sets the native game mode to 1 or 99. With the
            // validator's player zero, that mode accepts every serialized organism class.

            return reasons;
        }

        private static bool IsTerrainBlocked(int mapperValue, int flags)
        {
            bool waterMapper = mapperValue >= 195 && mapperValue <= 198;
            if ((flags & Sea) != 0 && !waterMapper)
                return true;
            if ((flags & IsFarm) != 0)
                return true;
            if ((flags & PitchTrap) != 0 && mapperValue == 99)
                return true;
            if ((flags & MapEdges) != 0)
                return true;
            if ((flags & River) != 0 && !waterMapper)
                return true;
            if ((flags & IsBuildingOrElevated) != 0)
                return true;
            if ((flags & FarmTypeFlags) != 0)
                return true;
            if ((flags & Ford) != 0)
                return true;

            bool bareImpassableEdge =
                (flags & (ImpassableEdge | IsWall)) == ImpassableEdge;
            // Skirmish/player-state initialization clears the general mapper
            // profile exception before the AIV validator checks this bit.
            if (bareImpassableEdge)
                return true;
            if ((flags & IsSwamp) != 0 && mapperValue != 91)
                return true;
            if ((flags & IsMoat) != 0 && mapperValue != 105)
                return true;

            return false;
        }

        private static void AddInternalOverlapIssues(
            IAivPlacementTileSource map,
            AivProjectedElement element,
            IDictionary<MapCoordinate, int> firstClaimants,
            ICollection<AivPlacementIssue> issues)
        {
            var coordinatesSeenInElement = new HashSet<MapCoordinate>();
            foreach (AivProjectedTile tile in element.OccupiedTiles)
            {
                if (!coordinatesSeenInElement.Add(tile.MapCoordinate))
                    continue;

                if (firstClaimants.TryGetValue(
                    tile.MapCoordinate,
                    out int conflictingElementIndex))
                {
                    int? tileId = null;
                    AivPlacementTileEvidence? evidence = null;
                    if (map.Geometry.TryGetTileId(
                        tile.MapCoordinate.X,
                        tile.MapCoordinate.Y,
                        out int validTileId))
                    {
                        tileId = validTileId;
                        evidence = map.GetTileEvidence(validTileId);
                    }

                    issues.Add(new AivPlacementIssue(
                        AivPlacementIssueKind.InternalOverlap,
                        element.OriginalIndex,
                        element.BuildIndex,
                        element.Mapper.Value,
                        tile.Kind,
                        tile.MapCoordinate,
                        tileId,
                        evidence,
                        conflictingElementIndex));
                }
                else
                {
                    firstClaimants.Add(tile.MapCoordinate, element.OriginalIndex);
                }
            }
        }

        private static AivPlacementIssue CreateIssue(
            AivPlacementIssueKind kind,
            AivProjectedElement element,
            AivProjectedTile tile,
            int? tileId,
            AivPlacementTileEvidence? evidence)
        {
            return new AivPlacementIssue(
                kind,
                element.OriginalIndex,
                element.BuildIndex,
                element.Mapper.Value,
                tile.Kind,
                tile.MapCoordinate,
                tileId,
                evidence);
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
}
