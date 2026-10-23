[CmdletBinding()]
param([string]$ApiSharedDirectory = $env:SHCDE_API_SHARED_DIR)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $ApiSharedDirectory) { $ApiSharedDirectory = Join-Path $workspace 'APIShared\BepInEx\plugins\APIShared_Serp' }
$ApiSharedDirectory = [IO.Path]::GetFullPath($ApiSharedDirectory)
# Provenance belongs to the selected build, never to a historic SDK file hash.
& (Join-Path $workspace 'Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1') -Workspace $workspace -PackageDirectory $ApiSharedDirectory
if (-not $?) { throw 'The selected APIShared package lacks valid build provenance.' }
$manifest = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'info.json')) | ConvertFrom-Json
$dependencies = @($manifest.Dependencies | Where-Object { $_.GUID -eq 'APIShared_Serp' })
if ($dependencies.Count -ne 1) { throw 'Expected one declared APIShared dependency.' }
$minimum = [Version]$dependencies[0].MinimumVersion
$assemblyPath = Join-Path $ApiSharedDirectory 'APIShared.dll'
$identity = [Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
$package = [IO.File]::ReadAllText((Join-Path $ApiSharedDirectory 'info.json')) | ConvertFrom-Json
if ($identity.Name -cne 'APIShared' -or $package.GUID -cne 'APIShared_Serp' -or [Version]$package.Version -lt $minimum) {
    throw "Selected SDK must be APIShared >= $minimum; found $identity"
}
Write-Host ("PASS: selected APIShared {0} >= {1}; required public members are checked by production compilation, not an exact version/hash pin. SDK={2}" -f $package.Version,$minimum,$assemblyPath)
