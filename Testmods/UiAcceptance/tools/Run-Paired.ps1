<#
Paired two-PC multiplayer acceptance run.

Host = this PC, started once on a separate, never activated Win32 desktop (no foreground, no global input).
Client = LENOVO_SERP, reached through its game share; its game is started by the on-demand start watcher
(_Automation\Start-ClientStartWatcher.bat, started by the user on the client).

Both PCs get the same plugin layout (-PluginLayout) plus the supplied APIShared pair. Everything changed is
backed up first and restored and verified afterwards on both PCs, on success and on failure.

Layout sessions (test series): applying the plugin layout records a session per PC in
BepInEx\SerpAutomationRollback\paired-session.json (layout identity, original plugin/patcher inventories, swap list).
  - Series workflow: first run with -KeepLayout, every further run with -KeepLayout, finally one call with -RestoreLayout.
  - -KeepLayout: settings, saves and test files are restored after the run as usual; the plugin layout, the
    rollback folder and the session stay installed for the next run.
  - At start, an existing session is reused when its identity (hash over the requested layout inventory plus the
    APIShared plugin/patcher hashes) matches and the installed layout checks out (host: full hash check; client:
    file list, sizes, info.json and pair-manifest hashes, bootstrap patcher hash). Otherwise that side is restored
    from its session first and the requested layout is applied fresh. SerpAutomationRollback without a valid
    session record is an unknown state and blocks the run.
  - The test mod UiAcceptance_Serp is test infrastructure, not part of the layout under test: identity and installed
    check ignore it. On a reused session it is compared with the layout's copy (full hash) and, if it differs,
    replaced in place ("TEST MOD REFRESHED side=..."). The original pre-series package stays in the session's swap
    backup, so -RestoreLayout still restores and verifies the original inventory.
  - Without -KeepLayout, a run restores the plugins at its end (also a reused session), like a single run always did.
  - -RestoreLayout: no game start, no scenario parameters needed. Restores plugins and patchers on both PCs from their
    sessions, verifies them against the recorded original inventories, then removes rollback folder and session.
  - Settings/saves of a run whose recovery was deferred (game still running) or failed are recorded in the session
    and completed automatically by the next run or by -RestoreLayout.
  - While a layout is kept, other in-game test runners refuse to start (SerpAutomationRollback exists).

PASS requires all of:
  - each log: exactly one "ACCEPT run=<label> step=SUMMARY ..." line, and it is "SUMMARY PASS ... failed=0 skipped=0";
  - no MOD_FAILURE line in either log since this run's game start;
  - no line matching (?i)desync|out of sync in either log from that side's first "run=<label>" line on;
  - OBSERVE lines for this run ("OBSERVE run=<label> session= tick=<t> label=<l> ..."): per (tick, label) present on both
    sides, the payloads from "label=" on are identical (lines on one side only are not comparable, no failure);
  - settings and saves restored and verified on both PCs (no deferred recovery); plugins and patchers as well,
    unless -KeepLayout kept the layout (result.json LayoutKept=true);
  - the saves folders only gained SerpAcceptance-<label>.msv, which is archived and removed again;
  - a host scenario with saveEvidence="round-trip" requires both archived saves and a genuine loaded mission
    on each peer at the same native archive tick. It does not install the trading Lord fixture;
  - other host scenarios with Mp.SaveConfirmed keep the existing Lord-data assertion.

-DryRun validates the inputs, inspects both PCs read-only and prints the plan (including whether each side would
apply, reuse or rebuild the layout). It changes nothing. Combined with -RestoreLayout it shows what would be restored.

-Manual: the user plays both games by hand (one normal Steam start per PC); the runner prepares, observes and evaluates.
  Usage: Run-Paired.ps1 -Manual -PluginLayout <dir> -ApiPluginPackage <dir> -ApiPatcherPackage <dll>
         [-HostScenario <json>] [-ClientScenario <json>] [-TimeoutMinutes 120] [-DryRun]
  - Implies -KeepLayout. Same preconditions as a run, except: no client start watcher, no Steam check, no muting and no
    steam_appid.txt; the runner starts no game. Layout apply/reuse/rebuild and test-mod refresh as in a normal run.
  - Arms both sides with the observer scenarios (default scenarios/manual-observe-host.json / manual-observe-client.json;
    they never finish, quitWhenDone may be false, no SUMMARY is expected).
  - Debug disk logging on both PCs: BepInEx\config\BepInEx.cfg is backed up and [Logging.Disk] LogLevels gets Debug
    (unchanged when it already holds Debug or All); restored byte-exact afterwards (APIShared logs SAVE_SETTINGS_CONFIRMED
    only at Debug).
  - Observation: important lines are printed as they appear; every 60 s a HEARTBEAT per side; "STALL side=<side>" when a
    running side is in a mission (last "Mission started" after the last "Mission ended") and wrote no log line for over
    60 s; "OBSERVE_QUIET side=<side>" when log lines arrive but no OBSERVE line for over 60 s in a mission. OBSERVE
  - End: both games ran at least once and both exited (30 s confirmation), or <evidence>\stop.txt appears, or
    -TimeoutMinutes (default 120 with -Manual). A game still running at the end defers its restoration (see -RestoreLayout).
  - Evidence (.work\a\paired-manual\<n>): full LogOutput.log and Player.log of both sides, all observed lines
    (<side>-observed.log), excerpts (<side>.log), new/changed .msv files (saves\<side>\...), result.json and report.txt.
    User saves are never deleted; the Saves inventories are only compared. Each new/changed .msv gets the Lord save check.
  - Report: per side game start/exit, missions (save=True/False), MOD_FAILURE, Error lines (the transient
    LOBBY_LAUNCH_BLOCKED ... worker-production-agreement separately), desync lines; per MP save load (F1) the
    SAVE_SETTINGS markers, wait/block/timeout markers, "Mission started ... save=True" and OBSERVE progress on both sides;
    OBSERVE payload comparison per (tick, label), one-sided lines not comparable; OBSERVE_MISSION/OBSERVE_FAILED lines; PASS/FAIL is informational: exit code 0 unless the runner
    failed (preconditions, restoration).
#>
[CmdletBinding()]
param(
    # Required unless -RestoreLayout is given.
    [string]$HostScenario,
    [string]$ClientScenario,
    [string]$PluginLayout,
    [string]$ApiPluginPackage,
    [string]$ApiPatcherPackage,
    [string]$ClientRoot = '\\LENOVO_SERP\Stronghold Crusader Definitive Edition',
    [string]$ClientUserRoot = '\\LENOVO_SERP\Firefly Studios\Stronghold Crusader Definitive Edition',
    [string]$HostRoot = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [ValidateRange(1,240)][int]$TimeoutMinutes = 26,
    [switch]$DryRun,
    [switch]$KeepLayout,
    [switch]$RestoreLayout,
    [switch]$Manual
)

$ErrorActionPreference = 'Stop'
if ($KeepLayout -and $RestoreLayout) { throw '-KeepLayout and -RestoreLayout cannot be combined.' }
if ($Manual -and $RestoreLayout) { throw '-Manual and -RestoreLayout cannot be combined.' }
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if ($Manual) {
    # Manual sessions always keep the layout for the rest of the series.
    $KeepLayout = [Management.Automation.SwitchParameter]$true
    if (-not $HostScenario) { $HostScenario = Join-Path $workspace 'Testmods/UiAcceptance/scenarios/manual-observe-host.json' }
    if (-not $ClientScenario) { $ClientScenario = Join-Path $workspace 'Testmods/UiAcceptance/scenarios/manual-observe-client.json' }
    if (-not $PSBoundParameters.ContainsKey('TimeoutMinutes')) { $TimeoutMinutes = 120 }
}
if (-not $RestoreLayout) {
    foreach ($name in @('HostScenario','ClientScenario','PluginLayout','ApiPluginPackage','ApiPatcherPackage')) {
        if (-not (Get-Variable -Name $name -ValueOnly)) { throw "-$name is required (unless -RestoreLayout is given)." }
    }
}
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
. (Join-Path $workspace 'Shared/Tools/Automation/InGameTest.Transaction.ps1')
. (Join-Path $workspace 'Shared/Tools/Automation/InGameTest.ClientStart.ps1')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$gameName = 'Stronghold Crusader Definitive Edition'
$appId = '3024040'
$crlf = [string][char]13 + [char]10
$utf8 = New-Object System.Text.UTF8Encoding($false)
$strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
# Must stay compatible with the in-game checkpoint regex ^paired-[a-f0-9]+$.
$label = 'paired-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$labelPattern = [regex]::Escape($label)
$runRegex = 'ACCEPT run=' + $labelPattern + ' step='
$testModName = 'UiAcceptance_Serp'
$lordName = 'SerpMarketRest4'
$lordHashes = @{
    'SerpMarketRest4.lordjson' = '17b809e9ea015a5181efd5f76cc2908dca50166bd68787db3c7588dbc08c4a5b'
    'SerpMarketRest4.aivjson' = '84bd02e51d2da4abb5c6ae5fbe76820a96b99ac4dfedf8760a6bcd26c8951806'
}
$saveEntryName = '_SE_ModData_ExtendedData-LordData.msgpack'
$testSaveName = 'SerpAcceptance-' + $label + '.msv'
$muteKeys = @('MasterVolume','SFXVolume','SpeechVolume','UnitSpeechVolume','MusicVolume')
$excerptRegex = 'ACCEPT run=| STEP |MP_HOST|MP_JOIN|MP_LOAD|MAKE_DESYNC|RESYNC_DATA|MARKET_STALE_NATIVE|MARKET_PENDING|PAIRED_|MP_RESTART_TEST|OBSERVE|Mission (started|ended)|MISSION_ANCHOR|LOBBY_LAUNCH|SAVE_SETTINGS|Lord-data|Lord data|MOD_FAILURE|(?i:desync|out of sync)|\[(Error|Fatal)|\[Message:.*BepInEx.*- Stronghold Crusader Definitive Edition'
$pollMilliseconds = 800
$heartbeatSeconds = 60

function Write-ProgressLine([string]$Message) {
    Write-Host ('[{0:HH:mm:ss.fff}] {1}' -f [DateTime]::Now, $Message)
}

function ConvertTo-JsonText($Value) {
    return (($Value | ConvertTo-Json -Depth 12) -replace '(?<!\r)\n', $crlf) + $crlf
}

function Write-JsonFile([string]$Path, $Value) {
    [IO.File]::WriteAllText($Path, (ConvertTo-JsonText $Value), $utf8)
}

function Write-TextLines([string]$Path, [string[]]$Lines) {
    $text = ''
    if ($Lines -and $Lines.Count) { $text = ($Lines -join $crlf) + $crlf }
    [IO.File]::WriteAllText($Path, $text, $utf8)
}

function New-Side([string]$Name, [string]$Root, [string]$UserRoot) {
    $bep = Join-Path $Root 'BepInEx'
    return [pscustomobject]@{
        Name = $Name; Root = $Root; UserRoot = $UserRoot; Bep = $bep
        Plugins = Join-Path $bep 'plugins'; Patchers = Join-Path $bep 'patchers'
        PatcherFile = Join-Path $bep 'patchers\APIShared.DependencyBootstrap.dll'
        ApiTarget = Join-Path $bep 'plugins\APIShared_Serp'
        Config = Join-Path $bep 'config'; Ui = Join-Path $bep 'config\UiAcceptance'
        Log = Join-Path $bep 'LogOutput.log'; Saves = Join-Path $UserRoot 'Saves'
        # Same volume as the plugins (directory moves) and the name Invoke-InGameTest refuses to overwrite.
        Rollback = Join-Path $bep 'SerpAutomationRollback'
        SessionPath = Join-Path $bep 'SerpAutomationRollback\paired-session.json'
        # Layout session state: LayoutActive = this run owns a session (applied, reused or not yet restored).
        LoadedSession = $null; LayoutActive = $false; LayoutAction = ''; LayoutReason = ''; LayoutState = ''
        SessionIdentity = ''; SessionCreated = ''; SessionRuns = 0; SessionLastRun = ''; SessionSource = $null
        PendingRun = $null; PendingBackup = $null; LayoutKept = $false; LayoutRestored = $false; TestModRefresh = $false
        Scenario = $null
        Swaps = New-Object 'System.Collections.Generic.List[object]'
        Snapshots = New-Object 'System.Collections.Generic.List[object]'
        TestPaths = New-Object 'System.Collections.Generic.List[string]'
        PluginTree = $null; PatcherTree = $null; SaveTree = $null; Plan = @()
        Tail = $null; LastAccept = ''; Summary = $null
        Changed = $false; Exited = $false; Restored = $false; Deferred = $false
        RestoreErrors = New-Object 'System.Collections.Generic.List[string]'
        SavesRestored = $false; ArchivedSave = $null
        # -Manual: BepInEx.cfg debug-level change and the session monitor.
        BepCfg = Join-Path $bep 'config\BepInEx.cfg'; DebugLog = $null; Monitor = $null
    }
}

function Get-Other($Side) { if ($Side.Name -eq 'host') { return $clientSide } else { return $hostSide } }

# --- input validation -------------------------------------------------------------------------------

function Read-Scenario([string]$Path, [string]$Role, [switch]$AllowNoQuit) {
    $full = Get-InGameTestPath $Path
    Assert-InGameTestUnlinked $full
    if (-not [IO.File]::Exists($full)) { throw "Scenario not found: $full" }
    $data = [IO.File]::ReadAllText($full) | ConvertFrom-Json
    if ($data.enabled -ne $true) { throw "Scenario must set enabled=true: $full" }
    # Manual observer scenarios run until the user exits the game.
    if (-not $AllowNoQuit -and $data.PSObject.Properties['quitWhenDone'] -and $data.quitWhenDone -ne $true) {
        throw "Scenario must quit when done so both PCs can be restored: $full"
    }
    if ([string]$data.role -ne $Role) { throw "Scenario role must be '$Role': $full" }
    $steps = @($data.steps)
    if ($steps.Count -eq 0) { throw "Scenario has no steps: $full" }
    $ids = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($step in $steps) {
        $id = [string]$step.id
        if ($id -notmatch '^\S+$' -or -not $ids.Add($id)) { throw "Scenario step ids must be unique and non-empty: $full ($id)" }
    }
    return [pscustomobject]@{ Path = $full; Data = $data; Ids = $ids; Does = @($steps | ForEach-Object { [string]$_.do }) }
}

function Get-SaveEvidenceMode($HostScenario, $ClientScenario, [bool]$ManualRun) {
    if ($ClientScenario.Data.PSObject.Properties['saveEvidence']) { throw 'saveEvidence belongs to the host scenario.' }
    if (-not $HostScenario.Data.PSObject.Properties['saveEvidence']) {
        if ($HostScenario.Does -contains 'Mp.SaveConfirmed') { return 'lord-data' }
        return 'none'
    }
    if ($HostScenario.Data.saveEvidence -cne 'round-trip' -or $ManualRun) {
        throw 'Explicit saveEvidence must be round-trip in an automated paired scenario.'
    }
    foreach ($requirement in @(
        @{ Scenario = $HostScenario; Step = 'Mp.SaveConfirmed' },
        @{ Scenario = $ClientScenario; Step = 'Mp.AwaitSaveFile' },
        @{ Scenario = $HostScenario; Step = 'Mp.LoadSave' }
    )) {
        $commands = @($requirement.Scenario.Data.steps | Where-Object { $_.do -ceq $requirement.Step })
        if ($commands.Count -ne 1 -or
            ($commands[0].PSObject.Properties['args'] -and @($commands[0].args).Count -gt 0) -or
            $commands[0].stopOnFail -ne $true) {
            throw "Round-trip save evidence requires one $($requirement.Step) step using the default run save name and stopOnFail=true."
        }
    }
    return 'round-trip'
}

function Get-RelayRules {
    $rules = New-Object 'System.Collections.Generic.List[object]'
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($side in @($hostSide, $clientSide)) {
        if (-not $side.Scenario.Data.PSObject.Properties['relay']) { continue }
        foreach ($entry in @($side.Scenario.Data.relay)) {
            $from = [string]$entry.from
            $step = [string]$entry.step
            $checkpoint = [string]$entry.checkpoint
            $result = 'PASS'
            if ($entry.PSObject.Properties['result'] -and $entry.result) { $result = ([string]$entry.result).ToUpperInvariant() }
            if ($from -notin @('host','client') -or $step -notmatch '^\S+$' -or $checkpoint -notmatch '^[A-Za-z0-9_-]{1,48}$' -or
                $result -notin @('PASS','FAIL','ANY')) {
                throw "Invalid relay entry in $($side.Scenario.Path): from=$from step=$step checkpoint=$checkpoint result=$result"
            }
            $source = if ($from -eq 'host') { $hostSide } else { $clientSide }
            if (-not $source.Scenario.Ids.Contains($step)) { throw "Relay step '$step' is not a step of the $from scenario." }
            if ($seen.Add("$from|$step|$checkpoint|$result")) {
                $rules.Add([pscustomobject]@{ From = $from; Step = $step; Checkpoint = $checkpoint; Result = $result; Sent = $false })
            }
        }
    }
    return ,$rules
}

