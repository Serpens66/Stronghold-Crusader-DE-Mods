using System;
using System.Collections.Generic;
using MapParser.Core;

namespace AIVPlacement.Core
{
    public enum AivElementPlacementStatus
    {
        Placeable,
        Blocked,
        NotEvaluable
    }

    public interface IAivPlacementTileSource
    {
        MapTileGeometry Geometry { get; }
        AivPlacementTileEvidence GetTileEvidence(int tileId);
    }

    public sealed class AivElementPlacementResult
    {
        internal AivElementPlacementResult(
            AivProjectedElement element,
            IReadOnlyList<AivPlacementIssue> issues)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Issues = ProjectionCollections.Copy(issues);
            Status = DetermineStatus(Issues);
        }

        public AivProjectedElement Element { get; }
        public AivElementPlacementStatus Status { get; }
        public IReadOnlyList<AivPlacementIssue> Issues { get; }

        private static AivElementPlacementStatus DetermineStatus(
            IReadOnlyList<AivPlacementIssue> issues)
        {
            bool unresolved = false;
            foreach (AivPlacementIssue issue in issues)
            {
                AivPlacementIssueKind resolvedReasons =
                    issue.Kind & ~(AivPlacementIssueKind.UnresolvedNativeRule |
                        AivPlacementIssueKind.InternalOverlap);
                if (resolvedReasons != AivPlacementIssueKind.None)
                    return AivElementPlacementStatus.Blocked;
                if (issue.Kind.HasFlag(AivPlacementIssueKind.UnresolvedNativeRule))
                    unresolved = true;
            }

            return unresolved
                ? AivElementPlacementStatus.NotEvaluable
                : AivElementPlacementStatus.Placeable;
        }
    }
}
