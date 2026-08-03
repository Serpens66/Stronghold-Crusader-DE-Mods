using AIVParser.Core;
using AIVPlacement.Core;
using MapParser.Core;

namespace AIVPlacement.OracleComparison;

public static class OfflineCaseDiagnosticBuilder
{
    public static OfflineCaseDiagnostic Build(
        IAivPlacementTileSource map,
        AivPlacementResult result)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(result);

        var elements = new List<OfflineElementDiagnostic>(result.ElementResults.Count);
        foreach (AivElementPlacementResult elementResult in result.ElementResults)
        {
            AivProjectedElement element = elementResult.Element;
            var issueKindsByCoordinate = new Dictionary<MapCoordinate, AivPlacementIssueKind>();
            foreach (AivPlacementIssue issue in elementResult.Issues)
            {
                issueKindsByCoordinate.TryGetValue(
                    issue.MapCoordinate,
                    out AivPlacementIssueKind existing);
                issueKindsByCoordinate[issue.MapCoordinate] = existing | issue.Kind;
            }

            var cells = new List<OfflineCellDiagnostic>(element.OccupiedTiles.Count);
            var reasonCounts = new Dictionary<AivPlacementIssueKind, int>();
            for (int cellIndex = 0; cellIndex < element.OccupiedTiles.Count; cellIndex++)
            {
                AivProjectedTile tile = element.OccupiedTiles[cellIndex];
                issueKindsByCoordinate.TryGetValue(
                    tile.MapCoordinate,
                    out AivPlacementIssueKind issueKind);
                bool blocked =
                    (issueKind & ~AivPlacementIssueKind.UnresolvedNativeRule) != 0;

                int? tileId = null;
                AivPlacementTileEvidence? evidence = null;
                AivPlacementTileEvidence? originalEvidence = null;
                AivStartBuildingAdjacency? startAdjacency = null;
                if (map.Geometry.TryGetTileId(
                    tile.MapCoordinate.X,
                    tile.MapCoordinate.Y,
                    out int validTileId))
                {
                    tileId = validTileId;
                    evidence = map.GetTileEvidence(validTileId);
                    originalEvidence = map is AivPreplacementMapState preplacementMap
                        ? preplacementMap.GetOriginalTileEvidence(validTileId)
                        : evidence;
                    if (map is AivPreplacementMapState normalizedMap)
                    {
                        startAdjacency = normalizedMap.GetStartBuildingAdjacency(
                            validTileId);
                    }
                }

                if (issueKind != AivPlacementIssueKind.None)
                {
                    foreach (AivPlacementIssueKind reason in EnumerateReasons(issueKind))
                    {
                        reasonCounts.TryGetValue(reason, out int count);
                        reasonCounts[reason] = count + 1;
                    }
                }

                cells.Add(new OfflineCellDiagnostic
                {
                    CellIndex = cellIndex,
                    TileKind = tile.Kind,
                    AssociatedAreaName = tile.AssociatedAreaName,
                    AssociatedAreaKind = tile.AssociatedAreaKind,
                    AssociatedAreaSource = tile.AssociatedAreaSource,
                    SourceAivRow = tile.SourceAivCoordinate.Row,
                    SourceAivColumn = tile.SourceAivCoordinate.Column,
                    RotatedAivRow = tile.RotatedAivCoordinate.Row,
                    RotatedAivColumn = tile.RotatedAivCoordinate.Column,
                    MapX = tile.MapCoordinate.X,
                    MapY = tile.MapCoordinate.Y,
                    TileId = tileId,
                    Blocked = blocked,
                    IssueKind = issueKind,
                    TerrainFlags = evidence?.TerrainFlags,
                    SecondaryLogic = evidence?.SecondaryLogic,
                    Height = evidence?.Height,
                    DefaultHeight = evidence?.DefaultHeight,
                    OrganismId = evidence?.OrganismId,
                    BuildingId = evidence?.BuildingId,
                    EntityId = evidence?.EntityId,
                    OwnerId = evidence?.OwnerId,
                    PlannedOccupancies = ToPlannedOccupancies(evidence),
                    WasPreplacementNormalized = EvidenceDiffers(evidence, originalEvidence),
                    OriginalTerrainFlags = originalEvidence?.TerrainFlags,
                    OriginalSecondaryLogic = originalEvidence?.SecondaryLogic,
                    OriginalHeight = originalEvidence?.Height,
                    OriginalDefaultHeight = originalEvidence?.DefaultHeight,
                    OriginalOrganismId = originalEvidence?.OrganismId,
                    OriginalBuildingId = originalEvidence?.BuildingId,
                    OriginalEntityId = originalEvidence?.EntityId,
                    OriginalOwnerId = originalEvidence?.OwnerId,
                    OriginalPlannedOccupancies = ToPlannedOccupancies(originalEvidence),
                    OrthogonalStartBuildingNeighborCount =
                        startAdjacency?.OrthogonalNeighborCount,
                    DiagonalStartBuildingNeighborCount =
                        startAdjacency?.DiagonalNeighborCount
                });
            }

            elements.Add(new OfflineElementDiagnostic
            {
                ElementIndex = element.OriginalIndex,
                BuildIndex = element.BuildIndex,
                PositionIndex = element.PositionIndex,
                MapperValue = element.Mapper.Value,
                Rotation = element.Rotation,
                ElementKind = element.Kind,
                Status = elementResult.Status,
                SourceAivRow = element.AivCoordinate.Row,
                SourceAivColumn = element.AivCoordinate.Column,
                RotatedAivRow = element.RotatedAivCoordinate.Row,
                RotatedAivColumn = element.RotatedAivCoordinate.Column,
                MapX = element.MapCoordinate.X,
                MapY = element.MapCoordinate.Y,
                EvaluatedCellCount = cells.Count,
                BlockedCellCount = cells.Count(cell => cell.Blocked),
                CoreCellCount = cells.Count(cell =>
                    cell.TileKind == AivProjectedTileKind.CoreFootprint),
                AssociatedCellCount = cells.Count(cell =>
                    cell.TileKind == AivProjectedTileKind.AssociatedBlockedArea),
                IssueCount = elementResult.Issues.Count,
                ReasonCounts = reasonCounts
                    .OrderBy(pair => pair.Key)
                    .Select(pair => new OfflineReasonDiagnostic
                    {
                        Kind = pair.Key,
                        CellCount = pair.Value
                    })
                    .ToList(),
                Cells = cells
            });
        }

