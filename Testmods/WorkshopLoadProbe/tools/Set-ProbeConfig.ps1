# Writes BepInEx/config/WorkshopLoadProbe.txt for the next game start.
# -Item: absolute folders treated like Workshop items (e.g. the "simulated" folder from New-TestItem.ps1).
# -Load: item keys (Workshop ID or folder name) or absolute folders that may actually be loaded in mode "load".
param(
    [Parameter(Mandatory)][ValidateSet('off', 'observe', 'load')][string]$Mode,
    [string[]]$Item = @(),
    [string[]]$Load = @(),
    [ValidateSet('skip', 'bepinex')][string]$Duplicates = 'skip',
    [ValidateSet('on', 'off')][string]$TypeCache = 'on',
    [ValidateSet('on', 'off')][string]$Resolver = 'on',
    [ValidateSet('on', 'off')][string]$HashCache = 'on',
    [switch]$ClearCaches)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-ProbeGameClosed
$game = Get-ProbeGame
$lines = @('# Written by Testmods/WorkshopLoadProbe/tools/Set-ProbeConfig.ps1 at ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'), "mode=$Mode",
    "duplicates=$Duplicates", "typecache=$TypeCache", "resolver=$Resolver", "hashcache=$HashCache")
foreach ($path in $Item) {
    $full = [IO.Path]::GetFullPath($path)
    if (-not (Test-Path -LiteralPath (Join-Path $full 'apishared-workshop.json'))) { throw "Not an item folder: $full" }
    $lines += "item=$full"
}
foreach ($key in $Load) { $lines += "load=$key" }
Write-ProbeText (Join-Path $game 'BepInEx\config\WorkshopLoadProbe.txt') $lines
if ($ClearCaches) {
    $cache = Join-Path $game 'BepInEx\cache'
    foreach ($file in @(Get-ChildItem -LiteralPath $cache -File | Where-Object { $_.Name -eq 'WorkshopLoadProbe.hashes.tsv' -or $_.Name -like 'wlp_*_typeloader.dat' })) {
        Remove-Item -LiteralPath $file.FullName
        Write-Output "Removed cache $($file.Name)"
    }
}
$lines | Select-Object -Skip 1 | ForEach-Object { Write-Output $_ }
