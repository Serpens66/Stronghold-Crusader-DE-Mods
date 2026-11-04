[CmdletBinding()]
param(
    [switch]$NoInstall,
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    # Compile reference only; the testmod never ships APIShared. Defaults to the installed package.
    [string]$ApiSharedDir = ''
)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
if (-not $ApiSharedDir) { $ApiSharedDir = Join-Path $GameDir 'BepInEx/plugins/APIShared_Serp' }
Write-Host ('[{0:HH:mm:ss.fff}] UiAcceptance build: current working sources, APIShared reference {1}.' -f [DateTime]::Now, $ApiSharedDir)
& (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -ModDirectory 'Testmods/UiAcceptance' -ConsumerOnly
& (Join-Path $workspace 'Shared/Tools/Validation/Test-DependencyMetadata.ps1') -ProjectFiles (Join-Path $PSScriptRoot 'UiAcceptance.csproj')
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.Extension -in '.cs','.csproj','.ps1','.bat','.json','.md' -and $_.FullName -notmatch '[\\/](obj|BepInEx)[\\/]' }) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n' -or $text -match '\r(?!\n)') { throw "Non-CRLF file: $($file.FullName)" }
}
# Standalone test utility: compiles against installed/selected binaries, not the workspace SDK target.
& $msbuild (Join-Path $PSScriptRoot 'UiAcceptance.csproj') /p:Configuration=Debug /p:ImportDirectoryBuildTargets=false ("/p:ApiSharedDir=" + $ApiSharedDir) ("/p:GameDir=" + $GameDir) /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'UiAcceptance compilation failed.' }
$output = Join-Path $PSScriptRoot 'BepInEx/plugins/UiAcceptance_Serp'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination $output
& (Join-Path $PSScriptRoot 'verify.ps1') -GameDir $GameDir -ApiSharedDir $ApiSharedDir -AssemblyPath (Join-Path $output 'UiAcceptance.dll')
if (-not $NoInstall) {
    $target = Join-Path $GameDir 'BepInEx/plugins/UiAcceptance_Serp'
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($name in 'UiAcceptance.dll','UiAcceptance.pdb','info.json') { Copy-Item -LiteralPath (Join-Path $output $name) -Destination $target -Force }
    Write-Host "Installed: $target"
}
Write-Host ('[{0:HH:mm:ss.fff}] UiAcceptance build complete.' -f [DateTime]::Now)
