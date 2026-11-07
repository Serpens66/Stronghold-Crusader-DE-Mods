[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Label,
    [string]$SavePath = 'test_stresstest5.sav',
    [int]$RunSeconds = 120,
    # Extra PerfProbe entries as "Section.Key=value;..."
    [string]$ExtraConfig = '',
    # Also build and install this mod first (working tree, no tests).
    [switch]$Build,
    # Logs the route statistics the analyzer sums up: [Diagnostics] Enabled for this run only.
    [switch]$RouteStats
)
# One automated PerfProbe run. RandomEvents and foreign test mods are parked for the run to keep the
# test world quiet and put back afterwards, also after a failure.
# Logs land in .work/a/pathfinding-build/runs. Development tooling only.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$plugins = Join-Path $game 'BepInEx\plugins'
$parked = Join-Path $game 'BepInEx\plugins_parked_pf'
$runs = Join-Path $root '.work\a\pathfinding-build\runs'
New-Item -ItemType Directory -Force -Path $runs, $parked | Out-Null
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'The game is running; nothing changed.' }
if ($Build) {
    & cmd.exe /c "`"$(Join-Path $root 'Pathfinding\build.bat')`" /source:working /notests /nopause" *>&1 | Out-File -Encoding utf8 (Join-Path $runs "$Label-build.log")
    if ($LASTEXITCODE -ne 0) { throw "Build failed; see $runs" }
}
$park = @('RandomEvents_Serp', 'PlayerControlRelayTest_Serp', 'AutoSendTest_Serp', 'TroopActionButtonTest_Serp')
$moved = @(); $probeDir = Join-Path $plugins 'PerfProbe_Serp'; $probeInstalled = $false
$modConfig = Join-Path $game 'BepInEx\config\Pathfinding_Serp.cfg'; $modConfigBytes = $null
try {
    if ($RouteStats) {
        $modConfigBytes = [IO.File]::ReadAllBytes($modConfig)
        $text = [Text.Encoding]::UTF8.GetString($modConfigBytes)
        $changed = [regex]::Replace($text, '(?ms)(^\[Diagnostics\].*?^Enabled = )\w+', '${1}true')
        if ($changed -notmatch '(?ms)^\[Diagnostics\].*?^Enabled = true') { throw 'No [Diagnostics] Enabled entry in the mod config.' }
        [IO.File]::WriteAllBytes($modConfig, [Text.Encoding]::UTF8.GetBytes($changed))
    }
    foreach ($name in $park) {
        $path = Join-Path $plugins $name
        if (Test-Path -LiteralPath $path) { Move-Item -LiteralPath $path -Destination (Join-Path $parked $name); $moved += $name }
    }
    if (-not (Test-Path -LiteralPath $probeDir)) {
        Copy-Item -LiteralPath (Join-Path $root 'Testmods\PerfProbe\BepInEx\plugins\PerfProbe_Serp') -Destination $probeDir -Recurse
        $probeInstalled = $true
    }
    $config = "Automation.PauseSeconds=1;Automation.RunSeconds=$RunSeconds;Automation.TimeoutSeconds=$($RunSeconds + 300);Automation.SettleSeconds=2;Automation.GameSpeed=250;Spikes.Enabled=true"
    if ($ExtraConfig) { $config += ';' + $ExtraConfig }
    & (Join-Path $root 'Testmods\PerfProbe\tools\Run-Automation.ps1') -Label $Label -OutputDirectory $runs -SavePath $SavePath -Config $config -TimeoutSeconds ($RunSeconds + 360)
}
finally {
    if ($null -ne $modConfigBytes) { [IO.File]::WriteAllBytes($modConfig, $modConfigBytes) }
    if ($probeInstalled) { Remove-Item -LiteralPath $probeDir -Recurse -Force }
    foreach ($name in $moved) { Move-Item -LiteralPath (Join-Path $parked $name) -Destination (Join-Path $plugins $name) }
}
& (Join-Path $PSScriptRoot 'Analyze-PathfindingRun.ps1') -LogPath (Join-Path $runs "$Label`_LogOutput.log")
