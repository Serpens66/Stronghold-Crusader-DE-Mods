[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$gameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
foreach ($dependency in @(
    (Join-Path $gameDir 'BepInEx\core\BepInEx.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\000shcdese\System.Memory.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\000shcdese\System.Buffers.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\000shcdese\SHCDESE.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\APIShared_Serp\APIShared.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\BugfixesAndQoL_Serp\BugfixesAndQoL.dll')
)) { [void][Reflection.Assembly]::LoadFrom($dependency) }
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'BepInEx\plugins\SurrenderDesyncDiagnostic_Serp\SurrenderDesyncDiagnostic.dll'))
$flags = [Reflection.BindingFlags]'Instance,NonPublic,Public'
$loggerType = [Reflection.Assembly]::LoadFrom((Join-Path $gameDir 'BepInEx\core\BepInEx.dll')).GetType('BepInEx.Logging.ManualLogSource', $true)
$logger = [Activator]::CreateInstance($loggerType, @('Surrender binary recorder test'))
$recorderType = $assembly.GetType('SurrenderDesyncDiagnostic.DeferredTraceRecorder', $true)
$recorder = [Activator]::CreateInstance($recorderType, $flags, $null, @($logger), $null)
$comparer = $assembly.GetType('SurrenderDesyncDiagnostic.BinaryTraceComparison', $true)
$folder = Join-Path ([IO.Path]::GetTempPath()) ('surrender-binary-test-' + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($folder) | Out-Null
$playerType = [Reflection.Assembly]::LoadFrom((Join-Path $gameDir 'BepInEx\plugins\000shcdese\SHCDESE.dll')).GetType('SHCDESE.Interop.GamePlayerResources', $true)

function New-ObjectOf([string]$name) {
    return [Activator]::CreateInstance($assembly.GetType("SurrenderDesyncDiagnostic.$name", $true), $true)
}
function Set-Field($target, [string]$name, $value) {
    $target.GetType().GetField($name, $flags).SetValue($target, $value)
}
function New-Work([string]$kind, [string]$base, $snapshot, [string]$text, [string]$reason, [bool]$probe = $false) {
    $work = New-ObjectOf 'TraceWork'
    $kindType = $assembly.GetType('SurrenderDesyncDiagnostic.TraceWorkKind', $true)
    Set-Field $work 'Kind' ([Enum]::Parse($kindType, $kind))
    Set-Field $work 'TraceBase' $base
    Set-Field $work 'Snapshot' $snapshot
    Set-Field $work 'Text' $text
    Set-Field $work 'Reason' $reason
    Set-Field $work 'Probe' $probe
    return $work
}
function Enqueue($work) {
    if (-not $recorderType.GetMethod('Enqueue', $flags).Invoke($recorder, @($work))) {
        throw 'Writer unexpectedly rejected an owned snapshot.'
    }
}
function New-Snapshot([int]$tick, [long]$order, [int]$lord, [string]$phase, [bool]$tenCategories = $false) {
    $snapshot = New-ObjectOf 'TraceSnapshot'
    Set-Field $snapshot 'MapTick' $tick
    Set-Field $snapshot 'DirectorTick' ($tick + 9)
    Set-Field $snapshot 'Order' $order
    Set-Field $snapshot 'Phase' $phase
    Set-Field $snapshot 'LocalState' 'localPlayer=1,spectator=0'
    $names = if ($tenCategories) {
        @('players','units','buildings','tribes','projectiles','vegetation',
            'path-components','path-edges','moat-work','connections')
    } else { @('players') }
    foreach ($name in $names) {
        $category = New-ObjectOf 'TraceCategory'
        Set-Field $category 'Name' $name
        if ($name -eq 'players') {
            $source = [Runtime.InteropServices.Marshal]::AllocHGlobal(22588)
            try {
                [Runtime.InteropServices.Marshal]::Copy([byte[]]::new(22588), 0, $source, 22588)
                [Runtime.InteropServices.Marshal]::WriteInt32($source, 0x21F8, $lord)
                $owned = [System.Buffers.ArrayPool[byte]]::Shared.Rent(22588)
                [Runtime.InteropServices.Marshal]::Copy($source, $owned, 0, 22588)
                [Runtime.InteropServices.Marshal]::WriteInt32($source, 0x21F8, 99999)
            }
            finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($source) }
            $record = New-ObjectOf 'TraceRecord'
            Set-Field $record 'Key' 'players/1/0'
            Set-Field $record 'Type' $playerType
            Set-Field $record 'Data' $owned
            Set-Field $record 'DataLength' 22588
            [void]$category.GetType().GetField('Records', $flags).GetValue($category).Add($record)
        }
        [void]$snapshot.GetType().GetField('Categories', $flags).GetValue($snapshot).Add($category)
    }
    return $snapshot
}
function Add-UnkeyedProjectile($snapshot, [bool]$present) {
    $category = New-ObjectOf 'TraceCategory'
    Set-Field $category 'Name' 'projectiles'
    if ($present) {
        $record = New-ObjectOf 'TraceRecord'
        Set-Field $record 'Key' 'projectiles/2/0'
        Set-Field $record 'Text' 'r_ProjectileType=40,'
        [void]$category.GetType().GetField('Records', $flags).GetValue($category).Add($record)
    }
    [void]$snapshot.GetType().GetField('Categories', $flags).GetValue($snapshot).Add($category)
    return $snapshot
}
function Wait-Complete([string]$base) {
    for ($attempt = 0; $attempt -lt 200; $attempt++) {
        try {
            $result = [string]$comparer.GetMethod('Compare').Invoke($null, @("$base-*.sdd", "$base-*.sdd"))
            if ($result.Contains('commonSnapshots=')) { return $result }
        }
        catch { }
        Start-Sleep -Milliseconds 100
    }
    throw "No durable completion for $base"
}

