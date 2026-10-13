[CmdletBinding()]
param([Parameter(Mandatory)][string]$GameDir)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\..\Shared\Tools\Release\Release.Common.ps1')
$source = Join-Path $PSScriptRoot 'BepInEx\plugins\FarmBuildPreviewTest_Serp'
$target = Join-Path $GameDir 'BepInEx\plugins\FarmBuildPreviewTest_Serp'
$names = @('FarmBuildPreviewTest.dll', 'FarmBuildPreviewTest.pdb', 'info.json')
$names += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Locales') -File -Filter '*.txt' | ForEach-Object { 'Locales\' + $_.Name })
foreach ($name in $names) { if (-not (Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) { throw "Missing package file $name" } }
$metadata = Get-Content -LiteralPath (Join-Path $source 'info.json') -Raw | ConvertFrom-Json
if ($metadata.GUID -ne 'FarmBuildPreviewTest_Serp' -or $metadata.Version -ne '0.1.0' -or $metadata.NetworkMode -ne 0) { throw 'Package metadata differs.' }
if (@($metadata.Dependencies | Where-Object { $_.GUID -eq 'APIShared_Serp' -and $_.MinimumVersion -eq '0.9.0' }).Count -ne 1) { throw 'Missing APIShared dependency.' }
$assembly = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $source 'FarmBuildPreviewTest.dll'))
$manifestVersion = [version]$metadata.Version
$expectedAssemblyVersion = [version]::new($manifestVersion.Major, $manifestVersion.Minor, [Math]::Max(0, $manifestVersion.Build), [Math]::Max(0, $manifestVersion.Revision))
if ($assembly.Version -ne $expectedAssemblyVersion) { throw 'Assembly and manifest versions differ.' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
foreach ($name in $names) {
    $original = Join-Path $source $name
    $installed = Join-Path $target $name
    New-Item -ItemType Directory -Path (Split-Path -Parent $installed) -Force | Out-Null
    Copy-Item -LiteralPath $original -Destination $installed -Force
    if ((Get-Sha256Hex -Path $original) -cne (Get-Sha256Hex -Path $installed)) { throw "Installed hash differs: $name" }
}
# Replaced by APIShared's shared host; do not keep the stale independently loaded patch.
foreach ($packageRoot in @($source, $target)) {
    $obsoletePatch = Join-Path $packageRoot 'Patches\Assets\GUI\XAML\MainHUD.xaml'
    if (Test-Path -LiteralPath $obsoletePatch -PathType Leaf) { Remove-Item -LiteralPath $obsoletePatch -Force }
}
Write-Host 'PASS: package identities, assembly/manifest version and installed file SHA-256 hashes.'
