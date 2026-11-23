[CmdletBinding()]
param([switch]$NoPause, [string]$OutputPath)
$ErrorActionPreference = 'Stop'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$project = Join-Path $PSScriptRoot 'ProjectileTrailsTest.csproj'
$buildArguments = @('/t:Rebuild', '/p:Configuration=Release', '/v:minimal')
if ($OutputPath) { $buildArguments += "/p:OutputPath=$OutputPath" }
& $compiler $project @buildArguments
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet run --project (Join-Path $PSScriptRoot 'tests/TrailPolicy.Tests.csproj') --configuration Release
exit $LASTEXITCODE
