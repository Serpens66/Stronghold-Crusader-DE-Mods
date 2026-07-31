param(
    [string]$CapturedDirectory = "E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\SpawnCastle_Serp\BlueprintImages\_Captured",
    [string]$LibraryDirectory = (Join-Path $PSScriptRoot "..\BepInEx\plugins\SpawnCastle_Serp\BlueprintImages"),
    [switch]$PruneSuperseded
)

$ErrorActionPreference = "Stop"
$manifestName = "BlueprintImages.tsv"
$fragmentCaptureManifestName = "BlueprintFragmentCaptures.tsv"
$fragmentTileManifestName = "BlueprintCaptureTiles.tsv"
$fragmentManifestName = "BlueprintFragments.tsv"
$capturedManifest = Join-Path $CapturedDirectory $manifestName
$libraryManifest = Join-Path $LibraryDirectory $manifestName

function Write-ImportLog([string]$Message) {
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] $Message"
}

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

function Unescape-FragmentField([string]$Value) {
    return $Value.Replace("%3D", "=").Replace("%0A", "`n").Replace("%0D", "`r").Replace("%09", "`t").Replace("%25", "%")
}

function Read-FragmentRecords([string]$Path) {
    $records = @()
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadAllLines($Path)) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            continue
        }
        $parts = $line.Split("`t")
        if ($parts.Count -lt 3 -or $parts[0] -ne "2") {
            throw "Invalid fragment record header at ${Path}:$lineNumber"
        }
        $fields = @{}
        for ($index = 2; $index -lt $parts.Count; $index++) {
            $equals = $parts[$index].IndexOf('=')
            if ($equals -le 0) {
                throw "Invalid fragment field at ${Path}:$lineNumber"
            }
            $name = $parts[$index].Substring(0, $equals)
            if ($fields.ContainsKey($name)) {
                throw "Duplicate fragment field '$name' at ${Path}:$lineNumber"
            }
            $fields[$name] = Unescape-FragmentField $parts[$index].Substring($equals + 1)
        }
        $records += [pscustomobject]@{
            Key = Unescape-FragmentField $parts[1]
            Index = if ($fields.ContainsKey("index")) { [int]$fields["index"] } else { -1 }
            Fields = $fields
            Line = $line
        }
    }
    return $records
}

function Assert-RequiredField($Record, [string]$Name, [string]$Path) {
    if (-not $Record.Fields.ContainsKey($Name) -or [string]::IsNullOrWhiteSpace($Record.Fields[$Name])) {
        throw "Required fragment field '$Name' is missing in $Path for key '$($Record.Key)'"
    }
}

function Assert-FieldPresent($Record, [string]$Name, [string]$Path) {
    if (-not $Record.Fields.ContainsKey($Name)) {
        throw "Required fragment field '$Name' is missing in $Path for key '$($Record.Key)'"
    }
}

function Get-RequiredInt($Record, [string]$Name, [string]$Path) {
    Assert-RequiredField $Record $Name $Path
    [int]$value = 0
    if (-not [int]::TryParse(
        $Record.Fields[$Name],
        [Globalization.NumberStyles]::Integer,
        [Globalization.CultureInfo]::InvariantCulture,
        [ref]$value)) {
        throw "Fragment field '$Name' is not an integer in $Path for key '$($Record.Key)'"
    }
    return $value
}

function Get-RequiredFloat($Record, [string]$Name, [string]$Path) {
    Assert-RequiredField $Record $Name $Path
    [single]$value = 0
    if (-not [single]::TryParse(
        $Record.Fields[$Name],
        [Globalization.NumberStyles]::Float,
        [Globalization.CultureInfo]::InvariantCulture,
        [ref]$value) -or
        [single]::IsNaN($value) -or
        [single]::IsInfinity($value)) {
        throw "Fragment field '$Name' is not finite in $Path for key '$($Record.Key)'"
    }
    return $value
}

