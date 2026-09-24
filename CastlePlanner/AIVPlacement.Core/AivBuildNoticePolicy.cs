using System.Collections.Generic;
using AIVPlacement.Core;

namespace CastlePlanner.AIVPlacement.Core
{
    public static class AivBuildNoticePolicy
    {
        public static bool HasProvenHighBuildExposure(
            int candidateId,
            AivPlacementRotationSelection selection,
            NativeAivAutoDecision autoDecision,
            bool aiHeightPatchProvenDisabled,
            IReadOnlyList<bool> buildTimeHeightProvenByRotation,
            IReadOnlyList<int> elevatedTilesByRotation)
        {
            if (!aiHeightPatchProvenDisabled || selection == null ||
                buildTimeHeightProvenByRotation == null || elevatedTilesByRotation == null ||
                autoDecision?.IsCertain != true ||
                autoDecision.CandidateId != candidateId)
                return false;
            int rotation = autoDecision.RotationIndex;
            return rotation >= 0 && rotation < selection.Variants.Count &&
                rotation < buildTimeHeightProvenByRotation.Count &&
                rotation < elevatedTilesByRotation.Count &&
                buildTimeHeightProvenByRotation[rotation] &&
                elevatedTilesByRotation[rotation] > 0 &&
                selection.Variants[rotation].Status != AivPlacementStatus.Impossible &&
                selection.Variants[rotation].Status != AivPlacementStatus.NotEvaluable;
        }

        public static bool HasPossiblePriorKeepContact(
            int candidateId,
            IReadOnlyList<NativeAivAutoDecision> possibleAuto,
            IReadOnlyList<bool> contactByRotation)
        {
            if (possibleAuto == null || contactByRotation == null)
                return false;
            foreach (NativeAivAutoDecision outcome in possibleAuto)
                if (outcome.CandidateId == candidateId &&
                    outcome.RotationIndex >= 0 &&
                    outcome.RotationIndex < contactByRotation.Count &&
                    contactByRotation[outcome.RotationIndex])
                    return true;
            return false;
        }

        public static bool HasPlannedCoreOverlap(
            IReadOnlyList<int> first,
            IReadOnlyList<int> second)
        {
            if (first == null || second == null || first.Count == 0 || second.Count == 0)
                return false;
            var tiles = new HashSet<int>(first);
            foreach (int tile in second)
                if (tiles.Contains(tile))
                    return true;
            return false;
        }
    }
}
