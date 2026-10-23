[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][string]$PluginRoot,
    [Parameter(Mandatory)][string]$Destination
)
$ErrorActionPreference = 'Stop'

function Assert-UnlinkedPath([string]$Path) {
    for ($part = $Path; $part; $part = [IO.Path]::GetDirectoryName($part)) {
        if ((Test-Path -LiteralPath $part) -and ((Get-Item -LiteralPath $part -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Linked installation/package path: $part"
        }
    }
}

$package = [IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\','/')
$plugins = [IO.Path]::GetFullPath($PluginRoot).TrimEnd('\','/')
$target = [IO.Path]::GetFullPath($Destination).TrimEnd('\','/')
if ([IO.Path]::GetDirectoryName($target) -ine $plugins -or
    [IO.Path]::GetFileName($target) -notin @('SerpsMods_Serp','SerpsMods_Serp_dev')) {
    throw 'Installation must target the exact main or dev host directory immediately below PluginRoot.'
}
if ($package -ieq $target -or $package.StartsWith($target + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $target.StartsWith($package + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Package and installation directories must not overlap.'
}
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before installing.' }
foreach ($path in @($package,$plugins,$target)) { Assert-UnlinkedPath $path }
foreach ($name in @('SerpsModsHost.dll','info.json','serps-modpack.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $package $name) -PathType Leaf)) { throw "Missing host package file: $name" }
}
$protected = @('LobbyModSettings','Infrastructure')
foreach ($name in $protected) {
    if (Test-Path -LiteralPath (Join-Path $package $name)) { throw "Host package must not replace independently owned data: $name" }
}
# Validate every entry before deleting anything, including descendants of a directory
# that will be removed. No junction may redirect cleanup or copying elsewhere.
foreach ($root in @($package,$target)) {
    if (Test-Path -LiteralPath $root) {
        foreach ($entry in Get-ChildItem -LiteralPath $root -Recurse -Force) {
            if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked package/install entry: $($entry.FullName)" }
        }
    }
}
[void][IO.Directory]::CreateDirectory($target)
foreach ($entry in Get-ChildItem -LiteralPath $target -Force) {
    if ($entry.PSIsContainer -and $entry.Name -in $protected) { continue }
    $resolved = [IO.Path]::GetFullPath($entry.FullName)
    if ([IO.Path]::GetDirectoryName($resolved) -ine $target) { throw 'Cleanup target escapes the host directory.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
foreach ($entry in Get-ChildItem -LiteralPath $package -Force) {
    Copy-Item -LiteralPath $entry.FullName -Destination $target -Recurse -Force
}
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] Host installed: {1}' -f [DateTime]::Now,$target)
