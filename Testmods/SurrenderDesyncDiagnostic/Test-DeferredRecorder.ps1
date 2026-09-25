[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$gameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$assemblyPath = Join-Path $PSScriptRoot 'BepInEx\plugins\SurrenderDesyncDiagnostic_Serp\SurrenderDesyncDiagnostic.dll'
foreach ($dependency in @(
    (Join-Path $gameDir 'BepInEx\core\BepInEx.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\000shcdese\System.Memory.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\000shcdese\System.Buffers.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\000shcdese\SHCDESE.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\APIShared_Serp\APIShared.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\BugfixesAndQoL_Serp\BugfixesAndQoL.dll'))) {
    [void][Reflection.Assembly]::LoadFrom($dependency)
}
$assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
$flags = [Reflection.BindingFlags]'Instance,NonPublic,Public'

function New-DiagnosticObject([string]$name) {
    $type = $assembly.GetType("SurrenderDesyncDiagnostic.$name", $true)
    return [Activator]::CreateInstance($type, $true)
}
function Set-DiagnosticField($target, [string]$name, $value) {
    $field = $target.GetType().GetField($name, $flags)
    if ($null -eq $field) { throw "Missing field: $name" }
    $field.SetValue($target, $value)
}
function New-Work([string]$kind) {
    $work = New-DiagnosticObject 'TraceWork'
    $enum = $assembly.GetType('SurrenderDesyncDiagnostic.TraceWorkKind', $true)
    Set-DiagnosticField $work 'Kind' ([Enum]::Parse($enum, $kind))
    return $work
}
function Queue-Work($recorder, $work) {
    $method = $recorder.GetType().GetMethod('Enqueue', $flags)
    return [bool]$method.Invoke($recorder, @($work))
}
function Wait-Completion([string]$base) {
    $file = "$base-0001.tsv"
    for ($attempt = 0; $attempt -lt 200; $attempt++) {
        if (Test-Path -LiteralPath $file) {
            try {
                $lines = @([IO.File]::ReadAllLines($file))
                if (@($lines | Where-Object { $_.StartsWith(('F' + [char]9)) }).Count -gt 0) {
                    return $lines
                }
            }
            catch [IO.IOException] { }
        }
        Start-Sleep -Milliseconds 100
    }
    throw "No completion marker: $file"
}

$loggerType = [Reflection.Assembly]::LoadFrom((Join-Path $gameDir 'BepInEx\core\BepInEx.dll')).GetType('BepInEx.Logging.ManualLogSource', $true)
$logger = [Activator]::CreateInstance($loggerType, @('Surrender deferred test'))
$recorderType = $assembly.GetType('SurrenderDesyncDiagnostic.DeferredTraceRecorder', $true)
$recorder = [Activator]::CreateInstance($recorderType, $flags, $null, @($logger), $null)
$folder = Join-Path ([IO.Path]::GetTempPath()) ('surrender-recorder-test-' + [Guid]::NewGuid().ToString('N'))

$source = [Runtime.InteropServices.Marshal]::AllocHGlobal(22588)
try {
    [Runtime.InteropServices.Marshal]::Copy([byte[]]::new(22588), 0, $source, 22588)
    [Runtime.InteropServices.Marshal]::WriteInt32($source, 0x21F8, 12345)
    $owned = [System.Buffers.ArrayPool[byte]]::Shared.Rent(22588)
    [Runtime.InteropServices.Marshal]::Copy($source, $owned, 0, 22588)
    [Runtime.InteropServices.Marshal]::WriteInt32($source, 0x21F8, 99999)
}
finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($source) }

$snapshot = New-DiagnosticObject 'TraceSnapshot'
Set-DiagnosticField $snapshot 'MapTick' 508
Set-DiagnosticField $snapshot 'DirectorTick' 545
Set-DiagnosticField $snapshot 'Order' ([long]2)
Set-DiagnosticField $snapshot 'Phase' 'before-kill'
Set-DiagnosticField $snapshot 'LocalState' 'localPlayer=1,spectator=0'
$category = New-DiagnosticObject 'TraceCategory'
Set-DiagnosticField $category 'Name' 'players'
$record = New-DiagnosticObject 'TraceRecord'
Set-DiagnosticField $record 'Key' 'players/1/0'
Set-DiagnosticField $record 'Type' ([Reflection.Assembly]::LoadFrom((Join-Path $gameDir 'BepInEx\plugins\000shcdese\SHCDESE.dll')).GetType('SHCDESE.Interop.GamePlayerResources', $true))
Set-DiagnosticField $record 'Data' $owned
[void]$category.GetType().GetField('Records', $flags).GetValue($category).Add($record)
[void]$snapshot.GetType().GetField('Categories', $flags).GetValue($snapshot).Add($category)

