[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Label,
    [Parameter(Mandatory)] [string]$OutputDirectory,
    # Feature switches as "RadarRebindSkip=true;MeritDataLockCache=false;..." (string: works with powershell.exe -File).
    [string]$Features = '',
    [string]$SavePath = 'test_dustyFPS2.sav',
    # Temporary Unity boot.config entries "key=value;..." (restored byte-exact after the run).
    [string]$BootConfig = '',
    # Unity command-line arguments (e.g. -force-d3d12); the game executable is then started directly.
    [string]$LaunchArgs = '',
    [int]$TimeoutSeconds = 420,
    # 0 disables the stall watchdog; measurement phases log at least every ~25 s.
    [int]$StallSeconds = 120
)
# Runs one automated PerfFixTest measurement: writes the [Automation]/[Features] config, starts the
# game through Steam, waits for the automation to quit the game and copies the logs and the
# screenshot before the next start overwrites them (BepInEx AppendLog is disabled by the user).
$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$config = Join-Path $game 'BepInEx\config\PerfFixTest_Serp.cfg'
$log = Join-Path $game 'BepInEx\LogOutput.log'
$playerDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition'
$shotDir = Join-Path $game 'BepInEx\PerfFixTest_shots'
$process = 'Stronghold Crusader Definitive Edition'

function Set-Value([string]$text, [string]$key, [string]$value) {
    $pattern = '(?m)^' + [regex]::Escape($key) + ' = .*$'
    if ($text -notmatch $pattern) {
        # Keys of a newer build appear only after its first start; probes may be added explicitly.
        $section = @{ SheetMergeProbe = 'Probes'; SheetMergeMidPause = 'Probes'; SheetMergeControl = 'Probes'; SheetMergeRenderCheck = 'Probes'; LodProbe = 'Probes'; QuadProbe = 'Probes'; NoesisProbe = 'Probes'; NoesisSingleMip = 'Probes'; HudProbe = 'Probes'; NoesisRebindProbe = 'Probes'; SheetMerge = 'Features' }[$key]
        if ($section) { return $text.TrimEnd() + "`r`n`r`n[" + $section + "]`r`n" + $key + ' = ' + $value + "`r`n" }
        throw "Config key missing: $key"
    }
    return [regex]::Replace($text, $pattern, ($key + ' = ' + $value))
}

