[CmdletBinding()]
param([switch]$NoPause, [switch]$DevDiag)
$ErrorActionPreference = 'Stop'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$env:SHCDE_DEVDIAG = if ($DevDiag) { '1' } else { '0' }
& $compiler (Join-Path $PSScriptRoot 'ProjectileTrailsTest.csproj') /t:Rebuild /p:Configuration=Release /v:minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet run --project (Join-Path $PSScriptRoot 'tests/TrailPolicy.Tests.csproj') --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
