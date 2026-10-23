[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$Label,
    [Parameter(Mandatory)] [string]$OutputDirectory,
    # Extra PerfProbe config entries as "Section.Key=value;..." (e.g. "Automation.RunSeconds=60;Sampler.Enabled=true").
    [string]$Config = '',
    [string]$SavePath = 'test_dustyFPS2.sav',
    # Baseline run: PerfProbe installs nothing except the automation ([Automation] AutomationOnly).
    [switch]$AutomationOnly,
    # Temporary Unity boot.config entries "key=value;..." (restored byte-exact after the run).
    [string]$BootConfig = '',
    # Unity command-line arguments (e.g. -force-d3d12); the game executable is then started directly.
    [string]$LaunchArgs = '',
    [int]$TimeoutSeconds = 420,
    # 0 disables the stall watchdog; measurement phases log at least every ~25 s.
    [int]$StallSeconds = 120,
    # Optional processor affinity mask for the game process (e.g. 0xFFFF = first 16 logical CPUs).
    # Makes runs comparable on CPUs with unequal core complexes (7950X3D: V-cache on one CCD).
    [long]$AffinityMask = 0
)
# Runs one automated PerfProbe measurement (DEVDIAG build of PerfProbe required): writes the
# [Automation] section and the requested config entries, starts the game through Steam, waits for
# the automation to quit the game and copies the logs and the screenshot before the next start
# overwrites them (BepInEx AppendLog is disabled by the user).
$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$configPath = Join-Path $game 'BepInEx\config\PerfProbe_Serp.cfg'
$log = Join-Path $game 'BepInEx\LogOutput.log'
$playerDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition'
$shotDir = Join-Path $game 'BepInEx\PerfProbe_shots'
$process = 'Stronghold Crusader Definitive Edition'

# Section-aware: PerfProbe uses the key "Enabled" in several sections. A missing key or section is
# appended (keys of a newer build appear in the file only after its first start). BepInEx writes the
# file with LF; the file's own line ending is kept, and values are inserted literally (no regex '$').
function Set-CfgValue([string]$text, [string]$section, [string]$key, [string]$value) {
    $nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $line = $key + ' = ' + $value
    $sectionMatch = [regex]::Match($text, '(?m)^\[' + [regex]::Escape($section) + '\][^\S\n]*(\n|$)')
    if (-not $sectionMatch.Success) { return $text.TrimEnd() + $nl + $nl + '[' + $section + ']' + $nl + $line + $nl }
    $headEnd = $sectionMatch.Index + $sectionMatch.Length
    $rest = $text.Substring($headEnd)
    $next = [regex]::Match($rest, '(?m)^\[')
    $body = if ($next.Success) { $rest.Substring(0, $next.Index) } else { $rest }
    $tail = if ($next.Success) { $rest.Substring($next.Index) } else { '' }
    $existing = [regex]::Match($body, '(?m)^' + [regex]::Escape($key) + ' = [^\r\n]*')
    if ($existing.Success) { $body = $body.Substring(0, $existing.Index) + $line + $body.Substring($existing.Index + $existing.Length) }
    elseif ($sectionMatch.Groups[1].Value.Length -eq 0) { $body = $nl + $line + $nl + $body }
    else { $body = $line + $nl + $body }
    return $text.Substring(0, $headEnd) + $body + $tail
}

if (Get-Process -Name $process -ErrorAction SilentlyContinue) { throw 'The game is already running.' }
# Steam refuses a new start while the previous instance is still shutting down (crash handler, Steam
# app state): wait until the crash handler is gone and keep a quiet gap before starting again.
$quiet = (Get-Date).AddSeconds(60)
while ((Get-Process -Name 'UnityCrashHandler64' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $quiet) { Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 10
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$text = if (Test-Path -LiteralPath $configPath) { [IO.File]::ReadAllText($configPath) } else { '' }
foreach ($pair in ($Config -split ';' | Where-Object { $_ -match '=' })) {
    $kv = $pair -split '=', 2
    $sectionKey = $kv[0].Trim() -split '\.', 2
    if ($sectionKey.Count -ne 2) { throw "Config entry needs Section.Key: $pair" }
    # Only booleans are normalized (BepInEx writes true/false); paths and labels stay as given.
    $value = $kv[1].Trim()
    if ($value -in @('true', 'false')) { $value = $value.ToLowerInvariant() }
    $text = Set-CfgValue $text $sectionKey[0] $sectionKey[1] $value
}
$text = Set-CfgValue $text 'Automation' 'Enabled' 'true'
$text = Set-CfgValue $text 'Automation' 'AutomationOnly' $(if ($AutomationOnly) { 'true' } else { 'false' })
$text = Set-CfgValue $text 'Automation' 'RunLabel' $Label
$text = Set-CfgValue $text 'Automation' 'SavePath' $SavePath
[IO.File]::WriteAllText($configPath, $text, (New-Object Text.UTF8Encoding($false)))
# A screenshot of an earlier run with the same label is deleted (exact name only).
$shot = Join-Path $shotDir ($Label + '_pause.png')
if (Test-Path -LiteralPath $shot) { [IO.File]::Delete($shot) }

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
    if ($AffinityMask -ne 0) {
        foreach ($p in @(Get-Process -Name $process -ErrorAction SilentlyContinue)) { $p.ProcessorAffinity = [IntPtr]$AffinityMask }
        $affinityApplied = '0x{0:X}' -f (Get-Process -Name $process | Select-Object -First 1).ProcessorAffinity.ToInt64()
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
if (Test-Path -LiteralPath $shot) { Copy-Item -LiteralPath $shot -Destination $OutputDirectory }

# Leave the installation in its normal state: automation off, all PerfProbe measurements available.
$text = [IO.File]::ReadAllText($configPath)
$text = Set-CfgValue $text 'Automation' 'Enabled' 'false'
$text = Set-CfgValue $text 'Automation' 'AutomationOnly' 'false'
$text = Set-CfgValue $text 'Automation' 'PauseSeconds' '25'
$text = Set-CfgValue $text 'Automation' 'RunSeconds' '25'
[IO.File]::WriteAllText($configPath, $text, (New-Object Text.UTF8Encoding($false)))

$gfx = Select-String -LiteralPath (Join-Path $OutputDirectory ($Label + '_Player.log')) -Pattern 'GfxDevice: creating|Version:  Direct3D|Vulkan API|Graphics API' | ForEach-Object { $_.Line.Trim() }
$markers = Select-String -LiteralPath (Join-Path $OutputDirectory ($Label + '_LogOutput.log')) -Pattern 'AUTOMATION .*(result|framestats|screenshot)|PERFPROBE_AUTOMATION_ONLY' | ForEach-Object Line
"run=$Label duration=" + [int]((Get-Date) - $started).TotalSeconds + 's' + $(if ($affinityApplied) { " affinity=$affinityApplied" } else { '' }) + $(if ($stalled) { " STALLED: no log output for $StallSeconds s, test instance stopped" } else { '' })
$gfx
$markers
