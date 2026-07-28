[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HostLog,

    [Parameter(Mandatory = $true)]
    [string]$ClientLog,

    [switch]$Comprehensive,

    [switch]$RequireDelayProof,

    [switch]$RequireBatchProof,

    [ValidateRange(0, 10000)]
    [int]$MinimumRequests = 0,

    [ValidateRange(0, 2500)]
    [int]$ExpectedDelayMs = 500,

    [ValidateRange(0, 1000)]
    [int]$DelayToleranceMs = 75
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Comprehensive) {
    $RequireDelayProof = $true
    $RequireBatchProof = $true
    if ($MinimumRequests -eq 0) {
        $MinimumRequests = 10
    }
}
elseif ($MinimumRequests -eq 0) {
    $MinimumRequests = 1
}

function Get-ProbeSessionLines {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Log file not found: $Path"
    }

    $lines = @(Get-Content -LiteralPath $Path)
    $sessionStart = -1
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if ($lines[$index] -like '*MPTest ChoreProbe: event=initialized*') {
            $sessionStart = $index
        }
    }

    if ($sessionStart -lt 0) {
        throw "No successful ChoreProbe initialization was found in: $Path"
    }

    return @($lines[$sessionStart..($lines.Count - 1)])
}

function Get-LatestExecutingMap {
    param([object[]]$SessionLines)

    $starts = [System.Collections.Generic.List[int]]::new()
    $starts.Add(0)
    for ($index = 0; $index -lt $SessionLines.Count; $index++) {
        if ($SessionLines[$index] -like '*MPTest ChoreProbe: event=map-state-cleared*' -and
            $index + 1 -lt $SessionLines.Count) {
            $starts.Add($index + 1)
        }
    }

    $selectedStart = $starts[$starts.Count - 1]
    $selectedEnd = $SessionLines.Count - 1
    for ($segmentIndex = 0; $segmentIndex -lt $starts.Count; $segmentIndex++) {
        $start = $starts[$segmentIndex]
        $end = if ($segmentIndex + 1 -lt $starts.Count) {
            $starts[$segmentIndex + 1] - 2
        }
        else {
            $SessionLines.Count - 1
        }

        if ($end -lt $start) {
            continue
        }

        $hasExecute = $false
        for ($lineIndex = $start; $lineIndex -le $end; $lineIndex++) {
            if ($SessionLines[$lineIndex] -like '*MPTest ChoreProbe: event=execute *') {
                $hasExecute = $true
                break
            }
        }

        if ($hasExecute) {
            $selectedStart = $start
            $selectedEnd = $end
        }
    }

    return [pscustomobject]@{
        Lines = @($SessionLines[$selectedStart..$selectedEnd])
        Start = $selectedStart
        End = $selectedEnd
    }
}

function ConvertTo-ProbeEvents {
    param([object[]]$Lines)

    $events = [System.Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $Lines.Count; $index++) {
        $line = [string]$Lines[$index]
        if ($line -notmatch 'MPTest ChoreProbe:\s+(?<data>.*)$') {
            continue
        }

        $fields = @{}
        foreach ($match in [regex]::Matches($Matches['data'], '(?<key>[A-Za-z][A-Za-z0-9]*)=(?<value>\S+)')) {
            $fields[$match.Groups['key'].Value] = $match.Groups['value'].Value
        }

        if (-not $fields.ContainsKey('event')) {
            continue
        }

        $events.Add([pscustomobject]@{
            Line = $line
            Index = $index
            Fields = $fields
            Event = $fields['event']
        })
    }

    return @($events)
}

function Get-Field {
    param(
        [object]$Event,
        [string]$Name,
        [string]$Default = ''
    )

    if ($null -ne $Event -and $Event.Fields.ContainsKey($Name)) {
        return $Event.Fields[$Name]
    }

    return $Default
}

function Get-IntegerField {
    param(
        [object]$Event,
        [string]$Name,
        [int]$Default = [int]::MinValue
    )

    $result = 0
    if ($null -ne $Event -and
        $Event.Fields.ContainsKey($Name) -and
        [int]::TryParse($Event.Fields[$Name], [ref]$result)) {
        return $result
    }

    return $Default
}

function Get-RequestKey {
    param([object]$Event)
    return "$(Get-Field $Event 'source'):$(Get-Field $Event 'request')"
}

function Get-EventsByName {
    param(
        [object[]]$Events,
        [string]$Name
    )

    return @($Events | Where-Object { $_.Event -eq $Name })
}

function Test-IdListContains {
    param(
        [object]$Event,
        [string]$Field,
        [string]$CommandId
    )

    $value = Get-Field $Event $Field 'none'
    return $value -ne 'none' -and @($value -split ',') -contains $CommandId
}

