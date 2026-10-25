[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$python = 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe'
& $python (Join-Path $PSScriptRoot 'tests/verify-popularity.py')
if ($LASTEXITCODE -ne 0) { throw 'Popularity asset/source validation failed.' }
& dotnet build (Join-Path $PSScriptRoot 'tests/RuntimeSignatures.csproj') --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Signature checker compilation failed.' }
& dotnet (Join-Path $PSScriptRoot 'tests/bin/Debug/net10.0/RuntimeSignatures.dll') $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Actual SDK/game signatures or contextual XAML bindings failed.' }
& dotnet msbuild (Join-Path $PSScriptRoot 'tests/ExtraFeatures.PresetTests.csproj') /t:Build /p:Configuration=Release /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'ExtraFeatures preset fixture compilation failed.' }
# This fixture exercises atomic File.Replace and is run outside the sandbox.
& (Join-Path $PSScriptRoot 'tests/bin/PresetTests/ExtraFeatures.PresetTests.exe') $PSScriptRoot
if ($LASTEXITCODE -ne 0) { throw 'Popularity/ExtraFeatures preset, authority, lifecycle or ownership checks failed.' }
& (Join-Path $PSScriptRoot 'tests/Test-Package.ps1')
if (-not $?) { throw 'Source/package/install regression failed.' }