$queued = $recorderType.GetField('queuedBytes', $flags)
$queued.SetValue($recorder, [long](512MB - 1))
$overflow = New-Work 'Capture'
Set-DiagnosticField $overflow 'Snapshot' $snapshot
if (Queue-Work $recorder $overflow) { throw 'Queue limit did not reject an oversized pending snapshot.' }
$queued.SetValue($recorder, [long]0)

$base = Join-Path $folder 'trace-first'
$start = New-Work 'Start'; Set-DiagnosticField $start 'TraceBase' $base
$line = New-Work 'Line'; Set-DiagnosticField $line 'Text' "E`t508`t545`t1`tsurrender-confirmed`tplayer=1"
$finish = New-Work 'Finish'; Set-DiagnosticField $finish 'Reason' 'first-resync-ended'
foreach ($work in @($start, $line, $overflow, $finish)) {
    if (-not (Queue-Work $recorder $work)) { throw 'Expected work item rejected.' }
}
$lines = @(Wait-Completion $base)
$events = @($lines | Where-Object { $_.StartsWith(('E' + [char]9)) })
$changes = @($lines | Where-Object { $_.StartsWith(('C' + [char]9)) })
$hashes = @($lines | Where-Object { $_.StartsWith(('S' + [char]9)) })
$footer = @($lines | Where-Object { $_.StartsWith(('F' + [char]9)) })
if ($events.Count -ne 1 -or $changes.Count -lt 1 -or $hashes.Count -ne 1 -or
    $footer.Count -ne 1 -or -not $footer[0].Contains('status=complete') -or
    -not ($changes[0].Contains('r_LordUnitId=12345,')) -or
    $changes[0].Contains('r_LordUnitId=99999,')) {
    throw 'Immutable capture, ordering, hash or completion failed.'
}
if ([Array]::IndexOf($lines, $events[0]) -ge [Array]::IndexOf($lines, $changes[0]) -or
    [Array]::IndexOf($lines, $changes[0]) -ge [Array]::IndexOf($lines, $hashes[0]) -or
    [Array]::IndexOf($lines, $hashes[0]) -ge [Array]::IndexOf($lines, $footer[0])) {
    throw 'Trace work order changed.'
}

$second = Join-Path $folder 'trace-second'
$start2 = New-Work 'Start'; Set-DiagnosticField $start2 'TraceBase' $second
$line2 = New-Work 'Line'; Set-DiagnosticField $line2 'Text' "E`t736`t2022`t3`tsurrender-chore`tplayer=2"
$finish2 = New-Work 'Finish'; Set-DiagnosticField $finish2 'Reason' 'map-end'
foreach ($work in @($start2, $line2, $finish2)) {
    if (-not (Queue-Work $recorder $work)) { throw 'Second trace rejected.' }
}
$secondLines = @(Wait-Completion $second)
if (-not (@($secondLines | Where-Object { $_ -like '*surrender-chore*' }).Count -eq 1) -or
    -not (@($secondLines | Where-Object { $_ -like '*status=complete*' }).Count -eq 1)) {
    throw 'Sequential trace completion failed.'
}