function Get-MatchingBarriers {
    param(
        [object[]]$Events,
        [string]$Direction,
        [string]$CommandId
    )

    return @(
        $Events |
            Where-Object {
                $_.Event -eq 'barrier' -and
                (Get-Field $_ 'direction') -eq $Direction -and
                (Test-IdListContains $_ 'matched' $CommandId)
            }
    )
}

function Assert-ExecuteSequence {
    param(
        [string]$Peer,
        [object[]]$ExecuteEvents,
        [System.Collections.Generic.List[string]]$Failures
    )

    $ordered = @($ExecuteEvents | Sort-Object Index)
    for ($index = 0; $index -lt $ordered.Count; $index++) {
        $actual = Get-IntegerField $ordered[$index] 'executeSequence'
        $expected = $index + 1
        if ($actual -ne $expected) {
            $Failures.Add(
                "${Peer}: executeSequence is not contiguous at log event $expected (found $actual)")
        }
    }
}

$hostSessionLines = Get-ProbeSessionLines -Path $HostLog
$clientSessionLines = Get-ProbeSessionLines -Path $ClientLog
$hostSessionEvents = @(ConvertTo-ProbeEvents $hostSessionLines)
$clientSessionEvents = @(ConvertTo-ProbeEvents $clientSessionLines)
$hostMap = Get-LatestExecutingMap $hostSessionLines
$clientMap = Get-LatestExecutingMap $clientSessionLines
$hostLines = @($hostMap.Lines)
$clientLines = @($clientMap.Lines)
$hostEvents = @(ConvertTo-ProbeEvents $hostLines)
$clientEvents = @(ConvertTo-ProbeEvents $clientLines)

$failures = [System.Collections.Generic.List[string]]::new()
$dangerPattern = 'SyncEvent\s*-\s*Forced run'
$legacyMutationPattern =
    'Spawned swordsman|CreateUnitLocal|scheduled synchronized spawn|received spawn|custom spawn packet'
$probeFailureEvents = @(
    'disabled',
    'handler-failed',
    'enqueue-failed',
    'edge-inspection-failed',
    'delay-flush-failed',
    'delay-bypassed',
    'malformed-buffer',
    'malformed-chore',
    'correlation-conflict',
    'duplicate-execute',
    'execute-tick-mismatch',
    'invalid-execute',
    'batch-failed',
    'resync-start',
    'resync-end'
)

foreach ($side in @(
    [pscustomobject]@{ Name = 'host'; Lines = $hostLines; Events = $hostEvents },
    [pscustomobject]@{ Name = 'client'; Lines = $clientLines; Events = $clientEvents }
)) {
    if (@($side.Lines | Select-String -Pattern $dangerPattern -CaseSensitive:$false).Count -gt 0) {
        $failures.Add("$($side.Name): native SyncEvent forced-run text found in selected map")
    }
    if (@($side.Lines | Select-String -Pattern $legacyMutationPattern -CaseSensitive:$false).Count -gt 0) {
        $failures.Add("$($side.Name): legacy spawn or state-mutation text found in selected map")
    }

    foreach ($failureEvent in $probeFailureEvents) {
        if (@($side.Events | Where-Object { $_.Event -eq $failureEvent }).Count -gt 0) {
            $failures.Add("$($side.Name): probe failure event found: $failureEvent")
        }
    }

    $invalidMode2 = @(
        $side.Events |
            Where-Object {
                $_.Event -eq 'handler' -and
                (Get-Field $_ 'mode') -eq '2' -and
                (Get-Field $_ 'valid') -ne 'true'
            }
    )
    if ($invalidMode2.Count -gt 0) {
        $failures.Add("$($side.Name): $($invalidMode2.Count) mode-2 handler event(s) lacked slot correlation")
    }
}

if ($Comprehensive) {
    foreach ($side in @(
        [pscustomobject]@{ Name = 'host'; Events = $hostSessionEvents },
        [pscustomobject]@{ Name = 'client'; Events = $clientSessionEvents }
    )) {
        $initialized = @(Get-EventsByName $side.Events 'initialized' | Select-Object -Last 1)
        if ($initialized.Count -ne 1) {
            $failures.Add("$($side.Name): comprehensive test could not find exactly one active initialization")
            continue
        }

        $configuredDelay = Get-IntegerField $initialized[0] 'delayMs'
        if ($configuredDelay -ne $ExpectedDelayMs) {
            $failures.Add(
                "$($side.Name): configured delay is $configuredDelay ms, expected $ExpectedDelayMs ms")
        }
    }
}

