$ErrorActionPreference = 'Stop'
$mod = [IO.Path]::GetFullPath('Testmods/MoatMove')
$reasons = [ordered]@{
    'FriendlyMoatMovementRuntime.cs' = 'Fast routing dispatch, removal of legacy Fast branches, pending scheduler initialization and group distribution retention; Precise branch preserved.'
    'NativeMovementRecovery.cs' = 'Actual topology observers and identity-bound native recovery without the expiring legacy permit.'
    'NativeFormationSlots.cs' = 'Fast-only coarse selector before unchanged Precise selection.'
    'MovementPathPublication.cs' = 'Ready Fast paths publish directly with the same native mode/variant, audit and rollback contracts.'
    'MovementSearchContext.cs' = 'Fast terminal work prefixes use the new field; Precise delegate remains unchanged.'
    'FastRouteField.cs' = 'New directed unweighted field with resumable frontier, multiple roots and direct packed encoding.'
    'FastCommandQueue.cs' = 'New per-unit identity ownership and versioned binary save format.'
    'FastRoutePool.cs' = 'New bounded pinned-field pool and dependency invalidation.'
    'FastTraversalCache.cs' = 'New actual-transition cache independent of native search stamps.'
    'FastMoatRouting.cs' = 'New ground-first shared Fast route integration.'
    'FastMovementScheduler.cs' = 'New deterministic pending commands, native replay, grouping and save/load integration.'
    'FastGroupDistribution.cs' = 'New shared anchor, radius-eight distribution and identity-bound suffix retention.'
    'FastIntegration.cs' = 'New directed attack fields, separate cursor state and metrics; replaces FastMoatBridge.cs.'
    'MoatMovePlugin.cs' = 'Preserved concurrent user-owned GameplaySessionLifecycle integration.'
}
$files = @(foreach ($name in $reasons.Keys) {
    [ordered]@{ file = $name; sha256 = (Get-FileHash -LiteralPath (Join-Path $mod "src/$name") -Algorithm SHA256).Hash; reason = $reasons[$name] }
})
$current = Get-Content -LiteralPath '_inspect/CrusaderDE-Native-Baseline/CURRENT.json' -Raw | ConvertFrom-Json
$record = [ordered]@{
    schemaVersion = 1
    nativeHash = $current.currentNativeHash
    extenderCommit = '5f02af6d074af7c741ebdaaccb48add39eba1bf4'
    deleted = @('FastMoatBridge.cs')
    files = $files
}
$target = Join-Path $mod 'FAST_PROVENANCE.json'
$expected = ($record | ConvertTo-Json -Depth 6).Replace("`r`n","`n").Replace("`n","`r`n") + "`r`n"
[IO.File]::WriteAllText($target,$expected,[Text.UTF8Encoding]::new($false))
if (-not [string]::Equals([IO.File]::ReadAllText($target),$expected,[StringComparison]::Ordinal)) { throw 'Provenance readback mismatch' }
Write-Output "Wrote explicit Fast source inventory: $($files.Count) files, one removed legacy source."
