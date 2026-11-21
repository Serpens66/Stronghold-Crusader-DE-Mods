[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GameDir,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{0,95}$')][string]$ModGuid,
    [Parameter(Mandatory)][string]$ModPackage,
    [string]$ApiPluginPackage,
    [string]$ApiPatcherPackage,
    [string]$PluginLayout,
    [string]$SaveFixture,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9][A-Za-z0-9_.-]{0,95}$')][string]$Scenario,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(1,86400)][int]$TimeoutSeconds = 300,
    [switch]$HiddenDesktop
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
. (Join-Path $PSScriptRoot '../ApiSharedRepository/RepositoryProcess.Common.ps1')
. (Join-Path $PSScriptRoot 'InGameTest.Transaction.ps1')

if ([bool]$ApiPluginPackage -ne [bool]$ApiPatcherPackage -or $ModGuid -ieq 'APIShared_Serp') {
    throw 'Supply both APIShared package paths together. The test mod must be a separate plugin.'
}
if ($ModGuid.EndsWith('.') -or $ModGuid -match '^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)') {
    throw 'The mod GUID cannot be used as a Windows directory name.'
}

$game = Get-InGameTestPath $GameDir
$bep = Join-Path $game 'BepInEx'
$plugins = Join-Path $bep 'plugins'
$modDestination = Join-Path $plugins $ModGuid
$apiDestination = Join-Path $plugins 'APIShared_Serp'
$patcherDestination = Join-Path $bep 'patchers/APIShared.DependencyBootstrap.dll'
$rollback = Join-Path $bep 'SerpAutomationRollback'
$processName = 'Stronghold Crusader Definitive Edition'
$gameExe = Join-Path $game ($processName + '.exe')
$crashExe = Join-Path $game 'UnityCrashHandler64.exe'
$log = Join-Path $bep 'LogOutput.log'
$config = Join-Path $bep 'config'
$request = Join-Path $config ("SerpAutomation/{0}.run" -f $ModGuid)
$player = Join-Path ([Environment]::GetFolderPath('UserProfile')) ('AppData/LocalLow/Firefly Studios/' + $processName)
$playerLog = Join-Path $player 'Player.log'
$settings = Join-Path $player 'settings.cfg'
$saves = Join-Path $player 'Saves'
$steamAppIdFile = Join-Path $game 'steam_appid.txt'
$documents = [Environment]::GetFolderPath('MyDocuments')
if (-not $documents) { throw 'The Windows Documents folder could not be resolved.' }
$apiSettings = Join-Path $documents 'SHCDE/APIShared'
$output = Get-InGameTestPath $OutputDirectory
$mod = Get-InGameTestPath $ModPackage
$run = [Guid]::NewGuid().ToString('N')
$evidence = Join-Path $output ($run.Substring(0, 12))
$testSaveName = "SerpAcceptance-$run.sav"
if ($PluginLayout) {
    $layout = Get-InGameTestPath $PluginLayout
    Assert-InGameTestUnlinked $layout
    Assert-InGameTestUnlinked $saves
    if (!$ApiPluginPackage -or (Test-InGameTestInside $layout $game) -or
        (Test-InGameTestInside $layout $player) -or (Test-InGameTestInside $output $layout) -or
        ![IO.Directory]::Exists((Join-Path $layout '000shcdese'))) {
        throw 'A plugin layout must be external, include the Script Extender and use a supplied APIShared pair.'
    }
}

if ($SaveFixture) {
    $fixture = Get-InGameTestPath $SaveFixture
    Assert-InGameTestUnlinked $fixture
    if (!$PluginLayout -or [IO.Path]::GetExtension($fixture) -ine '.sav' -or
        ![IO.File]::Exists($fixture) -or (Test-InGameTestInside $fixture $game)) {
        throw 'A save fixture must be an existing external .sav file and requires a plugin layout.'
    }
}

foreach ($path in @($gameExe, $plugins, $mod, $output, $rollback, $log, $settings, $playerLog, $apiSettings, $request)) {
    Assert-InGameTestUnlinked $path
}
if (-not [IO.File]::Exists($gameExe) -or -not [IO.Directory]::Exists($plugins)) { throw 'The selected game installation was not found.' }
foreach ($root in @($game, $player, $apiSettings)) {
    if (Test-InGameTestInside $output $root) { throw 'The evidence directory must be outside the game and settings trees.' }
}
Assert-InGameTestMod $mod $ModGuid
if (Test-InGameTestInside $mod $game) { throw 'Provide a mod package outside the installed game tree.' }
if ($ApiPluginPackage) {
    $api = Get-InGameTestPath $ApiPluginPackage
    $patcher = Get-InGameTestPath $ApiPatcherPackage
    if ((Test-InGameTestInside $api $game) -or (Test-InGameTestInside $patcher $game)) {
        throw 'Provide the APIShared pair from a prepared package outside the installed game tree.'
    }
    Assert-InGameTestApiPair $api $patcher
}

