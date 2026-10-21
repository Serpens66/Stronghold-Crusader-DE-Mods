[CmdletBinding()]
param(
    # Mod folder relative to the workspace (e.g. BugfixesAndQoL); shared runtime sources are always included.
    [string]$ModDirectory,
    [string]$Root = ''
)
$ErrorActionPreference = 'Stop'
# PowerShell 5.1 has no $PSScriptRoot inside parameter defaults.
if (-not $Root) { $Root = Join-Path $PSScriptRoot '..\..\..' }
. (Join-Path $PSScriptRoot 'DevDiagnostics.Common.ps1')
try {
    $found = @(Get-DevDiagnosticSources -Root $Root -ModDirectory $ModDirectory)
    $scope = if ($ModDirectory) { $ModDirectory + ' + Shared runtime' } else { 'Shared runtime' }
    if ($found.Count -eq 0) { Write-Host "DEVDIAG: no measurement points ($scope)."; exit 0 }
    Write-Host "DEVDIAG: $($found.Count) retained diagnostic source item(s), enabled only by /devdiag ($scope):"
    foreach ($item in $found) { Write-Host ('  ' + $item.Path + ':' + $item.Line + ' ' + $item.Text) }
    exit 0
} catch {
    Write-Host ('ERROR: ' + $_.Exception.Message)
    exit 1
}
