[CmdletBinding()]
param([switch]$NoInstall, [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$csc = Join-Path (Split-Path $msbuild) 'Roslyn/csc.exe'
Write-Host ('[{0:HH:mm:ss.fff}] AutoLoadSave build: current working sources, installed dependencies.' -f [DateTime]::Now)
& (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -ModDirectory 'Testmods/AutoLoadSave' -ConsumerOnly
& (Join-Path $workspace 'Shared/Tools/Validation/Test-DependencyMetadata.ps1') -ProjectFiles (Join-Path $PSScriptRoot 'AutoLoadSave.csproj')
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.Extension -in '.cs','.csproj','.ps1','.bat','.json','.cfg' -and $_.FullName -notmatch '[\\/](obj|BepInEx)[\\/]' }) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n' -or $text -match '\r(?!\n)') { throw "Non-CRLF file: $($file.FullName)" }
}
. (Join-Path $workspace 'Shared/Tools/BranchBuild/BranchBuild.Common.ps1')
$testDirectory = New-ShortWorkDirectory -Root $workspace -Kind t -Name autoload -Identity 'LoadRequest production-class tests'
& $csc /nologo /target:exe ("/out:" + (Join-Path $testDirectory 'Tests.exe')) (Join-Path $PSScriptRoot 'src/LoadRequest.cs') (Join-Path $PSScriptRoot 'tests/LoadRequestTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& (Join-Path $testDirectory 'Tests.exe') $testDirectory
if ($LASTEXITCODE -ne 0) { throw 'LoadRequest tests failed.' }
# This standalone test utility compiles against installed binaries. Do not import
# the workspace target which selects/requires a locally built APIShared SDK.
& $msbuild (Join-Path $PSScriptRoot 'AutoLoadSave.csproj') /p:Configuration=Debug /p:ImportDirectoryBuildTargets=false ("/p:ApiSharedDir=" + (Join-Path $GameDir 'BepInEx/plugins/APIShared_Serp')) ("/p:GameDir=" + $GameDir) /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'AutoLoadSave compilation failed.' }
$output = Join-Path $PSScriptRoot 'BepInEx/plugins/AutoLoadSave_Serp'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination $output
& (Join-Path $PSScriptRoot 'verify.ps1') -GameDir $GameDir -AssemblyPath (Join-Path $output 'AutoLoadSave.dll')
if (-not $NoInstall) {
    & (Join-Path $PSScriptRoot 'install.ps1') -GameDir $GameDir
}
Write-Host ('[{0:HH:mm:ss.fff}] AutoLoadSave build complete.' -f [DateTime]::Now)
