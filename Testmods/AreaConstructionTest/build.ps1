[CmdletBinding()]
param([string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$project = Join-Path $PSScriptRoot 'AreaConstructionTest.csproj'
& (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -ModDirectory 'Testmods/AreaConstructionTest' -ConsumerOnly
& (Join-Path $workspace 'Shared/Tools/Validation/Test-DependencyMetadata.ps1') -ProjectFiles $project
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild $project /p:Configuration=Debug /p:ImportDirectoryBuildTargets=false ("/p:ApiSharedDir=" + (Join-Path $GameDir 'BepInEx/plugins/APIShared_Serp')) ("/p:GameDir=" + $GameDir) /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'AreaConstructionTest compilation failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination (Join-Path $PSScriptRoot 'BepInEx/plugins/AreaConstructionTest_Serp')
