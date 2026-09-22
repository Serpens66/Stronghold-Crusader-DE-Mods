namespace Shared
{
    internal enum GroundMovePreviewRejection
    {
        None = 0,
        OutsideMap,
        CursorOutsideGame,
        CursorSnapshotMismatch,
        UnderCursorUnit,
        HoveredUnit,
        TileOccupiedByUnit,
        HoveredBuilding,
        HoveredWall,
        TileOccupiedByBuilding,
        TargetUnavailable,
        MissingPathComponent
    }

    internal readonly struct GroundMovePreviewSnapshot
    {
        internal GroundMovePreviewSnapshot(
            bool insideMap,
            bool cursorInGame,
            bool cursorSnapshotMatches,
            int underCursorUnitCount,
            int hoveredUnitId,
            int tileUnitId,
            int hoveredBuildingId,
            bool hoveringWall,
            int tileBuildingId,
            bool targetAvailable,
            bool hasPathComponent)
        {
            InsideMap = insideMap;
            CursorInGame = cursorInGame;
            CursorSnapshotMatches = cursorSnapshotMatches;
            UnderCursorUnitCount = underCursorUnitCount;
            HoveredUnitId = hoveredUnitId;
            TileUnitId = tileUnitId;
            HoveredBuildingId = hoveredBuildingId;
            HoveringWall = hoveringWall;
            TileBuildingId = tileBuildingId;
            TargetAvailable = targetAvailable;
            HasPathComponent = hasPathComponent;
        }

        internal bool InsideMap { get; }
        internal bool CursorInGame { get; }
        internal bool CursorSnapshotMatches { get; }
        internal int UnderCursorUnitCount { get; }
        internal int HoveredUnitId { get; }
        internal int TileUnitId { get; }
        internal int HoveredBuildingId { get; }
        internal bool HoveringWall { get; }
        internal int TileBuildingId { get; }
        internal bool TargetAvailable { get; }
        internal bool HasPathComponent { get; }
    }

    internal static class GroundMovePreviewEligibility
    {
        internal static GroundMovePreviewRejection EvaluateInitial(
            GroundMovePreviewSnapshot snapshot)
        {
            GroundMovePreviewRejection fixedTarget = EvaluateFixedTarget(
                snapshot.InsideMap,
                snapshot.TileUnitId,
                snapshot.TileBuildingId,
                snapshot.TargetAvailable,
                snapshot.HasPathComponent);
            if (fixedTarget != GroundMovePreviewRejection.None)
                return fixedTarget;
            if (!snapshot.CursorInGame)
                return GroundMovePreviewRejection.CursorOutsideGame;
            if (!snapshot.CursorSnapshotMatches)
                return GroundMovePreviewRejection.CursorSnapshotMismatch;
            if (snapshot.UnderCursorUnitCount > 0)
                return GroundMovePreviewRejection.UnderCursorUnit;
            if (snapshot.HoveredUnitId > 0)
                return GroundMovePreviewRejection.HoveredUnit;
            if (snapshot.HoveredBuildingId > 0)
                return GroundMovePreviewRejection.HoveredBuilding;
            if (snapshot.HoveringWall)
                return GroundMovePreviewRejection.HoveredWall;
            return GroundMovePreviewRejection.None;
        }

        internal static GroundMovePreviewRejection EvaluateFixedTarget(
            bool insideMap,
            int tileUnitId,
            int tileBuildingId,
            bool targetAvailable,
            bool hasPathComponent)
        {
            if (!insideMap)
                return GroundMovePreviewRejection.OutsideMap;
            if (tileUnitId > 0)
                return GroundMovePreviewRejection.TileOccupiedByUnit;
            if (tileBuildingId > 0)
                return GroundMovePreviewRejection.TileOccupiedByBuilding;
            if (!targetAvailable)
                return GroundMovePreviewRejection.TargetUnavailable;
            if (!hasPathComponent)
                return GroundMovePreviewRejection.MissingPathComponent;
            return GroundMovePreviewRejection.None;
        }
    }
}
