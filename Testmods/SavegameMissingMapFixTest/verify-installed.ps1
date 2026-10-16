$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$installed = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\SavegameMissingMapFixTest_Serp'
foreach ($name in @('SavegameMissingMapFixTest.dll','SavegameMissingMapFixTest.pdb','info.json')) {
    if ((Get-Sha256Hex (Join-Path $installed $name)) -ne (Get-Sha256Hex (Join-Path $PSScriptRoot "BepInEx\plugins\SavegameMissingMapFixTest_Serp\$name"))) { throw "Installed file differs: $name" }
}
'PASS: only SavegameMissingMapFixTest installed; file hashes verified.'
