$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $PSScriptRoot 'DependencyBootstrapProbe.Tests.csproj') /nologo /verbosity:minimal /p:Configuration=Release
if ($LASTEXITCODE -ne 0) { throw 'Contract test compilation failed' }
& (Join-Path $PSScriptRoot 'bin\DependencyBootstrapProbe.Tests.exe') $game
if ($LASTEXITCODE -ne 0) { throw 'Contract tests failed' }