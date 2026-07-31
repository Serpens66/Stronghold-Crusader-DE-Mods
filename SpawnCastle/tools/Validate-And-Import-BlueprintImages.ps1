param(
    [string]$CapturedDirectory = "E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\SpawnCastle_Serp\BlueprintImages\_Captured",
    [string]$LibraryDirectory = (Join-Path $PSScriptRoot "..\BepInEx\plugins\SpawnCastle_Serp\BlueprintImages"),
    [switch]$PruneSuperseded
)

$ErrorActionPreference = "Stop"
$manifestName = "BlueprintImages.tsv"
$capturedManifest = Join-Path $CapturedDirectory $manifestName
$libraryManifest = Join-Path $LibraryDirectory $manifestName

if (-not (Test-Path -LiteralPath $capturedManifest -PathType Leaf)) {
    throw "Capture manifest not found: $capturedManifest"
}

Add-Type -AssemblyName System.Drawing

function Read-BlueprintManifest([string]$Path) {
    $entries = @()
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            continue
        }

        $parts = $line.Split("`t")
        if ($parts.Count -ne 10) {
            throw "Invalid manifest column count at ${Path}:$lineNumber"
        }

        [single]$pivotX = 0.0
        [single]$pivotY = 0.0
        [single]$ppu = 0.0
        if ($parts[0] -ne "1" -or
            -not [single]::TryParse($parts[6], [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$pivotX) -or
            -not [single]::TryParse($parts[7], [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$pivotY) -or
            -not [single]::TryParse($parts[8], [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$ppu)) {
            throw "Invalid manifest values at ${Path}:$lineNumber"
        }
        if ($pivotX -lt 0 -or $pivotX -gt 1 -or $pivotY -lt 0 -or $pivotY -gt 1 -or [math]::Abs($ppu - 64) -gt 0.001) {
            throw "Invalid pivot or PPU at ${Path}:$lineNumber"
        }
        if ([IO.Path]::IsPathRooted($parts[5]) -or $parts[5].Contains("..")) {
            throw "Unsafe PNG path at ${Path}:$lineNumber"
        }

        $entries += [pscustomobject]@{
            Key = "$($parts[2])|$($parts[3])|$($parts[4])"
            Line = $line
            PngFile = $parts[5]
            MapperName = $parts[2]
            View = $parts[4]
        }
    }
    return $entries
}

function Test-IsSupersededBlueprintEntry($Entry) {
    if ($Entry.MapperName -eq "MAPPER_CRENAL" -and $Entry.View -eq "Default") {
        return $true
    }
    if ($Entry.MapperName -match '^MAPPER_STAIR[2-6]$') {
        return $true
    }

    $reservationDefaults = @(
        "MAPPER_BARRACKS_STONE",
        "MAPPER_BARRACKS_WOOD",
        "MAPPER_BEDOUIN_STOCKADE"
    )
    if ($reservationDefaults -contains $Entry.MapperName -and $Entry.View -eq "Default") {
        return $true
    }

    $reservationAxes = @(
        "MAPPER_ENGINEERS_GUILD",
        "MAPPER_TUNNELERS_GUILD",
        "MAPPER_OIL_SMELTER"
    )
    return $reservationAxes -contains $Entry.MapperName -and
        ($Entry.View -eq "AxisNorthSouth" -or
         $Entry.View -eq "AxisEastWest" -or
         $Entry.View -eq "ReservationAxisNorthSouth" -or
         $Entry.View -eq "ReservationAxisEastWest")
}

$capturedEntries = @(Read-BlueprintManifest $capturedManifest)
if ($PruneSuperseded) {
    $capturedEntries = @($capturedEntries | Where-Object {
        -not (Test-IsSupersededBlueprintEntry $_)
    })
}
if ($capturedEntries.Count -eq 0) {
    throw "The capture manifest contains no valid entries."
}

foreach ($entry in $capturedEntries) {
    $pngPath = Join-Path $CapturedDirectory $entry.PngFile
    if (-not (Test-Path -LiteralPath $pngPath -PathType Leaf)) {
        throw "PNG referenced by manifest is missing: $pngPath"
    }
    $image = [Drawing.Image]::FromFile($pngPath)
    try {
        if ($image.Width -le 0 -or $image.Height -le 0) {
            throw "PNG has invalid dimensions: $pngPath"
        }
    }
    finally {
        $image.Dispose()
    }
}

New-Item -ItemType Directory -Path $LibraryDirectory -Force | Out-Null
$libraryEntries = @()
if (Test-Path -LiteralPath $libraryManifest -PathType Leaf) {
    $libraryEntries = @(Read-BlueprintManifest $libraryManifest)
    if ($PruneSuperseded) {
        $libraryEntries = @($libraryEntries | Where-Object {
            -not (Test-IsSupersededBlueprintEntry $_)
        })
    }
}

$merged = @{}
foreach ($entry in $libraryEntries) {
    $merged[$entry.Key] = $entry
}
foreach ($entry in $capturedEntries) {
    Copy-Item -LiteralPath (Join-Path $CapturedDirectory $entry.PngFile) -Destination (Join-Path $LibraryDirectory $entry.PngFile) -Force
    $merged[$entry.Key] = $entry
}

$lines = @("# formatVersion`tmapperValue`tmapperName`tskin`tview`tpngFile`tpivotX`tpivotY`tppu`tfragmentSignature")
$lines += $merged.Values | Sort-Object Key | ForEach-Object { $_.Line }
[IO.File]::WriteAllText(
    $libraryManifest,
    (($lines -join "`r`n") + "`r`n"),
    [Text.UTF8Encoding]::new($false))

Write-Host "Validated and imported $($capturedEntries.Count) Blueprint capture(s)."
Write-Host "Library manifest: $libraryManifest"
