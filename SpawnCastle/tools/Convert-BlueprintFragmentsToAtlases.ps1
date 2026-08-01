param(
    [string]$SourceDirectory = (Join-Path $PSScriptRoot "..\BepInEx\plugins\SpawnCastle_Serp\BlueprintImages"),
    [string]$TargetDirectory = (Join-Path $PSScriptRoot "..\BepInEx\plugins\SpawnCastle_Serp\BlueprintImages"),
    [switch]$RemoveLegacy
)

$ErrorActionPreference = "Stop"
$manifestName = "BlueprintDepthAtlases.tsv"
$captureManifestName = "BlueprintFragmentCaptures.tsv"
$fragmentManifestName = "BlueprintFragments.tsv"
$tileManifestName = "BlueprintCaptureTiles.tsv"
$compositeManifestName = "BlueprintImages.tsv"
$atlasRelativeDirectory = "DepthAtlases"
$padding = 1
$maximumAtlasSize = 2048
$invariant = [Globalization.CultureInfo]::InvariantCulture

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

function Unescape-Field([string]$Value) {
    return $Value.Replace("\t", "`t").Replace("\r", "`r").Replace("\n", "`n").Replace("\\", "\")
}

function Read-Records([string]$Path) {
    $records = @()
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadLines($Path)) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#", [StringComparison]::Ordinal)) {
            continue
        }

        $parts = $line.Split("`t")
        if ($parts.Count -lt 3) {
            throw "Invalid record at ${Path}:$lineNumber"
        }
        $fields = @{}
        for ($index = 2; $index -lt $parts.Count; $index++) {
            $equals = $parts[$index].IndexOf('=')
            if ($equals -le 0) {
                throw "Invalid field at ${Path}:$lineNumber"
            }
            $fields[$parts[$index].Substring(0, $equals)] = Unescape-Field $parts[$index].Substring($equals + 1)
        }
        $records += [pscustomobject]@{
            Version = [int]$parts[0]
            Key = Unescape-Field $parts[1]
            Fields = $fields
        }
    }
    return $records
}

function Read-CompositeEntries([string]$Path) {
    $entries = @()
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadLines($Path)) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#", [StringComparison]::Ordinal)) {
            continue
        }

        $parts = $line.Split("`t")
        if ($parts.Count -ne 14 -or $parts[0] -ne "2") {
            throw "Invalid composite record at ${Path}:$lineNumber"
        }
        $entries += [pscustomobject]@{
            Key = "$($parts[2])|$($parts[3])|$($parts[4])"
            MapperValue = [int]::Parse($parts[1], $invariant)
            MapperName = $parts[2]
            Skin = $parts[3]
            View = $parts[4]
            PngFile = $parts[5]
            PivotX = [double]::Parse($parts[6], [Globalization.NumberStyles]::Float, $invariant)
            PivotY = [double]::Parse($parts[7], [Globalization.NumberStyles]::Float, $invariant)
            PixelsPerUnit = [double]::Parse($parts[8], [Globalization.NumberStyles]::Float, $invariant)
            FragmentSignature = $parts[13]
        }
    }
    return $entries
}

function Require-Field($Record, [string]$Name) {
    if (-not $Record.Fields.ContainsKey($Name)) {
        throw "Missing '$Name' for '$($Record.Key)'."
    }
    return [string]$Record.Fields[$Name]
}

function Parse-Int($Record, [string]$Name) {
    $value = 0
    if (-not [int]::TryParse((Require-Field $Record $Name), [Globalization.NumberStyles]::Integer, $invariant, [ref]$value)) {
        throw "Invalid integer '$Name' for '$($Record.Key)'."
    }
    return $value
}

function Parse-Float($Record, [string]$Name) {
    $value = 0.0
    if (-not [double]::TryParse((Require-Field $Record $Name), [Globalization.NumberStyles]::Float, $invariant, [ref]$value)) {
        throw "Invalid number '$Name' for '$($Record.Key)'."
    }
    return $value
}

function Sanitize-Name([string]$Value) {
    return [Text.RegularExpressions.Regex]::Replace($Value, '[^A-Za-z0-9_.-]', '_')
}

