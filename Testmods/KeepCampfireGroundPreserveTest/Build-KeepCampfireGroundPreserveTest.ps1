$ErrorActionPreference = 'Stop'
function Get-PreserveHash([string]$path) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash([IO.File]::ReadAllBytes($path))) }
    finally { $algorithm.Dispose() }
}
try {
    $root = $PSScriptRoot
    $game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
    $extender = Join-Path $game 'BepInEx\plugins\000shcdese'
    if ($env:SHCDESE_EXTENDER_DIR) { $extender = $env:SHCDESE_EXTENDER_DIR }
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) {
        throw 'Game is running. Close it before build/installation.'
    }
    & (Join-Path $root 'Verify-KeepCampfireGroundPreserveTest.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Preflight failed.' }
    $msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    & $msbuild (Join-Path $root 'tests\KeepCampfireGroundPreserveTest.Tests.csproj') /nologo /verbosity:minimal /p:Configuration=Debug "/p:ExtenderDir=$extender" "/p:GameDir=$game"
    if ($LASTEXITCODE -ne 0) { throw 'Native hook test build failed.' }
    & (Join-Path $root 'tests\bin\KeepCampfireGroundPreserveTest.Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Native hook tests failed.' }
    & $msbuild (Join-Path $root 'KeepCampfireGroundPreserveTest.csproj') /nologo /verbosity:minimal /p:Configuration=Debug "/p:ExtenderDir=$extender" "/p:GameDir=$game"
    if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed.' }
    $package = Join-Path $root 'BepInEx\plugins\KeepCampfireGroundPreserveTest_Serp'
    Copy-Item -LiteralPath (Join-Path $root 'info.json') -Destination $package -Force
    $sourceSprites = Join-Path $root 'assets\fire-source'
    $packageSprites = Join-Path $package 'fire-source'
    [IO.Directory]::CreateDirectory($packageSprites) | Out-Null
    foreach ($sprite in Get-ChildItem -LiteralPath $sourceSprites -Filter '*.png' -File) {
        Copy-Item -LiteralPath $sprite.FullName -Destination (Join-Path $packageSprites $sprite.Name) -Force
    }
    $sourceMasks = Join-Path $root 'assets\fire-mask'
    $packageMasks = Join-Path $package 'fire-mask'
    [IO.Directory]::CreateDirectory($packageMasks) | Out-Null
    foreach ($mask in Get-ChildItem -LiteralPath $sourceMasks -Filter '*.png' -File) {
        Copy-Item -LiteralPath $mask.FullName -Destination (Join-Path $packageMasks $mask.Name) -Force
    }
    $destination = Join-Path $game 'BepInEx\plugins\KeepCampfireGroundPreserveTest_Serp'
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    foreach ($name in @('KeepCampfireGroundPreserveTest.dll', 'KeepCampfireGroundPreserveTest.pdb', 'info.json')) {
        Copy-Item -LiteralPath (Join-Path $package $name) -Destination (Join-Path $destination $name) -Force
        if ((Get-PreserveHash (Join-Path $package $name)) -ne (Get-PreserveHash (Join-Path $destination $name))) {
            throw "Installation hash mismatch: $name"
        }
    }
    $destinationSprites = Join-Path $destination 'fire-source'
    [IO.Directory]::CreateDirectory($destinationSprites) | Out-Null
    foreach ($sprite in Get-ChildItem -LiteralPath $packageSprites -Filter '*.png' -File) {
        $target = Join-Path $destinationSprites $sprite.Name
        Copy-Item -LiteralPath $sprite.FullName -Destination $target -Force
        if ((Get-PreserveHash $sprite.FullName) -ne (Get-PreserveHash $target)) {
            throw "Installation hash mismatch: $($sprite.Name)"
        }
    }
    $destinationMasks = Join-Path $destination 'fire-mask'
    [IO.Directory]::CreateDirectory($destinationMasks) | Out-Null
    foreach ($mask in Get-ChildItem -LiteralPath $packageMasks -Filter '*.png' -File) {
        $target = Join-Path $destinationMasks $mask.Name
        Copy-Item -LiteralPath $mask.FullName -Destination $target -Force
        if ((Get-PreserveHash $mask.FullName) -ne (Get-PreserveHash $target)) {
            throw "Installation hash mismatch: $($mask.Name)"
        }
    }
    Write-Output "KeepCampfireGroundPreserveTest installed in $destination"
    exit 0
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
