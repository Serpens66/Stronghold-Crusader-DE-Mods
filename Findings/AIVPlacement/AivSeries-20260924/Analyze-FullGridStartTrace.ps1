param(
    [Parameter(Mandatory = $true)][string]$TraceDirectory,
    [string]$ExpectedMapSha256 = '',
    [string]$ExpectedNativeSha256 = ''
)

$ErrorActionPreference = 'Stop'
$files = @(Get-ChildItem -LiteralPath $TraceDirectory -File -Filter 'oracle-start-trace-*.tsv' |
    Sort-Object LastWriteTimeUtc)
if ($files.Count -eq 0) { throw 'No Keep-start trace files found.' }

$summaries = @()
foreach ($file in $files) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    $header = @{}
    $firstDataLine = -1
    for ($i = 0; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -eq "x`ty`ttileId`tlayer`tbefore`tafter") {
            $firstDataLine = $i + 1
            break
        }
        if ($lines[$i] -match '^# ([^=]+)=(.*)$') { $header[$Matches[1]] = $Matches[2] }
    }
    if ($firstDataLine -lt 0) { throw "Missing TSV header: $($file.Name)" }
    if (-not $header.ContainsKey('fullMapTileLayersComplete')) { continue }
    if ($header['fullMapTileLayersComplete'] -ne 'True' -or
        $header['sampledTiles'] -ne '320800') {
        throw "Incomplete full-grid capture: $($file.Name)"
    }
    if (-not $header.ContainsKey('preNativeStartCleanupFlag') -or
        -not $header.ContainsKey('postNativeDestroyedRecordMarker')) {
        throw "Missing constructor-branch flags: $($file.Name)"
    }
    if ($ExpectedMapSha256 -and $header['mapFileSha256'] -ne $ExpectedMapSha256) {
        throw "Map hash mismatch: $($file.Name)"
    }
    if ($ExpectedNativeSha256 -and $header['nativeDllSha256'] -ne $ExpectedNativeSha256) {
        throw "Native hash mismatch: $($file.Name)"
    }
    $keepX = [int]$header['keepX']
    $keepY = [int]$header['keepY']
    $outsideOldWindow = 0
    $maximumDistance = 0
    $changed = 0
    $seen = [System.Collections.Generic.HashSet[string]]::new()
    $layers = @{}
    for ($i = $firstDataLine; $i -lt $lines.Length; $i++) {
        if (-not $lines[$i]) { continue }
        $parts = $lines[$i].Split([char]9)
        if ($parts.Length -ne 6) { throw "Malformed change row: $($file.Name):$($i + 1)" }
        $x = [int]$parts[0]
        $y = [int]$parts[1]
        $tileId = [int]$parts[2]
        $layer = $parts[3]
        if ($tileId -lt 0 -or $tileId -ge 320800 -or
            -not $seen.Add("$tileId/$layer") -or
            [int]$parts[4] -eq [int]$parts[5]) {
            throw "Invalid or duplicate change row: $($file.Name):$($i + 1)"
        }
        $dx = $x - $keepX
        $dy = $y - $keepY
        if ($dx -lt -16 -or $dx -gt 24 -or $dy -lt -16 -or $dy -gt 24) {
            $outsideOldWindow++
        }
        $maximumDistance = [Math]::Max($maximumDistance,
            [Math]::Max([Math]::Abs($dx), [Math]::Abs($dy)))
        $layers[$layer] = 1 + [int]$layers[$layer]
        $changed++
    }
    if ($changed -ne [int]$header['changedLayerCells']) {
        throw "Change-count mismatch: $($file.Name)"
    }
    $summaries += [pscustomobject]@{
        File = $file.Name
        PlayerId = [int]$header['playerId']
        Orientation = [int]$header['nativeOrientation']
        FailureFlag = [int]$header['postNativeFailureFlag']
        PreStartCleanupFlag = [int]$header['preNativeStartCleanupFlag']
        PostDestroyedRecordMarker = [int]$header['postNativeDestroyedRecordMarker']
        ChangedLayerCells = $changed
        OutsideOldWindow = $outsideOldWindow
        MaximumChebyshevDistance = $maximumDistance
        BeforeScanMilliseconds = [double]::Parse($header['beforeScanMilliseconds'],
            [System.Globalization.CultureInfo]::InvariantCulture)
        AfterScanMilliseconds = [double]::Parse($header['afterScanMilliseconds'],
            [System.Globalization.CultureInfo]::InvariantCulture)
        Layers = $layers
    }
}
if ($summaries.Count -eq 0) { throw 'No new full-grid Keep-start traces found.' }
$summaries | ConvertTo-Json -Depth 5
