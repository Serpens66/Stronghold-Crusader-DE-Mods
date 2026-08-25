[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$workspaceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$imageWidth = 1240
$horizontalPadding = 56
$verticalPadding = 48
$contentWidth = $imageWidth - (2 * $horizontalPadding)
$jpegQuality = 92L
$maximumJpegSize = 950KB

function New-RenderFont {
    param(
        [Parameter(Mandatory = $true)]
        [float] $Size,

        [System.Drawing.FontStyle] $Style = [System.Drawing.FontStyle]::Regular
    )

    return [System.Drawing.Font]::new(
        'Segoe UI',
        $Size,
        $Style,
        [System.Drawing.GraphicsUnit]::Pixel
    )
}

function ConvertTo-PlainMarkdownText {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Text
    )

    $value = $Text
    $value = [System.Text.RegularExpressions.Regex]::Replace($value, '!\[([^\]]*)\]\([^\)]*\)', '$1')
    $value = [System.Text.RegularExpressions.Regex]::Replace($value, '\[([^\]]+)\]\([^\)]*\)', '$1')
    $value = [System.Text.RegularExpressions.Regex]::Replace($value, '<br\s*/?>', ' ', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    $value = [System.Text.RegularExpressions.Regex]::Replace($value, '<[^>]+>', '')
    $value = $value.Replace('**', '').Replace('__', '').Replace('`', '')
    $value = [System.Text.RegularExpressions.Regex]::Replace($value, '(?<!\*)\*(?!\*)', '')
    return [System.Net.WebUtility]::HtmlDecode($value.Trim())
}

function ConvertFrom-ReadmeMarkdown {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Markdown
    )

    $blocks = [System.Collections.Generic.List[object]]::new()
    $paragraph = [System.Collections.Generic.List[string]]::new()

    function Add-ParagraphBlock {
        if ($paragraph.Count -eq 0) {
            return
        }

        $text = ConvertTo-PlainMarkdownText -Text ($paragraph -join ' ')
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            $blocks.Add([PSCustomObject]@{ Kind = 'Body'; Text = $text })
        }
        $paragraph.Clear()
    }

    $normalized = $Markdown.Replace("`r`n", "`n").Replace("`r", "`n")
    foreach ($rawLine in $normalized.Split([char]10)) {
        $line = $rawLine.TrimEnd()

        if ([string]::IsNullOrWhiteSpace($line)) {
            Add-ParagraphBlock
            continue
        }

        $headingMatch = [System.Text.RegularExpressions.Regex]::Match($line, '^(#{1,6})\s+(.+?)\s*#*$')
        if ($headingMatch.Success) {
            Add-ParagraphBlock
            $level = [Math]::Min($headingMatch.Groups[1].Value.Length, 3)
            $text = ConvertTo-PlainMarkdownText -Text $headingMatch.Groups[2].Value
            $blocks.Add([PSCustomObject]@{ Kind = "Heading$level"; Text = $text })
            continue
        }

        $bulletMatch = [System.Text.RegularExpressions.Regex]::Match($line, '^\s*[-+*]\s+(.+)$')
        if ($bulletMatch.Success) {
            Add-ParagraphBlock
            $text = ConvertTo-PlainMarkdownText -Text $bulletMatch.Groups[1].Value
            $bulletText = ([char]0x2022) + " $text"
            $blocks.Add([PSCustomObject]@{ Kind = 'List'; Text = $bulletText })
            continue
        }

        $numberMatch = [System.Text.RegularExpressions.Regex]::Match($line, '^\s*(\d+)\.\s+(.+)$')
        if ($numberMatch.Success) {
            Add-ParagraphBlock
            $text = ConvertTo-PlainMarkdownText -Text $numberMatch.Groups[2].Value
            $blocks.Add([PSCustomObject]@{ Kind = 'List'; Text = "$($numberMatch.Groups[1].Value). $text" })
            continue
        }

        if ($line -match '^\s*([-*_])(?:\s*\1){2,}\s*$') {
            Add-ParagraphBlock
            continue
        }

        $paragraph.Add($line.Trim())
    }

    Add-ParagraphBlock
    return $blocks
}

function Get-BlockPresentation {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Kind,

        [Parameter(Mandatory = $true)]
        [hashtable] $Fonts,

        [Parameter(Mandatory = $true)]
        [hashtable] $Brushes
    )

    switch ($Kind) {
        'Heading1' { return [PSCustomObject]@{ Font = $Fonts.H1; Brush = $Brushes.Heading; GapBefore = 10; GapAfter = 18 } }
        'Heading2' { return [PSCustomObject]@{ Font = $Fonts.H2; Brush = $Brushes.Heading; GapBefore = 22; GapAfter = 12 } }
        'Heading3' { return [PSCustomObject]@{ Font = $Fonts.H3; Brush = $Brushes.Heading; GapBefore = 16; GapAfter = 8 } }
        'List'     { return [PSCustomObject]@{ Font = $Fonts.Body; Brush = $Brushes.Body; GapBefore = 0; GapAfter = 8 } }
        default    { return [PSCustomObject]@{ Font = $Fonts.Body; Brush = $Brushes.Body; GapBefore = 0; GapAfter = 15 } }
    }
}

function Get-JpegCodec {
    $codecs = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders()
    return $codecs | Where-Object MimeType -eq 'image/jpeg' | Select-Object -First 1
}

