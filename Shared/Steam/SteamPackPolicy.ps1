Set-StrictMode -Version Latest

function Get-SteamPackFileSha256 {
    param([Parameter(Mandatory)][string]$Path)

    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    } finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

function New-SteamPackReleaseZip {
    param(
        [Parameter(Mandatory)][string]$PackageDirectory,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    if (-not (Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
        throw "Pack release directory is missing: $PackageDirectory"
    }
    if (Test-Path -LiteralPath $DestinationPath) {
        throw "Pack release ZIP destination already exists: $DestinationPath"
    }
    Compress-Archive -LiteralPath $PackageDirectory -DestinationPath $DestinationPath -CompressionLevel Optimal

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $rootName = (Get-Item -LiteralPath $PackageDirectory).Name
    $expected = @(Get-ChildItem -LiteralPath $PackageDirectory -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($PackageDirectory.Length).TrimStart('\').Replace('\', '/')
        "$rootName/$relative|$(Get-SteamPackFileSha256 -Path $_.FullName)|$($_.Length)"
    } | Sort-Object)
    if ($expected.Count -eq 0) { throw 'Pack release directory is empty.' }
    $actual = [System.Collections.Generic.List[string]]::new()
    $fileRecords = [System.Collections.Generic.List[object]]::new()
    $stream = [IO.File]::OpenRead($DestinationPath)
    $archive = $null
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read)
        foreach ($entry in @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })) {
            $path = $entry.FullName.Replace('\', '/')
            if (-not $path.StartsWith("$rootName/", [StringComparison]::Ordinal) -or
                @($path.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0) {
                throw "Pack release ZIP contains an invalid path: $path"
            }
            $entryStream = $entry.Open()
            $algorithm = [Security.Cryptography.SHA256]::Create()
            try {
                $hash = ([BitConverter]::ToString($algorithm.ComputeHash($entryStream))).Replace('-', '').ToLowerInvariant()
            } finally {
                $algorithm.Dispose()
                $entryStream.Dispose()
            }
            $actual.Add("$path|$hash|$([long]$entry.Length)")
            $fileRecords.Add([PSCustomObject]@{ Path = $path; Sha256 = $hash; Size = [long]$entry.Length })
        }
    } finally {
        if ($null -ne $archive) { $archive.Dispose() }
        $stream.Dispose()
    }
    $actualRecords = @($actual | Sort-Object)
    if (($expected -join "`n") -cne ($actualRecords -join "`n")) {
        throw 'Pack release ZIP audit failed: archive contents differ from the staged package.'
    }
    return [PSCustomObject]@{
        File = [IO.Path]::GetFileName($DestinationPath)
        RootDirectory = $rootName
        Sha256 = Get-SteamPackFileSha256 -Path $DestinationPath
        Size = (Get-Item -LiteralPath $DestinationPath).Length
        Files = @($fileRecords | Sort-Object Path)
    }
}

function Resolve-SteamPackVersion {
    param(
        [AllowNull()][string]$PreviousVersion,
        [Parameter(Mandatory)][string]$PreparedVersion
    )
    $pattern = '^\d+\.\d+\.\d+$'
    if ($PreparedVersion -notmatch $pattern -or
        (-not [string]::IsNullOrWhiteSpace($PreviousVersion) -and $PreviousVersion -notmatch $pattern)) {
        throw "Steam pack versions must use major.minor.patch: previous='$PreviousVersion', prepared='$PreparedVersion'."
    }
    $prepared = [version]$PreparedVersion
    $minimum = if ([string]::IsNullOrWhiteSpace($PreviousVersion)) {
        [version]'1.0.0'
    } else {
        $previous = [version]$PreviousVersion
        [version]("{0}.{1}.{2}" -f $previous.Major, $previous.Minor, ($previous.Build + 1))
    }
    return $(if ($prepared -gt $minimum) { $prepared.ToString(3) } else { $minimum.ToString(3) })
}

function Resolve-ApiSharedReleaseAction {
    param(
        [Parameter(Mandatory)][string]$PreparedVersion,
        [AllowNull()][string]$PublishedVersion,
        [bool]$PublishedContentIsCurrent = $false,
        [bool]$PublishedArtifactIsValid = $false
    )

    if ($PreparedVersion -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
        throw "Invalid prepared APIShared version '$PreparedVersion'."
    }
    if ([string]::IsNullOrWhiteSpace($PublishedVersion)) {
        return [pscustomobject]@{ NeedsRelease = $true; Reason = 'No published APIShared release exists.' }
    }
    if ($PublishedVersion -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
        throw "Invalid published APIShared version '$PublishedVersion'."
    }

    $prepared = [version]($PreparedVersion -replace '[-+].*$', '')
    $published = [version]($PublishedVersion -replace '[-+].*$', '')
    if ($prepared -lt $published) {
        throw "Prepared APIShared version $PreparedVersion is older than published $PublishedVersion."
    }
    if ($prepared -gt $published) {
        return [pscustomobject]@{ NeedsRelease = $true; Reason = "Prepared APIShared v$PreparedVersion is newer than published v$PublishedVersion." }
    }
    if (-not $PublishedContentIsCurrent) {
        throw "APIShared has changed packaged content but no prepared higher version/changelog."
    }
    if (-not $PublishedArtifactIsValid) {
        throw "Published APIShared/v$PreparedVersion is missing or invalid and cannot be replaced automatically."
    }
    return [pscustomobject]@{ NeedsRelease = $false; Reason = "Published APIShared/v$PreparedVersion is current and valid." }
}

