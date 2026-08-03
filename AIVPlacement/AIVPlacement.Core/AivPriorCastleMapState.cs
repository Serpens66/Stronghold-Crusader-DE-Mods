using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AIVParser.Core;
using MapParser.Core;

namespace AIVPlacement.Core
{
    public sealed class AivPriorCastleMapState : IAivPlacementTileSource
    {
        private const int DrawbridgeMapper = 105;

        private readonly IAivPlacementTileSource source;
        private readonly Dictionary<int, IReadOnlyList<AivPlannedTileOccupancy>> plannedClaimsByTileId;

        public AivPriorCastleMapState(
            IAivPlacementTileSource source,
            IEnumerable<AivPriorPlacement> priorPlacements)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            if (priorPlacements == null)
                throw new ArgumentNullException(nameof(priorPlacements));

            var mutableClaims = new Dictionary<int, List<AivPlannedTileOccupancy>>();
            string sessionId = null;
            foreach (AivPriorPlacement priorPlacement in priorPlacements)
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
                    // A blocked element is omitted as a whole from the temporary
                    // native AIV state used to test the next AI.
                    if (elementResult.Status != AivElementPlacementStatus.Placeable ||
                        element.Mapper.Value == DrawbridgeMapper)
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
                                out List<AivPlannedTileOccupancy> claims))
                        {
                            claims = new List<AivPlannedTileOccupancy>();
                            mutableClaims.Add(tileId, claims);
                        }

                        claims.Add(new AivPlannedTileOccupancy(
                            priorPlacement.SessionId,
                            priorPlacement.PlayerId,
                            element.Mapper.Value,
                            element.Mapper.Category,
                            element.OriginalIndex,
                            element.BuildIndex));
                    }
                }
            }

            plannedClaimsByTileId = new Dictionary<int, IReadOnlyList<AivPlannedTileOccupancy>>();
            foreach (KeyValuePair<int, List<AivPlannedTileOccupancy>> pair in mutableClaims)
            {
                plannedClaimsByTileId.Add(
                    pair.Key,
                    new ReadOnlyCollection<AivPlannedTileOccupancy>(pair.Value.ToArray()));
            }
            SessionId = sessionId ?? string.Empty;
        }

        public MapTileGeometry Geometry => source.Geometry;
        public string SessionId { get; }

        public AivPlacementTileEvidence GetTileEvidence(int tileId)
        {
            AivPlacementTileEvidence evidence = source.GetTileEvidence(tileId);
            if (!plannedClaimsByTileId.TryGetValue(
                    tileId,
                    out IReadOnlyList<AivPlannedTileOccupancy> localClaims))
            {
                return evidence;
            }

            var combinedClaims = new List<AivPlannedTileOccupancy>();
            if (evidence.PlannedOccupancies != null)
                combinedClaims.AddRange(evidence.PlannedOccupancies);
            combinedClaims.AddRange(localClaims);

            // Do not invent a persistent BuildingId for native temporary AIV state.
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

    public sealed class AivPriorPlacement
    {
        public AivPriorPlacement(
            string sessionId,
            int playerId,
            AivPlacementResult placement)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                throw new ArgumentException("A prior placement needs a session ID.", nameof(sessionId));
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
