[CmdletBinding()]
param(
    # Scenario file name in Testmods/UiAcceptance/scenarios (copied into BepInEx/config/UiAcceptance).
    [Parameter(Mandatory)] [string]$Scenario,
    [Parameter(Mandatory)] [string]$OutputDirectory,
    [string]$Label = '',
    # Optional fullscreen resolution "WIDTHxHEIGHT"; settings.cfg is restored byte-exact afterwards.
    [string]$Resolution = '',
    # Prepare the client share for an MP run instead of starting the local game.
    [switch]$PrepareClient,
    [string]$ClientRoot = '\\LENOVO_SERP\Stronghold Crusader Definitive Edition',
    [int]$TimeoutSeconds = 900,
    [int]$StallSeconds = 180,
    # Host runs: writes the lobby ID from this run's "MP_HOST lobby=" line into the client's join file
    # (Steam friend status does not expose private lobbies).
    [switch]$PublishLobbyToClient
)
# Runs one UiAcceptance scenario on this PC (sp/host role): writes the [Run] config, starts the game
# through Steam, waits until the scenario quits the game and saves the logs and screenshots before the
# next start overwrites them (BepInEx AppendLog is disabled). -PrepareClient only writes scenario and
# config to the client share; the user starts the client game there.
$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$process = 'Stronghold Crusader Definitive Edition'
$source = Join-Path (Split-Path -Parent $PSScriptRoot) "scenarios\$Scenario"
if (-not (Test-Path -LiteralPath $source)) { throw "Scenario not found: $source" }
if (-not $Label) { $Label = [IO.Path]::GetFileNameWithoutExtension($Scenario) }

# Section-aware BepInEx cfg writer (PerfProbe pattern); keeps the file's own line ending.
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

function Write-RunConfig([string]$root) {
    $configDir = Join-Path $root 'BepInEx\config'
    New-Item -ItemType Directory -Force -Path (Join-Path $configDir 'UiAcceptance') | Out-Null
    Copy-Item -LiteralPath $source -Destination (Join-Path $configDir "UiAcceptance\$Scenario") -Force
    $cfg = Join-Path $configDir 'UiAcceptance_Serp.cfg'
    $text = if (Test-Path -LiteralPath $cfg) { [IO.File]::ReadAllText($cfg) } else { '' }
    $text = Set-CfgValue $text 'Run' 'Enabled' 'true'
    $text = Set-CfgValue $text 'Run' 'Scenario' $Scenario
    $text = Set-CfgValue $text 'Run' 'RunLabel' $Label
    [IO.File]::WriteAllText($cfg, $text, (New-Object Text.UTF8Encoding($false)))
}

if ($PrepareClient) {
    Write-RunConfig $ClientRoot
    "client prepared: $ClientRoot scenario=$Scenario label=$Label"
    return
}

