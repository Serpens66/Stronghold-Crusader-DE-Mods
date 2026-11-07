param([string]$OutputDirectory, [switch]$CopyOnly)
$ErrorActionPreference = 'Stop'
# Builds the native weighted Assassin search (PathfindingAssassin.cpp) into obj/native (offline tests and
# packaging). -OutputDirectory additionally copies the DLL there (build.bat: package folder);
# -CopyOnly copies the existing build after verifying it still matches the source. Objects, import
# library and the build proof stay in obj/native and are never packaged.
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $mod '../APIShared/tools/BuildProof.ps1')
$work = Join-Path $mod 'obj/native'
$source = Join-Path $PSScriptRoot 'PathfindingAssassin.cpp'
$dll = Join-Path $work 'PathfindingAssassin.dll'
$proof = Join-Path $work 'build.json'
if ($CopyOnly) {
    if (-not $OutputDirectory) { throw '-CopyOnly requires -OutputDirectory.' }
    if (-not (Test-Path -LiteralPath $dll -PathType Leaf) -or -not (Test-Path -LiteralPath $proof -PathType Leaf)) { throw 'No Assassin search build to copy.' }
    $recorded = Get-Content -LiteralPath $proof -Raw | ConvertFrom-Json
    if ($recorded.sourceSha256 -cne (Get-ApiFileHash $source) -or $recorded.dllSha256 -cne (Get-ApiFileHash $dll)) { throw 'Assassin search build does not match the current source.' }
} else {
    . (Join-Path $mod '../Shared/Tools/BuildCache/MsvcToolchain.ps1')
    $toolchain = Get-MsvcToolchain
    $compilerRoot = $toolchain.CompilerRoot
    $sdk = $toolchain.Sdk
    $env:INCLUDE = $toolchain.IncludeDirectories -join ';'
    $env:LIB = $toolchain.LibraryDirectories -join ';'
    [IO.Directory]::CreateDirectory($work) | Out-Null
    $hash = Get-ApiFileHash $source
    & (Join-Path $compilerRoot 'bin/Hostx64/x64/cl.exe') /nologo /std:c++17 /O2 /Ob2 /EHsc /MT /W4 /LD $source "/Fo$work/PathfindingAssassin.obj" /link "/OUT:$dll" "/IMPLIB:$work/PathfindingAssassin.lib"
    if ($LASTEXITCODE) { throw 'Assassin search compilation failed.' }
    if ((Get-ApiFileHash $source) -cne $hash) { throw 'Assassin search source changed during compilation.' }
    @{timestampUtc=[DateTime]::UtcNow.ToString('o');sourceSha256=$hash;dllSha256=(Get-ApiFileHash $dll);dll=$dll;compiler=$compilerRoot;windowsSdk=$sdk.Name;scope='Pathfinding native Assassin search'} | ConvertTo-Json | Set-Content -LiteralPath $proof -Encoding UTF8
    Write-Host "PASS: Assassin search built: $dll"
}
if ($OutputDirectory) {
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    [IO.Directory]::CreateDirectory($output) | Out-Null
    $target = Join-Path $output 'PathfindingAssassin.dll'
    Copy-Item -LiteralPath $dll -Destination $target -Force
    if ((Get-ApiFileHash $target) -cne (Get-ApiFileHash $dll)) { throw 'Assassin search copy hash mismatch.' }
    Write-Host "PASS: Assassin search packaged: $target"
}