function Test-SafeRelativePath([string]$Path) {
    return -not [string]::IsNullOrWhiteSpace($Path) -and
        -not [IO.Path]::IsPathRooted($Path) -and
        -not $Path.Contains("..") -and
        -not $Path.Contains(":")
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

$fragmentCapturePath = Join-Path $CapturedDirectory $fragmentCaptureManifestName
$fragmentTilePath = Join-Path $CapturedDirectory $fragmentTileManifestName
$fragmentPath = Join-Path $CapturedDirectory $fragmentManifestName
$fragmentManifestPresence = @($fragmentCapturePath, $fragmentTilePath, $fragmentPath) | Where-Object {
    Test-Path -LiteralPath $_ -PathType Leaf
}
if ($fragmentManifestPresence.Count -ne 0 -and $fragmentManifestPresence.Count -ne 3) {
    throw "Fragment capture is incomplete; all three fragment manifests are required."
}

$capturedFragmentCaptures = @()
$capturedFragmentTiles = @()
$capturedFragments = @()
if ($fragmentManifestPresence.Count -eq 3) {
    $capturedFragmentCaptures = @(Read-FragmentRecords $fragmentCapturePath)
    $capturedFragmentTiles = @(Read-FragmentRecords $fragmentTilePath)
    $capturedFragments = @(Read-FragmentRecords $fragmentPath)
    if ($capturedFragmentCaptures.Count -eq 0) {
        throw "Fragment capture manifest contains no captures."
    }

    $duplicateCapture = $capturedFragmentCaptures | Group-Object Key | Where-Object Count -ne 1
    if ($duplicateCapture) {
        throw "Fragment capture keys must be unique: $($duplicateCapture.Name -join ', ')"
    }
    foreach ($capture in $capturedFragmentCaptures) {
        foreach ($field in @("mapperValue", "mapperName", "skin", "view", "captureRotation", "normalizedHorizontalFlip", "fragmentCount", "tileCount", "minimumRow", "maximumRow", "fragmentSignature")) {
            Assert-RequiredField $capture $field $fragmentCapturePath
        }
        $mapperValue = Get-RequiredInt $capture "mapperValue" $fragmentCapturePath
        $captureRotation = Get-RequiredInt $capture "captureRotation" $fragmentCapturePath
        $fragmentCount = Get-RequiredInt $capture "fragmentCount" $fragmentCapturePath
        $tileCount = Get-RequiredInt $capture "tileCount" $fragmentCapturePath
        $minimumRow = Get-RequiredInt $capture "minimumRow" $fragmentCapturePath
        $maximumRow = Get-RequiredInt $capture "maximumRow" $fragmentCapturePath
        if ($mapperValue -lt 0 -or $captureRotation -lt 0 -or $captureRotation -gt 3 -or
            $fragmentCount -le 0 -or $tileCount -le 0 -or $maximumRow -lt $minimumRow) {
            throw "Invalid capture range or counts for '$($capture.Key)'"
        }
        if (@("Generic", "European", "Islamic") -notcontains $capture.Fields["skin"] -or
            @("Default", "ReservationDefault", "ReservationFront", "ReservationRear", "PlacedDefault", "StairNorth", "StairSouth", "DrawbridgeFront", "DrawbridgeRear") -notcontains $capture.Fields["view"] -or
            @("true", "false") -notcontains $capture.Fields["normalizedHorizontalFlip"]) {
            throw "Invalid capture enum or flip value for '$($capture.Key)'"
        }
        $expectedKey = "$($capture.Fields['mapperName'])|$($capture.Fields['skin'])|$($capture.Fields['view'])"
        if ($capture.Key -ne $expectedKey) {
            throw "Capture key does not match mapper, skin and view: '$($capture.Key)'"
        }
        $captureFragments = @($capturedFragments | Where-Object Key -eq $capture.Key | Sort-Object Index)
        $captureTiles = @($capturedFragmentTiles | Where-Object Key -eq $capture.Key | Sort-Object Index)
        if ($captureFragments.Count -ne $fragmentCount -or
            $captureTiles.Count -ne $tileCount) {
            throw "Fragment/tile count mismatch for '$($capture.Key)'"
        }
        for ($index = 0; $index -lt $captureFragments.Count; $index++) {
            if ($captureFragments[$index].Index -ne $index) {
                throw "Fragment indices are not contiguous for '$($capture.Key)'"
            }
        }
        for ($index = 0; $index -lt $captureTiles.Count; $index++) {
            if ($captureTiles[$index].Index -ne $index) {
                throw "Tile indices are not contiguous for '$($capture.Key)'"
            }
            foreach ($field in @("tileX", "tileY", "gameMapX", "gameMapY", "relativeTileX", "relativeTileY", "row", "column", "rowOffset", "columnOffset", "height", "buildingHeight", "positionX", "positionY", "positionZ", "positionOffsetX", "positionOffsetY", "tileImage", "tileTexture", "constructionOrigImage", "tileColor", "tileTransform", "tilemapSortingOrder", "tilemapSortingLayerId", "tilemapMaterial", "tilemapShader")) {
                Assert-FieldPresent $captureTiles[$index] $field $fragmentTilePath
            }
        }
        foreach ($fragment in $captureFragments) {
            $rowOffset = Get-RequiredInt $fragment "rowOffset" $fragmentPath
            if ($rowOffset -lt 0 -or $rowOffset -gt ($maximumRow - $minimumRow)) {
                throw "Fragment row offset is outside the capture range for '$($capture.Key)'"
            }
        }
    }

    foreach ($fragment in $capturedFragments) {
        foreach ($field in @("pngFile", "sha256", "width", "height", "pivotX", "pivotY", "ppu", "rowOffset", "positionOffsetX", "positionOffsetY", "positionOffsetZ")) {
            Assert-RequiredField $fragment $field $fragmentPath
        }
        foreach ($field in @("tileX", "tileY", "gameMapX", "gameMapY", "relativeTileX", "relativeTileY", "row", "column", "rowOffsetRaw", "columnOffsetRaw", "height", "position", "nativeSortingOrder", "sortingLayerId", "material", "shader", "normalizedHorizontalFlip", "spriteInstanceId", "spriteName", "textureName", "textureInstanceId", "alphaTextureName", "rect", "textureRect", "textureRectOffset", "pivot", "pixelsPerUnit", "boundsCenter", "boundsSize", "border", "packed", "packingRotation", "vertices", "uv", "triangles")) {
            Assert-FieldPresent $fragment $field $fragmentPath
        }
        $relativePng = $fragment.Fields["pngFile"]
        if (-not (Test-SafeRelativePath $relativePng)) {
            throw "Unsafe fragment PNG path for '$($fragment.Key)': $relativePng"
        }
        $fullPng = Join-Path $CapturedDirectory $relativePng
        if (-not (Test-Path -LiteralPath $fullPng -PathType Leaf)) {
            throw "Fragment PNG is missing: $fullPng"
        }
        $hash = (Get-FileHash -LiteralPath $fullPng -Algorithm SHA256).Hash
        if ($hash -ne $fragment.Fields["sha256"]) {
            throw "Fragment PNG hash mismatch: $fullPng"
        }
        $width = Get-RequiredInt $fragment "width" $fragmentPath
        $height = Get-RequiredInt $fragment "height" $fragmentPath
        $pivotX = Get-RequiredFloat $fragment "pivotX" $fragmentPath
        $pivotY = Get-RequiredFloat $fragment "pivotY" $fragmentPath
        $ppu = Get-RequiredFloat $fragment "ppu" $fragmentPath
        [void](Get-RequiredFloat $fragment "positionOffsetX" $fragmentPath)
        [void](Get-RequiredFloat $fragment "positionOffsetY" $fragmentPath)
        [void](Get-RequiredFloat $fragment "positionOffsetZ" $fragmentPath)
        if ($width -le 0 -or $height -le 0 -or $pivotX -lt 0 -or $pivotX -gt 1 -or
            $pivotY -lt 0 -or $pivotY -gt 1 -or [math]::Abs($ppu - 64) -gt 0.001) {
            throw "Fragment geometry is invalid: $fullPng"
        }
        $image = [Drawing.Image]::FromFile($fullPng)
        try {
            if ($image.Width -ne $width -or $image.Height -ne $height) {
                throw "Fragment PNG dimensions mismatch: $fullPng"
            }
        }
        finally {
            $image.Dispose()
        }
    }

    $captureKeys = @($capturedFragmentCaptures | ForEach-Object Key)
    $compositeKeys = @($capturedEntries | ForEach-Object Key)
    $orphanFragmentKeys = @($captureKeys | Where-Object { $compositeKeys -notcontains $_ })
    if ($orphanFragmentKeys.Count -gt 0) {
        throw "Fragment captures are missing composite fallbacks: $($orphanFragmentKeys -join ', ')"
    }

    # The game writes this report from GetRequiredRequests after every reload,
    # so zero is an exact catalog check rather than an assumption based on the
    # subset currently present in the staging manifests.
    $captureStatusPath = Join-Path $CapturedDirectory "MissingBlueprintCaptures.txt"
    if (-not (Test-Path -LiteralPath $captureStatusPath -PathType Leaf)) {
        throw "Catalog-derived capture status is missing: $captureStatusPath"
    }
    $fragmentMissingLines = @([IO.File]::ReadAllLines($captureStatusPath) | Where-Object {
        $_.StartsWith("fragmentMissing`t", [StringComparison]::Ordinal)
    })
    if ($fragmentMissingLines.Count -ne 1 -or $fragmentMissingLines[0] -ne "fragmentMissing`t0") {
        throw "Not all variants from GetRequiredRequests have fragment captures. See $captureStatusPath"
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

if ($fragmentManifestPresence.Count -eq 3) {
    $libraryFragmentCapturePath = Join-Path $LibraryDirectory $fragmentCaptureManifestName
    $libraryFragmentTilePath = Join-Path $LibraryDirectory $fragmentTileManifestName
    $libraryFragmentPath = Join-Path $LibraryDirectory $fragmentManifestName
    $libraryFragmentCaptures = if (Test-Path -LiteralPath $libraryFragmentCapturePath) { @(Read-FragmentRecords $libraryFragmentCapturePath) } else { @() }
    $libraryFragmentTiles = if (Test-Path -LiteralPath $libraryFragmentTilePath) { @(Read-FragmentRecords $libraryFragmentTilePath) } else { @() }
    $libraryFragments = if (Test-Path -LiteralPath $libraryFragmentPath) { @(Read-FragmentRecords $libraryFragmentPath) } else { @() }
    $importedKeys = @($capturedFragmentCaptures | ForEach-Object Key)
    $libraryFragmentCaptures = @($libraryFragmentCaptures | Where-Object { $importedKeys -notcontains $_.Key }) + $capturedFragmentCaptures
    $libraryFragmentTiles = @($libraryFragmentTiles | Where-Object { $importedKeys -notcontains $_.Key }) + $capturedFragmentTiles
    $libraryFragments = @($libraryFragments | Where-Object { $importedKeys -notcontains $_.Key }) + $capturedFragments

    foreach ($fragment in $capturedFragments) {
        $source = Join-Path $CapturedDirectory $fragment.Fields["pngFile"]
        $destination = Join-Path $LibraryDirectory $fragment.Fields["pngFile"]
        $destinationDirectory = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }

    $fragmentHeader = "# formatVersion`tcaptureKey`tname=value fields (UTF-8, CRLF)"
    foreach ($output in @(
        @{ Path = $libraryFragmentCapturePath; Records = $libraryFragmentCaptures },
        @{ Path = $libraryFragmentTilePath; Records = $libraryFragmentTiles },
        @{ Path = $libraryFragmentPath; Records = $libraryFragments }
    )) {
        $recordLines = @($output.Records | Sort-Object Key, Index | ForEach-Object Line)
        $outputLines = @($fragmentHeader) + $recordLines
        [IO.File]::WriteAllText(
            $output.Path,
            ($outputLines -join "`r`n") + "`r`n",
            [Text.UTF8Encoding]::new($false))
    }
}

$lines = @("# formatVersion`tmapperValue`tmapperName`tskin`tview`tpngFile`tpivotX`tpivotY`tppu`tfragmentSignature")
$lines += $merged.Values | Sort-Object Key | ForEach-Object { $_.Line }
[IO.File]::WriteAllText(
    $libraryManifest,
    (($lines -join "`r`n") + "`r`n"),
    [Text.UTF8Encoding]::new($false))

Write-ImportLog "Validated and imported $($capturedEntries.Count) Blueprint capture(s)."
if ($fragmentManifestPresence.Count -eq 3) {
    Write-ImportLog "Validated and imported $($capturedFragmentCaptures.Count) depth-fragment capture(s)."
}
Write-ImportLog "Library manifest: $libraryManifest"
