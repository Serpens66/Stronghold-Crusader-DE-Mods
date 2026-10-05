$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
function Edit([string]$path, [string]$old, [string]$new) {
    $target = Join-Path $workspace $path
    $source = [IO.File]::ReadAllText($target).Replace("`r`n", "`n")
    $old = $old.Replace("`r`n", "`n"); $new = $new.Replace("`r`n", "`n")
    if (-not $source.Contains($old)) { throw "Missing edit anchor: $path : $old" }
    $expected = $source.Replace($old, $new).Replace("`n", "`r`n")
    [IO.File]::WriteAllText($target, $expected, [Text.UTF8Encoding]::new($false))
    if (-not [string]::Equals($expected, [IO.File]::ReadAllText($target), [StringComparison]::Ordinal)) { throw "Write mismatch $path" }
}
$practice = 'CastlePlanner/AIVPlacement.Core/AivGeometricPractice.cs'
Edit $practice '            IReadOnlyList<int> elevatedDrawbridgeTilesByRotation = null)' @'
            IReadOnlyList<int> elevatedDrawbridgeTilesByRotation = null,
            IReadOnlyList<KeepRangeResult> keepRangeByRotation = null)
'@
Edit $practice '            CandidateId = candidateId;' @'
            CandidateId = candidateId;
            KeepRangeByRotation = keepRangeByRotation ?? Array.Empty<KeepRangeResult>();
'@
Edit $practice '        public int CandidateId { get; }' @'
        public int CandidateId { get; }
        public IReadOnlyList<KeepRangeResult> KeepRangeByRotation { get; }
'@
Edit $practice '            internal AivPlacementResult Base;' @'
            internal AivPlacementResult Base;
            internal KeepRangeLayout RangeLayout;
            internal KeepRangeResult RangeResult;
            internal readonly HashSet<int> Evaluated = new HashSet<int>();
'@
Edit $practice '            IEnumerable<int> otherPossibleAll, bool estimate)' @'
            IEnumerable<int> otherPossibleAll, bool estimate,
            IEnumerable<int> rangeCertain = null, IEnumerable<int> rangePossible = null)
'@
Edit $practice @'
            int minimum = ownFixed.Count(tile => guaranteed.Contains(tile) && !blocked.Contains(tile));
            int maximum = ownFixed.Count(tile => possible.Contains(tile) && !blocked.Contains(tile));
'@ @'
            guaranteed.IntersectWith(ownFixed);
            possible.IntersectWith(ownFixed);
            guaranteed.UnionWith(rangeCertain ?? Array.Empty<int>());
            possible.UnionWith(rangePossible ?? Array.Empty<int>());
            possible.UnionWith(guaranteed);
            guaranteed.ExceptWith(blocked);
            possible.ExceptWith(blocked);
            int minimum = guaranteed.Count;
            int maximum = possible.Count;
'@
Edit $practice '                            rotation = rotation == AivRotation.Degrees270' @'
                            foreach (AivProjectedTile tile in castle.OccupiedTiles)
                                if (baseline.Geometry.TryGetTileId(tile.MapCoordinate.X, tile.MapCoordinate.Y, out int evaluatedId))
                                    plan.Evaluated.Add(evaluatedId);
                            var rejected = new HashSet<MapCoordinate>(plan.Base.Issues.Where(issue =>
                                (issue.Kind & ~(AivPlacementIssueKind.UnresolvedNativeRule |
                                    AivPlacementIssueKind.InternalOverlap)) != 0).Select(issue => issue.MapCoordinate));
                            plan.RangeLayout = KeepRangePolicy.Prepare(castle, rejected);
                            rotation = rotation == AivRotation.Degrees270
'@
Edit $practice '                    Plan[] own = plans[request.PlayerId][candidate.CandidateId];' @'
                    if (!plans[request.PlayerId].TryGetValue(candidate.CandidateId, out Plan[] own))
                        own = new Plan[4];
'@
Edit $practice '                    bool otherUnknown = false;' @'
                    var allies = CaptureAllies(request.PlayerId, batch, plans, nativeByPlayer);
                    foreach (Plan plan in own)
                        plan.RangeResult = KeepRangePolicy.Evaluate(plan.RangeLayout, batch.KeepRange,
                            baseline.Geometry, plan.Evaluated, allies);
                    KeepRangeResult[] rangeResults = own.Select(plan => plan.RangeResult).ToArray();
                    if (rangeResults.Any(result => result.Unknown))
                    {
                        evaluations.Add(candidate.CandidateId, new AivPracticeCandidate(candidate.CandidateId,
                            Array.Empty<AivPracticeRotation>(), AivPlacementStatus.NotEvaluable, "KeepRangeUnknown",
                            projectedRotations: projectedRotations, keepRangeByRotation: rangeResults));
                        continue;
                    }
                    bool otherUnknown = false;
'@
Edit $practice '                            elevatedDrawbridgeTilesByRotation: elevatedDrawbridge));' @'
                            elevatedDrawbridgeTilesByRotation: elevatedDrawbridge,
                            keepRangeByRotation: rangeResults));
'@
Edit $practice '                            possibleGroups, possibleAll, plan.IsEstimate);' @'
                            possibleGroups, possibleAll, plan.IsEstimate,
                            plan.RangeResult.CertainTiles, plan.RangeResult.PossibleTiles);
