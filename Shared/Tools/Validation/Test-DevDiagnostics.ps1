[CmdletBinding()]
param(
    # Mod folder relative to the workspace (e.g. BugfixesAndQoL); shared runtime sources are always included.
    [string]$ModDirectory,
    [string]$Root = '',
    # Fail when any measurement point remains (main/release); otherwise list them for removal.
    [switch]$Enforce
)
$ErrorActionPreference = 'Stop'
# PowerShell 5.1 has no $PSScriptRoot inside parameter defaults.
if (-not $Root) { $Root = Join-Path $PSScriptRoot '..\..\..' }
. (Join-Path $PSScriptRoot 'DevDiagnostics.Common.ps1')
try {
    $found = @(Get-DevDiagnosticSources -Root $Root -ModDirectory $ModDirectory)
    $scope = if ($ModDirectory) { $ModDirectory + ' + Shared runtime' } else { 'Shared runtime' }
    if ($found.Count -eq 0) { Write-Host "DEVDIAG: no measurement points ($scope)."; exit 0 }
    Write-Host "DEVDIAG: $($found.Count) measurement point(s) to remove before release ($scope):"
    foreach ($item in $found) { Write-Host ('  ' + $item.Path + ':' + $item.Line + ' ' + $item.Text) }
    if ($Enforce) { Write-Host 'ERROR: DEVDIAG measurement points are not allowed in this build.'; exit 1 }
    exit 0
} catch {
    Write-Host ('ERROR: ' + $_.Exception.Message)
    exit 1
}
