[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HostLog,

    [Parameter(Mandatory = $true)]
    [string]$ClientLog,

    [switch]$RequireDelayProof
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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

function ConvertTo-ProbeEvent {
    param([string]$Line)

    if ($Line -notmatch 'MPTest ChoreProbe:\s+(?<data>.*)$') {
        return $null
    }

    $fields = @{}
    foreach ($match in [regex]::Matches($Matches['data'], '(?<key>[A-Za-z][A-Za-z0-9]*)=(?<value>\S+)')) {
        $fields[$match.Groups['key'].Value] = $match.Groups['value'].Value
    }

    if (-not $fields.ContainsKey('event')) {
        return $null
    }

    return [pscustomobject]@{
        Line = $Line
        Fields = $fields
        Event = $fields['event']
    }
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

function Test-BarrierContains {
    param(
        [object[]]$BarrierEvents,
        [string]$CommandId
    )

    foreach ($barrier in $BarrierEvents) {
        if ((Get-Field $barrier 'direction') -ne 'outgoing') {
            continue
        }

        $matched = Get-Field $barrier 'matched' 'none'
        if ($matched -eq 'none') {
            continue
        }

        if (@($matched -split ',') -contains $CommandId) {
            return $true
        }
    }

    return $false
}

$hostLines = Get-ProbeSessionLines -Path $HostLog
$clientLines = Get-ProbeSessionLines -Path $ClientLog
$hostEvents = @($hostLines | ForEach-Object { ConvertTo-ProbeEvent $_ } | Where-Object { $null -ne $_ })
$clientEvents = @($clientLines | ForEach-Object { ConvertTo-ProbeEvent $_ } | Where-Object { $null -ne $_ })

$failures = [System.Collections.Generic.List[string]]::new()
$dangerPattern = 'SyncEvent\s*-\s*Forced run'
$probeFailureEvents = @(
    'disabled',
    'handler-failed',
    'enqueue-failed',
    'edge-inspection-failed',
    'delay-flush-failed',
    'malformed-buffer',
    'malformed-chore',
    'correlation-conflict',
    'resync-start'
)

foreach ($side in @(
    [pscustomobject]@{ Name = 'host'; Lines = $hostLines; Events = $hostEvents },
    [pscustomobject]@{ Name = 'client'; Lines = $clientLines; Events = $clientEvents }
)) {
    if (@($side.Lines | Select-String -Pattern $dangerPattern -CaseSensitive:$false).Count -gt 0) {
        $failures.Add("$($side.Name): native SyncEvent forced-run text found after probe initialization")
    }

    foreach ($failureEvent in $probeFailureEvents) {
        if (@($side.Events | Where-Object { $_.Event -eq $failureEvent }).Count -gt 0) {
            $failures.Add("$($side.Name): probe failure event found: $failureEvent")
        }
    }
}

$hostExecute = @(Get-EventsByName $hostEvents 'execute')
$clientExecute = @(Get-EventsByName $clientEvents 'execute')
$allRequestKeys = @(
    @(
        @($hostExecute | ForEach-Object { Get-RequestKey $_ }) +
        @($clientExecute | ForEach-Object { Get-RequestKey $_ })
    ) | Sort-Object -Unique
)

if ($allRequestKeys.Count -eq 0) {
    $failures.Add('no execute events were found')
}

$rows = [System.Collections.Generic.List[object]]::new()
$hostBarriers = @(Get-EventsByName $hostEvents 'barrier')
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
    $hostScheduledTick = Get-Field $hostEvent 'scheduledTick'
    $clientScheduledTick = Get-Field $clientEvent 'scheduledTick'
    $hostActualTick = Get-Field $hostEvent 'actualTick'
    $clientActualTick = Get-Field $clientEvent 'actualTick'
    $barrierMatched = Test-BarrierContains $hostBarriers $hostCommandId

    if ((Get-Field $hostEvent 'valid') -ne 'true' -or (Get-Field $clientEvent 'valid') -ne 'true') {
        $failures.Add("${requestKey}: invalid execute payload or slot correlation")
    }
    if ((Get-Field $hostEvent 'mutation') -ne 'none' -or (Get-Field $clientEvent 'mutation') -ne 'none') {
        $failures.Add("${requestKey}: execute event did not report mutation=none")
    }
    if ($hostCommandId -eq '0' -or $hostCommandId -ne $clientCommandId) {
        $failures.Add("${requestKey}: command ID mismatch ($hostCommandId vs $clientCommandId)")
    }
    if ($hostScheduledTick -ne $clientScheduledTick) {
        $failures.Add("${requestKey}: scheduled tick mismatch ($hostScheduledTick vs $clientScheduledTick)")
    }
    if ($hostActualTick -ne $clientActualTick) {
        $failures.Add("${requestKey}: execute tick mismatch ($hostActualTick vs $clientActualTick)")
    }
    if (-not $barrierMatched) {
        $failures.Add("${requestKey}: command ID $hostCommandId was not matched in an outgoing host SyncEvent")
    }

    $rows.Add([pscustomobject]@{
        Request = $requestKey
        CommandId = $hostCommandId
        ScheduledTick = $hostScheduledTick
        HostExecuteTick = $hostActualTick
        ClientExecuteTick = $clientActualTick
        HostBarrier = $barrierMatched
    })
}

if ($RequireDelayProof) {
    $heldEvents = @(Get-EventsByName $clientEvents 'delay-held')
    $releasedEvents = @(Get-EventsByName $clientEvents 'delay-released')
    if ($heldEvents.Count -eq 0 -or $releasedEvents.Count -eq 0) {
        $failures.Add('delay proof requested, but client delay-held/delay-released events are missing')
    }

    foreach ($held in $heldEvents) {
        $key = Get-RequestKey $held
        $release = @($releasedEvents | Where-Object { (Get-RequestKey $_) -eq $key })
        if ($release.Count -ne 1) {
            $failures.Add("${key}: expected one delayed release, found $($release.Count)")
        }
    }
}

if ($rows.Count -gt 0) {
    $rows | Format-Table -AutoSize
}

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
exit 0
