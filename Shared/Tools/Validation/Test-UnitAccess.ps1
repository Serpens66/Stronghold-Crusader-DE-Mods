[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$unitAccessWatch = [Diagnostics.Stopwatch]::StartNew()
Write-Host '[Preflight] Building and running production UnitAccess contract tests...'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$project = Join-Path $workspace '_inspect\UnitIdAccess\Tests\UnitAccess.Tests.csproj'
& dotnet run --project $project
if ($LASTEXITCODE -ne 0) { throw 'Production UnitAccess contract tests failed.' }
Write-Host '[Preflight] Scanning workspace UnitAccess usage...'
& dotnet run --no-build --project $project -- --scan
if ($LASTEXITCODE -ne 0) { throw 'Workspace UnitAccess regression failed.' }
& (Join-Path $PSScriptRoot 'Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace permanent runtime-hook regression failed.' }
$unitAccessWatch.Stop()
Write-Host ('[Preflight] UnitAccess checks completed in {0:N1}s.' -f $unitAccessWatch.Elapsed.TotalSeconds)
