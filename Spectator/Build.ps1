[CmdletBinding()]
param([Parameter(Mandatory)][string]$ApiSharedDir, [int]$NoInstall = 0)
$ErrorActionPreference = 'Stop'
function FileHash([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create(); $stream = [IO.File]::OpenRead($path)
    try { [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') }
    finally { $stream.Dispose(); $sha.Dispose() }
}
$game = $env:SHCDE_GAME_DIR
if (-not $game) { $game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition' }
$extender = $env:SHCDESE_EXTENDER_DIR
if (-not $extender) { $extender = Join-Path $game 'BepInEx/plugins/000shcdese' }
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
if ($env:SHCDE_MSBUILD) { $msbuild = $env:SHCDE_MSBUILD }
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before building/installing.' }
Write-Host '[Spectator] Checks: own runtime/controllers, optional providers, native contracts, XAML/locales, shared-source boundaries. No unrelated mod tests.'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Spectator validation failed.' }
& $msbuild (Join-Path $PSScriptRoot 'Spectator.csproj') /t:Rebuild /p:Configuration=Release "/p:GameDir=$game" "/p:ExtenderDir=$extender" "/p:ApiSharedDir=$ApiSharedDir" /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Spectator compilation failed.' }
$package = Join-Path $PSScriptRoot 'BepInEx/plugins/Spectator_Serp'
foreach ($file in @('Spectator.dll','info.json','Override/ScriptExtenderUI/SpectatorSettings.xaml','Patches/Assets/GUI/XAML/IngameUIScreens.xaml','Patches/Assets/GUI/XAMLResources/HUD_Main.xaml','Patches/Assets/GUI/XAMLResources/HUD_AlliesPanel.xaml')) {
 if (-not (Test-Path -LiteralPath (Join-Path $package $file))) { throw "Missing package file: $file" }
}
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests/Test-CompiledContracts.ps1') -AssemblyPath (Join-Path $package 'Spectator.dll') -ApiSharedDir $ApiSharedDir -GameDir $game
if ($LASTEXITCODE -ne 0) { throw 'Compiled contracts failed.' }
# This development package is deliberately not added to the main-mod release whitelist.
$proof = [ordered]@{ BuiltUtc=[DateTime]::UtcNow.ToString('o'); Version='0.1.0'; ApiSharedDirectory=$ApiSharedDir; ApiSharedSha256=(FileHash (Join-Path $ApiSharedDir 'APIShared.dll')); SpectatorSha256=(FileHash (Join-Path $package 'Spectator.dll')); Installed=($NoInstall -eq 0) }
[void](New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot '.local') -Force)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot '.local/build.json'), (($proof | ConvertTo-Json) -replace "`r?`n","`r`n"), [Text.UTF8Encoding]::new($false))
if ($NoInstall -eq 0) {
 $destination = Join-Path $game 'BepInEx/plugins/Spectator_Serp'
 [void](New-Item -ItemType Directory -Path $destination -Force)
 Get-ChildItem -LiteralPath $package | Copy-Item -Destination $destination -Recurse -Force
 Write-Host '[Spectator] Built and installed.'
} else { Write-Host '[Spectator] Development package built; installation skipped.' }
