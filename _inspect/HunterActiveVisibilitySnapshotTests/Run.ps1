$ErrorActionPreference = 'Stop'

$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sourcePath = Join-Path $workspace 'ImprovedHunters\src\HunterActiveTargetVisibilitySnapshot.cs'
$source = [System.IO.File]::ReadAllText($sourcePath)
$runtimePath = Join-Path $workspace 'ImprovedHunters\src\ImprovedHuntersRuntime.cs'
$runtime = [System.IO.File]::ReadAllText($runtimePath)
$continuationPath = Join-Path $workspace 'ImprovedHunters\src\HunterVanillaPathContinuationDiagnostic.cs'
$continuation = [System.IO.File]::ReadAllText($continuationPath)
$nativeProbePath = Join-Path $workspace 'ImprovedHunters\src\HunterNativeVisibilityProbe.cs'
$nativeProbe = [System.IO.File]::ReadAllText($nativeProbePath)

$equalsStart = $source.IndexOf('public bool Equals(VisibilityInputs other)', [System.StringComparison]::Ordinal)
$hashStart = $source.IndexOf('public override int GetHashCode()', [System.StringComparison]::Ordinal)
$structEnd = $source.IndexOf("`r`n        }`r`n    }", $hashStart, [System.StringComparison]::Ordinal)
if ($equalsStart -lt 0 -or $hashStart -le $equalsStart -or $structEnd -le $hashStart) {
    throw 'VisibilityInputs equality or hash block was not found.'
}

$equalsBlock = $source.Substring($equalsStart, $hashStart - $equalsStart)
$hashBlock = $source.Substring($hashStart, $structEnd - $hashStart)
$stableFields = @(
    'HunterUnitId',
    'HunterGlobalId',
    'PreyUnitId',
    'PreyGlobalId',
    'PreyType',
    'PlayerId',
    'MapGeneration',
    'AiState',
    'PathState',
    'PathLength',
    'Reservation'
)

foreach ($field in $stableFields) {
    if (-not $equalsBlock.Contains($field) -or -not $hashBlock.Contains($field)) {
        throw "Stable visibility identity field is missing from equality or hash: $field"
    }
}

foreach ($liveField in @(
    'PathFieldF4',
    'PathProgress',
    'RawReservation',
    'HunterTileX',
    'HunterTileY',
    'PreyTileX',
    'PreyTileY'
)) {
    if ($equalsBlock.Contains($liveField) -or $hashBlock.Contains($liveField)) {
        throw "Live path field must not replace the active visibility identity: $liveField"
    }
}

$trackerStart = $source.IndexOf('private Tracker GetOrReplaceTracker(', [System.StringComparison]::Ordinal)
$trackerEnd = $source.IndexOf('private bool TryCaptureInputs(', $trackerStart, [System.StringComparison]::Ordinal)
if ($trackerStart -lt 0 -or $trackerEnd -le $trackerStart) {
    throw 'Visibility tracker replacement block was not found.'
}

$trackerBlock = $source.Substring($trackerStart, $trackerEnd - $trackerStart)
if ($trackerBlock.Contains('PathProgress') -or $trackerBlock.Contains('LastPathProgress')) {
    throw 'Raw path progress must not participate in visibility tracker replacement.'
}
if (-not $trackerBlock.Contains('tracker.PathGeneration != pathGeneration') -or
    -not $trackerBlock.Contains('new-path-generation-pending')) {
    throw 'An explicit accepted-MoveHere generation must replace the visibility tracker.'
}
if (-not $trackerBlock.Contains('tracker.LastValidatedAt = timestamp') -or
    -not $trackerBlock.Contains('tracker.MissingSince = 0')) {
    throw 'A valid scan or inline-hook access must refresh tracker retention without replacing its snapshot.'
}

$scanStart = $source.IndexOf('public void ProcessNativeScan(', [System.StringComparison]::Ordinal)
$scanEnd = $source.IndexOf('public bool TryGetObservation(', $scanStart, [System.StringComparison]::Ordinal)
if ($scanStart -lt 0 -or $scanEnd -le $scanStart) {
    throw 'Active visibility scan block was not found.'
}

