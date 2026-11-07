param([switch]$FailOnDrift)
$ErrorActionPreference = 'Stop'
# Lists source-mod files that still exist and changed since they were copied into this mod, so the
# changes can be ported. A removed origin is the finished state of the migration, not a failure.
# Blobs compare exact file contents, not paths.
$modDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace = [IO.Path]::GetFullPath((Join-Path $modDir '..'))
$map = Get-Content -LiteralPath (Join-Path $modDir 'migration\source-map.json') -Raw | ConvertFrom-Json
$changed = @(); $removed = 0
foreach ($entry in $map.entries) {
    $origin = Join-Path $workspace $entry.origin
    if (-not (Test-Path -LiteralPath $origin -PathType Leaf)) { $removed++; continue }
    $blob = (& git -C $workspace hash-object -- $entry.origin).Trim()
    if ($blob -cne $entry.originBlob) { $changed += $entry.origin }
}
Write-Output ("Checked {0} origins: {1} changed, {2} removed." -f @($map.entries).Count, $changed.Count, $removed)
foreach ($item in $changed) { Write-Output "CHANGED $item" }
if ($FailOnDrift -and $changed.Count -gt 0) { exit 1 }
