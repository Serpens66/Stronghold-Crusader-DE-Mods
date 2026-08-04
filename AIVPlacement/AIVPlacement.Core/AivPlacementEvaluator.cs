using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AIVParser.Core;
using MapParser.Core;

namespace AIVPlacement.Core
{
    public sealed class AivPlacementEvaluator
    {
        public const int CompleteSequentialScore = 999999;
        private const int AlternativeRotationMinimumPercentage = 86;

        private readonly AivCastleProjector projector;
        private readonly AivPlacementRuleEvaluator ruleEvaluator;

        public AivPlacementEvaluator()
            : this(new AivCastleProjector(), new AivPlacementRuleEvaluator())
        {
        }

        internal AivPlacementEvaluator(
            AivCastleProjector projector,
            AivPlacementRuleEvaluator ruleEvaluator)
        {
            this.projector = projector ?? throw new ArgumentNullException(nameof(projector));
            this.ruleEvaluator = ruleEvaluator ??
                throw new ArgumentNullException(nameof(ruleEvaluator));
        }

        public AivPlacementResult Evaluate(
            MapPlacementSnapshot map,
            AivBlueprint aiv,
            MapCoordinate keepPosition,
            AivRotation rotation)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            AivProjectedCastle castle = projector.Project(aiv, keepPosition, rotation);
            return BuildResult(castle, ruleEvaluator.EvaluateElements(map, castle));
        }

        public AivPlacementResult Evaluate(
            MapPlacementSnapshot map,
            AivProjectedCastle castle)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (castle == null)
                throw new ArgumentNullException(nameof(castle));

