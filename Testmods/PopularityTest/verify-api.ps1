[CmdletBinding()]
param([string]$ApiSharedDir = $env:SHCDE_API_SHARED_DIR)
$ErrorActionPreference = 'Stop'
if (-not $ApiSharedDir) { $ApiSharedDir = Join-Path $PSScriptRoot '../../APIShared/BepInEx/plugins/APIShared_Serp' }
$apiFile = Join-Path $ApiSharedDir 'APIShared.dll'
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Raw | ConvertFrom-Json
$minimum = [version]($manifest.Dependencies | Where-Object GUID -eq 'APIShared_Serp').MinimumVersion
if ([Reflection.AssemblyName]::GetAssemblyName($apiFile).Version -lt $minimum) { throw "Build the selected APIShared development SDK first (minimum $minimum)." }
Write-Host "PASS: selected APIShared SDK $apiFile; no dependency build or installation."