$scanBlock = $source.Substring($scanStart, $scanEnd - $scanStart)
if (-not $source.Contains('TrackerRetentionInterval = Stopwatch.Frequency * 2') -or
    -not $scanBlock.Contains('timestamp - tracker.LastValidatedAt < TrackerRetentionInterval') -or
    -not $scanBlock.Contains('tracker retained after') -or
    -not $scanBlock.Contains('tracker expired after scan absence')) {
    throw 'Transient scan misses must retain a recently validated tracker and expire only after the bounded grace interval.'
}
if (-not $scanBlock.Contains('captureFailures.TryGetValue') -or
    -not $source.Contains('out string failure')) {
    throw 'Tracked scan misses must retain their exact capture rejection reason for bounded diagnostics.'
}
if (-not $scanBlock.Contains('allowProbeReservationOne: true') -or
    -not $source.Contains('rawReservation != 1') -or
    -not $source.Contains('OwnHunterReservation,') -or
    -not $source.Contains('RawReservation = rawReservation')) {
    throw 'Only behavior-neutral scan probes may accept raw reservation 1 while stable identity remains normalized to the Hunter reservation.'
}
if (-not $source.Contains('NearTargetProbeInterval = Stopwatch.Frequency / 4') -or
    -not $scanBlock.Contains('inputs.TileManhattanDistance <= 30') -or
    -not $scanBlock.Contains('tracker.LastProbeRequestedAt')) {
    throw 'Near active targets must use the bounded 250-ms probe interval without changing the far-target interval.'
}

$observationStart = $source.IndexOf('public bool TryGetObservation(', [System.StringComparison]::Ordinal)
$observationEnd = $source.IndexOf('public void ResetForMap()', $observationStart, [System.StringComparison]::Ordinal)
if ($observationStart -lt 0 -or $observationEnd -le $observationStart) {
    throw 'Active visibility observation block was not found.'
}

$observationBlock = $source.Substring($observationStart, $observationEnd - $observationStart)
if (-not $observationBlock.Contains('visible-position-changed-refresh-pending') -or
    -not $observationBlock.Contains('tracker.ObservedHunterTileX == inputs.HunterTileX') -or
    -not $observationBlock.Contains('tracker.ObservedPreyTileY == inputs.PreyTileY')) {
    throw 'A visible snapshot must be bound to the exact observed Hunter and prey tile positions.'
}
if (-not $observationBlock.Contains('stale-blocked-refresh-pending') -or
    -not $observationBlock.Contains('HunterActiveVisibilityState.Blocked')) {
    throw 'A still-valid live path must retain a known blocked result while its refresh is pending instead of failing open.'
}

$acceptedStart = $source.IndexOf('public void RecordAcceptedVanillaPath(', [System.StringComparison]::Ordinal)
$acceptedEnd = $source.IndexOf('public void ProcessNativeScan(', $acceptedStart, [System.StringComparison]::Ordinal)
if ($acceptedStart -lt 0 -or $acceptedEnd -le $acceptedStart) {
    throw 'Accepted Vanilla path-generation recording block was not found.'
}

$acceptedBlock = $source.Substring($acceptedStart, $acceptedEnd - $acceptedStart)
if (-not $acceptedBlock.Contains('generation = ++nextPathGeneration') -or
    -not $acceptedBlock.Contains('acceptedPathGenerations[hunterUnitId] = generation')) {
    throw 'A successful Vanilla path must advance and store an explicit generation.'
}

$probeStart = $source.IndexOf('private void Probe(ProbeRequest request, long timestamp)', [System.StringComparison]::Ordinal)
$probeEnd = $source.IndexOf('private Tracker GetOrReplaceTracker(', $probeStart, [System.StringComparison]::Ordinal)
if ($probeStart -lt 0 -or $probeEnd -le $probeStart) {
    throw 'Visibility probe block was not found.'
}

$probeBlock = $source.Substring($probeStart, $probeEnd - $probeStart)
if (-not $probeBlock.Contains('IsCurrentProbeRequest(request)') -or
    -not $probeBlock.Contains('tracker.PathGeneration != request.PathGeneration') -or
    -not $probeBlock.Contains('GetPathGeneration(before.HunterUnitId) != request.PathGeneration')) {
    throw 'A stale in-flight probe must not commit across a new accepted path generation.'
}
if (-not $probeBlock.Contains('active-target visibility probe sample') -or
    -not $probeBlock.Contains('hunterTile=') -or
    -not $probeBlock.Contains('preyTile=') -or
    -not $probeBlock.Contains('worldChebyshevDistance=') -or
    -not $probeBlock.Contains('behaviorMutation=False')) {
    throw 'Every bounded active-target probe must expose its exact geometry without a behavior mutation.'
}
if (-not $probeBlock.Contains('allowProbeReservationOne: true') -or
    -not $probeBlock.Contains('rawProbeReservation=') -or
    -not $probeBlock.Contains('tracker.ObservedHunterTileX = geometry.HunterTileX') -or
    -not $probeBlock.Contains('tracker.ObservedPreyTileY = geometry.PreyTileY')) {
    throw 'Probe validation and commits must preserve raw reservation diagnostics and the exact sampled tile geometry.'
}

