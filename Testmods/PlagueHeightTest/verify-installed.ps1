$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\..\Shared\Tools\Release\Release.Common.ps1')
$installed = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\PlagueHeightTest_Serp'
$local = Join-Path $PSScriptRoot 'BepInEx\plugins\PlagueHeightTest_Serp'
foreach ($name in @('PlagueHeightTest.dll','PlagueHeightTest.pdb','info.json')) {
    if ((Get-Sha256Hex (Join-Path $installed $name)) -cne (Get-Sha256Hex (Join-Path $local $name))) { throw "Installed file mismatch: $name" }
}
Write-Output 'PASS: installed DLL/PDB/metadata match build output.'
