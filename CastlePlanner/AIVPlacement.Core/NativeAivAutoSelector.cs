using System;
using System.Collections.Generic;
using AIVParser.Core;
using AIVPlacement.Core;

namespace CastlePlanner.AIVPlacement.Core
{
    public sealed class NativeAivAutoFit
    {
        public NativeAivAutoFit(AivPlacementStatus status, int sequentialScore, int fitPercentage)
        {
            Status = status;
            SequentialScore = sequentialScore;
            FitPercentage = fitPercentage;
        }

        public AivPlacementStatus Status { get; }
        public int SequentialScore { get; }
        public int FitPercentage { get; }
    }

    public sealed class NativeAivAutoCandidate
    {
        public NativeAivAutoCandidate(int candidateId, IReadOnlyList<NativeAivAutoFit> rotations)
        {
            CandidateId = candidateId;
            Rotations = rotations ?? throw new ArgumentNullException(nameof(rotations));
            if (rotations.Count != 4)
                throw new ArgumentException("Four rotations in native order are required.", nameof(rotations));
        }

        public int CandidateId { get; }
        public IReadOnlyList<NativeAivAutoFit> Rotations { get; }
    }

    public sealed class NativeAivAutoDecision
    {
        internal NativeAivAutoDecision(
            bool isCertain,
            int? candidateId,
            int rotationIndex,
            AivPlacementStatus status)
        {
            IsCertain = isCertain;
            CandidateId = candidateId;
            RotationIndex = rotationIndex;
            Status = status;
        }

        public bool IsCertain { get; }
        public int? CandidateId { get; }
        public int RotationIndex { get; }
        public AivPlacementStatus Status { get; }
    }

    /// <summary>
    /// Replays RVA 0x54F60 for every possible RNG starting index and both
    /// possible values of the caller's try-other-rotations flag. Only a shared
    /// result is safe to publish before the native RNG state is known.
    /// </summary>
    public static class NativeAivAutoSelector
    {
        public static NativeAivAutoDecision SelectCertain(
            IReadOnlyList<AivPlacementCandidateEvaluation> candidates)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            var inputs = new List<NativeAivAutoCandidate>(candidates.Count);
            foreach (AivPlacementCandidateEvaluation candidate in candidates)
            {
                if (candidate?.Selection == null || candidate.Selection.Variants.Count != 4)
                    return Unknown();
                var fits = new List<NativeAivAutoFit>(4);
                foreach (AivPlacementResult variant in candidate.Selection.Variants)
                {
                    fits.Add(new NativeAivAutoFit(
                        variant.Status,
                        variant.Score.SequentialBuildScore,
                        variant.Score.FitPercentage));
                }
                inputs.Add(new NativeAivAutoCandidate(candidate.CandidateId, fits));
            }
            return SelectCertain(inputs);
        }

        public static NativeAivAutoDecision SelectCertain(
            IReadOnlyList<NativeAivAutoCandidate> candidates)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));
            if (candidates.Count == 0)
                return Unknown();

            foreach (NativeAivAutoCandidate candidate in candidates)
            {
                if (candidate == null)
                    return Unknown();
                foreach (NativeAivAutoFit fit in candidate.Rotations)
                {
                    if (fit == null || fit.Status == AivPlacementStatus.NotEvaluable)
                        return Unknown();
                }
            }

            NativeAivAutoDecision first = null;
            for (int start = 0; start < candidates.Count; start++)
            {
                for (int tryOther = 0; tryOther < 2; tryOther++)
                {
                    NativeAivAutoDecision outcome = Replay(
                        candidates,
                        start,
                        tryOther != 0);
                    if (first == null)
                    {
                        first = outcome;
                    }
                    else if (first.Status != outcome.Status ||
                             first.CandidateId != outcome.CandidateId ||
                             first.RotationIndex != outcome.RotationIndex)
                    {
                        return Unknown();
                    }
                }
            }

            return first;
        }

        private static NativeAivAutoDecision Replay(
            IReadOnlyList<NativeAivAutoCandidate> candidates,
            int randomStart,
            bool tryOtherRotations)
        {
            int bestPercentage = 0;
            int? bestPercentageId = null;
            int firstAbove95Id = -1;
            int bestSequential = 0;
            int? bestSequentialId = null;

            // Native increments the random index before its first evaluation.
            for (int step = 1; step <= candidates.Count; step++)
            {
                NativeAivAutoCandidate candidate = candidates[(randomStart + step) % candidates.Count];
                NativeAivAutoFit fit = candidate.Rotations[0];
                if (fit.SequentialScore <= 0)
                    continue;
                if (fit.Status == AivPlacementStatus.Complete)
                    return Selected(candidate.CandidateId, 0, AivPlacementStatus.Complete);
                if (fit.FitPercentage > bestPercentage)
                {
                    bestPercentage = fit.FitPercentage;
                    bestPercentageId = candidate.CandidateId;
                }
                if (firstAbove95Id < 0 && fit.FitPercentage > 95)
                    firstAbove95Id = candidate.CandidateId;
                if (fit.SequentialScore > bestSequential)
                {
                    bestSequential = fit.SequentialScore;
                    bestSequentialId = candidate.CandidateId;
                }
            }

            int bestAlternativeRotation = 0;
            if (tryOtherRotations)
            {
                for (int rotation = 1; rotation < 4; rotation++)
                {
                    for (int step = 1; step <= candidates.Count; step++)
                    {
                        NativeAivAutoCandidate candidate = candidates[(randomStart + step) % candidates.Count];
                        NativeAivAutoFit fit = candidate.Rotations[rotation];
                        if (fit.SequentialScore <= 0)
                            continue;
                        if (fit.Status == AivPlacementStatus.Complete)
                            return Selected(candidate.CandidateId, rotation, AivPlacementStatus.Complete);
                        if (!bestSequentialId.HasValue && fit.FitPercentage > bestPercentage)
                        {
                            bestPercentage = fit.FitPercentage;
                            bestPercentageId = candidate.CandidateId;
                            bestAlternativeRotation = rotation;
                        }
                    }
                }
            }

            if (!bestSequentialId.HasValue)
            {
                return bestPercentageId.HasValue && bestPercentage >= 86
                    ? Selected(bestPercentageId.Value, bestAlternativeRotation, AivPlacementStatus.Partial)
                    : new NativeAivAutoDecision(true, null, -1, AivPlacementStatus.Impossible);
            }

            int selectedId = firstAbove95Id >= 0
                ? firstAbove95Id
                : bestSequential > 29 || bestPercentage < 91
                    ? bestSequentialId.Value
                    : bestPercentageId.Value;
            return Selected(selectedId, 0, AivPlacementStatus.Partial);
        }

        private static NativeAivAutoDecision Selected(
            int candidateId,
            int rotationIndex,
            AivPlacementStatus status) =>
            new NativeAivAutoDecision(true, candidateId, rotationIndex, status);

        private static NativeAivAutoDecision Unknown() =>
            new NativeAivAutoDecision(false, null, -1, AivPlacementStatus.NotEvaluable);
    }
}
