$ErrorActionPreference = 'Stop'
function Get-ProbeHash([string]$path) {
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
    & (Join-Path $root 'Verify-KeepCampfireGroundProbe.ps1')
    $msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    & $msbuild (Join-Path $root 'KeepCampfireGroundProbe.csproj') /nologo /verbosity:minimal /p:Configuration=Debug "/p:ExtenderDir=$extender" "/p:GameDir=$game"
    if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed.' }

    $package = Join-Path $root 'BepInEx\plugins\KeepCampfireGroundProbe_Serp'
    Copy-Item -LiteralPath (Join-Path $root 'info.json') -Destination $package -Force
    $destination = Join-Path $game 'BepInEx\plugins\KeepCampfireGroundProbe_Serp'
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    foreach ($name in @('KeepCampfireGroundProbe.dll', 'KeepCampfireGroundProbe.pdb', 'info.json')) {
        Copy-Item -LiteralPath (Join-Path $package $name) -Destination (Join-Path $destination $name) -Force
        if ((Get-ProbeHash (Join-Path $package $name)) -ne (Get-ProbeHash (Join-Path $destination $name))) {
            throw "Installation hash mismatch: $name"
        }
    }
    Write-Output "KeepCampfireGroundProbe installed in $destination"
    exit 0
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
