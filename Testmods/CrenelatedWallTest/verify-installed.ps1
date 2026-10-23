$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$installed = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\CrenelatedWallTest_Serp'
$local = Join-Path $PSScriptRoot 'BepInEx\plugins\CrenelatedWallTest_Serp'
foreach ($file in Get-ChildItem -LiteralPath $local -Recurse -File) {
    $relative = $file.FullName.Substring($local.Length + 1)
    if ((Get-Sha256Hex (Join-Path $installed $relative)) -cne (Get-Sha256Hex $file.FullName)) { throw "Installed file mismatch: $relative" }
}
$assemblies = @(Get-ChildItem -LiteralPath $installed -Filter '*.dll' -File)
if ($assemblies.Count -ne 1 -or $assemblies[0].Name -ne 'CrenelatedWallTest.dll') { throw 'Consumer must not ship dependency assemblies.' }
Write-Output 'PASS: installed DLL/PDB/manifest/XAML/locales match output; no dependency DLLs shipped.'
