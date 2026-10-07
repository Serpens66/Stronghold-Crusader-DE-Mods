[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$project = Join-Path $workspace '_inspect\UnitIdAccess\Tests\UnitAccess.Tests.csproj'
& dotnet run --project $project
if ($LASTEXITCODE -ne 0) { throw 'Production UnitAccess contract tests failed.' }
& dotnet run --no-build --project $project -- --scan
if ($LASTEXITCODE -ne 0) { throw 'Workspace UnitAccess regression failed.' }
& (Join-Path $PSScriptRoot 'Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace permanent runtime-hook regression failed.' }
