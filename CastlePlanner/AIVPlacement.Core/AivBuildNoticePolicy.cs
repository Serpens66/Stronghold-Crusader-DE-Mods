using System.Collections.Generic;
using AIVPlacement.Core;

namespace CastlePlanner.AIVPlacement.Core
{
    public static class AivBuildNoticePolicy
    {
        public static IReadOnlyList<int> GetHighMapRotations(
            bool aiHeightPatchProvenDisabled,
            IReadOnlyList<int> elevatedTilesByRotation)
        {
            if (!aiHeightPatchProvenDisabled || elevatedTilesByRotation == null)
                return System.Array.Empty<int>();
            var affected = new List<int>();
            for (int index = 0; index < elevatedTilesByRotation.Count; index++)
                if (elevatedTilesByRotation[index] > 0)
                    affected.Add(index);
            return affected;
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
