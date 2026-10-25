[CmdletBinding()]
param([switch]$Install, [string]$InstalledDirectory = '')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Package.Common.ps1')
$layout = Get-ExtraFeaturesPackageLayout
if ($Install) {
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Game is running.' }
    if (-not $InstalledDirectory) { throw 'Installation destination is required.' }
    Install-ExtraFeaturesPackage $layout -Destination $InstalledDirectory
} else { Assert-ExtraFeaturesPackage $layout -InstalledDirectory $InstalledDirectory }
Write-Host 'PASS: ExtraFeatures source/package/install hashes and manifest/DLL identity.'
