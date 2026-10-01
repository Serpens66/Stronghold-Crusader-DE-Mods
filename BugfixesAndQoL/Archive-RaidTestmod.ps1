$ErrorActionPreference='Stop'
$workspace=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$game='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Game running; cannot retire loaded testmod.'}
$installed=Join-Path $game 'BepInEx\plugins\BugfixesAndQoL_Serp'
$manifest=[IO.File]::ReadAllText((Join-Path $installed 'info.json')) | ConvertFrom-Json
if($manifest.Version -ne '1.0.174'){throw 'Mainmod installation must be 1.0.174 before testmod archive.'}
$localDll=Join-Path $PSScriptRoot 'BepInEx\plugins\BugfixesAndQoL_Serp\BugfixesAndQoL.dll'
$installedDll=Join-Path $installed 'BugfixesAndQoL.dll'
function Read-Sha256([string]$path) {
 $sha=[Security.Cryptography.SHA256]::Create()
 try { return [BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($path))).Replace('-','') }
 finally { $sha.Dispose() }
}
if((Read-Sha256 $localDll) -cne (Read-Sha256 $installedDll)){throw 'Mainmod installation hash differs; leave testmod untouched.'}
$source=[IO.Path]::GetFullPath((Join-Path $game 'BepInEx\plugins\RaidRetargetDiagnostic_Serp'))
if(!(Test-Path -LiteralPath $source)){Write-Host 'PASS: no active RaidRetargetDiagnostic testmod installed.';exit 0}
$archiveRoot=[IO.Path]::GetFullPath((Join-Path $workspace '.inspect\RaidRetargetEvaluation'))
$destination=[IO.Path]::GetFullPath((Join-Path $archiveRoot ('ArchivedInstalledTestmod-0.1.1-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))))
if(!$destination.StartsWith($workspace+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
   $source -cne ([IO.Path]::GetFullPath('E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\RaidRetargetDiagnostic_Serp'))){throw 'Archive path boundary failed.'}
if(Test-Path -LiteralPath $destination){throw 'Archive destination exists.'}
Move-Item -LiteralPath $source -Destination $destination
if(Test-Path -LiteralPath $source){throw 'Testmod remains active after archive.'}
if(!(Test-Path -LiteralPath (Join-Path $destination 'RaidRetargetDiagnostic.dll'))){throw 'Archived testmod DLL missing.'}
Write-Host "PASS: retired active testmod; preserved archive: $destination"