function Write-ReadmeImage {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo] $Readme,

        [Parameter(Mandatory = $true)]
        [object[]] $Blocks
    )

    $fonts = @{
        H1 = New-RenderFont -Size 42 -Style Bold
        H2 = New-RenderFont -Size 34 -Style Bold
        H3 = New-RenderFont -Size 29 -Style Bold
        Body = New-RenderFont -Size 27
    }
    $brushes = @{
        Background = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(27, 40, 56))
        Heading = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(102, 192, 244))
        Body = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(199, 213, 224))
    }
    $format = [System.Drawing.StringFormat]::new([System.Drawing.StringFormat]::GenericTypographic)
    $format.FormatFlags = $format.FormatFlags -bor [System.Drawing.StringFormatFlags]::LineLimit
    $format.Trimming = [System.Drawing.StringTrimming]::Word

    $measureBitmap = [System.Drawing.Bitmap]::new(1, 1)
    $measureGraphics = [System.Drawing.Graphics]::FromImage($measureBitmap)
    try {
        $measureGraphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $layout = [System.Collections.Generic.List[object]]::new()
        $y = [float]$verticalPadding

        foreach ($block in $Blocks) {
            $presentation = Get-BlockPresentation -Kind $block.Kind -Fonts $fonts -Brushes $brushes
            $y += [float]$presentation.GapBefore
            $size = $measureGraphics.MeasureString(
                $block.Text,
                $presentation.Font,
                [System.Drawing.SizeF]::new([float]$contentWidth, 100000.0),
                $format
            )
            $height = [Math]::Ceiling($size.Height) + 2
            $layout.Add([PSCustomObject]@{
                Text = $block.Text
                Font = $presentation.Font
                Brush = $presentation.Brush
                Y = $y
                Height = $height
            })
            $y += $height + [float]$presentation.GapAfter
        }

        $imageHeight = [Math]::Max(200, [Math]::Ceiling($y + $verticalPadding))
    }
    finally {
        $measureGraphics.Dispose()
        $measureBitmap.Dispose()
    }

    if ($imageHeight -gt 65000) {
        throw "README is too long for one JPEG image: $($Readme.FullName)"
    }

    $bitmap = [System.Drawing.Bitmap]::new($imageWidth, $imageHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $bitmap.SetResolution(144, 144)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(27, 40, 56))
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        foreach ($item in $layout) {
            $rectangle = [System.Drawing.RectangleF]::new(
                [float]$horizontalPadding,
                [float]$item.Y,
                [float]$contentWidth,
                [float]$item.Height
            )
            $graphics.DrawString($item.Text, $item.Font, $item.Brush, $rectangle, $format)
        }

        $outputPath = Join-Path $Readme.DirectoryName 'README.jpg'
        $temporaryPath = Join-Path $Readme.DirectoryName 'README.jpg.tmp'
        $codec = Get-JpegCodec
        if ($null -eq $codec) {
            throw 'The Windows JPEG encoder is not available.'
        }

        $usedQuality = $jpegQuality
        while ($true) {
            if (Test-Path -LiteralPath $temporaryPath) {
                Remove-Item -LiteralPath $temporaryPath -Force
            }

            $encoderParameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
            try {
                $encoderParameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new(
                    [System.Drawing.Imaging.Encoder]::Quality,
                    $usedQuality
                )
                $bitmap.Save($temporaryPath, $codec, $encoderParameters)
            }
            finally {
                $encoderParameters.Dispose()
            }

            $temporarySize = (Get-Item -LiteralPath $temporaryPath).Length
            if ($temporarySize -le $maximumJpegSize -or $usedQuality -le 60) {
                break
            }

            $usedQuality -= 4
        }

        Move-Item -LiteralPath $temporaryPath -Destination $outputPath -Force
        return [PSCustomObject]@{
            Mod = $Readme.Directory.Name
            Output = $outputPath
            Width = $imageWidth
            Height = $imageHeight
            Quality = $usedQuality
            SizeKiB = [Math]::Round((Get-Item -LiteralPath $outputPath).Length / 1KB, 1)
        }
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
        $format.Dispose()
        foreach ($font in $fonts.Values) { $font.Dispose() }
        foreach ($brush in $brushes.Values) { $brush.Dispose() }
    }
}

$readmes = @(
    Get-ChildItem -LiteralPath $workspaceRoot -Filter 'README.md' -File -Recurse |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.DirectoryName 'release.bat') } |
        Sort-Object FullName
)

if ($readmes.Count -eq 0) {
    throw "No directory containing both README.md and release.bat was found below $workspaceRoot"
}

$results = [System.Collections.Generic.List[object]]::new()
foreach ($readme in $readmes) {
    $markdown = [System.IO.File]::ReadAllText($readme.FullName, [System.Text.Encoding]::UTF8)
    $blocks = @(ConvertFrom-ReadmeMarkdown -Markdown $markdown)
    if ($blocks.Count -eq 0) {
        throw "README contains no renderable text: $($readme.FullName)"
    }

    $result = Write-ReadmeImage -Readme $readme -Blocks $blocks
    $results.Add($result)
    Write-Host ("Created {0} ({1} x {2}, JPEG quality {3}, {4} KiB)" -f $result.Output, $result.Width, $result.Height, $result.Quality, $result.SizeKiB)
}

Write-Host
Write-Host ("Created {0} README image(s)." -f $results.Count)
