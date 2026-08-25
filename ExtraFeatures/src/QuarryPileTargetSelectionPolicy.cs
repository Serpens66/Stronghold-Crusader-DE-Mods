// Deterministic selection of the valid quarry-pile position nearest to an AI Keep.
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal readonly struct QuarryPileTargetCandidate
    {
        public QuarryPileTargetCandidate(
            int x,
            int y,
            int placementTry,
            int candidateIndex,
            bool isCurrentPosition)
        {
            X = x;
            Y = y;
            PlacementTry = placementTry;
            CandidateIndex = candidateIndex;
            IsCurrentPosition = isCurrentPosition;
        }

        public int X { get; }
        public int Y { get; }
        public int PlacementTry { get; }
        public int CandidateIndex { get; }
        public bool IsCurrentPosition { get; }
    }

    internal static class QuarryPileTargetSelectionPolicy
    {
        public static bool TrySelectNearestAtPlacementTry(
            IReadOnlyList<QuarryPileTargetCandidate> candidates,
            int requiredPlacementTry,
            int keepCenterXTimesTwo,
            int keepCenterYTimesTwo,
            out QuarryPileTargetCandidate selected)
        {
            selected = default;
            if (candidates == null || candidates.Count == 0)
                return false;

            bool found = false;
            long bestDistanceSquaredTimesFour = long.MaxValue;
            for (int index = 0; index < candidates.Count; index++)
            {
                QuarryPileTargetCandidate candidate = candidates[index];
                if (candidate.PlacementTry != requiredPlacementTry)
                    continue;

                long dxTimesTwo = candidate.X * 2L - keepCenterXTimesTwo;
                long dyTimesTwo = candidate.Y * 2L - keepCenterYTimesTwo;
                long distanceSquaredTimesFour =
                    dxTimesTwo * dxTimesTwo + dyTimesTwo * dyTimesTwo;

                if (found && distanceSquaredTimesFour > bestDistanceSquaredTimesFour)
                    continue;

                if (found && distanceSquaredTimesFour == bestDistanceSquaredTimesFour &&
                    CompareVanillaOrder(candidate, selected) >= 0)
                {
                    continue;
                }

                selected = candidate;
                bestDistanceSquaredTimesFour = distanceSquaredTimesFour;
                found = true;
            }

            return found;
        }

        private static int CompareVanillaOrder(
            QuarryPileTargetCandidate left,
            QuarryPileTargetCandidate right)
        {
            int placementTryComparison = left.PlacementTry.CompareTo(right.PlacementTry);
            return placementTryComparison != 0
                ? placementTryComparison
                : left.CandidateIndex.CompareTo(right.CandidateIndex);
        }
    }
}
