[CmdletBinding()]
param([int]$NoInstall = 0)
$ErrorActionPreference = 'Stop'
$mod = Split-Path -Parent $PSScriptRoot
$sdk = & (Join-Path $mod 'verify-api.ps1')
$env:SHCDE_API_SHARED_DIR = $sdk.Directory
$env:SHCDE_POPULARITY_API_SHA256 = $sdk.Sha256
if (-not $NoInstall -and (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue)) { throw 'Close the game before installation.' }
& (Join-Path $mod 'verify.ps1')
if (-not $?) { throw 'PopularityTest verification failed.' }
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' (Join-Path $mod 'PopularityTest.csproj') /p:Configuration=Debug "/p:ApiSharedDir=$($sdk.Directory)" /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'PopularityTest compilation failed.' }
$output = Join-Path $mod 'BepInEx/plugins/PopularityTest_Serp'
Copy-Item -LiteralPath (Join-Path $mod 'info.json') -Destination (Join-Path $output 'info.json') -Force
if (Test-Path -LiteralPath (Join-Path $output 'APIShared.dll')) { throw 'Consumer package must not contain APIShared.dll.' }
$null = & (Join-Path $mod 'verify-api.ps1') -ApiSharedDir $sdk.Directory
if (-not $NoInstall) { & (Join-Path $mod 'install.ps1') }
$null = & (Join-Path $mod 'verify-api.ps1') -ApiSharedDir $sdk.Directory
Write-Host "PASS: PopularityTest built; installation skipped=$([bool]$NoInstall); selected SDK unchanged."
