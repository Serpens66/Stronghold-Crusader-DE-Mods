using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AIVParser.Core;
using AIVPlacement.Core;
using MapParser.Core;

namespace CastlePlanner.AIVPlacement.Core
{
    // This is deliberately separate from the native-fit and native-auto result.
    public sealed class AivPracticeRotation
    {
        internal AivPracticeRotation(AivRotation rotation, int basePercentage,
            int minimumDeduction, int maximumDeduction, int minimumPercentage,
            int maximumPercentage, bool softOverlap, bool estimate)
        {
            Rotation = rotation;
            BasePercentage = basePercentage;
            MinimumDeduction = minimumDeduction;
            MaximumDeduction = maximumDeduction;
            MinimumPercentage = minimumPercentage;
            MaximumPercentage = maximumPercentage;
            SoftOverlap = softOverlap;
            IsEstimate = estimate;
        }
        public AivRotation Rotation { get; }
        public int BasePercentage { get; }
        public int MinimumDeduction { get; }
        public int MaximumDeduction { get; }
        public int MinimumPercentage { get; }
        public int MaximumPercentage { get; }
        public bool SoftOverlap { get; }
        public bool IsEstimate { get; }
    }

    public sealed class AivPracticeCandidate
    {
        internal AivPracticeCandidate(int candidateId, IReadOnlyList<AivPracticeRotation> rotations,
            AivPlacementStatus status, string reason,
            IReadOnlyList<int> relevantRotationIndexes = null,
            IReadOnlyList<AivRotation> projectedRotations = null,
            IReadOnlyList<int> elevatedMoatTilesByRotation = null,
            IReadOnlyList<int> elevatedDrawbridgeTilesByRotation = null)
        {
            CandidateId = candidateId;
            Rotations = rotations;
            Status = status;
            Reason = reason ?? string.Empty;
            RelevantRotationIndexes = relevantRotationIndexes ?? Array.Empty<int>();
            ProjectedRotations = projectedRotations ?? Array.Empty<AivRotation>();
            ElevatedMoatTilesByRotation = elevatedMoatTilesByRotation ?? Array.Empty<int>();
            ElevatedDrawbridgeTilesByRotation = elevatedDrawbridgeTilesByRotation ?? Array.Empty<int>();
        }
        public int CandidateId { get; }
        public IReadOnlyList<AivPracticeRotation> Rotations { get; }
        public AivPlacementStatus Status { get; }
        public string Reason { get; }
        public IReadOnlyList<int> RelevantRotationIndexes { get; }
        public IReadOnlyList<AivRotation> ProjectedRotations { get; }
        public IReadOnlyList<int> ElevatedMoatTilesByRotation { get; }
        public IReadOnlyList<int> ElevatedDrawbridgeTilesByRotation { get; }
    }

    public sealed class AivPracticeBatch
    {
        internal AivPracticeBatch(long generation,
            IReadOnlyDictionary<int, IReadOnlyDictionary<int, AivPracticeCandidate>> players)
        {
            Generation = generation;
            Players = players;
        }
        public long Generation { get; }
        public IReadOnlyDictionary<int, IReadOnlyDictionary<int, AivPracticeCandidate>> Players { get; }

        public static AivPracticeBatch Unavailable(AivPlacementRequestBatch request, string reason)
        {
            var players = new Dictionary<int, IReadOnlyDictionary<int, AivPracticeCandidate>>();
            foreach (AivPlacementCheckRequest player in request.Requests)
                players[player.PlayerId] = player.Candidates.ToDictionary(candidate => candidate.CandidateId,
                    candidate => new AivPracticeCandidate(candidate.CandidateId,
                        Array.Empty<AivPracticeRotation>(), AivPlacementStatus.NotEvaluable,
                        reason ?? "UnknownGeometry"));
            return new AivPracticeBatch(request.Generation, players);
        }
    }

    public static class AivPracticePresentation
    {
        public static int? BestPossiblePercentage(IReadOnlyList<AivPracticeRotation> rotations,
            IReadOnlyList<int> relevantRotationIndexes, AivPlacementStatus status)
        {
            if (status == AivPlacementStatus.NotEvaluable || rotations == null ||
                relevantRotationIndexes == null || relevantRotationIndexes.Count == 0)
                return null;

            int best = -1;
            foreach (int index in relevantRotationIndexes)
            {
                if (index < 0 || index >= rotations.Count || rotations[index] == null)
                    return null;
                best = Math.Max(best, rotations[index].MaximumPercentage);
            }
            return best >= 0 && best <= 100 ? (int?)best : null;
        }