$hostExecute = @(Get-EventsByName $hostEvents 'execute')
$clientExecute = @(Get-EventsByName $clientEvents 'execute')
Assert-ExecuteSequence 'host' $hostExecute $failures
Assert-ExecuteSequence 'client' $clientExecute $failures

$allRequestKeys = @(
    @(
        @($hostExecute | ForEach-Object { Get-RequestKey $_ }) +
        @($clientExecute | ForEach-Object { Get-RequestKey $_ })
    ) | Sort-Object -Unique
)

if ($allRequestKeys.Count -lt $MinimumRequests) {
    $failures.Add(
        "expected at least $MinimumRequests request(s), found $($allRequestKeys.Count)")
}

$allEvents = @($hostEvents) + @($clientEvents)
$rows = [System.Collections.Generic.List[object]]::new()
$seenCommandIds = @{}
$delayedRequestCount = 0
foreach ($requestKey in $allRequestKeys) {
    $hostMatches = @($hostExecute | Where-Object { (Get-RequestKey $_) -eq $requestKey })
    $clientMatches = @($clientExecute | Where-Object { (Get-RequestKey $_) -eq $requestKey })

    if ($hostMatches.Count -ne 1) {
        $failures.Add("${requestKey}: expected one host execute, found $($hostMatches.Count)")
        continue
    }
    if ($clientMatches.Count -ne 1) {
        $failures.Add("${requestKey}: expected one client execute, found $($clientMatches.Count)")
        continue
    }

    $hostEvent = $hostMatches[0]
    $clientEvent = $clientMatches[0]
    $hostCommandId = Get-Field $hostEvent 'commandId'
    $clientCommandId = Get-Field $clientEvent 'commandId'
    $hostScheduledTick = Get-IntegerField $hostEvent 'scheduledTick'
    $clientScheduledTick = Get-IntegerField $clientEvent 'scheduledTick'
    $hostActualTick = Get-IntegerField $hostEvent 'actualTick'
    $clientActualTick = Get-IntegerField $clientEvent 'actualTick'

    if ((Get-Field $hostEvent 'valid') -ne 'true' -or
        (Get-Field $clientEvent 'valid') -ne 'true') {
        $failures.Add("${requestKey}: invalid execute payload or slot correlation")
    }
    if ((Get-Field $hostEvent 'mutation') -ne 'none' -or
        (Get-Field $clientEvent 'mutation') -ne 'none') {
        $failures.Add("${requestKey}: execute event did not report mutation=none")
    }
    if ($hostCommandId -eq '0' -or $hostCommandId -ne $clientCommandId) {
        $failures.Add("${requestKey}: command ID mismatch ($hostCommandId vs $clientCommandId)")
    }
    if ($seenCommandIds.ContainsKey($hostCommandId) -and
        $seenCommandIds[$hostCommandId] -ne $requestKey) {
        $failures.Add(
            "${requestKey}: command ID $hostCommandId is also assigned to $($seenCommandIds[$hostCommandId])")
    }
    else {
        $seenCommandIds[$hostCommandId] = $requestKey
    }
    if ($hostScheduledTick -ne $clientScheduledTick) {
        $failures.Add(
            "${requestKey}: scheduled tick mismatch ($hostScheduledTick vs $clientScheduledTick)")
    }
    if ($hostActualTick -ne $clientActualTick) {
        $failures.Add(
            "${requestKey}: execute tick mismatch ($hostActualTick vs $clientActualTick)")
    }
    if ($hostScheduledTick -ne $hostActualTick -or
        $clientScheduledTick -ne $clientActualTick) {
        $failures.Add(
            "${requestKey}: execution did not occur on the final scheduled tick")
    }

    $enqueues = @(
        $allEvents |
            Where-Object { $_.Event -eq 'enqueue' -and (Get-RequestKey $_) -eq $requestKey }
    )
    if ($enqueues.Count -ne 1) {
        $failures.Add("${requestKey}: expected one originating enqueue, found $($enqueues.Count)")
    }

    $mode1 = @(
        $allEvents |
            Where-Object {
                $_.Event -eq 'handler' -and
                (Get-Field $_ 'mode') -eq '1' -and
                (Get-RequestKey $_) -eq $requestKey
            }
    )
    if ($mode1.Count -ne 1) {
        $failures.Add("${requestKey}: expected one mode-1 serialization, found $($mode1.Count)")
    }
    elseif ((Get-Field $mode1[0] 'commandId') -ne $hostCommandId) {
        $failures.Add("${requestKey}: mode-1 command ID differs from execute command ID")
    }

    $mode2 = @(
        $allEvents |
            Where-Object {
                $_.Event -eq 'handler' -and
                (Get-Field $_ 'mode') -eq '2' -and
                (Get-RequestKey $_) -eq $requestKey
            }
    )
    if ($mode2.Count -ne 1) {
        $failures.Add("${requestKey}: expected one remote mode-2 size query, found $($mode2.Count)")
    }

    $edges = @(
        $allEvents |
            Where-Object { $_.Event -eq 'edge' -and (Get-RequestKey $_) -eq $requestKey }
    )
    $outgoingEdges = @($edges | Where-Object { (Get-Field $_ 'direction') -eq 'outgoing' })
    $incomingEdges = @($edges | Where-Object { (Get-Field $_ 'direction') -eq 'incoming' })
    if ($outgoingEdges.Count -ne 1 -or $incomingEdges.Count -ne 1) {
        $failures.Add(
            "${requestKey}: expected one outgoing and one incoming edge, found $($outgoingEdges.Count)/$($incomingEdges.Count)")
    }
    foreach ($edge in $edges) {
        if ((Get-Field $edge 'valid') -ne 'true' -or
            (Get-Field $edge 'commandId') -ne $hostCommandId) {
            $failures.Add("${requestKey}: invalid or mismatched edge observation")
        }
    }

    $hostBarriers = @(Get-MatchingBarriers $hostEvents 'outgoing' $hostCommandId)
    $clientBarriers = @(Get-MatchingBarriers $clientEvents 'incoming' $hostCommandId)
    if ($hostBarriers.Count -ne 1) {
        $failures.Add(
            "${requestKey}: expected one outgoing host barrier, found $($hostBarriers.Count)")
    }
    if ($clientBarriers.Count -ne 1) {
        $failures.Add(
            "${requestKey}: expected one incoming client barrier, found $($clientBarriers.Count)")
    }
    foreach ($barrier in @($hostBarriers) + @($clientBarriers)) {
        if ((Get-IntegerField $barrier 'targetTick') -ne $hostScheduledTick) {
            $failures.Add(
                "${requestKey}: barrier target tick differs from final scheduled tick")
        }
    }

    $delayElapsed = '-'
    $delayMaxTick = '-'
    $delayWaitCalls = '-'
    if ($RequireDelayProof) {
        $delayExpected = @(
            $clientEvents |
                Where-Object {
                    $_.Event -eq 'edge' -and
                    (Get-Field $_ 'direction') -eq 'incoming' -and
                    (Get-RequestKey $_) -eq $requestKey
                }
        ).Count -eq 1
        $held = @(
            $clientEvents |
                Where-Object { $_.Event -eq 'delay-held' -and (Get-RequestKey $_) -eq $requestKey }
        )
        $observed = @(
            $clientEvents |
                Where-Object {
                    $_.Event -eq 'delay-barrier-observed' -and
                    (Get-RequestKey $_) -eq $requestKey
                }
        )
        $released = @(
            $clientEvents |
                Where-Object { $_.Event -eq 'delay-released' -and (Get-RequestKey $_) -eq $requestKey }
        )
        $injected = @(
            $clientEvents |
                Where-Object { $_.Event -eq 'delay-injected' -and (Get-RequestKey $_) -eq $requestKey }
        )

        if ($delayExpected) {
            $delayedRequestCount++
            foreach ($delayEventSet in @(
                [pscustomobject]@{ Name = 'delay-held'; Events = $held },
                [pscustomobject]@{ Name = 'delay-barrier-observed'; Events = $observed },
                [pscustomobject]@{ Name = 'delay-released'; Events = $released },
                [pscustomobject]@{ Name = 'delay-injected'; Events = $injected }
            )) {
                if ($delayEventSet.Events.Count -ne 1) {
                    $failures.Add(
                        "${requestKey}: expected one $($delayEventSet.Name), found $($delayEventSet.Events.Count)")
                }
            }
        }
        else {
            foreach ($delayEventSet in @(
                [pscustomobject]@{ Name = 'delay-held'; Events = $held },
                [pscustomobject]@{ Name = 'delay-barrier-observed'; Events = $observed },
                [pscustomobject]@{ Name = 'delay-released'; Events = $released },
                [pscustomobject]@{ Name = 'delay-injected'; Events = $injected }
            )) {
                if ($delayEventSet.Events.Count -ne 0) {
                    $failures.Add(
                        "${requestKey}: local client command unexpectedly logged $($delayEventSet.Name)")
                }
            }
        }

        if ($delayExpected -and
            $held.Count -eq 1 -and
            $observed.Count -eq 1 -and
            $released.Count -eq 1 -and
            $injected.Count -eq 1) {
            $delayElapsed = Get-IntegerField $released[0] 'elapsedMs'
            $delayMaxTick = Get-IntegerField $released[0] 'maxObservedTick'
            $delayWaitCalls = Get-IntegerField $released[0] 'barrierWaitRunCalls'
            $releaseTick = Get-IntegerField $released[0] 'releaseTick'
            $barrierTarget = Get-IntegerField $released[0] 'barrierTargetTick'
            $minimumElapsed = [Math]::Max(0, $ExpectedDelayMs - $DelayToleranceMs)

            foreach ($delayEvent in @($held[0], $observed[0], $released[0], $injected[0])) {
                if ((Get-Field $delayEvent 'commandId') -ne $hostCommandId) {
                    $failures.Add("${requestKey}: delayed-event command ID mismatch")
                }
            }
            if ((Get-IntegerField $held[0] 'delayMs') -ne $ExpectedDelayMs) {
                $failures.Add("${requestKey}: held delay did not equal $ExpectedDelayMs ms")
            }
            if ($delayElapsed -lt $minimumElapsed) {
                $failures.Add(
                    "${requestKey}: held for only $delayElapsed ms; expected at least $minimumElapsed ms")
            }
            if ((Get-Field $released[0] 'barrierObserved') -ne 'true' -or
                $barrierTarget -ne $hostScheduledTick) {
                $failures.Add("${requestKey}: release lacked the matching native barrier")
            }
            if ((Get-Field $released[0] 'crossedBarrier') -ne 'false' -or
                $delayMaxTick -gt $barrierTarget -or
                $releaseTick -gt $barrierTarget) {
                $failures.Add(
                    "${requestKey}: client simulation crossed barrier $barrierTarget before injection")
            }
            if ($delayWaitCalls -lt 1 -or
                (Get-IntegerField $released[0] 'repeatedTickRuns') -lt 1) {
                $failures.Add("${requestKey}: no repeated run calls while held at the barrier")
            }
            if (-not (
                $held[0].Index -lt $observed[0].Index -and
                $observed[0].Index -lt $released[0].Index -and
                $released[0].Index -lt $injected[0].Index -and
                $injected[0].Index -lt $clientEvent.Index)) {
                $failures.Add(
                    "${requestKey}: delay event order was not held -> barrier -> release -> injection -> execute")
            }
        }
    }

    $rows.Add([pscustomobject]@{
        Request = $requestKey
        CommandId = $hostCommandId
        Tick = $hostActualTick
        HostBarrier = $hostBarriers.Count -eq 1
        ClientBarrier = $clientBarriers.Count -eq 1
        DelayMs = $delayElapsed
        MaxHeldTick = $delayMaxTick
        BarrierWaitCalls = $delayWaitCalls
    })
}

