using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AIVParser.Core;
using MapParser.Core;

namespace AIVPlacement.Core
{
    public sealed class AivProjectedPrebuildMapState : IAivPlacementTileSource
    {
        private readonly IAivPlacementTileSource source;
        private readonly Dictionary<int, IReadOnlyList<AivTileOccupancy>> occupanciesByTileId;

        public AivProjectedPrebuildMapState(
            IAivPlacementTileSource source,
            IEnumerable<AivProjectedPrebuildPlacement> priorPlacements)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            if (priorPlacements == null)
                throw new ArgumentNullException(nameof(priorPlacements));

            var mutableClaims = new Dictionary<int, List<AivTileOccupancy>>();
            string sessionId = null;
            foreach (AivProjectedPrebuildPlacement priorPlacement in priorPlacements)
            {
                if (priorPlacement == null || priorPlacement.Placement == null)
                    continue;
                if (sessionId == null)
                {
                    sessionId = priorPlacement.SessionId;
                }
                else if (!string.Equals(
                             sessionId,
                             priorPlacement.SessionId,
                             StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "Prior AIV placements from different map sessions cannot share one state.",
                        nameof(priorPlacements));
                }

                foreach (AivElementPlacementResult elementResult in priorPlacement.Placement.ElementResults)
                {
                    AivProjectedElement element = elementResult.Element;
                    // PreBuild executes only the prepared, placeable part of a partial castle.
                    if (elementResult.Status != AivElementPlacementStatus.Placeable ||
                        element.Mapper.Category == AivItemCategory.Keep)
                    {
                        continue;
                    }

                    foreach (AivProjectedTile tile in element.OccupiedTiles)
                    {
                        // Associated reservation areas are not temporary live occupancy.
                        if (tile.Kind != AivProjectedTileKind.CoreFootprint ||
                            !Geometry.TryGetTileId(
                                tile.MapCoordinate.X,
                                tile.MapCoordinate.Y,
                                out int tileId))
                        {
                            continue;
                        }

                        if (!mutableClaims.TryGetValue(
                                tileId,
                                out List<AivTileOccupancy> claims))
                        {
                            claims = new List<AivTileOccupancy>();
                            mutableClaims.Add(tileId, claims);
                        }

                        bool isBuilding = element.Mapper.Category == AivItemCategory.Building;
                        claims.Add(new AivTileOccupancy(
                            isBuilding
                                ? AivTileOccupancyKind.ProjectedPrebuiltAivBuilding
                                : AivTileOccupancyKind.ProjectedPrebuiltAivTile,
                            priorPlacement.SessionId,
                            priorPlacement.PlayerId,
                            0,
                            0,
                            element.Mapper.Value,
                            element.Mapper.Category,
                            element.OriginalIndex,
                            element.BuildIndex,
                            true));
                    }
                }
            }

            occupanciesByTileId = new Dictionary<int, IReadOnlyList<AivTileOccupancy>>();
            foreach (KeyValuePair<int, List<AivTileOccupancy>> pair in mutableClaims)
            {
                occupanciesByTileId.Add(
                    pair.Key,
                    new ReadOnlyCollection<AivTileOccupancy>(pair.Value.ToArray()));
            }
            SessionId = sessionId ?? string.Empty;
        }

        public MapTileGeometry Geometry => source.Geometry;
        public string SessionId { get; }

        public AivPlacementTileEvidence GetTileEvidence(int tileId)
        {
            AivPlacementTileEvidence evidence = source.GetTileEvidence(tileId);
            if (!occupanciesByTileId.TryGetValue(
                    tileId,
                    out IReadOnlyList<AivTileOccupancy> localClaims))
            {
                return evidence;
            }

            var combinedClaims = new List<AivTileOccupancy>();
            if (evidence.Occupancies != null)
                combinedClaims.AddRange(evidence.Occupancies);
            combinedClaims.AddRange(localClaims);

            // The provenance is explicit; never invent a map BuildingId for simulated runtime state.
            return new AivPlacementTileEvidence(
                evidence.TerrainFlags,
                evidence.SecondaryLogic,
                evidence.Height,
                evidence.DefaultHeight,
                evidence.OrganismId,
                evidence.BuildingId,
                evidence.EntityId,
                evidence.OwnerId,
                combinedClaims);
        }
    }

    public sealed class AivProjectedPrebuildPlacement
    {
        public AivProjectedPrebuildPlacement(
            string sessionId,
            int playerId,
            AivPlacementResult placement)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A projected prebuild needs a session ID.", nameof(sessionId));
            if (playerId <= 0)
                throw new ArgumentOutOfRangeException(nameof(playerId));

            SessionId = sessionId;
            PlayerId = playerId;
            Placement = placement ?? throw new ArgumentNullException(nameof(placement));
        }

        public string SessionId { get; }
        public int PlayerId { get; }
        public AivPlacementResult Placement { get; }
    }
}
