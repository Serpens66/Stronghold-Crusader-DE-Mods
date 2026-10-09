param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
& (Join-Path $workspace 'APIShared\tools\Validation\Verify-Interop.ps1') -GameDir $game
if (-not $?) { throw 'Installed APIShared formation interop audit failed.' }