        public static string FormatPercentage(int minimum, int maximum) =>
            minimum == maximum ? $"{minimum}%" : $"{minimum}–{maximum}%";

        public static string FormatRotationLine(IReadOnlyList<AivPracticeRotation> rotations,
            Func<int, string> formatRotation, string label, string estimateLabel, string unknownLabel)
        {
            if (rotations == null || rotations.Count == 0)
                return unknownLabel;
            string values = string.Join(" | ", rotations.Select(rotation =>
                $"{formatRotation((int)rotation.Rotation)} {FormatPercentage(rotation.MinimumPercentage, rotation.MaximumPercentage)}"));
            string prefix = rotations.Any(rotation => rotation.IsEstimate) ? estimateLabel : label;
            return prefix.Replace("{Results}", values);
        }

        public static string ComposeTooltip(string practiceLine, string vanillaLine,
            IEnumerable<string> notices, string autoLine)
        {
            var lines = new List<string> { practiceLine, vanillaLine };
            if (notices != null)
                lines.AddRange(notices.Where(notice => !string.IsNullOrEmpty(notice)));
            if (!string.IsNullOrEmpty(autoLine))
                lines.Add(autoLine);
            return string.Join(Environment.NewLine, lines.Where(line => !string.IsNullOrEmpty(line)));
        }
    }

    public static class AivGeometricPractice
    {
        internal sealed class Plan
        {
            internal readonly HashSet<int> Fixed = new HashSet<int>();
            internal readonly HashSet<int> Soft = new HashSet<int>();
            internal readonly HashSet<int> All = new HashSet<int>();
            internal readonly HashSet<int> Blocked = new HashSet<int>();
            internal AivPlacementResult Base;
            internal AivRotation Rotation;
            internal bool HasKnownGeometry;
            internal bool IsEstimate;
            internal bool IsValid;
            internal int ElevatedMoatTiles;
            internal int ElevatedDrawbridgeTiles;
        }

        // Public pure entry point so the overlap and deduplication rules can be tested without Unity.
        public static AivPracticeRotation Score(AivRotation rotation, int basePercentage,
            int evaluatedTiles, int baseBlockedTiles, IEnumerable<int> fixedTiles, IEnumerable<int> softTiles,
            IEnumerable<int> nativeBlockedTiles, IEnumerable<IEnumerable<int>> otherGuaranteedFixed,
            IEnumerable<IEnumerable<int>> otherPossibleFixed,
            IEnumerable<int> otherPossibleAll, bool estimate)
        {
            var ownFixed = new HashSet<int>(fixedTiles);
            var ownSoft = new HashSet<int>(softTiles);
            var blocked = new HashSet<int>(nativeBlockedTiles);
            var guaranteed = new HashSet<int>();
            foreach (IEnumerable<int> group in otherGuaranteedFixed)
                guaranteed.UnionWith(group);
            var possible = new HashSet<int>();
            foreach (IEnumerable<int> group in otherPossibleFixed)
                possible.UnionWith(group);
            int minimum = ownFixed.Count(tile => guaranteed.Contains(tile) && !blocked.Contains(tile));
            int maximum = ownFixed.Count(tile => possible.Contains(tile) && !blocked.Contains(tile));
            int denominator = Math.Max(1, evaluatedTiles);
            int low = Math.Max(0, (evaluatedTiles - baseBlockedTiles - maximum) * 100 / denominator);
            int high = Math.Max(0, (evaluatedTiles - baseBlockedTiles - minimum) * 100 / denominator);
            return new AivPracticeRotation(rotation, basePercentage, minimum, maximum,
                low, high, ownSoft.Overlaps(otherPossibleAll), estimate);
        }