'@
Edit $practice '                        projectedRotations, elevatedMoat, elevatedDrawbridge));' @'
                        projectedRotations, elevatedMoat, elevatedDrawbridge, rangeResults));
'@
Edit $practice '        public static AivPlacementStatus ClassifyPercentage(int percentage)' @'
        private static IReadOnlyList<KeepRangeAlly> CaptureAllies(int playerId, AivPlacementRequestBatch batch,
            Dictionary<int, Dictionary<int, Plan[]>> plans,
            Dictionary<int, AivPlacementCheckResult> native)
        {
            var result = new List<KeepRangeAlly>();
            if (!batch.KeepRange.Teams.TryGetValue(playerId, out int ownTeam))
            {
                // Older/offline captures have no roster evidence.
                if (batch.KeepRange.Teams.Count > 0)
                    result.Add(new KeepRangeAlly(null, true, true));
                return result;
            }
            foreach (var team in batch.KeepRange.Teams)
            {
                if (team.Key == playerId) continue;
                if (ownTeam < 0 || team.Value < 0)
                {
                    result.Add(new KeepRangeAlly(null, true, true));
                    continue;
                }
                // Lobby team zero means independent players, not one allied team.
                if (ownTeam == 0 || team.Value != ownTeam) continue;
                if (!plans.TryGetValue(team.Key, out Dictionary<int, Plan[]> candidates) ||
                    !native.TryGetValue(team.Key, out AivPlacementCheckResult nativeResult))
                {
                    // Human starts can be changed by CastlePlanner/Fixes; do not invent their Keep.
                    result.Add(new KeepRangeAlly(null, true, true));
                    continue;
                }
                IReadOnlyList<NativeAivAutoDecision> outcomes = NativeAivAutoSelector.SelectPossible(nativeResult.Candidates);
                var references = new List<MapCoordinate>();
                bool unknown = outcomes.Count == 0;
                foreach (NativeAivAutoDecision outcome in outcomes)
                {
                    if (!outcome.CandidateId.HasValue) continue;
                    if (!candidates.TryGetValue(outcome.CandidateId.Value, out Plan[] variants) ||
                        outcome.RotationIndex < 0 || outcome.RotationIndex >= variants.Length ||
                        variants[outcome.RotationIndex]?.RangeLayout?.KeepReference == null)
                    {
                        unknown = true;
                        continue;
                    }
                    references.Add(variants[outcome.RotationIndex].RangeLayout.KeepReference.Value);
                }
                // Later start/prebuild cleanup can remove a Keep. Its existence at the build
                // frame is not generally proven by a successful fit; credit it only as possible help.
                result.Add(new KeepRangeAlly(references, true, unknown));
            }
            return result;
        }

        public static AivPlacementStatus ClassifyPercentage(int percentage)
'@
Edit 'CastlePlanner/src/AIVPlacement/AivSelectionDialogRuntime.cs' '            notices.InsertRange(0, BuildMapHeightNotices(practice));' @'
            notices.InsertRange(0, BuildMapHeightNotices(practice));
            KeepRangeResult[] rangeNotices = practice.KeepRangeByRotation
                .Where(result => result.HasNotice).ToArray();
            if (rangeNotices.Length > 0 && !rangeNotices.Any(result => result.Unknown))
            {
                bool certain = rangeNotices.All(result => result.CertainBuildings > 0);
                notices.Add(SerpLocalization.Get(certain ? "CastlePlanner.KeepRangeBlocked" :
                    "CastlePlanner.KeepRangePossible", "Range", rangeNotices[0].Range.ToString()));
            }
'@
Edit 'CastlePlanner/src/AIVPlacement/AivSelectionDialogRuntime.cs' '                case "OtherFootprintUnknown":' @'
                case "KeepRangeUnknown": return "CastlePlanner.KeepRangeUnknown";
                case "OtherFootprintUnknown":
'@
$localeRoot = Join-Path $workspace 'CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Locales'
foreach ($locale in Get-ChildItem -LiteralPath $localeRoot -Filter '*.txt') {
    $de = $locale.BaseName -eq 'de-DE'
    $addition = if ($de) {
        "CastlePlanner.KeepRangeBlocked=Defensive Gebäude liegen außerhalb der Keep-Baureichweite ({Range} Tiles).`r`nCastlePlanner.KeepRangePossible=Defensive Gebäude könnten außerhalb der Keep-Baureichweite liegen ({Range} Tiles).`r`nCastlePlanner.KeepRangeUnknown=Die Keep-Baureichweite kann für diese Burg noch nicht sicher ausgewertet werden.`r`n"
    } else {
        "CastlePlanner.KeepRangeBlocked=Defensive buildings are outside the allowed Keep build range ({Range} tiles).`r`nCastlePlanner.KeepRangePossible=Defensive buildings may be outside the allowed Keep build range ({Range} tiles).`r`nCastlePlanner.KeepRangeUnknown=The Keep build range cannot yet be reliably evaluated for this castle.`r`n"
    }
    $text = [IO.File]::ReadAllText($locale.FullName).TrimEnd([char]13,[char]10) + "`r`n" + $addition
    [IO.File]::WriteAllText($locale.FullName,$text,[Text.UTF8Encoding]::new($false))
}
