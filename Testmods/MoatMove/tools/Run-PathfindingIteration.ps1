[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Label,
    # Rebuild BugfixesAndQoL first and commit its DLL/PDB on the current branch (MoatMove compiles
    # against the tracked BugfixesAndQoL.dll).
    [switch]$BuildBugfixes,
    [switch]$SkipMoatMove,
    [int]$RunSeconds = 180,
    [string]$SavePath = 'test_stresstest4.sav',
    [string]$ExtraConfig = ''
)
# One pathfinding test iteration of the dev/pathfinding line: build and install from this
# working tree (/source:working, never the main folder), then one automated PerfProbe stress run.
# Logs land in Testmods/MoatMove/.local/runs. Development tooling only.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$runs = Join-Path $root 'Testmods\MoatMove\.local\runs'
New-Item -ItemType Directory -Force -Path $runs | Out-Null
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'The game is running.' }

function Invoke-Build([string]$driver, [string]$arguments, [string]$logName) {
    $log = Join-Path $runs $logName
    & cmd.exe /c "`"$driver`" $arguments" *>&1 | Out-File -Encoding utf8 $log
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $driver (log $log)" }
    Write-Host "built: $driver"
}

if ($BuildBugfixes) {
    # Known stale develop test (Test-ModOptionsPerformance) blocks full test mode; relevant
    # BugfixesAndQoL tests run separately before an iteration.
    Invoke-Build (Join-Path $root 'BugfixesAndQoL\build.bat') '/source:working /tests:none /nopause' "$Label-bfq-build.log"
    $installed = Join-Path $game 'BepInEx\plugins\BugfixesAndQoL_Serp'
    $tracked = Join-Path $root 'BugfixesAndQoL\BepInEx\plugins\BugfixesAndQoL_Serp'
    foreach ($name in 'BugfixesAndQoL.dll', 'BugfixesAndQoL.pdb') { Copy-Item -LiteralPath (Join-Path $installed $name) -Destination (Join-Path $tracked $name) -Force }
    & git -C $root add -- 'BugfixesAndQoL/BepInEx/plugins/BugfixesAndQoL_Serp/BugfixesAndQoL.dll' 'BugfixesAndQoL/BepInEx/plugins/BugfixesAndQoL_Serp/BugfixesAndQoL.pdb'
    & git -C $root diff --cached --quiet
    if ($LASTEXITCODE -ne 0) { & git -C $root commit -q -m "Pathfinding dev: BugfixesAndQoL build for $Label`n`nCo-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>" }
}
if (-not $SkipMoatMove) { Invoke-Build (Join-Path $root 'Testmods\MoatMove\build.bat') '/source:working /nopause' "$Label-moat-build.log" }

$config = 'Automation.PauseSeconds=1;Automation.RunSeconds=' + $RunSeconds + ';Automation.TimeoutSeconds=' + ($RunSeconds + 420) +
    ';Automation.SettleSeconds=2;Sampler.Enabled=true;Sampler.SimOutsideRun=true;Spikes.Enabled=true'
if ($ExtraConfig) { $config += ';' + $ExtraConfig }
& (Join-Path $root 'Testmods\PerfProbe\tools\Run-Automation.ps1') -Label $Label -OutputDirectory $runs -SavePath $SavePath -Config $config -TimeoutSeconds ($RunSeconds + 540)