if (Get-Process -Name $process -ErrorAction SilentlyContinue) { throw 'The game is already running.' }
# Steam refuses a new start while the previous instance is still shutting down (crash handler, Steam
# app state): wait until the crash handler is gone and keep a quiet gap before starting again.
$quiet = (Get-Date).AddSeconds(60)
while ((Get-Process -Name 'UnityCrashHandler64' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $quiet) { Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 10
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$text = [IO.File]::ReadAllText($config)
foreach ($pair in ($Features -split ';' | Where-Object { $_ -match '=' })) { $kv = $pair -split '=', 2; $text = Set-Value $text $kv[0].Trim() $kv[1].Trim().ToLowerInvariant() }
$text = Set-Value $text 'Enabled' 'true'
$text = Set-Value $text 'RunLabel' $Label
$text = Set-Value $text 'SavePath' $SavePath
[IO.File]::WriteAllText($config, $text, (New-Object Text.UTF8Encoding($false)))
# Screenshots of an earlier run with the same label are deleted one by one (exact names only).
$shotNames = @('_pause.png', '_pause_before.png', '_pause_after.png') | ForEach-Object { Join-Path $shotDir ($Label + $_) }
foreach ($shot in $shotNames) { if (Test-Path -LiteralPath $shot) { [IO.File]::Delete($shot) } }

$bootPath = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\boot.config'
$bootOriginal = [IO.File]::ReadAllBytes($bootPath)
if ($BootConfig) {
    $bootText = [Text.Encoding]::UTF8.GetString($bootOriginal)
    foreach ($pair in ($BootConfig -split ';' | Where-Object { $_ -match '=' })) {
        $kv = $pair -split '=', 2; $line = $kv[0].Trim() + '=' + $kv[1].Trim()
        $pattern = '(?m)^' + [regex]::Escape($kv[0].Trim()) + '=.*$'
        if ($bootText -match $pattern) { $bootText = [regex]::Replace($bootText, $pattern, $line) } else { $bootText = $bootText.TrimEnd("`r", "`n") + "`n" + $line + "`n" }
    }
    [IO.File]::WriteAllText($bootPath, $bootText, (New-Object Text.UTF8Encoding($false)))
}
$started = Get-Date
$stalled = $false
try {
    if ($LaunchArgs) { Start-Process -FilePath (Join-Path $game ($process + '.exe')) -ArgumentList $LaunchArgs -WorkingDirectory $game }
    else { Start-Process 'steam://rungameid/3024040' }
    $deadline = $started.AddSeconds($TimeoutSeconds)
    $retryAt = (Get-Date).AddSeconds(45)
    while (-not (Get-Process -Name $process -ErrorAction SilentlyContinue)) {
        if ((Get-Date) -gt $deadline) { throw 'The game did not start.' }
        if (-not $LaunchArgs -and $retryAt -and (Get-Date) -gt $retryAt) { $retryAt = $null; Start-Process 'steam://rungameid/3024040' }
        Start-Sleep -Seconds 2
    }
    while (Get-Process -Name $process -ErrorAction SilentlyContinue) {
        if ((Get-Date) -gt $deadline) { throw "Timeout after $TimeoutSeconds s; the game is still running." }
        # A test instance that writes no log line for StallSeconds is hung (e.g. a deadlocked probe):
        # stop this automated instance so the series does not wait for the full timeout.
        if ($StallSeconds -gt 0 -and (Test-Path -LiteralPath $log) -and
            ((Get-Date) - (Get-Item -LiteralPath $log).LastWriteTime).TotalSeconds -gt $StallSeconds -and
            ((Get-Date) - $started).TotalSeconds -gt $StallSeconds) {
            Copy-Item -LiteralPath $log -Destination (Join-Path $OutputDirectory ($Label + '_stalled_LogOutput.log'))
            Get-Process -Name $process -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
            # Continue with log copy and config reset below; the summary reports the stall.
            $stalled = $true
            break
        }
        Start-Sleep -Seconds 2
    }
    Start-Sleep -Seconds 2
}
finally {
    # boot.config is read only at engine start; restore the original bytes exactly.
    if ($BootConfig) { [IO.File]::WriteAllBytes($bootPath, $bootOriginal) }
}
Copy-Item -LiteralPath $log -Destination (Join-Path $OutputDirectory ($Label + '_LogOutput.log'))
Copy-Item -LiteralPath (Join-Path $playerDir 'Player.log') -Destination (Join-Path $OutputDirectory ($Label + '_Player.log'))
foreach ($shot in $shotNames) { if (Test-Path -LiteralPath $shot) { Copy-Item -LiteralPath $shot -Destination $OutputDirectory } }

# Leave the installation in its normal state: automation off, fixes on.
$text = [IO.File]::ReadAllText($config)
$text = Set-Value $text 'Enabled' 'false'
foreach ($key in 'RadarRebindSkip', 'MeritDataLockCache', 'InterpolateEventReuse', 'SheetMerge') { if ($text -match ('(?m)^' + $key + ' = ')) { $text = Set-Value $text $key 'true' } }
foreach ($probe in 'SheetMergeProbe', 'SheetMergeMidPause', 'SheetMergeControl', 'SheetMergeRenderCheck', 'LodProbe', 'QuadProbe', 'NoesisProbe', 'NoesisSingleMip', 'HudProbe', 'NoesisRebindProbe') { if ($text -match ('(?m)^' + $probe + ' = ')) { $text = Set-Value $text $probe 'false' } }
$text = Set-Value $text 'PauseSeconds' '25'
$text = Set-Value $text 'RunSeconds' '25'
[IO.File]::WriteAllText($config, $text, (New-Object Text.UTF8Encoding($false)))

$gfx = Select-String -LiteralPath (Join-Path $OutputDirectory ($Label + '_Player.log')) -Pattern 'GfxDevice: creating|Version:  Direct3D|Vulkan API|Graphics API' | ForEach-Object { $_.Line.Trim() }
$markers = Select-String -LiteralPath (Join-Path $OutputDirectory ($Label + '_LogOutput.log')) -Pattern 'AUTOMATION .*(result|framestats|screenshot|probe=|v7 )' | ForEach-Object Line
"run=$Label duration=" + [int]((Get-Date) - $started).TotalSeconds + 's' + $(if ($stalled) { " STALLED: no log output for $StallSeconds s, test instance stopped" } else { '' })
$gfx
$markers
