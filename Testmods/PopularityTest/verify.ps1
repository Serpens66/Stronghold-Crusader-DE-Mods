[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] PopularityTest verification starts' -f [DateTime]::Now)
$null = & (Join-Path $PSScriptRoot 'verify-api.ps1')
if (-not $?) { throw 'Selected dependency verification failed.' }
& $python (Join-Path $PSScriptRoot 'tests\verify-source.py')
if ($LASTEXITCODE -ne 0) { throw 'Catalog, UI or dependency identity verification failed.' }
& (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods\PopularityTest' -ConsumerOnly
if (-not $?) { throw 'Scoped Shared boundaries/dependency verification failed.' }
& dotnet build (Join-Path $PSScriptRoot 'tests\RuntimeSignatures.csproj') --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Signature checker compilation failed.' }
& dotnet (Join-Path $PSScriptRoot 'tests\bin\Debug\net10.0\RuntimeSignatures.dll') $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Actual installed API / public game assembly signature check failed.' }
& dotnet msbuild (Join-Path $PSScriptRoot 'tests\PopularityTest.PresetTests.csproj') /t:Build /p:Configuration=Release /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Preset/settings integration test compilation failed.' }
# Run this verifier elevated: the preset harness exercises atomic File.Replace.
& (Join-Path $PSScriptRoot 'tests\bin\PresetTests\PopularityTest.PresetTests.exe') $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Preset/settings/ownership integration regression failed.' }
& (Join-Path $PSScriptRoot 'tests/Test-Package.ps1')
if (-not $?) { throw 'Source/package/install regression failed.' }
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] PASS: PopularityTest preflight and relevant integration checks.' -f [DateTime]::Now)
