param(
    [string]$ResultDirectory = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
$report = Get-Content -LiteralPath (Join-Path $ResultDirectory 'oracle-report.json') -Raw | ConvertFrom-Json
$windows = @{
    1 = @('221154', '221155')
    2 = @('221209', '221212')
    3 = @('221230', '221231')
    4 = @('221244', '221253')
    5 = @('221305', '221314')
}

function Read-TraceTable {
    param([string]$Path, [string]$Header)
    $lines = [System.IO.File]::ReadAllLines($Path)
    $begin = [array]::FindIndex($lines, [Predicate[string]] { param($line) $line.StartsWith($Header, [StringComparison]::Ordinal) })
    if ($begin -lt 0) { throw "Missing table $Header in $Path" }
    $end = [array]::FindIndex($lines, $begin + 1, [Predicate[string]] { param($line) [string]::IsNullOrWhiteSpace($line) })
    if ($end -lt 0) { $end = $lines.Length }
    if ($end -eq $begin + 1) { return @() }
    return @($lines[$begin..($end - 1)] | ConvertFrom-Csv -Delimiter "`t")
}

$prebuildChanges = @{}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $ResultDirectory 'PrebuildTraces') -File -Filter '*.tsv') {
    if ($file.Name -notmatch '-session(?<session>\d+)-p(?<player>\d+)-') { continue }
    $key = '{0}:{1}' -f [int]$Matches.session, [int]$Matches.player
    $tileIds = [System.Collections.Generic.HashSet[int]]::new()
    foreach ($row in Read-TraceTable $file.FullName "captureFrameNumber`tframeIndex`tmapper`tlayer`ttileId") {
        [void]$tileIds.Add([int]$row.tileId)
    }
    $prebuildChanges[$key] = $tileIds
}

$cellFiles = @(Get-ChildItem -LiteralPath (Join-Path $ResultDirectory 'CellTraces') -File -Filter 'oracle-cell-trace-*.tsv')
$output = @(
    foreach ($case in $report.Cases) {
        $session = [int]($case.SessionId -replace '^map-load-', '')
        $orientation = [int]$case.Rotation / 45
        $suffix = '-p{0}-c0-r{1}-keep{2}-{3}.tsv' -f $case.PlayerId, $orientation, $case.KeepX, $case.KeepY
        $matches = @($cellFiles | Where-Object {
            $_.Name.EndsWith($suffix, [StringComparison]::Ordinal) -and
            $_.Name -match '^oracle-cell-trace-20260924-(?<clock>\d{6})-' -and
            $Matches.clock.Substring(0, 6) -ge ($windows[$session][0] + '000').Substring(0, 6) -and
            $Matches.clock.Substring(0, 6) -le ($windows[$session][1] + '999').Substring(0, 6)
        })
        if ($matches.Count -ne 1) { throw "Expected one cell trace for $($case.Id), got $($matches.Count)" }
        $cellFile = $matches[0]
        $grid = @(Read-TraceTable $cellFile.FullName "gridRow`tgridColumn`tworldX")
        $validator = @(Read-TraceTable $cellFile.FullName "validatorCallIndex`ttileId")
        $priorTiles = [System.Collections.Generic.HashSet[int]]::new()
        for ($player = 2; $player -lt [int]$case.PlayerId; $player++) {
            $key = '{0}:{1}' -f $session, $player
            if ($prebuildChanges.ContainsKey($key)) {
                $priorTiles.UnionWith($prebuildChanges[$key])
            }
        }
        $intersections = @($validator | Where-Object { $priorTiles.Contains([int]$_.tileId) })
        $firstChangedRead = if ($intersections.Count -gt 0) { [int]$intersections[0].tileId } else { '' }

        $firstCellDifference = ''
        if ($case.Classification -eq 'ExactMatch') {
            $diagnosticPath = Join-Path $ResultDirectory ('OfflineCellDiagnostics\' + $case.Id + '.json')
            $diagnostic = Get-Content -LiteralPath $diagnosticPath -Raw | ConvertFrom-Json
            $offlineCells = @($diagnostic.Diagnostic.Elements | ForEach-Object { $_.Cells })
            $nativeKeys = @($grid | ForEach-Object { '{0},{1},{2}' -f $_.worldX, $_.worldY, $_.blocked } | Sort-Object)
            $offlineKeys = @($offlineCells | ForEach-Object { '{0},{1},{2}' -f $_.MapX, $_.MapY, $_.Blocked } | Sort-Object)
            if ($nativeKeys.Count -ne $offlineKeys.Count) {
                $firstCellDifference = "cell count: native=$($nativeKeys.Count), offline=$($offlineKeys.Count)"
            } else {
                for ($index = 0; $index -lt $nativeKeys.Count; $index++) {
                    if ($nativeKeys[$index] -cne $offlineKeys[$index]) {
                        $firstCellDifference = "native=$($nativeKeys[$index]); offline=$($offlineKeys[$index])"
                        break
                    }
                }
            }
        }

        [pscustomobject]@{
            CaseId = $case.Id
            Session = $session
            PlayerId = $case.PlayerId
            Rotation = $case.Rotation
            Classification = $case.Classification
            NativeScore = $case.Native.RawFitScore
            NativePercent = $case.Native.FitPercentage
            NativeBlocked = $case.Native.BlockedCells
            NativeGridRows = $grid.Count
            PriorChangedTileCount = $priorTiles.Count
            PriorChangedReadCount = $intersections.Count
            FirstPriorChangedReadTileId = $firstChangedRead
            FirstUnavailableReason = $case.FirstDifference
            FirstCellDifference = $firstCellDifference
            CellTrace = $cellFile.Name
        }
    }
)

$outPath = Join-Path $ResultDirectory 'cell-review.csv'
$output | Export-Csv -LiteralPath $outPath -NoTypeInformation -Encoding UTF8
Write-Output "cases=$($output.Count), exact=$(@($output | Where-Object Classification -eq 'ExactMatch').Count), differences=$(@($output | Where-Object FirstCellDifference).Count)"
$output | Group-Object Session | ForEach-Object {
    Write-Output "session=$($_.Name), cases=$($_.Count), prior-change intersections=$((@($_.Group | Where-Object { $_.PriorChangedReadCount -gt 0 })).Count)"
}