if (Get-Process -Name $process -ErrorAction SilentlyContinue) { throw 'The game is already running.' }
$quiet = (Get-Date).AddSeconds(60)
while ((Get-Process -Name 'UnityCrashHandler64' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $quiet) { Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 10
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Write-RunConfig $game
$log = Join-Path $game 'BepInEx\LogOutput.log'
$playerDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition'
$shots = Join-Path $game 'BepInEx\UiAcceptance_shots'
$settings = Join-Path $playerDir 'settings.cfg'
$settingsOriginal = [IO.File]::ReadAllBytes($settings)
if ($Resolution) {
    if ($Resolution -notmatch '^(\d+)x(\d+)$') { throw 'Resolution must be WIDTHxHEIGHT.' }
    $width = $Matches[1]; $height = $Matches[2]
    $text = [Text.Encoding]::UTF8.GetString($settingsOriginal)
    if ($text -notmatch '(?m)^FullWidth:\d+' -or $text -notmatch '(?m)^FullHeight:\d+') { throw 'settings.cfg has no FullWidth/FullHeight.' }
    $text = [regex]::Replace($text, '(?m)^FullWidth:\d+', { param($m) 'FullWidth:' + $width })
    $text = [regex]::Replace($text, '(?m)^FullHeight:\d+', { param($m) 'FullHeight:' + $height })
    [IO.File]::WriteAllBytes($settings, (New-Object Text.UTF8Encoding($false)).GetBytes($text))
}
if (Test-Path -LiteralPath $shots) { Get-ChildItem -LiteralPath $shots -Filter ($Label + '_*.png') | Remove-Item -Force }
# Scenarios set mod settings explicitly; the user's personal APIShared settings are restored
# byte-exact afterwards (every file, including presets; files created by the run are removed).
$modSettings = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'SHCDE\APIShared'
$settingsBackup = @{}
if (Test-Path -LiteralPath $modSettings) {
    foreach ($file in Get-ChildItem -LiteralPath $modSettings -Recurse -File) { $settingsBackup[$file.FullName] = [IO.File]::ReadAllBytes($file.FullName) }
}
# The previous start's log is already saved; remove it so lobby detection reads only this run.
if (Test-Path -LiteralPath $log) { [IO.File]::Delete($log) }
$started = Get-Date
$stalled = $false
try {
    Start-Process 'steam://rungameid/3024040'
    $deadline = $started.AddSeconds($TimeoutSeconds)
    $retryAt = (Get-Date).AddSeconds(45)
    while (-not (Get-Process -Name $process -ErrorAction SilentlyContinue)) {
        if ((Get-Date) -gt $deadline) { throw 'The game did not start.' }
        if ($retryAt -and (Get-Date) -gt $retryAt) { $retryAt = $null; Start-Process 'steam://rungameid/3024040' }
        Start-Sleep -Seconds 2
    }
    $published = $null
    $joinFile = Join-Path $ClientRoot 'BepInEx\config\UiAcceptance\join-lobby.txt'
    if ($PublishLobbyToClient -and (Test-Path -LiteralPath $joinFile)) { [IO.File]::Delete($joinFile) }
    while (Get-Process -Name $process -ErrorAction SilentlyContinue) {
        if ($PublishLobbyToClient -and -not $published -and (Test-Path -LiteralPath $log)) {
            $hosted = Select-String -LiteralPath $log -Pattern 'MP_HOST lobby=(\d+)' | Select-Object -Last 1
            if ($hosted) {
                $published = $hosted.Matches[0].Groups[1].Value
                [IO.File]::WriteAllText($joinFile, $published)
                "published lobby $published to $joinFile"
            }
        }
        if ((Get-Date) -gt $deadline) {
            Get-Process -Name $process -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
            $stalled = $true; break
        }
        if ($StallSeconds -gt 0 -and (Test-Path -LiteralPath $log) -and
            ((Get-Date) - (Get-Item -LiteralPath $log).LastWriteTime).TotalSeconds -gt $StallSeconds -and
            ((Get-Date) - $started).TotalSeconds -gt $StallSeconds) {
            Get-Process -Name $process -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
            $stalled = $true; break
        }
        Start-Sleep -Seconds 2
    }
    Start-Sleep -Seconds 3
}
finally {
    # The game may rewrite settings.cfg on exit; restore the original bytes exactly in every case.
    [IO.File]::WriteAllBytes($settings, $settingsOriginal)
    if (Test-Path -LiteralPath $modSettings) {
        foreach ($file in Get-ChildItem -LiteralPath $modSettings -Recurse -File) {
            if (-not $settingsBackup.ContainsKey($file.FullName)) { [IO.File]::Delete($file.FullName) }
        }
    }
    foreach ($entry in $settingsBackup.GetEnumerator()) {
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($entry.Key))
        [IO.File]::WriteAllBytes($entry.Key, $entry.Value)
    }
}
$modSettingsRestored = $true
foreach ($entry in $settingsBackup.GetEnumerator()) {
    if (-not [Linq.Enumerable]::SequenceEqual([byte[]]$entry.Value, [byte[]][IO.File]::ReadAllBytes($entry.Key))) { $modSettingsRestored = $false }
}
$savedLog = Join-Path $OutputDirectory ($Label + '_LogOutput.log')
Copy-Item -LiteralPath $log -Destination $savedLog
Copy-Item -LiteralPath (Join-Path $playerDir 'Player.log') -Destination (Join-Path $OutputDirectory ($Label + '_Player.log'))
if (Test-Path -LiteralPath $shots) { Get-ChildItem -LiteralPath $shots -Filter ($Label + '_*.png') | Copy-Item -Destination $OutputDirectory }
$restored = [Linq.Enumerable]::SequenceEqual([byte[]]$settingsOriginal, [byte[]][IO.File]::ReadAllBytes($settings))
"run=$Label duration=" + [int]((Get-Date) - $started).TotalSeconds + "s settingsRestored=$restored modSettingsRestored=$modSettingsRestored files=$($settingsBackup.Count)" + $(if ($stalled) { ' STALLED/TIMEOUT: game stopped' } else { '' })
Select-String -LiteralPath $savedLog -Pattern 'ACCEPT run=' | ForEach-Object { $_.Line }