        public static AivPracticeBatch Evaluate(AivPlacementRequestBatch batch,
            IReadOnlyDictionary<string, string> assets, AivPlacementBatchResult native,
            CancellationToken cancellationToken)
        {
            var output = new Dictionary<int, IReadOnlyDictionary<int, AivPracticeCandidate>>();
            if (batch.Requests.Count == 0)
                return new AivPracticeBatch(batch.Generation, output);
            LobbyFileStamp mapStamp = LobbyFileStamp.Capture(batch.Requests[0].MapPath);
            MapDocument document = MapFileReader.Parse(batch.Requests[0].MapPath);
            if (!mapStamp.Equals(LobbyFileStamp.Capture(batch.Requests[0].MapPath)))
                throw new InvalidOperationException("Map changed during geometric assessment.");
            var anchors = MapKeepAnchors.Create(document);
            var aiSlots = new HashSet<int>(batch.Requests.Select(value => value.KeepSlotIndex));
            int[] humanSlots = batch.Requests[0].RetainedStartSlotIndexes
                .Where(slot => !aiSlots.Contains(slot)).ToArray();
            IAivPlacementTileSource baseline = AivPreplacementMapState.Create(document, humanSlots);
            var projector = new AivCastleProjector();
            var evaluator = new AivPlacementEvaluator();
            var plans = new Dictionary<int, Dictionary<int, Plan[]>>();
            var nativeByPlayer = native.Results.ToDictionary(value => value.PlayerId);
            foreach (AivPlacementCheckRequest request in batch.Requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidates = new Dictionary<int, Plan[]>();
                plans.Add(request.PlayerId, candidates);
                MapKeepAnchorResult anchor = anchors.GetSlot(request.KeepSlotIndex);
                if (anchor.Status != MapKeepAnchorStatus.Exact || !anchor.Coordinate.HasValue)
                    continue;
                AivRotation initial = request.UsesMapFacingRotation
                    ? AivInitialRotationResolver.ResolveMapFacing(anchor.Coordinate.Value)
                    : request.InitialRotation;
                foreach (AivPlacementCandidateRequest candidate in request.Candidates)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var variants = new Plan[4];
                    candidates.Add(candidate.CandidateId, variants);
                    try
                    {
                        AivJsonLoadResult loaded;
                        if (candidate.SourceKind == LobbyCandidateSourceKind.File)
                        {
                            LobbyFileStamp sourceStamp = LobbyFileStamp.Capture(candidate.Source);
                            loaded = AivJsonFileLoader.Load(candidate.Source);
                            if (!sourceStamp.Equals(LobbyFileStamp.Capture(candidate.Source)))
                                continue;
                        }
                        else
                        {
                            if (assets == null || !assets.TryGetValue(candidate.Source, out string text))
                                continue;
                            loaded = AivJsonFileLoader.LoadText(text, candidate.Source);
                        }
                        AivParseResult parsed = new AivBlueprintParser().Parse(
                            loaded.Document, candidate.Source, loaded.Diagnostics);
                        if (parsed.Diagnostics.Any(value => value.Severity == AivDiagnosticSeverity.Error))
                            continue;
                        AivRotation rotation = initial;
                        for (int index = 0; index < 4; index++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            AivProjectedCastle castle = projector.Project(parsed.Blueprint,
                                anchor.Coordinate.Value, rotation);
                            var plan = new Plan();
                            variants[index] = plan;
                            plan.Rotation = rotation;
                            plan.HasKnownGeometry = castle.Elements.All(element =>
                                element.Mapper != null && element.Mapper.Category != AivItemCategory.Unknown &&
                                element.OccupiedTiles.Any(tile =>
                                    tile.Kind == AivProjectedTileKind.CoreFootprint));
                            plan.IsValid = plan.HasKnownGeometry;
                            if (plan.HasKnownGeometry)
                            {
                                plan.ElevatedMoatTiles = MoatBuildExposure.Count(baseline, castle);
                                plan.ElevatedDrawbridgeTiles = MoatBuildExposure.CountDrawbridge(baseline, castle);
                            }
                            if (plan.IsValid)
                            foreach (AivProjectedElement element in castle.Elements)
                            foreach (AivProjectedTile tile in element.OccupiedTiles)
                            {
                                if (tile.Kind != AivProjectedTileKind.CoreFootprint ||
                                    !baseline.Geometry.TryGetTileId(tile.MapCoordinate.X,
                                        tile.MapCoordinate.Y, out int tileId))
                                    continue;
                                plan.All.Add(tileId);
                                if (element.Mapper.Category == AivItemCategory.Building ||
                                    element.Mapper.Category == AivItemCategory.Keep)
                                    plan.Fixed.Add(tileId);
                                else if (element.Mapper.Category == AivItemCategory.HighWallPath ||
                                         element.Mapper.Category == AivItemCategory.LowWallPath ||
                                         element.Mapper.Category == AivItemCategory.CrenelPath ||
                                         element.Mapper.Category == AivItemCategory.MoatPath)
                                    plan.Soft.Add(tileId);
                            }
                            AivPlacementResult exact = null;
                            if (nativeByPlayer.TryGetValue(request.PlayerId, out AivPlacementCheckResult nativeResult))
                                exact = nativeResult.Candidates.FirstOrDefault(value =>
                                    value.CandidateId == candidate.CandidateId)?.Selection?.Variants
                                    .FirstOrDefault(value => value.Rotation == rotation);
                            plan.IsEstimate = exact == null || exact.Status == AivPlacementStatus.NotEvaluable;
                            plan.Base = plan.IsEstimate ? evaluator.Evaluate(baseline, castle) : exact;
                            plan.IsValid = plan.IsValid && plan.Base.Status != AivPlacementStatus.NotEvaluable;
                            foreach (AivPlacementIssue issue in plan.Base.Issues)
                                if ((issue.Kind & ~(AivPlacementIssueKind.UnresolvedNativeRule |
                                    AivPlacementIssueKind.InternalOverlap)) != 0 &&
                                    baseline.Geometry.TryGetTileId(issue.MapCoordinate.X,
                                        issue.MapCoordinate.Y, out int blockedId))
                                    plan.Blocked.Add(blockedId);
                            rotation = rotation == AivRotation.Degrees270
                                ? AivRotation.Degrees0 : (AivRotation)((int)rotation + 90);
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        // A missing or changing source is unknown geometry, never free space.
                    }
                }
            }

            foreach (AivPlacementCheckRequest request in batch.Requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var evaluations = new Dictionary<int, AivPracticeCandidate>();
                output.Add(request.PlayerId, evaluations);
                foreach (AivPlacementCandidateRequest candidate in request.Candidates)
                {
                    Plan[] own = plans[request.PlayerId][candidate.CandidateId];
                    AivRotation[] projectedRotations = own.All(value => value?.HasKnownGeometry == true)
                        ? own.Select(value => value.Rotation).ToArray()
                        : Array.Empty<AivRotation>();
                    int[] elevatedMoat = projectedRotations.Length == 0
                        ? Array.Empty<int>() : own.Select(value => value.ElevatedMoatTiles).ToArray();
                    int[] elevatedDrawbridge = projectedRotations.Length == 0
                        ? Array.Empty<int>() : own.Select(value => value.ElevatedDrawbridgeTiles).ToArray();
                    if (own.Any(value => value == null || !value.IsValid))
                    {
                        evaluations.Add(candidate.CandidateId, new AivPracticeCandidate(
                            candidate.CandidateId, Array.Empty<AivPracticeRotation>(),
                            AivPlacementStatus.NotEvaluable, "UnknownGeometry"));
                        continue;
                    }
                    bool otherUnknown = false;
                    var guaranteedGroups = new List<IEnumerable<int>>();
                    var possibleGroups = new List<IEnumerable<int>>();
                    var possibleAll = new HashSet<int>();
                    foreach (AivPlacementCheckRequest other in batch.Requests)
                    {
                        if (other.PlayerId == request.PlayerId)
                            continue;
                        var options = new List<Plan>();
                        IReadOnlyList<NativeAivAutoDecision> outcomes = nativeByPlayer.TryGetValue(
                            other.PlayerId, out AivPlacementCheckResult otherNative)
                            ? NativeAivAutoSelector.SelectPossible(otherNative.Candidates)
                            : Array.Empty<NativeAivAutoDecision>();
                        if (outcomes.Count != 0)
                        {
                            foreach (NativeAivAutoDecision outcome in outcomes)
                            {
                                if (!outcome.CandidateId.HasValue)
                                {
                                    options.Add(new Plan { IsValid = true });
                                    continue;
                                }
                                if (plans[other.PlayerId].TryGetValue(outcome.CandidateId.Value,
                                        out Plan[] selected) && outcome.RotationIndex >= 0 &&
                                    outcome.RotationIndex < selected.Length)
                                    options.Add(selected[outcome.RotationIndex]);
                            }
                        }
                        else
                            options.AddRange(plans[other.PlayerId].Values.SelectMany(value => value));
                        if (options.Count == 0 || options.Any(value => value == null || !value.IsValid))
                        {
                            otherUnknown = true;
                            break;
                        }
                        var intersection = new HashSet<int>(options[0].Fixed);
                        foreach (Plan option in options.Skip(1))
                            intersection.IntersectWith(option.Fixed);
                        guaranteedGroups.Add(intersection);
                        var union = new HashSet<int>();
                        foreach (Plan option in options)
                        {
                            union.UnionWith(option.Fixed);
                            possibleAll.UnionWith(option.All);
                        }
                        possibleGroups.Add(union);
                    }
                    if (otherUnknown)
                    {
                        evaluations.Add(candidate.CandidateId, new AivPracticeCandidate(
                            candidate.CandidateId, Array.Empty<AivPracticeRotation>(),
                            AivPlacementStatus.NotEvaluable, "OtherFootprintUnknown",
                            projectedRotations: projectedRotations,
                            elevatedMoatTilesByRotation: elevatedMoat,
                            elevatedDrawbridgeTilesByRotation: elevatedDrawbridge));
                        continue;
                    }
                    var scored = new List<AivPracticeRotation>(4);
                    int minimum = int.MaxValue;
                    int maximum = int.MinValue;
                    IReadOnlyList<NativeAivAutoDecision> ownOutcomes = nativeByPlayer.TryGetValue(
                        request.PlayerId, out AivPlacementCheckResult ownNative)
                        ? NativeAivAutoSelector.SelectPossible(ownNative.Candidates)
                        : Array.Empty<NativeAivAutoDecision>();
                    var selectedRotations = new HashSet<int>(ownOutcomes.Where(value =>
                        value.CandidateId == candidate.CandidateId &&
                        value.RotationIndex >= 0 && value.RotationIndex < 4)
                        .Select(value => value.RotationIndex));
                    for (int index = 0; index < own.Length; index++)
                    {
                        Plan plan = own[index];
                        AivPracticeRotation score = Score(plan.Base.Rotation,
                            plan.Base.Score.FitPercentage, plan.Base.Score.EvaluatedTileCount,
                            plan.Base.Score.BlockedTileCount,
                            plan.Fixed, plan.Soft, plan.Blocked, guaranteedGroups,
                            possibleGroups, possibleAll, plan.IsEstimate);
                        scored.Add(score);
                        if (selectedRotations.Count == 0 || selectedRotations.Contains(index))
                        {
                            minimum = Math.Min(minimum, score.MinimumPercentage);
                            maximum = Math.Max(maximum, score.MaximumPercentage);
                        }
                    }
                    evaluations.Add(candidate.CandidateId, new AivPracticeCandidate(
                        candidate.CandidateId, scored,
                        ClassifyPossiblePercentages(minimum, maximum),
                        minimum == 0 && maximum > 0 ? "MixedZeroAndPositive" : string.Empty,
                        selectedRotations.Count == 0
                            ? Enumerable.Range(0, own.Length).ToArray()
                            : selectedRotations.OrderBy(value => value).ToArray(),
                        projectedRotations, elevatedMoat, elevatedDrawbridge));
                }
            }
            if (!mapStamp.Equals(LobbyFileStamp.Capture(batch.Requests[0].MapPath)))
                throw new InvalidOperationException("Map changed during geometric assessment.");
            return new AivPracticeBatch(batch.Generation, output);
        }

        public static AivPlacementStatus ClassifyPercentage(int percentage)
        {
            if (percentage <= 0)
                return AivPlacementStatus.Impossible;
            return percentage >= 100
                ? AivPlacementStatus.Complete : AivPlacementStatus.Partial;
        }

        public static AivPlacementStatus ClassifyPossiblePercentages(int minimum, int maximum)
        {
            if (minimum < 0 || maximum < minimum || maximum > 100)
                return AivPlacementStatus.NotEvaluable;
            if (minimum == 100)
                return AivPlacementStatus.Complete;
            if (maximum == 0)
                return AivPlacementStatus.Impossible;
            return minimum > 0 ? AivPlacementStatus.Partial : AivPlacementStatus.NotEvaluable;
        }
    }
}
