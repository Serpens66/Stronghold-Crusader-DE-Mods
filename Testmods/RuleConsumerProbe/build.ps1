[CmdletBinding()]
param([switch]$NoInstall, [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
Write-Host ('[{0:HH:mm:ss.fff}] RuleConsumerProbe build: current working sources, installed dependencies.' -f [DateTime]::Now)
& (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -ModDirectory 'Testmods/RuleConsumerProbe' -ConsumerOnly
& (Join-Path $workspace 'Shared/Tools/Validation/Test-DependencyMetadata.ps1') -ProjectFiles (Join-Path $PSScriptRoot 'RuleConsumerProbe.csproj')
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.Extension -in '.cs','.csproj','.ps1','.bat','.json' -and $_.FullName -notmatch '[\\/](obj|BepInEx)[\\/]' }) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n' -or $text -match '\r(?!\n)') { throw "Non-CRLF file: $($file.FullName)" }
}
# A third-party-style consumer: compiles against the installed public APIShared assembly only.
& $msbuild (Join-Path $PSScriptRoot 'RuleConsumerProbe.csproj') /p:Configuration=Debug /p:ImportDirectoryBuildTargets=false ("/p:ApiSharedDir=" + (Join-Path $GameDir 'BepInEx/plugins/APIShared_Serp')) ("/p:GameDir=" + $GameDir) /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'RuleConsumerProbe compilation failed.' }
$output = Join-Path $PSScriptRoot 'BepInEx/plugins/RuleConsumerProbe_Serp'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination $output
if (-not $NoInstall) {
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before installing.' }
    . (Join-Path $workspace 'Shared/Tools/BranchBuild/BranchBuild.Common.ps1')
    $destination = Join-Path $GameDir 'BepInEx/plugins/RuleConsumerProbe_Serp'
    [void][IO.Directory]::CreateDirectory($destination)
    foreach ($name in 'RuleConsumerProbe.dll','RuleConsumerProbe.pdb','info.json') {
        Copy-Item -LiteralPath (Join-Path $output $name) -Destination $destination
        if ((Get-BranchHash (Join-Path $output $name)) -ne (Get-BranchHash (Join-Path $destination $name))) { throw "Installed hash mismatch: $name" }
    }
    Write-Host 'PASS: installed files match build.'
}
Write-Host ('[{0:HH:mm:ss.fff}] RuleConsumerProbe build complete.' -f [DateTime]::Now)
