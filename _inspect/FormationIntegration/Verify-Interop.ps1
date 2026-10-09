param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$formationSources = @((Join-Path $workspace 'BugfixesAndQoL\src\UnitCommands\Formation\NativeFormationSlots.cs')) + @(Get-ChildItem -LiteralPath (Join-Path $workspace 'BugfixesAndQoL\src\Formations') -File -Filter 'FormationRuntime.*.cs' | ForEach-Object FullName)
& (Join-Path $workspace 'APIShared\tools\Validation\Verify-Interop.ps1') -GameDir $game -AdditionalSourceFiles $formationSources
if (-not $?) { throw 'Installed APIShared formation interop audit failed.' }