function Read-BgraImage([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $decoder = [Windows.Media.Imaging.PngBitmapDecoder]::new(
            $stream,
            [Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
            [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
        [Windows.Media.Imaging.BitmapSource]$source = $decoder.Frames[0]
        if ($source.Format -ne [Windows.Media.PixelFormats]::Bgra32) {
            $converted = [Windows.Media.Imaging.FormatConvertedBitmap]::new()
            $converted.BeginInit()
            $converted.Source = $source
            $converted.DestinationFormat = [Windows.Media.PixelFormats]::Bgra32
            $converted.EndInit()
            $converted.Freeze()
            $source = $converted
        }
        $stride = $source.PixelWidth * 4
        $pixels = [byte[]]::new($stride * $source.PixelHeight)
        $source.CopyPixels($pixels, $stride, 0)
        return [pscustomobject]@{
            Width = $source.PixelWidth
            Height = $source.PixelHeight
            Stride = $stride
            Pixels = $pixels
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Try-Pack($Images, [int]$Size) {
    $x = $padding
    $y = $padding
    $rowHeight = 0
    $placements = @{}
    $usedRight = 0
    $usedBottom = 0
    foreach ($image in $Images) {
        if ($image.Width + 2 * $padding -gt $Size -or $image.Height + 2 * $padding -gt $Size) {
            return $null
        }
        if ($x + $image.Width + $padding -gt $Size) {
            $x = $padding
            $y += $rowHeight + 2 * $padding
            $rowHeight = 0
        }
        if ($y + $image.Height + $padding -gt $Size) {
            return $null
        }
        $placements[$image.Index] = [pscustomobject]@{ X = $x; Y = $y }
        $usedRight = [Math]::Max($usedRight, $x + $image.Width + $padding)
        $usedBottom = [Math]::Max($usedBottom, $y + $image.Height + $padding)
        $x += $image.Width + 2 * $padding
        $rowHeight = [Math]::Max($rowHeight, $image.Height)
    }
    return [pscustomobject]@{
        Placements = $placements
        Width = [Math]::Max(4, [int][Math]::Ceiling($usedRight / 4.0) * 4)
        Height = [Math]::Max(4, [int][Math]::Ceiling($usedBottom / 4.0) * 4)
    }
}

function Write-Png([string]$Path, [byte[]]$Pixels, [int]$Width, [int]$Height) {
    $bitmap = [Windows.Media.Imaging.BitmapSource]::Create(
        $Width, $Height, 96, 96, [Windows.Media.PixelFormats]::Bgra32, $null, $Pixels, $Width * 4)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.File]::Create($Path)
    try { $encoder.Save($stream) } finally { $stream.Dispose() }
}

$capturePath = Join-Path $SourceDirectory $captureManifestName
$fragmentPath = Join-Path $SourceDirectory $fragmentManifestName
if (-not (Test-Path -LiteralPath $capturePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $fragmentPath -PathType Leaf)) {
    throw "Legacy capture and fragment manifests are required in '$SourceDirectory'."
}

$captures = @(Read-Records $capturePath)
$fragments = @(Read-Records $fragmentPath)
if ($captures.Count -eq 0 -or $fragments.Count -eq 0) {
    throw "No depth captures were found in '$SourceDirectory'."
}

# The four curated wall symbols are synthesized below as regular one-fragment
# atlas captures. Ignore older automatic captures so they cannot replace the
# manually verified finished-structure artwork.
$curatedSingleFragmentMappers = @("MAPPER_WALL", "MAPPER_WOODWALL", "MAPPER_CRENAL", "MAPPER_CRENAL2")
$selectedCaptures = [Collections.Generic.List[object]]::new()
$selectedOriginalKeys = @{}
foreach ($group in ($captures | Where-Object {
    $curatedSingleFragmentMappers -notcontains (Require-Field $_ "mapperName")
} | Group-Object {
    $_.Key -replace '^MAPPER_STAIR[1-6]\|', 'MAPPER_STAIR|'
})) {
    $selected = $group.Group | Sort-Object {
        if ($_.Fields.ContainsKey("capturedUtc")) { $_.Fields["capturedUtc"] } else { "" }
    } -Descending | Select-Object -First 1
    $originalKey = $selected.Key
    $selected.Key = $group.Name
    if ($selected.Key.StartsWith("MAPPER_STAIR|", [StringComparison]::Ordinal)) {
        $selected.Fields["mapperName"] = "MAPPER_STAIR"
    }
    $selectedCaptures.Add($selected)
    $selectedOriginalKeys[$originalKey] = $selected.Key
}
$captures = @($selectedCaptures)
$fragments = @($fragments | Where-Object {
    $selectedOriginalKeys.ContainsKey($_.Key)
} | ForEach-Object {
    $_.Key = $selectedOriginalKeys[$_.Key]
    $_
})

$compositeManifestPath = Join-Path $TargetDirectory $compositeManifestName
if (-not (Test-Path -LiteralPath $compositeManifestPath -PathType Leaf)) {
    throw "The production composite manifest is required: $compositeManifestPath"
}
$curatedEntries = @(Read-CompositeEntries $compositeManifestPath | Where-Object {
    $curatedSingleFragmentMappers -contains $_.MapperName
})
if ($curatedEntries.Count -ne $curatedSingleFragmentMappers.Count) {
    throw "Expected $($curatedSingleFragmentMappers.Count) curated wall composites but found $($curatedEntries.Count)."
}
foreach ($mapperName in $curatedSingleFragmentMappers) {
    if (@($curatedEntries | Where-Object MapperName -eq $mapperName).Count -ne 1) {
        throw "The curated wall composite '$mapperName' must have exactly one manifest entry."
    }
}

New-Item -ItemType Directory -Path $TargetDirectory -Force | Out-Null
$atlasDirectory = Join-Path $TargetDirectory $atlasRelativeDirectory
New-Item -ItemType Directory -Path $atlasDirectory -Force | Out-Null
$manifestLines = [Collections.Generic.List[string]]::new()
$manifestLines.Add("# SpawnCastle Blueprint depth atlas format 1 (UTF-8, CRLF)")
$generatedAtlasPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$atlasCount = 0
$fragmentCount = 0

foreach ($capture in ($captures | Sort-Object Key)) {
    $captureFragments = @($fragments | Where-Object Key -eq $capture.Key | ForEach-Object {
        $record = $_
        $index = Parse-Int $record "index"
        $relative = Require-Field $record "pngFile"
        if ([IO.Path]::IsPathRooted($relative) -or $relative.Contains("..") -or $relative.Contains(':')) {
            throw "Unsafe fragment path '$relative'."
        }
        $fullPath = [IO.Path]::GetFullPath((Join-Path $SourceDirectory $relative))
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Fragment PNG is missing: $fullPath"
        }
        $image = Read-BgraImage $fullPath
        $expectedWidth = Parse-Int $record "width"
        $expectedHeight = Parse-Int $record "height"
        if ($image.Width -ne $expectedWidth -or $image.Height -ne $expectedHeight) {
            throw "Fragment dimensions differ for '$($capture.Key)' index $index."
        }
        [pscustomobject]@{
            Record = $record
            Index = $index
            Width = $image.Width
            Height = $image.Height
            Pixels = $image.Pixels
            Stride = $image.Stride
            RowOffset = Parse-Int $record "rowOffset"
            SortingOffset = if ($record.Fields.ContainsKey("sortingOffset")) { Parse-Int $record "sortingOffset" } else { 0 }
        }
    } | Sort-Object RowOffset, SortingOffset, @{Expression='Height';Descending=$true}, @{Expression='Width';Descending=$true}, Index)

    $expectedCount = Parse-Int $capture "fragmentCount"
    if ($captureFragments.Count -ne $expectedCount) {
        throw "Capture '$($capture.Key)' expected $expectedCount fragments but found $($captureFragments.Count)."
    }
    $indices = @($captureFragments.Index | Sort-Object)
    for ($index = 0; $index -lt $indices.Count; $index++) {
        if ($indices[$index] -ne $index) { throw "Fragment indices are not contiguous for '$($capture.Key)'." }
    }

    $packing = $null
    foreach ($size in @(256, 512, 1024, 2048)) {
        $packing = Try-Pack $captureFragments $size
        if ($null -ne $packing) { break }
    }
    if ($null -eq $packing -or $packing.Width -gt $maximumAtlasSize -or $packing.Height -gt $maximumAtlasSize) {
        throw "Capture '$($capture.Key)' does not fit in a ${maximumAtlasSize}x${maximumAtlasSize} atlas."
    }

    $atlasStride = $packing.Width * 4
    $atlasPixels = [byte[]]::new($atlasStride * $packing.Height)
    foreach ($fragment in $captureFragments) {
        $placement = $packing.Placements[$fragment.Index]
        for ($row = 0; $row -lt $fragment.Height; $row++) {
            [Buffer]::BlockCopy(
                $fragment.Pixels,
                $row * $fragment.Stride,
                $atlasPixels,
                ($placement.Y + $row) * $atlasStride + $placement.X * 4,
                $fragment.Stride)
        }
    }

    $baseName = Sanitize-Name $capture.Key
    $pngFileName = "${baseName}_p00.png"
    $relativeAtlasPath = "$atlasRelativeDirectory\$pngFileName"
    $atlasPath = Join-Path $atlasDirectory $pngFileName
    Write-Png $atlasPath $atlasPixels $packing.Width $packing.Height
    [void]$generatedAtlasPaths.Add([IO.Path]::GetFullPath($atlasPath))
    $validation = Read-BgraImage $atlasPath
    if ($validation.Width -ne $packing.Width -or $validation.Height -ne $packing.Height) {
        throw "Atlas dimensions changed while encoding '$($capture.Key)'."
    }
    foreach ($fragment in $captureFragments) {
        $placement = $packing.Placements[$fragment.Index]
        for ($row = 0; $row -lt $fragment.Height; $row++) {
            $sourceOffset = $row * $fragment.Stride
            $atlasOffset = ($placement.Y + $row) * $validation.Stride + $placement.X * 4
            for ($byte = 0; $byte -lt $fragment.Stride; $byte++) {
                if ($fragment.Pixels[$sourceOffset + $byte] -ne $validation.Pixels[$atlasOffset + $byte]) {
                    throw "Atlas pixel verification failed for '$($capture.Key)' fragment $($fragment.Index)."
                }
            }
        }
    }
    $sha256 = (Get-FileHash -LiteralPath $atlasPath -Algorithm SHA256).Hash

    $manifestLines.Add((@(
        "C", $capture.Key,
        (Require-Field $capture "mapperValue"),
        (Require-Field $capture "mapperName"),
        (Require-Field $capture "skin"),
        (Require-Field $capture "view"),
        (Require-Field $capture "captureRotation"),
        (Require-Field $capture "normalizedHorizontalFlip"),
        (Require-Field $capture "minimumRow"),
        (Require-Field $capture "maximumRow"),
        $captureFragments.Count,
        1,
        (Require-Field $capture "fragmentSignature"),
        $(if ($capture.Fields.ContainsKey("captureSource")) { $capture.Fields["captureSource"] } else { "preview" }),
        $(if ($capture.Fields.ContainsKey("placedVisualVersion")) { $capture.Fields["placedVisualVersion"] } else { "" })
    ) -join "`t"))
    $manifestLines.Add((@("P", $capture.Key, 0, $relativeAtlasPath, $packing.Width, $packing.Height, $sha256) -join "`t"))

    foreach ($fragment in ($captureFragments | Sort-Object Index)) {
        $record = $fragment.Record
        $placement = $packing.Placements[$fragment.Index]
        $unityY = $packing.Height - $placement.Y - $fragment.Height
        $manifestLines.Add((@(
            "F", $capture.Key, $fragment.Index, 0,
            $placement.X, $unityY, $fragment.Width, $fragment.Height,
            $fragment.RowOffset, $fragment.SortingOffset,
            (Require-Field $record "positionOffsetX"),
            (Require-Field $record "positionOffsetY"),
            (Require-Field $record "positionOffsetZ")
        ) -join "`t"))
        $fragmentCount++
    }
    $atlasCount++
}

foreach ($entry in ($curatedEntries | Sort-Object Key)) {
    if ([IO.Path]::IsPathRooted($entry.PngFile) -or $entry.PngFile.Contains("..") -or $entry.PngFile.Contains(':')) {
        throw "Unsafe curated composite path '$($entry.PngFile)'."
    }
    if ([Math]::Abs($entry.PixelsPerUnit - 64.0) -gt 0.001 -or
        $entry.PivotX -lt 0.0 -or $entry.PivotX -gt 1.0 -or
        $entry.PivotY -lt 0.0 -or $entry.PivotY -gt 1.0) {
        throw "Invalid PPU or pivot for curated composite '$($entry.Key)'."
    }

    $sourcePath = [IO.Path]::GetFullPath((Join-Path $TargetDirectory $entry.PngFile))
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Curated composite PNG is missing: $sourcePath"
    }
    $sourceImage = Read-BgraImage $sourcePath
    $singleFragment = [pscustomobject]@{
        Index = 0
        Width = $sourceImage.Width
        Height = $sourceImage.Height
        Pixels = $sourceImage.Pixels
        Stride = $sourceImage.Stride
    }
    $packing = $null
    foreach ($size in @(256, 512, 1024, 2048)) {
        $packing = Try-Pack @($singleFragment) $size
        if ($null -ne $packing) { break }
    }
    if ($null -eq $packing -or $packing.Width -gt $maximumAtlasSize -or $packing.Height -gt $maximumAtlasSize) {
        throw "Curated composite '$($entry.Key)' does not fit in a ${maximumAtlasSize}x${maximumAtlasSize} atlas."
    }

    $placement = $packing.Placements[0]
    $atlasStride = $packing.Width * 4
    $atlasPixels = [byte[]]::new($atlasStride * $packing.Height)
    for ($row = 0; $row -lt $singleFragment.Height; $row++) {
        [Buffer]::BlockCopy(
            $singleFragment.Pixels,
            $row * $singleFragment.Stride,
            $atlasPixels,
            ($placement.Y + $row) * $atlasStride + $placement.X * 4,
            $singleFragment.Stride)
    }

    $baseName = Sanitize-Name $entry.Key
    $pngFileName = "${baseName}_p00.png"
    $relativeAtlasPath = "$atlasRelativeDirectory\$pngFileName"
    $atlasPath = Join-Path $atlasDirectory $pngFileName
    Write-Png $atlasPath $atlasPixels $packing.Width $packing.Height
    [void]$generatedAtlasPaths.Add([IO.Path]::GetFullPath($atlasPath))
    $validation = Read-BgraImage $atlasPath
    if ($validation.Width -ne $packing.Width -or $validation.Height -ne $packing.Height) {
        throw "Atlas dimensions changed while encoding '$($entry.Key)'."
    }
    for ($row = 0; $row -lt $singleFragment.Height; $row++) {
        $sourceOffset = $row * $singleFragment.Stride
        $atlasOffset = ($placement.Y + $row) * $validation.Stride + $placement.X * 4
        for ($byte = 0; $byte -lt $singleFragment.Stride; $byte++) {
            if ($singleFragment.Pixels[$sourceOffset + $byte] -ne $validation.Pixels[$atlasOffset + $byte]) {
                throw "Atlas pixel verification failed for curated composite '$($entry.Key)'."
            }
        }
    }

    $sha256 = (Get-FileHash -LiteralPath $atlasPath -Algorithm SHA256).Hash
    $left = (-$entry.PivotX * $singleFragment.Width / $entry.PixelsPerUnit).ToString("R", $invariant)
    $bottom = (-$entry.PivotY * $singleFragment.Height / $entry.PixelsPerUnit).ToString("R", $invariant)
    $unityY = $packing.Height - $placement.Y - $singleFragment.Height
    $manifestLines.Add((@(
        "C", $entry.Key, $entry.MapperValue, $entry.MapperName, $entry.Skin,
        $entry.View, 0, "false", 0, 0, 1, 1,
        $entry.FragmentSignature, "placed", "4"
    ) -join "`t"))
    $manifestLines.Add((@(
        "P", $entry.Key, 0, $relativeAtlasPath,
        $packing.Width, $packing.Height, $sha256
    ) -join "`t"))
    $manifestLines.Add((@(
        "F", $entry.Key, 0, 0,
        $placement.X, $unityY, $singleFragment.Width, $singleFragment.Height,
        0, 0, $left, $bottom, 0
    ) -join "`t"))
    $fragmentCount++
    $atlasCount++
}

$manifestPath = Join-Path $TargetDirectory $manifestName
[IO.File]::WriteAllText(
    $manifestPath,
    ($manifestLines -join "`r`n") + "`r`n",
    [Text.UTF8Encoding]::new($false))

# Remove pages from captures that no longer belong to the compact production
# manifest, but only after every replacement page and the manifest succeeded.
$resolvedAtlasDirectory = [IO.Path]::GetFullPath($atlasDirectory).TrimEnd('\') + '\'
foreach ($existingAtlas in Get-ChildItem -LiteralPath $atlasDirectory -Filter '*.png' -File) {
    $existingPath = [IO.Path]::GetFullPath($existingAtlas.FullName)
    if (-not $existingPath.StartsWith($resolvedAtlasDirectory, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unsafe stale atlas path '$existingPath'."
    }
    if (-not $generatedAtlasPaths.Contains($existingPath)) {
        Remove-Item -LiteralPath $existingPath -Force
    }
}

if ($RemoveLegacy) {
    $legacyFragmentDirectory = Join-Path $TargetDirectory "Fragments"
    $resolvedTarget = [IO.Path]::GetFullPath($TargetDirectory).TrimEnd('\') + '\'
    if (Test-Path -LiteralPath $legacyFragmentDirectory) {
        $resolvedLegacy = [IO.Path]::GetFullPath($legacyFragmentDirectory)
        if (-not $resolvedLegacy.StartsWith($resolvedTarget, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to remove unsafe legacy path '$resolvedLegacy'."
        }
        Remove-Item -LiteralPath $resolvedLegacy -Recurse -Force
    }
    foreach ($name in @($captureManifestName, $fragmentManifestName, $tileManifestName)) {
        $legacyManifest = Join-Path $TargetDirectory $name
        if (Test-Path -LiteralPath $legacyManifest) {
            Remove-Item -LiteralPath $legacyManifest -Force
        }
    }
}

Write-Host "Generated $atlasCount depth atlas(es) containing $fragmentCount fragments."
Write-Host "Manifest: $manifestPath"