$first = Join-Path $folder 'first'
Enqueue (New-Work 'Start' $first $null $null $null)
Enqueue (New-Work 'Line' $first $null ('E' + [char]9 + '508' + [char]9 + 'surrender-confirmed') $null)
Enqueue (New-Work 'Capture' $first (New-Snapshot 508 2 12345 'before-kill') $null $null)
Enqueue (New-Work 'Finish' $first $null $null 'first-resync-ended')
$same = Wait-Complete $first
if ($same -notmatch 'commonSnapshots=1') { throw 'Single capture or event ordering failed.' }
$events = @($comparer.GetMethod('Events').Invoke($null, @("$first-*.sdd")))
if ($events.Count -ne 1 -or $events[0] -notmatch 'surrender-confirmed') {
    throw 'Captured event was not readable after completion.'
}

$second = Join-Path $folder 'second'
Enqueue (New-Work 'Start' $second $null $null $null)
Enqueue (New-Work 'Capture' $second (New-Snapshot 508 2 23456 'before-kill') $null $null)
Enqueue (New-Work 'Finish' $second $null $null 'map-end')
[void](Wait-Complete $second)
$difference = [string]$comparer.GetMethod('Compare').Invoke($null, @("$first-*.sdd", "$second-*.sdd"))
if ($difference -notmatch 'FIRST_DIFFERENCE.*category=players.*field=r_LordUnitId host=12345 client=23456') {
    throw "Large-record field comparison failed: $difference"
}

$unkeyedHost = Join-Path $folder 'unkeyed-host'
$unkeyedClient = Join-Path $folder 'unkeyed-client'
Enqueue (New-Work 'Start' $unkeyedHost $null $null $null)
Enqueue (New-Work 'Capture' $unkeyedHost (Add-UnkeyedProjectile (New-Snapshot 633 1 12345 'pre-native-tick') $false) $null $null)
Enqueue (New-Work 'Finish' $unkeyedHost $null $null 'first-resync-ended')
Enqueue (New-Work 'Start' $unkeyedClient $null $null $null)
Enqueue (New-Work 'Capture' $unkeyedClient (Add-UnkeyedProjectile (New-Snapshot 633 1 12345 'pre-native-tick') $true) $null $null)
Enqueue (New-Work 'Finish' $unkeyedClient $null $null 'first-resync-ended')
[void](Wait-Complete $unkeyedHost)
[void](Wait-Complete $unkeyedClient)
$uncertain = [string]$comparer.GetMethod('Compare').Invoke($null, @("$unkeyedHost-*.sdd", "$unkeyedClient-*.sdd"))
if ($uncertain -notmatch '^ONLY_UNKEYED_PROJECTILE_DIFFERENCE') {
    throw "Unkeyed projectile classification failed: $uncertain"
}