function Get-SubTree($Tree, [string]$Name) {
    $prefix = $Name + '\'
    $files = @{}
    $directories = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($key in $Tree.Files.Keys) {
        if ($key.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { $files[$key.Substring($prefix.Length)] = $Tree.Files[$key] }
    }
    foreach ($directory in $Tree.Directories) {
        if ($directory.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { $null = $directories.Add($directory.Substring($prefix.Length)) }
    }
    return [pscustomobject]@{ Exists = $Tree.Directories.Contains($Name); Files = $files; Directories = $directories }
}

function Test-TreeEqual($Left, $Right) {
    if ($Left.Exists -ne $Right.Exists -or $Left.Files.Count -ne $Right.Files.Count -or -not $Left.Directories.SetEquals($Right.Directories)) { return $false }
    foreach ($key in $Left.Files.Keys) { if ($Right.Files[$key] -cne $Left.Files[$key]) { return $false } }
    return $true
}

function Get-TopNames($Tree) {
    return @($Tree.Directories | Where-Object { $_ -notmatch '\\' })
}

function Get-ExpectedPluginTree {
    $files = @{}
    $directories = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($key in $layoutTree.Files.Keys) {
        if (-not $key.StartsWith('APIShared_Serp\', [StringComparison]::OrdinalIgnoreCase)) { $files[$key] = $layoutTree.Files[$key] }
    }
    foreach ($directory in $layoutTree.Directories) {
        if ($directory -ine 'APIShared_Serp' -and -not $directory.StartsWith('APIShared_Serp\', [StringComparison]::OrdinalIgnoreCase)) {
            $null = $directories.Add($directory)
        }
    }
    $null = $directories.Add('APIShared_Serp')
    foreach ($directory in $apiTree.Directories) { $null = $directories.Add('APIShared_Serp\' + $directory) }
    foreach ($key in $apiTree.Files.Keys) { $files['APIShared_Serp\' + $key] = $apiTree.Files[$key] }
    return [pscustomobject]@{ Exists = $true; Files = $files; Directories = $directories }
}

function ConvertTo-TreeRecord($Tree) {
    if (-not $Tree) { return $null }
    $files = [ordered]@{}
    foreach ($key in @($Tree.Files.Keys | Sort-Object)) { $files[$key] = $Tree.Files[$key] }
    return [ordered]@{ Exists = $Tree.Exists; Directories = @($Tree.Directories | Sort-Object); Files = $files }
}

function ConvertFrom-TreeRecord($Record) {
    if ($null -eq $Record) { return $null }
    $files = @{}
    if ($Record.Files) { foreach ($property in $Record.Files.PSObject.Properties) { $files[$property.Name] = [string]$property.Value } }
    $directories = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($directory in @($Record.Directories)) { if ($null -ne $directory) { $null = $directories.Add([string]$directory) } }
    return [pscustomobject]@{ Exists = [bool]$Record.Exists; Files = $files; Directories = $directories }
}

# --- layout sessions ----------------------------------------------------------------------------------

function Get-SizeTree([string]$Path) {
    # Like Get-InGameTestTree (same relative keys, same link refusal) but with lengths instead of hashes:
    # one directory enumeration per folder, no file reads, so it is cheap over the client share.
    Assert-InGameTestUnlinked $Path
    $files = @{}
    $directories = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $exists = [IO.Directory]::Exists($Path)
    if ($exists) {
        $prefix = [IO.Path]::GetFullPath($Path).TrimEnd('\', '/') + '\'
        $pending = New-Object 'System.Collections.Generic.Stack[object]'
        $pending.Push((New-Object IO.DirectoryInfo $Path))
        while ($pending.Count) {
            foreach ($entry in $pending.Pop().GetFileSystemInfos()) {
                if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked entry is unsupported: $($entry.FullName)" }
                $relative = $entry.FullName.Substring($prefix.Length)
                if ($entry.Attributes -band [IO.FileAttributes]::Directory) { $null = $directories.Add($relative); $pending.Push($entry) }
                else { $files[$relative] = [long]$entry.Length }
            }
        }
    }
    return [pscustomobject]@{ Exists = $exists; Files = $files; Directories = $directories }
}

function Remove-RollbackTree($Side) {
    # Link check by enumeration (Remove-InGameTestTree would hash every backup file over the share first).
    if ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Side.Rollback).TrimEnd('\')) -ine [IO.Path]::GetFullPath($Side.Bep).TrimEnd('\')) {
        throw "Refusing recursive removal outside $($Side.Bep)."
    }
    $inventory = Get-SizeTree $Side.Rollback
    if ($inventory.Exists) { [IO.Directory]::Delete($Side.Rollback, $true) }
}

function Test-TestModKey([string]$Key) {
    return $Key -ieq $testModName -or $Key.StartsWith($testModName + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Get-TreeWithoutTestMod($Tree) {
    # The test mod changes often while tests are developed; it is test infrastructure, not part of the layout under
    # test. Identity and installed check ignore it, a reused session refreshes it instead (Update-SideTestMod).
    $files = @{}
    $directories = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($key in $Tree.Files.Keys) { if (-not (Test-TestModKey $key)) { $files[$key] = $Tree.Files[$key] } }
    foreach ($directory in $Tree.Directories) { if (-not (Test-TestModKey $directory)) { $null = $directories.Add($directory) } }
    return [pscustomobject]@{ Exists = $Tree.Exists; Files = $files; Directories = $directories }
}

function Get-LayoutIdentity {
    # Identity of the requested layout: the deployed plugin inventory (layout without its APIShared_Serp, plus the
    # supplied APIShared plugin, without the test mod) and the bootstrap patcher hash. All inputs are hashed locally on the host.
    $layoutPlugins = Get-TreeWithoutTestMod $expectedPlugins
    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add('paired-layout-identity/2')
    [string[]]$directories = @($layoutPlugins.Directories | ForEach-Object { ([string]$_).ToUpperInvariant() })
    [Array]::Sort($directories, [StringComparer]::Ordinal)
    foreach ($directory in $directories) { $lines.Add('D|' + $directory) }
    [string[]]$entries = @($layoutPlugins.Files.Keys | ForEach-Object { ([string]$_).ToUpperInvariant() + '|' + $layoutPlugins.Files[$_] })
    [Array]::Sort($entries, [StringComparer]::Ordinal)
    foreach ($entry in $entries) { $lines.Add('F|' + $entry) }
    $lines.Add('P|' + $patcherHash)
    return Get-Sha256Upper ($lines.ToArray() -join "`n")
}

function Get-ExpectedSizeTree {
    $layoutPlugins = Get-TreeWithoutTestMod $expectedPlugins
    $files = @{}
    foreach ($key in $layoutPlugins.Files.Keys) {
        $source = if ($key.StartsWith('APIShared_Serp\', [StringComparison]::OrdinalIgnoreCase)) { Join-Path $api $key.Substring(15) } else { Join-Path $layout $key }
        $files[$key] = [long](New-Object IO.FileInfo $source).Length
    }
    return [pscustomobject]@{ Exists = $true; Files = $files; Directories = $layoutPlugins.Directories }
}

function Get-TreeDifferences($Expected, $Actual, [string]$What) {
    $problems = New-Object 'System.Collections.Generic.List[string]'
    if (-not $Actual.Directories.SetEquals($Expected.Directories)) { $problems.Add('plugin directory list differs') }
    foreach ($key in $Expected.Files.Keys) {
        if (-not $Actual.Files.ContainsKey($key)) { $problems.Add('missing ' + $key) }
        elseif ([string]$Actual.Files[$key] -cne [string]$Expected.Files[$key]) { $problems.Add("$What differs " + $key) }
    }
    foreach ($key in $Actual.Files.Keys) { if (-not $Expected.Files.ContainsKey($key)) { $problems.Add('extra ' + $key) } }
    return ,$problems
}

function Test-InstalledLayout($Side, [switch]$Full) {
    # Returns the differences between the installed plugins/patcher and the requested layout (empty = installed).
    # The test mod is excluded on both sides of the comparison (see Get-TreeWithoutTestMod).
    $problems = New-Object 'System.Collections.Generic.List[string]'
    try {
        if ([IO.Directory]::GetFiles($Side.Plugins).Length -ne 0) { $problems.Add('loose files in BepInEx\plugins') }
        if ($Full) {
            $actual = Get-TreeWithoutTestMod (Get-InGameTestTree $Side.Plugins)
            foreach ($problem in (Get-TreeDifferences (Get-TreeWithoutTestMod $expectedPlugins) $actual 'digest')) { $problems.Add($problem) }
        } else {
            $expected = Get-ExpectedSizeTree
            $actual = Get-TreeWithoutTestMod (Get-SizeTree $Side.Plugins)
            foreach ($problem in (Get-TreeDifferences $expected $actual 'size')) { $problems.Add($problem) }
            if ($problems.Count -eq 0) {
                # Content spot check of the files that identify each package version and the APIShared pair.
                foreach ($key in @($expectedPlugins.Files.Keys | Where-Object { -not (Test-TestModKey $_) -and $_ -match '^[^\\]+\\(info\.json|dependency-bootstrap-pair\.json)$' })) {
                    if ((Get-ApiRepositoryHash (Join-Path $Side.Plugins $key)) -cne $expectedPlugins.Files[$key]) { $problems.Add('content differs ' + $key) }
                }
            }
        }
        if (-not [IO.File]::Exists($Side.PatcherFile)) { $problems.Add('bootstrap patcher missing') }
        elseif ((Get-ApiRepositoryHash $Side.PatcherFile) -cne $patcherHash) { $problems.Add('bootstrap patcher differs') }
    } catch { $problems.Add('check failed: ' + $_.Exception.Message) }
    return ,$problems
}

function ConvertTo-SwapRecord($Swap) {
    $original = if ($Swap.Directory) { ConvertTo-TreeRecord $Swap.Original } else { $Swap.Original }
    $restored = [bool]($Swap.PSObject.Properties['Restored'] -and $Swap.Restored)
    return [ordered]@{
        Target = $Swap.Target; Backup = $Swap.Backup; Stage = $Swap.Stage; Directory = [bool]$Swap.Directory
        Saved = [bool]$Swap.Saved; Published = [bool]$Swap.Published; Restored = $restored; Original = $original
    }
}

function ConvertTo-SnapshotRecord($Snapshot) {
    $inventory = if ($Snapshot.Directory) { ConvertTo-TreeRecord $Snapshot.Inventory } else { $Snapshot.Inventory }
    return [ordered]@{ Path = $Snapshot.Path; Backup = $Snapshot.Backup; Directory = [bool]$Snapshot.Directory; Inventory = $inventory }
}

function New-PendingRunRecord($Side) {
    # Run-level state (settings, saves, test files) that this run could not restore; completed by the next start.
    $snapshots = New-Object 'System.Collections.Generic.List[object]'
    foreach ($snapshot in $Side.Snapshots) { $snapshots.Add((ConvertTo-SnapshotRecord $snapshot)) }
    return [ordered]@{
        Run = $label; Evidence = $run; Backup = $backup; Recovery = (Join-Path $run 'recovery.json'); SaveName = $testSaveName
        SavesDone = [bool]$Side.SavesRestored; OriginalSaves = (ConvertTo-TreeRecord $Side.SaveTree)
        Snapshots = $snapshots.ToArray(); TestPaths = $Side.TestPaths.ToArray()
    }
}

function Write-LayoutSession($Side) {
    $swaps = New-Object 'System.Collections.Generic.List[object]'
    foreach ($swap in $Side.Swaps) { $swaps.Add((ConvertTo-SwapRecord $swap)) }
    $record = [ordered]@{
        Kind = 'SerpPairedLayoutSession'; Schema = 1; Side = $Side.Name; Root = $Side.Root
        State = $Side.LayoutState; Identity = $Side.SessionIdentity; Source = $Side.SessionSource
        Created = $Side.SessionCreated; Updated = [DateTime]::Now.ToString('yyyy-MM-ddTHH:mm:ss.fffzzz')
        Runs = $Side.SessionRuns; LastRun = $Side.SessionLastRun
        OriginalPlugins = (ConvertTo-TreeRecord $Side.PluginTree); OriginalPatchers = (ConvertTo-TreeRecord $Side.PatcherTree)
        Swaps = $swaps.ToArray(); PendingRun = $Side.PendingRun
    }
    Write-AtomicText $Side.SessionPath (ConvertTo-JsonText $record)
}

function Read-LayoutSession($Side) {
    # $null = no rollback folder. A rollback folder without a valid session record is an unknown state.
    if (-not (Test-Path -LiteralPath $Side.Rollback)) { return $null }
    $unknown = "$($Side.Name): $($Side.Rollback) exists without a valid layout session record (unknown state; recover manually from the last run's recovery.json)"
    if (-not [IO.File]::Exists($Side.SessionPath)) { throw "$unknown." }
    try { $data = [IO.File]::ReadAllText($Side.SessionPath) | ConvertFrom-Json } catch { throw "${unknown}: $($_.Exception.Message)" }
    $rootOk = $data.Root -and [IO.Path]::GetFullPath([string]$data.Root).TrimEnd('\') -ieq [IO.Path]::GetFullPath($Side.Root).TrimEnd('\')
    if ($data.Kind -cne 'SerpPairedLayoutSession' -or $data.Schema -ne 1 -or [string]$data.Side -cne $Side.Name -or -not $rootOk -or
        [string]$data.State -notin @('applied','partial','restored') -or $null -eq $data.OriginalPlugins -or $null -eq $data.OriginalPatchers) {
        throw "${unknown}: record fields invalid."
    }
    $swaps = New-Object 'System.Collections.Generic.List[object]'
    foreach ($entry in @($data.Swaps)) {
        if ($null -eq $entry) { continue }
        foreach ($path in @($entry.Target, $entry.Backup, $entry.Stage)) {
            if ($path -and -not (Test-InGameTestInside ([string]$path) $Side.Bep)) { throw "${unknown}: swap path outside BepInEx: $path" }
        }
        if (-not $entry.Target -or -not $entry.Backup) { throw "${unknown}: swap without Target/Backup." }
        $original = if ($entry.Directory) { ConvertFrom-TreeRecord $entry.Original } elseif ($null -ne $entry.Original) { [string]$entry.Original } else { $null }
        if ($entry.Directory -and $null -eq $original) { throw "${unknown}: directory swap without original inventory." }
        $swaps.Add([pscustomobject]@{
            Target = [string]$entry.Target; Backup = [string]$entry.Backup; Stage = $(if ($entry.Stage) { [string]$entry.Stage } else { $null })
            Directory = [bool]$entry.Directory; Original = $original
            Saved = [bool]$entry.Saved; Published = [bool]$entry.Published; Restored = [bool]$entry.Restored
        })
    }
    return [pscustomobject]@{
        Path = $Side.SessionPath; State = [string]$data.State; Identity = [string]$data.Identity; Source = $data.Source
        Created = [string]$data.Created; Runs = [int]$data.Runs; LastRun = [string]$data.LastRun
        OriginalPlugins = (ConvertFrom-TreeRecord $data.OriginalPlugins); OriginalPatchers = (ConvertFrom-TreeRecord $data.OriginalPatchers)
        Swaps = $swaps; PendingRun = $data.PendingRun
    }
}

function Use-LayoutSession($Side, $Session) {
    $Side.LayoutActive = $true
    $Side.Swaps = $Session.Swaps
    $Side.PluginTree = $Session.OriginalPlugins
    $Side.PatcherTree = $Session.OriginalPatchers
    $Side.LayoutState = $Session.State
    $Side.SessionIdentity = $Session.Identity
    $Side.SessionSource = $Session.Source
    $Side.SessionCreated = $Session.Created
    $Side.SessionRuns = $Session.Runs
    $Side.SessionLastRun = $Session.LastRun
    $Side.PendingRun = $Session.PendingRun
}

function Get-LayoutDecision($Side, $Session) {
    if (-not $Session) { return [pscustomobject]@{ Action = 'apply'; Reason = 'no layout session' } }
    if ($Session.State -ne 'applied') { return [pscustomobject]@{ Action = 'rebuild'; Reason = "session state is $($Session.State)" } }
    if ($Session.Identity -cne $layoutIdentity) { return [pscustomobject]@{ Action = 'rebuild'; Reason = 'session holds a different layout or APIShared pair' } }
    $full = $Side.Name -eq 'host'
    $problems = Test-InstalledLayout $Side -Full:$full
    if ($problems.Count) {
        return [pscustomobject]@{ Action = 'rebuild'; Reason = ('installed layout differs from the session: ' + (@($problems | Select-Object -First 5) -join '; ')) }
    }
    $testMod = Get-TestModState $Side $Session
    if ($testMod.Problem) { return [pscustomobject]@{ Action = 'rebuild'; Reason = $testMod.Problem } }
    $how = if ($full) { 'full hash check' } else { 'file list, sizes, info.json/pair hashes, patcher hash' }
    $note = if ($testMod.Refresh) { "; test mod $testModName differs from the layout and is refreshed" } else { "; test mod $testModName identical" }
    return [pscustomobject]@{ Action = 'reuse'; Reason = "identity matches, installed layout verified ($how)$note; session runs so far: $($Session.Runs)"; RefreshTestMod = $testMod.Refresh }
}

function Find-TestModSwap($Swaps, [string]$Target) {
    $full = [IO.Path]::GetFullPath($Target).TrimEnd('\')
    $found = @($Swaps | Where-Object { $_ -and $_.Directory -and -not ($_.PSObject.Properties['Restored'] -and $_.Restored) -and
        [IO.Path]::GetFullPath([string]$_.Target).TrimEnd('\') -ieq $full })
    if ($found.Count -gt 1) { throw "several open swaps for $Target" }
    if ($found.Count) { return $found[0] }
    return $null
}

function Get-TestModState($Side, $Session) {
    # Reused session: the installed test mod against the layout's copy (full hash on both PCs; the package is small).
    try {
        $target = Join-Path $Side.Plugins $testModName
        $have = Get-InGameTestTree $target
        if (Test-TreeEqual (Get-SubTree $layoutTree $testModName) $have) { return [pscustomobject]@{ Refresh = $false; Problem = $null } }
        $swap = Find-TestModSwap $Session.Swaps $target
        # Without a swap the test mod equalled the layout's at apply time, so the installed one must still be the original.
        if (-not $swap -and -not (Test-TreeEqual (Get-SubTree $Session.OriginalPlugins $testModName) $have)) {
            return [pscustomobject]@{ Refresh = $false; Problem = "installed $testModName is neither the layout's nor the recorded original (no swap)" }
        }
        return [pscustomobject]@{ Refresh = $true; Problem = $null }
    } catch { return [pscustomobject]@{ Refresh = $false; Problem = "test mod check failed: $($_.Exception.Message)" } }
}

function Update-SideTestMod($Side) {
    # Reused session with a changed test mod: replace the installed copy with the layout's. The original pre-series
    # package stays untouched in the session's swap backup, so restoring the swaps still yields (and verifies) the
    # recorded original inventory. Staging and the replaced copy live in the rollback folder (same volume).
    $target = Join-Path $Side.Plugins $testModName
    $source = Join-Path $layout $testModName
    $want = Get-SubTree $layoutTree $testModName
    $stage = Join-Path $Side.Rollback 'testmod-refresh.new'
    $old = Join-Path $Side.Rollback 'testmod-refresh.old'
    foreach ($path in @($stage, $old)) { Remove-InGameTestTree $path $Side.Rollback }
    $Side.Changed = $true
    $swap = Find-TestModSwap $Side.Swaps $target
    if (-not $swap) {
        # The original is still installed: back it up like an apply does; the session records the new swap.
        $backupPath = Join-Path $Side.Rollback 'testmod'
        if ((Test-Path -LiteralPath $backupPath) -or (Test-Path -LiteralPath ($backupPath + '.new'))) {
            throw "$($Side.Name): $backupPath exists without a recorded swap (unknown state)."
        }
        try { Set-InGameTestPackage $source $target $backupPath $Side.Swaps -Directory }
        finally { Write-LayoutSession $Side }
    } else {
        Copy-InGameTestTree $source $stage $want
        $moved = $false
        if ([IO.Directory]::Exists($target)) { [IO.Directory]::Move($target, $old); $moved = $true }
        try { [IO.Directory]::Move($stage, $target) }
        catch { if ($moved -and -not [IO.Directory]::Exists($target)) { [IO.Directory]::Move($old, $target) }; throw }
        try { Remove-InGameTestTree $old $Side.Rollback } catch { Write-ProgressLine "$($Side.Name): replaced test mod copy kept in $($old): $($_.Exception.Message)" }
    }
    Assert-InGameTestTree $target $want
    Write-ProgressLine "TEST MOD REFRESHED side=$($Side.Name) ($testModName, $($want.Files.Count) files, verified by hash)"
}

function Complete-PendingRun($Side) {
    $pending = $Side.PendingRun
    if (-not $pending) { return }
    Write-ProgressLine "$($Side.Name): completing the unfinished settings/saves recovery of run $($pending.Run)."
    $errors = New-Object 'System.Collections.Generic.List[string]'
    if (-not $pending.SavesDone -and $pending.OriginalSaves) {
        $current = $Side.SaveTree
        $Side.SaveTree = ConvertFrom-TreeRecord $pending.OriginalSaves
        try {
            if (-not [IO.Directory]::Exists([string]$pending.Evidence)) { throw "evidence folder of run $($pending.Run) is missing: $($pending.Evidence)" }
            Complete-SideSaves $Side ([string]$pending.SaveName) ([string]$pending.Evidence)
        } catch { $errors.Add($_.Exception.Message) }
        finally { $Side.SaveTree = $current; $Side.SavesRestored = $false; $Side.ArchivedSave = $null }
    }
    foreach ($entry in @($pending.Snapshots)) {
        if ($null -eq $entry) { continue }
        try {
            if (-not (Test-InGameTestInside ([string]$entry.Backup) ([string]$pending.Backup))) { throw "snapshot backup outside the run backup: $($entry.Backup)" }
            $inventory = if ($entry.Directory) { ConvertFrom-TreeRecord $entry.Inventory } elseif ($null -ne $entry.Inventory) { [string]$entry.Inventory } else { $null }
            $snapshot = [pscustomobject]@{ Path = [string]$entry.Path; Backup = [string]$entry.Backup; Directory = [bool]$entry.Directory; Inventory = $inventory }
            if (-not $snapshot.Directory -and -not $snapshot.Inventory -and -not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($snapshot.Path))) { continue }
            Restore-InGameTestState $snapshot
        } catch { $errors.Add($_.Exception.Message) }
    }
    foreach ($path in @($pending.TestPaths)) {
        try { if ($path -and [IO.File]::Exists([string]$path)) { [IO.File]::Delete([string]$path) } } catch { $errors.Add($_.Exception.Message) }
    }
    if ($errors.Count) { throw "$($Side.Name): the unfinished recovery of run $($pending.Run) failed: $($errors -join ' | ') (details: $($pending.Recovery))" }
    $Side.PendingBackup = [pscustomobject]@{ Backup = [string]$pending.Backup; Evidence = [string]$pending.Evidence }
    $Side.PendingRun = $null
    Write-LayoutSession $Side
    Write-ProgressLine "$($Side.Name): settings, saves and test files of run $($pending.Run) restored and verified."
}

function Remove-PendingBackups {
    # Both sides' snapshots of one run share one local backup folder; remove it only when no side still needs it.
    foreach ($side in @($hostSide, $clientSide)) {
        $done = $side.PendingBackup
        if (-not $done) { continue }
        $side.PendingBackup = $null
        $needed = @(@($hostSide, $clientSide) | Where-Object { $_.PendingRun -and [string]$_.PendingRun.Backup -ieq $done.Backup }).Count
        if ($needed -or -not [IO.Directory]::Exists($done.Backup)) { continue }
        try { Remove-InGameTestTree $done.Backup $done.Evidence } catch { Write-ProgressLine ('Old run backup kept: ' + $_.Exception.Message) }
    }
}

function Restore-SideLayout($Side, [switch]$KeepSession) {
    # Reverses the session's swaps (already restored ones are skipped), verifies the original plugin and patcher
    # inventories (full hash) and removes the rollback folder with the session, unless -KeepSession.
    $errors = New-Object 'System.Collections.Generic.List[string]'
    for ($i = $Side.Swaps.Count - 1; $i -ge 0; $i--) {
        $swap = $Side.Swaps[$i]
        if ($swap.PSObject.Properties['Restored'] -and $swap.Restored) { continue }
        try { Restore-InGameTestPackage $swap; $swap | Add-Member -NotePropertyName Restored -NotePropertyValue $true -Force }
        catch { $errors.Add("$($Side.Name): " + $_.Exception.Message) }
    }
    if ($errors.Count -eq 0) {
        try { Assert-InGameTestTree $Side.Plugins $Side.PluginTree } catch { $errors.Add("$($Side.Name): " + $_.Exception.Message) }
        try { Assert-InGameTestTree $Side.Patchers $Side.PatcherTree } catch { $errors.Add("$($Side.Name): " + $_.Exception.Message) }
    }
    if ($errors.Count -eq 0) {
        $Side.LayoutState = 'restored'
        $Side.LayoutRestored = $true
        if ($KeepSession) { try { Write-LayoutSession $Side } catch { $errors.Add("$($Side.Name): session: " + $_.Exception.Message) } }
        else {
            try { Remove-RollbackTree $Side; $Side.LayoutActive = $false } catch { $errors.Add("$($Side.Name): " + $_.Exception.Message) }
        }
    } else {
        $Side.LayoutState = 'partial'
        try { Write-LayoutSession $Side } catch { $errors.Add("$($Side.Name): session: " + $_.Exception.Message) }
    }
    return ,$errors
}

function Initialize-SideLayout($Side) {
    # Before any change of this run: finish an unfinished earlier run, then reuse, rebuild or newly apply the layout.
    $session = $Side.LoadedSession
    if ($session) {
        Use-LayoutSession $Side $session
        Complete-PendingRun $Side
    }
    $decision = Get-LayoutDecision $Side $session
    $Side.LayoutAction = $decision.Action
    $Side.LayoutReason = $decision.Reason
    $Side.TestModRefresh = [bool]$decision.RefreshTestMod
    Write-ProgressLine ("$($Side.Name) layout: $($decision.Action) ($($decision.Reason))")
    if ($decision.Action -eq 'rebuild') {
        $errors = Restore-SideLayout $Side
        if ($errors.Count) { throw ("$($Side.Name): restoring the previous layout session failed: " + ($errors -join ' | ')) }
        # The originals were just verified by full hash; Get-SidePlan uses them without hashing again.
        $Side.Swaps = New-Object 'System.Collections.Generic.List[object]'
        Write-ProgressLine "$($Side.Name): previous layout session restored and verified; applying the requested layout."
    } elseif ($decision.Action -eq 'apply') {
        $Side.PluginTree = $null
        $Side.PatcherTree = $null
    }
}

function Assert-LordFixture {
    $inventory = Get-InGameTestTree $lordSource
    if ($inventory.Files.Count -ne 2 -or $inventory.Directories.Count -ne 0) { throw 'The trading AI fixture differs from the reviewed fixture.' }
    foreach ($name in $lordHashes.Keys) {
        if ($inventory.Files[$name] -cne $lordHashes[$name]) { throw 'The trading AI AIC or AIV differs from the reviewed fixture.' }
    }
    $installed = Get-InGameTestTree $lordTarget
    if ($installed.Exists) { Assert-InGameTestTree $lordTarget $inventory }
    return [pscustomobject]@{ Inventory = $inventory; Installed = $installed.Exists }
}

# --- read-only state checks --------------------------------------------------------------------------

function Test-LogUnlocked([string]$Path) {
    # A running game keeps LogOutput.log open for writing; a read open without write sharing then fails.
    try { $stream = [IO.File]::Open($Path, 'Open', 'Read', 'Read'); $stream.Dispose(); return $true }
    catch [IO.FileNotFoundException] { return $true }
    catch { return $false }
}

function Test-SessionState($Side) {
    # Loads the layout session once (cached for Initialize-SideLayout); a rollback folder without one blocks.
    try {
        $Side.LoadedSession = Read-LayoutSession $Side
        if (-not $Side.LoadedSession) { return [pscustomobject]@{ Ok = $true; Detail = "no layout session ($($Side.Rollback) absent)" } }
        $s = $Side.LoadedSession
        $pending = if ($s.PendingRun) { "; unfinished recovery of run $($s.PendingRun.Run) will be completed first" } else { '' }
        return [pscustomobject]@{ Ok = $true; Detail = "layout session $($s.State), created $($s.Created), runs $($s.Runs)$pending ($($s.Path))" }
    } catch { return [pscustomobject]@{ Ok = $false; Detail = $_.Exception.Message } }
}

function Get-Preconditions([switch]$RestoreOnly, [switch]$ManualRun) {
    $checks = New-Object 'System.Collections.Generic.List[object]'
    function Add-Check([string]$Name, [bool]$Ok, [string]$Detail) { $checks.Add([pscustomobject]@{ Check = $Name; Ok = $Ok; Detail = $Detail }) }
    $games = @(Get-Process -Name $gameName -ErrorAction SilentlyContinue)
    Add-Check 'host-game-closed' ($games.Count -eq 0) ('game processes: ' + $games.Count)
    foreach ($game in $games) { $game.Dispose() }
    Add-Check 'host-log-free' (Test-LogUnlocked $hostSide.Log) $hostSide.Log
    $state = Test-SessionState $hostSide
    Add-Check 'host-rollback-state' $state.Ok $state.Detail
    if ($RestoreOnly) {
        $shareOk = [IO.Directory]::Exists($clientSide.Bep)
        Add-Check 'client-share' $shareOk $clientSide.Bep
        if ($shareOk) {
            Add-Check 'client-log-free' (Test-LogUnlocked $clientSide.Log) ('client game must be closed: ' + $clientSide.Log)
            $state = Test-SessionState $clientSide
            Add-Check 'client-rollback-state' $state.Ok $state.Detail
        }
        return ,$checks
    }
    Add-Check 'host-install' ([IO.File]::Exists($exe) -and [IO.Directory]::Exists($hostSide.Plugins)) $exe
    if ($ManualRun) {
        # The user starts both games normally via Steam: no watcher, no muting, no steam_appid.txt.
        Add-Check 'client-share' ([IO.Directory]::Exists($clientSide.Plugins) -and [IO.Directory]::Exists($clientSide.Patchers)) $clientSide.Bep
        Add-Check 'client-user-share' ([IO.Directory]::Exists($ClientUserRoot)) $ClientUserRoot
        if ([IO.Directory]::Exists($clientSide.Plugins)) {
            Add-Check 'client-log-free' (Test-LogUnlocked $clientSide.Log) ('client game must be closed: ' + $clientSide.Log)
            $state = Test-SessionState $clientSide
            Add-Check 'client-rollback-state' $state.Ok $state.Detail
            Add-Check 'client-mods-enabled' ([IO.File]::Exists((Join-Path $ClientRoot 'winhttp.dll'))) 'winhttp.dll'
        }
        foreach ($side in @($hostSide, $clientSide)) {
            if (-not [IO.Directory]::Exists($side.Config)) { continue }
            $plan = $null
            try { $plan = Get-DebugLogPlan $side; Add-Check "$($side.Name)-bepinex-cfg" $true $plan.Detail }
            catch { Add-Check "$($side.Name)-bepinex-cfg" $false ($side.BepCfg + ': ' + $_.Exception.Message) }
        }
        return ,$checks
    }
    $steam = @(Get-Process -Name steam -ErrorAction SilentlyContinue)
    Add-Check 'host-steam-running' ($steam.Count -gt 0) 'Steam must run in the interactive session'
    foreach ($process in $steam) { $process.Dispose() }
    $settingsOk = $false
    $settingsDetail = $hostSettings
    if ([IO.File]::Exists($hostSettings)) {
        $text = [IO.File]::ReadAllText($hostSettings)
        $missing = @($muteKeys | Where-Object { [regex]::Matches($text, '(?m)^' + $_ + ':[^\r\n]*').Count -ne 1 })
        $settingsOk = $missing.Count -eq 0
        if (-not $settingsOk) { $settingsDetail = 'expected exactly one of: ' + ($missing -join ', ') }
    }
    Add-Check 'host-settings-audio-keys' $settingsOk $settingsDetail
    $appIdOk = -not [IO.File]::Exists($steamAppId) -or [IO.File]::ReadAllText($steamAppId).Trim() -ceq $appId
    Add-Check 'host-steam-appid' $appIdOk $steamAppId
    $shareOk = [IO.Directory]::Exists($clientSide.Plugins) -and [IO.Directory]::Exists($clientSide.Patchers)
    Add-Check 'client-share' $shareOk $clientSide.Bep
    Add-Check 'client-user-share' ([IO.Directory]::Exists($ClientUserRoot)) $ClientUserRoot
    if ($shareOk) {
        Add-Check 'client-log-free' (Test-LogUnlocked $clientSide.Log) ('client game must be closed: ' + $clientSide.Log)
        $state = Test-SessionState $clientSide
        Add-Check 'client-rollback-state' $state.Ok $state.Detail
        Add-Check 'client-mods-enabled' ([IO.File]::Exists((Join-Path $ClientRoot 'winhttp.dll'))) 'winhttp.dll (the watcher refuses with mods-disabled otherwise)'
    }
    Add-Check 'client-start-watcher' (Test-ClientStartWatcher $ClientRoot) ('heartbeat ' + (Join-Path (Get-ClientStartFolder $ClientRoot) 'watcher.alive') +
        ' must be fresh; start _Automation\Start-ClientStartWatcher.bat on the client')
    return ,$checks
}

function Get-SidePlan($Side) {
    $rows = New-Object 'System.Collections.Generic.List[object]'
    if ([IO.Directory]::GetFiles($Side.Plugins).Length -ne 0) { throw "$($Side.Name): BepInEx\plugins contains loose files; the layout swap only handles package directories." }
    $Side.SaveTree = Get-InGameTestTree $Side.Saves
    if (@($Side.SaveTree.Files.Keys | Where-Object { [IO.Path]::GetFileName($_) -ieq $testSaveName }).Count) { throw "$($Side.Name): test save already exists." }
    if ($Side.LayoutAction -eq 'reuse') {
        $Side.Plan = @([pscustomobject]@{ Package = "(all packages, layout session $($Side.SessionPath))"; Action = 'reuse' })
        if ($Side.TestModRefresh) { $Side.Plan += [pscustomobject]@{ Package = "$testModName (test mod, from the layout)"; Action = 'refresh' } }
        return
    }
    # A rebuilt (or, in a dry run, to-be-rebuilt) side plans against the session's recorded originals.
    if (-not $Side.PluginTree) { $Side.PluginTree = Get-InGameTestTree $Side.Plugins }
    if (-not $Side.PatcherTree) { $Side.PatcherTree = Get-InGameTestTree $Side.Patchers }
    $names = New-Object 'System.Collections.Generic.SortedSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($name in (Get-TopNames $layoutTree) + (Get-TopNames $Side.PluginTree)) { if ($name -ine 'APIShared_Serp') { $null = $names.Add($name) } }
    foreach ($name in $names) {
        $want = Get-SubTree $layoutTree $name
        $have = Get-SubTree $Side.PluginTree $name
        $action = if ($want.Exists -and $have.Exists) { if (Test-TreeEqual $want $have) { 'keep' } else { 'replace' } }
            elseif ($want.Exists) { 'add' } else { 'move-out' }
        $rows.Add([pscustomobject]@{ Package = $name; Action = $action })
    }
    $haveApi = Get-SubTree $Side.PluginTree 'APIShared_Serp'
    $apiAction = if (-not $haveApi.Exists) { 'add' } elseif (Test-TreeEqual $apiTree $haveApi) { 'keep' } else { 'replace' }
    $rows.Add([pscustomobject]@{ Package = 'APIShared_Serp (pair plugin)'; Action = $apiAction })
    $patcherRelative = 'APIShared.DependencyBootstrap.dll'
    $patcherAction = if (-not $Side.PatcherTree.Files.ContainsKey($patcherRelative)) { 'add' }
        elseif ($Side.PatcherTree.Files[$patcherRelative] -ceq $patcherHash) { 'keep' } else { 'replace' }
    $rows.Add([pscustomobject]@{ Package = 'patchers\APIShared.DependencyBootstrap.dll (pair patcher)'; Action = $patcherAction })
    $Side.Plan = $rows.ToArray()
}

function Get-BackupTargets($Side) {
    if ($Manual) {
        $list = @(
            [pscustomobject]@{ Path = (Join-Path $Side.Config 'UiAcceptance_Serp.cfg'); Name = 'ui.cfg'; Directory = $false; Why = '[Run] observer scenario arming' }
            [pscustomobject]@{ Path = $Side.BepCfg; Name = 'BepInEx.cfg'; Directory = $false; Why = 'Debug disk log level for the session' }
        )
        if ($Side.Name -eq 'host') {
            $list += [pscustomobject]@{ Path = $bfqSettings; Name = 'mod-settings'; Directory = $true; Why = 'personal APIShared mod settings of all mods (changed while playing)' }
            if ($lordState -and -not $lordState.Installed) { $list += [pscustomobject]@{ Path = $lordTarget; Name = 'custom-lord'; Directory = $true; Why = 'trading AI fixture copied in' } }
        }
        return $list
    }
    if ($Side.Name -eq 'host') {
        $list = @(
            [pscustomobject]@{ Path = $hostSettings; Name = 'settings.cfg'; Directory = $false; Why = 'audio muted for the hidden test instance' }
            [pscustomobject]@{ Path = $steamAppId; Name = 'steam_appid.txt'; Directory = $false; Why = 'direct development start' }
            [pscustomobject]@{ Path = $bfqSettings; Name = 'mod-settings'; Directory = $true; Why = 'personal APIShared mod settings of all mods' }
            [pscustomobject]@{ Path = (Join-Path $Side.Config 'UiAcceptance_Serp.cfg'); Name = 'ui.cfg'; Directory = $false; Why = '[Run] scenario arming' }
        )
        if ($lordState -and -not $lordState.Installed) { $list += [pscustomobject]@{ Path = $lordTarget; Name = 'custom-lord'; Directory = $true; Why = 'trading AI fixture copied in' } }
        return $list
    }
    return @(
        [pscustomobject]@{ Path = (Join-Path $Side.Config 'UiAcceptance_Serp.cfg'); Name = 'ui.cfg'; Directory = $false; Why = '[Run] scenario arming' }
        [pscustomobject]@{ Path = (Join-Path $Side.Ui 'join-lobby.txt'); Name = 'join-lobby.txt'; Directory = $false; Why = 'lobby ID hand-over' }
    )
}

# --- log tailing --------------------------------------------------------------------------------------

function Open-SharedRead([string]$Path) {
    $share = [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete
    return [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, $share)
}

function Read-StreamBytes($Stream, [long]$Start, [int]$Count) {
    $Stream.Position = $Start
    $buffer = New-Object byte[] $Count
    $read = 0
    while ($read -lt $Count) {
        $n = $Stream.Read($buffer, $read, $Count - $read)
        if ($n -le 0) { break }
        $read += $n
    }
    if ($read -lt $Count) { $short = New-Object byte[] $read; [Array]::Copy($buffer, $short, $read); return ,$short }
    return ,$buffer
}

function Get-TailSignature($Stream, [long]$End) {
    $count = [int][Math]::Min([long]64, $End)
    if ($count -le 0) { return '' }
    return [Convert]::ToBase64String((Read-StreamBytes $Stream ($End - $count) $count))
}

function New-LogTail([string]$Path) {
    # Starts at the current end; a rewritten log (AppendLog off) is detected by length or by the bytes before the offset.
    $tail = [pscustomobject]@{
        Path = $Path; Offset = [long]0; Signature = ''; Decoder = $utf8.GetDecoder(); Pending = ''; Length = [long]0
        Lines = New-Object 'System.Collections.Generic.List[string]'; Scanned = 0; Rewrites = 0; Error = $null
    }
    try {
        $stream = Open-SharedRead $Path
        try { $tail.Offset = $stream.Length; $tail.Length = $stream.Length; $tail.Signature = Get-TailSignature $stream $tail.Offset }
        finally { $stream.Dispose() }
    } catch [IO.FileNotFoundException] { }
    return $tail
}

function Update-LogTail($Tail, [switch]$Final) {
    $stream = $null
    try { $stream = Open-SharedRead $Tail.Path }
    catch [IO.FileNotFoundException] { $Tail.Error = $null; return }
    catch { $Tail.Error = $_.Exception.Message; return }
    try {
        $Tail.Error = $null
        $length = $stream.Length
        $Tail.Length = $length
        if ($length -lt $Tail.Offset -or ($Tail.Offset -gt 0 -and (Get-TailSignature $stream $Tail.Offset) -cne $Tail.Signature)) {
            if ($Tail.Pending) { $Tail.Lines.Add($Tail.Pending) }
            $Tail.Offset = 0; $Tail.Pending = ''; $Tail.Decoder = $utf8.GetDecoder(); $Tail.Rewrites++
        }
        while ($length -gt $Tail.Offset) {
            $count = [int][Math]::Min([long]33554432, $length - $Tail.Offset)
            $start = $Tail.Offset
            $bytes = Read-StreamBytes $stream $start $count
            if ($bytes.Length -eq 0) { break }
            $chars = New-Object char[] ($Tail.Decoder.GetCharCount($bytes, 0, $bytes.Length))
            $null = $Tail.Decoder.GetChars($bytes, 0, $bytes.Length, $chars, 0)
            $text = $Tail.Pending + (New-Object string (,$chars))
            if ($start -eq 0) { $text = $text.TrimStart([char]0xFEFF) }
            $parts = $text.Split([char]10)
            for ($i = 0; $i -lt $parts.Length - 1; $i++) { $Tail.Lines.Add($parts[$i].TrimEnd([char]13)) }
            $Tail.Pending = $parts[$parts.Length - 1]
            $Tail.Offset = $start + $bytes.Length
            $Tail.Signature = Get-TailSignature $stream $Tail.Offset
        }
    } finally { $stream.Dispose() }
    if ($Final -and $Tail.Pending) { $Tail.Lines.Add($Tail.Pending.TrimEnd([char]13)); $Tail.Pending = '' }
}

# --- run-time actions ---------------------------------------------------------------------------------

function Save-SideState($Side, [string]$Path, [string]$Name, [switch]$Directory) {
    $snapshot = Save-InGameTestState $Path (Join-Path $backup ($Side.Name + '-' + $Name)) -Directory:$Directory
    $Side.Snapshots.Add($snapshot)
}

function Write-AtomicText([string]$Path, [string]$Text) {
    $temporary = $Path + '.tmp'
    [IO.File]::WriteAllText($temporary, $Text, $utf8)
    if (-not [IO.File]::Exists($Path)) { [IO.File]::Move($temporary, $Path); return }
    # The readers poll; a partially written lobby ID could parse as a wrong number, so prefer an atomic replace.
    try { [IO.File]::Replace($temporary, $Path, $null) }
    catch { [IO.File]::Copy($temporary, $Path, $true); [IO.File]::Delete($temporary) }
}

function Publish-Checkpoint($Target, [string]$Checkpoint) {
    $path = Join-Path $Target.Ui ($label + '-' + $Checkpoint + '.ok')
    $Target.TestPaths.Add($path)
    $Target.TestPaths.Add($path + '.tmp')
    if ([IO.File]::Exists($path)) { return }
    Write-AtomicText $path ($label + $crlf)
}

function Set-SideLayout($Side) {
    $Side.Changed = $true
    $null = [IO.Directory]::CreateDirectory($Side.Rollback)
    $Side.LayoutActive = $true
    $Side.LayoutState = 'partial'
    $Side.SessionIdentity = $layoutIdentity
    $Side.SessionSource = [ordered]@{ PluginLayout = $layout; ApiPlugin = $api; ApiPatcher = $patcher }
    $Side.SessionCreated = [DateTime]::Now.ToString('yyyy-MM-ddTHH:mm:ss.fffzzz')
    $Side.SessionRuns = 0
    $Side.SessionLastRun = $label
    $ok = $false
    try {
        Set-InGameTestPluginLayout $layout $Side.Plugins (Join-Path $Side.Rollback 'layout') $Side.Swaps @('APIShared_Serp')
        Set-InGameTestPackage $api $Side.ApiTarget (Join-Path $Side.Rollback 'api') $Side.Swaps -Directory
        Set-InGameTestPackage $patcher $Side.PatcherFile (Join-Path $Side.Rollback 'bootstrap') $Side.Swaps
        Assert-InGameTestApiPair $Side.ApiTarget $Side.PatcherFile
        Assert-InGameTestMod (Join-Path $Side.Plugins 'UiAcceptance_Serp') 'UiAcceptance_Serp'
        Assert-InGameTestTree $Side.Plugins $expectedPlugins
        $Side.LayoutState = 'applied'
        $ok = $true
    } finally {
        # The session record is what makes the rollback folder restorable by a later process.
        try { Write-LayoutSession $Side }
        catch { if ($ok) { throw }; Write-ProgressLine ("$($Side.Name): layout session could not be written: " + $_.Exception.Message) }
    }
}

function Set-SideScenario($Side) {
    $Side.Changed = $true
    $null = [IO.Directory]::CreateDirectory($Side.Ui)
    $copy = Join-Path $Side.Ui ($label + '-' + $Side.Name + '.json')
    $Side.TestPaths.Add($copy)
    [IO.File]::Copy($Side.Scenario.Path, $copy, $false)
    $cfg = @('[Run]', 'Enabled = true', ('Scenario = ' + [IO.Path]::GetFileName($copy)), ('RunLabel = ' + $label), '')
    [IO.File]::WriteAllText((Join-Path $Side.Config 'UiAcceptance_Serp.cfg'), ($cfg -join $crlf), $utf8)
}

function Write-Recovery([string]$State) {
    $record = [ordered]@{ Run = $label; State = $State; Evidence = $run; LocalBackups = $backup; Sides = [ordered]@{} }
    foreach ($side in @($hostSide, $clientSide)) {
        $record.Sides[$side.Name] = [ordered]@{
            Root = $side.Root; Rollback = $side.Rollback; Exited = $side.Exited; Restored = $side.Restored; Deferred = $side.Deferred
            Layout = [ordered]@{ Action = $side.LayoutAction; Reason = $side.LayoutReason; State = $side.LayoutState; Active = $side.LayoutActive; Kept = $side.LayoutKept; Session = $side.SessionPath }
            Note = ('Preferred: Run-Paired.ps1 -RestoreLayout once both games are closed (uses the layout session, incl. unfinished settings/saves). ' +
                'Manual: restore swaps not Restored in reverse order: move Target to Stage if Published, then Backup to Target if Saved. Then restore Snapshots from Backup.')
            Swaps = @($side.Swaps | ForEach-Object { [ordered]@{ Target = $_.Target; Backup = $_.Backup; Stage = $_.Stage; Directory = $_.Directory; Saved = $_.Saved; Published = $_.Published; Restored = [bool]($_.PSObject.Properties['Restored'] -and $_.Restored) } })
            Snapshots = @($side.Snapshots | ForEach-Object {
                $existed = if ($_.Directory) { [bool]$_.Inventory.Exists } else { [bool]$_.Inventory }
                [ordered]@{ Path = $_.Path; Backup = $_.Backup; Directory = $_.Directory; Existed = $existed }
            })
            TestPaths = @($side.TestPaths)
            OriginalPlugins = ConvertTo-TreeRecord $side.PluginTree
            OriginalPatchers = ConvertTo-TreeRecord $side.PatcherTree
            OriginalSaves = ConvertTo-TreeRecord $side.SaveTree
        }
    }
    Write-JsonFile (Join-Path $run 'recovery.json') $record
}

function Complete-SideSaves($Side, [string]$SaveName = $testSaveName, [string]$Evidence = $run) {
    $expected = $Side.SaveTree
    $actual = Get-InGameTestTree $Side.Saves
    $created = @($actual.Files.Keys | Where-Object { -not $expected.Files.ContainsKey($_) -and [IO.Path]::GetFileName($_) -ieq $SaveName })
    if ($created.Count -gt 1) { throw "$($Side.Name): several test saves were created: $($created -join ', ')" }
    foreach ($relative in $created) {
        $source = Join-Path $Side.Saves $relative
        $artifact = Join-Path $Evidence ($Side.Name + '-' + $SaveName)
        $digest = $actual.Files[$relative]
        [IO.File]::Copy($source, $artifact, $false)
        if ((Get-ApiRepositoryHash $artifact) -cne $digest -or (Get-ApiRepositoryHash $source) -cne $digest) { throw "$($Side.Name): test save changed while it was archived." }
        $Side.ArchivedSave = $artifact
        [IO.File]::Delete($source)
        $actual.Files.Remove($relative)
    }
    $problems = New-Object 'System.Collections.Generic.List[string]'
    foreach ($relative in $actual.Files.Keys) {
        if (-not $expected.Files.ContainsKey($relative)) { $problems.Add('new ' + $relative) }
        elseif ($actual.Files[$relative] -cne $expected.Files[$relative]) { $problems.Add('changed ' + $relative) }
    }
    foreach ($relative in $expected.Files.Keys) { if (-not $actual.Files.ContainsKey($relative)) { $problems.Add('missing ' + $relative) } }
    foreach ($directory in @($actual.Directories | Where-Object { -not $expected.Directories.Contains($_) } | Sort-Object Length -Descending)) {
        $full = Join-Path $Side.Saves $directory
        # Only a directory that held nothing but the archived test save may go; anything else is evidence.
        if ([IO.Directory]::GetFileSystemEntries($full).Length -eq 0 -and $problems.Count -eq 0) { [IO.Directory]::Delete($full, $false) }
        else { $problems.Add('new directory ' + $directory) }
    }
    if (-not $expected.Exists -and $actual.Exists -and $problems.Count -eq 0 -and [IO.Directory]::GetFileSystemEntries($Side.Saves).Length -eq 0) {
        [IO.Directory]::Delete($Side.Saves, $false)
    }
    if ($problems.Count) { throw "$($Side.Name): unexpected save changes (left untouched): $($problems -join '; ')" }
    Assert-InGameTestTree $Side.Saves $expected
    $Side.SavesRestored = $true
}

function Restore-Side($Side) {
    $errors = $Side.RestoreErrors
    # Run level (every run): saves, settings snapshots, test files. -Manual marks the saves done up front: the
    # user's saves are only inventoried, never archived or removed.
    if ($Side.SaveTree -and -not $Side.SavesRestored) { try { Complete-SideSaves $Side } catch { $errors.Add($_.Exception.Message) } }
    foreach ($snapshot in $Side.Snapshots) {
        # An absent file whose folder never came into existence is already in its original state.
        if (-not $snapshot.Directory -and -not $snapshot.Inventory -and -not [IO.Directory]::Exists([IO.Path]::GetDirectoryName($snapshot.Path))) { continue }
        try { Restore-InGameTestState $snapshot } catch { $errors.Add("$($Side.Name): " + $_.Exception.Message) }
    }
    foreach ($path in $Side.TestPaths) {
        try { if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) } } catch { $errors.Add("$($Side.Name): " + $_.Exception.Message) }
    }
    # Layout level: kept for the next run (-KeepLayout and fully applied) or restored from the session.
    if ($Side.LayoutActive) {
        $runOk = $errors.Count -eq 0
        $Side.SessionRuns++
        $Side.SessionLastRun = $label
        # Unrestored run-level state stays recorded in the session so the next start or -RestoreLayout completes it.
        # An older record that could not be completed this run is kept (this run then made no snapshots on this side).
        if (-not $runOk -and -not $Side.PendingRun) { $Side.PendingRun = New-PendingRunRecord $Side }
        if ($KeepLayout -and $Side.LayoutState -eq 'applied') {
            try { Write-LayoutSession $Side; $Side.LayoutKept = $true } catch { $errors.Add("$($Side.Name): layout session: " + $_.Exception.Message) }
        } else {
            foreach ($message in (Restore-SideLayout $Side -KeepSession:([bool]$Side.PendingRun))) { $errors.Add($message) }
        }
        if ($Side.PendingRun) { $errors.Add("$($Side.Name): unfinished recovery of run $($Side.PendingRun.Run) recorded in $($Side.SessionPath)") }
    }
    $Side.Restored = $errors.Count -eq 0
}

function Save-LogExcerpt($Side) {
    if (-not $Side.Tail) { return }
    $lines = @($Side.Tail.Lines | Where-Object { $_ -match $excerptRegex })
    Write-TextLines (Join-Path $run ($Side.Name + '.log')) ([string[]]$lines)
}

# --- evaluation ---------------------------------------------------------------------------------------

function Get-SideFindings($Side) {
    $lines = @()
    if ($Side.Tail) { $lines = $Side.Tail.Lines.ToArray() }
    $summaries = @($lines | Where-Object { $_ -match ($runRegex + 'SUMMARY ') })
    $start = -1
    for ($i = 0; $i -lt $lines.Length; $i++) { if ($lines[$i] -match ('run=' + $labelPattern + '(\s|$)')) { $start = $i; break } }
    $desync = @()
    if ($start -ge 0) { $desync = @($lines[$start..($lines.Length - 1)] | Where-Object { $_ -match '(?i)desync|out of sync' }) }
    # OBSERVE lines of this run, compared per (tick, label) by Compare-PairedObserve.
    $observed = Get-ManualSideFindings ([string[]]$lines) $label
    return [pscustomobject]@{
        Lines = $lines.Length; Summaries = $summaries; ModFailures = @($lines | Where-Object { $_ -match 'MOD_FAILURE' })
        Desync = $desync; RunStartFound = $start -ge 0; Observed = $observed
    }
}

function Test-MultiplayerSaveRoundTrip($Side, $Observed, [string]$ExpectedName) {
    $problems = New-Object 'System.Collections.Generic.List[string]'
    $archive = $Side.ArchivedSave
    $expectedArchive = $Side.Name + '-' + $ExpectedName
    if (-not $archive -or [IO.Path]::GetFileName($archive) -cne $expectedArchive -or
        -not [IO.File]::Exists($archive) -or ([IO.FileInfo]::new($archive)).Length -le 0) {
        $problems.Add("Missing verified archive copy $expectedArchive")
    }
    $load = @($Observed.Missions | Where-Object { $_.Mp -and $_.Save -and -not $_.Editor })
    $tick = $null
    if ($load.Count -ne 1 -or $load[0].Line -notmatch '\brealNetworkGameMembers=([2-9]|[1-9]\d+)(?:,|\s|$)') {
        $problems.Add('Expected one restored multiplayer mission with at least two human peers.')
    } else {
        $anchorPattern = '\bMISSION_ANCHOR session=' + [regex]::Escape($load[0].Session) + ' mapTick=(\d+) save=True(?:\s|$)'
        $anchors = @($Side.Tail.Lines | Select-String -Pattern $anchorPattern)
        if ($anchors.Count -eq 1) { $tick = [long]$anchors[0].Matches[0].Groups[1].Value }
        else { $problems.Add('Expected one native tick at the restored mission anchor.') }
    }
    return [ordered]@{ Path = $archive; Ok = $problems.Count -eq 0; Problems = $problems.ToArray(); ArchiveTick = $tick }
}

function Read-JsonText([string]$Text) { return $Text | ConvertFrom-Json }

function Get-Sha256Upper([string]$Text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash($utf8.GetBytes($Text)))).Replace('-', '') } finally { $sha.Dispose() }
}

function Test-NoValue($Object, [string]$Name) {
    if (-not $Object.PSObject.Properties[$Name]) { return $true }
    return $null -eq $Object.$Name
}

# A .msv is the native save followed by the Script Extender's mod-data zip. ZipFile.OpenRead sees
# no entries there, so locate the zip start from its end record: EOCD offset - CD size - CD offset.
function Open-AppendedZip([string]$Path) {
    $bytes = [IO.File]::ReadAllBytes($Path)
    for ($i = $bytes.Length - 22; $i -ge [Math]::Max(0, $bytes.Length - 65557); $i--) {
        if ($bytes[$i] -ne 0x50 -or $bytes[$i + 1] -ne 0x4B -or $bytes[$i + 2] -ne 5 -or $bytes[$i + 3] -ne 6) { continue }
        $cdSize = [BitConverter]::ToUInt32($bytes, $i + 12)
        $cdOffset = [BitConverter]::ToUInt32($bytes, $i + 16)
        $start = $i - $cdSize - $cdOffset
        if ($start -lt 0) { throw "Invalid zip end record in $Path" }
        $stream = [IO.MemoryStream]::new($bytes, [int]$start, $bytes.Length - [int]$start, $false)
        return [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read)
    }
    throw "No mod-data zip found in $Path"
}

function Test-LordSave([string]$Path) {
    $problems = New-Object 'System.Collections.Generic.List[string]'
    $info = [ordered]@{ Path = $Path; Ok = $false; Problems = $problems; FixesInstalled = $null; SnapshotSlots = @(); ManifestSlots = @() }
    if (-not $Path -or -not [IO.File]::Exists($Path)) { $problems.Add('test save missing'); return $info }
    $zip = Open-AppendedZip $Path
    try {
        $entry = @($zip.Entries | Where-Object { $_.FullName -ieq $saveEntryName }) | Select-Object -First 1
        if (-not $entry) { $problems.Add("entry $saveEntryName missing"); return $info }
        $reader = New-Object IO.StreamReader($entry.Open(), $strictUtf8)
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $zip.Dispose() }
    # LordDataSaveRecord: {version:2, snapshot:<wire string|null>, lordPackageManifest:<wire string|null>}; older saves hold the snapshot wire directly.
    $record = Read-JsonText $text
    $snapshotWire = $text
    $manifestWire = $null
    if ($record.PSObject.Properties['lordPackageManifest']) {
        if ($record.version -ne 2) { $problems.Add('unsupported save record version ' + $record.version) }
        $snapshotWire = $record.snapshot
        $manifestWire = $record.lordPackageManifest
    }
    if (-not $snapshotWire) { $problems.Add('no Lord-data snapshot in the save') }
    else {
        $envelope = Read-JsonText $snapshotWire
        $payload = $envelope.payload
        if ($payload -is [string]) {
            if ((Get-Sha256Upper $payload) -ine [string]$envelope.digest) { $problems.Add('legacy snapshot digest mismatch') }
            $payload = Read-JsonText $payload
        }
        $info.FixesInstalled = $payload.fixesInstalled
        if (-not ($payload.fixesInstalled -is [bool]) -or $payload.fixesInstalled) { $problems.Add('snapshot fixesInstalled is not false') }
        $slots = @($payload.slots | Where-Object { $_ -and [string]$_.lordName -ceq $lordName })
        $info.SnapshotSlots = @($slots | ForEach-Object { $_.playerId })
        if ($slots.Count -eq 0) { $problems.Add("no snapshot slot with lordName $lordName") }
        foreach ($slot in $slots) {
            foreach ($field in @('modLordData','modLordJson','fixesJson','fixesSources')) {
                if (-not (Test-NoValue $slot $field)) { $problems.Add("snapshot slot $($slot.playerId) carries $field") }
            }
        }
    }
    if (-not $manifestWire) { $problems.Add('no Lord package manifest in the save') }
    else {
        $envelope = Read-JsonText $manifestWire
        if (-not ($envelope.payload -is [string])) { $problems.Add('manifest payload is not a string') }
        else {
            if ((Get-Sha256Upper $envelope.payload) -ine [string]$envelope.digest) { $problems.Add('manifest digest mismatch') }
            $data = Read-JsonText $envelope.payload
            $slots = @($data.slots | Where-Object { $_ -and [string]$_.lordName -ceq $lordName })
            $info.ManifestSlots = @($slots | ForEach-Object { [ordered]@{ PlayerId = $_.playerId; NeedsSnapshot = $_.needsSnapshot; NeedsLocalFiles = $_.needsLocalFiles } })
            if ($slots.Count -eq 0) { $problems.Add("no manifest slot with lordName $lordName") }
            foreach ($slot in $slots) {
                if (-not ($slot.needsSnapshot -is [bool]) -or $slot.needsSnapshot) { $problems.Add("manifest slot $($slot.playerId) needsSnapshot is not false") }
                if (-not ($slot.needsLocalFiles -is [bool]) -or $slot.needsLocalFiles) { $problems.Add("manifest slot $($slot.playerId) needsLocalFiles is not false") }
            }
        }
    }
    $info.Ok = $problems.Count -eq 0
    return $info
}

# --- -Manual: debug logging, observation, evaluation ----------------------------------------------------

function Get-ManualRegex {
    return [pscustomobject]@{
        MissionStart = 'Mission started session=(\S+) save=(\w+) editor=(\w+)(?: mode=(.*))?$'
        MissionEnd = 'Mission ended session='
        BepStart = '\[Message:\s*BepInEx\] BepInEx .* - Stronghold Crusader Definitive Edition'
        Important = 'LOBBY_LAUNCH_BLOCKED|SAVE_SETTINGS_|MOD_FAILURE|\[(Error|Fatal)|\[Warning[^\]]*\].*(Lord-data|Lord data)|(?i:desync|out of sync)|Mission started|Mission ended|MP_LOAD|OBSERVE_MISSION|OBSERVE_FAILED'
        Error = '\[(Error|Fatal)\s*:'
        LaunchBlocked = 'LOBBY_LAUNCH_BLOCKED.*worker-production-agreement'
        Desync = '(?i)desync|out of sync'
        Tick = '\b(?:mapTick|tick)=(\d+)'
        LoadMarker = 'SAVE_SETTINGS_(WAITING|CONFIRMED|LOAD_BLOCKED|PREPARATION_CANCELLED)|\bMP_LOAD\b|Waiting for the matching saved Lord values'
        Waiting = 'Waiting for the matching saved Lord values'
        Blocked = 'SAVE_SETTINGS_LOAD_BLOCKED'
        TimedOut = '(?i)timed out'
        NoAck = 'Lord data was loaded without an acknowledged host snapshot|no acknowledged host snapshot'
        Confirmed = 'SAVE_SETTINGS_CONFIRMED'
    }
}

function Format-Clock($Time) {
    if ($null -eq $Time) { return '-' }
    return ([DateTime]$Time).ToString('yyyy-MM-dd HH:mm:ss.fff')
}

function Get-ShortLine([string]$Line, [int]$Max = 220) {
    if (-not $Line) { return '-' }
    $text = $Line.Trim()
    if ($text.Length -gt $Max) { return $text.Substring(0, $Max) + '...' }
    return $text
}

function Get-DebugLogText([string]$Text) {
    # Adds Debug to [Logging.Disk] LogLevels. Only that value changes; every other line and every line ending
    # stays as it is. Already writing Debug (Debug or All) = unchanged.
    $parts = [regex]::Split($Text, '(?<=\n)')
    $section = ''
    for ($i = 0; $i -lt $parts.Length; $i++) {
        $line = $parts[$i].TrimEnd([char]13, [char]10)
        $header = [regex]::Match($line, '^\s*\[([^\]]+)\]\s*$')
        if ($header.Success) { $section = $header.Groups[1].Value.Trim(); continue }
        if ($section -ine 'Logging.Disk') { continue }
        $entry = [regex]::Match($line, '^(\s*LogLevels\s*=\s*)(.*?)\s*$')
        if (-not $entry.Success) { continue }
        $before = $entry.Groups[2].Value
        $tokens = @($before -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        if (@($tokens | Where-Object { $_ -ieq 'All' -or $_ -ieq 'Debug' }).Count) {
            return [pscustomobject]@{ Text = $Text; Changed = $false; Before = $before; After = $before }
        }
        $kept = @($tokens | Where-Object { $_ -ine 'None' })
        if ($kept.Count -eq 0) { $kept = @('Fatal', 'Error', 'Warning', 'Message', 'Info') }
        $after = (@($kept) + @('Debug')) -join ', '
        $parts[$i] = $entry.Groups[1].Value + $after + $parts[$i].Substring($line.Length)
        return [pscustomobject]@{ Text = ($parts -join ''); Changed = $true; Before = $before; After = $after }
    }
    throw '[Logging.Disk] LogLevels not found'
}

function Get-DebugLogPlan($Side) {
    $path = $Side.BepCfg
    $all = 'Fatal, Error, Warning, Message, Info, Debug'
    if (-not [IO.File]::Exists($path)) {
        # BepInEx creates its config on start and keeps the values present; the snapshot deletes the file again.
        $created = '[Logging.Disk]' + $crlf + $crlf + 'LogLevels = ' + $all + $crlf
        return [pscustomobject]@{ Exists = $false; Changed = $true; Before = '(no BepInEx.cfg)'; After = $all; Bytes = $utf8.GetBytes($created)
            Detail = "absent; created with [Logging.Disk] LogLevels = $all and removed afterwards" }
    }
    $bytes = [IO.File]::ReadAllBytes($path)
    $offset = 0
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { $offset = 3 }
    $change = Get-DebugLogText ($strictUtf8.GetString($bytes, $offset, $bytes.Length - $offset))
    $new = $null
    if ($change.Changed) {
        $body = $utf8.GetBytes($change.Text)
        $new = New-Object byte[] ($offset + $body.Length)
        [Array]::Copy($bytes, 0, $new, 0, $offset)
        [Array]::Copy($body, 0, $new, $offset, $body.Length)
    }
    $detail = if ($change.Changed) { "[Logging.Disk] LogLevels '$($change.Before)' -> '$($change.After)' (restored byte-exact afterwards)" }
        else { "[Logging.Disk] LogLevels '$($change.Before)' already writes Debug; unchanged" }
    return [pscustomobject]@{ Exists = $true; Changed = $change.Changed; Before = $change.Before; After = $change.After; Bytes = $new; Detail = $detail }
}

function Set-SideDebugLogging($Side) {
    # The snapshot of BepInEx.cfg (Get-BackupTargets) is taken before; Restore-Side restores it byte-exact.
    $plan = Get-DebugLogPlan $Side
    $Side.DebugLog = [ordered]@{ Path = $Side.BepCfg; Existed = $plan.Exists; Changed = $plan.Changed; Before = $plan.Before; After = $plan.After }
    if ($plan.Changed) {
        $Side.Changed = $true
        [IO.File]::WriteAllBytes($Side.BepCfg, $plan.Bytes)
        if ((Get-DebugLogPlan $Side).Changed) { throw "$($Side.Name): the Debug log level could not be set in $($Side.BepCfg)." }
    }
    Write-ProgressLine "DEBUG LOGGING side=$($Side.Name): $($plan.Detail)"
}

function Test-HostGameRunning {
    $games = @(Get-Process -Name $gameName -ErrorAction SilentlyContinue)
    foreach ($game in $games) { $game.Dispose() }
    return ($games.Count -gt 0 -or -not (Test-LogUnlocked $hostSide.Log))
}

function Get-ClientGameRunning {
    # $null = share not reachable (state unknown); the client game holds its log while it runs.
    if (-not [IO.Directory]::Exists($clientSide.Bep)) { return $null }
    return (-not (Test-LogUnlocked $clientSide.Log))
}

function New-ManualMonitor([string]$Name) {
    return [pscustomobject]@{
        Name = $Name; Running = $false; EverRan = $false; Starts = 0; StartedAt = $null; ExitedAt = $null; Unreachable = 0
        InMission = $false; MissionAt = $null; LastMissionStart = ''; LastMissionEnd = ''; Missions = 0
        LastLineAt = $null; LastObserve = ''; LastObserveAt = $null; ObserveCount = 0; ObserveTick = ''
        LastMarker = ''; BeatLines = 0; BeatObserve = 0; Printed = 0; Stall = $null; Quiet = $null
        Stalls = New-Object 'System.Collections.Generic.List[object]'
    }
}

function Update-ManualMonitor($Monitor, $Tail, $Running, [DateTime]$Now, [string]$RunLabel) {
    # Consumes the new tail lines; returns state change events and the important lines to print immediately.
    $events = New-Object 'System.Collections.Generic.List[string]'
    $important = New-Object 'System.Collections.Generic.List[string]'
    if ($null -eq $Running) { $Monitor.Unreachable++ }
    elseif ($Running -and -not $Monitor.Running) {
        $Monitor.Running = $true; $Monitor.EverRan = $true; $Monitor.Starts++; $Monitor.StartedAt = $Now; $Monitor.ExitedAt = $null
        $Monitor.InMission = $false; $Monitor.LastLineAt = $Now
        $events.Add("GAME STARTED side=$($Monitor.Name) (start #$($Monitor.Starts))")
    } elseif (-not $Running -and $Monitor.Running) {
        $Monitor.Running = $false; $Monitor.ExitedAt = $Now
        $events.Add("GAME EXITED side=$($Monitor.Name) (log unlocked; in mission: $($Monitor.InMission))")
    }
    $rx = Get-ManualRegex
    $observeRx = '\bOBSERVE run=' + [regex]::Escape($RunLabel) + '(\s|$)'
    for (; $Tail.Scanned -lt $Tail.Lines.Count; $Tail.Scanned++) {
        $line = $Tail.Lines[$Tail.Scanned]
        $Monitor.LastLineAt = $Now
        if ($line -match $rx.MissionStart) { $Monitor.InMission = $true; $Monitor.MissionAt = $Now; $Monitor.LastMissionStart = $line; $Monitor.Missions++ }
        elseif ($line -match $rx.MissionEnd) { $Monitor.InMission = $false; $Monitor.LastMissionEnd = $line }
        if ($line -match $observeRx) {
            $Monitor.ObserveCount++; $Monitor.LastObserve = $line; $Monitor.LastObserveAt = $Now
            $tick = [regex]::Match($line, $rx.Tick)
            if ($tick.Success) { $Monitor.ObserveTick = $tick.Groups[1].Value }
        }
        if ($line -match $rx.Important) { $Monitor.LastMarker = $line; $important.Add($line) }
    }
    return [pscustomobject]@{ Events = $events; Important = $important }
}

function Close-ManualEpisode($Episode, [DateTime]$Now, [string]$Why) {
    $Episode.End = Format-Clock $Now
    $Episode.Seconds = [Math]::Round(($Now - $Episode.SinceTime).TotalSeconds, 1)
    $Episode.EndReason = $Why
}

function Test-ManualStall($Monitor, [DateTime]$Now, [int]$Seconds = 60) {
    # Called once per heartbeat. A stall needs a running game inside a mission (last "Mission started" after the last
    # "Mission ended"); menu, lobby and a closed game are idle, not stalled.
    $messages = New-Object 'System.Collections.Generic.List[string]'
    $active = $Monitor.Running -and $Monitor.InMission
    $quiet = 0
    if ($Monitor.LastLineAt) { $quiet = ($Now - $Monitor.LastLineAt).TotalSeconds }
    $why = if (-not $Monitor.Running) { 'game exited' } elseif (-not $Monitor.InMission) { 'mission ended' } else { 'log lines resumed' }
    if ($active -and $quiet -gt $Seconds) {
        if ($Monitor.Quiet) { Close-ManualEpisode $Monitor.Quiet $Now 'superseded by STALL'; $Monitor.Quiet = $null }
        if (-not $Monitor.Stall) {
            $Monitor.Stall = [pscustomobject]@{ Side = $Monitor.Name; Kind = 'STALL'; Since = (Format-Clock $Monitor.LastLineAt); SinceTime = $Monitor.LastLineAt
                Detected = (Format-Clock $Now); End = $null; Seconds = $null; EndReason = $null; Mission = (Get-ShortLine $Monitor.LastMissionStart 160) }
            $Monitor.Stalls.Add($Monitor.Stall)
            $messages.Add(('STALL side={0}: running, in a mission, no new log line and no OBSERVE progress for {1:N0} s (last line at {2}); a paused game looks the same' -f $Monitor.Name, $quiet, $Monitor.Stall.Since))
        } else { $messages.Add(('STALL side={0} continues ({1:N0} s without a log line)' -f $Monitor.Name, $quiet)) }
        return ,$messages
    }
    if ($Monitor.Stall) {
        Close-ManualEpisode $Monitor.Stall $Now $why
        $messages.Add(('STALL_END side={0} after {1} s ({2})' -f $Monitor.Name, $Monitor.Stall.Seconds, $why))
        $Monitor.Stall = $null
    }
    # Log lines arrive, but the observer reports nothing (e.g. simulation hangs while other code keeps logging).
    $since = $Monitor.MissionAt
    if ($Monitor.LastObserveAt -and $Monitor.MissionAt -and $Monitor.LastObserveAt -gt $Monitor.MissionAt) { $since = $Monitor.LastObserveAt }
    $observeQuiet = 0
    if ($since) { $observeQuiet = ($Now - $since).TotalSeconds }
    if ($active -and $since -and $observeQuiet -gt $Seconds) {
        if (-not $Monitor.Quiet) {
            $Monitor.Quiet = [pscustomobject]@{ Side = $Monitor.Name; Kind = 'OBSERVE_QUIET'; Since = (Format-Clock $since); SinceTime = $since
                Detected = (Format-Clock $Now); End = $null; Seconds = $null; EndReason = $null; Mission = (Get-ShortLine $Monitor.LastMissionStart 160) }
            $Monitor.Stalls.Add($Monitor.Quiet)
            $messages.Add(('OBSERVE_QUIET side={0}: log lines arrive, but no OBSERVE line for {1:N0} s in the mission (since {2}); paused or hanging simulation' -f $Monitor.Name, $observeQuiet, $Monitor.Quiet.Since))
        }
    } elseif ($Monitor.Quiet) {
        $end = if ($active) { 'OBSERVE resumed' } else { $why }
        Close-ManualEpisode $Monitor.Quiet $Now $end
        $messages.Add(('OBSERVE_QUIET_END side={0} after {1} s ({2})' -f $Monitor.Name, $Monitor.Quiet.Seconds, $end))
        $Monitor.Quiet = $null
    }
    return ,$messages
}

function Format-ManualHeartbeat($Monitor, $Tail, [double]$Elapsed) {
    $lines = $Tail.Lines.Count
    $text = ('HEARTBEAT {0:N0}s side={1} running={2} starts={3} log={4} B lines={5} (+{6}) mission={7} observe={8} (+{9}) tick={10}' -f $Elapsed,
        $Monitor.Name, $Monitor.Running, $Monitor.Starts, $Tail.Length, $lines, ($lines - $Monitor.BeatLines),
        $(if ($Monitor.InMission) { 'in' } else { 'none' }), $Monitor.ObserveCount, ($Monitor.ObserveCount - $Monitor.BeatObserve),
        $(if ($Monitor.ObserveTick) { $Monitor.ObserveTick } else { '-' }))
    if ($Tail.Error) { $text += ' (read error: ' + $Tail.Error + ')' }
    if ($Monitor.Unreachable) { $text += " (share unreachable polls: $($Monitor.Unreachable))" }
    $Monitor.BeatLines = $lines
    $Monitor.BeatObserve = $Monitor.ObserveCount
    return @($text,
        ('    last OBSERVE: ' + (Get-ShortLine $Monitor.LastObserve)),
        ('    last mission start: ' + (Get-ShortLine $Monitor.LastMissionStart) + ' | last end: ' + (Get-ShortLine $Monitor.LastMissionEnd 80)),
        ('    last marker: ' + (Get-ShortLine $Monitor.LastMarker)))
}

function Get-ManualSideFindings([string[]]$Lines, [string]$RunLabel) {
    # Scope of an OBSERVE line (informational): mp<n> = n-th realMultiplayer mission of this side, local<n> = single
    # player/editor mission, none = outside a mission.
    $rx = Get-ManualRegex
    # Contract (src/ObserveSteps.cs): OBSERVE run=<run> session=<s> tick=<t> label=<save|new>-<anchor> buildings=... hash=<16hex>.
    $observeRx = '\bOBSERVE run=' + [regex]::Escape($RunLabel) + '\s.*?\b(label=(\S+).*?)\s*$'
    $missionRx = '\bOBSERVE_MISSION run=' + [regex]::Escape($RunLabel) + '(\s|$)'
    $observeMissions = New-Object 'System.Collections.Generic.List[string]'
    $observeFailed = New-Object 'System.Collections.Generic.List[string]'
    $missions = New-Object 'System.Collections.Generic.List[object]'
    $ends = New-Object 'System.Collections.Generic.List[int]'
    $observe = New-Object 'System.Collections.Generic.List[object]'
    $errors = New-Object 'System.Collections.Generic.List[string]'
    $launch = New-Object 'System.Collections.Generic.List[string]'
    $desync = New-Object 'System.Collections.Generic.List[string]'
    $failures = New-Object 'System.Collections.Generic.List[string]'
    $starts = 0; $mp = 0; $local = 0; $scope = 'none'
    if (-not $Lines) { $Lines = @() }
    for ($i = 0; $i -lt $Lines.Length; $i++) {
        $line = $Lines[$i]
        if ($line -match $rx.BepStart) { $starts++; $scope = 'none' }
        $mission = [regex]::Match($line, $rx.MissionStart)
        if ($mission.Success) {
            $isMp = $mission.Groups[4].Value -match 'realMultiplayer=True'
            if ($isMp) { $mp++; $scope = 'mp' + $mp } else { $local++; $scope = 'local' + $local }
            $missions.Add([pscustomobject]@{ Index = $i; Session = $mission.Groups[1].Value; Save = $mission.Groups[2].Value -ieq 'True'
                Editor = $mission.Groups[3].Value -ieq 'True'; Mp = $isMp; Scope = $scope; Line = $line.Trim() })
        } elseif ($line -match $rx.MissionEnd) { $ends.Add($i); $scope = 'none' }
        $observed = [regex]::Match($line, $observeRx)
        if ($observed.Success) {
            $tick = [regex]::Match($line, $rx.Tick)
            $observe.Add([pscustomobject]@{ Index = $i; Scope = $scope; Label = $observed.Groups[2].Value; Payload = $observed.Groups[1].Value
                Tick = $(if ($tick.Success) { [long]$tick.Groups[1].Value } else { $null }) })
            continue
        }
        if ($line -match $missionRx) { $observeMissions.Add($line.Trim()) }
        if ($line -match 'OBSERVE_FAILED') { $observeFailed.Add($line.Trim()) }
        if ($line -match 'MOD_FAILURE') { $failures.Add($line.Trim()) }
        if ($line -match $rx.Error) { if ($line -match $rx.LaunchBlocked) { $launch.Add($line.Trim()) } else { $errors.Add($line.Trim()) } }
        # OBSERVE payloads are compared separately (they may carry desync counters); everything else counts.
        if ($line -match $rx.Desync) { $desync.Add($line.Trim()) }
    }
    return [pscustomobject]@{
        Lines = $Lines.Length; GameStarts = $starts; Missions = $missions.ToArray(); MissionEnds = $ends.ToArray(); Observe = $observe.ToArray()
        Errors = $errors.ToArray(); LaunchBlockedInfo = $launch.ToArray(); Desync = $desync.ToArray(); ModFailures = $failures.ToArray()
        ObserveMissions = $observeMissions.ToArray(); ObserveFailed = $observeFailed.ToArray()
    }
}

function Get-NextBoundary($Findings, [int]$Index, [int]$Count) {
    # First mission start or end after $Index (exclusive window end).
    $next = $Count
    foreach ($mission in $Findings.Missions) { if ($mission.Index -gt $Index -and $mission.Index -lt $next) { $next = $mission.Index } }
    foreach ($end in $Findings.MissionEnds) { if ($end -gt $Index -and $end -lt $next) { $next = $end } }
    return $next
}

function Get-ObserveProgress($Findings, [int]$MissionIndex) {
    # OBSERVE lines between this mission start and the next mission start/end. Advanced = tick grew, or without
    # tick values at least two OBSERVE lines.
    $until = Get-NextBoundary $Findings $MissionIndex $Findings.Lines
    $rows = @($Findings.Observe | Where-Object { $_.Index -gt $MissionIndex -and $_.Index -lt $until })
    $ticks = @($rows | Where-Object { $null -ne $_.Tick } | ForEach-Object { $_.Tick })
    $advanced = $false
    if ($ticks.Count -ge 2) { $advanced = ($ticks | Measure-Object -Maximum).Maximum -gt $ticks[0] }
    elseif ($ticks.Count -eq 0) { $advanced = $rows.Count -ge 2 }
    $first = $null; $last = $null
    if ($ticks.Count) { $first = $ticks[0]; $last = $ticks[$ticks.Count - 1] }
    return [pscustomobject]@{ Lines = $rows.Count; FirstTick = $first; LastTick = $last; Advanced = [bool]$advanced }
}

function Get-WindowLines([string[]]$Lines, [int]$From, [int]$Until, [string]$Pattern) {
    $found = New-Object 'System.Collections.Generic.List[string]'
    for ($i = [Math]::Max(0, $From); $i -lt [Math]::Min($Until, $Lines.Length); $i++) { if ($Lines[$i] -match $Pattern) { $found.Add($Lines[$i].Trim()) } }
    return ,$found.ToArray()
}

function Get-ManualF1([string[]]$HostLines, [string[]]$ClientLines, $HostF, $ClientF, [bool]$DebugLogging) {
    # Host load attempts: start at the first load marker (SAVE_SETTINGS_*, MP_LOAD, Lord wait) and end at the host's
    # next "Mission started" (an MP save mission completes it, any other mission aborts it). A host MP save mission
    # without markers is an attempt as well. The k-th completed attempt pairs with the client's k-th MP save mission.
    $rx = Get-ManualRegex
    if (-not $HostLines) { $HostLines = @() }
    if (-not $ClientLines) { $ClientLines = @() }
    $byIndex = @{}
    foreach ($mission in $HostF.Missions) { $byIndex[$mission.Index] = $mission }
    $raw = New-Object 'System.Collections.Generic.List[object]'
    $current = $null
    for ($i = 0; $i -lt $HostLines.Length; $i++) {
        if ($byIndex.ContainsKey($i)) {
            $mission = $byIndex[$i]
            if ($mission.Save -and $mission.Mp) {
                if (-not $current) { $current = [pscustomobject]@{ Start = $i; Mission = $null; Aborted = $null } }
                $current.Mission = $mission
                $raw.Add($current); $current = $null
            } elseif ($current) {
                $current.Aborted = 'the next host mission is not an MP save load: ' + (Get-ShortLine $mission.Line 160)
                $raw.Add($current); $current = $null
            }
            continue
        }
        if (-not $current -and $HostLines[$i] -match $rx.LoadMarker) { $current = [pscustomobject]@{ Start = $i; Mission = $null; Aborted = $null } }
    }
    if ($current) { $raw.Add($current) }
    $clientLoads = @($ClientF.Missions | Where-Object { $_.Save -and $_.Mp })
    $attempts = New-Object 'System.Collections.Generic.List[object]'
    $paired = 0
    for ($k = 0; $k -lt $raw.Count; $k++) {
        $attempt = $raw[$k]
        # Marker window: from the first marker to the next host mission start after the load (markers logged after
        # the load, e.g. a missing acknowledged snapshot, still count) or the next attempt, whichever comes first.
        $anchor = if ($attempt.Mission) { $attempt.Mission.Index } else { $attempt.Start }
        $until = $HostLines.Length
        foreach ($mission in $HostF.Missions) { if ($mission.Index -gt $anchor) { $until = $mission.Index; break } }
        if ($k + 1 -lt $raw.Count -and $raw[$k + 1].Start -lt $until) { $until = $raw[$k + 1].Start }
        $issues = New-Object 'System.Collections.Generic.List[string]'
        $notes = New-Object 'System.Collections.Generic.List[string]'
        $hostMarkers = [ordered]@{
            SaveSettings = Get-WindowLines $HostLines $attempt.Start $until 'SAVE_SETTINGS_'
            Waiting = Get-WindowLines $HostLines $attempt.Start $until $rx.Waiting
            LoadBlocked = Get-WindowLines $HostLines $attempt.Start $until $rx.Blocked
            TimedOut = Get-WindowLines $HostLines $attempt.Start $until $rx.TimedOut
            NoAcknowledgedSnapshot = Get-WindowLines $HostLines $attempt.Start $until $rx.NoAck
        }
        $confirmed = (Get-WindowLines $HostLines $attempt.Start $until $rx.Confirmed).Count -gt 0
        $hostProgress = $null; $clientMission = $null; $clientProgress = $null; $clientMarkers = $null
        if ($attempt.Mission) {
            $hostProgress = Get-ObserveProgress $HostF $attempt.Mission.Index
            if ($paired -lt $clientLoads.Count) {
                $clientMission = $clientLoads[$paired]
                $from = 0
                foreach ($mission in $ClientF.Missions) { if ($mission.Index -lt $clientMission.Index) { $from = $mission.Index + 1 } }
                $to = $ClientLines.Length
                foreach ($mission in $ClientF.Missions) { if ($mission.Index -gt $clientMission.Index) { $to = $mission.Index; break } }
                $clientMarkers = [ordered]@{
                    Waiting = Get-WindowLines $ClientLines $from $to $rx.Waiting
                    LoadBlocked = Get-WindowLines $ClientLines $from $to $rx.Blocked
                    TimedOut = Get-WindowLines $ClientLines $from $to $rx.TimedOut
                    NoAcknowledgedSnapshot = Get-WindowLines $ClientLines $from $to $rx.NoAck
                }
                $clientProgress = Get-ObserveProgress $ClientF $clientMission.Index
            }
            $paired++
        }
        if ($DebugLogging -and -not $confirmed) { $issues.Add('host SAVE_SETTINGS_CONFIRMED missing (Debug logging was on)') }
        if (-not $DebugLogging -and -not $confirmed) { $notes.Add('SAVE_SETTINGS_CONFIRMED not expected (Debug logging not confirmed on the host)') }
        foreach ($set in @(@('host', $hostMarkers), @('client', $clientMarkers))) {
            if (-not $set[1]) { continue }
            if ($set[1].LoadBlocked.Count) { $issues.Add("$($set[0]): SAVE_SETTINGS_LOAD_BLOCKED x$($set[1].LoadBlocked.Count)") }
            if ($set[1].TimedOut.Count) { $issues.Add("$($set[0]): 'timed out' x$($set[1].TimedOut.Count)") }
            if ($set[1].NoAcknowledgedSnapshot.Count) { $issues.Add("$($set[0]): Lord data loaded without an acknowledged host snapshot x$($set[1].NoAcknowledgedSnapshot.Count)") }
            if ($set[1].Waiting.Count) {
                $text = "$($set[0]): 'Waiting for the matching saved Lord values' x$($set[1].Waiting.Count)"
                if ($attempt.Mission) { $notes.Add($text + ' (the load went on)') } else { $issues.Add($text) }
            }
        }
        if ($attempt.Aborted) { $issues.Add('aborted: ' + $attempt.Aborted) }
        elseif (-not $attempt.Mission) { $issues.Add('host: no "Mission started ... save=True" after the load attempt (load hang or log end)') }
        else {
            if (-not $hostProgress.Advanced) { $issues.Add("host: OBSERVE did not advance after the load ($($hostProgress.Lines) OBSERVE lines) - post-load hang?") }
            if (-not $clientMission) { $issues.Add('client: no matching "Mission started ... save=True" (realMultiplayer) after the load') }
            elseif (-not $clientProgress.Advanced) { $issues.Add("client: OBSERVE did not advance after the load ($($clientProgress.Lines) OBSERVE lines) - post-load hang?") }
        }
        $attempts.Add([ordered]@{
            Number = $k + 1; Verdict = $(if ($issues.Count) { 'ISSUES' } else { 'OK' }); Issues = $issues.ToArray(); Notes = $notes.ToArray()
            HostFirstMarker = (Get-ShortLine $HostLines[$attempt.Start] 300); HostConfirmed = $confirmed
            HostMission = $(if ($attempt.Mission) { $attempt.Mission.Line } else { $null })
            ClientMission = $(if ($clientMission) { $clientMission.Line } else { $null })
            HostObserve = $hostProgress; ClientObserve = $clientProgress; HostMarkers = $hostMarkers; ClientMarkers = $clientMarkers
        })
    }
    return [ordered]@{ DebugLogging = $DebugLogging; Attempts = $attempts.ToArray(); ClientMpSaveMissions = $clientLoads.Count }
}

function Compare-PairedObserve($HostF, $ClientF) {
    # Key (tick, label): the label names the mission (save|new-<anchor tick>), the tick the sample. Payload = everything
    # from "label=" on. Keys on one side only (other side paused, in the editor, exited) are not comparable, no failure.
    $sides = @{}
    foreach ($pair in @(@('host', $HostF), @('client', $ClientF))) {
        $map = @{}
        foreach ($row in $pair[1].Observe) {
            $key = 'tick=' + $(if ($null -ne $row.Tick) { $row.Tick } else { '?' }) + ' label=' + $row.Label
            if (-not $map.ContainsKey($key)) { $map[$key] = New-Object 'System.Collections.Generic.List[string]' }
            $map[$key].Add($row.Payload)
        }
        $sides[$pair[0]] = $map
    }
    $keys = New-Object 'System.Collections.Generic.SortedSet[string]' ([StringComparer]::Ordinal)
    foreach ($key in @($sides.host.Keys) + @($sides.client.Keys)) { if ($key) { $null = $keys.Add([string]$key) } }
    $mismatches = New-Object 'System.Collections.Generic.List[object]'
    $hostOnly = New-Object 'System.Collections.Generic.List[string]'
    $clientOnly = New-Object 'System.Collections.Generic.List[string]'
    $compared = 0
    foreach ($key in $keys) {
        $inHost = $sides.host.ContainsKey($key); $inClient = $sides.client.ContainsKey($key)
        if ($inHost -and $inClient) {
            $compared++
            $h = $sides.host[$key].ToArray(); $c = $sides.client[$key].ToArray()
            if (($h -join "`n") -cne ($c -join "`n")) { $mismatches.Add([ordered]@{ Key = $key; Host = $h; Client = $c }) }
        } elseif ($inHost) { $hostOnly.Add($key) } else { $clientOnly.Add($key) }
    }
    return [ordered]@{
        Compared = $compared; Matching = $compared - $mismatches.Count; Mismatches = $mismatches.ToArray()
        HostOnly = $hostOnly.ToArray(); ClientOnly = $clientOnly.ToArray()
    }
}

function Get-SaveDiff($Before, $After) {
    $new = New-Object 'System.Collections.Generic.List[string]'
    $changed = New-Object 'System.Collections.Generic.List[string]'
    $removed = New-Object 'System.Collections.Generic.List[string]'
    $beforeFiles = @{}; $afterFiles = @{}
    if ($Before -and $Before.Files) { $beforeFiles = $Before.Files }
    if ($After -and $After.Files) { $afterFiles = $After.Files }
    foreach ($key in $afterFiles.Keys) {
        if (-not $beforeFiles.ContainsKey($key)) { $new.Add($key) }
        elseif ([string]$afterFiles[$key] -cne [string]$beforeFiles[$key]) { $changed.Add($key) }
    }
    foreach ($key in $beforeFiles.Keys) { if (-not $afterFiles.ContainsKey($key)) { $removed.Add($key) } }
    $new.Sort([StringComparer]::OrdinalIgnoreCase); $changed.Sort([StringComparer]::OrdinalIgnoreCase); $removed.Sort([StringComparer]::OrdinalIgnoreCase)
    return [pscustomobject]@{ New = $new.ToArray(); Changed = $changed.ToArray(); Removed = $removed.ToArray() }
}

function Copy-SharedFile([string]$Source, [string]$Target) {
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Target))
    $stream = Open-SharedRead $Source
    try { $out = [IO.File]::Create($Target); try { $stream.CopyTo($out) } finally { $out.Dispose() } } finally { $stream.Dispose() }
}

function Save-ManualSaves([string]$SideName, [string]$Saves, $Diff, [string]$Evidence) {
    # Copies new/changed .msv files into the evidence folder (the originals stay) and runs the Lord save check on
    # the copies. Informational: shows whether a save is the F1 case (SerpMarketRest4, no Fixes).
    $checks = New-Object 'System.Collections.Generic.List[object]'
    foreach ($relative in @($Diff.New) + @($Diff.Changed)) {
        if (-not $relative -or $relative -notmatch '(?i)\.msv$') { continue }
        $kind = if (@($Diff.New) -contains $relative) { 'new' } else { 'changed' }
        $copy = Join-Path $Evidence ('saves\' + $SideName + '\' + $relative)
        $check = $null
        try {
            Copy-SharedFile (Join-Path $Saves $relative) $copy
            $check = Test-LordSave $copy
        } catch { $check = [ordered]@{ Path = $copy; Ok = $false; Problems = @('copy or save check error: ' + $_.Exception.Message); FixesInstalled = $null; SnapshotSlots = @(); ManifestSlots = @() } }
        [string[]]$problems = @($check.Problems)
        $snapshotSlots = @($check.SnapshotSlots)
        $manifestSlots = @($check.ManifestSlots)
        $checks.Add([ordered]@{
            Save = $relative; Kind = $kind; Copy = $copy; LordSaveOk = [bool]$check.Ok; Problems = $problems
            FixesInstalled = $check.FixesInstalled; SnapshotSlots = $snapshotSlots; ManifestSlots = $manifestSlots
        })
    }
    return ,$checks.ToArray()
}

function ConvertTo-ManualSideRecord($Monitor, [bool]$Exited, $Findings, $DebugLog, $Tail) {
    $missions = @($Findings.Missions | ForEach-Object { [ordered]@{ Session = $_.Session; Save = $_.Save; Editor = $_.Editor; RealMultiplayer = $_.Mp; Line = $_.Line } })
    $saveTrue = @($Findings.Missions | Where-Object { $_.Save }).Count
    $errors = @($Findings.Errors)
    return [ordered]@{
        GameStarted = [bool]$Monitor.EverRan; GameExited = $Exited; StartsObserved = $Monitor.Starts; BepInExStarts = $Findings.GameStarts
        LogLines = $Findings.Lines; LogRewrites = $(if ($Tail) { $Tail.Rewrites } else { 0 }); DebugLogging = $DebugLog
        Missions = [ordered]@{ Count = $missions.Count; SaveTrue = $saveTrue; SaveFalse = $missions.Count - $saveTrue; List = $missions }
        ModFailures = [ordered]@{ Count = @($Findings.ModFailures).Count; Lines = @($Findings.ModFailures) }
        Errors = [ordered]@{ Count = $errors.Count; Lines = @($errors | Select-Object -First 300); Truncated = $errors.Count -gt 300 }
        LaunchBlockedInfo = [ordered]@{ Count = @($Findings.LaunchBlockedInfo).Count; Lines = @($Findings.LaunchBlockedInfo) }
        Desync = [ordered]@{ Count = @($Findings.Desync).Count; Lines = @($Findings.Desync) }
        ObserveLines = @($Findings.Observe).Count; ObserveMissions = @($Findings.ObserveMissions)
        ObserveFailed = [ordered]@{ Count = @($Findings.ObserveFailed).Count; Lines = @($Findings.ObserveFailed) }
    }
}

function Format-ManualReport($Result) {
    $out = New-Object 'System.Collections.Generic.List[string]'
    $out.Add("Manual paired session $($Result.Run)")
    $out.Add("Started $($Result.Started), ended $($Result.Ended) ($($Result.EndReason)). Evidence: $($Result.Evidence)")
    $out.Add("Findings: $($Result.Verdict) (informational; runner status: $(if ($Result.RunnerOk) { 'OK' } else { 'FAILED' }))")
    foreach ($failure in @($Result.Failures)) { $out.Add('  RUNNER: ' + $failure) }
    foreach ($name in @('Host', 'Client')) {
        $side = $Result.Sides[$name]
        if (-not $side) { continue }
        $out.Add('')
        $out.Add(("{0}: game started={1} exited={2} starts={3} log lines={4}; Debug log level: {5}" -f $name, $side.GameStarted, $side.GameExited,
            $side.StartsObserved, $side.LogLines, $(if ($side.DebugLogging) { $side.DebugLogging.After } else { 'not set' })))
        $out.Add(("  missions: {0} (save=True {1}, save=False {2})" -f $side.Missions.Count, $side.Missions.SaveTrue, $side.Missions.SaveFalse))
        foreach ($mission in @($side.Missions.List)) { $out.Add('    ' + (Get-ShortLine $mission.Line 200)) }
        $out.Add("  MOD_FAILURE: $($side.ModFailures.Count)"); foreach ($line in @($side.ModFailures.Lines)) { $out.Add('    ' + (Get-ShortLine $line 300)) }
        $out.Add("  Error lines: $($side.Errors.Count)"); foreach ($line in @($side.Errors.Lines | Select-Object -First 40)) { $out.Add('    ' + (Get-ShortLine $line 300)) }
        if ($side.Errors.Count -gt 40) { $out.Add('    ... (all in result.json / the full log)') }
        $out.Add("  info: LOBBY_LAUNCH_BLOCKED worker-production-agreement (known transient): $($side.LaunchBlockedInfo.Count)")
        $out.Add("  OBSERVE lines: $($side.ObserveLines); OBSERVE_MISSION: $(@($side.ObserveMissions).Count); OBSERVE_FAILED: $($side.ObserveFailed.Count)")
        foreach ($line in @($side.ObserveMissions)) { $out.Add('    ' + (Get-ShortLine $line 300)) }
        foreach ($line in @($side.ObserveFailed.Lines | Select-Object -First 20)) { $out.Add('    FAILED ' + (Get-ShortLine $line 300)) }
        $out.Add("  desync/out-of-sync lines: $($side.Desync.Count)"); foreach ($line in @($side.Desync.Lines | Select-Object -First 20)) { $out.Add('    ' + (Get-ShortLine $line 300)) }
    }
    $out.Add('')
    $f1 = $Result.F1
    $out.Add("F1 (MP save loads): $(@($f1.Attempts).Count) attempt(s); host Debug logging: $($f1.DebugLogging); client MP save missions: $($f1.ClientMpSaveMissions)")
    foreach ($attempt in @($f1.Attempts)) {
        $out.Add(("  #{0} {1}: host SAVE_SETTINGS_CONFIRMED={2}; host mission={3}; client mission={4}" -f $attempt.Number, $attempt.Verdict, $attempt.HostConfirmed,
            $(if ($attempt.HostMission) { 'yes' } else { 'NO' }), $(if ($attempt.ClientMission) { 'yes' } else { 'NO' })))
        foreach ($pair in @(@('host', $attempt.HostObserve), @('client', $attempt.ClientObserve))) {
            if ($pair[1]) { $out.Add(("     {0} OBSERVE after load: lines={1} ticks {2}->{3} advanced={4}" -f $pair[0], $pair[1].Lines, $pair[1].FirstTick, $pair[1].LastTick, $pair[1].Advanced)) }
        }
        foreach ($issue in @($attempt.Issues)) { $out.Add('     ISSUE: ' + $issue) }
        foreach ($note in @($attempt.Notes)) { $out.Add('     note: ' + $note) }
    }
    $out.Add('')
    $obs = $Result.Observe
    $out.Add(("OBSERVE comparison per (tick, label): {0} compared, {1} matching, {2} MISMATCHING (desync evidence); not comparable (one side only): host {3}, client {4}" -f
        $obs.Compared, $obs.Matching, @($obs.Mismatches).Count, @($obs.HostOnly).Count, @($obs.ClientOnly).Count))
    foreach ($mismatch in @($obs.Mismatches | Select-Object -First 30)) {
        $out.Add('  MISMATCH ' + $mismatch.Key)
        $out.Add('    host:   ' + (Get-ShortLine (@($mismatch.Host) -join ' || ') 300))
        $out.Add('    client: ' + (Get-ShortLine (@($mismatch.Client) -join ' || ') 300))
    }
    if (@($obs.HostOnly).Count) { $out.Add('  host only: ' + (Get-ShortLine (@($obs.HostOnly) -join ', ') 400)) }
    if (@($obs.ClientOnly).Count) { $out.Add('  client only: ' + (Get-ShortLine (@($obs.ClientOnly) -join ', ') 400)) }
    $out.Add('')
    $out.Add("Stall candidates (a paused game looks the same; check against what you did): $(@($Result.Stalls).Count)")
    foreach ($stall in @($Result.Stalls)) {
        $out.Add(("  {0} side={1} since {2}, detected {3}, end {4} ({5} s, {6}); mission: {7}" -f $stall.Kind, $stall.Side, $stall.Since, $stall.Detected,
            $(if ($stall.End) { $stall.End } else { 'open at session end' }), $stall.Seconds, $stall.EndReason, $stall.Mission))
    }
    $out.Add('')
    foreach ($name in @('Host', 'Client')) {
        $saves = $Result.Saves[$name]
        if (-not $saves) { $out.Add("$name saves: not inventoried"); continue }
        $out.Add(("{0} saves: new {1}, changed {2}, removed {3}" -f $name, @($saves.New).Count, @($saves.Changed).Count, @($saves.Removed).Count))
        foreach ($check in @($saves.Checks)) {
            $out.Add(("  {0} ({1}): Lord save check {2}{3}" -f $check.Save, $check.Kind, $(if ($check.LordSaveOk) { 'OK (F1 case: SerpMarketRest4, no Fixes)' } else { 'not F1/problems' }),
                $(if (@($check.Problems).Count) { ' - ' + (@($check.Problems) -join '; ') } else { '' })))
        }
        foreach ($removed in @($saves.Removed)) { $out.Add('  removed: ' + $removed) }
    }
    $out.Add('')
    $out.Add("Restored: host=$($Result.Restored.Host) client=$($Result.Restored.Client); layout kept: $($Result.LayoutKept)")
    foreach ($note in @($Result.Notes)) { $out.Add('note: ' + $note) }
    return ,$out.ToArray()
}

# --- setup shared by dry-run and real run -------------------------------------------------------------

$hostUserRoot = Join-Path ([Environment]::GetFolderPath('UserProfile')) ('AppData\LocalLow\Firefly Studios\' + $gameName)
$hostSide = New-Side 'host' (Get-InGameTestPath $HostRoot) $hostUserRoot
$clientSide = New-Side 'client' $ClientRoot $ClientUserRoot
$exe = Join-Path $hostSide.Root ($gameName + '.exe')
$hostSettings = Join-Path $hostUserRoot 'settings.cfg'
$steamAppId = Join-Path $hostSide.Root 'steam_appid.txt'
$documents = [Environment]::GetFolderPath('MyDocuments')
if (-not $documents) { throw 'The Windows Documents folder could not be resolved.' }
# All mods' personal settings: scenarios reset them (ModSettingsDefaults) and set test values (SetModSetting).
$bfqSettings = Join-Path $documents 'SHCDE\APIShared'
$lordSource = Join-Path $workspace 'Testmods\AIMarketAcceptance\Fixtures\SerpMarketRest4'
$lordTarget = Join-Path $hostUserRoot ('CustomLords\' + $lordName)

# --- -RestoreLayout: end of a test series ------------------------------------------------------------

if ($RestoreLayout) {
    $failures = New-Object 'System.Collections.Generic.List[string]'
    $mutex = New-Object Threading.Mutex($false, 'Local\SHCDE.InGameTest.3024040')
    $locked = $false
    try {
        try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
        if (-not $locked) { throw 'Another in-game test is using the host game.' }
        $checks = Get-Preconditions -RestoreOnly
        foreach ($check in $checks) { Write-ProgressLine ('[{0}] {1}: {2}' -f $(if ($check.Ok) { 'OK' } else { 'BLOCK' }), $check.Check, $check.Detail) }
        $blocked = @($checks | Where-Object { -not $_.Ok })
        if ($blocked.Count) { throw ('Preconditions failed before any change: ' + (($blocked | ForEach-Object { $_.Check + ' (' + $_.Detail + ')' }) -join '; ')) }
        foreach ($side in @($hostSide, $clientSide)) {
            $session = $side.LoadedSession
            if (-not $session) { Write-ProgressLine "$($side.Name): no layout session, nothing to restore."; continue }
            Use-LayoutSession $side $session
            $open = @($side.Swaps | Where-Object { -not $_.Restored }).Count
            if ($DryRun) {
                if ($side.PendingRun) { Write-ProgressLine "$($side.Name): would first complete the unfinished settings/saves recovery of run $($side.PendingRun.Run)." }
                Write-ProgressLine ("$($side.Name): would restore $open of $($side.Swaps.Count) swaps (session $($session.State), runs $($session.Runs)), " +
                    "verify plugins ($($side.PluginTree.Files.Count) files) and patchers ($($side.PatcherTree.Files.Count) files) by full hash, then remove $($side.Rollback).")
                continue
            }
            try {
                Complete-PendingRun $side
                Write-ProgressLine "$($side.Name): restoring $open swaps and verifying the original inventories (full hash)."
                $errors = Restore-SideLayout $side
                if ($errors.Count) { foreach ($message in $errors) { $failures.Add($message) } }
                else { Write-ProgressLine "$($side.Name): plugins and patchers restored and verified; rollback folder and session removed." }
            } catch { $failures.Add($_.Exception.Message) }
        }
        if (-not $DryRun) { Remove-PendingBackups }
    } catch { $failures.Add($_.Exception.Message) }
    finally {
        if ($locked) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
    if ($failures.Count) {
        foreach ($failure in $failures) { Write-ProgressLine ('FAIL: ' + $failure) }
        throw ('RESTORE_LAYOUT_FAIL ' + ($failures -join ' | '))
    }
    if ($DryRun) { Write-ProgressLine 'DRYRUN complete; nothing was changed.' }
    else { Write-ProgressLine 'LAYOUT RESTORED on host and client; no layout session remains.' }
    return
}

$hostSide.Scenario = Read-Scenario $HostScenario 'host' -AllowNoQuit:$Manual
$clientSide.Scenario = Read-Scenario $ClientScenario 'client' -AllowNoQuit:$Manual
$relays = Get-RelayRules
$saveEvidenceMode = Get-SaveEvidenceMode $hostSide.Scenario $clientSide.Scenario ([bool]$Manual)

$layout = Get-InGameTestPath $PluginLayout
$api = Get-InGameTestPath $ApiPluginPackage
$patcher = Get-InGameTestPath $ApiPatcherPackage
foreach ($path in @($layout, $api, $patcher)) {
    Assert-InGameTestUnlinked $path
    foreach ($root in @($hostSide.Root, $hostUserRoot, $ClientRoot, $ClientUserRoot)) {
        if (Test-InGameTestInside $path $root) { throw "Supply layout and APIShared pair from outside the game and user trees: $path" }
    }
}
if (-not [IO.Directory]::Exists($layout) -or [IO.Directory]::GetFiles($layout).Length -ne 0) { throw 'The plugin layout must be a directory holding only package directories.' }
$layoutTree = Get-InGameTestTree $layout
foreach ($required in @('000shcdese', 'UiAcceptance_Serp')) {
    if (-not $layoutTree.Directories.Contains($required)) { throw "The plugin layout must contain $required." }
}
Assert-InGameTestMod (Join-Path $layout 'UiAcceptance_Serp') 'UiAcceptance_Serp'
Assert-InGameTestApiPair $api $patcher
$apiTree = Get-InGameTestTree $api
$patcherHash = Get-ApiRepositoryHash $patcher
$expectedPlugins = Get-ExpectedPluginTree
$layoutIdentity = Get-LayoutIdentity
$lordState = if ($saveEvidenceMode -ne 'round-trip') { Assert-LordFixture } else { $null }

Write-ProgressLine ("Run label $label. Host scenario $($hostSide.Scenario.Path). Client scenario $($clientSide.Scenario.Path).")
Write-ProgressLine ("Layout packages: " + ((Get-TopNames $layoutTree | Sort-Object) -join ', '))
if ($layoutTree.Directories.Contains('APIShared_Serp')) { Write-ProgressLine 'The layout''s APIShared_Serp is ignored; the supplied APIShared pair is deployed instead.' }
Write-ProgressLine ("Relays: " + $(if ($relays.Count) { ($relays | ForEach-Object { "$($_.From):$($_.Step) $($_.Result) -> $($_.Checkpoint)" }) -join '; ' } else { 'none' }))
if ($Manual) {
    Write-ProgressLine ("MANUAL session: the user starts and plays both games; observer scenarios, Debug disk logging on both PCs, timeout $TimeoutMinutes min. " +
        'Lord save check on every new/changed .msv (informational).')
} else {
    Write-ProgressLine "Save evidence mode: $saveEvidenceMode"
}
Write-ProgressLine ("Layout identity $layoutIdentity. After the run: " + $(if ($KeepLayout) { 'keep the layout (-KeepLayout)' } else { 'restore plugins and patchers' }))

if ($DryRun) {
    $blocking = New-Object 'System.Collections.Generic.List[string]'
    $mutexProbe = New-Object Threading.Mutex($false, 'Local\SHCDE.InGameTest.3024040')
    try {
        $free = $false
        try { $free = $mutexProbe.WaitOne(0) } catch [Threading.AbandonedMutexException] { $free = $true }
        if ($free) { $mutexProbe.ReleaseMutex() }
        if (-not $free) { $blocking.Add('host-test-mutex: another in-game test is running') }
        Write-ProgressLine ('[{0}] host-test-mutex' -f $(if ($free) { 'OK' } else { 'BLOCK' }))
    } finally { $mutexProbe.Dispose() }
    foreach ($check in (Get-Preconditions -ManualRun:$Manual)) {
        Write-ProgressLine ('[{0}] {1}: {2}' -f $(if ($check.Ok) { 'OK' } else { 'BLOCK' }), $check.Check, $check.Detail)
        if (-not $check.Ok) { $blocking.Add($check.Check + ': ' + $check.Detail) }
    }
    if ($lordState) {
        Write-ProgressLine ('Trading AI fixture: ' + $(if ($lordState.Installed) { 'installed and identical' } else { "would be copied to $lordTarget and removed afterwards" }))
    }
    foreach ($side in @($hostSide, $clientSide)) {
        if (-not [IO.Directory]::Exists($side.Plugins)) { Write-ProgressLine "$($side.Name): plugins not reachable, no plan."; continue }
        if (-not $side.LoadedSession -and (Test-Path -LiteralPath $side.Rollback)) { Write-ProgressLine "$($side.Name) layout: BLOCKED (rollback folder without valid session), no plan."; continue }
        try {
            if ($side.LoadedSession) { Use-LayoutSession $side $side.LoadedSession }
            if ($side.PendingRun) { Write-ProgressLine "$($side.Name): would first complete the unfinished settings/saves recovery of run $($side.PendingRun.Run)." }
            $decision = Get-LayoutDecision $side $side.LoadedSession
            $side.LayoutAction = $decision.Action
            $side.TestModRefresh = [bool]$decision.RefreshTestMod
            $what = switch ($decision.Action) {
                'reuse' { 'REUSE the installed layout session (no backup, swap or plugin hashing)' }
                'rebuild' { 'REBUILD: restore the side from its session (full verify), then apply the requested layout' }
                default { 'APPLY the requested layout (backup, swap, verify; new session)' }
            }
            Write-ProgressLine ("$($side.Name) layout: $what. Reason: $($decision.Reason)")
            if ($decision.Action -eq 'reuse') {
                Write-ProgressLine ("$($side.Name) test mod ${testModName}: " + $(if ($side.TestModRefresh) { 'would be REFRESHED from the layout (staged, swapped, hash-verified; the original stays in the session backup)' } else { 'identical to the layout, no refresh' }))
            }
            if ($decision.Action -eq 'apply') { $side.PluginTree = $null; $side.PatcherTree = $null }
            Get-SidePlan $side
        }
        catch { $blocking.Add($_.Exception.Message); Write-ProgressLine ('BLOCK ' + $_.Exception.Message); continue }
        Write-ProgressLine ("$($side.Name) plugin plan ($($side.Plugins)), backups under $($side.Rollback):")
        foreach ($row in $side.Plan) { Write-ProgressLine ('    {0,-9} {1}' -f $row.Action, $row.Package) }
        Write-ProgressLine ("    original inventories: plugins $($side.PluginTree.Files.Count) files, patchers $($side.PatcherTree.Files.Count) files; saves $($side.SaveTree.Files.Count) files (exists=$($side.SaveTree.Exists)) in $($side.Saves)")
        Write-ProgressLine ('    after the run: ' + $(if ($KeepLayout) { "layout kept, session $($side.SessionPath)" } else { 'plugins and patchers restored, rollback folder and session removed' }))
        Write-ProgressLine "    file backups:"
        foreach ($item in (Get-BackupTargets $side)) {
            $exists = if ($item.Directory) { [IO.Directory]::Exists($item.Path) } else { [IO.File]::Exists($item.Path) }
            Write-ProgressLine ('      {0} (exists={1}; {2})' -f $item.Path, $exists, $item.Why)
        }
        if ($Manual) {
            Write-ProgressLine ("    test files: $($side.Ui)\$label-$($side.Name).json, UiAcceptance_Serp.cfg [Run] (observer scenario)")
            try { Write-ProgressLine ('    Debug logging: ' + (Get-DebugLogPlan $side).Detail) } catch { Write-ProgressLine ('    Debug logging: BLOCK ' + $_.Exception.Message) }
            Write-ProgressLine ("    saves: inventoried before and after ($($side.SaveTree.Files.Count) files now); new/changed .msv copied to the evidence and Lord-checked, nothing deleted")
        } else {
            Write-ProgressLine ("    test files: $($side.Ui)\$label-$($side.Name).json, UiAcceptance_Serp.cfg [Run], relay checkpoints $label-<checkpoint>.ok")
        }
    }
    if ($Manual) {
        Write-ProgressLine ('Manual flow: arm both sides, print the start instructions, observe (important lines at once, HEARTBEAT/STALL every ' +
            "$heartbeatSeconds s), end when both games ran and exited, <evidence>\stop.txt appears or after $TimeoutMinutes min; " +
            'then collect logs, Player.log and saves, restore cfg/settings/BepInEx.cfg, keep the layout, write result.json and report.txt.')
        Write-ProgressLine 'Evidence folder: .work\a\paired-manual\<n> (allocated at the real start).'
    }
    Write-ProgressLine ("DRYRUN complete; nothing was changed. Blocking problems: " + $blocking.Count)
    if ($blocking.Count) { throw ('DRYRUN_BLOCKED: ' + ($blocking -join ' | ')) }
    return
}

# --- -Manual: the user plays both games, the runner prepares, observes and evaluates ---------------------

if ($Manual) {
    $mutex = New-Object Threading.Mutex($false, 'Local\SHCDE.InGameTest.3024040')
    $locked = $false
    $run = $null
    $backup = $null
    $failures = New-Object 'System.Collections.Generic.List[string]'
    $notes = New-Object 'System.Collections.Generic.List[string]'
    $runError = $null
    $endReason = 'not started'
    $sessionStart = [DateTime]::Now
    $sessionEnd = $null
    $saveReports = [ordered]@{ Host = $null; Client = $null }
    $result = $null
    $hostSide.Monitor = New-ManualMonitor 'host'
    $clientSide.Monitor = New-ManualMonitor 'client'
    try {
        try {
            try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
            if (-not $locked) { throw 'Another in-game test is using the host game.' }
            $checks = Get-Preconditions -ManualRun
            foreach ($check in $checks) { Write-ProgressLine ('[{0}] {1}: {2}' -f $(if ($check.Ok) { 'OK' } else { 'BLOCK' }), $check.Check, $check.Detail) }
            $blocked = @($checks | Where-Object { -not $_.Ok })
            if ($blocked.Count) { throw ('Preconditions failed before any change: ' + (($blocked | ForEach-Object { $_.Check + ' (' + $_.Detail + ')' }) -join '; ')) }
            $run = New-ShortWorkDirectory -Root $workspace -Kind a -Name paired-manual -Identity $label
            $backup = [IO.Directory]::CreateDirectory((Join-Path $run 'rollback')).FullName
            $stopFile = Join-Path $run 'stop.txt'
            Write-ProgressLine "Evidence: $run"
            [IO.File]::Copy($hostSide.Scenario.Path, (Join-Path $run ($label + '-host.json')))
            [IO.File]::Copy($clientSide.Scenario.Path, (Join-Path $run ($label + '-client.json')))

            foreach ($side in @($hostSide, $clientSide)) { Initialize-SideLayout $side }
            Remove-PendingBackups
            foreach ($side in @($hostSide, $clientSide)) {
                Get-SidePlan $side
                # The user's saves are inventoried only; Restore-Side never archives or removes them.
                $side.SavesRestored = $true
                Write-ProgressLine ("$($side.Name) plan: " + (($side.Plan | ForEach-Object { $_.Action + ' ' + $_.Package }) -join '; '))
                foreach ($item in (Get-BackupTargets $side)) { Save-SideState $side $item.Path $item.Name -Directory:$item.Directory }
            }
            Write-Recovery 'prepared'
            if ($lordState -and -not $lordState.Installed) { $hostSide.Changed = $true; Copy-InGameTestTree $lordSource $lordTarget $lordState.Inventory }
            foreach ($side in @($hostSide, $clientSide)) {
                if ($side.LayoutAction -eq 'reuse') {
                    if ($side.TestModRefresh) { try { Update-SideTestMod $side } finally { Write-Recovery 'layout' } }
                    Assert-InGameTestMod (Join-Path $side.Plugins 'UiAcceptance_Serp') 'UiAcceptance_Serp'
                }
                else { try { Set-SideLayout $side } finally { Write-Recovery 'layout' } }
                Set-SideScenario $side
                Set-SideDebugLogging $side
            }
            Write-Recovery 'armed'
            Write-ProgressLine ('Plugin layout and APIShared pair in place on host and client (host ' + $hostSide.LayoutAction + ', client ' + $clientSide.LayoutAction + '); observer scenarios armed.')

            $hostSide.Tail = New-LogTail $hostSide.Log
            $clientSide.Tail = New-LogTail $clientSide.Log
            Write-ProgressLine '==================== MANUAL SESSION READY ===================='
            Write-ProgressLine 'Start the game on the host (this PC) and on the client (LENOVO_SERP) now - normal Steam start.'
            Write-ProgressLine 'Play the checklist: F1 (MP game with AI Lord SerpMarketRest4, no Fixes mod: save, then load the save in MP),'
            Write-ProgressLine '    quarry area building in MP, ExtraFeatures gate automation (also in the map editor).'
            Write-ProgressLine 'Exit both games when done; the runner then collects logs and saves, restores and evaluates.'
            Write-ProgressLine "Stop early: create $stopFile. Timeout: $TimeoutMinutes min."
            Write-ProgressLine '==============================================================='

            $watch = [Diagnostics.Stopwatch]::StartNew()
            $exitWatch = $null
            $nextBeat = $heartbeatSeconds
            $printLimit = 400
            while ($true) {
                if ($watch.Elapsed.TotalMinutes -ge $TimeoutMinutes) { $endReason = "timeout after $TimeoutMinutes min"; break }
                if ([IO.File]::Exists($stopFile)) { $endReason = 'stop file'; break }
                $now = [DateTime]::Now
                $hostRunning = Test-HostGameRunning
                $clientRunning = Get-ClientGameRunning
                Update-LogTail $hostSide.Tail
                Update-LogTail $clientSide.Tail
                foreach ($side in @($hostSide, $clientSide)) {
                    $running = if ($side.Name -eq 'host') { $hostRunning } else { $clientRunning }
                    $update = Update-ManualMonitor $side.Monitor $side.Tail $running $now $label
                    foreach ($message in $update.Events) { Write-ProgressLine $message }
                    foreach ($line in $update.Important) {
                        $side.Monitor.Printed++
                        if ($side.Monitor.Printed -le $printLimit) { Write-ProgressLine ("[$($side.Name)] " + $line.Trim()) }
                        elseif ($side.Monitor.Printed -eq $printLimit + 1) { Write-ProgressLine "[$($side.Name)] further important lines are only recorded (limit $printLimit)." }
                    }
                }
                if ($watch.Elapsed.TotalSeconds -ge $nextBeat) {
                    $nextBeat += $heartbeatSeconds
                    foreach ($side in @($hostSide, $clientSide)) {
                        foreach ($text in (Format-ManualHeartbeat $side.Monitor $side.Tail $watch.Elapsed.TotalSeconds)) { Write-ProgressLine $text }
                        foreach ($message in (Test-ManualStall $side.Monitor $now $heartbeatSeconds)) { Write-ProgressLine $message }
                    }
                    $waiting = @(@($hostSide, $clientSide) | Where-Object { -not $_.Monitor.EverRan } | ForEach-Object { $_.Name })
                    if ($waiting.Count) { Write-ProgressLine ('    waiting for the game start on: ' + ($waiting -join ', ')) }
                }
                $bothDone = $hostSide.Monitor.EverRan -and $clientSide.Monitor.EverRan -and $hostRunning -eq $false -and $clientRunning -eq $false
                if ($bothDone) {
                    if (-not $exitWatch) { $exitWatch = [Diagnostics.Stopwatch]::StartNew(); Write-ProgressLine 'Both games exited; confirming for 30 s.' }
                    elseif ($exitWatch.Elapsed.TotalSeconds -ge 30) { $endReason = 'both games ran and exited'; break }
                } elseif ($exitWatch) { $exitWatch = $null; Write-ProgressLine 'A game runs again; observation continues.' }
                Start-Sleep -Milliseconds 1000
            }
            Write-ProgressLine "Observation ended: $endReason."
        } catch {
            $runError = $_.Exception.Message
            $failures.Add('RUN: ' + $runError)
            Write-ProgressLine ('FAILED: ' + $runError)
        } finally {
            $sessionEnd = [DateTime]::Now
            $hostSide.Exited = -not (Test-HostGameRunning)
            if ([IO.Directory]::Exists($clientSide.Bep)) { $clientSide.Exited = Test-LogUnlocked $clientSide.Log }
            foreach ($side in @($hostSide, $clientSide)) {
                if ($side.Tail) { try { Update-LogTail $side.Tail -Final; $null = Update-ManualMonitor $side.Monitor $side.Tail $null $sessionEnd $label } catch { } }
                foreach ($message in (Test-ManualStall $side.Monitor $sessionEnd $heartbeatSeconds)) { Write-ProgressLine $message }
            }
            if ($run) {
                foreach ($side in @($hostSide, $clientSide)) {
                    $player = Join-Path $side.UserRoot 'Player.log'
                    foreach ($pair in @(@($side.Log, ($side.Name + '-LogOutput.log')), @($player, ($side.Name + '-Player.log')))) {
                        try { if ([IO.File]::Exists($pair[0])) { Copy-SharedFile $pair[0] (Join-Path $run $pair[1]) } else { $notes.Add("$($side.Name): $($pair[0]) not found") } }
                        catch { $notes.Add("$($side.Name) $($pair[1]): " + $_.Exception.Message) }
                    }
                    if ($side.Tail) {
                        try { Write-TextLines (Join-Path $run ($side.Name + '-observed.log')) ([string[]]$side.Tail.Lines.ToArray()) } catch { $notes.Add("$($side.Name) observed log: " + $_.Exception.Message) }
                    }
                    try { Save-LogExcerpt $side } catch { $notes.Add("$($side.Name) log excerpt: " + $_.Exception.Message) }
                    if ($side.SaveTree) {
                        try {
                            $diff = Get-SaveDiff $side.SaveTree (Get-InGameTestTree $side.Saves)
                            $checks = Save-ManualSaves $side.Name $side.Saves $diff $run
                            $key = if ($side.Name -eq 'host') { 'Host' } else { 'Client' }
                            $saveReports[$key] = [ordered]@{ Folder = $side.Saves; New = $diff.New; Changed = $diff.Changed; Removed = $diff.Removed; Checks = $checks }
                            foreach ($check in $checks) {
                                Write-ProgressLine ("SAVE side=$($side.Name) $($check.Kind) $($check.Save): Lord save check " + $(if ($check.LordSaveOk) { 'OK (F1 case)' } else { 'not F1: ' + (@($check.Problems) -join '; ') }))
                            }
                        } catch { $notes.Add("$($side.Name) saves: " + $_.Exception.Message) }
                    }
                }
                foreach ($side in @($hostSide, $clientSide)) {
                    if ($side.Exited) { Restore-Side $side }
                    elseif ($side.Changed -or $side.Swaps.Count) {
                        $side.Deferred = $true
                        if ($side.LayoutActive) {
                            if (-not $side.PendingRun) { $side.PendingRun = New-PendingRunRecord $side }
                            try { Write-LayoutSession $side } catch { $notes.Add("$($side.Name) layout session: " + $_.Exception.Message) }
                        }
                    }
                    else { $side.Restored = $true }
                }
                try { Write-Recovery $(if ($hostSide.Deferred -or $clientSide.Deferred) { 'deferred' } else { 'completed' }) } catch { $notes.Add('recovery.json: ' + $_.Exception.Message) }
                foreach ($side in @($hostSide, $clientSide)) {
                    if ($side.Deferred) {
                        $how = if ($side.LayoutActive) { "run Run-Paired.ps1 -RestoreLayout (or the next run, which completes it first); session $($side.SessionPath)" }
                            else { "restore from $($side.Rollback) and $backup as described in $(Join-Path $run 'recovery.json')" }
                        Write-Warning ("DEFERRED RECOVERY: the $($side.Name) game still holds its log. Nothing was restored on the $($side.Name) (cfg, BepInEx.cfg, settings). After it exits, $how.")
                        $failures.Add("$($side.Name): deferred recovery, not restored")
                    } elseif (-not $side.Restored) { $failures.Add("$($side.Name): restoration failed: " + ($side.RestoreErrors -join ' | ')) }
                    foreach ($message in $side.RestoreErrors) { Write-ProgressLine ('RESTORE ERROR: ' + $message) }
                }
                if ($hostSide.Restored -and $clientSide.Restored) {
                    try { Remove-InGameTestTree $backup $run } catch { $notes.Add('local backups kept: ' + $_.Exception.Message) }
                }
            }
        }

        if ($run) {
            $hostLines = @(); $clientLines = @()
            if ($hostSide.Tail) { $hostLines = [string[]]$hostSide.Tail.Lines.ToArray() }
            if ($clientSide.Tail) { $clientLines = [string[]]$clientSide.Tail.Lines.ToArray() }
            $hostF = Get-ManualSideFindings $hostLines $label
            $clientF = Get-ManualSideFindings $clientLines $label
            $debugOn = [bool]($hostSide.DebugLog -and $clientSide.DebugLog)
            $f1 = Get-ManualF1 $hostLines $clientLines $hostF $clientF ([bool]$hostSide.DebugLog)
            $observe = Compare-PairedObserve $hostF $clientF
            $stalls = @(@($hostSide.Monitor.Stalls.ToArray()) + @($clientSide.Monitor.Stalls.ToArray()) | ForEach-Object {
                [ordered]@{ Side = $_.Side; Kind = $_.Kind; Since = $_.Since; Detected = $_.Detected; End = $_.End; Seconds = $_.Seconds; EndReason = $_.EndReason; Mission = $_.Mission } })
            $sides = [ordered]@{
                Host = ConvertTo-ManualSideRecord $hostSide.Monitor ([bool]$hostSide.Exited) $hostF $hostSide.DebugLog $hostSide.Tail
                Client = ConvertTo-ManualSideRecord $clientSide.Monitor ([bool]$clientSide.Exited) $clientF $clientSide.DebugLog $clientSide.Tail
            }
            $findingCount = 0
            foreach ($name in @('Host', 'Client')) { $findingCount += $sides[$name].ModFailures.Count + $sides[$name].Errors.Count + $sides[$name].Desync.Count }
            $findingCount += $sides.Host.ObserveFailed.Count + $sides.Client.ObserveFailed.Count
            # Stall candidates are listed but not counted: a paused game looks the same in the log.
            $findingCount += @($f1.Attempts | Where-Object { $_.Verdict -ne 'OK' }).Count + @($observe.Mismatches).Count
            $result = [ordered]@{
                Run = $label; Mode = 'manual'; Verdict = $(if ($findingCount) { 'FINDINGS' } else { 'NO_FINDINGS' }); RunnerOk = $failures.Count -eq 0
                Failures = $failures.ToArray(); Error = $runError; Evidence = $run; EndReason = $endReason
                Started = (Format-Clock $sessionStart); Ended = (Format-Clock $sessionEnd); TimeoutMinutes = $TimeoutMinutes
                DebugLoggingBoth = $debugOn; Sides = $sides; F1 = $f1; Observe = $observe; Stalls = $stalls; Saves = $saveReports
                Restored = [ordered]@{ Host = $hostSide.Restored; Client = $clientSide.Restored }
                Deferred = [ordered]@{ Host = $hostSide.Deferred; Client = $clientSide.Deferred }
                RestoreErrors = @($hostSide.RestoreErrors.ToArray()) + @($clientSide.RestoreErrors.ToArray())
                LayoutKept = [bool]($hostSide.LayoutKept -and $clientSide.LayoutKept)
                Layout = [ordered]@{
                    Identity = $layoutIdentity
                    Host = [ordered]@{ Action = $hostSide.LayoutAction; Reason = $hostSide.LayoutReason; State = $hostSide.LayoutState; Kept = $hostSide.LayoutKept; Session = $hostSide.SessionPath }
                    Client = [ordered]@{ Action = $clientSide.LayoutAction; Reason = $clientSide.LayoutReason; State = $clientSide.LayoutState; Kept = $clientSide.LayoutKept; Session = $clientSide.SessionPath }
                }
                Notes = $notes.ToArray()
            }
            try {
                $report = Format-ManualReport $result
                Write-TextLines (Join-Path $run 'report.txt') $report
                foreach ($line in $report) { Write-Host $line }
            } catch { Write-Warning ('report.txt: ' + $_.Exception.Message) }
        }
    } finally {
        if ($run) {
            if (-not $result) { $result = [ordered]@{ Run = $label; Mode = 'manual'; RunnerOk = $false; Failures = @($failures) + @('evaluation did not complete'); Error = $runError; Evidence = $run } }
            try { Write-JsonFile (Join-Path $run 'result.json') $result } catch { Write-Warning ('result.json: ' + $_.Exception.Message) }
            Write-ProgressLine ('Evidence: ' + $run)
        }
        if ($locked) { $mutex.ReleaseMutex() }
        $mutex.Dispose()
    }
    $kept = @(@($hostSide, $clientSide) | Where-Object { $_.LayoutKept })
    if ($kept.Count) {
        Write-ProgressLine ('LAYOUT KEPT session=' + (($kept | ForEach-Object { $_.Name + ':' + $_.SessionPath }) -join ', ') + ' - restore with -RestoreLayout after all tests')
    }
    if ($failures.Count) {
        foreach ($failure in $failures) { Write-ProgressLine ('FAIL: ' + $failure) }
        throw ("MANUAL_RUNNER_FAIL run=$label. " + ($failures -join ' | '))
    }
    Write-ProgressLine ("MANUAL_DONE run=$label findings=$($result.Verdict) (informational; see report.txt). Settings, cfg and BepInEx.cfg restored on both PCs; plugin layout kept.")
    return
}

# --- real run -----------------------------------------------------------------------------------------

Add-Type -Path (Join-Path $workspace 'Shared/Tools/Automation/InGameTest.DesktopHost.cs')
$mutex = New-Object Threading.Mutex($false, 'Local\SHCDE.InGameTest.3024040')
$locked = $false
$run = $null
$backup = $null
$instance = $null
$desktop = [IntPtr]::Zero
$hostExitCode = $null
$failures = New-Object 'System.Collections.Generic.List[string]'
$runError = $null
$clientStart = $null
$lobbies = New-Object 'System.Collections.Generic.List[string]'
$desktopEvents = New-Object 'System.Collections.Generic.List[string]'
$result = $null
try {
    try {
        try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
        if (-not $locked) { throw 'Another in-game test is using the host game.' }
        $blocked = @((Get-Preconditions) | Where-Object { -not $_.Ok })
        if ($blocked.Count) {
            throw ('Preconditions failed before any change: ' + (($blocked | ForEach-Object { $_.Check + ' (' + $_.Detail + ')' }) -join '; '))
        }
        $run = New-ShortWorkDirectory -Root $workspace -Kind a -Name paired -Identity $label
        $backup = [IO.Directory]::CreateDirectory((Join-Path $run 'rollback')).FullName
        Write-ProgressLine "Evidence: $run"
        [IO.File]::Copy($hostSide.Scenario.Path, (Join-Path $run ($label + '-host.json')))
        [IO.File]::Copy($clientSide.Scenario.Path, (Join-Path $run ($label + '-client.json')))

        foreach ($side in @($hostSide, $clientSide)) { Initialize-SideLayout $side }
        Remove-PendingBackups
        foreach ($side in @($hostSide, $clientSide)) {
            Get-SidePlan $side
            Write-ProgressLine ("$($side.Name) plan: " + (($side.Plan | ForEach-Object { $_.Action + ' ' + $_.Package }) -join '; '))
            foreach ($item in (Get-BackupTargets $side)) { Save-SideState $side $item.Path $item.Name -Directory:$item.Directory }
        }
        Write-Recovery 'prepared'

        if ($lordState -and -not $lordState.Installed) { $hostSide.Changed = $true; Copy-InGameTestTree $lordSource $lordTarget $lordState.Inventory }
        foreach ($side in @($hostSide, $clientSide)) {
            if ($side.LayoutAction -eq 'reuse') {
                if ($side.TestModRefresh) { try { Update-SideTestMod $side } finally { Write-Recovery 'layout' } }
                Assert-InGameTestMod (Join-Path $side.Plugins 'UiAcceptance_Serp') 'UiAcceptance_Serp'
            }
            else { try { Set-SideLayout $side } finally { Write-Recovery 'layout' } }
            Set-SideScenario $side
        }
        Write-ProgressLine ('Plugin layout and APIShared pair in place on host and client (host ' + $hostSide.LayoutAction + ', client ' + $clientSide.LayoutAction + ').')
        $joinFile = Join-Path $clientSide.Ui 'join-lobby.txt'
        $clientSide.TestPaths.Add($joinFile + '.tmp')
        if ([IO.File]::Exists($joinFile)) { [IO.File]::Delete($joinFile) }
        if (-not [IO.File]::Exists($steamAppId)) { [IO.File]::WriteAllText($steamAppId, ($appId + $crlf), $utf8) }
        $text = [IO.File]::ReadAllText($hostSettings)
        foreach ($key in $muteKeys) { $text = [regex]::Replace($text, ('(?m)^' + $key + ':[^\r\n]*'), ($key + ':0')) }
        [IO.File]::WriteAllText($hostSettings, $text, $utf8)

        $hostSide.Tail = New-LogTail $hostSide.Log
        $clientSide.Tail = New-LogTail $clientSide.Log
        $inputDesktop = [InGameTestDesktopHost]::InputDesktop()
        $desktopName = 'SerpTest_' + $label
        # Desktop access without DESKTOP_SWITCHDESKTOP; SwitchDesktop is never called.
        $desktop = [InGameTestDesktopHost]::CreateDesktopW($desktopName, [IntPtr]::Zero, [IntPtr]::Zero, 0, 0xC3, [IntPtr]::Zero)
        if ($desktop -eq [IntPtr]::Zero) { throw (New-Object ComponentModel.Win32Exception) }
        $arguments = '-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "' + (Join-Path $run 'Player.log') + '"'
        $instance = [InGameTestDesktopHost]::Start($exe, $arguments, $hostSide.Root, $desktopName)
        Write-ProgressLine ("Started the host game on $desktopName, PID $($instance.Id). Timeout $TimeoutMinutes min.")

        $watch = [Diagnostics.Stopwatch]::StartNew()
        $lockWatch = [Diagnostics.Stopwatch]::StartNew()
        $hostExitWatch = $null
        $seenWindow = $false
        $clientRunningSeen = $false
        $nextBeat = $heartbeatSeconds
        $lobbyRegex = 'MP_HOST lobby=(\d+) run=' + $labelPattern + '(\s|$)'
        $acceptRegex = $runRegex + '(\S+) (PASS|FAIL)\b'
        while ($true) {
            if ($watch.Elapsed.TotalMinutes -ge $TimeoutMinutes) { throw "TIMEOUT after $TimeoutMinutes min." }
            if (-not $instance.HasExited) {
                if ([InGameTestDesktopHost]::InputDesktop() -cne $inputDesktop -or [InGameTestDesktopHost]::HasInputWindow($instance.Id)) {
                    throw 'The input desktop changed or received the host game window.'
                }
                $others = @(Get-Process -Name $gameName -ErrorAction SilentlyContinue)
                $foreign = @($others | Where-Object { $_.Id -ne $instance.Id }).Count
                foreach ($process in $others) { $process.Dispose() }
                if ($foreign) { throw 'A second host game process appeared (Steam restart?).' }
                if (-not $seenWindow) {
                    $seenWindow = @([InGameTestDesktopHost]::Windows($desktop) | Where-Object { $_.Pid -eq $instance.Id }).Count -gt 0
                    if ($seenWindow) { Write-ProgressLine "Host window confirmed on $desktopName." }
                    elseif ($watch.Elapsed.TotalSeconds -gt 80) { throw 'The host window did not appear on the separate desktop.' }
                }
            }
            Update-LogTail $hostSide.Tail
            Update-LogTail $clientSide.Tail
            foreach ($side in @($hostSide, $clientSide)) {
                $tail = $side.Tail
                for (; $tail.Scanned -lt $tail.Lines.Count; $tail.Scanned++) {
                    $line = $tail.Lines[$tail.Scanned]
                    if ($side.Name -eq 'host') {
                        $hosted = [regex]::Match($line, $lobbyRegex)
                        if ($hosted.Success) {
                            # Every new lobby (e.g. after QuitToMenu) is handed over again.
                            Write-AtomicText $joinFile $hosted.Groups[1].Value
                            $lobbies.Add($hosted.Groups[1].Value)
                            Write-ProgressLine ("Published host lobby $($hosted.Groups[1].Value) to the client (#$($lobbies.Count)).")
                            if (-not $clientStart) {
                                $clientStart = Request-ClientGameStart $ClientRoot $label
                                if (-not $clientStart.Started) {
                                    throw ("The client start watcher did not start the client game ($($clientStart.Detail)). " +
                                        'Start _Automation\Start-ClientStartWatcher.bat on the client and run again.')
                                }
                                Write-ProgressLine 'The client start watcher launched the client game.'
                            }
                        }
                    }
                    $accept = [regex]::Match($line, $acceptRegex)
                    if (-not $accept.Success) { continue }
                    $side.LastAccept = $line
                    if ($accept.Groups[1].Value -ceq 'SUMMARY') { if (-not $side.Summary) { $side.Summary = $line; Write-ProgressLine ("$($side.Name): $line") } ; continue }
                    foreach ($rule in $relays) {
                        if ($rule.Sent -or $rule.From -ne $side.Name -or $rule.Step -cne $accept.Groups[1].Value) { continue }
                        if ($rule.Result -ne 'ANY' -and $rule.Result -ne $accept.Groups[2].Value) { continue }
                        Publish-Checkpoint (Get-Other $side) $rule.Checkpoint
                        $rule.Sent = $true
                        Write-ProgressLine ("Relayed $($side.Name) step $($rule.Step) $($accept.Groups[2].Value) as checkpoint $($rule.Checkpoint).")
                    }
                }
            }
            if ($hostSide.Summary -and $clientSide.Summary) { break }
            if ($instance.HasExited) {
                if (-not $hostSide.Summary) { throw ('The host game exited without its SUMMARY (exit code {0}).' -f $instance.ExitCode) }
                if (-not $hostExitWatch) { $hostExitWatch = [Diagnostics.Stopwatch]::StartNew() }
                elseif ($hostExitWatch.Elapsed.TotalSeconds -gt 180) { throw 'The client did not reach its SUMMARY within 180 s after the host exited.' }
            }
            if ($clientStart -and $lockWatch.Elapsed.TotalSeconds -ge 5) {
                $lockWatch.Restart()
                $clientRunning = -not (Test-LogUnlocked $clientSide.Log)
                if ($clientRunning -and -not $clientRunningSeen) { $clientRunningSeen = $true; Write-ProgressLine 'Client game is running (its log is locked).' }
                elseif (-not $clientRunning -and $clientRunningSeen -and -not $clientSide.Summary) {
                    Update-LogTail $clientSide.Tail
                    if (-not @($clientSide.Tail.Lines | Where-Object { $_ -match ($runRegex + 'SUMMARY ') }).Count) { throw 'The client game exited without its SUMMARY.' }
                }
            }
            if ($watch.Elapsed.TotalSeconds -ge $nextBeat) {
                $nextBeat += $heartbeatSeconds
                $cpu = ''
                if (-not $instance.HasExited) { $instance.Refresh(); $cpu = ' cpu={0:N0}s' -f $instance.TotalProcessorTime.TotalSeconds }
                Write-ProgressLine ('HEARTBEAT {0:N0}s{1} host log={2} B lines={3} | client log={4} B lines={5}{6} | lobbies={7}' -f $watch.Elapsed.TotalSeconds, $cpu,
                    $hostSide.Tail.Length, $hostSide.Tail.Lines.Count, $clientSide.Tail.Length, $clientSide.Tail.Lines.Count,
                    $(if ($clientSide.Tail.Error) { ' (read error: ' + $clientSide.Tail.Error + ')' } else { '' }), $lobbies.Count)
                Write-ProgressLine ('    host last: ' + $(if ($hostSide.LastAccept) { $hostSide.LastAccept.Trim() } else { '-' }))
                Write-ProgressLine ('    client last: ' + $(if ($clientSide.LastAccept) { $clientSide.LastAccept.Trim() } else { '-' }))
            }
            Start-Sleep -Milliseconds $pollMilliseconds
        }
    } catch {
        $runError = $_.Exception.Message
        $failures.Add('RUN: ' + $runError)
        Write-ProgressLine ('FAILED: ' + $runError)
    } finally {
        if ($instance) {
            try {
                $grace = if ($hostSide.Summary) { 60000 } else { 20000 }
                if (-not $instance.HasExited -and -not $instance.WaitForExit($grace)) {
                    Write-ProgressLine 'Terminating the host test game.'
                    $instance.Kill()
                    $null = $instance.WaitForExit(15000)
                }
                if ($instance.HasExited) { $hostExitCode = $instance.ExitCode }
            } catch { $desktopEvents.Add('host stop: ' + $_.Exception.Message) }
            $instance.Dispose()
        }
        if ($desktop -ne [IntPtr]::Zero -and -not [InGameTestDesktopHost]::CloseDesktop($desktop)) { $desktopEvents.Add('desktop handle could not be closed') }
        $clock = [Diagnostics.Stopwatch]::StartNew()
        do {
            $games = @(Get-Process -Name $gameName -ErrorAction SilentlyContinue)
            foreach ($game in $games) { $game.Dispose() }
            $hostSide.Exited = $games.Count -eq 0
            if (-not $hostSide.Exited) { Start-Sleep -Milliseconds $pollMilliseconds }
        } while (-not $hostSide.Exited -and $clock.Elapsed.TotalSeconds -lt 30)
        if ($run -and [IO.Directory]::Exists($clientSide.Bep)) {
            $limit = if ($clientStart) { 120 } else { 10 }
            $clock = [Diagnostics.Stopwatch]::StartNew()
            $nextNote = 0
            do {
                $clientSide.Exited = Test-LogUnlocked $clientSide.Log
                if (-not $clientSide.Exited) {
                    if ($clock.Elapsed.TotalSeconds -ge $nextNote) { Write-ProgressLine ('Waiting for the client game to exit ({0:N0}/{1} s).' -f $clock.Elapsed.TotalSeconds, $limit); $nextNote += 30 }
                    Start-Sleep -Milliseconds $pollMilliseconds
                }
            } while (-not $clientSide.Exited -and $clock.Elapsed.TotalSeconds -lt $limit)
        }
        foreach ($side in @($hostSide, $clientSide)) { if ($side.Tail) { try { Update-LogTail $side.Tail -Final } catch { } } }
        if ($run) {
            if ($clientSide.Exited) {
                try {
                    $clientPlayer = Join-Path $ClientUserRoot 'Player.log'
                    if ($clientStart -and [IO.File]::Exists($clientPlayer)) {
                        $stream = Open-SharedRead $clientPlayer
                        try { $out = [IO.File]::Create((Join-Path $run 'client-Player.log')); try { $stream.CopyTo($out) } finally { $out.Dispose() } } finally { $stream.Dispose() }
                    }
                } catch { $desktopEvents.Add('client Player.log: ' + $_.Exception.Message) }
            }
            foreach ($side in @($hostSide, $clientSide)) {
                try { Save-LogExcerpt $side } catch { $desktopEvents.Add("$($side.Name) log excerpt: " + $_.Exception.Message) }
                if ($side.Exited) { Restore-Side $side }
                elseif ($side.Changed -or $side.Swaps.Count) {
                    $side.Deferred = $true
                    if ($side.LayoutActive) {
                        # Nothing is restored while the game runs; the session records what the next start must complete.
                        if (-not $side.PendingRun) { $side.PendingRun = New-PendingRunRecord $side }
                        try { Write-LayoutSession $side } catch { $desktopEvents.Add("$($side.Name) layout session: " + $_.Exception.Message) }
                    }
                }
                else { $side.Restored = $true }
            }
            try { Write-Recovery $(if ($hostSide.Deferred -or $clientSide.Deferred) { 'deferred' } else { 'completed' }) } catch { $desktopEvents.Add('recovery.json: ' + $_.Exception.Message) }
            foreach ($side in @($hostSide, $clientSide)) {
                if ($side.Deferred) {
                    $how = if ($side.LayoutActive) { "run Run-Paired.ps1 -RestoreLayout (or the next run, which completes it first); session $($side.SessionPath)" }
                        else { "restore from $($side.Rollback) and $backup as described in $(Join-Path $run 'recovery.json')" }
                    Write-Warning ("DEFERRED RECOVERY: the $($side.Name) game still holds its log. Nothing was restored on the $($side.Name). After it exits, $how.")
                }
                foreach ($message in $side.RestoreErrors) { Write-ProgressLine ('RESTORE ERROR: ' + $message) }
            }
            if ($hostSide.Restored -and $clientSide.Restored) {
                try { Remove-InGameTestTree $backup $run } catch { $desktopEvents.Add('local backups kept: ' + $_.Exception.Message) }
            }
        }
    }

    if ($run) {
        $findings = @{ host = Get-SideFindings $hostSide; client = Get-SideFindings $clientSide }
        foreach ($name in @('host','client')) {
            $found = $findings[$name]
            if ($found.Summaries.Count -ne 1) { $failures.Add("${name}: expected exactly one SUMMARY line, found $($found.Summaries.Count)") }
            elseif ($found.Summaries[0] -notmatch ($runRegex + 'SUMMARY PASS .*\bfailed=0 skipped=0\b')) { $failures.Add("${name}: SUMMARY is not PASS with failed=0 skipped=0") }
            if ($found.ModFailures.Count) { $failures.Add("${name}: $($found.ModFailures.Count) MOD_FAILURE line(s)") }
            if ($found.Desync.Count) { $failures.Add("${name}: $($found.Desync.Count) desync/out-of-sync line(s)") }
        }
        $observeCompare = Compare-PairedObserve $findings.host.Observed $findings.client.Observed
        if (@($observeCompare.Mismatches).Count) { $failures.Add("OBSERVE differs between host and client for: " + ((@($observeCompare.Mismatches) | ForEach-Object { $_.Key }) -join ', ')) }
        foreach ($side in @($hostSide, $clientSide)) {
            if ($side.Deferred) { $failures.Add("$($side.Name): deferred recovery, not restored") }
            elseif (-not $side.Restored) { $failures.Add("$($side.Name): restoration failed: " + ($side.RestoreErrors -join ' | ')) }
        }
        $saveCheck = $null
        if ($saveEvidenceMode -ne 'none') {
            $saveCheck = [ordered]@{}
            foreach ($side in @($hostSide, $clientSide)) {
                try {
                    $saveCheck[$side.Name] = if ($saveEvidenceMode -eq 'round-trip') {
                        Test-MultiplayerSaveRoundTrip $side $findings[$side.Name].Observed $testSaveName
                    } else { Test-LordSave $side.ArchivedSave }
                }
                catch { $saveCheck[$side.Name] = [ordered]@{ Path = $side.ArchivedSave; Ok = $false; Problems = @('save check error: ' + $_.Exception.Message) } }
                if (-not $saveCheck[$side.Name].Ok) { $failures.Add("$($side.Name) save: " + (@($saveCheck[$side.Name].Problems) -join '; ')) }
            }
            if ($saveEvidenceMode -eq 'round-trip' -and $saveCheck.host.Ok -and $saveCheck.client.Ok -and
                $saveCheck.host.ArchiveTick -ne $saveCheck.client.ArchiveTick) {
                $failures.Add("The restored native tick differs between host ($($saveCheck.host.ArchiveTick)) and client ($($saveCheck.client.ArchiveTick)).")
            }
        }
        $result = [ordered]@{
            Run = $label; Pass = $failures.Count -eq 0; Failures = @($failures); Error = $runError; Evidence = $run
            HostSummary = $hostSide.Summary; ClientSummary = $clientSide.Summary
            HostExitCode = $hostExitCode; HostExited = $hostSide.Exited; ClientExited = $clientSide.Exited
            ClientStart = $(if ($clientStart) { $clientStart.Detail } else { $null }); Lobbies = @($lobbies)
            Relays = @($relays | ForEach-Object { [ordered]@{ From = $_.From; Step = $_.Step; Result = $_.Result; Checkpoint = $_.Checkpoint; Sent = $_.Sent } })
            Restored = [ordered]@{ Host = $hostSide.Restored; Client = $clientSide.Restored; HostSaves = $hostSide.SavesRestored; ClientSaves = $clientSide.SavesRestored }
            LayoutKept = [bool]($hostSide.LayoutKept -and $clientSide.LayoutKept)
            Layout = [ordered]@{
                Identity = $layoutIdentity; KeepRequested = [bool]$KeepLayout
                Host = [ordered]@{ Action = $hostSide.LayoutAction; Reason = $hostSide.LayoutReason; State = $hostSide.LayoutState; Kept = $hostSide.LayoutKept; PluginsRestored = $hostSide.LayoutRestored; Session = $(if ($hostSide.LayoutActive) { $hostSide.SessionPath } else { $null }) }
                Client = [ordered]@{ Action = $clientSide.LayoutAction; Reason = $clientSide.LayoutReason; State = $clientSide.LayoutState; Kept = $clientSide.LayoutKept; PluginsRestored = $clientSide.LayoutRestored; Session = $(if ($clientSide.LayoutActive) { $clientSide.SessionPath } else { $null }) }
            }
            Deferred = [ordered]@{ Host = $hostSide.Deferred; Client = $clientSide.Deferred }
            RestoreErrors = @($hostSide.RestoreErrors) + @($clientSide.RestoreErrors)
            SaveEvidenceMode = $saveEvidenceMode; SaveCheck = $saveCheck
            Observe = $observeCompare
            ModFailures = [ordered]@{ Host = $findings.host.ModFailures; Client = $findings.client.ModFailures }
            DesyncLines = [ordered]@{ Host = $findings.host.Desync; Client = $findings.client.Desync }
            LogLines = [ordered]@{ Host = $findings.host.Lines; Client = $findings.client.Lines; HostRewrites = $hostSide.Tail.Rewrites; ClientRewrites = $(if ($clientSide.Tail) { $clientSide.Tail.Rewrites } else { 0 }) }
            Notes = @($desktopEvents)
        }
    }
} finally {
    if ($run) {
        if (-not $result) { $result = [ordered]@{ Run = $label; Pass = $false; Failures = @($failures) + @('evaluation did not complete'); Error = $runError; Evidence = $run } }
        try { Write-JsonFile (Join-Path $run 'result.json') $result } catch { Write-Warning ('result.json: ' + $_.Exception.Message) }
        Write-ProgressLine ('Evidence: ' + $run)
    }
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}

$kept = @(@($hostSide, $clientSide) | Where-Object { $_.LayoutKept })
if ($kept.Count) {
    Write-ProgressLine ('LAYOUT KEPT session=' + (($kept | ForEach-Object { $_.Name + ':' + $_.SessionPath }) -join ', ') +
        ' - restore with -RestoreLayout after all tests')
}
if ($KeepLayout -and $run -and $kept.Count -ne 2) {
    Write-ProgressLine ('Layout not kept on: ' + ((@(@($hostSide, $clientSide) | Where-Object { -not $_.LayoutKept }) | ForEach-Object { $_.Name }) -join ', ') +
        ' (not fully applied or restored instead); the next run applies it again there.')
}
if ($failures.Count) {
    foreach ($failure in $failures) { Write-ProgressLine ('FAIL: ' + $failure) }
    throw ("PAIRED_FAIL run=$label. " + ($failures -join ' | '))
}
$restoredWhat = if ($kept.Count) { 'settings and saves restored on both PCs; plugin layout kept' } else { 'plugins, settings and saves restored on both PCs' }
Write-ProgressLine "PAIRED_PASS run=$label. Both peers passed; $restoredWhat."
