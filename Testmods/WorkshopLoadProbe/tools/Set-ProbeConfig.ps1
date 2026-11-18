# Writes BepInEx/config/WorkshopLoadProbe.txt for the next game start.
#   -QuitAfter   unattended runs: the observer flushes the log and quits after this many seconds
param([ValidateRange(0, 3600)][int]$QuitAfter = 0)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
Assert-ProbeGameClosed
$lines = @(('# Written by Testmods/WorkshopLoadProbe/tools/Set-ProbeConfig.ps1 at ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')), "quitafter=$QuitAfter")
Write-ProbeText (Join-Path (Get-ProbeGame) 'BepInEx\config\WorkshopLoadProbe.txt') $lines
$lines | Select-Object -Skip 1 | ForEach-Object { Write-Output $_ }