Set-Field $recorder 'QueueLimitBytes' ([long]1)
Set-Field $recorder 'SegmentLimitBytes' ([long]256)
Set-Field $recorder 'ArtificialWriteDelayMilliseconds' 120
$slow = Join-Path $folder 'slow'
Enqueue (New-Work 'Start' $slow $null $null $null)
$watch = [Diagnostics.Stopwatch]::StartNew()
for ($n = 0; $n -lt 4; $n++) {
    Enqueue (New-Work 'Capture' $slow (New-Snapshot (700+$n) (20+$n) (300+$n) 'pre-native-tick') $null $null)
}
$watch.Stop()
Enqueue (New-Work 'Finish' $slow $null $null 'first-resync-ended')
$slowResult = Wait-Complete $slow
if ($watch.ElapsedMilliseconds -lt 120 -or $slowResult -notmatch 'commonSnapshots=4' -or
    @(Get-ChildItem -LiteralPath $folder -Filter 'slow-*.sdd').Count -lt 2) {
    throw 'Backpressure dropped a capture or segment rotation failed.'
}
Set-Field $recorder 'ArtificialWriteDelayMilliseconds' 0
Set-Field $recorder 'QueueLimitBytes' ([long](64MB))
Set-Field $recorder 'SegmentLimitBytes' ([long](128MB))

$probe = Join-Path $folder 'probe'
Enqueue (New-Work 'Start' $probe $null $null $null $true)
Enqueue (New-Work 'Capture' $probe (New-Snapshot 2 1 1 'probe' $true) $null $null)
Enqueue (New-Work 'Finish' $probe $null $null 'probe')
[void](Wait-Complete $probe)
if (-not $comparer.GetMethod('ValidateProbe').Invoke($null, @("$probe-*.sdd"))) {
    throw 'Written probe did not validate after readback.'
}

$open = Join-Path $folder 'unfinished'
Enqueue (New-Work 'Start' $open $null $null $null)
Enqueue (New-Work 'Capture' $open (New-Snapshot 900 1 1 'pre-native-tick') $null $null)
Start-Sleep -Milliseconds 1600
try {
    [void]$comparer.GetMethod('Compare').Invoke($null, @("$open-*.sdd", "$first-*.sdd"))
    throw 'Comparator accepted a trace without completion marker.'
}
catch {
    if ($_.Exception.ToString() -notmatch 'incomplete or missing completion marker') { throw }
}
Enqueue (New-Work 'Finish' $open $null $null 'map-end')
[void](Wait-Complete $open)

$gap = Join-Path $folder 'gap'
Enqueue (New-Work 'Start' $gap $null $null $null)
Enqueue (New-Work 'Line' $gap $null ('I' + [char]9 + '901' + [char]9 + 'RESYNC_CAPTURE') $null)
Enqueue (New-Work 'Finish' $gap $null $null 'first-resync-ended')
$next = Join-Path $folder 'next'
Enqueue (New-Work 'Start' $next $null $null $null)
Enqueue (New-Work 'Capture' $next (New-Snapshot 1000 1 7 'baseline') $null $null)
Enqueue (New-Work 'Finish' $next $null $null 'map-end')
[void](Wait-Complete $next)
try {
    [void]$comparer.GetMethod('Compare').Invoke($null, @("$gap-*.sdd", "$first-*.sdd"))
    throw 'Comparator accepted an explicit capture gap.'
}
catch {
    if ($_.Exception.ToString() -notmatch 'incomplete or missing completion marker') { throw }
}

$blocked = Join-Path $folder 'blocked-parent'
[IO.File]::WriteAllText($blocked, 'not a directory')
$bad = Join-Path $blocked 'bad'
Enqueue (New-Work 'Start' $bad $null $null $null)
Enqueue (New-Work 'Finish' $bad $null $null 'map-end')
$recovery = Join-Path $folder 'recovery'
Enqueue (New-Work 'Start' $recovery $null $null $null)
Enqueue (New-Work 'Capture' $recovery (New-Snapshot 1010 1 8 'baseline') $null $null)
Enqueue (New-Work 'Finish' $recovery $null $null 'map-end')
[void](Wait-Complete $recovery)
if (Test-Path -LiteralPath "$bad-0001.sdd") { throw 'Writer failure created a misleading complete trace.' }

Write-Output 'Binary recorder immutable snapshot, ordering, repeated captures, segment rotation, backpressure, probe, incomplete/footer, write failure and recovery passed.'