$idleBase = Join-Path $folder 'trace-idle-flush'
$idleStart = New-Work 'Start'; Set-DiagnosticField $idleStart 'TraceBase' $idleBase
$idleLine = New-Work 'Line'; Set-DiagnosticField $idleLine 'Text' "E`t750`t2067`t4`tspectator-after-action`tplayer=1"
foreach ($work in @($idleStart, $idleLine)) {
    if (-not (Queue-Work $recorder $work)) { throw 'Idle-flush trace rejected.' }
}
$idleFile = "$idleBase-0001.tsv"
$flushed = $false
for ($attempt = 0; $attempt -lt 30 -and -not $flushed; $attempt++) {
    Start-Sleep -Milliseconds 100
    if (-not (Test-Path -LiteralPath $idleFile)) { continue }
    $stream = [IO.FileStream]::new($idleFile, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $reader = [IO.StreamReader]::new($stream)
        $flushed = $reader.ReadToEnd().Contains('spectator-after-action')
        $reader.Dispose()
    }
    finally { $stream.Dispose() }
}
if (-not $flushed) { throw 'Idle worker did not flush buffered data.' }
$idleFinish = New-Work 'Finish'; Set-DiagnosticField $idleFinish 'Reason' 'map-end'
if (-not (Queue-Work $recorder $idleFinish)) { throw 'Idle-flush finish rejected.' }
[void](Wait-Completion $idleBase)

$incompleteBase = Join-Path $folder 'trace-incomplete'
$start3 = New-Work 'Start'; Set-DiagnosticField $start3 'TraceBase' $incompleteBase
$gap = New-Work 'Line'; Set-DiagnosticField $gap 'Text' "I`t804`tRESYNC_CAPTURE"
$finish3 = New-Work 'Finish'; Set-DiagnosticField $finish3 'Reason' 'first-resync-ended'
foreach ($work in @($start3, $gap, $finish3)) {
    if (-not (Queue-Work $recorder $work)) { throw 'Incomplete trace rejected.' }
}
$incompleteLines = @(Wait-Completion $incompleteBase)
if (-not (@($incompleteLines | Where-Object { $_ -like '*status=incomplete*' }).Count -eq 1)) {
    throw 'Incomplete capture footer missing.'
}

$blocked = Join-Path $folder 'blocked-parent'
[IO.File]::WriteAllText($blocked, 'not a directory')
$badBase = Join-Path $blocked 'trace-failed'
$badStart = New-Work 'Start'; Set-DiagnosticField $badStart 'TraceBase' $badBase
$badFinish = New-Work 'Finish'; Set-DiagnosticField $badFinish 'Reason' 'map-end'
foreach ($work in @($badStart, $badFinish)) {
    if (-not (Queue-Work $recorder $work)) { throw 'Write failure case rejected.' }
}
$recoveryBase = Join-Path $folder 'trace-recovered'
$recoveryStart = New-Work 'Start'; Set-DiagnosticField $recoveryStart 'TraceBase' $recoveryBase
$recoveryFinish = New-Work 'Finish'; Set-DiagnosticField $recoveryFinish 'Reason' 'map-end'
foreach ($work in @($recoveryStart, $recoveryFinish)) {
    if (-not (Queue-Work $recorder $work)) { throw 'Recovery trace rejected.' }
}
$recoveryLines = @(Wait-Completion $recoveryBase)
if (Test-Path -LiteralPath "$badBase-0001.tsv") { throw 'Failed writer created a misleading trace.' }
if (-not (@($recoveryLines | Where-Object { $_ -like '*status=complete*' }).Count -eq 1)) {
    throw 'Writer did not recover after failed trace.'
}
$compare = Join-Path $PSScriptRoot 'Compare-Traces.ps1'
$same = & $compare -HostTrace "$base-*.tsv" -ClientTrace "$base-*.tsv"
if ($same -notmatch 'NO_CAPTURED_DIFFERENCE') { throw 'Complete trace comparison failed.' }
try {
    & $compare -HostTrace "$incompleteBase-*.tsv" -ClientTrace "$base-*.tsv" > $null
    throw 'Comparator accepted an incomplete trace.'
}
catch {
    if ($_.Exception.Message -eq 'Comparator accepted an incomplete trace.') { throw }
}
$truncated = Join-Path $folder 'trace-no-footer-0001.tsv'
[IO.File]::WriteAllLines($truncated, @($lines | Where-Object { -not $_.StartsWith(('F' + [char]9)) }))
try {
    & $compare -HostTrace $truncated -ClientTrace "$base-*.tsv" > $null
    throw 'Comparator accepted a trace without a footer.'
}
catch {
    if ($_.Exception.Message -eq 'Comparator accepted a trace without a footer.') { throw }
}
Write-Output 'Deferred recorder immutable snapshot, queue limit, ordering, idle flush, repeated traces, incomplete/write failure and comparator gates passed.'
