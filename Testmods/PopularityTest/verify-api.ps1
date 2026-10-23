[CmdletBinding()]
param([string]$ApiSharedDir = $env:SHCDE_API_SHARED_DIR)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Release/Release.Common.ps1')
if (-not $ApiSharedDir) { $ApiSharedDir = Join-Path $workspace 'APIShared/BepInEx/plugins/APIShared_Serp' }
$ApiSharedDir = [IO.Path]::GetFullPath($ApiSharedDir).TrimEnd('\','/')
$installedDev = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\APIShared_Serp_dev'
# Explicit selection of the installed dev SDK is local to this testmod. Other
# package selections retain the workspace's normal source/provenance validation.
if ($ApiSharedDir -ine $installedDev) {
    & (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/Test-ConsumerPackage.ps1') -Workspace $workspace -PackageDirectory $ApiSharedDir
    if (-not $?) { throw 'Selected APIShared package validation failed.' }
}
$apiFile = Join-Path $ApiSharedDir 'APIShared.dll'
$identity = [Reflection.AssemblyName]::GetAssemblyName($apiFile)
$info = Get-Content -LiteralPath (Join-Path $ApiSharedDir 'info.json') -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Raw | ConvertFrom-Json
$dependency = @($manifest.Dependencies | Where-Object GUID -eq 'APIShared_Serp')
if ($dependency.Count -ne 1) { throw 'Exactly one APIShared dependency is required.' }
$minimum = [version]$dependency[0].MinimumVersion
if ($identity.Name -cne 'APIShared' -or $info.GUID -cne 'APIShared_Serp' -or
    $identity.Version -lt $minimum -or [version]$info.Version -lt $minimum -or
    $identity.Version.ToString(3) -ne ([version]$info.Version).ToString(3)) { throw 'APIShared DLL/manifest identity or minimum version mismatch.' }
if ($dependency[0].MaximumVersion -and $identity.Version -gt [version]$dependency[0].MaximumVersion) { throw 'APIShared exceeds declared maximum version.' }
$hash = Get-Sha256Hex $apiFile
if ($env:SHCDE_POPULARITY_API_SHA256 -and $hash -ine $env:SHCDE_POPULARITY_API_SHA256) { throw 'Selected APIShared DLL changed during the build/install run.' }
Write-Host "PASS: APIShared $($identity.Version); $apiFile; SHA256=$hash"
[pscustomobject]@{ Directory=$ApiSharedDir; Version=$identity.Version.ToString(); Sha256=$hash }
