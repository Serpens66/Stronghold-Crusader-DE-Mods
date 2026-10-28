$ErrorActionPreference = 'Stop'
# Phase 1 of ANCHOR_MODE_PLAN.md: builds the anchor-mode core into .local/anchor only.
# Nothing is copied to BepInEx; build.bat integration follows in Phase 3.
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$compilerRoot = (Get-ChildItem -LiteralPath 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC' -Directory | Sort-Object { [version]$_.Name } -Descending | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'bin/Hostx64/x64/cl.exe') } | Select-Object -First 1).FullName
$sdkRoot = 'C:\Program Files (x86)\Windows Kits\10'
$sdk = Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'Include') -Directory | Sort-Object Name -Descending | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'ucrt/stdio.h') } | Select-Object -First 1
$env:INCLUDE = (Join-Path $compilerRoot 'include') + ';' + (Join-Path $sdk.FullName 'ucrt') + ';' + (Join-Path $sdk.FullName 'shared') + ';' + (Join-Path $sdk.FullName 'um')
$env:LIB = (Join-Path $compilerRoot 'lib/x64') + ';' + (Join-Path $sdkRoot ('Lib/' + $sdk.Name + '/ucrt/x64')) + ';' + (Join-Path $sdkRoot ('Lib/' + $sdk.Name + '/um/x64'))
. (Join-Path $mod '../../APIShared/tools/BuildProof.ps1')
$output = Join-Path $mod '.local/anchor'
[IO.Directory]::CreateDirectory($output) | Out-Null
$source = Join-Path $PSScriptRoot 'MoatMoveAnchor.cpp'
$hash = Get-ApiFileHash $source
& (Join-Path $compilerRoot 'bin/Hostx64/x64/cl.exe') /nologo /std:c++17 /O2 /Ob2 /EHsc /MT /W4 /LD $source "/Fo$output/MoatMoveAnchor.obj" /link "/OUT:$output/MoatMoveAnchor.dll" "/IMPLIB:$output/MoatMoveAnchor.lib"
if ($LASTEXITCODE) { throw 'MoatMove anchor core compilation failed.' }
if ((Get-ApiFileHash $source) -cne $hash) { throw 'Anchor core source changed during compilation.' }
@{timestampUtc=[DateTime]::UtcNow.ToString('o');sourceSha256=$hash;dllSha256=(Get-ApiFileHash (Join-Path $output 'MoatMoveAnchor.dll'));compiler=$compilerRoot;windowsSdk=$sdk.Name;scope='MoatMove anchor core, phase 1 offline build; not installed'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build.json') -Encoding UTF8
Write-Host 'PASS: MoatMove anchor core built. Game directory unchanged.'
