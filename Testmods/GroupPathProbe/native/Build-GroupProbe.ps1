$ErrorActionPreference = 'Stop'
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$compilerRoot = (Get-ChildItem -LiteralPath 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Tools\MSVC' -Directory | Sort-Object { [version]$_.Name } -Descending | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'bin/Hostx64/x64/cl.exe') } | Select-Object -First 1).FullName
$sdkRoot = 'C:\Program Files (x86)\Windows Kits\10'
$sdk = Get-ChildItem -LiteralPath (Join-Path $sdkRoot 'Include') -Directory | Sort-Object Name -Descending | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'ucrt/stdio.h') } | Select-Object -First 1
$env:INCLUDE = (Join-Path $compilerRoot 'include') + ';' + (Join-Path $sdk.FullName 'ucrt') + ';' + (Join-Path $sdk.FullName 'shared') + ';' + (Join-Path $sdk.FullName 'um')
$env:LIB = (Join-Path $compilerRoot 'lib/x64') + ';' + (Join-Path $sdkRoot ('Lib/' + $sdk.Name + '/ucrt/x64')) + ';' + (Join-Path $sdkRoot ('Lib/' + $sdk.Name + '/um/x64'))
. (Join-Path $mod '../../APIShared/tools/BuildProof.ps1')
$output = Join-Path $mod '.local/native'
[IO.Directory]::CreateDirectory($output) | Out-Null
$source = Join-Path $PSScriptRoot 'GroupProbe.cpp'
$hash = Get-ApiFileHash $source
& (Join-Path $compilerRoot 'bin/Hostx64/x64/cl.exe') /nologo /std:c++17 /O2 /Ob2 /EHsc /MT /W4 /LD /DSERP_DEVDIAG=1 $source "/Fo$output/GroupProbe.obj" /link "/OUT:$output/GroupProbe.dll" "/IMPLIB:$output/GroupProbe.lib"
if ($LASTEXITCODE) { throw 'Offline group probe compilation failed.' }
if ((Get-ApiFileHash $source) -cne $hash) { throw 'Group probe source changed during compilation.' }
@{timestampUtc=[DateTime]::UtcNow.ToString('o');sourceSha256=$hash;dllSha256=(Get-ApiFileHash (Join-Path $output 'GroupProbe.dll'));compiler=$compilerRoot;windowsSdk=$sdk.Name;scope='offline group-planning probe only; never installed'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build.json') -Encoding UTF8
Write-Host 'PASS: offline group probe built. Game directory unchanged.'
