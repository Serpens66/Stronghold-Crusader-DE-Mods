[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
# The probes target audited managed signatures of exactly this Assembly-CSharp.
$managed = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
if ((Get-Sha256Hex -Path $managed) -cne 'bc8b6a395f01d48557db413600c8dd8d1fdfd3abdf97bfbbb68a3c56b04fd789') { Write-Warning 'Assembly-CSharp differs from the audited build; patches validate signatures at runtime and skip on mismatch.' }
$files = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md') })
foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n') { throw "Bare LF: $($file.FullName)" }
    if ($file.Extension -eq '.cs') {
        if ($text -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|LateUpdate|FixedUpdate|StartCoroutine|InvokeRepeating)\s*\(') { throw "Forbidden lifecycle path: $($file.Name)" }
        if ($text -match '\.(?:Unpatch|UnpatchAll|UnpatchSelf)\s*\(|onBeforeRender\s*-=') { throw "Runtime hook teardown: $($file.Name)" }
    }
}
. (Join-Path $workspace 'Shared\Tools\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')
Assert-SERuntimeModPreflight ([pscustomobject]@{ Name='PerfProbe'; Project='Testmods\PerfProbe\PerfProbe.csproj'; Plugin='Testmods\PerfProbe\src\PerfProbePlugin.cs' }) $workspace
Write-Output 'PASS: PerfProbe source checks.'
