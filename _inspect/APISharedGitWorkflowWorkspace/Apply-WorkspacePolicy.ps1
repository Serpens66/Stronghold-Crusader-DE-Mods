[CmdletBinding()]
param([string]$Workspace, [switch]$Apply)
$ErrorActionPreference='Stop'
if (-not $Workspace) { $Workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..')) }
. (Join-Path $PSScriptRoot 'APIShared/tools/BuildProof.ps1')
$manifest=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'workspace-changes.json')) | ConvertFrom-Json
$workspaceRoot=[IO.Path]::GetFullPath($Workspace).TrimEnd('\','/')
$candidates=@()
foreach ($entry in $manifest.Files) {
    $target=[IO.Path]::GetFullPath((Join-Path $workspaceRoot $entry.Path))
    if (-not $target.StartsWith($workspaceRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Workspace patch path escapes its root.' }
    $prepared=Join-Path $PSScriptRoot $entry.Path
    if ((Get-ApiFileHash $prepared) -cne $entry.PreparedSha256) { throw "Prepared file changed: $($entry.Path)" }
    $actual=Get-ApiFileHash $target
    if ($actual -cne $entry.OriginalSha256 -and $actual -cne $entry.PreparedSha256) {
        throw "Workspace file changed since preparation; reconcile manually instead of overwriting: $($entry.Path)"
    }
    $text=[IO.File]::ReadAllText($prepared)
    if ($text -match '(?<!\r)\n') { throw "Prepared text is not CRLF: $($entry.Path)" }
    $candidates += [pscustomobject]@{Path=$target;Text=$text;Unchanged=($actual -ceq $entry.PreparedSha256)}
    Write-Host "$($entry.Path): $(if($actual -ceq $entry.PreparedSha256){'already applied'}else{'ready'})"
}
if (-not $Apply) { Write-Host 'READ ONLY: active workspace files unchanged.'; return }
foreach ($candidate in $candidates) {
    if ($candidate.Unchanged) { continue }
    [IO.File]::WriteAllText($candidate.Path,$candidate.Text,[Text.UTF8Encoding]::new($false))
    if (-not [string]::Equals($candidate.Text,[IO.File]::ReadAllText($candidate.Path),[StringComparison]::Ordinal)) { throw 'Workspace file roundtrip failed.' }
}
Write-Host 'PASS: prepared workspace tooling applied. No Git checkout, submodule pointer, commit, build or release changed.'
