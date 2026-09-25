[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$HostTrace,
    [Parameter(Mandatory=$true)][string]$ClientTrace
)

$ErrorActionPreference = 'Stop'

function Read-Samples([string]$pattern) {
    $paths = @(Resolve-Path -Path $pattern | ForEach-Object { $_.Path } | Sort-Object)
    if ($paths.Count -eq 0) { throw "No trace segments: $pattern" }
    $samples = @{}
    $occurrences = @{}
    $incomplete = $false
    foreach ($path in $paths) {
        foreach ($line in [IO.File]::ReadLines($path)) {
            if ($line.StartsWith("I`t", [StringComparison]::Ordinal)) { $incomplete = $true }
            if (-not $line.StartsWith("S`t", [StringComparison]::Ordinal)) { continue }
            $cells = $line.Split([char]9)
            if ($cells.Length -lt 7) { throw "Invalid sample in ${path}: $line" }
            $identity = "$($cells[1])|$($cells[4])|$($cells[5])"
            if (-not $occurrences.ContainsKey($identity)) { $occurrences[$identity] = 0 }
            $occurrences[$identity]++
            $key = "$identity|$($occurrences[$identity])"
            $samples[$key] = [pscustomobject]@{
                Tick = [int]$cells[1]; Phase = $cells[4]; Category = $cells[5]
                Hash = $cells[6]; Key = $key
            }
        }
    }
    if ($incomplete) { throw "Trace explicitly reports incomplete capture: $pattern" }
    return $samples
}

function Read-ObjectState([string]$pattern, [int]$tick, [string]$phase,
    [string]$category, [int]$occurrence) {
    $paths = @(Resolve-Path -Path $pattern | ForEach-Object { $_.Path } | Sort-Object)
    $state = @{}
    $seen = 0
    foreach ($path in $paths) {
        foreach ($line in [IO.File]::ReadLines($path)) {
            $cells = $line.Split([char]9)
            if ($cells.Length -lt 2) { continue }
            if ($cells[0] -eq 'C' -and $cells.Length -ge 7 -and
                $cells[5].StartsWith("$category/", [StringComparison]::Ordinal)) {
                if ($cells[6] -eq '<removed>') { $state.Remove($cells[5]) | Out-Null }
                else { $state[$cells[5]] = $cells[6] }
            }
            if ($cells[0] -eq 'S' -and $cells.Length -ge 7 -and
                [int]$cells[1] -eq $tick -and $cells[4] -eq $phase -and
                $cells[5] -eq $category) {
                $seen++
                if ($seen -eq $occurrence) { return $state }
            }
        }
    }
    throw "Sample not found: $tick/$phase/$category/$occurrence"
}

$hostSamples = Read-Samples $HostTrace
$clientSamples = Read-Samples $ClientTrace
$common = @($hostSamples.Keys | Where-Object { $clientSamples.ContainsKey($_) } |
    ForEach-Object { $hostSamples[$_] } |
    Sort-Object Tick,Phase,Category,Key)
if ($common.Count -eq 0) { throw 'No matching map-tick/phase/category samples; check map and mod builds.' }

foreach ($sample in $common) {
    $other = $clientSamples[$sample.Key]
    if ($sample.Hash -eq $other.Hash) { continue }
    $parts = $sample.Key.Split('|')
    $ordinal = [int]$parts[$parts.Length - 1]
    $hostObjects = Read-ObjectState $HostTrace $sample.Tick $sample.Phase $sample.Category $ordinal
    $clientObjects = Read-ObjectState $ClientTrace $sample.Tick $sample.Phase $sample.Category $ordinal
    $objects = @(@($hostObjects.Keys) + @($clientObjects.Keys) | Sort-Object -Unique)
    foreach ($objectId in $objects) {
        $hostValue = if ($hostObjects.ContainsKey($objectId)) { $hostObjects[$objectId] } else { '<absent>' }
        $clientValue = if ($clientObjects.ContainsKey($objectId)) { $clientObjects[$objectId] } else { '<absent>' }
        if ($hostValue -ceq $clientValue) { continue }
        $hostFields = @{}
        $clientFields = @{}
        foreach ($part in $hostValue.Split(',')) {
            $pair = $part.Split('=', 2)
            if ($pair.Length -eq 2) { $hostFields[$pair[0]] = $pair[1] }
        }
        foreach ($part in $clientValue.Split(',')) {
            $pair = $part.Split('=', 2)
            if ($pair.Length -eq 2) { $clientFields[$pair[0]] = $pair[1] }
        }
        foreach ($field in @(@($hostFields.Keys) + @($clientFields.Keys) | Sort-Object -Unique)) {
            $left = if ($hostFields.ContainsKey($field)) { $hostFields[$field] } else { '<absent>' }
            $right = if ($clientFields.ContainsKey($field)) { $clientFields[$field] } else { '<absent>' }
            if ($left -cne $right) {
                Write-Output "FIRST_DIFFERENCE mapTick=$($sample.Tick) phase=$($sample.Phase) category=$($sample.Category) object=$objectId field=$field host=$left client=$right"
                return
            }
        }
        Write-Output "FIRST_DIFFERENCE mapTick=$($sample.Tick) phase=$($sample.Phase) category=$($sample.Category) object=$objectId host=$hostValue client=$clientValue"
        return
    }
    Write-Output "HASH_DIFFERENCE_WITHOUT_CAPTURED_OBJECT mapTick=$($sample.Tick) phase=$($sample.Phase) category=$($sample.Category); inspect trace completeness and capture gaps"
    return
}

Write-Output "NO_CAPTURED_DIFFERENCE commonSamples=$($common.Count); remaining gaps: unknown, nested and array fields, pointers, padding, visual presentation, unsupported native state and events between observation phases."
