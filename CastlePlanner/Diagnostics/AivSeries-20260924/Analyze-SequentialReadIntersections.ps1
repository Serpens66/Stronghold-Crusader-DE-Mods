param(
    [Parameter(Mandatory = $true)][string]$DetectorDirectory,
    [Parameter(Mandatory = $true)][string]$CaptureToken
)

$ErrorActionPreference = 'Stop'

function Read-Header([string[]]$lines) {
    $header = @{}
    foreach ($line in $lines) {
        if ($line -match '^# ([^=]+)=(.*)$') { $header[$Matches[1]] = $Matches[2] }
    }
    return $header
}

function Get-SectionIds([string[]]$lines, [string]$section, [int]$column) {
    $ids = [System.Collections.Generic.HashSet[int]]::new()
    $inside = $false
    foreach ($line in $lines) {
        if ($line -eq $section) { $inside = $true; continue }
        if (-not $inside) { continue }
        if ($line -match '^# ' -or ($line -and $line[0] -notmatch '[0-9]')) { break }
        if (-not $line) { continue }
        $parts = $line.Split([char]9)
        if ($parts.Length -le $column -or -not $ids.Add([int]$parts[$column])) {
            if ($parts.Length -le $column) { throw "Malformed section row: $line" }
        }
    }
    return ,$ids
}

$startByPlayer = @{}
$prebuildByPlayer = @{}
$root = (Resolve-Path -LiteralPath $DetectorDirectory).Path
$startFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'StartTraces') -File |
    Where-Object { $_.Name -like "*$CaptureToken*" })
$prebuildFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'PrebuildTraces') -File |
    Where-Object { $_.Name -like "*$CaptureToken*" })
$cellFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'CellTraces') -File -Filter 'oracle-cell-trace-*.tsv' |
    Where-Object { $_.Name -like "*$CaptureToken*" })
if ($startFiles.Count -eq 0 -or $cellFiles.Count -eq 0) {
    throw 'Start and candidate-cell traces are required.'
}

foreach ($file in $startFiles) {
    $lines = [IO.File]::ReadAllLines($file.FullName)
    $header = Read-Header $lines
    $player = [int]$header['playerId']
    if ($startByPlayer.ContainsKey($player)) { throw "Duplicate start for player $player" }
    $startByPlayer[$player] = Get-SectionIds $lines "x`ty`ttileId`tlayer`tbefore`tafter" 2
}
foreach ($file in $prebuildFiles) {
    $lines = [IO.File]::ReadAllLines($file.FullName)
    $header = Read-Header $lines
    if ($header['frameSnapshotsComplete'] -ne 'True' -or
        $header['provenanceComplete'] -ne 'True' -or
        $header['pointerProblemFrames'] -ne '0' -or
        $header['captureErrorFrames'] -ne '0') {
        throw "Incomplete prebuild trace: $($file.Name)"
    }
    $player = [int]$header['playerId']
    if ($prebuildByPlayer.ContainsKey($player)) { throw "Duplicate prebuild for player $player" }
    $prebuildByPlayer[$player] = Get-SectionIds $lines "captureFrameNumber`tframeIndex`tmapper`tlayer`ttileId`tbefore`tafter" 4
}

$results = @()
foreach ($file in $cellFiles) {
    $lines = [IO.File]::ReadAllLines($file.FullName)
    $header = Read-Header $lines
    $player = [int]$header['playerId']
    $readIds = Get-SectionIds $lines "validatorCallIndex`ttileId`tplayerId`tmapperValue`tmode`tresult`tblocked`tnativeTerrainFlags`tnativeHeight`tnativeDefaultHeight`tnativeOrganismId`tnativeOrganismClass`tnativeBuildingId`tnativeEntityId`tnativeOwnerId`tnativeGameMode" 1
    if ($readIds.Count -ne [int]$header['validatorCalls']) {
        throw "Validator read count differs: $($file.Name)"
    }
    $startHits = 0
    $prebuildHits = 0
    $prior = @()
    foreach ($priorPlayer in @($startByPlayer.Keys | Where-Object { $_ -ge 2 -and $_ -lt $player } | Sort-Object)) {
        $sHits = 0
        foreach ($id in $startByPlayer[$priorPlayer]) {
            if ($readIds.Contains($id)) { $sHits++ }
        }
        $bHits = 0
        if ($prebuildByPlayer.ContainsKey($priorPlayer)) {
            foreach ($id in $prebuildByPlayer[$priorPlayer]) {
                if ($readIds.Contains($id)) { $bHits++ }
            }
        }
        $startHits += $sHits
        $prebuildHits += $bHits
        if ($sHits -or $bHits) {
            $prior += "p${priorPlayer}:start=$sHits,prebuild=$bHits"
        }
    }
    $results += [pscustomobject]@{
        File = $file.Name
        PlayerId = $player
        Rotation = [int]$header['orientation']
        ValidatorReads = $readIds.Count
        PriorStartHits = $startHits
        PriorPrebuildHits = $prebuildHits
        PriorPlayers = $prior
    }
}
$results | Sort-Object PlayerId,Rotation | ConvertTo-Json -Depth 4
