param([string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $PSScriptRoot 'engine\EngineContract.csproj') /nologo /t:Build /verbosity:minimal "/p:GameDir=$GameDir"
if ($LASTEXITCODE) { throw 'Engine contract test build failed.' }
& (Join-Path $PSScriptRoot 'engine\bin\EngineContract.exe') $GameDir
if ($LASTEXITCODE) { throw 'Installed engine lock contract failed.' }
