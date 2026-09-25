param(
    [string]$Destination,
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$archivePath = Join-Path $PSScriptRoot 'EVIDENCE.zip'
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $manifestEntry = $archive.GetEntry('manifest.json')
    if ($null -eq $manifestEntry) { throw 'Archive has no manifest.' }
    $reader = [IO.StreamReader]::new($manifestEntry.Open(), [Text.Encoding]::UTF8)
    try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json }
    finally { $reader.Dispose() }
    if ($manifest.SchemaVersion -ne 1) { throw 'Unsupported manifest version.' }
    if (-not $VerifyOnly -and [string]::IsNullOrWhiteSpace($Destination)) {
        throw 'Specify a new destination directory or use -VerifyOnly.'
    }
    $targetRoot = if ($VerifyOnly) { '' } else { [IO.Path]::GetFullPath($Destination) }
    if (-not $VerifyOnly -and (Test-Path -LiteralPath $targetRoot)) {
        throw "Destination already exists: $targetRoot"
    }
    $blobs = @{}
    $paths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $manifest.Paths) {
        $relative = [string]$file.Path
        if ([IO.Path]::IsPathRooted($relative) -or $relative -match '(^|/)\.\.(/|$)' -or
            -not $paths.Add($relative)) { throw "Unsafe or repeated path: $relative" }
        $blobName = 'blobs/' + [string]$file.Sha256
        $entry = $archive.GetEntry($blobName)
        if ($null -eq $entry -or $entry.Length -ne [long]$file.Length) {
            throw "Missing or incorrectly sized blob: $blobName"
        }
        $blobs[[string]$file.Sha256] = $entry
        if (-not $VerifyOnly) {
            $target = [IO.Path]::GetFullPath((Join-Path $targetRoot $relative))
            if (-not $target.StartsWith($targetRoot.TrimEnd('\') + '\',
                    [StringComparison]::OrdinalIgnoreCase)) {
                throw "Path escaped destination: $relative"
            }
        }
    }
    $checked = 0
    foreach ($pair in $blobs.GetEnumerator()) {
        $stream = $pair.Value.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actual = [Convert]::ToHexString($sha.ComputeHash($stream)) }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($actual -ne $pair.Key) { throw "Blob hash mismatch: $($pair.Key)" }
        $checked++
        if ($checked % 250 -eq 0) { Write-Output "Verified $checked/$($blobs.Count) blobs" }
    }
    if (-not $VerifyOnly) {
        [void][IO.Directory]::CreateDirectory($targetRoot)
        $restored = 0
        foreach ($file in $manifest.Paths) {
            $target = [IO.Path]::GetFullPath((Join-Path $targetRoot ([string]$file.Path)))
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            $source = $blobs[[string]$file.Sha256].Open()
            $output = [IO.File]::Create($target)
            try { $source.CopyTo($output) }
            finally { $output.Dispose(); $source.Dispose() }
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.Sha256) {
                throw "Restored file hash mismatch: $target"
            }
            $restored++
            if ($restored % 250 -eq 0) { Write-Output "Restored $restored/$($manifest.Paths.Count) paths" }
        }
    }
    Write-Output "Verified $($manifest.Paths.Count) paths and $($blobs.Count) unique blobs."
}
finally { $archive.Dispose() }
