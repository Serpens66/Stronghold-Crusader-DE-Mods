using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AIVParser.Core;

namespace AIVPlacement.Core
{
    public enum AivPlacementStatus
    {
        Complete,
        Partial,
        Impossible,
        NotEvaluable
    }

    public sealed class AivPlacementScore
    {
        internal AivPlacementScore(
            int sequentialBuildScore,
            int fitPercentage,
            int evaluatedTileCount,
            int blockedTileCount)
        {
            SequentialBuildScore = sequentialBuildScore;
            FitPercentage = fitPercentage;
            EvaluatedTileCount = evaluatedTileCount;
            BlockedTileCount = blockedTileCount;
        }

        // Vanilla ranks partial candidates with both dimensions, not one scalar.
        public int SequentialBuildScore { get; }
        public int FitPercentage { get; }
        public int EvaluatedTileCount { get; }
        public int BlockedTileCount { get; }
    }

    public sealed class AivPlacementResult
    {
        internal AivPlacementResult(
            AivProjectedCastle castle,
            AivPlacementStatus status,
            IReadOnlyList<AivElementPlacementResult> elementResults,
            IReadOnlyList<AivPlacementIssue> issues,
            int placeableElementCount,
            int blockedElementCount,
            int notEvaluableElementCount,
            int? firstBlockingBuildStep,
            AivPlacementScore score)
        {
            Castle = castle ?? throw new ArgumentNullException(nameof(castle));
            Status = status;
            ElementResults = ProjectionCollections.Copy(elementResults);
            Issues = ProjectionCollections.Copy(issues);
            PlaceableElementCount = placeableElementCount;
            BlockedElementCount = blockedElementCount;
            NotEvaluableElementCount = notEvaluableElementCount;
            FirstBlockingBuildStep = firstBlockingBuildStep;
            Score = score ?? throw new ArgumentNullException(nameof(score));
        }

        public AivProjectedCastle Castle { get; }
        public AivPlacementStatus Status { get; }
        public AivRotation Rotation => Castle.Rotation;
        public int TotalElementCount => ElementResults.Count;
        public int PlaceableElementCount { get; }
        public int BlockedElementCount { get; }
        public int NotEvaluableElementCount { get; }
        public IReadOnlyList<AivElementPlacementResult> ElementResults { get; }
        public IReadOnlyList<AivPlacementIssue> Issues { get; }
        public int? FirstBlockingBuildStep { get; }
        public AivPlacementScore Score { get; }
    }

    public sealed class AivPlacementRotationSelection
    {
        internal AivPlacementRotationSelection(
            AivRotation initialRotation,
            AivPlacementStatus status,
            AivPlacementResult bestVariant,
            IReadOnlyList<AivPlacementResult> variants)
        {
            InitialRotation = initialRotation;
            Status = status;
            BestVariant = bestVariant;
            Variants = ProjectionCollections.Copy(variants);
            CompleteVariants = FilterAndSort(variants, AivPlacementStatus.Complete);
            PartialVariants = FilterAndSort(variants, AivPlacementStatus.Partial);
        }

        public AivRotation InitialRotation { get; }
        public AivPlacementStatus Status { get; }
        public AivPlacementResult BestVariant { get; }
        public IReadOnlyList<AivPlacementResult> Variants { get; }
        public IReadOnlyList<AivPlacementResult> CompleteVariants { get; }
        public IReadOnlyList<AivPlacementResult> PartialVariants { get; }

        private static IReadOnlyList<AivPlacementResult> FilterAndSort(
            IReadOnlyList<AivPlacementResult> variants,
            AivPlacementStatus status)
        {
            var filtered = new List<AivPlacementResult>();
            foreach (AivPlacementResult variant in variants)
            {
                if (variant.Status == status)
                    filtered.Add(variant);
            }

            filtered.Sort((left, right) => CompareVariants(left, right, status));
            return new ReadOnlyCollection<AivPlacementResult>(filtered.ToArray());
        }

        private static int CompareVariants(
            AivPlacementResult left,
            AivPlacementResult right,
            AivPlacementStatus status)
        {
            if (status == AivPlacementStatus.Partial)
            {
                int percentage = right.Score.FitPercentage.CompareTo(
                    left.Score.FitPercentage);
                if (percentage != 0)
                    return percentage;

                int sequential = right.Score.SequentialBuildScore.CompareTo(
                    left.Score.SequentialBuildScore);
                if (sequential != 0)
                    return sequential;
            }

            return ((int)left.Rotation).CompareTo((int)right.Rotation);
        }
    }
}
