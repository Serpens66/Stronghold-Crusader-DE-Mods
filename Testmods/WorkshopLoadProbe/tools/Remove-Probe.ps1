# Removes what the probe put into the game folder: patcher (and an old .old copy), observer plugin
# and configuration. The test payload is installed by APIShared like any Workshop package; remove
# it with Stage-ApiSharedInstall.ps1 -Action Remove (or unsubscribe), never by deleting the folder.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-ProbeGameClosed
$game = Get-ProbeGame
foreach ($relative in @('BepInEx\patchers\WorkshopLoadProbe.Patcher.dll', 'BepInEx\patchers\WorkshopLoadProbe.Patcher.dll.old', 'BepInEx\config\WorkshopLoadProbe.txt')) {
    $path = Join-Path $game $relative
    if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path; Write-Output "Removed $relative" }
}
$observer = Join-Path $game 'BepInEx\plugins\WorkshopLoadProbe_Serp'
if (Test-Path -LiteralPath $observer) {
    $unknown = @(Get-ChildItem -LiteralPath $observer -Recurse -File | Where-Object { $_.Name -notin @('WorkshopLoadProbe.dll', 'info.json') })
    if ($unknown.Count) { throw ('Observer folder contains unknown files; left untouched: ' + ($unknown.Name -join ', ')) }
    Remove-Item -LiteralPath $observer -Recurse -Force
    Write-Output 'Removed BepInEx\plugins\WorkshopLoadProbe_Serp'
}
if (Test-Path -LiteralPath (Join-Path $game ('BepInEx\plugins\' + $ProbePayloadGuid))) {
    Write-Output "Test payload still installed; remove it with Stage-ApiSharedInstall.ps1 -ItemId <id> -Action Remove."
}