# Removes everything the probe put into the game folder: patcher, observer plugin,
# configuration and caches. A local payload copy is removed via Set-LocalPayloadCopy.ps1.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-ProbeGameClosed
& (Join-Path $PSScriptRoot 'Set-LocalPayloadCopy.ps1') -State Absent
$game = Get-ProbeGame
$targets = @('BepInEx\patchers\WorkshopLoadProbe.Patcher.dll', 'BepInEx\config\WorkshopLoadProbe.txt', 'BepInEx\cache\WorkshopLoadProbe.hashes.tsv') +
    @(Get-ChildItem -LiteralPath (Join-Path $game 'BepInEx\cache') -Filter 'wlp_*_typeloader.dat' -File | ForEach-Object { 'BepInEx\cache\' + $_.Name })
foreach ($relative in $targets) {
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