        return new OfflineCaseDiagnostic
        {
            Rotation = result.Rotation,
            EvaluatedCellCount = elements.Sum(element => element.EvaluatedCellCount),
            BlockedCellCount = elements.Sum(element => element.BlockedCellCount),
            Elements = elements
        };
    }

    private static IEnumerable<AivPlacementIssueKind> EnumerateReasons(
        AivPlacementIssueKind combined)
    {
        foreach (AivPlacementIssueKind value in Enum.GetValues<AivPlacementIssueKind>())
        {
            if (value != AivPlacementIssueKind.None && combined.HasFlag(value))
                yield return value;
        }
    }

    private static bool EvidenceDiffers(
        AivPlacementTileEvidence? effective,
        AivPlacementTileEvidence? original)
    {
        if (!effective.HasValue || !original.HasValue)
            return effective.HasValue != original.HasValue;

        AivPlacementTileEvidence left = effective.Value;
        AivPlacementTileEvidence right = original.Value;
        IReadOnlyList<AivPlannedTileOccupancy> leftPlans =
            left.PlannedOccupancies ?? Array.Empty<AivPlannedTileOccupancy>();
        IReadOnlyList<AivPlannedTileOccupancy> rightPlans =
            right.PlannedOccupancies ?? Array.Empty<AivPlannedTileOccupancy>();
        return left.TerrainFlags != right.TerrainFlags ||
            left.SecondaryLogic != right.SecondaryLogic ||
            left.Height != right.Height ||
            left.DefaultHeight != right.DefaultHeight ||
            left.OrganismId != right.OrganismId ||
            left.BuildingId != right.BuildingId ||
            left.EntityId != right.EntityId ||
            left.OwnerId != right.OwnerId ||
            !leftPlans.SequenceEqual(rightPlans);
    }

    private static List<OfflinePlannedOccupancyDiagnostic> ToPlannedOccupancies(
        AivPlacementTileEvidence? evidence)
    {
        if (!evidence.HasValue || evidence.Value.PlannedOccupancies == null)
            return new List<OfflinePlannedOccupancyDiagnostic>();

        return evidence.Value.PlannedOccupancies
            .Select(item => new OfflinePlannedOccupancyDiagnostic
            {
                SessionId = item.SessionId,
                PlayerId = item.PlayerId,
                MapperValue = item.MapperValue,
                Category = item.Category,
                ElementIndex = item.ElementIndex,
                BuildIndex = item.BuildIndex
            })
            .ToList();
    }
}

