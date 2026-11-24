[CmdletBinding()]
param(
    # Scenario file name in Testmods/UiAcceptance/scenarios (copied into BepInEx/config/UiAcceptance).
    [Parameter(Mandatory)] [string]$Scenario,
    [Parameter(Mandatory)] [string]$OutputDirectory,
    [string]$Label = '',
    # Optional fullscreen resolution "WIDTHxHEIGHT"; settings.cfg is restored byte-exact afterwards.
    [string]$Resolution = '',
    # Explicit SerpLocalization language source; the SE config is restored byte-exact afterwards.
    [ValidateSet('', 'de-DE', 'en-US')] [string]$Locale = '',
    # Prepare the client share for an MP run instead of starting the local game.
    [switch]$PrepareClient,
    [string]$ClientRoot = '\\LENOVO_SERP\Stronghold Crusader Definitive Edition',
    [int]$TimeoutSeconds = 900,
    [int]$StallSeconds = 180,
    # Host runs: writes the lobby ID from this run's "MP_HOST lobby=" line into the client's join file
    # (Steam friend status does not expose private lobbies).
    [switch]$PublishLobbyToClient,
    # Starts the game on a separate, never activated Win32 desktop (AGENTS/shell-windows.md): no visible window,
    # no focus change and muted audio for the user. Needs the running Steam session; windowed 1280x720.
    [switch]$HiddenDesktop
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
    if ($Locale) { throw '-Locale is only supported for local runs with automatic rollback.' }
    Write-RunConfig $ClientRoot
    "client prepared: $ClientRoot scenario=$Scenario label=$Label"
    return
}

