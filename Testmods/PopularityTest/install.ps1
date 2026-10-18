$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$localPlugin = Join-Path $PSScriptRoot 'BepInEx\plugins\PopularityTest_Serp'
$gamePlugin = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\PopularityTest_Serp'
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Game is running.' }
$null = & (Join-Path $PSScriptRoot 'verify-api.ps1')
foreach ($file in Get-ChildItem -LiteralPath $localPlugin -Recurse -File) {
    $relative = $file.FullName.Substring($localPlugin.Length + 1)
    $destination = Join-Path $gamePlugin $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    if ((Get-Sha256Hex $file.FullName) -cne (Get-Sha256Hex $destination)) { throw "Installation mismatch: $relative" }
}
$null = & (Join-Path $PSScriptRoot 'verify-api.ps1')
Write-Host 'PASS: installed PopularityTest file hashes and unchanged selected dependency.'