function Read-TestUnityLog {
    # Unity can still hold an open writer after the game process exits.
    $share = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
    $inputLog = [IO.File]::Open($playerLog, [IO.FileMode]::Open, [IO.FileAccess]::Read, $share)
    $buffer = [IO.MemoryStream]::new()
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        $inputLog.CopyTo($buffer)
        $bytes = $buffer.ToArray()
        [pscustomobject]@{ Bytes = $bytes; Hash = [Convert]::ToBase64String($sha256.ComputeHash($bytes)) }
    }
    finally { $sha256.Dispose(); $buffer.Dispose(); $inputLog.Dispose() }
}

function Get-TestGame([int]$PathWaitSeconds = 0) {
    # Directly after a Steam start the image path can be unreadable for a moment (run c8731202 on 18.11.2026).
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $reported = $false
    while ($true) {
        $gameProcesses = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
        $selection = $null
        try { $selection = Select-InGameTestProcess $gameProcesses $gameExe }
        finally {
            foreach ($candidate in $gameProcesses) {
                if (-not [object]::ReferenceEquals($candidate, $selection.Process)) { $candidate.Dispose() }
            }
        }
        if ($selection.State -ne 'Pending') {
            if ($reported) { Write-TestProgress ('Game process identified after {0:N1} s.' -f $clock.Elapsed.TotalSeconds) }
            return $selection.Process
        }
        if ($clock.Elapsed.TotalSeconds -ge $PathWaitSeconds) {
            throw "A game process could not be identified as the selected installation within $PathWaitSeconds s ($($selection.Detail))."
        }
        if (-not $reported) {
            $reported = $true
            Write-TestProgress "Game process path is not readable yet ($($selection.Detail)); retrying for up to $PathWaitSeconds s."
        }
        Start-Sleep -Milliseconds 250
    }
}

function Wait-TestGameExit([int]$Seconds) {
    # Restoration must not touch files while any game process may still own them.
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $nextReport = 0
    while ($true) {
        $gameProcesses = @(Get-Process -Name $processName -ErrorAction SilentlyContinue)
        foreach ($candidate in $gameProcesses) { $candidate.Dispose() }
        if ($gameProcesses.Count -eq 0) { return $true }
        if ($clock.Elapsed.TotalSeconds -ge $Seconds) { return $false }
        if ($clock.Elapsed.TotalSeconds -ge $nextReport) {
            Write-TestProgress ('Waiting for the unmonitored game process to exit before restoring ({0:N0}/{1} s).' -f $clock.Elapsed.TotalSeconds, $Seconds)
            $nextReport += 60
        }
        Start-Sleep -Seconds 1
    }
}

function Wait-TestCrashHandler {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $waited = $false
    while (@(Get-Process -Name 'UnityCrashHandler64' -ErrorAction SilentlyContinue | Where-Object { $_.Path -ieq $crashExe }).Count) {
        if ($clock.Elapsed.TotalSeconds -ge 60) { throw 'The game crash handler is still running.' }
        $waited = $true
        Start-Sleep -Seconds 1
    }
    if ($waited) { Start-Sleep -Seconds 10 }
}

function Write-TestProgress([string]$Message) {
    Write-Host ('[{0:HH:mm:ss.fff}] run={1} {2}' -f [DateTime]::Now, $run, $Message)
}

function Initialize-TestDesktop {
    if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) {
        throw 'Hidden desktop launch requires the existing Steam session to be running.'
    }
    Assert-InGameTestUnlinked $steamAppIdFile
    if ([IO.File]::Exists($steamAppIdFile) -and [IO.File]::ReadAllText($steamAppIdFile).Trim() -cne '3024040') {
        throw 'Existing steam_appid.txt identifies a different Steam application.'
    }
    if (-not ('InGameTestDesktopHost' -as [type])) {
        Add-Type -Path (Join-Path $PSScriptRoot 'InGameTest.DesktopHost.cs')
    }
}

