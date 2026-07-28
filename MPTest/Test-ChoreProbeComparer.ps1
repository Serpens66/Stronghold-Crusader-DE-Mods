[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$comparer = Join-Path $root 'Compare-ChoreProbeLogs.ps1'
$hostFixture = Join-Path $root 'tests\fixtures\ComprehensiveHost.log'
$clientFixture = Join-Path $root 'tests\fixtures\ComprehensiveClient.log'

& $comparer `
    -HostLog $hostFixture `
    -ClientLog $clientFixture `
    -Comprehensive `
    -MinimumRequests 2

if ($LASTEXITCODE -ne 0) {
    throw "Positive comparer fixture failed with exit code $LASTEXITCODE."
}

& powershell.exe `
    -NoProfile `
    -ExecutionPolicy Bypass `
    -File $comparer `
    -HostLog $hostFixture `
    -ClientLog $clientFixture `
    -Comprehensive `
    -MinimumRequests 3 *> $null

if ($LASTEXITCODE -ne 1) {
    throw "Negative comparer fixture returned $LASTEXITCODE instead of 1."
}

Write-Host 'Chore probe comparer self-test PASSED.' -ForegroundColor Green
$global:LASTEXITCODE = 0
