[CmdletBinding()]
param([string]$SourceDirectory, [string]$PackageDirectory, [string]$ReferencePackage, [switch]$Installed)
$ErrorActionPreference = 'Stop'
if (-not $SourceDirectory) { $SourceDirectory = $PSScriptRoot }
$SourceDirectory = [IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\','/')
if (-not $PackageDirectory) { $PackageDirectory = Join-Path $SourceDirectory 'BepInEx/plugins/BugfixesAndQoL_Serp' }
$PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\','/')
. (Join-Path $PSScriptRoot '../Shared/Tools/Release/Release.Common.ps1')

function Assert-PlainTree([string]$Directory) {
    for ($part = [IO.Path]::GetFullPath($Directory); $part; $part = [IO.Path]::GetDirectoryName($part)) {
        if ((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Linked package/resource path: $part"
        }
    }
    if (-not [IO.Directory]::Exists($Directory)) { throw "Missing package/resource directory: $Directory" }
    foreach ($item in Get-ChildItem -LiteralPath $Directory -Force) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked package/resource entry: $($item.FullName)" }
        if ($item.PSIsContainer) { Assert-PlainTree $item.FullName }
    }
}
function Assert-SameFile([string]$Source, [string]$Target) {
    if (-not [IO.File]::Exists($Target)) { throw "Missing package file: $Target" }
    # Shared SHA-256 implementation; timestamps are deliberately not evidence of matching content.
    if ((Get-Sha256Hex -Path $Source) -cne (Get-Sha256Hex -Path $Target)) {
        throw "Resource/package hash mismatch: $Target"
    }
}

Assert-PlainTree $PackageDirectory
$expected = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[void]$expected.Add('info.json')
foreach ($folder in @('Patches','Override','Locales')) {
    $sourceRoot = Join-Path $SourceDirectory $folder
    Assert-PlainTree $sourceRoot
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File) {
        $relative = $folder + '/' + $file.FullName.Substring($sourceRoot.Length + 1).Replace('\','/')
        [void]$expected.Add($relative)
    }
}
foreach ($relative in $expected) {
    Assert-SameFile (Join-Path $SourceDirectory $relative) (Join-Path $PackageDirectory $relative)
}
if (-not $Installed) {
    # Extra generated resources can resurrect removed code. Keep historical paths as explicit
    # source tombstones instead of silently shipping stale files or deleting them here.
    foreach ($folder in @('Patches','Override','Locales')) {
        $targetRoot = Join-Path $PackageDirectory $folder
        if (-not [IO.Directory]::Exists($targetRoot)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $targetRoot -Recurse -File) {
            $relative = $folder + '/' + $file.FullName.Substring($targetRoot.Length + 1).Replace('\','/')
            if (-not $expected.Contains($relative)) { throw "Unclaimed generated resource; retain a reviewed source replacement: $relative" }
        }
    }
}
$verifiedPackageFiles = 0
if ($ReferencePackage) {
    $ReferencePackage = [IO.Path]::GetFullPath($ReferencePackage).TrimEnd('\','/')
    Assert-PlainTree $ReferencePackage
    foreach ($file in Get-ChildItem -LiteralPath $ReferencePackage -Recurse -File) {
        $relative = $file.FullName.Substring($ReferencePackage.Length + 1)
        Assert-SameFile $file.FullName (Join-Path $PackageDirectory $relative)
        $verifiedPackageFiles++
    }
}
Write-Host "PASS: $($expected.Count) source resources match the package; $verifiedPackageFiles reference-package files verified. No files removed."
