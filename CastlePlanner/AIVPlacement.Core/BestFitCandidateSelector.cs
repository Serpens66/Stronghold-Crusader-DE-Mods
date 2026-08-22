using System;
using System.Collections.Generic;
using AIVPlacement.Core;

namespace CastlePlanner.AIVPlacement.Core
{
    public static class BestFitCandidateSelector
    {
        public static IReadOnlyList<int> GetEligibleCandidateIds(AivPlacementCheckResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));

            var eligible = new List<int>();
            foreach (AivPlacementCandidateEvaluation candidate in result.Candidates)
            {
                if (candidate.Selection?.CompleteVariants.Count > 0)
                    eligible.Add(candidate.CandidateId);
            }
            if (eligible.Count > 0)
                return eligible;

            int? highestScore = null;
            foreach (AivPlacementCandidateEvaluation candidate in result.Candidates)
            {
                if (candidate?.Selection == null)
                    continue;
                foreach (AivPlacementResult variant in candidate.Selection.PartialVariants)
                {
                    int score = variant.Score.SequentialBuildScore;
                    if (!highestScore.HasValue || score > highestScore.Value)
                        highestScore = score;
                }
            }

            if (highestScore.HasValue)
            {
                foreach (AivPlacementCandidateEvaluation candidate in result.Candidates)
                {
                    if (HasPartialScore(candidate, highestScore.Value))
                        eligible.Add(candidate.CandidateId);
                }
                return eligible;
            }

            foreach (AivPlacementCandidateEvaluation candidate in result.Candidates)
            {
                if (candidate.Status == AivPlacementStatus.Impossible)
                    eligible.Add(candidate.CandidateId);
            }

            return eligible;
        }

        private static bool HasPartialScore(
            AivPlacementCandidateEvaluation candidate,
            int highestScore)
        {
            if (candidate?.Selection == null)
                return false;

            foreach (AivPlacementResult variant in candidate.Selection.PartialVariants)
            {
                // The native raw build score decides; percentages do not break exact score ties.
                if (variant.Score.SequentialBuildScore == highestScore)
                    return true;
            }
            return false;
        }
    }
}
