param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $mod 'BepInEx/plugins/BetterPathfindingTest_Serp' }
$compilerRoot = (Get-ChildItem -LiteralPath 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC' -Directory | Sort-Object { [version]$_.Name } -Descending | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'bin/Hostx64/x64/cl.exe') } | Select-Object -First 1).FullName
$sdkRoot = 'C:\Program Files (x86)\Windows Kits\10'
$sdk = Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'Include') -Directory | Sort-Object Name -Descending | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'ucrt/stdio.h') } | Select-Object -First 1
if (-not $sdk -or -not (Test-Path -LiteralPath (Join-Path $compilerRoot 'bin/Hostx64/x64/cl.exe'))) { throw 'Installed x64 C++ compiler/Windows SDK unavailable.' }
$env:INCLUDE = (Join-Path $compilerRoot 'include') + ';' + (Join-Path $sdk.FullName 'ucrt') + ';' + (Join-Path $sdk.FullName 'shared') + ';' + (Join-Path $sdk.FullName 'um')
$env:LIB = (Join-Path $compilerRoot 'lib/x64') + ';' + (Join-Path $sdkRoot ('Lib/' + $sdk.Name + '/ucrt/x64')) + ';' + (Join-Path $sdkRoot ('Lib/' + $sdk.Name + '/um/x64'))
. (Join-Path $mod '../../APIShared/tools/BuildProof.ps1')
$sourcePath = Join-Path $PSScriptRoot 'Navigation.cpp'
$sourceHash = Get-ApiFileHash $sourcePath
$objects = Join-Path $mod '.local/native'
[IO.Directory]::CreateDirectory($objects) | Out-Null
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
& (Join-Path $compilerRoot 'bin/Hostx64/x64/cl.exe') /nologo /std:c++17 /O2 /Ob2 /EHsc /MT /W4 /LD /DSERP_DEVDIAG=1 (Join-Path $PSScriptRoot 'Navigation.cpp') "/Fo$objects/Navigation.obj" /link "/OUT:$OutputDirectory/BetterPathfindingNative.dll" "/IMPLIB:$objects/BetterPathfindingNative.lib"
if ($LASTEXITCODE) { throw 'Native navigation compilation failed.' }
Write-Host 'PASS: private x64 native navigation backend built; static CRT; no game imports.'

if ((Get-ApiFileHash $sourcePath) -cne $sourceHash) { throw 'Native source changed during compilation.' }
@{ timestampUtc=[DateTime]::UtcNow.ToString('o'); sourcePath=$sourcePath; sourceSha256=$sourceHash; dllSha256=(Get-ApiFileHash (Join-Path $OutputDirectory 'BetterPathfindingNative.dll')); compiler=$compilerRoot; windowsSdk=$sdk.Name; diagnostic=$true; flags='/std:c++17 /O2 /Ob2 /EHsc /MT /W4 /LD'; abi='0x04003828' } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $objects 'build.json') -Encoding UTF8