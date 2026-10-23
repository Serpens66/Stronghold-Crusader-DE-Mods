[CmdletBinding()]
param(
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [switch]$SkipIntro
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before installing or changing its settings.' }
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/BranchBuild/BranchBuild.Common.ps1')
$output = Join-Path $PSScriptRoot 'BepInEx/plugins/AutoLoadSave_Serp'
$destination = Join-Path $GameDir 'BepInEx/plugins/AutoLoadSave_Serp'
[void][IO.Directory]::CreateDirectory($destination)
foreach ($name in 'AutoLoadSave.dll','AutoLoadSave.pdb','info.json') {
    Copy-Item -LiteralPath (Join-Path $output $name) -Destination $destination
    if ((Get-BranchHash (Join-Path $output $name)) -ne (Get-BranchHash (Join-Path $destination $name))) { throw "Installed hash mismatch: $name" }
}
$config = Join-Path $GameDir 'BepInEx/config/AutoLoadSave_Serp.cfg'
if (-not (Test-Path -LiteralPath $config)) {
    [void][IO.Directory]::CreateDirectory((Split-Path $config))
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AutoLoadSave_Serp.cfg') -Destination $config
}
Write-Host 'PASS: installed files match build; existing config preserved.'
if ($SkipIntro) {
    $settings = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/Firefly Studios/Stronghold Crusader Definitive Edition/settings.cfg'
    $bytes = [IO.File]::ReadAllBytes($settings)
    $encoding = [Text.UTF8Encoding]::new($false, $true)
    $text = $encoding.GetString($bytes)
    $pattern = '(?m)^SkipIntro:(True|False)(?=\r?$)'
    if ([regex]::Matches($text, $pattern).Count -ne 1) { throw 'Expected exactly one existing SkipIntro setting.' }
    $updated = [regex]::Replace($text, $pattern, 'SkipIntro:True')
    # Preserve Vanilla's existing line endings and UTF-8 bytes (including any BOM).
    if ($updated -cne $text) { [IO.File]::WriteAllBytes($settings, $encoding.GetBytes($updated)) }
    if ($encoding.GetString([IO.File]::ReadAllBytes($settings)) -cne $updated) { throw 'SkipIntro write verification failed.' }
    Write-Host 'PASS: SkipIntro:True; all other settings preserved.'
}
