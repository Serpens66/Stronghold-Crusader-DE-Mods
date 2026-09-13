$ErrorActionPreference = 'Stop'
$path = [IO.Path]::GetFullPath('Testmods/MoatMove/src/FriendlyMoatMovementRuntime.cs')
$source = [IO.File]::ReadAllText($path)
$start = $source.IndexOf('        private bool TryFindRequiredFriendlyCompletedMoatRouteForPlan(')
$start = $source.IndexOf('        private bool TryFindRequiredFriendlyCompletedMoatRouteForPlan(', $start + 1)
$end = $source.IndexOf('        private ', $start + 20)
if ($start -lt 0 -or $end -le $start) { throw 'route method boundaries' }
$method = $source.Substring($start, $end - $start)
function Replace-Exact([string]$old, [string]$replacement) {
    $old = $old.Replace("`r`n","`n").Replace("`n","`r`n")
    $replacement = $replacement.Replace("`r`n","`n").Replace("`n","`r`n")
    if (-not $script:method.Contains($old)) { throw "Missing replacement: $old" }
    $script:method = $script:method.Replace($old, $replacement)
}
# Partial evaluation of the Precise branch; the new Fast return precedes it.
Replace-Exact 'bool requiredOnly = RequiredOnlyMode;' 'const bool requiredOnly = false;'
$a = $method.IndexOf('                bool groundSeparationProven =')
$b = $method.IndexOf('                    if (friendlyReachable)', $a)
if ($a -lt 0 -or $b -le $a) { throw 'legacy bridge branch boundaries' }
$replacement = @'
                if (!groundReachable)
                {
                    long requiredSearchStarted = Stopwatch.GetTimestamp();
                    WeightedMoatEncodedRoute encoded = default;
                    friendlyReachable = hasCost
                        ? weightedMoatRoutePlanner.TryBuildEncoded(playerId, startX, startY,
                            plan.TargetX, plan.TargetY, routeCost, allowReservedTarget,
                            out friendly, out encoded)
                        : weightedMoatRoutePlanner.TryBuildReachabilityEncoded(playerId, startX, startY,
                            plan.TargetX, plan.TargetY, allowReservedTarget, out friendly, out encoded);
'@
$replacement = $replacement.Replace("`r`n","`n").Replace("`n","`r`n") + "`r`n"
$method = $method.Substring(0,$a) + $replacement + $method.Substring($b)
$source = $source.Substring(0,$start) + $method + $source.Substring($end)
# Remove the single expiring authority. Deferred work is now owned per identity.
$a = $source.IndexOf("                try`r`n                {`r`n                    CaptureDeferredFastMoveScope(command);")
$b = $source.IndexOf('                activeMoveCommand = null;', $a)
if ($a -lt 0 -or $b -le $a) { throw 'legacy deferred scope boundaries' }
$source = $source.Substring(0,$a) + $source.Substring($b)
$source = $source.Replace("            ClearDeferredFastMoveScope();`r`n", '')
$source = $source.Replace('            InvalidateFastMoatData();', '            ResetFastCommandMap();')
# Remove undirected bridge vetoes, leaving the exact directed candidate search.
foreach ($needle in @('                if (RequiredOnlyMode &&' + "`r`n" + '                    !HasFastFriendlyMoatBridgeForCells')) {
    while (($a = $source.IndexOf($needle)) -ge 0) {
        $b = $source.IndexOf("                }`r`n", $a)
        if ($b -lt $a) { throw 'legacy candidate veto boundary' }
        $source = $source.Remove($a, $b + 19 - $a)
    }
}
[IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))
if (-not [string]::Equals([IO.File]::ReadAllText($path),$source,[StringComparison]::Ordinal)) { throw 'source write mismatch' }