public sealed class OfflineCaseDiagnostic
{
    public AivRotation Rotation { get; set; }
    public int EvaluatedCellCount { get; set; }
    public int BlockedCellCount { get; set; }
    public List<OfflineElementDiagnostic> Elements { get; set; } = new();
}

public sealed class OfflineElementDiagnostic
{
    public int ElementIndex { get; set; }
    public int BuildIndex { get; set; }
    public int PositionIndex { get; set; }
    public int MapperValue { get; set; }
    public AivRotation Rotation { get; set; }
    public AivProjectedElementKind ElementKind { get; set; }
    public AivElementPlacementStatus Status { get; set; }
    public int SourceAivRow { get; set; }
    public int SourceAivColumn { get; set; }
    public int RotatedAivRow { get; set; }
    public int RotatedAivColumn { get; set; }
    public int MapX { get; set; }
    public int MapY { get; set; }
    public int EvaluatedCellCount { get; set; }
    public int BlockedCellCount { get; set; }
    public int CoreCellCount { get; set; }
    public int AssociatedCellCount { get; set; }
    public int IssueCount { get; set; }
    public List<OfflineReasonDiagnostic> ReasonCounts { get; set; } = new();
    public List<OfflineCellDiagnostic> Cells { get; set; } = new();
}

public sealed class OfflineReasonDiagnostic
{
    public AivPlacementIssueKind Kind { get; set; }
    public int CellCount { get; set; }
}

public sealed class OfflineCellDiagnostic
{
    public int CellIndex { get; set; }
    public AivProjectedTileKind TileKind { get; set; }
    public string AssociatedAreaName { get; set; } = string.Empty;
    public AivBlockedAreaKind? AssociatedAreaKind { get; set; }
    public AivBlockedAreaSource? AssociatedAreaSource { get; set; }
    public int SourceAivRow { get; set; }
    public int SourceAivColumn { get; set; }
    public int RotatedAivRow { get; set; }
    public int RotatedAivColumn { get; set; }
    public int MapX { get; set; }
    public int MapY { get; set; }
    public int? TileId { get; set; }
    public bool Blocked { get; set; }
    public AivPlacementIssueKind IssueKind { get; set; }
    public int? TerrainFlags { get; set; }
    public byte? SecondaryLogic { get; set; }
    public byte? Height { get; set; }
    public byte? DefaultHeight { get; set; }
    public ushort? OrganismId { get; set; }
    public ushort? BuildingId { get; set; }
    public ushort? EntityId { get; set; }
    public byte? OwnerId { get; set; }
    public List<OfflinePlannedOccupancyDiagnostic> PlannedOccupancies { get; set; } = new();
    public bool WasPreplacementNormalized { get; set; }
    public int? OriginalTerrainFlags { get; set; }
    public byte? OriginalSecondaryLogic { get; set; }
    public byte? OriginalHeight { get; set; }
    public byte? OriginalDefaultHeight { get; set; }
    public ushort? OriginalOrganismId { get; set; }
    public ushort? OriginalBuildingId { get; set; }
    public ushort? OriginalEntityId { get; set; }
    public byte? OriginalOwnerId { get; set; }
    public List<OfflinePlannedOccupancyDiagnostic> OriginalPlannedOccupancies { get; set; } = new();
    public int? OrthogonalStartBuildingNeighborCount { get; set; }
    public int? DiagonalStartBuildingNeighborCount { get; set; }
}

public sealed class OfflinePlannedOccupancyDiagnostic
{
    public string SessionId { get; set; } = string.Empty;
    public int PlayerId { get; set; }
    public int MapperValue { get; set; }
    public AivItemCategory Category { get; set; }
    public int ElementIndex { get; set; }
    public int BuildIndex { get; set; }
}
