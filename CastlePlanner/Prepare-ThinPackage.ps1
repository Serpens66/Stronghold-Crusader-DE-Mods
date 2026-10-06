[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [switch]$ValidateOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$gamePlugins = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins'
$allowedDirectories = @(
    (Join-Path $projectRoot 'BepInEx\plugins\CastlePlanner_Serp'),
    (Join-Path $gamePlugins 'CastlePlanner_Serp'),
    (Join-Path $gamePlugins 'SerpsMods_Serp\Mods\CastlePlanner_Serp')
)
$target = [IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\')
if ($target -notin $allowedDirectories) { throw "Unexpected CastlePlanner package directory: $target" }
if (-not (Test-Path -LiteralPath $target -PathType Container)) {
    if ($ValidateOnly) { throw "Package directory is missing: $target" }
    return
}
if ((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw "Refusing to modify a redirected package directory: $target"
}

# The plugin and its explicitly private references define the owned assemblies.
# Extender/game references remain available for compilation and runtime loading.
[xml]$project = [IO.File]::ReadAllText((Join-Path $projectRoot 'CastlePlanner.csproj'))
$ownedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
[void]$ownedNames.Add('CastlePlanner')
foreach ($reference in @($project.SelectNodes("//*[local-name()='Reference']"))) {
    $private = $reference.SelectSingleNode("*[local-name()='Private']")
    if ($null -ne $private -and $private.InnerText -ceq 'true') {
        [void]$ownedNames.Add($reference.GetAttribute('Include'))
    }
}
if ($ownedNames.Count -ne 5) { throw 'The CastlePlanner thin-package assembly contract changed; review the build filter.' }
$files = @(Get-ChildItem -LiteralPath $target -File | Where-Object { $_.Extension -in @('.dll', '.pdb') })
$unexpected = @($files | Where-Object { -not $ownedNames.Contains($_.BaseName) })
if ($ValidateOnly) {
    if ($unexpected.Count -gt 0) { throw "Non-private assembly in thin package: $($unexpected.Name -join ', ')" }
    foreach ($name in $ownedNames) {
        if (-not (Test-Path -LiteralPath (Join-Path $target ($name + '.dll')) -PathType Leaf)) {
            throw "Required private assembly is missing: $name"
        }
    }
    Write-Host 'PASS: CastlePlanner package contains exactly its five own DLLs and no shared runtime copies.'
    return
}
foreach ($file in $unexpected) {
    if ($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Redirected package file: $($file.FullName)" }
    # Root files only: never recurse into resources or player-created settings.
    Remove-Item -LiteralPath $file.FullName -Force
}
Write-Host "Removed $($unexpected.Count) shared runtime copies from $target."