function Prepare-TestDesktopFiles {
    $snapshots.Add((Save-InGameTestState $steamAppIdFile (Join-Path $rollback 'steam_appid.txt')))
    if (-not [IO.File]::Exists($steamAppIdFile)) {
        [IO.File]::WriteAllLines($steamAppIdFile, [string[]]@('3024040'), [Text.UTF8Encoding]::new($false))
    }
    $text = [IO.File]::ReadAllText($settings)
    foreach ($volume in @('MasterVolume','SFXVolume','SpeechVolume','UnitSpeechVolume','MusicVolume')) {
        $pattern = '(?m)^' + $volume + ':[^\r\n]*'
        if ([regex]::Matches($text, $pattern).Count -ne 1) { throw "Expected exactly one audio setting: $volume" }
        $text = [regex]::Replace($text, $pattern, ($volume + ':0'))
    }
    [IO.File]::WriteAllText($settings, $text, [Text.UTF8Encoding]::new($false))
}

function Wait-TestDesktopRun($Process, [IntPtr]$Desktop, $Observation) {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $nextReport = 0
    while (-not $Process.WaitForExit(100)) {
        if ([InGameTestDesktopHost]::InputDesktop() -cne $Observation.InputDesktop -or
            [InGameTestDesktopHost]::HasInputWindow($Process.Id)) {
            throw 'The input desktop changed or received a test game window.'
        }
        $related = @(Get-Process -Name $processName,'UnityCrashHandler64' -ErrorAction SilentlyContinue)
        try {
            foreach ($candidate in $related) {
                if ($candidate.ProcessName -eq $processName -and $candidate.Id -ne $Process.Id) {
                    throw 'A separate game process appeared during the hidden desktop run.'
                }
                if ($candidate.ProcessName -eq 'UnityCrashHandler64' -and $candidate.Path -ieq $crashExe -and
                    [InGameTestDesktopHost]::HasInputWindow($candidate.Id)) {
                    throw 'The Unity crash handler appeared on the input desktop.'
                }
            }
        }
        finally { foreach ($candidate in $related) { $candidate.Dispose() } }
        if (-not $Observation.Windows.Count) {
            $Observation.Windows = @([InGameTestDesktopHost]::Windows($Desktop) | Where-Object { $_.Pid -eq $Process.Id })
            if ($Observation.Windows.Count) { Write-TestProgress "Game window confirmed on $($Observation.TestDesktop)." }
        }
        if ($clock.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw "TIMEOUT: the hidden test process exceeded $TimeoutSeconds seconds." }
        if ($clock.Elapsed.TotalSeconds -ge $nextReport) {
            $logBytes = if ([IO.File]::Exists($log)) { [IO.FileInfo]::new($log).Length } else { 0 }
            $Process.Refresh()
            Write-TestProgress ('Hidden process elapsed={0:N0}s CPU={1:N1}s logBytes={2}' -f
                $clock.Elapsed.TotalSeconds, $Process.TotalProcessorTime.TotalSeconds, $logBytes)
            $nextReport += 15
        }
    }
    if (-not $Observation.Windows.Count) { throw 'No game window was confirmed on the hidden test desktop.' }
}

