param(
    [string]$BlueprintImagesDirectory = (Join-Path $PSScriptRoot "..\BepInEx\plugins\SpawnCastle_Serp\BlueprintImages")
)

$ErrorActionPreference = "Stop"
$manifestPath = Join-Path $BlueprintImagesDirectory "BlueprintImages.tsv"
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Blueprint image manifest not found: $manifestPath"
}

Add-Type -AssemblyName PresentationCore

function Get-AlphaBounds([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $decoder = [Windows.Media.Imaging.PngBitmapDecoder]::new(
            $stream,
            [Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
            [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
        $source = $decoder.Frames[0]
        if ($source.Format -ne [Windows.Media.PixelFormats]::Bgra32) {
            $source = [Windows.Media.Imaging.FormatConvertedBitmap]::new(
                $source,
                [Windows.Media.PixelFormats]::Bgra32,
                $null,
                0)
        }
        $width = $source.PixelWidth
        $height = $source.PixelHeight
        $stride = $width * 4
        $pixels = [byte[]]::new($stride * $height)
        $source.CopyPixels($pixels, $stride, 0)
    }
    finally {
        $stream.Dispose()
    }

    $minimumX = $width
    $maximumX = -1
    $minimumY = $height
    $maximumY = -1
    for ($y = 0; $y -lt $height; $y++) {
        $row = $y * $stride
        for ($x = 0; $x -lt $width; $x++) {
            if ($pixels[$row + $x * 4 + 3] -le 8) {
                continue
            }
            $minimumX = [Math]::Min($minimumX, $x)
            $maximumX = [Math]::Max($maximumX, $x)
            $minimumY = [Math]::Min($minimumY, $y)
            $maximumY = [Math]::Max($maximumY, $y)
        }
    }
    if ($maximumX -lt $minimumX -or $maximumY -lt $minimumY) {
        throw "Blueprint image contains no visible pixels: $Path"
    }
    return [pscustomobject]@{
        X = $minimumX
        # WPF rows start at the top; Unity texture UV rectangles start at the bottom.
        Y = $height - $maximumY - 1
        Width = $maximumX - $minimumX + 1
        Height = $maximumY - $minimumY + 1
    }
}

$output = [Collections.Generic.List[string]]::new()
$output.Add("# formatVersion`tmapperValue`tmapperName`tskin`tview`tpngFile`tpivotX`tpivotY`tppu`talphaX`talphaY`talphaWidth`talphaHeight`tfragmentSignature")
$count = 0
$lineNumber = 0
foreach ($line in [IO.File]::ReadAllLines($manifestPath)) {
    $lineNumber++
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
        continue
    }
    $parts = $line.Split("`t")
    if (($parts.Count -ne 10 -or $parts[0] -ne "1") -and
        ($parts.Count -ne 14 -or $parts[0] -ne "2")) {
        throw "Unsupported manifest record at ${manifestPath}:$lineNumber"
    }
    $relativePath = $parts[5]
    if ([IO.Path]::IsPathRooted($relativePath) -or $relativePath.Contains("..")) {
        throw "Unsafe Blueprint PNG path at ${manifestPath}:$lineNumber"
    }
    $bounds = Get-AlphaBounds (Join-Path $BlueprintImagesDirectory $relativePath)
    $signature = if ($parts[0] -eq "1") { $parts[9] } else { $parts[13] }
    $output.Add((@(
        "2",
        $parts[1],
        $parts[2],
        $parts[3],
        $parts[4],
        $parts[5],
        $parts[6],
        $parts[7],
        $parts[8],
        $bounds.X,
        $bounds.Y,
        $bounds.Width,
        $bounds.Height,
        $signature
    ) -join "`t"))
    $count++
}

[IO.File]::WriteAllText(
    $manifestPath,
    (($output -join "`r`n") + "`r`n"),
    [Text.UTF8Encoding]::new($false))
Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] Added alpha bounds to $count Blueprint composite record(s): $manifestPath"
