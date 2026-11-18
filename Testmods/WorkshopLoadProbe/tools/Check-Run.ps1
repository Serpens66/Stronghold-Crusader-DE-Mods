# Archives the probe- and installation-relevant lines of the last game start and prints a summary.
# Run after each start: LogOutput.log is overwritten by the next start.
param([Parameter(Mandatory)][string]$Label)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$lines = [IO.File]::ReadAllLines((Join-Path (Get-ProbeGame) 'BepInEx\LogOutput.log'))
$patterns = @(' WLP ', 'WorkshopLoadProbe', 'APIShared.WorkshopInstall', 'Workshop preloader installation', 'Workshop startup result',
    'Chainloader startup complete', 'Could not load \[', 'Skipping \[')
$relevant = @($lines | Where-Object { $line = $_; @($patterns | Where-Object { $line -match $_ }).Count -gt 0 })
$name = (Get-Date).ToString('yyyyMMdd-HHmmss') + '-' + ($Label -replace '[^A-Za-z0-9_-]', '-') + '.log'
$path = Join-Path (Get-ProbeEvidence 'runs') $name
Write-ProbeText $path $relevant
Write-Output "Archived $($relevant.Count) lines: $path"
foreach ($marker in @('INACTIVE', 'CONFIG', 'PAYLOAD_VARIANT', 'PAYLOAD_VARIANT_FAILED', 'QUIT_SCHEDULED', 'QUIT', 'OBSERVER_ERROR', 'ERROR')) {
    $found = @($relevant | Where-Object { $_ -match (' WLP ' + $marker + '( |$)') })
    if ($found.Count -eq 0) { Write-Output ("  {0,-24} -" -f $marker) }
    else { foreach ($line in $found) { Write-Output ("  {0,-24} {1}" -f $marker, ($line -replace '^.* WLP \S+ ?', '')) } }
}
# APIShared's own preloader outcome (installed, removed, recovered, failed) and the startup result.
$relevant | Where-Object { $_ -match 'APIShared.WorkshopInstall|Workshop startup result' } | ForEach-Object { Write-Output "  $_" }
$relevant | Where-Object { $_ -match 'Could not load \[|Skipping \[' } | ForEach-Object { Write-Output "  $_" }