$mutex = [Threading.Mutex]::new($false, 'Local\SHCDE.InGameTest.3024040')
$locked = $false
$instance = $null
$recovery = $false
$steamStarted = $false
$runLogIsolated = $false
$previousPlayerLog = $null
$saveSnapshot = $null
$installedPlugins = $null
$swaps = [Collections.Generic.List[object]]::new()
$snapshots = [Collections.Generic.List[object]]::new()
$restorationErrors = [Collections.Generic.List[string]]::new()
$savedLog = Join-Path $evidence 'LogOutput.log'
$desktop = [IntPtr]::Zero
$desktopObservation = $null
$failure = $null
try {
    try { $locked = $mutex.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $locked = $true }
    if (-not $locked) { throw 'Another in-game test is already running.' }
    if (Get-TestGame) { throw 'Close the game before starting an in-game test.' }
    Wait-TestCrashHandler
    if (Get-TestGame) { throw 'The game started while preparing the test.' }
    if (Test-Path -LiteralPath $rollback) { throw "A prior rollback is incomplete. Preserved files: $rollback" }
    if (Test-Path -LiteralPath $request) { throw "An older test request is already present: $request" }
    if ($HiddenDesktop) { Initialize-TestDesktop }

    $null = [IO.Directory]::CreateDirectory($evidence)
    $null = [IO.Directory]::CreateDirectory($rollback)
    $recovery = $true
    Write-TestProgress "Preparing $ModGuid/$Scenario. Evidence: $evidence"
    $snapshots.Add((Save-InGameTestState $config (Join-Path $rollback 'config') -Directory))
    $snapshots.Add((Save-InGameTestState $settings (Join-Path $rollback 'settings.cfg')))
    $snapshots.Add((Save-InGameTestState $apiSettings (Join-Path $rollback 'api-settings') -Directory))
    $snapshots.Add((Save-InGameTestState $log (Join-Path $rollback 'previous-LogOutput.log')))
    if ($HiddenDesktop) { Prepare-TestDesktopFiles }
    $previousPlayerLog = if (-not $HiddenDesktop -and [IO.File]::Exists($playerLog)) { (Read-TestUnityLog).Hash }
    [IO.File]::Delete($log) # Isolate the new log with either BepInEx AppendLog setting.
    $runLogIsolated = $true

    if ($PluginLayout) {
        $saveSnapshot = Save-InGameTestState $saves (Join-Path $rollback 'saves') -Directory
        if ($saveSnapshot.Inventory.Files.ContainsKey($testSaveName)) {
            throw "The test save already exists: $testSaveName"
        }
        if ($SaveFixture) {
            $null = Save-InGameTestState $fixture (Join-Path $rollback 'fixture.sav')
        }
        $installedPlugins = Get-InGameTestTree $plugins
        Set-InGameTestPluginLayout $layout $plugins (Join-Path $rollback 'plugin-layout') $swaps @($ModGuid, 'APIShared_Serp')
    }
    if ($ApiPluginPackage) {
        Set-InGameTestPackage $api $apiDestination (Join-Path $rollback 'api') $swaps -Directory
        Set-InGameTestPackage $patcher $patcherDestination (Join-Path $rollback 'bootstrap') $swaps
        Assert-InGameTestApiPair $apiDestination $patcherDestination
    }
    Set-InGameTestPackage $mod $modDestination (Join-Path $rollback 'mod') $swaps -Directory
    if (Get-TestGame) { throw 'The game started before the test could be armed.' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($request)) | Out-Null
    $pending = Join-Path $rollback 'request'
    [IO.File]::WriteAllLines($pending, [string[]]@($run, $Scenario), [Text.UTF8Encoding]::new($false))
    [IO.File]::Move($pending, $request)

    if ($HiddenDesktop) {
        $desktopName = 'SerpTest_' + $run.Substring(0, 12)
        $desktopObservation = [ordered]@{
            InputDesktop = [InGameTestDesktopHost]::InputDesktop()
            TestDesktop = $desktopName
            Windows = @()
        }
        # This handle does not request desktop-switch access.
        $desktop = [InGameTestDesktopHost]::CreateDesktopW($desktopName, [IntPtr]::Zero, [IntPtr]::Zero, 0, 0xC3, [IntPtr]::Zero)
        if ($desktop -eq [IntPtr]::Zero) { throw [ComponentModel.Win32Exception]::new() }
        $hiddenPlayerLog = Join-Path $evidence 'Player.log'
        $arguments = '-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "' + $hiddenPlayerLog + '"'
        Write-TestProgress "Starting the Steam game once on $desktopName."
        $instance = [InGameTestDesktopHost]::Start($gameExe, $arguments, $game, $desktopName)
        $desktopObservation.GamePid = $instance.Id
        Wait-TestDesktopRun $instance $desktop $desktopObservation
    } else {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        Write-TestProgress 'Starting the game through Steam.'
        $steamStarted = $true
        Start-Process -FilePath 'steam://rungameid/3024040' -WindowStyle Hidden
        $retried = $false
        while (-not ($instance = Get-TestGame -PathWaitSeconds 30)) {
            if ([IO.File]::Exists($log)) { throw 'GAME_CRASH: the game wrote its startup log and exited before its process could be observed.' }
            if ($watch.Elapsed.TotalSeconds -ge 90) { throw 'START_FAILED: Steam did not start the game within 90 seconds.' }
            if (-not $retried -and $watch.Elapsed.TotalSeconds -ge 45) {
                $retried = $true
                Write-TestProgress 'Retrying the Steam start.'
                Start-Process -FilePath 'steam://rungameid/3024040' -WindowStyle Hidden
            }
            Start-Sleep -Milliseconds 100
        }
        # Retain the process handle to obtain the actual exit code, even if it exits during the run.
        try { $null = $instance.Handle }
        catch { if ($instance.HasExited) { throw 'GAME_CRASH: the game exited before its startup could be observed.' }; throw }
        Write-TestProgress "Game started (PID $($instance.Id))."
        if (-not $instance.WaitForExit($TimeoutSeconds * 1000)) { throw "TIMEOUT: the test process exceeded $TimeoutSeconds seconds." }
    }
    if ($instance.ExitCode -ne 0) { throw ('GAME_CRASH: game exited with 0x{0:X8}.' -f $instance.ExitCode) }
}
catch { $failure = $_.Exception.Message }
finally {
    try {
        if ($recovery -and $steamStarted -and -not $instance) {
            # Start or identification failed while the game may still run the armed test; restore after it exits.
            if (-not (Wait-TestGameExit $TimeoutSeconds)) {
                Write-TestProgress 'The unmonitored game process is still running after the timeout.'
                $instance = Get-TestGame -PathWaitSeconds 30
            }
        }
        if ($instance -and -not $instance.HasExited) {
            $instance.Kill()
            if (-not $instance.WaitForExit(15000)) { throw 'The test process did not exit after termination.' }
        }
        if ($recovery -and $HiddenDesktop) { Wait-TestCrashHandler }
        if ($recovery -and (Get-TestGame)) { throw 'Game is still running. Do not restore files while the process owns them.' }
        if ($recovery) {
            if ($runLogIsolated -and [IO.File]::Exists($log)) {
                try { [IO.File]::Copy($log, $savedLog, $false) }
                catch { $restorationErrors.Add("Cannot save BepInEx log: $($_.Exception.Message)") }
            }
            try {
                if (-not $HiddenDesktop -and [IO.File]::Exists($playerLog)) {
                    $unityLog = Read-TestUnityLog
                    if ($unityLog.Hash -cne $previousPlayerLog) {
                        [IO.File]::WriteAllBytes((Join-Path $evidence 'Player.log'), $unityLog.Bytes)
                    }
                }
            }
            catch { $restorationErrors.Add("Cannot save Unity log: $($_.Exception.Message)") }
            if ($saveSnapshot) {
                try { Complete-InGameTestSaves $saveSnapshot $run $evidence }
                catch { $restorationErrors.Add($_.Exception.Message) }
            }
            for ($i = $swaps.Count - 1; $i -ge 0; $i--) {
                try { Restore-InGameTestPackage $swaps[$i] }
                catch { $restorationErrors.Add($_.Exception.Message) }
            }
            if ($installedPlugins) {
                try { Assert-InGameTestTree $plugins $installedPlugins }
                catch { $restorationErrors.Add($_.Exception.Message) }
            }
            foreach ($snapshot in $snapshots) {
                try { Restore-InGameTestState $snapshot }
                catch { $restorationErrors.Add($_.Exception.Message) }
            }
            if ($restorationErrors.Count -eq 0) { Remove-InGameTestTree $rollback $bep }
        }
    }
    catch { $restorationErrors.Add($_.Exception.Message) }
    finally {
        if ($instance) { $instance.Dispose() }
        if ($desktopObservation) {
            try {
                $desktopObservation.Closed = $desktop -eq [IntPtr]::Zero -or [InGameTestDesktopHost]::CloseDesktop($desktop)
                $desktopObservation.FinalInputDesktop = [InGameTestDesktopHost]::InputDesktop()
                $desktopObservation.Failure = $failure
                [IO.File]::WriteAllText((Join-Path $evidence 'desktop.json'), ($desktopObservation | ConvertTo-Json -Depth 5))
                if (-not $desktopObservation.Closed) { throw 'The hidden desktop handle could not be closed.' }
            }
            catch { $restorationErrors.Add($_.Exception.Message) }
        }
        if ($locked) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
}

if ($restorationErrors.Count) {
    throw "RESTORE_FAILED: $($restorationErrors -join ' | '). Original outcome: $failure. Recovery: $rollback. Evidence: $evidence"
}
if ($failure) { throw "$failure Evidence: $evidence" }
try { $result = Get-InGameTestResult $savedLog $run }
catch { throw "$($_.Exception.Message) Evidence: $evidence" }
if ($result -cne 'PASS') { throw "TEST_FAILED: run=$run result=$result. Evidence: $evidence" }
Write-TestProgress 'PASS. Plugin files and settings restored and verified.'