if ($RequireDelayProof -and $delayedRequestCount -eq 0) {
    $failures.Add(
        'delay proof requested, but the selected map contained no probe incoming to the client')
}

if ($RequireBatchProof) {
    $batchCompleted = @(Get-EventsByName $allEvents 'batch-complete')
    if ($batchCompleted.Count -lt 2) {
        $failures.Add(
            "batch proof requested, but only $($batchCompleted.Count) completed batch(es) were logged")
    }

    $multiCommandBarriers = @(
        $hostEvents |
            Where-Object {
                $_.Event -eq 'barrier' -and
                (Get-Field $_ 'direction') -eq 'outgoing' -and
                (Get-Field $_ 'matched' 'none') -ne 'none' -and
                @((Get-Field $_ 'matched') -split ',').Count -ge 2
            }
    )
    if ($multiCommandBarriers.Count -eq 0) {
        $failures.Add(
            'batch proof requested, but no outgoing host SyncEvent contained multiple probe command IDs')
    }
}

if ($rows.Count -gt 0) {
    $rows | Sort-Object Request | Format-Table -AutoSize
}

Write-Host ''
Write-Host "Selected map segments: host lines $($hostMap.Start)-$($hostMap.End), client lines $($clientMap.Start)-$($clientMap.End)."

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'Chore probe comparison FAILED:' -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host " - $failure" -ForegroundColor Red
    }
    exit 1
}

Write-Host ''
Write-Host "Chore probe comparison PASSED for $($rows.Count) request(s)." -ForegroundColor Green
if ($RequireDelayProof) {
    Write-Host "Native barrier waiting was proven with a configured $ExpectedDelayMs ms client delay." -ForegroundColor Green
}
if ($RequireBatchProof) {
    Write-Host 'At least one host SyncEvent carried multiple probe command IDs.' -ForegroundColor Green
}
exit 0
