$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'tools/Package.Common.ps1')
$gamePlugin = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\PopularityTest_Serp'
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Game is running.' }
$sdk = & (Join-Path $PSScriptRoot 'verify-api.ps1')
$env:SHCDE_API_SHARED_DIR = $sdk.Directory
$env:SHCDE_POPULARITY_API_SHA256 = $sdk.Sha256
$layout = Get-PopularityPackageLayout -ModDirectory $PSScriptRoot -ApiSharedDir $sdk.Directory
Install-PopularityPackage $layout -Destination $gamePlugin
$null = & (Join-Path $PSScriptRoot 'verify-api.ps1')
Write-Host 'PASS: PopularityTest source/package/installation hashes, DLL/manifest identity and unchanged selected dependency.'
