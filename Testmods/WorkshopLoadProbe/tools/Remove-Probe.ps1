# Removes everything the probe put into the game folder: patcher (and its .old), observer
# plugin, configuration, the install cache and the test payload the swap installed.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-ProbeGameClosed
$game = Get-ProbeGame
foreach ($relative in @('BepInEx\patchers\WorkshopLoadProbe.Patcher.dll', 'BepInEx\patchers\WorkshopLoadProbe.Patcher.dll.old', 'BepInEx\config\WorkshopLoadProbe.txt')) {
    $path = Join-Path $game $relative
    if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path; Write-Output "Removed $relative" }
}
$cache = Join-Path $game 'BepInEx\cache\WLPI'
if (Test-Path -LiteralPath $cache) { Remove-Item -LiteralPath $cache -Recurse -Force; Write-Output 'Removed BepInEx\cache\WLPI' }
# Only a folder installed by the swap (completion marker, payload GUID) is removed.
$installed = Join-Path $game ('BepInEx\plugins\' + $ProbePayloadGuid)
if (Test-Path -LiteralPath $installed) {
    $info = Join-Path $installed 'info.json'
    if (-not (Test-Path -LiteralPath (Join-Path $installed '.wlp-complete')) -or -not (Test-Path -LiteralPath $info) -or
        (Get-Content -LiteralPath $info -Raw | ConvertFrom-Json).GUID -cne $ProbePayloadGuid) { throw "Not a probe-installed payload, left untouched: $installed" }
    Remove-Item -LiteralPath $installed -Recurse -Force
    Write-Output "Removed installed test payload"
}
$observer = Join-Path $game 'BepInEx\plugins\WorkshopLoadProbe_Serp'
if (Test-Path -LiteralPath $observer) {
    $unknown = @(Get-ChildItem -LiteralPath $observer -Recurse -File | Where-Object { $_.Name -notin @('WorkshopLoadProbe.dll', 'info.json') })
    if ($unknown.Count) { throw ('Observer folder contains unknown files; left untouched: ' + ($unknown.Name -join ', ')) }
    Remove-Item -LiteralPath $observer -Recurse -Force
    Write-Output 'Removed BepInEx\plugins\WorkshopLoadProbe_Serp'
}
