[CmdletBinding()]
param([Parameter(Mandatory)][string]$GameDirectory)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\Shared\Tools\Release\Release.Common.ps1')
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before gate migration.' }
$bep = [IO.Path]::GetFullPath((Join-Path $GameDirectory 'BepInEx'))
$plugins = Join-Path $bep 'plugins'
$legacy = [IO.Path]::GetFullPath((Join-Path $plugins 'EnemyGatePathfindingTest_Serp\EnemyGatePathfindingTest.dll'))
$copies = @(Get-ChildItem -LiteralPath $plugins -Filter 'EnemyGatePathfindingTest.dll' -File -Recurse)
foreach ($copy in $copies) { if ($copy.FullName -ne $legacy) { throw ('Unexpected legacy gate DLL location; refusing duplicate installation: ' + $copy.FullName) } }
if (-not (Test-Path -LiteralPath $legacy)) { Write-Host 'Legacy gate test DLL already inactive.'; exit 0 }
$metadata = Get-Content -LiteralPath (Join-Path ([IO.Path]::GetDirectoryName($legacy)) 'info.json') -Raw | ConvertFrom-Json
if ($metadata.GUID -ne 'EnemyGatePathfindingTest_Serp') { throw 'Legacy gate package identity mismatch.' }
$hash = Get-Sha256Hex -Path $legacy
$destination = [IO.Path]::GetFullPath((Join-Path $bep ('disabledmods\GateMigration\' + $hash + '\EnemyGatePathfindingTest.dll')))
if (-not $legacy.StartsWith($plugins + '\',[StringComparison]::OrdinalIgnoreCase) -or
    -not $destination.StartsWith((Join-Path $bep 'disabledmods\GateMigration') + '\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Gate migration target escapes its approved directory.' }
if (Test-Path -LiteralPath $destination) { throw 'A same-hash backup already exists while the legacy DLL is active; inspect before replacing it.' }
$null = New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force
Move-Item -LiteralPath $legacy -Destination $destination
if ((Get-Sha256Hex -Path $destination) -ne $hash -or (Test-Path -LiteralPath $legacy)) { throw 'Legacy gate DLL deactivation verification failed.' }
Write-Host ('Legacy gate test DLL deactivated and preserved: ' + $destination + '; SHA256=' + $hash)
