# One unattended start: Steam launch, wait for the probe's own quit (config quitafter>0),
# then archive LogOutput.log/Player.log and print the Check-Run summary. No mouse or keyboard input.
param([Parameter(Mandatory)][string]$Label, [int]$TimeoutSeconds = 300, [int]$StallSeconds = 180)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$game = Get-ProbeGame
$config = Join-Path $game 'BepInEx\config\WorkshopLoadProbe.txt'
if (-not (Select-String -LiteralPath $config -Pattern '^quitafter=[1-9]' -Quiet)) { throw 'Set -QuitAfter in Set-ProbeConfig.ps1 for unattended runs.' }
$processName = 'Stronghold Crusader Definitive Edition'
Assert-ProbeGameClosed
while (Get-Process -Name 'UnityCrashHandler64' -ErrorAction SilentlyContinue) { Start-Sleep -Seconds 1 }
Start-Sleep -Seconds 10
$log = Join-Path $game 'BepInEx\LogOutput.log'

$started = Get-Date
Start-Process 'steam://rungameid/3024040'
$process = $null
for ($attempt = 0; -not $process; $attempt++) {
    $deadline = (Get-Date).AddSeconds(45)
    while (-not $process -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 1; $process = Get-Process -Name $processName -ErrorAction SilentlyContinue | Select-Object -First 1 }
    if (-not $process) { if ($attempt -ge 1) { throw 'Game did not start.' }; Start-Process 'steam://rungameid/3024040' }
}
Write-Output ("Started pid={0} at {1:HH:mm:ss}" -f $process.Id, (Get-Date))
$result = 'exited'
$lastSize = -1; $lastGrowth = Get-Date
while (-not $process.HasExited) {
    Start-Sleep -Seconds 2
    $size = if (Test-Path -LiteralPath $log) { (Get-Item -LiteralPath $log).Length } else { 0 }
    if ($size -ne $lastSize) { $lastSize = $size; $lastGrowth = Get-Date }
    if (((Get-Date) - $started).TotalSeconds -gt $TimeoutSeconds) { $result = 'timeout'; break }
    if (((Get-Date) - $lastGrowth).TotalSeconds -gt $StallSeconds) { $result = 'stall'; break }
}
if ($result -ne 'exited') { Stop-Process -Id $process.Id -Force; $process.WaitForExit(30000) | Out-Null }
Write-Output ("Result={0} after {1:n0}s" -f $result, ((Get-Date) - $started).TotalSeconds)

$evidence = Get-ProbeEvidence 'runs'
$stamp = (Get-Date).ToString('yyyyMMdd-HHmmss') + '-' + ($Label -replace '[^A-Za-z0-9_-]', '-')
Copy-Item -LiteralPath $log -Destination (Join-Path $evidence ($stamp + '.LogOutput.log'))
$player = Join-Path $env:USERPROFILE 'AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\Player.log'
if (Test-Path -LiteralPath $player) { Copy-Item -LiteralPath $player -Destination (Join-Path $evidence ($stamp + '.Player.log')) }
& (Join-Path $PSScriptRoot 'Check-Run.ps1') -Label $Label
if ($result -ne 'exited') { throw "Run $Label ended by $result." }