$nearProbeStart = $nativeProbe.IndexOf('private bool TryInvokeNearVisibility(', [System.StringComparison]::Ordinal)
$nearProbeEnd = $nativeProbe.IndexOf('private static bool TryInvokeGuardedVisibility(', $nearProbeStart, [System.StringComparison]::Ordinal)
if ($nearProbeStart -lt 0 -or $nearProbeEnd -le $nearProbeStart) {
    throw 'Native near-visibility probe block was not found.'
}

$nearProbeBlock = $nativeProbe.Substring($nearProbeStart, $nearProbeEnd - $nearProbeStart)
if ($nearProbeBlock.Contains('if (wrapperResult <= 0)') -or
    -not $nearProbeBlock.Contains('if (wrapperResult == 0)') -or
    -not $nearProbeBlock.Contains('hunterToPreyResult = 0') -or
    -not $nearProbeBlock.Contains('preyToHunterResult = 0') -or
    -not $nearProbeBlock.Contains('visibilityCore') -or
    -not $nearProbeBlock.Contains('out HunterNearVisibilityGeometry geometry')) {
    throw 'Wrapper-zero probes must retain the authoritative zero result and avoid redundant core calls; positive probes still validate both directions and exact geometry.'
}

$recordStart = $runtime.IndexOf('private void RecordHunterPclMoveHereResult(', [System.StringComparison]::Ordinal)
$recordEnd = $runtime.IndexOf('private void InitializeHunterActiveTargetVisibilitySnapshot()', $recordStart, [System.StringComparison]::Ordinal)
if ($recordStart -lt 0 -or $recordEnd -le $recordStart) {
    throw 'Runtime MoveHere-result bridge was not found.'
}

$recordBlock = $runtime.Substring($recordStart, $recordEnd - $recordStart)
if (-not $recordBlock.Contains('if (moveHereResult != 0 && CanRunHunterPathfinding())') -or
    -not $recordBlock.Contains('RecordAcceptedVanillaPath(')) {
    throw 'Only a successful observed Vanilla MoveHere may advance the visibility path generation.'
}

if (-not $continuation.Contains('lastWorldVisibilityDecisions') -or
    -not $continuation.Contains('lastTileVisibilityDecisions') -or
    $continuation.Contains('lastVisibilityDecisions')) {
    throw 'World-refresh and tile-decision log throttles must remain independent.'
}
if (-not $continuation.Contains('release-distance-override-vanilla-attack-gate-ready') -or
    -not $continuation.Contains('release-distance-override-vanilla-attack-gate-deferred') -or
    $continuation.Contains('"allow-vanilla-attack"')) {
    throw 'A visible snapshot log must distinguish distance release from Vanilla locomotion-gate readiness.'
}
if (-not $continuation.Contains('ValidateDistanceContinuationHookSpan') -or
    -not $continuation.Contains('ValidateAttackGateHookSpan') -or
    -not $continuation.Contains('ValidateNoExternalDirectBranchTargetsInsideHook') -or
    -not $continuation.Contains('FlowControl.IndirectBranch') -or
    -not $continuation.Contains('hookLength != 0x13') -or
    -not $continuation.Contains('hookLength != AttackGateHookLength')) {
    throw 'Both native hook spans must be instruction-decoded and audited against external direct branch targets before installation.'
}
if (-not $continuation.Contains('AttackGateHookRva = 0x130160') -or
    -not $continuation.Contains('AttackGateHookLength = 0x14') -or
    -not $continuation.Contains('AttackGateFirstBranchTargetRva = 0x13017A') -or
    -not $continuation.Contains('placement: OverwrittenInstructionPlacement.BeforeCallback')) {
    throw 'The attack-gate handoff must use the audited 20-byte basic-block span and run only after its relocated comparisons.'
}
if (-not $continuation.Contains('TryHandoffFreshVisibleAttack') -or
    -not $continuation.Contains('visibility.PositionsMatch') -or
    -not $continuation.Contains('FreshAttackGateSnapshotLifetime') -or
    -not $continuation.Contains('context.Pointer->Rflags &= ~ZeroFlagMask')) {
    throw 'Attack-gate handoff must require a fresh position-matched visible snapshot and change only the captured zero flag.'
}

Write-Output 'Hunter active visibility snapshot tests passed: reservation-1 scan gaps are covered; near probes are position-bound; stale blocking remains conservative; both native spans and the zero-flag-only visible handoff are guarded.'
