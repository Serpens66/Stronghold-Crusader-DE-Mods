$ErrorActionPreference = 'Stop'
$gameDirectory = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$apiFile = Join-Path $gameDirectory 'BepInEx\plugins\APIShared_Serp\APIShared.dll'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
if ([Reflection.AssemblyName]::GetAssemblyName($apiFile).Version.ToString() -cne '0.9.2.0' -or
    (Get-Sha256Hex $apiFile) -cne '4ef262a5f69f16ef6b6e5238606f5013b6400b83d2f8c354e5c53f4efdd52bc1') {
    throw 'The user-selected existing APIShared 0.9.2 SDK changed. Do not rebuild or replace it.'
}
Write-Host 'PASS: existing installed APIShared 0.9.2 identity; no dependency build/copy.'
