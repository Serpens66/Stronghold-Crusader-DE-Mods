[CmdletBinding()]
param([string]$AssemblyPath, [string]$ApiSharedDir, [string]$ExtenderDir, [switch]$RunNative)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $AssemblyPath) { $AssemblyPath = Join-Path $PSScriptRoot 'BepInEx/plugins/BugfixesAndQoL_Serp/BugfixesAndQoL.dll' }
$AssemblyPath = (Resolve-Path -LiteralPath $AssemblyPath).Path
if (-not $ApiSharedDir) { $ApiSharedDir = $env:API_SHARED_DIR }
if (-not $ApiSharedDir) { $ApiSharedDir = Join-Path $workspace 'APIShared/BepInEx/plugins/APIShared_Serp' }
if (-not $ExtenderDir) { $ExtenderDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese' }
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
& $msbuild (Join-Path $PSScriptRoot 'tests/Modsettings.Tests/Modsettings.Tests.csproj') /t:Build /p:Configuration=Release /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Modsettings production DLL test compilation failed.' }
& (Join-Path $PSScriptRoot 'tests/Modsettings.Tests/bin/Modsettings.Tests.exe') $workspace $AssemblyPath $ApiSharedDir
if ($LASTEXITCODE -ne 0) { throw 'Modsettings production DLL regression failed.' }
if ($RunNative) {
    & $msbuild (Join-Path $PSScriptRoot 'tests/ModsettingsNative.Tests/ModsettingsNative.Tests.csproj') /t:Build /p:Configuration=Release "/p:ExtenderDir=$ExtenderDir" /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Modsettings native test compilation failed.' }
    & (Join-Path $PSScriptRoot 'tests/ModsettingsNative.Tests/bin/ModsettingsNative.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Modsettings production native regression failed.' }
}
Write-Host 'PASS: production modsettings activation and ownership.'
