param([string]$OutputDirectory, [switch]$CopyOnly, [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
# Builds the native anchor core (PathfindingAnchor.cpp) into obj/native-anchor (offline tests and
# packaging). -OutputDirectory additionally copies the DLL there (build.bat: package folder);
# -CopyOnly copies the existing build after verifying it still matches the source. Objects, import
# library and the build proof stay in obj/native-anchor and are never packaged.
# Builds run the production native regression suite unless -SkipTests is requested;
# -CopyOnly never compiles or runs tests.
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $mod '../APIShared/tools/BuildProof.ps1')
$work = Join-Path $mod 'obj/native-anchor'
$source = Join-Path $PSScriptRoot 'PathfindingAnchor.cpp'
$dll = Join-Path $work 'PathfindingAnchor.dll'
$proof = Join-Path $work 'build.json'
$testSource = Join-Path $mod 'tests/Engine/SurfaceCoreTests.cpp'
if ($CopyOnly) {
    if (-not $OutputDirectory) { throw '-CopyOnly requires -OutputDirectory.' }
    if (-not (Test-Path -LiteralPath $dll -PathType Leaf) -or -not (Test-Path -LiteralPath $proof -PathType Leaf)) { throw 'No anchor core build to copy.' }
    $recorded = Get-Content -LiteralPath $proof -Raw | ConvertFrom-Json
    if ($recorded.sourceSha256 -cne (Get-ApiFileHash $source) -or $recorded.dllSha256 -cne (Get-ApiFileHash $dll)) { throw 'anchor core build does not match the current source.' }
} else {
    . (Join-Path $mod '../Shared/Tools/BuildCache/MsvcToolchain.ps1')
    $toolchain = Get-MsvcToolchain
    $compilerRoot = $toolchain.CompilerRoot
    $sdk = $toolchain.Sdk
    $env:INCLUDE = $toolchain.IncludeDirectories -join ';'
    $env:LIB = $toolchain.LibraryDirectories -join ';'
    [IO.Directory]::CreateDirectory($work) | Out-Null
    # A failed rebuild must not leave an earlier success proof reusable by -CopyOnly.
    if (Test-Path -LiteralPath $proof) { Remove-Item -LiteralPath $proof -Force }
    $hash = Get-ApiFileHash $source
    $testHash = Get-ApiFileHash $testSource
    $testStatus = 'skipped'
    & (Join-Path $compilerRoot 'bin/Hostx64/x64/cl.exe') /nologo /std:c++17 /O2 /Ob2 /EHsc /MT /W4 /LD $source "/Fo$work/PathfindingAnchor.obj" /link "/OUT:$dll" "/IMPLIB:$work/PathfindingAnchor.lib"
    if ($LASTEXITCODE) { throw 'anchor core compilation failed.' }
    if (-not $SkipTests) {
        $testExe = Join-Path $work 'SurfaceCoreTests.exe'
        # The suite includes the production core directly; no copied implementation or game process.
        & (Join-Path $compilerRoot 'bin/Hostx64/x64/cl.exe') /nologo /std:c++17 /O2 /Ob2 /EHsc /MT /W4 $testSource "/Fo$work/SurfaceCoreTests.obj" "/Fe$testExe" /link "/IMPLIB:$work/SurfaceCoreTests.lib"
        if ($LASTEXITCODE) { throw 'anchor core regression compilation failed.' }
        & $testExe
        if ($LASTEXITCODE) { throw 'anchor core regression tests failed.' }
        $testStatus = 'passed'
    }
    if ((Get-ApiFileHash $source) -cne $hash -or (Get-ApiFileHash $testSource) -cne $testHash) { throw 'anchor core or regression source changed during compilation/tests.' }
    @{timestampUtc=[DateTime]::UtcNow.ToString('o');sourceSha256=$hash;dllSha256=(Get-ApiFileHash $dll);dll=$dll;compiler=$compilerRoot;windowsSdk=$sdk.Name;scope='Pathfinding native anchor core';tests=@{status=$testStatus;sourceSha256=$testHash}} | ConvertTo-Json | Set-Content -LiteralPath $proof -Encoding UTF8
    Write-Host "PASS: anchor core built: $dll"
}
if ($OutputDirectory) {
    $output = [IO.Path]::GetFullPath($OutputDirectory)
    [IO.Directory]::CreateDirectory($output) | Out-Null
    $target = Join-Path $output 'PathfindingAnchor.dll'
    Copy-Item -LiteralPath $dll -Destination $target -Force
    if ((Get-ApiFileHash $target) -cne (Get-ApiFileHash $dll)) { throw 'anchor core copy hash mismatch.' }
    Write-Host "PASS: anchor core packaged: $target"
}
