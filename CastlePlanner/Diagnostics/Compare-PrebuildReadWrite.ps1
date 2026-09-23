param(
    [Parameter(Mandatory = $true)][string]$PrebuildTraceDirectory,
    [Parameter(Mandatory = $true)][string]$CellTraceDirectory,
    [Parameter(Mandatory = $true)][string]$CapturePattern
)

$ErrorActionPreference = 'Stop'

function Read-TraceHeader([string]$path) {
    $values = @{}
    foreach ($line in [System.IO.File]::ReadLines($path)) {
        if (-not $line.StartsWith('# ')) { break }
        $parts = $line.Substring(2).Split(@('='), 2)
        if ($parts.Count -eq 2) { $values[$parts[0]] = $parts[1] }
    }
    return $values
}

function Read-SectionTileIds([string]$path, [string]$sectionTitle, [int]$tileColumn) {
    $tiles = [System.Collections.Generic.HashSet[int]]::new()
    $inside = $false
    foreach ($line in [System.IO.File]::ReadLines($path)) {
        if ($line -eq $sectionTitle) {
            $inside = $true
            continue
        }
        if (-not $inside) { continue }
        if ($line.StartsWith('# ')) { break }
        if ($line -notmatch '^\d+\t') { continue }
        $columns = $line.Split("`t")
        if ($columns.Length -le $tileColumn) {
            throw "Missing tile column in $path"
        }
        [void]$tiles.Add([int]$columns[$tileColumn])
    }
    if (-not $inside) { throw "Missing section '$sectionTitle' in $path" }
    return ,$tiles
}

$buildFiles = @(Get-ChildItem -LiteralPath $PrebuildTraceDirectory -Filter "oracle-prebuild-trace-$CapturePattern.tsv" -File)
$fitFiles = @(Get-ChildItem -LiteralPath $CellTraceDirectory -Filter "oracle-cell-trace-$CapturePattern.tsv" -File)
if ($buildFiles.Count -eq 0 -or $fitFiles.Count -eq 0) {
    throw 'No matching prebuild or cell traces.'
}

$buildByPlayer = @{}
$nativeHash = $null
$mapHash = $null
foreach ($file in $buildFiles) {
    $header = Read-TraceHeader $file.FullName
    $playerId = [int]$header['playerId']
    if ($buildByPlayer.ContainsKey($playerId)) { throw "Duplicate build trace for player $playerId" }
    if ($header['frameSnapshotsComplete'] -ne 'True' -or $header['provenanceComplete'] -ne 'True' -or
        [int]$header['pointerProblemFrames'] -ne 0 -or [int]$header['captureErrorFrames'] -ne 0) {
        throw "Incomplete build trace: $($file.FullName)"
    }
    if ($null -eq $nativeHash) { $nativeHash = $header['nativeDllSha256'] }
    if ($null -eq $mapHash) { $mapHash = $header['mapFileSha256'] }
    if ($header['nativeDllSha256'] -ne $nativeHash -or $header['mapFileSha256'] -ne $mapHash) {
        throw "Mixed native or map hashes: $($file.FullName)"
    }
    $buildByPlayer[$playerId] = Read-SectionTileIds $file.FullName `
        '# synchronous validator-input layer changes per ExecuteBuildStep frame' 4
}

$rows = @(
    foreach ($file in $fitFiles) {
        $header = Read-TraceHeader $file.FullName
        $playerId = [int]$header['playerId']
        if ($header['nativeDllSha256'] -ne $nativeHash -or $header['mapFileSha256'] -ne $mapHash -or
            $header['preBuildSetting'] -ne '1') {
            throw "Mismatched fit trace: $($file.FullName)"
        }
        if ($playerId -le ($buildByPlayer.Keys | Measure-Object -Minimum).Minimum) { continue }
        $priorWrites = [System.Collections.Generic.HashSet[int]]::new()
        foreach ($priorPlayerId in $buildByPlayer.Keys) {
            if ($priorPlayerId -lt $playerId) { $priorWrites.UnionWith($buildByPlayer[$priorPlayerId]) }
        }
        $fitCalls = Read-SectionTileIds $file.FullName `
            '# validator calls captured only inside the filtered fit window' 1
        $fitCalls.IntersectWith($priorWrites)
        [pscustomobject]@{
            PlayerId = $playerId
            CandidateId = [int]$header['candidateId']
            Orientation = [int]$header['orientation']
            IntersectingTileCount = $fitCalls.Count
            TraceFile = $file.Name
        }
    }
)

Write-Output ([pscustomobject]@{
    NativeSha256 = $nativeHash
    MapSha256 = $mapHash
    CompleteBuildSequences = $buildByPlayer.Count
    LaterFitTraces = $rows.Count
    TracesWithTileIntersection = @($rows | Where-Object { $_.IntersectingTileCount -gt 0 }).Count
})
$rows | Sort-Object PlayerId, CandidateId, Orientation