            // Keeping projection separate lets offline callers time both expensive phases.
            return BuildResult(castle, ruleEvaluator.EvaluateElements(map, castle));
        }

        public AivPlacementResult Evaluate(
            IAivPlacementTileSource map,
            AivBlueprint aiv,
            MapCoordinate keepPosition,
            AivRotation rotation)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            AivProjectedCastle castle = projector.Project(aiv, keepPosition, rotation);
            return BuildResult(castle, ruleEvaluator.EvaluateElements(map, castle));
        }

        public AivPlacementResult Evaluate(
            IAivPlacementTileSource map,
            AivProjectedCastle castle)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));
            if (castle == null)
                throw new ArgumentNullException(nameof(castle));

            // Lobby workers time projection and native-rule evaluation separately.
            return BuildResult(castle, ruleEvaluator.EvaluateElements(map, castle));
        }

        public AivPlacementRotationSelection EvaluateAllRotations(
            MapPlacementSnapshot map,
            AivBlueprint aiv,
            MapCoordinate keepPosition,
            AivRotation initialRotation)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            return EvaluateAllRotationsCore(
                rotation => Evaluate(map, aiv, keepPosition, rotation),
                initialRotation);
        }

        public AivPlacementRotationSelection EvaluateAllRotations(
            IAivPlacementTileSource map,
            AivBlueprint aiv,
            MapCoordinate keepPosition,
            AivRotation initialRotation)
        {
            if (map == null)
                throw new ArgumentNullException(nameof(map));

            return EvaluateAllRotationsCore(
                rotation => Evaluate(map, aiv, keepPosition, rotation),
                initialRotation);
        }

        private static AivPlacementResult BuildResult(
            AivProjectedCastle castle,
            IReadOnlyList<AivElementPlacementResult> elementResults)
        {
            var issues = new List<AivPlacementIssue>();
            int placeableElements = 0;
            int blockedElements = 0;
            int notEvaluableElements = 0;
            var evaluatedCoordinates = new HashSet<MapCoordinate>(
                castle.OccupiedTiles.Select(tile => tile.MapCoordinate));
            var blockedCoordinates = new HashSet<MapCoordinate>();
            int? firstBlockingBuildStep = null;
            bool unresolved = false;

            foreach (AivElementPlacementResult elementResult in elementResults)
            {
                switch (elementResult.Status)
                {
                    case AivElementPlacementStatus.Placeable:
                        placeableElements++;
                        break;
                    case AivElementPlacementStatus.Blocked:
                        blockedElements++;
                        if (!firstBlockingBuildStep.HasValue ||
                            elementResult.Element.BuildIndex < firstBlockingBuildStep.Value)
                        {
                            firstBlockingBuildStep = elementResult.Element.BuildIndex;
                        }
                        break;
                    case AivElementPlacementStatus.NotEvaluable:
                        notEvaluableElements++;
                        break;
                }

                foreach (AivPlacementIssue issue in elementResult.Issues)
                {
                    issues.Add(issue);
                    if ((issue.Kind & AivPlacementIssueKind.UnresolvedNativeRule) != 0)
                        unresolved = true;
                    if ((issue.Kind & ~(AivPlacementIssueKind.UnresolvedNativeRule |
                            AivPlacementIssueKind.InternalOverlap)) != 0)
                        blockedCoordinates.Add(issue.MapCoordinate);
                }
            }

            int evaluatedTiles = evaluatedCoordinates.Count;
            int blockedTiles = blockedCoordinates.Count;
            int fitPercentage = evaluatedTiles == 0
                ? 100
                : (evaluatedTiles - blockedTiles) * 100 / evaluatedTiles;
            AivPlacementStatus status;
            int sequentialScore;
            if (unresolved || evaluatedTiles == 0)
            {
                status = AivPlacementStatus.NotEvaluable;
                sequentialScore = -1;
            }
            else if (!firstBlockingBuildStep.HasValue)
            {
                status = AivPlacementStatus.Complete;
                sequentialScore = CompleteSequentialScore;
            }
            else
            {
                sequentialScore = firstBlockingBuildStep.Value;
                status = sequentialScore > 0
                    ? AivPlacementStatus.Partial
                    : AivPlacementStatus.Impossible;
            }

            return new AivPlacementResult(
                castle,
                status,
                elementResults,
                new ReadOnlyCollection<AivPlacementIssue>(issues.ToArray()),
                placeableElements,
                blockedElements,
                notEvaluableElements,
                firstBlockingBuildStep,
                new AivPlacementScore(
                    sequentialScore,
                    fitPercentage,
                    evaluatedTiles,
                    blockedTiles));
        }

        private static AivPlacementRotationSelection EvaluateAllRotationsCore(
            Func<AivRotation, AivPlacementResult> evaluate,
            AivRotation initialRotation)
        {
            ValidateRotation(initialRotation);
            var variants = new List<AivPlacementResult>(4);
            AivRotation rotation = initialRotation;
            for (int index = 0; index < 4; index++)
            {
                variants.Add(evaluate(rotation));
                rotation = NextRotation(rotation);
            }

            return SelectRotationResultsCore(variants, initialRotation);
        }

        public AivPlacementRotationSelection SelectRotationResults(
            IReadOnlyList<AivPlacementResult> variants,
            AivRotation initialRotation)
        {
            if (variants == null)
                throw new ArgumentNullException(nameof(variants));
            if (variants.Count != 4)
                throw new ArgumentException("Exactly four rotation results are required.", nameof(variants));

            ValidateRotation(initialRotation);
            AivRotation expected = initialRotation;
            for (int index = 0; index < variants.Count; index++)
            {
                if (variants[index] == null || variants[index].Rotation != expected)
                {
                    throw new ArgumentException(
                        "Rotation results must follow native order from the initial rotation.",
                        nameof(variants));
                }
                expected = NextRotation(expected);
            }

            return SelectRotationResultsCore(variants, initialRotation);
        }

        private static AivPlacementRotationSelection SelectRotationResultsCore(
            IReadOnlyList<AivPlacementResult> variants,
            AivRotation initialRotation)
        {

            // Native SelectBestFit checks rotations in this order and stops at a complete fit.
            foreach (AivPlacementResult variant in variants)
            {
                if (variant.Status == AivPlacementStatus.NotEvaluable)
                {
                    return Selection(
                        initialRotation,
                        AivPlacementStatus.NotEvaluable,
                        null,
                        variants);
                }
                if (variant.Status == AivPlacementStatus.Complete)
                {
                    return Selection(
                        initialRotation,
                        AivPlacementStatus.Complete,
                        variant,
                        variants);
                }
            }

            AivPlacementResult initial = variants[0];
            if (initial.Status == AivPlacementStatus.Partial)
            {
                // For one AIV, Vanilla retains a positive partial from the initial rotation.
                return Selection(
                    initialRotation,
                    AivPlacementStatus.Partial,
                    initial,
                    variants);
            }

            AivPlacementResult bestAlternative = null;
            for (int index = 1; index < variants.Count; index++)
            {
                AivPlacementResult candidate = variants[index];
                if (candidate.Status != AivPlacementStatus.Partial ||
                    candidate.Score.FitPercentage < AlternativeRotationMinimumPercentage)
                {
                    continue;
                }

                if (bestAlternative == null ||
                    candidate.Score.FitPercentage > bestAlternative.Score.FitPercentage)
                {
                    // Strict comparison preserves the first rotation on a native tie.
                    bestAlternative = candidate;
                }
            }

            return bestAlternative == null
                ? Selection(
                    initialRotation,
                    AivPlacementStatus.Impossible,
                    null,
                    variants)
                : Selection(
                    initialRotation,
                    AivPlacementStatus.Partial,
                    bestAlternative,
                    variants);
        }

        private static AivPlacementRotationSelection Selection(
            AivRotation initialRotation,
            AivPlacementStatus status,
            AivPlacementResult bestVariant,
            IReadOnlyList<AivPlacementResult> variants) =>
            new AivPlacementRotationSelection(
                initialRotation,
                status,
                bestVariant,
                variants);

        private static AivRotation NextRotation(AivRotation rotation)
        {
            switch (rotation)
            {
                case AivRotation.Degrees0:
                    return AivRotation.Degrees90;
                case AivRotation.Degrees90:
                    return AivRotation.Degrees180;
                case AivRotation.Degrees180:
                    return AivRotation.Degrees270;
                case AivRotation.Degrees270:
                    return AivRotation.Degrees0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }

        private static void ValidateRotation(AivRotation rotation)
        {
            if (rotation != AivRotation.Degrees0 &&
                rotation != AivRotation.Degrees90 &&
                rotation != AivRotation.Degrees180 &&
                rotation != AivRotation.Degrees270)
            {
                throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }
    }
}