if (Get-Process -Name $process -ErrorAction SilentlyContinue) { throw 'The game is already running.' }
if ($HiddenDesktop) {
    if ($PublishLobbyToClient) { throw '-HiddenDesktop is for single-player runs.' }
    if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) { throw 'A hidden desktop run needs the running Steam session.' }
    $workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
    if (-not ('InGameTestDesktopHost' -as [type])) { Add-Type -Path (Join-Path $workspace 'Shared/Tools/Automation/InGameTest.DesktopHost.cs') }
    # The same machine-wide lock as Invoke-InGameTest and Run-PairedResync: one automated game test at a time.
    $testMutex = [Threading.Mutex]::new($false, 'Local\SHCDE.InGameTest.3024040')
    try { $mutexHeld = $testMutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $mutexHeld = $true }
    if (-not $mutexHeld) { $testMutex.Dispose(); throw 'Another automated game test holds the game.' }
}
$quiet = (Get-Date).AddSeconds(60)
while ((Get-Process -Name 'UnityCrashHandler64' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $quiet) { Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 10
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Write-RunConfig $game
if ($Locale) {
    # Expected language is part of each box assertion, independent of the label or requested cfg.
    $scenarioTarget = Join-Path $game "BepInEx/config/UiAcceptance/$Scenario"
    $scenarioData = [IO.File]::ReadAllText($scenarioTarget) | ConvertFrom-Json
    foreach ($item in $scenarioData.steps) {
        if ($item.do -eq 'AiUi.Box') { $item.args = @($item.args[0], $Locale) }
    }
    [IO.File]::WriteAllText($scenarioTarget, ($scenarioData | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
}
$log = Join-Path $game 'BepInEx\LogOutput.log'
$playerDir = Join-Path $env:USERPROFILE 'AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition'
$shots = Join-Path $game 'BepInEx\UiAcceptance_shots'
$settings = Join-Path $playerDir 'settings.cfg'
$settingsOriginal = [IO.File]::ReadAllBytes($settings)
$extenderConfig = Join-Path $game 'BepInEx/config/000shcdese.cfg'
$extenderOriginal = if ($Locale) { [IO.File]::ReadAllBytes($extenderConfig) } else { $null }
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
$hiddenPlayerLog = Join-Path $OutputDirectory ($Label + '_Player.log')
$steamAppId = Join-Path $game 'steam_appid.txt'
$createdSteamAppId = $false
$desktop = [IntPtr]::Zero
$instance = $null
try {
    if ($Locale) {
        $extenderText = [Text.Encoding]::UTF8.GetString($extenderOriginal)
        $extenderText = Set-CfgValue $extenderText 'Visual' 'LanguageProvider' $Locale
        [IO.File]::WriteAllText($extenderConfig, $extenderText, (New-Object Text.UTF8Encoding($false)))
    }
    if ($HiddenDesktop) {
        # Valve's development start with the running Steam session (AppID 3024040); a file the user had stays.
        if ([IO.File]::Exists($steamAppId)) {
            if ([IO.File]::ReadAllText($steamAppId).Trim() -cne '3024040') { throw 'steam_appid.txt names another application.' }
        } else { [IO.File]::WriteAllText($steamAppId, "3024040`r`n", (New-Object Text.UTF8Encoding($false))); $createdSteamAppId = $true }
        # Muted for this run only; settings.cfg is restored byte-exact below.
        $text = [IO.File]::ReadAllText($settings)
        foreach ($volume in @('MasterVolume', 'SFXVolume', 'SpeechVolume', 'UnitSpeechVolume', 'MusicVolume')) {
            $pattern = '(?m)^' + $volume + ':[^\r\n]*'
            if ([regex]::Matches($text, $pattern).Count -ne 1) { throw "Expected exactly one audio setting: $volume" }
            $text = [regex]::Replace($text, $pattern, ($volume + ':0'))
        }
        [IO.File]::WriteAllText($settings, $text, (New-Object Text.UTF8Encoding($false)))
        $inputDesktop = [InGameTestDesktopHost]::InputDesktop()
        $desktopName = 'SerpTest_' + ($Label -replace '[^A-Za-z0-9_-]', '_')
        $desktop = [InGameTestDesktopHost]::CreateDesktopW($desktopName, [IntPtr]::Zero, [IntPtr]::Zero, 0, 0xC3, [IntPtr]::Zero)
        if ($desktop -eq [IntPtr]::Zero) { throw [ComponentModel.Win32Exception]::new() }
        $arguments = '-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "' + $hiddenPlayerLog + '"'
        $instance = [InGameTestDesktopHost]::Start((Join-Path $game ($process + '.exe')), $arguments, $game, $desktopName)
        "[{0:HH:mm:ss.fff}] started PID {1} on hidden desktop {2}" -f (Get-Date), $instance.Id, $desktopName
        $deadline = $started.AddSeconds($TimeoutSeconds)
        $windowSeen = $false
        # Checked every 100 ms: the user's input desktop stays active and never receives a game window; any other
        # game process (for example a Steam relaunch on the user's desktop) aborts the run.
        while (-not $instance.WaitForExit(100)) {
            if ([InGameTestDesktopHost]::InputDesktop() -cne $inputDesktop -or [InGameTestDesktopHost]::HasInputWindow($instance.Id)) {
                $instance.Kill(); throw 'The game reached the input desktop; run aborted.'
            }
            if (@(Get-Process -Name $process -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $instance.Id }).Count) {
                $instance.Kill(); throw 'A second game process appeared; run aborted.'
            }
            if (-not $windowSeen) {
                $windowSeen = @([InGameTestDesktopHost]::Windows($desktop) | Where-Object { $_.Pid -eq $instance.Id }).Count -gt 0
                if ($windowSeen) { "[{0:HH:mm:ss.fff}] game window confirmed on {1}" -f (Get-Date), $desktopName }
                elseif (((Get-Date) - $started).TotalSeconds -gt 80) { $instance.Kill(); throw 'No game window appeared on the hidden desktop.' }
            }
            $now = Get-Date
            if ($now -gt $deadline -or ($StallSeconds -gt 0 -and (Test-Path -LiteralPath $log) -and
                ($now - (Get-Item -LiteralPath $log).LastWriteTime).TotalSeconds -gt $StallSeconds -and
                ($now - $started).TotalSeconds -gt $StallSeconds)) {
                $instance.Kill(); $stalled = $true; break
            }
        }
        [void]$instance.WaitForExit(15000)
    }
    else {
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
            if (-not (Get-Process -Name $process -ErrorAction SilentlyContinue)) {
                # Steam can end the first game process and start a second one a few seconds later. Restoring
                # settings in that gap let the second process run the scenario unsupervised on 08.11.2026 and
                # overwrite personal settings, so only a sustained absence counts as the end of the run.
                $relaunchUntil = (Get-Date).AddSeconds(20)
                while (-not (Get-Process -Name $process -ErrorAction SilentlyContinue) -and (Get-Date) -lt $relaunchUntil) { Start-Sleep -Seconds 1 }
            }
        }
    }
    Start-Sleep -Seconds 3
}
finally {
    if ($HiddenDesktop) {
        # Only our own process; then the desktop, a steam_appid.txt this run created, and the shared test lock.
        if ($instance -and -not $instance.HasExited) { try { $instance.Kill(); [void]$instance.WaitForExit(15000) } catch { } }
        if ($desktop -ne [IntPtr]::Zero) { [void][InGameTestDesktopHost]::CloseDesktop($desktop) }
        if ($createdSteamAppId -and [IO.File]::Exists($steamAppId)) { [IO.File]::Delete($steamAppId) }
        if ($testMutex) { $testMutex.ReleaseMutex(); $testMutex.Dispose() }
    }
    if ($Locale) { [IO.File]::WriteAllBytes($extenderConfig, $extenderOriginal) }
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
if (-not $HiddenDesktop) { Copy-Item -LiteralPath (Join-Path $playerDir 'Player.log') -Destination $hiddenPlayerLog }
if (Test-Path -LiteralPath $shots) { Get-ChildItem -LiteralPath $shots -Filter ($Label + '_*.png') | Copy-Item -Destination $OutputDirectory }
$restored = [Linq.Enumerable]::SequenceEqual([byte[]]$settingsOriginal, [byte[]][IO.File]::ReadAllBytes($settings))
$localeRestored = -not $Locale -or [Linq.Enumerable]::SequenceEqual([byte[]]$extenderOriginal, [byte[]][IO.File]::ReadAllBytes($extenderConfig))
"run=$Label duration=" + [int]((Get-Date) - $started).TotalSeconds + "s settingsRestored=$restored modSettingsRestored=$modSettingsRestored localeRestored=$localeRestored locale=$Locale files=$($settingsBackup.Count)" + $(if ($stalled) { ' STALLED/TIMEOUT: game stopped' } else { '' })
Select-String -LiteralPath $savedLog -Pattern 'ACCEPT run=' | ForEach-Object { $_.Line }
# Process exit alone also occurs after a startup crash. Only this run's complete
# successful scenario plus verified settings restoration can qualify as acceptance.
$summaryPattern = 'ACCEPT run=' + [regex]::Escape($Label) + ' step=SUMMARY PASS .* failed=0 skipped=0(?:\s|$)'
$completeSummary = @(Select-String -LiteralPath $savedLog -Pattern $summaryPattern)
if ($stalled -or -not $restored -or -not $modSettingsRestored -or -not $localeRestored -or $completeSummary.Count -ne 1) {
    throw "Acceptance did not complete successfully: $Label. Evidence: $OutputDirectory"
}