function Get-MissingSteamPackPaths {
    param(
        [AllowEmptyCollection()][string[]]$PreviousPaths = @(),
        [AllowEmptyCollection()][string[]]$CurrentPaths = @()
    )

    $current = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in @($CurrentPaths)) {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        [void]$current.Add($path.Replace('\','/').TrimStart('/'))
    }

    $missing = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in @($PreviousPaths)) {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        $normalized = $path.Replace('\','/').TrimStart('/')
        if (-not $current.Contains($normalized)) { [void]$missing.Add($normalized) }
    }

    return @($missing | Sort-Object)
}

function Assert-SteamArchivePath {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$PackGuid)

    $normalized = $Path.Replace('\','/').TrimStart('/')
    if ([string]::IsNullOrWhiteSpace($normalized) -or [IO.Path]::IsPathRooted($Path) -or
        @($normalized.Split('/') | Where-Object { $_ -eq '..' -or $_ -eq '.' -or $_ -eq '' }).Count -gt 0) {
        throw "Unsafe Steam archive path: $Path"
    }
    $root = "BepInEx/plugins/$PackGuid"
    if (-not $normalized.StartsWith($root + '/', [StringComparison]::OrdinalIgnoreCase) -and
        -not [string]::Equals($normalized, $root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Steam archive path is outside $root`: $Path"
    }
    return $normalized
}

function Get-SteamMissingPathAction {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$PackGuid)

    $normalized = Assert-SteamArchivePath -Path $Path -PackGuid $PackGuid
    if ($normalized -match '(?i)/Patches/.+\.xaml$') { return 'XamlTombstone' }
    if ($normalized -match '(?i)\.pdb$' -or $normalized -match '(?i)/(?:docs?|examples?)/' -or
        [IO.Path]::GetFileName($normalized) -match '(?i)^readme(?:\..+)?$') { return 'Retain' }
    return 'Explicit'
}

function Get-SteamXamlTombstoneText {
    return '<?xml version="1.0" encoding="utf-8"?>' + "`r`n" +
        '<Patch xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />' + "`r`n"
}

function Write-SteamXamlTombstone {
    param([Parameter(Mandatory)][string]$Path)

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory)) { [void](New-Item -ItemType Directory -Path $directory -Force) }
    $text = Get-SteamXamlTombstoneText
    [IO.File]::WriteAllText($Path, $text, [Text.UTF8Encoding]::new($false))
    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -ne 106 -or [IO.File]::ReadAllText($Path) -cne $text) {
        throw "XAML tombstone write verification failed: $Path"
    }
}

function Find-SteamMovedPath {
    param(
        [Parameter(Mandatory)][string]$OldPath,
        [Parameter(Mandatory)][string]$OldSha256,
        [AllowEmptyCollection()][array]$CurrentRecords = @()
    )
    $matches = @($CurrentRecords | Where-Object {
        [string]$_.Path -cne $OldPath -and [string]$_.Sha256 -ceq $OldSha256
    } | Select-Object -ExpandProperty Path)
    if ($matches.Count -eq 1) { return [string]$matches[0] }
    return $null
}

function Assert-ScriptExtenderXamlPatchContract {
    param([Parameter(Mandatory)][string]$Directory)
    $patchRoot = Join-Path $Directory 'Patches'
    if (-not (Test-Path -LiteralPath $patchRoot -PathType Container)) { return 0 }
    $structuralTypes = @('Add', 'InsertBefore', 'InsertAfter', 'Replace')
    $validated = 0
    foreach ($file in @(Get-ChildItem -LiteralPath $patchRoot -Filter '*.xaml' -File -Recurse)) {
        try { [xml]$document = Get-Content -LiteralPath $file.FullName -Raw }
        catch { throw "Invalid XAML patch XML '$($file.FullName)': $($_.Exception.Message)" }
        if ($null -eq $document.DocumentElement -or $document.DocumentElement.LocalName -cne 'Patch') {
            throw "XAML patch has no Patch root: $($file.FullName)"
        }
        foreach ($operation in @($document.DocumentElement.ChildNodes | Where-Object {
            $_.NodeType -eq [Xml.XmlNodeType]::Element -and $_.LocalName -ceq 'Operation'
        })) {
            $type = $operation.GetAttribute('Type')
            if ($type -notin $structuralTypes) { continue }
            $contentNode = $null
            $contentNodeCount = 0
            foreach ($child in $operation.ChildNodes) {
                if ($child.NodeType -eq [Xml.XmlNodeType]::Element -and $child.LocalName -ceq 'Content') {
                    $contentNode = $child
                    $contentNodeCount++
                }
            }
            $contentElementCount = 0
            if ($contentNodeCount -eq 1) {
                foreach ($child in $contentNode.ChildNodes) {
                    if ($child.NodeType -eq [Xml.XmlNodeType]::Element) { $contentElementCount++ }
                }
            }
            if ($contentNodeCount -ne 1 -or $contentElementCount -ne 1) {
                throw "Script Extender XAML operation '$type' must contain exactly one direct Content root element: $($file.FullName)"
            }
        }
        $validated++
    }
    return $validated
}
